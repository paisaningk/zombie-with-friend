using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Transporting.Multipass;
using Sirenix.OdinInspector;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace Networking.TransportProvider
{
    public class SteamTransportProvider : MonoBehaviour, ITransportProvider
    {
        public string ConnectionAddress => currentLobby.Id.IsValid ? currentLobby.Owner.Id.ToString() : string.Empty;
        public string LobbyName => currentLobby.Id.ToString();
        
        // stage
        [ShowInInspector] [ReadOnly] private Lobby currentLobby;
        [SerializeField] private Multipass multipass;

        
        public bool SupportsLobby => true;
        public bool RequiresCode  => true;

        public event Action<List<string>> OnPlayerListChanged;
        public event Action OnDisconnect;
        public event Action<string> OnError;

        public async UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var lobby = await SteamMatchmaking.CreateLobbyAsync(4)
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .Timeout(TimeSpan.FromSeconds(10));

            if (lobby == null) {
                OnError?.Invoke("On Lobby Created Error");
                return string.Empty;
            }

            currentLobby = lobby.Value;
            currentLobby.SetPrivate();

            // Set หลัง Steam พร้อมแล้ว
            multipass.SetClientTransport<FishyFacepunch.FishyFacepunch>();

            return currentLobby.Id.ToString();
        }

        public async UniTask<bool> JoinLobby(string lobbyCode, CancellationToken ct = default)
        {
            if (!ulong.TryParse(lobbyCode, out var id))
            {
                OnError?.Invoke("JoinLobby lobbyCode Error");
                return false;
            }
            
            ct.ThrowIfCancellationRequested();
            var lobby = await SteamMatchmaking.JoinLobbyAsync(id)
                .AsUniTask()
                .AttachExternalCancellation(ct)
                .Timeout(TimeSpan.FromSeconds(10));

            if (lobby == null)
            {
                OnError?.Invoke("Join Lobby Failed");
                return false;
            }
            
            multipass.SetClientTransport<FishyFacepunch.FishyFacepunch>();
            
            currentLobby = lobby.Value;
            return false;
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
    }
}
