using System;
using FishNet;
using FishNet.Managing.Scened;
using GameUI.Component;
using Networking;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
namespace GameUI.MainMenu
{
    public class LobbyPanel : UIPanel
    {
        public string LoadScenes;
        public TMP_Text LobbyNameText;
        public ButtonFx StartGameButton;
        public ButtonFx CopyCodeButton;
        public ButtonFx ExitLobbyButton;

        public Action OnExitLobby;

        public void Start()
        {
            StartGameButton.onClick.AddListener(StartGame);
            ExitLobbyButton.onClick.AddListener(ExitLobby);
            CopyCodeButton.onClick.AddListener(CopyCode);
        }
        private void StartGame()
        {
            if (!LobbyManager.Instance.IsHost()) return;

            var scene = new SceneLoadData(LoadScenes);
            InstanceFinder.SceneManager.LoadGlobalScenes(scene);
        }

        public void CopyCode()
        {
            GUIUtility.systemCopyBuffer = LobbyManager.Instance.GetCode();
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

        // private IEnumerator ShowCopiedFeedback()
        // {
        //     copyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Copied!";
        //     yield return new WaitForSeconds(1.5f);
        //     copyButton.GetComponentInChildren<TextMeshProUGUI>().text = "Copy";
        // }
    }
}
