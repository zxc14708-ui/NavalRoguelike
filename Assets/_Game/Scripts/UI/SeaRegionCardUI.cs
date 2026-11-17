using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.World;

namespace Game.UI
{
    /// <summary>
    /// 해역 안내 카드(2026-10-11 점검 K04). 스테이지가 시작돼 해역이 바뀌면 화면 위 가운데에 7초 동안
    /// "제2 해역 · 좁은 수로"와 전술 한 줄을 띄웠다가 사라진다. 해역 이름은 그 뒤에도 구간 제목 앞에 붙어 남는다.
    /// </summary>
    public sealed class SeaRegionCardUI : MonoBehaviour
    {
        private const float ShowSeconds = 7f, FadeSeconds = 0.8f;
        private CanvasGroup _group;
        private TMP_Text _title, _body;
        private float _shownAt = -99f;

        /// <summary>검증용: 지금 보이는가와 그 글.</summary>
        public bool Visible => _group != null && _group.alpha > 0.01f;
        public string Text => _title != null ? $"{_title.text} {_body.text}" : "";

        public static SeaRegionCardUI Create(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Sea region card", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -122f);
            rect.sizeDelta = new Vector2(820f, 78f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.015f, 0.06f, 0.075f, 0.86f);
            bg.raycastTarget = false;
            var ui = go.AddComponent<SeaRegionCardUI>();
            ui._group = go.GetComponent<CanvasGroup>();
            ui._group.alpha = 0f;
            ui._group.blocksRaycasts = false;
            ui._title = ui.Label(rect, font, new Vector2(0f, -6f), 24f, new Color(1f, 0.85f, 0.45f), 34f);
            ui._body = ui.Label(rect, font, new Vector2(0f, -40f), 16f, new Color(0.78f, 0.9f, 0.88f), 34f);
            ui._body.textWrappingMode = TextWrappingModes.Normal;
            return ui;
        }

        private TMP_Text Label(RectTransform parent, TMP_FontAsset font, Vector2 pos, float size, Color color, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(790f, height);
            return t;
        }

        private void OnEnable() => SeaRegion.Changed += OnChanged;
        private void OnDisable() => SeaRegion.Changed -= OnChanged;

        private void OnChanged(SeaKind kind)
        {
            if (!SeaRegion.Active) { _shownAt = -99f; return; }
            var p = SeaRegion.For(kind);
            _title.text = p.Title;
            _body.text = p.Tactics;
            _shownAt = Time.unscaledTime;
            transform.SetAsLastSibling();
        }

        private void Update()
        {
            float age = Time.unscaledTime - _shownAt;
            _group.alpha = age < ShowSeconds ? Mathf.Clamp01(age / 0.3f) : Mathf.Clamp01(1f - (age - ShowSeconds) / FadeSeconds);
        }
    }
}
