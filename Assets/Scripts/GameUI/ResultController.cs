using FishNet;
using Game;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// The end-of-match result screen (decision 0015). Client-side plain scene object; builds its own
    /// placeholder uGUI (same spirit as StagingController). Shows on <see cref="GameState.Won"/> /
    /// <see cref="GameState.Lost"/>, hidden otherwise.
    ///
    /// Buttons drive the task-15 lifecycle mechanisms. Host only (host == server) sees Play Again
    /// (<see cref="GameManager.RestartMatch"/>) + Exit; a client sees Exit only, plus a "waiting for
    /// host" note. Exit tears the session/connection down and returns to the menu
    /// (<see cref="LobbyManager.HandleTransportDisconnect"/>). No local-player lookup needed — every
    /// action goes through a singleton.
    /// </summary>
    public class ResultController : MonoBehaviour
    {
        private GameManager _gm;
        private bool _subscribed;

        private GameObject _panel;
        private TextMeshProUGUI _banner;
        private GameObject _playAgainObj;
        private GameObject _waitingObj;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_subscribed && _gm != null)
                _gm.OnGameStateChanged -= HandleGameState;
        }

        private void Update()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            _gm = GameManager.Instance;
            _gm.OnGameStateChanged += HandleGameState;
            _subscribed = true;
            HandleGameState(_gm.State, _gm.State);
        }

        private void HandleGameState(GameState prev, GameState next)
        {
            bool over = next == GameState.Won || next == GameState.Lost;
            SetVisible(over);
            if (!over) return;

            bool won = next == GameState.Won;
            if (_banner != null)
            {
                _banner.text = won ? "VICTORY" : "DEFEAT";
                _banner.color = won ? new Color(0.42f, 0.90f, 0.48f) : new Color(0.92f, 0.38f, 0.38f);
            }

            // Host controls the restart; a client can only leave.
            bool isHost = InstanceFinder.IsServerStarted;
            if (_playAgainObj != null) _playAgainObj.SetActive(isHost);
            if (_waitingObj != null) _waitingObj.SetActive(!isHost);
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        // ---- button handlers ----

        private void OnPlayAgain()
        {
            // Host only (host is the server). RestartMatch is guarded to Won/Lost server-side anyway.
            if (!InstanceFinder.IsServerStarted) return;
            if (GameManager.Instance != null) GameManager.Instance.RestartMatch();
        }

        private void OnExit()
        {
            // Host → tears down the whole session; client → leaves only itself. LobbyManager decides
            // which (IsHost) and returns everyone/this peer to the main menu.
            if (LobbyManager.Instance != null) LobbyManager.Instance.HandleTransportDisconnect();
        }

        // ---- code-built placeholder UI ----

        private void BuildUI()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("ResultCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120; // above the HUD, below nothing
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("ResultPanel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _panel.AddComponent<RectTransform>();
            Stretch(panelRt);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.7f);

            var col = new GameObject("Content");
            col.transform.SetParent(_panel.transform, false);
            var colRt = col.AddComponent<RectTransform>();
            colRt.anchorMin = colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            colRt.sizeDelta = new Vector2(720, 480);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24;
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            _banner = MakeText(col.transform, "RESULT", 72, FontStyles.Bold, 120);

            var playAgain = MakeButton(col.transform, "PLAY AGAIN", OnPlayAgain, 90);
            _playAgainObj = playAgain.gameObject;

            _waitingObj = MakeText(col.transform, "waiting for host…", 24, FontStyles.Italic, 40).gameObject;

            MakeButton(col.transform, "EXIT TO MENU", OnExit, 90);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string text, float size, FontStyles style, float minHeight)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            return tmp;
        }

        private static Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float minHeight)
        {
            var go = new GameObject($"Button_{label}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.36f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = new Color(0.22f, 0.26f, 0.36f, 1f);
            colors.highlightedColor = new Color(0.30f, 0.36f, 0.50f, 1f);
            colors.pressedColor = new Color(0.16f, 0.20f, 0.28f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            Stretch(rt);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 30;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btn;
        }
    }
}
