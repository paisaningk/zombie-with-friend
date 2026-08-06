using UnityEngine;

namespace Combat
{
    /// <summary>
    /// One attachment template (decision 0016, W3): stat mods + shot behavior + optional server effects.
    /// Generic — fits any of a gun's 3 mod slots (Q9, no compatibility matrix). Stacking follows Q10b:
    /// flats sum, percents sum then multiply once, all on top of the base template and the player's
    /// upgrade multiplier — <c>(base + Σflat) × (1 + Σpct) × upgradeMult</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Weapons/Attachment", fileName = "Attachment")]
    public class AttachmentData : ScriptableObject
    {
        [Header("Shop")]
        public string displayName = "Attachment";
        [Tooltip("Gold cost to buy + equip this attachment (decision 0016, W4).")]
        public int cost = 300;

        [Header("Stat mods")]
        public float flatDamage;   // added to base damage before the percent step
        public float pctDamage;    // 0.2 = +20% damage
        public float pctFireRate;  // 0.2 = +20% fire rate (→ shorter cooldown)
        public int flatMagazine;   // added to magazine size

        [Header("Shot behavior (folds into WeaponProfile)")]
        public bool forceFullAuto; // make a semi weapon full-auto
        [Tooltip("Extra hitscan pellets per shot (shotgun). 0 = single shot.")]
        public int addPellets;
        [Tooltip("Cone half-angle in degrees applied when there are pellets.")]
        public float addSpread;
        [Tooltip("Extra targets a hitscan shot pierces through.")]
        public int addPierce;

        [Header("Effects (server-side, W5)")]
        [Tooltip("Event effects this attachment grants (Explosive / Chain / Heal-Ammo …).")]
        public WeaponEffect[] effects;
    }
}
