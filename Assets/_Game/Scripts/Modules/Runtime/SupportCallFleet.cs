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
    /// 머무는 시간이 끝나면 바깥쪽으로 빠져 사라진다. 표적이 아니며(격침되지 않음) 섬은 고려하지 않는다(임시).
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
            public Vector3 Velocity;
        }

        private ShipController _player;
        private readonly List<Unit> _units = new();
        private Unit _supply;
        private Phase _phase = Phase.Approach;
        private float _phaseTime, _stay, _skillCut;
        private int _repairs;
        private string _source;
        private bool _supplied;
        private LineRenderer _hose;

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
            Vector3 origin = player.transform.position - fwd * SpawnBehind;
            float halfBeam = HalfBeam;

            // 보급선: 왼현에 나란히(선체 반폭 + 보급선 반폭 + 3m)
            var sRoot = new GameObject("Supply ship").transform;
            sRoot.SetParent(transform, false);
            var model = SupportFx.BuildSupplyShip(sRoot, Level);
            float supplyHalfBeam = Level == 1 ? 1.6f : Level == 2 ? 2.0f : 2.4f;
            _supply = new Unit { T = sRoot, Slot = new Vector3(-(halfBeam + supplyHalfBeam + 3f), 0f, 0f), Supply = true, Phase = Random.value * 6f };
            sRoot.position = origin - right * 14f;
            sRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _units.Add(_supply);

            // 고속정 편대: 뒤에서 V자로 들어와 자리로 흩어진다
            for (int i = 0; i < boats && i < BoatSlots.Length; i++)
            {
                var bRoot = new GameObject($"Fast boat {i + 1}").transform;
                bRoot.SetParent(transform, false);
                var b = SupportFx.BuildFastBoat(bRoot);
                int row = i / 2 + 1; float side = i % 2 == 0 ? 1f : -1f;
                bRoot.position = origin + right * (side * row * 4.5f + 6f) - fwd * (row * 6f);
                bRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                var slot = BoatSlots[i];
                _units.Add(new Unit
                {
                    T = bRoot,
                    Slot = new Vector3(Mathf.Sign(slot.x) * (Mathf.Abs(slot.x) + halfBeam), 0f, slot.y),
                    Muzzle = ModuleVariantVisual.Find(b, "Muzzle"),
                    GunAt = Time.time + Random.Range(0.5f, 1.5f),
                    Phase = Random.value * 6f,
                });
            }
            CombatLog.Add(_source, $"지원 함정 호출 · 보급선 Lv{Level} + 고속정 {boats}척 · {stay:0}초");
        }

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
            if (p == Phase.Depart && _hose != null) { Destroy(_hose.gameObject); _hose = null; }
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
                u.Velocity = Vector3.Lerp(u.Velocity, vel, 1f - Mathf.Exp(-3f * dt));
                Vector3 pos = u.T.position + u.Velocity * dt;
                pos.y = Mathf.Sin(Time.time * 1.3f + u.Phase) * 0.06f;
                u.T.position = pos;

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
            _hose.widthMultiplier = 0.12f;
            _hose.positionCount = 8;
            _hose.useWorldSpace = true;
            _hose.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UpdateHose();
        }

        private void UpdateHose()
        {
            if (_hose == null || _supply == null || _player == null) return;
            var tp = ModuleVariantVisual.Find(_supply.T, "TransferPoint");
            Vector3 a = tp != null ? tp.position : _supply.T.position + Vector3.up * 2f;
            Vector3 right = Vector3.Cross(Vector3.up, Fwd);
            Vector3 b = _player.transform.position - right * (HalfBeam - 0.6f) + Vector3.up * 1.6f;
            for (int i = 0; i < 8; i++)
            {
                float t = i / 7f;
                _hose.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.9f));   // 늘어진 호스
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
