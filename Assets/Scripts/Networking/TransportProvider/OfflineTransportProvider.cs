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
        public string ConnectionAddress => string.Empty;
        public string LobbyId => "OFFLINE";
        
        public bool SupportsLobby => false;
        public bool RequiresCode  => false;

        public UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            multipass.SetClientTransport<Yak>();
            return UniTask.FromResult("OFFLINE");
        }

        public UniTask<bool> JoinLobby(string address, CancellationToken ct = default)
            => UniTask.FromResult(false);

        public void         Disconnect()        { }

        public List<string> GetPlayersInLobby()
        {
            return new List<string>();
        }
    }
}
