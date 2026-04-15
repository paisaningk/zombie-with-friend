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
            // subscribe event จาก LobbyManager
            // LobbyManager.Instance.OnLobbyCreated += ShowLobby;
            // LobbyManager.Instance.OnLobbyJoined += ShowLobby;
            // LobbyManager.Instance.OnDisconnect += ShowMainMenu;

            ShowMainMenu();

            MainMenu.OnJoinLobby += ShowJoin;
            MainMenu.OnCreateLobby += ShowLobby;

            JoinPanel.OnJoinLobby += ShowJoin;
            JoinPanel.OnBackToMainMenu += ShowMainMenu;

            LobbyPanel.OnExitLobby += ShowMainMenu;
        }

        [Button]
        private void ShowMainMenu()
        {
            MainMenu.OpenPanel();
            JoinPanel.ClosePanel();
            LobbyPanel.ClosePanel();
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
            LobbyPanel.Setup(lobbyCode);
            LobbyPanel.OpenPanel();
        }
    }
}
