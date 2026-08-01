using UnityEngine;

namespace Shop
{
    /// <summary>
    /// Data-driven shop config (see decision 0011). Holds per-upgrade cost / effect / cap so all
    /// three can be tuned in the inspector without touching code. Referenced by PlayerUpgrades.
    ///
    /// <see cref="UpgradeEntry.effectPerLevel"/> is interpreted per upgrade:
    /// Damage / FireRate = a FRACTION of the base stat per level (0.20 = +20%); MaxHp = a FLAT HP
    /// bonus per level (25 = +25 HP). The consuming multipliers live in PlayerUpgrades.
    /// </summary>
    [CreateAssetMenu(menuName = "Horde/Shop Data", fileName = "ShopData")]
    public class ShopData : ScriptableObject
    {
        [System.Serializable]
        public class UpgradeEntry
        {
            [Tooltip("Fixed gold cost per purchase (no cost-scaling in MVP).")]
            public int cost = 150;
            [Tooltip("Damage/FireRate: fraction of base per level (0.20 = +20%). MaxHp: flat HP per level.")]
            public float effectPerLevel = 0.20f;
            [Tooltip("Max times this upgrade can be bought.")]
            public int maxLevel = 5;
        }

        public UpgradeEntry damage = new UpgradeEntry { cost = 150, effectPerLevel = 0.20f, maxLevel = 5 };
        public UpgradeEntry maxHp = new UpgradeEntry { cost = 150, effectPerLevel = 25f, maxLevel = 5 };
        public UpgradeEntry fireRate = new UpgradeEntry { cost = 150, effectPerLevel = 0.15f, maxLevel = 5 };

        /// <summary>The entry for a given upgrade, or null for an unknown type.</summary>
        public UpgradeEntry Get(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Damage: return damage;
                case UpgradeType.MaxHp: return maxHp;
                case UpgradeType.FireRate: return fireRate;
                default: return null;
            }
        }
    }
}
