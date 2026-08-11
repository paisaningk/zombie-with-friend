// Two-peer verification harness for the lobby roster (task L1, decision 0019).
//
// INERT BY DEFAULT. Compiles to nothing unless the scripting define symbol L1_ROSTER_TEST is added
// (Project Settings → Player → Scripting Define Symbols). Without that guard this would auto-run on
// every Play and hijack the menu.
//
// WHAT IT DOES
//   Drives the real menu flow on both MPPM peers so the roster can be checked across a genuine process
//   boundary: the main editor hosts, the virtual player joins 127.0.0.1, and both log their roster
//   every 2s under the [L1TEST] tag. The virtual player's output lands in
//   Library/VP/<clone>/Logs/Editor.log.
//
// HOW TO RUN
//   1. Multiplayer Play Mode window → make sure a virtual player is Active.
//   2. Add L1_ROSTER_TEST to Scripting Define Symbols.
//   3. Open Assets/Scenes/Init.unity with InitScene.IsSkipMenu = false, press Play.
//   4. Read [L1TEST] lines in both the main Console and the virtual player's Editor.log.
//   Remove the define afterwards.
//
// KNOWN SNAG ON THIS MACHINE (2026-08-11): the MPPM clone stopped following the main editor into play
// mode — its log shows no InvokePlay for recent runs, which is why the 2-peer run never happened. If
// [L1TEST] never appears in the clone's log, the problem is the clone, not this harness (the HOST half
// was proven working); recreate the virtual player in the MPPM window and re-run.
//
// Deliberately NOT wired through InitScene.IsSkipMenu: skip-menu never loads MainMenu, so there would
// be no LobbyPanel and no roster UI to exercise.
//
// Peer discrimination uses Application.dataPath rather than DevBootstrap's CurrentPlayer.Tags, because
// both MPPM players are configured with empty tags in this project (Library/VP/SystemData.json) and
// tagging them requires the MPPM window.

#if L1_ROSTER_TEST
using Cysharp.Threading.Tasks;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DevTool
{
    public class DevLobbyRosterTest : MonoBehaviour
    {
        private const string HostName = "kao";
        private const string ClientName = "friend2";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("L1RosterTest");
            DontDestroyOnLoad(go);
            go.AddComponent<DevLobbyRosterTest>();
        }

        private bool IsVirtualPlayer =>
            Application.dataPath.Replace('\\', '/').Contains("/VP/mppm");

        private void Start() => Run().Forget();

        private async UniTaskVoid Run()
        {
            string role = IsVirtualPlayer ? "CLIENT" : "HOST";
            Debug.Log($"[L1TEST] {role} boot — dataPath={Application.dataPath}");

            await UniTask.WaitUntil(() => GameObject.Find("UI") != null);
            Transform ui = GameObject.Find("UI").transform;

            if (IsVirtualPlayer)
            {
                // Let the host finish coming up first. PlayerPrefs are shared between the two peers
                // (ProjectSettings is a symlink), so the host must already have sent its own name
                // before we overwrite the stored value with ours.
                await UniTask.WaitForSeconds(5f);
                LobbyManager.SetLocalPlayerName(ClientName);
                Debug.Log($"[L1TEST] CLIENT name set to '{LobbyManager.GetLocalPlayerName()}'");

                ui.Find("MainMenu/Panel/Join Button").GetComponent<Button>().onClick.Invoke();
                await UniTask.WaitForSeconds(0.5f);

                ui.Find("JoinPanel/Panel/InputField (TMP)").GetComponent<TMP_InputField>().text = "127.0.0.1";
                await UniTask.WaitForSeconds(0.2f);
                ui.Find("JoinPanel/Panel/JoinButton").GetComponent<Button>().onClick.Invoke();
            }
            else
            {
                await UniTask.WaitForSeconds(1f);
                LobbyManager.SetLocalPlayerName(HostName);
                Debug.Log($"[L1TEST] HOST name set to '{LobbyManager.GetLocalPlayerName()}'");

                ui.Find("MainMenu/Panel/Host Button").GetComponent<Button>().onClick.Invoke();
            }

            for (int i = 0; i < 30; i++)
            {
                await UniTask.WaitForSeconds(2f);
                LogRoster(role);
            }
        }

        private static void LogRoster(string role)
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null)
            {
                Debug.Log($"[L1TEST] {role} roster: LobbyManager.Instance is null");
                return;
            }

            string line = $"[L1TEST] {role} roster {lobby.Roster.Count}/{lobby.RosterMaxPlayers}:";
            foreach (LobbyRosterEntry entry in lobby.Roster)
                line += $" [id={entry.ClientId} name='{entry.Name}' host={entry.IsHost}]";

            Debug.Log(line);
        }
    }
}
#endif
