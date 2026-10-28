using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>군항 건물이 맡는 기능(2026-10-28). 없음 = 이름만.</summary>
    public enum HarborRole { None, Research, Records, Workshop }

    /// <summary>군항 섬의 건물 하나: 이름과 섬 좌표(빌더 NavalBaseBuilder.Layout과 같은 좌표, m — 지붕 위 표식 자리).</summary>
    public readonly struct HarborBuilding
    {
        public readonly string Id, Name, RoleTitle;
        public readonly HarborRole Role;
        public readonly Vector3 Anchor;
        /// <summary>확대할 때 보는 거리(섬 좌표).</summary>
        public readonly float ViewSize;

        public HarborBuilding(string id, string name, float x, float height, float z, HarborRole role = HarborRole.None, string roleTitle = null, float viewSize = 140f)
        {
            Id = id; Name = name; Anchor = new Vector3(x, height, z); Role = role; RoleTitle = roleTitle; ViewSize = viewSize;
        }
    }

    /// <summary>
    /// 군항 건물(2026-10-28, 1단계). 시작 화면의 섬 위에 기능 건물 이름표(해군 기술연구소 · 사령부 기록실 · 함정 정비창)를 띄우고,
    /// 다른 건물은 마우스를 올리면 이름만 보인다. 이름표를 누르면 카메라가 그 건물로 다가가고 오른쪽에 건물 패널이 열린다
    /// ([돌아가기] · Esc로 개관). 기록실은 출격 기록·도전 과제 화면을 연다. 연구소·정비창은 2·3단계에서 열린다(지금은 작전 포인트와 안내).
    /// 화면 위치는 군항 카메라 투영(NavalBaseMenu.TryProject — 렌더 텍스처가 화면 전체)으로 매 프레임 맞춘다.
    /// 작전 포인트는 규칙 집합별 프로필에 쌓인다(현대 / WWII 따로).
    /// </summary>
    public sealed class HarborBuildingsUI : MonoBehaviour
    {
        /// <summary>섬의 건물(번호는 건물 지도 순서). 높이는 지붕 위 이름표 자리.</summary>
        public static readonly HarborBuilding[] All =
        {
            new("port_ops", "항무청", 420, 40, 505),
            new("boat_shop", "지원정 정비고", 240, 22, 500),
            new("fire_harbor", "항만 소방대", 330, 18, 498),
            new("tank_harbor", "연료 탱크", 560, 22, 420),
            new("floating_dock", "함정 정비창", 640, 30, 650, HarborRole.Workshop, "정비소 · 함교 강화 · 시작 배치 개조", 170f),
            new("supply_wh", "보급 창고", -250, 22, 400),
            new("container_yard", "컨테이너 야적장", -245, 16, 290),
            new("vehicle_shop", "차량 정비고", -640, 18, 250),
            new("engineering", "해군 기술연구소", -445, 30, 330, HarborRole.Research, "연구소 · 작전 포인트로 기본 능력 연구", 150f),
            new("headquarters", "사령부 기록실", 110, 40, 330, HarborRole.Records, "기록소 · 출격 기록 · 도전 과제 · 칭호", 160f),
            new("admin_a", "행정동 A", -10, 22, 330),
            new("admin_b", "행정동 B", -10, 16, 225),
            new("comms", "통신소", 240, 30, 400),
            new("mess", "식당", 570, 14, 365),
            new("gym", "체육관", 570, 20, 290),
            new("clinic", "의무대", 680, 16, 385),
            new("exchange", "복지회관", 560, 12, 205),
            new("barracks", "장병 숙소", 420, 22, 300),
            new("training_hall", "교육대 강당", -560, 18, 70),
            new("logistics", "물류 창고", -265, 20, 60),
            new("school", "학교", 125, 16, -10),
            new("family", "관사 단지", 250, 12, 40),
            new("power", "발전소", 540, 24, 70),
            new("aircraft_mx", "항공기 정비고", -655, 20, -95),
            new("engine_test", "엔진 시험동", -520, 16, -95),
            new("sof_hq", "특수전 본부", -810, 16, 250),
            new("boathouse", "고속단정 격납고", -805, 14, 372),
            new("mx_hangar", "정비 격납고", -153, 26, -102),
            new("arch_hangar", "아치 격납고", 91, 26, -102),
            new("heli_hangar", "헬기 격납고", 220, 20, -102),
            new("squadron_hq", "비행대대 본부", 320, 20, -96),
            new("tower", "관제탑", 470, 45, -150),
            new("search_radar", "탐색 레이더", 120, 35, -440),
            new("radome", "레이돔 기지", 830, 40, 20),
            new("igloos", "탄약고", 832, 12, -195),
            new("fuel_air", "항공유 저장소", 650, 22, -140),
            new("piers", "돌제 부두", -290, 10, 640),
        };

        private static readonly Color Panel = new(0.015f, 0.045f, 0.06f, 0.94f);
        private static readonly Color Accent = new(0.12f, 0.62f, 0.45f, 1f);
        private static readonly Color Muted = new(0.62f, 0.78f, 0.74f);
        private static readonly Color Gold = new(0.88f, 0.75f, 0.35f);

        private NavalBaseMenu _harbor;
        private TMP_FontAsset _font;
        private Func<bool> _menuOpen;
        private Func<IEnumerable<RectTransform>> _blockers;
        private RectTransform _quick;
        private Action<bool> _showConsoles;
        private Action _openRecords;
        private RectTransform _rect;
        private readonly List<(HarborBuilding b, RectTransform tag)> _tags = new();
        private RectTransform _hover;
        private TMP_Text _hoverText, _badge, _panelTitle, _panelRole, _panelBody;
        private RectTransform _panel;
        private Button _panelAction;
        private TMP_Text _panelActionText;

        /// <summary>지금 확대해 보는 건물(없으면 null) — 검증용.</summary>
        public HarborBuilding? Focused { get; private set; }
        public bool PanelOpen => _panel != null && _panel.gameObject.activeSelf;
        public string PanelTitle => _panelTitle != null ? _panelTitle.text : null;
        public string HoverName => _hover != null && _hover.gameObject.activeSelf ? _hoverText.text : null;
        public Button PanelAction => _panelAction;

        public static HarborBuildingsUI Create(Transform parent, NavalBaseMenu harbor, TMP_FontAsset font,
                                               Func<bool> menuOpen, Action<bool> showConsoles, Action openRecords,
                                               Func<IEnumerable<RectTransform>> blockers = null)
        {
            var go = new GameObject("Harbor buildings", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.transform.SetSiblingIndex(1);   // 군항 배경 바로 위, 콘솔 아래
            var ui = go.AddComponent<HarborBuildingsUI>();
            ui._harbor = harbor; ui._font = font; ui._menuOpen = menuOpen; ui._showConsoles = showConsoles; ui._openRecords = openRecords; ui._blockers = blockers;
            ui._rect = rect;
            ui.Build();
            return ui;
        }

        private void Build()
        {
            foreach (var b in All)
            {
                if (b.Role == HarborRole.None) continue;
                var tag = Box(_rect, "Tag " + b.Id, new Vector2(250f, 54f), new Color(0.02f, 0.07f, 0.09f, 0.88f));
                tag.pivot = new Vector2(0.5f, 0f);
                var edge = Box(tag, "Edge", new Vector2(250f, 3f), Accent); edge.anchorMin = edge.anchorMax = new Vector2(0.5f, 0f); edge.pivot = new Vector2(0.5f, 0f);
                var t = Text(tag, $"<b>{b.Name}</b>\n<size=70%><color=#9fc7bd>{RoleShort(b.Role)}</color></size>", 20f, TextAlignmentOptions.Center);
                t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
                var stem = Box(tag, "Stem", new Vector2(3f, 26f), Accent); stem.anchorMin = stem.anchorMax = new Vector2(0.5f, 0f); stem.pivot = new Vector2(0.5f, 1f);
                var button = tag.gameObject.AddComponent<Button>();
                button.targetGraphic = tag.GetComponent<Image>();
                var c = button.colors; c.highlightedColor = new Color(0.55f, 1f, 0.8f); button.colors = c;
                var building = b;
                button.onClick.AddListener(() => Focus(building));
                tag.name = "Harbor tag " + b.Id;
                _tags.Add((b, tag));
            }

            _hover = Box(_rect, "Hover name", new Vector2(190f, 34f), new Color(0.01f, 0.03f, 0.04f, 0.82f));
            _hover.pivot = new Vector2(0.5f, 0f);
            _hover.GetComponent<Image>().raycastTarget = false;
            _hoverText = Text(_hover, "", 18f, TextAlignmentOptions.Center);
            _hoverText.rectTransform.anchorMin = Vector2.zero; _hoverText.rectTransform.anchorMax = Vector2.one; _hoverText.rectTransform.offsetMin = _hoverText.rectTransform.offsetMax = Vector2.zero;
            _hover.gameObject.SetActive(false);

            // 작전 포인트(오른쪽 위)
            var badge = Box(_rect, "Op points", new Vector2(280f, 46f), Panel);
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f); badge.pivot = new Vector2(1f, 1f); badge.anchoredPosition = new Vector2(-28f, -24f);
            badge.GetComponent<Image>().raycastTarget = false;
            _badge = Text(badge, "", 20f, TextAlignmentOptions.Center);
            _badge.rectTransform.anchorMin = Vector2.zero; _badge.rectTransform.anchorMax = Vector2.one; _badge.rectTransform.offsetMin = _badge.rectTransform.offsetMax = Vector2.zero;

            // 시설 바로 가기(작전 포인트 아래): 섬이 돌다 건물이 콘솔 뒤로 가려져도 늘 누를 수 있게
            _quick = Box(_rect, "Harbor facilities", new Vector2(280f, 10f), new Color(0f, 0f, 0f, 0f));
            _quick.anchorMin = _quick.anchorMax = new Vector2(1f, 1f); _quick.pivot = new Vector2(1f, 1f); _quick.anchoredPosition = new Vector2(-28f, -78f);
            _quick.GetComponent<Image>().raycastTarget = false;
            int row = 0;
            foreach (var b in All)
            {
                if (b.Role == HarborRole.None) continue;
                var button = Button(_quick, $"{b.Name}  <size=78%><color=#9fc7bd>{RoleShort(b.Role)}</color></size>", new Vector2(280f, 42f), new Vector2(0f, -row * 48f), new Color(0.02f, 0.07f, 0.09f, 0.9f), out var label);
                label.fontSize = 19f;
                button.name = "Harbor facility " + b.Id;
                var building = b;
                button.onClick.AddListener(() => Focus(building));
                row++;
            }
            _quick.sizeDelta = new Vector2(280f, row * 48f);

            // 건물 패널(오른쪽)
            _panel = Box(_rect, "Building panel", new Vector2(500f, 470f), Panel);
            _panel.anchorMin = _panel.anchorMax = new Vector2(1f, 0.5f); _panel.pivot = new Vector2(1f, 0.5f); _panel.anchoredPosition = new Vector2(-40f, -20f);
            var top = Box(_panel, "Top signal", new Vector2(500f, 4f), Accent); top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f); top.pivot = new Vector2(0.5f, 1f);
            _panelRole = Text(_panel, "", 16f, TextAlignmentOptions.TopLeft, Muted); Place(_panelRole.rectTransform, 26f, 22f, 450f, 26f);
            _panelTitle = Text(_panel, "", 34f, TextAlignmentOptions.TopLeft, Color.white); Place(_panelTitle.rectTransform, 26f, 50f, 450f, 48f);
            _panelBody = Text(_panel, "", 19f, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.92f, 0.9f)); Place(_panelBody.rectTransform, 26f, 110f, 450f, 230f);
            _panelBody.textWrappingMode = TextWrappingModes.Normal;
            _panelAction = Button(_panel, "", new Vector2(448f, 50f), new Vector2(26f, -352f), Accent, out _panelActionText);
            var back = Button(_panel, "돌아가기  [Esc]", new Vector2(448f, 44f), new Vector2(26f, -412f), new Color(0.1f, 0.2f, 0.24f), out _);
            back.name = "Harbor building back";
            back.onClick.AddListener(Back);
            _panel.gameObject.SetActive(false);
        }

        private static string RoleShort(HarborRole r) => r switch
        {
            HarborRole.Research => "연구소",
            HarborRole.Records => "기록소",
            HarborRole.Workshop => "정비소",
            _ => "",
        };

        /// <summary>이름표를 눌렀다: 그 건물로 다가가고 패널을 연다(콘솔은 숨긴다).</summary>
        public void Focus(HarborBuilding b)
        {
            if (_harbor == null) return;
            Focused = b;
            _harbor.FocusBuilding(b.Anchor - Vector3.up * b.Anchor.y * 0.5f, b.ViewSize);
            _showConsoles?.Invoke(false);
            FillPanel(b);
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
        }

        /// <summary>패널을 닫고 개관으로.</summary>
        public void Back()
        {
            if (Focused == null) return;
            Focused = null;
            _panel.gameObject.SetActive(false);
            _harbor?.ShowOverview();
            _showConsoles?.Invoke(true);
        }

        private void FillPanel(HarborBuilding b)
        {
            _panelTitle.text = b.Name;
            _panelRole.text = b.RoleTitle ?? "";
            var d = PlayerProfile.Current;
            _panelAction.onClick.RemoveAllListeners();
            _panelAction.gameObject.SetActive(true);
            switch (b.Role)
            {
                case HarborRole.Records:
                    _panelBody.text = $"출격 {d.runs}회 · 승리 {d.wins}회 · 격침 {d.totalKills}척\n" +
                                      $"도전 과제 {d.achievements.Count} / {PlayerProfile.Catalog.Length}" +
                                      (PlayerProfile.Title is { } title ? $"\n칭호 「{title}」" : "") +
                                      $"\n\n<color=#9fc7bd>작전 포인트 누적 {PlayerProfile.OpPointsEarned}</color>";
                    _panelActionText.text = "출격 기록 · 도전 과제 열기";
                    _panelAction.name = "Harbor records open";
                    _panelAction.onClick.AddListener(() => _openRecords?.Invoke());
                    break;
                case HarborRole.Research:
                    _panelBody.text = $"보유 작전 포인트 <color=#e0c05a><b>{PlayerProfile.OpPoints}</b></color>\n\n" +
                                      "함포·미사일·생존 계열의 기본 능력을 작전 포인트로 연구한다.\n<color=#9fc7bd>연구 항목은 다음 단계에서 열린다.</color>\n\n" + EarnRules();
                    _panelAction.gameObject.SetActive(false);
                    break;
                case HarborRole.Workshop:
                    _panelBody.text = $"보유 작전 포인트 <color=#e0c05a><b>{PlayerProfile.OpPoints}</b></color>\n\n" +
                                      "함교 강화와 시작 배치 개조를 맡는다.\n<color=#9fc7bd>정비 항목은 다음 단계에서 열린다.</color>\n\n" + EarnRules();
                    _panelAction.gameObject.SetActive(false);
                    break;
                default:
                    _panelBody.text = "";
                    _panelAction.gameObject.SetActive(false);
                    break;
            }
        }

        private static string EarnRules() =>
            $"<size=88%><color=#9fc7bd>작전 포인트 — 도달 스테이지마다 {PlayerProfile.StagePoints} · 보스 격침 {PlayerProfile.BossPoints} · " +
            $"도전 과제 {PlayerProfile.AchievementPoints} · 고난도 출항 ×{PlayerProfile.DangerPointScale:0.#}</color></size>";

        private void Update()
        {
            if (_harbor == null) return;
            bool focused = Focused != null;
            bool overview = !focused && (_menuOpen == null || _menuOpen());
            _badge.transform.parent.gameObject.SetActive(overview || focused);
            if (_quick.gameObject.activeSelf != overview) _quick.gameObject.SetActive(overview);
            _badge.text = $"작전 포인트 <color=#e0c05a><b>{PlayerProfile.OpPoints}</b></color>";

            if (focused && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Back(); return; }

            Vector2 size = _rect.rect.size;
            foreach (var (b, tag) in _tags)
            {
                bool show = overview && _harbor.TryProject(b.Anchor, out var vp) && vp.x > 0.02f && vp.x < 0.98f && vp.y > 0.05f && vp.y < 0.95f && !Blocked(vp);
                if (tag.gameObject.activeSelf != show) tag.gameObject.SetActive(show);
                if (show && _harbor.TryProject(b.Anchor, out vp)) tag.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y + 26f);
            }

            // 다른 건물: 마우스 가까이(화면 50 px 안)면 이름만
            string hover = null; Vector2 at = default;
            if (overview && Mouse.current != null)
            {
                Vector2 mouse = Mouse.current.position.ReadValue();
                var (name, pos) = NearestName(new Vector2(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height)), 50f / Mathf.Max(1, Screen.height));
                hover = name; at = pos;
            }
            SetHover(hover, at, size);
        }

        /// <summary>이 화면 자리를 콘솔(출항·WWII·이어하기)이나 시설 바로 가기가 가리는가 — 가려진 이름표는 숨긴다(누를 수 없으므로).</summary>
        private bool Blocked(Vector2 viewport)
        {
            var screen = new Vector2(viewport.x * Screen.width, viewport.y * Screen.height);
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (_quick != null && _quick.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(_quick, screen, cam)) return true;
            if (_blockers == null) return false;
            foreach (var r in _blockers())
                if (r != null && r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, cam)) return true;
            return false;
        }

        /// <summary>화면 비율 좌표에서 가장 가까운 이름만 있는 건물(반경 안). 검증도 쓴다.</summary>
        public (string name, Vector2 viewport) NearestName(Vector2 viewport, float radius)
        {
            string best = null; Vector2 bestAt = default; float bestD = radius * radius;
            foreach (var b in All)
            {
                if (b.Role != HarborRole.None || !_harbor.TryProject(b.Anchor, out var vp)) continue;
                var dv = vp - viewport; dv.x *= (float)Screen.width / Mathf.Max(1, Screen.height);
                float d = dv.sqrMagnitude;
                if (d < bestD) { bestD = d; best = b.Name; bestAt = vp; }
            }
            return (best, bestAt);
        }

        private void SetHover(string name, Vector2 vp, Vector2 size)
        {
            bool show = name != null;
            if (_hover.gameObject.activeSelf != show) _hover.gameObject.SetActive(show);
            if (!show) return;
            _hoverText.text = name;
            _hover.sizeDelta = new Vector2(Mathf.Max(120f, _hoverText.preferredWidth + 28f), 34f);
            _hover.anchoredPosition = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y + 14f);
        }

        // ------------------------------------------------------------ UI 조각

        private RectTransform Box(RectTransform parent, string name, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return r;
        }

        private TMP_Text Text(RectTransform parent, string value, float fontSize, TextAlignmentOptions align, Color? color = null)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = _font; t.text = value; t.fontSize = fontSize; t.alignment = align; t.color = color ?? Color.white; t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        /// <summary>패널 안 위에서부터 놓기(왼쪽 위 기준).</summary>
        private static void Place(RectTransform r, float left, float top, float width, float height)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(left, -top); r.sizeDelta = new Vector2(width, height);
        }

        private Button Button(RectTransform parent, string label, Vector2 size, Vector2 topLeft, Color color, out TMP_Text text)
        {
            var r = Box(parent, label, size, color);
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f); r.anchoredPosition = topLeft;
            text = Text(r, label, 20f, TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one; text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = r.GetComponent<Image>();
            return b;
        }
    }
}
