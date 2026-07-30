using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Transporting.Multipass;
using FishNet.Transporting.Tugboat;
using UnityEngine;
namespace Networking.TransportProvider
{
    // LocalTransportProvider.cs — Tugboat
    public class TugboatTransprotProvider : MonoBehaviour, ITransportProvider
    {
        [SerializeField] private Multipass multipass;
        public string ConnectionAddress => "127.0.0.1";
        public string LobbyName => "LOCAL";
        
        // Tugboat is a direct-IP transport with no lobby service: GetPlayersInLobby has
        // nothing to return. Reporting false routes the player list through FishNet's
        // connection events (LobbyManager.connectedPlayers) instead. Reserve true for
        // transports with a real lobby backend (Steam).
        public bool SupportsLobby => false;
        public bool RequiresCode  => true;

        public UniTask<string> CreateLobby(CancellationToken ct = default)
        {
            multipass.SetClientTransport<Tugboat>();
            // return IP ของเครื่องตัวเอง
            return UniTask.FromResult("127.0.0.1");
        }

        public UniTask<bool> JoinLobby(string address, CancellationToken ct = default)
        {
            multipass.SetClientTransport<Tugboat>();
            return UniTask.FromResult(true); // address = IP ที่พิมพ์
        }

        public void         Disconnect()        { }
        public string       GetHostSteamId()    => string.Empty;

        public List<string> GetPlayersInLobby()
        {
            return new List<string>();
        }
    }
}
