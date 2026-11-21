using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 기지 섬 탐조등(2026-11-21 Codex IslandBaseFollowup_v1 MIL_Searchlight: SearchlightYaw → SearchlightPitch → BeamOrigin).
    /// 바다 쪽을 ±45°로 천천히 훑는다(좌우 짝은 엇갈려). 밤(DayNight.LightsOn)에는 렌즈 앞에서 스포트라이트를 켠다 — 그림자 없음, 사거리 42m.
    /// 기함의 Searchlight(적을 드러내는 장비)와 다르다 — 이건 분위기 · 경계 연출만.
    /// </summary>
    public sealed class BaseSearchlight : MonoBehaviour
    {
        public const float SweepDegrees = 45f, SweepSeconds = 9f, Range = 42f;
        private Transform _yaw, _pitch, _beam;
        private Quaternion _yawRest;
        private Light _light;
        private float _phase;

        /// <summary>검증용: 지금 빛이 켜졌나.</summary>
        public bool LightOn => _light != null && _light.enabled;

        public static BaseSearchlight Attach(GameObject model, int side)
        {
            if (model == null) return null;
            var s = model.AddComponent<BaseSearchlight>();
            s._phase = side > 0 ? 0f : 0.5f;
            return s;
        }

        private void Start()
        {
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "SearchlightYaw") _yaw = t;
                else if (t.name == "SearchlightPitch") _pitch = t;
                else if (t.name == "BeamOrigin") _beam = t;
            }
            if (_yaw != null) _yawRest = _yaw.localRotation;
            var at = _beam != null ? _beam : _pitch != null ? _pitch : transform;
            var go = new GameObject("Base searchlight beam");
            go.transform.SetParent(at, false);
            // 렌즈가 보는 쪽: BeamOrigin이 있으면 그 앞(+Z), 없으면 모델 앞 · 아래로 조금
            go.transform.localRotation = Quaternion.identity;
            _light = go.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.range = Range;
            _light.spotAngle = 22f;
            _light.innerSpotAngle = 10f;
            _light.color = new Color(0.95f, 0.97f, 1f);
            _light.intensity = 7f;
            _light.shadows = LightShadows.None;
            _light.enabled = false;
            // 빛줄기가 땅이 아니라 바다 쪽 수평 근처로 — 렌즈 축이 위를 보면 아래로 12° 숙인다
            if (Vector3.Dot(go.transform.forward, Vector3.up) > 0.3f) go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.down * 0.2f);
        }

        private void Update()
        {
            float t = Time.time / SweepSeconds + _phase;
            if (_yaw != null)
            {
                // 모델 축 방향과 상관없이 월드 위축으로 돈다(레이더 회전부와 같은 방식)
                _yaw.localRotation = _yawRest;
                _yaw.Rotate(Vector3.up, Mathf.Sin(t * Mathf.PI * 2f) * SweepDegrees, Space.World);
            }
            if (_light != null)
            {
                bool on = DayNight.LightsOn;
                if (_light.enabled != on) _light.enabled = on;
            }
        }
    }
}
