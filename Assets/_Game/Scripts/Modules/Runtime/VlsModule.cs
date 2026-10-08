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

        [Header("Hatch (발사 연출, 2026-10-08)")]
        [Tooltip("셀 덮개. hatches(발사점)와 같은 순서. 비어 있으면 열림 연출 없이 바로 쏜다(그레이박스).")]
        [SerializeField] private Transform[] hatchLids;
        [Tooltip("덮개 위 경고 띠(덮개와 함께 열린다). 없어도 된다.")]
        [SerializeField] private Transform[] hatchStripes;
        [Tooltip("덮개마다 경첩 위치·축(모듈 기준). 프리팹 빌더가 모델에서 계산해 넣는다.")]
        [SerializeField] private Vector3[] hingePoints;
        [SerializeField] private Vector3[] hingeAxes;
        [SerializeField, Min(0.01f)] private float hatchOpenSeconds = 0.18f;
        [SerializeField, Min(0f)] private float hatchHoldSeconds = 0.9f;
        [SerializeField, Min(0.01f)] private float hatchCloseSeconds = 0.35f;
        [SerializeField, Range(30f, 150f)] private float hatchOpenAngle = 105f;

        /// <summary>셀 하나의 닫힌 자세(모듈 기준)와 열린 시각.</summary>
        private struct HatchState
        {
            public Vector3 LidPos, StripePos;
            public Quaternion LidRot, StripeRot;
            public float Sign;      // 경첩 축 둘레로 이쪽(+1/−1)으로 돌려야 덮개가 위로 들린다
            public float OpenedAt;  // 열리기 시작한 시각(−1 = 닫혀 쉬는 중)
        }
        private HatchState[] _hatchStates;

        /// <summary>덮개가 다 열리면 쏠 발사 예약.</summary>
        private struct PendingLaunch
        {
            public float At;
            public int Cell;
            public ITargetable Target;
            public PayloadMode Mode;
        }
        private readonly System.Collections.Generic.List<PendingLaunch> _pending = new();

        /// <summary>검증용: 지금 열려 있는(열리는 중 포함) 셀 수와 덮개가 들린 정도(0~1)의 최댓값, 지금까지 쏜 수.</summary>
        public int OpenHatchCount { get; private set; }
        public float MaxHatchOpen { get; private set; }
        public int LaunchCount { get; private set; }
        /// <summary>마지막으로 미사일이 나간 셀(0부터).</summary>
        public int LastCell { get; private set; } = -1;
        public bool HasHatchAnimation => _hatchStates != null;

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
            InitHatches();

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
            UpdatePendingLaunches();
            AnimateHatches();
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

            _lastShotTime = Time.time;
            int cell = NextCell();
            if (_hatchStates == null || cell < 0)
            {
                _ammo.Consume();
                Launch(target, cell, payloadMode);
                return;
            }
            // 덮개를 열고, 다 열리면 그 셀에서 쏜다(탄은 실제로 나갈 때 쓴다)
            OpenHatch(cell);
            _pending.Add(new PendingLaunch { At = Time.time + hatchOpenSeconds, Cell = cell, Target = target, Mode = payloadMode });
        }

        private void UpdatePendingLaunches()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (Time.time < p.At) continue;
                _pending.RemoveAt(i);
                // 덮개가 열리는 사이에 표적이 사라졌으면 쏘지 않는다(덮개는 그대로 닫힌다)
                if (p.Target == null || p.Target.Transform == null || !p.Target.Transform.gameObject.activeInHierarchy) continue;
                if (!_ammo.CanFire) continue;
                _ammo.Consume();
                Launch(p.Target, p.Cell, p.Mode);
            }
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

        private void Launch(ITargetable target, int cell, PayloadMode mode)
        {
            var hatch = cell >= 0 && hatches != null && cell < hatches.Length && hatches[cell] != null ? hatches[cell] : transform;
            var prefab = mode == PayloadMode.Air ? interceptorPrefab : missilePrefab;
            var go = PoolManager.Instance?.Spawn(prefab, hatch.position, Quaternion.LookRotation(Vector3.up));
            if (go == null) return;
            LaunchCount++;
            LastCell = cell;

            var missile = go.GetComponent<Missile>();
            if (mode == PayloadMode.AntiSubmarine && target is Game.Enemies.SubmarineBase sub)
                missile?.LaunchAsw(sub.LastKnownPosition + sub.LastKnownVelocity * 1.5f,
                                   aswTorpedoPrefab, Stats.Damage * 0.8f);
            else
                missile?.Launch(target.Transform, mode == PayloadMode.Air ? Stats.Damage * 0.45f : DamageAgainst(target));
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

        /// <summary>
        /// 다음 셀: 1번부터 차례로(쏜 발 수 = 용량 − 남은 탄). 8발을 다 쓰면 전량 재장전되고 다시 1번부터(2026-10-08).
        /// 덮개가 열리는 사이 발사가 취소되면 탄이 줄지 않으므로 다음에 같은 셀을 다시 쓴다. 셀이 없으면 −1.
        /// </summary>
        private int NextCell()
        {
            if (hatches == null || hatches.Length == 0) return -1;
            int fired = _ammo.Infinite ? _nextHatch++ : _ammo.Capacity - _ammo.Current;
            return ((fired % hatches.Length) + hatches.Length) % hatches.Length;
        }

        // ------------------------------------------------------------ 덮개 열림 연출

        /// <summary>덮개마다 닫힌 자세(모듈 기준)를 기억하고, 경첩 축 둘레로 어느 쪽으로 돌려야 위로 들리는지 정한다.</summary>
        private void InitHatches()
        {
            _hatchStates = null;
            if (hatchLids == null || hatchLids.Length == 0 || hingePoints == null || hingeAxes == null) return;
            int n = Mathf.Min(hatchLids.Length, Mathf.Min(hingePoints.Length, hingeAxes.Length));
            if (n == 0) return;
            var inv = Quaternion.Inverse(transform.rotation);
            _hatchStates = new HatchState[n];
            for (int i = 0; i < n; i++)
            {
                var s = new HatchState { OpenedAt = -1f };
                var lid = hatchLids[i];
                if (lid != null)
                {
                    s.LidPos = transform.InverseTransformPoint(lid.position);
                    s.LidRot = inv * lid.rotation;
                }
                var stripe = hatchStripes != null && i < hatchStripes.Length ? hatchStripes[i] : null;
                if (stripe != null)
                {
                    s.StripePos = transform.InverseTransformPoint(stripe.position);
                    s.StripeRot = inv * stripe.rotation;
                }
                Vector3 arm = s.LidPos - hingePoints[i];
                s.Sign = (Quaternion.AngleAxis(90f, hingeAxes[i]) * arm).y >= 0f ? 1f : -1f;
                _hatchStates[i] = s;
            }
        }

        private void OpenHatch(int cell)
        {
            if (_hatchStates == null || cell < 0 || cell >= _hatchStates.Length) return;
            _hatchStates[cell].OpenedAt = Time.time;
        }

        /// <summary>열림 0.18초(감속) → 0.9초 유지 → 닫힘 0.35초. 닫히면 처음 자세로 정확히 되돌린다.</summary>
        private void AnimateHatches()
        {
            OpenHatchCount = 0;
            MaxHatchOpen = 0f;
            if (_hatchStates == null) return;
            for (int i = 0; i < _hatchStates.Length; i++)
            {
                ref var s = ref _hatchStates[i];
                if (s.OpenedAt < 0f || hatchLids[i] == null) continue;
                float t = Time.time - s.OpenedAt;
                float open;
                if (t < hatchOpenSeconds) { float u = 1f - t / hatchOpenSeconds; open = 1f - u * u; }
                else if (t < hatchOpenSeconds + hatchHoldSeconds) open = 1f;
                else if (t < hatchOpenSeconds + hatchHoldSeconds + hatchCloseSeconds)
                    open = 1f - Mathf.SmoothStep(0f, 1f, (t - hatchOpenSeconds - hatchHoldSeconds) / hatchCloseSeconds);
                else { open = 0f; s.OpenedAt = -1f; }

                var rot = Quaternion.AngleAxis(hatchOpenAngle * open * s.Sign, hingeAxes[i]);
                SetPose(hatchLids[i], hingePoints[i] + rot * (s.LidPos - hingePoints[i]), rot * s.LidRot);
                var stripe = hatchStripes != null && i < hatchStripes.Length ? hatchStripes[i] : null;
                if (stripe != null) SetPose(stripe, hingePoints[i] + rot * (s.StripePos - hingePoints[i]), rot * s.StripeRot);
                if (s.OpenedAt >= 0f) { OpenHatchCount++; MaxHatchOpen = Mathf.Max(MaxHatchOpen, open); }
            }
        }

        private void SetPose(Transform t, Vector3 localPos, Quaternion localRot)
        {
            t.SetPositionAndRotation(transform.TransformPoint(localPos), transform.rotation * localRot);
        }
    }
}
