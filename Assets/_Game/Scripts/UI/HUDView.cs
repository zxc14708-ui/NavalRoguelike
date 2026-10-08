using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Core;
using Game.Modules.Runtime;
using Game.TaskForce;

namespace Game.UI
{
    /// <summary>
    /// 전투 중 상시 표시되는 정보.
    ///   위 가운데: 스테이지 진행(구간·경과·격침)
    ///   아래 가운데 한 덩어리(위에서 아래로):
    ///     레벨·경험치 막대
    ///     스킬 줄(그림 아이콘: 준비·재장전·장비 없음)
    ///     [레이더] [기어·체력(함 현황)] [무장 장탄] — 같은 높이로 나란히
    /// </summary>
    public class HUDView : MonoBehaviour
    {
        [Header("Hull / Speed")]
        [SerializeField] private Slider hullBar;
        [SerializeField] private TMP_Text hullText;
        [SerializeField] private TMP_Text speedText;

        [Header("Stage")]
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private Slider roundBar;
        [SerializeField] private TMP_Text killsText;

        [Header("Level")]
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Slider xpBar;

        [Header("Decoy Skill")]
        [SerializeField] private Button decoyButton;
        [SerializeField] private TMP_Text decoyText;
        [SerializeField] private Slider decoyBar;

        [Header("Jam Skill")]
        [SerializeField] private Button jamButton;
        [SerializeField] private TMP_Text jamText;
        [SerializeField] private Slider jamBar;

        [Header("Repair Skill")]
        [SerializeField] private Button repairButton;
        [SerializeField] private TMP_Text repairText;
        [SerializeField] private Slider repairBar;

        [Header("Smoke Skill")]
        [SerializeField] private Button smokeButton;
        [SerializeField] private TMP_Text smokeText;
        [SerializeField] private Slider smokeBar;

        [Header("Flank Skill")]
        [SerializeField] private Button flankButton;
        [SerializeField] private TMP_Text flankText;
        [SerializeField] private Slider flankBar;

        [Header("Banner")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private TMP_Text bannerText;

        private int _stage = 1, _phase, _phaseCount;
        private string _phaseTitle = "";
        private int _reconCount;

        private void OnEnable()
        {
            CombatPolicies.PolicyChanged += RefreshPolicyLabels;
            GameEvents.HullHpChanged += OnHull;
            GameEvents.SpeedChanged += OnSpeed;
            GameEvents.PhaseStarted += OnPhaseStarted;
            GameEvents.StageProgressChanged += OnStageProgress;
            GameEvents.ExperienceChanged += OnExperience;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.EnemyKilled += OnKill;
            GameEvents.DecoyStatusChanged += OnDecoyStatus;
            GameEvents.JamStatusChanged += OnJamStatus;
            GameEvents.SkillStatusChanged += OnSkillStatus;
            GameEvents.ReconChanged += OnReconChanged;
            GameEvents.BossPhaseChanged += ShowBanner;
        }

        private void OnDisable()
        {
            PlayerPrefs.Save();
            CombatPolicies.PolicyChanged -= RefreshPolicyLabels;
            GameEvents.HullHpChanged -= OnHull;
            GameEvents.SpeedChanged -= OnSpeed;
            GameEvents.PhaseStarted -= OnPhaseStarted;
            GameEvents.StageProgressChanged -= OnStageProgress;
            GameEvents.ExperienceChanged -= OnExperience;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.EnemyKilled -= OnKill;
            GameEvents.DecoyStatusChanged -= OnDecoyStatus;
            GameEvents.JamStatusChanged -= OnJamStatus;
            GameEvents.SkillStatusChanged -= OnSkillStatus;
            GameEvents.ReconChanged -= OnReconChanged;
            GameEvents.BossPhaseChanged -= ShowBanner;
        }

        private void Awake()
        {
            BuildNavalHud();
            if (bannerRoot != null) bannerRoot.SetActive(false);

            // HUD는 함선을 모른다. 버튼은 요청만 보내고 DecoySkill이 처리한다.
            if (decoyButton != null) decoyButton.onClick.AddListener(GameEvents.RaiseDecoyRequested);
            if (jamButton != null) jamButton.onClick.AddListener(GameEvents.RaiseJamRequested);
            if (repairButton != null) repairButton.onClick.AddListener(() => GameEvents.RaiseSkillRequested(ActiveSkillId.Repair));
            if (smokeButton != null) smokeButton.onClick.AddListener(() => GameEvents.RaiseSkillRequested(ActiveSkillId.Smoke));
            if (flankButton != null) flankButton.onClick.AddListener(() => GameEvents.RaiseSkillRequested(ActiveSkillId.Flank));
        }

        // 스킬 줄 칸 순서
        private const int DecoySlot = 0, JamSlot = 1, RepairSlot = 2, SmokeSlot = 3, FlankSlot = 4;

        private void OnSkillStatus(ActiveSkillId id, int ready, int total, float next01)
        {
            switch (id)
            {
                case ActiveSkillId.Repair: ShowSkill(RepairSlot, ready, total, next01); break;
                case ActiveSkillId.Smoke: ShowSkill(SmokeSlot, ready, total, next01); break;
                case ActiveSkillId.Flank: ShowSkill(FlankSlot, ready, total, next01); break;
            }
        }

        /// <summary>정찰기가 새로 나타나면 한 번 경고한다.</summary>
        private void OnReconChanged(int count)
        {
            if (count > _reconCount && _reconCount == 0) ShowBanner("적 정찰기 표적 지시 — 적 공격 가속");
            _reconCount = count;
        }

        private void OnDecoyStatus(int ready, int total, float next01) => ShowSkill(DecoySlot, ready, total, next01);

        private void OnJamStatus(int ready, int total, float next01) => ShowSkill(JamSlot, ready, total, next01);

        /// <summary>액티브 스킬 칸 표시: 장비 없음 / 준비 수 / 재장전 진행(그림으로).</summary>
        private void ShowSkill(int slot, int ready, int total, float next01)
        {
            if (_skillBar != null) _skillBar.Set(slot, ready, total, next01);
        }

        private void OnHull(float cur, float max)
        {
            if (hullBar != null) hullBar.value = max > 0f ? cur / max : 0f;
            if (hullText != null) hullText.text = $"HULL {Mathf.CeilToInt(cur)} / {Mathf.CeilToInt(max)}";
        }

        private void OnSpeed(float speed)
        {
            if (speedText != null) speedText.text = $"속력  {Game.Ship.ShipController.ToKnots(speed):0.0} kn";
        }

        private TMP_FontAsset _navalFont;
        private SkillBarUI _skillBar;
        private TMP_Text _xpText;
        private CanvasGroup _combatGroup;
        private GameObject _menu;
        private CodexUI _codex;
        private GameObject _settingsOverlay;
        private RectTransform _settingsPage;
        private TMP_Text _settingsMessage;
        private Image _controlsTabImage, _audioTabImage;
        private NavalControl? _pendingBinding;
        private int _settingsTab;
        private GameObject _routeChoice;
        private System.Action<bool> _onRouteChosen;
        private GameObject _doctrineChoice;
        private System.Action<NavalDoctrine> _onDoctrineChosen;
        private TMP_Text _doctrineLabel;
        private readonly List<HelicopterDeckModule> _helicopterDecks = new();
        private int _selectedHelicopterDeck;
        private TMP_Text _helicopterHeader, _helicopterStatus;
        private readonly Image[] _helicopterModes = new Image[3];
        private float _statusTimer;
        private static readonly Color Navy = new Color(0.025f, 0.065f, 0.10f, 0.65f);
        private static readonly Color Gold = new Color(0.98f, 0.73f, 0.28f);
        private static readonly Color Cyan = new Color(0.30f, 0.88f, 0.85f);
        private static readonly Color ConsoleBackground = new Color(0.015f, 0.09f, 0.065f, 0.93f);
        private static readonly Color ConsoleIdle = new Color(0.035f, 0.19f, 0.12f, 1f);
        private static readonly Color ConsoleActive = new Color(0.12f, 0.52f, 0.29f, 1f);
        private static readonly Color ConsoleText = new Color(0.66f, 1f, 0.75f);

        // Rebuild only the HUD's own visuals; refit, result and warning panels stay connected.
        private void BuildNavalHud()
        {
            _navalFont = hullText != null ? hullText.font : TMP_Settings.defaultFontAsset;
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0f;
            }
            var background = GetComponent<Image>();
            if (background != null) background.raycastTarget = false;
            _combatGroup = gameObject.GetComponent<CanvasGroup>();
            if (_combatGroup == null) _combatGroup = gameObject.AddComponent<CanvasGroup>();

            // --- 화면 아래 가운데 한 덩어리: [레이더][기어·체력][무장] 위에 스킬 줄, 그 위에 레벨 막대.
            //     아래 가운데에 붙은 틀(Bottom HUD) 안에 왼쪽 아래 기준으로 쌓는다 — 화면 비율이 달라도 가운데를 지킨다.
            const float left = 0f, bottom = 0f, gap = 8f, screenMargin = 14f;
            const float radarWidth = 175f;
            const float skillHeight = 56f, levelHeight = 22f;
            float rowHeight = ShipStatusPanelUI.Height;
            float clusterWidth = 440f;
            float clusterHeight = skillHeight + 6f + levelHeight;

            var cluster = new GameObject("Bottom HUD", typeof(RectTransform)).GetComponent<RectTransform>();
            cluster.SetParent(transform, false);
            cluster.anchorMin = cluster.anchorMax = new Vector2(0.5f, 0f);
            cluster.pivot = new Vector2(0.5f, 0f);
            cluster.anchoredPosition = new Vector2(0f, screenMargin);
            cluster.sizeDelta = new Vector2(clusterWidth, clusterHeight);

            var radar = RadarScopeUI.Create(cluster, _navalFont, Navy, Cyan, new Vector2(left, bottom), radarWidth);

            // 함 현황: 월드 오브 워쉽 조함 패널 참고(선체·타각·함선 도식·기관 전령기). 수치 갱신은 패널이 직접 한다.
            hullText = null;
            hullBar = null;
            speedText = null;
            var ship = ShipStatusPanelUI.Create(cluster, _navalFont, Navy, Cyan, Gold, new Vector2(left + radarWidth + gap, bottom));
            var weapons = WeaponStatusPanelUI.Create(cluster, _navalFont, Navy, Cyan, Gold,
                                                     new Vector2(left + radarWidth + gap + ShipStatusPanelUI.Width + gap, bottom), WeaponStatusPanelUI.DefaultHeight);

            // Keep the centre clear: radar + helm on the left, ammunition on the right.
            DockPanel((RectTransform)radar.transform, Vector2.zero, new Vector2(14, 14));
            DockPanel((RectTransform)ship.transform, Vector2.zero, new Vector2(197, 14));
            ship.transform.localScale = Vector3.one * 0.72f;
            DockPanel((RectTransform)weapons.transform, new Vector2(1, 0),
                      new Vector2(-14 - WeaponStatusPanelUI.Width * 0.82f, 14));
            weapons.transform.localScale = Vector3.one * 0.82f;

            float skillY = 0f;
            decoyText = jamText = repairText = smokeText = flankText = null;
            decoyBar = jamBar = repairBar = smokeBar = flankBar = null;
            _skillBar = SkillBarUI.Create(cluster, _navalFont, Navy, Cyan, Gold, new Vector2(left, skillY), new Vector2(clusterWidth, skillHeight), new[]
            {
                (HudTextures.SkillIcon.Decoy, GameSettings.BindingLabel(NavalControl.Decoy)),
                (HudTextures.SkillIcon.Jam, GameSettings.BindingLabel(NavalControl.Jam)),
                (HudTextures.SkillIcon.Repair, GameSettings.BindingLabel(NavalControl.Repair)),
                (HudTextures.SkillIcon.Smoke, GameSettings.BindingLabel(NavalControl.Smoke)),
                (HudTextures.SkillIcon.Flank, GameSettings.BindingLabel(NavalControl.Flank)),
            });
            decoyButton = _skillBar.ButtonAt(DecoySlot);
            jamButton = _skillBar.ButtonAt(JamSlot);
            repairButton = _skillBar.ButtonAt(RepairSlot);
            smokeButton = _skillBar.ButtonAt(SmokeSlot);
            flankButton = _skillBar.ButtonAt(FlankSlot);

            var level = Panel(cluster, "Level bar", Vector2.zero, new Vector2(left, skillY + skillHeight + 6f), new Vector2(clusterWidth, levelHeight), Navy);
            levelText = Label(level, "Level", "Lv 1", new Vector2(12, 3), new Vector2(70, 20), 15, Gold);
            levelText.fontStyle = FontStyles.Bold;
            xpBar = Bar(level, "Experience", new Vector2(72, 11), new Vector2(clusterWidth - 72 - 96, 4), Gold);
            _xpText = Label(level, "XP", "0 / 0 XP", new Vector2(clusterWidth - 90, 4), new Vector2(80, 18), 12, new Color(0.62f, 0.72f, 0.76f));
            _xpText.alignment = TextAlignmentOptions.Right;

            var progress = Panel(transform, "Operation", new Vector2(0.5f, 1), new Vector2(-230, -62), new Vector2(460, 48), Navy);
            roundText = Label(progress, "Stage", "작전 대기", new Vector2(12, 22), new Vector2(436, 23), 17, Color.white);
            roundBar = Bar(progress, "Progress", new Vector2(12, 18), new Vector2(436, 2), Cyan);
            killsText = Label(progress, "Kills", "격침 0", new Vector2(12, 1), new Vector2(436, 17), 13, Gold);

            bannerRoot = Panel(transform, "Announcement", new Vector2(0.5f, 1), new Vector2(-230, -104), new Vector2(460, 34), Navy).gameObject;
            bannerText = Label(bannerRoot.transform, "Message", "", new Vector2(12, 3), new Vector2(436, 28), 19, Gold);
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 13;
            bannerText.fontSizeMax = 19;

            EnsureThreatIndicators(progress, level, (RectTransform)_skillBar.transform, (RectTransform)ship.transform,
                                   (RectTransform)radar.transform, (RectTransform)weapons.transform);
            _vlsPanel = (RectTransform)VlsModePanelUI.Create(transform, _navalFont).transform;

            BuildHelicopterConsole();
            var tactics = Panel(transform, "Doctrine console", Vector2.one, new Vector2(-284f, -352f),
                new Vector2(270f, 28f), ConsoleBackground);
            _doctrinePanel = tactics;
            _doctrineLabel = Label(tactics, "Doctrine", "교리 미선택", new Vector2(9, 4), new Vector2(252, 21), 13, ConsoleText);
            RefreshPolicyLabels();
            LayoutRightColumn();
        }

        private RectTransform _vlsPanel, _helicopterPanel, _doctrinePanel;

        /// <summary>
        /// 오른쪽 열: 지금 쓸 수 있는 콘솔만 위에서부터 차례로 쌓는다(VLS → 헬기 데크 → 교리 → 전투단 지원).
        /// 설치하지 않은 장비의 빈 콘솔("설치된 발사기 없음")이나 고르지 않은 교리는 숨긴다.
        /// </summary>
        private void LayoutRightColumn()
        {
            var grid = GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.Grid : null;
            bool hasVls = false;
            if (grid != null)
                foreach (var m in grid.Modules)
                    if (m != null && !m.IsDestroyed && m.Runtime is Game.Modules.Runtime.VlsModule) { hasVls = true; break; }

            float y = 76f;   // 위쪽 지원 알림(토스트) 아래부터
            void Stack(RectTransform panel, bool visible, bool topRightPivot)
            {
                if (panel == null) return;
                if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
                if (!visible) return;
                float h = panel.sizeDelta.y;
                panel.anchoredPosition = topRightPivot ? new Vector2(-14f, -y) : new Vector2(-14f - panel.sizeDelta.x, -y - h);
                y += h + 8f;
            }
            Stack(_vlsPanel, hasVls, true);
            Stack(_helicopterPanel, _helicopterDecks.Count > 0, false);
            Stack(_doctrinePanel, CombatPolicies.Doctrine != NavalDoctrine.None, false);
            var taskForce = TaskForcePanelUI.VisiblePanel;
            if (taskForce != null) taskForce.anchoredPosition = new Vector2(-14f, -y);
        }

        private void RefreshPolicyLabels()
        {
            if (_doctrineLabel != null) _doctrineLabel.text = CombatPolicies.DoctrineLabel;
        }

        private void BuildHelicopterConsole()
        {
            var panel = Panel(transform, "Helicopter fire-control console", Vector2.one,
                new Vector2(-284f, -318f), new Vector2(270f, 120f), ConsoleBackground);
            _helicopterPanel = panel;
            _helicopterHeader = Label(panel, "Deck", "헬기 데크 · 대기", new Vector2(10, 88),
                new Vector2(188, 26), 18, ConsoleText);
            var previous = ConsoleButton(panel, "Previous deck", "◀", new Vector2(207, 89), new Vector2(25, 25), 14);
            previous.onClick.AddListener(() => CycleHelicopterDeck(-1));
            var next = ConsoleButton(panel, "Next deck", "▶", new Vector2(237, 89), new Vector2(25, 25), 14);
            next.onClick.AddListener(() => CycleHelicopterDeck(1));
            string[] labels = { "자율", "대잠", "대함" };
            for (int i = 0; i < labels.Length; i++)
            {
                var policy = (HelicopterPolicy)i;
                var button = ConsoleButton(panel, labels[i], labels[i], new Vector2(8 + 86 * i, 37), new Vector2(82, 42), 17);
                _helicopterModes[i] = button.GetComponent<Image>();
                button.onClick.AddListener(() => SetSelectedHelicopterPolicy(policy));
            }
            _helicopterStatus = Label(panel, "Deck status", "데크를 설치하면 정책을 선택할 수 있습니다.",
                new Vector2(9, 8), new Vector2(252, 21), 11, ConsoleText);
            RefreshHelicopterConsole();
        }

        private Button ConsoleButton(Transform parent, string name, string title, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Panel(parent, name, Vector2.zero, position, size, ConsoleIdle);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var label = Label(rect, "Label", title, Vector2.zero, size, fontSize, ConsoleText);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private HelicopterDeckModule SelectedHelicopterDeck => _helicopterDecks.Count == 0 ? null :
            _helicopterDecks[Mathf.Clamp(_selectedHelicopterDeck, 0, _helicopterDecks.Count - 1)];

        private void CycleHelicopterDeck(int direction)
        {
            if (_helicopterDecks.Count == 0) return;
            _selectedHelicopterDeck = (_selectedHelicopterDeck + direction + _helicopterDecks.Count) % _helicopterDecks.Count;
            RefreshHelicopterConsole();
        }

        private void SetSelectedHelicopterPolicy(HelicopterPolicy policy)
        {
            SelectedHelicopterDeck?.SetPolicy(policy);
            RefreshHelicopterConsole();
        }

        private void RefreshHelicopterConsole()
        {
            _helicopterDecks.Clear();
            var grid = GameManager.Instance?.Player?.Grid;
            if (grid != null)
                foreach (var module in grid.Modules)
                    if (module != null && !module.IsDestroyed && module.Runtime is HelicopterDeckModule deck)
                        _helicopterDecks.Add(deck);
            _selectedHelicopterDeck = _helicopterDecks.Count == 0 ? 0 :
                Mathf.Clamp(_selectedHelicopterDeck, 0, _helicopterDecks.Count - 1);
            var current = SelectedHelicopterDeck;
            if (_helicopterHeader != null) _helicopterHeader.text = current == null ? "헬기 데크 · 대기" :
                $"헬기 데크 ({_selectedHelicopterDeck + 1}/{_helicopterDecks.Count})";
            if (_helicopterStatus != null) _helicopterStatus.text = current == null ? "설치된 데크 없음" :
                !current.CanOperate ? "데크 손상 · 출격 불가" : current.SortieActive
                    ? $"출격 중 · [{GameSettings.BindingLabel(NavalControl.HelicopterSelector)}] 정책 전환" :
                current.CooldownRemaining > 0f
                    ? $"재정비 {current.CooldownRemaining:0.0}초 · [{GameSettings.BindingLabel(NavalControl.HelicopterSelector)}] 정책 전환"
                    : $"출격 대기 · [{GameSettings.BindingLabel(NavalControl.HelicopterSelector)}] 정책 전환";
            for (int i = 0; i < _helicopterModes.Length; i++)
                if (_helicopterModes[i] != null)
                    _helicopterModes[i].color = current != null && (int)current.Policy == i ? ConsoleActive : ConsoleIdle;
        }

        private void DockPanel(RectTransform panel, Vector2 anchor, Vector2 position)
        {
            panel.SetParent(transform, false);
            panel.anchorMin = panel.anchorMax = anchor;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = position;
        }

        /// <summary>
        /// 화면 밖 위협 표시(MissileWarningUI)가 씬에 없으면 HUD 아래에 만든다.
        /// 셋업 빌더를 다시 돌리지 않아도 동작하게 하기 위함이다.
        /// </summary>
        private void EnsureThreatIndicators(params RectTransform[] avoid)
        {
            if (FindFirstObjectByType<MissileWarningUI>() != null) return;

            var holder = new GameObject("Threat indicators", typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            holder.AddComponent<MissileWarningUI>().Setup(rect, Camera.main, _navalFont, avoid);
        }

        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rect;
        }

        private TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = _navalFont;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.zero;
            text.rectTransform.pivot = Vector2.zero;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            return text;
        }

        private Slider Bar(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = Panel(parent, name, Vector2.zero, position, size, new Color(0.15f, 0.22f, 0.26f));
            var fill = Panel(rect, "Fill", Vector2.zero, Vector2.zero, size, color);
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.value = 0;
            return slider;
        }

        public void ShowStartScreen(System.Action onLaunch)
        {
            if(Resources.Load<GameObject>("Environment/NavalBaseMenu")!=null)
            {
                ShowHarborStartScreen(onLaunch);
                return;
            }
            _combatGroup.alpha = 0;
            _combatGroup.blocksRaycasts = false;
            var canvas = GetComponentInParent<Canvas>();
            var menu = Panel(canvas.transform, "Fleet command / main menu", Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.008f, 0.035f, 0.04f, 0.98f));
            menu.anchorMax = Vector2.one;
            menu.offsetMin = menu.offsetMax = Vector2.zero;
            menu.GetComponent<Image>().raycastTarget = true;
            _menu = menu.gameObject;
            var content = Panel(menu, "Command briefing", new Vector2(0.5f, 0.5f),
                new Vector2(-520, -310), new Vector2(1040, 620), Color.clear);
            Panel(content, "Top signal", Vector2.zero, new Vector2(0, 590), new Vector2(1040, 2), ConsoleActive);
            Label(content, "Command", "FLEET COMMAND   /   작전 통제실", new Vector2(0, 548), new Vector2(680, 32), 19, ConsoleText);
            Label(content, "Readiness", "SYSTEM READY    •    SEA SECTOR 01", new Vector2(670, 550),
                new Vector2(370, 27), 14, ConsoleText).alignment = TextAlignmentOptions.Right;
            Label(content, "Title", "NAVAL ROGUELIKE", new Vector2(0, 420), new Vector2(580, 80), 54, Color.white);
            Label(content, "Subtitle", "나만의 군함을 조립하고 해역을 돌파하세요.", new Vector2(2, 371),
                new Vector2(570, 42), 24, Gold);
            Label(content, "Brief line", "탐지  →  교전  →  손상 통제  →  함대 확장", new Vector2(2, 320),
                new Vector2(555, 32), 19, ConsoleText);

            var briefing = Panel(content, "Mission board", Vector2.zero, new Vector2(610, 164),
                new Vector2(430, 346), ConsoleBackground);
            Panel(briefing, "Board accent", Vector2.zero, new Vector2(0, 342), new Vector2(430, 4), ConsoleActive);
            Label(briefing, "Header", "작전 개요  /  MISSION BRIEF", new Vector2(22, 295), new Vector2(390, 36), 22, ConsoleText);
            Label(briefing, "Sector", "해역     제1 전투 해역", new Vector2(22, 245), new Vector2(390, 31), 19, Color.white);
            Label(briefing, "Vessel", "함정     모듈식 전투함", new Vector2(22, 204), new Vector2(390, 31), 19, Color.white);
            Label(briefing, "Objective", "목표     생존 · 적 함대 격침", new Vector2(22, 163), new Vector2(390, 31), 19, Color.white);
            Panel(briefing, "Divider", Vector2.zero, new Vector2(22, 141), new Vector2(386, 1), ConsoleActive);
            Label(briefing, "Hint", "소나 자동 핑 · VLS 탄종 및 헬기 정책은\n전투 화면 오른쪽 콘솔에서 조정", new Vector2(22, 65),
                new Vector2(390, 64), 17, ConsoleText);

            var launch = MenuButton(content, "출항  /  작전 시작", new Vector2(0, 146),
                new Vector2(540, 70), ConsoleActive, 26);

            void CompleteLaunch()
            {
                _menu.SetActive(false);
                _combatGroup.alpha = 1;
                _combatGroup.blocksRaycasts = true;
                onLaunch?.Invoke();
            }

            // 출항 편성(전투단 지원) 화면은 없앴다 — 편대는 전투 중 정비 카드(편대 배치·편대 강화)로 꾸린다
            launch.onClick.AddListener(() =>
            {
                launch.interactable = false;
                var initializer = FindFirstObjectByType<Game.Ship.ShipInitializer>();
                if (!StartingShipSelectorUI.Show(_menu.transform, _navalFont, initializer,
                    CompleteLaunch, () => launch.interactable = true)) CompleteLaunch();
            });
            MenuButton(content, "설정  /  조작 · 음향", new Vector2(0, 70), new Vector2(540, 58),
                ConsoleIdle, 22).onClick.AddListener(ShowSettings);
            // 사전: 무장 · 호위함 · 적 정보(3D 미리보기, 지금 게임 데이터 기준)
            MenuButton(content, "사전  /  무장 · 호위함 · 적", new Vector2(610, 70), new Vector2(430, 58),
                ConsoleIdle, 22).onClick.AddListener(ShowCodex);
            Label(content, "Footer", "기관 전령기 · 조타 · 함 현황     |     설정에서 전투 조작키 변경 가능",
                new Vector2(0, 17), new Vector2(1030, 28), 16, new Color(0.55f, 0.73f, 0.65f));
        }

        private void ShowHarborStartScreen(System.Action onLaunch)
        {
            _combatGroup.alpha=0; _combatGroup.blocksRaycasts=false;
            var canvas=GetComponentInParent<Canvas>();
            var menu=Panel(canvas.transform,"Fleet command / main menu",Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            menu.anchorMax=Vector2.one; menu.offsetMin=menu.offsetMax=Vector2.zero;
            _menu=menu.gameObject;
            var harbor=NavalBaseMenu.Create(menu);
            var content=Panel(menu,"Harbor departure console",new Vector2(0,.5f),new Vector2(64,-270),new Vector2(520,540),new Color(.015f,.045f,.06f,.88f));
            Label(content,"Header","NAVAL COMMAND  /  모항",new Vector2(28,478),new Vector2(464,30),18,ConsoleText);
            Label(content,"Title","NAVAL\nROGUELIKE",new Vector2(26,320),new Vector2(472,142),52,Color.white);
            Label(content,"Subtitle","함선을 선택하고 작전 해역으로 출항하세요.",new Vector2(28,267),new Vector2(462,44),20,ConsoleText);
            var launch=MenuButton(content,"출항  /  작전 시작",new Vector2(28,182),new Vector2(464,64),ConsoleActive,25);
            launch.onClick.AddListener(()=>
            {
                content.gameObject.SetActive(false);
                var initializer=FindFirstObjectByType<Game.Ship.ShipInitializer>();
                void Complete(){_menu.SetActive(false);_combatGroup.alpha=1;_combatGroup.blocksRaycasts=true;onLaunch?.Invoke();}
                if(!StartingShipSelectorUI.Show(menu,_navalFont,initializer,Complete,()=>{harbor.ShowOverview();content.gameObject.SetActive(true);}))
                    content.gameObject.SetActive(true);
            });
            MenuButton(content,"설정  /  조작 · 음향",new Vector2(28,110),new Vector2(464,54),ConsoleIdle,21).onClick.AddListener(ShowSettings);
            MenuButton(content,"사전  /  무장 · 호위함 · 적",new Vector2(28,44),new Vector2(464,54),ConsoleIdle,21).onClick.AddListener(ShowCodex);
        }

        private Button MenuButton(Transform parent, string title, Vector2 position, Vector2 size, Color color, float fontSize)
        {
            var rect = Panel(parent, title, Vector2.zero, position, size, color);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            var label = Label(rect, "Text", title, Vector2.zero, size, fontSize, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private void ShowCodex()
        {
            if (_codex == null) _codex = CodexUI.Create(_menu.transform, _navalFont);
            _codex.Show();
        }

        private void ShowSettings()
        {
            if (_settingsOverlay != null)
            {
                _settingsOverlay.SetActive(true);
                ShowSettingsTab(_settingsTab);
                return;
            }
            var overlay = Panel(_menu.transform, "Settings overlay", Vector2.zero, Vector2.zero,
                Vector2.zero, new Color(0.005f, 0.025f, 0.03f, 0.97f));
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().raycastTarget = true;
            _settingsOverlay = overlay.gameObject;
            var content = Panel(overlay, "Settings console", new Vector2(0.5f, 0.5f),
                new Vector2(-500, -310), new Vector2(1000, 620), ConsoleBackground);
            Panel(content, "Top signal", Vector2.zero, new Vector2(0, 616), new Vector2(1000, 4), ConsoleActive);
            Label(content, "Eyebrow", "FLEET COMMAND  /  SYSTEM CONFIGURATION", new Vector2(26, 571),
                new Vector2(700, 27), 16, ConsoleText);
            Label(content, "Title", "작전 설정", new Vector2(26, 518), new Vector2(420, 51), 35, Color.white);
            var controls = MenuButton(content, "조작키", new Vector2(26, 468), new Vector2(146, 42), ConsoleIdle, 19);
            var audio = MenuButton(content, "음향", new Vector2(181, 468), new Vector2(146, 42), ConsoleIdle, 19);
            _controlsTabImage = controls.GetComponent<Image>();
            _audioTabImage = audio.GetComponent<Image>();
            controls.onClick.AddListener(() => ShowSettingsTab(0));
            audio.onClick.AddListener(() => ShowSettingsTab(1));
            _settingsMessage = Label(content, "Status", "변경 사항은 자동 저장됩니다.",
                new Vector2(26, 15), new Vector2(725, 30), 15, ConsoleText);
            MenuButton(content, "메뉴로", new Vector2(820, 11), new Vector2(154, 42),
                ConsoleIdle, 17).onClick.AddListener(CloseSettings);
            ShowSettingsTab(0);
        }

        private void CloseSettings()
        {
            _pendingBinding = null;
            if (_settingsOverlay != null) _settingsOverlay.SetActive(false);
            PlayerPrefs.Save();
        }

        private void ShowSettingsTab(int tab)
        {
            _settingsTab = tab;
            _pendingBinding = null;
            if (_controlsTabImage != null) _controlsTabImage.color = tab == 0 ? ConsoleActive : ConsoleIdle;
            if (_audioTabImage != null) _audioTabImage.color = tab == 1 ? ConsoleActive : ConsoleIdle;
            if (_settingsPage != null)
            {
                _settingsPage.gameObject.SetActive(false);
                Destroy(_settingsPage.gameObject);
            }
            var content = _settingsMessage.transform.parent;
            _settingsPage = Panel(content, tab == 0 ? "Controls page" : "Audio page", Vector2.zero,
                new Vector2(26, 59), new Vector2(948, 395), new Color(0.012f, 0.065f, 0.05f, 0.85f));
            if (tab == 0) BuildControlsSettings(_settingsPage);
            else BuildAudioSettings(_settingsPage);
            _settingsMessage.text = tab == 0
                ? "키를 누른 뒤 새 키를 입력하세요. ESC로 취소 · 정비 배치키는 고정입니다."
                : "음량은 즉시 적용되며 자동 저장됩니다.";
        }

        private void BuildControlsSettings(Transform page)
        {
            var controls = new (NavalControl action, string title)[]
            {
                (NavalControl.ThrottleUp, "기관 전령기 +"), (NavalControl.ThrottleDown, "기관 전령기 -"),
                (NavalControl.RudderLeft, "좌현 조타"), (NavalControl.RudderRight, "우현 조타"),
                (NavalControl.CenterRudder, "타 중앙"), (NavalControl.Decoy, "기만체"),
                (NavalControl.Jam, "재밍"), (NavalControl.Repair, "응급 수리"),
                (NavalControl.Smoke, "연막"), (NavalControl.Flank, "전속"),
                (NavalControl.VlsSelector, "VLS 발사기 전환"),
                (NavalControl.HelicopterSelector, "헬기 정책 전환"),
                (NavalControl.ShipStatus, "함 현황"), (NavalControl.Formation, "편대 진형 전환")
            };
            for (int i = 0; i < controls.Length; i++)
            {
                int column = i / 7, row = i % 7;
                float x = 14 + column * 466, y = 339 - row * 48;
                var line = Panel(page, controls[i].title, Vector2.zero, new Vector2(x, y),
                    new Vector2(452, 41), ConsoleIdle);
                Label(line, "Action", controls[i].title, new Vector2(12, 8),
                    new Vector2(292, 27), 17, Color.white);
                var action = controls[i].action;
                var key = MenuButton(line, GameSettings.BindingLabel(action), new Vector2(312, 4),
                    new Vector2(134, 33), ConsoleActive, 15);
                key.onClick.AddListener(() =>
                {
                    _pendingBinding = action;
                    key.GetComponentInChildren<TMP_Text>().text = "키 입력 대기…";
                    _settingsMessage.text = $"{controls[(int)action].title}: 새 키를 누르세요. ESC는 취소.";
                });
            }
            MenuButton(page, "기본 키 복원", new Vector2(720, 10), new Vector2(210, 36),
                ConsoleIdle, 15).onClick.AddListener(() =>
                {
                    GameSettings.ResetBindings();
                    RefreshSkillKeyLabels();
                    ShowSettingsTab(0);
                    _settingsMessage.text = "기본 조작키로 복원했습니다.";
                });
        }

        private void BuildAudioSettings(Transform page)
        {
            Label(page, "Audio header", "음향 채널  /  AUDIO MIX", new Vector2(25, 335),
                new Vector2(600, 32), 22, ConsoleText);
            AddVolumeSlider(page, SoundChannel.Master, "전체 음량", 270);
            AddVolumeSlider(page, SoundChannel.Effects, "무장·전투 효과음", 205);
            AddVolumeSlider(page, SoundChannel.Engine, "함선 기관음", 140);
            AddVolumeSlider(page, SoundChannel.Warning, "미사일 경보음", 75);
            Label(page, "Audio hint", "기관음과 경보음은 별도 조절됩니다. 전체 음량은 모든 채널에 적용됩니다.",
                new Vector2(25, 15), new Vector2(890, 26), 15, ConsoleText);
        }

        private void AddVolumeSlider(Transform parent, SoundChannel channel, string title, float y)
        {
            Label(parent, title, title, new Vector2(25, y), new Vector2(245, 30), 19, Color.white);
            var track = Panel(parent, title + " track", Vector2.zero, new Vector2(280, y + 6),
                new Vector2(500, 20), ConsoleIdle);
            track.GetComponent<Image>().raycastTarget = true;
            var fill = Panel(track, "Fill", Vector2.zero, Vector2.zero, new Vector2(500, 20), ConsoleActive);
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            var handle = Panel(track, "Handle", Vector2.zero, new Vector2(0, -4),
                new Vector2(12, 28), ConsoleText);
            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var value = Label(parent, "Value", "", new Vector2(805, y), new Vector2(95, 28), 18, ConsoleText);
            slider.SetValueWithoutNotify(GameSettings.Volume(channel));
            value.text = $"{Mathf.RoundToInt(slider.value * 100)}%";
            slider.onValueChanged.AddListener(v =>
            {
                GameSettings.SetVolume(channel, v);
                value.text = $"{Mathf.RoundToInt(v * 100)}%";
            });
        }

        private void RefreshSkillKeyLabels()
        {
            if (_skillBar == null) return;
            _skillBar.SetKeyLabel(DecoySlot, GameSettings.BindingLabel(NavalControl.Decoy));
            _skillBar.SetKeyLabel(JamSlot, GameSettings.BindingLabel(NavalControl.Jam));
            _skillBar.SetKeyLabel(RepairSlot, GameSettings.BindingLabel(NavalControl.Repair));
            _skillBar.SetKeyLabel(SmokeSlot, GameSettings.BindingLabel(NavalControl.Smoke));
            _skillBar.SetKeyLabel(FlankSlot, GameSettings.BindingLabel(NavalControl.Flank));
        }

        /// <summary>보스 격침 후 다음 해역에 진입하기 전에 한 번만 여는 항로 선택.</summary>
        public void ShowRouteChoice(int nextStage, float repairAmount, System.Action<bool> onChosen)
        {
            if (_routeChoice != null) return;
            _onRouteChosen = onChosen;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { ChooseRoute(false); return; }
            var overlay = Panel(canvas.transform, "Route choice", Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.01f, 0.035f, 0.06f, 0.94f));
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().raycastTarget = true;
            _routeChoice = overlay.gameObject;

            var content = Panel(overlay, "Route briefing", new Vector2(0.5f, 0.5f),
                new Vector2(-400f, -180f), new Vector2(800f, 360f), Color.clear);
            Label(content, "Eyebrow", $"STAGE {nextStage}  /  항로 결정", new Vector2(12, 304), new Vector2(770, 36), 21, Cyan);
            Label(content, "Title", "다음 해역으로 향할 항로를 선택하세요", new Vector2(12, 235), new Vector2(770, 62), 32, Color.white);
            Label(content, "Hint", "선택 결과는 다음 스테이지에만 적용됩니다.  1 / 2 키로도 선택 가능", new Vector2(12, 200), new Vector2(770, 30), 17, new Color(0.67f, 0.78f, 0.83f));
            RouteButton(content, "Supply route", new Vector2(12, 42),
                "[1] 보급 항로", $"선체 {repairAmount:0} 수리 · 적 편성 기본", new Color(0.10f, 0.29f, 0.34f), false);
            RouteButton(content, "Danger route", new Vector2(410, 42),
                "[2] 위험 항로", "적 등장·동시 수 +25% · 격침 XP +25%", new Color(0.34f, 0.17f, 0.15f), true);
        }

        private void RouteButton(Transform parent, string name, Vector2 position, string title, string detail, Color color, bool danger)
        {
            var panel = Panel(parent, name, Vector2.zero, position, new Vector2(378, 130), color);
            var image = panel.GetComponent<Image>();
            image.raycastTarget = true;
            Label(panel, "Title", title, new Vector2(18, 78), new Vector2(340, 38), 25, Color.white);
            var description = Label(panel, "Effect", detail, new Vector2(18, 20), new Vector2(342, 48), 18, Gold);
            description.textWrappingMode = TextWrappingModes.Normal;
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ChooseRoute(danger));
        }

        private void ChooseRoute(bool danger)
        {
            if (_routeChoice != null)
            {
                Destroy(_routeChoice);
                _routeChoice = null;
            }
            var callback = _onRouteChosen;
            _onRouteChosen = null;
            callback?.Invoke(danger);
        }

        /// <summary>첫 스테이지 이후 한 번만 선택하는 교리. 화면이 열린 동안 전투 시간은 정지한다.</summary>
        public void ShowDoctrineChoice(System.Action<NavalDoctrine> onChosen)
        {
            if (_doctrineChoice != null) return;
            _onDoctrineChosen = onChosen;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) { ChooseDoctrine(NavalDoctrine.None); return; }
            var overlay = Panel(canvas.transform, "Doctrine choice", Vector2.zero, Vector2.zero, Vector2.zero,
                new Color(0.01f, 0.035f, 0.06f, 0.96f));
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            overlay.GetComponent<Image>().raycastTarget = true;
            _doctrineChoice = overlay.gameObject;

            var content = Panel(overlay, "Doctrine briefing", new Vector2(0.5f, 0.5f),
                new Vector2(-420f, -180f), new Vector2(840f, 360f), Color.clear);
            Label(content, "Eyebrow", "FLEET DOCTRINE  /  작전 교리", new Vector2(10, 304), new Vector2(810, 35), 21, Cyan);
            Label(content, "Title", "이번 출항의 전투 교리를 선택하세요", new Vector2(10, 237), new Vector2(810, 62), 32, Color.white);
            Label(content, "Hint", "선택은 이번 회차 동안 유지됩니다.  1 / 2 / 3 키로도 선택 가능", new Vector2(10, 198), new Vector2(810, 30), 17, new Color(0.67f, 0.78f, 0.83f));
            DoctrineButton(content, new Vector2(10, 35), "[1] 방공", "SAM 재장전 -15%\nCIWS 포착 -30%", NavalDoctrine.AirDefense);
            DoctrineButton(content, new Vector2(290, 35), "[2] 대잠", "소나 확정 시간 -30%\n자동 핑 간격 -25%", NavalDoctrine.AntiSub);
            DoctrineButton(content, new Vector2(570, 35), "[3] 포격", "76mm 파편 범위 +25%\n기관포 점사 간격 -20%", NavalDoctrine.Gunnery);
        }

        private void DoctrineButton(Transform parent, Vector2 position, string title, string detail, NavalDoctrine doctrine)
        {
            var panel = Panel(parent, title, Vector2.zero, position, new Vector2(260f, 140f), new Color(0.09f, 0.23f, 0.29f));
            var image = panel.GetComponent<Image>();
            image.raycastTarget = true;
            Label(panel, "Title", title, new Vector2(15, 88), new Vector2(230, 39), 26, Color.white);
            Label(panel, "Effect", detail, new Vector2(15, 18), new Vector2(235, 67), 18, Gold);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => ChooseDoctrine(doctrine));
        }

        private void ChooseDoctrine(NavalDoctrine doctrine)
        {
            if (_doctrineChoice != null)
            {
                _doctrineChoice.SetActive(false);
                Destroy(_doctrineChoice);
                _doctrineChoice = null;
            }
            var callback = _onDoctrineChosen;
            _onDoctrineChosen = null;
            callback?.Invoke(doctrine);
        }

        private void Update()
        {
            if (_settingsOverlay != null && _settingsOverlay.activeSelf && Keyboard.current != null)
            {
                if (_pendingBinding.HasValue)
                {
                    foreach (var key in Keyboard.current.allKeys)
                    {
                        if (!key.wasPressedThisFrame) continue;
                        if (key.keyCode == Key.Escape)
                        {
                            _pendingBinding = null;
                            _settingsMessage.text = "키 변경을 취소했습니다.";
                        }
                        else
                        {
                            var action = _pendingBinding.Value;
                            bool changed = GameSettings.TryBind(action, key.keyCode, out string message);
                            _pendingBinding = null;
                            if (changed)
                            {
                                RefreshSkillKeyLabels();
                                ShowSettingsTab(0);
                            }
                            _settingsMessage.text = message;
                        }
                        break;
                    }
                }
                else if (Keyboard.current.escapeKey.wasPressedThisFrame) CloseSettings();
            }
            if (_doctrineChoice != null && Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) ChooseDoctrine(NavalDoctrine.AirDefense);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) ChooseDoctrine(NavalDoctrine.AntiSub);
                else if (Keyboard.current.digit3Key.wasPressedThisFrame) ChooseDoctrine(NavalDoctrine.Gunnery);
            }
            if (_routeChoice != null && Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) ChooseRoute(false);
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) ChooseRoute(true);
            }
            var gm = GameManager.Instance;
            if (gm == null || _combatGroup == null) return;
            if (gm.State == GameState.Playing && GameSettings.Pressed(NavalControl.HelicopterSelector) &&
                SelectedHelicopterDeck != null)
                SetSelectedHelicopterPolicy((HelicopterPolicy)(((int)SelectedHelicopterDeck.Policy + 1) % 3));
            bool show = gm.State == GameState.Playing;
            _combatGroup.alpha = show ? 1 : 0;
            _combatGroup.blocksRaycasts = show;
            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer > 0 || gm.Player == null) return;
            _statusTimer = 0.2f;
            RefreshHelicopterConsole();
            RefreshPolicyLabels();
            LayoutRightColumn();
            var player = gm.Player;
            OnHull(player.HullHp, player.HullMaxHp);
            OnSpeed(player.CurrentSpeed);
        }

        private void OnPhaseStarted(int stage, int phase, int count, string title)
        {
            _stage = stage;
            _phase = phase;
            _phaseCount = count;
            _phaseTitle = title;
            ShowBanner(phase == 1 ? $"스테이지 {stage}   {title}" : title);
        }

        private void OnStageProgress(float elapsed, float total)
        {
            int m = Mathf.FloorToInt(elapsed / 60f), s = Mathf.FloorToInt(elapsed % 60f);
            if (roundText != null)
                roundText.text = $"STAGE {_stage}  ·  {_phaseTitle} {_phase}/{_phaseCount}   {m:0}:{s:00}";

            if (roundBar != null) roundBar.value = total > 0f ? elapsed / total : 0f;
        }

        private void OnExperience(int level, int xp, int next)
        {
            if (levelText != null) levelText.text = $"Lv {level}";
            if (_xpText != null) _xpText.text = $"{xp} / {next} XP";
            if (xpBar != null) xpBar.value = next > 0 ? (float)xp / next : 0f;
        }

        private void OnLevelUp(int level) => ShowBanner($"레벨 {level}!");

        private void OnKill(int total)
        {
            if (killsText != null) killsText.text = $"격침 {total}";
        }

        /// <summary>구간 전환·레벨업을 알리는 짧은 배너.</summary>
        private void ShowBanner(string message)
        {
            if (bannerRoot == null || bannerText == null) return;

            bannerText.text = message;
            bannerRoot.SetActive(true);

            CancelInvoke(nameof(HideBanner));
            Invoke(nameof(HideBanner), 1.8f);
        }

        private void HideBanner()
        {
            if (bannerRoot != null) bannerRoot.SetActive(false);
        }
    }
}
