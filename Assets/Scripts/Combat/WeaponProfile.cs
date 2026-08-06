using FishNet.Object;

namespace Combat
{
    /// <summary>
    /// Fully resolved fire stats for one slot: a <see cref="WeaponData"/> template folded with the
    /// player's upgrade multipliers (W1) and — later — attachment mods (W3). The fire path reads ONLY
    /// this, never the raw SO, so per-player scaling lives in one place. Rebuilt whenever the slot's
    /// weapon/mods change or upgrades change (decision 0016, Q11 "resolved profile").
    /// </summary>
    public class WeaponProfile
    {
        public float damage;         // already upgrade-adjusted
        public float cooldown;       // seconds between shots (already fire-rate-adjusted)
        public int magazineSize;
        public float reloadTime;
        public bool fullAuto;

        // Shot mechanics.
        public bool isProjectile;
        public float range;                  // hitscan reach
        public float projectileSpeed;        // projectile
        public NetworkObject projectilePrefab; // projectile template (server-only; never sent over RPC)

        // Attachment-driven behavior (W3). Defaults = a plain single, non-piercing shot.
        public int pelletCount = 1;          // >1 = shotgun spread of hitscan rays
        public float spread;                 // cone half-angle (deg) applied when pelletCount > 1
        public int pierceCount;              // extra targets a hitscan ray passes through

        // Server-side event effects (W5), gathered from the slot's attachments.
        public WeaponEffect[] effects = System.Array.Empty<WeaponEffect>();
    }
}
