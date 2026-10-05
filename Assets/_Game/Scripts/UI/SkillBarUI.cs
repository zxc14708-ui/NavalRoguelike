using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 액티브 스킬 줄(레벨 막대 아래, 레이더·함 현황·무장 패널 위). 글자 대신 그림으로 상태를 보여 준다.
    ///   - 아이콘: 기만체(채프 섬광) · 재밍(전파) · 응급 수리(렌치) · 연막(구름) · 전속(겹 화살표)
    ///   - 준비: 아이콘이 밝고 위쪽에 청록 줄, 아래 점 = 쓸 수 있는 횟수(채운 점)와 최대(빈 점)
    ///   - 재장전: 아이콘이 어둡게 깔리고 그 위로 밝은 아이콘이 시계 방향으로 차오른다 + 아래 진행 막대
    ///   - 장비 없음: 아주 흐린 회색 아이콘에 사선
    /// 왼쪽 위 작은 글자는 단축키. 타일을 눌러도 쓴다.
    /// </summary>
    public class SkillBarUI : MonoBehaviour
    {
        public const float TileGap = 8f, Padding = 8f;
        private const int MaxPips = 4;

        private static readonly Color TileIdle = new(0.06f, 0.13f, 0.17f, 1f);
        private static readonly Color TileReady = new(0.07f, 0.21f, 0.25f, 1f);
        private static readonly Color Dim = new(0.62f, 0.72f, 0.76f, 0.75f);
        private static readonly Color Charging = new(1f, 0.78f, 0.38f, 1f);
        private static readonly Color Missing = new(0.45f, 0.5f, 0.53f, 0.25f);

        private sealed class Tile
        {
            public Button Button;
            public Image Background, Base, Fill, Accent, Slash;
            public RectTransform ProgressFill;
            public float ProgressWidth;
            public TMP_Text Key;
            public Image[] Pips;
        }

        private Tile[] _tiles;
        private Color _accent, _gold;

        public static SkillBarUI Create(Transform parent, TMP_FontAsset font, Color panelColor, Color accent, Color gold,
                                        Vector2 position, Vector2 size, (HudTextures.SkillIcon icon, string key)[] skills)
        {
            var panel = Rect("Skills", parent, position, size);
            var bg = panel.gameObject.AddComponent<Image>();
            bg.color = panelColor;
            bg.raycastTarget = false;
            panel.gameObject.AddComponent<Canvas>();
            panel.gameObject.AddComponent<GraphicRaycaster>();   // 하위 캔버스는 자기 레이캐스터가 있어야 버튼이 눌린다

            var ui = panel.gameObject.AddComponent<SkillBarUI>();
            ui._accent = accent;
            ui._gold = gold;
            ui.Build(panel, font, size, skills);
            return ui;
        }

        public Button ButtonAt(int index) => _tiles != null && index >= 0 && index < _tiles.Length ? _tiles[index].Button : null;
        public void SetKeyLabel(int index, string label)
        {
            if (_tiles != null && index >= 0 && index < _tiles.Length && _tiles[index].Key != null)
                _tiles[index].Key.text = label;
        }

        private void Build(RectTransform panel, TMP_FontAsset font, Vector2 size, (HudTextures.SkillIcon icon, string key)[] skills)
        {
            int n = skills.Length;
            float tileW = (size.x - Padding * 2f - TileGap * (n - 1)) / n;
            float tileH = size.y - Padding * 2f;
            float iconSize = Mathf.Min(tileH - 16f, 44f);
            _tiles = new Tile[n];

            for (int i = 0; i < n; i++)
            {
                var root = Rect(skills[i].icon.ToString(), panel, new Vector2(Padding + i * (tileW + TileGap), Padding), new Vector2(tileW, tileH));
                var tile = new Tile();
                tile.Background = root.gameObject.AddComponent<Image>();
                tile.Background.color = TileIdle;
                tile.Button = root.gameObject.AddComponent<Button>();
                tile.Button.targetGraphic = tile.Background;
                tile.Button.transition = Selectable.Transition.None;
                tile.Button.interactable = false;

                tile.Accent = Img(root, "Ready line", null, new Vector2(0, tileH - 2f), new Vector2(tileW, 2f), accent: true);

                var sprite = HudTextures.SkillIconSprite(skills[i].icon);
                var iconPos = new Vector2((tileW - iconSize) * 0.5f, (tileH - iconSize) * 0.5f + 4f);
                tile.Base = Img(root, "Icon", sprite, iconPos, new Vector2(iconSize, iconSize));
                tile.Fill = Img(root, "Icon charge", sprite, iconPos, new Vector2(iconSize, iconSize));
                tile.Fill.type = Image.Type.Filled;
                tile.Fill.fillMethod = Image.FillMethod.Radial360;
                tile.Fill.fillOrigin = (int)Image.Origin360.Top;
                tile.Fill.fillClockwise = true;
                tile.Slash = Img(root, "Unavailable", null, new Vector2(tileW * 0.5f - 1f, (tileH - iconSize) * 0.5f), new Vector2(2f, iconSize + 8f));
                tile.Slash.rectTransform.pivot = new Vector2(0.5f, 0f);
                tile.Slash.rectTransform.anchoredPosition = new Vector2(tileW * 0.5f, (tileH - iconSize) * 0.5f);
                tile.Slash.rectTransform.localRotation = Quaternion.Euler(0, 0, -40f);
                tile.Slash.color = Missing;

                tile.Key = Label(root, font, "Key", skills[i].key, new Vector2(6, tileH - 18f), new Vector2(60, 16), 12, _gold);

                // 준비 횟수 점
                tile.Pips = new Image[MaxPips];
                for (int k = 0; k < MaxPips; k++)
                {
                    tile.Pips[k] = Img(root, $"Pip {k}", null, Vector2.zero, new Vector2(6, 6));
                    tile.Pips[k].gameObject.SetActive(false);
                }

                // 다음 충전 진행 막대(맨 아래)
                var track = Img(root, "Progress", null, new Vector2(6, 3), new Vector2(tileW - 12f, 2f));
                track.color = new Color(0.15f, 0.22f, 0.26f, 1f);
                var fill = Img(track.rectTransform, "Fill", null, Vector2.zero, new Vector2(0, 2f));
                fill.color = Charging;
                tile.ProgressFill = fill.rectTransform;
                tile.ProgressWidth = tileW - 12f;

                _tiles[i] = tile;
                Set(i, 0, 0, 0f);
            }
        }

        /// <summary>스킬 상태: 장비 없음(total 0) / 준비 ready개 / 다음 충전 진행(0~1).</summary>
        public void Set(int index, int ready, int total, float next01)
        {
            if (_tiles == null || index < 0 || index >= _tiles.Length) return;
            var t = _tiles[index];
            bool equipped = total > 0;
            bool usable = equipped && ready > 0;
            bool charging = equipped && ready < total;

            t.Button.interactable = usable;
            t.Background.color = usable ? TileReady : TileIdle;
            t.Accent.gameObject.SetActive(usable);
            t.Slash.gameObject.SetActive(!equipped);
            t.Key.color = equipped ? _gold : Missing;

            // 아이콘: 준비면 밝게, 재장전이면 어두운 바탕 위로 차오름, 장비 없음이면 흐리게
            t.Base.color = !equipped ? Missing : usable ? Color.white : new Color(1f, 1f, 1f, 0.18f);
            t.Fill.gameObject.SetActive(equipped && !usable);
            t.Fill.fillAmount = Mathf.Clamp01(next01);
            t.Fill.color = Charging;

            // 점: 채운 점 = 준비, 빈 점 = 재장전 중인 몫
            int pips = equipped ? Mathf.Min(total, MaxPips) : 0;
            float tileW = t.ProgressWidth + 12f;
            float startX = tileW * 0.5f - (pips * 10f - 4f) * 0.5f;
            for (int k = 0; k < MaxPips; k++)
            {
                var pip = t.Pips[k];
                bool on = k < pips;
                pip.gameObject.SetActive(on && total > 1);
                if (!on) continue;
                pip.rectTransform.anchoredPosition = new Vector2(startX + k * 10f, 8f);
                pip.color = k < ready ? _accent : new Color(_accent.r, _accent.g, _accent.b, 0.22f);
            }

            float progress = charging ? Mathf.Clamp01(next01) : 0f;
            t.ProgressFill.sizeDelta = new Vector2(t.ProgressWidth * progress, 2f);
            t.ProgressFill.parent.gameObject.SetActive(charging);
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

        private Image Img(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, bool accent = false)
        {
            var rect = Rect(name, parent, position, size);
            var img = rect.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = sprite != null;
            img.color = accent ? _accent : Color.white;
            img.raycastTarget = false;
            return img;
        }

        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string value, Vector2 position, Vector2 size, float fontSize, Color color)
        {
            var rect = Rect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
