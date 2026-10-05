using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 함대공 미사일 발사기. 탐지 범위 안의 적 미사일과 항공기(자폭 드론·정찰기)를 멀리서(교전거리 대역) 요격미사일로 격추한다.
    ///
    /// 방어 1단계다: 함대공(8~42, 멀리) → 기만체·재밍(중간) → CIWS(14, 가까이).
    /// 함선에 닿기까지 남은 예상 시간(충돌 예상 시간)이 가장 짧은 위협부터 쏜다 — 가까워지지 않는 표적(선회하는 정찰기)은 뒤로.
    /// 같은 표적에 여러 발이 몰리지 않도록 날아가는 요격미사일의 예상 피해를 배정한다(TargetAllocator).
    ///
    /// VLS(수직 셀, 대함)와 한눈에 구별되도록 선회식 박스 발사기다: 위협 쪽으로 돌아선 뒤 비스듬히 쏜다.
    /// 선회 받침이 없는 모델이면 수직으로 쏜다.
    /// </summary>
    public class SamLauncherModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject interceptorPrefab;
        [SerializeField] private Transform[] launchPoints;


        [Header("Train (선택)")]
        [Tooltip("수평으로 도는 받침. 비어 있으면 돌지 않고 수직으로 쏜다.")]
        [SerializeField] private Transform trainPivot;
        [SerializeField] private float trainRateDegPerSec = 270f;
        [Tooltip("이 각도 안으로 돌아서면 발사한다")]
        [SerializeField] private float fireToleranceDeg = 15f;
        [Tooltip("발사점의 +Z 방향으로 내보낸다(그레이박스처럼 축을 믿을 수 있을 때만). 끄면 수직 발사.")]
        [SerializeField] private bool launchAlongPoint;


        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;

        private TargetingSystem _targeting;
        private float _cooldown;
        private int _nextTube;

        // 받침은 모듈 기준 수평 각도만 바꾼다. 모델 노드의 원래 회전(Blender 축)은 그대로 곱해 둔다.
        private Quaternion _trainBase;
        private float _trainYaw;
        private bool _trainBaseReady;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[SAM] TargetingSystem을 찾지 못했습니다.", this);
            if (interceptorPrefab == null) Debug.LogError("[SAM] interceptorPrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
        }

        protected override void Tick(float dt)
        {
            if (_cooldown > 0f) _cooldown -= dt;
            _ammo.Tick(dt);
            if (_targeting == null || interceptorPrefab == null) return;
            if (!_ammo.CanFire) return;

            var threat = PickThreat();
            if (threat == null) return;
            if (!TrainTowards(threat.Transform.position, dt)) return;
            if (_cooldown > 0f) return;

            var tube = NextTube();
            var rotation = launchAlongPoint && tube != transform ? tube.rotation : Quaternion.LookRotation(Vector3.up);
            var go = PoolManager.Instance?.Spawn(interceptorPrefab, tube.position, rotation);
            if (go == null) return;

            go.GetComponent<Missile>()?.Launch(threat.Transform, DamageAgainst(threat));   // 발사하면서 표적에 배정된다
            _ammo.Consume();
            _lastShotTime = Time.time;
            _cooldown = Mathf.Max(0.2f, Stats.ReloadTime *
                (CombatPolicies.Doctrine == NavalDoctrine.AirDefense ? 0.85f : 1f));
        }

        /// <summary>받침을 위협 쪽으로 돌린다(모듈 수직축). 충분히 돌아섰으면 true. 받침이 없으면 항상 true.</summary>
        private bool TrainTowards(Vector3 point, float dt)
        {
            if (trainPivot == null) return true;
            if (!_trainBaseReady)
            {
                _trainBase = Quaternion.Inverse(transform.rotation) * trainPivot.rotation;
                _trainBaseReady = true;
            }

            Vector3 local = transform.InverseTransformDirection(point - trainPivot.position);
            if (local.x * local.x + local.z * local.z < 0.01f) return true;

            float want = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            _trainYaw = Mathf.MoveTowardsAngle(_trainYaw, want, trainRateDegPerSec * dt);
            trainPivot.rotation = transform.rotation * Quaternion.Euler(0f, _trainYaw, 0f) * _trainBase;
            return Mathf.Abs(Mathf.DeltaAngle(_trainYaw, want)) <= fireToleranceDeg;
        }

        /// <summary>교전거리 대역 안에서, 아직 충분히 배정되지 않은 위협 중 충돌 예상 시간이 가장 짧은 것.</summary>
        private ITargetable PickThreat()
        {
            ITargetable best = null;
            float bestTti = float.MaxValue;
            Vector3 ship = Ship != null ? Ship.transform.position : transform.position;

            bool layered = ModuleSynergy.IntegratedAirDefense(Grid, Instance);
            if (layered) Consider(_targeting.IncomingMissiles, ship, ref best, ref bestTti, 16f);
            if (best == null) Consider(_targeting.IncomingMissiles, ship, ref best, ref bestTti, 0f);
            Consider(_targeting.DetectedAircraft, ship, ref best, ref bestTti, 0f);
            return best;
        }

        private void Consider(IReadOnlyList<ITargetable> list, Vector3 ship, ref ITargetable best, ref float bestTti, float preferredMinRange)
        {
            float minRange = Stats.MinRange * (CombatPolicies.Doctrine == NavalDoctrine.AirDefense ? 0.8f : 1f);
            float min = minRange * minRange, max = Stats.Range * Stats.Range;

            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;
                if (t is Missile m && m.IsDecoyed) continue;   // 기만체에 속은 미사일은 이미 빗나간다

                float d = (t.Transform.position - transform.position).sqrMagnitude;
                if (d < min || d > max) continue;

                float tti = TargetingSystem.TimeToImpact(t, ship);
                if (d < preferredMinRange * preferredMinRange && tti > 2f) continue;
                if (tti >= bestTti || TargetAllocator.IsCovered(t)) continue;
                if (EfficiencyAgainst(t) <= 0.001f) continue;

                best = t;
                bestTti = tti;
            }
        }

        private Transform NextTube()
        {
            if (launchPoints == null || launchPoints.Length == 0) return transform;
            var t = launchPoints[_nextTube % launchPoints.Length];
            _nextTube++;
            return t != null ? t : transform;
        }
    }
}
