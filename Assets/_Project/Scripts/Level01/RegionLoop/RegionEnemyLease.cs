using UnityEngine;

namespace AlienPurge.Level01.RegionLoop
{
    // Observes the existing IsDead contract. Pool return/disable alone never counts as death.
    // This component is attached only to enemies explicitly leased by the region controller.
    [DisallowMultipleComponent]
    public sealed class RegionEnemyLease : MonoBehaviour
    {
        public string RunId { get; private set; }
        public RegionEnemyTicket Ticket { get; private set; }
        public RegionRunState Region { get; private set; }
        public PooledEnemy PoolMember { get; private set; }
        public EnemyHealth Health { get; private set; }
        RegionLoopController owner;
        EnemyMovement movement;
        EnemyContactDamage contact;
        EnemyPathNavigator navigator;
        EnemyDefinition definition;
        Behaviour[] attacks;
        bool[] attackEnabled;
        Vector3 origin;
        int priorHealth, priorExp, priorDamage;
        float priorSpeed, nextSense;
        bool reported, engaged, bound, priorNavigatorEnabled, gateInitialized, lastChase;

        public void Bind(RegionLoopController controller, RegionRunState region, RegionEnemyTicket ticket)
        {
            owner = controller; Region = region; Ticket = ticket; RunId = controller.RunId;
            PoolMember = GetComponent<PooledEnemy>(); Health = GetComponent<EnemyHealth>();
            movement = GetComponent<EnemyMovement>(); contact = GetComponent<EnemyContactDamage>();
            navigator = GetComponent<EnemyPathNavigator>(); origin = transform.position;
            definition = GetComponent<EnemyDefinition>();
            attacks = new Behaviour[] { GetComponent<FastChargeAttack>(), GetComponent<HeavyShockwaveAttack>(), GetComponent<RangedEnemyAttack>() };
            attackEnabled = new bool[attacks.Length];
            for (int i = 0; i < attacks.Length; i++) attackEnabled[i] = attacks[i] != null && attacks[i].enabled;
            bound = true; priorNavigatorEnabled = navigator != null && navigator.enabled;
            priorHealth = Health.MaxHealth; priorExp = Health.ExperienceAmount;
            priorSpeed = movement.MoveSpeed; priorDamage = contact.Damage;
            var profile = region.config.Profile(ticket.type);
            definition.InitializeInstanceAttributes(profile.health, profile.speed, profile.damage, profile.experience);
            // Restore surviving enemies without re-awarding anything or healing on region return.
            if (ticket.savedHealth > 0 && ticket.savedHealth < profile.health)
                Health.TakeDamage(profile.health - ticket.savedHealth);
            reported = false; engaged = ticket.role == RegionEnemyRole.Mission;
            enabled = true; ticket.lease = this; nextSense = 0; gateInitialized = false;
            Sense();
        }

        void LateUpdate()
        {
            if (owner == null || reported) return;
            if (Health.IsDead) { Report(true); return; }
            if (Time.time >= nextSense) { nextSense = Time.time + .3f; Sense(); }
        }
        void Sense()
        {
            if (owner == null || Health.IsDead) return;
            float distance = Vector2.Distance(transform.position, owner.world.PlayerFeet);
            if (Health.CurrentHealth < Health.MaxHealth || distance <= Region.config.perceptionRadius && owner.CanSenseV02(Region, transform.position)) engaged = true;
            bool chase = engaged && distance <= Region.config.leashRadius &&
                (Vector2.Distance(origin, transform.position) <= Region.config.leashRadius) &&
                (!owner.v02Enabled || owner.CurrentRegion == Region.config.regionId);
            if (gateInitialized && lastChase == chase) return;
            gateInitialized = true; lastChase = chase;
            // Only change the lease's movement gate on transitions. Native windups own their lock while engaged.
            for (int i = 0; i < attacks.Length; i++) if (attacks[i] != null) attacks[i].enabled = chase && attackEnabled[i];
            movement.SetMovementLocked(!chase);
            if (navigator != null) navigator.enabled = chase;
        }
        void Report(bool death)
        {
            if (reported || owner == null) return;
            reported = true;
            owner.OnEnemyReleased(this, death);
        }
        public void Release()
        {
            if (owner != null && !reported) Report(Health != null && Health.IsDead);
            // Detach before returning, so another caller (including a Boss) can reuse safely.
            owner = null; Ticket = null; Region = null; RunId = null;
            if (PoolMember != null && !PoolMember.IsReturned) PoolMember.ReturnToPool();
            enabled = false;
        }
        void OnDisable()
        {
            if (!bound) return;
            bound = false;
            bool recycle = owner != null && !owner.ShuttingDown && owner.gameObject.scene.isLoaded &&
                PoolMember != null && !PoolMember.IsReturned;
            var lifecycleOwner = owner;
            if (owner != null && !reported) Report(Health != null && Health.IsDead && !owner.ShuttingDown);
            owner = null; Ticket = null; Region = null; RunId = null;
            if (Health != null)
            {
                Health.InitializeHealth(priorHealth); Health.InitializeExperienceAmount(priorExp);
                movement.InitializeMoveSpeed(priorSpeed); contact.InitializeDamage(priorDamage);
                movement.SetMovementLocked(false);
                if (navigator != null) { navigator.enabled = priorNavigatorEnabled; navigator.ResetNavigation(); }
                for (int i = 0; i < attacks.Length; i++) if (attacks[i] != null) attacks[i].enabled = attackEnabled[i];
                if (definition != null) definition.ClearInstanceInitialization();
            }
            // An external SetActive(false) must not leak an ActiveSet slot in the shared pool.
            // Reparenting inside SetActive's OnDisable is forbidden by Unity. The scene owner drains this next Update.
            if (recycle && PoolMember.HasOwningPool) lifecycleOwner.DeferPoolReturn(PoolMember);
        }
    }
}
