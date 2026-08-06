using System;

namespace Combat
{
    /// <summary>
    /// One arsenal slot's synced state — a single entry in the owner-only
    /// <c>SyncList&lt;WeaponSlot&gt;</c> on <see cref="PlayerWeapon"/> (decision 0016, Phase 6 W1).
    /// Value type + <see cref="IEquatable{T}"/> so the SyncList compares/serializes cleanly.
    ///
    /// <para><c>weaponId</c> indexes <see cref="WeaponCatalog"/> (-1 = empty slot). <c>mod0..2</c> index
    /// the attachment catalog (-1 = empty) — carried from W1 so buying attachments in W3 doesn't reshape
    /// the struct (and ammo stays atomic with the weapon it belongs to). <c>ammo</c> is per-slot: each
    /// gun keeps its own magazine across swaps.</para>
    /// </summary>
    [Serializable]
    public struct WeaponSlot : IEquatable<WeaponSlot>
    {
        public int weaponId;
        public int ammo;
        public int mod0;
        public int mod1;
        public int mod2;

        public bool IsEmpty => weaponId < 0;

        public static WeaponSlot Empty =>
            new WeaponSlot { weaponId = -1, ammo = 0, mod0 = -1, mod1 = -1, mod2 = -1 };

        /// <summary>A gun slot with no attachments and a full/given magazine.</summary>
        public static WeaponSlot Of(int weaponId, int ammo) =>
            new WeaponSlot { weaponId = weaponId, ammo = ammo, mod0 = -1, mod1 = -1, mod2 = -1 };

        /// <summary>Copy with a new ammo count — SyncList entries are replaced, never mutated in place.</summary>
        public WeaponSlot WithAmmo(int newAmmo)
        {
            WeaponSlot c = this;
            c.ammo = newAmmo;
            return c;
        }

        /// <summary>True when the weapon/attachment identity differs (ammo-only changes don't count) —
        /// lets the cache skip an expensive rebuild on every shot.</summary>
        public bool SameLoadout(WeaponSlot o) =>
            weaponId == o.weaponId && mod0 == o.mod0 && mod1 == o.mod1 && mod2 == o.mod2;

        public bool Equals(WeaponSlot o) =>
            weaponId == o.weaponId && ammo == o.ammo && mod0 == o.mod0 && mod1 == o.mod1 && mod2 == o.mod2;

        public override bool Equals(object obj) => obj is WeaponSlot s && Equals(s);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = weaponId;
                h = (h * 397) ^ ammo;
                h = (h * 397) ^ mod0;
                h = (h * 397) ^ mod1;
                h = (h * 397) ^ mod2;
                return h;
            }
        }
    }
}
