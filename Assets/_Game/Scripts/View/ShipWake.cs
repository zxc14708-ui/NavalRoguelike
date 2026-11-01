using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 배가 지나간 물살(항적). 수면 위에 띠 두 개를 그린다.
    ///   - 켈빈 항적: 선수에서 나와 뒤로 갈수록 V자로 벌어지는 두 줄의 흰 물결.
    ///     실제 배처럼 양쪽 팔이 약 19.5°로 벌어진다(폭 = 선폭 + 2·tan19.5°·속력·경과 시간).
    ///   - 추진기 물살: 선미 뒤로 곧게 남는 거품 띠. 조금씩 넓어지며 더 오래 남는다.
    /// 배가 지나간 자리를 일정 간격으로 기록해 띠를 만들므로 선회하면 물살도 휘어진다.
    /// 배 움직임(2026-11-01): 세게 돌거나 가속하면(휘저음) 추진기 물살이 넓고 하얗게 일고, 거품 둘레에 공기 섞인 밝은 물빛이 남는다.
    ///   정점 색 r = 남은 수명(1 = 막 생김), g = 휘저음(0~1), a = 세기 — 셰이더가 오래된 거품일수록 성기게 흩는다.
    /// 선체 둘레 거품(2026-11-01): 수면선에서 선체를 두르는 거품 고리(둥근 사각, 48칸). 멈춰 있으면 옅게 찰랑이고,
    ///   속력이 붙을수록 짙어지며 선수 쪽이 넓고 하얗다(물이 부딪히는 자리). 배를 따라 움직이는 자식 물체라 띠와 달리 기록이 없다.
    /// 세기는 속력에 비례하고 시간이 지나면 흐려진다. 정지하면 새 물살이 생기지 않는다.
    ///
    /// 플레이어 함선과 수상함은 자동으로 붙는다(ShipController, EnemyController).
    /// 잠수함은 드러났을 때만 물살을 남긴다 — 잠항 중 물살이 위치를 알려 주면 안 된다.
    /// </summary>
    public class ShipWake : MonoBehaviour
    {
        private struct Point
        {
            public Vector3 Position;
            public Vector3 Right;
            public float Time;
            public float Speed;
            public float Strength;
            public float Distance;   // 띠를 따라 누적 거리(무늬용)
            public float Churn;      // 선회·가속으로 휘저은 정도(0~1)
        }

        private sealed class Ribbon
        {
            public readonly List<Point> Points = new(96);
            public GameObject Go;
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public float Lifetime, BaseWidthRatio, SpreadPerSpeed, MaxWidth, FullStrengthSpeed, HeightOffset;
            public bool AtBow;
            /// <summary>켈빈 항적: 가운데는 비우고 양쪽 가장자리의 두 줄만 그린다.</summary>
            public bool Arms;
            public float ArmThickness, ArmThicknessGrowth;
        }

        private const float WaterY = -0.88f;        // 바다 평면(-0.9) 바로 위
        private const float EmitDistance = 0.8f;
        private const float EmitInterval = 0.2f;
        private const float TeleportDistance = 25f;
        private static readonly float KelvinSpread = 2f * Mathf.Tan(19.47f * Mathf.Deg2Rad);   // ≈ 0.707

        private static Material s_kelvinMaterial, s_washMaterial;

        private Ribbon _kelvin, _wash;

        // 선체 둘레 거품
        private const int HullFoamSegments = 48;
        private GameObject _hullFoamGo;
        private Mesh _hullFoam;
        private Color32[] _hullFoamColors;
        private float[] _hullFoamBow;        // 정점마다 선수 쪽 가중(0 선미 ~ 1 선수)
        private Vector3 _hullFoamSize;       // 만든 때의 선체 크기(바뀌면 다시 만든다)

        /// <summary>검증용: 선체 둘레 거품의 지금 세기(선수 쪽 정점 알파, 0~1).</summary>
        public float HullFoamStrength { get; private set; }
        private float _sprayDebt;
        private int _spraySide = 1;

        /// <summary>이 속력(m/s, ≈ 8노트)부터 뱃머리 물보라가 튄다.</summary>
        private const float SprayStartSpeed = 4f;
        private BoxCollider _hull;
        private Vector3 _lastPos;
        private float _speed;
        private bool _hasLast;
        private float _lastYaw, _yawRate, _accel, _churn;

        /// <summary>검증용: 지금 휘저음(0~1).</summary>
        public float Churn => _churn;

        /// <summary>세기 배율(0이면 물살 없음). 잠수함은 드러났을 때만 1.</summary>
        public System.Func<float> StrengthMultiplier;

        // 버퍼(모든 항적이 함께 쓴다)
        private static readonly List<Vector3> s_verts = new(512);
        private static readonly List<Color32> s_colors = new(512);
        private static readonly List<Vector2> s_uvs = new(512);
        private static readonly List<int> s_tris = new(1024);

        /// <summary>없으면 붙인다.</summary>
        public static ShipWake Ensure(GameObject ship, System.Func<float> strength = null)
        {
            if (!ship.TryGetComponent<ShipWake>(out var wake)) wake = ship.AddComponent<ShipWake>();
            wake.StrengthMultiplier = strength;
            return wake;
        }

        public int KelvinPoints => _kelvin != null ? _kelvin.Points.Count : 0;
        public int WashPoints => _wash != null ? _wash.Points.Count : 0;
        public float MeasuredSpeed => _speed;
        public bool Visible => _kelvin != null && _kelvin.Renderer.enabled && _kelvin.Mesh.vertexCount > 0;

        private void Awake()
        {
            _hull = GetComponent<BoxCollider>();
            if (s_kelvinMaterial == null) s_kelvinMaterial = Resources.Load<Material>("Water/MAT_WakeKelvin");
            if (s_washMaterial == null) s_washMaterial = Resources.Load<Material>("Water/MAT_WakeWash");

            _kelvin = CreateRibbon("Kelvin", s_kelvinMaterial);
            _kelvin.AtBow = true;
            _kelvin.Arms = true;
            _kelvin.ArmThickness = 0.7f;
            _kelvin.ArmThicknessGrowth = 0.03f;
            _kelvin.Lifetime = 4.5f;
            _kelvin.BaseWidthRatio = 1.0f;
            _kelvin.SpreadPerSpeed = KelvinSpread;
            _kelvin.MaxWidth = 26f;   // 반폭(13m)이 선회 반경(전속 약 18m)보다 작아야 안쪽 줄이 접히지 않는다
            _kelvin.FullStrengthSpeed = 7f;
            _kelvin.HeightOffset = 0f;

            _wash = CreateRibbon("Wash", s_washMaterial);
            _wash.AtBow = false;
            _wash.Lifetime = 9f;
            _wash.BaseWidthRatio = 0.6f;
            _wash.SpreadPerSpeed = 0.1f;
            _wash.MaxWidth = 11f;
            _wash.FullStrengthSpeed = 6f;
            _wash.HeightOffset = 0.01f;

            if (s_washMaterial != null)
            {
                _hullFoamGo = new GameObject("Hull foam", typeof(MeshFilter), typeof(MeshRenderer));
                _hullFoamGo.transform.SetParent(transform, false);
                _hullFoam = new Mesh { name = "Hull foam" };
                _hullFoam.MarkDynamic();
                _hullFoamGo.GetComponent<MeshFilter>().sharedMesh = _hullFoam;
                var r = _hullFoamGo.GetComponent<MeshRenderer>();
                r.sharedMaterial = s_washMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private Ribbon CreateRibbon(string kind, Material material)
        {
            var go = new GameObject($"Wake {kind} ({name})", typeof(MeshFilter), typeof(MeshRenderer));
            var mesh = new Mesh { name = $"Wake {kind}" };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = material != null;
            return new Ribbon { Go = go, Mesh = mesh, Renderer = r };
        }

        private void OnEnable() => ResetTrail(true);

        private void OnDisable()
        {
            // 풀로 돌아가면(격침·정리) 물살도 거둔다
            ResetTrail(false);
        }

        private void OnDestroy()
        {
            foreach (var r in new[] { _kelvin, _wash })
            {
                if (r == null) continue;
                if (r.Mesh != null) Destroy(r.Mesh);
                if (r.Go != null) Destroy(r.Go);
            }
            if (_hullFoam != null) Destroy(_hullFoam);
        }

        private void ResetTrail(bool visible)
        {
            _hasLast = false;
            _speed = 0f;
            foreach (var r in new[] { _kelvin, _wash })
            {
                if (r == null) continue;
                r.Points.Clear();
                if (r.Mesh != null) r.Mesh.Clear();
                if (r.Go != null) r.Go.SetActive(visible);
            }
            if (_hullFoamGo != null) _hullFoamGo.SetActive(visible);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            // 속력: 실제로 움직인 거리로 잰다(누가 움직이든 같은 방식)
            if (_hasLast && dt > 0f)
            {
                Vector3 delta = pos - _lastPos;
                delta.y = 0f;
                if (delta.magnitude > TeleportDistance) { ResetTrail(true); }
                else
                {
                    float forwardSpeed = Mathf.Abs(Vector3.Dot(delta, Flat(transform.forward))) / dt;
                    float before = _speed;
                    _speed = Mathf.Lerp(_speed, forwardSpeed, 1f - Mathf.Exp(-dt * 6f));
                    float k = 1f - Mathf.Exp(-dt * 3f);
                    _accel = Mathf.Lerp(_accel, (_speed - before) / dt, k);
                    _yawRate = Mathf.Lerp(_yawRate, Mathf.DeltaAngle(_lastYaw, transform.eulerAngles.y) / dt, k);
                }
            }
            _lastPos = pos;
            _lastYaw = transform.eulerAngles.y;
            _hasLast = true;
            // 휘저음: 세게 돌수록(초당 25°면 가득), 가속할수록(초당 3m/s면 가득). 느리면 휘저을 물살도 없다.
            _churn = Mathf.Clamp01(Mathf.Abs(_yawRate) / 25f + Mathf.Max(0f, _accel) / 3f) * Mathf.Clamp01(_speed / 3f);

            float mul = StrengthMultiplier != null ? Mathf.Clamp01(StrengthMultiplier()) : 1f;
            GetHull(out Vector3 bow, out Vector3 stern, out float beam);

            UpdateRibbon(_kelvin, bow, beam, mul);
            UpdateRibbon(_wash, stern, beam, mul);
            EmitBowSpray(dt, bow, beam, mul);
            UpdateHullFoam(mul);
        }

        /// <summary>선체 둘레 거품: 선체 크기가 바뀌면 고리를 다시 만들고, 매 프레임 세기(정점 알파)만 바꾼다.</summary>
        private void UpdateHullFoam(float mul)
        {
            if (_hullFoam == null) return;
            if (_hull == null) _hull = GetComponent<BoxCollider>();
            Vector3 size = _hull != null ? _hull.size : new Vector3(2f, 1f, 6f);
            Vector3 center = _hull != null ? _hull.center : Vector3.zero;
            if (_hullFoamColors == null || (size - _hullFoamSize).sqrMagnitude > 1e-4f) BuildHullFoam(size, center);

            // 수면 높이에 붙인다(배 뿌리 높이가 달라도)
            var t = _hullFoamGo.transform;
            float sy = Mathf.Abs(transform.lossyScale.y) > 1e-4f ? transform.lossyScale.y : 1f;
            t.localPosition = new Vector3(0f, (WaterY + 0.015f - transform.position.y) / sy, 0f);

            float ratio = Mathf.Clamp01(_speed / 6f);
            byte churn = (byte)Mathf.RoundToInt(ratio * 200f);
            float strongest = 0f;
            for (int i = 0; i < _hullFoamColors.Length; i++)
            {
                float bow = _hullFoamBow[i];
                // 멈춰 있으면 옅게 찰랑, 속력이 붙으면 선수부터 짙게(선미 쪽은 추진기 물살이 맡는다)
                float a = (0.22f + ratio * (0.35f + 0.55f * bow)) * mul;
                strongest = Mathf.Max(strongest, a);
                _hullFoamColors[i] = new Color32(255, churn, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
            }
            HullFoamStrength = strongest;
            _hullFoam.colors32 = _hullFoamColors;
        }

        /// <summary>둥근 사각(초타원) 고리. 안쪽은 선체 가장자리 살짝 안(선체가 가림), 바깥은 0.7m + 선수 쪽 최대 +1m. uv.x 0.5(안) → 1(밖).</summary>
        private void BuildHullFoam(Vector3 size, Vector3 center)
        {
            _hullFoamSize = size;
            int n = HullFoamSegments;
            var verts = new Vector3[(n + 1) * 2];
            var uvs = new Vector2[(n + 1) * 2];
            _hullFoamColors = new Color32[(n + 1) * 2];
            _hullFoamBow = new float[(n + 1) * 2];
            var tris = new int[n * 6];
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            float along = 0f;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= n; i++)
            {
                float ang = i * Mathf.PI * 2f / n;
                float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                var p = new Vector3(Mathf.Sign(c) * Mathf.Sqrt(Mathf.Abs(c)) * hx, 0f, Mathf.Sign(s) * Mathf.Sqrt(Mathf.Abs(s)) * hz);
                var outward = new Vector3(p.x / Mathf.Max(0.1f, hx * hx), 0f, p.z / Mathf.Max(0.1f, hz * hz)).normalized;
                float bow = Mathf.Clamp01(s);   // +z = 선수
                float width = 0.7f + 1.0f * bow * bow;
                var inner = new Vector3(center.x, 0f, center.z) + p - outward * 0.15f;
                var outer = new Vector3(center.x, 0f, center.z) + p + outward * width;
                if (i > 0) along += Vector3.Distance(inner, prev);
                prev = inner;
                verts[i * 2] = inner; verts[i * 2 + 1] = outer;
                uvs[i * 2] = new Vector2(0.5f, along); uvs[i * 2 + 1] = new Vector2(1f, along);
                _hullFoamBow[i * 2] = _hullFoamBow[i * 2 + 1] = bow;
                if (i == n) continue;
                int b = i * 2, k = i * 6;
                tris[k] = b; tris[k + 1] = b + 2; tris[k + 2] = b + 1;
                tris[k + 3] = b + 1; tris[k + 4] = b + 2; tris[k + 5] = b + 3;
            }
            _hullFoam.Clear();
            _hullFoam.vertices = verts;
            _hullFoam.uv = uvs;
            _hullFoam.colors32 = _hullFoamColors;
            _hullFoam.triangles = tris;
            _hullFoam.RecalculateBounds();
        }

        /// <summary>
        /// 뱃머리 물보라(장식): 속력이 붙으면 선수 양옆으로 흰 물방울이 튀어 올랐다 떨어진다.
        /// 빠를수록 많고 높게, 배가 클수록 크게. 파티클은 DecorFx.Spray 하나를 모든 배가 나눠 쓴다.
        /// </summary>
        private void EmitBowSpray(float dt, Vector3 bow, float beam, float mul)
        {
            float over = _speed - SprayStartSpeed;
            if (over <= 0f || mul <= 0.01f || dt <= 0f) { _sprayDebt = 0f; return; }

            float size = Mathf.Clamp(beam / 3f, 0.6f, 2f);
            _sprayDebt += over * 5f * size * mul * dt;
            int n = Mathf.Min(10, (int)_sprayDebt);
            _sprayDebt -= n;
            if (n == 0) return;

            Vector3 fwd = Flat(transform.forward), right = Flat(transform.right);
            float k = Mathf.Clamp(Mathf.Sqrt(_speed / 10f), 0.6f, 1.5f);
            for (int i = 0; i < n; i++)
            {
                _spraySide = -_spraySide;
                Vector3 p = bow - fwd * (beam * Random.Range(0.1f, 0.6f)) + right * (_spraySide * beam * Random.Range(0.3f, 0.5f));
                p.y = WaterY + 0.35f;
                Vector3 v = right * (_spraySide * Random.Range(1.4f, 3.2f) * k) + Vector3.up * Random.Range(2f, 3.6f) * k
                            + fwd * (_speed * Random.Range(0.3f, 0.6f));
                DecorFx.Emit(DecorFx.Spray, p, v, Random.Range(0.45f, 0.8f), Random.Range(0.6f, 1.4f) * size,
                    new Color(0.93f, 0.97f, 1f, Random.Range(0.4f, 0.7f)));
            }
        }

        /// <summary>검증용: 뱃머리 물보라가 지금 튀고 있는가(속력 기준).</summary>
        public bool Spraying => _speed > SprayStartSpeed;

        private void GetHull(out Vector3 bow, out Vector3 stern, out float beam)
        {
            if (_hull == null) _hull = GetComponent<BoxCollider>();
            Vector3 center = _hull != null ? _hull.center : Vector3.zero;
            Vector3 size = _hull != null ? _hull.size : new Vector3(2f, 1f, 6f);
            Vector3 scale = transform.lossyScale;
            bow = transform.TransformPoint(new Vector3(center.x, 0f, center.z + size.z * 0.5f));
            stern = transform.TransformPoint(new Vector3(center.x, 0f, center.z - size.z * 0.5f));
            beam = Mathf.Max(0.5f, size.x * Mathf.Abs(scale.x));
        }

        private void UpdateRibbon(Ribbon r, Vector3 origin, float beam, float mul)
        {
            if (r.Mesh == null) return;
            float now = Time.time;

            // 오래된 점 버리기
            int drop = 0;
            while (drop < r.Points.Count && now - r.Points[drop].Time > r.Lifetime) drop++;
            if (drop > 0) r.Points.RemoveRange(0, drop);

            // 새 점: 일정 거리나 시간마다
            origin.y = WaterY + r.HeightOffset;
            float strength = Mathf.Clamp01(_speed / r.FullStrengthSpeed) * mul;
            var last = r.Points.Count > 0 ? r.Points[r.Points.Count - 1] : default;
            bool due = r.Points.Count == 0
                       || (origin - last.Position).sqrMagnitude >= EmitDistance * EmitDistance
                       || now - last.Time >= EmitInterval;
            if (due)
            {
                float dist = r.Points.Count > 0 ? last.Distance + Vector3.Distance(origin, last.Position) : 0f;
                r.Points.Add(new Point
                {
                    Position = origin,
                    Right = Flat(transform.right),
                    Time = now,
                    Speed = _speed,
                    Strength = strength,
                    Distance = dist,
                    Churn = _churn,
                });
            }

            BuildMesh(r, origin, beam, strength, now);
        }

        /// <summary>기록한 점 + 지금 위치(머리)를 이어 띠를 만든다. 가장 새 점이 배에 붙어 있다.</summary>
        private void BuildMesh(Ribbon r, Vector3 head, float beam, float headStrength, float now)
        {
            s_verts.Clear();
            s_colors.Clear();
            s_uvs.Clear();
            s_tris.Clear();

            int n = r.Points.Count;
            float headDistance = n > 0 ? r.Points[n - 1].Distance + Vector3.Distance(head, r.Points[n - 1].Position) : 0f;
            bool any = false;

            for (int i = 0; i <= n; i++)
            {
                Point p = i < n
                    ? r.Points[i]
                    : new Point { Position = head, Right = Flat(transform.right), Time = now, Speed = _speed, Strength = headStrength, Distance = headDistance, Churn = _churn };

                float age = Mathf.Max(0f, now - p.Time);
                float life = 1f - Mathf.Clamp01(age / r.Lifetime);
                float width = Mathf.Min(r.MaxWidth, beam * r.BaseWidthRatio + r.SpreadPerSpeed * p.Speed * age);
                if (!r.Arms) width = Mathf.Min(r.MaxWidth * 1.3f, width * (1f + 0.6f * p.Churn));   // 휘저은 물살은 넓게 인다

                // 배 바로 뒤는 선체에 가려지므로 머리 쪽 0.25초는 서서히 나타나게(선체와 겹쳐 번쩍이지 않게)
                float fadeIn = Mathf.Clamp01(age / 0.25f);
                float a = p.Strength * life * life * Mathf.Lerp(0.35f, 1f, fadeIn);
                if (a > 0.004f) any = true;
                byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);

                var c = new Color32((byte)Mathf.RoundToInt(life * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(p.Churn) * 255f), 255, alpha);
                if (r.Arms)
                {
                    // 양쪽 팔: 가장자리를 따라 두 줄(각 줄의 가로 uv 0~1)
                    float half = width * 0.5f;
                    float t = Mathf.Min(half, (r.ArmThickness + r.ArmThicknessGrowth * p.Speed * age) * 0.5f);
                    Vector3 R = p.Right;
                    s_verts.Add(p.Position - R * (half + t));
                    s_verts.Add(p.Position - R * (half - t));
                    s_verts.Add(p.Position + R * (half - t));
                    s_verts.Add(p.Position + R * (half + t));
                    for (int k = 0; k < 4; k++) s_colors.Add(c);
                    s_uvs.Add(new Vector2(0f, p.Distance));
                    s_uvs.Add(new Vector2(1f, p.Distance));
                    s_uvs.Add(new Vector2(0f, p.Distance));
                    s_uvs.Add(new Vector2(1f, p.Distance));

                    if (i == 0) continue;
                    int b = (i - 1) * 4;
                    AddQuad(b, b + 1, b + 4, b + 5);
                    AddQuad(b + 2, b + 3, b + 6, b + 7);
                    continue;
                }

                Vector3 w = p.Right * (width * 0.5f);
                s_verts.Add(p.Position - w);
                s_verts.Add(p.Position + w);
                s_colors.Add(c);
                s_colors.Add(c);
                s_uvs.Add(new Vector2(0f, p.Distance));
                s_uvs.Add(new Vector2(1f, p.Distance));

                if (i == 0) continue;
                int q = (i - 1) * 2;
                AddQuad(q, q + 1, q + 2, q + 3);
            }

            r.Mesh.Clear();
            if (!any || n == 0) return;
            r.Mesh.SetVertices(s_verts);
            r.Mesh.SetColors(s_colors);
            r.Mesh.SetUVs(0, s_uvs);
            r.Mesh.SetTriangles(s_tris, 0, true);
        }

        /// <summary>이전 점의 두 정점(a, b)과 다음 점의 두 정점(c, d)을 잇는다.</summary>
        private static void AddQuad(int a, int b, int c, int d)
        {
            s_tris.Add(a); s_tris.Add(c); s_tris.Add(b);
            s_tris.Add(b); s_tris.Add(c); s_tris.Add(d);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.right;
        }
    }
}
