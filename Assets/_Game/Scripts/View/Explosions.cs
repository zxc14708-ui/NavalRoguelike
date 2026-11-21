using System.Collections.Generic;
using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 폭발 연출(판정 없음). 미사일 명중·요격, 자폭 보트·자폭 드론의 자폭과 격추에 쓴다.
    ///   섬광 → 불덩이(바깥으로 퍼짐) → 불똥(포물선으로 떨어짐) → 연기 + 짧은 주황 조명.
    ///   수면 가까이서 터지면 흰 물기둥과 물보라가 함께 솟는다. 공중이면 연기가 적고 불똥이 사방으로.
    /// 2026-11-21 다듬기(사용자: 폭발 효과 손보기):
    ///   - 불빛에 물든 연기: 불덩이 자리에 검붉은 연기가 잠깐 — 불과 검은 연기 사이가 이어진다.
    ///   - 검은 연기 기둥(함체 명중 · 자폭, 크기 0.5 이상): 아래는 좁고 위로 갈수록 빠르고 넓게 — 감속하며 위가 버섯처럼 퍼진다.
    ///   - 파편: 어두운 조각이 포물선으로 튀었다 떨어진다.
    ///   - 공중 폭발: 제자리에 오래 떠 있는 검은 연기 뭉치(대공포 탄막처럼) — 바람에 흘러간다.
    ///   - 수면: 빠르게 퍼지는 충격파 고리 + 천천히 번지는 물거품 고리, 물기둥 끝에서 물방울 왕관이 사방으로 떨어진다.
    ///   - 조명: 6개를 돌려 쓰고, 큰 폭발일수록 오래(0.35~0.75초), 밤일수록 밝다.
    /// 파티클은 DecorFx 공용 시스템을 쓴다.
    /// </summary>
    public static class Explosions
    {
        public enum Kind
        {
            /// <summary>함체·표적 명중</summary>
            Impact,
            /// <summary>공중 요격·격추</summary>
            Air,
            /// <summary>수면 명중(빗나감·추락)</summary>
            Water,
            /// <summary>자폭(폭약) — 크고 물기둥이 높다</summary>
            Charge,
        }

        private const float WaterY = -0.9f;
        private const int MaxLights = 6;
        private const int MaxRings = 10;

        /// <summary>검증용: 지금까지 만든 폭발 수.</summary>
        public static int SpawnCount { get; private set; }
        /// <summary>검증용: 지금까지 띄운 수면 고리 수 · 연기 기둥 수.</summary>
        public static int RingCount { get; private set; }
        public static int ColumnCount { get; private set; }
        /// <summary>불덩이를 디졸브 불덩이(DecorFx.BlastFire)로 그린다(함체 명중 · 자폭 · 공중 · 격침). 검증에서 예전 모양과 나란히 찍을 때만 끈다.</summary>
        public static bool DissolveFire = true;

        private static readonly List<Light> s_lights = new();
        private static int s_nextLight;
        private static FlashDriver s_driver;

        /// <summary>폭발 하나. size 1 ≈ 대함미사일 명중(불덩이 지름 약 4m).</summary>
        public static void Spawn(Vector3 position, float size, Kind kind, bool counted = true)
        {
            if (counted) SpawnCount++;   // 격침 잔해의 장식 폭발은 세지 않는다(검증이 무장 폭발 수를 센다)
            size = Mathf.Max(0.2f, size);
            float root = Mathf.Sqrt(size);
            float night = DayNight.Darkness01;
            bool nearWater = kind == Kind.Water || kind == Kind.Charge || position.y < WaterY + 2.5f && kind != Kind.Air;
            bool big = size >= 0.5f;

            // 섬광
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.14f, 4.2f * size, new Color(1f, 0.95f, 0.8f, 0.95f));
            DecorFx.Emit(DecorFx.Flash, position, Vector3.zero, 0.22f, 2.6f * size, new Color(1f, 0.72f, 0.3f, 0.9f));

            // 불덩이 — 갈라지며 타 사라지는 큰 덩이(디졸브)에 부드러운 원 몇 개를 곁들인다. 수면 명중은 물기둥이 주인공이라 예전 원만.
            var blast = DissolveFire ? BlastBlobs(kind, size) : 0;
            for (int i = 0; i < blast; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                // 자폭은 낮고 넓게 부풀고, 함체 명중은 위로, 공중은 사방으로
                if (kind == Kind.Charge) dir = new Vector3(dir.x * 1.3f, Mathf.Abs(dir.y) * 0.6f + 0.15f, dir.z * 1.3f);
                else if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                float life = kind == Kind.Air ? Random.Range(0.4f, 0.7f) : kind == Kind.Charge ? Random.Range(0.65f, 1.1f) : Random.Range(0.55f, 0.95f);
                float speed = kind == Kind.Charge ? Random.Range(2.2f, 5f) : Random.Range(1.6f, 4.2f);
                DecorFx.Emit(DecorFx.BlastFire, position + dir * 0.35f * size, dir * speed * size, life, Random.Range(1.4f, 2.4f) * size,
                    Color.Lerp(new Color(1f, 0.93f, 0.62f, 1f), new Color(1f, 0.5f, 0.16f, 1f), Random.value));
            }
            int fire = blast > 0 ? Mathf.RoundToInt(3 + 2 * size) : Mathf.RoundToInt(10 + 8 * size);
            for (int i = 0; i < fire; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                DecorFx.Emit(DecorFx.Fire, position + dir * 0.3f * size, dir * Random.Range(2.5f, 6f) * size,
                    Random.Range(0.3f, 0.65f), Random.Range(1.1f, 2.3f) * size,
                    Color.Lerp(new Color(1f, 0.9f, 0.55f, 1f), new Color(1f, 0.36f, 0.08f, 0.9f), Random.value));
            }

            // 불똥
            int sparks = Mathf.RoundToInt(12 + 10 * size);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y) + 0.35f;
                DecorFx.Emit(DecorFx.Sparks, position, dir.normalized * Random.Range(7f, 15f) * root,
                    Random.Range(0.45f, 1f), Random.Range(0.14f, 0.3f) * root,
                    new Color(1f, Random.Range(0.55f, 0.85f), 0.25f, 1f));
            }

            // 파편: 어두운 조각이 높이 튀었다 떨어진다(함체 명중 · 자폭)
            if (big && (kind == Kind.Impact || kind == Kind.Charge))
            {
                int debris = Mathf.RoundToInt(4 + 5 * size);
                for (int i = 0; i < debris; i++)
                {
                    Vector3 dir = Random.onUnitSphere;
                    dir.y = Mathf.Abs(dir.y) * 1.2f + 0.5f;
                    float g = Random.Range(0.08f, 0.2f);
                    DecorFx.Emit(DecorFx.Sparks, position + dir * 0.2f * size, dir.normalized * Random.Range(5f, 11f) * root,
                        Random.Range(0.9f, 1.6f), Random.Range(0.22f, 0.4f) * root, new Color(g, g * 0.95f, g * 0.9f, 1f));
                }
            }

            // 불빛에 물든 연기: 불덩이 자리에서 검붉은 연기가 잠깐 부푼다(불과 검은 연기 사이를 잇는다)
            int lit = Mathf.RoundToInt((kind == Kind.Air ? 2 : 4) + 3 * size);
            for (int i = 0; i < lit; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                if (kind != Kind.Air) dir.y = Mathf.Abs(dir.y);
                float w = Random.Range(0.75f, 1.1f);
                DecorFx.Emit(DecorFx.BlastSmoke, position + dir * 0.7f * size, dir * 1.4f * size + Vector3.up * 0.8f * root,
                    Random.Range(0.9f, 1.4f), Random.Range(1.4f, 2.1f) * size,
                    new Color(0.42f * w, 0.2f * w, 0.1f * w, 0.75f));
            }

            if (kind == Kind.Air)
            {
                // 공중: 제자리에 떠 있는 검은 연기 뭉치 — 대공포 탄막처럼 오래 남아 바람에 흘러간다
                int puffs = big ? Mathf.RoundToInt(5 + 4 * size) : Mathf.RoundToInt(3 + 5 * size);
                for (int i = 0; i < puffs; i++)
                {
                    Vector3 dir = Random.insideUnitSphere;
                    float g = Random.Range(0.12f, 0.22f) + 0.08f * night;
                    DecorFx.Emit(DecorFx.BlastSmoke, position + dir * 0.5f * size, dir * 1.1f * size + DecorFx.Wind * 0.6f + Vector3.up * 0.3f,
                        big ? Random.Range(2.6f, 4f) : Random.Range(1.4f, 2.2f), Random.Range(0.9f, 1.5f) * size, new Color(g, g, g, 0.62f));
                }
            }
            else
            {
                // 퍼지는 검은 연기
                int smoke = Mathf.RoundToInt(5 + 3 * size);
                for (int i = 0; i < smoke; i++)
                {
                    Vector3 dir = Random.insideUnitSphere;
                    Vector3 v = dir * 1.6f * size + Vector3.up * Random.Range(1.2f, 2.6f) * root + DecorFx.Wind * 0.5f;
                    float g = Random.Range(0.1f, 0.2f);
                    DecorFx.Emit(DecorFx.WreckSmoke, position + dir * 0.6f * size, v, Random.Range(1.8f, 3.2f),
                        Random.Range(1.2f, 2.2f) * size, new Color(g, g, g, kind == Kind.Water ? 0.35f : 0.6f));
                }
                // 검은 연기 기둥: 아래는 좁고 느리게, 위로 갈수록 빠르고 넓게 — 연기가 감속하며 꼭대기가 버섯처럼 퍼진다
                if (big && kind != Kind.Water)
                {
                    ColumnCount++;
                    int n = Mathf.RoundToInt(14 + 10 * size);
                    for (int i = 0; i < n; i++)
                    {
                        float h = n > 1 ? i / (float)(n - 1) : 0f;
                        Vector2 r = Random.insideUnitCircle * Mathf.Lerp(0.25f, 1.5f, h * h) * size;
                        Vector3 v = new Vector3(r.x, Mathf.Lerp(1.2f, 6.5f, h) * root, r.y) + DecorFx.Wind * 0.3f;
                        float g = Random.Range(0.07f, 0.16f);
                        DecorFx.Emit(DecorFx.BlastSmoke, position + Vector3.up * 0.4f * size + new Vector3(r.x, 0f, r.y) * 0.3f, v,
                            Random.Range(3.8f, 5.6f), Mathf.Lerp(1.5f, 2.6f, h) * size, new Color(g, g, g, 0.78f));
                    }
                }
            }

            // 물기둥·물보라
            if (nearWater) WaterBurst(position, size, kind == Kind.Charge ? 1.5f : kind == Kind.Water ? 1.1f : 0.7f, kind == Kind.Charge ? 9f : 6.5f, kind == Kind.Charge ? 5f : 3.6f, kind == Kind.Charge || kind == Kind.Water && size >= 1f, night);

            float lightDur = 0.35f + 0.2f * Mathf.Clamp(size - 0.5f, 0f, 2f);
            FlashLight(position + Vector3.up * 0.5f, size, lightDur);
        }

        /// <summary>
        /// 물보라만(2026-11-21 — 불 없이): 흰 물기둥 · 물방울 왕관 · 낮은 물안개 · 충격파 · 물거품 고리. 반잠수정이 떠오를 때 · 보트가 튀어나올 때.
        /// </summary>
        public static void Splash(Vector3 position, float size)
        {
            size = Mathf.Max(0.2f, size);
            WaterBurst(position, size, 1.1f, 5.5f, 3.4f, false, DayNight.Darkness01);
        }

        private static void WaterBurst(Vector3 position, float size, float column, float shock, float foam, bool trace, float night)
        {
            float root = Mathf.Sqrt(size);
            Vector3 at = new(position.x, SurfaceY() + 0.2f, position.z);
            float shade = Mathf.Lerp(1f, 0.55f, night);   // 밤 물보라는 어둡게
            var water = new Color(0.94f * shade, 0.97f * shade, 1f * shade, 1f);
            int n = Mathf.RoundToInt((12 + 8 * size) * column);
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                Vector3 v = new Vector3(r.x * 2.2f, Random.Range(6f, 12f) * column, r.y * 2.2f) * root;
                water.a = Random.Range(0.55f, 0.85f);
                DecorFx.Emit(DecorFx.Spray, at + new Vector3(r.x, 0f, r.y) * 0.8f * size, v, Random.Range(0.9f, 1.5f),
                    Random.Range(1.0f, 2.0f) * size, water);
            }
            // 물방울 왕관: 물기둥 둘레에서 늘어난 물방울이 사방으로 솟았다 떨어진다
            int drops = Mathf.RoundToInt((18 + 16 * size) * column);
            for (int i = 0; i < drops; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                float up = Random.Range(4f, 11f) * column;
                Vector3 v = new Vector3(r.x * Random.Range(1.5f, 5f), up, r.y * Random.Range(1.5f, 5f)) * root;
                water.a = Random.Range(0.7f, 0.95f);
                DecorFx.Emit(DecorFx.Drops, at + new Vector3(r.x, 0f, r.y) * Random.Range(0.4f, 1.2f) * size, v,
                    Random.Range(1.1f, 1.8f), Random.Range(0.1f, 0.22f) * root, water);
            }
            // 수면에 낮게 깔리는 물안개
            for (int i = 0; i < n / 2; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized;
                Vector3 v = new Vector3(r.x * Random.Range(3f, 6f), Random.Range(0.8f, 2f), r.y * Random.Range(3f, 6f)) * root;
                water.a = Random.Range(0.35f, 0.55f);
                DecorFx.Emit(DecorFx.Spray, at, v, Random.Range(0.8f, 1.3f), Random.Range(1f, 1.8f) * size, water);
            }
            // 충격파 고리(빠르게) + 물거품 고리(천천히)
            if (size >= 0.4f)
            {
                Ring(at, 0.6f * size, shock * size, 0.45f + 0.1f * size, 0.85f);
                Ring(at, 0.8f * size, foam * size, 2.2f + 0.4f * size, 0.55f);
            }
            if (trace) OceanSurface.SpawnSinkingTrace(at);
        }

        // ------------------------------------------------------------ 수면 고리

        private static OceanSurface s_ocean;
        private static float s_oceanLookup = -1f;

        /// <summary>바다 높이(바다가 없으면 기본값). 찾기는 5초에 한 번만.</summary>
        private static float SurfaceY()
        {
            if (s_ocean == null && Time.unscaledTime >= s_oceanLookup)
            {
                s_oceanLookup = Time.unscaledTime + 5f;
                s_ocean = Object.FindFirstObjectByType<OceanSurface>();
            }
            return s_ocean != null ? s_ocean.transform.position.y : WaterY;
        }

        private static readonly List<RingFx> s_rings = new();
        private static Mesh s_ringMesh;
        private static Material s_ringMaterial;
        private static readonly int FoamColorId = Shader.PropertyToID("_FoamColor");

        private sealed class RingFx
        {
            public GameObject Object;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Props;
            public float Age, Duration, From, To, Alpha;
            public bool Active;
        }

        /// <summary>수면에 퍼지는 고리 하나(10개를 돌려 쓴다 — 가장 오래된 것부터 다시 쓴다).</summary>
        private static void Ring(Vector3 at, float from, float to, float duration, float alpha)
        {
            if (s_ringMaterial == null) s_ringMaterial = Resources.Load<Material>("Water/MAT_WakeKelvin");
            if (s_ringMaterial == null) return;
            EnsureDriver();
            if (s_ringMesh == null) s_ringMesh = BuildRingMesh();
            RingFx ring = null;
            foreach (var r in s_rings) if (!r.Active && r.Object != null) { ring = r; break; }
            if (ring == null && s_rings.Count < MaxRings)
            {
                var go = new GameObject("Blast ring", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(s_driver.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = s_ringMesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = s_ringMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                ring = new RingFx { Object = go, Renderer = mr, Props = new MaterialPropertyBlock() };
                s_rings.Add(ring);
            }
            if (ring == null)
                foreach (var r in s_rings) if (r.Object != null && (ring == null || r.Age / r.Duration > ring.Age / ring.Duration)) ring = r;
            if (ring == null) return;
            RingCount++;
            ring.Object.transform.SetPositionAndRotation(new Vector3(at.x, SurfaceY() + 0.09f + 0.004f * (RingCount % 5), at.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ring.Object.transform.localScale = new Vector3(from, 1f, from);
            ring.Age = 0f; ring.Duration = duration; ring.From = from; ring.To = to; ring.Alpha = alpha;
            ring.Active = true;
            ring.Object.SetActive(true);
            ring.Props.SetColor(FoamColorId, new Color(0.9f, 0.96f, 1f, alpha));
            ring.Renderer.SetPropertyBlock(ring.Props);
        }

        private static void UpdateRings(float dt)
        {
            float shade = Mathf.Lerp(1f, 0.82f, DayNight.Darkness01);   // 밤에도 고리가 보이게 조금만 어둡게
            foreach (var r in s_rings)
            {
                if (!r.Active) continue;
                r.Age += dt;
                if (r.Age >= r.Duration || r.Object == null) { r.Active = false; if (r.Object != null) r.Object.SetActive(false); continue; }
                float k = r.Age / r.Duration;
                float ease = 1f - (1f - k) * (1f - k) * (1f - k);   // 처음엔 빠르고 끝으로 느려진다
                float radius = Mathf.Lerp(r.From, r.To, ease);
                r.Object.transform.localScale = new Vector3(radius, 1f, radius);
                r.Props.SetColor(FoamColorId, new Color(0.9f * shade, 0.96f * shade, shade, r.Alpha * Mathf.Pow(1f - k, 1.3f)));
                r.Renderer.SetPropertyBlock(r.Props);
            }
        }

        /// <summary>얇고 살짝 울퉁불퉁한 고리(반지름 1).</summary>
        private static Mesh BuildRingMesh()
        {
            const int segments = 56;
            var vertices = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float uneven = 1f + 0.05f * Mathf.Sin(angle * 6f) + 0.03f * Mathf.Sin(angle * 11f + 1.7f);
                var d = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = d * (uneven * 0.84f);
                vertices[i * 2 + 1] = d * (uneven * 1.04f);
                uvs[i * 2] = new Vector2(0f, i * 0.3f);
                uvs[i * 2 + 1] = new Vector2(1f, i * 0.3f);
                colors[i * 2] = colors[i * 2 + 1] = new Color32(255, 255, 255, 255);
                if (i == segments) continue;
                int t = i * 6, v = i * 2;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Blast ring" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>검증용: 지금 퍼지고 있는 수면 고리 수.</summary>
        public static int ActiveRings
        {
            get { int n = 0; foreach (var r in s_rings) if (r.Active) n++; return n; }
        }

        /// <summary>검증용: 포탄 명중 연출 수.</summary>
        public static int ShellHits { get; private set; }
        /// <summary>검증용: 그중 큰 포탄(피해 6 이상) 명중 연출 수.</summary>
        public static int BigShellHits { get; private set; }

        /// <summary>한 발 피해 → 명중 연출 크기(76mm 24 = 1, 기관포 2 · 근접방어무기 0.75 = 0.3, 최대 1.5).</summary>
        public static float CaliberScale(float damage) => Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, damage) / 24f), 0.3f, 1.5f);

        /// <summary>
        /// 포탄이 선체에 맞은 자리(2026-11-20 타격감). 구경(한 발 피해)에 비례한다 — 작은 탄은 짧은 섬광과 불똥 몇 개,
        /// 큰 포탄(피해 6 이상)은 불덩이 · 연기 · 튀는 파편 · 짧은 조명까지. 불똥은 맞은 면에서 탄이 온 쪽으로 튄다.
        /// 미사일 · 자폭 같은 폭발 수(SpawnCount)에는 세지 않는다.
        /// </summary>
        public static void ShellHit(Vector3 point, Vector3 travel, float damage)
        {
            ShellHits++;
            float s = CaliberScale(damage);
            float root = Mathf.Sqrt(s);
            Vector3 back = travel.sqrMagnitude > 1e-4f ? -travel.normalized : Vector3.up;

            DecorFx.Emit(DecorFx.Flash, point, Vector3.zero, 0.06f + 0.04f * s, 2.2f * s, new Color(1f, 0.96f, 0.84f, 0.95f));
            int sparks = Mathf.RoundToInt(3 + 10 * s);
            for (int i = 0; i < sparks; i++)
            {
                Vector3 dir = back * 0.6f + Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.25f;
                DecorFx.Emit(DecorFx.Sparks, point, dir.normalized * Random.Range(5f, 11f) * root,
                    Random.Range(0.25f, 0.55f), Random.Range(0.1f, 0.2f) * root,
                    new Color(1f, Random.Range(0.62f, 0.9f), 0.3f, 1f));
            }
            if (damage < 6f) return;
            BigShellHits++;

            // 큰 포탄: 불덩이 · 연기 · 어두운 파편 · 조명
            int blobs = DecorFx.BlastFire != null ? Mathf.RoundToInt(2 + 3 * s) : 0;
            for (int i = 0; i < blobs; i++)
            {
                Vector3 dir = back * 0.5f + Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                DecorFx.Emit(DecorFx.BlastFire, point + dir * 0.15f * s, dir.normalized * Random.Range(1.2f, 2.8f) * s,
                    Random.Range(0.3f, 0.5f), Random.Range(0.8f, 1.3f) * s,
                    Color.Lerp(new Color(1f, 0.93f, 0.62f, 1f), new Color(1f, 0.5f, 0.16f, 1f), Random.value));
            }
            if (blobs == 0)
                for (int i = 0; i < 3; i++)
                    DecorFx.Emit(DecorFx.Fire, point, Random.onUnitSphere * 2f * s, Random.Range(0.2f, 0.4f), Random.Range(0.7f, 1.2f) * s,
                        new Color(1f, 0.7f, 0.3f, 1f));
            int debris = Mathf.RoundToInt(2 + 3 * s);
            for (int i = 0; i < debris; i++)
            {
                Vector3 dir = back * 0.4f + Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.6f;
                float g = Random.Range(0.12f, 0.25f);
                DecorFx.Emit(DecorFx.Sparks, point, dir.normalized * Random.Range(3f, 6.5f) * root,
                    Random.Range(0.6f, 0.95f), Random.Range(0.16f, 0.26f) * root, new Color(g, g, g, 1f));
            }
            int smoke = Mathf.RoundToInt(2 + 3 * s);
            for (int i = 0; i < smoke; i++)
            {
                float g = Random.Range(0.14f, 0.26f);
                DecorFx.Emit(DecorFx.WreckSmoke, point + Random.insideUnitSphere * 0.3f * s,
                    Vector3.up * Random.Range(0.8f, 1.6f) + back * 0.6f + DecorFx.Wind * 0.4f,
                    Random.Range(1f, 1.8f), Random.Range(0.7f, 1.2f) * s, new Color(g, g, g, 0.5f));
            }
            FlashLight(point + back * 0.4f + Vector3.up * 0.3f, 0.2f * s, 0.35f);
        }

        /// <summary>
        /// 디졸브 불덩이 수. 함체 명중 6~11 · 자폭 7~14 · 공중 요격·격추 4~7(포구 섬광처럼 아주 작은 것은 0) · 수면 명중 0.
        /// 재질이 없으면(Resources에서 못 찾음) 0 — 예전 부드러운 원으로 그린다.
        /// </summary>
        public static int BlastBlobs(Kind kind, float size)
        {
            if (DecorFx.BlastFire == null) return 0;
            return kind switch
            {
                Kind.Impact => Mathf.RoundToInt(6 + 4 * size),
                Kind.Charge => Mathf.RoundToInt(7 + 5 * size),
                Kind.Air => size >= 0.4f ? Mathf.RoundToInt(4 + 2 * size) : 0,
                _ => 0,
            };
        }

        /// <summary>격침 순간의 불덩이 — 갑판 위 여러 점에서 디졸브 불덩이를 띄운다(ShipWreck). 재질이 없으면 false.</summary>
        public static bool DeckBlast(System.Func<Vector3> point, float scale, int count)
        {
            if (!DissolveFire || DecorFx.BlastFire == null) return false;
            for (int i = 0; i < count; i++)
            {
                Vector3 r = Random.insideUnitSphere; r.y = Mathf.Abs(r.y) * 0.7f + 0.3f;
                DecorFx.Emit(DecorFx.BlastFire, point(), r * 2.4f * scale + Vector3.up * Random.Range(1.2f, 3f),
                    Random.Range(0.6f, 1.05f), Random.Range(1.3f, 2.3f) * scale,
                    Color.Lerp(new Color(1f, 0.9f, 0.58f, 1f), new Color(1f, 0.46f, 0.14f, 1f), Random.value));
            }
            return true;
        }

        // ------------------------------------------------------------ 조명

        /// <summary>짧은 점광(0.35초에 꺼짐). 폭발 말고도 섬광 연출(기만체 발사·폭발)에 쓴다. 6개를 돌려 쓴다.</summary>
        public static void FlashLight(Vector3 position, float size, Color color)
        {
            FlashLight(position, size, 0.35f);
            var used = s_lights[(s_nextLight + s_lights.Count - 1) % s_lights.Count];
            used.color = color;
        }

        private static void EnsureDriver()
        {
            if (s_driver != null) return;
            s_lights.Clear();
            s_rings.Clear();
            var root = new GameObject("Explosion lights");
            s_driver = root.AddComponent<FlashDriver>();
            for (int i = 0; i < MaxLights; i++)
            {
                var go = new GameObject($"Blast light {i}");
                go.transform.SetParent(root.transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.28f);
                l.shadows = LightShadows.None;
                l.enabled = false;
                s_lights.Add(l);
            }
        }

        private static void FlashLight(Vector3 position, float size, float duration)
        {
            EnsureDriver();
            var light = s_lights[s_nextLight];
            s_nextLight = (s_nextLight + 1) % s_lights.Count;
            light.transform.position = position;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 10f + 8f * size;
            float nightBoost = Mathf.Lerp(0.85f, 1.3f, DayNight.Darkness01);   // 밤에는 더 밝게(바다 · 배가 폭발 불빛에 물든다)
            s_driver.Begin(light, (6f + 4f * size) * nightBoost * Game.Core.DisplaySettings.FlashScale, duration);   // 번쩍임 줄이기
        }

        /// <summary>조명 세기를 짧게 줄이고, 수면 고리를 퍼뜨린다.</summary>
        private sealed class FlashDriver : MonoBehaviour
        {
            private readonly Dictionary<Light, (float peak, float t, float dur)> _active = new();
            private readonly List<Light> _keys = new();

            public void Begin(Light light, float peak, float duration)
            {
                _active[light] = (peak, 0f, Mathf.Max(0.05f, duration));
                light.intensity = peak;
                light.enabled = true;
            }

            private void Update()
            {
                UpdateRings(Time.deltaTime);
                _keys.Clear();
                _keys.AddRange(_active.Keys);
                foreach (var l in _keys)
                {
                    var (peak, t, dur) = _active[l];
                    t += Time.deltaTime;
                    if (l == null) { _active.Remove(l); continue; }
                    if (t >= dur) { l.enabled = false; _active.Remove(l); continue; }
                    float k = 1f - t / dur;
                    l.intensity = peak * k * k * (0.85f + 0.15f * Mathf.PerlinNoise(t * 30f, l.GetInstanceID() * 0.1f));   // 불길처럼 살짝 일렁인다
                    _active[l] = (peak, t, dur);
                }
            }
        }
    }
}
