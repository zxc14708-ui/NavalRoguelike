using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 메인 화면의 사전(무장 · 호위함 · 적). 왼쪽 목록에서 고르면 오른쪽에 3D 미리보기(형태·강화 단계 버튼)와
    /// 분류 줄·설명·수치가 나온다. 내용은 <see cref="CodexCatalog"/>가 지금 게임 데이터에서 만든다.
    /// 메인 화면 위 덮개 하나로, 닫으면 숨겨 두고 다시 열 때 그대로 쓴다.
    /// </summary>
    public sealed class CodexUI : MonoBehaviour
    {
        private static readonly Color Backdrop = new(0.005f, 0.025f, 0.03f, 0.97f);
        private static readonly Color ConsoleBackground = new(0.015f, 0.09f, 0.065f, 1f);
        private static readonly Color ConsoleIdle = new(0.035f, 0.19f, 0.12f, 1f);
        private static readonly Color ConsoleActive = new(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color ConsoleText = new(0.66f, 1f, 0.75f);
        private static readonly Color ListIdle = new(0.02f, 0.11f, 0.075f, 1f);
        private static readonly Color PagePanel = new(0.012f, 0.065f, 0.05f, 0.85f);

        private TMP_FontAsset _font;
        private readonly Dictionary<CodexSection, List<CodexEntry>> _entries = new();
        private readonly List<Image> _tabImages = new();
        private readonly List<(Image image, TMP_Text text)> _listItems = new();
        private readonly List<Image> _modelButtons = new();
        private RectTransform _listContent, _bodyContent, _modelRow;
        private ScrollRect _listScroll, _bodyScroll;
        private TMP_Text _title, _tag, _body, _count;
        private Image _titleAccent;
        private CodexPreview _preview;

        public CodexSection Section { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public int SelectedModel { get; private set; }
        public CodexEntry Selected => SelectedIndex >= 0 && SelectedIndex < Entries(Section).Count ? Entries(Section)[SelectedIndex] : null;
        /// <summary>검증용: 미리보기에 올라간 메시 수.</summary>
        public int PreviewRendererCount => _preview != null ? _preview.RendererCount : 0;
        public string BodyText => _body != null ? _body.text : "";
        /// <summary>검증용: 미리보기를 지금 그려 모델이 차지한 화면 비율(0~1)을 잰다.</summary>
        public float MeasurePreview() => _preview != null ? _preview.RenderAndMeasure() : 0f;

        public static CodexUI Create(Transform parent, TMP_FontAsset font)
        {
            var overlay = Box(parent, "Codex overlay", Vector2.zero, Vector2.zero, Backdrop, true);
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            var ui = overlay.gameObject.AddComponent<CodexUI>();
            ui._font = font;
            ui.Build(overlay);
            return ui;
        }

        public List<CodexEntry> Entries(CodexSection section)
        {
            if (!_entries.TryGetValue(section, out var list))
            {
                list = CodexCatalog.Build(section);
                _entries[section] = list;
            }
            return list;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowSection(Section);
        }

        public void Close() => gameObject.SetActive(false);

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        // ------------------------------------------------------------ 화면 구성

        private void Build(RectTransform overlay)
        {
            var content = Box(overlay, "Codex console", new Vector2(-580, -335), new Vector2(1160, 670), ConsoleBackground);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            Box(content, "Top signal", new Vector2(0, 666), new Vector2(1160, 4), ConsoleActive);
            Text(content, "Eyebrow", "FLEET COMMAND  /  NAVAL CODEX", new Vector2(26, 624), new Vector2(700, 27), 16, ConsoleText);
            Text(content, "Title", "함정 사전", new Vector2(26, 572), new Vector2(420, 51), 35, Color.white);
            _count = Text(content, "Count", "", new Vector2(700, 580), new Vector2(434, 30), 16, ConsoleText);
            _count.alignment = TextAlignmentOptions.Right;

            var sections = new[] { CodexSection.Weapons, CodexSection.Escorts, CodexSection.Enemies };
            for (int i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                var tab = Btn(content, CodexCatalog.SectionName(section), new Vector2(26 + i * 156, 522), new Vector2(146, 42), 19);
                tab.name = $"Codex tab {section}";
                tab.onClick.AddListener(() => ShowSection(section));
                _tabImages.Add(tab.GetComponent<Image>());
            }

            _listContent = Scroll(content, "Entry list", new Vector2(26, 62), new Vector2(300, 446), out _listScroll);
            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 4;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 오른쪽: 이름 · 분류 줄 · 미리보기 · 형태 버튼 · 설명
            _titleAccent = Box(content, "Title accent", new Vector2(342, 470), new Vector2(5, 34), ConsoleText).GetComponent<Image>();
            _title = Text(content, "Entry title", "", new Vector2(356, 466), new Vector2(778, 42), 30, Color.white);
            _tag = Text(content, "Entry tag", "", new Vector2(342, 434), new Vector2(792, 28), 16, ConsoleText);

            var frame = Box(content, "Preview frame", new Vector2(340, 112), new Vector2(364, 316), PagePanel);
            var imageGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(frame, false);
            var image = imageGo.GetComponent<RawImage>();
            Place(image.rectTransform, new Vector2(2, 2), new Vector2(360, 312));
            var empty = Text(frame, "No model", "모델 없음", new Vector2(0, 140), new Vector2(364, 36), 20, ConsoleText);
            empty.alignment = TextAlignmentOptions.Center;
            _preview = CodexPreview.Create(image, empty.gameObject);
            Text(frame, "Drag hint", "끌어서 회전", new Vector2(8, 4), new Vector2(200, 22), 13, new Color(0.55f, 0.73f, 0.65f));

            _modelRow = Box(content, "Model buttons", new Vector2(340, 62), new Vector2(364, 42), Color.clear);

            _bodyContent = Scroll(content, "Entry body", new Vector2(716, 62), new Vector2(418, 366), out _bodyScroll);
            _body = _bodyContent.gameObject.AddComponent<TextMeshProUGUI>();
            _body.font = _font;
            _body.fontSize = 16;
            _body.color = Color.white;
            _body.richText = true;
            _body.raycastTarget = false;
            _body.margin = new Vector4(12, 10, 12, 10);
            _body.lineSpacing = 4;
            _bodyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text(content, "Footer", "목록 선택 · 휠로 스크롤 · ESC 또는 [메뉴로]로 닫기 · 수치는 지금 게임 데이터 기준",
                new Vector2(26, 18), new Vector2(780, 28), 15, ConsoleText);
            var close = Btn(content, "메뉴로", new Vector2(980, 12), new Vector2(154, 40), 17);
            close.name = "Codex close";
            close.onClick.AddListener(Close);
        }

        public void ShowSection(CodexSection section)
        {
            Section = section;
            for (int i = 0; i < _tabImages.Count; i++) _tabImages[i].color = i == (int)section ? ConsoleActive : ConsoleIdle;

            foreach (Transform child in _listContent) Destroy(child.gameObject);
            _listItems.Clear();
            var list = Entries(section);
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                var item = new GameObject(list[i].Title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                item.transform.SetParent(_listContent, false);
                item.GetComponent<LayoutElement>().preferredHeight = 40;
                var img = item.GetComponent<Image>();
                img.color = ListIdle;
                var btn = item.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => Select(index));
                var label = Text(item.transform, "Text", list[i].Title, new Vector2(14, 0), new Vector2(270, 40), 18, Color.white);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                _listItems.Add((img, label));
            }
            _count.text = $"{CodexCatalog.SectionName(section)} {list.Count}종";
            _listScroll.verticalNormalizedPosition = 1f;
            Select(list.Count > 0 ? 0 : -1);
        }

        public void Select(int index)
        {
            var list = Entries(Section);
            SelectedIndex = index;
            for (int i = 0; i < _listItems.Count; i++)
            {
                _listItems[i].image.color = i == index ? ConsoleActive : ListIdle;
                _listItems[i].text.color = i == index ? Color.white : new Color(0.82f, 0.92f, 0.86f);
            }
            var entry = Selected;
            _title.text = entry != null ? entry.Title : "";
            _tag.text = entry != null ? entry.Tag : "";
            _body.text = entry != null ? entry.Body : "항목이 없습니다.";
            _titleAccent.color = entry != null ? entry.Accent : ConsoleText;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bodyContent);
            _bodyScroll.verticalNormalizedPosition = 1f;

            foreach (Transform child in _modelRow) Destroy(child.gameObject);
            _modelButtons.Clear();
            int count = entry != null ? entry.Models.Count : 0;
            if (count > 1)
            {
                float width = Mathf.Min(120f, (364f - 6f * (count - 1)) / count);
                for (int i = 0; i < count; i++)
                {
                    int model = i;
                    var b = Btn(_modelRow, entry.Models[i].Label, new Vector2(i * (width + 6f), 0), new Vector2(width, 40), 15);
                    b.onClick.AddListener(() => SelectModel(model));
                    _modelButtons.Add(b.GetComponent<Image>());
                }
            }
            SelectModel(count > 0 ? entry.Models.Count - 1 : -1);
        }

        /// <summary>형태·강화 단계 고르기(처음에는 마지막 = 가장 강화된 외형).</summary>
        public void SelectModel(int model)
        {
            var entry = Selected;
            SelectedModel = model;
            for (int i = 0; i < _modelButtons.Count; i++) _modelButtons[i].color = i == model ? ConsoleActive : ConsoleIdle;
            if (entry == null || model < 0 || model >= entry.Models.Count) { _preview.Show(null, null); return; }
            var m = entry.Models[model];
            _preview.Show(m.Prefab, m.OnlyChild);
        }

        // ------------------------------------------------------------ 도우미

        private static RectTransform Box(Transform parent, string name, Vector2 position, Vector2 size, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, position, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            Place(text.rectTransform, position, size);
            return text;
        }

        private Button Btn(Transform parent, string title, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Box(parent, title, position, size, ConsoleIdle, true);
            var label = Text(rect, "Text", title, Vector2.zero, size, fontSize, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            return button;
        }

        /// <summary>세로 스크롤 영역. 반환값은 내용 칸(위쪽 기준, 폭은 영역에 맞춤).</summary>
        private static RectTransform Scroll(Transform parent, string name, Vector2 position, Vector2 size, out ScrollRect scroll)
        {
            var frame = Box(parent, name, position, size, PagePanel, true);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(frame, false);
            var vp = (RectTransform)viewport.transform;
            vp.anchorMin = Vector2.zero;
            vp.anchorMax = Vector2.one;
            vp.offsetMin = vp.offsetMax = Vector2.zero;
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(vp, false);
            var rect = (RectTransform)content.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            scroll = frame.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = vp;
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = false;
            return rect;
        }
    }
}
