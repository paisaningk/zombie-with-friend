using Eflatun.SceneReference;
using UnityEngine;
using UnityEngine.SceneManagement;
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

                SceneManager.LoadScene(GameScene.Name);

                return;
            }
  #endif
            SceneManager.LoadScene(MainMenuScene.Name);

        }
    }
}
