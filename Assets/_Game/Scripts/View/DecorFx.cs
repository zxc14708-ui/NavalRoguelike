using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 장식 효과(판정 없음)가 함께 쓰는 파티클. 종류마다 월드 공간 파티클 시스템을 하나만 만들고
    /// 모든 배가 Emit으로 나눠 쓴다(그리기 한 번, 배가 풀로 돌아가도 이미 뿜은 연기는 남는다).
    ///   - Spray: 뱃머리 물보라(흰색, 중력으로 떨어짐)
    ///   - Smoke: 연돌 배기 연기(회색, 커지며 흐려짐)
    ///   - WreckSmoke: 격침 연기 기둥(검은색, 크게 퍼짐)
    ///   - Fire: 불꽃(주황, 줄어들며 사라짐)
    ///   - BlastFire: 함체 명중 폭발 불덩이(노이즈를 따라 갈라지며 녹고, 녹는 가장자리가 빛남 — Naval/BlastFire)
    /// 재질은 기존 효과와 같은 부드러운 원 스프라이트(Resources/Effects/MAT_FxSoft), BlastFire만 Resources/Effects/MAT_BlastFire.
    /// 씬이 바뀌면 시스템이 함께 사라지고 다음에 쓸 때 다시 만든다.
    /// </summary>
    public static class DecorFx
    {
        /// <summary>바람(m/s). 연기와 구름 그림자가 같은 쪽으로 흐른다.</summary>
        public static readonly Vector3 Wind = new(2.2f, 0f, 1.2f);

        private static Transform s_root;
        private static ParticleSystem s_spray, s_smoke, s_wreckSmoke, s_fire, s_sparks, s_flash, s_blastFire, s_chaff, s_missileSmoke, s_drops, s_blastSmoke;
        private static Material s_material, s_blastMaterial;

        public static ParticleSystem Spray => Get(ref s_spray, "Bow spray", 2000, 1f, 1.7f, 0.75f, false);
        /// <summary>물방울(2026-11-21): 속도 방향으로 늘어난 작은 물방울이 튀어 올랐다 중력으로 떨어진다 — 뱃머리 · 물기둥.</summary>
        public static ParticleSystem Drops => Get(ref s_drops, "Spray droplets", 4000, 1f, 0.7f, 1.15f, false, stretched: true);
        /// <summary>미사일 · 로켓 연기 꼬리(2026-11-21): 작게 나와 수명 동안 5배로 부풀며 천천히 흩어진다.</summary>
        public static ParticleSystem MissileSmoke => Get(ref s_missileSmoke, "Missile smoke", 9000, 0.7f, 4.2f, -0.01f, true);
        /// <summary>기만체 은박(채프): 작은 반짝이가 아주 천천히 떨어진다(크기 그대로, 2026-11-09)</summary>
        public static ParticleSystem Chaff => Get(ref s_chaff, "Decoy chaff", 1500, 1f, 0.9f, 0.04f, false);
        public static ParticleSystem Smoke => Get(ref s_smoke, "Funnel smoke", 2500, 1f, 3.4f, 0f, true);
        /// <summary>폭발 연기(2026-11-21): 불덩이 뒤에 솟는 검은 연기 기둥 · 공중 폭발 연기 뭉치. 작게 나와 3배로 부풀며 오래 남는다.</summary>
        public static ParticleSystem BlastSmoke => Get(ref s_blastSmoke, "Blast smoke", 2500, 0.55f, 3.1f, -0.015f, true);
        public static ParticleSystem WreckSmoke => Get(ref s_wreckSmoke, "Wreck smoke", 1200, 1f, 3.6f, 0f, true);
        public static ParticleSystem Fire => Get(ref s_fire, "Wreck fire", 900, 1f, 0.35f, -0.05f, false);
        /// <summary>폭발 파편 불똥(속도 방향으로 늘어남, 중력으로 떨어짐)</summary>
        public static ParticleSystem Sparks => Get(ref s_sparks, "Blast sparks", 800, 1f, 0.4f, 1f, false, stretched: true);
        /// <summary>폭발 순간 섬광(아주 짧고 크게)</summary>
        public static ParticleSystem Flash => Get(ref s_flash, "Blast flash", 120, 0.6f, 1.4f, 0f, false);
        /// <summary>
        /// 함체 명중 폭발 불덩이. 정점 알파가 디졸브 진행이라(1 = 막 터짐) 색 알파는 1로 준다.
        /// 수명 동안 부풀며 노랑 → 주황 → 검붉게 식고, 얇은 곳부터 갈라져 사라진다. 재질이 없으면 null.
        /// </summary>
        public static ParticleSystem BlastFire
        {
            get
            {
                if (s_blastFire != null) return s_blastFire;
                if (s_blastMaterial == null) s_blastMaterial = Resources.Load<Material>("Effects/MAT_BlastFire");
                if (s_blastMaterial == null || !s_blastMaterial.shader.isSupported) return null;
                var cool = new Gradient();
                cool.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.62f, 0.38f), 0.35f), new GradientColorKey(new Color(0.42f, 0.2f, 0.14f), 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.82f, 0.3f), new GradientAlphaKey(0f, 1f) });
                return Get(ref s_blastFire, "Blast fireball", 1500, 0.8f, 1.55f, -0.04f, false, material: s_blastMaterial, gradient: cool);
            }
        }

        /// <summary>검증용: 지금 떠 있는 파티클 수.</summary>
        public static int Count(ParticleSystem ps) => ps != null ? ps.particleCount : 0;

        public static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float lifetime, float size, Color color)
        {
            if (ps == null) return;
            if (ps == s_flash) color.a *= Game.Core.DisplaySettings.FlashScale;   // 번쩍임 줄이기(섬광 입자)
            else if (ps != s_sparks && ps != s_missileSmoke && Random.value > Game.Core.DisplaySettings.DecorDensity) return;   // 장식 효과 품질(폭발 불똥·섬광 · 미사일 연기는 그대로 — 연기 꼬리는 위협 방향을 알려 준다, 빠지면 점선이 된다)
            if (Game.Core.DisplaySettings.ThinSmoke && (ps == s_smoke || ps == s_wreckSmoke || ps == s_missileSmoke || ps == s_blastSmoke)) color.a *= 0.5f;   // 연기 옅게
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime,
                startSize = size,
                startColor = color,
                rotation = Random.Range(0f, 360f),
            };
            ps.Emit(p, 1);
        }

        private static ParticleSystem Get(ref ParticleSystem cache, string name, int capacity,
                                          float startScale, float endScale, float gravity, bool fadeIn, bool stretched = false,
                                          Material material = null, Gradient gradient = null)
        {
            if (cache != null) return cache;
            if (s_root == null) s_root = new GameObject("Decor FX").transform;
            if (s_material == null) s_material = Resources.Load<Material>("Effects/MAT_FxSoft");

            var go = new GameObject(name);
            go.transform.SetParent(s_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeIn
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0.6f, 0.55f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.45f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient ?? fade;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, startScale, 1f, endScale));

            // 연기는 올라가며 느려진다
            if (fadeIn)
            {
                var drag = ps.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.drag = 0.35f;
                drag.multiplyDragByParticleSize = false;
                drag.multiplyDragByParticleVelocity = false;
                drag.limit = 100f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            if ((material ?? s_material) != null) r.sharedMaterial = material ?? s_material;
            r.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched) { r.velocityScale = 0.06f; r.lengthScale = 1.2f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.OldestInFront;
            ps.Play();
            cache = ps;
            return ps;
        }
    }
}
