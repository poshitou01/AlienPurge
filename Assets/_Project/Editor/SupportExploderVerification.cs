using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs the real scene, pool, physics, A*, native abilities and reward systems in Editor Play Mode.
[InitializeOnLoad]
public static partial class SupportExploderVerification
{
    const string Key = "SupportExploder.Verification";
    const string Folder = "Docs/Enemies/CoreEnemies/PolishV2/SupportExploder";
    static readonly Stack<IEnumerator> jobs = new Stack<IEnumerator>();
    static readonly List<string> report = new List<string>(), errors = new List<string>();
    static double started;
    static bool done;
    static EnemyAbilityTestLab lab;
    static PlayerHealth player;
    static SupportExploderVerification()
    {
        EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick;
        Application.logMessageReceived += (message, trace, type) =>
        { if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors.Add(message + "\n" + trace); };
    }
    [MenuItem("AlienPurge/Support + Exploder/4 Run Play Mode Verification")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        SupportExploderSetup.Configure(); SupportExploderSetup.Configure(); // Real idempotence exercise.
        Directory.CreateDirectory(Folder);
        Object.FindObjectOfType<EnemyAbilityTestLab>().spawnOnStart = false; // Unsaved test fixture only.
        SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".Exit", 1); EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report.Clear(); errors.Clear(); jobs.Clear(); done = false; started = EditorApplication.timeSinceStartup;
            report.Add("Unity " + Application.unityVersion + " / real Editor Play Mode / " + SystemInfo.graphicsDeviceType);
            jobs.Push(Suite());
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorSceneManager.OpenScene(SupportExploderSetup.ScenePath);
            CoreEnemyTestProfileIsolation.Restore();
            if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Key + ".Exit", 1));
        }
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || done || jobs.Count == 0) return;
        try
        {
            if (errors.Count > 0) throw new InvalidOperationException("Runtime error: " + errors[0]);
            if (EditorApplication.timeSinceStartup - started > 300) throw new TimeoutException("Verification exceeded five minutes");
            var job = jobs.Peek();
            if (!job.MoveNext()) { jobs.Pop(); if (jobs.Count == 0) Finish(true); }
            else if (job.Current is IEnumerator child) jobs.Push(child);
        }
        catch (Exception exception) { report.Add("FAIL: " + exception); Finish(false); }
    }
    static void Finish(bool pass)
    {
        done = true; jobs.Clear(); Time.captureDeltaTime=0; Time.timeScale=1; if (errors.Count > 0) { pass = false; report.AddRange(errors); }
        report.Add(pass ? "RESULT: PASS" : "RESULT: FAIL");
        File.WriteAllLines(Folder + "/PlayMode_Result.txt", report);
        SessionState.SetInt(Key + ".Exit", pass ? 0 : 1); EditorApplication.isPlaying = false;
    }
    static void Check(bool condition, string label)
    {
        report.Add((condition ? "PASS: " : "FAIL: ") + label);
        if (!condition) throw new InvalidOperationException(label);
    }
    static IEnumerator Wait(float duration)
    {
        if(Time.captureDeltaTime>0 && Time.timeScale>0)
        { float end=Time.time+duration*Time.timeScale; while(Time.time<end) yield return null; }
        else { float end=Time.realtimeSinceStartup+duration; while(Time.realtimeSinceStartup<end) yield return null; }
    }
    static void Place(Component target, Vector2 point)
    {
        target.transform.position = point; if (target.TryGetComponent<Rigidbody2D>(out var rb)) { rb.position = point; rb.velocity = Vector2.zero; }
        Physics2D.SyncTransforms();
    }
    static PooledEnemy Spawn(EnemyType type, Vector2 position, bool freeze = true)
    {
        var enemy = lab.Spawn(type, position); Check(enemy != null && !enemy.IsReturned, "Pool spawn " + type);
        if (freeze) enemy.GetComponent<EnemyMovement>().enabled = false;
        foreach (var component in enemy.GetComponents<MonoBehaviour>()) if (component.GetType().Name.EndsWith("Attack")) component.enabled = false;
        return enemy;
    }
    static IEnumerator Suite()
    {
        yield return Wait(.8f); lab = Object.FindObjectOfType<EnemyAbilityTestLab>(); player = Object.FindObjectOfType<PlayerHealth>();
        lab.RecycleAll(); player.GetComponent<PlayerShooting>().enabled = false;
        Check(Object.FindObjectsOfType<EnemyPool>().Length == 1 && Object.FindObjectsOfType<GameManager>().Length == 1 && Object.FindObjectsOfType<EnemyAbilityTestLab>().Length == 1, "Repeated configure leaves one of each manager/lab");
        Check(EnemyPool.Instance.GetTotalCount(EnemyType.Support) == 2 && EnemyPool.Instance.GetTotalCount(EnemyType.Exploder) == 3, "Explicit test-pool capacities (2 Support / 3 Exploder)");
        yield return SupportCases();
        yield return ExploderCases();
        yield return MovementAndWeapons();
        yield return AuthoredArtCases();
        yield return BossWhitelist();
        lab.RecycleAll(); Place(player, new Vector2(0, -6));
        Spawn(EnemyType.Support, new Vector2(-7, 5), false); Spawn(EnemyType.Normal, new Vector2(-6, 3), false);
        Spawn(EnemyType.Exploder, new Vector2(9, 5), false); yield return Wait(.5f);
        EnemyAbilityTestLab.Restart(); yield return Wait(1);
        player = Object.FindObjectOfType<PlayerHealth>(); lab = Object.FindObjectOfType<EnemyAbilityTestLab>();
        Check(player.CurrentHealth == player.MaxHealth && GameManager.Instance.KillCount == 0, "Restart creates a fresh run and clears kill state");
        Check(Object.FindObjectsOfType<EnemyPool>().Length == 1 && Object.FindObjectsOfType<SupportEnemyAbility>().Length == 1 && Object.FindObjectsOfType<ExploderEnemyAbility>().Length == 1, "Restart loads the authored encounter without duplicated managers/enemies");
        Check(Object.FindObjectsOfType<ExploderEnemyAbility>().All(e => e.ExplosionCount == 0 && e.State != ExploderState.Warning), "Restart clears countdowns and blast history");
        Capture("TestScene_Overview");
        Check(errors.Count == 0, "No Unity errors or exceptions");
    }
    static IEnumerator SupportCases()
    {
        Place(player, new Vector2(10, -7));
        var first = Spawn(EnemyType.Support, new Vector2(-9, 5)); var support = first.GetComponent<SupportEnemyAbility>();
        var allies = new List<PooledEnemy>();
        for (int i = 0; i < 8; i++) allies.Add(Spawn(EnemyType.Normal, new Vector2(-10 + (i % 4), 3 + (i / 4))));
        yield return Wait(.3f);
        Check(support.TargetCount == 8, "Support hard target cap is eight");
        var affected = allies.Where(a => a.GetComponent<EnemyContactDamage>().Damage == 2).ToArray();
        Check(affected.Length == 8, "Eight base-damage-one allies receive +1");
        Capture("Support_Links_Authored_Art");
        var second = Spawn(EnemyType.Support, new Vector2(-9, 5)); yield return Wait(.3f);
        Check(allies.All(a => a.GetComponent<EnemyContactDamage>().Damage <= 2), "Two Supports never stack past strongest +1");
        Check(first.GetComponent<EnemyContactDamage>().Damage == 1 && second.GetComponent<EnemyContactDamage>().Damage == 1, "Supports cannot buff one another");
        var target = affected[0]; int baseDamage = target.GetComponent<EnemyDefinition>().FinalContactDamage;
        Place(target, new Vector2(12, 8));
        Check(target.GetComponent<EnemyContactDamage>().Damage == baseDamage, "Out-of-range damage revocation is immediate (before refresh)");
        Place(target, new Vector2(-9, 4)); yield return Wait(.3f);
        var buff = target.GetComponent<EnemySupportBuff>(); uint generation = buff.Generation;
        target.ReturnToPool(); var reused = Spawn(EnemyType.Normal, new Vector2(12, 8));
        Check(reused == target && buff.Generation != generation && reused.GetComponent<EnemyContactDamage>().Damage == baseDamage, "Same pooled target has no stale buff on reuse");
        int kills = GameManager.Instance.KillCount, drops = ExperienceOrbPool.Instance.ActiveCount;
        first.GetComponent<EnemyHealth>().TakeDamage(9999);
        Check(support.TargetCount == 0 && GameManager.Instance.KillCount == kills + 1 && ExperienceOrbPool.Instance.ActiveCount == drops + 1, "Support death immediately revokes its leases and rewards once");
        Check(allies.Any(a => a.GetComponent<EnemyContactDamage>().Damage == 2), "Other living Support remains effective after one source dies");
        second.ReturnToPool(); Check(allies.All(a => a.GetComponent<EnemyContactDamage>().Damage == 1), "Source recycle removes remaining bonus synchronously");
        first.GetComponent<EnemyHealth>().TakeDamage(9999); yield return Wait(.3f);
        Check(GameManager.Instance.KillCount == kills + 1 && ExperienceOrbPool.Instance.ActiveCount == drops + 1, "Repeated lethal hit does not duplicate rewards");
        Check(AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/_Project/Data/Enemies/Enemy_Normal_Data.asset").DamageMultiplier == 1, "Shared EnemyData unchanged by buffs");
        lab.RecycleAll();
        // All original role components consume the same effective damage accessor.
        support = Spawn(EnemyType.Support, new Vector2(10, 6)).GetComponent<SupportEnemyAbility>();
        Check(support.TargetCount == 0, "Reused Support starts without old leases");
        var fast = Spawn(EnemyType.Fast, new Vector2(9, 6)); var heavy = Spawn(EnemyType.Heavy, new Vector2(10, 5)); var ranged = Spawn(EnemyType.Ranged, new Vector2(11, 6));
        yield return Wait(.3f);
        foreach (var ally in new[] { fast, heavy, ranged }) Check(ally.GetComponent<EnemyContactDamage>().Damage == ally.GetComponent<EnemyDefinition>().FinalContactDamage + 1, "Damage integration for " + ally.Type);
        Place(fast, new Vector2(-12, 6)); Place(heavy, new Vector2(-10, 6)); Place(ranged, new Vector2(-8, 6));
        Place(player, support.transform.position); int hp = player.CurrentHealth; yield return Wait(.15f);
        Check(player.CurrentHealth == hp, "Support has no contact damage while touched");
        lab.RecycleAll(); Place(player, new Vector2(0, -7));
    }
    static IEnumerator ExploderCases()
    {
        // Hold two objects so each return is followed by guaranteed reuse of the same third object.
        var reserveA = Spawn(EnemyType.Exploder, new Vector2(14, 8)); reserveA.GetComponent<ExploderEnemyAbility>().enabled = false;
        var reserveB = Spawn(EnemyType.Exploder, new Vector2(13, 8)); reserveB.GetComponent<ExploderEnemyAbility>().enabled = false;
        Vector2 origin = new Vector2(-9, -6); Place(player, origin);
        var enemy = Spawn(EnemyType.Exploder, origin); var ability = enemy.GetComponent<ExploderEnemyAbility>(); int hp = player.CurrentHealth;
        yield return Wait(1.7f);
        Check(ability.State == ExploderState.Safe && ability.ExplosionCount == 0 && player.CurrentHealth == hp, "Unsafe spawn on player cannot arm, detonate or deal contact damage");
        Place(player, origin + Vector2.right * 6); yield return Wait(.1f); Place(player, origin + Vector2.right * 2); yield return Wait(.15f);
        Check(ability.State == ExploderState.Warning && ability.ExplosionCount == 0, "Grace and observed safe separation precede a full warning");
        int kills = GameManager.Instance.KillCount, drops = ExperienceOrbPool.Instance.ActiveCount;
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
        Check(GameManager.Instance.KillCount == kills + 1 && ExperienceOrbPool.Instance.ActiveCount == drops + 1, "Killed Exploder rewards exactly once");
        Place(player, origin + Vector2.right * 7); yield return Wait(1.7f);
        Check(ability.ExplosionCount == 0 && enemy.IsReturned && player.CurrentHealth == hp, "Killing during warning cancels active explosion and returns to pool");

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        Check(ability.ExplosionCount == 0 && ability.WarningProgress < .2f, "Same instance resets old explosion count and timer");
        enemy.GetComponent<EnemyHealth>().TakeDamage(1); Check(ability.State == ExploderState.Warning, "Nonlethal damage does not skip or reset warning");
        Vector2 locked = ability.BlastCenter; Place(player, origin + Vector2.up * 5); yield return Wait(.2f);
        Check(ability.BlastCenter == locked && (Vector2)enemy.transform.position == locked && !enemy.GetComponent<EnemyMovement>().enabled, "Warning locks position and stops chase/knockback movement");
        var presentation = enemy.GetComponent<EnemyAbilityPresentation>();
        Check(presentation.WarningVisible && Mathf.Approximately(presentation.DisplayedRadius, ability.BlastRadius), "Displayed world radius equals damage query radius");
        Capture("Exploder_Warning_Authored_Art"); kills = GameManager.Instance.KillCount; drops = ExperienceOrbPool.Instance.ActiveCount; hp = player.CurrentHealth;
        yield return Wait(2.4f);
        Check(ability.ExplosionCount == 1 && player.CurrentHealth == hp && enemy.IsReturned, "Moving out avoids one committed blast and automatic pool return");
        Check(GameManager.Instance.KillCount == kills + 1 && ExperienceOrbPool.Instance.ActiveCount == drops + 1, "Active self-destruction grants exactly one native kill / XP orb");
        Check(!presentation.WarningVisible, "Return hides all warning geometry");

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        kills = GameManager.Instance.KillCount; drops = ExperienceOrbPool.Instance.ActiveCount; hp = player.CurrentHealth;
        enemy.ReturnToPool(); yield return Wait(1.7f);
        Check(ability.ExplosionCount == 0 && player.CurrentHealth == hp && GameManager.Instance.KillCount == kills && ExperienceOrbPool.Instance.ActiveCount == drops, "Direct recycle during warning causes no blast or reward");

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        var extraCollider = player.gameObject.AddComponent<CircleCollider2D>(); extraCollider.radius = .15f;
        var friendly = Spawn(EnemyType.Normal, origin + Vector2.up); int friendlyHP = friendly.GetComponent<EnemyHealth>().CurrentHealth;
        hp = player.CurrentHealth; int damage = Mathf.CeilToInt(enemy.GetComponent<EnemyContactDamage>().Damage * ability.Config.damageMultiplier);
        yield return Wait(1.9f);
        Check(player.CurrentHealth == hp - damage && ability.DamageApplications == 1 && ability.ExplosionCount == 1, "Multiple player colliders resolve one blast hit");
        Check(friendly.GetComponent<EnemyHealth>().CurrentHealth == friendlyHP, "Players-only faction excludes ordinary enemies");
        yield return Wait(.5f); Check(player.CurrentHealth == hp - damage, "Blast cannot damage a second time on later frames");
        Object.Destroy(extraCollider); friendly.ReturnToPool(); player.RestoreHealth(player.MaxHealth);

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        float extent = player.GetComponents<Collider2D>().Where(c => c.enabled && !c.isTrigger).Max(c => c.bounds.extents.x);
        Place(player, origin + Vector2.right * (ability.BlastRadius + extent + .08f)); hp = player.CurrentHealth;
        yield return Wait(1.9f); Check(player.CurrentHealth == hp, "Full player collider beyond ring edge receives no damage");

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        Place(player, origin + Vector2.right * (ability.BlastRadius + extent - .08f)); hp = player.CurrentHealth;
        yield return Wait(1.9f); Check(player.CurrentHealth == hp - damage, "Collider overlapping ring edge receives exactly the configured hit"); player.RestoreHealth(player.MaxHealth);

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        while (ability.WarningProgress < .85f) yield return null;
        typeof(PlayerJetJump).GetMethod("TryStartJump", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player.GetComponent<PlayerJetJump>(), null);
        Check(player.GetComponent<PlayerJetJump>().IsActive, "Native Jet Jump starts through its normal gate"); hp = player.CurrentHealth;
        yield return Wait(.8f); Check(player.CurrentHealth == hp && ability.ExplosionCount == 1, "Native airborne state avoids ground blast");

        yield return ArmedCase(enemy, origin); enemy = currentExploder; ability = enemy.GetComponent<ExploderEnemyAbility>();
        while (ability.WarningProgress < .88f) yield return null;
        // Equivalent to the last native movement input being rightward, away from this blast.
        // No invulnerability or Dash distance override: the authored Dash has no i-frames.
        typeof(PlayerMovement).GetField("lastNonZeroMoveDirection", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player.GetComponent<PlayerMovement>(), Vector2.right);
        typeof(PlayerDash).GetMethod("TryStartDash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player.GetComponent<PlayerDash>(), null);
        Check(player.GetComponent<PlayerDash>().IsActive, "Native Dash starts through its normal gate"); hp = player.CurrentHealth;
        yield return Wait(.8f); Check(player.CurrentHealth == hp, "Native Dash / movement avoids warning expiration");
        lab.RecycleAll();
    }
    static PooledEnemy currentExploder;
    static IEnumerator ArmedCase(PooledEnemy expected, Vector2 origin)
    {
        while (!expected.IsReturned) yield return null; // Authored death/rupture finishes before pool reuse.
        Place(player, origin + Vector2.right * 7); currentExploder = Spawn(EnemyType.Exploder, origin);
        Check(currentExploder == expected, "Guaranteed same-instance Exploder pool reuse");
        var ability = currentExploder.GetComponent<ExploderEnemyAbility>();
        Check(ability.ExplosionCount == 0 && ability.State == ExploderState.Safe, "Fresh spawn clears state immediately");
        yield return Wait(1.6f); Place(player, origin + Vector2.right * 2); yield return Wait(.1f);
        Check(ability.State == ExploderState.Warning, "Proximity enters visible warning after arming");
    }
    static IEnumerator MovementAndWeapons()
    {
        Place(player, new Vector2(0, -6));
        var support = Spawn(EnemyType.Support, new Vector2(-7, 5), false);
        var ally = Spawn(EnemyType.Normal, new Vector2(-5, 2), false);
        var exploder = Spawn(EnemyType.Exploder, new Vector2(9, 5), false);
        Vector2 s = support.transform.position, e = exploder.transform.position; bool supportPath = false, exploderPath = false;
        var supportFrames = new HashSet<Sprite>(); var exploderFrames = new HashSet<Sprite>();
        float until = Time.time + 4;
        while (Time.time < until)
        {
            supportPath |= support.GetComponent<EnemyPathNavigator>().HasValidPath;
            exploderPath |= exploder.GetComponent<EnemyPathNavigator>().HasValidPath;
            supportFrames.Add(support.GetComponent<SpriteRenderer>().sprite); exploderFrames.Add(exploder.GetComponent<SpriteRenderer>().sprite);
            yield return null;
        }
        Check(supportPath && exploderPath, "Both specialists obtain native A* paths around cover");
        Check(Vector2.Distance(s, support.transform.position) > .5f && Vector2.Distance(e, exploder.transform.position) > 1, "Both specialists physically move using native motor");
        Check(supportFrames.Count > 2 && exploderFrames.Count > 2 && support.transform.rotation == Quaternion.identity && exploder.transform.rotation == Quaternion.identity, "Native Animator swaps independent authored frames without rotating the body");
        Check(Physics2D.OverlapCircle(support.transform.position, .3f, 1 << 7) == null && Physics2D.OverlapCircle(exploder.transform.position, .3f, 1 << 7) == null, "Native collision guard keeps enemies out of environment");
        lab.RecycleAll(); Place(player, new Vector2(0, -6));
        foreach (var type in new[] { EnemyType.Support, EnemyType.Exploder })
        {
            var enemy = Spawn(type, new Vector2(0, -2)); int hp = enemy.GetComponent<EnemyHealth>().CurrentHealth;
            var weapon = player.GetComponent<WeaponManager>(); weapon.AimAt(enemy.transform.position);
            Check(weapon.TryFire(), "Native weapon fires toward " + type); yield return Wait(.7f);
            Check(enemy.GetComponent<EnemyHealth>().CurrentHealth < hp, "Real pooled bullet / physics damages " + type);
            enemy.GetComponent<EnemyHealth>().TakeDamage(9999); yield return Wait(.4f);
            var xp = player.GetComponent<PlayerExperience>(); int oldXp = xp.CurrentExperience, level = xp.CurrentLevel;
            Place(player, enemy.transform.position); yield return Wait(.4f);
            Check(xp.CurrentExperience > oldXp || xp.CurrentLevel > level, "Native XP pickup collects reward for " + type);
            Place(player, new Vector2(0, -6));
        }
    }
    static IEnumerator BossWhitelist()
    {
        lab.RecycleAll(); Place(player, new Vector2(0, -6));
        var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HiveTyrantCombatSetup.BossPath)).GetComponent<HiveTyrantCombat>();
        Place(boss, new Vector2(0, 3)); boss.AutomaticSkills = false; boss.Motor.SetTarget(player.transform); boss.Motor.enabled = false;
        yield return Wait(.1f); boss.CaptureEntryLoadout(); boss.Summoning.Prepare();
        Check(EnemyPool.Instance.TryGetConfiguredPrefab(EnemyType.Support, out _) && EnemyPool.Instance.TryGetConfiguredPrefab(EnemyType.Exploder, out _), "Boss probe uses a pool with both new types registered");
        Check(boss.PendingSpawnCount > 0, "Boss prepares an actual native summon plan");
        for (int i = 0; i < boss.PendingSpawnCount; i++) Check((int)boss.Summoning.Pending(i).choice.type <= 3, "Boss plan retains original four-type whitelist");
        boss.Summoning.Release();
        Check(EnemyPool.Instance.GetActiveCount(EnemyType.Support) == 0 && EnemyPool.Instance.GetActiveCount(EnemyType.Exploder) == 0 && boss.LivingSummons > 0, "Boss releases original summons only with expanded registration");
        Check(!SupportEnemyAbility.IsEligible(boss.GetComponent<PooledEnemy>()), "Boss is never a Support recipient");
        boss.Summoning.Cleanup(); Object.Destroy(boss.gameObject); yield return Wait(.2f);
    }
    static void Capture(string name)
    {
        var camera = Camera.main; var target = new RenderTexture(1600, 900, 24); var before = camera.targetTexture; var active = RenderTexture.active;
        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1600, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); image.Apply();
        File.WriteAllBytes(Folder + "/" + name + ".png", image.EncodeToPNG());
        camera.targetTexture = before; RenderTexture.active = active; Object.Destroy(image); Object.Destroy(target);
    }
}
