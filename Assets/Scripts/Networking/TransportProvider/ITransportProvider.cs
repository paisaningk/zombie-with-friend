using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Networking.TransportProvider
{
    public interface ITransportProvider
    {
        UniTask<string> CreateLobby(CancellationToken ct = default); // return code/id
        UniTask<bool>   JoinLobby(string code, CancellationToken ct = default);
        void            Disconnect();
    }
}
