using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Connection;
using FishNet.Transporting;
using Networking.TransportProvider;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Networking
{
    
    public enum Transport
    {
        Yak            = 0,
        Tugboat        = 1,
        FacePunch = 2
    }
    
    public class LobbyManager : Singleton<LobbyManager>
    {
        protected override bool dontDestroyOnLoad => false;

        private List<NetworkConnection> connectedPlayers = new();

        [SerializeField] private ITransportProvider currentTransport;
        [SerializeField] private SteamTransportProvider steamTransport;
        [SerializeField] private OfflineTransportProvider offlineTransport;
        [SerializeField] private TugboatTransprotProvider tugboatTransport;

        public Transport CurrentTransportMode;
        private bool _isIntentionalDisconnect = false;


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

                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection(currentTransport.ConnectionAddress);

                Debug.Log(lobbyID);

                OnLobbyCreated?.Invoke(lobbyID);

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

        public async UniTask OnJoinPressed(string lobbyID)
        {
            SetTransport(CurrentTransportMode);
            
            if (string.IsNullOrEmpty(lobbyID))
            {
                OnError?.Invoke("กรุณาใส่ Lobby ID");
                
                return;
            }

            try
            {
                var success = await currentTransport.JoinLobby(lobbyID, cts.Token);

                if (!success)
                {
                    OnError?.Invoke("เข้าห้องไม่สำเร็จ");
                    return;
                }

                if (!string.IsNullOrEmpty(currentTransport.ConnectionAddress))
                    InstanceFinder.ClientManager.StartConnection(currentTransport.ConnectionAddress);

                OnLobbyJoined?.Invoke(lobbyID);
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
            }
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
                Transport.Yak => offlineTransport,
                Transport.Tugboat => tugboatTransport,
                Transport.FacePunch => steamTransport,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        // ============ Wrappers (ข้อมูล lobby) ============
        public string GetLobbyID() => currentTransport?.LobbyId ?? string.Empty;

        [Button]
        public bool IsHost()
        {
            return InstanceFinder.IsHostStarted;
        }

        // ============ Private ============
        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnPlayerConnected;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;


            // subscribe Steam transport events
            steamTransport.OnPlayerListChanged += HandlePlayerListChanged;
            steamTransport.OnDisconnect += HandleTransportDisconnect;
            steamTransport.OnError += OnErrorLog;
        }

        private void OnDisable()
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnPlayerConnected;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;

            steamTransport.OnPlayerListChanged -= HandlePlayerListChanged;
            steamTransport.OnDisconnect -= HandleTransportDisconnect;
            steamTransport.OnError -= OnErrorLog;

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
            if (_isIntentionalDisconnect)
            {
                _isIntentionalDisconnect = false;
                return;
            }

            // หลุดเอง
            connectedPlayers.Clear();
            currentTransport?.Disconnect();
            OnDisconnect?.Invoke();
        }

        public void HandleTransportDisconnect()
        {
            _isIntentionalDisconnect = true;

            if (IsHost())
                InstanceFinder.ServerManager.StopConnection(true);
            else
                InstanceFinder.ClientManager.StopConnection();

            currentTransport?.Disconnect();
            connectedPlayers.Clear();
            OnDisconnect?.Invoke();
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
