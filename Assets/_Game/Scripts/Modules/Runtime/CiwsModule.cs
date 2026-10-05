using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 근접방어 무기(0~20). 적 함선이 아니라 가까이 도달한 미사일과 항공기를 처리한다.
    /// "강력한 최종 방어 수단이지만 지속적으로 모든 것을 처리할 수는 없다."
    ///
    /// 자체 추적 레이더로 반경 안의 위협을 직접 찾는다 — 함선 레이더의 탐지·추적 수와 상관없이 반응한다.
    /// 우선순위: 접근 미사일(충돌 예상 시간 짧은 순) → 항공기·드론(가까운 순).
    ///
    /// 교전 절차(한 문 = 교전 채널 하나)
    ///   표적 포착 → 추적(acquireTime) → 선회·조준 → 점사 → 격추 확인(killAssessTime/burstPause) → 다음 표적
    /// 점사: 한 번 방아쇠를 당기면 최소 minBurstTime 동안은 표적이 먼저 떨어져도 끝까지 쏜다(남은 탄은 허공으로).
    ///   표적이 살아 있으면 maxBurstTime까지 이어 쏘고, 그 뒤에는 잠깐 멈춰 결과를 확인한 뒤 다음 점사.
    ///   탄은 dispersion만큼 흩어져 발마다 맞는 것이 아니다 — 많이 쏴서 몇 발 맞힌다.
    /// 표적을 바꿀 때마다 포착·확인 시간이 들어 여러 발이 동시에 오면 일부가 돌파한다(포화 공격).
    /// 여러 문을 달면 다른 CIWS가 이미 맡은 위협은 피해 나눠 맡는다. 맡을 것이 그것뿐이면 함께 쏜다.
    /// 탄약: 비면 전량 재장전(그동안 사격 불가). 드론에 탄을 다 쓰면 뒤이은 미사일 앞에서 위험해진다(의도).
    /// </summary>
    public class CiwsModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private GameObject projectilePrefab;

        [Header("Audio")]
        [Tooltip("연사음 클립에서 사격이 끝나고 총열 감속음이 시작되는 시점")]
        [SerializeField] private float burstTailTime = 1.15f;
        [Tooltip("이 시간 동안 쏘지 않으면 연사음을 감속음으로 넘긴다")]
        [SerializeField] private float burstStopDelay = 0.2f;

        [Header("Visual")]
        [Tooltip("고각부 아래의 6총열 회전축. 포구(Muzzle)는 이 축 밖에 둔다")]
        [SerializeField] private Transform barrelCluster;
        [SerializeField, Min(0.01f)] private float barrelSpinUpTime = 0.12f;
        [Tooltip("4500발/분 ÷ 6총열 = 약 750회전/분")]
        [SerializeField, Min(100f)] private float barrelRpm = 750f;
        [Tooltip("총열 연기. 초당 12발이라 몇 발에 한 번만 뿜는다")]
        [SerializeField] private GameObject muzzleSmoke;
        [SerializeField, Min(1)] private int smokeEveryShots = 4;

        [Header("Ammo")]
        [Tooltip("이 시간 동안 교전이 없고 탄이 아래 비율 이하로 줄어 있으면 미리 재장전한다(그동안은 쏘지 못함)")]
        [SerializeField] private float idleReloadDelay = 3f;
        [Tooltip("교전 사이 미리 재장전하는 탄약 비율. 조금 쓴 뒤 곧바로 8초간 무방비가 되지 않게 절반 이하에서만.")]
        [SerializeField, Range(0f, 1f)] private float idleReloadBelow = 0.5f;

        [Header("Targeting")]
        [Tooltip("지금 쏘는 표적보다 충돌 예상 시간이 이만큼 이상 짧은 위협이 나타나야 표적을 바꾼다(포탑이 떨지 않게)")]
        [SerializeField] private float switchMarginSeconds = 0.5f;

        [Header("Engagement Cycle")]
        [Tooltip("동시에 교전할 수 있는 표적 수(교전 채널). 포탑 하나는 1. 고급형 확장용.")]
        [SerializeField, Min(1)] private int maxSimultaneousTargets = 1;
        [Tooltip("대기 상태에서 새 표적을 잡고 추적이 안정될 때까지 쏘지 못하는 시간")]
        [SerializeField] private float acquireTime = 0.2f;
        [Tooltip("격추 직후 탐색 레이더가 이미 따라가던 다음 표적으로 넘길 때의 포착 시간")]
        [SerializeField] private float handoffTime = 0.08f;
        [Tooltip("표적을 격추한 뒤 결과를 확인하고 다음 표적으로 넘어가는 시간")]
        [SerializeField] private float killAssessTime = 0.1f;

        [Header("Burst")]
        [Tooltip("한 번 방아쇠를 당기면 최소 이만큼 쏜다. 표적이 먼저 떨어져도 남은 시간은 계속 쏜다.")]
        [SerializeField] private float minBurstTime = 0.3f;
        [Tooltip("표적이 살아 있어도 이만큼 쏘면 멈추고 결과를 확인한다")]
        [SerializeField] private float maxBurstTime = 1.0f;
        [Tooltip("점사가 끝난 뒤 다시 쏘기까지(격추 확인·재조준)")]
        [SerializeField] private float burstPause = 0.1f;
        [Tooltip("탄이 흩어지는 원뿔의 반각(도). 발마다 명중하지 않게.")]
        [SerializeField, Range(0f, 10f)] private float dispersion = 5f;
        [Tooltip("한 프레임에 최대 몇 발까지 몰아 쏠지(프레임이 떨어져도 연사속도 유지)")]
        [SerializeField, Min(1)] private int maxShotsPerFrame = 3;

        /// <summary>CIWS들이 맡고 있는 표적과 맡은 문 수. 여러 문이 같은 미사일에 몰리지 않게 한다.</summary>
        private static readonly Dictionary<ITargetable, int> s_engaged = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_engaged.Clear();

        private int _shotCount;
        private MuzzleBurstVfx _muzzleVfx;
        private float _barrelSpeedRpm;
        private float _spinReadyAt;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => _inBurst || _current != null || Time.time - _lastShotTime < 0.6f;

        /// <summary>개발용: 마지막으로 쏘지 못한 이유(전투 기록·검증).</summary>
        public string LastBlockReason { get; private set; } = "";
        public int MaxSimultaneousTargets => maxSimultaneousTargets;
        public int ShotsFired => _shotCount;
        public bool HasTarget => _current != null;
        public bool InBurst => _inBurst;
        /// <summary>개발용: 끝난 점사 수와 점사당 평균 탄약 소모.</summary>
        public int BurstCount { get; private set; }
        public float AverageBurstAmmo => BurstCount > 0 ? (float)_burstAmmoTotal / BurstCount : 0f;

        private ITargetable _current;
        private readonly HashSet<ITargetable> _loggedWaiting = new();
        private string _loggedReason = "";
        private float _readyAt;          // 이 시각 이전에는 쏘지 못한다(포착·격추 확인)
        private float _lastLossTime = -999f;
        private TargetingSystem _targeting;
        private SfxHandle _burst;
        private float _sinceShot = float.MaxValue;

        // 점사 상태
        private bool _inBurst;
        private float _burstStart;
        private float _shotDebt;          // 이번 프레임까지 쏴야 할 발 수(프레임 속도와 무관한 연사)
        private int _burstAmmo;           // 이번 점사에서 쓴 탄약
        private int _burstAmmoTotal;
        private bool _burstHasFired;
        private bool _burstTargetDown;    // 점사 도중 표적이 떨어졌다(남은 탄은 허공으로)
        private float _burstDownAt;

        protected override void OnInitialized()
        {
            // 기존 프리팹의 FBX 하위 오브젝트 ID가 달라져도 새 소켓 이름으로 복구한다.
            Transform turret = null, elevation = null, muzzle = null;
            foreach (var part in GetComponentsInChildren<Transform>(true))
            {
                switch (part.name)
                {
                    case "TurretPivot": turret = part; break;
                    case "ElevationPivot": elevation = part; break;
                    case "Muzzle": muzzle = part; break;
                    case "BarrelCluster": barrelCluster = part; break;
                }
            }
            if (turret != null && elevation != null && muzzle != null)
                weapon.BindArtTransforms(turret, elevation, muzzle);
            else Debug.LogError("[CIWS] 새 모델의 조준 피벗/포구를 찾지 못했습니다.", this);

            _targeting = GetComponentInParent<TargetingSystem>();
            if (_targeting == null) Debug.LogError("[CIWS] TargetingSystem을 찾지 못했습니다.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
            OnShipLayoutChanged();
        }

        // 기반 클래스가 OnEnable/OnDisable을 쓰므로 여기서 정의하지 않는다(유니티는 가장 파생된 것만 부른다).
        // 철거·비활성으로 남은 배정은 표적이 사라지면 PruneEngaged가 지운다.
        public override void OnModuleDestroyed()
        {
            SetCurrent(null);
            _inBurst = false;
            _barrelSpeedRpm = 0f;
        }

        /// <summary>함교·레이더·헬기데크 방향은 사격 금지. 머리 위로 넘어오는 높은 미사일은 쏠 수 있다.</summary>
        protected override void OnShipLayoutChanged()
        {
            if (Grid == null || Instance == null) return;
            weapon.SetFireArc(FireArcCalculator.ComputeCiws(Grid, Instance.Origin, Instance.RotationSteps));
        }

        protected override void Tick(float dt)
        {
            weapon.Tick(dt);
            _ammo.Tick(dt);
            UpdateBarrelSpin(dt);
            UpdateBurstAudio(dt);
            LogReason();   // 직전 프레임에 쏘지 못한 이유가 바뀌었으면 기록
            if (_targeting == null) return;

            // 쏘던 표적이 사라졌으면(격추·명중·기만) 결과 확인 시간을 둔다. 점사 중이면 점사가 끝난 뒤에.
            if (_current != null && (!_current.IsAlive || _current.Transform == null))
            {
                SetCurrent(null);
                _lastLossTime = Time.time;
                if (_inBurst) { _burstTargetDown = true; _burstDownAt = Time.time - _burstStart; }
                else _readyAt = Mathf.Max(_readyAt, Time.time + killAssessTime *
                    (CombatPolicies.Doctrine == NavalDoctrine.AirDefense ? 0.7f : 1f));
            }

            // 점사 중에는 표적을 바꾸지 않는다(방아쇠를 당긴 채)
            if (_inBurst)
            {
                TickBurst(dt);
                return;
            }

            var threat = PickThreat();
            if (threat != _current)
            {
                // 새 표적: 추적이 안정될 때까지 기다린다
                SetCurrent(threat);
                if (threat != null) CombatLog.Add("CIWS", $"{LogName} {CombatLog.Describe(threat)} 포착");
                bool cued = Time.time - _lastLossTime < 1f;   // 직전 표적을 처리하며 다음 표적도 추적하고 있었다
                if (threat != null) _readyAt = Mathf.Max(_readyAt, Time.time + (cued ? handoffTime : acquireTime) *
                    (CombatPolicies.Doctrine == NavalDoctrine.AirDefense ? 0.7f : 1f));
            }

            if (threat != null) LogWaitingThreats(threat);

            if (threat == null)
            {
                _loggedWaiting.Clear();
                if (Time.time - _lastShotTime > idleReloadDelay && _ammo.Current <= _ammo.Capacity * idleReloadBelow)
                    _ammo.BeginReload();
                return;
            }

            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;

            // 탄이 날아가는 동안 미사일도 움직인다. 현재 위치를 쏘면 거의 다 뒤로 빗나간다.
            Vector3 p = threat.Transform.position;
            if (threat is IHasVelocity m)
                p = Ballistics.PredictIntercept(muzzle.position, p, m.Velocity, Stats.ProjectileSpeed);

            weapon.AimAt(p, Stats.TurretTurnRate, dt);

            if (!weapon.IsInFireArc(p)) { LastBlockReason = "사격 금지 구역"; return; }
            if (Time.time < _readyAt) { LastBlockReason = "표적 포착·확인 중"; return; }
            if (!weapon.IsAimedAt(p)) { LastBlockReason = "선회 중"; return; }
            if (!_ammo.CanFire) { LastBlockReason = _ammo.IsReloading ? "재장전 중" : "탄약 없음"; return; }

            // 방아쇠: 점사 시작
            _inBurst = true;
            _burstStart = Time.time;
            _burstAmmo = 0;
            _burstTargetDown = false;
            _burstHasFired = false;
            _spinReadyAt = Time.time + barrelSpinUpTime;
            _shotDebt = 0f;
            LastBlockReason = "";
        }

        /// <summary>
        /// 점사 진행: 표적이 살아 있으면 따라가며 쏘고, 떨어졌으면 최소 점사 시간까지 포신 방향으로 계속 쏜다.
        /// 사격 금지 구역에 들어가거나 탄이 떨어지면 즉시 끊는다(안전 차단).
        /// </summary>
        private void TickBurst(float dt)
        {
            float elapsed = Time.time - _burstStart;
            var muzzle = weapon.Muzzle != null ? weapon.Muzzle : transform;
            float damage = Stats.Damage;

            if (_current != null)
            {
                if (elapsed >= maxBurstTime) { EndBurst("최대 점사"); return; }

                Vector3 p = _current.Transform.position;
                if (_current is IHasVelocity m)
                    p = Ballistics.PredictIntercept(muzzle.position, p, m.Velocity, Stats.ProjectileSpeed);
                weapon.AimAt(p, Stats.TurretTurnRate, dt);
                if (!weapon.IsInFireArc(p)) { LastBlockReason = "사격 금지 구역"; EndBurst("사격 금지 구역"); return; }
                damage = DamageAgainst(_current);
                LogWaitingThreats(_current);
            }
            else if (elapsed >= minBurstTime)
            {
                EndBurst(_burstTargetDown ? "표적 소멸" : "표적 없음");
                return;
            }

            if (!_ammo.CanFire)
            {
                LastBlockReason = _ammo.IsReloading ? "재장전 중" : "탄약 없음";
                EndBurst(LastBlockReason);
                return;
            }

            // 실제 개틀링처럼 총열이 먼저 가속되고 나서 점사를 시작한다.
            if (Time.time < _spinReadyAt) { LastBlockReason = "총열 가속 중"; return; }
            if (!_burstHasFired) { _shotDebt = 1f; _burstHasFired = true; } // 첫 발은 가속 완료 즉시
            _shotDebt += dt / Mathf.Max(0.01f, Stats.ReloadTime);
            FireDue(muzzle, damage);
        }

        private void UpdateBarrelSpin(float dt)
        {
            float targetRpm = _inBurst ? barrelRpm : 0f;
            float changeRate = barrelRpm / Mathf.Max(0.01f, _inBurst ? barrelSpinUpTime : 0.35f);
            _barrelSpeedRpm = Mathf.MoveTowards(_barrelSpeedRpm, targetRpm, changeRate * dt);
            if (barrelCluster != null && _barrelSpeedRpm > 0f)
                barrelCluster.Rotate(weapon.FireDirection, _barrelSpeedRpm * 6f * dt, Space.World);
        }

        public override void BindUpgradeVisual(Transform visualRoot)
        {
            var turret = FindVisualPart(visualRoot, "TurretPivot");
            var elevation = FindVisualPart(visualRoot, "ElevationPivot");
            var muzzle = FindVisualPart(visualRoot, "Muzzle");
            barrelCluster = FindVisualPart(visualRoot, "BarrelCluster");
            if (turret != null && elevation != null && muzzle != null && barrelCluster != null)
                weapon.BindArtTransforms(turret, elevation, muzzle);
            else
                Debug.LogError("[CIWS] 업그레이드 모델의 조준 피벗/포구/총열 묶음을 찾지 못했습니다.", this);
        }

        /// <summary>밀린 발 수만큼 쏜다(한 프레임 최대 maxShotsPerFrame).</summary>
        private void FireDue(Transform muzzle, float damage)
        {
            int shots = 0;
            while (_shotDebt >= 1f && shots < maxShotsPerFrame && _ammo.CanFire)
            {
                _shotDebt -= 1f;
                shots++;
                FireRound(muzzle, damage);
            }
            if (shots >= maxShotsPerFrame) _shotDebt = Mathf.Min(_shotDebt, 1f);   // 멈춘 프레임 뒤에 몰아 쏘지 않게
        }

        private void FireRound(Transform muzzle, float damage)
        {
            int before = _ammo.Current;
            _ammo.Consume();
            _burstAmmo += Mathf.Max(0, before - _ammo.Current);
            _lastShotTime = Time.time;

            // 포신 방향 + 흩어짐(원뿔 안에 고르게)
            Vector3 dir = Disperse(weapon.FireDirection, dispersion);
            var go = PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir));
            if (go == null) return;

            go.GetComponent<Projectile>()?.Launch(dir, Stats.ProjectileSpeed, damage, DamageSource.Gun);

            if (_muzzleVfx == null) _muzzleVfx = gameObject.AddComponent<MuzzleBurstVfx>();
            _muzzleVfx.Emit(muzzle.position, weapon.FireDirection, MuzzleBurstVfx.Style.Ciws,
                muzzleSmoke, ++_shotCount % smokeEveryShots == 0);

            // 이미 연사음이 울리는 중이면 새로 틀지 않는다. 끝나갈 때만 이어서 다시 튼다.
            _sinceShot = 0f;
            if (!_burst.IsPlaying || _burst.Time >= burstTailTime - 0.05f)
                _burst = AudioManager.Play(Game.Data.SfxId.CiwsBurst, muzzle.position);
        }

        private void EndBurst(string reason)
        {
            _inBurst = false;
            _shotDebt = 0f;
            BurstCount++;
            _burstAmmoTotal += _burstAmmo;
            _readyAt = Mathf.Max(_readyAt, Time.time + burstPause);   // 점사 끝부분이 격추 확인을 겸한다
            CombatLog.Add("CIWS", $"{LogName} 점사 종료({reason}) {Time.time - _burstStart:0.00}초 · {_burstAmmo}발" + (_burstTargetDown ? $" (표적 소멸 {_burstDownAt:0.00}초, 최소 {minBurstTime:0.00}초)" : ""));
        }

        private static Vector3 Disperse(Vector3 dir, float halfAngle)
        {
            if (halfAngle <= 0f) return dir;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(dir, side);
            Vector2 o = Random.insideUnitCircle * halfAngle;
            return Quaternion.AngleAxis(o.x, up) * Quaternion.AngleAxis(o.y, side) * dir;
        }

        /// <summary>
        /// 반경 안에 들어왔는데 아무 CIWS도 맡지 못한 미사일을 한 번씩 기록한다("채널 사용 중").
        /// 포화 공격에서 어느 미사일이 왜 방치됐는지 보여 준다.
        /// </summary>
        private void LogWaitingThreats(ITargetable engaged)
        {
            float maxSqr = Stats.Range * Stats.Range;
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < missiles.Count; i++)
            {
                var t = missiles[i];
                if (t == null || t == engaged || !t.IsAlive || t.Transform == null) continue;
                if ((t.Transform.position - transform.position).sqrMagnitude > maxSqr) continue;
                if (s_engaged.ContainsKey(t) || !_loggedWaiting.Add(t)) continue;
                string why = weapon.IsInFireArc(t.Transform.position) ? $"채널 사용 중({CombatLog.Describe(engaged)} 교전)" : "사격 금지 구역";
                CombatLog.Add("CIWS", $"{LogName} {CombatLog.Describe(t)} 교전 불가: {why}");
            }
        }

        /// <summary>쏘지 못하는 이유가 바뀔 때만 기록(재장전·탄약 없음·사격 금지 구역).</summary>
        private void LogReason()
        {
            string reason = LastBlockReason;
            if (reason == _loggedReason) return;
            _loggedReason = reason;
            if (reason == "재장전 중" || reason == "탄약 없음" || reason == "사격 금지 구역")
                CombatLog.Add("CIWS", $"{LogName} {CombatLog.Describe(_current)} 사격 불가: {reason}");
        }

        /// <summary>교전 채널: 맡은 표적을 바꾸고 공유 목록을 갱신한다.</summary>
        private void SetCurrent(ITargetable target)
        {
            if (_current == target) return;
            if (_current != null && s_engaged.TryGetValue(_current, out int n))
            {
                if (n <= 1) s_engaged.Remove(_current);
                else s_engaged[_current] = n - 1;
            }
            _current = target;
            if (target != null) s_engaged[target] = s_engaged.TryGetValue(target, out int c) ? c + 1 : 1;
        }

        /// <summary>다른 CIWS가 맡고 있는 표적인가(내가 맡은 것은 제외).</summary>
        private bool EngagedByOther(ITargetable t)
        {
            if (!s_engaged.TryGetValue(t, out int n)) return false;
            return t == _current ? n > 1 : n > 0;
        }

        /// <summary>사라진 표적이 목록에 남지 않게 가끔 정리한다.</summary>
        private static void PruneEngaged()
        {
            if (s_engaged.Count == 0 || Time.frameCount % 30 != 0) return;
            List<ITargetable> dead = null;
            foreach (var kv in s_engaged)
                if (kv.Key == null || !kv.Key.IsAlive) (dead ??= new List<ITargetable>()).Add(kv.Key);
            if (dead != null) foreach (var d in dead) s_engaged.Remove(d);
        }

        /// <summary>사격이 멎으면 연사음의 남은 부분을 건너뛰고 감속음으로 끝낸다.</summary>
        private void UpdateBurstAudio(float dt)
        {
            _sinceShot += dt;
            if (_sinceShot < burstStopDelay) return;
            if (_burst.IsPlaying && _burst.Time < burstTailTime) _burst.SkipTo(burstTailTime);
        }

        /// <summary>
        /// 자체 센서: 반경 안의 적 미사일 중 충돌 예상 시간이 가장 짧은 것, 없으면 가장 가까운 항공기.
        /// 다른 CIWS가 맡은 위협은 다른 선택지가 없을 때만 고른다. 사격 금지 구역(함교 방향 등)에 있는 위협은 건너뛴다.
        /// </summary>
        private ITargetable PickThreat()
        {
            PruneEngaged();
            var best = PickMissile(allowShared: false) ?? PickMissile(allowShared: true);
            if (best != null) return best;
            return PickAircraft(allowShared: false) ?? PickAircraft(allowShared: true);
        }

        private ITargetable PickMissile(bool allowShared)
        {
            Vector3 self = transform.position;
            Vector3 ship = Ship != null ? Ship.transform.position : self;
            float maxSqr = Stats.Range * Stats.Range;

            ITargetable best = null;
            float bestTti = float.MaxValue;
            float currentTti = float.MaxValue;
            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = 0; i < missiles.Count; i++)
            {
                var t = missiles[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;
                if ((t.Transform.position - self).sqrMagnitude > maxSqr) continue;
                if (!allowShared && EngagedByOther(t)) continue;

                float tti = TargetingSystem.TimeToImpact(t, ship);
                if (tti >= bestTti && t != _current) continue;
                if (!weapon.IsInFireArc(t.Transform.position)) continue;
                if (t == _current) currentTti = tti;
                if (tti < bestTti) { best = t; bestTti = tti; }
            }

            // 지금 쏘던 미사일이 여전히 유효하면, 훨씬 급한 위협이 아닌 한 계속 쏜다
            if (currentTti < float.MaxValue && bestTti > currentTti - switchMarginSeconds) best = _current;
            return best;
        }

        private ITargetable PickAircraft(bool allowShared)
        {
            Vector3 self = transform.position;
            float bestScore = float.MaxValue;
            float maxSqr = Stats.Range * Stats.Range;
            ITargetable best = null;

            var aircraft = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Aircraft);
            for (int i = 0; i < aircraft.Count; i++)
            {
                var t = aircraft[i];
                if (t == null || !t.IsAlive || t.Transform == null) continue;
                float d = (t.Transform.position - self).sqrMagnitude;
                if (d > maxSqr || !weapon.IsInFireArc(t.Transform.position)) continue;
                if (!allowShared && EngagedByOther(t)) continue;

                float score = TargetInfo.Weighted(d, EfficiencyAgainst(t));
                if (t == _current) score *= 0.5f;   // 쏘던 기체를 끝까지
                if (score >= bestScore) continue;
                best = t;
                bestScore = score;
            }
            return best;
        }
    }
}
