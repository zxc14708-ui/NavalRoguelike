using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;
using Game.TaskForce;

namespace Game.UI
{
    /// <summary>
    /// 편대 현황 패널(오른쪽 열, 2026-10-02 편대 개편). 누를 것이 없는 표시 전용:
    /// 호위함마다 역할 코드 · 이름(개량 단계) · 상태(선체 · 역할 능력 / 전투 불능 — 복귀 N초) · 선체 막대.
    /// 편대가 없으면 숨는다(오른쪽 열 자리도 비운다 — HUDView.LayoutRightColumn이 VisiblePanel을 본다).
    /// 예전의 출항 편성 화면(FORCE PACKAGE)·지원 요청 버튼·지원 알림은 지원 스킬과 함께 없앴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskForcePanelUI : MonoBehaviour
    {
        private sealed class Row
        {
            public RectTransform Root;
            public Image Background;
            public Image CodeTile;
            public TMP_Text Code;
            public TMP_Text Name;
            public TMP_Text Status;
            public Image HullFill;
        }

        private static readonly Color Console = new(0.018f, 0.055f, 0.075f, 0.96f);
        private static readonly Color Console2 = new(0.035f, 0.105f, 0.125f, 0.98f);
        private static readonly Color Line = new(0.14f, 0.42f, 0.45f, 0.9f);
        private static readonly Color Green = new(0.39f, 1f, 0.64f, 1f);
        private static readonly Color Amber = new(1f, 0.72f, 0.28f, 1f);
        private static readonly Color Dim = new(0.40f, 0.54f, 0.57f, 1f);
        private static readonly Color Alert = new(1f, 0.32f, 0.24f, 1f);

        public const float PanelWidth = 270f;
        private const float HeaderHeight = 30f, RowHeight = 40f, RowGap = 4f, Pad = 6f;

        private static TaskForcePanelUI s_instance;
        private TaskForceEscortFormation _formation;
        private TMP_FontAsset _font;
        private CanvasGroup _group;
        private TMP_Text _title;
        private readonly List<Row> _rows = new();
        private float _nextRefresh;

        /// <summary>오른쪽 열 배치용: 편대가 있으면 패널, 없으면 null.</summary>
        public static RectTransform VisiblePanel
            => s_instance != null && s_instance._rows.Count > 0 ? (RectTransform)s_instance.transform : null;

        public static void Ensure(TaskForceEscortFormation formation)
        {
            if (formation == null) return;
            if (s_instance != null) { s_instance.Bind(formation); return; }

            Canvas canvas = null;
            foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate != null && candidate.isRootCanvas) { canvas = candidate; break; }
            if (canvas == null) return;

            var root = NewRect("Fleet status", canvas.transform);
            root.anchorMin = root.anchorMax = Vector2.one;
            root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-14f, -380f);
            root.sizeDelta = new Vector2(PanelWidth, HeaderHeight);
            root.gameObject.AddComponent<TaskForcePanelUI>().Build(root, formation);
        }

        private void Build(RectTransform root, TaskForceEscortFormation formation)
        {
            s_instance = this;
            _font = FindFont();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;   // 표시 전용
            var background = gameObject.AddComponent<Image>();
            background.color = Console;
            background.raycastTarget = false;
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1f, -1f);
            _title = Label(root, "Title", "편대", Vector2.zero, new Vector2(PanelWidth - 20, 24), 15, Green, FontStyles.Bold);
            Bind(formation);
        }

        private void Bind(TaskForceEscortFormation formation)
        {
            if (_formation != null) _formation.Changed -= Rebuild;
            _formation = formation;
            if (_formation != null) _formation.Changed += Rebuild;
            Rebuild();
        }

        private void OnDestroy()
        {
            if (_formation != null) _formation.Changed -= Rebuild;
            if (s_instance == this) s_instance = null;
        }

        /// <summary>편대 척수에 맞춰 줄을 다시 만든다.</summary>
        private void Rebuild()
        {
            int count = _formation != null ? _formation.EscortCount : 0;
            if (count != _rows.Count)
            {
                foreach (var r in _rows) if (r.Root != null) Destroy(r.Root.gameObject);
                _rows.Clear();
                var root = (RectTransform)transform;
                float height = HeaderHeight + count * (RowHeight + RowGap) + Pad;
                root.sizeDelta = new Vector2(PanelWidth, height);
                Place(_title.rectTransform, new Vector2(10, height - 27), new Vector2(PanelWidth - 20, 24));
                float y = height - HeaderHeight - RowHeight;
                float w = PanelWidth - Pad * 2;
                for (int i = 0; i < count; i++)
                {
                    var rowRect = NewRect($"Escort row {i + 1}", root);
                    Place(rowRect, new Vector2(Pad, y), new Vector2(w, RowHeight));
                    var bg = rowRect.gameObject.AddComponent<Image>();
                    bg.color = Console2;
                    bg.raycastTarget = false;
                    var tile = Box(rowRect, "Code tile", Vector2.zero, new Vector2(46, RowHeight), Color.white);
                    var code = Label(tile.rectTransform, "Code", "", Vector2.zero, new Vector2(46, RowHeight), 15, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                    var name = Label(rowRect, "Name", "", new Vector2(54, 16), new Vector2(w - 58, 22), 15, Color.white, FontStyles.Bold);
                    var status = Label(rowRect, "Status", "", new Vector2(54, 3), new Vector2(w - 60, 16), 12, Green);
                    var rail = Box(rowRect, "Hull rail", new Vector2(46, 0), new Vector2(w - 46, 3), new Color(0.05f, 0.13f, 0.15f, 1f));
                    var fill = Box(rail.rectTransform, "Hull fill", Vector2.zero, Vector2.zero, Green);
                    fill.rectTransform.anchorMax = Vector2.one;
                    fill.rectTransform.sizeDelta = Vector2.zero;
                    fill.type = Image.Type.Filled;
                    fill.fillMethod = Image.FillMethod.Horizontal;
                    _rows.Add(new Row { Root = rowRect, Background = bg, CodeTile = tile, Code = code, Name = name, Status = status, HullFill = fill });
                    y -= RowHeight + RowGap;
                }
            }
            Refresh();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool visible = gm != null && gm.State == GameState.Playing && _rows.Count > 0;
            _group.alpha = visible ? 1f : 0f;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            if (_formation != null && _formation.EscortCount != _rows.Count) Rebuild();
            else Refresh();
        }

        private void Refresh()
        {
            if (_formation == null) return;
            if (_title != null)
                _title.text = $"편대  <size=80%><color=#8fa4b8>{_formation.EscortCount}/{TaskForceEscortFormation.MaxEscorts}척 · </color>" +
                              $"<color=#9fe0a0>{FleetFormations.Name(_formation.Formation)}</color> " +
                              $"<color=#8fa4b8>[{GameSettings.BindingLabel(NavalControl.Formation)}]</color></size>";
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var info = _formation.GetInfo(_formation.IndexByRosterOrder(i));   // 편대 슬롯 번호 순서로
                Color rc = TaskForceEscortFormation.RoleColor(info.Role);
                row.CodeTile.color = rc * new Color(1f, 1f, 1f, 0.28f);
                row.Code.text = TaskForceEscortFormation.RoleCode(info.Role);
                row.Code.color = rc;
                string tier = info.Role != EscortRole.None && info.Tier > 0 ? $"  <size=75%><color=#9fd8e0>개량 {info.Tier}</color></size>" : "";
                row.Name.text = info.Name + tier;
                if (info.Disabled)
                {
                    row.Background.color = new Color(0.16f, 0.05f, 0.05f, 0.98f);
                    row.Name.color = Dim;
                    row.Status.color = Alert;
                    row.Status.text = $"전투 불능 — 복귀 {info.RecoverRemaining:0}초";
                    row.HullFill.color = Alert;
                    row.HullFill.fillAmount = 1f - Mathf.Clamp01(info.RecoverRemaining / TaskForceEscortFormation.RecoverSeconds);
                    continue;
                }
                row.Background.color = Console2;
                row.Name.color = Color.white;
                Color hc = info.Hull01 < 0.25f ? Alert : info.Hull01 < 0.5f ? Amber : Green;
                row.Status.color = hc;
                row.Status.text = $"선체 {Mathf.CeilToInt(info.Hull01 * 100f)}% · {TaskForceEscortFormation.RoleShort(info.Role)}";
                row.HullFill.color = hc;
                row.HullFill.fillAmount = info.Hull01;
            }
        }

        // ------------------------------------------------------------ 도우미

        private TMP_FontAsset FindFont()
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text != null && text.font != null) return text.font;
            return TMP_Settings.defaultFontAsset;
        }

        private Image Box(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = NewRect(name, parent);
            Place(rect, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size,
                               float fontSize, Color color, FontStyles style = FontStyles.Normal,
                               TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = NewRect(name, parent);
            Place(rect, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;   // 한 줄 고정(넘치면 말줄임)
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
