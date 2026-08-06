using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Game;
using Pathfinding;
using Player;
using UnityEngine;

namespace Enemies
{
    /// <summary>
    /// A server-authoritative enemy. One component drives every type; behaviour comes from the
    /// assigned <see cref="EnemyData"/> (see decision 0008).
    ///
    /// Ownership model — enemies are server-spawned and have NO owner, so ALL logic runs on the
    /// server: HP, target selection, pathfinding (<see cref="FollowerEntity"/>) and attacks.
    /// Clients only see the result: pose is replicated by NetworkTransform, HP by a SyncVar. To
    /// avoid two authorities fighting over the transform, FollowerEntity runs on the server only
    /// (disabled on every non-server peer).
    ///
    /// Damage in via <see cref="IHitReceiver"/> (player hitscan/projectile). HP hitting 0 raises
    /// <see cref="OnDied"/> (hook for wave tracking / gold, tasks 10 &amp; 12) then despawns.
    /// Clients flash the mesh on any HP drop.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class Enemy : NetworkBehaviour, IHitReceiver
    {
        [SerializeField] private EnemyData _data;
        [Tooltip("Renderer flashed on damage (client feedback). Optional; leave null to skip flashing.")]
        [SerializeField] private Renderer _flashRenderer;

        // Hidden like PlayerHealth's SyncVar: consumers read the properties / react to the flash.
        private readonly SyncVar<float> _health = new SyncVar<float>();

        /// <summary>
        /// Server-side, fired once when HP reaches 0 (before despawn). Passes the dying enemy so a
        /// subscriber (WaveManager) can remove it from its live-list. Wave/gold hooks (tasks 10 &amp; 12).
        /// </summary>
        public event Action<Enemy> OnDied;

        public float Current => _health.Value;
        public float Max => _data != null ? _data.maxHp : 0f;
        public float Normalized => Max > 0f ? Mathf.Clamp01(_health.Value / Max) : 0f;
        public bool IsDead => _health.Value <= 0f;

        /// <summary>Gold this enemy is worth on death (split among players — task 11).</summary>
        public int GoldReward => _data != null ? _data.goldReward : 0;

        // --- movement / target / attack (server-only) ---
        private FollowerEntity _follower;
        private PlayerController _target;
        private float _nextRetarget;
        private float _nextAttackTime;
        private const float RetargetInterval = 0.5f;

        // --- hit flash (all peers, purely visual) ---
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color FlashColor = Color.white;
        private const float FlashDuration = 0.08f;
        private int _colorId;
        private Color _baseColor;
        private bool _canFlash;
        private float _flashUntil;

        private void Awake()
        {
            _follower = GetComponent<FollowerEntity>();
            // Off by default so non-server peers never run pathfinding; the server re-enables it
            // in OnStartServer. (Set before OnEnable so the disabled agent isn't even created.)
            if (_follower != null) _follower.enabled = false;

            _health.OnChange += HandleHealthChange;
            CacheFlashColor();
        }

        private void OnDestroy()
        {
            _health.OnChange -= HandleHealthChange;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _health.Value = _data != null ? _data.maxHp : 1f;
            if (_follower != null)
            {
                _follower.enabled = true;
                if (_data != null) _follower.maxSpeed = _data.moveSpeed;
            }
            // Force an immediate target pick on the first tick.
            _nextRetarget = 0f;
        }

        private void Update()
        {
            UpdateFlash();
            if (!IsServerInitialized) return;
            ServerTick();
        }

        // ---- server AI ----

        private void ServerTick()
        {
            if (_data == null || _follower == null) return;

            // Re-pick a target periodically, or immediately if the current one died / went down.
            if (Time.time >= _nextRetarget || _target == null || _target.State == null || !_target.State.IsAlive)
            {
                _nextRetarget = Time.time + RetargetInterval;
                _target = FindNearestAliveTarget();
            }

            if (_target == null)
            {
                // No one left to chase (all downed/dead). Hold position; lose (task 8) resolves the match.
                _follower.isStopped = true;
                return;
            }

            Vector3 targetPos = _target.transform.position;
            float dist = Vector3.Distance(transform.position, targetPos);
            bool inRange = dist <= _data.attackRange;

            _follower.destination = targetPos;
            _follower.isStopped = inRange; // stop to attack (melee touches at range; ranger kites-free for MVP)

            if (inRange && Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + _data.AttackCooldown;
                Attack(_target);
            }
        }

        private PlayerController FindNearestAliveTarget()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return null;

            PlayerController best = null;
            float bestSqr = float.MaxValue;
            Vector3 pos = transform.position;

            var players = gm.Players;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController p = players[i];
                if (p == null || p.State == null || !p.State.IsAlive) continue;
                float sqr = (p.transform.position - pos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        [Server]
        private void Attack(PlayerController target)
        {
            if (target == null || target.State == null || !target.State.IsAlive) return;

            if (_data.attackType == EnemyAttackType.Melee)
            {
                // Server owns both sides — call the target's health directly, no RPC needed.
                if (target.Health != null)
                    target.Health.ApplyDamage(_data.damage);
            }
            else
            {
                FireProjectile(target);
            }
        }

        [Server]
        private void FireProjectile(PlayerController target)
        {
            if (_data.projectilePrefab == null) return;

            // Aim center-to-center: the target's transform origin sits at its collider mid, so
            // this ray passes squarely through the body regardless of its height (a +chest offset
            // overshot the ~1m placeholder cube and flew over it).
            Vector3 origin = transform.position + Vector3.up * 0.5f; // keep the muzzle off the floor
            Vector3 aim = target.transform.position;
            Vector3 dir = (aim - origin).normalized;

            NetworkObject proj = Instantiate(_data.projectilePrefab, origin, Quaternion.LookRotation(dir));
            Spawn(proj);
            if (proj.TryGetComponent(out NetworkProjectile np))
                np.ServerInit(dir, _data.projectileSpeed, _data.damage);
        }

        // ---- damage / death ----

        /// <summary>
        /// Combat pipeline entry (player hitscan/projectile). Server-only; only the damage is used.
        /// Returns true when this hit killed the enemy — local attribution for on-kill weapon effects
        /// (decision 0016, W5).
        /// </summary>
        public bool ReceiveHit(in HitInfo hit)
        {
            if (!IsServerInitialized) return false;
            bool aliveBefore = _health.Value > 0f;
            ApplyDamage(hit.Damage);
            return aliveBefore && _health.Value <= 0f;
        }

        /// <summary>Reduce HP (ignored if &lt;= 0 or already dead), clamped at 0. Reaching 0 dies. Server-only.</summary>
        [Server]
        public void ApplyDamage(float amount)
        {
            if (amount <= 0f) return;
            if (_health.Value <= 0f) return;

            float next = Mathf.Max(0f, _health.Value - amount);
            if (next == _health.Value) return;
            _health.Value = next;

            if (next <= 0f)
                Die();
        }

        [Server]
        private void Die()
        {
            OnDied?.Invoke(this);
            if (IsSpawned)
                Despawn();
        }

        /// <summary>
        /// Despawn without dying — for WaveManager cleanup when a match ends (Lost). Deliberately
        /// skips <see cref="Die"/>/<see cref="OnDied"/> so it counts as neither a kill nor a
        /// wave-clear (the match is already over). Server-only.
        /// </summary>
        [Server]
        public void DespawnByManager()
        {
            if (IsSpawned)
                Despawn();
        }

        // ---- hit flash (client feedback, dedupe-agnostic) ----

        private void CacheFlashColor()
        {
            // Art pass (task 16c): the placeholder cube renderer is disabled and the visible body
            // is a child Synty model. Fall back to the first enabled child renderer so the hit-flash
            // lands on whatever is actually visible, without per-prefab rewiring of _flashRenderer.
            if (_flashRenderer == null || !_flashRenderer.enabled)
                _flashRenderer = FindVisibleRenderer();
            if (_flashRenderer == null) return;
            Material mat = _flashRenderer.material; // instance (placeholder art; fine for MVP)
            _colorId = mat.HasProperty(BaseColorId) ? BaseColorId : Shader.PropertyToID("_Color");
            if (mat.HasProperty(_colorId))
            {
                _baseColor = mat.GetColor(_colorId);
                _canFlash = true;
            }
        }

        // Returns the first enabled renderer under this enemy (the visible Synty model), skipping
        // the disabled placeholder cube renderer on the root.
        private Renderer FindVisibleRenderer()
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
                if (r != null && r.enabled) return r;
            return null;
        }

        private void HandleHealthChange(float prev, float next, bool asServer)
        {
            if (next < prev) TriggerFlash(); // took damage
        }

        private void TriggerFlash()
        {
            if (!_canFlash) return;
            _flashRenderer.material.SetColor(_colorId, FlashColor);
            _flashUntil = Time.time + FlashDuration;
        }

        private void UpdateFlash()
        {
            if (!_canFlash || _flashUntil <= 0f) return;
            if (Time.time >= _flashUntil)
            {
                _flashRenderer.material.SetColor(_colorId, _baseColor);
                _flashUntil = 0f;
            }
        }
    }
}
