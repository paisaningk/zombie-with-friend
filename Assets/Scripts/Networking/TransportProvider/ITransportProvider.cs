using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace Networking.TransportProvider
{
    public interface ITransportProvider
    {
        // บอก UI ว่า mode นี้ทำอะไรได้บ้าง
        bool SupportsLobby { get; }  // Steam = true, Offline = false
        bool RequiresCode  { get; }  // Steam/Local = true, Offline = false
        
        UniTask<string> CreateLobby(CancellationToken ct = default); // return code/id
        UniTask<bool>   JoinLobby(string code, CancellationToken ct = default);
        void            Disconnect();
        
        string GetHostSteamId();
        string GetCurrentLobbyId();

    }
}
