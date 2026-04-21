using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Yak;
using UnityEngine;
namespace Networking.TransportProvider
{
    // OfflineTransportProvider.cs — Yak
    public class OfflineTransportProvider : MonoBehaviour, ITransportProvider
    {
        [SerializeField] private Multipass multipass;
        
        public bool SupportsLobby => false;
        public bool RequiresCode  => false;

        public UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            multipass.SetClientTransport<Yak>();
            return UniTask.FromResult("OFFLINE");
        }

        public UniTask<bool> JoinLobby(string address, CancellationToken ct = default)
            => UniTask.FromResult(true); // ไม่มี join ในโหมด offline

        public void         Disconnect()        { }
        public string       GetHostSteamId()    => string.Empty;
        public string       GetCurrentLobbyId() => "OFFLINE";
        public List<string> GetPlayersInLobby() => new();
    }
}
