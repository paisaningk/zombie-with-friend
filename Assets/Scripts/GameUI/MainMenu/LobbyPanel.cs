using System;
using FishNet;
using GameUI.Component;
using Networking;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
namespace GameUI.MainMenu
{
    public class LobbyPanel : UIPanel
    {
        public TMP_Text LobbyNameText;
        public ButtonFx StartGameButton;
        public ButtonFx CopyCodeButton;
        public ButtonFx ExitLobbyButton;

        public Action OnExitLobby;

        public void Start()
        {
            ExitLobbyButton.onClick.AddListener(ExitLobby);
        }

        public void Setup(string lobbyCode)
        {
            LobbyNameText.text = lobbyCode;

            StartGameButton.gameObject.SetActive(LobbyManager.Instance.IsHost());
        }

        [Button]
        public void Rename()
        {
            StartGameButton.gameObject.name = "StartGameButton";
            StartGameButton.Text.SetText("Start Game");
            ExitLobbyButton.gameObject.name = "ExitLobbyButton";
            ExitLobbyButton.Text.SetText("Exit Lobby");

            CopyCodeButton.gameObject.name = "CopyCodeButton";
            CopyCodeButton.Text.SetText("Copy Code");
        }

        public void ExitLobby()
        {
            OnExitLobby?.Invoke();
        }
    }
}
