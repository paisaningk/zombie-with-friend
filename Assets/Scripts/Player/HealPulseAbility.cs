using System.Collections;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// The Support class ability — Heal Pulse (decision 0012). An owner-driven ability built on the
    /// standard action pattern: [SyncVar] cooldown end + ServerRpc (trigger/validate) + ObserversRpc
    /// (effect). Structurally mirrors <see cref="PlayerReviver"/> minus the hold/target-pick.
    ///
    /// Cast (Q): the owner client-gates (Alive + Support + off cooldown → avoids RPC spam), the SERVER
    /// re-validates everything (authoritative), stamps the cooldown, then heals everyone in radius —
    ///   Alive  → +<see cref="_healAmount"/> HP (includes the caster; clamped to Max)
    ///   Downed → revived (Alive + <see cref="_reviveHpPercent"/> of Max) — a weak top-up so the
    ///            deliberate <see cref="PlayerReviver"/> (higher %) still matters
    ///   Dead   → skipped (spectate until wave clear)
    /// Then an ObserversRpc plays a placeholder pulse on every peer.
    ///
    /// Self-guards on <see cref="PlayerState.IsAlive"/> (like Reviver); it is NOT in PlayerController's
    /// disable set — a downed/dead Support simply can't cast because the client gate + server both
    /// reject a non-Alive caster.
    ///
    /// The cooldown SyncVar uses FishNet NETWORK time (shared across peers), not Time.time, so a future
    /// cooldown bar (task 16) reads <see cref="CooldownRemaining"/> correctly on a remote owner.
    /// Owner-only: a cooldown is private to its owner (like gold / upgrades).
    /// </summary>
    public class HealPulseAbility : NetworkBehaviour
    {
        [Tooltip("Flat HP restored to each Alive player in range (incl. the caster). Class SO later (task 14).")]
        [SerializeField] private float _healAmount = 40f;
        [Range(0f, 1f)]
        [Tooltip("Fraction of Max HP a Downed player is revived with. Kept below PlayerReviver's % on purpose.")]
        [SerializeField] private float _reviveHpPercent = 0.15f;
        [Tooltip("Pulse radius in metres (around the caster).")]
        [SerializeField] private float _radius = 6f;
        [Tooltip("Cooldown between casts, seconds.")]
        [SerializeField] private float _cooldownSeconds = 15f;
        [Tooltip("Placeholder pulse visual lifetime, seconds.")]
        [SerializeField] private float _effectDuration = 0.35f;

        // Owner-only: a cooldown is personal. Network time (double) so it's meaningful across peers.
        private readonly SyncVar<double> _cooldownEndTime =
            new SyncVar<double>(new SyncTypeSettings(ReadPermission.OwnerOnly));

        private PlayerState _state;
        private PlayerClass _class;
        private InputSystem_Actions _input;

        /// <summary>Seconds left on cooldown, for a future cooldown UI (owner-local). 0 when ready.</summary>
        public float CooldownRemaining =>
            Mathf.Max(0f, (float)(_cooldownEndTime.Value - NetworkTimeNow));

        private double NetworkTimeNow => TimeManager != null
            ? TimeManager.TicksToTime(TickType.Tick)
            : 0d;

        private void Awake()
        {
            _state = GetComponent<PlayerState>();
            _class = GetComponent<PlayerClass>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (IsOwner)
            {
                _input = new InputSystem_Actions();
                _input.Player.Enable();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_input != null) { _input.Dispose(); _input = null; }
        }

        private void Update()
        {
            if (!IsOwner || _input == null) return;

            if (_input.Player.Ability.WasPressedThisFrame() && CanCastLocally())
                CmdHealPulse();
        }

        // Client-side gate: UX only (predicts the server's answer to avoid spamming the RPC). The
        // server re-checks all of this authoritatively.
        private bool CanCastLocally() =>
            _state != null && _state.IsAlive &&
            _class != null && _class.IsSupport &&
            NetworkTimeNow >= _cooldownEndTime.Value;

        /// <summary>Owner asks to cast. Server validates ownership + all conditions.</summary>
        [ServerRpc]
        private void CmdHealPulse() => ServerCast();

        /// <summary>
        /// Server-authoritative cast. Rejects (changing nothing) if the caster isn't a living Support
        /// or the cooldown hasn't elapsed. On success: stamp the cooldown, heal/revive everyone in
        /// radius, and fire the effect. Exposed for MCP verification.
        /// </summary>
        [Server]
        public void ServerCast()
        {
            if (_state == null || !_state.IsAlive) return;
            if (_class == null || !_class.IsSupport) return;

            double now = NetworkTimeNow;
            if (now < _cooldownEndTime.Value) return;
            _cooldownEndTime.Value = now + _cooldownSeconds;

            ApplyPulse();
            RpcPlayEffect(transform.position);
        }

        [Server]
        private void ApplyPulse()
        {
            var gm = Game.GameManager.Instance;
            if (gm == null) return;

            Vector3 origin = transform.position;
            float radiusSqr = _radius * _radius;
            var players = gm.Players;

            for (int i = 0; i < players.Count; i++)
            {
                PlayerController pc = players[i];
                if (pc == null || pc.State == null || pc.Health == null) continue;
                if ((pc.transform.position - origin).sqrMagnitude > radiusSqr) continue;

                if (pc.State.IsAlive)
                {
                    pc.Health.SetHp(pc.Health.Current + _healAmount); // clamps to Max
                }
                else if (pc.State.IsDowned)
                {
                    // Weak revive: state first, then HP (SetHp has no Alive-guard, but keep the
                    // order intuitive — the player is Alive by the time HP lands).
                    pc.State.SetState(PlayerLifeState.Alive);
                    pc.Health.SetHp(pc.Health.Max * _reviveHpPercent);
                }
                // Dead → skipped on purpose (spectate until wave clear).
            }
        }

        // --- placeholder effect (all peers) ---

        [ObserversRpc]
        private void RpcPlayEffect(Vector3 position)
        {
            Debug.Log($"[HealPulse] pulse at {position} (r={_radius})");
            StartCoroutine(PulseVisual(position));
        }

        private IEnumerator PulseVisual(Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HealPulseFx";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // visual only — never interact with physics
            go.transform.position = position;

            var mr = go.GetComponent<MeshRenderer>();
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null && mr != null)
                mr.material = new Material(sh) { color = new Color(0.3f, 1f, 0.5f) };

            float t = 0f;
            float maxScale = _radius * 2f; // diameter == radius reach
            while (t < _effectDuration)
            {
                t += Time.deltaTime;
                float s = Mathf.Lerp(0f, maxScale, t / _effectDuration);
                go.transform.localScale = new Vector3(s, s, s);
                yield return null;
            }
            Destroy(go);
        }
    }
}
