using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.TaskForce;

namespace Game.UI
{
    /// <summary>
    /// 진형 선택판(2026-10-03, 하단 HUD 스킬 줄과 무장 패널 사이). 진형 셋(함대원형진 · 단종진 · 자율)을 작은 도식으로 보여 주고
    /// 클릭으로 바꾼다(G 키 순환도 그대로). 도식의 점은 편대 슬롯 1~4 — 호위함이 있는 슬롯은 역할색, 빈 슬롯은 흐리게.
    /// 기함은 오른쪽을 향한 막대(선수 = 오른쪽). 마우스를 올리면 제목 줄에 그 진형 설명이 나온다.
    /// 편대가 없거나 전투 중이 아니면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FormationSelectorUI : MonoBehaviour
    {
        private static readonly Color Console = new(0.018f, 0.055f, 0.075f, 0.96f);
        private static readonly Color Tile = new(0.03f, 0.09f, 0.11f, 1f);
        private static readonly Color TileOn = new(0.06f, 0.24f, 0.22f, 1f);
        private static readonly Color Line = new(0.14f, 0.42f, 0.45f, 0.9f);
        private static readonly Color Green = new(0.39f, 1f, 0.64f, 1f);
        private static readonly Color Gold = new(0.88f, 0.75f, 0.35f, 1f);
        private static readonly Color Dim = new(0.40f, 0.54f, 0.57f, 1f);
        private static readonly Color Hull = new(0.78f, 0.84f, 0.88f, 1f);

        public const float Width = 432f, Height = 118f;
        private const float TileWidth = 136f, TileHeight = 84f, Gap = 6f, Pad = 7f;
        /// <summary>도식 축척(픽셀/m): 기함 둘레 약 18m가 타일 안에 들어가게.</summary>
        private const float DiagramScale = 1.15f;

        private sealed class Option
        {
            public FleetFormation Formation;
            public RectTransform Rect;
            public Image Background;
            public Outline Border;
            public TMP_Text Label;
            public readonly List<Image> Dots = new();
            public readonly List<TMP_Text> Numbers = new();
        }

        private static FormationSelectorUI s_instance;
        private TaskForceEscortFormation _formation;
        private TMP_FontAsset _font;
        private CanvasGroup _group;
        private TMP_Text _title;
        private readonly List<Option> _options = new();
        private float _nextRefresh;

        /// <summary>검증용: 지금 선택판이 보이는가.</summary>
        public static bool IsVisible => s_instance != null && s_instance._group != null && s_instance._group.alpha > 0.5f;

        /// <summary>검증용: 그 진형 칸을 누른 것처럼 바꾼다.</summary>
        public static void Click(FleetFormation formation) { if (s_instance != null) s_instance.Choose(formation); }

        public static void Ensure(TaskForceEscortFormation formation)
        {
            if (formation == null) return;
            if (s_instance != null) { s_instance._formation = formation; s_instance.Refresh(); return; }

            Transform parent = null;
            var hud = Object.FindFirstObjectByType<HUDView>();
            if (hud != null) parent = hud.transform;
            if (parent == null)
                foreach (var candidate in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (candidate != null && candidate.isRootCanvas) { parent = candidate.transform; break; }
            if (parent == null) return;

            var root = NewRect("Formation selector", parent);
            // 하단 오른쪽: 무장 패널(오른쪽 끝에서 약 229px) 왼쪽, 스킬 줄(가운데 440px) 오른쪽
            root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-14f - WeaponStatusPanelUI.Width * 0.82f - 14f, 14f);
            root.sizeDelta = new Vector2(Width, Height);
            root.gameObject.AddComponent<FormationSelectorUI>().Build(root, formation);
        }

        private void Build(RectTransform root, TaskForceEscortFormation formation)
        {
            s_instance = this;
            _formation = formation;
            _font = FindFont();
            _group = gameObject.AddComponent<CanvasGroup>();
            var bg = gameObject.AddComponent<Image>();
            bg.color = Console;
            bg.raycastTarget = true;   // 판 위 클릭이 바다(카메라 조작)로 새지 않게
            var outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1f, -1f);

            _title = Label(root, "Title", "", new Vector2(Pad + 2f, Height - 25f), new Vector2(Width - Pad * 2 - 4f, 22f), 14, Green);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            _title.overflowMode = TextOverflowModes.Ellipsis;

            for (int i = 0; i < FleetFormations.All.Length; i++)
                _options.Add(BuildOption(root, FleetFormations.All[i], new Vector2(Pad + i * (TileWidth + Gap), Pad)));
            Refresh();
        }

        private Option BuildOption(RectTransform root, FleetFormation f, Vector2 position)
        {
            var o = new Option { Formation = f };
            o.Rect = NewRect($"Formation {f}", root);
            Place(o.Rect, position, new Vector2(TileWidth, TileHeight));
            o.Background = o.Rect.gameObject.AddComponent<Image>();
            o.Background.color = Tile;
            o.Border = o.Rect.gameObject.AddComponent<Outline>();
            o.Border.effectDistance = new Vector2(2f, -2f);
            var button = o.Rect.gameObject.AddComponent<Button>();
            button.targetGraphic = o.Background;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                Choose(f);
                // 선택된 채로 두면 Space·Enter(UI 제출)가 다시 누르게 된다
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            });

            // 도식: 가운데 (타일 위쪽 62px 영역), 기함은 오른쪽을 향한 막대
            var diagram = NewRect("Diagram", o.Rect);
            Place(diagram, new Vector2(0f, 20f), new Vector2(TileWidth, TileHeight - 20f));
            var center = new Vector2(TileWidth * 0.5f, (TileHeight - 20f) * 0.5f);
            if (f == FleetFormation.Column) center.x += 34f;   // 단종진은 줄이 뒤(왼쪽)로 길다
            Box(diagram, "Flagship", center - new Vector2(10f, 3f), new Vector2(20f, 6f), Hull);
            Box(diagram, "Bow", center + new Vector2(6f, -2f), new Vector2(5f, 4f), Gold);   // 선수(오른쪽) 표시

            if (f == FleetFormation.Autonomous)
            {
                // 점선 고리(움직이는 범위) + 흩어진 점
                for (int k = 0; k < 14; k++)
                {
                    float a = k * Mathf.PI * 2f / 14f;
                    Box(diagram, "Ring", center + new Vector2(Mathf.Cos(a) * 30f, Mathf.Sin(a) * 25f) - Vector2.one, new Vector2(2f, 2f), Dim);
                }
            }
            for (int s = 0; s < TaskForceEscortFormation.MaxEscorts; s++)
            {
                Vector2 p = DotPosition(f, s);
                var dot = Box(diagram, $"Slot {s + 1}", center + p - new Vector2(6f, 6f), new Vector2(12f, 12f), Dim);
                var num = Label(dot.rectTransform, "Number", (s + 1).ToString(), Vector2.zero, new Vector2(12f, 12f), 9, Color.black, TextAlignmentOptions.Center);
                num.fontStyle = FontStyles.Bold;
                o.Dots.Add(dot);
                o.Numbers.Add(num);
            }

            o.Label = Label(o.Rect, "Name", FleetFormations.Name(f), new Vector2(0f, 2f), new Vector2(TileWidth, 18f), 13, Color.white, TextAlignmentOptions.Center);
            return o;
        }

        /// <summary>도식 위 슬롯 점 위치(픽셀, 기함 중심 기준, 오른쪽 = 선수).</summary>
        private static Vector2 DotPosition(FleetFormation f, int slot)
        {
            switch (f)
            {
                case FleetFormation.Column:
                    return new Vector2(-(18f + slot * 15f), 0f);
                case FleetFormation.Autonomous:
                    // 고정 자리가 없다: 고리 안 아무 데나 흩어 놓은 모양
                    return slot switch
                    {
                        0 => new Vector2(22f, 13f),
                        1 => new Vector2(-8f, -19f),
                        2 => new Vector2(-27f, 6f),
                        _ => new Vector2(12f, -15f),
                    };
                default:
                {
                    // 함대원형진: 실제 슬롯 모양(선수 = 오른쪽으로 돌려서)
                    var v = FleetFormations.Slot(FleetFormation.Circular, slot, TaskForceEscortFormation.MaxEscorts, 6f, -6f, 1.5f);
                    return new Vector2(v.y, -v.x) * DiagramScale;
                }
            }
        }

        private void Choose(FleetFormation f)
        {
            if (_formation == null) return;
            _formation.SetFormation(f);
            Refresh();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool visible = gm != null && gm.State == GameState.Playing && _formation != null && _formation.EscortCount > 0;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = _group.interactable = visible;
            if (!visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            if (_formation == null || _title == null) return;
            var current = _formation.Formation;

            // 마우스를 올린 칸이 있으면 그 진형 설명, 없으면 지금 진형 설명
            var shown = current;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                foreach (var o in _options)
                    if (RectTransformUtility.RectangleContainsScreenPoint(o.Rect, p, null)) { shown = o.Formation; break; }
            }
            _title.text = $"<b>진형</b> <color=#8fa4b8>[{GameSettings.BindingLabel(NavalControl.Formation)}]</color>  " +
                          $"<size=85%><color=#c8d4dc>{FleetFormations.Summary(shown)}</color></size>";

            foreach (var o in _options)
            {
                bool on = o.Formation == current;
                o.Background.color = on ? TileOn : Tile;
                o.Border.effectColor = on ? Green : new Color(Line.r, Line.g, Line.b, 0.5f);
                o.Label.color = on ? Gold : Dim;
                o.Label.text = on ? $"<b>{FleetFormations.Name(o.Formation)}</b>" : FleetFormations.Name(o.Formation);
                for (int s = 0; s < o.Dots.Count; s++)
                {
                    int index = _formation.IndexOfRosterSlot(s);
                    if (index < 0)
                    {
                        o.Dots[s].color = new Color(Dim.r, Dim.g, Dim.b, 0.28f);
                        o.Numbers[s].color = new Color(0.7f, 0.8f, 0.85f, 0.45f);
                        continue;
                    }
                    var info = _formation.GetInfo(index);
                    Color rc = info.Disabled ? new Color(1f, 0.32f, 0.24f) : TaskForceEscortFormation.RoleColor(info.Role);
                    o.Dots[s].color = on ? rc : new Color(rc.r, rc.g, rc.b, 0.55f);
                    o.Numbers[s].color = Color.black;
                }
            }
        }

        // ------------------------------------------------------------ 도구

        private TMP_FontAsset FindFont()
        {
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (text != null && text.font != null) return text.font;
            return TMP_Settings.defaultFontAsset;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>왼쪽 아래 기준 배치.</summary>
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image Box(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = NewRect(name, parent);
            Place(rect, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text Label(RectTransform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color,
                               TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var rect = NewRect(name, parent);
            Place(rect, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }
    }
}
