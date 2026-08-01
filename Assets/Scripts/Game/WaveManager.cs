using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Enemies;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Player;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Server-authoritative wave progression (see decision 0009). Lives as a scene NetworkObject
    /// alongside <see cref="GameManager"/> (bundle prefab "GameManagers") — torn down with the game
    /// scene, like GameManager.
    ///
    /// Drives the whole match loop on the server via UniTask: wait for Playing → prep delay → for
    /// each wave { resolve a spawn queue (guaranteed minimums + weighted-random fill, shuffled),
    /// trickle enemies in on an interval never exceeding maxAlive (a death frees a slot), wait until
    /// cleared, revive everyone } → <see cref="GameManager.SetGameState"/>(Won) after the last wave.
    /// If the match is Lost mid-wave the loop is cancelled and remaining enemies are despawned.
    ///
    /// <see cref="CurrentWave"/> is synced (1-based; 0 = not started) for a future HUD / result
    /// screen; consumers subscribe to <see cref="OnWaveChanged"/> like GameManager's state event.
    /// </summary>
    public class WaveManager : NetworkBehaviour
    {
        [Header("Data")]
        [SerializeField] private WaveData _waves;
        [Tooltip("Fixed enemy spawn points in THIS scene (arena geometry). Picked at random per spawn.")]
        [SerializeField] private Transform[] _spawnPoints = Array.Empty<Transform>();

        [Header("Pacing")]
        [Tooltip("Seconds after the match becomes Playing before wave 1 starts.")]
        [SerializeField] private float _prepDelay = 5f;
        [Tooltip("Seconds between a wave clearing (post-revive) and the next wave.")]
        [SerializeField] private float _interWaveDelay = 3f;

        [Header("Economy")]
        [Tooltip("Gold each player still Alive at wave clear receives (awarded BEFORE the revive).")]
        [SerializeField] private int _surviveBonus = 200;

        // Synced 1-based wave number (0 = not started). Consumers react via OnWaveChanged.
        private readonly SyncVar<int> _currentWave = new SyncVar<int>();

        /// <summary>(previous, next) wave number. Fires once on start (prev == next), then per change.</summary>
        public event Action<int, int> OnWaveChanged;

        private int _notifiedWave;
        private bool _hasNotifiedWave;

        public int CurrentWave => _currentWave.Value;
        public int WaveCount => _waves != null ? _waves.WaveCount : 0;

        // --- server-only runtime state ---
        private readonly List<Enemy> _alive = new List<Enemy>();
        private CancellationTokenSource _cts;
        private GameManager _gm;

        private void Awake()
        {
            _currentWave.OnChange += HandleWaveSync;
        }

        private void OnDestroy()
        {
            _currentWave.OnChange -= HandleWaveSync;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            NotifyWave(_currentWave.Value); // initial fire on every peer
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _cts = new CancellationTokenSource();
            RunAsync(_cts.Token).Forget();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_cts != null) { _cts.Cancel(); _cts.Dispose(); _cts = null; }
            if (_gm != null) { _gm.OnGameStateChanged -= HandleGameState; _gm = null; }
        }

        // ---- the match loop (server) ----

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            try
            {
                // Wait for GameManager, subscribe (to catch Lost), then wait until the match is live.
                await UniTask.WaitUntil(() => GameManager.Instance != null, cancellationToken: ct);
                _gm = GameManager.Instance;
                _gm.OnGameStateChanged += HandleGameState;

                await UniTask.WaitUntil(() => _gm.State == GameState.Playing, cancellationToken: ct);

                if (_waves == null || _waves.WaveCount == 0)
                {
                    Debug.LogWarning("[WaveManager] No WaveData assigned or it has no waves — nothing to run.");
                    return;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(_prepDelay), cancellationToken: ct);

                for (int i = 0; i < _waves.WaveCount; i++)
                {
                    SetCurrentWave(i + 1); // 1-based
                    await RunWave(_waves.waves[i], ct);

                    if (_gm.State != GameState.Playing) return; // ended mid-wave (shouldn't reach here; Lost cancels)
                    _gm.AwardSurvivalBonus(_surviveBonus); // reward survivors BEFORE revive resurrects everyone
                    ReviveAll();
                    await UniTask.Delay(TimeSpan.FromSeconds(_interWaveDelay), cancellationToken: ct);
                }

                _gm.SetGameState(GameState.Won);
            }
            catch (OperationCanceledException)
            {
                // Cancelled by scene teardown or a Lost match — expected, nothing to do.
            }
        }

        private async UniTask RunWave(Wave wave, CancellationToken ct)
        {
            Queue<NetworkObject> queue = BuildSpawnQueue(wave);
            int maxAlive = Mathf.Max(1, wave.maxAlive);
            float interval = Mathf.Max(0.01f, wave.spawnInterval);

            while (queue.Count > 0 || _alive.Count > 0)
            {
                // Refill up to the concurrency cap from the remaining budget.
                while (_alive.Count < maxAlive && queue.Count > 0)
                {
                    SpawnEnemy(queue.Dequeue());
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
                }

                // Either the cap is full (wait for a death to free a slot) or the budget is spent
                // (wait for the board to clear). Both break as soon as _alive shrinks appropriately.
                if (queue.Count > 0)
                    await UniTask.WaitUntil(() => _alive.Count < maxAlive, cancellationToken: ct);
                else
                    await UniTask.WaitUntil(() => _alive.Count == 0, cancellationToken: ct);
            }
        }

        // ---- spawn-queue resolution ----

        private static Queue<NetworkObject> BuildSpawnQueue(Wave wave)
        {
            var list = new List<NetworkObject>();
            if (wave.pool != null)
            {
                // 1. guaranteed minimums
                foreach (WeightedEnemy e in wave.pool)
                {
                    if (e == null || e.prefab == null) continue;
                    for (int k = 0; k < e.minCount; k++) list.Add(e.prefab);
                }

                // 2. fill the rest of the budget with weighted-random draws (minimums win if over budget)
                int remaining = wave.totalCount - list.Count;
                for (int i = 0; i < remaining; i++)
                {
                    NetworkObject prefab = WeightedPick(wave.pool);
                    if (prefab == null) break; // no weighted candidates
                    list.Add(prefab);
                }
            }

            Shuffle(list); // 3. randomize spawn order
            return new Queue<NetworkObject>(list);
        }

        private static NetworkObject WeightedPick(WeightedEnemy[] pool)
        {
            float total = 0f;
            foreach (WeightedEnemy e in pool)
                if (e != null && e.prefab != null && e.weight > 0f) total += e.weight;
            if (total <= 0f) return null;

            float r = UnityEngine.Random.value * total;
            foreach (WeightedEnemy e in pool)
            {
                if (e == null || e.prefab == null || e.weight <= 0f) continue;
                r -= e.weight;
                if (r <= 0f) return e.prefab;
            }
            return null;
        }

        private static void Shuffle(List<NetworkObject> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // ---- spawning ----

        private void SpawnEnemy(NetworkObject prefab)
        {
            if (prefab == null) return;
            GetSpawnPose(out Vector3 pos, out Quaternion rot);

            NetworkObject nob = Instantiate(prefab, pos, rot);
            Spawn(nob); // server-owned (no owner)
            if (nob.TryGetComponent(out Enemy enemy))
            {
                _alive.Add(enemy);
                enemy.OnDied += HandleEnemyDied;
            }
        }

        private void GetSpawnPose(out Vector3 position, out Quaternion rotation)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                Transform p = _spawnPoints[UnityEngine.Random.Range(0, _spawnPoints.Length)];
                if (p != null) { position = p.position; rotation = p.rotation; return; }
            }
            position = transform.position;
            rotation = Quaternion.identity;
        }

        private void HandleEnemyDied(Enemy e)
        {
            if (e != null) e.OnDied -= HandleEnemyDied;
            _alive.Remove(e);
            if (e != null && _gm != null)
                _gm.AwardGoldForKill(e.GoldReward); // split equally among all players (task 11)
        }

        // ---- between-wave revive ----

        /// <summary>
        /// Reset every player to Alive + full HP (the seam task 12 replaces with shop/ready). Downed
        /// and Dead players come back; already-Alive players are topped up. Guarded to Playing so a
        /// Lost match never revives (Lost cancels the loop before this runs, but belt-and-suspenders).
        /// </summary>
        private void ReviveAll()
        {
            if (_gm == null || _gm.State != GameState.Playing) return;

            IReadOnlyList<PlayerController> players = _gm.Players;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController pc = players[i];
                if (pc == null) continue;
                if (pc.State != null) pc.State.SetState(PlayerLifeState.Alive);
                if (pc.Health != null) pc.Health.SetHp(pc.Health.Max);
            }
        }

        // ---- Lost handling ----

        private void HandleGameState(GameState prev, GameState next)
        {
            if (!IsServerInitialized) return;
            if (next != GameState.Lost) return;

            _cts?.Cancel(); // break the loop out of its awaits
            DespawnRemaining();
        }

        private void DespawnRemaining()
        {
            for (int i = 0; i < _alive.Count; i++)
            {
                Enemy e = _alive[i];
                if (e == null) continue;
                e.OnDied -= HandleEnemyDied;
                e.DespawnByManager(); // silent (no OnDied) — the match is over
            }
            _alive.Clear();
        }

        // ---- currentWave SyncVar → event plumbing (mirrors GameManager) ----

        [Server]
        private void SetCurrentWave(int wave)
        {
            if (_currentWave.Value == wave) return;
            _currentWave.Value = wave;
        }

        private void HandleWaveSync(int prev, int next, bool asServer) => NotifyWave(next);

        private void NotifyWave(int next)
        {
            if (_hasNotifiedWave && _notifiedWave == next) return;

            int prev = _hasNotifiedWave ? _notifiedWave : next; // initial fire => prev == next
            _notifiedWave = next;
            _hasNotifiedWave = true;
            OnWaveChanged?.Invoke(prev, next);
        }
    }
}
