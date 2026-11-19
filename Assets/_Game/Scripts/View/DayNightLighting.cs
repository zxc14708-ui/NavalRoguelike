using UnityEngine;
using UnityEngine.Rendering;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 낮과 밤의 조명(2026-11-19). DayNight의 From → To 기준점을 섞어 해(방향광)·주변광·반사·물색·안개 색을 바꾼다.
    ///   - 씬의 처음 값(해 각도·색·세기·그림자, 주변광 SH, 반사 세기)을 기억해 두고 거기에 곱한다 — 낮은 씬 그대로.
    ///   - 물색·안개 색은 해역 물색(SeaAtmosphere)이 칠한 값에 곱한다(SetDayTint) — 수면 머티리얼 원본은 건드리지 않는다.
    ///   - 밤 밝기 설정(DisplaySettings.NightBrightness)이 밤 기준점의 세기에 곱해진다.
    /// 전투 씬(Ocean이 있는 씬)에서만 붙고, 없어지면 처음 값으로 돌려놓는다.
    /// </summary>
    public sealed class DayNightLighting : MonoBehaviour
    {
        private struct Preset
        {
            public float Sun;          // 해 세기 배율
            public Color SunColor;     // 해 색 배율
            public float Elevation;    // 해 높이(도) — 음수면 씬 그대로
            public float YawOffset;    // 씬 해 방위에서 더 도는 각
            public Color Ambient;      // 주변광 배율(채널별)
            public float Reflection;   // 반사 세기 배율
            public float Shadow;       // 그림자 진하기 배율
            public Color Ocean;        // 물색 배율
            public Color Fog;          // 안개 색 배율
            public Color OceanAdd;     // 수면 하늘 반사에 더하는 빛(노을)
        }

        private static Preset For(DayPhase p, float bright) => p switch
        {
            DayPhase.Dusk => new Preset { Sun = 1f, SunColor = new Color(1f, 0.58f, 0.34f), Elevation = 28f, YawOffset = 35f,
                Ambient = new Color(1.15f, 0.78f, 0.7f), Reflection = 0.7f, Shadow = 0.9f,
                Ocean = new Color(0.66f, 0.56f, 0.72f), Fog = new Color(1.1f, 0.7f, 0.5f), OceanAdd = new Color(0.11f, 0.04f, 0.005f) },
            DayPhase.Night => new Preset { Sun = 0.2f * bright, SunColor = new Color(0.55f, 0.68f, 1f), Elevation = 55f, YawOffset = 160f,
                Ambient = new Color(0.24f, 0.29f, 0.45f) * bright, Reflection = 0.2f * bright, Shadow = 0.55f,
                Ocean = new Color(0.22f, 0.3f, 0.46f) * Mathf.Min(1.4f, Mathf.Sqrt(bright)), Fog = new Color(0.1f, 0.14f, 0.22f) * bright,
                OceanAdd = new Color(0f, 0.005f, 0.02f) },
            DayPhase.Dawn => new Preset { Sun = 0.85f, SunColor = new Color(1f, 0.76f, 0.62f), Elevation = 30f, YawOffset = 200f,
                Ambient = new Color(0.95f, 0.92f, 1.15f), Reflection = 0.6f, Shadow = 0.85f,
                Ocean = new Color(0.72f, 0.72f, 0.88f), Fog = new Color(0.9f, 0.8f, 0.82f), OceanAdd = new Color(0.06f, 0.035f, 0.04f) },
            _ => new Preset { Sun = 1f, SunColor = Color.white, Elevation = -1f, YawOffset = 0f, Ambient = Color.white, Reflection = 1f,
                Shadow = 1f, Ocean = Color.white, Fog = Color.white },
        };

        private Light _sun;
        private Quaternion _baseRotation;
        private Color _baseColor;
        private float _baseIntensity, _baseShadow, _baseReflection;
        private SphericalHarmonicsL2 _baseProbe;
        private AmbientMode _baseMode;
        private Color _baseSky, _baseEquator, _baseGround, _baseFlat;
        // 스카이박스 주변광의 위·옆·아래 색(스크립트로 SH를 바꾸면 스카이박스 모드가 다시 덮어써서, 3색 모드로 칠한다)
        private Color _ambSky, _ambEquator, _ambGround;
        private bool _captured;
        private float _lastKey = -1f;

        public static DayNightLighting Instance { get; private set; }
        /// <summary>검증용: 지금 해 세기와 주변광 배율(밝기 R).</summary>
        public float SunIntensity => _sun != null ? _sun.intensity : 0f;
        public float AmbientScale { get; private set; } = 1f;
        /// <summary>검증용: 수면에 곱하는 물빛 배율.</summary>
        public Color OceanTint { get; private set; } = Color.white;
        private static readonly int DayTintId = Shader.PropertyToID("_NavalDayTint");
        private static readonly int DayAddId = Shader.PropertyToID("_NavalDayAdd");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<DayNightLighting>() != null) return;
            if (GameObject.Find("Ocean") == null) return;   // 전투 씬에서만
            new GameObject("Day night lighting").AddComponent<DayNightLighting>();
        }

        private void Awake() { Instance = this; }

        /// <summary>
        /// 씬의 처음 값을 기억한다 — 전투가 실제로 시작된 뒤(항구 메뉴가 해를 끄고 주변광을 바꿔 두는 동안은 기다린다).
        /// </summary>
        private bool TryCapture()
        {
            if (_captured) return true;
            var gm = Game.Core.GameManager.Instance;
            if (gm == null || gm.State != Game.Core.GameState.Playing) return false;
            _sun = FindSun();
            if (_sun == null) return false;
            Capture();
            return true;
        }

        private void Capture()
        {
            if (_captured) return;
            if (_sun == null) _sun = FindSun();
            if (_sun != null)
            {
                _baseRotation = _sun.transform.rotation;
                _baseColor = _sun.color;
                _baseIntensity = _sun.intensity;
                _baseShadow = _sun.shadowStrength;
            }
            _baseProbe = RenderSettings.ambientProbe;
            _baseMode = RenderSettings.ambientMode;
            _baseSky = RenderSettings.ambientSkyColor; _baseEquator = RenderSettings.ambientEquatorColor;
            _baseGround = RenderSettings.ambientGroundColor; _baseFlat = RenderSettings.ambientLight;
            var dirs = new[] { Vector3.up, Vector3.forward, Vector3.down };
            var cols = new Color[3];
            _baseProbe.Evaluate(dirs, cols);
            _ambSky = cols[0]; _ambEquator = cols[1]; _ambGround = cols[2];
            _baseReflection = RenderSettings.reflectionIntensity;
            _captured = true;
        }

        /// <summary>
        /// 전투 바다를 비추는 해. 항구 메뉴의 "Harbor daylight"처럼 한 레이어만 비추는 빛은 건너뛴다(기본 레이어를 비추는 방향광 중 가장 센 것).
        /// </summary>
        private static Light FindSun()
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && (sun.cullingMask & 1) != 0) return sun;
            sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional && l.isActiveAndEnabled && (l.cullingMask & 1) != 0 && l.name != "Harbor daylight" &&
                    (sun == null || l.intensity > sun.intensity))
                    sun = l;
            return sun;
        }

        /// <summary>검증용: 조명이 다루는 해 이름.</summary>
        public string SunName => _sun != null ? _sun.name : "없음";

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (!_captured) return;
            if (_sun != null)
            {
                _sun.transform.rotation = _baseRotation;
                _sun.color = _baseColor;
                _sun.intensity = _baseIntensity;
                _sun.shadowStrength = _baseShadow;
            }
            RenderSettings.ambientMode = _baseMode;
            RenderSettings.ambientSkyColor = _baseSky; RenderSettings.ambientEquatorColor = _baseEquator;
            RenderSettings.ambientGroundColor = _baseGround; RenderSettings.ambientLight = _baseFlat;
            RenderSettings.ambientProbe = _baseProbe;
            RenderSettings.reflectionIntensity = _baseReflection;
            Shader.SetGlobalVector(DayAddId, Vector4.zero);
            Shader.SetGlobalVector(DayTintId, Vector4.zero);
            if (SeaAtmosphere.Instance != null) SeaAtmosphere.Instance.SetDayTint(Color.white, Color.white);
        }

        private void LateUpdate()
        {
            if (!TryCapture()) return;
            float bright = Game.Core.DisplaySettings.NightBrightnessScale;
            // 바뀐 것이 없으면 다시 칠하지 않는다
            float key = (int)DayNight.From * 1000f + (int)DayNight.To * 100f + Mathf.Round(DayNight.Blend * 500f) / 500f + bright * 7f;
            if (Mathf.Approximately(key, _lastKey)) return;
            _lastKey = key;
            Apply(DayNight.From, DayNight.To, Mathf.SmoothStep(0f, 1f, DayNight.Blend), bright);
        }

        private void Apply(DayPhase fromPhase, DayPhase toPhase, float k, float bright)
        {
            var a = For(fromPhase, bright);
            var b = For(toPhase, bright);

            if (_sun != null)
            {
                _sun.intensity = _baseIntensity * Mathf.Lerp(a.Sun, b.Sun, k);
                _sun.color = _baseColor * Color.Lerp(a.SunColor, b.SunColor, k);
                _sun.shadowStrength = _baseShadow * Mathf.Lerp(a.Shadow, b.Shadow, k);
                _sun.transform.rotation = Quaternion.Slerp(SunRotation(a), SunRotation(b), k);
            }

            var amb = Color.Lerp(a.Ambient, b.Ambient, k);
            AmbientScale = (amb.r + amb.g + amb.b) / 3f;
            if (amb == Color.white && _baseMode == AmbientMode.Skybox)
            {
                // 한낮: 씬 그대로(스카이박스 주변광)
                if (RenderSettings.ambientMode != AmbientMode.Skybox) { RenderSettings.ambientMode = AmbientMode.Skybox; RenderSettings.ambientProbe = _baseProbe; }
            }
            else
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = _ambSky * amb;
                RenderSettings.ambientEquatorColor = _ambEquator * amb;
                RenderSettings.ambientGroundColor = _ambGround * amb;
            }
            RenderSettings.reflectionIntensity = _baseReflection * Mathf.Lerp(a.Reflection, b.Reflection, k);

            var ocean = Color.Lerp(a.Ocean, b.Ocean, k);
            OceanTint = ocean;
            Shader.SetGlobalVector(DayTintId, new Vector4(ocean.r, ocean.g, ocean.b, 1f));
            var add = Color.Lerp(a.OceanAdd, b.OceanAdd, k);
            Shader.SetGlobalVector(DayAddId, new Vector4(add.r, add.g, add.b, 0f));
            if (SeaAtmosphere.Instance != null) SeaAtmosphere.Instance.SetDayTint(ocean, Color.Lerp(a.Fog, b.Fog, k));
        }

        private Quaternion SunRotation(in Preset p)
        {
            if (p.Elevation < 0f) return _baseRotation;
            float yaw = _baseRotation.eulerAngles.y + p.YawOffset;
            return Quaternion.Euler(p.Elevation, yaw, 0f);
        }
    }
}
