using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 무장 탄약 패널(함 현황 오른쪽). 같은 종류 무기는 한 줄로 묶어 남은 탄/용량과 상태를 보여 준다.
    ///   예) 기관포 ×2  170/400  ▮▮▮▯   /   CIWS  245/300  교전   /   VLS  0/8  EMPTY 12s
    /// 상태: 재장전(남은 초) · EMPTY(다음 보급까지) · 부족(25% 이하) · 교전(CIWS).
    /// 일부가 파괴되면 이름 옆에 "가동/전체"를 붉게(예: CIWS 1/2), 상태 칸에 "N문 파괴" — 개수가 방어력으로 오해되지 않게(2026-10-11 점검 B07).
    /// 쏠 수 있는데 안 쏘면 상태 칸에 짧은 이유(범위 밖·근접·미탐지·사각…), 줄을 누르면 문마다 자세한 이유를 위에 펼친다(점검 G03, WeaponDiagnosis).
    /// 적 미사일이 떠 있으면 CIWS 줄을 붉게 강조해 최종 방어 상태를 바로 확인하게 한다.
    /// </summary>
    public class WeaponStatusPanelUI : MonoBehaviour
    {
        public const float Width = 262f, DefaultHeight = 196f;
        private const float RowHeight = 22f;

        private float _height = DefaultHeight;
        private float TopY => _height - 32f;

        private static readonly (ModuleType type, string name)[] Order =
        {
            (ModuleType.Autocannon, "기관포"),
            (ModuleType.Nobong, "40mm"),
            (ModuleType.NavalGun, "76mm"),
            (ModuleType.Howitzer, "곡사포"),
            (ModuleType.GuidedRocket, "유도로켓"),
            (ModuleType.Vls, "VLS"),
            (ModuleType.SamLauncher, "함대공"),
            (ModuleType.Ciws, "CIWS"),
            (ModuleType.AswLauncher, "폭뢰"),
            (ModuleType.TorpedoTube, "경어뢰"),
            (ModuleType.MineRail, "기뢰"),
            (ModuleType.Ww2HeavyGun, "주포"),                // WWII(패치 C3a)
            (ModuleType.Ww2DualPurposeGun, "양용포"),        // WWII(패치 B)
            (ModuleType.Ww2QuadAa, "4연장 대공"),
            (ModuleType.Ww2BroadsideTorpedo, "현측 어뢰"),
        };

        private static readonly Color Dim = new(0.62f, 0.72f, 0.76f, 0.75f);
        private static readonly Color LowColor = new(1f, 0.62f, 0.25f, 1f);
        private static readonly Color EmptyColor = new(1f, 0.32f, 0.24f, 1f);
        private static readonly Color ReloadColor = new(0.45f, 0.85f, 1f, 1f);
        private static readonly Color Track = new(0.15f, 0.22f, 0.26f, 1f);

        private sealed class Row
        {
            public ModuleType Type;
            public RectTransform Root;
            public RawImage Highlight, BarFill;
            public TMP_Text Name, Count, Status;
        }

        private sealed class Group
        {
            public int Count, Alive, Current, Capacity;
            public bool Infinite, Engaged, AnyReloading, AllEmpty, Low;
            public float NextSeconds;
            public string Reason;
            public void Reset()
            {
                Reason = null;
                Count = Alive = Current = Capacity = 0;
                Infinite = Engaged = AnyReloading = Low = false;
                AllEmpty = true;
                NextSeconds = float.MaxValue;
            }
        }

        private TMP_FontAsset _font;
        private Color _accent, _gold;
        private readonly List<Row> _rows = new();
        private readonly Dictionary<ModuleType, Group> _groups = new();
        private TMP_Text _emptyHint;
        private float _pollTimer;

        public static WeaponStatusPanelUI Create(Transform parent, TMP_FontAsset font, Color panelColor, Color accent, Color gold, Vector2 position,
                                                 float height = DefaultHeight)
        {
            var panel = Rect("Weapon status", parent, position, new Vector2(Width, height));
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
            panel.gameObject.AddComponent<Canvas>();
            panel.gameObject.AddComponent<GraphicRaycaster>();   // 줄을 눌러 이유 펼치기

            var ui = panel.gameObject.AddComponent<WeaponStatusPanelUI>();
            ui._font = font;
            ui._accent = accent;
            ui._gold = gold;
            ui._height = height;
            ui.Build(panel);
            return ui;
        }

        private void Build(RectTransform panel)
        {
            Label(panel, "Title", "WEAPONS  /  무장", new Vector2(12, _height - 26f), new Vector2(160, 20), 14, _accent);
            _emptyHint = Label(panel, "None", "탑재 무장 없음", new Vector2(12, _height - 56f), new Vector2(200, 20), 13, Dim);
            foreach (var (type, _) in Order) _groups[type] = new Group();
        }

        private void Update()
        {
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = 0.1f;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (_targeting == null && player != null) _targeting = player.GetComponentInChildren<TargetingSystem>();
            Gather(player != null ? player.Grid : null);
            bool vampire = HostileMissileInbound();

            int used = 0;
            foreach (var (type, name) in Order)
            {
                var g = _groups[type];
                if (g.Count == 0) continue;
                var row = GetRow(used);
                row.Type = type;
                Show(row, name, g, type == ModuleType.Ciws, vampire && type == ModuleType.Ciws);
                used++;
            }
            for (int i = used; i < _rows.Count; i++) _rows[i].Root.gameObject.SetActive(false);
            _emptyHint.gameObject.SetActive(used == 0);
            RefreshDetail(player != null ? player.Grid : null);
        }

        // ------------------------------------------------------------ 안 쏘는 이유(펼침)

        private TargetingSystem _targeting;
        private ModuleType? _detailType;
        private RectTransform _detail;
        private TMP_Text _detailText;

        /// <summary>줄을 누르면 그 무장 종류의 문마다 이유를 펼치고, 다시 누르면 접는다.</summary>
        public void ToggleDetail(ModuleType type)
        {
            _detailType = _detailType == type ? null : type;
            _pollTimer = 0f;
        }
        public bool DetailOpen => _detailType.HasValue;

        /// <summary>펼친 무장의 사전 항목으로(일시정지).</summary>
        public void OpenCodexForDetail()
        {
            if (_detailType == null) return;
            var grid = GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.Grid : null;
            string id = null;
            if (grid != null) foreach (var m in grid.Modules) if (m?.Definition != null && m.Definition.Type == _detailType.Value) { id = m.Definition.Id; break; }
            var hud = FindFirstObjectByType<HUDView>();
            if (id != null && hud != null && hud.OpenCodexAt(id)) _detailType = null;
        }
        public string DetailText => _detailText != null && _detailType.HasValue ? _detailText.text : "";

        private void RefreshDetail(ShipGrid grid)
        {
            if (_detailType == null || grid == null || _groups[_detailType.Value].Count == 0)
            {
                _detailType = null;
                if (_detail != null && _detail.gameObject.activeSelf) _detail.gameObject.SetActive(false);
                return;
            }
            if (_detail == null)
            {
                _detail = Rect("Why not firing", transform, new Vector2(Width - 430f, _height + 6f), new Vector2(430f, 60f));
                var bg = _detail.gameObject.AddComponent<Image>();
                bg.color = new Color(0.02f, 0.05f, 0.07f, 0.94f);
                bg.raycastTarget = false;
                _detailText = Label(_detail, "Text", "", new Vector2(10, 6), new Vector2(410, 48), 13, Color.white);
                _detailText.textWrappingMode = TextWrappingModes.Normal;
                _detailText.alignment = TextAlignmentOptions.BottomLeft;
                // 사전에서 보기(점검 U08): 일시정지하고 사전을 이 무장 항목으로
                var codex = Rect("Open codex", _detail, Vector2.zero, new Vector2(104f, 20f));
                codex.anchorMin = codex.anchorMax = codex.pivot = new Vector2(1f, 1f);
                codex.anchoredPosition = new Vector2(-6f, -4f);
                var bg2 = codex.gameObject.AddComponent<Image>();
                bg2.color = new Color(0.08f, 0.2f, 0.26f, 1f);
                var button = codex.gameObject.AddComponent<Button>();
                button.targetGraphic = bg2;
                button.onClick.AddListener(OpenCodexForDetail);
                var label = Label(codex, "Text", "사전에서 보기 ▶", Vector2.zero, new Vector2(104f, 20f), 12, new Color(0.62f, 0.88f, 1f), TextAlignmentOptions.Center);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = Vector2.zero;
            }
            var sb = new System.Text.StringBuilder();
            string name = "";
            foreach (var (t, n) in Order) if (t == _detailType.Value) name = n;
            sb.Append($"<color=#9FE0FF><b>{name}</b> — 문마다 상태 <size=85%>(다시 누르면 닫힘)</size></color>");
            int index = 0;
            foreach (var m in grid.Modules)
            {
                if (m?.Definition == null || m.Definition.Type != _detailType.Value) continue;
                var r = WeaponDiagnosis.Explain(m, _targeting);
                string state = string.IsNullOrEmpty(r.Detail) ? "작동 중" : r.Detail;
                sb.Append($"\n<color=#e0c05a>#{++index}</color> {state}");
            }
            _detailText.text = sb.ToString();
            _detailText.rectTransform.sizeDelta = new Vector2(410f, 400f);
            _detailText.ForceMeshUpdate();
            float h = _detailText.preferredHeight + 12f;
            _detail.sizeDelta = new Vector2(430f, h);
            _detailText.rectTransform.sizeDelta = new Vector2(410f, h - 12f);
            if (!_detail.gameObject.activeSelf) _detail.gameObject.SetActive(true);
        }

        private void Gather(ShipGrid grid)
        {
            foreach (var g in _groups.Values) g.Reset();
            if (grid == null) return;

            foreach (var m in grid.Modules)
            {
                if (m?.Definition == null || !_groups.TryGetValue(m.Definition.Type, out var g)) continue;
                g.Count++;
                if (!m.IsDestroyed) g.Alive++;
                if (!m.IsDestroyed && g.Reason == null)
                {
                    var why = WeaponDiagnosis.Explain(m, _targeting);
                    if (why.Idle) g.Reason = why.Short;
                }
                if (m.IsDestroyed || m.Runtime is not IAmmoUser user || user.Ammo == null) continue;

                var ammo = user.Ammo;
                if (ammo.Infinite) { g.Infinite = true; g.AllEmpty = false; continue; }

                g.Current += ammo.Current;
                g.Capacity += ammo.Capacity;
                g.Engaged |= user.IsEngaged;
                var state = ammo.Status;
                if (state == AmmoMagazine.State.Reloading) g.AnyReloading = true;
                if (state != AmmoMagazine.State.Empty) g.AllEmpty = false;
                if (state == AmmoMagazine.State.Low) g.Low = true;
                float next = ammo.SecondsToNext;
                if (next > 0f && (state == AmmoMagazine.State.Reloading || state == AmmoMagazine.State.Empty))
                    g.NextSeconds = Mathf.Min(g.NextSeconds, next);
            }
        }

        private void Show(Row row, string name, Group g, bool isCiws, bool alert)
        {
            row.Root.gameObject.SetActive(true);
            int lost = g.Count - g.Alive;
            row.Name.text = lost > 0 ? $"{name} <color=#FF6A50>{g.Alive}/{g.Count}</color>"
                : g.Count > 1 ? $"{name} <color=#9FB3BA>×{g.Count}</color>" : name;
            row.Highlight.enabled = alert;

            if (g.Infinite || g.Capacity <= 0)
            {
                row.Count.text = g.Infinite ? "무한" : "파괴";
                row.Count.color = g.Infinite ? Color.white : EmptyColor;
                row.BarFill.rectTransform.sizeDelta = new Vector2(g.Infinite ? 40f : 0f, 4f);
                row.Status.text = g.Infinite && !string.IsNullOrEmpty(g.Reason) ? g.Reason : "";
                row.Status.color = Dim;
                return;
            }

            float f = Mathf.Clamp01((float)g.Current / g.Capacity);
            row.Count.text = $"{g.Current}/{g.Capacity}";
            row.BarFill.rectTransform.sizeDelta = new Vector2(40f * f, 4f);

            string next = g.NextSeconds < float.MaxValue ? $" {g.NextSeconds:0.0}s" : "";
            Color c;
            if (g.AllEmpty) { row.Status.text = "EMPTY" + next; c = EmptyColor; }
            else if (g.AnyReloading) { row.Status.text = "재장전" + next; c = ReloadColor; }
            else if (isCiws && g.Engaged) { row.Status.text = g.Low ? "교전·부족" : "교전"; c = g.Low ? LowColor : _gold; }
            else if (g.Low) { row.Status.text = "탄약 부족"; c = LowColor; }
            else if (lost > 0) { row.Status.text = $"{lost}문 파괴"; c = EmptyColor; }
            else if (!g.Engaged && !string.IsNullOrEmpty(g.Reason)) { row.Status.text = g.Reason; c = Dim; }
            else { row.Status.text = ""; c = Color.white; }

            row.Status.color = c;
            row.Count.color = g.AllEmpty ? EmptyColor : g.Low ? LowColor : Color.white;
            row.BarFill.color = g.AllEmpty ? EmptyColor : g.AnyReloading ? ReloadColor : g.Low ? LowColor : _accent;
        }

        private static bool HostileMissileInbound()
        {
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].IsAlive) return true;
            return false;
        }

        private Row GetRow(int index)
        {
            while (_rows.Count <= index)
            {
                float y = TopY - (_rows.Count + 1) * RowHeight;
                var root = Rect($"Row {_rows.Count}", transform, new Vector2(0, y), new Vector2(Width, RowHeight));
                var row = new Row
                {
                    Root = root,
                    Highlight = Raw(root, "Alert", Texture2D.whiteTexture, new Vector2(4, 1), new Vector2(Width - 8, RowHeight - 2), new Color(1f, 0.25f, 0.2f, 0.22f)),
                    Name = Label(root, "Name", "", new Vector2(12, 1), new Vector2(84, 19), 14, Color.white),
                    Count = Label(root, "Count", "", new Vector2(96, 1), new Vector2(62, 19), 14, Color.white, TextAlignmentOptions.Right),
                };
                var hit = root.gameObject.AddComponent<Image>();
                hit.color = Color.clear;   // 줄 전체를 누를 수 있게
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => ToggleDetail(row.Type));
                var track = Raw(root, "Bar", Texture2D.whiteTexture, new Vector2(164, 8), new Vector2(40, 4), Track);
                row.BarFill = Raw(track.transform, "Fill", Texture2D.whiteTexture, Vector2.zero, new Vector2(40, 4), _accent);
                row.Status = Label(root, "Status", "", new Vector2(208, 2), new Vector2(Width - 212, 17), 11, Color.white);
                _rows.Add(row);
            }
            return _rows[index];
        }

        // ------------------------------------------------------------ 만들기

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RawImage Raw(Transform parent, string name, Texture texture, Vector2 position, Vector2 size, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var img = rect.gameObject.AddComponent<RawImage>();
            img.texture = texture;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private TMP_Text Label(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color,
                               TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) text.font = _font;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
