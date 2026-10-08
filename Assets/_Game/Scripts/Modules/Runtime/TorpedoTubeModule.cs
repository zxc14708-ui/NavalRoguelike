using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 경어뢰 발사관(대잠 전용, 2026-10-08). 소나 접촉·실제 부상·어뢰 발사 흔적을 향해 트인 현측으로
    /// 경어뢰 3발을 부채꼴(−fanAngle / 0 / +fanAngle)로 차례로 쏜다.
    ///   - 잠항한 잠수함은 접촉 위치에 오차가 있다. 부채꼴이 그 오차를 덮고, 어뢰는 반경 안에 들어온 잠수함을 추적한다.
    ///   - 사거리 MinRange~Range(8~45). 폭뢰(0~22)와 VLS 대잠(16~48) 사이를 메운다.
    ///   - 좌현이나 우현이 트인 자리에만 놓이고(PlacementZone.SideOnly), 트인 현측의 정횡 ±75° 안으로만 쏜다.
    /// 어뢰는 VLS 대잠탄과 같은 AswTorpedo — 잠항 중인 잠수함에도 맞는다.
    /// </summary>
    public class TorpedoTubeModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject torpedoPrefab;

        [Tooltip("발사관 묶음. 쏘는 현측을 향해 돈다(없으면 돌지 않음).")]
        [SerializeField] private Transform mount;
        [SerializeField] private Transform[] launchPoints;

        [Tooltip("부채꼴 한 번에 쏘는 어뢰 수")]
        [SerializeField, Min(1)] private int salvo = 3;
        [Tooltip("부채꼴 양끝 각도(도). 3발이면 −각도 / 0 / +각도")]
        [SerializeField] private float fanAngle = 20f;
        [Tooltip("부채꼴 안에서 한 발씩 쏘는 간격(초)")]
        [SerializeField] private float shotSpacing = 0.3f;
        [Tooltip("어뢰가 단서 지점까지 가는 속력(AswTorpedo와 맞출 것). 잠수함 이동 예측에 쓴다.")]
        [SerializeField] private float torpedoSpeed = 10f;
        [SerializeField] private float mountTurnRate = 160f;

        private readonly AmmoMagazine _ammo = new();
        private TargetingSystem _targeting;
        private float _cooldown, _shotTimer, _lastShotTime = -999f;
        private int _pending, _fanIndex, _nextTube;
        private Vector3 _fanDir, _fanAim;
        private float _fanDistance;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.8f;

        /// <summary>검증용: 마지막 부채꼴의 갈래 끝 지점들.</summary>
        public List<Vector3> LastAims { get; } = new();
        public int Salvos { get; private set; }

        private Vector3 ShipForward
        {
            get
            {
                var f = Grid != null ? Grid.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (torpedoPrefab == null) Debug.LogError("[TorpedoTube] torpedoPrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
        }

        /// <summary>트인 현측의 정횡 ±75° 안인가. 형태가 아직 판정되지 않은 직접 설치(검증)는 양현 모두 허용.</summary>
        public bool CanReach(Vector3 aim)
        {
            Vector3 d = aim - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return false;
            Vector3 right = Vector3.Cross(Vector3.up, ShipForward);
            var sides = Instance != null && Instance.Variant != ModuleVariant.None
                ? Instance.Sides : ModuleSides.Port | ModuleSides.Starboard;
            float half = ModuleVariants.TorpedoTubeHalfArc;
            if ((sides & ModuleSides.Starboard) != 0 && Vector3.Angle(right, d) <= half) return true;
            if ((sides & ModuleSides.Port) != 0 && Vector3.Angle(-right, d) <= half) return true;
            return false;
        }

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            if (_pending > 0) { FanTick(dt); return; }
            if (_cooldown > 0f) { _cooldown -= dt; return; }
            if (torpedoPrefab == null || !_ammo.CanFire) return;

            if (!TryFindAim(out Vector3 aim)) { _cooldown = 0.25f; return; }

            Vector3 delta = aim - transform.position;
            delta.y = 0f;
            _fanDistance = delta.magnitude;
            _fanDir = delta / Mathf.Max(0.01f, _fanDistance);
            _fanAim = aim;
            _pending = salvo;
            _fanIndex = 0;
            _shotTimer = 0f;
            Salvos++;
            LastAims.Clear();
            CombatLog.Add("대잠", $"{LogName} 경어뢰 부채꼴 발사 — 거리 {_fanDistance:0}m");
        }

        /// <summary>부채꼴을 한 발씩: 왼쪽 끝 → 가운데 → 오른쪽 끝.</summary>
        private void FanTick(float dt)
        {
            AimMount(_fanDir, dt);
            _shotTimer -= dt;
            if (_shotTimer > 0f) return;

            if (!_ammo.Consume()) { EndFan(); return; }   // 남은 어뢰만큼만
            float t = salvo <= 1 ? 0.5f : _fanIndex / (salvo - 1f);
            float angle = Mathf.Lerp(-fanAngle, fanAngle, t);
            Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * _fanDir;
            Vector3 cue = transform.position + dir * _fanDistance;
            cue.y = _fanAim.y;

            var tube = NextTube();
            var go = PoolManager.Instance?.Spawn(torpedoPrefab, tube.position, Quaternion.LookRotation(dir, Vector3.up));
            go?.GetComponent<AswTorpedo>()?.LaunchFromTube(tube.position, dir, cue, Stats.Damage);
            LastAims.Add(cue);
            _lastShotTime = Time.time;
            AudioManager.Play(Game.Data.SfxId.EscortTorpedoLaunch, transform.position, 0.75f, 0.6f);

            _fanIndex++;
            _pending--;
            _shotTimer = shotSpacing;
            if (_pending <= 0) EndFan();
        }

        private void EndFan()
        {
            _pending = 0;
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime);
        }

        /// <summary>
        /// 쏠 곳: 사거리 안에서 가장 가까운 잠수함(소나 접촉 = 마지막 위치 + 어뢰가 닿는 동안의 이동, 부상 = 실제 위치),
        /// 없으면 어뢰 발사 흔적. 트인 현측으로 닿지 않는 곳은 건너뛴다.
        /// </summary>
        private bool TryFindAim(out Vector3 aim)
        {
            aim = default;
            float min = Stats.MinRange, max = Stats.Range;
            float bestSqr = max * max;
            bool found = false;

            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                if (!sub.IsContactConfirmed && !sub.IsRevealed) continue;
                Vector3 reported = sub.IsRevealed ? sub.transform.position : sub.LastKnownPosition;
                Vector3 delta = reported - transform.position;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d >= bestSqr || d < min * min) continue;

                Vector3 predicted = reported + sub.LastKnownVelocity * (Mathf.Sqrt(d) / Mathf.Max(1f, torpedoSpeed));
                if (!CanReach(predicted)) continue;
                bestSqr = d;
                aim = predicted;
                found = true;
            }
            if (found) return true;

            if (_targeting != null && _targeting.TryGetLaunchCue(transform.position, max, out Vector3 cue))
            {
                Vector3 delta = cue - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude >= min * min && CanReach(cue)) { aim = cue; return true; }
            }
            return false;
        }

        private void AimMount(Vector3 dir, float dt)
        {
            if (mount == null || dir.sqrMagnitude < 1e-4f) return;
            var want = Quaternion.LookRotation(dir, Vector3.up);
            mount.rotation = Quaternion.RotateTowards(mount.rotation, want, mountTurnRate * dt);
        }

        private Transform NextTube()
        {
            if (launchPoints == null || launchPoints.Length == 0) return mount != null ? mount : transform;
            var t = launchPoints[_nextTube % launchPoints.Length];
            _nextTube++;
            return t != null ? t : transform;
        }
    }
}
