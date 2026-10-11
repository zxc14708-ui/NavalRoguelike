using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 보스 막대(2026-10-11 점검 K05). 보스가 나와 있는 동안 화면 위 가운데(진행·알림 아래)에:
    ///   이름 · 체력 막대(단계가 바뀌는 지점에 눈금, 지난 눈금은 흐리게) · 지금 단계 · "다음: 미사일 일제사격 3.2초" · 대응 한 줄(사전과 같은 글).
    /// 다음 공격이 2초 안이면 붉게 깜빡인다(번쩍임 줄이기면 깜빡이지 않고 붉게만). 첫 패배 뒤 대응법을 미리 읽게 하는 것이 목적.
    /// </summary>
    public sealed class BossBarUI : MonoBehaviour
    {
        private const float Width = 620f;
        private static readonly Color Panel = new(0.025f, 0.065f, 0.10f, 0.82f);
        private static readonly Color Fill = new(0.92f, 0.32f, 0.24f, 1f);
        private static readonly Color Track = new(0.15f, 0.2f, 0.24f, 1f);
        private static readonly Color Gold = new(0.98f, 0.73f, 0.28f);

        private RectTransform _panel, _fill, _ticks;
        private TMP_Text _name, _phase, _next, _tip;
        private EnemyController _boss;
        private float _poll;

        /// <summary>검증용: 지금 따라가는 보스와 글.</summary>
        public EnemyController Boss => _boss;
        public string NextText => _next != null ? _next.text : "";
        public string PhaseText => _phase != null ? _phase.text : "";
        public bool Shown => _panelHolder != null && _panelHolder.GetComponent<Image>().enabled;

        public static BossBarUI Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Boss bar", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var ui = go.AddComponent<BossBarUI>();
            ui.Build((RectTransform)go.transform, font);
            return ui;
        }

        private void Build(RectTransform root, TMP_FontAsset font)
        {
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -112f);
            root.sizeDelta = new Vector2(Width, 92f);
            _panel = root;
            var bg = root.gameObject.AddComponent<Image>();
            bg.color = Panel;
            bg.raycastTarget = false;

            _name = Label(root, font, "Name", new Vector2(12, -6), new Vector2(300, 24), 18, Color.white, TextAlignmentOptions.TopLeft);
            _phase = Label(root, font, "Phase", new Vector2(Width - 12, -8), new Vector2(330, 22), 14, Gold, TextAlignmentOptions.TopRight);
            var track = Rect(root, "Track", new Vector2(12, -34), new Vector2(Width - 24, 10), Track);
            _fill = Rect(track, "Fill", Vector2.zero, new Vector2(Width - 24, 10), Fill);
            _fill.anchorMin = _fill.anchorMax = _fill.pivot = new Vector2(0f, 1f);
            _ticks = Rect(track, "Ticks", Vector2.zero, new Vector2(Width - 24, 10), Color.clear);
            _next = Label(root, font, "Next", new Vector2(12, -48), new Vector2(Width - 24, 20), 15, Color.white, TextAlignmentOptions.TopLeft);
            _tip = Label(root, font, "Tip", new Vector2(12, -68), new Vector2(Width - 24, 20), 13, new Color(0.66f, 1f, 0.75f), TextAlignmentOptions.TopLeft);
            _tip.overflowMode = TextOverflowModes.Ellipsis;
            _tip.textWrappingMode = TextWrappingModes.NoWrap;
            _panelHolder = root.gameObject;
        }

        private GameObject _panelHolder;
        private string _lastPhase;
        private bool _soonAnnounced;

        private void Update()
        {
            _poll -= Time.unscaledDeltaTime;
            if (_poll <= 0f)
            {
                _poll = 0.25f;
                if (_boss == null || !_boss.IsAlive || !_boss.isActiveAndEnabled) Find();
            }
            var gm = GameManager.Instance;
            bool show = _boss != null && _boss.IsAlive && gm != null && gm.State == GameState.Playing;
            // 패널은 자기 오브젝트라 SetActive(false)면 Update가 멈춘다 — 자식만 켜고 끈다
            foreach (Transform child in _panelHolder.transform) if (child.gameObject.activeSelf != show) child.gameObject.SetActive(show);
            var bg = _panelHolder.GetComponent<Image>();
            if (bg.enabled != show) bg.enabled = show;
            if (!show) return;

            var def = _boss.Definition;
            float hp = def != null && def.MaxHp > 0f ? Mathf.Clamp01(_boss.CurrentHp / def.MaxHp) : 0f;
            _fill.sizeDelta = new Vector2((Width - 24) * hp, 10f);
            _name.text = $"<color=#ff6b5a>보스</color>  {def?.DisplayName}  <size=80%><color=#9fb3c0>{hp * 100f:0}%</color></size>";

            if (_boss is IBossTelegraph t)
            {
                // 소리 단서(점검 O06): 단계가 바뀌면 내려가는 세 음, 다음 공격 2초 전 삑 한 번(글자도 붉게 바뀐다)
                if (_lastPhase != null && _lastPhase != t.PhaseName) UiSound.Play(UiSound.Cue.BossPhase);
                _lastPhase = t.PhaseName;
                _phase.text = t.PhaseName;
                var (name, sec) = t.NextAttack;
                bool soon = name != null && sec < 2f;
                if (soon && !_soonAnnounced) { _soonAnnounced = true; UiSound.Play(UiSound.Cue.AttackSoon, 0.7f); }
                else if (!soon && (name == null || sec > 2.5f)) _soonAnnounced = false;
                bool blink = soon && !DisplaySettings.ReduceFlashes && Mathf.Repeat(Time.unscaledTime, 0.5f) < 0.25f;
                _next.text = name == null ? "<color=#9fb3c0>지금 거리에서 쓸 공격 없음</color>"
                    : $"다음  <b>{name}</b>  <color={(soon ? (blink ? "#ffffff" : "#ff6b5a") : "#e0c05a")}>{(sec < 0.1f ? "곧" : $"{sec:0.0}초")}</color>";
            }
            else { _phase.text = ""; _next.text = ""; }
        }

        private void Find()
        {
            var previous = _boss;
            _boss = null;
            _lastPhase = null;
            _soonAnnounced = false;
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
                if (list[i] is EnemyController e && e.IsAlive && e.Definition != null && e.Definition.Rank == EnemyRank.Boss) { _boss = e; break; }
            if (_boss == null) return;
            if (_boss != previous) UiSound.Play(UiSound.Cue.BossAlert);   // 보스 등장(배너 글과 함께)
            string tip = CodexCatalog.CounterTip(_boss.Definition);
            if (!string.IsNullOrEmpty(tip))
            {
                int dot = tip.IndexOf(". ", System.StringComparison.Ordinal);
                if (dot > 0 && dot < 80) tip = tip.Substring(0, dot + 1);
            }
            _tip.text = string.IsNullOrEmpty(tip) ? "" : $"대응  {tip}";
            BuildTicks();
        }

        private void BuildTicks()
        {
            foreach (Transform c in _ticks) Destroy(c.gameObject);
            if (_boss is not IBossTelegraph t || t.PhaseThresholds == null) return;
            foreach (float f in t.PhaseThresholds)
            {
                var tick = Rect(_ticks, "Tick", new Vector2((Width - 24) * f - 1f, 3f), new Vector2(2f, 16f), Color.white);
                tick.anchorMin = tick.anchorMax = tick.pivot = new Vector2(0f, 1f);
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return r;
        }

        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, Vector2 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            var r = t.rectTransform;
            bool right = align == TextAlignmentOptions.TopRight;
            r.anchorMin = r.anchorMax = r.pivot = right ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            r.anchoredPosition = right ? new Vector2(pos.x - Width, pos.y) : pos;
            r.sizeDelta = size;
            return t;
        }
    }
}
