using UnityEngine;

namespace Combat
{
    /// <summary>Instant-hit weapon: one raycast along the aim, up to <see cref="range"/>.</summary>
    [CreateAssetMenu(menuName = "Weapons/Hitscan Weapon", fileName = "HitscanWeapon")]
    public class HitscanWeaponData : WeaponData
    {
        [Header("Hitscan")]
        public float range = 100f;
    }
}
