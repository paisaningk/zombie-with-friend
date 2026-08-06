using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Eflatun.SceneReference;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting;
using Networking.TransportProvider;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Networking
{
    
    public enum Transport
    {
        Yak = 0,
        Tugboat = 1,
        FacePunch = 2,
    }
    
    public class LobbyManager : Singleton<LobbyManager>
    {
        protected override bool dontDestroyOnLoad => true;

        [SerializeField] private List<NetworkConnection> connectedPlayers = new List<NetworkConnection>();

        [SerializeField] private ITransportProvider currentTransport;
        [SerializeField] private SteamTransportProvider SteamTransport;
        [SerializeField] private OfflineTransportProvider OfflineTransport;
        [SerializeField] private TugboatTransprotProvider TugboatTransport;

        public Transport CurrentTransportMode;
        public NetworkManager NetworkManager;
        private bool isIntentionalDisconnect = false;

        // True while OnJoinPressed is awaiting the initial connection result. While set, the
        // persistent OnClientConnectionState handler must NOT run its "dropped mid-game" logic:
        // a Stopped during a join attempt means "join failed" and is handled by the awaiter, not
        // a real disconnect (task 16b, decision C1). Backstop for the await if the connection
        // neither authenticates nor reports Stopped.
        private bool _isJoining = false;
        private const float JoinTimeoutSeconds = 10f;

        [Tooltip("Scene to return to when a connection ends while in-game (Exit to MainMenu / a dropped " +
                 "connection). No-op if already in this scene — the menu handles its own back-to-main.")]
        [SerializeField] private SceneReference _mainMenuScene;

        // How long OnCreateLobby waits for the host (server + local client) to come up
        // before reporting failure. Loopback is near-instant; this is just a safety bound.
        private const float HostStartTimeoutSeconds = 5f;


        // ============ Events แจ้ง UI ============
        public event Action<string> OnLobbyCreated; //UI แสดง code
        public event Action<string> OnLobbyJoined; // เปลี่ยนหน้าไป LobbyPanel
        public event Action<List<string>> OnPlayerListChanged; // refresh รายชื่อ
        public event Action<string> OnError; // แสดง error
        public event Action OnDisconnect; // กลับ MainMenu

        private CancellationTokenSource cts;

        // ============ Public Methods (UI เรียก) ============
        public async UniTask<bool> OnCreateLobby()
        {
            SetTransport(CurrentTransportMode);

            try
            {
                var lobbyID = await currentTransport.CreateLobby(cts.Token);

                if (string.IsNullOrEmpty(lobbyID))
                {
                    return false;
                }

                Debug.Log("Create Lobby");

                NetworkManager.ServerManager.StartConnection();
                NetworkManager.ClientManager.StartConnection(currentTransport.ConnectionAddress);

                // Wait until the host is actually up (server + local client) instead of guessing
                // with a fixed delay — the caller opens LobbyPanel right after and gates the Start
                // button on IsHostStarted. Bounded by a timeout so a failed start reports and tears
                // down cleanly rather than leaving a half-started host / hanging the caller.
                try
                {
                    await UniTask.WaitUntil(() => NetworkManager.IsHostStarted, cancellationToken: cts.Token)
                        .Timeout(TimeSpan.FromSeconds(HostStartTimeoutSeconds));
                }
                catch (TimeoutException)
                {
                    OnErrorLog("เริ่มโฮสต์ไม่สำเร็จ (timeout)");
                    NetworkManager.ClientManager.StopConnection();
                    NetworkManager.ServerManager.StopConnection(true);
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);

                return false;
            }
        }

        public async UniTask<bool> OnJoinPressed(string lobbyID)
        {
            SetTransport(CurrentTransportMode);

            if (string.IsNullOrEmpty(lobbyID))
            {
                OnError?.Invoke("กรุณาใส่ IP");

                return false;
            }

            try
            {
                var success = await currentTransport.JoinLobby(lobbyID, cts.Token);

                if (!success)
                {
                    OnError?.Invoke("เข้าห้องไม่สำเร็จ");
                    return false;
                }

                var address = currentTransport.ConnectionAddress;
                if (string.IsNullOrEmpty(address))
                {
                    OnError?.Invoke("ไม่พบที่อยู่เชื่อมต่อ");
                    return false;
                }

                // await-real: wait for a genuine connection result before reporting success, so the
                // caller doesn't flash the LobbyPanel and bounce back on failure (the common path on
                // a real network — wrong IP / host not up). OnAuthenticated = fully joined; a Stopped
                // (or the backstop timeout) = failed (task 16b, decision C1).
                var joined = await AwaitClientJoin(address, cts.Token);
                if (!joined)
                {
                    // Tear down any half-open attempt (Starting state never reaches Started).
                    NetworkManager.ClientManager.StopConnection();
                    OnError?.Invoke("เชื่อมต่อไม่สำเร็จ — ตรวจสอบ IP หรือ host อาจยังไม่เปิด");
                    return false;
                }

                OnLobbyJoined?.Invoke(lobbyID);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Starts the client connection and awaits a real result: true on OnAuthenticated (fully
        /// joined), false on transport Stopped or the backstop timeout. Subscribes BEFORE
        /// StartConnection so a fast loopback auth can't fire before the awaiter is wired
        /// (task 16b, decision C1 — success-race guard flagged in review).
        /// </summary>
        private async UniTask<bool> AwaitClientJoin(string address, CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource<bool>();

            void OnAuthenticated() => tcs.TrySetResult(true);
            void OnState(ClientConnectionStateArgs a)
            {
                if (a.ConnectionState == LocalConnectionState.Stopped)
                    tcs.TrySetResult(false);
            }

            _isJoining = true;
            NetworkManager.ClientManager.OnAuthenticated += OnAuthenticated;
            NetworkManager.ClientManager.OnClientConnectionState += OnState;

            try
            {
                NetworkManager.ClientManager.StartConnection(address);
                return await tcs.Task
                    .AttachExternalCancellation(ct)
                    .Timeout(TimeSpan.FromSeconds(JoinTimeoutSeconds));
            }
            catch (TimeoutException)
            {
                return false;
            }
            finally
            {
                NetworkManager.ClientManager.OnAuthenticated -= OnAuthenticated;
                NetworkManager.ClientManager.OnClientConnectionState -= OnState;
                _isJoining = false;
            }
        }

        public async UniTask QuickJoin()
        {
            if (currentTransport == null)
            {
                SetTransport(CurrentTransportMode);
            }

            await OnJoinPressed(GetCode());
        }

        public void OnCancelPressed()
        {
            cts.Cancel();
            cts = new CancellationTokenSource();
        }

        private void SetTransport(Transport transport)
        {
            currentTransport = transport switch
            {
                Transport.Yak => OfflineTransport,
                Transport.Tugboat => TugboatTransport,
                Transport.FacePunch => SteamTransport,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        // ============ Wrappers (ข้อมูล lobby) ============
        public string GetLobbyName()
        {
            return currentTransport?.LobbyName ?? string.Empty;
        }

        [Button]
        public bool IsHost()
        {
            return NetworkManager.IsHostStarted;
        }

        public string GetCode()
        {
            return currentTransport.ConnectionAddress;
        }

        /// <summary>
        /// Best-guess LAN IPv4 for the host to read out / copy so a friend can connect. This is a
        /// DISPLAY value only — the host still self-connects on loopback (127.0.0.1). Logs every
        /// candidate so a wrong guess (VPN / Docker / virtual adapter) is diagnosable, and the host
        /// can override the value in the editable field (task 16b, decision B3).
        /// </summary>
        public string GetHostDisplayAddress()
        {
            var candidates = ResolveLocalIPv4Candidates();
            if (candidates.Count == 0)
            {
                Debug.LogWarning("[Lobby] No LAN IPv4 found — falling back to 127.0.0.1 (LAN join won't work).");
                return "127.0.0.1";
            }

            string best = candidates[0];
            Debug.Log($"[Lobby] LAN IPv4 candidates: {string.Join(", ", candidates)} → using {best}");
            return best;
        }

        // Enumerates up interfaces for private IPv4 addresses, ranked so the real LAN adapter comes
        // first. Key signal: a default GATEWAY — the adapter actually attached to the router/network
        // has one; Hyper-V/WSL/VMware virtual switches don't, even though they report as Ethernet and
        // sit in 192.168.* (verified on this dev box: vEthernet "Internal Switch" tied the real NIC on
        // type+range and only the gateway broke the tie). Then private range and real adapter type.
        // Excludes loopback, tunnel, link-local (169.254.*) and IPv6.
        private List<string> ResolveLocalIPv4Candidates()
        {
            var scored = new List<(string ip, int score)>();

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                var props = ni.GetIPProperties();

                bool hasGateway = props.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.ToString().StartsWith("0."));

                bool realAdapter = ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                   ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;

                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue; // IPv4 only
                    string ip = ua.Address.ToString();
                    if (ip.StartsWith("169.254")) continue; // APIPA / link-local

                    int score = 0;
                    if (hasGateway) score += 10; // dominant: this NIC is on the real network
                    if (ip.StartsWith("192.168.")) score += 3;
                    else if (ip.StartsWith("10.")) score += 2;
                    else if (IsPrivate172(ip)) score += 1;
                    if (realAdapter) score += 1;

                    scored.Add((ip, score));
                }
            }

            return scored.OrderByDescending(c => c.score).Select(c => c.ip).ToList();
        }

        private static bool IsPrivate172(string ip)
        {
            // 172.16.0.0 – 172.31.255.255
            if (!ip.StartsWith("172.")) return false;
            var parts = ip.Split('.');
            return parts.Length == 4 && int.TryParse(parts[1], out int second) && second >= 16 && second <= 31;
        }

        // ============ Private ============
        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            NetworkManager.ServerManager.OnRemoteConnectionState += OnPlayerConnected;
            NetworkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;


            // subscribe Steam transport events
            SteamTransport.OnPlayerListChanged += HandlePlayerListChanged;
            SteamTransport.OnDisconnect += HandleTransportDisconnect;
            SteamTransport.OnError += OnErrorLog;
        }

        private void OnDisable()
        {
            NetworkManager.ServerManager.OnRemoteConnectionState -= OnPlayerConnected;
            NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            SteamTransport.OnPlayerListChanged -= HandlePlayerListChanged;
            SteamTransport.OnDisconnect -= HandleTransportDisconnect;
            SteamTransport.OnError -= OnErrorLog;

            cts?.Cancel();
            cts?.Dispose(); // ← เพิ่ม dispose
            cts = null;
        }

        private void OnPlayerConnected(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (currentTransport == null) return;
            
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                connectedPlayers.Add(conn);
                // Host-side visibility that a client arrived. The menu lobby has no synced roster
                // (coordination lives in in-game staging, decision 0013) — this log is the host's
                // signal during Direct-IP testing (task 16b, decision H1).
                Debug.Log($"[Lobby] Client connected (ClientId {conn.ClientId}) — total {connectedPlayers.Count}");
            }
            else
            {
                connectedPlayers.Remove(conn);
                Debug.Log($"[Lobby] Client disconnected (ClientId {conn.ClientId}) — total {connectedPlayers.Count}");
            }

            if (!currentTransport.SupportsLobby)
                OnPlayerListChanged?.Invoke(GetPlayerList());
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;

            // A Stopped during an in-flight join is a failed connection, handled by AwaitClientJoin —
            // not a mid-game drop. Skip the drop/return-to-menu logic so we don't double-fire error
            // and OnDisconnect while the joiner is still deciding (task 16b, decision C1).
            if (_isJoining) return;

            if (isIntentionalDisconnect)
            {
                isIntentionalDisconnect = false;
                return;
            }

            // หลุดเอง (ไม่ได้ตั้งใจ) — optimistic join พาเข้าห้องไปก่อน ถ้าต่อไม่ติด/หลุด
            // ต้องแจ้ง error ไม่ให้เด้งกลับเมนูเงียบๆ ใช้ OnErrorLog เพื่อให้มี Debug.LogError จริง
            // (ยิง OnError ให้ UI ที่ subscribe ด้วย — ตอนนี้ยังไม่มี element แสดง ค่อยต่อทีหลัง)
            connectedPlayers.Clear();
            currentTransport?.Disconnect();
            OnErrorLog("การเชื่อมต่อหลุด");
            OnDisconnect?.Invoke();
            ReturnToMainMenuIfInGame(); // dropped mid-game → don't strand the player in the game scene
        }

        public void HandleTransportDisconnect()
        {
            isIntentionalDisconnect = true;

            if (IsHost())
                NetworkManager.ServerManager.StopConnection(true);
            else
                NetworkManager.ClientManager.StopConnection();

            currentTransport?.Disconnect();
            connectedPlayers.Clear();
            OnDisconnect?.Invoke();
            ReturnToMainMenuIfInGame(); // Exit to MainMenu from the in-game result screen (task 15)
        }

        /// <summary>
        /// Load the MainMenu scene locally after a connection ends while in the game scene (task 15 —
        /// Exit to MainMenu, and any dropped connection). Networking is already down here, so this is a
        /// plain single-scene load — NOT a FishNet networked load. No-op when already in the menu (the
        /// menu drives its own panels via OnDisconnect), so leaving the lobby doesn't reload the scene.
        /// </summary>
        private void ReturnToMainMenuIfInGame()
        {
            if (_mainMenuScene == null || string.IsNullOrEmpty(_mainMenuScene.Name)) return;

            string menu = _mainMenuScene.Name;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == menu) return;

            UnityEngine.SceneManagement.SceneManager.LoadScene(menu);
        }

        private void HandlePlayerListChanged(List<string> players)
        {
            OnPlayerListChanged?.Invoke(players);
        }


        private List<string> GetPlayerList()
        {
            if (currentTransport.SupportsLobby)
                return currentTransport.GetPlayersInLobby();

            return connectedPlayers.Select(c => $"Player {c.ClientId}").ToList();
        }

        public void OnErrorLog(string error)
        {
            OnError?.Invoke(error);
            Debug.LogError(error);
        }
    }
}
