using System;
using GameUI.Component;
using Networking;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
namespace GameUI.MainMenu
{
    public class MainMenuPanel : UIPanel
    {
        public ButtonFx HostButton;
        public ButtonFx JoinButton;
        public ButtonFx ExitButton;

        [Tooltip("Display name shown in the lobby roster. Persisted to PlayerPrefs (task L1).")]
        public TMP_InputField NameInputField;

        [Tooltip("Why the last connection ended — shown on returning from a dropped/closed lobby.")]
        public TMP_Text NoticeText;

        public Action<string> OnCreateLobby;
        public Action OnOpenLobbyUI;

        public void Start()
        {
            HostButton.onClick.AddListener(HostButtonClick);
            JoinButton.onClick.AddListener(JoinButtonClick);
            ExitButton.onClick.AddListener(ExitButtonClick);

            if (NameInputField != null)
            {
                NameInputField.characterLimit = LobbyManager.MaxNameLength;
                NameInputField.text = LobbyManager.GetLocalPlayerName();
                NameInputField.onEndEdit.AddListener(LobbyManager.SetLocalPlayerName);
            }

            // Deliberately does NOT clear the notice here. A mid-game drop reloads the menu scene, so
            // this Start() and MenuFlowController.Start() both run with a reason already pending, in
            // undefined order — clearing here would sometimes wipe the "Host closed the lobby" message
            // that MenuFlowController had just set. The notice starts hidden in the scene instead.
        }

        /// <summary>
        /// Displays why the player is back at the main menu (host closed the room / connection lost).
        /// Called by MenuFlowController on every return to this panel; a null/empty reason hides it,
        /// so a normal Back keeps the menu clean.
        /// </summary>
        public void ShowNotice(string message)
        {
            if (NoticeText == null) return;

            NoticeText.text = message ?? string.Empty;
            NoticeText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        // The field only writes PlayerPrefs on end-edit, so a player who types a name and clicks
        // straight through would otherwise host under their previous one.
        private void CommitName()
        {
            if (NameInputField != null)
                LobbyManager.SetLocalPlayerName(NameInputField.text);
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
                CommitName();

                var canCreateLobby = await LobbyManager.Instance.OnCreateLobby();

                if (!canCreateLobby)
                {
                    LobbyManager.Instance.OnErrorLog($"Can't create lobby");
                    return;
                }

                OnCreateLobby.Invoke(LobbyManager.Instance.GetLobbyName());
            }
            catch (Exception e)
            {
                LobbyManager.Instance.OnErrorLog(e.StackTrace);
            }
        }

        private void JoinButtonClick()
        {
            CommitName();
            OnOpenLobbyUI.Invoke();
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
