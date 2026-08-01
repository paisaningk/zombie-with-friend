using System.Text;
using Game;
using Player;
using Shop;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameUI
{
    /// <summary>
    /// The between-wave shop (decision 0015). Client-side plain scene object; builds its own placeholder
    /// uGUI (same spirit as StagingController). Shows while <see cref="GameManager.ShopOpen"/> — a SyncVar
    /// so every client's shop opens/closes with the server's window.
    ///
    /// Three fixed-cost upgrades (Damage / MaxHP / FireRate) → the owner's <see cref="PlayerUpgrades.CmdBuy"/>;
    /// a Ready toggle → <see cref="PlayerReady.CmdSetReady"/> (advancing the wave once everyone is ready).
    /// The owner <see cref="PlayerController"/> is found by polling (mirrors StagingController) — the shop
    /// is the only consumer that needs it, so no shared local-player accessor was added (decision 0015).
    /// Cost / cap come from a <see cref="ShopData"/> asset (same one PlayerUpgrades validates against).
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private ShopData _shop;
        [SerializeField] private float _refresh = 0.3f;

        private static readonly UpgradeType[] Upgrades =
            { UpgradeType.Damage, UpgradeType.MaxHp, UpgradeType.FireRate };

        private GameManager _gm;
        private bool _subscribed;

        private GameObject _panel;
        private TextMeshProUGUI _goldText;
        private TextMeshProUGUI _readyLabel;
        private readonly TextMeshProUGUI[] _upgradeLabels = new TextMeshProUGUI[3];

        private float _nextRefresh;

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_subscribed && _gm != null)
                _gm.OnShopOpenChanged -= HandleShopOpen;
        }

        private void Update()
        {
            if (!_subscribed)
            {
                if (GameManager.Instance == null) return;
                _gm = GameManager.Instance;
                _gm.OnShopOpenChanged += HandleShopOpen;
                _subscribed = true;
                HandleShopOpen(_gm.ShopOpen, _gm.ShopOpen);
            }

            if (_panel == null || !_panel.activeSelf) return;

            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + Mathf.Max(0.1f, _refresh);
                Refresh();
            }
        }

        private void HandleShopOpen(bool prev, bool open)
        {
            SetVisible(open);
            if (open) Refresh();
        }

        private void SetVisible(bool visible)
        {
            if (_panel != null) _panel.SetActive(visible);
        }

        // ---- button handlers ----

        private void OnBuy(UpgradeType type) => LocalPlayer()?.Upgrades?.CmdBuy(type);

        private void OnToggleReady()
        {
            PlayerController local = LocalPlayer();
            if (local == null || local.Ready == null) return;
            local.Ready.CmdSetReady(!local.Ready.IsReady);
        }

        // ---- refresh (poll owner) ----

        private void Refresh()
        {
            PlayerController local = LocalPlayer();

            if (_goldText != null)
            {
                int gold = local != null && local.Wallet != null ? local.Wallet.Gold : 0;
                _goldText.text = $"Gold: {gold}";
            }

            for (int i = 0; i < Upgrades.Length; i++)
            {
                TextMeshProUGUI label = _upgradeLabels[i];
                if (label == null) continue;

                UpgradeType type = Upgrades[i];
                ShopData.UpgradeEntry entry = _shop != null ? _shop.Get(type) : null;
                int level = local != null && local.Upgrades != null ? local.Upgrades.LevelOf(type) : 0;
                int max = entry != null ? entry.maxLevel : 0;
                int cost = entry != null ? entry.cost : 0;

                label.text = level >= max
                    ? $"{Name(type)}\nLv {level}/{max}  (MAX)"
                    : $"{Name(type)}\nLv {level}/{max}  —  ${cost}";
            }

            if (_readyLabel != null)
            {
                bool ready = local != null && local.Ready != null && local.Ready.IsReady;
                _readyLabel.text = ready ? "Cancel Ready" : "Ready";
            }
        }

        private static string Name(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.Damage: return "DAMAGE +";
                case UpgradeType.MaxHp: return "MAX HP +";
                case UpgradeType.FireRate: return "FIRE RATE +";
                default: return type.ToString();
            }
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

            var canvasGo = new GameObject("ShopCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110; // above HUD, below result screen
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("ShopPanel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelRt = _panel.AddComponent<RectTransform>();
            Stretch(panelRt);
            var bg = _panel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.45f);

            var col = new GameObject("Content");
            col.transform.SetParent(_panel.transform, false);
            var colRt = col.AddComponent<RectTransform>();
            colRt.anchorMin = colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            colRt.sizeDelta = new Vector2(820, 640);
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

            MakeText(col.transform, "SHOP", 44, FontStyles.Bold, 64);
            _goldText = MakeText(col.transform, "Gold: 0", 28, FontStyles.Normal, 44);

            for (int i = 0; i < Upgrades.Length; i++)
            {
                UpgradeType type = Upgrades[i];
                Button btn = MakeButton(col.transform, "", () => OnBuy(type), out TextMeshProUGUI label, 96);
                _upgradeLabels[i] = label;
            }

            MakeButton(col.transform, "Ready", OnToggleReady, out _readyLabel, 80);
            MakeText(col.transform, "(next wave starts when everyone is Ready — or the timer runs out)",
                20, FontStyles.Italic, 34);
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
            out TextMeshProUGUI labelTmp, float minHeight)
        {
            var go = new GameObject("Button");
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
            labelTmp = textGo.AddComponent<TextMeshProUGUI>();
            labelTmp.text = label;
            labelTmp.fontSize = 26;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = Color.white;
            return btn;
        }
    }
}
