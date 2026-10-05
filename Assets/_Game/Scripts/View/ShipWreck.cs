using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 격침 연출(장식, 판정 없음). 수상함이 격침되면 적 본체는 바로 풀로 돌아가고,
    /// 그 자리에 모델 겉모습만 복사한 잔해가 남아 기울며 가라앉는다.
    ///   1) 불덩이·검은 연기가 터지고 남은 속력으로 조금 미끄러진다.
    ///   2) 한쪽으로 기울고(횡경사) 선수나 선미부터 들리며 가라앉는다. 갑판에서 불꽃과 연기 기둥이 오른다.
    ///   3) 절반쯤 가라앉으면 기름띠가 번지고 잔해 조각이 떠오른다. 선체가 잠길 때 수면에 포말 고리(OceanSurface).
    ///   4) 기름띠 위에서 옅은 연기가 잠시 더 오르고, 기름띠·잔해는 천천히 옅어져 사라진다.
    /// 배가 클수록(선체 길이) 오래·크게. 콜라이더·표적 등록이 없어 전투에는 아무 영향이 없다.
    /// 잠항 중인 잠수함처럼 보이는 모델이 없으면 만들지 않는다.
    /// </summary>
    public class ShipWreck : MonoBehaviour
    {
        public static readonly List<ShipWreck> Active = new();
        private const int MaxActive = 8;
        private const float WaterY = -0.9f;
        private const float SlickLife = 16f;

        private static Mesh s_quad, s_cube;
        private static Material s_slickMaterial;
        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        private struct Debris
        {
            public Transform T;
            public Vector3 Velocity;
            public float Phase, Spin, Yaw, Life;
        }

        private Transform _body;
        private float _length, _beam, _height, _scale, _deck;
        private float _duration, _depth, _rollTarget, _pitchTarget, _speed, _yawRate;
        private Vector3 _startPos;
        private Quaternion _startRot;
        private float _sinkY;
        private float _smokeDebt, _fireDebt;
        private bool _slickSpawned, _foamSpawned, _bodyGone;
        private GameObject _slick;
        private MaterialPropertyBlock _slickProps;
        private float _slickAge, _slickSize;
        private readonly List<Debris> _debris = new();
        private Material _debrisMaterial;

        /// <summary>검증용</summary>
        public float Age { get; private set; }
        public float Duration => _duration;
        public float RollDegrees { get; private set; }
        public float SinkDepth => -_sinkY;
        public bool HasSlick => _slick != null;
        public int DebrisCount => _debris.Count;
        public int PartCount { get; private set; }

        /// <summary>
        /// 원본의 켜져 있는 불투명 메시만 복사해 잔해를 만든다. 원본은 호출 직후 풀로 돌아가도 된다.
        /// speed: 격침 순간의 속력(m/s) — 잔해가 그만큼 미끄러진다.
        /// </summary>
        public static ShipWreck Spawn(Transform source, float speed)
        {
            if (source == null) return null;

            var parts = new List<MeshRenderer>();
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || !r.TryGetComponent<MeshFilter>(out var f) || f.sharedMesh == null) continue;
                var m = r.sharedMaterial;
                if (m == null || m.renderQueue >= 2500) continue;   // 표시용 반투명(사각 표시 등)은 빼고 선체만
                parts.Add(r);
            }
            if (parts.Count == 0) return null;

            while (Active.Count >= MaxActive) { var old = Active[0]; Active.RemoveAt(0); if (old != null) Destroy(old.gameObject); }

            // 선체 크기: 함체 상자(없으면 메시 경계)
            Vector3 center, size;
            if (source.TryGetComponent<BoxCollider>(out var hull))
            {
                center = source.TransformPoint(hull.center);
                Vector3 s = source.lossyScale;
                size = new Vector3(hull.size.x * Mathf.Abs(s.x), hull.size.y * Mathf.Abs(s.y), hull.size.z * Mathf.Abs(s.z));
            }
            else
            {
                var b = parts[0].bounds;
                foreach (var r in parts) b.Encapsulate(r.bounds);
                center = b.center;
                size = new Vector3(Mathf.Min(b.size.x, b.size.z), b.size.y, Mathf.Max(b.size.x, b.size.z));
            }

            Vector3 fwd = source.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            var rot = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            var pivot = new Vector3(center.x, WaterY + 0.3f, center.z);

            var go = new GameObject($"Wreck ({source.name})");
            go.transform.SetPositionAndRotation(pivot, rot);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            var wreck = go.AddComponent<ShipWreck>();
            wreck._body = body;
            foreach (var r in parts)
            {
                var src = r.transform;
                var copy = new GameObject(src.name, typeof(MeshFilter), typeof(MeshRenderer));
                copy.transform.SetParent(body, false);
                copy.transform.SetPositionAndRotation(src.position, src.rotation);
                copy.transform.localScale = src.lossyScale;
                copy.GetComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
                var cr = copy.GetComponent<MeshRenderer>();
                cr.sharedMaterials = r.sharedMaterials;
                cr.shadowCastingMode = r.shadowCastingMode;
                cr.receiveShadows = r.receiveShadows;
                if (wreck._debrisMaterial == null) wreck._debrisMaterial = r.sharedMaterial;
            }
            wreck.PartCount = parts.Count;
            wreck.Begin(size, Mathf.Max(0.3f, center.y + size.y * 0.5f - pivot.y), speed);
            Active.Add(wreck);
            return wreck;
        }

        /// <summary>검증·정리용: 모든 잔해를 지운다.</summary>
        public static void ClearAll()
        {
            foreach (var w in Active) if (w != null) Destroy(w.gameObject);
            Active.Clear();
        }

        private void Begin(Vector3 size, float deck, float speed)
        {
            _deck = deck;
            _length = Mathf.Max(1f, size.z);
            _beam = Mathf.Max(0.5f, size.x);
            _height = Mathf.Max(0.5f, size.y);
            _scale = Mathf.Clamp(_length / 8f, 0.55f, 2.4f);
            _duration = Mathf.Clamp(4.5f + _length * 0.25f, 5f, 12f);

            // 작은 배일수록 크게 뒤집힌다
            float side = Random.value < 0.5f ? -1f : 1f;
            _rollTarget = side * Mathf.Lerp(45f, 18f, Mathf.InverseLerp(3f, 20f, _length)) * Random.Range(0.85f, 1.1f);
            _pitchTarget = (Random.value < 0.6f ? 1f : -1f) * Random.Range(8f, 18f);   // + 선수가 먼저 잠김
            _depth = _height + _beam * 0.6f + 0.5f * _length * Mathf.Sin(Mathf.Abs(_pitchTarget) * Mathf.Deg2Rad) + 1.5f;
            _speed = Mathf.Max(0f, speed);
            _yawRate = Random.Range(-8f, 8f);
            _startPos = transform.position;
            _startRot = transform.rotation;

            // 격침 순간: 불덩이 + 검은 연기
            int fire = Mathf.RoundToInt(10 * _scale + 6);
            for (int i = 0; i < fire; i++)
            {
                Vector3 p = DeckPoint(Random.Range(-0.35f, 0.35f));
                Vector3 r = Random.insideUnitSphere; r.y = Mathf.Abs(r.y);
                Vector3 v = r * 3.2f * _scale + Vector3.up * Random.Range(1.5f, 4f);
                DecorFx.Emit(DecorFx.Fire, p, v, Random.Range(0.35f, 0.7f), Random.Range(1.0f, 2.2f) * _scale,
                    Color.Lerp(new Color(1f, 0.82f, 0.4f, 0.95f), new Color(1f, 0.42f, 0.12f, 0.9f), Random.value));
            }
            for (int i = 0; i < fire; i++)
            {
                Vector3 p = DeckPoint(Random.Range(-0.3f, 0.3f));
                Vector3 v = Vector3.Scale(Random.insideUnitSphere, new Vector3(1.6f, 0.4f, 1.6f)) * _scale + Vector3.up * Random.Range(1.5f, 3f) + DecorFx.Wind * 0.5f;
                DecorFx.Emit(DecorFx.WreckSmoke, p, v, Random.Range(2.2f, 3.6f), Random.Range(1.2f, 2.2f) * _scale,
                    new Color(0.1f, 0.1f, 0.1f, 0.7f));
            }
        }

        /// <summary>갑판 위 한 점(along: 선체 길이 비율 -0.5 선미 ~ 0.5 선수).</summary>
        private Vector3 DeckPoint(float along)
        {
            return transform.position + transform.rotation * new Vector3(0f, _deck, along * _length);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;
            float u = Mathf.Clamp01(Age / _duration);

            if (!_bodyGone)
            {
                // 미끄러짐(남은 속력이 빠르게 줄어든다)과 조금 도는 선수
                _speed *= Mathf.Exp(-dt / 1.4f);
                _yawRate *= Mathf.Exp(-dt / 2f);
                _startPos += (_startRot * Vector3.forward) * (_speed * dt);
                _startRot = Quaternion.Euler(0f, _yawRate * dt, 0f) * _startRot;

                float roll = _rollTarget * Mathf.SmoothStep(0f, 1f, u / 0.8f);
                float pitch = _pitchTarget * Mathf.SmoothStep(0f, 1f, (u - 0.2f) / 0.8f);
                _sinkY = -_depth * (0.15f * Mathf.SmoothStep(0f, 1f, u / 0.3f) +
                                    0.85f * Mathf.Pow(Mathf.Clamp01((u - 0.3f) / 0.7f), 2f));
                RollDegrees = roll;
                transform.SetPositionAndRotation(_startPos + Vector3.up * _sinkY, _startRot * Quaternion.Euler(pitch, 0f, roll));

                // 갑판의 불과 연기 기둥(가라앉을수록 잦아든다)
                if (u < 0.85f) EmitSmoke(dt, (8f + 2.2f * _length) * Mathf.Sqrt(1f - u), DeckPoint(Random.Range(-0.3f, 0.3f)), 1f);
                if (u < 0.6f) EmitFire(dt, 14f * _scale * (1f - u / 0.6f));

                if (!_slickSpawned && u >= 0.6f) SpawnSlickAndDebris();
                if (!_foamSpawned && u >= 0.85f)
                {
                    _foamSpawned = true;
                    OceanSurface.SpawnSinkingTrace(new Vector3(_startPos.x, 0f, _startPos.z));
                }
                if (u >= 1f)
                {
                    _bodyGone = true;
                    if (_body != null) Destroy(_body.gameObject);
                }
            }

            UpdateSlick(dt);
            UpdateDebris(dt);

            if (_bodyGone && _slick == null && _debris.Count == 0)
            {
                Active.Remove(this);
                Destroy(gameObject);
            }
        }

        private void EmitSmoke(float dt, float rate, Vector3 at, float strength)
        {
            _smokeDebt += rate * dt;
            int n = Mathf.Min(8, (int)_smokeDebt);
            _smokeDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = at + Random.insideUnitSphere * 0.4f * _scale;
                Vector3 v = Vector3.up * Random.Range(2.2f, 3.6f) * Mathf.Sqrt(_scale) + DecorFx.Wind * 0.8f + Random.insideUnitSphere * 0.5f;
                float g = Random.Range(0.08f, 0.2f);
                DecorFx.Emit(DecorFx.WreckSmoke, p, v, Random.Range(3.5f, 5.5f), Random.Range(1.0f, 1.8f) * _scale,
                    new Color(g, g, g * 1.05f, 0.62f * strength));
            }
        }

        private void EmitFire(float dt, float rate)
        {
            _fireDebt += rate * dt;
            int n = Mathf.Min(6, (int)_fireDebt);
            _fireDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = DeckPoint(Random.Range(-0.4f, 0.4f)) + Random.insideUnitSphere * 0.3f * _scale;
                Vector3 v = Vector3.up * Random.Range(1.0f, 2.4f) + Random.insideUnitSphere * 0.4f;
                DecorFx.Emit(DecorFx.Fire, p, v, Random.Range(0.3f, 0.6f), Random.Range(0.6f, 1.3f) * _scale,
                    Color.Lerp(new Color(1f, 0.85f, 0.45f, 0.95f), new Color(1f, 0.38f, 0.1f, 0.85f), Random.value));
            }
        }

        // ------------------------------------------------------------ 기름띠 · 잔해 조각

        private void SpawnSlickAndDebris()
        {
            _slickSpawned = true;
            Vector3 at = new(_startPos.x, WaterY + 0.03f, _startPos.z);

            if (s_slickMaterial == null) s_slickMaterial = Resources.Load<Material>("Water/MAT_OilSlick");
            if (s_slickMaterial != null)
            {
                _slick = new GameObject("Oil slick", typeof(MeshFilter), typeof(MeshRenderer));
                _slick.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                _slick.GetComponent<MeshFilter>().sharedMesh = Quad();
                var r = _slick.GetComponent<MeshRenderer>();
                r.sharedMaterial = s_slickMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                _slickProps = new MaterialPropertyBlock();
                _slickProps.SetFloat(SeedId, Random.Range(0f, 100f));
                _slickSize = _length * 1.25f + 4f;
                _slickAge = 0f;
                UpdateSlick(0f);
            }

            if (s_cube == null) s_cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            int count = Mathf.Clamp(Mathf.RoundToInt(4 + _length * 0.35f), 4, 14);
            float pieceScale = Mathf.Clamp(_scale, 0.7f, 1.5f);
            for (int i = 0; i < count; i++)
            {
                var d = new GameObject("Debris", typeof(MeshFilter), typeof(MeshRenderer));
                d.GetComponent<MeshFilter>().sharedMesh = s_cube;
                var dr = d.GetComponent<MeshRenderer>();
                dr.sharedMaterial = _debrisMaterial;
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 off = _startRot * new Vector3(Random.Range(-0.5f, 0.5f) * _beam, 0f, Random.Range(-0.45f, 0.45f) * _length);
                d.transform.position = at + off + Vector3.up * 0.05f;
                d.transform.rotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
                d.transform.localScale = new Vector3(Random.Range(0.25f, 0.7f), Random.Range(0.12f, 0.22f), Random.Range(0.4f, 1.3f)) * pieceScale;
                Vector3 outward = off.sqrMagnitude > 0.01f ? off.normalized : Random.insideUnitSphere;
                outward.y = 0f;
                _debris.Add(new Debris
                {
                    T = d.transform,
                    Velocity = outward * Random.Range(0.4f, 1.3f) + DecorFx.Wind * 0.15f,
                    Phase = Random.Range(0f, 6.28f),
                    Spin = Random.Range(-20f, 20f),
                    Yaw = d.transform.eulerAngles.y,
                    Life = Random.Range(12f, 18f),
                });
            }
        }

        private void UpdateSlick(float dt)
        {
            if (_slick == null) return;
            _slickAge += dt;
            float grow = 1f - Mathf.Exp(-_slickAge / 3.5f);
            float size = Mathf.Lerp(_length * 0.35f, _slickSize, grow);
            _slick.transform.localScale = new Vector3(size, 1f, size);

            float fade = Mathf.Clamp01(_slickAge / 1.5f) * (1f - Mathf.Clamp01((_slickAge - (SlickLife - 6f)) / 6f));
            _slickProps.SetFloat(FadeId, fade);
            _slick.GetComponent<MeshRenderer>().SetPropertyBlock(_slickProps);

            // 기름에 붙은 불: 가라앉은 뒤에도 잠시 옅은 연기가 오른다
            if (_bodyGone && _slickAge < 7f)
                EmitSmoke(dt, (2f + 0.6f * _length) * (1f - _slickAge / 7f), _slick.transform.position + Vector3.up * 0.3f, 0.6f);

            if (_slickAge >= SlickLife)
            {
                Destroy(_slick);
                _slick = null;
            }
        }

        private void UpdateDebris(float dt)
        {
            float now = Time.time;
            for (int i = _debris.Count - 1; i >= 0; i--)
            {
                var d = _debris[i];
                if (d.T == null) { _debris.RemoveAt(i); continue; }
                d.Life -= dt;
                d.Velocity *= Mathf.Exp(-dt / 3f);
                Vector3 p = d.T.position + (d.Velocity + DecorFx.Wind * 0.1f) * dt;
                float sink = d.Life < 2f ? (2f - d.Life) * 0.6f : 0f;   // 마지막 2초에 잠긴다
                p.y = WaterY + 0.04f + Mathf.Sin(now * 1.7f + d.Phase) * 0.06f - sink;
                d.T.position = p;
                d.Yaw += d.Spin * dt;
                d.T.rotation = Quaternion.Euler(Mathf.Sin(now * 1.3f + d.Phase) * 6f, d.Yaw, Mathf.Cos(now * 1.1f + d.Phase) * 6f);
                _debris[i] = d;
                if (d.Life <= 0f)
                {
                    Destroy(d.T.gameObject);
                    _debris.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            Active.Remove(this);
            if (_slick != null) Destroy(_slick);
            foreach (var d in _debris) if (d.T != null) Destroy(d.T.gameObject);
            _debris.Clear();
        }

        private static Mesh Quad()
        {
            if (s_quad != null) return s_quad;
            s_quad = new Mesh { name = "Oil slick" };
            s_quad.SetVertices(new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) });
            s_quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            s_quad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            s_quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            s_quad.RecalculateBounds();
            return s_quad;
        }
    }
}
