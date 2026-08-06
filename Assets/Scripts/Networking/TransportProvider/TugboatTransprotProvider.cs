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

        // The address the local client will connect to. Defaults to loopback so the HOST
        // (which calls CreateLobby but never JoinLobby) always self-connects on 127.0.0.1 —
        // reliable, no NIC dependency. A joining CLIENT overwrites this via JoinLobby with the
        // typed host IP, and ConnectionAddress then reports it. One provider instance is either
        // host or client, never both, so the two paths never collide (task 16b, decision A1).
        private string _joinAddress = "127.0.0.1";
        public string ConnectionAddress => _joinAddress;
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
            // Store the typed host IP so ConnectionAddress reports it and the client connects
            // there instead of loopback (task 16b, decision A1). Host never calls this, so its
            // _joinAddress stays at the loopback default.
            _joinAddress = address;
            return UniTask.FromResult(true);
        }

        public void         Disconnect()        { }
        public string       GetHostSteamId()    => string.Empty;

        public List<string> GetPlayersInLobby()
        {
            return new List<string>();
        }
    }
}
