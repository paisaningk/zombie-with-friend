using Combat;
using FishNet.Object;
using Game;
using Obvious.Soap;
using Player;
using UnityEngine;

namespace GameUI
{
    /// <summary>
    /// Bridges this player's networked state into the SOAP ScriptableVariables the HUD binds to
    /// (decision 0015). SOAP variables are local, non-networked assets; the authoritative data lives in
    /// FishNet SyncVars — so ONE owner-side publisher reads them and writes the SO variables, and the
    /// HUD's Bind* components display them with zero UI code.
    ///
    /// Owner-only: only the local player's publisher writes (each client shows ITS OWN player's HUD).
    /// A remote player's copy of this component sits idle. Values are pushed only when they change, so
    /// a SOAP OnValueChanged (and the bound UI refresh) fires no more than the data actually moves.
    ///
    /// Composite readouts (ammo "27 / 30", wave "Wave 3 / 5") are formatted here into StringVariables —
    /// BindTextMeshPro binds a single value, so the two-number strings are assembled on this side.
    /// </summary>
    public class HudPublisher : NetworkBehaviour
    {
        [Header("SOAP variables (shared assets — the HUD binds to these)")]
        [SerializeField] private FloatVariable _health;   // normalized 0..1 (BindFillingImage max = 1)
        [SerializeField] private IntVariable _gold;
        [SerializeField] private IntVariable _enemies;    // enemies left to kill this wave
        [SerializeField] private StringVariable _ammo;    // "27 / 30" or "RELOADING"
        [SerializeField] private StringVariable _wave;    // "Wave 3 / 5"
        [SerializeField] private StringVariable _class;   // "Gunner" / "Support"
        [SerializeField] private FloatVariable _cooldown; // ability cooldown seconds remaining (0 = ready)
        [SerializeField] private BoolVariable _isSupport; // HUD hides the cooldown widget when false

        private PlayerHealth _healthSrc;
        private PlayerWallet _walletSrc;
        private PlayerClass _classSrc;
        private PlayerWeapon _weaponSrc;
        private HealPulseAbility _abilitySrc;

        private void Awake()
        {
            _healthSrc = GetComponent<PlayerHealth>();
            _walletSrc = GetComponent<PlayerWallet>();
            _classSrc = GetComponent<PlayerClass>();
            _weaponSrc = GetComponent<PlayerWeapon>();
            _abilitySrc = GetComponent<HealPulseAbility>();
        }

        // Owner drives the HUD. Poll every frame (ammo / cooldown have no change-event) and write each
        // SO only on change — cheap, and it keeps bound UI from refreshing every frame.
        private void Update()
        {
            if (!IsOwner) return;

            if (_health != null && _healthSrc != null)
                SetFloat(_health, _healthSrc.Normalized);

            if (_gold != null && _walletSrc != null)
                SetInt(_gold, _walletSrc.Gold);

            if (_ammo != null && _weaponSrc != null)
                SetString(_ammo, _weaponSrc.IsReloading
                    ? "RELOADING"
                    : $"{_weaponSrc.Ammo} / {_weaponSrc.MagazineSize}");

            {
                WaveManager wm = WaveManager.Instance;
                if (_wave != null)
                    SetString(_wave, wm != null ? $"Wave {wm.CurrentWave} / {wm.WaveCount}" : "");
                if (_enemies != null)
                    SetInt(_enemies, wm != null ? wm.EnemiesRemaining : 0);
            }

            if (_classSrc != null)
            {
                if (_class != null) SetString(_class, _classSrc.Current.ToString());
                if (_isSupport != null) SetBool(_isSupport, _classSrc.IsSupport);
            }

            if (_cooldown != null && _abilitySrc != null)
                SetFloat(_cooldown, _abilitySrc.CooldownRemaining);
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (!IsOwner) return;

            // Clear so a stale HUD doesn't linger after the local player despawns (disconnect / scene
            // change). SOAP also resets runtime values on play-mode exit, but this covers in-play teardown.
            SetFloat(_health, 0f);
            SetInt(_gold, 0);
            SetInt(_enemies, 0);
            SetString(_ammo, "");
            SetString(_wave, "");
            SetString(_class, "");
            SetFloat(_cooldown, 0f);
            SetBool(_isSupport, false);
        }

        // Write-on-change helpers (avoid firing SOAP's OnValueChanged when nothing moved).
        private static void SetFloat(FloatVariable v, float value)
        {
            if (v != null && !Mathf.Approximately(v.Value, value)) v.Value = value;
        }

        private static void SetInt(IntVariable v, int value)
        {
            if (v != null && v.Value != value) v.Value = value;
        }

        private static void SetString(StringVariable v, string value)
        {
            if (v != null && v.Value != value) v.Value = value;
        }

        private static void SetBool(BoolVariable v, bool value)
        {
            if (v != null && v.Value != value) v.Value = value;
        }
    }
}
