using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Modules;

namespace Game.UI
{
    /// <summary>
    /// 메인 화면의 사전(무장 · 호위함 · 적 · 함정·용어). 왼쪽 목록에서 고르면 오른쪽에 3D 미리보기(형태·강화 단계 버튼)와
    /// 분류 줄·설명·수치가 나온다. 내용은 <see cref="CodexCatalog"/>가 지금 게임 데이터에서 만든다.
    /// 메인 화면 위 덮개 하나로, 닫으면 숨겨 두고 다시 열 때 그대로 쓴다.
    /// 2026-10-11 점검 B·C:
    ///   - 이름 검색 · 역할 필터(전체/대공/대함/대잠/지원) · 분류순/이름순(C01)
    ///   - 형태·단계 버튼을 고르면 본문 위에 그 단계 요약(이전 단계 대비), 처음에는 기본형(C02·B05)
    ///   - 미리보기 아래 배치 도식(설치할 수 있는 자리)과 0~60 m 사거리 막대, 본문의 함께 쓰기·관련 무장 링크(C03)
    ///   - 함정·용어 탭(C04), 탭마다 고른 항목·목록 위치를 기억해 다시 열면 그대로(B06)
    /// 2026-10-13 점검 C05: 항목 즐겨찾기(★, 기록 파일에 남음 — 목록 맨 위로, 검색 옆 ★로 즐겨찾기만 보기)와 발견 기록
    ///   (만난 적·설치한 블록·합류한 호위함만 "발견" — 안 만난 항목도 잠그지 않고 "미발견"만 표시, 탭 머리에 발견 수).
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
        private static readonly Color RangeFill = new(0.35f, 0.9f, 0.55f, 1f);
        private static readonly Color RangeDead = new(0.55f, 0.22f, 0.18f, 1f);
        private const float RangeScale = 60f;
        private const float ItemHeight = 40f, ItemSpacing = 4f;

        private static readonly (CodexRole role, string name)[] Filters =
        {
            (CodexRole.None, "전체"), (CodexRole.Air, "대공"), (CodexRole.Surface, "대함"), (CodexRole.Sub, "대잠"), (CodexRole.Support, "지원"),
        };
        private static readonly CodexSection[] Sections = { CodexSection.Weapons, CodexSection.Escorts, CodexSection.Enemies, CodexSection.Guide };

        private TMP_FontAsset _font;
        private readonly Dictionary<CodexSection, List<CodexEntry>> _entries = new();
        private readonly List<Image> _tabImages = new();
        private readonly List<Image> _filterImages = new();
        private readonly List<(int index, Image image, TMP_Text text)> _listItems = new();
        private readonly List<Image> _modelButtons = new();
        private readonly Dictionary<CodexSection, string> _lastId = new();
        private readonly Dictionary<CodexSection, float> _lastScroll = new();
        private RectTransform _listContent, _bodyContent, _modelRow, _strip, _ranges;
        private ScrollRect _listScroll, _bodyScroll;
        private TMP_Text _title, _tag, _body, _count, _sortLabel, _zoneLabel;
        private RawImage _zoneImage;
        private TMP_InputField _search;
        private Image _titleAccent;
        private CodexPreview _preview;
        private CodexRole _filter;
        private bool _byName;
        private bool _favoritesOnly;
        private Image _favoritesOnlyImage, _favoriteImage;
        private TMP_Text _favoriteLabel;
        private static readonly Color Gold = new(1f, 0.82f, 0.4f);
        private string _baseBody = "";
        private bool _built;

        public CodexSection Section { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public int SelectedModel { get; private set; }
        public CodexEntry Selected => SelectedIndex >= 0 && SelectedIndex < Entries(Section).Count ? Entries(Section)[SelectedIndex] : null;
        /// <summary>검증용: 미리보기에 올라간 메시 수.</summary>
        public int PreviewRendererCount => _preview != null ? _preview.RendererCount : 0;
        public string BodyText => _body != null ? _body.text : "";
        /// <summary>검증용: 지금 목록에 보이는 항목 수(검색·필터 적용 후).</summary>
        public int VisibleCount => _listItems.Count;
        public CodexRole Filter => _filter;
        /// <summary>검증용: 즐겨찾기만 보기 · 목록에 보이는 글(★·미발견 표시 포함) · 머리 글.</summary>
        public bool FavoritesOnly => _favoritesOnly;
        public List<string> VisibleLabels { get { var l = new List<string>(); foreach (var i in _listItems) l.Add(i.text.text); return l; } }
        public string CountText => _count != null ? _count.text : "";
        /// <summary>검증용: 목록 글을 태그 해석 뒤 실제로 보이는 글자로(잘못된 태그는 글자로 남는다).</summary>
        public List<string> VisibleParsedLabels { get { var l = new List<string>(); foreach (var i in _listItems) { i.text.ForceMeshUpdate(); l.Add(i.text.GetParsedText()); } return l; } }
        public string TagText => _tag != null ? _tag.text : "";
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

        /// <summary>다시 열면 마지막으로 본 탭·항목·목록 위치 그대로.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            ShowSection(Section);
        }

        public void Close()
        {
            SaveListScroll();
            gameObject.SetActive(false);
        }

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

            for (int i = 0; i < Sections.Length; i++)
            {
                var section = Sections[i];
                var tab = Btn(content, CodexCatalog.SectionName(section), new Vector2(26 + i * 156, 522), new Vector2(146, 42), 19);
                tab.name = $"Codex tab {section}";
                tab.onClick.AddListener(() => ShowSection(section));
                _tabImages.Add(tab.GetComponent<Image>());
            }
            var sort = Btn(content, "분류순", new Vector2(1004, 522), new Vector2(130, 42), 16);
            sort.name = "Codex sort";
            _sortLabel = sort.GetComponentInChildren<TMP_Text>();
            sort.onClick.AddListener(ToggleSort);

            // 왼쪽: 검색 · 역할 필터 · 목록
            _search = SearchBox(content, new Vector2(26, 474), new Vector2(252, 40));
            var favOnly = Btn(content, "★", new Vector2(282, 474), new Vector2(44, 40), 20);
            favOnly.name = "Codex favorites only";
            _favoritesOnlyImage = favOnly.GetComponent<Image>();
            favOnly.onClick.AddListener(() => SetFavoritesOnly(!_favoritesOnly));
            for (int i = 0; i < Filters.Length; i++)
            {
                var role = Filters[i].role;
                var chip = Btn(content, Filters[i].name, new Vector2(26 + i * 61, 436), new Vector2(56, 32), 15);
                chip.name = $"Codex filter {Filters[i].name}";
                chip.onClick.AddListener(() => SetFilter(role));
                _filterImages.Add(chip.GetComponent<Image>());
            }

            _listContent = Scroll(content, "Entry list", new Vector2(26, 62), new Vector2(300, 366), out _listScroll);
            var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ItemSpacing;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 오른쪽: 이름 · 분류 줄 · 미리보기 · 배치/사거리 · 형태 버튼 · 설명
            _titleAccent = Box(content, "Title accent", new Vector2(342, 470), new Vector2(5, 34), ConsoleText).GetComponent<Image>();
            _title = Text(content, "Entry title", "", new Vector2(356, 466), new Vector2(636, 42), 30, Color.white);
            var fav = Btn(content, "☆ 즐겨찾기", new Vector2(1004, 470), new Vector2(130, 36), 15);
            fav.name = "Codex favorite";
            _favoriteImage = fav.GetComponent<Image>();
            _favoriteLabel = fav.GetComponentInChildren<TMP_Text>();
            fav.onClick.AddListener(ToggleFavoriteSelected);
            _tag = Text(content, "Entry tag", "", new Vector2(342, 434), new Vector2(792, 28), 16, ConsoleText);

            var frame = Box(content, "Preview frame", new Vector2(340, 190), new Vector2(364, 238), PagePanel);
            var imageGo = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(frame, false);
            var image = imageGo.GetComponent<RawImage>();
            Place(image.rectTransform, new Vector2(2, 2), new Vector2(360, 234));
            var empty = Text(frame, "No model", "모델 없음", new Vector2(0, 100), new Vector2(364, 36), 20, ConsoleText);
            empty.alignment = TextAlignmentOptions.Center;
            _preview = CodexPreview.Create(image, empty.gameObject);
            Text(frame, "Drag hint", "끌어서 회전", new Vector2(8, 4), new Vector2(200, 22), 13, new Color(0.55f, 0.73f, 0.65f));

            _strip = Box(content, "Placement and range", new Vector2(340, 112), new Vector2(364, 72), PagePanel);
            _zoneImage = new GameObject("Zone", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            _zoneImage.transform.SetParent(_strip, false);
            _zoneImage.raycastTarget = false;
            Place(_zoneImage.rectTransform, new Vector2(6, 20), new Vector2(104, 48));
            _zoneLabel = Text(_strip, "Zone label", "", new Vector2(4, 2), new Vector2(110, 18), 12, ConsoleText);
            _zoneLabel.alignment = TextAlignmentOptions.Center;
            _ranges = Box(_strip, "Ranges", new Vector2(118, 0), new Vector2(246, 72), Color.clear);

            _modelRow = Box(content, "Model buttons", new Vector2(340, 62), new Vector2(364, 42), Color.clear);

            _bodyContent = Scroll(content, "Entry body", new Vector2(716, 62), new Vector2(418, 366), out _bodyScroll);
            _body = _bodyContent.gameObject.AddComponent<TextMeshProUGUI>();
            _body.font = _font;
            _body.fontSize = 16;
            _body.color = Color.white;
            _body.richText = true;
            _body.raycastTarget = true;   // 링크를 누를 수 있게(휠·끌기는 위의 스크롤로 넘어간다)
            _body.margin = new Vector4(12, 10, 12, 10);
            _body.lineSpacing = 4;
            _bodyContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var click = _bodyContent.gameObject.AddComponent<LinkClick>();
            click.Owner = this;
            click.Text = _body;

            Text(content, "Footer", "검색·역할로 좁히기 · 파란 밑줄은 눌러서 이동 · ESC 또는 [메뉴로]로 닫기 · 수치는 지금 게임 데이터 기준",
                new Vector2(26, 18), new Vector2(940, 28), 15, ConsoleText);
            var close = Btn(content, "메뉴로", new Vector2(980, 12), new Vector2(154, 40), 17);
            close.name = "Codex close";
            close.onClick.AddListener(Close);
            _built = true;
            PaintFilters();
        }

        private TMP_InputField SearchBox(Transform parent, Vector2 position, Vector2 size)
        {
            var rect = Box(parent, "Codex search", position, size, ListIdle, true);
            var area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(rect, false);
            var ar = (RectTransform)area.transform;
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(12, 4);
            ar.offsetMax = new Vector2(-12, -4);
            TMP_Text Fill(string name, string value, Color color)
            {
                var t = Text(ar, name, value, Vector2.zero, Vector2.zero, 17, color);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                return t;
            }
            var placeholder = Fill("Placeholder", "이름 검색…", new Color(0.55f, 0.73f, 0.65f));
            var text = Fill("Text", "", Color.white);
            var input = rect.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = ar;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.fontAsset = _font;
            input.pointSize = 17;
            input.targetGraphic = rect.GetComponent<Image>();
            input.onValueChanged.AddListener(_ => RefreshList(keepSelection: true));
            return input;
        }

        // ------------------------------------------------------------ 목록 · 필터

        /// <summary>검색어(이름·분류 줄·id에 포함).</summary>
        public void SetSearch(string query)
        {
            if (_search != null) _search.text = query ?? "";   // onValueChanged가 목록을 다시 만든다
        }

        public void SetFilter(CodexRole role)
        {
            _filter = role;
            PaintFilters();
            RefreshList(keepSelection: true);
        }

        private void ToggleSort()
        {
            _byName = !_byName;
            if (_sortLabel != null) _sortLabel.text = _byName ? "이름순" : "분류순";
            RefreshList(keepSelection: true);
        }

        private void PaintFilters()
        {
            for (int i = 0; i < _filterImages.Count; i++) _filterImages[i].color = Filters[i].role == _filter ? ConsoleActive : ConsoleIdle;
            if (_favoritesOnlyImage != null) _favoritesOnlyImage.color = _favoritesOnly ? ConsoleActive : ConsoleIdle;
        }

        /// <summary>즐겨찾기한 항목만 보기(검색·역할 필터와 함께 걸린다).</summary>
        public void SetFavoritesOnly(bool on)
        {
            _favoritesOnly = on;
            PaintFilters();
            RefreshList(keepSelection: true);
        }

        /// <summary>지금 항목의 즐겨찾기를 켜고 끈다(기록 파일에 남는다). 목록 순서·표시가 바로 바뀐다.</summary>
        public void ToggleFavoriteSelected()
        {
            var entry = Selected;
            if (entry == null) return;
            Game.Core.PlayerProfile.ToggleFavorite(entry.Id);
            Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Select);
            PaintFavorite();
            RefreshList(keepSelection: true);
        }

        private void PaintFavorite()
        {
            if (_favoriteLabel == null) return;
            bool on = Selected != null && Game.Core.PlayerProfile.IsFavorite(Selected.Id);
            _favoriteLabel.text = on ? "★ 즐겨찾기" : "☆ 즐겨찾기";
            _favoriteLabel.color = on ? Gold : Color.white;
            _favoriteImage.color = on ? ConsoleActive : ConsoleIdle;
        }

        /// <summary>발견 기록을 쓰는 탭(함정·용어는 늘 전부 보인다).</summary>
        private static bool TracksDiscovery(CodexSection section) => section != CodexSection.Guide;

        private bool Undiscovered(CodexEntry e) => TracksDiscovery(Section) && !Game.Core.PlayerProfile.IsDiscovered(e.Id);

        private static readonly Regex Tags = new("<[^>]+>");

        private bool Matches(CodexEntry e)
        {
            if (_filter != CodexRole.None && (e.Roles & _filter) == 0) return false;
            if (_favoritesOnly && !Game.Core.PlayerProfile.IsFavorite(e.Id)) return false;
            string q = _search != null ? _search.text.Trim() : "";
            if (q.Length == 0) return true;
            return e.Title.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
                || Tags.Replace(e.Tag ?? "", "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0
                || (e.Id ?? "").IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>검색·필터·정렬을 적용해 목록을 다시 만든다. keepSelection이면 보이는 한 지금 항목을 유지한다.</summary>
        private void RefreshList(bool keepSelection)
        {
            if (!_built) return;
            foreach (Transform child in _listContent) Destroy(child.gameObject);
            _listItems.Clear();
            var list = Entries(Section);
            var visible = new List<int>();
            for (int i = 0; i < list.Count; i++) if (Matches(list[i])) visible.Add(i);
            if (_byName) visible.Sort((a, b) => string.CompareOrdinal(list[a].Title, list[b].Title));
            // 즐겨찾기를 맨 위로(그 안에서는 지금 순서 그대로)
            var favorites = visible.FindAll(i => Game.Core.PlayerProfile.IsFavorite(list[i].Id));
            if (favorites.Count > 0) { visible.RemoveAll(favorites.Contains); visible.InsertRange(0, favorites); }
            foreach (int index in visible)
            {
                var item = new GameObject(list[index].Title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                item.transform.SetParent(_listContent, false);
                item.GetComponent<LayoutElement>().preferredHeight = ItemHeight;
                var img = item.GetComponent<Image>();
                img.color = ListIdle;
                var btn = item.GetComponent<Button>();
                btn.targetGraphic = img;
                int captured = index;
                btn.onClick.AddListener(() => { Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Select); Select(captured); });
                string title = list[index].Title;
                if (Game.Core.PlayerProfile.IsFavorite(list[index].Id)) title = "<color=#FFD166>★</color> " + title;
                if (Undiscovered(list[index])) title = $"<alpha=#88>{title}<alpha=#FF> <size=70%><color=#7d9a8c>미발견</color></size>";
                var label = Text(item.transform, "Text", title, new Vector2(14, 0), new Vector2(270, ItemHeight), 18, Color.white);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                _listItems.Add((index, img, label));
            }
            bool narrowed = visible.Count != list.Count;
            _count.text = narrowed ? $"{CodexCatalog.SectionName(Section)} {visible.Count} / {list.Count}종" : $"{CodexCatalog.SectionName(Section)} {list.Count}종";
            if (TracksDiscovery(Section))
            {
                int found = 0;
                foreach (var e in list) if (Game.Core.PlayerProfile.IsDiscovered(e.Id)) found++;
                _count.text += $"  ·  발견 {found}/{list.Count}";
            }
            if (keepSelection && visible.Contains(SelectedIndex)) { PaintList(); ScrollListTo(SelectedIndex); return; }
            Select(visible.Count > 0 ? visible[0] : -1);
            _listScroll.verticalNormalizedPosition = 1f;
        }

        private void PaintList()
        {
            foreach (var (index, image, text) in _listItems)
            {
                image.color = index == SelectedIndex ? ConsoleActive : ListIdle;
                text.color = index == SelectedIndex ? Color.white : new Color(0.82f, 0.92f, 0.86f);
            }
        }

        private void SaveListScroll()
        {
            if (_listScroll != null && _built) _lastScroll[Section] = _listScroll.verticalNormalizedPosition;
        }

        /// <summary>탭 전환. 그 탭에서 마지막으로 본 항목·목록 위치로 돌아간다(처음이면 첫 항목).</summary>
        public void ShowSection(CodexSection section)
        {
            if (section != Section) SaveListScroll();
            Section = section;
            for (int i = 0; i < _tabImages.Count; i++) _tabImages[i].color = Sections[i] == section ? ConsoleActive : ConsoleIdle;

            var list = Entries(section);
            int restore = _lastId.TryGetValue(section, out var id) ? list.FindIndex(e => e.Id == id) : -1;
            SelectedIndex = -1;
            RefreshList(keepSelection: false);
            bool shown = false;
            foreach (var item in _listItems) if (item.index == restore) { shown = true; break; }
            if (shown)
            {
                Select(restore);
                Canvas.ForceUpdateCanvases();
                _listScroll.verticalNormalizedPosition = _lastScroll.TryGetValue(section, out float pos) ? pos : 1f;
            }
        }

        /// <summary>링크: id의 항목으로(어느 탭이든). 검색·필터에 가려 있으면 풀고 목록을 그 항목까지 내린다.</summary>
        public bool JumpTo(string id)
        {
            foreach (var section in Sections)
            {
                int index = Entries(section).FindIndex(e => e.Id == id);
                if (index < 0) continue;
                if (!Matches(Entries(section)[index]))
                {
                    _filter = CodexRole.None;
                    _favoritesOnly = false;
                    PaintFilters();
                    if (_search != null) _search.SetTextWithoutNotify("");
                }
                if (section != Section) SaveListScroll();
                Section = section;
                for (int i = 0; i < _tabImages.Count; i++) _tabImages[i].color = Sections[i] == section ? ConsoleActive : ConsoleIdle;
                RefreshList(keepSelection: false);
                Select(index);
                ScrollListTo(index);
                return true;
            }
            return false;
        }

        private void ScrollListTo(int index)
        {
            int position = _listItems.FindIndex(i => i.index == index);
            if (position < 0) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            float content = _listContent.rect.height, view = ((RectTransform)_listScroll.transform).rect.height;
            if (content <= view) { _listScroll.verticalNormalizedPosition = 1f; return; }
            float top = 4f + position * (ItemHeight + ItemSpacing);
            _listScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((top - view * 0.4f) / (content - view));
        }

        // ------------------------------------------------------------ 항목

        public void Select(int index)
        {
            SelectedIndex = index;
            PaintList();
            var entry = Selected;
            if (entry != null) _lastId[Section] = entry.Id;
            _title.text = entry != null ? entry.Title : "";
            _tag.text = entry != null ? entry.Tag : "";
            if (entry != null && Undiscovered(entry)) _tag.text += "  ·  <color=#9fb3c0>미발견 — 전투에서 만나면 기록</color>";
            PaintFavorite();
            _baseBody = entry != null ? entry.Body : "항목이 없습니다.";
            _titleAccent.color = entry != null ? entry.Accent : ConsoleText;
            ShowStrip(entry);

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
            SelectModel(count > 0 ? 0 : -1);   // 처음에는 기본형(점검 C02) — 강화 단계는 버튼으로 비교
        }

        /// <summary>형태·강화 단계 고르기: 미리보기 모델과 본문 맨 위 단계 요약이 같이 바뀐다.</summary>
        public void SelectModel(int model)
        {
            var entry = Selected;
            SelectedModel = model;
            for (int i = 0; i < _modelButtons.Count; i++) _modelButtons[i].color = i == model ? ConsoleActive : ConsoleIdle;
            string note = entry?.ModelNote != null && model >= 0 ? entry.ModelNote(model) : null;
            _body.text = string.IsNullOrEmpty(note) ? _baseBody : note + "\n\n" + _baseBody;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bodyContent);
            _bodyScroll.verticalNormalizedPosition = 1f;
            if (entry == null || model < 0 || model >= entry.Models.Count) { _preview.Show(null, null); return; }
            var m = entry.Models[model];
            _preview.Show(m.Prefab, m.OnlyChild);
        }

        // ------------------------------------------------------------ 배치 도식 · 사거리 막대

        private void ShowStrip(CodexEntry entry)
        {
            foreach (Transform child in _ranges) Destroy(child.gameObject);
            bool zone = entry?.Zone != null;
            int ranges = entry != null ? Mathf.Min(3, entry.Ranges.Count) : 0;
            _strip.gameObject.SetActive(zone || ranges > 0);
            if (!zone && ranges == 0) return;

            _zoneImage.gameObject.SetActive(zone);
            _zoneLabel.gameObject.SetActive(zone);
            if (zone)
            {
                _zoneImage.texture = ZoneTexture(entry.Zone.Value);
                _zoneLabel.text = ZoneName(entry.Zone.Value);
            }
            _ranges.anchoredPosition = new Vector2(zone ? 118 : 6, 0);
            float width = zone ? 240f : 352f;
            float labelW = 52f, valueW = 54f, trackW = width - labelW - valueW - 8f;
            float rowH = 20f, top = 72f - (72f - ranges * rowH) * 0.5f;
            if (ranges == 0)
            {
                var none = Text(_ranges, "No range", "사거리 없음 · 자리만 정해진 블록", new Vector2(0, 26), new Vector2(width, 20), 13, ConsoleText);
                none.alignment = TextAlignmentOptions.Center;
            }
            for (int i = 0; i < ranges; i++)
            {
                var r = entry.Ranges[i];
                float y = top - (i + 1) * rowH;
                Text(_ranges, "Label", r.Label, new Vector2(0, y), new Vector2(labelW, rowH), 13, ConsoleText);
                var track = Box(_ranges, "Track", new Vector2(labelW + 4, y + 7), new Vector2(trackW, 6), new Color(0.15f, 0.22f, 0.2f, 1f));
                float x0 = Mathf.Clamp01(r.Min / RangeScale) * trackW, x1 = Mathf.Clamp01(r.Max / RangeScale) * trackW;
                if (x0 > 0f) Box(track, "Dead", Vector2.zero, new Vector2(x0, 6), RangeDead);
                Box(track, "Fill", new Vector2(x0, 0), new Vector2(Mathf.Max(2f, x1 - x0), 6), RangeFill);
                for (int t = 1; t < 3; t++) Box(track, "Tick", new Vector2(trackW * t / 3f, -2), new Vector2(1, 10), new Color(1f, 1f, 1f, 0.25f));
                string value = r.Min > 0f ? $"{r.Min:0}~{r.Max:0.#} m" : $"{r.Max:0.#} m";
                if (r.Max > RangeScale) value += "+";
                var v = Text(_ranges, "Value", value, new Vector2(labelW + 8 + trackW, y), new Vector2(valueW, rowH), 12, Color.white);
                v.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private static string ZoneName(PlacementZone zone) => zone switch
        {
            PlacementZone.BowOnly => "뱃머리만",
            PlacementZone.SternOnly => "선미만",
            PlacementZone.SideOnly => "현측만",
            PlacementZone.SideOrStern => "현측·선미",
            _ => "어디든",
        };

        private static readonly Dictionary<PlacementZone, Texture2D> s_zones = new();

        /// <summary>위에서 본 함체(오른쪽이 뱃머리)에 설치할 수 있는 자리를 초록으로 칠한 그림.</summary>
        private static Texture2D ZoneTexture(PlacementZone zone)
        {
            if (s_zones.TryGetValue(zone, out var cached) && cached != null) return cached;
            const int w = 104, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var allowed = new Color(0.35f, 0.9f, 0.5f, 1f);
            var hull = new Color(0.16f, 0.25f, 0.22f, 1f);
            var edge = new Color(0.66f, 1f, 0.75f, 1f);
            var px = new Color[w * h];
            float cy = (h - 1) * 0.5f;
            float Half(int x) => x < 8 ? 0f : x < 76 ? 16f : Mathf.Lerp(16f, 0f, (x - 76) / 24f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float half = Half(x), d = Mathf.Abs(y - cy);
                    Color c = Color.clear;
                    if (x >= 8 && x <= 100 && d <= half)
                    {
                        bool bow = x >= 74, stern = x <= 24, side = d >= half - 5f;
                        bool ok = zone switch
                        {
                            PlacementZone.BowOnly => bow,
                            PlacementZone.SternOnly => stern,
                            PlacementZone.SideOnly => side && !bow,
                            PlacementZone.SideOrStern => (side && !bow) || stern,
                            _ => true,
                        };
                        c = d >= half - 1.2f || x <= 9 ? edge : ok ? allowed : hull;
                    }
                    px[y * w + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply();
            s_zones[zone] = tex;
            return tex;
        }

        // ------------------------------------------------------------ 링크

        /// <summary>본문의 &lt;link&gt;를 누르면 그 항목으로 이동.</summary>
        private sealed class LinkClick : MonoBehaviour, IPointerClickHandler
        {
            public CodexUI Owner;
            public TMP_Text Text;

            public void OnPointerClick(PointerEventData e)
            {
                if (Owner == null || Text == null) return;
                int link = TMP_TextUtilities.FindIntersectingLink(Text, e.position, e.pressEventCamera);
                if (link >= 0) Owner.JumpTo(Text.textInfo.linkInfo[link].GetLinkID());
            }
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
            button.onClick.AddListener(() => Game.Core.UiSound.Play(Game.Core.UiSound.Cue.Select));
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
