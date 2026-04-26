using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        public string MenuScene;
        public string GameScene;
        public TMP_Text LobbyNameText;
        public ButtonFx StartGameButton;
        public ButtonFx CopyCodeButton;
        public ButtonFx ExitLobbyButton;

        public Action OnExitLobby;

        private CancellationTokenSource cts;


        public void Start()
        {
            StartGameButton.onClick.AddListener(StartGame);
            ExitLobbyButton.onClick.AddListener(ExitLobby);
            CopyCodeButton.onClick.AddListener(CopyCode);
        }
        private void StartGame()
        {
            if (!LobbyManager.Instance.IsHost()) return;

            var menuScene = new SceneUnloadData(MenuScene);
            InstanceFinder.SceneManager.UnloadGlobalScenes(menuScene);

            var gameScene = new SceneLoadData(GameScene);
            InstanceFinder.SceneManager.LoadGlobalScenes(gameScene);
        }

        public void CopyCode()
        {
            // cancel animation เดิมถ้ากดซ้ำ
            cts?.Cancel();
            cts = new CancellationTokenSource();

            GUIUtility.systemCopyBuffer = LobbyManager.Instance.GetCode();

            ShowCopiedFeedback(cts.Token).Forget();
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

        private async UniTaskVoid ShowCopiedFeedback(CancellationToken ct = default)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                CopyCodeButton.Text.text = "Copied!";
                await UniTask.WaitForSeconds(1.5f, cancellationToken: ct);
                CopyCodeButton.Text.text = "Copy Code";
            }
            catch (OperationCanceledException)
            {
                // reset text ถ้าถูก cancel
                CopyCodeButton.Text.text = "Copy Code";
            }
            catch (Exception e)
            {
                Debug.LogError($"ShowCopiedFeedback error: {e.Message}");
                CopyCodeButton.Text.text = "Copy Code";
            }
        }
    }
}
