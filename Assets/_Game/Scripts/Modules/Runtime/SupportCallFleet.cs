using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;
using Game.View;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 전투단 통신 중계가 부른 지원 함정(2026-11-22). 기함 뒤 약 55m에서 나타나 자리로 온다:
    ///   - 보급선: 기함 왼현에 나란히 붙으면 보급(탄약 전량 · 블록 수리 · 스킬 재사용 단축)하고, 머무는 동안 보급 호스로 이어진다.
    ///   - 고속정 편대: 기함 둘레 자리에서 조금씩 흔들리며 따라오고 26m 안의 적 수상함 · 항공기를 기관포로 쏜다.
    /// 머무는 시간이 끝나면 바깥쪽으로 빠져 사라진다. 표적이 아니다(격침되지 않음).
    /// 섬(2026-11-22): 나타날 자리는 섬이 없는 곳으로 고르고, 진행 방향 앞 · 좌우 30° 세 갈래 탐지선으로 섬을 비켜 가며,
    /// 그래도 섬에 닿으면 그 칸으로는 들어가지 않고 미끄러진다(자리가 섬 안이면 섬 가장자리에서 기다린다).
    /// 정비 · 패배 화면으로 넘어가면 바로 사라진다.
    /// </summary>
    public sealed class SupportCallFleet : MonoBehaviour
    {
        public enum Phase { Approach, Station, Depart }

        public const float SpawnBehind = 55f;
        public const float ApproachTimeout = 22f;
        public const float DepartTime = 7f;
        public const float BoatGunRange = 26f;
        public const float BoatGunInterval = 0.55f;
        public const float BoatGunDamage = 3f;

        // 고속정 자리(기함 기준 x = 우현, z = 함수 쪽, m). 왼현 가운데는 보급선 자리라 비운다.
        private static readonly Vector2[] BoatSlots =
        {
            new(12f, 15f), new(-12f, 19f), new(17f, -5f), new(-16f, -15f), new(9f, -19f), new(21f, 9f),
        };

        private sealed class Unit
        {
            public Transform T;
            public Vector3 Slot;          // 기함 기준 자리
            public bool Supply;
            public Transform Muzzle;
            public float GunAt, Phase;
            public float Radius;          // 섬 회피 반경(m)
            public Vector3 Velocity;
        }

        private static readonly float[] Whiskers = { 0f, -30f, 30f };

        /// <summary>검증용: 지원 함정이 섬 안에 들어간 프레임 수(0이어야 한다) · 섬을 비켜 간 프레임 수.</summary>
        public int IslandOverlapFrames { get; private set; }
        public int AvoidFrames { get; private set; }

        private ShipController _player;
        private readonly List<Unit> _units = new();
        private Unit _supply;
        private Transform _supplyModel;
        private float _supplyAbeam;     // 보급선이 기함 옆에 서는 거리(m, 기함 중심에서)
        private float _sideCheckAt;

        /// <summary>보급선이 붙는 쪽: −1 = 왼현(기본), +1 = 오른현. 왼현이 해안 · 섬에 막히면 오른현으로 바꾼다(2026-11-23 Codex 점검).</summary>
        public int SupplySide { get; private set; } = -1;
        /// <summary>검증용: 붙는 쪽을 바꾼 횟수 · 나타난 자리가 섬에 걸려 옮긴 함정 수.</summary>
        public int SideSwitches { get; private set; }
        public int SpawnAdjusted { get; private set; }
        private Phase _phase = Phase.Approach;
        private float _phaseTime, _stay, _skillCut;
        private int _repairs;
        private string _source;
        private bool _supplied;
        private LineRenderer _hose, _highline;
        private Transform _cargo;
        private float _cargoT;

        public Phase State => _phase;
        public int Level { get; private set; }
        public int BoatCount => _units.Count - (_supply != null ? 1 : 0);
        public float StayLeft => _phase == Phase.Station ? Mathf.Max(0f, _stay - _phaseTime) : _stay;
        public bool Supplied => _supplied;
        public Transform SupplyShip => _supply?.T;
        /// <summary>검증용: 마지막 보급 결과.</summary>
        public int LastRefilled { get; private set; }
        public int LastRepaired { get; private set; }
        public string LastSkill { get; private set; }
        public int BoatShots { get; private set; }
        public float BoatDamage { get; private set; }

        public string StatusText => _phase switch
        {
            Phase.Approach => "접근 중",
            Phase.Station => $"지원 중 · 남은 {StayLeft:0}초",
            _ => "복귀 중",
        };

        public static SupportCallFleet Summon(ShipController player, int level, int boats, float stay, int repairs, float skillCut, string source)
        {
            var go = new GameObject($"Support call fleet Lv{level}");
            var f = go.AddComponent<SupportCallFleet>();
            f.Init(player, level, boats, stay, repairs, skillCut, source);
            return f;
        }

        private void Init(ShipController player, int level, int boats, float stay, int repairs, float skillCut, string source)
        {
            _player = player;
            Level = Mathf.Clamp(level, 1, 3);
            _stay = stay;
            _repairs = repairs;
            _skillCut = skillCut;
            _source = source;

            Vector3 fwd = Fwd, right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = PickSpawn(player.transform.position, fwd);
            float halfBeam = HalfBeam;

            // 보급선: 왼현에 나란히(선체 반폭 + 보급선 반폭 + 3m)
            var sRoot = new GameObject("Supply ship").transform;
            sRoot.SetParent(transform, false);
            _supplyModel = SupportFx.BuildSupplyShip(sRoot, Level);
            float supplyHalfBeam = Level == 1 ? 1.6f : Level == 2 ? 2.0f : 2.4f;
            _supplyAbeam = halfBeam + supplyHalfBeam + 3f;
            _supply = new Unit { T = sRoot, Slot = new Vector3(-_supplyAbeam, 0f, 0f), Supply = true, Phase = Random.value * 6f, Radius = supplyHalfBeam + 0.4f };
            ChooseSupplySide(true);
            sRoot.position = ClearSpawn(origin + right * (14f * SupplySide), origin, _supply.Radius + 1.5f);
            sRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _units.Add(_supply);

            // 고속정 편대: 뒤에서 V자로 들어와 자리로 흩어진다
            for (int i = 0; i < boats && i < BoatSlots.Length; i++)
            {
                var bRoot = new GameObject($"Fast boat {i + 1}").transform;
                bRoot.SetParent(transform, false);
                var b = SupportFx.BuildFastBoat(bRoot);
                int row = i / 2 + 1; float side = i % 2 == 0 ? 1f : -1f;
                bRoot.position = ClearSpawn(origin + right * (side * row * 4.5f + 6f) - fwd * (row * 6f), origin, 2.5f);   // 실제 나타날 자리를 본다
                bRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                var slot = BoatSlots[i];
                _units.Add(new Unit
                {
                    T = bRoot,
                    Slot = new Vector3(Mathf.Sign(slot.x) * (Mathf.Abs(slot.x) + halfBeam), 0f, slot.y),
                    Muzzle = ModuleVariantVisual.Find(b, "Muzzle"),
                    GunAt = Time.time + Random.Range(0.5f, 1.5f),
                    Phase = Random.value * 6f,
                    Radius = 1.1f,
                });
            }
            CombatLog.Add(_source, $"지원 함정 호출 · 보급선 Lv{Level} + 고속정 {boats}척 · {stay:0}초");
        }

        /// <summary>
        /// 함정 하나가 실제로 나타날 자리(2026-11-23 Codex 점검 — 예전엔 무리 중심 12m만 보고 보급선은 그 옆 14m에 생겼다).
        /// 그 자리가 섬에 걸리면 무리 중심 쪽으로, 그래도 걸리면 둘레를 돌며 가장 가까운 빈 곳.
        /// </summary>
        private Vector3 ClearSpawn(Vector3 want, Vector3 origin, float radius)
        {
            if (Game.World.Islands.IsClear(want, radius)) return want;
            SpawnAdjusted++;
            for (float k = 0.75f; k >= 0f; k -= 0.25f)
            {
                Vector3 p = Vector3.Lerp(origin, want, k);
                if (Game.World.Islands.IsClear(p, radius)) return p;
            }
            for (float r = 6f; r <= 30f; r += 6f)
                for (int a = 0; a < 8; a++)
                {
                    Vector3 p = origin + Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward * r;
                    if (Game.World.Islands.IsClear(p, radius)) return p;
                }
            return want;
        }

        /// <summary>
        /// 보급선이 붙을 쪽: 그 쪽 자리(기함 옆 · 앞뒤로 보급선 길이만큼)가 섬에 걸리거나 기함과 그 자리 사이를 섬이 가리면 반대쪽으로.
        /// 둘 다 막히면 덜 막힌 쪽(지금 쪽 유지). 처음에는 바로, 그 뒤에는 0.5초마다 다시 본다.
        /// </summary>
        private void ChooseSupplySide(bool force)
        {
            if (_supply == null || _player == null) return;
            if (!force && Time.time < _sideCheckAt) return;
            _sideCheckAt = Time.time + 0.5f;
            bool okNow = SideClear(SupplySide), okOther = SideClear(-SupplySide);
            if (okNow || !okOther) return;
            SupplySide = -SupplySide;
            SideSwitches++;
            _supply.Slot = new Vector3(_supplyAbeam * SupplySide, 0f, 0f);
            // 보급 붐(TransferPoint)은 모델의 왼현 쪽 — 오른현에 붙으면 모델을 좌우로 뒤집어 기함 쪽을 향하게
            if (_supplyModel != null) { var sc = _supplyModel.localScale; sc.x = SupplySide < 0 ? Mathf.Abs(sc.x) : -Mathf.Abs(sc.x); _supplyModel.localScale = sc; }
            if (_hose != null) UpdateHose();
        }

        private bool SideClear(int side)
        {
            if (Game.World.Islands.All.Count == 0) return true;
            Vector3 fwd = Fwd, right = Vector3.Cross(Vector3.up, fwd);
            Vector3 p0 = _player.transform.position;
            Vector3 slot = p0 + right * (_supplyAbeam * side);
            float half = Level == 1 ? 5.5f : Level == 2 ? 7.5f : 9.5f;
            float r = _supply.Radius + 0.6f;
            return Game.World.Islands.IsClear(slot, r) && Game.World.Islands.IsClear(slot + fwd * half * 0.8f, r)
                && Game.World.Islands.IsClear(slot - fwd * half * 0.8f, r) && !Game.World.Islands.BlocksShipLine(p0, slot);
        }

        /// <summary>기함 뒤 55m에서 시작해 좌우로 30°씩 돌려 가며 섬이 없는(반경 12m) 곳을 고른다.</summary>
        private static Vector3 PickSpawn(Vector3 player, Vector3 fwd)
        {
            foreach (float yaw in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 120f, -120f })
            {
                Vector3 p = player - Quaternion.Euler(0f, yaw, 0f) * fwd * SpawnBehind;
                if (Game.World.Islands.IsClear(p, 12f) && !Game.World.Islands.BlocksShipLine(p, player)) return p;
            }
            foreach (float yaw in new[] { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f })
            {
                Vector3 p = player - Quaternion.Euler(0f, yaw, 0f) * fwd * (SpawnBehind * 0.6f);
                if (Game.World.Islands.IsClear(p, 10f)) return p;
            }
            return player - fwd * SpawnBehind;
        }

        /// <summary>
        /// 섬 비켜 가기: 진행 방향 앞 · 좌우 30° 탐지선이 섬에 닿으면 섬 가장자리 밖(법선 쪽 8m)을 향해 밀어 준다. 가까울수록 세게.
        /// </summary>
        private Vector3 AvoidIslands(Unit u, Vector3 vel, float max)
        {
            if (Game.World.Islands.All.Count == 0) return vel;
            float speed = vel.magnitude;
            Vector3 dir = speed > 0.3f ? vel / speed : (u.Velocity.sqrMagnitude > 0.1f ? u.Velocity.normalized : u.T.forward);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return vel;
            dir.Normalize();
            float len = 6f + Mathf.Max(speed, u.Velocity.magnitude) * 1.4f + u.Radius;
            Vector3 pos = u.T.position, push = Vector3.zero;
            foreach (float yaw in Whiskers)
            {
                Vector3 wd = Quaternion.Euler(0f, yaw, 0f) * dir;
                if (!Game.World.Islands.ProbeAhead(pos, wd, u.Radius, yaw == 0f ? len : len * 0.85f, out var hit)) continue;
                Vector3 n = hit.normal; n.y = 0f;
                if (n.sqrMagnitude < 1e-4f) n = -wd;
                n.Normalize();
                Vector3 goal = hit.point + n * (8f + u.Radius) - pos; goal.y = 0f;
                float close = 1f - Mathf.Clamp01(hit.distance / len);
                // 비켜 갈 쪽(섬을 따라)과 바깥쪽을 섞는다
                Vector3 along = Vector3.ProjectOnPlane(dir, n);
                if (along.sqrMagnitude < 1e-3f) along = Vector3.Cross(Vector3.up, n);
                push += (goal.normalized * (0.5f + close) + along.normalized * 0.6f) * Mathf.Max(speed, 6f);
            }
            if (push == Vector3.zero) return vel;
            AvoidFrames++;
            Vector3 v = vel + push;
            v.y = 0f;
            return v.sqrMagnitude > max * max ? v.normalized * max : v;
        }

        /// <summary>새 자리가 섬에 닿으면 들어가지 않는다: 축별로 미끄러지고, 그래도 막히면 제자리(이미 박혀 있으면 가장 가까운 섬 밖으로).</summary>
        private Vector3 KeepOffIslands(Unit u, Vector3 from, Vector3 to)
        {
            if (Game.World.Islands.All.Count == 0 || Game.World.Islands.IsClear(to, u.Radius)) return to;
            if (!Game.World.Islands.IsClear(from, u.Radius))
            {
                // 이미 박힘(나타나자마자 등): 가장 가까운 섬 중심에서 멀어진다
                Game.World.IslandInfo near = null; float best = float.MaxValue;
                foreach (var isl in Game.World.Islands.All)
                {
                    float d = (Flat(isl.Center) - Flat(from)).sqrMagnitude;
                    if (d < best) { best = d; near = isl; }
                }
                Vector3 away = near != null ? Flat(from - near.Center) : -u.T.forward;
                if (away.sqrMagnitude < 1e-4f) away = Vector3.forward;
                return from + away.normalized * Mathf.Max(4f, u.Velocity.magnitude) * Time.deltaTime;
            }
            Vector3 d2 = to - from;
            var sx = new Vector3(from.x + d2.x, to.y, from.z);
            if (Game.World.Islands.IsClear(sx, u.Radius)) { u.Velocity.z = 0f; return sx; }
            var sz = new Vector3(from.x, to.y, from.z + d2.z);
            if (Game.World.Islands.IsClear(sz, u.Radius)) { u.Velocity.x = 0f; return sz; }
            u.Velocity *= 0.2f;
            return new Vector3(from.x, to.y, from.z);
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        private Vector3 Fwd
        {
            get
            {
                var f = _player != null ? _player.transform.forward : Vector3.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        private float HalfBeam
        {
            get
            {
                var g = _player != null ? _player.Grid : null;
                return g != null ? (g.MaxHalfBeam + 0.5f) * g.CellSize : 3f;
            }
        }

        /// <summary>바로 떠나게 한다(블록이 사라짐 등).</summary>
        public void Dismiss() { if (this != null && _phase != Phase.Depart) Enter(Phase.Depart); }

        private void Enter(Phase p)
        {
            _phase = p;
            _phaseTime = 0f;
            if (p == Phase.Depart)
            {
                if (_hose != null) { Destroy(_hose.gameObject); _hose = null; }
                if (_highline != null) { Destroy(_highline.gameObject); _highline = null; }
                if (_cargo != null) { Destroy(_cargo.gameObject); _cargo = null; }
            }
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (_player == null || gm == null || gm.State == GameState.Refit || gm.State == GameState.GameOver || gm.State == GameState.Victory)
            { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _phaseTime += dt;

            Vector3 fwd = Fwd, right = Vector3.Cross(Vector3.up, fwd);
            Vector3 p0 = _player.transform.position; p0.y = 0f;
            float playerSpeed = Mathf.Abs(_player.CurrentSpeed);
            if (_phase != Phase.Depart) ChooseSupplySide(false);

            foreach (var u in _units)
            {
                if (u.T == null) continue;
                Vector3 want;
                if (_phase == Phase.Depart)
                {
                    Vector3 out_ = (u.Slot.x >= 0f ? right : -right) * 0.7f + fwd * 0.7f;
                    want = u.T.position + out_.normalized * 40f;
                }
                else
                {
                    Vector3 slot = u.Slot;
                    if (!u.Supply) slot += new Vector3(Mathf.Sin(Time.time * 0.7f + u.Phase) * 1.6f, 0f, Mathf.Cos(Time.time * 0.5f + u.Phase) * 1.2f);   // 편대가 조금씩 흔들린다
                    want = p0 + right * slot.x + fwd * slot.z;
                }
                Vector3 to = want - u.T.position; to.y = 0f;
                float dist = to.magnitude;
                float max = u.Supply ? Mathf.Max(14f, playerSpeed + 5f) : Mathf.Max(22f, playerSpeed + 8f);
                float speed = _phase == Phase.Depart ? max : Mathf.Min(max, dist * 1.4f + playerSpeed * 0.9f);
                Vector3 vel = dist > 0.01f ? to / dist * speed : Vector3.zero;
                vel = AvoidIslands(u, vel, max);
                u.Velocity = Vector3.Lerp(u.Velocity, vel, 1f - Mathf.Exp(-3f * dt));
                Vector3 pos = KeepOffIslands(u, u.T.position, u.T.position + u.Velocity * dt);
                pos.y = Mathf.Sin(Time.time * 1.3f + u.Phase) * 0.06f;
                u.T.position = pos;
                if (Game.World.Islands.All.Count > 0 && !Game.World.Islands.IsClear(pos, 0.3f)) IslandOverlapFrames++;

                // 빨리 움직이면 진행 방향, 자리에 붙으면 기함과 같은 방향
                Vector3 face = u.Velocity.sqrMagnitude > (playerSpeed + 3f) * (playerSpeed + 3f) * 0.5f || _phase == Phase.Depart ? u.Velocity : fwd;
                face.y = 0f;
                if (face.sqrMagnitude > 1e-4f)
                {
                    var rot = Quaternion.LookRotation(face.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 1.1f + u.Phase) * 2.5f);
                    u.T.rotation = Quaternion.Slerp(u.T.rotation, rot, 1f - Mathf.Exp(-3.5f * dt));
                }

                if (!u.Supply && _phase != Phase.Depart) BoatGun(u);
            }

            switch (_phase)
            {
                case Phase.Approach:
                {
                    if (_supply != null && SlotDistance(_supply, p0, fwd, right) < 4f) { Arrive(); Enter(Phase.Station); }
                    else if (_phaseTime > ApproachTimeout) Enter(Phase.Station);
                    break;
                }
                case Phase.Station:
                    if (!_supplied && _supply != null && SlotDistance(_supply, p0, fwd, right) < 6f) Arrive();
                    UpdateHose();
                    if (_phaseTime >= _stay) { Enter(Phase.Depart); GameEvents.RaiseBanner("지원 함정 복귀"); }
                    break;
                case Phase.Depart:
                    if (_phaseTime >= DepartTime) Destroy(gameObject);
                    break;
            }
        }

        private float SlotDistance(Unit u, Vector3 p0, Vector3 fwd, Vector3 right)
        {
            Vector3 want = p0 + right * u.Slot.x + fwd * u.Slot.z;
            Vector3 d = want - u.T.position; d.y = 0f;
            return d.magnitude;
        }

        // ------------------------------------------------------------ 보급

        private void Arrive()
        {
            if (_supplied) return;
            _supplied = true;
            var grid = _player.Grid;
            int refilled = 0, repaired = 0;
            if (grid != null)
            {
                foreach (var m in grid.Modules)
                    if (m?.Runtime is IAmmoUser user && user.Ammo != null && !user.Ammo.Infinite && user.Ammo.Current < user.Ammo.Capacity)
                    { user.Ammo.Refill(); refilled++; }

                // 부서진 블록 먼저, 없으면 가장 많이 상한 블록부터
                var list = new List<ModuleInstance>();
                foreach (var m in grid.Modules) if (m != null && m.Definition != null) list.Add(m);
                list.Sort((a, b) =>
                {
                    int da = a.IsDestroyed ? 0 : 1, db = b.IsDestroyed ? 0 : 1;
                    if (da != db) return da.CompareTo(db);
                    return (a.Hp / Mathf.Max(1f, a.MaxHp)).CompareTo(b.Hp / Mathf.Max(1f, b.MaxHp));
                });
                foreach (var m in list)
                {
                    if (repaired >= _repairs) break;
                    if (m.IsDestroyed) { m.Restore(1f); repaired++; }
                    else if (m.Hp < m.MaxHp - 0.01f) { m.Repair(m.MaxHp); repaired++; }
                }
            }
            LastRefilled = refilled;
            LastRepaired = repaired;
            LastSkill = CutSkill();
            string skill = LastSkill != null ? $" · {LastSkill} 재사용 −{_skillCut * 100f:0}%" : "";
            GameEvents.RaiseBanner($"보급 완료 — 탄약 보급 {refilled} · 블록 수리 {repaired}{skill}");
            CombatLog.Add(_source, $"보급: 탄약 {refilled}기 · 수리 {repaired}개{skill}");
            AudioManager.Play(Game.Data.SfxId.SonarPing, _player.transform.position, 0.5f, 0.7f);
            BuildHose();
        }

        /// <summary>재사용 중인 스킬 가운데 남은 시간이 가장 긴 것 하나를 줄인다. 줄인 스킬 이름, 없으면 null.</summary>
        private string CutSkill()
        {
            string best = null; float bestLeft = 0f; System.Action apply = null;
            var sig = SignatureSkill.Active;
            if (sig != null && sig.CooldownLeft > bestLeft)
            {
                bestLeft = sig.CooldownLeft;
                best = SignatureSkill.Describe(sig.Kind).title;
                apply = () => sig.ReduceCooldown(_skillCut);
            }
            var grid = _player.Grid;
            if (grid != null)
                foreach (var m in grid.Modules)
                {
                    if (m == null || !m.IsOperational) continue;
                    if (m.Runtime is BridgeModule bridge && bridge.FlankCooldownLeft > bestLeft)
                    {
                        bestLeft = bridge.FlankCooldownLeft; best = "전속";
                        var b = bridge; apply = () => b.ReduceFlankCooldown(_skillCut);
                    }
                    if (m.Runtime is DecoyLauncherModule decoy && decoy.DecoyEnabled && decoy.CooldownLeft > bestLeft)
                    {
                        bestLeft = decoy.CooldownLeft; best = "기만체";
                        var d = decoy; apply = () => d.ReduceCooldown(_skillCut);
                    }
                }
            apply?.Invoke();
            return best;
        }

        private void BuildHose()
        {
            if (_supply == null || _hose != null) return;
            var go = new GameObject("Supply hose");
            go.transform.SetParent(transform, false);
            _hose = go.AddComponent<LineRenderer>();
            _hose.sharedMaterial = SignatureFx.Lit(new Color(0.12f, 0.12f, 0.12f));
            _hose.widthMultiplier = 0.16f;
            _hose.positionCount = 12;
            _hose.useWorldSpace = true;
            _hose.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 화물 줄(높이 걸린 줄) + 오가는 화물 상자 — 보급 중임을 멀리서도 알게
            var hl = new GameObject("Supply highline");
            hl.transform.SetParent(transform, false);
            _highline = hl.AddComponent<LineRenderer>();
            _highline.sharedMaterial = SignatureFx.Lit(new Color(0.85f, 0.62f, 0.2f));
            _highline.widthMultiplier = 0.06f;
            _highline.positionCount = 2;
            _highline.useWorldSpace = true;
            _highline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Supply cargo";
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(transform, false);
            box.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
            box.GetComponent<Renderer>().sharedMaterial = SignatureFx.Lit(new Color(0.86f, 0.62f, 0.18f));
            _cargo = box.transform;
            UpdateHose();
        }

        private void UpdateHose()
        {
            if (_hose == null || _supply == null || _player == null) return;
            var tp = ModuleVariantVisual.Find(_supply.T, "TransferPoint");
            Vector3 a = tp != null ? tp.position : _supply.T.position + Vector3.up * 2f;
            Vector3 right = Vector3.Cross(Vector3.up, Fwd);
            Vector3 b = _player.transform.position + right * ((HalfBeam - 0.6f) * SupplySide) + Vector3.up * 1.6f;
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                float sway = Mathf.Sin(Time.time * 1.6f + t * 3f) * 0.12f;   // 두 배가 흔들리며 호스도 출렁인다
                _hose.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * (1.1f + sway)));
            }
            if (_highline != null && _cargo != null)
            {
                Vector3 ha = a + Vector3.up * 1.3f, hb = b + Vector3.up * 1.6f;
                _highline.SetPosition(0, ha); _highline.SetPosition(1, hb);
                _cargoT += Time.deltaTime / 2.6f;                   // 2.6초에 한 번 건너간다
                float k = Mathf.PingPong(_cargoT, 1f);
                k = k * k * (3f - 2f * k);
                _cargo.position = Vector3.Lerp(ha, hb, k) + Vector3.down * (0.45f + Mathf.Sin(k * Mathf.PI) * 0.25f);
                _cargo.rotation = Quaternion.LookRotation(Flat(hb - ha).sqrMagnitude > 1e-4f ? Flat(hb - ha) : Vector3.forward, Vector3.up);
            }
        }

        // ------------------------------------------------------------ 고속정 기관포

        private void BoatGun(Unit u)
        {
            if (Time.time < u.GunAt) return;
            Vector3 self = u.T.position;
            ITargetable best = null; float bestSqr = BoatGunRange * BoatGunRange;
            Consider(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface), true);
            Consider(TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Aircraft), false);
            if (best == null || best is not IDamageable target || !target.IsAlive) { u.GunAt = Time.time + 0.25f; return; }

            u.GunAt = Time.time + BoatGunInterval / RunUpgrades.FireRateMultiplier(ModuleType.Autocannon);
            Vector3 hit = best.Transform.position + Vector3.up * 0.6f;
            Vector3 muzzle = u.Muzzle != null ? u.Muzzle.position : self + Vector3.up;
            float damage = BoatGunDamage * (1f + RunUpgrades.Get(RunStat.Damage));
            target.TakeDamage(new DamageInfo(damage, hit, (hit - muzzle).normalized, DamageSource.Gun, "지원 고속정"));
            BoatShots++;
            BoatDamage += damage;
            SupportFx.Tracer(muzzle, hit, new Color(1f, 0.85f, 0.45f, 1f), 0.1f, 0.08f);
            Explosions.Spawn(hit, 0.2f, Explosions.Kind.Impact, false);
            AudioManager.Play(Game.Data.SfxId.EscortGunShot, muzzle, 0.55f, 1.2f);

            void Consider(IReadOnlyList<ITargetable> list, bool surface)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i];
                    if (t == null || !t.IsAlive || !t.IsRevealed || t.Transform == null) continue;
                    Vector3 d = t.Transform.position - self; d.y = 0f;
                    if (d.sqrMagnitude >= bestSqr) continue;
                    if (surface && Game.World.Islands.BlocksShipLine(self, t.Transform.position)) continue;
                    bestSqr = d.sqrMagnitude;
                    best = t;
                }
            }
        }
    }
}
