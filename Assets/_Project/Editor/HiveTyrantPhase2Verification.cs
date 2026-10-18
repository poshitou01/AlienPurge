using System.Collections;
using System.Linq;
using UnityEngine;
using static HiveTyrantPressureVerification;

// Collision fixtures deliberately move actors and restore HP. The separate full-fight
// probes reload the scene and use only native movement, shooting and jumping input.
public static class HiveTyrantPhase2Verification
{
    static IEnumerator Idle(HiveTyrantCombat boss)
    {
        float until = Time.realtimeSinceStartup + 12;
        while ((boss.Motor.HasActionOwner || boss.Wave.IsTravelling || boss.Pressure.Active) && Time.realtimeSinceStartup < until) yield return null;
        Check(!boss.Motor.HasActionOwner && !boss.Wave.IsTravelling && !boss.Pressure.Active, "Cast and all independently travelling hazards finish");
    }
    static IEnumerator EnterPhase2(HiveTyrantCombat boss)
    {
        boss.CaptureEntryLoadout();
        boss.Health.TakeDamage(boss.Health.CurrentHealth - Mathf.FloorToInt(boss.Health.MaxHealth * boss.Config.phase2HealthRatio));
        yield return Wait(boss.Config.phaseTransitionTime + .15f);
        Check(boss.Phase2Entered && boss.Motor.IsPhase2 && boss.PhaseTransitionCount == 1, "Exactly one complete Riftwing reveal at the health threshold");
    }
    public static IEnumerator Verify()
    {
        Random.InitState(2710);
        var boss = Object.FindObjectOfType<HiveTyrantCombat>();
        var player = Object.FindObjectOfType<PlayerHealth>();
        var c = boss.Config;
        Check(boss.AutomaticSkills, "Saved encounter still starts autonomous AI without function keys");
        boss.AutomaticSkills = false; boss.Motor.enabled = false;
        player.GetComponent<PlayerMovement>().SetCanMove(false);
        player.IncreaseMaxHealth(80);
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        boss.CaptureEntryLoadout();
        Check(c.sweepRadius == 7 && c.sweepAngle == 120 && Mathf.Approximately(c.sweepWindup, 1.15f) && Mathf.Approximately(c.sweepTravelSpeed, 8.5f), "P1 sweep geometry, warning and propagation speed preserved");
        Check(HiveTyrantWavePlan.TryCreate(boss, out var p1) && p1.SegmentCount == 3 && p1.GapCount == 2 && p1.volleyCount == 1 && p1.speed == 8 && Mathf.Approximately(p1.warning, 1.55f), "P1 faultline remains three segments, two gaps and one front");
        Check(c.sporeCount == 3 && Mathf.Approximately(c.sporeWarning, 1.3f) && Mathf.Approximately(c.sporeInterval, .75f) && Mathf.Approximately(c.sporeResidueTime, 2.2f), "P1 spore timing and quantity preserved");
        Check(c.summonWaveSize == 7 && c.summonInterval == 12 && Mathf.Approximately(c.summonWarning, 1.4f), "P1 brood composition and timing preserved");
        boss.Pressure.LaunchCrescent(boss.Motor.CombatOrigin, Vector2.down);
        Check(boss.Pressure.Crescents[0].speed == c.sweepTravelSpeed, "P1 crescent captures its original travel speed");
        boss.Pressure.MarkSpore(player.transform.position);
        Check(boss.Pressure.Spores[0].warning == c.sporeWarning && boss.Pressure.Spores[0].residue == c.sporeResidueTime, "P1 shell captures its own detonation and residue times");
        yield return EnterPhase2(boss);
        Check(!boss.Pressure.Active && boss.ActiveWarningCount == 0, "Phase reveal cancels P1 hazards before installing the new pattern");
        Check(c.phase2SweepWindup < c.sweepWindup && c.phase2SlamWindup < c.slamWindup && c.phase2SummonWarning < c.summonWarning, "All P2 attack preparations are faster than their P1 versions");
        Check(c.phase2RecoveryMultiplier < 1 && c.phase2CooldownMultiplier < 1 && c.phase2GlobalRecovery < c.globalRecovery, "P2 reduces recovery, cooldowns and the decision gap");
        Check(c.phase2SweepSpacing > player.GetComponent<PlayerJetJump>().CooldownDuration, "Twin sweeps still allow the native 2.5-second jump cooldown");

        // Native base-speed walking, after a 250ms reaction delay, survives every front.
        var pilot = player.gameObject.AddComponent<HiveTyrantPlaytestPilot>();
        for (int range = 0; range < 3; range++) for (int side = 0; side < 2; side++)
        {
            float distance = new[] { 5f, 9f, 14f }[range];
            Place(player, boss.Motor.CombatOrigin + Vector2.down * distance); player.RestoreHealth(player.MaxHealth);
            player.GetComponent<PlayerMovement>().SetCanMove(true); pilot.Destination = player.transform.position;
            int hp = player.CurrentHealth;
            Vector2 bossBefore = boss.transform.position, playerBefore = player.transform.position;
            float began = Time.time;
            Check(boss.RequestSkill(HiveTyrantSkill.Slam, true), "P2 cascade accepted at " + distance + "m, gap side " + side);
            var plan = boss.Wave.Plan; int gap = side + 1;
            Check(plan.SegmentCount == 5 && plan.GapCount == 4 && plan.volleyCount == 3 && plan.speed > p1.speed && plan.reachableGaps[gap], "Five segments / four physical gaps / three scheduled fronts; inner escape reachable");
            Check(Vector2.Distance(bossBefore, boss.transform.position) < .001f && Vector2.Distance(playerBefore, player.transform.position) < .001f, "Preparing cascade never relocates Boss or player");
            float z = Vector2.Dot((Vector2)player.transform.position - plan.origin, plan.forward);
            Vector2 lockedOrigin = plan.origin, lockedDirection = plan.forward;
            yield return Wait(.25f);
            pilot.Destination = plan.Point(plan.GapCenter(gap), z);
            if (range == 1 && side == 0) yield return Capture("Phase2_Cascade_Warning");
            while (boss.Wave.IsWarning) yield return null;
            Check(Mathf.Abs(Time.time - began - c.phase2SlamWindup) < .12f, "Real P2 faultline release matches the shorter 1.10-second warning");
            Check(plan.origin == lockedOrigin && plan.forward == lockedDirection, "Moving during the tell cannot rotate or translate the locked cascade");
            while (boss.Wave.IsTravelling && boss.Wave.LaunchedFronts < 3) yield return null;
            if (range == 1 && side == 0) yield return Capture("Phase2_Cascade_ThreeFronts");
            yield return Idle(boss);
            Check(boss.Wave.LaunchedFronts == 3 && player.CurrentHealth == hp && !player.IsTemporarilyInvulnerable && pilot.Jumps == 0,
                "All three fronts avoided with base-speed walking and no jump/invulnerability at " + distance + "m");
            Check(Vector2.Distance(player.transform.position, pilot.Destination) < .2f, "Whole player body reaches and remains in the fixed safe corridor");
        }
        Object.Destroy(pilot); player.GetComponent<PlayerMovement>().SetCanMove(false); yield return null;

        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(HiveTyrantWavePlan.TryCreate(boss, out var cascade), "P2 collision fixture uses the real arena plan");
        int health = player.CurrentHealth;
        boss.Wave.Prepare(cascade); boss.Wave.Launch(); boss.Wave.Advance(4);
        Check(player.CurrentHealth == health - c.slamDamage * 3 && boss.Wave.HitCount == 3, "Large 4-second step resolves each of three fronts once, with no tunnelling");
        boss.Wave.Advance(5);
        Check(player.CurrentHealth == health - c.slamDamage * 3, "A completed cascade has no lingering damage"); boss.Wave.Cancel();
        for (int gap = 0; gap < cascade.GapCount; gap++) for (int edge = -1; edge <= 1; edge += 2) for (int outside = 0; outside < 2; outside++)
        {
            player.RestoreHealth(player.MaxHealth); health = player.CurrentHealth;
            float radius = HiveTyrantGeometry.PlayerWidth(player, cascade.right) * .5f;
            float x = cascade.GapCenter(gap) + edge * (cascade.gap * .5f - radius + (outside == 0 ? -.04f : .04f));
            Place(player, cascade.Point(x, 5)); boss.Wave.Prepare(cascade); boss.Wave.Launch(); boss.Wave.Advance(4);
            Check(player.CurrentHealth == health - (outside == 0 ? 0 : c.slamDamage * 3), "P2 collider-sized gap " + gap + " edge " + edge + (outside == 0 ? " remains safe through every front" : " exposed body is hit once by each front"));
            boss.Wave.Cancel();
        }
        player.RestoreHealth(player.MaxHealth); health = player.CurrentHealth;
        Place(player, cascade.Point(0, .4f)); boss.Wave.Prepare(cascade); boss.Wave.Launch();
        Place(player, cascade.Point(cascade.GapCenter(2), .4f)); boss.Wave.Advance(cascade.volleyInterval + .05f);
        Check(boss.Wave.HitCount == 1 && player.CurrentHealth == health - c.slamDamage, "A later front born during a long step never hits a player's earlier position"); boss.Wave.Cancel();
        Place(player, cascade.Point(0, 5));
        var ability = player.GetComponent<PlayerAbilityState>();
        Check(ability.TryEnterAbility(PlayerAbilityMode.Airborne), "Existing airborne state accepts collision fixture");
        health = player.CurrentHealth; boss.Wave.Prepare(cascade); boss.Wave.Launch(); boss.Wave.Advance(4);
        Check(player.CurrentHealth == health, "Every P2 ground front respects the existing airborne exclusion");
        ability.ExitAbility(PlayerAbilityMode.Airborne); boss.Wave.Cancel();

        var wall = new GameObject("Phase2_Verification_Wall"); wall.layer = LayerMask.NameToLayer("Environment");
        wall.transform.position = boss.Motor.CombatOrigin + Vector2.down * 9;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(40, .4f); Physics2D.SyncTransforms();
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        Check(HiveTyrantWavePlan.TryCreate(boss, out var clipped), "P2 finds a reachable escape in front of a wall");
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 12); health = player.CurrentHealth;
        boss.Wave.Prepare(clipped); boss.Wave.Launch(); boss.Wave.Advance(4);
        Check(clipped.MaximumLength < 6 && player.CurrentHealth == health, "All five segments and all three fronts stop at the environment wall"); boss.Wave.Cancel();
        wall.transform.position = boss.Motor.CombatOrigin + Vector2.down * 3.5f; Physics2D.SyncTransforms();
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 5);
        Check(!HiveTyrantWavePlan.TryCreate(boss, out _), "Sealed launch/escape lanes reject a P2 cascade");
        wall.SetActive(false); Object.Destroy(wall); Physics2D.SyncTransforms();

        Place(player, boss.Motor.CombatOrigin + Vector2.right * 10);
        float start = Time.time, first = -1, second = -1; int before = boss.SkillReleases[0];
        Check(boss.RequestSkill(HiveTyrantSkill.Sweep, true), "P2 twin-rift sweep accepted");
        while (Time.time < start + 7 && second < 0)
        {
            if (boss.SkillReleases[0] > before && first < 0) first = Time.time;
            if (boss.SkillReleases[0] > before + 1) second = Time.time;
            yield return null;
        }
        Check(first > 0 && Mathf.Abs(first - start - c.phase2SweepWindup) < .12f, "First P2 sweep has the actual 0.85-second tell");
        Check(second - first >= c.phase2SweepSpacing - .06f, "Faster twin sweep retains separately warned releases and jump recovery");
        Check(boss.Pressure.Crescents.Any(a => a.active && a.enraged && a.speed == c.phase2SweepTravelSpeed), "P2 crescent propagates at 10.5 with distinct energy coloring");
        yield return Capture("Phase2_Twin_Rift"); yield return Idle(boss);

        Place(player, boss.Motor.CombatOrigin + Vector2.down * 8); health = player.CurrentHealth;
        Check(boss.RequestSkill(HiveTyrantSkill.SporeBarrage, true), "P2 paired spore attack accepted");
        yield return Wait(.45f);
        var shells = boss.Pressure.Spores;
        Check(shells.Count(s => s.active) == 2 && Mathf.Abs(shells[0].age - shells[1].age) < .001f, "First salvo locks two simultaneous circles");
        Check(Mathf.Abs(Vector2.Distance(shells[0].position, shells[1].position) - c.phase2SporeFlankOffset) < .01f, "One flanking circle leaves the opposite side open");
        Check(player.CurrentHealth == health && shells[0].warning == c.phase2SporeWarning && shells[0].residue == c.phase2SporeResidueTime, "P2 warning does no early damage and captures shorter residue timing");
        Vector2 firstMark = shells[0].position;
        yield return Wait(.6f); yield return Capture("Phase2_Spore_Crossfire");
        yield return Wait(.45f);
        Check(shells.Count(s => s.active) == 6 && boss.SkillReleases[3] == 6, "Three paired salvos produce six independently telegraphed shells");
        Place(player, firstMark + Vector2.down * 3);
        Check(shells[0].position == firstMark, "Paired spore markers never follow the player after locking");
        Check(!boss.RequestSkill(HiveTyrantSkill.Slam, true), "Crossfire cannot overlap a new faultline and seal its safe lanes");
        yield return Idle(boss);

        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        start = Time.time;
        Check(boss.RequestSkill(HiveTyrantSkill.Summon, true), "P2 brood rally accepted");
        int ranged = 0, heavy = 0, fast = 0;
        for (int i = 0; i < boss.PendingSpawnCount; i++)
        {
            var type = boss.Summoning.Pending(i).choice.type;
            if (type == EnemyType.Ranged) ranged++; if (type == EnemyType.Heavy) heavy++; if (type == EnemyType.Fast) fast++;
        }
        Check(boss.PendingSpawnCount >= 8 && ranged >= 2 && heavy >= 1 && fast >= 1, "P2 legal rally includes a heavy anchor, fast flanker and two ranged threats");
        while (boss.TotalSummoned == 0 && Time.time - start < 3) yield return null;
        Check(boss.TotalSummoned > 0 && Mathf.Abs(Time.time - start - c.phase2SummonWarning) < .12f, "Rally releases on its shorter 1.05-second animation tell");
        boss.Summoning.Cleanup(); yield return Idle(boss);

        int meshes = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("GroundWave_"));
        for (int i = 0; i < 15; i++) { boss.Wave.Prepare(cascade); boss.Wave.Launch(); boss.Wave.Advance(.6f); boss.Wave.Cancel(); }
        Check(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("GroundWave_")) == meshes, "Repeated cascades reuse the same five meshes without creating projectile objects");
        boss.Wave.Prepare(cascade); boss.Wave.Launch(); boss.Wave.Advance(.1f); health = player.CurrentHealth;
        boss.enabled = false; boss.Wave.Advance(4);
        Check(boss.Wave.Plan == null && !boss.Wave.IsTravelling && player.CurrentHealth == health && boss.ActiveWarningCount == 0, "Disable cancels travelling and not-yet-born fronts without invisible damage");
        boss.enabled = true; boss.RestartEncounter(); boss.Motor.enabled = false;
        Place(player, boss.Motor.CombatOrigin + Vector2.down * 10);
        Check(!boss.Phase2Entered && HiveTyrantWavePlan.TryCreate(boss, out var reset) && reset.SegmentCount == 3 && reset.volleyCount == 1, "Restart restores P1 without leaking any P2 pattern parameters");

        boss.AutomaticSkills = true;
        yield return EnterPhase2(boss);
        float deadline = Time.time + 12;
        while (boss.SkillReleases[2] == 0 && Time.time < deadline) yield return null;
        Check(boss.SkillReleases[2] == 1 && boss.SkillReleases[0] == 0 && boss.SkillReleases[1] == 0, "Automatic P2 opener first rallies the brood");
        while ((boss.Wave.Plan == null || !boss.Wave.Plan.enraged) && Time.time < deadline) yield return null;
        Check(boss.Wave.Plan != null && boss.Wave.Plan.enraged && boss.Wave.IsWarning, "Automatic opener follows with the new cascade, without debug keys");
        boss.Health.TakeDamage(boss.Health.CurrentHealth); health = player.CurrentHealth; yield return Wait(.15f);
        Check(boss.Wave.Plan == null && !boss.Pressure.Active && boss.ActiveWarningCount == 0 && boss.LivingSummons == 0, "P2 death cancels the full queue, markers and owned summons");
        yield return Wait(.5f);
        Check(player.CurrentHealth == health, "No post-death delayed front can damage the player");
    }
}
