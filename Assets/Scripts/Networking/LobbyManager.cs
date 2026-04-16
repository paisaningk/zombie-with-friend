using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet;
using Networking.TransportProvider;
using Sirenix.OdinInspector;
using UnityEngine;
namespace Networking
{
    public class LobbyManager : Singleton<LobbyManager>
    {
        protected override bool dontDestroyOnLoad => false;

        [SerializeField] private SteamTransportProvider Transport;

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
            try
            {
                var lobbyID = await Transport.CreateLobby(cts.Token);

                if (string.IsNullOrEmpty(lobbyID))
                {
                    return false;
                }

                Debug.Log("Create Lobby");

                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();

                OnLobbyCreated?.Invoke(lobbyID);

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message); // ส่ง error ไป UI

                return false;
            }
        }

        public async UniTask OnJoinPressed(string lobbyID)
        {
            if (string.IsNullOrEmpty(lobbyID))
            {
                OnError?.Invoke("กรุณาใส่ Lobby ID");
                return;
            }

            try
            {
                await Transport.JoinLobby(lobbyID, cts.Token);

                // InstanceFinder.ClientManager.StartConnection(Transport.GetHostSteamId());
                //
                // OnLobbyJoined?.Invoke(Transport.GetCurrentLobbyId());
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

        // ============ Wrappers (ข้อมูล lobby) ============
        public string GetLobbyID()
        {
            // return Transport.GetCurrentLobbyId();
            return "";
        }

        public List<string> GetPlayerList()
        {
            return Transport.GetPlayersInLobby();
            return new List<string>();
        }

        [Button]
        public bool IsHost()
        {
            return InstanceFinder.IsHostStarted;
        }

        // ============ Private ============
        private void OnEnable()
        {
            // Transport.OnDisconnect += HandleTransportDisconnect;
            // Transport.OnPlayerListChanged += OnPlayerListChang;
            // Transport.OnError += OnErrorLog;

            cts = new CancellationTokenSource();
        }

        protected override void OnDestroy()
        {
            // Transport.OnDisconnect -= HandleTransportDisconnect;
            // Transport.OnPlayerListChanged -= OnPlayerListChang;
            // Transport.OnError -= OnErrorLog;

            cts?.Cancel();
            cts?.Dispose();
        }

        private void HandleTransportDisconnect()
        {
            OnDisconnect?.Invoke();

            if (IsHost())
            {
                InstanceFinder.ServerManager.StopConnection(true);
            }

            InstanceFinder.ClientManager.StopConnection();
        }

        private void OnPlayerListChang(List<string> players)
        {
            OnPlayerListChanged?.Invoke(players);
        }

        public void OnErrorLog(string error)
        {
            OnError?.Invoke(error);
            Debug.LogError(error);
        }
    }
}
