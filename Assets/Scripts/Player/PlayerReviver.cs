using FishNet.Object;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Lets an Alive owner revive a nearby Downed teammate by holding Interact. The client
    /// detects the target (proximity) and tracks the hold; the server RE-validates (reviver
    /// Alive, target Downed, in range) then brings the target back — PlayerState.SetState(Alive)
    /// + PlayerHealth.SetHp(partial). PlayerController on the target re-enables it + stops
    /// bleed-out. Uninterruptible: taking damage does NOT cancel; the hold resets only on
    /// releasing, the target leaving range / no longer downed, or the reviver going down.
    /// </summary>
    public class PlayerReviver : NetworkBehaviour
    {
        [SerializeField] private float _reviveRadius = 2.5f;
        [SerializeField] private float _reviveHoldSeconds = 3f;
        [Range(0f, 1f)]
        [SerializeField] private float _reviveHpPercent = 0.3f;
        [Tooltip("Layers to scan for downed players (the Player layer).")]
        [SerializeField] private LayerMask _playerMask = ~0;

        private PlayerState _state;
        private InputSystem_Actions _input;
        private float _hold;
        private readonly Collider[] _overlap = new Collider[8];

        /// <summary>Hold progress 0..1 for a future revive UI bar (owner-local).</summary>
        public float Progress => _reviveHoldSeconds > 0f ? Mathf.Clamp01(_hold / _reviveHoldSeconds) : 0f;

        private void Awake() => _state = GetComponent<PlayerState>();

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

            // Only an alive player actively holding Interact revives.
            if (_state == null || !_state.IsAlive || !_input.Player.Interact.IsPressed())
            {
                _hold = 0f;
                return;
            }

            PlayerState target = FindDownedTarget();
            if (target == null) // no valid target in range → reset
            {
                _hold = 0f;
                return;
            }

            _hold += Time.deltaTime;
            if (_hold >= _reviveHoldSeconds)
            {
                _hold = 0f;
                ServerRevive(target.NetworkObject);
            }
        }

        private PlayerState FindDownedTarget()
        {
            int n = Physics.OverlapSphereNonAlloc(transform.position, _reviveRadius, _overlap, _playerMask, QueryTriggerInteraction.Ignore);
            PlayerState best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var ps = _overlap[i].GetComponentInParent<PlayerState>();
                if (ps == null || ps == _state || !ps.IsDowned) continue;
                float d = (ps.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = ps; }
            }
            return best;
        }

        [ServerRpc]
        private void ServerRevive(NetworkObject targetNob)
        {
            if (targetNob == null) return;
            if (_state == null || !_state.IsAlive) return; // reviver must still be alive

            var targetState = targetNob.GetComponent<PlayerState>();
            var targetHealth = targetNob.GetComponent<PlayerHealth>();
            if (targetState == null || targetHealth == null || !targetState.IsDowned) return;

            // Re-validate distance on the server (target came from the client).
            float max = _reviveRadius + 1f; // tolerance for latency/movement
            if ((targetNob.transform.position - transform.position).sqrMagnitude > max * max) return;

            targetState.SetState(PlayerLifeState.Alive);
            targetHealth.SetHp(targetHealth.Max * _reviveHpPercent);
        }
    }
}
