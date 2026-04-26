using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Networking.TransportProvider
{
    public interface ITransportProvider
    {
        bool SupportsLobby { get; }
        bool RequiresCode { get; }
        string ConnectionAddress { get; }
        string LobbyName { get; }

        UniTask<string> CreateLobby(CancellationToken ct = default);
        UniTask<bool>   JoinLobby(string code, CancellationToken ct = default);
        void            Disconnect();

        List<string> GetPlayersInLobby();
    }
}
