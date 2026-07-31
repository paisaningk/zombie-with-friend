using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Base weapon definition (ScriptableObject-as-strategy). Holds shared stats and defines
    /// HOW the weapon fires via <see cref="Fire"/>. <see cref="PlayerWeapon"/> owns the shared
    /// concerns (ammo, reload, fire-rate gating, input) and exposes the networked primitives
    /// that Fire() drives. Swap the WeaponData reference on PlayerWeapon to change guns; add a
    /// new gun behaviour by subclassing this — no PlayerWeapon change.
    /// </summary>
    public abstract class WeaponData : ScriptableObject
    {
        [Header("Shared stats")]
        public float damage = 10f;
        [Tooltip("Rounds per second.")]
        public float fireRate = 8f;
        public int magazineSize = 30;
        public float reloadTime = 2f;
        [Tooltip("Hold to keep firing (auto) vs one shot per press (semi).")]
        public bool fullAuto = true;

        public float Cooldown => fireRate > 0f ? 1f / fireRate : 0f;

        /// <summary>
        /// Executes one shot on the owner/client. Called once per trigger pull AFTER PlayerWeapon
        /// has gated ammo/cooldown locally. Drives the owner's networked fire primitives.
        /// </summary>
        public abstract void Fire(PlayerWeapon owner);
    }
}
