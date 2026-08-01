using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// The player's weapon: owns ammo / reload / fire-rate + input, and the networked fire
    /// primitives that a <see cref="WeaponData"/> strategy drives. Swap <c>_weaponData</c> to
    /// change guns; add a gun type by subclassing WeaponData (no change here).
    ///
    /// Hitscan = client-authoritative targeting (Model B): the owner raycasts locally and
    /// reports the hit target; the SERVER applies <c>WeaponData.damage</c> (client never sends a
    /// damage value). Projectiles are server-spawned from the server's own WeaponData (prefab is
    /// never sent over an RPC). Fire-rate + ammo are client-gated AND server-validated.
    /// </summary>
    public class PlayerWeapon : NetworkBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private WeaponData _weaponData;
        [Tooltip("Layers the hitscan can hit. Exclude Player (no friendly fire / self) + IgnoreRaycast.")]
        [SerializeField] private LayerMask _hitMask = ~0;

        [Header("Refs")]
        [Tooltip("Raycast origin + direction (the owner camera / CameraHolder).")]
        [SerializeField] private Transform _aimSource;
        [Tooltip("Tracer visual origin (muzzle).")]
        [SerializeField] private Transform _muzzle;

        private readonly SyncVar<int> _ammo = new SyncVar<int>();
        private readonly SyncVar<bool> _reloading = new SyncVar<bool>();

        private InputSystem_Actions _input;
        private float _nextClientFire; // client-side gate
        private float _nextServerFire; // server-side validate
        private float _reloadEndTime;  // server-side reload timer

        private LineRenderer _tracer;
        private float _tracerHideTime;

        private Player.PlayerUpgrades _upgrades; // per-player damage / fire-rate scalars (task 12b)

        public int Ammo => _ammo.Value;
        public bool IsReloading => _reloading.Value;
        /// <summary>Magazine capacity of the current weapon (0 if none) — for the ammo HUD "x / y".</summary>
        public int MagazineSize => _weaponData != null ? _weaponData.magazineSize : 0;
        public Vector3 AimOrigin => _aimSource != null ? _aimSource.position : transform.position;
        public Vector3 AimDirection => _aimSource != null ? _aimSource.forward : transform.forward;

        // Upgrade-adjusted stats (fall back to base when there's no upgrades component / no data).
        private float DamageMultiplier => _upgrades != null ? _upgrades.DamageMultiplier : 1f;
        private float EffectiveCooldown
        {
            get
            {
                float baseCd = _weaponData != null ? _weaponData.Cooldown : 0.1f;
                float mult = _upgrades != null ? _upgrades.FireRateMultiplier : 1f;
                return mult > 0f ? baseCd / mult : baseCd; // higher fire rate → shorter cooldown
            }
        }

        private void Awake()
        {
            _upgrades = GetComponent<Player.PlayerUpgrades>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _ammo.Value = _weaponData != null ? _weaponData.magazineSize : 0;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetupTracer();
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
            // Server: finish an in-progress reload.
            if (IsServerInitialized && _reloading.Value && Time.time >= _reloadEndTime)
            {
                _ammo.Value = _weaponData != null ? _weaponData.magazineSize : 0;
                _reloading.Value = false;
            }

            // Any client: hide the tracer after its brief flash.
            if (_tracer != null && _tracer.enabled && Time.time >= _tracerHideTime)
                _tracer.enabled = false;

            if (!IsOwner || _input == null || _weaponData == null) return;

            if (_input.Player.Reload.WasPressedThisFrame())
                ServerReload();

            bool firePressed = _weaponData.fullAuto
                ? _input.Player.Attack.IsPressed()
                : _input.Player.Attack.WasPressedThisFrame();

            if (firePressed && CanFireLocally())
            {
                _nextClientFire = Time.time + EffectiveCooldown;
                _weaponData.Fire(this); // strategy → FireHitscan / SpawnProjectile
            }
        }

        private bool CanFireLocally() =>
            Time.time >= _nextClientFire && _ammo.Value > 0 && !_reloading.Value;

        // ---- primitives driven by WeaponData.Fire (owner client) ----

        /// <summary>Raycast the aim, report the hit target to the server (server applies damage).</summary>
        public void FireHitscan(float range)
        {
            Vector3 origin = AimOrigin;
            Vector3 dir = AimDirection;
            Vector3 end = origin + dir * range;
            NetworkObject targetNob = null;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, _hitMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                if (hit.collider.GetComponentInParent<IHitReceiver>() != null)
                    targetNob = hit.collider.GetComponentInParent<NetworkObject>();
            }

            ShowTracerLocal(_muzzle != null ? _muzzle.position : origin, end);
            RpcTracer(_muzzle != null ? _muzzle.position : origin, end);
            ServerReportHit(targetNob, end, dir);
        }

        /// <summary>
        /// Ask the server to spawn the current weapon's projectile.
        /// ⚠️ UNVERIFIED path — see <see cref="ProjectileWeaponData"/>: the legacy Bullet prefab
        /// can't register hits yet (non-trigger collider). Verified only up to spawn+init; the
        /// projectile does not damage anything until the prefab is fixed (task 9).
        /// </summary>
        public void SpawnProjectile()
        {
            ServerSpawnProjectile(_muzzle != null ? _muzzle.position : AimOrigin, AimDirection);
        }

        // ---- server: validate + apply ----

        [Server]
        private bool ServerTryConsume()
        {
            if (_reloading.Value) return false;
            if (Time.time < _nextServerFire) return false;
            if (_ammo.Value <= 0) return false;

            _nextServerFire = Time.time + EffectiveCooldown;
            _ammo.Value--;
            if (_ammo.Value <= 0)
                ServerStartReload(); // auto-reload on empty
            return true;
        }

        [ServerRpc]
        private void ServerReportHit(NetworkObject target, Vector3 point, Vector3 dir)
        {
            if (!ServerTryConsume()) return;
            if (target == null) return;
            var receiver = target.GetComponent<IHitReceiver>();
            if (receiver != null)
            {
                float dmg = (_weaponData != null ? _weaponData.damage : 0f) * DamageMultiplier;
                receiver.ReceiveHit(new HitInfo(point, dir, dmg, 0f));
            }
        }

        [ServerRpc]
        private void ServerSpawnProjectile(Vector3 origin, Vector3 dir)
        {
            if (!ServerTryConsume()) return;
            if (_weaponData is not ProjectileWeaponData pw || pw.projectilePrefab == null) return;

            NetworkObject proj = Instantiate(pw.projectilePrefab, origin, Quaternion.LookRotation(dir));
            Spawn(proj);
            if (proj.TryGetComponent(out NetworkProjectile np))
                np.ServerInit(dir, pw.projectileSpeed, pw.damage * DamageMultiplier);
        }

        // ---- reload ----

        [ServerRpc]
        private void ServerReload()
        {
            if (_reloading.Value || _weaponData == null) return;
            if (_ammo.Value >= _weaponData.magazineSize) return;
            ServerStartReload();
        }

        [Server]
        private void ServerStartReload()
        {
            if (_weaponData == null || _reloading.Value) return;
            _reloading.Value = true;
            _reloadEndTime = Time.time + _weaponData.reloadTime;
        }

        // ---- tracer placeholder ----

        private void SetupTracer()
        {
            var go = new GameObject("WeaponTracer");
            go.transform.SetParent(transform, false);
            _tracer = go.AddComponent<LineRenderer>();
            _tracer.widthMultiplier = 0.03f;
            _tracer.positionCount = 2;
            _tracer.useWorldSpace = true;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh != null) _tracer.material = new Material(sh);
            _tracer.enabled = false;
        }

        private void ShowTracerLocal(Vector3 from, Vector3 to)
        {
            if (_tracer == null) return;
            _tracer.SetPosition(0, from);
            _tracer.SetPosition(1, to);
            _tracer.enabled = true;
            _tracerHideTime = Time.time + 0.04f;
        }

        [ObserversRpc(ExcludeOwner = true)]
        private void RpcTracer(Vector3 from, Vector3 to) => ShowTracerLocal(from, to);
    }
}
