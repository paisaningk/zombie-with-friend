using System;
using System.Collections.Generic;
using System.Linq;
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
                OnError?.Invoke("กรุณาใส่ Lobby ID");

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

                if (!string.IsNullOrEmpty(currentTransport.ConnectionAddress))
                    NetworkManager.ClientManager.StartConnection(currentTransport.ConnectionAddress);

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
            }
            else
            {
                connectedPlayers.Remove(conn);
            }

            if (!currentTransport.SupportsLobby)
                OnPlayerListChanged?.Invoke(GetPlayerList());
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Stopped) return;
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
