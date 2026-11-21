using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 손상 통제반이 불 · 구멍을 맡는다(2026-11-21). 조작 없이 자동으로만 — 배가 작아 신경 쓸 것을 늘리지 않는다(사용자 2026-11-21:
    /// 순서 바꾸기 · 역침수 같은 상세 조작은 뺐다).
    ///   - 조: 작동 중인 함교 1 + 손상통제반 블록마다 1. 한 조는 한 번에 일 하나(불 하나 또는 구멍 하나)를 끝날 때까지 맡는다.
    ///   - 순서: 유폭 위험 화재 → 물이 많이 든 구획의 구멍 → 센 불 → 나머지. 맡은 일보다 훨씬 급한 일(유폭 위험)이 생기면 그리로 간다.
    ///   - 속도: 불은 1조가 초당 세기 0.2(교전 중 절반), 구멍은 1조가 8초(교전 중 4배 느림). "수리 효율" 카드가 빠르게 한다.
    ///   - 화면: 함 현황 위에 한 줄(침수 · 기울기 · 화재 · 유폭 초). 일이 없으면 숨긴다.
    /// </summary>
    public sealed class ShipDamageControl : MonoBehaviour
    {
        public const float UnderFireWindow = 3f, FireUnderFireSlow = 2f, HoleUnderFireSlow = 4f;

        /// <summary>검사용: 켜면 조가 일하지 않는다(유폭 카운트다운 등을 볼 때).</summary>
        public static bool DevPaused;

        private ShipController _ship;
        private ShipGrid _grid;
        private ShipFlooding _flood;
        private ShipFire _fire;
        private readonly List<object> _tasks = new();
        private readonly List<object> _held = new();   // 조마다 지금 붙어 있는 일
        private readonly List<object> _assigned = new();

        /// <summary>지금 일하는 조 수 · 지금 맡은 일(검증 · 작동 연출).</summary>
        public int Crews { get; private set; }
        public IReadOnlyList<object> Assigned => _assigned;
        /// <summary>화면에 띄울 한 줄(비면 숨김).</summary>
        public string StatusLine { get; private set; } = "";
        public bool StatusUrgent { get; private set; }

        private void Awake()
        {
            _ship = GetComponentInParent<ShipController>();
            var resolver = GetComponent<DamageResolver>();
            _grid = resolver != null ? resolver.Grid : null;
            _flood = GetComponent<ShipFlooding>();
            _fire = GetComponent<ShipFire>();
        }

        private int CountCrews()
        {
            int n = 0;
            if (_grid == null) return 0;
            foreach (var m in _grid.Modules)
                if (m != null && m.IsOperational && m.Definition != null &&
                    (m.Definition.Type == ModuleType.Bridge || m.Definition.Type == ModuleType.RepairBay)) n++;
            return n;
        }

        private float Priority(object task)
        {
            if (task is ShipFire.Fire f)
            {
                float p = f.Intensity;
                if (ShipFire.IsCookOffRisk(f.Module) && !f.Module.IsDestroyed) p += f.CookOff >= 0f ? 10f : 5f;   // 유폭 위험이 가장 먼저
                return p;
            }
            if (task is ShipFlooding.Hole h && _flood != null && _flood.Compartments.Count > 0)
            {
                var c = _flood.Compartments[Mathf.Clamp(h.Compartment, 0, _flood.Compartments.Count - 1)];
                return 0.5f + c.Water * 2f + h.Rate * 10f;
            }
            return 0f;
        }

        private void Update()
        {
            if (_ship == null) return;
            float dt = Time.deltaTime;
            Crews = CountCrews();
            _tasks.Clear();
            _assigned.Clear();
            if (_fire != null) foreach (var f in _fire.Fires) _tasks.Add(f);
            if (_flood != null) foreach (var h in _flood.Holes) _tasks.Add(h);
            if (dt > 0f && Crews > 0 && _tasks.Count > 0 && !DevPaused)
            {
                _tasks.Sort((a, b) => Priority(b).CompareTo(Priority(a)));
                // 조는 맡은 일을 끝낼 때까지 붙어 있다(매 프레임 가장 센 불로 옮겨 다니면 불이 다 같이 조금씩만 줄고 하나도 꺼지지 않았다)
                _held.RemoveAll(t => !_tasks.Contains(t));
                while (_held.Count > Crews) _held.RemoveAt(_held.Count - 1);
                foreach (var t in _tasks)
                {
                    if (_held.Contains(t)) continue;
                    if (_held.Count < Crews) { _held.Add(t); continue; }
                    int weakest = -1; float wp = float.MaxValue;
                    for (int i = 0; i < _held.Count; i++) { float p = Priority(_held[i]); if (p < wp) { wp = p; weakest = i; } }
                    if (weakest >= 0 && Priority(t) > wp + 3f) _held[weakest] = t;
                }
                bool underFire = Time.time - _ship.LastHullDamageTime < UnderFireWindow;
                float eff = RunUpgrades.RepairMultiplier;
                _held.Sort((a, b) => Priority(b).CompareTo(Priority(a)));
                for (int i = 0; i < _held.Count; i++)
                {
                    var t = _held[i];
                    _assigned.Add(t);
                    if (t is ShipFire.Fire f) _fire.Work(f, ShipFire.FightRate * eff / (underFire ? FireUnderFireSlow : 1f) * dt);
                    else if (t is ShipFlooding.Hole h) _flood.Work(h, eff / ShipFlooding.PatchSeconds / (underFire ? HoleUnderFireSlow : 1f) * dt);
                }
            }
            BuildStatus();
            UpdateHud();
        }

        private void BuildStatus()
        {
            var sb = new System.Text.StringBuilder();
            bool urgent = false;
            if (_flood != null && (_flood.FloodedCompartments > 0.02f || _flood.Holes.Count > 0))
            {
                sb.Append($"침수 {_flood.FloodedCompartments:0.0}구획");
                if (_flood.Holes.Count > 0) sb.Append($"(구멍 {_flood.Holes.Count})");
                if (Mathf.Abs(_flood.TargetList) >= 1f) sb.Append($" · 기울기 {(_flood.TargetList > 0 ? "우" : "좌")} {Mathf.Abs(_flood.TargetList):0}°");
                if (_flood.Sinking) { sb.Append(" · 침몰 위험"); urgent = true; }
            }
            if (_fire != null && _fire.Fires.Count > 0)
            {
                if (sb.Length > 0) sb.Append("  |  ");
                sb.Append($"화재 {_fire.Fires.Count}");
                float cook = float.MaxValue;
                foreach (var f in _fire.Fires) if (f.CookOff >= 0f) cook = Mathf.Min(cook, f.CookOff);
                if (cook < float.MaxValue) { sb.Append($" · 유폭 {Mathf.CeilToInt(cook)}초"); urgent = true; }
            }
            if (sb.Length > 0) sb.Append($"  |  손상 통제 {Crews}조 작업 중");
            StatusLine = sb.ToString();
            StatusUrgent = urgent;
        }

        // ---- 화면 한 줄(함 현황 패널 바로 위, HUD 캔버스에 붙인다)
        private TMPro.TextMeshProUGUI _hudText;
        private UnityEngine.UI.Image _hudBack;
        private bool _hudTried;

        private void UpdateHud()
        {
            if (_hudText == null && !_hudTried)
            {
                _hudTried = true;
                var hud = Object.FindFirstObjectByType<Game.UI.HUDView>();
                var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
                if (canvas == null) return;
                var any = canvas.GetComponentInChildren<TMPro.TMP_Text>(true);
                var go = new GameObject("Damage control status", typeof(RectTransform));
                go.layer = canvas.gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas.transform, false);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(16f, 206f);
                rt.sizeDelta = new Vector2(760f, 30f);
                _hudBack = go.AddComponent<UnityEngine.UI.Image>();
                _hudBack.color = new Color(0.04f, 0.07f, 0.1f, 0.72f);
                _hudBack.raycastTarget = false;
                var tgo = new GameObject("Text", typeof(RectTransform));
                tgo.layer = go.layer;
                var trt = (RectTransform)tgo.transform;
                trt.SetParent(rt, false);
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10f, 0f); trt.offsetMax = new Vector2(-10f, 0f);
                _hudText = tgo.AddComponent<TMPro.TextMeshProUGUI>();
                if (any != null) _hudText.font = any.font;
                _hudText.fontSize = 17;
                _hudText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                _hudText.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
                _hudText.raycastTarget = false;
            }
            if (_hudText == null) return;
            bool show = !string.IsNullOrEmpty(StatusLine) && (GameManager.Instance == null || GameManager.Instance.State == GameState.Playing);
            _hudBack.enabled = _hudText.enabled = show;
            if (!show) return;
            _hudText.text = StatusLine;
            float pulse = StatusUrgent ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) : 1f;
            _hudText.color = StatusUrgent ? new Color(1f, 0.45f, 0.3f, pulse) : new Color(0.86f, 0.92f, 0.96f, 1f);
            var size = _hudText.GetPreferredValues(StatusLine);
            ((RectTransform)_hudBack.transform).sizeDelta = new Vector2(Mathf.Min(1100f, size.x + 24f), 30f);
        }

        /// <summary>검증용: 화면 한 줄이 보이는가.</summary>
        public bool HudVisible => _hudText != null && _hudText.enabled;
    }
}
