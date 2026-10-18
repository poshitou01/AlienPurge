using System;
using System.Collections;
using System.Linq;
using AlienPurge.Level01.RegionLoop;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlienPurge.Level01.Editor
{
    public static partial class RegionLoopV02Regression
    {
        [MenuItem("AlienPurge/Gameplay/Level01/Run V02 Impact Checks")]
        public static void RunImpact() => Start(false, false, true, false, false, false, true);

        static void CheckImpactAudio(AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            Check(clip.GetData(samples, 0), "PCM impact clip is readable and preloaded: " + clip.name, false);
            float peak = samples.Max(s => Mathf.Abs(s));
            double rms = Math.Sqrt(samples.Average(s => (double)s * s));
            Check(clip.channels == 1 && clip.frequency == 48000 && peak > .7f && peak < .84f && rms > .025 && rms < .28 &&
                Mathf.Abs(samples[0]) < .0001f && Mathf.Abs(samples[samples.Length - 1]) < .0001f,
                "Original impact clip has headroom, bounded energy and click-free ends: " + clip.name);
            results.Add("MEASURE " + clip.name + " seconds=" + clip.length + " peak=" + peak + " RMS=" + rms.ToString("F4"));
        }
        static ProjectileModifierSnapshot ImpactModifiers(bool explosion, bool chain)
        {
            object boxed = ProjectileModifierSnapshot.Default;
            WriteField(boxed, "explosive", explosion); WriteField(boxed, "explosionRadius", 2f); WriteField(boxed, "explosionDamageMultiplier", .5f);
            WriteField(boxed, "chainLightning", chain); WriteField(boxed, "chainCount", 2); WriteField(boxed, "chainRange", 2f);
            WriteField(boxed, "chainDamageMultiplier", .5f); WriteField(boxed, "maxChainTriggerCount", 1);
            return (ProjectileModifierSnapshot)boxed;
        }
        static IEnumerator ImpactChecks()
        {
            yield return null; accelerate = invulnerable = false; Time.timeScale = 1;
            loop = Object.FindObjectOfType<RegionLoopController>();
            yield return Until(() => loop.Regions[0].initialized, "Impact fixture initialized");
            Check(Application.persistentDataPath.Contains("AlienPurge_RegionLoopV02_Regression"), "Impact checks use an isolated save profile");
            loop.regionModeEnabled = false;
            foreach (var region in loop.Regions) foreach (var ticket in region.enemies.ToArray()) if (ticket.lease != null) ticket.lease.Release();
            var player = loop.world.player.gameObject;
            player.GetComponent<PlayerShooting>().enabled = false;
            var motor = player.GetComponent<PlayerMovement>(); motor.UseExternalInput = true; motor.SetMovementInput(Vector2.zero);
            var origin = AbilityClearSpace(loop.Terminal("QuarantineCommons").transform.position); Move(origin, true);
            yield return Seconds(.6f);
            var impact = CombatImpactFeedback.Instance;
            Check(impact != null && impact.CanPresent && impact.confirmation != null && !impact.confirmation.raycastTarget,
                "Scene feedback is installed once and its confirmation cannot block aiming");
            foreach (var clip in new[] { impact.hitSound, impact.strongSound, impact.killSound, impact.heavyKillSound }) CheckImpactAudio(clip);
            Check(impact.GetComponentsInChildren<AudioSource>().Length == 6, "Audio has a fixed six-voice budget with dedicated kill voices");

            var normal = AbilityEnemy(EnemyType.Normal, origin + Vector3.right * 2.4f, 100);
            var health = normal.GetComponent<EnemyHealth>(); var root = normal.transform.position;
            var weapons = player.GetComponent<WeaponManager>();
            foreach (var old in Object.FindObjectsOfType<Bullet>()) old.ReturnToPool();
            int hits = impact.PresentedHits, oldEffects = Object.FindObjectsOfType<HitEffect>().Length;
            weapons.AimAt(normal.GetComponent<Collider2D>().bounds.center);
            Check(weapons.TryFire(), "Native equipped weapon fires into the impact fixture");
            yield return Until(() => health.CurrentHealth < 100, "Native bullet hits", 4);
            Check(impact.PresentedHits == hits + 1 && impact.LastTier == CombatImpactTier.Hit, "Native projectile damage emits exactly one ordinary hit confirmation");
            var reaction = normal.GetComponent<EnemyImpactReaction>();
            results.Add("MEASURE first reaction=" + (reaction == null ? "missing" : "fraction=" + reaction.HealthFraction + " bar=" + reaction.HealthBarVisible + " flash=" + reaction.FlashVisible) +
                " hp=" + health.CurrentHealth + "/" + health.MaxHealth + " renderers=" + string.Join(",", normal.GetComponentsInChildren<SpriteRenderer>().Select(s => s.name + ":" + s.enabled + ":" + (s.sprite == null ? "null" : s.sprite.name))));
            Check(reaction != null && Mathf.Abs(reaction.HealthFraction - (float)health.CurrentHealth / health.MaxHealth) < .00001f && reaction.HealthBarVisible,
                "Enemy health loss display reads actual health rather than a second health state");
            Check(Object.FindObjectsOfType<HitEffect>().Length == oldEffects, "Enhanced projectile impact replaces the old duplicate hit sprite");
            Check(normal.transform.position == root && !normal.GetComponent<EnemyMovement>().IsKnockedBack,
                "Visual flinch does not translate physics or interrupt enemy attacks");
            yield return Seconds(.2f);
            Check(reaction.VisualOffset.sqrMagnitude < .000001f && !reaction.FlashVisible, "Body impulse and flash restore after the short hit window");

            // Capture the real damage response in the next rendered game frame.
            CombatImpactFeedback.ApplyDamage(health, 24, reaction.ImpactCenter, Vector2.right, PlayerImpactSource.Projectile);
            Check(impact.LastTier == CombatImpactTier.Strong && reaction.FlashVisible, "A substantial real health loss produces a stronger impact and silhouette flash");
            Capture("Impact_StrongHit"); yield return null; yield return null;
            yield return Seconds(.25f);
            hits = impact.PresentedHits;
            health.TakeDamage(1);
            Check(impact.PresentedHits == hits, "Non-player health changes and lease restoration do not fabricate combat confirmations");
            Check(!CombatImpactFeedback.ApplyDamage(health, 0, root, Vector2.right, PlayerImpactSource.Projectile) && impact.PresentedHits == hits,
                "Zero damage is not a hit");

            // A same-frame multi-hit and a heavy kill resolve to one prioritized sound.
            var heavy = AbilityEnemy(EnemyType.Heavy, origin + new Vector3(2.2f, 1.5f), 10);
            int credits = GameManager.Instance.KillCount, kills = impact.PresentedKills, sounds = impact.PlayedSounds;
            CombatImpactFeedback.ApplyDamage(heavy.GetComponent<EnemyHealth>(), 10, heavy.transform.position, Vector2.right, PlayerImpactSource.Projectile);
            health.InitializeHealth(1000);
            int steadyObjects = impact.GetComponentsInChildren<Transform>().Length;
            for (int i = 0; i < 40; i++) CombatImpactFeedback.ApplyDamage(health, 1, reaction.ImpactCenter, Vector2.right, PlayerImpactSource.Chain);
            Check(impact.ActiveBurstCount <= 24 && impact.GetComponentsInChildren<Transform>().Length == steadyObjects,
                "A 40-hit frame reuses a bounded burst pool without allocating more emitters");
            Capture("Impact_HeavyKill"); yield return null; yield return null;
            Check(impact.LastPlayedTier == CombatImpactTier.HeavyKill && impact.PlayedSounds == sounds + 1,
                "One heavy-kill sound wins over forty ordinary hits in the same frame");
            Check(GameManager.Instance.KillCount == credits + 1 && impact.PresentedKills == kills + 1,
                "A confirmed kill retains exactly one native kill credit");
            CombatImpactFeedback.ApplyDamage(heavy.GetComponent<EnemyHealth>(), 100, heavy.transform.position, Vector2.right, PlayerImpactSource.Projectile);
            Check(GameManager.Instance.KillCount == credits + 1 && impact.PresentedKills == kills + 1,
                "Repeated damage on a corpse cannot repeat rewards or kill feedback");
            yield return Seconds(.5f);
            Check(impact.ActiveBurstCount == 0 && impact.ActiveVoiceCount <= 6, "Burst pool drains and audio polyphony remains bounded");

            var a = AbilityEnemy(EnemyType.Normal, origin + new Vector3(2, -1), 1);
            var b = AbilityEnemy(EnemyType.Fast, origin + new Vector3(2.8f, 1), 1);
            CombatImpactFeedback.ApplyDamage(a.GetComponent<EnemyHealth>(), 1, a.transform.position, Vector2.right, PlayerImpactSource.Projectile);
            CombatImpactFeedback.ApplyDamage(b.GetComponent<EnemyHealth>(), 1, b.transform.position, Vector2.right, PlayerImpactSource.Projectile);
            Check(impact.ConsecutiveKills == 3 && impact.confirmation.text.Contains("3") && impact.confirmationGroup.alpha > 0 && !impact.confirmationGroup.blocksRaycasts,
                "Consecutive-kill feedback counts actual deaths in a readable inventory-style panel without intercepting input");
            Capture("Impact_KillConfirmation"); yield return null; yield return null;
            yield return Seconds(impact.chainWindow + .1f);
            Check(impact.ConsecutiveKills == 0 && impact.confirmation.color.a == 0, "Kill confirmation expires without changing XP or damage multipliers");

            // Area damage and chain lightning still execute the production Bullet paths.
            var secondary = AbilityEnemy(EnemyType.Normal, normal.transform.position + Vector3.up * .9f, 100);
            foreach (bool chain in new[] { false, true })
            {
                health.InitializeHealth(100); secondary.GetComponent<EnemyHealth>().InitializeHealth(100);
                hits = impact.PresentedHits;
                var bullet = Object.FindObjectOfType<BulletPool>().GetBullet(normal.transform.position - Vector3.right, Quaternion.identity);
                bullet.Initialize(Vector2.right, 15, 4, 1, 2, ImpactModifiers(!chain, chain));
                yield return Until(() => secondary.GetComponent<EnemyHealth>().CurrentHealth < 100, chain ? "Native chain hit" : "Native explosion hit", 4);
                Check(impact.PresentedHits >= hits + 2 && secondary.GetComponent<EnemyImpactReaction>() != null,
                    (chain ? "Chain lightning" : "Explosion") + " shares confirmed feedback for secondary targets");
                foreach (var old in Object.FindObjectsOfType<Bullet>()) old.ReturnToPool();
                yield return Seconds(.3f);
            }
            hits = impact.PresentedHits;
            player.GetComponent<PlayerAbilityFeel>().ApplyPulse(3.8f);
            Check(impact.PresentedHits >= hits + 2 && impact.LastTier == CombatImpactTier.Strong,
                "Q's native damage and knockback also produce strong feedback on each eligible enemy");
            yield return Seconds(.4f);

            var offscreen = AbilityEnemy(EnemyType.Normal, origin + Vector3.right * 25, 1);
            hits = impact.PresentedHits; kills = impact.PresentedKills;
            CombatImpactFeedback.ApplyDamage(offscreen.GetComponent<EnemyHealth>(), 1, offscreen.transform.position, Vector2.right, PlayerImpactSource.Projectile);
            Check(impact.PresentedHits == hits && impact.PresentedKills == kills && offscreen.GetComponent<EnemyHealth>().IsDead,
                "Offscreen player damage still resolves natively without misleading local feedback");

            var audio = AudioManager.Instance;
            Check(audio != null, "Native audio settings bus is present");
            float priorSfx = audio.SfxVolume;
            audio.SetSfxVolume(0);
            CombatImpactFeedback.ApplyDamage(health, 1, reaction.ImpactCenter, Vector2.right, PlayerImpactSource.Projectile);
            yield return null; yield return null;
            Check(impact.GetComponentsInChildren<AudioSource>().All(v => v.volume == 0), "SFX mute reaches every new feedback voice");
            audio.SetSfxVolume(priorSfx);
            CombatImpactFeedback.ApplyDamage(health, 1, reaction.ImpactCenter, Vector2.right, PlayerImpactSource.Projectile);
            var pause = Object.FindObjectOfType<PauseMenuController>(); pause.PauseGame(); yield return null; yield return null;
            Check(impact.ActiveVoiceCount == 0 && impact.ActiveBurstCount == 0 && !reaction.FlashVisible && reaction.VisualOffset == Vector3.zero,
                "Pause clears audio, pending impacts and body offsets without touching time ownership");
            sounds = impact.PlayedSounds; pause.ResumeGame(); yield return Seconds(.25f);
            Check(impact.PlayedSounds == sounds && Time.timeScale == 1, "Resume does not replay queued hits or leave slow motion behind");
            CombatImpactFeedback.ApplyDamage(health, 1, reaction.ImpactCenter, Vector2.right, PlayerImpactSource.Projectile);
            normal.ReturnToPool();
            Check(!reaction.FlashVisible && !reaction.HealthBarVisible && reaction.VisualOffset == Vector3.zero,
                "Returning an enemy to its native pool clears all presentation state");
            secondary.ReturnToPool();
            var reused = AbilityEnemy(EnemyType.Normal, origin + Vector3.right * 2, 30);
            var reusedReaction = reused.GetComponent<EnemyImpactReaction>();
            Check(reusedReaction == null || !reusedReaction.FlashVisible && !reusedReaction.HealthBarVisible,
                "A newly leased enemy cannot inherit a previous occupant's health bar or flash");
            impact.enabled = false;
            int oldHealth = reused.GetComponent<EnemyHealth>().CurrentHealth;
            Check(!CombatImpactFeedback.ApplyDamage(reused.GetComponent<EnemyHealth>(), 1, reused.transform.position, Vector2.right, PlayerImpactSource.Projectile) &&
                reused.GetComponent<EnemyHealth>().CurrentHealth == oldHealth - 1, "Disabling the scene service preserves the native damage fallback");
            impact.enabled = true; reused.ReturnToPool();
            Check(!CombatImpactFeedback.OwnsDeath(health), "Damage scope never leaks beyond its call");
        }
    }
}
