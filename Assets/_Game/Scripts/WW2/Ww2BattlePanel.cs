using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.WW2
{
    /// <summary>
    /// WWII 시험 전투 안내판(패치 B, 왼쪽 위). 무엇을 할 수 있고 무엇이 오는지 한눈에:
    ///   - 구간 이름(StageDirector 구간 시작)
    ///   - 어뢰: [키] 좌현 장전/전체 · [키] 우현 장전/전체 · 재장전 남은 초(쏠 수 있는 현측만 밝게)
    ///   - 공습 경보: 방향을 확정한 뇌격기·급강하폭격기마다 "어느 쪽 · 투하까지 몇 초"(가장 급한 것부터 3개)
    ///   - 마지막 어뢰 안내(발사 결과·못 쏜 이유)
    /// 시간이 멈춰도(일시정지) 그대로 보인다. 결과 화면에서는 숨는다.
    /// </summary>
    public sealed class Ww2BattlePanel : MonoBehaviour
    {
        private static readonly Color Panel = new(0.07f, 0.055f, 0.03f, 0.9f);
        private static readonly Color Accent = new(0.85f, 0.62f, 0.25f, 1f);

        public static Ww2BattlePanel Instance { get; private set; }

        private RectTransform _root;
        private TMP_Text _header, _torpedo, _salvo, _threats, _message;
        private string _phase = "";
        private float _poll;
        private bool _shown = true;

        public string Text => $"{_header?.text}\n{_torpedo?.text}\n{_threats?.text}\n{_message?.text}";

        public static Ww2BattlePanel Create(Transform canvas, TMP_FontAsset font)
        {
            var go = new GameObject("WW2 battle panel", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var p = go.AddComponent<Ww2BattlePanel>();
            p._font = font;
            p.Build((RectTransform)go.transform, font);
            Instance = p;
            return p;
        }

        private void OnEnable() => GameEvents.PhaseStarted += OnPhase;
        private void OnDisable() => GameEvents.PhaseStarted -= OnPhase;
        private void OnDestroy() { if (Instance == this) Instance = null; }
        private void OnPhase(int stage, int index, int count, string title) => _phase = $"{index}/{count}  {title}";

        private void Build(RectTransform root, TMP_FontAsset font)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
            root.anchoredPosition = new Vector2(14f, -14f);
            root.sizeDelta = new Vector2(430f, 174f);
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;
            var top = new GameObject("Top", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            top.SetParent(root, false);
            top.anchorMin = new Vector2(0f, 1f); top.anchorMax = new Vector2(1f, 1f); top.pivot = new Vector2(0.5f, 1f);
            top.sizeDelta = new Vector2(0f, 3f);
            top.GetComponent<Image>().color = Accent;
            _root = root;
            _header = Label(root, font, "Header", 16, Color.white, 8f, 24f);
            _torpedo = Label(root, font, "Torpedo", 15, new Color(1f, 0.9f, 0.7f), 34f, 24f);
            _salvo = Label(root, font, "Salvo", 15, new Color(1f, 0.9f, 0.7f), 58f, 24f);
            _threats = Label(root, font, "Threats", 15, new Color(1f, 0.55f, 0.35f), 84f, 66f);
            _message = Label(root, font, "Message", 13, new Color(0.8f, 0.85f, 0.88f), 150f, 20f);
        }

        private static TMP_Text Label(RectTransform parent, TMP_FontAsset font, string name, float size, Color color, float top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.richText = true;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.alignment = TextAlignmentOptions.TopLeft;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(12f, -top);
            r.sizeDelta = new Vector2(410f, height);
            return t;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool show = gm != null && !gm.IsRunOver;
            if (_shown != show)
            {
                _shown = show;
                foreach (Transform c in _root) c.gameObject.SetActive(show);
                _root.GetComponent<Image>().enabled = show;
                if (!show) foreach (var m in _markers) if (m != null) m.gameObject.SetActive(false);
            }
            if (!show) return;
            _poll -= Time.unscaledDeltaTime;
            if (_poll > 0f) return;
            _poll = 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            _header.text = $"<color=#FFC870>WWII 시험 전투</color>  {_phase}";

            var control = Ww2TorpedoControl.Instance;
            if (control != null)
            {
                _torpedo.text = $"{Side(control, Ww2Side.Port, Ww2Control.TorpedoPort)}    {Side(control, Ww2Side.Starboard, Ww2Control.TorpedoStarboard)}";
                _message.text = control.LastMessage != null && Time.unscaledTime - control.LastMessageTime < 4f ? control.LastMessage : "";
            }
            // 주포 일제(패치 C3a): 대구경 주포가 있을 때만
            var salvo = Ww2SalvoControl.Instance;
            if (salvo != null)
            {
                var (loaded, total, reload) = salvo.Status();
                string k = $"<color=#e0c05a>[{Ww2Controls.BindingLabel(Ww2Control.MainSalvo)}]</color>";
                _salvo.text = total == 0 ? "" : loaded > 0
                    ? $"{k} 주포 일제 <b>{loaded}</b>/{total}문 준비" + (salvo.Aiming ? "  <color=#FFC870>겨누는 중 — 떼면 발사</color>" : "")
                    : $"{k} 주포 <color=#ff8a6a>장전 {reload:0.0}초</color>";
                if (salvo.LastMessage != null && Time.unscaledTime - salvo.LastMessageTime < 4f
                    && (control == null || control.LastMessage == null || salvo.LastMessageTime > control.LastMessageTime))
                    _message.text = salvo.LastMessage;
            }

            var sb = new StringBuilder();
            int shown = 0;
            var ship = gm.Player;
            // 가장 급한 것부터
            var list = new System.Collections.Generic.List<IAirStrike>();
            foreach (var s in AirStrikes.Active) if (s != null && s.IsCommitted && !s.HasReleased) list.Add(s);
            list.Sort((a, b) => a.SecondsToRelease.CompareTo(b.SecondsToRelease));
            foreach (var s in list)
            {
                if (shown++ >= 3) break;
                sb.Append($"▲ {s.StrikeName} — {Bearing(ship != null ? ship.transform : null, s.DangerPoint)} · 투하 {s.SecondsToRelease:0.0}초\n");
            }
            _threats.text = shown == 0 ? "<color=#8a9aa0>공습 경보 없음</color>" : sb.ToString().TrimEnd();
            PlaceMarkers(list);
        }

        // ------------------------------------------------------------ 위험 지점 표식(화면 안 · 밖 가장자리)

        private readonly System.Collections.Generic.List<TMP_Text> _markers = new();
        private TMP_FontAsset _font;

        /// <summary>
        /// 방향을 확정한 공격마다 위험한 곳(급강하 착탄점 · 뇌격 어뢰가 내 침로와 만나는 곳)에 표식을 띄운다.
        /// 화면 밖이면 그쪽 가장자리에 붙인다 — 전속으로 달리면 만나는 곳이 화면 위로 벗어나기 때문이다.
        /// </summary>
        private void PlaceMarkers(System.Collections.Generic.List<IAirStrike> strikes)
        {
            var cam = Camera.main;
            var canvas = transform.parent as RectTransform;
            int n = 0;
            if (cam != null && canvas != null)
                foreach (var s in strikes)
                {
                    if (n >= 4) break;
                    var m = Marker(n++, canvas);
                    Vector3 sp = cam.WorldToScreenPoint(s.DangerPoint);
                    bool behind = sp.z < 0f;
                    if (behind) sp = -sp;
                    float pad = 70f;
                    bool inside = !behind && sp.x > pad && sp.x < Screen.width - pad && sp.y > pad && sp.y < Screen.height - pad;
                    if (!inside)
                    {
                        Vector2 c = new(Screen.width * 0.5f, Screen.height * 0.5f);
                        Vector2 d = (Vector2)sp - c;
                        if (d.sqrMagnitude < 1f) d = Vector2.up;
                        float k = Mathf.Min((Screen.width * 0.5f - pad) / Mathf.Max(1f, Mathf.Abs(d.x)), (Screen.height * 0.5f - pad) / Mathf.Max(1f, Mathf.Abs(d.y)));
                        sp = c + d * Mathf.Min(1f, k);
                    }
                    if (inside) sp.y += 46f;   // 화면 안이면 원 위쪽에(붉은 원 위에 글자가 겹치지 않게)
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, null, out var local);
                    m.rectTransform.anchoredPosition = local;
                    bool dive = s is Ww2DiveBomber;
                    m.color = dive ? new Color(1f, 0.32f, 0.25f) : new Color(1f, 0.62f, 0.2f);
                    m.text = $"{(inside ? "◎" : "▲")} {(dive ? "급강하" : "뇌격")} {s.SecondsToRelease:0.0}초";
                    m.gameObject.SetActive(true);
                }
            for (int i = n; i < _markers.Count; i++) _markers[i].gameObject.SetActive(false);
        }

        private TMP_Text Marker(int index, RectTransform canvas)
        {
            while (_markers.Count <= index)
            {
                var go = new GameObject($"WW2 strike marker {_markers.Count}", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(canvas, false);
                var t = go.GetComponent<TextMeshProUGUI>();
                t.font = _font;
                t.fontSize = 18f;
                t.fontStyle = FontStyles.Bold;
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color32(0, 0, 0, 200);
                var r = t.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(180f, 28f);
                _markers.Add(t);
            }
            return _markers[index];
        }

        private GameManager gm => GameManager.Instance;

        private static string Side(Ww2TorpedoControl control, Ww2Side side, Ww2Control key)
        {
            var (loaded, tubes, reload, mounts) = control.Status(side);
            string name = side == Ww2Side.Port ? "좌현" : "우현";
            string k = $"<color=#e0c05a>[{Ww2Controls.BindingLabel(key)}]</color>";
            if (mounts == 0) return $"{k} {name} <color=#8a9aa0>발사관 없음</color>";
            if (loaded > 0) return $"{k} {name} 어뢰 <b>{loaded}</b>/{tubes}";
            return $"{k} {name} <color=#ff8a6a>재장전 {reload:0}초</color>";
        }

        /// <summary>기함 기준 방향(좌현·우현·앞·뒤).</summary>
        public static string Bearing(Transform ship, Vector3 point)
        {
            if (ship == null) return "";
            Vector3 to = point - ship.position;
            to.y = 0f;
            Vector3 fwd = ship.forward;
            fwd.y = 0f;
            float a = Vector3.SignedAngle(fwd, to, Vector3.up);
            if (Mathf.Abs(a) <= 35f) return "앞";
            if (Mathf.Abs(a) >= 145f) return "뒤";
            return a < 0f ? "좌현" : "우현";
        }
    }
}
