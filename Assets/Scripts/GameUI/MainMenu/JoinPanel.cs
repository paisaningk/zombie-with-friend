using System;
using GameUI.Component;
using Networking;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
namespace GameUI.MainMenu
{
    public class JoinPanel : UIPanel
    {
        public TMP_InputField LobbyInputField;
        public ButtonFx JoinButton;
        public ButtonFx BackButton;

        public Action<string> OnJoinLobby;
        public Action OnBackToMainMenu;

        public void Start()
        {
            JoinButton.onClick.AddListener(Joinlobby);
            BackButton.onClick.AddListener(BackToMainMenu);
        }

        [Button]
        public void Rename()
        {
            JoinButton.gameObject.name = "JoinButton";
            JoinButton.Text.SetText("Join");
            BackButton.gameObject.name = "BackButton";
            BackButton.Text.SetText("Back To Main Menu");
        }

        private async void Joinlobby()
        {
            try
            {
                var canCreateLobby = await LobbyManager.Instance.OnJoinPressed(LobbyInputField.text);

                if (!canCreateLobby)
                {
                    LobbyManager.Instance.OnErrorLog($"Can't create lobby");
                    return;
                }

                Debug.Log("Join lobby");
                OnJoinLobby.Invoke("LobbyInputField");
            }
            catch (Exception e)
            {
                LobbyManager.Instance.OnErrorLog(e.StackTrace);
            }
        }

        private void BackToMainMenu()
        {
            OnBackToMainMenu.Invoke();
        }
    }
}
