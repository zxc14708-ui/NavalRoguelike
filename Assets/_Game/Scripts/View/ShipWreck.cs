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
    ///
    /// 침몰 방식(SinkMode)은 마지막 한 방에 따라 갈린다(2026-11-16):
    ///   List           — 기울며 가라앉는다(기본, 위 설명).
    ///   Capsize        — 침수 전복(어뢰·충돌에 흔함): 불은 적고, 천천히 뒤집혀 배 밑이 드러난 뒤 가라앉는다. 수면에 물보라·거품.
    ///   Superstructure — 함교 파괴(포탄에 흔함): 맞은 곳 가까운 상부 구조물이 폭발로 날아가 바다에 떨어지고, 그 자리에서 불기둥이 오른다.
    ///   Magazine       — 유폭(미사일에 흔함): 큰 폭발과 연쇄 폭발, 파편이 사방으로 날고, 선체가 두 동강 나거나(부품이 나뉘어 있을 때) 곤두서서 빠르게 가라앉는다.
    /// </summary>
    public partial class ShipWreck : MonoBehaviour
    {
        public static readonly List<ShipWreck> Active = new();
        private const int MaxActive = 8;
        private const float WaterY = -0.9f;
        private const float SlickLife = 16f;

        public enum SinkMode { List, Capsize, Superstructure, Magazine, WigCrash, LunCrash, CushionCollapse, FoilCollapse, DeckCollapse }

        /// <summary>검증·개발용: 값을 넣으면 모든 잔해가 이 방식으로 가라앉는다.</summary>
        public static SinkMode? ForcedMode;

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

        /// <summary>폭발로 날아가는 조각(떼어 낸 상부 구조물·파편). 바다에 떨어지면 물보라를 내고 사라진다.</summary>
        private struct Flying
        {
            public Transform T;
            public Vector3 Velocity, Spin;
        }

        private Transform _body, _bowHalf, _sternHalf;
        private float _splitAngle, _blastAlong, _fireMul = 1f, _smokeMul = 1f, _sprayDebt, _nextBoom;
        private int _boomsLeft;
        private readonly List<Flying> _flying = new();
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
        public SinkMode Mode { get; private set; }
        public int FlyingCount => _flying.Count;
        public bool BrokeInTwo => _bowHalf != null;

        /// <summary>
        /// 원본의 켜져 있는 불투명 메시만 복사해 잔해를 만든다. 원본은 호출 직후 풀로 돌아가도 된다.
        /// speed: 격침 순간의 속력(m/s) — 잔해가 그만큼 미끄러진다.
        /// cause · hitPoint: 마지막 한 방의 종류와 맞은 곳 — 침몰 방식을 고른다(없으면 기본: 기울며 침몰).
        /// </summary>
        public static ShipWreck Spawn(Transform source, float speed, Game.Combat.DamageSource? cause = null, Vector3 hitPoint = default)
            => Spawn(source, speed, cause, hitPoint, null);

        private static ShipWreck Spawn(Transform source, float speed, Game.Combat.DamageSource? cause, Vector3 hitPoint,
                                       SinkMode? stage4Mode, float lift = 0f)
        {
            if (source == null) return null;

            var parts = new List<MeshRenderer>();
            foreach (var r in source.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || r.name == "Night lamp" || !r.TryGetComponent<MeshFilter>(out var f) || f.sharedMesh == null) continue;
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
            var pivot = stage4Mode.HasValue ? source.position : new Vector3(center.x, WaterY + 0.3f, center.z);

            var go = new GameObject($"Wreck ({source.name})");
            go.transform.SetPositionAndRotation(pivot, rot);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);

            var wreck = go.AddComponent<ShipWreck>();
            wreck._body = body;
            wreck._stage4Lift = lift;
            MaterialPropertyBlock props = null;
            var copies = new List<(Transform t, Bounds b)>(parts.Count);
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
                if (stage4Mode.HasValue && r.HasPropertyBlock())
                {
                    props ??= new MaterialPropertyBlock();
                    r.GetPropertyBlock(props);
                    cr.SetPropertyBlock(props);
                }
                cr.shadowCastingMode = r.shadowCastingMode;
                cr.receiveShadows = r.receiveShadows;
                if (stage4Mode.HasValue) wreck.BindStage4Copy(stage4Mode.Value, src, copy.transform);
                if (wreck._debrisMaterial == null) wreck._debrisMaterial = r.sharedMaterial;
                copies.Add((copy.transform, r.bounds));
            }
            wreck.PartCount = parts.Count;
            wreck.Begin(size, Mathf.Max(0.3f, center.y + size.y * 0.5f - pivot.y), speed, stage4Mode ?? Pick(cause), hitPoint, copies);
            Active.Add(wreck);
            return wreck;
        }

        /// <summary>검증·정리용: 모든 잔해를 지운다.</summary>
        public static void ClearAll()
        {
            foreach (var w in Active) if (w != null) Destroy(w.gameObject);
            Active.Clear();
        }

        /// <summary>마지막 한 방의 종류로 침몰 방식을 고른다. 원인을 모르면 기본(기울며 침몰).</summary>
        private static SinkMode Pick(Game.Combat.DamageSource? cause)
        {
            if (ForcedMode.HasValue) return ForcedMode.Value;
            float r = Random.value;
            switch (cause)
            {
                case Game.Combat.DamageSource.Torpedo:
                case Game.Combat.DamageSource.Collision:
                    return r < 0.65f ? SinkMode.Capsize : r < 0.85f ? SinkMode.List : SinkMode.Magazine;
                case Game.Combat.DamageSource.Missile:
                case Game.Combat.DamageSource.CookOff:
                    return r < 0.55f ? SinkMode.Magazine : r < 0.8f ? SinkMode.Superstructure : SinkMode.List;
                case Game.Combat.DamageSource.Gun:
                    return r < 0.45f ? SinkMode.Superstructure : r < 0.8f ? SinkMode.List : SinkMode.Capsize;
                default:
                    return SinkMode.List;
            }
        }

        private void Begin(Vector3 size, float deck, float speed, SinkMode mode, Vector3 hitPoint, List<(Transform t, Bounds b)> copies)
        {
            Mode = mode;
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

            // 맞은 곳(선체 길이 비율). 모르면 중앙 부근 아무 데나
            _blastAlong = Random.Range(-0.25f, 0.25f);
            if (hitPoint != Vector3.zero)
                _blastAlong = Mathf.Clamp(Vector3.Dot(hitPoint - transform.position, transform.forward) / _length, -0.35f, 0.35f);
            if (IsStage4Motion)
            {
                BeginStage4();
                return;
            }
            float power = Mathf.Clamp(Mathf.Sqrt(_scale), 0.8f, 1.5f);

            switch (mode)
            {
                case SinkMode.Capsize:
                    // 침수 전복: 불은 적고, 오래 걸려 뒤집힌다
                    _rollTarget = side * Random.Range(150f, 175f);
                    _pitchTarget = (Random.value < 0.5f ? 1f : -1f) * Random.Range(3f, 8f);
                    _depth = _height + _beam * 0.6f + 0.5f * _length * Mathf.Sin(Mathf.Abs(_pitchTarget) * Mathf.Deg2Rad) + 1.5f;
                    _duration = Mathf.Min(15f, _duration * 1.3f);
                    _fireMul = 0.25f; _smokeMul = 0.5f;
                    Sound(Game.Data.SfxId.SinkCapsize, 1f);
                    break;

                case SinkMode.Superstructure:
                {
                    // 함교 파괴: 맞은 곳 가까운 상부 구조물이 날아가고 그 자리에서 불기둥
                    Vector3 at = DeckPoint(_blastAlong);
                    Explosions.Spawn(at + Vector3.up * 0.5f * _scale, Mathf.Clamp(0.8f * _scale, 0.5f, 1.8f), Explosions.Kind.Impact, false);
                    int blown = BlowOffUpperParts(copies, at, power);
                    SpawnChunks(at, blown > 0 ? 3 : 6, power);
                    _fireMul = 1.5f; _smokeMul = 1.3f;
                    _boomsLeft = 1; _nextBoom = Random.Range(0.5f, 1.1f);
                    Sound(Game.Data.SfxId.WreckBlast, 0.8f);
                    break;
                }

                case SinkMode.Magazine:
                {
                    // 유폭: 큰 폭발 · 연쇄 폭발 · 파편. 부품이 앞뒤로 나뉘어 있으면 두 동강, 아니면 곤두서서 빠르게 가라앉는다
                    Vector3 at = DeckPoint(_blastAlong);
                    Explosions.Spawn(at, Mathf.Clamp(1.5f * _scale, 0.9f, 3.2f), Explosions.Kind.Impact, false);
                    SpawnChunks(at, Mathf.Clamp(Mathf.RoundToInt(5 + _length * 0.3f), 5, 12), power * 1.25f);
                    _duration = Mathf.Max(4.5f, _duration * 0.8f);
                    if (TrySplit(copies))
                    {
                        _splitAngle = Random.Range(28f, 42f);
                        _rollTarget *= 0.5f;
                        _pitchTarget = 0f;
                        _depth = _height + _beam * 0.3f + 0.6f * _length * Mathf.Sin(_splitAngle * Mathf.Deg2Rad) + 1.5f;
                    }
                    else
                    {
                        _pitchTarget = (Random.value < 0.5f ? 1f : -1f) * Random.Range(48f, 65f);
                        _depth = _height + _beam * 0.6f + 0.5f * _length * Mathf.Sin(Mathf.Abs(_pitchTarget) * Mathf.Deg2Rad) + 1.5f;
                    }
                    _fireMul = 1.3f; _smokeMul = 1.4f;
                    _boomsLeft = 2 + (_length > 10f ? 1 : 0); _nextBoom = Random.Range(0.3f, 0.6f);
                    Sound(Game.Data.SfxId.WreckBlast, 1f, 0.85f);
                    Sound(Game.Data.SfxId.SinkGroan, 0.8f);
                    break;
                }
            }

            if (mode == SinkMode.List && _length >= 6f) Sound(Game.Data.SfxId.SinkGroan, 0.55f);   // 큰 배는 기울며 선체가 운다

            // 격침 순간: 불덩이(디졸브 불덩이 + 부드러운 원 조금) + 검은 연기. 침수 전복은 불 대신 물보라가 솟는다
            int fire = Mathf.RoundToInt((10 * _scale + 6) * (mode == SinkMode.Capsize ? 0.4f : 1f));
            if (mode == SinkMode.Capsize)
            {
                for (int i = 0; i < Mathf.RoundToInt(14 * _scale + 8); i++)
                {
                    Vector3 p = WaterlinePoint(Random.Range(-0.3f, 0.3f) + _blastAlong * 0.5f, Random.Range(-0.5f, 0.5f));
                    Vector3 v = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(4f, 9f), Random.Range(-1.5f, 1.5f)) * power;
                    DecorFx.Emit(DecorFx.Spray, p, v, Random.Range(0.8f, 1.4f), Random.Range(0.9f, 1.8f) * _scale,
                        new Color(0.94f, 0.97f, 1f, Random.Range(0.55f, 0.85f)));
                }
            }
            bool blast = Explosions.DeckBlast(() => DeckPoint(Random.Range(-0.35f, 0.35f)), _scale, Mathf.RoundToInt(4 * _scale + 4));
            for (int i = 0; i < (blast ? fire / 3 : fire); i++)
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

        /// <summary>
        /// 함교 파괴: 맞은 곳 가까이의 높은 부품(상부 구조물·포탑)을 선체에서 떼어 날린다. 날린 수를 돌려준다.
        /// 모델이 한 덩이면 뗄 것이 없다(0) — 그때는 파편 조각만 난다.
        /// </summary>
        private int BlowOffUpperParts(List<(Transform t, Bounds b)> copies, Vector3 blast, float power)
        {
            if (copies.Count < 2) return 0;
            var all = copies[0].b;
            foreach (var c in copies) all.Encapsulate(c.b);
            float minY = all.min.y + all.size.y * 0.5f;
            float reach = _length * 0.4f;
            var picks = new List<(Transform t, float d)>();
            foreach (var c in copies)
            {
                if (c.b.center.y < minY) continue;                                         // 낮은 것(선체)은 남긴다
                if (Mathf.Max(c.b.size.x, c.b.size.z) > _length * 0.5f) continue;          // 선체만큼 긴 것도 남긴다
                Vector3 d = c.b.center - blast; d.y = 0f;
                if (d.sqrMagnitude > reach * reach) continue;
                picks.Add((c.t, d.sqrMagnitude));
            }
            picks.Sort((a, b) => a.d.CompareTo(b.d));
            int n = Mathf.Min(picks.Count, Mathf.Clamp(copies.Count / 3, 1, 6));
            for (int i = 0; i < n; i++)
            {
                picks[i].t.SetParent(null, true);
                Launch(picks[i].t, blast, power);
            }
            return n;
        }

        /// <summary>유폭: 부품이 앞뒤로 나뉘어 있으면 폭발 지점에서 선수 쪽·선미 쪽 두 묶음으로 가른다. 선체가 한 덩이면 false.</summary>
        private bool TrySplit(List<(Transform t, Bounds b)> copies)
        {
            if (copies.Count < 2) return false;
            Vector3 fwd = transform.forward;
            float z0 = _blastAlong * _length;
            int bow = 0, stern = 0;
            foreach (var c in copies)
            {
                float span = Mathf.Abs(fwd.x) * c.b.size.x + Mathf.Abs(fwd.z) * c.b.size.z;
                if (span > _length * 0.7f) return false;   // 앞뒤를 다 덮는 선체 한 덩이 — 가를 수 없다
                if (Vector3.Dot(c.b.center - transform.position, fwd) > z0) bow++; else stern++;
            }
            if (bow == 0 || stern == 0) return false;

            _bowHalf = new GameObject("Bow half").transform;
            _sternHalf = new GameObject("Stern half").transform;
            foreach (var half in new[] { _bowHalf, _sternHalf })
            {
                half.SetParent(_body, false);
                half.localPosition = new Vector3(0f, 0f, z0);
            }
            foreach (var c in copies)
                c.t.SetParent(Vector3.Dot(c.b.center - transform.position, fwd) > z0 ? _bowHalf : _sternHalf, true);
            return true;
        }

        /// <summary>선체 재질의 파편 조각을 폭발 지점에서 사방으로 날린다.</summary>
        private void SpawnChunks(Vector3 at, int count, float power)
        {
            if (s_cube == null) s_cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            float pieceScale = Mathf.Clamp(_scale, 0.7f, 1.5f);
            for (int i = 0; i < count; i++)
            {
                var d = new GameObject("Blast chunk", typeof(MeshFilter), typeof(MeshRenderer));
                d.GetComponent<MeshFilter>().sharedMesh = s_cube;
                var dr = d.GetComponent<MeshRenderer>();
                dr.sharedMaterial = _debrisMaterial;
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.transform.SetPositionAndRotation(at + Random.insideUnitSphere * 0.4f * _scale, Random.rotation);
                d.transform.localScale = new Vector3(Random.Range(0.25f, 0.8f), Random.Range(0.1f, 0.3f), Random.Range(0.4f, 1.2f)) * pieceScale;
                Launch(d.transform, at, power);
            }
        }

        private void Launch(Transform t, Vector3 from, float power)
        {
            Vector3 dir = t.position - from; dir.y = 0f;
            if (dir.sqrMagnitude < 0.04f) { Vector2 r = Random.insideUnitCircle.normalized; dir = new Vector3(r.x, 0f, r.y); }
            dir = dir.normalized + Random.insideUnitSphere * 0.6f;
            dir.y = 0f;
            _flying.Add(new Flying
            {
                T = t,
                Velocity = dir.normalized * Random.Range(3f, 8f) * power + Vector3.up * Random.Range(6f, 11f) * power
                           + (_startRot * Vector3.forward) * _speed,
                Spin = Random.onUnitSphere * Random.Range(120f, 420f),
            });
        }

        private void UpdateFlying(float dt)
        {
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                var f = _flying[i];
                if (f.T == null) { _flying.RemoveAt(i); continue; }
                f.Velocity += Vector3.down * (14f * dt);
                f.T.position += f.Velocity * dt;
                f.T.Rotate(f.Spin * dt, Space.World);
                _flying[i] = f;

                // 불붙은 조각의 연기 꼬리
                if (Random.value < dt * 18f)
                {
                    float g = Random.Range(0.1f, 0.25f);
                    DecorFx.Emit(DecorFx.WreckSmoke, f.T.position, Vector3.up * 0.5f + DecorFx.Wind * 0.3f,
                        Random.Range(0.6f, 1.1f), Random.Range(0.4f, 0.8f) * _scale, new Color(g, g, g, 0.5f));
                }

                if (Mode == SinkMode.DeckCollapse && f.Velocity.y < 0f && f.T.position.y <= _startPos.y + 0.17f)
                {
                    Vector3 at = f.T.position;
                    at.y = _startPos.y + 0.17f;
                    f.T.position = at;
                    _flying.RemoveAt(i);
                    f.T.SetParent(_body, true);
                }
                else if (f.Velocity.y < 0f && f.T.position.y < WaterY - 0.2f)
                {
                    Vector3 at = new(f.T.position.x, WaterY + 0.1f, f.T.position.z);
                    for (int k = 0; k < 7; k++)
                    {
                        Vector2 r = Random.insideUnitCircle;
                        DecorFx.Emit(DecorFx.Spray, at, new Vector3(r.x * 1.6f, Random.Range(2.5f, 5.5f), r.y * 1.6f),
                            Random.Range(0.5f, 0.9f), Random.Range(0.6f, 1.2f), new Color(0.94f, 0.97f, 1f, 0.7f));
                    }
                    Destroy(f.T.gameObject);
                    _flying.RemoveAt(i);
                }
            }
        }

        /// <summary>수면 높이의 선체 둘레 한 점(along: 길이 비율, across: 폭 비율 -0.5 ~ 0.5).</summary>
        private Vector3 WaterlinePoint(float along, float across)
        {
            Vector3 p = _startPos + _startRot * new Vector3(across * _beam, 0f, along * _length);
            p.y = WaterY + 0.1f;
            return p;
        }

        /// <summary>갑판 위 한 점(along: 선체 길이 비율 -0.5 선미 ~ 0.5 선수).</summary>
        private Vector3 DeckPoint(float along)
        {
            return transform.position + transform.rotation * new Vector3(0f, _deck, along * _length);
        }

        private void Update()
        {
            float dt = s_finale == this && Game.Core.GameManager.Instance?.State == Game.Core.GameState.Victory
                ? Time.unscaledDeltaTime : Time.deltaTime;
            if (dt <= 0f) return;
            Age += dt;
            if (IsStage4Motion)
            {
                TickStage4(dt);
                return;
            }
            float u = Mathf.Clamp01(Age / _duration);

            if (!_bodyGone)
            {
                // 미끄러짐(남은 속력이 빠르게 줄어든다)과 조금 도는 선수
                _speed *= Mathf.Exp(-dt / 1.4f);
                _yawRate *= Mathf.Exp(-dt / 2f);
                _startPos += (_startRot * Vector3.forward) * (_speed * dt);
                _startRot = Quaternion.Euler(0f, _yawRate * dt, 0f) * _startRot;

                float roll, pitch;
                if (Mode == SinkMode.Capsize)
                {
                    // 천천히 뒤집힌 뒤(배 밑이 드러난다) 가라앉는다
                    roll = _rollTarget * Mathf.SmoothStep(0f, 1f, u / 0.65f);
                    pitch = _pitchTarget * Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.6f);
                    _sinkY = -_depth * (0.22f * Mathf.SmoothStep(0f, 1f, u / 0.5f) +
                                        0.78f * Mathf.Pow(Mathf.Clamp01((u - 0.5f) / 0.5f), 2f));
                }
                else
                {
                    // 유폭으로 곤두서는 배는 선수(선미)가 일찍 들린다
                    float lead = Mode == SinkMode.Magazine ? 0.02f : 0.2f;
                    float span = Mode == SinkMode.Magazine ? 0.7f : 0.8f;
                    roll = _rollTarget * Mathf.SmoothStep(0f, 1f, u / 0.8f);
                    pitch = _pitchTarget * Mathf.SmoothStep(0f, 1f, (u - lead) / span);
                    _sinkY = -_depth * (0.15f * Mathf.SmoothStep(0f, 1f, u / 0.3f) +
                                        0.85f * Mathf.Pow(Mathf.Clamp01((u - 0.3f) / 0.7f), 2f));
                }
                RollDegrees = roll;
                transform.SetPositionAndRotation(_startPos + Vector3.up * _sinkY, _startRot * Quaternion.Euler(pitch, 0f, roll));

                // 두 동강: 부러진 곳이 먼저 잠기고 선수·선미 끝이 들린다
                if (_bowHalf != null)
                {
                    float k = Mathf.SmoothStep(0f, 1f, u / 0.55f);
                    _bowHalf.localRotation = Quaternion.Euler(-_splitAngle * k, 0f, _rollTarget * 0.4f * k);
                    _sternHalf.localRotation = Quaternion.Euler(_splitAngle * 0.8f * k, 0f, -_rollTarget * 0.3f * k);
                }

                // 연쇄 폭발(함교 파괴 1번 · 유폭 2~3번)
                if (_boomsLeft > 0 && Age >= _nextBoom && u < 0.6f)
                {
                    _boomsLeft--;
                    _nextBoom = Age + Random.Range(0.35f, 0.9f);
                    Vector3 at = DeckPoint(Mathf.Clamp(_blastAlong + Random.Range(-0.3f, 0.3f), -0.4f, 0.4f));
                    Explosions.Spawn(at, Mathf.Clamp(Random.Range(0.5f, 0.9f) * _scale, 0.4f, 1.8f), Explosions.Kind.Impact, false);
                    if (Mode == SinkMode.Magazine) SpawnChunks(at, 2, Mathf.Clamp(Mathf.Sqrt(_scale), 0.8f, 1.5f));
                    Game.Core.AudioManager.Play(Game.Data.SfxId.WreckBlast, at, Mathf.Clamp(0.4f + 0.2f * _scale, 0.4f, 0.8f), Random.Range(0.95f, 1.25f));
                }

                // 갑판의 불과 연기 기둥(가라앉을수록 잦아든다). 함교 파괴는 맞은 자리에서 주로 오른다
                if (u < 0.85f) EmitSmoke(dt, (8f + 2.2f * _length) * Mathf.Sqrt(1f - u) * _smokeMul, DeckPoint(FireAlong(0.3f)), 1f);
                if (u < 0.6f) EmitFire(dt, 14f * _scale * (1f - u / 0.6f) * _fireMul);

                // 침수 전복: 뒤집히는 동안 선체 둘레에서 물보라가 인다
                if (Mode == SinkMode.Capsize && u > 0.05f && u < 0.92f) EmitWaterline(dt, (6f + 1.2f * _length) * (0.5f + u), 1f);

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
            UpdateFlying(dt);

            if (_bodyGone && _slick == null && _debris.Count == 0 && _flying.Count == 0)
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

        /// <summary>격침 소리(2026-11-16). 배가 클수록 크게.</summary>
        private void Sound(Game.Data.SfxId id, float volume, float pitch = 1f) =>
            Game.Core.AudioManager.Play(id, transform.position, volume * Mathf.Clamp(0.5f + 0.25f * _scale, 0.5f, 1f), pitch);

        /// <summary>불·연기가 오르는 자리(길이 비율). 함교 파괴는 대부분 맞은 자리.</summary>
        private float FireAlong(float spread)
        {
            if (Mode == SinkMode.Superstructure && Random.value < 0.7f)
                return Mathf.Clamp(_blastAlong + Random.Range(-0.06f, 0.06f), -0.45f, 0.45f);
            return Random.Range(-spread, spread);
        }

        /// <summary>선체 둘레 수면의 물보라·거품(침수 전복).</summary>
        private void EmitWaterline(float dt, float rate, float strength)
        {
            _sprayDebt += rate * dt;
            int n = Mathf.Min(8, (int)_sprayDebt);
            _sprayDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = WaterlinePoint(Random.Range(-0.45f, 0.45f), (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.3f, 0.6f));
                Vector3 v = new Vector3(Random.Range(-0.6f, 0.6f), Random.Range(1.2f, 3.2f) * strength, Random.Range(-0.6f, 0.6f));
                DecorFx.Emit(DecorFx.Spray, p, v, Random.Range(0.5f, 1f), Random.Range(0.5f, 1.1f) * _scale,
                    new Color(0.94f, 0.97f, 1f, Random.Range(0.4f, 0.7f) * strength));
            }
        }

        private void EmitFire(float dt, float rate)
        {
            _fireDebt += rate * dt;
            int n = Mathf.Min(6, (int)_fireDebt);
            _fireDebt -= n;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = DeckPoint(FireAlong(0.4f)) + Random.insideUnitSphere * 0.3f * _scale;
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
                EmitSmoke(dt, (2f + 0.6f * _length) * (1f - _slickAge / 7f) * _smokeMul, _slick.transform.position + Vector3.up * 0.3f, 0.6f);
            // 침수 전복: 가라앉은 자리에서 공기 방울이 잠시 끓어오른다
            if (_bodyGone && Mode == SinkMode.Capsize && _slickAge < 9f)
                EmitWaterline(dt, (4f + 0.8f * _length) * (1f - _slickAge / 9f), 0.6f);

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
            EndFinale();
            Active.Remove(this);
            if (_slick != null) Destroy(_slick);
            foreach (var d in _debris) if (d.T != null) Destroy(d.T.gameObject);
            _debris.Clear();
            foreach (var f in _flying) if (f.T != null) Destroy(f.T.gameObject);
            _flying.Clear();
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
