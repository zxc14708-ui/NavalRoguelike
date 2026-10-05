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
    /// 이 파일은 열기·닫기·입력 분기·카메라·공용 UI 도구. 화면별 코드는 RefitUI.*.cs(카드·편대·장비 개량·배치·격자 표시)로 나눴다(2026-10-06).
    /// </summary>
    public partial class RefitUI : MonoBehaviour
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
    }
}
