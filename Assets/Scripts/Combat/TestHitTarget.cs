using FishNet.Object;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// THROWAWAY test target — remove when real enemies exist (task 9).
    /// A damageable <see cref="IHitReceiver"/> so the weapon damage path (client raycast →
    /// server → ReceiveHit → HP drops) can be verified before any enemy exists. Lives on a
    /// scene NetworkObject on a non-Player layer so both hitscan and projectiles can hit it.
    /// Records the last hit so an MCP/runtime check can assert damage == WeaponData.damage.
    /// </summary>
    public class TestHitTarget : NetworkBehaviour, IHitReceiver
    {
        [SerializeField] private float hp = 1000f;

        public float Hp => hp;
        public float LastDamage { get; private set; }
        public int HitCount { get; private set; }

        // Called on the server (hitscan RPC applies here; projectiles resolve hits server-side).
        public void ReceiveHit(in HitInfo hit)
        {
            LastDamage = hit.Damage;
            HitCount++;
            hp -= hit.Damage;
            Debug.Log($"[TestHitTarget] took {hit.Damage} dmg -> hp={hp} (hits={HitCount})");
        }
    }
}
