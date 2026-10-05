using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 전투 중 Tab으로 여는 함선 시스템 현황.
    /// 지금 무엇이 설치돼 있고 어디가 부서졌는지, 무기 탄약이 얼마나 남았는지를 한 화면에서 확인한다.
    /// 정비 중에는 열리지 않는다.
    ///
    /// 모듈이 많아져도 창을 벗어나지 않게: 표 형식(열 고정) → 한 단에 안 들어가면 두 단 → 그래도 넘치면 글자를 줄이고,
    /// 최소 크기에서도 넘치면 마지막 줄에 "외 N개". 파괴·손상·탄약 부족 모듈을 위로 올려 넘쳐도 중요한 것은 보인다.
    /// 열려 있는 동안 0.25초마다 갱신한다(탄약·재장전은 계속 변한다).
    /// </summary>
    public class ModuleStatusUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text headerText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private ShipGrid grid;
        [SerializeField] private ShipSystems systems;

        [Header("Layout")]
        [SerializeField] private Vector2 panelSize = new(1400f, 780f);
        [SerializeField] private float bodyTop = 86f;
        [SerializeField] private float bodyBottom = 64f;
        [SerializeField] private float columnGap = 40f;
        [SerializeField] private float maxFontSize = 20f;
        [SerializeField] private float minFontSize = 12f;
        [Tooltip("줄 높이 = 글자 크기 × 이 값")]
        [SerializeField] private float lineHeightRatio = 1.32f;
        [SerializeField] private float refreshInterval = 0.25f;

        private readonly StringBuilder _sb = new();
        private readonly List<ModuleInstance> _sorted = new();
        private TMP_Text _bodyRight;
        private TMP_Text _growthFooter;   // 성장 카드로 쌓은 보너스 요약(아래 한 줄)
        private bool _open;
        private bool _layoutReady;
        private float _refreshTimer;
        private float _columnWidth, _bodyHeight;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (grid == null) Debug.LogError("[ModuleStatusUI] grid 미할당.", this);
        }

        private void OnEnable()
        {
            GameEvents.ModuleInstalled += OnModulesChanged;
            GameEvents.ModuleDestroyed += OnModulesChanged;
            GameEvents.ModuleRemoved += OnModulesChanged;
            GameEvents.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.ModuleInstalled -= OnModulesChanged;
            GameEvents.ModuleDestroyed -= OnModulesChanged;
            GameEvents.ModuleRemoved -= OnModulesChanged;
            GameEvents.StateChanged -= OnStateChanged;
        }

        private void OnModulesChanged(ModuleInstance _) { if (_open) Refresh(); }

        /// <summary>배치 모드로 들어가면 겹치지 않도록 닫는다.</summary>
        private void OnStateChanged(GameState state)
        {
            if (state != GameState.Playing) SetOpen(false);
        }

        private void Update()
        {
            if (GameSettings.Pressed(NavalControl.ShipStatus) &&
                GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)   // 전투 중에만 연다
                SetOpen(!_open);

            if (!_open) return;
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f) Refresh();
        }

        /// <summary>개발 검증용: 열고 닫기.</summary>
        public void SetOpen(bool open)
        {
            _open = open;
            if (root != null) root.SetActive(open);
            if (open) Refresh();
        }

        /// <summary>개발 검증용: 지금 표시 중인 줄 수와 글자 크기, 넘쳐서 생략한 모듈 수.</summary>
        public (int shown, int hidden, float fontSize, int columns) LastLayout { get; private set; }

        /// <summary>창을 넓히고 오른쪽 단을 만든다. 씬의 기존 배치(빌더)를 그대로 두고 런타임에만 바꾼다.</summary>
        private void EnsureLayout()
        {
            if (_layoutReady || root == null || bodyText == null) return;
            _layoutReady = true;

            var panel = root.GetComponent<RectTransform>();
            if (panel != null) panel.sizeDelta = panelSize;

            float innerWidth = panelSize.x - 80f;
            _columnWidth = (innerWidth - columnGap) * 0.5f;
            _bodyHeight = panelSize.y - bodyTop - bodyBottom;

            if (headerText != null) headerText.rectTransform.sizeDelta = new Vector2(innerWidth, headerText.rectTransform.sizeDelta.y);

            ConfigureBody(bodyText, 40f);
            _bodyRight = Instantiate(bodyText, bodyText.transform.parent);
            _bodyRight.name = "BodyRight";
            ConfigureBody(_bodyRight, 40f + _columnWidth + columnGap);

            _growthFooter = Instantiate(bodyText, bodyText.transform.parent);
            _growthFooter.name = "GrowthFooter";
            var fr = _growthFooter.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0f);
            fr.pivot = new Vector2(0f, 0f);
            fr.anchoredPosition = new Vector2(40f, 14f);
            fr.sizeDelta = new Vector2(innerWidth, 30f);
            _growthFooter.fontSize = 19f;
            _growthFooter.enableAutoSizing = false;
            _growthFooter.textWrappingMode = TextWrappingModes.NoWrap;
            _growthFooter.overflowMode = TextOverflowModes.Truncate;
            _growthFooter.alignment = TextAlignmentOptions.BottomLeft;
            _growthFooter.richText = true;
        }

        private void ConfigureBody(TMP_Text text, float x)
        {
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -bodyTop);
            rt.sizeDelta = new Vector2(_columnWidth, _bodyHeight);
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = true;
        }

        /// <summary>설치된 모듈을 정리해 표시한다.</summary>
        private void Refresh()
        {
            _refreshTimer = refreshInterval;
            if (grid == null) return;
            EnsureLayout();

            int total = 0, destroyed = 0, damaged = 0;
            _sorted.Clear();
            foreach (var m in grid.Modules)
            {
                if (m == null) continue;
                _sorted.Add(m);
                total++;
                if (m.IsDestroyed) destroyed++;
                else if (m.Hp < m.MaxHp * 0.6f) damaged++;
            }
            _sorted.Sort(CompareUrgency);

            if (headerText != null && systems != null)
            {
                headerText.text =
                    $"함선 시스템 현황    모듈 {total - destroyed}/{total}" +
                    (destroyed > 0 ? $"  <color=#e05a4a>파괴 {destroyed}</color>" : "") +
                    (damaged > 0 ? $"  <color=#e0c05a>손상 {damaged}</color>" : "") +
                    $"    탐지 {systems.DetectionRange:0}    소나 {systems.SonarRange:0}    피해 감소 {systems.DamageReduction * 100f:0}%";
            }

            string growth = RunUpgrades.Any ? ModuleCardText.BuildGrowthSummary() : "";
            if (_growthFooter != null) _growthFooter.text = growth.Length > 0 ? $"<color=#ffd060>성장</color>  {growth}" : "";
            float footer = growth.Length > 0 ? 32f : 0f;

            if (bodyText == null) return;

            if (total == 0)
            {
                bodyText.text = "설치된 모듈이 없습니다.";
                if (_bodyRight != null) _bodyRight.text = "";
                LastLayout = (0, 0, maxFontSize, 1);
                return;
            }

            // 한 단 → 두 단 → 글자 줄이기 → 생략 순으로 맞춘다(머리줄 1줄 포함)
            float font = maxFontSize;
            int columns = 1, rowsPerColumn = RowsFor(font);
            if (total > rowsPerColumn && _bodyRight != null) columns = 2;
            while (total > rowsPerColumn * columns && font > minFontSize)
            {
                font = Mathf.Max(minFontSize, font - 1f);
                rowsPerColumn = RowsFor(font);
            }
            int capacity = rowsPerColumn * columns;
            int hidden = Mathf.Max(0, total - capacity);
            if (hidden > 0) hidden++;   // 마지막 줄을 "외 N개"에 쓴다
            int shown = total - hidden;

            bodyText.fontSize = font;
            bodyText.text = BuildColumn(0, Mathf.Min(shown, rowsPerColumn), font, columns == 1 ? hidden : 0);
            if (_bodyRight != null)
            {
                _bodyRight.fontSize = font;
                _bodyRight.text = columns == 2 ? BuildColumn(rowsPerColumn, shown - rowsPerColumn, font, hidden) : "";
            }
            LastLayout = (shown, hidden, font, columns);

            // 창 높이는 내용에 맞춘다(모듈이 적으면 빈 공간 없이 작게). 줄 계산은 최대 높이 기준 그대로.
            int rowsUsed = columns == 1 ? Mathf.Min(shown, rowsPerColumn) + 1 + (hidden > 0 ? 1 : 0) : rowsPerColumn + 1;
            float height = Mathf.Clamp(bodyTop + rowsUsed * font * lineHeightRatio + bodyBottom + 12f + footer, 300f, panelSize.y + footer);
            var panel = root != null ? root.GetComponent<RectTransform>() : null;
            if (panel != null && !Mathf.Approximately(panel.sizeDelta.y, height)) panel.sizeDelta = new Vector2(panelSize.x, height);
        }

        private int RowsFor(float font) => Mathf.Max(1, Mathf.FloorToInt(_bodyHeight / (font * lineHeightRatio)) - 1);

        /// <summary>파괴 → 손상 → 탄약 없음/부족 → 나머지(종류, 위치 순).</summary>
        private static int CompareUrgency(ModuleInstance a, ModuleInstance b)
        {
            int ua = Urgency(a), ub = Urgency(b);
            if (ua != ub) return ua.CompareTo(ub);
            int ta = (int)a.Definition.Type, tb = (int)b.Definition.Type;
            if (ta != tb) return ta.CompareTo(tb);
            return a.Origin.X != b.Origin.X ? b.Origin.X.CompareTo(a.Origin.X) : a.Origin.Z.CompareTo(b.Origin.Z);
        }

        private static int Urgency(ModuleInstance m)
        {
            if (m.IsDestroyed) return 0;
            if (m.Hp < m.MaxHp * 0.3f) return 1;
            if (m.Runtime is IAmmoUser u && u.Ammo != null && !u.Ammo.Infinite)
            {
                var s = u.Ammo.Status;
                if (s == AmmoMagazine.State.Empty || s == AmmoMagazine.State.Reloading) return 2;
                if (s == AmmoMagazine.State.Low) return 3;
            }
            if (m.Hp < m.MaxHp * 0.6f) return 4;
            return 5;
        }

        /// <summary>열 위치는 글자 크기에 비례해 정한다(작아져도 줄이 맞게).</summary>
        private string BuildColumn(int start, int count, float font, int hiddenAfter)
        {
            float k = font / maxFontSize;
            string P(float x) => $"<pos={x * k:0}>";

            _sb.Clear();
            _sb.Append("<color=#8fa4b8>모듈").Append(P(190)).Append("위치").Append(P(265)).Append("내구")
               .Append(P(395)).Append("상태").Append(P(460)).Append("탄약</color>\n");

            for (int i = start; i < start + count && i < _sorted.Count; i++)
            {
                var m = _sorted[i];
                float f = m.MaxHp > 0f ? m.Hp / m.MaxHp : 0f;
                string hpColor = m.IsDestroyed ? "#e05a4a" : f < 0.3f ? "#ff7a4d" : f < 0.6f ? "#e0c05a" : "#d8e2e8";
                string state = m.IsDestroyed ? "<color=#e05a4a>파괴</color>" : m.IsRaised ? "올림" : "정상";

                _sb.Append(m.Definition.DisplayName)
                   .Append(m.UpgradeLevel > 0 ? $" <color=#7fd08a>{Game.Modules.ModuleUpgrades.Roman(m.UpgradeLevel)}</color>" : "")
                   .Append(P(190)).Append($"{m.Origin.X},{m.Origin.Z}")
                   .Append(P(265)).Append($"<color={hpColor}>{m.Hp:0}/{m.MaxHp:0}</color>")
                   .Append(P(395)).Append(state)
                   .Append(P(460)).Append(AmmoText(m))
                   .Append('\n');
            }

            if (hiddenAfter > 0) _sb.Append($"<color=#8fa4b8>… 외 {hiddenAfter}개 (정상 모듈)</color>");
            return _sb.ToString();
        }

        private static string AmmoText(ModuleInstance m)
        {
            if (m.IsDestroyed || m.Runtime is not IAmmoUser user || user.Ammo == null || user.Ammo.Infinite) return "";
            var a = user.Ammo;
            string tag = a.Status switch
            {
                AmmoMagazine.State.Reloading => $" <color=#73d9ff>재장전 {a.SecondsToNext:0.0}s</color>",
                AmmoMagazine.State.Empty => $" <color=#ff5a3d>EMPTY {a.SecondsToNext:0.0}s</color>",
                AmmoMagazine.State.Low => " <color=#ff9e40>부족</color>",
                _ => "",
            };
            return $"{a.Current}/{a.Capacity}{tag}";
        }
    }
}
