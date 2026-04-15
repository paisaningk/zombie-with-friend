using System;
using GameUI.Component;
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

        public Action OnJoinLobby;
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

        private void Joinlobby()
        {
            Debug.Log("Join lobby");
        }

        private void BackToMainMenu()
        {
            OnBackToMainMenu.Invoke();
        }
    }
}
