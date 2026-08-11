using System.Collections.Generic;
using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameUI.MainMenu
{
    /// <summary>
    /// The "who is in this room" list on <see cref="LobbyPanel"/> (task L1, decision 0019).
    ///
    /// Built in code rather than authored in the scene — same call as <c>StagingController</c>. The
    /// row count is fixed at capacity, so every slot (filled or empty) is visible at once and the
    /// list never reflows as players come and go; joining just fills the next slot in place.
    ///
    /// Plain C# (not a MonoBehaviour): nothing outside the panel addresses a row, so there is nothing
    /// for a component to buy here.
    /// </summary>
    public class LobbyRosterView
    {
        private static readonly Color PanelColor = new Color(0.10f, 0.11f, 0.15f, 0.95f);
        private static readonly Color RowFilledColor = new Color(0.17f, 0.20f, 0.28f, 1f);
        private static readonly Color RowEmptyColor = new Color(0.13f, 0.14f, 0.18f, 0.55f);
        private static readonly Color NameColor = Color.white;
        private static readonly Color EmptyTextColor = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color HostBadgeColor = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly Color OnlineDotColor = new Color(0.37f, 0.88f, 0.42f, 1f);
        private static readonly Color EmptyDotColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color YouColor = new Color(0.62f, 0.71f, 1f, 1f);

        private class Row
        {
            public Image Background;
            public TextMeshProUGUI Dot;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Badge;
        }

        private readonly List<Row> rows = new List<Row>();
        private TextMeshProUGUI countText;

        /// <summary>Builds the roster card under <paramref name="parent"/>. Call once.</summary>
        public void Build(Transform parent, int capacity)
        {
            var card = new GameObject("RosterCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            card.GetComponent<Image>().color = PanelColor;

            // Fill whatever slot the scene gave us, so the card's size is authored in the scene
            // (RosterRoot) rather than hard-coded here.
            var cardRt = (RectTransform)card.transform;
            cardRt.anchorMin = Vector2.zero;
            cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;

            var layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 16);
            layout.spacing = 8;
            layout.childControlWidth = true;
            // Must be true, or the group lays rows out at their raw RectTransform height (100 for a
            // fresh GameObject) and the list overflows the card onto the buttons below it.
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // Header: "PLAYERS" on the left, "2/4" on the right.
            var header = MakeRow(card.transform, "Header", 30);
            var title = MakeLabel(header.transform, "PLAYERS", 22, FontStyles.Bold, TextAlignmentOptions.Left);
            title.color = new Color(1f, 1f, 1f, 0.6f);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            StretchToAnchors(title.rectTransform);

            countText = MakeLabel(header.transform, $"0/{capacity}", 22, FontStyles.Bold, TextAlignmentOptions.Right);
            countText.color = new Color(1f, 1f, 1f, 0.6f);
            countText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            countText.rectTransform.anchorMax = new Vector2(1f, 1f);
            StretchToAnchors(countText.rectTransform);

            for (int i = 0; i < capacity; i++)
                rows.Add(MakePlayerRow(card.transform, i));
        }

        /// <summary>
        /// Repaints every slot from the latest roster. <paramref name="localClientId"/> tags the local
        /// player's own row with "(คุณ)" — with 4 near-identical rows it is otherwise easy to lose
        /// track of which one is you. Pass -1 when the local client id isn't known yet.
        /// </summary>
        public void Refresh(IReadOnlyList<LobbyRosterEntry> roster, int capacity, int localClientId)
        {
            int filled = roster?.Count ?? 0;
            if (countText != null)
                countText.text = $"{Mathf.Min(filled, capacity)}/{capacity}";

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                bool occupied = roster != null && i < roster.Count;

                if (!occupied)
                {
                    row.Background.color = RowEmptyColor;
                    row.Dot.text = "○";
                    row.Dot.color = EmptyDotColor;
                    row.Name.text = "— empty —";
                    row.Name.color = EmptyTextColor;
                    row.Badge.text = string.Empty;
                    continue;
                }

                LobbyRosterEntry entry = roster[i];
                row.Background.color = RowFilledColor;
                row.Dot.text = "●";
                row.Dot.color = OnlineDotColor;
                row.Name.color = NameColor;

                // Names are sanitized server-side (LobbyManager.SanitizeName) before they get here, so
                // this label can't be used to inject rich-text into the roster.
                //
                // Labels here are English on purpose: the project's TMP font asset has no Thai glyphs,
                // so Thai renders as tofu boxes (verified in the L1 play test). See decision 0019.
                row.Name.text = entry.ClientId == localClientId
                    ? $"{entry.Name}  <color=#9EB5FF>(you)</color>"
                    : entry.Name;

                row.Badge.text = entry.IsHost ? "HOST" : string.Empty;
                row.Badge.color = HostBadgeColor;
            }
        }

        // ---- construction helpers ----

        private static Row MakePlayerRow(Transform parent, int index)
        {
            GameObject go = MakeRow(parent, $"Slot{index}", 52);
            var background = go.AddComponent<Image>();
            background.color = RowEmptyColor;

            var dot = MakeLabel(go.transform, "○", 24, FontStyles.Normal, TextAlignmentOptions.Center);
            dot.rectTransform.anchorMin = new Vector2(0f, 0f);
            dot.rectTransform.anchorMax = new Vector2(0f, 1f);
            dot.rectTransform.pivot = new Vector2(0f, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(46f, 0f);
            dot.rectTransform.anchoredPosition = Vector2.zero;

            var name = MakeLabel(go.transform, string.Empty, 26, FontStyles.Normal, TextAlignmentOptions.Left);
            name.rectTransform.anchorMin = new Vector2(0f, 0f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(52f, 0f);
            name.rectTransform.offsetMax = new Vector2(-96f, 0f);

            var badge = MakeLabel(go.transform, string.Empty, 18, FontStyles.Bold, TextAlignmentOptions.Right);
            badge.rectTransform.anchorMin = new Vector2(1f, 0f);
            badge.rectTransform.anchorMax = new Vector2(1f, 1f);
            badge.rectTransform.pivot = new Vector2(1f, 0.5f);
            badge.rectTransform.sizeDelta = new Vector2(90f, 0f);
            badge.rectTransform.anchoredPosition = new Vector2(-12f, 0f);

            return new Row { Background = background, Dot = dot, Name = name, Badge = badge };
        }

        private static GameObject MakeRow(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return go;
        }

        private static TextMeshProUGUI MakeLabel(Transform parent, string text, float size, FontStyles style,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = NameColor;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        private static void StretchToAnchors(RectTransform rt)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
