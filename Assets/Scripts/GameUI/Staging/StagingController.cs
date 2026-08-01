using System.Text;
using FishNet;
using Game;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI.Staging
{
    /// <summary>
    /// Client-side driver for the in-game staging screen (decision 0013). Lives as a plain scene
    /// object in the game scene, so it exists on every peer and builds its own local UI.
    ///
    /// It shows while the match is in <see cref="GameState.Lobby"/> (pre-match): the owner picks a
    /// class (Gunner/Support) and toggles Ready, and — on the host only — a Start button appears,
    /// enabled once everyone is ready. Start flips the match to Playing via
    /// <see cref="WaveManager.TryStartMatch"/> (host == server in FishNet host mode); the wave loop is
    /// already waiting on that. When Playing, the panel hides and CursorController re-locks the cursor
    /// (it is the single owner of cursor state — decision 0014).
    ///
    /// Placeholder UI built in code (no art / no scene wiring), same spirit as the tracer / heal-pulse
    /// visuals — task 18 replaces the look. Roster is polled (players may still be loading in).
    /// </summary>
    public class StagingController : MonoBehaviour
    {
        [SerializeField] private float _rosterRefresh = 0.5f;

        private GameManager _gm;
        private bool _subscribed;

        private GameObject _panel;
        private TextMeshProUGUI _rosterText;
        private TextMeshProUGUI _readyLabel;
        private Button _gunnerBtn;
        private Button _supportBtn;
        private Button _readyBtn;
        private GameObject _startObj;
        private Button _startBtn;

        private float _nextRoster;
        private readonly StringBuilder _sb = new StringBuilder(256);

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
            // GameManager (scene object) may init a frame or two after us — subscribe once available,
            // then apply the current state (we may have missed its initial fire).
            if (!_subscribed)
            {
                if (GameManager.Instance == null) return;
                _gm = GameManager.Instance;
                _gm.OnGameStateChanged += HandleGameState;
                _subscribed = true;
                HandleGameState(_gm.State, _gm.State);
            }

            if (_panel == null || !_panel.activeSelf) return;

            // Cursor is owned solely by CursorController now (decision 0014) — it frees the cursor
            // whenever the match isn't Playing (Lobby staging included), so we don't touch it here.

            if (Time.unscaledTime >= _nextRoster)
            {
                _nextRoster = Time.unscaledTime + Mathf.Max(0.1f, _rosterRefresh);
                RefreshRoster();
            }
        }

        private void HandleGameState(GameState prev, GameState next)
        {
            SetVisible(next == GameState.Lobby);
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
            if (visible)
            {
                RefreshRoster();
                _nextRoster = Time.unscaledTime + _rosterRefresh;
            }
        }

        // ---- button handlers ----

        private void OnPickGunner() => LocalPlayer()?.Class?.CmdSetClass(PlayerClassType.Gunner);
        private void OnPickSupport() => LocalPlayer()?.Class?.CmdSetClass(PlayerClassType.Support);

        private void OnToggleReady()
        {
            PlayerController local = LocalPlayer();
            if (local == null || local.Ready == null) return;
            local.Ready.CmdSetReady(!local.Ready.IsReady);
        }

        private void OnStart()
        {
            // Host only (host is the server). Server re-validates all-ready inside TryStartMatch.
            if (!InstanceFinder.IsServerStarted) return;
            if (WaveManager.Instance != null) WaveManager.Instance.TryStartMatch();
        }

        // ---- roster + button state ----

        private void RefreshRoster()
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

            _sb.Clear();
            _sb.AppendLine("<b>Players</b>");
            bool allReady = players.Length > 0;
            PlayerController local = null;

            foreach (PlayerController pc in players)
            {
                if (pc == null) continue;
                if (pc.IsOwner) local = pc;

                string cls = pc.Class != null ? pc.Class.Current.ToString() : "?";
                bool ready = pc.Ready != null && pc.Ready.IsReady;
                if (!ready) allReady = false;

                string dot = ready ? "<color=#5EE06B>●</color>" : "<color=#E05E5E>○</color>";
                _sb.AppendLine($"{dot}  Player {pc.OwnerId}  <color=#9FB4FF>[{cls}]</color>  {(ready ? "READY" : "...")}");
            }

            if (_rosterText != null) _rosterText.text = _sb.ToString();

            // Reflect the local player's own ready state on the toggle label.
            if (_readyLabel != null)
            {
                bool localReady = local != null && local.Ready != null && local.Ready.IsReady;
                _readyLabel.text = localReady ? "Cancel Ready" : "Ready";
            }

            // Host-only Start button, enabled only when everyone is ready.
            bool isHost = InstanceFinder.IsServerStarted;
            if (_startObj != null) _startObj.SetActive(isHost);
            if (_startBtn != null) _startBtn.interactable = isHost && allReady;
        }

        private PlayerController LocalPlayer()
        {
            PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController pc in players)
                if (pc != null && pc.IsOwner) return pc;
            return null;
        }

        // ---- code-built placeholder UI ----

        private void BuildUI()
        {
            EnsureEventSystem();

            // Canvas
            var canvasGo = new GameObject("StagingCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dim full-screen backdrop = the panel root we toggle.
            _panel = new GameObject("StagingPanel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _panel.AddComponent<RectTransform>();
            Stretch(panelRt);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            // Centered content column.
            var col = new GameObject("Content");
            col.transform.SetParent(_panel.transform, false);
            var colRt = col.AddComponent<RectTransform>();
            colRt.anchorMin = colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            colRt.sizeDelta = new Vector2(760, 620);
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16;
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var colBg = col.AddComponent<Image>();
            colBg.color = new Color(0.10f, 0.11f, 0.15f, 0.95f);

            MakeText(col.transform, "CHOOSE YOUR CLASS", 40, FontStyles.Bold, 60);

            // class buttons row
            var row = new GameObject("ClassRow");
            row.transform.SetParent(col.transform, false);
            row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            var rowLe = row.AddComponent<LayoutElement>();
            rowLe.minHeight = 110;
            _gunnerBtn = MakeButton(row.transform, "GUNNER", OnPickGunner, out _);
            _supportBtn = MakeButton(row.transform, "SUPPORT", OnPickSupport, out _);

            _readyBtn = MakeButton(col.transform, "Ready", OnToggleReady, out _readyLabel, 70);

            _startBtn = MakeButton(col.transform, "START MATCH", OnStart, out _, 80);
            _startObj = _startBtn.gameObject;

            MakeText(col.transform, "(host starts once everyone is Ready)", 20, FontStyles.Italic, 34);

            _rosterText = MakeText(col.transform, "", 24, FontStyles.Normal, 220);
            _rosterText.alignment = TextAlignmentOptions.TopLeft;
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

        private static Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick,
            out TextMeshProUGUI labelTmp, float minHeight = 90)
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
            colors.disabledColor = new Color(0.18f, 0.18f, 0.20f, 0.6f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = minHeight;

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            Stretch(rt);
            labelTmp = textGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 30;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = Color.white;
            return btn;
        }
    }
}
