using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Refit;
using Game.Ship;
using Game.TaskForce;

namespace Game.UI
{
    /// <summary>
    /// 정비 화면. 함선 전체를 위에서 내려다보며 3D 모듈을 직접 올려놓는다.
    ///
    /// 함체가 고정 크기가 아니므로, 배가 자랄 때마다 카메라가 알아서 물러난다.
    /// 조작은 마우스와 키보드 양쪽으로 모두 가능하다.
    /// </summary>
    public class RefitUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private GameObject root;
        [SerializeField] private ShipGrid grid;
        [SerializeField] private ShipSystems systems;
        [SerializeField] private ModuleFactory factory;

        [Header("View")]
        [SerializeField] private Camera shipCamera;
        [SerializeField] private RawImage shipView;
        [SerializeField] private Transform moduleRoot;
        [SerializeField] private GameObject cellHighlightPrefab;

        [Header("Fire Arc Preview")]
        [Tooltip("반투명·양면 머티리얼. 사격각 부채꼴에 쓴다.")]
        [SerializeField] private Material arcMaterial;
        [SerializeField] private float arcInnerRadiusCells = 0.55f;
        [SerializeField] private float arcOuterRadiusCells = 1.5f;

        [Header("Camera Framing")]
        [SerializeField] private float viewPitch = 55f;
        [SerializeField] private float viewDistance = 40f;
        [SerializeField] private float minOrthoSize = 6f;
        [SerializeField] private float orthoPadding = 3f;

        [Header("Cards")]
        [SerializeField] private GameObject cardPanel;
        [SerializeField] private RectTransform[] cardRects;
        [SerializeField] private TMP_Text[] cardNames;
        [SerializeField] private Image[] cardIcons;
        [SerializeField] private TMP_Text[] cardStats;

        [Header("Texts")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private TMP_Text statusText;

        private ModuleDefinition _reward;
        private bool _rewardUsed;
        // --- 편대 카드(배치·강화). 역할 없는 고속정의 강화 카드는 역할 넷 중 하나를 고르게 한다.
        private TaskForceEscortFormation _fleet;
        private string _fleetResult;
        private bool _rolePicking;
        private int _rolePickIndex = -1;
        private RectTransform _rolePanel;
        private readonly List<RectTransform> _roleOptions = new();

        private bool _choosing;
        private readonly List<RefitCard> _cards = new();
        private bool _bossReward;
        private string _growthResult;

        private ModuleDefinition _held;
        private int _heldRotation;
        private bool _heldIsReward;

        private bool _heldWasInstalled;
        private bool _heldWasRaised;
        private GridCoord _heldOrigin;
        private int _heldOriginRotation;
        private string _originSynergy;
        /// <summary>집어 든 기존 블록의 강화 상태. 다시 놓을 때 새 인스턴스가 이어받는다(재배치해도 강화 유지).</summary>
        private ModuleUpgradeState _heldUpgrade;

        // --- 무장 강화: 강화 카드를 고르면 설치된 같은 블록 중 하나를 고른다(하나뿐이면 바로 적용)
        private bool _upgradePicking;
        private string _upgradeResult;
        private readonly List<Transform> _upgradeMarkers = new();
        private readonly List<TextMeshPro> _badges = new();
        private static readonly Color UpgradeableColor = new(0.3f, 1f, 0.4f, 0.55f);
        private static readonly Color MaxedColor = new(1f, 0.78f, 0.2f, 0.5f);

        private GridCoord _cursor;
        private Action _onLaunch;

        private GameObject _ghost;
        private Transform _highlight;
        private Renderer _highlightRenderer;
        private readonly List<Transform> _synergyHighlights = new();
        private readonly List<GridCoord> _previewCells = new();

        private readonly StringBuilder _sb = new();

        private readonly List<MeshFilter> _arcViews = new();
        private MaterialPropertyBlock _arcBlock;
        private static readonly Color InstalledArcColor = new(0.45f, 1f, 0.55f, 0.28f);
        private static readonly Color HeldArcColor = new(1f, 0.85f, 0.3f, 0.45f);
        private static readonly Color CutoutArcColor = new(1f, 0.25f, 0.2f, 0.35f);

        private static readonly Color OkColor = new(0.35f, 1f, 0.55f, 0.45f);
        private static readonly Color BadColor = new(1f, 0.30f, 0.25f, 0.45f);

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (grid == null) Debug.LogError("[RefitUI] ShipGrid 미할당.", this);

            if (shipCamera != null) shipCamera.enabled = false;
        }

        private void OnEnable() => Game.Core.GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => Game.Core.GameEvents.StateChanged -= OnStateChanged;

        /// <summary>
        /// 정비가 아닌 상태로 넘어가면 커서와 미리보기를 반드시 치운다.
        /// 전투 화면에 배치용 표시가 남아 있으면 안 된다.
        /// </summary>
        private void OnStateChanged(Game.Core.GameState state)
        {
            if (state != Game.Core.GameState.Refit) CleanupStageObjects();
        }

        // --------------------------------------------------------------- 흐름

        /// <summary>예전 호출 호환: 블록 정의 목록을 설치 카드로 연다.</summary>
        public void Open(int level, List<ModuleDefinition> choices, Action onLaunch)
        {
            var cards = new List<RefitCard>();
            if (choices != null) foreach (var def in choices) if (def != null) cards.Add(RefitCard.Install(def));
            Open(level, cards, onLaunch);
        }

        /// <summary>레벨업 화면을 연다. 카드(설치 · 무장 강화 · 성장)를 먼저 고르고, 설치 카드면 그 블록을 배치한다.</summary>
        public void Open(int level, List<RefitCard> cards, Action onLaunch, bool bossReward = false)
        {
            _onLaunch = onLaunch;
            _bossReward = bossReward;

            _cards.Clear();
            if (cards != null) _cards.AddRange(cards);

            _fleet = Game.Core.GameManager.Instance != null && Game.Core.GameManager.Instance.Player != null
                ? Game.Core.GameManager.Instance.Player.GetComponent<TaskForceEscortFormation>() : null;
            _openLevel = level;
            _fleetResult = null;
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            CloseFleetPanel();
            _upgradeResult = null;
            _growthResult = null;
            _upgradePicking = false;

            _reward = null;
            _rewardUsed = true;
            _choosing = _cards.Count > 0;

            ClearHeld();
            _cursor = default;

            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = bossReward ? "보스 격침 보상 — 배치 · 강화" : $"레벨 {level} 정비 — 배치 · 강화";
            if (shipCamera != null) shipCamera.enabled = true;

            EnsureHighlight();
            ShowCards();
            UpdateCamera();
            Refresh();
        }

        public void Close()
        {
            _choosing = false;
            _upgradePicking = false;
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            CloseFleetPanel();
            if (cardPanel != null) cardPanel.SetActive(false);

            CleanupStageObjects();

            if (shipCamera != null) shipCamera.enabled = false;
            if (root != null) root.SetActive(false);

            _held = null;
        }

        // --- 카드 꾸밈(코드로 한 번 만든다): 위 색 띠 · 분류 줄 · 그림 틀 · 그림 위 글자(호위함 코드)
        private Image[] _cardAccents, _iconFrames;
        private TMP_Text[] _cardTags, _iconLabels;
        private static readonly Color CardColor = new(0.085f, 0.115f, 0.15f, 1f);
        private static readonly Color FrameColor = new(0.05f, 0.075f, 0.1f, 1f);

        private void ShowCards()
        {
            if (cardPanel != null) cardPanel.SetActive(_choosing);
            PrepareCardPanel();
            if (cardRects == null) return;

            for (int i = 0; i < cardRects.Length; i++)
            {
                bool used = i < _cards.Count;
                if (cardRects[i] != null) cardRects[i].gameObject.SetActive(used);
                if (!used) continue;

                LayoutCard(i);
                var name = cardNames != null && i < cardNames.Length ? cardNames[i] : null;
                var icon = cardIcons != null && i < cardIcons.Length ? cardIcons[i] : null;
                var stats = cardStats != null && i < cardStats.Length ? cardStats[i] : null;

                var card = _cards[i];
                var def = card.Module;

                if (card.Kind is RefitCardKind.FleetDeploy or RefitCardKind.FleetUpgrade)
                {
                    ShowFleetCard(i, card, name, icon, stats);
                    continue;
                }

                if (card.Kind == RefitCardKind.Growth)
                {
                    var gdef = RunUpgrades.Definition(card.Stat);
                    var tc = RunUpgrades.TierColor(card.Tier);
                    string hex = ColorUtility.ToHtmlStringRGB(tc);
                    if (name != null)
                        name.text = $"<color=#e0c05a>[{i + 1}]</color> {gdef.Name}  <color=#{hex}><size=80%>{RunUpgrades.TierName(card.Tier)}</size></color>";
                    SetCardDecor(i, tc, ModuleCardText.BuildGrowthTag(gdef, card.Tier, card.BossReward), ModuleCardText.BuildGrowthIconLabel(gdef, card.Tier));
                    if (icon != null) { icon.sprite = null; icon.enabled = false; }
                    if (stats != null) stats.text = ModuleCardText.BuildGrowthStats(gdef, card.Tier);
                    continue;
                }

                if (card.Kind == RefitCardKind.WeaponUpgrade)
                {
                    if (def == null) { ShowEquipmentUpgradeCard(i, name, icon, stats); continue; }
                    int from = LowestUpgradeLevel(def);
                    var stage = ModuleUpgrades.ProfileFor(def)?.GetStage(from + 1);
                    if (name != null)
                        name.text = $"<color=#e0c05a>[{i + 1}]</color> {def.DisplayName} 강화 {ModuleUpgrades.Roman(from + 1)}";
                    SetCardDecor(i, UpgradeAccent, ModuleCardText.BuildTag(def), null);
                    if (icon != null)
                    {
                        // 강화 후 외형(아이콘이 있으면)을 보여 준다
                        var sprite = stage != null && stage.Icon != null ? stage.Icon : def.Icon;
                        icon.sprite = sprite;
                        icon.preserveAspect = true;
                        icon.color = Color.white;
                        icon.enabled = sprite != null;
                    }
                    if (stats != null) stats.text = ModuleCardText.BuildUpgradeChoice(def, from);
                    continue;
                }

                // 설치 카드
                if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> {def.DisplayName}";
                SetCardDecor(i, CategoryAccent(def.Type), ModuleCardText.BuildTag(def), null);
                if (icon != null)
                {
                    icon.sprite = def.Icon;
                    icon.preserveAspect = true;
                    icon.color = Color.white;
                    icon.enabled = def.Icon != null;
                }
                if (stats != null) stats.text = ModuleCardText.BuildStats(def);
            }
        }

        /// <summary>카드 화면은 불투명하게 덮고(뒤의 정비 문구가 비치지 않게) 제목에 레벨을 넣는다.</summary>
        private void PrepareCardPanel()
        {
            if (cardPanel == null) return;
            var bg = cardPanel.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.025f, 0.04f, 0.055f, 1f);
            var title = cardPanel.transform.Find("CardTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = _bossReward ? "보스 격침 — 보상 선택" : _openLevel > 0 ? $"레벨 {_openLevel} — 보상 선택" : "보상 선택";
                title.rectTransform.anchoredPosition = new Vector2(0f, -56f);
            }
        }

        private int _openLevel;

        private void SetCardDecor(int i, Color accent, string tag, string iconLabel)
        {
            if (_cardAccents != null && _cardAccents[i] != null) _cardAccents[i].color = accent;
            if (_cardTags != null && _cardTags[i] != null) _cardTags[i].text = tag;
            if (_iconFrames != null && _iconFrames[i] != null)
                _iconFrames[i].color = iconLabel != null ? new Color(accent.r * 0.25f, accent.g * 0.25f, accent.b * 0.25f, 1f) : FrameColor;
            if (_iconLabels != null && _iconLabels[i] != null)
            {
                _iconLabels[i].gameObject.SetActive(iconLabel != null);
                _iconLabels[i].text = iconLabel ?? "";
                _iconLabels[i].color = accent;
            }
        }

        private static Color CategoryAccent(ModuleType t) => t switch
        {
            ModuleType.Autocannon => new Color(0.88f, 0.58f, 0.42f),
            ModuleType.NavalGun or ModuleType.GuidedRocket => new Color(0.88f, 0.75f, 0.35f),
            ModuleType.Vls => new Color(0.5f, 0.72f, 0.88f),
            ModuleType.Ciws or ModuleType.SamLauncher => new Color(0.56f, 0.82f, 1f),
            ModuleType.AswLauncher or ModuleType.Sonar or ModuleType.HelicopterDeck => new Color(0.41f, 0.72f, 1f),
            ModuleType.DecoyLauncher or ModuleType.EwSuite => new Color(0.82f, 0.6f, 1f),
            ModuleType.RepairBay => new Color(0.5f, 0.82f, 0.54f),
            _ => new Color(0.78f, 0.83f, 0.86f),
        };

        /// <summary>통합 장비 강화 카드(2026-10-03): 강화할 수 있는 장비 목록. 대상은 고른 뒤 함선에서 고른다.</summary>
        private void ShowEquipmentUpgradeCard(int i, TMP_Text name, Image icon, TMP_Text stats)
        {
            int count = 0;
            foreach (var m in grid.Modules) if (m != null && m.CanUpgrade) count++;
            if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 장비 강화";
            SetCardDecor(i, UpgradeAccent, $"<color=#7fd08a>장비 강화</color>  ·  설치된 장비 하나를 한 단계  ·  대상 {count}개",
                         "<size=170%><b>UP</b></size>\n<color=#c8d4dc>장비 +1단계</color>");
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            if (stats != null) stats.text = ModuleCardText.BuildEquipmentUpgradeChoice(grid);
        }

        private static readonly Color UpgradeAccent = new(0.35f, 0.9f, 0.45f);

        /// <summary>이 카드로 강화할 수 있는 블록 중 가장 낮은 단계(카드에 "다음 단계"로 보여 준다).</summary>
        private int LowestUpgradeLevel(ModuleDefinition def)
        {
            int lowest = int.MaxValue;
            foreach (var m in grid.Modules)
                if (ModuleUpgrades.CanUpgradeWith(m, def)) lowest = Mathf.Min(lowest, m.UpgradeLevel);
            return lowest == int.MaxValue ? 0 : lowest;
        }

        /// <summary>
        /// 카드 한 장의 구역(위→아래): 색 띠 · 이름 · 분류 줄 · 그림(틀 안) · 표(항목 | 값).
        /// 글자는 모두 왼쪽 정렬, 값은 표의 같은 지점(44%)에 맞춘다.
        /// </summary>
        private void LayoutCard(int index)
        {
            var rect = cardRects[index];
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.07f + index * 0.30f, 0.14f);
            rect.anchorMax = new Vector2(0.33f + index * 0.30f, 0.86f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (rect.GetComponent<RectMask2D>() == null) rect.gameObject.AddComponent<RectMask2D>();
            var cardImage = rect.GetComponent<Image>();
            if (cardImage != null) cardImage.color = CardColor;
            EnsureCardDecor(index, rect);

            if (cardNames != null && index < cardNames.Length && cardNames[index] != null)
            {
                var n = cardNames[index];
                FitCardRegion(n.rectTransform, new Vector2(0, 0.905f), new Vector2(1, 0.985f), 22, 4);
                n.enableAutoSizing = true;
                n.fontSizeMin = 20;
                n.fontSizeMax = 30;
                n.fontStyle = FontStyles.Bold;
                n.alignment = TextAlignmentOptions.MidlineLeft;
                n.overflowMode = TextOverflowModes.Ellipsis;
                n.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (_cardTags[index] != null) FitCardRegion(_cardTags[index].rectTransform, new Vector2(0, 0.855f), new Vector2(1, 0.905f), 22, 2);
            if (_iconFrames[index] != null) FitCardRegion(_iconFrames[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 22, 6);
            if (cardIcons != null && index < cardIcons.Length && cardIcons[index] != null)
            {
                FitCardRegion(cardIcons[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 30, 12);
                cardIcons[index].transform.SetAsLastSibling();
            }
            if (_iconLabels[index] != null)
            {
                FitCardRegion(_iconLabels[index].rectTransform, new Vector2(0, 0.53f), new Vector2(1, 0.85f), 30, 12);
                _iconLabels[index].transform.SetAsLastSibling();
            }
            if (cardStats != null && index < cardStats.Length && cardStats[index] != null)
            {
                var s = cardStats[index];
                FitCardRegion(s.rectTransform, new Vector2(0, 0.02f), new Vector2(1, 0.51f), 24, 8);
                s.enableAutoSizing = true;
                s.fontSizeMin = 16;
                s.fontSizeMax = 24;
                s.lineSpacing = 22f;
                s.alignment = TextAlignmentOptions.TopLeft;
                s.overflowMode = TextOverflowModes.Ellipsis;
                s.textWrappingMode = TextWrappingModes.Normal;
                s.color = new Color(0.9f, 0.94f, 0.96f);
            }
        }

        private void EnsureCardDecor(int index, RectTransform card)
        {
            int n = cardRects.Length;
            _cardAccents ??= new Image[n];
            _iconFrames ??= new Image[n];
            _cardTags ??= new TMP_Text[n];
            _iconLabels ??= new TMP_Text[n];
            if (_cardAccents[index] != null) return;

            var font = cardNames != null && index < cardNames.Length && cardNames[index] != null ? cardNames[index].font : null;
            _cardAccents[index] = UiRect("Accent", card, Vector2.zero, Vector2.zero, Color.white).GetComponent<Image>();
            var ar = _cardAccents[index].rectTransform;
            ar.anchorMin = new Vector2(0, 1); ar.anchorMax = Vector2.one; ar.pivot = new Vector2(0.5f, 1f);
            ar.sizeDelta = new Vector2(0, 6); ar.anchoredPosition = Vector2.zero;

            _iconFrames[index] = UiRect("Icon frame", card, Vector2.zero, Vector2.zero, FrameColor).GetComponent<Image>();

            var tag = UiText((RectTransform)card, font, Vector2.zero, Vector2.zero, 17);
            tag.name = "Tag";
            tag.alignment = TextAlignmentOptions.MidlineLeft;
            tag.color = new Color(0.72f, 0.8f, 0.86f);
            tag.enableAutoSizing = true;
            tag.fontSizeMin = 13; tag.fontSizeMax = 17;
            tag.textWrappingMode = TextWrappingModes.NoWrap;
            tag.overflowMode = TextOverflowModes.Ellipsis;
            _cardTags[index] = tag;

            var label = UiText((RectTransform)card, font, Vector2.zero, Vector2.zero, 34);
            label.name = "Icon label";
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            _iconLabels[index] = label;
            label.gameObject.SetActive(false);
        }

        private static void FitCardRegion(RectTransform rect, Vector2 min, Vector2 max, float padX = 18, float padY = 12)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        private void ChooseCard(int index)
        {
            if (!_choosing || index < 0 || index >= _cards.Count) return;

            var card = _cards[index];
            _upgradeResult = null;
            _growthResult = null;
            _fleetResult = null;
            if (cardPanel != null) cardPanel.SetActive(false);

            switch (card.Kind)
            {
                case RefitCardKind.Growth:
                    ApplyGrowth(card);
                    return;

                case RefitCardKind.WeaponUpgrade:
                    _reward = card.Module;
                    _rewardUsed = false;
                    _choosing = false;
                    BeginUpgradePick();
                    return;

                case RefitCardKind.FleetDeploy:   // 편대 슬롯 화면에서 빈 슬롯을 고른다
                    _choosing = false;
                    _reward = null;
                    OpenFleetPanel(true, -1);
                    return;

                case RefitCardKind.FleetUpgrade:  // 편대 슬롯 화면에서 강화할 호위함을 고른다
                    _choosing = false;
                    _reward = null;
                    OpenFleetPanel(false, card.EscortIndex);
                    return;

                default:   // 설치
                    _reward = card.Module;
                    _rewardUsed = false;
                    _choosing = false;
                    TakeReward();
                    return;
            }
        }

        /// <summary>성장 카드: 즉시 적용하고(함선 전체 능력) 배치 화면으로 넘어간다.</summary>
        private void ApplyGrowth(RefitCard card)
        {
            var def = RunUpgrades.Definition(card.Stat);
            float add = def != null ? def.ValueOf(card.Tier) : 0f;
            RunUpgrades.Add(card.Stat, card.Tier);
            _choosing = false;
            _reward = null;
            _rewardUsed = true;
            _growthResult = def != null
                ? $"성장 완료: {def.Name} +{add * 100f:0.#}% ({RunUpgrades.TierName(card.Tier)}) — 누적 {RunUpgrades.Describe(card.Stat, RunUpgrades.Get(card.Stat))}"
                : "성장 카드를 적용하지 못했습니다.";
            Refresh();
        }

        // --------------------------------------------------------------- 편대 카드

        private static readonly Color FleetAccent = new(0.4f, 0.9f, 0.95f);

        private void ShowFleetCard(int i, RefitCard card, TMP_Text name, Image icon, TMP_Text stats)
        {
            if (icon != null) { icon.sprite = null; icon.enabled = false; }
            int count = _fleet != null ? _fleet.EscortCount : 0;
            if (card.Kind == RefitCardKind.FleetDeploy)
            {
                if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 고속정 합류";
                SetCardDecor(i, FleetAccent, $"<color=#66e6f2>편대 카드</color>  ·  편대 배치  ·  {count}/{TaskForceEscortFormation.MaxEscorts}척",
                             "<size=170%><b>PB</b></size>\n<color=#c8d4dc>편대 +1</color>");
                if (stats != null) stats.text = ModuleCardText.BuildFleetDeploy(count + 1);
                return;
            }

            // 편대 강화: 대상은 고른 뒤 편대 슬롯 화면에서 고른다 — 카드에는 슬롯마다 무엇이 되는지 보여 준다
            if (name != null) name.text = $"<color=#e0c05a>[{i + 1}]</color> 편대 강화";
            SetCardDecor(i, FleetAccent, "<color=#66e6f2>편대 강화</color>  ·  슬롯에서 호위함 선택  ·  역할 지정 또는 개량",
                         "<size=170%><b>TF</b></size>\n<color=#c8d4dc>편대 강화</color>");
            if (stats != null) stats.text = ModuleCardText.BuildFleetUpgradeAny(_fleet);
        }

        private void ApplyFleetDeploy(int slot)
        {
            _choosing = false;
            _reward = null;
            _rewardUsed = true;
            int index = _fleet != null ? _fleet.Deploy(slot) : -1;
            _fleetResult = index >= 0
                ? $"편대 합류: {_fleet.GetInfo(index).Name} → {slot + 1}번 슬롯 ({_fleet.EscortCount}/{TaskForceEscortFormation.MaxEscorts}척) — 편대 강화 카드로 역할을 고를 수 있습니다"
                : "그 슬롯에 배치하지 못했습니다.";
            ShowFleetResult();
        }

        private void ApplyFleetUpgrade(int index)
        {
            _rewardUsed = true;
            bool ok = _fleet != null && _fleet.Upgrade(index);
            var info = _fleet != null ? _fleet.GetInfo(index) : default;
            _fleetResult = ok
                ? $"편대 강화: {info.Name} 개량 {info.Tier} — {TaskForceEscortFormation.TierDescription(info.Role, info.Tier)}"
                : "편대 강화를 적용하지 못했습니다.";
            ShowFleetResult();
        }

        /// <summary>역할 없는 고속정의 역할을 고른다: 1~4 키 또는 클릭, 우클릭·Esc = 카드로 돌아가기.</summary>
        private void OpenRolePicker(int index)
        {
            _rolePickIndex = index;
            _rolePicking = true;
            EnsureRolePanel();
            if (_rolePanel != null) { _rolePanel.gameObject.SetActive(true); _rolePanel.SetAsLastSibling(); }   // 편대 슬롯 화면 위에
            Refresh();
        }

        private void ChooseRole(int option)
        {
            if (!_rolePicking || option < 0 || option >= TaskForceEscortFormation.Roles.Length) return;
            var role = TaskForceEscortFormation.Roles[option];
            string before = _fleet != null ? _fleet.GetInfo(_rolePickIndex).Name : "";
            bool ok = _fleet != null && _fleet.AssignRole(_rolePickIndex, role);
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            _rewardUsed = true;
            _fleetResult = ok
                ? $"편대 강화: {before} → {_fleet.GetInfo(_rolePickIndex).Name} — {TaskForceEscortFormation.RoleSummary(role)}"
                : "역할을 지정하지 못했습니다.";
            ShowFleetResult();
        }

        private void HandleRoleInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { ChooseRole(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { ChooseRole(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { ChooseRole(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { ChooseRole(3); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
            }
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            Vector2 p = mouse.position.ReadValue();
            for (int i = 0; i < _roleOptions.Count; i++)
                if (Hit(_roleOptions[i], p)) { ChooseRole(i); return; }
        }

        /// <summary>역할 선택 패널(코드로 한 번만 만든다). 카드 패널과 같은 캔버스, 화면 가운데.</summary>
        private void EnsureRolePanel()
        {
            if (_rolePanel != null) return;
            var parent = cardPanel != null ? cardPanel.transform.parent : root != null ? root.transform : null;
            if (parent == null) return;
            var font = titleText != null ? titleText.font : null;

            _rolePanel = UiRect("Escort role choice", parent, new Vector2(0.5f, 0.5f), new Vector2(1180, 300), new Color(0.02f, 0.07f, 0.09f, 0.96f));
            UiText(_rolePanel, font, new Vector2(0, 112), new Vector2(1100, 44), 26).text = "<b>호위함 역할 지정</b>  —  1 / 2 / 3 / 4 키 또는 클릭";
            var roles = TaskForceEscortFormation.Roles;
            for (int i = 0; i < roles.Length; i++)
            {
                var rc = TaskForceEscortFormation.RoleColor(roles[i]);
                var opt = UiRect($"Role {TaskForceEscortFormation.RoleCode(roles[i])}", _rolePanel, new Vector2(0.5f, 0.5f), new Vector2(270, 170),
                                 new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
                opt.anchoredPosition = new Vector2(-435 + i * 290, -10);
                var t = UiText(opt, font, Vector2.zero, new Vector2(250, 160), 21);
                t.text = $"<color=#e0c05a>[{i + 1}]</color>\n<size=140%><b><color=#{ColorUtility.ToHtmlStringRGB(rc)}>{TaskForceEscortFormation.RoleCode(roles[i])}</color></b></size>\n" +
                         $"<b>{TaskForceEscortFormation.RoleName(roles[i])}</b>\n<size=80%><color=#c8d4dc>{TaskForceEscortFormation.RoleSummary(roles[i])}</color></size>";
                _roleOptions.Add(opt);
            }
            UiText(_rolePanel, font, new Vector2(0, -125), new Vector2(900, 30), 17).text = "<color=#8fa4b8>우클릭 / Esc — 카드로 돌아가기</color>";
            _rolePanel.gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- 편대 슬롯 화면(2026-10-03)
        // 편대 카드(배치·강화)를 고르면 함선 블록 배치 화면 대신 이 화면이 뜬다. 슬롯 1~4 중
        // 배치 카드 = 고속정을 넣을 빈 슬롯, 강화 카드 = 강화할 호위함을 고른다. 적용 뒤 결과를 보여 주고
        // 전투 재개(Space·Enter) 또는 함선 배치 화면(Tab, 블록 재배치)으로 넘어간다.

        private bool _fleetPicking;      // 슬롯 고르는 중
        private bool _fleetDeployMode;   // true = 편대 배치(빈 슬롯), false = 편대 강화(호위함)
        private bool _fleetDone;         // 적용 끝 — 결과 화면
        private int _fleetHover = -1;
        private RectTransform _fleetPanel, _fleetResume, _fleetToShip;
        private TMP_Text _fleetTitle, _fleetDetail, _fleetFooter;
        private readonly List<RectTransform> _fleetSlotRects = new();
        private readonly List<Image> _fleetSlotBgs = new();
        private readonly List<TMP_Text> _fleetSlotTexts = new();

        /// <summary>편대 슬롯 화면을 연다. preselectIndex = 강화 카드에서 처음 가리킬 호위함 번호(-1 = 고를 수 있는 첫 슬롯).</summary>
        private void OpenFleetPanel(bool deploy, int preselectIndex)
        {
            _fleetDeployMode = deploy;
            _fleetPicking = true;
            _fleetDone = false;
            EnsureFleetPanel();
            _fleetHover = -1;
            if (_fleet != null)
                _fleetHover = deploy ? _fleet.FirstEmptyRosterSlot()
                            : preselectIndex >= 0 ? _fleet.GetInfo(preselectIndex).RosterSlot : -1;
            if (!FleetSlotSelectable(_fleetHover)) _fleetHover = FirstSelectableSlot();
            if (_fleetPanel != null) { _fleetPanel.gameObject.SetActive(true); _fleetPanel.SetAsLastSibling(); }
            RefreshFleetPanel();
        }

        private void CloseFleetPanel()
        {
            _fleetPicking = false;
            _fleetDone = false;
            if (_fleetPanel != null) _fleetPanel.gameObject.SetActive(false);
        }

        /// <summary>지금 고를 수 있는 슬롯인가: 배치 = 빈 슬롯, 강화 = 역할이 없거나 최대 개량이 아닌 호위함.</summary>
        private bool FleetSlotSelectable(int slot)
        {
            if (_fleet == null || slot < 0 || slot >= TaskForceEscortFormation.MaxEscorts) return false;
            int index = _fleet.IndexOfRosterSlot(slot);
            return _fleetDeployMode ? index < 0 && _fleet.CanDeploy : index >= 0 && _fleet.CanUpgrade(index);
        }

        private int FirstSelectableSlot()
        {
            for (int s = 0; s < TaskForceEscortFormation.MaxEscorts; s++) if (FleetSlotSelectable(s)) return s;
            return -1;
        }

        /// <summary>슬롯(0~3)을 고른다 — 1~4 키·클릭·Enter. 역할 없는 고속정을 강화하면 역할 선택 창이 이어서 뜬다.</summary>
        private void ChooseFleetSlot(int slot)
        {
            if (!_fleetPicking || !FleetSlotSelectable(slot)) return;
            _fleetHover = slot;
            if (_fleetDeployMode) { ApplyFleetDeploy(slot); return; }

            int index = _fleet.IndexOfRosterSlot(slot);
            if (_fleet.GetInfo(index).Role == EscortRole.None)
            {
                _fleetPicking = false;   // 슬롯 화면은 뒤에 그대로 두고 역할 선택 창을 위에 띄운다
                RefreshFleetPanel();
                OpenRolePicker(index);
                return;
            }
            ApplyFleetUpgrade(index);
        }

        /// <summary>적용 끝: 바뀐 슬롯과 결과를 보여 주고 전투 재개 / 함선 배치 화면을 기다린다.</summary>
        private void ShowFleetResult()
        {
            _fleetPicking = false;
            _fleetDone = true;
            EnsureFleetPanel();
            if (_fleetPanel != null) { _fleetPanel.gameObject.SetActive(true); _fleetPanel.SetAsLastSibling(); }
            RefreshFleetPanel();
        }

        private void HandleFleetInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            Vector2 p = mouse != null ? mouse.position.ReadValue() : default;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (_fleetDone)
            {
                if ((kb != null && kb.tabKey.wasPressedThisFrame) || (click && Hit(_fleetToShip, p)))
                {
                    CloseFleetPanel();   // 함선 배치 화면(블록 재배치 · Space로 재개)
                    Refresh();
                    return;
                }
                if ((kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ||
                    (click && Hit(_fleetResume, p)))
                {
                    CloseFleetPanel();
                    TryLaunch();
                }
                return;
            }

            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) { ChooseFleetSlot(0); return; }
                if (kb.digit2Key.wasPressedThisFrame) { ChooseFleetSlot(1); return; }
                if (kb.digit3Key.wasPressedThisFrame) { ChooseFleetSlot(2); return; }
                if (kb.digit4Key.wasPressedThisFrame) { ChooseFleetSlot(3); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { ChooseFleetSlot(_fleetHover); return; }
                int dir = (kb.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (kb.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                if (dir != 0)
                {
                    int max = TaskForceEscortFormation.MaxEscorts;
                    int from = _fleetHover < 0 ? (dir > 0 ? -1 : max) : _fleetHover;
                    for (int k = 1; k <= max; k++)
                    {
                        int s = ((from + dir * k) % max + max) % max;
                        if (FleetSlotSelectable(s)) { _fleetHover = s; break; }
                    }
                    RefreshFleetPanel();
                }
            }

            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            for (int s = 0; s < _fleetSlotRects.Count; s++)
            {
                if (!Hit(_fleetSlotRects[s], p)) continue;
                if (FleetSlotSelectable(s) && _fleetHover != s) { _fleetHover = s; RefreshFleetPanel(); }
                if (click) ChooseFleetSlot(s);
                return;
            }
        }

        /// <summary>편대 슬롯 화면(코드로 한 번만 만든다). 카드 화면처럼 화면 전체를 불투명하게 덮는다.</summary>
        private void EnsureFleetPanel()
        {
            if (_fleetPanel != null) return;
            var parent = cardPanel != null ? cardPanel.transform.parent : root != null ? root.transform : null;
            if (parent == null) return;
            var font = titleText != null ? titleText.font : null;

            _fleetPanel = UiRect("Fleet slots", parent, new Vector2(0.5f, 0.5f), Vector2.zero, new Color(0.025f, 0.04f, 0.055f, 1f));
            _fleetPanel.anchorMin = Vector2.zero;
            _fleetPanel.anchorMax = Vector2.one;
            _fleetPanel.offsetMin = _fleetPanel.offsetMax = Vector2.zero;

            _fleetTitle = UiText(_fleetPanel, font, new Vector2(0, 330), new Vector2(1600, 56), 34);
            _fleetTitle.fontStyle = FontStyles.Bold;
            for (int s = 0; s < TaskForceEscortFormation.MaxEscorts; s++)
            {
                var slot = UiRect($"Fleet slot {s + 1}", _fleetPanel, new Vector2(0.5f, 0.5f), new Vector2(350, 420), FrameColor);
                slot.anchoredPosition = new Vector2(-570 + s * 380, 40);
                var text = UiText(slot, font, Vector2.zero, new Vector2(320, 390), 22);
                text.alignment = TextAlignmentOptions.Top;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.lineSpacing = 6f;
                _fleetSlotRects.Add(slot);
                _fleetSlotBgs.Add(slot.GetComponent<Image>());
                _fleetSlotTexts.Add(text);
            }
            _fleetDetail = UiText(_fleetPanel, font, new Vector2(0, -225), new Vector2(1560, 70), 22);
            _fleetDetail.textWrappingMode = TextWrappingModes.Normal;
            _fleetFooter = UiText(_fleetPanel, font, new Vector2(0, -285), new Vector2(1560, 34), 19);
            _fleetResume = FleetButton("Resume", font, new Vector2(-200, -360), "<b>전투 재개</b>  <color=#8fa4b8>[Space]</color>");
            _fleetToShip = FleetButton("To ship", font, new Vector2(200, -360), "<b>함선 배치 화면</b>  <color=#8fa4b8>[Tab]</color>");
            _fleetPanel.gameObject.SetActive(false);
        }

        private RectTransform FleetButton(string name, TMP_FontAsset font, Vector2 position, string label)
        {
            var button = UiRect(name, _fleetPanel, new Vector2(0.5f, 0.5f), new Vector2(360, 58), new Color(0.09f, 0.22f, 0.26f, 1f));
            button.anchoredPosition = position;
            UiText(button, font, Vector2.zero, new Vector2(340, 52), 22).text = label;
            return button;
        }

        private void RefreshFleetPanel()
        {
            if (_fleetPanel == null) return;
            int max = TaskForceEscortFormation.MaxEscorts;
            int count = _fleet != null ? _fleet.EscortCount : 0;
            string formation = _fleet != null ? FleetFormations.Name(_fleet.Formation) : "";
            _fleetTitle.text = _fleetDone ? $"편대  <size=75%><color=#8fa4b8>{count}/{max}척 · {formation}</color></size>"
                : _fleetDeployMode ? "편대 배치 — 고속정을 넣을 슬롯을 고르세요"
                : "편대 강화 — 강화할 호위함을 고르세요";

            for (int s = 0; s < max; s++)
            {
                int index = _fleet != null ? _fleet.IndexOfRosterSlot(s) : -1;
                var info = index >= 0 ? _fleet.GetInfo(index) : default;
                bool selectable = _fleetPicking && FleetSlotSelectable(s);
                bool lit = s == _fleetHover && (selectable || _fleetDone);
                Color rc = index >= 0 ? TaskForceEscortFormation.RoleColor(info.Role) : FleetAccent;
                float k = lit ? 0.42f : selectable ? 0.2f : 0.09f;
                _fleetSlotBgs[s].color = new Color(rc.r * k, rc.g * k, rc.b * k, 1f);
                _fleetSlotTexts[s].alpha = !_fleetPicking || selectable ? 1f : 0.45f;
                _fleetSlotTexts[s].text = FleetSlotText(s, index, info);
            }

            _fleetDetail.text = _fleetDone ? $"<color=#68dede>{_fleetResult}</color>" : FleetHoverDetail();
            _fleetFooter.text = _fleetDone
                ? "<color=#8fa4b8>Space / Enter — 전투 재개    Tab — 함선 배치 화면(블록 재배치)</color>"
                : "<color=#8fa4b8>1 ~ 4 키 · 클릭 · ←→ + Enter 로 선택    우클릭 / Esc — 카드로 돌아가기</color>";
            _fleetResume.gameObject.SetActive(_fleetDone);
            _fleetToShip.gameObject.SetActive(_fleetDone);
        }

        /// <summary>그 슬롯의 진형 자리: "함대원형진 앞 · 단종진 뒤 1번째 · 자율 스스로 판단".</summary>
        private static string SlotPlaces(int slot)
        {
            var sb = new StringBuilder();
            foreach (var f in FleetFormations.All)
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append($"{FleetFormations.Name(f)} {FleetFormations.SlotLabel(f, slot)}");
            }
            return sb.ToString();
        }

        private string FleetSlotText(int slot, int index, TaskForceEscortFormation.EscortInfo info)
        {
            _sb.Clear();
            _sb.Append($"<color=#e0c05a>[{slot + 1}]</color>  <b>{slot + 1}번 슬롯</b>\n");
            if (_fleet != null)
                _sb.Append($"<size=75%><color=#9fe0a0>{FleetFormations.Name(_fleet.Formation)} · {FleetFormations.SlotLabel(_fleet.Formation, slot)}</color></size>\n");
            if (index < 0)
            {
                _sb.Append("\n\n\n<size=125%><color=#5a6570>비어 있음</color></size>");
                if (_fleetPicking && _fleetDeployMode) _sb.Append("\n\n<color=#7fd08a>여기에 고속정 합류</color>");
                return _sb.ToString();
            }
            string hex = ColorUtility.ToHtmlStringRGB(TaskForceEscortFormation.RoleColor(info.Role));
            _sb.Append($"\n<size=200%><b><color=#{hex}>{TaskForceEscortFormation.RoleCode(info.Role)}</color></b></size>\n");
            _sb.Append($"<b>{info.Name}</b>\n");
            _sb.Append(info.Role == EscortRole.None ? "<color=#8fa4b8>역할 없음</color>\n" : $"개량 {info.Tier} / {TaskForceEscortFormation.MaxUpgradeLevel}\n");
            _sb.Append($"<size=85%><color=#8fa4b8>선체 {TaskForceEscortFormation.MaxHullFor(info.Tier):0} · {TaskForceEscortFormation.RoleShort(info.Role)}</color></size>");
            if (_fleetPicking && !_fleetDeployMode)
                _sb.Append(info.Role == EscortRole.None ? "\n\n<color=#7fd08a>역할 지정</color>"
                           : info.Tier >= TaskForceEscortFormation.MaxUpgradeLevel ? "\n\n<color=#e0c05a>최대 개량</color>"
                           : $"\n\n<color=#7fd08a>개량 {info.Tier} → {info.Tier + 1}</color>");
            else if (_fleetPicking) _sb.Append("\n\n<color=#5a6570>사용 중</color>");
            return _sb.ToString();
        }

        private string FleetHoverDetail()
        {
            if (_fleet == null || _fleetHover < 0)
                return _fleetDeployMode ? "빈 슬롯이 없습니다." : "강화할 수 있는 호위함이 없습니다.";
            if (_fleetDeployMode)
            {
                return $"<b>{_fleetHover + 1}번 슬롯</b>에 고속정 합류 — 진형 자리: {SlotPlaces(_fleetHover)}" +
                       "  <color=#8fa4b8>(슬롯마다 진형 자리가 정해져 있다)</color>";
            }
            var info = _fleet.GetInfo(_fleet.IndexOfRosterSlot(_fleetHover));
            if (info.Role == EscortRole.None)
                return $"<b>{info.Name}</b> — 역할 지정: 방공 · 대잠 · 전자전 · 미사일 중 하나(고르면 역할 선택 창)";
            int to = info.Tier + 1;
            return $"<b>{info.Name}</b> 개량 {info.Tier} → {to}: {TaskForceEscortFormation.TierDescription(info.Role, to)}" +
                   $"  <color=#8fa4b8>· 선체 {TaskForceEscortFormation.MaxHullFor(info.Tier):0} → {TaskForceEscortFormation.MaxHullFor(to):0}</color>";
        }

        // --------------------------------------------------------------- 장비 강화

        private static string UpgradeStepLabel(int from) => from <= 0 ? "기본 → 강화 I" : $"강화 {ModuleUpgrades.Roman(from)} → 강화 {ModuleUpgrades.Roman(from + 1)}";

        /// <summary>
        /// 강화할 블록을 고르게 한다. 대상이 하나뿐이면 고르지 않고 바로 적용한다.
        /// 통합 장비 강화 카드(_reward = null)는 강화 단계가 남은 모든 블록, 블록별 카드는 그 블록만.
        /// </summary>
        private void BeginUpgradePick()
        {
            _upgradePicking = true;
            ModuleInstance only = null;
            int count = 0;
            foreach (var m in grid.Modules)
            {
                if (!ModuleUpgrades.CanUpgradeWith(m, _reward)) continue;
                if (count == 0) { only = m; _cursor = m.Origin; }
                count++;
            }
            if (count == 0) { BackToCards(); return; }
            if (count == 1) TryUpgradeAt(only.Origin);
        }

        /// <summary>강화 선택을 그만두고 카드 선택 화면으로 돌아간다(우클릭·Esc·취소).</summary>
        private void BackToCards()
        {
            _upgradePicking = false;
            _rolePicking = false;
            if (_rolePanel != null) _rolePanel.gameObject.SetActive(false);
            CloseFleetPanel();
            _reward = null;
            _rewardUsed = true;
            _choosing = _cards.Count > 0;
            ShowCards();
        }

        /// <summary>커서 아래 블록을 한 단계 강화한다. 같은 블록이 아니거나 최대 단계면 아무것도 하지 않는다.</summary>
        private bool TryUpgradeAt(GridCoord coord)
        {
            if (!_upgradePicking) return false;
            var target = grid.Get(coord);
            if (!ModuleUpgrades.CanUpgradeWith(target, _reward) || !target.ApplyUpgrade()) return false;

            var stage = ModuleUpgrades.ProfileFor(target.Definition)?.GetStage(target.UpgradeLevel);
            _upgradeResult = $"{target.Definition.DisplayName} ({target.Origin.X},{target.Origin.Z}) 강화 {ModuleUpgrades.Roman(target.UpgradeLevel)} 완료" +
                             (stage != null && !string.IsNullOrEmpty(stage.Description) ? $" — {stage.Description}" : "");
            _upgradePicking = false;
            _rewardUsed = true;
            return true;
        }

        private void HandleUpgradePickInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                int dx = 0, dz = 0;
                if (kb.rightArrowKey.wasPressedThisFrame) dx += 1;
                if (kb.leftArrowKey.wasPressedThisFrame) dx -= 1;
                if (kb.downArrowKey.wasPressedThisFrame) dz += 1;
                if (kb.upArrowKey.wasPressedThisFrame) dz -= 1;
                if (dx != 0 || dz != 0) MoveCursor(dx, dz);
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { TryUpgradeAt(_cursor); return; }
                if (kb.escapeKey.wasPressedThisFrame) { BackToCards(); return; }
            }

            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.rightButton.wasPressedThisFrame) { BackToCards(); return; }
            if (TryGetCellUnderMouse(mouse.position.ReadValue(), out var coord))
            {
                _cursor = coord;
                ClampCursor();
                if (mouse.leftButton.wasPressedThisFrame) TryUpgradeAt(_cursor);
            }
        }

        private static bool Hit(RectTransform rect, Vector2 screenPos)
            => rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);

        private static RectTransform UiRect(string name, Transform parent, Vector2 anchor, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rect;
        }

        private static TMP_Text UiText(RectTransform parent, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        // --------------------------------------------------------------- 입력

        private void Update()
        {
            if (root == null || !root.activeSelf) return;

            if (_choosing) { HandleCardInput(); UpdateCamera(); return; }
            if (_rolePicking) { HandleRoleInput(); UpdateCamera(); return; }
            if (_fleetPicking || _fleetDone) { HandleFleetInput(); UpdateCamera(); return; }
            if (_upgradePicking) { HandleUpgradePickInput(); UpdateCamera(); Refresh(); return; }

            HandleKeyboard();
            HandleMouse();

            UpdateCamera();
            Refresh();
        }

        private void HandleCardInput()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) ChooseCard(0);
                if (kb.digit2Key.wasPressedThisFrame) ChooseCard(1);
                if (kb.digit3Key.wasPressedThisFrame) ChooseCard(2);
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || cardRects == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            for (int i = 0; i < cardRects.Length; i++)
            {
                if (cardRects[i] == null || !cardRects[i].gameObject.activeSelf) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(cardRects[i], screenPos, null)) continue;

                ChooseCard(i);
                return;
            }
        }

        private void HandleKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) TakeReward();

            int dx = 0, dz = 0;
            if (kb.rightArrowKey.wasPressedThisFrame) dx += 1;   // 선수 쪽
            if (kb.leftArrowKey.wasPressedThisFrame) dx -= 1;
            if (kb.downArrowKey.wasPressedThisFrame) dz += 1;    // 우현 쪽
            if (kb.upArrowKey.wasPressedThisFrame) dz -= 1;
            if (dx != 0 || dz != 0) MoveCursor(dx, dz);

            if (kb.rKey.wasPressedThisFrame) Rotate();
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) Confirm();
            if (kb.escapeKey.wasPressedThisFrame) ReturnHeld();
            if (kb.xKey.wasPressedThisFrame) ScrapUnderCursor();
            if (kb.eKey.wasPressedThisFrame) ToggleRaiseUnderCursor();
            if (kb.spaceKey.wasPressedThisFrame) TryLaunch();
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (TryGetCellUnderMouse(mouse.position.ReadValue(), out var coord))
            {
                _cursor = coord;
                ClampCursor();

                if (mouse.leftButton.wasPressedThisFrame) Confirm();
            }

            if (mouse.rightButton.wasPressedThisFrame && _heldWasInstalled) ReturnHeld();

            if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f) Rotate();
        }

        /// <summary>화면 좌표 -> 격자 칸. 렌더 텍스처라 뷰 안의 비율로 바꿔 광선을 쏜다.</summary>
        private bool TryGetCellUnderMouse(Vector2 screenPos, out GridCoord coord)
        {
            coord = default;
            if (shipView == null || shipCamera == null || grid == null) return false;

            var rect = shipView.rectTransform;
            if (!RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPos, null, out var local))
                return false;

            var r = rect.rect;
            var viewport = new Vector3((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height, 0f);

            var ray = shipCamera.ViewportPointToRay(viewport);
            var plane = new Plane(grid.transform.up, grid.CoordToWorld(default));
            if (!plane.Raycast(ray, out float enter)) return false;

            coord = grid.WorldToNearestCoord(ray.GetPoint(enter));
            return grid.InBounds(coord);
        }

        // --------------------------------------------------------------- 조작

        private void TakeReward()
        {
            if (_held != null || _rewardUsed || _reward == null) return;

            _held = _reward;
            _heldIsReward = true;
            _heldWasInstalled = false;
            _heldRotation = 0;

            // 빠른 배치: 바로 놓을 수 있는 좋은 자리에서 시작한다. 클릭 한 번이면 설치된다.
            if (TryFindSuggestedPlacement(_held, out var cell, out int rotation))
            {
                _cursor = cell;
                _heldRotation = rotation;
            }

            ClampCursor();
            SpawnGhost();
        }

        /// <summary>
        /// 설치 가능한 자리 중 함교에 가까운 곳을 고른다.
        /// 사격각이 주변 블록에 따라 달라지는 무기는 사격각이 넓은 자리를 먼저 본다.
        /// </summary>
        private bool TryFindSuggestedPlacement(ModuleDefinition def, out GridCoord best, out int bestRotation)
        {
            best = default;
            bestRotation = 0;
            if (def == null || grid == null) return false;

            bool arcWeapon = Game.Combat.FireArcCalculator.UsesNeighborArc(def.Type);
            int rotations = def.CanRotate ? 4 : 1;
            float bestScore = float.MaxValue;
            bool found = false;

            for (int rot = 0; rot < rotations; rot++)
            {
                for (int x = -grid.MaxHalfLength; x <= grid.MaxHalfLength; x++)
                {
                    for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++)
                    {
                        var c = new GridCoord(x, z);
                        if (!grid.CanPlace(def, c, rot, out _)) continue;

                        // 가까울수록, 좌우 중앙일수록, (무기라면) 사격각이 넓을수록 좋다
                        float score = Mathf.Abs(x) + Mathf.Abs(z) * 1.5f + rot * 0.01f;
                        if (arcWeapon)
                            score -= Game.Combat.FireArcCalculator.Compute(grid, c, rot, def.HeightClass).Width / 30f;

                        if (score >= bestScore) continue;
                        bestScore = score;
                        best = c;
                        bestRotation = rot;
                        found = true;
                    }
                }
            }
            return found;
        }

        private void Rotate()
        {
            if (_held == null || !_held.CanRotate) return;

            _heldRotation = (_heldRotation + 1) & 3;
            ClampCursor();

            if (_ghost != null)
                _ghost.transform.localRotation = Quaternion.Euler(0f, _heldRotation * 90f, 0f);
        }

        private void MoveCursor(int dx, int dz)
        {
            _cursor = new GridCoord(_cursor.X + dx, _cursor.Z + dz);
            ClampCursor();
        }

        private void ClampCursor()
        {
            GetFootprintSize(out int w, out int h);

            _cursor = new GridCoord(
                Mathf.Clamp(_cursor.X, -grid.MaxHalfLength, grid.MaxHalfLength - (w - 1)),
                Mathf.Clamp(_cursor.Z, -grid.MaxHalfBeam, grid.MaxHalfBeam - (h - 1)));
        }

        private void GetFootprintSize(out int w, out int h)
        {
            w = h = 1;
            if (_held == null) return;

            w = _held.Width; h = _held.Height;
            if ((_heldRotation & 1) == 1) (w, h) = (h, w);
        }

        /// <summary>들고 있으면 놓고, 아니면 커서 아래 모듈을 집는다(재배치).</summary>
        private void Confirm()
        {
            if (_held != null) { PlaceHeld(); return; }

            var instance = grid.Get(_cursor);
            if (instance == null) return;

            _held = instance.Definition;
            _heldRotation = instance.RotationSteps;
            _heldIsReward = false;

            _heldWasInstalled = true;
            _heldOrigin = instance.Origin;
            _heldOriginRotation = instance.RotationSteps;
            _heldWasRaised = instance.IsRaised;
            _heldUpgrade = instance.Upgrade;
            _cursor = instance.Origin;

            factory.Uninstall(instance);
            _originSynergy = ModuleSynergy.Preview(grid, _held, _heldOrigin, _heldOriginRotation);
            ClampCursor();
            SpawnGhost();
        }

        private void PlaceHeld()
        {
            if (!CanPlaceHere()) return;
            var placed = factory.Install(_held, _cursor, _heldRotation, _heldWasInstalled ? _heldUpgrade : null);
            if (placed == null) return;
            RestoreRaise(placed);

            if (_heldIsReward) _rewardUsed = true;
            ClearHeld();
        }

        private bool CanPlaceHere()
            => _held != null && grid.CanPlace(_held, _cursor, _heldRotation, out _);

        /// <summary>집어든 기존 모듈을 원래 자리로 되돌린다.</summary>
        private void ReturnHeld()
        {
            if (_held == null || !_heldWasInstalled) return;

            RestoreRaise(factory.Install(_held, _heldOrigin, _heldOriginRotation, _heldUpgrade));
            ClearHeld();
        }

        /// <summary>올려져 있던 무기를 옮겼으면, 새 자리도 조건을 만족할 때 다시 올린다.</summary>
        private void RestoreRaise(ModuleInstance placed)
        {
            if (!_heldWasRaised || placed == null) return;
            if (Game.Combat.FireArcCalculator.CanRaise(grid, placed)) factory.SetRaised(placed, true);
        }

        private void ClearHeld()
        {
            _originSynergy = null;
            _heldWasRaised = false;
            _heldUpgrade = null;
            _held = null;
            _heldIsReward = false;
            _heldWasInstalled = false;
            _heldRotation = 0;
            ClearGhost();
        }

        /// <summary>커서 아래 무기를 승강 거치대에 올리거나 내린다. 올리기는 3면 이상 막혔을 때만.</summary>
        private void ToggleRaiseUnderCursor()
        {
            if (_held != null || factory == null) return;

            var instance = grid.Get(_cursor);
            if (instance == null) return;

            if (instance.IsRaised) factory.SetRaised(instance, false);
            else if (Game.Combat.FireArcCalculator.CanRaise(grid, instance)) factory.SetRaised(instance, true);
        }

        /// <summary>커서 아래 모듈을 영구 철거한다. 함교는 배의 뿌리라 지울 수 없다.</summary>
        private void ScrapUnderCursor()
        {
            if (_held != null) return;

            var instance = grid.Get(_cursor);
            if (instance == null || instance.Definition.Type == ModuleType.Bridge) return;

            factory.Uninstall(instance);
        }

        private void TryLaunch()
        {
            if (_held != null) return;   // 들고 있으면 재개 불가
            _onLaunch?.Invoke();
        }

        // ----------------------------------------------------------- 3D 표시

        /// <summary>배가 자란 만큼 카메라가 물러나 전체가 항상 화면에 들어오게 한다.</summary>
        private void UpdateCamera()
        {
            if (grid == null || shipCamera == null) return;

            grid.GetExtent(out var min, out var max);

            float cell = grid.CellSize;
            var centerCoord = new Vector3((min.Z + max.Z) * 0.5f * cell,
                                          0f,
                                          (min.X + max.X) * 0.5f * cell);

            Vector3 focus = grid.transform.TransformPoint(
                centerCoord + Vector3.up * grid.DeckHeight);

            float lengthCells = Mathf.Max(1, max.X - min.X + 1);
            float beamCells = Mathf.Max(1, max.Z - min.Z + 1);

            // 가로로 긴 화면이라 길이를 절반으로 나눠 잡아도 충분히 담긴다
            float needed = Mathf.Max(lengthCells * cell * 0.5f, beamCells * cell) + orthoPadding;

            var rot = Quaternion.Euler(viewPitch, grid.transform.eulerAngles.y - 90f, 0f);

            shipCamera.orthographic = true;
            shipCamera.orthographicSize = Mathf.Max(minOrthoSize, needed);
            shipCamera.transform.SetPositionAndRotation(focus - rot * Vector3.forward * viewDistance, rot);
        }

        private void SpawnGhost()
        {
            ClearGhost();
            if (_held == null || _held.Prefab == null || moduleRoot == null) return;

            _ghost = Instantiate(_held.Prefab, moduleRoot);
            _ghost.name = "RefitGhost";
            _ghost.transform.localRotation = Quaternion.Euler(0f, _heldRotation * 90f, 0f);

            foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            foreach (var col in _ghost.GetComponentsInChildren<Collider>(true)) col.enabled = false;

            SetLayerRecursive(_ghost, ModuleFactory.LayerShip);
        }

        private void ClearGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
        }

        /// <summary>정비용 임시 오브젝트를 전부 없앤다. 남겨두면 전투 중에 보인다.</summary>
        private void CleanupStageObjects()
        {
            ClearGhost();
            ClearArcViews();
            foreach (var marker in _synergyHighlights) if (marker != null) Destroy(marker.gameObject);
            _synergyHighlights.Clear();
            foreach (var marker in _upgradeMarkers) if (marker != null) Destroy(marker.gameObject);
            _upgradeMarkers.Clear();
            foreach (var badge in _badges) if (badge != null) Destroy(badge.gameObject);
            _badges.Clear();

            if (_highlight != null) Destroy(_highlight.gameObject);
            _highlight = null;
            _highlightRenderer = null;
        }

        private void EnsureHighlight()
        {
            if (_highlight != null || cellHighlightPrefab == null || moduleRoot == null) return;

            var go = Instantiate(cellHighlightPrefab, moduleRoot);
            go.name = "RefitCursor";
            _highlight = go.transform;
            _highlightRenderer = go.GetComponentInChildren<Renderer>();

            SetLayerRecursive(go, ModuleFactory.LayerShip);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        // --------------------------------------------------------------- 갱신

        private void Refresh()
        {
            GetFootprintSize(out int w, out int h);
            float cell = grid.CellSize;

            if (_highlight != null)
            {

                _highlight.localPosition = grid.CoordToLocal(_cursor)
                    + new Vector3((h - 1) * 0.5f * cell, 0.06f, (w - 1) * 0.5f * cell);
                _highlight.localRotation = Quaternion.identity;
                _highlight.localScale = new Vector3(h * cell * 0.96f, 1f, w * cell * 0.96f);

                if (_highlightRenderer != null)
                    _highlightRenderer.material.color = _upgradePicking
                        ? (ModuleUpgrades.CanUpgradeWith(grid.Get(_cursor), _reward) ? OkColor : BadColor)
                        : (_held == null || CanPlaceHere()) ? OkColor : BadColor;
            }

            if (_ghost != null)
            {
                _ghost.transform.localPosition = grid.CoordToLocal(_cursor)
                    + new Vector3((h - 1) * 0.5f * cell, 0f, (w - 1) * 0.5f * cell);
            }

            UpdateSynergyHighlights();
            UpdateUpgradeMarkers();
            UpdateBadges();

            if (statusText != null && systems != null)
            {
                statusText.text = $"모듈 {grid.Modules.Count}   탐지 {systems.DetectionRange:0}   " +
                                  $"피해 감소 {systems.DamageReduction * 100f:0}%";
            }

            UpdateArcViews();

            if (rewardText != null) rewardText.text = !string.IsNullOrEmpty(_fleetResult)
                ? $"<color=#68dede>{_fleetResult}</color>"
                : !string.IsNullOrEmpty(_growthResult) ? $"<color=#ffd060>{_growthResult}</color>"
                : !string.IsNullOrEmpty(_upgradeResult) ? $"<color=#7fd08a>{_upgradeResult}</color>"
                : RewardLabel(_reward, _rewardUsed);
            if (infoText != null) infoText.text = BuildInfo();
        }

        /// <summary>
        /// 강화 선택 중: 강화 대상이 될 수 있는 블록(통합 카드 = 강화 단계가 있는 모든 블록, 블록별 카드 = 그 블록) 중
        /// 강화 가능 = 초록, 최대 강화 = 금색. 다른 블록은 강조하지 않는다.
        /// </summary>
        private void UpdateUpgradeMarkers()
        {
            int used = 0;
            if (_upgradePicking && cellHighlightPrefab != null && moduleRoot != null)
            {
                float cell = grid.CellSize;
                foreach (var m in grid.Modules)
                {
                    if (!ShownForUpgrade(m)) continue;
                    if (used >= _upgradeMarkers.Count)
                    {
                        var go = Instantiate(cellHighlightPrefab, moduleRoot);
                        go.name = "UpgradeTarget";
                        SetLayerRecursive(go, ModuleFactory.LayerShip);
                        _upgradeMarkers.Add(go.transform);
                    }
                    int w = m.Definition.Width, h = m.Definition.Height;
                    if ((m.RotationSteps & 1) == 1) (w, h) = (h, w);
                    var marker = _upgradeMarkers[used++];
                    marker.gameObject.SetActive(true);
                    marker.localPosition = grid.CoordToLocal(m.Origin) + new Vector3((h - 1) * 0.5f * cell, 0.07f, (w - 1) * 0.5f * cell);
                    marker.localRotation = Quaternion.identity;
                    marker.localScale = new Vector3(h * cell * 0.9f, 1f, w * cell * 0.9f);
                    var r = marker.GetComponentInChildren<Renderer>();
                    if (r != null) r.material.color = m.CanUpgrade ? UpgradeableColor : MaxedColor;
                }
            }
            for (int i = used; i < _upgradeMarkers.Count; i++)
                if (_upgradeMarkers[i] != null) _upgradeMarkers[i].gameObject.SetActive(false);
        }

        /// <summary>강화 선택 중 표시할 블록: 강화 단계가 있는(강화 프로필이 있는) 블록. 블록별 카드면 그 블록만.</summary>
        private bool ShownForUpgrade(ModuleInstance m)
            => m != null && !m.IsDestroyed &&
               (_reward == null ? ModuleUpgrades.MaxLevel(m.Definition) > 0 : m.Definition == _reward);

        /// <summary>설치된 블록 위의 강화 단계 표시(강화 I = "I", 강화 II = "II"). 기본은 표시 없음.</summary>
        private void UpdateBadges()
        {
            int used = 0;
            if (moduleRoot != null && shipCamera != null)
            {
                float cell = grid.CellSize;
                foreach (var m in grid.Modules)
                {
                    if (m == null || m.UpgradeLevel <= 0) continue;
                    if (used >= _badges.Count)
                    {
                        var go = new GameObject("UpgradeBadge");
                        go.transform.SetParent(moduleRoot, false);
                        go.layer = ModuleFactory.LayerShip;
                        var t = go.AddComponent<TextMeshPro>();
                        if (titleText != null) t.font = titleText.font;
                        t.fontSize = 13f;
                        t.fontStyle = FontStyles.Bold;
                        t.alignment = TextAlignmentOptions.Center;
                        t.rectTransform.sizeDelta = new Vector2(6f, 3f);
                        t.outlineWidth = 0.25f;
                        t.outlineColor = new Color32(0, 0, 0, 255);
                        _badges.Add(t);
                    }
                    var badge = _badges[used++];
                    badge.gameObject.SetActive(true);
                    badge.text = $"<mark=#081216D9> {ModuleUpgrades.Roman(m.UpgradeLevel)} </mark>";   // 어두운 바탕으로 갑판 위에서도 읽히게
                    badge.color = m.CanUpgrade ? new Color(0.55f, 1f, 0.6f) : new Color(1f, 0.82f, 0.3f);
                    int w = m.Definition.Width, h = m.Definition.Height;
                    if ((m.RotationSteps & 1) == 1) (w, h) = (h, w);
                    badge.transform.localPosition = grid.CoordToLocal(m.Origin) + new Vector3((h - 1) * 0.5f * cell, 3.2f, (w - 1) * 0.5f * cell);
                    badge.transform.rotation = shipCamera.transform.rotation;
                }
            }
            for (int i = used; i < _badges.Count; i++)
                if (_badges[i] != null) _badges[i].gameObject.SetActive(false);
        }

        private void UpdateSynergyHighlights()
        {
            int used = 0;
            if (_held != null && grid != null && grid.CanPlace(_held, _cursor, _heldRotation, out _) &&
                cellHighlightPrefab != null && moduleRoot != null &&
                !string.IsNullOrEmpty(ModuleSynergy.Preview(grid, _held, _cursor, _heldRotation)))
            {
                ShipGrid.GetFootprint(_held, _cursor, _heldRotation, _previewCells);
                foreach (var module in grid.Modules)
                {
                    if (module == null || !module.IsOperational ||
                        !RelevantSynergyNeighbour(_held.Type, module.Definition.Type) ||
                        !ModuleSynergy.Touches(_previewCells, module.OccupiedCoords)) continue;
                    if (used >= _synergyHighlights.Count)
                    {
                        var go = Instantiate(cellHighlightPrefab, moduleRoot);
                        go.name = "SynergyNeighbour";
                        SetLayerRecursive(go, ModuleFactory.LayerShip);
                        var renderer = go.GetComponentInChildren<Renderer>();
                        if (renderer != null) renderer.material.color = new Color(0.25f, 0.9f, 1f, 0.35f);
                        _synergyHighlights.Add(go.transform);
                    }
                    var marker = _synergyHighlights[used++];
                    marker.gameObject.SetActive(true);
                    marker.localPosition = grid.CoordToLocal(module.Origin) + Vector3.up * 0.07f;
                    marker.localRotation = Quaternion.identity;
                    marker.localScale = new Vector3(grid.CellSize * 0.94f, 1f, grid.CellSize * 0.94f);
                }
            }
            for (int i = used; i < _synergyHighlights.Count; i++)
                _synergyHighlights[i].gameObject.SetActive(false);
        }

        private static bool RelevantSynergyNeighbour(ModuleType held, ModuleType other)
        {
            if (held == ModuleType.RepairBay || other == ModuleType.RepairBay) return true;
            return held switch
            {
                ModuleType.HelicopterDeck => other == ModuleType.HelicopterDeck || other == ModuleType.Sonar,
                ModuleType.Sonar => other == ModuleType.HelicopterDeck || other == ModuleType.AswLauncher,
                ModuleType.AswLauncher => other == ModuleType.Sonar,
                ModuleType.Radar => other == ModuleType.SamLauncher || other == ModuleType.Ciws,
                ModuleType.SamLauncher or ModuleType.Ciws => other == ModuleType.Radar,
                ModuleType.EwSuite => other == ModuleType.DecoyLauncher,
                ModuleType.DecoyLauncher => other == ModuleType.EwSuite,
                ModuleType.NavalGun => other == ModuleType.Magazine,
                ModuleType.Magazine => other == ModuleType.NavalGun,
                _ => false,
            };
        }

        // ------------------------------------------------------------ 사격각 표시

        /// <summary>
        /// 설치된 무기와 들고 있는 무기의 사격각을 갑판 위 부채꼴로 그린다.
        /// 들고 있는 무기와 커서 아래 무기는 사격 금지 구역도 붉게 그린다.
        /// </summary>
        private void UpdateArcViews()
        {
            int used = 0;
            var hovered = _held == null ? grid.Get(_cursor) : null;

            foreach (var m in grid.Modules)
            {
                if (m?.Definition == null || !Game.Combat.FireArcCalculator.HasCutout(m.Definition.Type)) continue;
                var arc = Game.Combat.FireArcCalculator.ComputeFor(m.Definition.Type, grid, m.Origin, m.RotationSteps, m.EffectiveHeight);
                DrawArc(ref used, m.Origin, m.RotationSteps, arc, InstalledArcColor, m == hovered);
            }

            if (_held != null && Game.Combat.FireArcCalculator.HasCutout(_held.Type))
            {
                var arc = Game.Combat.FireArcCalculator.ComputeFor(_held.Type, grid, _cursor, _heldRotation, _held.HeightClass);
                DrawArc(ref used, _cursor, _heldRotation, arc, HeldArcColor, true);
            }

            for (int i = used; i < _arcViews.Count; i++)
                if (_arcViews[i] != null) _arcViews[i].gameObject.SetActive(false);
        }

        private void DrawArc(ref int used, GridCoord origin, int rotationSteps, Game.Combat.FireArc arc, Color color, bool showCutout)
        {
            arc.GetRuns(_arcRuns, cutOut: false);
            DrawRuns(used++, origin, rotationSteps, color, arcInnerRadiusCells, arcOuterRadiusCells);

            if (!showCutout) return;
            arc.GetRuns(_arcRuns, cutOut: true);
            if (_arcRuns.Count > 0)
                DrawRuns(used++, origin, rotationSteps, CutoutArcColor, arcInnerRadiusCells, arcOuterRadiusCells * 0.8f);
        }

        private void DrawRuns(int index, GridCoord origin, int rotationSteps, Color color, float innerCells, float outerCells)
        {
            var view = GetArcView(index);
            if (view == null) return;

            float cell = grid.CellSize;
            view.gameObject.SetActive(true);
            view.transform.localPosition = grid.CoordToLocal(origin) + Vector3.up * (color == CutoutArcColor ? 0.09f : 0.08f);
            view.transform.localRotation = Quaternion.identity;

            BuildArcMesh(view.sharedMesh, Game.Combat.FireArcCalculator.FacingYaw(rotationSteps), _arcRuns,
                         innerCells * cell, outerCells * cell);

            _arcBlock ??= new MaterialPropertyBlock();
            _arcBlock.SetColor("_BaseColor", color);
            _arcBlock.SetColor("_Color", color);
            view.GetComponent<MeshRenderer>().SetPropertyBlock(_arcBlock);
        }

        private MeshFilter GetArcView(int index)
        {
            if (moduleRoot == null || arcMaterial == null) return null;

            while (_arcViews.Count <= index)
            {
                var go = new GameObject($"FireArc_{_arcViews.Count}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(moduleRoot, false);
                go.layer = ModuleFactory.LayerShip;

                var mf = go.GetComponent<MeshFilter>();
                mf.sharedMesh = new Mesh { name = "FireArc" };
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = arcMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _arcViews.Add(mf);
            }
            return _arcViews[index];
        }

        private readonly List<(float start, float width)> _arcRuns = new();

        /// <summary>열린 조각마다 고리 모양 부채꼴을 그려 한 메시로 합친다. 0도 = 선수(+Z 로컬), 조각 각도는 facingYaw 기준.</summary>
        private static void BuildArcMesh(Mesh mesh, float facingYaw, List<(float start, float width)> runs, float inner, float outer)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            foreach (var (runStart, width) in runs)
            {
                int segments = Mathf.Max(2, Mathf.CeilToInt(width / 5f));
                int baseIndex = verts.Count;
                float start = facingYaw + runStart;

                for (int i = 0; i <= segments; i++)
                {
                    float yaw = (start + width * i / segments) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    verts.Add(dir * inner);
                    verts.Add(dir * outer);
                }

                for (int i = 0; i < segments; i++)
                {
                    int v = baseIndex + i * 2;
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 3);
                    tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
                }
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        private void ClearArcViews()
        {
            foreach (var view in _arcViews)
            {
                if (view == null) continue;
                if (view.sharedMesh != null) Destroy(view.sharedMesh);
                Destroy(view.gameObject);
            }
            _arcViews.Clear();
        }

        private static string RewardLabel(ModuleDefinition def, bool used)
        {
            if (def == null) return "이번 보상: —";
            if (used) return $"<color=#5a6570>이번 보상: {def.DisplayName} (배치 완료)</color>";

            return $"이번 보상 [1] <b>{def.DisplayName}</b>  {def.Width}x{def.Height}";
        }

        private void AppendRaiseHint(ModuleInstance m)
        {
            if (!Game.Combat.FireArcCalculator.UsesNeighborArc(m.Definition.Type)) return;

            if (m.IsRaised)
            {
                _sb.AppendLine("<color=#7fb8e0>거치대에 올림</color> — 중간 블록 너머로 사격, 내구 −25%  (E로 내리기)");
                return;
            }

            if (Game.Combat.FireArcCalculator.RaisesAnywhere(m.Definition.Type))
            {
                _sb.AppendLine("<color=#e0c05a>E: 거치대 올리기</color> — 중간 블록 너머로 사격 (내구 −25%)");
                return;
            }

            int blocked = Game.Combat.FireArcCalculator.CountBlockingNeighbors(grid, m);
            int need = Game.Combat.FireArcCalculator.RaiseRequiredBlockedSides;
            _sb.AppendLine(blocked >= need
                ? "<color=#e0c05a>E: 거치대 올리기</color> — 막힌 면이 많아 높여 쏠 수 있다 (내구 −25%)"
                : $"<color=#8fa4b8>거치대: 막힌 면 {blocked}/{need} — {need}면 이상 막히면 올릴 수 있다</color>");
        }

        private string BuildInfo()
        {
            _sb.Clear();

            if (_upgradePicking)
            {
                _sb.AppendLine(_reward != null
                    ? $"<b>강화할 {_reward.DisplayName}을(를) 고르세요</b> — 클릭 또는 방향키+Enter"
                    : "<b>강화할 장비를 고르세요</b> — 클릭 또는 방향키+Enter");
                _sb.AppendLine("<color=#7fd08a>초록 = 강화 가능</color>   <color=#e0c05a>금색 = 최대 강화</color>   우클릭·Esc = 카드로 돌아가기");
                var hovered = grid.Get(_cursor);
                if (ShownForUpgrade(hovered))
                {
                    if (hovered.CanUpgrade)
                        _sb.AppendLine($"{hovered.Definition.DisplayName} ({hovered.Origin.X},{hovered.Origin.Z}) {UpgradeStepLabel(hovered.UpgradeLevel)}\n" +
                                       ModuleCardText.UpgradeDiff(hovered.Definition, hovered.UpgradeLevel, hovered.UpgradeLevel + 1, 4));
                    else _sb.AppendLine($"{hovered.Definition.DisplayName} ({hovered.Origin.X},{hovered.Origin.Z}) <color=#e0c05a>이미 최대 강화</color>");
                }
                return _sb.ToString();
            }

            if (_held != null)
            {
                _sb.AppendLine($"들고 있음: <b>{_held.DisplayName}</b>  {_held.Width}x{_held.Height}" +
                               (_heldWasInstalled ? $"   (원래 자리 {_heldOrigin} · 우클릭/Esc로 복귀)" : ""));

                if (!grid.CanPlace(_held, _cursor, _heldRotation, out string reason) && reason != null)
                    _sb.AppendLine($"<color=#e06a5a>{reason}</color>");
                else
                {
                    string synergy = ModuleSynergy.Preview(grid, _held, _cursor, _heldRotation);
                    if (!string.IsNullOrEmpty(synergy)) _sb.AppendLine($"<color=#68dede>연결 시너지: {synergy}</color>");
                    if (!string.IsNullOrEmpty(_originSynergy) && _originSynergy != synergy)
                        _sb.AppendLine($"<color=#e0a16a>이전 자리 연결 해제: {_originSynergy}</color>");
                    // 자리로 형태가 정해지는 블록: 여기 놓으면 무엇이 되는가
                    if (ModuleVariants.HasVariants(_held.Type))
                    {
                        var foot = new List<GridCoord>();
                        ShipGrid.GetFootprint(_held, _cursor, _heldRotation, foot);
                        var v = ModuleVariants.Resolve(_held.Type, grid, foot, out _);
                        if (v != ModuleVariant.None)
                            _sb.AppendLine($"<color=#9fe0a0>형태: <b>{ModuleVariants.Name(v)}</b></color> — {ModuleVariants.Summary(v)}");
                    }
                }

                if (Game.Combat.FireArcCalculator.HasCutout(_held.Type))
                {
                    var arc = Game.Combat.FireArcCalculator.ComputeFor(_held.Type, grid, _cursor, _heldRotation, _held.HeightClass);
                    _sb.AppendLine($"<color=#e0c05a>사격각 {arc.Width:0}°</color>  <color=#e0705a>붉은 곳 = 사격 금지 구역</color>  (R로 포신 방향 전환)");
                }
            }
            else
            {
                var m = grid.Get(_cursor);
                if (m != null)
                {
                    _sb.AppendLine($"{m.Definition.DisplayName}" +
                                   (m.UpgradeLevel > 0 ? $" <color=#7fd08a>강화 {ModuleUpgrades.Roman(m.UpgradeLevel)}</color>" : "") +
                                   $"  HP {m.Hp:0}/{m.MaxHp:0}" +
                                   (m.IsDestroyed ? "  <color=#e06a5a>파괴됨</color>" : ""));
                    _sb.AppendLine(m.Definition.Description);
                    if (m.Variant != ModuleVariant.None)
                        _sb.AppendLine($"<color=#9fe0a0>형태: <b>{ModuleVariants.Name(m.Variant)}</b></color> — {ModuleVariants.Summary(m.Variant)}");
                    if (m.UpgradeLevel > 0)
                        _sb.AppendLine("<color=#7fd08a>적용 중:</color> " + ModuleCardText.UpgradeDiff(m.Definition, 0, m.UpgradeLevel, 4).Replace("\n", " · "));
                    AppendRaiseHint(m);
                }
                else
                {
                    _sb.AppendLine($"{_cursor} — 빈 자리 (기존 모듈과 맞닿아야 설치 가능)");
                }
            }

            string growth = ModuleCardText.BuildGrowthSummary();
            if (!string.IsNullOrEmpty(growth)) _sb.AppendLine($"<color=#ffd060>성장</color> {growth}");
            _sb.AppendLine();
            _sb.AppendLine("마우스: 클릭 집기·놓기 · 우클릭 되돌리기 · 휠 회전");
            _sb.Append("키보드: 방향키 이동 · Enter 집기·놓기 · R 회전 · E 거치대 · X 철거 · Space 전투 재개");

            if (_held != null) _sb.Append("   <color=#e0c05a>(내려놓아야 재개할 수 있습니다)</color>");

            return _sb.ToString();
        }
    }
}
