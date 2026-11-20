using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 한 번 재생되고 사라지는 이펙트(물기둥, 폭발 등). 풀에서 꺼낼 때마다 파티클을 처음부터 다시 튼다.
    /// 수명이 끝나면 스스로 풀에 돌아간다.
    /// </summary>
    public class PooledEffect : MonoBehaviour, IPoolable
    {
        [Tooltip("이 시간이 지나면 풀로 돌아간다. 가장 긴 파티클 수명보다 길게.")]
        [SerializeField] private float lifeTime = 2f;

        private ParticleSystem[] _systems;
        private float _age;

        /// <summary>위치에 이펙트를 하나 띄운다. 프리팹이 없으면 아무 일도 하지 않는다.</summary>
        public static void Spawn(GameObject prefab, Vector3 position) => Spawn(prefab, position, 1f);

        /// <summary>
        /// 크기를 바꿔 띄운다. 같은 이펙트를 작은 명중 불꽃·큰 공중 요격 폭발처럼 재사용한다.
        /// 파티클은 계층 스케일을 따르도록 바꿔 두므로 크기·범위가 함께 줄고 는다.
        /// </summary>
        public static void Spawn(GameObject prefab, Vector3 position, float scale)
        {
            if (prefab == null || PoolManager.Instance == null) return;
            var go = PoolManager.Instance.Spawn(prefab, position, Quaternion.identity);
            if (go != null) go.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
        }

        private void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in _systems)
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age < lifeTime) return;

            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            _age = 0f;
            _systems ??= GetComponentsInChildren<ParticleSystem>(true);

            foreach (var ps in _systems)
            {
                ps.Clear(true);
                ps.Play(true);
            }
        }

        public void OnDespawned()
        {
            if (_systems == null) return;
            foreach (var ps in _systems) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// 플레이어 무장의 포구 이펙트. 무기당 파티클 세 개를 한 번만 만들고 Emit으로 재사용한다.
    /// 위치와 속도를 월드 공간에서 지정하므로 Blender 소켓의 로컬 축이 달라도 실제 탄도와 일치한다.
    /// </summary>
    public class MuzzleBurstVfx : MonoBehaviour
    {
        /// <summary>Heavy(2026-11-20): 2차대전 주포처럼 큰 포 — 함포보다 크게.</summary>
        public enum Style { Autocannon, NavalGun, Ciws, Heavy }

        /// <summary>
        /// 무장별 포구 화염 크기(2026-11-20 타격감에서 함포 · 중포만 키움). 자주 쏘는 기관포 · 근접방어무기는 그대로 둔다 —
        /// 다 키우면 화면을 덮고 표적을 가린다. 함포 · 중포는 옆으로 터지는 화염 · 연기 고리 · 짧은 조명을 더한다.
        /// </summary>
        public static float SizeFor(Style style) => style switch
        {
            Style.Heavy => 1.7f,
            Style.NavalGun => 1.3f,
            Style.Autocannon => 0.42f,
            _ => 0.30f,
        };

        /// <summary>검증용: 함포 · 중포 포구 화염 수.</summary>
        public static int BigBursts { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => BigBursts = 0;

        private ParticleSystem _core;
        private ParticleSystem _flare;
        private ParticleSystem _smoke;
        private bool _ready;

        public void Emit(Vector3 position, Vector3 fireDirection, Style style, GameObject materialSource, bool withSmoke)
        {
            if (!_ready) Initialize(materialSource);
            Vector3 forward = fireDirection.sqrMagnitude > 0.001f ? fireDirection.normalized : transform.forward;
            Vector3 side = Vector3.Cross(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.9f
                ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, forward).normalized;
            bool big = style is Style.NavalGun or Style.Heavy;
            float size = SizeFor(style);
            int flareCount = style == Style.Heavy ? 16 : style == Style.NavalGun ? 12 : style == Style.Autocannon ? 4 : 3;
            int smokeCount = style == Style.Heavy ? 16 : style == Style.NavalGun ? 12 : style == Style.Autocannon ? 3 : 2;

            EmitParticle(_core, position + forward * (0.06f * size), Vector3.zero,
                0.09f, 0.5f * size, new Color(1f, 0.94f, 0.67f, 0.95f));
            if (big)
            {
                BigBursts++;
                // 하얗게 달아오른 속심 + 앞으로 길게 뻗는 큰 불꽃
                EmitParticle(_core, position + forward * (0.35f * size), forward * (2.5f * size),
                    0.07f, 0.75f * size, new Color(1f, 0.98f, 0.9f, 1f));
                EmitParticle(_core, position + forward * (0.7f * size), forward * (4f * size),
                    0.12f, 0.9f * size, new Color(1f, 0.6f, 0.22f, 0.85f));
                // 포구 양옆 · 위아래로 터지는 화염(제퇴기처럼)
                for (int i = 0; i < 6; i++)
                {
                    float a = (i / 6f + Random.Range(-0.05f, 0.05f)) * Mathf.PI * 2f;
                    Vector3 radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    EmitParticle(_flare, position + forward * (0.08f * size), (radial * 3.4f + forward * 1.2f) * size,
                        Random.Range(0.06f, 0.11f), Random.Range(0.3f, 0.5f) * size, new Color(1f, 0.72f, 0.3f, 0.85f));
                }
                Game.View.Explosions.FlashLight(position + forward * (0.6f * size), style == Style.Heavy ? 0.45f : 0.25f,
                    new Color(1f, 0.74f, 0.4f));
            }
            for (int i = 0; i < flareCount; i++)
            {
                Vector2 spread = Random.insideUnitCircle * (big ? 0.28f : 0.18f);
                Vector3 velocity = forward * Random.Range(2.2f, 5.2f) * size +
                                   (side * spread.x + up * spread.y) * (big ? 5f : 2.8f);
                Color color = i % 3 == 0
                    ? new Color(1f, 0.91f, 0.53f, 0.9f)
                    : new Color(1f, 0.48f, 0.17f, 0.75f);
                EmitParticle(_flare, position + forward * (0.10f * size), velocity,
                    Random.Range(0.07f, 0.16f), Random.Range(0.24f, 0.53f) * size, color);
            }

            if (!withSmoke) return;
            for (int i = 0; i < smokeCount; i++)
            {
                Vector2 spread = Random.insideUnitCircle;
                Vector3 velocity = forward * Random.Range(0.45f, 1.4f) * size +
                                   (side * spread.x + up * spread.y) * (0.7f * size) +
                                   Vector3.up * Random.Range(0.2f, 0.65f);
                Color color = i % 3 == 0
                    ? new Color(0.64f, 0.69f, 0.68f, 0.38f)
                    : new Color(0.39f, 0.49f, 0.49f, 0.27f);
                EmitParticle(_smoke, position + forward * (0.13f * size), velocity,
                    Random.Range(0.55f, 1.2f), Random.Range(0.38f, 0.72f) * size, color);
            }
            if (!big) return;
            // 포구 앞에 퍼지는 연기 고리
            for (int i = 0; i < 8; i++)
            {
                float a = (i / 8f) * Mathf.PI * 2f;
                Vector3 radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                Vector3 velocity = (radial * 1.5f + forward * 0.9f) * size + Vector3.up * 0.3f;
                EmitParticle(_smoke, position + forward * (0.45f * size) + radial * (0.15f * size), velocity,
                    Random.Range(0.7f, 1.3f), Random.Range(0.4f, 0.6f) * size, new Color(0.6f, 0.64f, 0.63f, 0.32f));
            }
        }

        private static void EmitParticle(ParticleSystem system, Vector3 position, Vector3 velocity,
                                         float lifetime, float size, Color color)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime,
                startSize = size,
                startColor = color
            };
            system.Emit(particle, 1);
        }

        private void Initialize(GameObject materialSource)
        {
            var sourceRenderer = materialSource != null
                ? materialSource.GetComponentInChildren<ParticleSystemRenderer>(true) : null;
            var material = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            _core = CreateSystem("Muzzle core", material, 48, 1f, 1.25f, false);
            _flare = CreateSystem("Muzzle flame jets", material, 192, 1f, 1.6f, true);
            _smoke = CreateSystem("Muzzle drifting smoke", material, 256, 1f, 2.8f, false);
            _ready = true;
        }

        private ParticleSystem CreateSystem(string name, Material material, int capacity,
                                            float startScale, float endScale, bool stretched)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var system = child.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1.5f;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            var shape = system.shape;
            shape.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.78f, 0.42f),
                        new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
            var renderer = child.GetComponent<ParticleSystemRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched) { renderer.velocityScale = 0.12f; renderer.lengthScale = 1.15f; }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
            return system;
        }
    }
}
