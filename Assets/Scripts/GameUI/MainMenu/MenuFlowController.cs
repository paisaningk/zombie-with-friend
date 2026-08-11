using Networking;
using Sirenix.OdinInspector;
using UnityEngine;
namespace GameUI.MainMenu
{
    public class MenuFlowController : MonoBehaviour
    {
        [SerializeField] private MainMenuPanel MainMenu;
        [SerializeField] private JoinPanel JoinPanel;
        [SerializeField] private LobbyPanel LobbyPanel;

        private void Start()
        {
            ShowMainMenu();

            LobbyManager.Instance.OnDisconnect += ShowMainMenu;

            MainMenu.OnOpenLobbyUI += ShowJoin;
            MainMenu.OnCreateLobby += ShowLobby;

            JoinPanel.OnJoinLobby += ShowLobby;
            JoinPanel.OnBackToMainMenu += ShowMainMenu;

            LobbyPanel.OnExitLobby += ShowMainMenu;
            LobbyPanel.OnExitLobby += LobbyManager.Instance.HandleTransportDisconnect;
        }

        [Button]
        private void ShowMainMenu()
        {
            MainMenu.OpenPanel();
            JoinPanel.ClosePanel();
            LobbyPanel.ClosePanel();

            // Surface why we're back here. The reason is consumed (read-and-cleared), so it shows once
            // after a drop and not again on a normal Back. Empty on the initial Start() call and when
            // the player left on purpose (task L1, decision 0019).
            MainMenu.ShowNotice(LobbyManager.Instance.ConsumeDisconnectReason());
        }

        [Button]
        private void ShowJoin()
        {
            MainMenu.ClosePanel();
            JoinPanel.OpenPanel();
            LobbyPanel.ClosePanel();
        }

        [Button]
        private void ShowLobby(string lobbyCode)
        {
            MainMenu.ClosePanel();
            JoinPanel.ClosePanel();
            Debug.Log(LobbyManager.Instance.IsHost());
            LobbyPanel.Setup(lobbyCode);
            LobbyPanel.OpenPanel();
        }
    }
}
