using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FishNet;
using Sirenix.OdinInspector;
using Steamworks;
using UnityEngine;

namespace Networking.TransportProvider
{
    public class SteamTransportProvider : MonoBehaviour, ITransportProvider
    {
        // stage
        [ShowInInspector] [ReadOnly] private SteamId currentLobbyId;
        [ShowInInspector] [ReadOnly] private string hostSteamId;

        public event Action<List<string>> OnPlayerListChanged;
        public event Action OnDisconnect;
        public event Action<string> OnError;

        private UniTaskCompletionSource<bool> joinLobbyTcs;

        public async UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            var lobby = await SteamMatchmaking.CreateLobbyAsync(4);

            if (lobby == null)
            {
                Debug.Log("สร้างห้องไม่สำเร็จ");
                OnError?.Invoke("On Lobby Created Error");
                return string.Empty;
            }

            lobby.Value.SetPrivate();

            // เก็บ LobbyID ไว้ใช้
            currentLobbyId = lobby.Value.Id;

            lobby.Value.SetMemberData("name", "Test1");
            //lobby.Value.SetMemberData("map", "level_01");

            return currentLobbyId.ToString();
        }

        public UniTask JoinLobby(string lobbyCode, CancellationToken ct = default)
        {
            joinLobbyTcs = new UniTaskCompletionSource<bool>();

            if (!ulong.TryParse(lobbyCode, out var id))
            {
                joinLobbyTcs.TrySetException(new Exception("ID ไม่ถูกต้อง"));
                OnError?.Invoke("JoinLobby lobbyCode Error");
                return joinLobbyTcs.Task;
            }

            currentLobbyId = new CSteamID(id);
            SteamMatchmaking.JoinLobby(currentLobbyId);
            return joinLobbyTcs.Task.Timeout(TimeSpan.FromSeconds(10));

        }

        public void Disconnect()
        {
            if (currentLobbyId.IsValid())
                SteamMatchmaking.LeaveLobby(currentLobbyId);

            OnDisconnect?.Invoke();
        }

        public List<string> GetPlayersInLobby()
        {
            var players = new List<string>();
            var memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyId);

            for (var i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(
                    currentLobbyId, i
                );
                var name = SteamFriends.GetFriendPersonaName(memberId);
                players.Add(name);
            }

            return players;
        }

        private void OnLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                Debug.Log("สร้างห้องไม่สำเร็จ");
                OnError?.Invoke("On Lobby Created Error");
                return;
            }

            Debug.Log("aaaa");

            var lobbyId = new CSteamID(result.m_ulSteamIDLobby);

            // เก็บ LobbyID ไว้ใช้
            currentLobbyId = lobbyId;

            // เก็บข้อมูลใน Lobby Data — client อื่นดึงได้
            SteamMatchmaking.SetLobbyData(lobbyId, "name", "ห้องของเรา");
            SteamMatchmaking.SetLobbyData(lobbyId, "map", "level_01");

            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            createLobbyTcs?.TrySetResult(currentLobbyId.m_SteamID.ToString());
        }

        private void OnLobbyEnter(LobbyEnter_t result)
        {
            currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);

            hostSteamId = SteamMatchmaking.GetLobbyOwner(currentLobbyId).m_SteamID.ToString();

            joinLobbyTcs?.TrySetResult(true);
        }

        private void LobbyChatUpdate(LobbyChatUpdate_t param)
        {
            // ui update callback
            OnPlayerListChanged?.Invoke(GetPlayersInLobby());
        }

        public string GetCurrentLobbyId()
        {
            return currentLobbyId.ToString();
        }

        public string GetHostSteamId()
        {
            return hostSteamId;
        }
    }
}
