using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Game.Combat;
using Game.Core;
using Game.Enemies;

namespace Game.UI
{
    /// <summary>
    /// 전투 숫자(2026-11-17). 카드의 공격력이 적 체력에 비해 얼마나 되는지 감이 오게:
    ///   - 피해 숫자: 적이 받은 피해를 MergeSeconds 동안 합쳐 하나로 띄운다(기관포처럼 빠른 무장도 숫자가 쏟아지지 않게).
    ///     격침한 한 방은 주황색에 "격침".
    ///   - 체력 숫자: 마우스를 올린 적(없으면 방금 맞힌 적 하나) 머리 위에 "이름 48 / 160"과 체력 막대. 보스는 보스 막대가 맡아 뺀다.
    /// 설정 › 그래픽 › 전투 숫자에서 끈다(DisplaySettings.CombatNumbers). 적대 진영이 받은 피해만 센다(EnemyController.TakeDamage).
    /// </summary>
    public sealed class CombatNumbersUI : MonoBehaviour
    {
        public const float MergeSeconds = 0.3f, FloatSeconds = 0.9f, FloatRise = 46f;
        public const float HoverPixels = 70f, RecentSeconds = 2f;
        private const int MaxFloaters = 40;

        private static CombatNumbersUI s_instance;
        private static readonly Color NumberColor = new(1f, 0.82f, 0.4f);
        private static readonly Color KillColor = new(1f, 0.54f, 0.42f);

        private RectTransform _root;
        private TMP_FontAsset _font;
        private Camera _camera;

        private sealed class Pending { public float Sum, Since; public Vector3 Where; public bool Killed; }
        private readonly Dictionary<EnemyController, Pending> _pending = new();
        private readonly List<EnemyController> _flush = new();

        private sealed class Floater { public TextMeshProUGUI Text; public Vector3 World; public float Age; public bool Live; }
        private readonly List<Floater> _floaters = new();

        private RectTransform _hpRoot;
        private TextMeshProUGUI _hpText;
        private RectTransform _hpFill;
        private EnemyController _recent;
        private float _recentAt = -10f;

        /// <summary>검증용: 띄운 피해 숫자 수 · 마지막 숫자 글 · 체력 표시 글(꺼져 있으면 "").</summary>
        public static int Shown { get; private set; }
        public static string LastText { get; private set; } = "";
        public string HpText => _hpRoot != null && _hpRoot.gameObject.activeSelf && _hpText != null ? _hpText.text ?? "" : "";
        public static CombatNumbersUI Instance => s_instance;

        public static CombatNumbersUI Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Combat numbers", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsFirstSibling();   // 다른 HUD 패널 밑에 그린다
            var ui = go.AddComponent<CombatNumbersUI>();
            ui._root = rect;
            ui._font = font;
            ui.BuildHp();
            s_instance = ui;
            return ui;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        /// <summary>적이 피해를 받았다(EnemyController가 부른다). amount = 실제로 깎인 체력.</summary>
        public static void Note(EnemyController enemy, float amount, bool killed)
        {
            var ui = s_instance;
            if (ui == null || enemy == null || amount <= 0f || !DisplaySettings.CombatNumbers) return;
            if (!ui._pending.TryGetValue(enemy, out var p))
            {
                p = new Pending { Since = Time.time };
                ui._pending[enemy] = p;
            }
            p.Sum += amount;
            p.Where = enemy.transform.position;
            p.Killed |= killed;
            ui._recent = killed ? null : enemy;
            ui._recentAt = Time.time;
        }

        private void LateUpdate()
        {
            var gm = GameManager.Instance;
            if (_camera == null) _camera = Camera.main;
            bool on = DisplaySettings.CombatNumbers && gm != null && gm.State == GameState.Playing && _camera != null;
            if (!on)
            {
                _pending.Clear();
                foreach (var f in _floaters) if (f.Live) { f.Live = false; f.Text.gameObject.SetActive(false); }
                if (_hpRoot != null && _hpRoot.gameObject.activeSelf) _hpRoot.gameObject.SetActive(false);
                return;
            }
            float scale = _root.lossyScale.x > 0f ? 1f / _root.lossyScale.x : 1f;   // 화면 픽셀 → 캔버스 단위

            // 합친 피해를 띄운다
            _flush.Clear();
            foreach (var kv in _pending)
                if (kv.Value.Killed || Time.time - kv.Value.Since >= MergeSeconds) _flush.Add(kv.Key);
            foreach (var e in _flush)
            {
                var p = _pending[e];
                _pending.Remove(e);
                Spawn(p);
            }

            // 떠오르며 사라진다
            foreach (var f in _floaters)
            {
                if (!f.Live) continue;
                f.Age += Time.deltaTime;
                if (f.Age >= FloatSeconds) { f.Live = false; f.Text.gameObject.SetActive(false); continue; }
                Vector3 sp = _camera.WorldToScreenPoint(f.World);
                if (sp.z < 0f) { f.Text.gameObject.SetActive(false); continue; }
                f.Text.gameObject.SetActive(true);
                float k = f.Age / FloatSeconds;
                f.Text.rectTransform.anchoredPosition = new Vector2(sp.x, sp.y) * scale + new Vector2(40f, 18f + FloatRise * k);   // 체력 막대(머리 위 가운데)와 겹치지 않게 오른쪽에서
                var c = f.Text.color;
                c.a = k < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.6f) / 0.4f);
                f.Text.color = c;
            }

            RefreshHp(scale);
        }

        private void Spawn(Pending p)
        {
            Floater f = null;
            foreach (var x in _floaters) if (!x.Live) { f = x; break; }
            if (f == null)
            {
                if (_floaters.Count >= MaxFloaters) return;
                f = new Floater { Text = MakeText("Damage number", _root, 24) };
                f.Text.fontStyle = FontStyles.Bold;
                f.Text.outlineWidth = 0.22f;
                f.Text.outlineColor = new Color32(0, 0, 0, 200);
                _floaters.Add(f);
            }
            string num = p.Sum >= 10f ? $"{p.Sum:0}" : $"{p.Sum:0.#}";
            f.Text.text = p.Killed ? $"{num} <size=75%>격침</size>" : num;
            f.Text.color = p.Killed ? KillColor : NumberColor;
            f.Text.fontSize = p.Killed ? 26 : p.Sum >= 20f ? 25 : 21;
            // 여러 숫자가 겹치지 않게 조금씩 흩는다
            f.World = p.Where + Vector3.up * 2f + new Vector3(Random.Range(-1.2f, 1.2f), 0f, Random.Range(-0.6f, 0.6f));
            f.Age = 0f;
            f.Live = true;
            f.Text.gameObject.SetActive(true);
            Shown++;
            LastText = f.Text.text;
        }

        // ------------------------------------------------------------ 체력 숫자

        private void BuildHp()
        {
            var go = new GameObject("Enemy hp", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            _hpRoot = (RectTransform)go.transform;
            _hpRoot.anchorMin = _hpRoot.anchorMax = Vector2.zero;
            _hpRoot.pivot = new Vector2(0.5f, 0f);
            _hpRoot.sizeDelta = new Vector2(150f, 34f);
            _hpText = MakeText("Hp text", _hpRoot, 16);
            var tr = _hpText.rectTransform;
            tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = new Vector2(220f, 22f);
            _hpText.outlineWidth = 0.2f;
            _hpText.outlineColor = new Color32(0, 0, 0, 200);

            var back = new GameObject("Hp back", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(_hpRoot, false);
            var br = (RectTransform)back.transform;
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0f);
            br.anchoredPosition = new Vector2(0f, 2f);
            br.sizeDelta = new Vector2(110f, 7f);
            var bi = back.GetComponent<Image>();
            bi.color = new Color(0f, 0f, 0f, 0.65f);
            bi.raycastTarget = false;
            var fill = new GameObject("Hp fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(br, false);
            _hpFill = (RectTransform)fill.transform;
            _hpFill.anchorMin = _hpFill.anchorMax = _hpFill.pivot = new Vector2(0f, 0.5f);
            _hpFill.anchoredPosition = Vector2.zero;
            _hpFill.sizeDelta = new Vector2(110f, 7f);
            var fi = fill.GetComponent<Image>();
            fi.color = new Color(0.88f, 0.34f, 0.29f);
            fi.raycastTarget = false;
            go.SetActive(false);
        }

        private void RefreshHp(float scale)
        {
            var target = Hovered(scale);
            if (target == null && _recent != null && Time.time - _recentAt <= RecentSeconds && Showable(_recent)) target = _recent;
            if (target == null)
            {
                if (_hpRoot.gameObject.activeSelf) _hpRoot.gameObject.SetActive(false);
                return;
            }
            Vector3 sp = _camera.WorldToScreenPoint(target.transform.position + Vector3.up * HeadHeight(target));
            if (sp.z < 0f) { _hpRoot.gameObject.SetActive(false); return; }
            if (!_hpRoot.gameObject.activeSelf) _hpRoot.gameObject.SetActive(true);
            _hpRoot.anchoredPosition = new Vector2(sp.x, sp.y) * scale + new Vector2(0f, 14f);
            float max = Mathf.Max(1f, target.Definition.MaxHp), hp = Mathf.Max(0f, target.CurrentHp);
            string hpNum = hp > 0f && hp < 1f ? "1" : $"{Mathf.Ceil(hp):0}";
            _hpText.text = $"<color=#c8d4dc>{target.Definition.DisplayName}</color>  <b>{hpNum}</b><color=#8fa4b8> / {max:0}</color>";
            _hpFill.sizeDelta = new Vector2(110f * Mathf.Clamp01(hp / max), 7f);
        }

        /// <summary>마우스에 가장 가까운 적(화면 HoverPixels 안).</summary>
        private EnemyController Hovered(float scale)
        {
            var mouse = Mouse.current;
            if (mouse == null) return null;
            Vector2 m = mouse.position.ReadValue();
            float best = HoverPixels * HoverPixels;
            EnemyController found = null;
            foreach (var kind in Kinds)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is not EnemyController e || !Showable(e)) continue;
                    Vector3 sp = _camera.WorldToScreenPoint(e.transform.position);
                    if (sp.z < 0f) continue;
                    float d = ((Vector2)sp - m).sqrMagnitude;
                    if (d < best) { best = d; found = e; }
                }
            }
            return found;
        }

        private static readonly TargetKind[] Kinds = { TargetKind.Surface, TargetKind.Aircraft, TargetKind.Submarine };

        private static bool Showable(EnemyController e)
            => e != null && e.isActiveAndEnabled && e.IsAlive && e.IsRevealed && e.Definition != null && e is not IBossParts && e is not SeaMine;

        private static float HeadHeight(EnemyController e) => e.Kind switch
        {
            TargetKind.Aircraft => 1.6f,
            TargetKind.Submarine => 1.4f,
            _ => 2.6f,
        };

        private TextMeshProUGUI MakeText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.richText = true;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(160f, 34f);
            return t;
        }
    }
}
