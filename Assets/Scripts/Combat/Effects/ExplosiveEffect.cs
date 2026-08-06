using FishNet.Object;
using UnityEngine;

namespace Combat.Effects
{
    /// <summary>
    /// Explosive rounds (decision 0016, W5 example 1): every hit detonates, damaging everything in a
    /// radius around the impact point. Server-only. Splash damage is applied through
    /// <see cref="PlayerWeapon.EffectDamage"/>, which bypasses the effect hooks — that's the recursion
    /// guard (an explosion can never trigger another explosion).
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Effects/Explosive", fileName = "ExplosiveEffect")]
    public class ExplosiveEffect : WeaponEffect
    {
        [SerializeField] private float _radius = 4f;
        [SerializeField] private float _damage = 15f;
        [Tooltip("Layers the blast damages (Enemy).")]
        [SerializeField] private LayerMask _damageMask = ~0;

        public override void OnHitDealt(in EffectContext ctx)
        {
            if (ctx.Weapon == null) return;

            Collider[] hits = Physics.OverlapSphere(ctx.Point, _radius, _damageMask, QueryTriggerInteraction.Ignore);
            foreach (Collider col in hits)
            {
                NetworkObject nob = col.GetComponentInParent<NetworkObject>();
                if (nob == null) continue;
                if (ctx.Victim != null && nob == ctx.Victim) continue; // the direct hit already took full damage
                if (col.GetComponentInParent<IHitReceiver>() == null) continue;

                Vector3 dir = (nob.transform.position - ctx.Point).normalized;
                ctx.Weapon.EffectDamage(nob, nob.transform.position, dir, _damage);
            }

            ctx.Weapon.RpcEffectBurst(ctx.Point, _radius);
        }
    }
}
