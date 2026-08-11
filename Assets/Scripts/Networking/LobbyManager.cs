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
using FishNet.Transporting.Tugboat;
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

        // ============ Lobby roster (task L1, decision 0019) ============

        /// <summary>
        /// Room capacity. Drives BOTH the "x/4" the UI prints and the transport's real refusal point
        /// (see <see cref="ApplyMaxPlayersToTransport"/>) — a single constant so the label can never
        /// promise a capacity the server doesn't enforce.
        /// </summary>
        public const int MaxPlayers = 4;

        public const int MaxNameLength = 16;
        private const string PlayerNamePrefsKey = "player.displayName";

        // Server-side: ClientId → sanitized display name, populated by PlayerNameBroadcast.
        private readonly Dictionary<int, string> serverNames = new Dictionary<int, string>();

        // Last roster this peer knows about. On the server it is the authored snapshot; on a client
        // it is whatever the server last broadcast. Survives the menu→game scene load because this
        // manager is dontDestroyOnLoad — which is what lets in-game staging show real names.
        private LobbyRosterEntry[] roster = Array.Empty<LobbyRosterEntry>();
        private int rosterMaxPlayers = MaxPlayers;

        // Set by LobbyClosedBroadcast so the disconnect that follows can be reported as "host closed
        // the room" rather than the generic "connection lost". Consumed once, by the menu.
        private string pendingDisconnectReason;

        public IReadOnlyList<LobbyRosterEntry> Roster => roster;
        public int RosterMaxPlayers => rosterMaxPlayers;

        /// <summary>Fires on every roster change, on server and client alike. UI subscribes to this.</summary>
        public event Action OnRosterChanged;

        /// <summary>
        /// Display name for a ClientId, from the last roster this peer received. Falls back to
        /// "Player {id}" so a row is never blank — used by the menu roster and by in-game staging.
        /// </summary>
        public string GetPlayerName(int clientId)
        {
            foreach (LobbyRosterEntry entry in roster)
            {
                if (entry.ClientId == clientId)
                    return entry.Name;
            }

            return FallbackName(clientId);
        }

        /// <summary>
        /// Reads and clears the reason the last connection ended. Empty when the player left on
        /// purpose or nothing has gone wrong — the menu shows a notice only for a non-empty reason.
        /// </summary>
        public string ConsumeDisconnectReason()
        {
            string reason = pendingDisconnectReason;
            pendingDisconnectReason = null;
            return reason;
        }

        // ---- local player name (PlayerPrefs) ----

        public static string GetLocalPlayerName() => SanitizeName(PlayerPrefs.GetString(PlayerNamePrefsKey, string.Empty));

        public static void SetLocalPlayerName(string name)
        {
            PlayerPrefs.SetString(PlayerNamePrefsKey, SanitizeName(name));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Trims, length-caps, and strips angle brackets. The bracket strip matters: roster rows are
        /// TMP labels, so an unsanitized name could inject rich-text tags and repaint other players'
        /// rows. Applied on the server too — never trust the client's copy.
        /// </summary>
        public static string SanitizeName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            string trimmed = raw.Trim().Replace("<", string.Empty).Replace(">", string.Empty);
            if (trimmed.Length > MaxNameLength)
                trimmed = trimmed.Substring(0, MaxNameLength);

            return trimmed;
        }

        private static string FallbackName(int clientId) => $"Player {clientId}";

        private string ResolveName(int clientId)
        {
            return serverNames.TryGetValue(clientId, out string stored) && !string.IsNullOrEmpty(stored)
                ? stored
                : FallbackName(clientId);
        }

        // ---- roster broadcast plumbing ----

        /// <summary>
        /// Caps the transport at <see cref="MaxPlayers"/> connections. Must run BEFORE
        /// ServerManager.StartConnection — Tugboat passes its value into the socket at start.
        ///
        /// Tugboat counts client SOCKETS, and under loopback the host's own client is a real socket,
        /// so this value is total players including the host, not "host + N guests".
        /// </summary>
        private void ApplyMaxPlayersToTransport()
        {
            var tugboat = NetworkManager.TransportManager.GetTransport<Tugboat>();
            if (tugboat == null)
            {
                Debug.LogWarning("[Lobby] No Tugboat transport found — room capacity is NOT enforced.");
                return;
            }

            tugboat.SetMaximumClients(MaxPlayers);
        }

        /// <summary>
        /// Rebuilds the roster from FishNet's own connection table and pushes it to every client.
        /// Called on connect, disconnect, and on receiving a name — the last one is what makes a row
        /// that first rendered as "Player 2" self-correct to the real name instead of staying wrong.
        /// </summary>
        private void RebuildAndBroadcastRoster()
        {
            if (NetworkManager == null || !NetworkManager.ServerManager.Started) return;

            int hostId = ResolveHostClientId();
            var entries = new List<LobbyRosterEntry>();

            // Read ServerManager.Clients rather than our own connectedPlayers list: it is FishNet's
            // authoritative table, so the roster can't drift from who is actually connected.
            foreach (var pair in NetworkManager.ServerManager.Clients)
            {
                if (pair.Value == null) continue;

                entries.Add(new LobbyRosterEntry
                {
                    ClientId = pair.Key,
                    Name = ResolveName(pair.Key),
                    IsHost = pair.Key == hostId,
                });
            }

            // Safety net: do NOT assume the host's own loopback client appears as a server connection.
            // If it ever doesn't, a two-player room would render "1/4" with no HOST row — exactly the
            // bug this feature exists to kill. Adding it explicitly is correct either way.
            if (hostId >= 0 && !entries.Exists(e => e.ClientId == hostId))
            {
                entries.Add(new LobbyRosterEntry
                {
                    ClientId = hostId,
                    Name = ResolveName(hostId),
                    IsHost = true,
                });
            }

            // Host first, then join order, so rows don't reshuffle as players come and go.
            entries.Sort((a, b) => a.IsHost != b.IsHost
                ? (a.IsHost ? -1 : 1)
                : a.ClientId.CompareTo(b.ClientId));

            roster = entries.ToArray();
            rosterMaxPlayers = MaxPlayers;

            // Fire locally as well as broadcasting: on a host the two are the same process, and if the
            // host client is only present via the safety net above the broadcast would never reach it.
            // Refreshing the roster UI is idempotent, so the duplicate fire is harmless.
            OnRosterChanged?.Invoke();

            NetworkManager.ServerManager.Broadcast(new LobbyRosterBroadcast
            {
                Entries = roster,
                MaxPlayers = MaxPlayers,
            });
        }

        /// <summary>ClientId of the host's own client, or -1 when this peer isn't a host.</summary>
        private int ResolveHostClientId()
        {
            if (NetworkManager == null || !NetworkManager.IsHostStarted) return -1;

            NetworkConnection local = NetworkManager.ClientManager.Connection;
            return local != null && local.ClientId >= 0 ? local.ClientId : -1;
        }

        // Client side: announce our name the moment the connection is live. The server may already
        // have broadcast a roster listing us by fallback name; it re-broadcasts on receipt.
        // Server side: the connection is authenticated by now (FishNet fires this after SendAuthenticated),
        // so a broadcast actually reaches it — including the newcomer itself.
        private void OnServerAuthenticationResult(NetworkConnection conn, bool authenticated)
        {
            if (authenticated) RebuildAndBroadcastRoster();
        }

        private void OnClientAuthenticated()
        {
            NetworkManager.ClientManager.Broadcast(new PlayerNameBroadcast { Name = GetLocalPlayerName() });
        }

        // Channel is fully qualified throughout: UniTask ships a Cysharp.Threading.Tasks.Channel and
        // both namespaces are imported here.
        private void OnServerReceiveName(NetworkConnection conn, PlayerNameBroadcast msg,
            FishNet.Transporting.Channel channel)
        {
            serverNames[conn.ClientId] = SanitizeName(msg.Name);
            RebuildAndBroadcastRoster();
        }

        private void OnClientReceiveRoster(LobbyRosterBroadcast msg, FishNet.Transporting.Channel channel)
        {
            roster = msg.Entries ?? Array.Empty<LobbyRosterEntry>();
            rosterMaxPlayers = msg.MaxPlayers > 0 ? msg.MaxPlayers : MaxPlayers;

            // Logged because this line arriving with the right count is the proof that FishNet's
            // codegen produced a working serializer for LobbyRosterEntry[] — the highest-risk part of
            // the roster (decision 0019). A client whose roster is empty checks here first.
            Debug.Log($"[Lobby] roster received over the wire — {roster.Length}/{rosterMaxPlayers} entries");

            OnRosterChanged?.Invoke();
        }

        private void OnClientReceiveLobbyClosed(LobbyClosedBroadcast msg, FishNet.Transporting.Channel channel)
        {
            // Arrives just before the disconnect packet; the Stopped handler below reads it.
            // English because the TMP font asset in this project has no Thai glyphs — Thai renders as
            // tofu boxes on the menu notice (verified in the L1 play test, decision 0019).
            pendingDisconnectReason = "Host closed the lobby";
        }

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

                // Before StartConnection — Tugboat hands its cap to the socket at start time, so a
                // later change wouldn't apply to this session (task L1).
                ApplyMaxPlayersToTransport();

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

                // Seed the roster so the host sees its own row immediately, without waiting for the
                // name round-trip to come back and rebuild it.
                RebuildAndBroadcastRoster();

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

            // Roster broadcasts are registered HERE, not on the lobby panel: this manager is
            // dontDestroyOnLoad, so no handler can dangle when ReplaceOption.All destroys the menu
            // scene mid-match-start (task L1, decision 0019).
            NetworkManager.ServerManager.OnAuthenticationResult += OnServerAuthenticationResult;
            NetworkManager.ServerManager.RegisterBroadcast<PlayerNameBroadcast>(OnServerReceiveName);
            NetworkManager.ClientManager.RegisterBroadcast<LobbyRosterBroadcast>(OnClientReceiveRoster);
            NetworkManager.ClientManager.RegisterBroadcast<LobbyClosedBroadcast>(OnClientReceiveLobbyClosed);
            NetworkManager.ClientManager.OnAuthenticated += OnClientAuthenticated;

            // subscribe Steam transport events
            SteamTransport.OnPlayerListChanged += HandlePlayerListChanged;
            SteamTransport.OnDisconnect += HandleTransportDisconnect;
            SteamTransport.OnError += OnErrorLog;
        }

        private void OnDisable()
        {
            NetworkManager.ServerManager.OnRemoteConnectionState -= OnPlayerConnected;
            NetworkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            NetworkManager.ServerManager.OnAuthenticationResult -= OnServerAuthenticationResult;
            NetworkManager.ServerManager.UnregisterBroadcast<PlayerNameBroadcast>(OnServerReceiveName);
            NetworkManager.ClientManager.UnregisterBroadcast<LobbyRosterBroadcast>(OnClientReceiveRoster);
            NetworkManager.ClientManager.UnregisterBroadcast<LobbyClosedBroadcast>(OnClientReceiveLobbyClosed);
            NetworkManager.ClientManager.OnAuthenticated -= OnClientAuthenticated;

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
                Debug.Log($"[Lobby] Client connected (ClientId {conn.ClientId}) — total {connectedPlayers.Count}");
            }
            else
            {
                connectedPlayers.Remove(conn);
                serverNames.Remove(conn.ClientId); // drop the name with the connection, so a reused id can't inherit it
                Debug.Log($"[Lobby] Client disconnected (ClientId {conn.ClientId}) — total {connectedPlayers.Count}");
            }

            if (!currentTransport.SupportsLobby)
                OnPlayerListChanged?.Invoke(GetPlayerList());

            // Only rebuild on DISCONNECT here. A connection that just Started has not authenticated
            // yet, and Broadcast skips unauthenticated connections (and logs a warning for each one) —
            // so broadcasting now would never reach the very client that just arrived. The join-side
            // rebuild happens in OnServerAuthenticationResult instead (task L1).
            if (args.ConnectionState != RemoteConnectionState.Started)
                RebuildAndBroadcastRoster();
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
            ClearRoster();
            currentTransport?.Disconnect();

            // A LobbyClosedBroadcast may have set a specific reason moments ago; only fall back to
            // the generic wording when the host didn't tell us why (task L1, decision 0019).
            if (string.IsNullOrEmpty(pendingDisconnectReason))
                pendingDisconnectReason = "Connection lost";

            OnErrorLog(pendingDisconnectReason);
            OnDisconnect?.Invoke();
            ReturnToMainMenuIfInGame(); // dropped mid-game → don't strand the player in the game scene
        }

        public void HandleTransportDisconnect()
        {
            isIntentionalDisconnect = true;

            if (IsHost())
            {
                // Tell the guests why they are about to be dropped, so they get "host closed the room"
                // instead of "connection lost". Queued before StopConnection on purpose: that call ends
                // in IterateOutgoing, which flushes this message together with the disconnect packet.
                // Excludes our own client — the host doesn't need to be told it closed its own room.
                NetworkManager.ServerManager.BroadcastExcept(NetworkManager.ClientManager.Connection,
                    new LobbyClosedBroadcast { Reason = 0 });

                NetworkManager.ServerManager.StopConnection(true);
            }
            else
            {
                NetworkManager.ClientManager.StopConnection();
            }

            currentTransport?.Disconnect();
            connectedPlayers.Clear();
            ClearRoster();
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

        private void ClearRoster()
        {
            serverNames.Clear();
            roster = Array.Empty<LobbyRosterEntry>();
            OnRosterChanged?.Invoke();
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
