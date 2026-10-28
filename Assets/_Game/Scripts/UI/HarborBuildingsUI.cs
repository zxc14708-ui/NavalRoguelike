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
    /// ([돌아가기] · Esc로 개관). 기록실은 출격 기록·도전 과제 화면을 연다. 연구소는 연구 목록(2단계), 정비창은 함교 강화 · 시작 배치 개조(3단계).
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
        public bool PanelOpen => _panel != null && _panel.gameObject.activeSelf || ResearchOpen || WorkshopOpen;
        public string PanelTitle => ResearchOpen ? "해군 기술연구소" : WorkshopOpen ? "함정 정비창" : _panelTitle != null ? _panelTitle.text : null;

        // ---- 정비창(3단계): 함급 탭 · 함교 강화 · 시작 배치 개조
        private static readonly Game.Data.StartShipKind[] WorkshopKinds =
            { Game.Data.StartShipKind.Patrol, Game.Data.StartShipKind.Command, Game.Data.StartShipKind.Assault, Game.Data.StartShipKind.MissileDestroyer };
        private RectTransform _workshop;
        private TMP_Text _workshopHeader, _bridgeInfo, _bridgeLevel, _bridgeButtonText, _workshopResetText;
        private Button _bridgeButton;
        private readonly List<(Button button, Image image)> _tabs = new();
        private readonly List<(TMP_Text info, Button button, TMP_Text buttonText, Image row)> _layoutRows = new();
        private Game.Data.StartShipKind _workshopKind = Game.Data.StartShipKind.Patrol;
        public bool WorkshopOpen => _workshop != null && _workshop.gameObject.activeSelf;
        /// <summary>검증용: 정비창 단추 · 문구.</summary>
        public Game.Data.StartShipKind WorkshopKind => _workshopKind;
        public Button WorkshopTab(Game.Data.StartShipKind kind) => System.Array.IndexOf(WorkshopKinds, kind) is var i and >= 0 && i < _tabs.Count ? _tabs[i].button : null;
        public Button WorkshopBridgeButton => _bridgeButton;
        public string WorkshopBridgeText => _bridgeInfo != null ? $"{_bridgeInfo.text} | {_bridgeLevel.text} | {_bridgeButtonText.text}" : null;
        /// <summary>0 = 기본형, 1·2 = 개조안.</summary>
        public Button WorkshopLayoutButton(int index) => index >= 0 && index < _layoutRows.Count ? _layoutRows[index].button : null;
        public string WorkshopLayoutText(int index) => index >= 0 && index < _layoutRows.Count ? $"{_layoutRows[index].info.text} | {_layoutRows[index].buttonText.text}" : null;
        public Button WorkshopResetButton { get; private set; }

        // ---- 연구소(2단계): 연구 목록
        private RectTransform _research;
        private TMP_Text _researchHeader, _resetText;
        private readonly Dictionary<string, (TMP_Text info, TMP_Text level, Button button, TMP_Text buttonText)> _rows = new();
        private float _resetArmedUntil;
        public bool ResearchOpen => _research != null && _research.gameObject.activeSelf;
        /// <summary>검증용: 연구 단추 · 줄 문구.</summary>
        public Button ResearchButton(string id) => _rows.TryGetValue(id, out var r) ? r.button : null;
        public string ResearchRowText(string id) => _rows.TryGetValue(id, out var r) ? $"{r.info.text} | {r.level.text} | {r.buttonText.text}" : null;
        public Button ResearchResetButton { get; private set; }
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
            BuildResearch();
            BuildWorkshop();
        }

        /// <summary>
        /// 함정 정비창(2026-10-28 3단계): 위에 함급 탭 넷, 그 함급의 함교 강화 한 줄, 시작 배치 세 줄(기본형 · 개조안 2 — 앞 → 뒤 블록, 바뀐 칸 강조).
        /// 개조안은 한 번 열면 계속 쓰고, 고른 배치로 다음 출항부터 시작한다.
        /// </summary>
        private void BuildWorkshop()
        {
            const float w = 700f;
            float h = 640f;
            _workshop = Box(_rect, "Workshop panel", new Vector2(w, h), Panel);
            _workshop.anchorMin = _workshop.anchorMax = new Vector2(1f, 0.5f); _workshop.pivot = new Vector2(1f, 0.5f); _workshop.anchoredPosition = new Vector2(-40f, -10f);
            var top = Box(_workshop, "Top signal", new Vector2(w, 4f), Accent); top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f); top.pivot = new Vector2(0.5f, 1f);
            var role = Text(_workshop, "정비소 · 작전 포인트로 함급별 함교 강화와 시작 배치 개조", 16f, TextAlignmentOptions.TopLeft, Muted); Place(role.rectTransform, 24f, 18f, w - 48f, 24f);
            var title = Text(_workshop, "함정 정비창", 32f, TextAlignmentOptions.TopLeft, Color.white); Place(title.rectTransform, 24f, 42f, w - 48f, 44f);
            _workshopHeader = Text(_workshop, "", 19f, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.92f, 0.9f)); Place(_workshopHeader.rectTransform, 24f, 92f, w - 48f, 30f);

            // 함급 탭
            float tabW = (w - 40f - 3f * 8f) / 4f;
            for (int i = 0; i < WorkshopKinds.Length; i++)
            {
                var kind = WorkshopKinds[i];
                var concept = Game.Data.StartingShipCatalog.Get(kind);
                var tab = Button(_workshop, concept != null ? concept.Title : kind.ToString(), new Vector2(tabW, 40f), new Vector2(20f + i * (tabW + 8f), -128f), new Color(0.06f, 0.12f, 0.15f), out var tt);
                tt.fontSize = 18f;
                tab.name = "Workshop tab " + kind;
                tab.onClick.AddListener(() => { _workshopKind = kind; RefreshWorkshop(); });
                _tabs.Add((tab, tab.GetComponent<Image>()));
            }

            // 함교 강화
            var bridgeLabel = Text(_workshop, "함교 강화", 17f, TextAlignmentOptions.TopLeft, Gold); Place(bridgeLabel.rectTransform, 24f, 182f, w - 48f, 24f);
            var bridgeRow = Box(_workshop, "Workshop bridge row", new Vector2(w - 40f, 62f), new Color(0.03f, 0.09f, 0.11f, 0.95f));
            Place(bridgeRow, 20f, 208f, w - 40f, 62f);
            bridgeRow.GetComponent<Image>().raycastTarget = false;
            _bridgeInfo = Text(bridgeRow, "", 18f, TextAlignmentOptions.MidlineLeft); Place(_bridgeInfo.rectTransform, 14f, 2f, 250f, 58f);
            _bridgeLevel = Text(bridgeRow, "", 16f, TextAlignmentOptions.MidlineLeft, new Color(0.82f, 0.9f, 0.88f)); Place(_bridgeLevel.rectTransform, 270f, 2f, 250f, 58f);
            _bridgeLevel.textWrappingMode = TextWrappingModes.Normal;
            _bridgeButton = Button(bridgeRow, "", new Vector2(130f, 46f), new Vector2(w - 40f - 140f, -8f), Accent, out _bridgeButtonText);
            _bridgeButtonText.fontSize = 18f;
            Grey(_bridgeButton);
            _bridgeButton.name = "Workshop bridge";
            _bridgeButton.onClick.AddListener(() => { if (Game.Modules.MetaWorkshop.TryUpgradeBridge(_workshopKind)) RefreshWorkshop(); });

            // 시작 배치 개조
            var layoutLabel = Text(_workshop, "시작 배치 개조  <size=85%><color=#9fc7bd>앞 → 뒤 · 바뀌는 칸은 금색</color></size>", 17f, TextAlignmentOptions.TopLeft, Gold); Place(layoutLabel.rectTransform, 24f, 286f, w - 48f, 24f);
            float y = 312f;
            for (int i = 0; i < 3; i++)
            {
                var row = Box(_workshop, "Workshop layout row " + i, new Vector2(w - 40f, 64f), new Color(0.03f, 0.09f, 0.11f, 0.95f));
                Place(row, 20f, y, w - 40f, 64f);
                var rowImage = row.GetComponent<Image>(); rowImage.raycastTarget = false;
                var info = Text(row, "", 17f, TextAlignmentOptions.MidlineLeft); Place(info.rectTransform, 14f, 2f, w - 40f - 170f, 60f);
                info.textWrappingMode = TextWrappingModes.Normal;
                var button = Button(row, "", new Vector2(130f, 46f), new Vector2(w - 40f - 140f, -9f), Accent, out var bt);
                bt.fontSize = 18f;
                Grey(button);
                button.name = "Workshop layout " + i;
                int index = i;
                button.onClick.AddListener(() => PressLayout(index));
                _layoutRows.Add((info, button, bt, rowImage));
                y += 70f;
            }
            var note = Text(_workshop, "고른 배치는 다음 출항부터 씁니다. 정박지 모형은 기본형으로 보입니다. 훈련은 늘 기본형.", 15f, TextAlignmentOptions.TopLeft, Muted);
            Place(note.rectTransform, 24f, y + 4f, w - 48f, 24f);
            y += 40f;

            WorkshopResetButton = Button(_workshop, "", new Vector2(320f, 46f), new Vector2(20f, -(y + 14f)), new Color(0.32f, 0.16f, 0.1f), out _workshopResetText);
            WorkshopResetButton.name = "Workshop reset";
            WorkshopResetButton.onClick.AddListener(() =>
            {
                // 두 번 눌러야 되돌린다 — 함교 강화 · 개조안 모두, 쓴 포인트는 모두 돌려받고 배치는 기본형으로
                if (Time.unscaledTime > _resetArmedUntil) { _resetArmedUntil = Time.unscaledTime + 3f; RefreshWorkshop(); return; }
                _resetArmedUntil = 0f;
                Game.Modules.MetaWorkshop.ResetAll();
                RefreshWorkshop();
            });
            var back = Button(_workshop, "돌아가기  [Esc]", new Vector2(w - 380f, 46f), new Vector2(360f, -(y + 14f)), new Color(0.1f, 0.2f, 0.24f), out _);
            back.name = "Workshop back";
            back.onClick.AddListener(Back);
            _workshop.sizeDelta = new Vector2(w, y + 74f);
            _workshop.gameObject.SetActive(false);
        }

        private static void Grey(Button b)
        {
            var colors = b.colors; colors.disabledColor = new Color(0.3f, 0.32f, 0.33f, 1f); b.colors = colors;
        }

        /// <summary>시작 배치 줄 단추: 열린 배치면 고르고, 아니면 연다(열면 바로 고름).</summary>
        private void PressLayout(int index)
        {
            var variants = new List<Game.Modules.MetaWorkshop.Variant>(Game.Modules.MetaWorkshop.VariantsFor(_workshopKind));
            if (index == 0) Game.Modules.MetaWorkshop.SelectLayout(_workshopKind, null);
            else if (index - 1 < variants.Count)
            {
                var v = variants[index - 1];
                if (Game.Modules.MetaWorkshop.Unlocked(v)) Game.Modules.MetaWorkshop.SelectLayout(_workshopKind, v.Id);
                else Game.Modules.MetaWorkshop.TryUnlock(v.Id);
            }
            RefreshWorkshop();
        }

        private void RefreshWorkshop()
        {
            if (_workshop == null) return;
            int points = PlayerProfile.OpPoints, spent = Game.Modules.MetaWorkshop.Spent();
            _workshopHeader.text = $"보유 작전 포인트 <color=#e0c05a><b>{points}</b></color>   <color=#9fc7bd>정비에 쓴 포인트 {spent}</color>";
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].image.color = WorkshopKinds[i] == _workshopKind ? new Color(0.1f, 0.42f, 0.34f) : new Color(0.06f, 0.12f, 0.15f);

            var kind = _workshopKind;
            var track = Game.Modules.MetaWorkshop.Bridge(kind);
            int l = Game.Modules.MetaWorkshop.BridgeLevel(kind);
            int cost = Game.Modules.MetaWorkshop.NextBridgeCost(kind);
            _bridgeInfo.text = $"<b>{track.Name}</b>\n<size=78%><color=#9fc7bd>{track.Passive}</color></size>";
            string pips = new string('■', l) + new string('□', track.MaxLevel - l);
            _bridgeLevel.text = cost < 0 ? $"{pips}  <color=#e0c05a>{track.Effect(l)}</color>"
                                         : $"{pips}  {(l > 0 ? track.Effect(l) : "기본")}\n→ <color=#e0c05a>{track.Effect(l + 1)}</color>";
            _bridgeButton.interactable = cost >= 0 && points >= cost;
            _bridgeButtonText.text = cost < 0 ? "완료" : $"강화 · {cost}";

            var concept = Game.Data.StartingShipCatalog.Get(kind);
            var selected = Game.Modules.MetaWorkshop.SelectedVariant(kind);
            var variants = new List<Game.Modules.MetaWorkshop.Variant>(Game.Modules.MetaWorkshop.VariantsFor(kind));
            for (int i = 0; i < _layoutRows.Count; i++)
            {
                var (info, button, bt, row) = _layoutRows[i];
                var v = i == 0 ? null : i - 1 < variants.Count ? variants[i - 1] : null;
                bool exists = i == 0 || v != null;
                row.gameObject.SetActive(exists);
                if (!exists) continue;
                bool active = v == selected;
                bool open = v == null || Game.Modules.MetaWorkshop.Unlocked(v);
                string layout = Game.Modules.MetaWorkshop.Describe(Game.Modules.MetaWorkshop.LoadoutWith(concept, v), v);
                info.text = (v == null ? "<b>기본형</b>" : $"<b>{v.Name}</b>  <size=80%><color=#9fc7bd>{v.Change}</color></size>") + $"\n<size=86%>{layout}</size>";
                row.color = active ? new Color(0.05f, 0.2f, 0.17f, 0.97f) : new Color(0.03f, 0.09f, 0.11f, 0.95f);
                if (active) { bt.text = "사용 중"; button.interactable = false; }
                else if (open) { bt.text = "선택"; button.interactable = true; }
                else { bt.text = $"개조 · {v.Cost}"; button.interactable = points >= v.Cost; }
            }
            bool armed = Time.unscaledTime <= _resetArmedUntil;
            _workshopResetText.text = armed ? $"한 번 더 누르면 되돌림(+{spent})" : "정비 되돌리기(포인트 반환)";
            WorkshopResetButton.interactable = spent > 0;
        }

        /// <summary>해군 기술연구소 연구 목록(2026-10-28 2단계): 줄마다 이름·대상 · 단계(■□) · 지금 → 다음 효과 · [연구 · 비용].</summary>
        private void BuildResearch()
        {
            const float w = 700f, rowH = 54f;
            int n = Game.Modules.MetaResearch.Nodes.Length;
            float h = 132f + n * rowH + 74f;
            _research = Box(_rect, "Research panel", new Vector2(w, h), Panel);
            _research.anchorMin = _research.anchorMax = new Vector2(1f, 0.5f); _research.pivot = new Vector2(1f, 0.5f); _research.anchoredPosition = new Vector2(-40f, -10f);
            var top = Box(_research, "Top signal", new Vector2(w, 4f), Accent); top.anchorMin = top.anchorMax = new Vector2(0.5f, 1f); top.pivot = new Vector2(0.5f, 1f);
            var role = Text(_research, "연구소 · 작전 포인트로 기본 능력 연구(판 안 성장 카드에 더해진다)", 16f, TextAlignmentOptions.TopLeft, Muted); Place(role.rectTransform, 24f, 18f, w - 48f, 24f);
            var title = Text(_research, "해군 기술연구소", 32f, TextAlignmentOptions.TopLeft, Color.white); Place(title.rectTransform, 24f, 42f, w - 48f, 44f);
            _researchHeader = Text(_research, "", 19f, TextAlignmentOptions.TopLeft, new Color(0.86f, 0.92f, 0.9f)); Place(_researchHeader.rectTransform, 24f, 92f, w - 48f, 30f);
            float y = 132f;
            foreach (var node in Game.Modules.MetaResearch.Nodes)
            {
                var row = Box(_research, "Research " + node.Id, new Vector2(w - 40f, rowH - 6f), new Color(0.03f, 0.09f, 0.11f, 0.95f));
                Place(row, 20f, y, w - 40f, rowH - 6f);
                row.GetComponent<Image>().raycastTarget = false;
                var info = Text(row, "", 18f, TextAlignmentOptions.MidlineLeft); Place(info.rectTransform, 14f, 2f, 290f, rowH - 10f);
                var level = Text(row, "", 16f, TextAlignmentOptions.MidlineLeft, new Color(0.82f, 0.9f, 0.88f)); Place(level.rectTransform, 306f, 2f, 210f, rowH - 10f);
                level.textWrappingMode = TextWrappingModes.NoWrap;
                var button = Button(row, "", new Vector2(130f, rowH - 16f), new Vector2(w - 40f - 140f, -5f), Accent, out var bt);
                bt.fontSize = 18f;
                // 못 하는 연구(포인트 모자람·완료)는 확실히 회색으로
                var colors = button.colors; colors.disabledColor = new Color(0.3f, 0.32f, 0.33f, 1f); button.colors = colors;
                button.name = "Research do " + node.Id;
                string id = node.Id;
                button.onClick.AddListener(() => { if (Game.Modules.MetaResearch.TryResearch(id)) RefreshResearch(); });
                _rows[node.Id] = (info, level, button, bt);
                y += rowH;
            }
            ResearchResetButton = Button(_research, "", new Vector2(320f, 46f), new Vector2(20f, -(y + 14f)), new Color(0.32f, 0.16f, 0.1f), out _resetText);
            ResearchResetButton.name = "Research reset";
            ResearchResetButton.onClick.AddListener(() =>
            {
                // 두 번 눌러야 되돌린다(실수 방지) — 쓴 포인트는 모두 돌려받는다
                if (Time.unscaledTime > _resetArmedUntil) { _resetArmedUntil = Time.unscaledTime + 3f; RefreshResearch(); return; }
                _resetArmedUntil = 0f;
                Game.Modules.MetaResearch.ResetAll();
                RefreshResearch();
            });
            var back = Button(_research, "돌아가기  [Esc]", new Vector2(w - 380f, 46f), new Vector2(360f, -(y + 14f)), new Color(0.1f, 0.2f, 0.24f), out _);
            back.name = "Research back";
            back.onClick.AddListener(Back);
            _research.gameObject.SetActive(false);
        }

        private void RefreshResearch()
        {
            if (_research == null) return;
            int points = PlayerProfile.OpPoints, spent = Game.Modules.MetaResearch.Spent();
            _researchHeader.text = $"보유 작전 포인트 <color=#e0c05a><b>{points}</b></color>   <color=#9fc7bd>연구에 쓴 포인트 {spent}</color>";
            foreach (var node in Game.Modules.MetaResearch.Nodes)
            {
                var (info, level, button, bt) = _rows[node.Id];
                int l = Game.Modules.MetaResearch.Level(node.Id);
                int cost = Game.Modules.MetaResearch.NextCost(node);
                info.text = $"<b>{node.Name}</b>\n<size=78%><color=#9fc7bd>{node.Scope}</color></size>";
                string pips = new string('■', l) + new string('□', node.MaxLevel - l);
                level.text = cost < 0 ? $"{pips}  <color=#e0c05a>{node.Effect(l)}</color>"
                                      : $"{pips}  {(l > 0 ? node.Effect(l) : "—")} → <color=#e0c05a>{node.Effect(l + 1)}</color>";
                bool can = cost >= 0 && points >= cost;
                button.interactable = can;
                bt.text = cost < 0 ? "완료" : $"연구 · {cost}";
            }
            bool armed = Time.unscaledTime <= _resetArmedUntil;
            _resetText.text = armed ? $"한 번 더 누르면 되돌림(+{spent})" : "연구 되돌리기(포인트 반환)";
            ResearchResetButton.interactable = spent > 0;
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
            if (b.Role == HarborRole.Research)
            {
                _panel.gameObject.SetActive(false);
                _resetArmedUntil = 0f;
                RefreshResearch();
                _research.gameObject.SetActive(true);
                _research.SetAsLastSibling();
                return;
            }
            if (b.Role == HarborRole.Workshop)
            {
                _panel.gameObject.SetActive(false);
                _resetArmedUntil = 0f;
                var selected = FindFirstObjectByType<Game.Ship.ShipInitializer>()?.SelectedConcept;
                if (selected != null && System.Array.IndexOf(WorkshopKinds, selected.Kind) >= 0) _workshopKind = selected.Kind;
                RefreshWorkshop();
                _workshop.gameObject.SetActive(true);
                _workshop.SetAsLastSibling();
                return;
            }
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
            if (_research != null) _research.gameObject.SetActive(false);
            if (_workshop != null) _workshop.gameObject.SetActive(false);
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
            if (ResearchOpen && _resetArmedUntil > 0f && Time.unscaledTime > _resetArmedUntil) { _resetArmedUntil = 0f; RefreshResearch(); }
            if (WorkshopOpen && _resetArmedUntil > 0f && Time.unscaledTime > _resetArmedUntil) { _resetArmedUntil = 0f; RefreshWorkshop(); }

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
