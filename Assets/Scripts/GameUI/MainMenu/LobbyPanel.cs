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
        public TMP_InputField IpInputField; // host: editable LAN IP to share · client: connected IP (task 16b, B3)
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
            // Only the host drives the networked scene load; clients receive it automatically.
            if (!LobbyManager.Instance.IsHost()) return;

            // ReplaceOption.All unloads every currently-loaded scene (including the menu,
            // which was loaded outside FishNet as an "offline" scene) on all peers, then
            // loads the game scene. This is why we don't UnloadGlobalScenes(menu) first —
            // the menu isn't a FishNet-managed scene, so that call would be a no-op.
            var gameScene = new SceneLoadData(GameScene) { ReplaceScenes = ReplaceOption.All };
            InstanceFinder.SceneManager.LoadGlobalScenes(gameScene);
        }

        public void CopyCode()
        {
            // cancel animation เดิมถ้ากดซ้ำ
            cts?.Cancel();
            cts = new CancellationTokenSource();

            // Copy the shareable IP shown in the field (which the host may have corrected), not the
            // host's own loopback self-connect address (task 16b, B3).
            GUIUtility.systemCopyBuffer = IpInputField != null
                ? IpInputField.text
                : LobbyManager.Instance.GetCode();

            ShowCopiedFeedback(cts.Token).Forget();
        }

        public void Setup(string lobbyCode)
        {
            LobbyNameText.text = lobbyCode;

            bool isHost = LobbyManager.Instance.IsHost();
            StartGameButton.gameObject.SetActive(isHost);

            if (IpInputField != null)
            {
                // Host shows its best-guess LAN IP, editable so it can be corrected when the guess
                // picks a VPN/virtual adapter. Client shows the host IP it connected to, read-only.
                IpInputField.text = isHost ? LobbyManager.Instance.GetHostDisplayAddress() : lobbyCode;
                IpInputField.interactable = isHost;
            }
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
