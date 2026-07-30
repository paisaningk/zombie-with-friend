using Eflatun.SceneReference;
using FishNet;
using FishNet.Managing.Scened;
using Networking;
using UnityEngine;
namespace DevTool
{
    public class InitScene : MonoBehaviour
    {
        public bool IsSkipMenu;
        [SerializeField] private SceneReference MainMenuScene;
        [SerializeField] private SceneReference GameScene;

#if UNITY_EDITOR
        public DevBootstrap DevBootstrap;
#endif

        public async void Start()
        {
#if UNITY_EDITOR
            if (IsSkipMenu)
            {
                await DevBootstrap.AutoStart();

                // Use the SAME networked load as the real menu flow (LobbyPanel.StartGame):
                // only the host loads the game scene, over FishNet, with ReplaceOption.All so
                // it unloads the current scene on every peer. Clients receive it automatically
                // and must NOT plain-load it themselves. This keeps skip-menu dev testing
                // identical to production (and lets the presence-based spawner fire).
                if (LobbyManager.Instance.IsHost())
                {
                    var gameScene = new SceneLoadData(GameScene.Name) { ReplaceScenes = ReplaceOption.All };
                    InstanceFinder.SceneManager.LoadGlobalScenes(gameScene);
                }

                return;
            }
  #endif
            // Fully qualified: FishNet.Managing.Scened also defines a SceneManager type.
            UnityEngine.SceneManagement.SceneManager.LoadScene(MainMenuScene.Name);

        }
    }
}
