using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 선택한 탄종에 따라 대함·대공·대잠을 맡는 VLS(16~48). 한 발사기의 셀을 공유한다.
    /// 수직으로 솟은 뒤 표적 쪽으로 꺾어 팝업 궤적으로 내리꽂는다. 한 발 피해가 크고 재장전이 길다.
    /// 이미 날아가는 미사일로 충분히 잡히는 표적은 건너뛴다(TargetAllocator) — 미사일정 한 척에 여러 발을 낭비하지 않는다.
    /// 대함·대공은 레이더, 대잠은 소나·헬기 접촉의 영향을 받는다.
    /// </summary>
    public class VlsModule : ModuleRuntime, IAmmoUser
    {
        public enum PayloadMode { Surface, Air, AntiSubmarine }
        [Header("Selected payload")]
        [SerializeField] private PayloadMode payloadMode = PayloadMode.Surface;
        [SerializeField] private GameObject interceptorPrefab;
        [SerializeField] private GameObject aswTorpedoPrefab;
        [SerializeField, Min(0f)] private float switchSeconds = 2f;
        private float _switchRemaining;
        public PayloadMode Mode => payloadMode;
        public float SwitchRemaining => _switchRemaining;

        public bool SelectMode(PayloadMode mode)
        {
            if (mode == payloadMode) return true;
            if (mode == PayloadMode.Air && interceptorPrefab == null) return false;
            if (mode == PayloadMode.AntiSubmarine && aswTorpedoPrefab == null) return false;
            payloadMode = mode;
            _switchRemaining = switchSeconds;
            return true;
        }

        /// <summary>VLS가 쓸 표적 규칙. 나중에 플레이어 선택지로 열 수 있게 데이터로 둔다.</summary>
        public enum TargetRule
        {
            Standard,       // 가치 minTargetValue 이상. 셀이 lowCellFraction 이하면 lowCellMinValue 이상만
            HighValueOnly,  // 항상 lowCellMinValue 이상만
            AnyTarget,      // 가치와 상관없이(예전 동작)
        }

        [Header("Target Economy")]
        [SerializeField] private TargetRule targetRule = TargetRule.Standard;
        [Tooltip("이 가치 미만(고속정·드론 1)에는 쏘지 않는다")]
        [SerializeField, Min(0)] private int minTargetValue = 3;
        [Tooltip("남은 셀이 이 비율 이하이면 대형·보스만")]
        [SerializeField, Range(0f, 1f)] private float lowCellFraction = 0.25f;
        [SerializeField, Min(0)] private int lowCellMinValue = 5;

        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject missilePrefab;
        [SerializeField] private Transform[] hatches;
        // 수직 상승 후 유도로 전환하는 연출은 Missile 쪽 비행 단계에서 구현한다.

        private TargetingSystem _targeting;
        private System.Predicate<Vector3> _beyondMinRange;
        private System.Func<ITargetable, float, float> _toughestUncovered;
        private int _nextHatch;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[VLS] TargetingSystem을 찾지 못했습니다.", this);
            if (missilePrefab == null) Debug.LogError("[VLS] missilePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;

            _beyondMinRange = p =>
            {
                Vector3 d = p - transform.position;
                d.y = 0f;
                return d.sqrMagnitude >= Stats.MinRange * Stats.MinRange;
            };
            _toughestUncovered = (t, sqr) =>
            {
                if (TargetAllocator.IsCovered(t)) return float.MaxValue;
                int value = TargetInfo.Value(t);
                if (value < RequiredValue()) return float.MaxValue;
                if (EfficiencyAgainst(t) <= 0.001f) return float.MaxValue;
                float hp = t is Game.Enemies.EnemyController e && e.Definition != null ? e.Definition.MaxHp : 0f;
                return -(value * 100000f + hp * 1000f) + sqr;   // 가치 → 체력 → 거리 순
            };
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            if (_switchRemaining > 0f) { _switchRemaining -= dt; return; }
            if (_targeting == null || missilePrefab == null) return;
            if (!_ammo.CanFire) return;   // 빈 셀: 장전될 때까지 표적도 고르지 않는다

            // 장거리 대물 무기: 대역 안에서 가장 튼튼한 적을 먼저 노린다.
            // 탐지 밖은 사거리와 무관하게 조준 불가 — 레이더가 있어야 멀리 쏠 수 있다.
            ITargetable target;
            switch (payloadMode)
            {
                case PayloadMode.Air:
                    target = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Air | TargetClass.Missile,
                        (t, sqr) => TargetAllocator.IsCovered(t) ? float.MaxValue :
                            TargetingSystem.TimeToImpact(t, Ship != null ? Ship.transform.position : transform.position), _beyondMinRange);
                    break;
                case PayloadMode.AntiSubmarine:
                    target = FindSubmarine();
                    break;
                default:
                    target = _targeting.GetBest(transform.position, Stats.Range, TargetClass.Surface,
                                                _toughestUncovered, _beyondMinRange);
                    break;
            }
            if (target == null) return;
            if (!weapon.TryFire(Stats.ReloadTime)) return;

            _ammo.Consume();
            _lastShotTime = Time.time;
            Launch(target);
        }

        /// <summary>지금 쏠 수 있는 최소 표적 가치.</summary>
        private int RequiredValue()
        {
            switch (targetRule)
            {
                case TargetRule.AnyTarget: return 0;
                case TargetRule.HighValueOnly: return lowCellMinValue;
            }
            bool lowCells = !_ammo.Infinite && _ammo.Current <= Mathf.CeilToInt(_ammo.Capacity * lowCellFraction);
            return lowCells ? lowCellMinValue : minTargetValue;
        }

        private void Launch(ITargetable target)
        {
            var hatch = NextHatch();
            var prefab = payloadMode == PayloadMode.Air ? interceptorPrefab : missilePrefab;
            var go = PoolManager.Instance?.Spawn(prefab, hatch.position, Quaternion.LookRotation(Vector3.up));
            if (go == null) return;

            var missile = go.GetComponent<Missile>();
            if (payloadMode == PayloadMode.AntiSubmarine && target is Game.Enemies.SubmarineBase sub)
                missile?.LaunchAsw(sub.LastKnownPosition + sub.LastKnownVelocity * 1.5f,
                                   aswTorpedoPrefab, Stats.Damage * 0.8f);
            else
                missile?.Launch(target.Transform, payloadMode == PayloadMode.Air ? Stats.Damage * 0.45f : DamageAgainst(target));
            // TODO: Hatch Open 애니메이션 + 점화 VFX/SFX
        }

        private ITargetable FindSubmarine()
        {
            ITargetable best = null;
            float bestSqr = Stats.Range * Stats.Range;
            foreach (var t in _targeting.DetectedSubmarines)
            {
                if (t is not Game.Enemies.SubmarineBase sub || !sub.IsAlive || !sub.IsContactConfirmed) continue;
                Vector3 delta = sub.LastKnownPosition - transform.position;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d >= bestSqr || !_beyondMinRange(sub.LastKnownPosition)) continue;
                best = sub;
                bestSqr = d;
            }
            return best;
        }

        private Transform NextHatch()
        {
            if (hatches == null || hatches.Length == 0) return transform;
            var t = hatches[_nextHatch % hatches.Length];
            _nextHatch++;
            return t != null ? t : transform;
        }
    }
}
