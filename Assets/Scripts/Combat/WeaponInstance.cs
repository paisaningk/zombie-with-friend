using System.Collections.Generic;
using Player;

namespace Combat
{
    /// <summary>
    /// Runtime per-slot cache (owner + host): the resolved template + <see cref="WeaponProfile"/> with the
    /// slot's attachments (W3) and the player's upgrades folded in. Rebuilt when the slot's synced identity
    /// (weaponId/mods) or the player's upgrades change. Ammo is NOT cached here — it's read live from the
    /// SyncList so an ammo-only change (every shot) doesn't force a rebuild (decision 0016, layer-3 cache).
    /// </summary>
    public class WeaponInstance
    {
        public WeaponData template;
        public WeaponProfile profile;

        /// <summary>
        /// Fold a weapon slot (template + up to 3 attachment ids) and the player's upgrade multipliers into
        /// a fire-ready profile. Stacking (Q10b): <c>(base + Σflat) × (1 + Σpct) × upgradeMult</c>. Behavior
        /// and effects from every attachment accumulate. Returns null for an empty slot (null template).
        /// </summary>
        public static WeaponInstance Resolve(WeaponData template, in WeaponSlot slot, AttachmentCatalog attachments, PlayerUpgrades upgrades)
        {
            if (template == null)
                return null;

            float flatDamage = 0f, pctDamage = 0f, pctFireRate = 0f;
            int flatMag = 0, addPellets = 0, addPierce = 0;
            float addSpread = 0f;
            bool forceFullAuto = false;
            var effects = new List<WeaponEffect>();

            if (attachments != null)
            {
                Accumulate(attachments.Get(slot.mod0), ref flatDamage, ref pctDamage, ref pctFireRate, ref flatMag, ref addPellets, ref addSpread, ref addPierce, ref forceFullAuto, effects);
                Accumulate(attachments.Get(slot.mod1), ref flatDamage, ref pctDamage, ref pctFireRate, ref flatMag, ref addPellets, ref addSpread, ref addPierce, ref forceFullAuto, effects);
                Accumulate(attachments.Get(slot.mod2), ref flatDamage, ref pctDamage, ref pctFireRate, ref flatMag, ref addPellets, ref addSpread, ref addPierce, ref forceFullAuto, effects);
            }

            float dmgMult = upgrades != null ? upgrades.DamageMultiplier : 1f;
            float fireRateMult = upgrades != null ? upgrades.FireRateMultiplier : 1f;

            float damage = (template.damage + flatDamage) * (1f + pctDamage) * dmgMult;
            float fireRateFactor = fireRateMult * (1f + pctFireRate);
            float cooldown = fireRateFactor > 0f ? template.Cooldown / fireRateFactor : template.Cooldown;

            var profile = new WeaponProfile
            {
                damage = damage,
                cooldown = cooldown,
                magazineSize = template.magazineSize + flatMag,
                reloadTime = template.reloadTime,
                fullAuto = template.fullAuto || forceFullAuto,
                isProjectile = template is ProjectileWeaponData,
                range = template is HitscanWeaponData hitscan ? hitscan.range : 0f,
                pelletCount = 1 + addPellets,
                spread = addSpread,
                pierceCount = addPierce,
                effects = effects.ToArray(),
            };

            if (template is ProjectileWeaponData proj)
            {
                profile.projectileSpeed = proj.projectileSpeed;
                profile.projectilePrefab = proj.projectilePrefab;
            }

            return new WeaponInstance { template = template, profile = profile };
        }

        private static void Accumulate(AttachmentData a,
            ref float flatDamage, ref float pctDamage, ref float pctFireRate, ref int flatMag,
            ref int addPellets, ref float addSpread, ref int addPierce, ref bool forceFullAuto,
            List<WeaponEffect> effects)
        {
            if (a == null) return;
            flatDamage += a.flatDamage;
            pctDamage += a.pctDamage;
            pctFireRate += a.pctFireRate;
            flatMag += a.flatMagazine;
            addPellets += a.addPellets;
            addSpread += a.addSpread;
            addPierce += a.addPierce;
            forceFullAuto |= a.forceFullAuto;
            if (a.effects != null)
                foreach (var e in a.effects)
                    if (e != null) effects.Add(e);
        }
    }
}
