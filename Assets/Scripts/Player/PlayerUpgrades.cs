using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Shop;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Per-player stat upgrades bought from the shop (see decision 0011). Stores a LEVEL per upgrade
    /// (never mutates the shared WeaponData SO / prefab maxHP) and exposes the multipliers that the
    /// stat readers apply at use-time:
    ///   damage   = weaponData.damage   × <see cref="DamageMultiplier"/>   (PlayerWeapon, server)
    ///   fireRate = weaponData.Cooldown ÷ <see cref="FireRateMultiplier"/> (PlayerWeapon gate+validate)
    ///   maxHP    = base _maxHp + <see cref="BonusMaxHp"/>                  (PlayerHealth.Max)
    ///
    /// Levels are OwnerOnly SyncVars — like gold, an upgrade is private to its owner. Every place
    /// that reads a multiplier is either the server (authoritative value) or the owner (has the
    /// value), so owner-only visibility is sufficient; other clients never need it.
    ///
    /// Purchase is server-authoritative: owner calls <see cref="CmdBuy"/> → <see cref="TryBuy"/>
    /// validates shop-open + cap + affordability, spends gold, bumps the level (and heals the MaxHp
    /// delta immediately so buying at full HP feels rewarding).
    /// </summary>
    public class PlayerUpgrades : NetworkBehaviour
    {
        [Tooltip("Cost / effect / cap per upgrade. Shared asset; only levels are per-player.")]
        [SerializeField] private ShopData _shop;

        // OwnerOnly: an upgrade is private to its owner (mirrors PlayerWallet's gold).
        private readonly SyncVar<int> _damageLevel = new SyncVar<int>(new SyncTypeSettings(ReadPermission.OwnerOnly));
        private readonly SyncVar<int> _maxHpLevel = new SyncVar<int>(new SyncTypeSettings(ReadPermission.OwnerOnly));
        private readonly SyncVar<int> _fireRateLevel = new SyncVar<int>(new SyncTypeSettings(ReadPermission.OwnerOnly));

        /// <summary>Fires (owner + server) whenever any level changes — for a future shop HUD.</summary>
        public event Action OnUpgradesChanged;

        private PlayerWallet _wallet;
        private PlayerHealth _health;

        public int LevelOf(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Damage: return _damageLevel.Value;
                case UpgradeType.MaxHp: return _maxHpLevel.Value;
                case UpgradeType.FireRate: return _fireRateLevel.Value;
                default: return 0;
            }
        }

        private float EffectOf(UpgradeType type)
        {
            ShopData.UpgradeEntry e = _shop != null ? _shop.Get(type) : null;
            return e != null ? e.effectPerLevel : 0f;
        }

        /// <summary>Damage scalar: 1 + level × fraction. 1 when no upgrade / no data.</summary>
        public float DamageMultiplier => 1f + LevelOf(UpgradeType.Damage) * EffectOf(UpgradeType.Damage);

        /// <summary>Fire-rate scalar (&gt;= 1): effective cooldown = base ÷ this.</summary>
        public float FireRateMultiplier => 1f + LevelOf(UpgradeType.FireRate) * EffectOf(UpgradeType.FireRate);

        /// <summary>Flat bonus added to base max HP.</summary>
        public float BonusMaxHp => LevelOf(UpgradeType.MaxHp) * EffectOf(UpgradeType.MaxHp);

        private void Awake()
        {
            _wallet = GetComponent<PlayerWallet>();
            _health = GetComponent<PlayerHealth>();

            _damageLevel.OnChange += HandleChange;
            _maxHpLevel.OnChange += HandleChange;
            _fireRateLevel.OnChange += HandleChange;
        }

        private void OnDestroy()
        {
            _damageLevel.OnChange -= HandleChange;
            _maxHpLevel.OnChange -= HandleChange;
            _fireRateLevel.OnChange -= HandleChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _damageLevel.Value = 0;
            _maxHpLevel.Value = 0;
            _fireRateLevel.Value = 0;
        }

        /// <summary>Reset all upgrade levels to 0 for a match replay (task 15). Server-only; called via
        /// PlayerController.ResetForReplay. Stat readers (damage/fireRate/maxHP multipliers) fall back to
        /// their base values automatically once levels are 0.</summary>
        [Server]
        public void ResetForReplay()
        {
            _damageLevel.Value = 0;
            _maxHpLevel.Value = 0;
            _fireRateLevel.Value = 0;
        }

        /// <summary>Owner asks to buy an upgrade (clicking a shop button). Server validates ownership.</summary>
        [ServerRpc]
        public void CmdBuy(UpgradeType type) => TryBuy(type);

        /// <summary>
        /// Server-authoritative purchase. Rejects (changing nothing) if the shop is closed, the
        /// upgrade is maxed, or gold is insufficient. On success: spend, bump the level, and — for
        /// MaxHp — heal the delta immediately (bump the level FIRST so the new cap admits it).
        /// Returns whether the purchase happened. Server-only (exposed for MCP verification).
        /// </summary>
        [Server]
        public bool TryBuy(UpgradeType type)
        {
            if (_shop == null) return false;
            if (Game.GameManager.Instance == null || !Game.GameManager.Instance.ShopOpen) return false;

            ShopData.UpgradeEntry entry = _shop.Get(type);
            if (entry == null) return false;

            int level = LevelOf(type);
            if (level >= entry.maxLevel) return false;
            if (_wallet == null || !_wallet.TrySpend(entry.cost)) return false;

            SetLevel(type, level + 1);

            if (type == UpgradeType.MaxHp && _health != null)
                _health.SetHp(_health.Current + entry.effectPerLevel); // heal the +MaxHp delta now

            return true;
        }

        [Server]
        private void SetLevel(UpgradeType type, int value)
        {
            switch (type)
            {
                case UpgradeType.Damage: _damageLevel.Value = value; break;
                case UpgradeType.MaxHp: _maxHpLevel.Value = value; break;
                case UpgradeType.FireRate: _fireRateLevel.Value = value; break;
            }
        }

        private void HandleChange(int prev, int next, bool asServer) => OnUpgradesChanged?.Invoke();
    }
}
