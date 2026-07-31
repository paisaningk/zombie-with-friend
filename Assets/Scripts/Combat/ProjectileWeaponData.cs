using FishNet.Object;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Fires a travelling projectile (server-spawned). The prefab is read server-side from this
    /// asset — never sent over the RPC — so <see cref="PlayerWeapon.SpawnProjectile"/> only asks
    /// the server to spawn "the current weapon's projectile".
    ///
    /// ⚠️ UNVERIFIED (MVP): the spawn/init path runs, but the legacy Bullet prefab can't register
    /// hits — its collider is non-trigger while NetworkProjectile uses OnTriggerEnter. Making a
    /// projectile weapon actually work needs prototype surgery on the projectile prefab (trigger
    /// collider + FishNet "Reserialize NetworkObjects"). Deferred to task 9 (enemies = real target).
    /// Not wired to any player by default; the equipped weapon is a HitscanWeaponData.
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Projectile Weapon", fileName = "ProjectileWeapon")]
    public class ProjectileWeaponData : WeaponData
    {
        [Header("Projectile")]
        public NetworkObject projectilePrefab;
        public float projectileSpeed = 30f;

        public override void Fire(PlayerWeapon owner)
        {
            owner.SpawnProjectile();
        }
    }
}
