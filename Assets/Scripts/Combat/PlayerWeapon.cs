using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat
{
    /// <summary>
    /// The player's arsenal (decision 0016, Phase 6 W1): two fixed slots (Primary/Secondary) synced as a
    /// small owner-only <see cref="SyncList{T}"/> of <see cref="WeaponSlot"/> (weaponId + per-slot ammo +
    /// mod ids) plus an <c>activeSlot</c> index. Each machine resolves the synced ids through the shared
    /// <see cref="WeaponCatalog"/> into a cached <see cref="WeaponInstance"/> (template + resolved
    /// <see cref="WeaponProfile"/>); the fire path reads ONLY the active profile.
    ///
    /// <para>Hitscan = client-authoritative targeting (Model B): the owner raycasts locally and reports
    /// the hit; the SERVER applies <c>profile.damage</c>. Projectiles are server-spawned from the
    /// server's own resolved profile (prefab never crosses an RPC). Fire-rate + ammo are client-gated
    /// AND server-validated, per slot. Swapping cancels an in-progress reload (decision 7a).</para>
    /// </summary>
    public class PlayerWeapon : NetworkBehaviour
    {
        private const int SlotCount = 2; // Primary + Secondary (Q4 fixed typed slots)

        [Header("Arsenal")]
        [SerializeField] private WeaponCatalog _catalog;
        [Tooltip("Attachment templates (W3). Slot mod ids index this catalog.")]
        [SerializeField] private AttachmentCatalog _attachments;
        [Tooltip("Weapon ids (index in the catalog) that fill Primary/Secondary at match start. -1 = empty.")]
        [SerializeField] private int[] _startingLoadout = { 0, 1 };

        [Header("Refs")]
        [Tooltip("Layers the hitscan can hit. Exclude Player (no friendly fire / self) + IgnoreRaycast.")]
        [SerializeField] private LayerMask _hitMask = ~0;
        [Tooltip("Raycast origin + direction (the owner camera / CameraHolder).")]
        [SerializeField] private Transform _aimSource;
        [Tooltip("Tracer visual origin (muzzle).")]
        [SerializeField] private Transform _muzzle;

        // --- synced (owner-only): the arsenal + which slot is active ---
        private readonly SyncList<WeaponSlot> _slots =
            new SyncList<WeaponSlot>(new SyncTypeSettings(ReadPermission.OwnerOnly));
        private readonly SyncVar<int> _activeSlot =
            new SyncVar<int>(new SyncTypeSettings(ReadPermission.OwnerOnly));
        // Only the active slot can be reloading (swapping cancels it), so one flag covers the HUD.
        private readonly SyncVar<bool> _reloading =
            new SyncVar<bool>(new SyncTypeSettings(ReadPermission.OwnerOnly));

        // --- runtime cache (owner + host): resolved instance per slot, rebuilt on slot/upgrade change ---
        private readonly WeaponInstance[] _instances = new WeaponInstance[SlotCount];

        // --- transient timers (never synced) ---
        private readonly float[] _nextClientFire = new float[SlotCount]; // owner-side gate, per slot
        private readonly float[] _nextServerFire = new float[SlotCount]; // server-side validate, per slot
        private float _reloadEndTime;                                    // server-side reload timer (active slot)

        private InputSystem_Actions _input;
        private LineRenderer _tracer;
        private float _tracerHideTime;
        private Player.PlayerUpgrades _upgrades; // per-player damage / fire-rate scalars (task 12b)

        // ---- public surface (kept identical so the HUD / HudPublisher keeps working) ----
        public int Ammo
        {
            get
            {
                int a = _activeSlot.Value;
                return (a >= 0 && a < _slots.Count) ? _slots[a].ammo : 0;
            }
        }
        public bool IsReloading => _reloading.Value;
        /// <summary>Magazine capacity of the active weapon (0 if none) — for the ammo HUD "x / y".</summary>
        public int MagazineSize => ActiveInstance != null ? ActiveInstance.profile.magazineSize : 0;
        public Vector3 AimOrigin => _aimSource != null ? _aimSource.position : transform.position;
        public Vector3 AimDirection => _aimSource != null ? _aimSource.forward : transform.forward;

        private WeaponInstance ActiveInstance
        {
            get
            {
                int a = _activeSlot.Value;
                return (a >= 0 && a < _instances.Length) ? _instances[a] : null;
            }
        }

        private void Awake()
        {
            _upgrades = GetComponent<Player.PlayerUpgrades>();
            _slots.OnChange += HandleSlotsChanged;
            if (_upgrades != null) _upgrades.OnUpgradesChanged += RebuildAll;
        }

        private void OnDestroy()
        {
            _slots.OnChange -= HandleSlotsChanged;
            if (_upgrades != null) _upgrades.OnUpgradesChanged -= RebuildAll;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Fill the arsenal from the starting loadout (server authors the SyncList).
            _slots.Clear();
            for (int i = 0; i < SlotCount; i++)
            {
                int id = (_startingLoadout != null && i < _startingLoadout.Length) ? _startingLoadout[i] : -1;
                WeaponData template = _catalog != null ? _catalog.Get(id) : null;
                _slots.Add(template != null ? WeaponSlot.Of(id, template.magazineSize) : WeaponSlot.Empty);
            }
            _activeSlot.Value = 0;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SetupTracer();
            RebuildAll(); // list may already be synced before this runs
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

        // ---- cache rebuild (the loop that ties layer 2 → layer 3) ----

        private void HandleSlotsChanged(SyncListOperation op, int index, WeaponSlot oldItem, WeaponSlot newItem, bool asServer)
        {
            switch (op)
            {
                // A batch/structural change (initial sync, late join, buy) → rebuild everything.
                case SyncListOperation.Add:
                case SyncListOperation.Insert:
                case SyncListOperation.RemoveAt:
                case SyncListOperation.Clear:
                case SyncListOperation.Complete:
                    RebuildAll();
                    break;
                // A per-entry write: skip the rebuild on ammo-only changes (every shot), rebuild on a
                // weapon/attachment change so the profile stays correct.
                case SyncListOperation.Set:
                    if (!oldItem.SameLoadout(newItem)) RebuildInstance(index);
                    break;
            }
        }

        private void RebuildAll()
        {
            for (int i = 0; i < SlotCount; i++)
                RebuildInstance(i);
        }

        private void RebuildInstance(int index)
        {
            if (index < 0 || index >= _instances.Length || index >= _slots.Count)
            {
                if (index >= 0 && index < _instances.Length) _instances[index] = null;
                return;
            }

            WeaponSlot slot = _slots[index];
            WeaponData template = _catalog != null ? _catalog.Get(slot.weaponId) : null;
            _instances[index] = WeaponInstance.Resolve(template, slot, _attachments, _upgrades);
        }

        // ---- input / fire loop (owner) ----

        private void Update()
        {
            // Server: finish an in-progress reload (refills the active slot's magazine).
            if (IsServerInitialized && _reloading.Value && Time.time >= _reloadEndTime)
            {
                WeaponInstance inst = ActiveInstance;
                int a = _activeSlot.Value;
                if (inst != null && a >= 0 && a < _slots.Count)
                    _slots[a] = _slots[a].WithAmmo(inst.profile.magazineSize);
                _reloading.Value = false;
            }

            // Any client: hide the tracer after its brief flash.
            if (_tracer != null && _tracer.enabled && Time.time >= _tracerHideTime)
                _tracer.enabled = false;

            if (!IsOwner || _input == null) return;

            // Swap (1 = Primary, 2 = Secondary). Direct keyboard read for W1 — migrate to an input action
            // alongside the other bindings later.
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) CmdSwap(0);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) CmdSwap(1);
            }

            WeaponInstance active = ActiveInstance;
            if (active == null) return;

            if (_input.Player.Reload.WasPressedThisFrame())
                ServerReload();

            bool firePressed = active.profile.fullAuto
                ? _input.Player.Attack.IsPressed()
                : _input.Player.Attack.WasPressedThisFrame();

            if (firePressed && CanFireLocally())
            {
                int a = _activeSlot.Value;
                _nextClientFire[a] = Time.time + active.profile.cooldown;
                if (active.profile.isProjectile) SpawnProjectile();
                else FireHitscanShot(active.profile);
            }
        }

        private bool CanFireLocally()
        {
            int a = _activeSlot.Value;
            if (a < 0 || a >= _slots.Count) return false;
            return Time.time >= _nextClientFire[a] && _slots[a].ammo > 0 && !_reloading.Value;
        }

        // ---- primitives driven by the active profile (owner client) ----

        /// <summary>
        /// Owner-side hitscan for one trigger pull: fires <c>pelletCount</c> rays (shotgun spread from
        /// attachments, W3), each piercing up to <c>pierceCount</c> extra targets, and reports every hit
        /// target to the server in ONE rpc — the server consumes a single round and applies the damage
        /// (client never sends damage values).
        /// </summary>
        private void FireHitscanShot(WeaponProfile profile)
        {
            Vector3 origin = AimOrigin;
            Vector3 baseDir = AimDirection;
            Vector3 muzzle = _muzzle != null ? _muzzle.position : origin;

            int pellets = Mathf.Max(1, profile.pelletCount);
            int maxTargetsPerRay = 1 + Mathf.Max(0, profile.pierceCount);

            var targets = new List<NetworkObject>();
            var points = new List<Vector3>();

            for (int p = 0; p < pellets; p++)
            {
                Vector3 dir = pellets > 1 ? ScatterDirection(baseDir, profile.spread) : baseDir;
                Vector3 end = origin + dir * profile.range;

                // Sort hits by distance so "pierce" consumes the nearest targets first.
                RaycastHit[] hits = Physics.RaycastAll(origin, dir, profile.range, _hitMask, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

                int taken = 0;
                for (int i = 0; i < hits.Length && taken < maxTargetsPerRay; i++)
                {
                    if (hits[i].collider.GetComponentInParent<IHitReceiver>() == null)
                    {
                        // Solid geometry stops the ray.
                        end = hits[i].point;
                        break;
                    }

                    NetworkObject nob = hits[i].collider.GetComponentInParent<NetworkObject>();
                    if (nob != null && !targets.Contains(nob))
                    {
                        targets.Add(nob);
                        points.Add(hits[i].point);
                        taken++;
                    }
                    end = hits[i].point;
                }

                ShowTracerLocal(muzzle, end);
                RpcTracer(muzzle, end);
            }

            ServerReportHits(targets.ToArray(), points.ToArray(), baseDir);
        }

        // Random direction inside a cone of half-angle `spreadDegrees` around `dir`.
        private static Vector3 ScatterDirection(Vector3 dir, float spreadDegrees)
        {
            if (spreadDegrees <= 0f) return dir;
            Vector2 disc = Random.insideUnitCircle * Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(dir, right).normalized;
            return (dir + right * disc.x + up * disc.y).normalized;
        }

        /// <summary>Ask the server to spawn the active weapon's projectile from its resolved profile.</summary>
        public void SpawnProjectile()
        {
            ServerSpawnProjectile(_muzzle != null ? _muzzle.position : AimOrigin, AimDirection);
        }

        // ---- server: validate + apply (per active slot) ----

        [Server]
        private bool ServerTryConsume()
        {
            int a = _activeSlot.Value;
            if (a < 0 || a >= _slots.Count) return false;
            WeaponInstance inst = ActiveInstance;
            if (inst == null || _reloading.Value) return false;
            if (Time.time < _nextServerFire[a]) return false;

            WeaponSlot slot = _slots[a];
            if (slot.ammo <= 0) return false;

            _nextServerFire[a] = Time.time + inst.profile.cooldown;
            int remaining = slot.ammo - 1;
            _slots[a] = slot.WithAmmo(remaining);
            if (remaining <= 0)
                ServerStartReload(); // auto-reload on empty
            return true;
        }

        /// <summary>
        /// Server: consume ONE round for the trigger pull, then apply damage to every reported target
        /// (multiple = shotgun pellets / pierce) and run the weapon's effect hooks (W5).
        /// </summary>
        [ServerRpc]
        private void ServerReportHits(NetworkObject[] targets, Vector3[] points, Vector3 dir)
        {
            if (!ServerTryConsume()) return;
            if (targets == null || points == null) return;

            WeaponInstance inst = ActiveInstance;
            if (inst == null) return;
            WeaponProfile profile = inst.profile;

            int count = Mathf.Min(targets.Length, points.Length);
            for (int i = 0; i < count; i++)
            {
                NetworkObject target = targets[i];
                if (target == null) continue;
                var receiver = target.GetComponent<IHitReceiver>();
                if (receiver == null) continue;

                bool killed = receiver.ReceiveHit(new HitInfo(points[i], dir, profile.damage, 0f));

                // Effect hooks run server-side only, after the hit resolved (decision 0016, Q11).
                if (profile.effects != null && profile.effects.Length > 0)
                {
                    var ctx = new EffectContext(this, target, points[i], dir, profile.damage, killed);
                    foreach (WeaponEffect effect in profile.effects)
                    {
                        if (effect == null) continue;
                        effect.OnHitDealt(ctx);
                        if (killed) effect.OnKill(ctx);
                    }
                }
            }
        }

        /// <summary>
        /// Damage applied BY an effect (explosion / chain). Goes straight to the receiver and never runs
        /// the effect hooks again — the recursion guard from decision 0016 (explosion → hit → explosion).
        /// </summary>
        [Server]
        public void EffectDamage(NetworkObject target, Vector3 point, Vector3 dir, float damage)
        {
            if (target == null || damage <= 0f) return;
            var receiver = target.GetComponent<IHitReceiver>();
            receiver?.ReceiveHit(new HitInfo(point, dir, damage, 0f));
        }

        /// <summary>Heal the shooter (on-kill effects). Server-only.</summary>
        [Server]
        public void EffectHealShooter(float amount)
        {
            if (amount <= 0f) return;
            var health = GetComponent<Player.PlayerHealth>();
            if (health != null) health.SetHp(Mathf.Min(health.Max, health.Current + amount));
        }

        /// <summary>Top up the ACTIVE slot's magazine (on-kill effects), clamped to capacity. Server-only.</summary>
        [Server]
        public void EffectRefillAmmo(int rounds)
        {
            if (rounds <= 0) return;
            int a = _activeSlot.Value;
            WeaponInstance inst = ActiveInstance;
            if (inst == null || a < 0 || a >= _slots.Count) return;

            WeaponSlot slot = _slots[a];
            int refilled = Mathf.Min(inst.profile.magazineSize, slot.ammo + rounds);
            if (refilled != slot.ammo) _slots[a] = slot.WithAmmo(refilled);
        }

        /// <summary>Play a radial burst on every client (explosion FX placeholder).</summary>
        [ObserversRpc]
        public void RpcEffectBurst(Vector3 center, float radius)
        {
            Debug.Log($"[WeaponEffect] burst at {center} r={radius}");
        }

        /// <summary>Play a beam between two points on every client (chain-lightning FX placeholder).</summary>
        [ObserversRpc]
        public void RpcEffectBeam(Vector3 from, Vector3 to)
        {
            ShowTracerLocal(from, to);
        }

        [ServerRpc]
        private void ServerSpawnProjectile(Vector3 origin, Vector3 dir)
        {
            if (!ServerTryConsume()) return;
            WeaponInstance inst = ActiveInstance;
            if (inst == null || !inst.profile.isProjectile || inst.profile.projectilePrefab == null) return;

            NetworkObject proj = Instantiate(inst.profile.projectilePrefab, origin, Quaternion.LookRotation(dir));
            Spawn(proj);
            if (proj.TryGetComponent(out NetworkProjectile np))
                np.ServerInit(dir, inst.profile.projectileSpeed, inst.profile.damage);
        }

        // ---- shop (W4): buy a weapon into a slot / equip an attachment ----

        /// <summary>
        /// Buy <paramref name="weaponId"/> into <paramref name="slot"/>. Server validates the shop window,
        /// the catalog id and the gold, then OVERWRITES the whole slot entry — weapon + fresh magazine +
        /// cleared mods in one atomic write, so ammo can never belong to the previous gun (decision 0016).
        /// </summary>
        [ServerRpc]
        public void CmdBuyWeapon(int weaponId, int slot) => TryBuyWeapon(weaponId, slot);

        [Server]
        public bool TryBuyWeapon(int weaponId, int slot)
        {
            if (Game.GameManager.Instance == null || !Game.GameManager.Instance.ShopOpen) return false;
            if (slot < 0 || slot >= _slots.Count) return false;

            WeaponData template = _catalog != null ? _catalog.Get(weaponId) : null;
            if (template == null) return false;
            if (_slots[slot].weaponId == weaponId) return false; // already owned in that slot

            var wallet = GetComponent<Player.PlayerWallet>();
            if (wallet == null || !wallet.TrySpend(template.cost)) return false;

            _slots[slot] = WeaponSlot.Of(weaponId, template.magazineSize);
            if (slot == _activeSlot.Value) _reloading.Value = false; // fresh gun, cancel any reload
            return true;
        }

        /// <summary>
        /// Buy + equip <paramref name="attachmentId"/> into one of the slot's 3 mod sockets (0..2).
        /// Server validates shop window / ids / gold, then rewrites the entry keeping the current ammo.
        /// </summary>
        [ServerRpc]
        public void CmdEquipAttachment(int attachmentId, int slot, int modSocket) =>
            TryEquipAttachment(attachmentId, slot, modSocket);

        [Server]
        public bool TryEquipAttachment(int attachmentId, int slot, int modSocket)
        {
            if (Game.GameManager.Instance == null || !Game.GameManager.Instance.ShopOpen) return false;
            if (slot < 0 || slot >= _slots.Count) return false;
            if (modSocket < 0 || modSocket > 2) return false;

            AttachmentData mod = _attachments != null ? _attachments.Get(attachmentId) : null;
            if (mod == null) return false;

            WeaponSlot entry = _slots[slot];
            if (entry.IsEmpty) return false; // nothing to attach to

            var wallet = GetComponent<Player.PlayerWallet>();
            if (wallet == null || !wallet.TrySpend(mod.cost)) return false;

            switch (modSocket)
            {
                case 0: entry.mod0 = attachmentId; break;
                case 1: entry.mod1 = attachmentId; break;
                case 2: entry.mod2 = attachmentId; break;
            }
            _slots[slot] = entry; // struct copy back → OnChange → cache rebuild (loadout changed)
            return true;
        }

        // ---- swap + reload ----

        [ServerRpc]
        private void CmdSwap(int slot)
        {
            if (slot < 0 || slot >= _slots.Count) return;
            if (slot == _activeSlot.Value) return;
            if (_slots[slot].IsEmpty) return;

            if (_reloading.Value) _reloading.Value = false; // 7a: swapping away cancels the reload
            _activeSlot.Value = slot;
        }

        [ServerRpc]
        private void ServerReload()
        {
            WeaponInstance inst = ActiveInstance;
            int a = _activeSlot.Value;
            if (inst == null || _reloading.Value || a < 0 || a >= _slots.Count) return;
            if (_slots[a].ammo >= inst.profile.magazineSize) return;
            ServerStartReload();
        }

        [Server]
        private void ServerStartReload()
        {
            WeaponInstance inst = ActiveInstance;
            if (inst == null || _reloading.Value) return;
            _reloading.Value = true;
            _reloadEndTime = Time.time + inst.profile.reloadTime;
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
