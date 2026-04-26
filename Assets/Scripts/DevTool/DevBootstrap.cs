using System.Linq;
using Cysharp.Threading.Tasks;
using FishNet;
using Networking;
using Unity.Multiplayer.PlayMode;
using UnityEngine;
namespace DevTool
{
    public class DevBootstrap : MonoBehaviour
    {
        public async UniTask AutoStart()
        {
            // ดึง tags ของ Virtual Player ตัวนี้
            var currentPlayerTags = CurrentPlayer.Tags;

            if (currentPlayerTags.Contains("Host"))
            {
                Debug.Log("host");
                await StartHost();
                Debug.Log("[DevBootstrap] Started as Host");
            }
            else
            {
                await StartClient();
                Debug.Log("[DevBootstrap] Started as Client");
            }
        }

        private async UniTask StartHost()
        {
            await LobbyManager.Instance.OnCreateLobby();
        }

        private async UniTask StartClient()
        {
            await LobbyManager.Instance.QuickJoin();
        }
    }
}
