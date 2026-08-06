using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Combat.Effects
{
    /// <summary>
    /// Chain lightning (decision 0016, W5 example 2): a hit arcs to the nearest other targets, dealing a
    /// reduced amount to each. Server-only; chained damage goes through
    /// <see cref="PlayerWeapon.EffectDamage"/> so an arc can never trigger another chain (recursion guard).
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Effects/Chain Lightning", fileName = "ChainLightningEffect")]
    public class ChainLightningEffect : WeaponEffect
    {
        [SerializeField] private int _maxJumps = 2;
        [SerializeField] private float _jumpRadius = 6f;
        [Tooltip("Fraction of the shot's damage dealt to each chained target.")]
        [SerializeField] private float _damageFraction = 0.5f;
        [SerializeField] private LayerMask _damageMask = ~0;

        public override void OnHitDealt(in EffectContext ctx)
        {
            if (ctx.Weapon == null || _maxJumps <= 0) return;

            var hit = new List<NetworkObject>();
            if (ctx.Victim != null) hit.Add(ctx.Victim);

            Vector3 from = ctx.Point;
            float damage = ctx.Damage * _damageFraction;

            for (int jump = 0; jump < _maxJumps; jump++)
            {
                NetworkObject next = FindNearest(from, hit);
                if (next == null) break;

                Vector3 to = next.transform.position;
                ctx.Weapon.EffectDamage(next, to, (to - from).normalized, damage);
                ctx.Weapon.RpcEffectBeam(from, to);

                hit.Add(next);
                from = to;
            }
        }

        private NetworkObject FindNearest(Vector3 origin, List<NetworkObject> exclude)
        {
            Collider[] candidates = Physics.OverlapSphere(origin, _jumpRadius, _damageMask, QueryTriggerInteraction.Ignore);
            NetworkObject best = null;
            float bestSqr = float.MaxValue;

            foreach (Collider col in candidates)
            {
                NetworkObject nob = col.GetComponentInParent<NetworkObject>();
                if (nob == null || exclude.Contains(nob)) continue;
                if (col.GetComponentInParent<IHitReceiver>() == null) continue;

                float sqr = (nob.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = nob;
                }
            }

            return best;
        }
    }
}
