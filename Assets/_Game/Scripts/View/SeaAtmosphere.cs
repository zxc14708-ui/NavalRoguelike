using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 해역별 물색·안개(2026-10-11 점검 K04). 해역이 바뀌면 4초에 걸쳐 바꾼다.
    ///   군도: 얕은 청록 물빛 · 옅은 연무 / 좁은 수로: 어두운 회녹색 · 짙은 해무 / 개방해역: 원래 짙은 바다색 · 맑음
    /// 수면 머티리얼 원본은 바꾸지 않는다 — 수면 렌더러의 MaterialPropertyBlock으로만 덮어쓴다(해역이 없으면 지운다).
    /// 안개는 RenderSettings(지수 안개)로, 처음 값을 기억해 해역이 끝나면 돌려놓는다.
    /// </summary>
    public sealed class SeaAtmosphere : MonoBehaviour
    {
        private static readonly int DeepId = Shader.PropertyToID("_DeepColor");
        private static readonly int ScatterId = Shader.PropertyToID("_ScatterColor");
        private static readonly int TransmissionId = Shader.PropertyToID("_TransmissionColor");
        private const float BlendSeconds = 4f;

        private MeshRenderer _ocean;
        private MaterialPropertyBlock _block;
        private Color _baseDeep, _baseScatter, _baseTransmission;
        private Color _fromDeep, _fromScatter, _fromTransmission, _toDeep, _toScatter, _toTransmission;
        private float _fromFog, _toFog, _t = 1f;
        private bool _baseFog;
        private float _baseDensity;
        private FogMode _baseMode;
        private Color _baseFogColor;

        public static SeaAtmosphere Instance { get; private set; }
        /// <summary>검증용: 지금 수면 깊은 물색과 안개 농도.</summary>
        public Color CurrentDeep { get; private set; }
        public float CurrentFog { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SeaAtmosphere>() != null) return;
            if (GameObject.Find("Ocean") == null) return;   // 전투 씬에서만
            new GameObject("Sea atmosphere").AddComponent<SeaAtmosphere>();
        }

        private void Awake()
        {
            Instance = this;
            _baseFog = RenderSettings.fog;
            _baseDensity = RenderSettings.fogDensity;
            _baseMode = RenderSettings.fogMode;
            _baseFogColor = RenderSettings.fogColor;
            SeaRegion.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            SeaRegion.Changed -= OnChanged;
            if (Instance == this) Instance = null;
            RenderSettings.fog = _baseFog;
            RenderSettings.fogDensity = _baseDensity;
            RenderSettings.fogMode = _baseMode;
            RenderSettings.fogColor = _baseFogColor;
        }

        private bool FindOcean()
        {
            if (_ocean != null) return true;
            var surface = FindFirstObjectByType<OceanSurface>();
            _ocean = surface != null ? surface.GetComponent<MeshRenderer>() : null;
            if (_ocean == null || _ocean.sharedMaterial == null) return false;
            var m = _ocean.sharedMaterial;
            _baseDeep = m.HasProperty(DeepId) ? m.GetColor(DeepId) : Color.black;
            _baseScatter = m.HasProperty(ScatterId) ? m.GetColor(ScatterId) : Color.black;
            _baseTransmission = m.HasProperty(TransmissionId) ? m.GetColor(TransmissionId) : Color.black;
            _block = new MaterialPropertyBlock();
            CurrentDeep = _toDeep = _baseDeep; _toScatter = _baseScatter; _toTransmission = _baseTransmission;
            return true;
        }

        private void OnChanged(SeaKind kind)
        {
            if (!FindOcean()) return;
            var p = SeaRegion.Active ? SeaRegion.For(kind) : null;
            bool useBase = p == null || p.Deep.r < 0f;
            _fromDeep = CurrentDeep; _fromScatter = _toScatter; _fromTransmission = _toTransmission;
            _toDeep = useBase ? _baseDeep : p.Deep;
            _toScatter = useBase ? _baseScatter : p.Scatter;
            _toTransmission = useBase ? _baseTransmission : p.Transmission;
            _fromFog = CurrentFog;
            _toFog = p != null ? p.FogDensity : 0f;
            _t = 0f;
        }

        private void Update()
        {
            if (_t >= 1f || _ocean == null) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / BlendSeconds);
            float k = Mathf.SmoothStep(0f, 1f, _t);
            CurrentDeep = Color.Lerp(_fromDeep, _toDeep, k);
            _ocean.GetPropertyBlock(_block);
            _block.SetColor(DeepId, CurrentDeep);
            _block.SetColor(ScatterId, Color.Lerp(_fromScatter, _toScatter, k));
            _block.SetColor(TransmissionId, Color.Lerp(_fromTransmission, _toTransmission, k));
            _ocean.SetPropertyBlock(_block);

            CurrentFog = Mathf.Lerp(_fromFog, _toFog, k);
            if (CurrentFog > 0.0001f)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = CurrentFog;
                RenderSettings.fogColor = new Color(0.62f, 0.69f, 0.74f);
            }
            else
            {
                RenderSettings.fog = _baseFog;
                RenderSettings.fogDensity = _baseDensity;
                RenderSettings.fogMode = _baseMode;
                RenderSettings.fogColor = _baseFogColor;
            }
        }
    }
}
