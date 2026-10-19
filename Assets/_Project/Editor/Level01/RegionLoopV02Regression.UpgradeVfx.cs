using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run Integrated Upgrade Effects Checks")]
        public static void RunIntegratedUpgradeVfx()
        {
            IntegratedUpgradeVfx.Install();
            var bytes = File.ReadAllBytes(IntegratedWorldInstaller.Target);
            IntegratedUpgradeVfx.Install();
            if (!bytes.SequenceEqual(File.ReadAllBytes(IntegratedWorldInstaller.Target))) throw new InvalidOperationException("Special upgrade installer is not idempotent.");
            StartIntegrated(false, false, false, true);
        }

        static ProjectileModifierSnapshot NativeUpgradeSnapshot()
        {
            var weapon = loop.world.player.GetComponentInChildren<WeaponController>();
            Check(weapon != null, "Native weapon exists", false);
            return (ProjectileModifierSnapshot)typeof(WeaponController).GetMethod("BuildProjectileModifierSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(weapon, null);
        }
        static Bullet UpgradeBullet(Vector3 position, ProjectileModifierSnapshot snapshot)
        {
            var bullet = Object.FindObjectOfType<BulletPool>().GetBullet(position, Quaternion.identity);
            bullet.Initialize(Vector2.right, 15, 10, 1, 2, snapshot); return bullet;
        }
        static void ClearUpgradeEffects()
        {
            foreach (var item in Object.FindObjectsOfType<ExplosionEffect>()) ExplosionEffectPool.Instance.ReturnEffect(item);
            foreach (var item in Object.FindObjectsOfType<ChainLightningEffect>()) ChainLightningEffectPool.Instance.ReturnEffect(item);
            foreach (var item in Object.FindObjectsOfType<Bullet>()) item.ReturnToPool();
        }
        static IEnumerator IntegratedUpgradeVfxChecks()
        {
            yield return null; accelerate = false; Time.timeScale = 1;
            loop = Object.FindObjectOfType<RegionLoopController>();
            yield return Until(() => loop.Regions[0].initialized, "Upgrade VFX fixture initialized");
            Check(Application.persistentDataPath.Contains("AlienPurge_IntegratedV01_Regression"), "VFX tests use an isolated save profile");
            loop.regionModeEnabled = false;
            foreach (var region in loop.Regions) foreach (var ticket in region.enemies.ToArray()) if (ticket.lease != null) ticket.lease.Release();
            var player = loop.world.player.gameObject;
            player.GetComponent<PlayerMovement>().UseExternalInput = true;
            player.GetComponent<PlayerMovement>().SetMovementInput(Vector2.zero);
            var origin = AbilityClearSpace(loop.Terminal("QuarantineCommons").transform.position);
            Move(origin - Vector3.right * 3, true); yield return Seconds(.5f);
            var mods = player.GetComponent<PlayerWeaponModifiers>();
            var explosionPool = ExplosionEffectPool.Instance; var chainPool = ChainLightningEffectPool.Instance;
            Check(explosionPool.GetComponentsInChildren<ExplosionEffect>(true).Length == 24 && chainPool.GetComponentsInChildren<ChainLightningEffect>(true).Length == 64,
                "Existing pools prewarm exactly 24 explosion / 64 lightning effects without a parallel spawner");
            Check(Object.FindObjectsOfType<UpgradeVfxAudio>().Length == 1 && Read<AudioSource[]>(Object.FindObjectOfType<UpgradeVfxAudio>(), "voices").Length == 4,
                "One audio host has a fixed four-voice budget");
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Effects/ChainLightningEffect.prefab").GetComponent<UpgradeEffectVisual>() == null,
                "Legacy lightning prefab remains opt-out");

            for (int level = 1; level <= 3; level++)
            {
                mods.ResetMechanicUpgrades(); for (int n = 0; n < level; n++) mods.ApplyExplosiveUpgrade();
                var snapshot = NativeUpgradeSnapshot();
                Check(snapshot.Explosive && !snapshot.ChainLightning && Mathf.Approximately(snapshot.ExplosionRadius, new[] { 1.4f, 1.7f, 2f }[level - 1]),
                    "Explosion Lv" + level + " preserves the native radius and upgrade snapshot");
                var direct = AbilityEnemy(EnemyType.Normal, origin, 100);
                var secondary = AbilityEnemy(EnemyType.Normal, origin + Vector3.up * .9f, 100);
                var outside = AbilityEnemy(EnemyType.Normal, origin + Vector3.up * 3.3f, 100);
                var extraCollider = direct.gameObject.AddComponent<CircleCollider2D>(); extraCollider.radius = .1f; extraCollider.isTrigger = true;
                var bullet = UpgradeBullet(origin - Vector3.right, snapshot);
                yield return Until(() => secondary.GetComponent<EnemyHealth>().CurrentHealth < 100, "Native explosion collision", 4);
                int damage = Mathf.Max(1, Mathf.RoundToInt(10 * snapshot.ExplosionDamageMultiplier));
                Check(direct.GetComponent<EnemyHealth>().CurrentHealth == 90 - damage && secondary.GetComponent<EnemyHealth>().CurrentHealth == 100 - damage && outside.GetComponent<EnemyHealth>().CurrentHealth == 100,
                    "Explosion Lv" + level + " applies one correct area hit per enemy, excludes outside targets, and deduplicates colliders");
                var effect = Object.FindObjectsOfType<UpgradeEffectVisual>().Single(e => e.Radius > 0);
                Check(effect.Visible && Mathf.Approximately(effect.Radius, snapshot.ExplosionRadius) && !effect.GetComponent<LineRenderer>().enabled,
                    "Explosion Lv" + level + " uses the enhanced mesh at its real damage radius, without the old ring doubling it");
                Object.Destroy(extraCollider); direct.ReturnToPool(); secondary.ReturnToPool(); outside.ReturnToPool();
                yield return Seconds(.5f);
                Check(Read<int>(explosionPool, "activeCount") == 0, "Explosion Lv" + level + " fully returns to its original pool");
            }
            for (int level = 1; level <= 3; level++)
            {
                mods.ResetMechanicUpgrades(); for (int n = 0; n < level; n++) mods.ApplyChainLightningUpgrade();
                var snapshot = NativeUpgradeSnapshot();
                Check(snapshot.ChainCount == level && snapshot.ChainRange == 3.5f && snapshot.ChainDamageMultiplier == .6f,
                    "Lightning Lv" + level + " preserves hop count, range and damage");
                var enemies = new PooledEnemy[4];
                for (int i = 0; i < 4; i++) enemies[i] = AbilityEnemy(EnemyType.Normal, origin + Vector3.up * i * 2.4f, 100);
                var bullet = UpgradeBullet(origin - Vector3.right, snapshot);
                yield return Until(() => enemies[0].GetComponent<EnemyHealth>().CurrentHealth < 100, "Native chain collision", 4);
                Check(enemies[0].GetComponent<EnemyHealth>().CurrentHealth == 90 && enemies.Skip(1).Select((e, i) => e.GetComponent<EnemyHealth>().CurrentHealth == (i < level ? 94 : 100)).All(v => v),
                    "Lightning Lv" + level + " hits exactly the native unique targets with no visual damage or repeated hit");
                var effects = Object.FindObjectsOfType<UpgradeEffectVisual>().Where(e => e.Radius == 0).OrderBy(e => e.Hop).ToArray();
                Check(effects.Length == level && effects.Select((e, i) => e.Hop == i && (e.EndPoint - e.StartPoint).magnitude <= snapshot.ChainRange).All(v => v),
                    "Lightning Lv" + level + " presents actual hit endpoints and ordered hops");
                foreach (var enemy in enemies) enemy.ReturnToPool();
                yield return Seconds(.4f);
                Check(Read<int>(chainPool, "activeCount") == 0, "Lightning Lv" + level + " fully returns to its original pool");
            }

            // The VFX capacity never gates damage, death credit or XP.
            ClearUpgradeEffects();
            var ex = new ExplosionEffect[24]; var ch = new ChainLightningEffect[64];
            for (int i = 0; i < ex.Length; i++) ex[i] = explosionPool.GetEffect(origin, 2);
            for (int i = 0; i < ch.Length; i++) ch[i] = chainPool.GetEffect(origin, origin + Vector3.up, i % 3);
            Check(ex.All(e => e != null) && ch.All(e => e != null) && explosionPool.GetEffect(origin, 2) == null && chainPool.GetEffect(origin, origin + Vector3.up) == null,
                "Full simultaneous VFX budget refuses expansion at 24 / 64");
            mods.ResetMechanicUpgrades(); mods.ApplyExplosiveUpgrade(); mods.ApplyChainLightningUpgrade();
            var victim = AbilityEnemy(EnemyType.Normal, origin, 10);
            var splash = AbilityEnemy(EnemyType.Normal, origin + Vector3.up, 4);
            var chained = AbilityEnemy(EnemyType.Normal, origin + Vector3.up * 2.8f, 6);
            int kills = GameManager.Instance.KillCount, xp = Object.FindObjectsOfType<ExperienceOrb>().Length;
            var killBullet = UpgradeBullet(origin, NativeUpgradeSnapshot());
            Call(killBullet, "OnTriggerEnter2D", victim.GetComponent<Collider2D>());
            Check(victim.GetComponent<EnemyHealth>().IsDead && splash.GetComponent<EnemyHealth>().IsDead && chained.GetComponent<EnemyHealth>().IsDead && GameManager.Instance.KillCount == kills + 3,
                "A saturated visual pool still resolves direct / explosion / chain kills exactly once");
            Check(Object.FindObjectsOfType<ExperienceOrb>().Length == xp + 3, "Saturated VFX preserves one native XP drop for each of the three kills");
            foreach (var e in ex) explosionPool.ReturnEffect(e); foreach (var e in ch) chainPool.ReturnEffect(e);
            Check(Read<int>(explosionPool, "activeCount") == 0 && Read<int>(chainPool, "activeCount") == 0,
                "Explicit return cancels all enhanced meshes and coroutines without further damage");
            yield return Seconds(.5f);
            Check(GameManager.Instance.KillCount == kills + 3, "Recycled effects never resolve a second death");

            var reusable = explosionPool.GetEffect(origin, .65f);
            Check(reusable.GetComponent<UpgradeEffectVisual>().Radius == .65f, "Reusing a large explosion as a child explosion resets its scale");
            var flame = Read<SpriteRenderer>(reusable.GetComponent<UpgradeEffectVisual>(), "flame"); var firstSprite = flame.sprite;
            yield return Seconds(.06f);
            Check(flame.enabled && flame.sprite != firstSprite && reusable.GetComponent<UpgradeEffectVisual>().profile.explosionFrames.Length == 8,
                "The pooled explosion actually advances through the existing eight formal animation frames");
            var pause = Object.FindObjectOfType<PauseMenuController>(); pause.PauseGame(); yield return null; yield return null;
            var visual = reusable.GetComponent<UpgradeEffectVisual>(); float elapsed = visual.Elapsed;
            Check(!visual.Visible && Read<AudioSource[]>(Object.FindObjectOfType<UpgradeVfxAudio>(), "voices").All(v => !v.isPlaying),
                "Pause hides the visual and stops special upgrade voices");
            yield return null; yield return null;
            Check(visual.Elapsed == elapsed, "Effect lifetime does not advance during Pause");
            pause.ResumeGame(); yield return null; yield return null;
            Check(visual.Visible, "Resume continues the remaining effect instead of spawning it again");
            yield return Seconds(.5f);
            var audio = AudioManager.Instance; float master = audio.MasterVolume, sfx = audio.SfxVolume;
            // Use the same public settings API as the native menu.
            audio.SetSfxVolume(0); explosionPool.GetEffect(origin, 2); chainPool.GetEffect(origin, origin + Vector3.up);
            yield return null;
            Check(Read<AudioSource[]>(Object.FindObjectOfType<UpgradeVfxAudio>(), "voices").All(v => v.volume == 0), "Special upgrade sound obeys the native SFX volume");
            audio.SetSfxVolume(sfx); audio.SetMasterVolume(master); ClearUpgradeEffects();

            yield return UpgradePreview(origin, false);
            yield return UpgradePreview(origin, true);
            yield return Seconds(1);
            // Measure uncaptured repeated overlap after prewarm; no screenshots or scene edits during sampling.
            int objects = explosionPool.GetComponentsInChildren<Transform>(true).Length + chainPool.GetComponentsInChildren<Transform>(true).Length;
            var frameTimes = new System.Collections.Generic.List<double>();
            double previous = UnityEditor.EditorApplication.timeSinceStartup;
            float until = Time.time + 4, next = 0;
            while (Time.time < until)
            {
                if (Time.time >= next)
                {
                    next = Time.time + .05f;
                    for (int i = 0; i < 5; i++) explosionPool.GetEffect(origin + Vector3.right * (i - 2), 1.4f);
                    for (int i = 0; i < 12; i++) chainPool.GetEffect(origin, origin + new Vector3((i % 4) - 1.5f, 2 + (i % 3)), i % 3);
                }
                yield return null;
                double now = UnityEditor.EditorApplication.timeSinceStartup; frameTimes.Add((now - previous) * 1000); previous = now;
            }
            Check(objects == explosionPool.GetComponentsInChildren<Transform>(true).Length + chainPool.GetComponentsInChildren<Transform>(true).Length,
                "Repeated simultaneous effects never grow the warmed pool hierarchy");
            yield return Seconds(.6f);
            Check(Read<int>(explosionPool, "activeCount") == 0 && Read<int>(chainPool, "activeCount") == 0, "All effects drain after the stress burst");
            frameTimes.Sort(); results.Add("MEASURE Editor VFX stress frames=" + frameTimes.Count + " p50_ms=" + frameTimes[frameTimes.Count / 2].ToString("F2") + " p95_ms=" + frameTimes[(int)(frameTimes.Count * .95)].ToString("F2"));
            explosionPool.GetEffect(origin, 2); chainPool.GetEffect(origin, origin + Vector3.up);
            invulnerable = false;
            var health = player.GetComponent<PlayerHealth>(); health.CancelTemporaryInvulnerability(); health.TakeDamage(int.MaxValue);
            yield return null; yield return null; yield return null;
            Check(GameManager.Instance.IsGameOver && Read<int>(explosionPool, "activeCount") == 0 && Read<int>(chainPool, "activeCount") == 0,
                "Native player death returns active special effects even while game time is stopped");
            Check(Read<AudioSource[]>(Object.FindObjectOfType<UpgradeVfxAudio>(), "voices").All(v => !v.isPlaying), "Death stops all special upgrade voices");
            yield return Fresh(); accelerate = false; Time.timeScale = 1;
            Check(Read<int>(ExplosionEffectPool.Instance, "activeCount") == 0 && Read<int>(ChainLightningEffectPool.Instance, "activeCount") == 0 && Object.FindObjectsOfType<UpgradeVfxAudio>().Length == 1,
                "Native Restart clears active effects and installs exactly one fresh audio host");
            Check(player == null && explosionPool == null && chainPool == null, "Restart destroys the prior scene's visual owners");
            Check(loop.world.player.GetComponent<PlayerWeaponModifiers>().ExplosiveLevel == 0 && loop.world.player.GetComponent<PlayerWeaponModifiers>().ChainLightningLevel == 0,
                "Restart leaves no special upgrade level from the fixture");
        }

        static IEnumerator UpgradePreview(Vector3 origin, bool lightning)
        {
            ClearUpgradeEffects();
            var points = lightning ? new[] { new Vector3(-2.2f, -.4f), new Vector3(-.8f, 1), new Vector3(.9f, -.25f), new Vector3(2.35f, .9f) } :
                new[] { Vector3.zero, new Vector3(.9f, .5f), new Vector3(-.9f, -.5f), new Vector3(.6f, -1.05f) };
            var enemies = points.Select(p => AbilityEnemy(EnemyType.Normal, origin + p, 100)).ToArray();
            string kind = lightning ? "Lightning" : "Explosion", folder = "Logs/UpgradeVfxFrames/" + kind;
            Directory.CreateDirectory(folder); int previousRate = Time.captureFramerate; Time.captureFramerate = 60;
            for (int frame = 0; frame < 32; frame++)
            {
                if (frame == 3)
                {
                    if (lightning) for (int i = 0; i < 3; i++) ChainLightningEffectPool.Instance.GetEffect(origin + points[i], origin + points[i + 1], i);
                    else ExplosionEffectPool.Instance.GetEffect(origin, 2);
                }
                IntegratedFeedbackAudit.Render(origin + Vector3.up * .1f, 2.6f, folder + "/" + frame.ToString("D3") + ".png", 768, 480);
                if (frame == 8) File.Copy(folder + "/" + frame.ToString("D3") + ".png", IntegratedUpgradeVfx.ReportFolder + "/" + kind + ".png", true);
                yield return null;
            }
            Time.captureFramerate = previousRate; foreach (var enemy in enemies) enemy.ReturnToPool(); ClearUpgradeEffects();
            Check(true, kind + " preview consists of 32 real Unity camera frames at 60fps in the integrated map");
        }
    }
}
