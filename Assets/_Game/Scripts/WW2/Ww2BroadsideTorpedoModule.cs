using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Modules;

namespace Game.WW2
{
    /// <summary>현측: 좌현(−1)·우현(+1). 격자 Z+가 우현이다.</summary>
    public enum Ww2Side { Port = -1, Starboard = 1 }

    /// <summary>
    /// 현측 어뢰 발사관(WWII 패치 B, ww2_mod_broadside_torpedo). 스스로 쏘지 않는다 — 플레이어가 좌현/우현 일제 발사를 명령한다
    /// (<see cref="Ww2TorpedoControl"/>: 키를 누르고 있으면 예상 경로, 떼면 발사).
    ///   - 사격각: 고른 현측의 정횡(배 옆 방향) ± FireArcDegrees/2(기본 ±30°). 밖이면 이유를 알리고 쏘지 않는다.
    ///   - 발사 통로: 그 현측 바깥 칸이 비어 있어야 한다(가운데 줄 발사관은 양쪽 모두, 옆에 블록이 붙으면 그쪽은 못 쏜다).
    ///     설치·회전·재배치마다 다시 계산한다.
    ///   - 일제 발사: 장전된 발사관 전부(MagazineCapacity)를 SpreadDegrees 간격 부채꼴로. 다 쓰면 AmmoReloadTime 동안 전량 재장전.
    ///   - 어뢰는 직진(<see cref="Ww2StraightTorpedo"/>), 사거리 Range, 신관 작동 MinRange, 피해 Damage, 속력 ProjectileSpeed.
    ///   - 성장 카드 장탄수는 어뢰에 적용하지 않는다(패치 A 6장).
    /// </summary>
    public class Ww2BroadsideTorpedoModule : ModuleRuntime, IAmmoUser
    {
        public const float SpreadDegrees = 3f;

        [SerializeField] private GameObject torpedoPrefab;
        [Tooltip("발사관 묶음(쏘는 현측으로 돈다)")]
        [SerializeField] private Transform mount;
        [SerializeField] private float mountTurnRate = 90f;

        private readonly AmmoMagazine _ammo = new();
        private bool _portOpen, _starboardOpen;
        private float _mountYaw, _mountYawTarget;
        private Quaternion _mountRest;
        private bool _restCaptured;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => false;
        public int Fired { get; private set; }
        public float HalfArc => Mathf.Max(5f, Stats.FireArcDegrees * 0.5f);

        public bool IsOpen(Ww2Side side) => side == Ww2Side.Port ? _portOpen : _starboardOpen;
        public bool Operational => Instance != null && Instance.IsOperational;
        public int Loaded => _ammo.Infinite ? 0 : _ammo.Current;
        public int Tubes => _ammo.Capacity;
        public bool Reloading => _ammo.IsReloading;
        public float ReloadLeft => _ammo.SecondsToNext;

        protected override void OnInitialized()
        {
            if (torpedoPrefab == null) Debug.LogError("[WW2 어뢰] torpedoPrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            if (mount != null && !_restCaptured) { _mountRest = mount.localRotation; _restCaptured = true; }
            OnShipLayoutChanged();
        }

        /// <summary>현측 바깥 칸이 비었는지 다시 잰다(설치·회전·이웃 변화).</summary>
        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            _portOpen = PlacementRuleEvaluator.IsOpen(Grid, Instance.OccupiedCoords, 0, -1);
            _starboardOpen = PlacementRuleEvaluator.IsOpen(Grid, Instance.OccupiedCoords, 0, +1);
        }

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            if (mount == null) return;
            _mountYaw = Mathf.MoveTowardsAngle(_mountYaw, _mountYawTarget, mountTurnRate * dt);
            mount.localRotation = Quaternion.AngleAxis(_mountYaw, Vector3.up) * _mountRest;
        }

        /// <summary>배 옆(정횡) 방향(월드, 수평).</summary>
        public Vector3 Beam(Ww2Side side)
        {
            var ship = Ship != null ? Ship.transform : transform;
            Vector3 r = ship.right * (int)side;
            r.y = 0f;
            return r.normalized;
        }

        /// <summary>발사 지점(발사관 바깥쪽 수면).</summary>
        public Vector3 LaunchOrigin(Ww2Side side)
        {
            Vector3 p = (mount != null ? mount.position : transform.position) + Beam(side) * 1.4f;
            p.y = 0f;
            return p;
        }

        /// <summary>
        /// 원하는 방향을 이 현측 사격각에 맞춘다. 사격각 안이면 그대로 true, 밖이면 가장 가까운 경계로 잘라 false.
        /// </summary>
        public bool ClampToArc(Ww2Side side, Vector3 desired, out Vector3 clamped, out float offBeamDegrees)
        {
            Vector3 beam = Beam(side);
            desired.y = 0f;
            if (desired.sqrMagnitude < 1e-4f) desired = beam;
            offBeamDegrees = Vector3.SignedAngle(beam, desired, Vector3.up);
            float half = HalfArc;
            bool inside = Mathf.Abs(offBeamDegrees) <= half + 0.01f;
            clamped = Quaternion.Euler(0f, Mathf.Clamp(offBeamDegrees, -half, half), 0f) * beam;
            return inside;
        }

        /// <summary>이 현측으로 지금 쏠 수 있는가(이유 포함). 사격각은 따로(<see cref="ClampToArc"/>).</summary>
        public bool CanFire(Ww2Side side, out string reason)
        {
            reason = null;
            if (!Operational) { reason = "발사관 파괴됨"; return false; }
            if (!IsOpen(side)) { reason = $"{SideName(side)} 발사 통로가 막힘(옆 칸 블록)"; return false; }
            if (_ammo.IsReloading) { reason = $"재장전 중 {Mathf.CeilToInt(_ammo.SecondsToNext)}초"; return false; }
            if (!_ammo.CanFire) { reason = "장전된 어뢰 없음"; return false; }
            return true;
        }

        /// <summary>일제 발사: 장전된 어뢰 전부를 direction 중심 부채꼴로. 쏜 발 수.</summary>
        public int Fire(Ww2Side side, Vector3 direction)
        {
            if (!CanFire(side, out _) || torpedoPrefab == null) return 0;
            int count = _ammo.Current;
            Vector3 origin = LaunchOrigin(side);
            Vector3 along = Vector3.Cross(Vector3.up, direction).normalized;
            var s = Stats;
            int fired = 0;
            for (int i = 0; i < count; i++)
            {
                if (!_ammo.Consume()) break;
                float a = (i - (count - 1) * 0.5f) * SpreadDegrees;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * direction;
                Vector3 p = origin + along * ((i - (count - 1) * 0.5f) * 0.45f);
                var go = PoolManager.Instance?.Spawn(torpedoPrefab, p, Quaternion.LookRotation(dir));
                if (go != null && go.TryGetComponent<Ww2StraightTorpedo>(out var torpedo))
                {
                    torpedo.Launch(dir, s.ProjectileSpeed, s.Range, s.MinRange, s.Damage);
                    fired++;
                }
            }
            Fired += fired;
            Train(side);
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, origin, 0.55f, 0.55f);
            CombatLog.Add("어뢰", $"{SideName(side)} 일제 발사 {fired}발");
            return fired;
        }

        /// <summary>조준 중인 현측으로 발사관 묶음을 돌린다(보이는 신호).</summary>
        public void Train(Ww2Side side)
        {
            if (mount == null || mount.parent == null) return;
            Vector3 local = mount.parent.InverseTransformDirection(Beam(side));
            Vector3 rest = _mountRest * Vector3.forward;
            local.y = rest.y = 0f;
            _mountYawTarget = Vector3.SignedAngle(rest, local, Vector3.up);   // 블록을 돌려 놓아도 현측을 향한다
        }

        public static string SideName(Ww2Side side) => side == Ww2Side.Port ? "좌현" : "우현";
    }
}
