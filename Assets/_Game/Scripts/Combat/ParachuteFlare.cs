using System.Collections.Generic;
using UnityEngine;
using Game.Enemies;
using Game.World;

namespace Game.Combat
{
    /// <summary>조명탄에 비치면 드러나는 적(반잠수정 등, 2026-11-19).</summary>
    public interface IIlluminable
    {
        /// <summary>seconds초 동안 드러난다(이미 더 오래 드러나 있으면 줄이지 않는다).</summary>
        void Illuminate(float seconds);
    }

    /// <summary>
    /// 낙하산 조명탄(2026-11-19 낮과 밤). 함선에서 쏘아 올려 목표 상공에서 터지고, 천천히 내려오며 둘레를 밝힌다.
    ///   - 빛: 점광원(배·수면을 비춘다 — 수면 셰이더가 추가 광원을 받는다) + 밝은 불꽃 구(블룸).
    ///   - 효과(어둠 0.35 이상일 때만): 반경 Radius 안의 IIlluminable(반잠수정)은 드러나고, 잠수함은 확정 접촉(대잠 무기가 정확히 노린다).
    ///     잠수함을 물 위로 띄우지는 않는다. 낮에는 빛만 난다.
    /// 프리팹 없이 코드로 만든다(구 · 빛 · 꼬리선). 터진 뒤에는 낙하산 모델(Codex FX_ParachuteFlare — 원점 = 불꽃 자리 FlarePoint)이 펼쳐진다.
    /// </summary>
    public sealed class ParachuteFlare : MonoBehaviour
    {
        public const float Radius = 22f;
        public const float Life = 11f;
        public const float FlightSeconds = 1.1f;
        public const float BurstHeight = 15f, EndHeight = 3f;
        private const float EffectInterval = 0.25f;
        public const float LightIntensity = 320f;

        private static readonly List<ParachuteFlare> s_active = new();
        private static Material s_glow, s_trail;

        private Vector3 _from, _target;
        private float _age, _effectTimer;
        private Light _light;
        private Transform _ball;
        private GameObject _chute;
        private Vector3 _chuteScale = Vector3.one;
        private Quaternion _chuteBase = Quaternion.identity;
        private float _chuteYaw;
        private LineRenderer _trail;
        private bool _burst;
        private float _seed;

        /// <summary>떠 있는 조명탄(검증용 · 다른 시스템이 "비친 곳인가"를 묻는다).</summary>
        public static IReadOnlyList<ParachuteFlare> Active => s_active;
        /// <summary>터져서 빛을 내는 중인가.</summary>
        public bool Burning => _burst && _age < FlightSeconds + Life;
        public Vector3 Ground => _target;
        /// <summary>이번 조명탄이 확정 접촉으로 만든 잠수함 · 드러낸 적 수(검증용).</summary>
        public int Contacts { get; private set; }
        public int Revealed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_active.Clear(); }

        /// <summary>그 지점이 지금 조명탄 빛 안인가(xz 거리).</summary>
        public static bool IsLit(Vector3 p)
        {
            foreach (var f in s_active)
                if (f != null && f.Burning)
                {
                    Vector3 d = p - f._target; d.y = 0f;
                    if (d.sqrMagnitude <= Radius * Radius) return true;
                }
            return false;
        }

        public static ParachuteFlare Launch(Vector3 from, Vector3 groundTarget)
        {
            var go = new GameObject("Parachute flare");
            var f = go.AddComponent<ParachuteFlare>();
            f._from = from;
            f._target = new Vector3(groundTarget.x, 0f, groundTarget.z);
            go.transform.position = from;
            f.Build();
            return f;
        }

        private void Build()
        {
            _seed = Random.value * 10f;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.name = "Flare glow";
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * 0.35f;
            var r = ball.GetComponent<MeshRenderer>();
            r.sharedMaterial = GlowMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            _ball = ball.transform;

            _chuteYaw = Random.Range(0f, 360f);
            _chute = Game.View.DayNightProps.Place(transform, Game.View.DayNightProps.Flare, Vector3.zero, _chuteYaw);
            if (_chute != null)
            {
                _chute.name = "Flare parachute";
                foreach (var cr in _chute.GetComponentsInChildren<MeshRenderer>(true)) cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _chute.SetActive(false);
                _chuteScale = _chute.transform.localScale;
                _chuteBase = Quaternion.Inverse(Quaternion.Euler(0f, _chuteYaw, 0f)) * _chute.transform.localRotation;   // 모델 자체 회전(FBX 축 변환)
            }

            _light = new GameObject("Flare light").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.type = LightType.Point;
            _light.color = new Color(1f, 0.92f, 0.76f);
            _light.range = Radius * 2f;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;

            _trail = gameObject.AddComponent<LineRenderer>();
            _trail.sharedMaterial = TrailMaterial();
            _trail.positionCount = 2;
            _trail.startWidth = 0.18f; _trail.endWidth = 0.04f;
            _trail.startColor = new Color(1f, 0.85f, 0.6f, 0.9f); _trail.endColor = new Color(1f, 0.85f, 0.6f, 0f);
            _trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trail.SetPosition(0, _from); _trail.SetPosition(1, _from);
            s_active.Add(this);
        }

        private void OnDestroy() { s_active.Remove(this); }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            Vector3 burstPoint = _target + Vector3.up * BurstHeight;
            if (_age < FlightSeconds)
            {
                // 쏘아 올리는 중: 포물선
                float u = _age / FlightSeconds;
                Vector3 p = Vector3.Lerp(_from, burstPoint, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 6f;
                transform.position = p;
                _trail.SetPosition(0, p);
                _trail.SetPosition(1, Vector3.Lerp(_from, p, 0.7f));
                _ball.localScale = Vector3.one * 0.18f;
                return;
            }
            if (!_burst)
            {
                _burst = true;
                _trail.enabled = false;
                if (_chute != null) _chute.SetActive(true);
                Game.Core.AudioManager.Play(Game.Data.SfxId.ChaffLaunch, burstPoint, 0.6f, 0.7f);
            }

            float t = _age - FlightSeconds;
            if (t >= Life) { Destroy(gameObject); return; }
            float k = t / Life;
            // 천천히 내려오며 바람에 조금 흔들린다
            Vector3 sway = new Vector3(Mathf.Sin(_age * 0.7f + _seed), 0f, Mathf.Cos(_age * 0.5f + _seed)) * 0.8f;
            transform.position = Vector3.Lerp(burstPoint, _target + Vector3.up * EndHeight, k) + sway;

            float rise = Mathf.Clamp01(t / 0.35f), fade = Mathf.Clamp01((Life - t) / 1.5f);
            float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(_age * 9f, _seed);
            float night = Mathf.Lerp(0.6f, 1f, DayNight.Darkness01);
            // 거리 제곱으로 줄어드는 점광원 — 15m 아래 수면에 1 남짓 닿게
            _light.intensity = LightIntensity * rise * fade * flicker * night;
            // 낙하산 모델이 있으면 불꽃은 통 아래 작은 구로(낙하산보다 크면 흰 원판처럼 보인다)
            float ballSize = _chute != null ? 0.5f : 1.1f;
            _ball.localScale = Vector3.one * (ballSize + 0.12f * flicker) * fade;
            if (_chute != null)
            {
                // 펼쳐지며(0.4초) 커지고, 바람에 조금 기운다
                float open = Mathf.SmoothStep(0.2f, 1f, Mathf.Clamp01(t / 0.4f));
                _chute.transform.localScale = _chuteScale * open;
                _chute.transform.localRotation = Quaternion.Euler(sway.z * 6f, _chuteYaw, -sway.x * 6f) * _chuteBase;
            }

            _effectTimer -= dt;
            if (_effectTimer > 0f) return;
            _effectTimer = EffectInterval;
            if (DayNight.Darkness01 >= DayNight.LightsOnDarkness) Illuminate();
        }

        /// <summary>반경 안의 잠수함은 확정 접촉, 반잠수정(IIlluminable)은 드러낸다.</summary>
        private void Illuminate()
        {
            float r2 = Radius * Radius;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 d = sub.transform.position - _target; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                if (!sub.IsContactConfirmed) Contacts++;
                sub.ConfirmContact(EffectInterval + 2.5f);
            }
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t is not IIlluminable lit || t.Transform == null) continue;
                Vector3 d = t.Transform.position - _target; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                Revealed++;
                lit.Illuminate(EffectInterval + 2.5f);
            }
        }

        private static Material GlowMaterial()
        {
            if (s_glow != null) return s_glow;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            s_glow = new Material(shader) { name = "Flare glow (Runtime)" };
            // HDR 색 — 블룸으로 번진다
            var c = new Color(1f, 0.82f, 0.55f) * 2.6f;
            if (s_glow.HasProperty("_BaseColor")) s_glow.SetColor("_BaseColor", c);
            s_glow.color = c;
            return s_glow;
        }

        private static Material TrailMaterial()
        {
            if (s_trail != null) return s_trail;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            s_trail = new Material(shader) { name = "Flare trail (Runtime)" };
            return s_trail;
        }
    }
}
