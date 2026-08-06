using System;
using System.Net;
using System.Net.Sockets;
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
        public TMP_Text ErrorText; // shows join failures on-screen (task 16b, decision E1a)

        public Action<string> OnJoinLobby;
        public Action OnBackToMainMenu;

        public void Start()
        {
            JoinButton.onClick.AddListener(Joinlobby);
            BackButton.onClick.AddListener(BackToMainMenu);

            // Surface LobbyManager errors on-screen. Direct-IP joins fail often (wrong IP / host not
            // up), and until now OnError only hit the Console. Persists for the panel's lifetime —
            // UIPanel toggles a child Panel, not this component's GameObject.
            LobbyManager.Instance.OnError += ShowError;
            ClearError();
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnError -= ShowError;
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
            ClearError();
            var input = LobbyInputField.text?.Trim();

            // Reject malformed input up front (IPv4 only, decision D1) so the player gets an instant
            // "bad IP" instead of waiting ~5.5s for the transport to time out on garbage.
            if (string.IsNullOrEmpty(input) ||
                !IPAddress.TryParse(input, out var addr) ||
                addr.AddressFamily != AddressFamily.InterNetwork)
            {
                ShowError("IP ไม่ถูกต้อง (ต้องเป็น IPv4 เช่น 192.168.1.42)");
                return;
            }

            SetConnecting(true);
            try
            {
                // On failure OnJoinPressed already raised OnError → ShowError, so we just restore the
                // button. On success we advance to the lobby.
                var joined = await LobbyManager.Instance.OnJoinPressed(input);
                if (joined)
                    OnJoinLobby?.Invoke(input);
            }
            catch (Exception e)
            {
                LobbyManager.Instance.OnErrorLog(e.Message);
            }
            finally
            {
                SetConnecting(false);
            }
        }

        // Disables the button and shows "Connecting..." while the join is in flight (decision F1) —
        // gives feedback during the up-to-~5.5s wait and blocks duplicate StartConnection calls.
        private void SetConnecting(bool connecting)
        {
            JoinButton.interactable = !connecting;
            JoinButton.Text.SetText(connecting ? "Connecting..." : "Join");
        }

        private void ShowError(string message)
        {
            if (ErrorText == null) return;
            ErrorText.text = message;
            ErrorText.gameObject.SetActive(true);
        }

        private void ClearError()
        {
            if (ErrorText == null) return;
            ErrorText.text = string.Empty;
            ErrorText.gameObject.SetActive(false);
        }

        private void BackToMainMenu()
        {
            OnBackToMainMenu.Invoke();
        }
    }
}
