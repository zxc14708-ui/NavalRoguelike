using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 선체 위험 경고. 미사일 한두 발로 급사하기 전에 화면만 봐도 알 수 있게 한다.
    ///   - 선체 35% 이하 "위험": 화면 가장자리가 붉게 맥박친다(1.1Hz) + 상단 경고 띠 "선체 위험 N%".
    ///   - 선체 20% 이하 "치명": 더 진하고 빠르게(1.9Hz) + "선체 치명 N% — 회피·수리".
    ///   - 한 번에 최대 선체의 8% 이상을 잃으면(대함미사일·어뢰 명중 등) 선체와 관계없이 붉은 번쩍임.
    /// HUD 루트 캔버스의 맨 뒤에 깔려 HUD 콘솔을 가리지 않고, 입력을 막지 않는다. 전투 중(Playing)에만 보인다.
    /// </summary>
    public sealed class HullDangerOverlay : MonoBehaviour
    {
        public const float DangerFraction = 0.35f, CriticalFraction = 0.20f, BigHitFraction = 0.08f;

        public static HullDangerOverlay Instance { get; private set; }

        /// <summary>0 = 정상, 1 = 위험, 2 = 치명.</summary>
        public int Level { get; private set; }
        /// <summary>지금 가장자리 붉은빛의 불투명도(검증용).</summary>
        public float VignetteAlpha => _vignette != null ? _vignette.color.a : 0f;
        public bool BannerVisible => _banner != null && _banner.gameObject.activeSelf;
        public int BigHitCount { get; private set; }

        private Image _vignette;
        private RectTransform _banner;
        private Image _bannerBack;
        private TMP_Text _bannerText;
        private float _hull01 = 1f, _lastHp = -1f;
        private float _flash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("Hull danger overlay").AddComponent<HullDangerOverlay>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable() => GameEvents.HullHpChanged += OnHullChanged;
        private void OnDisable() => GameEvents.HullHpChanged -= OnHullChanged;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnHullChanged(float current, float max)
        {
            if (max <= 0f) return;
            if (_lastHp >= 0f && _lastHp - current >= max * BigHitFraction)
            {
                _flash = 1.3f;   // 1 위는 최대 밝기로 잠깐 유지(약 0.15초) 뒤 사라진다
                BigHitCount++;
            }
            _lastHp = current;
            _hull01 = Mathf.Clamp01(current / max);
            Level = _hull01 <= CriticalFraction ? 2 : _hull01 <= DangerFraction ? 1 : 0;
        }

        /// <summary>HUD 루트 캔버스 맨 뒤에 가장자리 이미지와 경고 띠를 만든다(HUD가 늦게 생기면 다음 프레임에 다시 본다).</summary>
        private bool EnsureBuilt()
        {
            if (_vignette != null) return true;
            var hud = Object.FindFirstObjectByType<HUDView>();
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return false;
            canvas = canvas.rootCanvas;

            var vg = new GameObject("Hull danger vignette", typeof(RectTransform), typeof(Image));
            var vr = (RectTransform)vg.transform;
            vr.SetParent(canvas.transform, false);
            vr.SetAsFirstSibling();
            vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
            vr.offsetMin = vr.offsetMax = Vector2.zero;
            _vignette = vg.GetComponent<Image>();
            _vignette.sprite = VignetteSprite();
            _vignette.raycastTarget = false;
            _vignette.color = new Color(1f, 0.08f, 0.04f, 0f);

            var bg = new GameObject("Hull danger banner", typeof(RectTransform), typeof(Image));
            _banner = (RectTransform)bg.transform;
            _banner.SetParent(canvas.transform, false);
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 1f);
            _banner.pivot = new Vector2(0.5f, 1f);
            _banner.anchoredPosition = new Vector2(0f, -112f);
            _banner.sizeDelta = new Vector2(420f, 40f);
            _bannerBack = bg.GetComponent<Image>();
            _bannerBack.raycastTarget = false;
            _bannerBack.color = new Color(0.35f, 0.02f, 0.02f, 0.85f);
            var outline = bg.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.3f, 0.2f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            var tg = new GameObject("Text", typeof(RectTransform));
            var tr = (RectTransform)tg.transform;
            tr.SetParent(_banner, false);
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = tr.offsetMax = Vector2.zero;
            _bannerText = tg.AddComponent<TextMeshProUGUI>();
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
                if (t != null && t.font != null && t != _bannerText) { _bannerText.font = t.font; break; }
            _bannerText.fontSize = 22f;
            _bannerText.fontStyle = FontStyles.Bold;
            _bannerText.alignment = TextAlignmentOptions.Center;
            _bannerText.color = Color.white;
            _bannerText.raycastTarget = false;
            _banner.gameObject.SetActive(false);
            return true;
        }

        private void Update()
        {
            if (!EnsureBuilt()) return;
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.State == GameState.Playing;

            // 매 프레임 기함의 실제 선체로 다시 맞춘다: 이벤트 없이 선체·최대치가 바뀌어도(설정 변경 등) 기준이 어긋나지 않게.
            // 큰 한 방 판정은 피해 이벤트(OnHullChanged)에서 이 기준과 비교해 한다.
            if (gm != null && gm.Player != null && gm.Player.HullMaxHp > 0f)
            {
                _lastHp = gm.Player.HullHp;
                _hull01 = Mathf.Clamp01(gm.Player.HullHp / gm.Player.HullMaxHp);
                Level = _hull01 <= CriticalFraction ? 2 : _hull01 <= DangerFraction ? 1 : 0;
            }

            float t = Time.unscaledTime;
            float alpha = 0f;
            if (playing && Level > 0)
            {
                float hz = Level == 2 ? 1.9f : 1.1f;
                // 심장 박동처럼: 짧게 치솟고 천천히 가라앉는다
                float beat = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(t * hz * Mathf.PI * 2f), 3f);
                float baseA = Level == 2 ? 0.34f : 0.20f, pulseA = Level == 2 ? 0.30f : 0.24f;
                alpha = baseA + pulseA * beat;
            }
            if (_flash > 0f)
            {
                alpha = Mathf.Max(alpha, 0.7f * Mathf.Min(1f, _flash));
                // 한 프레임이 길어도(명중 순간 끊김) 번쩍임을 건너뛰지 않게 프레임당 감소를 제한한다
                _flash = Mathf.Max(0f, _flash - Mathf.Min(Time.unscaledDeltaTime, 0.05f) / 0.5f);
            }
            if (!playing) { alpha = 0f; _flash = 0f; }
            var c = _vignette.color; c.a = alpha; _vignette.color = c;

            bool banner = playing && Level > 0;
            if (_banner.gameObject.activeSelf != banner) _banner.gameObject.SetActive(banner);
            if (banner)
            {
                int pct = Mathf.CeilToInt(_hull01 * 100f);
                _bannerText.text = Level == 2 ? $"선체 치명 {pct}%  —  회피·수리!" : $"선체 위험 {pct}%";
                float blink = 0.5f + 0.5f * Mathf.Sin(t * (Level == 2 ? 9f : 5f));
                _bannerBack.color = Color.Lerp(new Color(0.30f, 0.02f, 0.02f, 0.85f), new Color(0.75f, 0.06f, 0.04f, 0.92f), blink);
            }
        }

        /// <summary>가운데가 투명하고 가장자리로 갈수록 진해지는 둥근 빛 번짐(런타임 생성).</summary>
        private static Sprite VignetteSprite()
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Hull danger vignette" };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                // 둥근 사각형 거리: 모서리가 가장 진하고 가장자리 중앙도 충분히 붉게
                float d = Mathf.Pow(Mathf.Pow(Mathf.Abs(u), 4f) + Mathf.Pow(Mathf.Abs(v), 4f), 0.25f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 1.08f, d));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
