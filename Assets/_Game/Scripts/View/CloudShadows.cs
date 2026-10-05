using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Game.View
{
    /// <summary>
    /// 구름 그림자(장식, 판정 없음). 해(주 방향광)에 구름 무늬 쿠키를 씌워 바다·배·섬에 똑같이 그림자가 지게 하고,
    /// 쿠키를 바람 방향(DecorFx.Wind — 연기와 같은 쪽)으로 천천히 흘린다.
    ///   - 무늬: 이어 붙여도 이음매가 없는 fBm 노이즈를 문턱으로 잘라 뭉게구름 모양(한 장 = TileSize m).
    ///   - 그늘에서는 직사광이 Darkness만큼 줄어든다. 바다 셰이더(Naval/Ocean)는 쿠키 값을 직접 읽어 물빛도 어둡게 한다.
    /// 씬을 다시 만들지 않아도 되게 씬이 열린 뒤 해에 스스로 붙는다. 꺼지면 원래 쿠키로 되돌린다.
    /// </summary>
    public class CloudShadows : MonoBehaviour
    {
        private const int Resolution = 256;
        private const float TileSize = 260f;     // m. 구름 하나 ≈ 40~80m
        private const float Darkness = 0.5f;
        private const float Coverage = 0.51f;    // 클수록 구름이 적다(그늘 면적 약 35~40%)

        private Light _light;
        private UniversalAdditionalLightData _data;
        private Texture2D _texture;
        private Texture _previousCookie;
        private Vector2 _previousSize, _previousOffset, _offset;

        /// <summary>검증용: 지금 적용 중인가, 흐른 거리(m).</summary>
        public bool Applied => _light != null && _texture != null && _light.cookie == _texture;
        public Vector2 Offset => _offset;
        public static CloudShadows Instance { get; private set; }
        /// <summary>검증용: 무늬에서 그늘(구름 절반 이상)이 차지하는 비율.</summary>
        public static float ShadedFraction { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null || sun.GetComponent<CloudShadows>() != null) return;
            sun.gameObject.AddComponent<CloudShadows>();
        }

        private void OnEnable()
        {
            Instance = this;
            _light = GetComponent<Light>();
            if (_light == null) return;
            _data = _light.GetUniversalAdditionalLightData();
            if (_texture == null) _texture = BuildTexture();

            _previousCookie = _light.cookie;
            _previousSize = _data.lightCookieSize;
            _previousOffset = _data.lightCookieOffset;
            _light.cookie = _texture;
            _data.lightCookieSize = new Vector2(TileSize, TileSize);
            _offset = new Vector2(Random.Range(0f, TileSize), Random.Range(0f, TileSize));
            _data.lightCookieOffset = _offset;
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (_light == null || _data == null) return;
            _light.cookie = _previousCookie;
            _data.lightCookieSize = _previousSize;
            _data.lightCookieOffset = _previousOffset;
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            if (_light == null || _data == null) return;
            // 쿠키 좌표는 빛의 오른쪽·위쪽 축 기준이다. 바람을 그 축에 나눠 싣는다.
            Vector3 w = DecorFx.Wind;
            var t = _light.transform;
            _offset += new Vector2(Vector3.Dot(w, t.right), Vector3.Dot(w, t.up)) * Time.deltaTime;
            _offset.x = Mathf.Repeat(_offset.x, TileSize);
            _offset.y = Mathf.Repeat(_offset.y, TileSize);
            _data.lightCookieOffset = _offset;
        }

        private static Texture2D BuildTexture()
        {
            var tex = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
            {
                name = "Cloud shadow cookie",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[Resolution * Resolution];
            var seed = new System.Random(1931);
            int salt = seed.Next(1, 100000);
            int shaded = 0;
            for (int y = 0; y < Resolution; y++)
            for (int x = 0; x < Resolution; x++)
            {
                float u = (float)x / Resolution, v = (float)y / Resolution;
                float n = 0f, amp = 0.5f, sum = 0f;
                for (int o = 0, period = 4; o < 5; o++, period *= 2)
                {
                    n += amp * TileNoise(u * period, v * period, period, salt + o * 131);
                    sum += amp;
                    amp *= 0.5f;
                }
                n /= sum;
                float cloud = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Coverage - 0.07f, Coverage + 0.1f, n));
                if (cloud > 0.5f) shaded++;
                byte b = (byte)Mathf.RoundToInt((1f - Darkness * cloud) * 255f);
                px[y * Resolution + x] = new Color32(b, b, b, b);
            }
            ShadedFraction = shaded / (float)px.Length;
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>격자 period마다 반복되는 값 노이즈(이음매 없음).</summary>
        private static float TileNoise(float x, float y, int period, int salt)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(x0, y0, period, salt), b = Hash(x0 + 1, y0, period, salt);
            float c = Hash(x0, y0 + 1, period, salt), d = Hash(x0 + 1, y0 + 1, period, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Hash(int x, int y, int period, int salt)
        {
            x = ((x % period) + period) % period;
            y = ((y % period) + period) % period;
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
