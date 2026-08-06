using FishNet.Object;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Fires a travelling projectile (server-spawned). The prefab is read server-side from this asset —
    /// never sent over an RPC — so the owner only asks the server to spawn "the active weapon's
    /// projectile" (<see cref="PlayerWeapon.SpawnProjectile"/>). The resolved
    /// <see cref="WeaponProfile.isProjectile"/> flag drives that branch of the fire path (Phase 6 W1/W2).
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Projectile Weapon", fileName = "ProjectileWeapon")]
    public class ProjectileWeaponData : WeaponData
    {
        [Header("Projectile")]
        public NetworkObject projectilePrefab;
        public float projectileSpeed = 30f;
    }
}
