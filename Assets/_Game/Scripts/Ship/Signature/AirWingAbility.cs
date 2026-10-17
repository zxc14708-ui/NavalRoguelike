using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Modules.Runtime;

namespace Game.Ship
{
    /// <summary>
    /// 지휘함 고유 스킬 — 항공대 호출(2026-10-11). 함재 전투기 3기와 조기경보기(E-2) 1기가 편대로 날아와 20초 동안 함께 싸운다.
    ///   - 전투기: 뒤쪽 하늘에서 편대로 진입 → 함선 둘레 60m 안의 적(수상함·항공기)을 기총으로 훑고 지나가며 공격(한 번 지날 때 8발,
    ///     발당 3 — 함교 기관총과 같은 탄) → 시간이 다 되면 상승하며 이탈.
    ///   - 조기경보기: 함선 둘레를 높이 크게 돌며 레이더돔이 돌고, 3초마다 레이더 고리를 뿌려 70m 안의 잠수함을 4초씩 드러낸다.
    /// 연출: 날개 끝 비행운, 후기연소 불꽃, 항법등, 진입 안내 띠, 조기경보 레이더 고리. 항공기는 적의 표적이 되지 않는다.
    /// </summary>
    public sealed class AirWingAbility : ISignatureAbility
    {
        public const float Duration = 20f;

        public string Title => "항공대 호출";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.AirWing;
        public float Cooldown => 60f;
        public readonly List<FriendlyAircraft> Flight = new();

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            return owner.Ship != null;
        }

        public void Activate(SignatureSkill owner)
        {
            Flight.RemoveAll(a => a == null);
            var ship = owner.Ship;
            GameObject round = null;
            if (ship.Grid != null)
                foreach (var m in ship.Grid.Modules)
                    if (m?.Runtime is BridgeModule b && b.MachineGun != null && b.MachineGun.Projectile != null) { round = b.MachineGun.Projectile; break; }

            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 entry = ship.transform.position - fwd * 150f;
            // 쐐기 대형: 선두기 · 좌우 윙맨 · 뒤 위에 조기경보기
            var slots = new[] { (Vector3.zero, false), (-right * 9f - fwd * 8f, false), (right * 9f - fwd * 8f, false), (-fwd * 26f + Vector3.up * 3f, true) };
            for (int i = 0; i < slots.Length; i++)
            {
                var (offset, aew) = slots[i];
                // 전투 카메라가 위에서 비스듬히 보므로 높이 날면 화면에 너무 크게 보인다 — 낮게(전투기 11m, 조기경보기 14m)
                var a = FriendlyAircraft.Spawn(aew, ship, entry + offset + Vector3.up * (aew ? 14f : 11f), fwd, round, Duration, i);
                Flight.Add(a);
            }
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, ship.transform.position, 0.5f, 0.6f);
            GameEvents.RaiseBanner("항공대 호출 — 전투기 3기 · 조기경보기 1기 진입(20초)");
        }

        public void Tick(SignatureSkill owner, float dt) { }
    }

    /// <summary>
    /// 아군 항공기(항공대 호출). 진입 → 교전 → 이탈 순서로 스스로 난다. 모델은 런타임 도형으로 만든다(전투기·조기경보기).
    /// </summary>
    public class FriendlyAircraft : MonoBehaviour
    {
        private enum Phase { Ingress, Engage, Egress }

        public bool IsAew { get; private set; }
        public int ShotsFired { get; private set; }
        public int RadarPings { get; private set; }
        public bool Engaging => _phase == Phase.Engage;

        private ShipController _ship;
        private GameObject _round;
        private Phase _phase;
        private float _until, _speed, _turn, _altitude, _fireTimer, _breakTimer, _pingTimer, _orbitAngle;
        private int _burst;
        private ITargetable _target;
        private Transform _rotodome;
        private Renderer _burner;

        public static FriendlyAircraft Spawn(bool aew, ShipController ship, Vector3 at, Vector3 heading, GameObject round, float duration, int index)
        {
            var go = new GameObject(aew ? "AEW E-2 (항공대)" : $"Fighter {index + 1} (항공대)");
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(heading, Vector3.up));
            var a = go.AddComponent<FriendlyAircraft>();
            a.IsAew = aew;
            a._ship = ship;
            a._round = round;
            a._until = Time.time + duration;
            a._speed = aew ? 22f : 34f;
            a._turn = aew ? 35f : 120f;
            a._altitude = aew ? 14f : 11f;
            a._orbitAngle = index * 120f;
            a._pingTimer = 1.5f;
            if (aew) a.BuildAew(); else a.BuildFighter();
            return a;
        }

        private void Update()
        {
            if (_ship == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 shipPos = _ship.transform.position;

            if (_phase != Phase.Egress && Time.time >= _until) { _phase = Phase.Egress; _until = Time.time + 7f; }
            if (_phase == Phase.Egress && Time.time >= _until) { Destroy(gameObject); return; }
            if (_phase == Phase.Ingress && Flat(transform.position - shipPos).magnitude < 55f) _phase = Phase.Engage;

            Vector3 goal;
            float wantAlt = _altitude;
            if (_phase == Phase.Egress)
            {
                goal = transform.position + transform.forward * 100f;
                wantAlt = 40f;
            }
            else if (IsAew)
            {
                _orbitAngle += dt * (_speed / 48f) * Mathf.Rad2Deg;
                goal = shipPos + Quaternion.Euler(0f, _orbitAngle, 0f) * Vector3.forward * 55f;
                if (_phase == Phase.Engage) Ping(dt, shipPos);
            }
            else goal = FighterGoal(shipPos, dt, ref wantAlt);

            Steer(goal, wantAlt, dt);
            if (_rotodome != null) _rotodome.Rotate(Vector3.up, 90f * dt, Space.Self);
            if (_burner != null) _burner.transform.localScale = Vector3.one * (0.55f + 0.15f * Mathf.PerlinNoise(Time.time * 18f, GetInstanceID()));
        }

        /// <summary>전투기: 표적을 향해 낮게 내려가 기총을 쏘며 지나가고, 지나치면 잠깐 빠졌다가 다시 들어온다.</summary>
        private Vector3 FighterGoal(Vector3 shipPos, float dt, ref float wantAlt)
        {
            if (_phase == Phase.Ingress) return shipPos;
            if (_target == null || !_target.IsAlive || _target.Transform == null || Flat(_target.Transform.position - shipPos).magnitude > 70f)
                _target = PickTarget(shipPos);
            if (_target == null)
            {
                _orbitAngle += dt * (_speed / 30f) * Mathf.Rad2Deg;
                return shipPos + Quaternion.Euler(0f, _orbitAngle, 0f) * Vector3.forward * 30f;
            }

            Vector3 tp = _target.Transform.position;
            if (_breakTimer > 0f)
            {
                _breakTimer -= dt;
                wantAlt = _altitude + 3f;
                return transform.position + transform.forward * 60f + (transform.position - tp).normalized * 20f;
            }
            Vector3 to = tp - transform.position;
            float dist = Flat(to).magnitude;
            bool air = _target is AirEnemy;
            wantAlt = air ? Mathf.Max(6f, tp.y) : Mathf.Lerp(6f, _altitude, Mathf.Clamp01((dist - 15f) / 40f));
            // 기총 사격: 수평으로 앞쪽 15° 안, 40m 안(내려다 쏘므로 높낮이 각은 보지 않는다 — 탄은 표적을 향해 나간다)
            if (dist < 40f && Vector3.Angle(Flat(transform.forward), Flat(to)) < 15f && _round != null)
            {
                _fireTimer -= dt;
                if (_fireTimer <= 0f)
                {
                    _fireTimer = 0.08f;
                    Fire(tp + Vector3.up * (air ? 0f : 0.5f));
                    if (++_burst >= 8) { _burst = 0; _breakTimer = 1.6f; }
                }
            }
            if (dist < 7f) { _breakTimer = 1.6f; _burst = 0; }
            return tp;
        }

        private void Fire(Vector3 aim)
        {
            Vector3 from = transform.position + transform.forward * 3f - transform.up * 0.3f;
            Vector3 dir = (aim - from).normalized;
            dir = Quaternion.AngleAxis(Random.Range(-1.2f, 1.2f), Vector3.up) * dir;
            var go = PoolManager.Instance?.Spawn(_round, from, Quaternion.LookRotation(dir));
            if (go != null && go.TryGetComponent<Projectile>(out var round)) { round.Launch(dir, 95f, 3f, DamageSource.Gun); round.StatsKey = "항공대"; }
            ShotsFired++;
            if (ShotsFired % 4 == 0) AudioManager.Play(Game.Data.SfxId.AutocannonShot, from, 0.35f, 1.6f);
        }

        private ITargetable PickTarget(Vector3 shipPos)
        {
            ITargetable best = null;
            float bestD = 60f * 60f;
            foreach (var kind in new[] { TargetKind.Aircraft, TargetKind.Surface })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    float d = Flat(t.Transform.position - shipPos).sqrMagnitude * (kind == TargetKind.Aircraft ? 0.6f : 1f);   // 항공기 먼저
                    if (d < bestD) { bestD = d; best = t; }
                }
            }
            return best;
        }

        /// <summary>조기경보기: 3초마다 레이더 고리를 뿌리고 70m 안 잠수함을 4초 드러낸다.</summary>
        private void Ping(float dt, Vector3 shipPos)
        {
            _pingTimer -= dt;
            if (_pingTimer > 0f) return;
            _pingTimer = 3f;
            RadarPings++;
            SignatureFx.Ring(new Vector3(transform.position.x, 0f, transform.position.z), 70f, 1.8f, new Color(0.35f, 0.85f, 1f, 0.7f), 0.7f);
            var subs = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine);
            for (int i = 0; i < subs.Count; i++)
                if (subs[i] is SubmarineBase sub && sub.IsAlive && Flat(sub.transform.position - shipPos).magnitude < 70f) sub.RevealFor(4f);
        }

        private void Steer(Vector3 goal, float wantAlt, float dt)
        {
            Vector3 flat = Flat(goal - transform.position);
            if (flat.sqrMagnitude < 1f) flat = Flat(transform.forward);
            float climb = Mathf.Clamp((wantAlt - transform.position.y) * 0.08f, -0.35f, 0.35f);
            var want = Quaternion.LookRotation((flat.normalized + Vector3.up * climb).normalized, Vector3.up);
            float before = transform.eulerAngles.y;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, _turn * dt);
            // 선회 방향으로 기울인다(뱅크)
            float yawRate = Mathf.DeltaAngle(before, transform.eulerAngles.y) / dt;
            var model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (model != null) model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(0f, 0f, Mathf.Clamp(-yawRate * 0.5f, -55f, 55f)), dt * 4f);
            transform.position += transform.forward * _speed * dt;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ------------------------------------------------------------ 모델(런타임 도형)

        private static readonly Color Hull = new(0.55f, 0.6f, 0.66f);
        private static readonly Color Dark = new(0.2f, 0.23f, 0.27f);

        private void BuildFighter()
        {
            var m = new GameObject("Model").transform;
            m.SetParent(transform, false);
            var body = SignatureFx.Lit(Hull);
            Part(PrimitiveType.Cylinder, m, new Vector3(0f, 0f, 0f), new Vector3(0.7f, 2.6f, 0.7f), body, new Vector3(90f, 0f, 0f));   // 동체(길이 5.2)
            Part(PrimitiveType.Sphere, m, new Vector3(0f, 0f, 2.7f), new Vector3(0.62f, 0.55f, 1.6f), body);                            // 기수
            Part(PrimitiveType.Sphere, m, new Vector3(0f, 0.38f, 1.4f), new Vector3(0.45f, 0.32f, 1.2f), SignatureFx.Lit(new Color(0.15f, 0.25f, 0.35f), 0.9f));   // 조종석
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, m, new Vector3(s * 1.5f, -0.05f, -0.4f), new Vector3(2.6f, 0.08f, 1.9f), body, new Vector3(0f, s * -24f, 0f));   // 날개
                Part(PrimitiveType.Cube, m, new Vector3(s * 0.45f, 0.6f, -2.1f), new Vector3(0.08f, 1.1f, 0.9f), body, new Vector3(0f, 0f, s * 14f));    // 수직 꼬리
                Part(PrimitiveType.Cube, m, new Vector3(s * 0.85f, 0f, -2.35f), new Vector3(1.2f, 0.06f, 0.7f), body);                                      // 수평 꼬리
                var tip = new GameObject("Wingtip").transform;
                tip.SetParent(m, false);
                tip.localPosition = new Vector3(s * 2.7f, -0.05f, -0.9f);
                Trail(tip, 0.18f, 0.9f);
                Part(PrimitiveType.Sphere, m, new Vector3(s * 2.75f, 0f, -0.85f), Vector3.one * 0.18f,
                     SignatureFx.Lit(s < 0 ? Color.red : Color.green, 0.5f, s < 0 ? new Color(2f, 0.2f, 0.2f) : new Color(0.2f, 2f, 0.3f)));   // 항법등
            }
            _burner = Part(PrimitiveType.Sphere, m, new Vector3(0f, 0f, -2.75f), new Vector3(0.5f, 0.5f, 0.9f),
                           SignatureFx.Lit(new Color(1f, 0.6f, 0.2f), 0.2f, new Color(4f, 1.8f, 0.4f))).GetComponent<Renderer>();
        }

        private void BuildAew()
        {
            var m = new GameObject("Model").transform;
            m.SetParent(transform, false);
            var body = SignatureFx.Lit(new Color(0.78f, 0.8f, 0.82f));
            Part(PrimitiveType.Cylinder, m, Vector3.zero, new Vector3(1.1f, 3.4f, 1.1f), body, new Vector3(90f, 0f, 0f));        // 동체(길이 6.8)
            Part(PrimitiveType.Sphere, m, new Vector3(0f, 0f, 3.4f), new Vector3(1.05f, 1f, 1.6f), body);
            Part(PrimitiveType.Cube, m, new Vector3(0f, 0.55f, 0.4f), new Vector3(12f, 0.16f, 1.3f), body);                      // 고익
            foreach (float s in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cylinder, m, new Vector3(s * 2.4f, 0.35f, 1.1f), new Vector3(0.45f, 0.7f, 0.45f), body, new Vector3(90f, 0f, 0f));   // 엔진
                Part(PrimitiveType.Cylinder, m, new Vector3(s * 2.4f, 0.35f, 1.85f), new Vector3(1.9f, 0.02f, 1.9f), SignatureFx.Lit(Dark), new Vector3(90f, 0f, 0f));   // 프로펠러 원판
                Part(PrimitiveType.Cube, m, new Vector3(s * 1.2f, 0.9f, -3.1f), new Vector3(0.08f, 1.3f, 0.9f), body);         // 수직 꼬리(넷 중 바깥 둘)
                Part(PrimitiveType.Cube, m, new Vector3(s * 0.45f, 0.9f, -3.1f), new Vector3(0.08f, 1.1f, 0.8f), body);
                var tip = new GameObject("Wingtip").transform;
                tip.SetParent(m, false);
                tip.localPosition = new Vector3(s * 6f, 0.55f, 0.2f);
                Trail(tip, 0.12f, 0.6f);
            }
            Part(PrimitiveType.Cube, m, new Vector3(0f, 0.75f, -3.1f), new Vector3(3f, 0.08f, 0.8f), body);                     // 수평 꼬리
            Part(PrimitiveType.Cylinder, m, new Vector3(0f, 1.25f, -0.6f), new Vector3(0.2f, 0.35f, 0.2f), body);              // 레이더돔 기둥
            _rotodome = Part(PrimitiveType.Cylinder, m, new Vector3(0f, 1.65f, -0.6f), new Vector3(3.4f, 0.18f, 3.4f), SignatureFx.Lit(new Color(0.92f, 0.93f, 0.95f)));
            Part(PrimitiveType.Cube, _rotodome, new Vector3(0f, 1.1f, 0f), new Vector3(0.9f, 0.6f, 0.06f), SignatureFx.Lit(Dark));   // 돔 무늬(도는 게 보이게)
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        private static void Trail(Transform at, float width, float time)
        {
            var tr = at.gameObject.AddComponent<TrailRenderer>();
            tr.time = time;
            tr.widthMultiplier = width;
            tr.minVertexDistance = 0.4f;
            tr.sharedMaterial = SignatureFx.Additive;
            tr.startColor = new Color(1f, 1f, 1f, 0.75f);
            tr.endColor = new Color(1f, 1f, 1f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
