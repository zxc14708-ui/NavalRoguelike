using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 지원 블록의 시간 표시(2026-11-22): 전투단 통신 중계 · 포격 지원 요청 · 정찰 드론 격납고 · 응급 차폐 장갑.
    /// 블록이 스스로 작동하므로 "다음 지원까지 몇 초"와 "지금 작동 중"만 보여 준다.
    /// </summary>
    public interface ISupportTimer
    {
        /// <summary>표시 이름(예: "지원 함정").</summary>
        string TimerLabel { get; }
        /// <summary>한 줄 상태(예: "다음 호출 12초", "도착 · 남은 9초"). null이면 줄을 숨긴다.</summary>
        string TimerStatus { get; }
        /// <summary>지금 작동 중이면 밝게.</summary>
        bool TimerActive { get; }
    }

    /// <summary>지원 블록 시간 목록. 블록이 켜질 때 넣고 꺼질 때 뺀다. HUD 왼쪽 아래(레이더 위) 작은 판이 읽는다.</summary>
    public static class SupportTimers
    {
        private static readonly List<ISupportTimer> s_list = new();
        public static IReadOnlyList<ISupportTimer> All => s_list;

        public static void Register(ISupportTimer t)
        {
            if (t == null || s_list.Contains(t)) return;
            s_list.Add(t);
            SupportTimersPanel.Ensure();
        }

        public static void Unregister(ISupportTimer t) => s_list.Remove(t);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_list.Clear();
    }

    /// <summary>HUD 판: "SUPPORT / 지원" 머리 + 블록마다 한 줄. 지원 블록이 없으면 숨는다.</summary>
    public sealed class SupportTimersPanel : MonoBehaviour
    {
        private static SupportTimersPanel s_instance;
        private RectTransform _rect;
        private TextMeshProUGUI _text;
        private Image _bg;
        private float _next;
        private readonly StringBuilder _sb = new();

        /// <summary>검증용: 지금 판의 글.</summary>
        public static string Text => s_instance != null && s_instance._text != null ? s_instance._text.text : "";
        public static bool Visible => s_instance != null && s_instance._bg != null && s_instance._bg.enabled;

        public static void Ensure()
        {
            if (s_instance != null) return;
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (hud == null) return;
            var go = new GameObject("Support timers", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hud.transform, false);
            s_instance = go.AddComponent<SupportTimersPanel>();
            s_instance.Build(hud);
        }

        private void Build(Game.UI.HUDView hud)
        {
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = Vector2.zero;
            _rect.anchoredPosition = new Vector2(14f, 214f);   // 레이더 · 선체 판 위
            _rect.sizeDelta = new Vector2(392f, 40f);
            _bg = GetComponent<Image>();
            _bg.color = new Color(0.02f, 0.07f, 0.09f, 0.78f);
            _bg.raycastTarget = false;

            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(_rect, false);
            _text = label.GetComponent<TextMeshProUGUI>();
            _text.font = hud.Font;
            _text.fontSize = 15;
            _text.color = new Color(0.78f, 0.9f, 0.92f);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.raycastTarget = false;
            _text.richText = true;
            _text.textWrappingMode = TextWrappingModes.NoWrap;
            var r = _text.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(10f, 4f); r.offsetMax = new Vector2(-8f, -5f);
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            var list = SupportTimers.All;
            _sb.Clear();
            _sb.Append("<color=#3fd0c8><size=12>SUPPORT / 지원</size></color>");
            int lines = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                if (t == null || (t is Object o && o == null)) { SupportTimers.Unregister(t); continue; }
            }
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                string status = t.TimerStatus;
                if (status == null) continue;
                _sb.Append('\n');
                _sb.Append(t.TimerActive ? "<color=#7dffb0>" : "<color=#c8d4dc>");
                _sb.Append(t.TimerLabel).Append("</color>  ").Append(status);
                lines++;
            }
            bool show = lines > 0 && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;
            _bg.enabled = show;
            _text.enabled = show;
            if (!show) return;
            _text.text = _sb.ToString();
            _rect.sizeDelta = new Vector2(392f, 26f + lines * 19f);
        }

        private void OnDestroy() { if (s_instance == this) s_instance = null; }
    }
}
