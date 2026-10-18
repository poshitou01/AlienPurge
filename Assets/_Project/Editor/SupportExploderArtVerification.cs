using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SupportExploderVerification
{
    static readonly string[] ArtDirections = { "D", "DR", "R", "UR", "U", "UL", "L", "DL" };
    static IEnumerator AuthoredArtCases()
    {
        lab.RecycleAll(); Place(player, new Vector2(0, -8));
        foreach (var type in new[] { EnemyType.Support, EnemyType.Exploder })
        {
            var enemy = Spawn(type, new Vector2(-8, 3));
            if (enemy.TryGetComponent<SupportEnemyAbility>(out var support)) support.enabled = false;
            if (enemy.TryGetComponent<ExploderEnemyAbility>(out var exploder)) exploder.enabled = false;
            var animator = enemy.GetComponent<Animator>(); var driver = enemy.GetComponent<SupportExploderAnimation>(); var body = enemy.GetComponent<SpriteRenderer>();
            Check(!enemy.GetComponent<EnemyAbilityPresentation>().TemporaryArt, type + " has no placeholder organ overlay");
            enemy.GetComponent<EnemyHealth>().TakeDamage(1);
            Check(body.color.r > 1 && body.color.g > 1 && body.color.b > 1, type + " native hit feedback visibly brightens own Sprite material");
            yield return Wait(.12f);
            Check(body.color == Color.white, type + " native hit flash restores authored tint");
            Vector3 scale = enemy.transform.localScale; float radius = enemy.GetComponent<CircleCollider2D>().radius;
            for (int d = 0; d < 8; d++)
            {
                float angle = (d - 2) * 45 * Mathf.Deg2Rad;
                Place(enemy, (Vector2)enemy.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * .15f);
                yield return Wait(.05f);
                Check(animator.GetInteger("Direction") == d && driver.CurrentDirection == d && driver.CurrentPose == SupportExploderAnimation.Pose.Move,
                    type + " native movement selects authored " + ArtDirections[d]);
            }
            for (int d = 0; d < 8; d++)
            {
                driver.Preview(SupportExploderAnimation.Pose.Move, d);
                var frames = new HashSet<Sprite>(); float end = Time.time + 1.35f;
                while (Time.time < end) { frames.Add(body.sprite); yield return null; }
                Check(frames.Count == 8 && frames.All(s => AssetDatabase.GetAssetPath(s).Contains("/" + type + "/Move/" + ArtDirections[d] + "/")),
                    type + " " + ArtDirections[d] + " plays all 8 unique own walk frames");
                var poses = type == EnemyType.Support ? new[] { SupportExploderAnimation.Pose.Idle, SupportExploderAnimation.Pose.SupportStart,
                    SupportExploderAnimation.Pose.SupportLoop, SupportExploderAnimation.Pose.SupportMove, SupportExploderAnimation.Pose.SupportEnd, SupportExploderAnimation.Pose.Death }
                    : new[] { SupportExploderAnimation.Pose.Idle, SupportExploderAnimation.Pose.Warning, SupportExploderAnimation.Pose.Death, SupportExploderAnimation.Pose.Burst };
                foreach (var pose in poses)
                {
                    driver.Preview(pose, d); yield return Wait(.04f);
                    Check(body.sprite != null && body.sprite.name.Contains("_" + ArtDirections[d] + "_") && !body.sprite.name.Contains("Swarmer"), type + " " + ArtDirections[d] + " " + pose + " resolves a valid own sprite");
                }
            }
            Check(enemy.transform.localScale == scale && enemy.transform.rotation == Quaternion.identity && enemy.GetComponent<CircleCollider2D>().radius == radius,
                type + " all visual states preserve collision root scale / rotation / collider");
            lab.RecycleAll();
            yield return ReuseArtCase(type);
        }
        yield return SupportVisualStates();
        yield return WarningVisualTiming();
        lab.EnterPreview(); yield return Wait(.15f);
        Check(EnemyPool.Instance.GetActiveCount(EnemyType.Support) == 1 && EnemyPool.Instance.GetActiveCount(EnemyType.Exploder) == 1, "Animation preview uses the real bounded enemy pool");
        Check(Object.FindObjectsOfType<EnemyAbilityPresentation>().All(v => v.DisplayedRadius == 0 && !v.ExplosionVisible), "Disabled abilities leave no range or explosion in body preview");
        CaptureClose("Authored_Characters_Close", Vector2.zero, 3.5f);
        lab.RecycleAll(); lab.SpawnEncounter(); yield return Wait(.1f);
        Check(Object.FindObjectsOfType<SupportEnemyAbility>().All(a => a.enabled) && Object.FindObjectsOfType<ExploderEnemyAbility>().All(a => a.enabled), "Leaving preview restores real abilities on reused pooled objects");
        lab.RecycleAll();
    }
    static IEnumerator ReuseArtCase(EnemyType type)
    {
        Place(player, new Vector2(0, -8));
        var reserved = new List<PooledEnemy>();
        while (EnemyPool.Instance.GetAvailableCount(type) > 1)
        {
            var reserve = Spawn(type, new Vector2(13, 8));
            if (reserve.TryGetComponent<SupportEnemyAbility>(out var s)) s.enabled = false;
            if (reserve.TryGetComponent<ExploderEnemyAbility>(out var e)) e.enabled = false;
            reserved.Add(reserve);
        }
        var enemy = Spawn(type, new Vector2(-8, 3));
        for (int cycle = 0; cycle < 3; cycle++)
        {
            var driver = enemy.GetComponent<SupportExploderAnimation>(); var body = enemy.GetComponent<SpriteRenderer>();
            int kills = GameManager.Instance.KillCount;
            enemy.GetComponent<EnemyHealth>().TakeDamage(9999);
            Check(driver.CurrentPose == SupportExploderAnimation.Pose.Death, type + " hit death selects collapse, never rupture");
            yield return Wait(.43f);
            Check(body.color.a < 1 && !enemy.IsReturned && !enemy.GetComponent<EnemyAbilityPresentation>().ExplosionVisible, type + " collapse fades while alive in pool accounting, without explosion VFX");
            if (cycle == 0)
            {
                float elapsed = driver.DeathElapsed, alpha = body.color.a;
                Time.timeScale = 0; yield return Wait(.2f);
                Check(Mathf.Approximately(elapsed, driver.DeathElapsed) && Mathf.Approximately(alpha, body.color.a) && !enemy.IsReturned, type + " pause freezes collapse and pool-return timer together");
                Time.timeScale = 1;
            }
            enemy.ReturnToPool();
            var next = Spawn(type, new Vector2(-8, 3));
            Check(next == enemy && !driver.IsPreview && driver.CurrentPose == SupportExploderAnimation.Pose.Idle && driver.CurrentDirection == 1 && body.color == Color.white && enemy.GetComponent<Animator>().speed == 1,
                type + " same-instance reuse clears pose/frame/direction/tint/fade/speed cycle " + cycle);
            yield return Wait(.7f);
            Check(!enemy.GetComponent<EnemyHealth>().IsDead && !enemy.IsReturned && GameManager.Instance.KillCount == kills + 1 && !enemy.GetComponent<EnemyAbilityPresentation>().ExplosionVisible,
                type + " old death callback cannot affect next life cycle " + cycle);
        }
        lab.RecycleAll();
    }
    static IEnumerator SupportVisualStates()
    {
        Place(player, new Vector2(0, -8));
        var enemy = Spawn(EnemyType.Support, new Vector2(-8, 3)); var ally = Spawn(EnemyType.Normal, new Vector2(-7, 3));
        var ability = enemy.GetComponent<SupportEnemyAbility>(); var driver = enemy.GetComponent<SupportExploderAnimation>();
        yield return Wait(.1f); Check(ability.TargetCount == 1 && driver.CurrentPose == SupportExploderAnimation.Pose.SupportStart, "Real aura acquisition opens authored organs");
        yield return Wait(.4f); Check(driver.CurrentPose == SupportExploderAnimation.Pose.SupportLoop, "Stationary aura sustains authored pulse without changing gameplay");
        Place(enemy, (Vector2)enemy.transform.position + Vector2.right * .15f); yield return Wait(.04f);
        Check(driver.CurrentPose == SupportExploderAnimation.Pose.SupportMove, "Moving aura uses active four-foot gait instead of sliding cast pose");
        yield return Wait(.15f); ally.ReturnToPool(); yield return Wait(.04f);
        Check(ability.TargetCount == 0 && driver.CurrentPose == SupportExploderAnimation.Pose.SupportEnd, "Lost target immediately clears lease and plays closing organs");
        yield return Wait(.4f); Check(driver.CurrentPose == SupportExploderAnimation.Pose.Idle, "Closing returns to idle");
        lab.RecycleAll();
    }
    static IEnumerator WarningVisualTiming()
    {
        Place(player, new Vector2(0, -8));
        var enemy = Spawn(EnemyType.Exploder, Vector2.zero); var ability = enemy.GetComponent<ExploderEnemyAbility>();
        var driver = enemy.GetComponent<SupportExploderAnimation>(); var visual = enemy.GetComponent<EnemyAbilityPresentation>();
        yield return Wait(1.65f); Place(player, Vector2.right * 2); yield return Wait(.1f);
        Check(ability.State == ExploderState.Warning && driver.CurrentPose == SupportExploderAnimation.Pose.Warning && visual.WarningVisible && !visual.ExplosionVisible, "Real warning selects charge art and visible damage circle only");
        float progress = ability.WarningProgress; var sprite = enemy.GetComponent<SpriteRenderer>().sprite;
        Time.timeScale = 0; yield return Wait(.35f);
        Check(Mathf.Approximately(progress, ability.WarningProgress) && enemy.GetComponent<SpriteRenderer>().sprite == sprite && ability.ExplosionCount == 0, "Pause freezes warning countdown and authored charge frame together");
        Time.timeScale = .5f; yield return Wait(.4f);
        Check(ability.WarningProgress > progress + .08f && ability.WarningProgress < progress + .2f, "Half speed advances warning and sampled charge by scaled time");
        Time.timeScale = 1;
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999); yield return Wait(.05f);
        Check(driver.CurrentPose == SupportExploderAnimation.Pose.Death && !visual.WarningVisible && !visual.ExplosionVisible && ability.ExplosionCount == 0, "Killing a charging enemy closes warning and plays nonexplosive collapse");
        yield return Wait(.6f); lab.RecycleAll();
        Place(player, new Vector2(0, -8)); enemy = Spawn(EnemyType.Exploder, Vector2.zero);
        ability = enemy.GetComponent<ExploderEnemyAbility>(); visual = enemy.GetComponent<EnemyAbilityPresentation>(); driver = enemy.GetComponent<SupportExploderAnimation>();
        yield return Wait(1.65f); Place(player, Vector2.right * 2); yield return Wait(.06f); Place(player, new Vector2(0, -8));
        int kills = GameManager.Instance.KillCount;
        Directory.CreateDirectory("Logs/ArtCapture");
        var camera = Camera.main; Vector3 position = camera.transform.position; float zoom = camera.orthographicSize;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographicSize = 4;
        var times = new List<string>(); bool sawRupture = false;
        for (int i = 0; i < 29; i++)
        {
            times.Add(Time.time.ToString("F5", System.Globalization.CultureInfo.InvariantCulture));
            if (!sawRupture && ability.State == ExploderState.Detonated)
            {
                sawRupture = true;
                Check(driver.CurrentPose == SupportExploderAnimation.Pose.Burst && visual.ExplosionVisible, "Committed detonation plays directional rupture and bounded radial VFX together");
            }
            CaptureRuntimeFrame("Logs/ArtCapture/Exploder_" + i.ToString("00") + ".png"); yield return Wait(1f / 12);
        }
        File.WriteAllLines("Logs/ArtCapture/Exploder_times.txt", times);
        camera.transform.position = position; camera.orthographicSize = zoom;
        Check(sawRupture && ability.ExplosionCount == 1 && enemy.IsReturned && GameManager.Instance.KillCount == kills + 1 && !visual.ExplosionVisible && !visual.WarningVisible,
            "Recorded real warning/rupture/disappearance grants one reward and clears every effect");
        lab.RecycleAll();
    }
    static void CaptureClose(string name, Vector2 center, float zoom)
    {
        var camera = Camera.main; var position = camera.transform.position; float oldZoom = camera.orthographicSize;
        camera.transform.position = new Vector3(center.x, center.y, -10); camera.orthographicSize = zoom;
        Capture(name); camera.transform.position = position; camera.orthographicSize = oldZoom;
    }
    static void CaptureRuntimeFrame(string path)
    {
        var camera = Camera.main; var previous = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(960, 540, 24); var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
        try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
        finally { camera.targetTexture = previous; RenderTexture.active = active; Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target); }
    }
}
