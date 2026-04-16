using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FishNet;
using Sirenix.OdinInspector;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Networking.TransportProvider
{
    public class SteamTransportProvider : MonoBehaviour, ITransportProvider
    {
        // stage
        [ShowInInspector] [ReadOnly] private Lobby currentLobby;

        public event Action<List<string>> OnPlayerListChanged;
        public event Action OnDisconnect;
        public event Action<string> OnError;

        public async UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var lobby = await SteamMatchmaking.CreateLobbyAsync(4).AsUniTask().AttachExternalCancellation(ct);;

            if (lobby == null)
            {
                Debug.Log("สร้างห้องไม่สำเร็จ");
                OnError?.Invoke("On Lobby Created Error");
                return string.Empty;
            }

            currentLobby = lobby.Value;
            currentLobby.SetPrivate();
            currentLobby.SetMemberData("name", "Test1");

            return currentLobby.ToString();
        }

        public async UniTask<bool> JoinLobby(string lobbyCode, CancellationToken ct = default)
        {
            if (!ulong.TryParse(lobbyCode, out var id))
            {
                OnError?.Invoke("JoinLobby lobbyCode Error");
                return false;
            }
            
            ct.ThrowIfCancellationRequested();
            var lobby = await SteamMatchmaking.JoinLobbyAsync(id).AsUniTask().AttachExternalCancellation(ct);

            if (lobby == null)
            {
                OnError?.Invoke("Join Lobby Failed");
                return false;
            }
            
            currentLobby = lobby.Value;
            return true;
        }

        public void Disconnect()
        {
            if (currentLobby.Id.IsValid)
            {
                currentLobby.Leave();
            }

            OnDisconnect?.Invoke();
        }

        public List<string> GetPlayersInLobby()
        {
            var players = new List<string>();
            
            if (!currentLobby.Id.IsValid)
                return players;

            players.AddRange(currentLobby.Members.Select(member => member.Name));

            return players;
        }

        public SteamId GetCurrentLobbyId()
        {
            return currentLobby.Id;
        }
    }
}
