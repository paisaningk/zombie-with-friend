using System;
using GameUI.Component;
using Networking;
using Sirenix.OdinInspector;
namespace GameUI.MainMenu
{
    public class MainMenuPanel : UIPanel
    {
        public ButtonFx HostButton;
        public ButtonFx JoinButton;
        public ButtonFx ExitButton;

        public Action<string> OnCreateLobby;
        public Action OnJoinLobby;

        public void Start()
        {
            HostButton.onClick.AddListener(HostButtonClick);
            JoinButton.onClick.AddListener(JoinButtonClick);
            ExitButton.onClick.AddListener(ExitButtonClick);
        }

        [Button]
        public void Rename()
        {
            HostButton.name = "Host Button";
            HostButton.Text.text = "Host Button";
            JoinButton.name = "Join Button";
            JoinButton.Text.text = "Join Button";
            ExitButton.name = "Exit Button";
            ExitButton.Text.text = "Exit Button";
        }

        private async void HostButtonClick()
        {
            try
            {
                var canCreateLobby = await LobbyManager.Instance.OnCreateLobby();

                if (!canCreateLobby)
                {
                    LobbyManager.Instance.OnErrorLog($"Can't create lobby");
                    return;
                }

                OnCreateLobby.Invoke(LobbyManager.Instance.GetLobbyID());
            }
            catch (Exception e)
            {
                LobbyManager.Instance.OnErrorLog(e.StackTrace);
            }
        }

        private void JoinButtonClick()
        {
            OnJoinLobby.Invoke();
        }

        private void ExitButtonClick()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif

        }
    }
}
