using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Data;
using Game.Combat;

namespace Game.Ship
{
    /// <summary>
    /// 플레이어 함선의 이동/선회와 Hull HP를 담당한다.
    /// 무기, 모듈, 탐지 로직은 전혀 알지 못한다.
    /// 함체가 자라면 콜라이더를 다시 만든다. 속력과 선회는 모듈 수와 무관하게 ShipConfig 값 그대로다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ShipController : MonoBehaviour, IDamageable, ITargetable
    {
        // --- 표적(아군 진영 수상함). 적이 등록소에서 플레이어 쪽(기함·아군 호위함)을 찾을 수 있게 등록한다.
        //     플레이어 무기·센서·UI는 TargetRegistry.HostileTo(Player, …)만 보므로 자기 함을 노리지 않는다.
        public Transform Transform => transform;
        public TargetKind Kind => TargetKind.Surface;
        public bool IsRevealed => true;
        public CombatFaction Faction => CombatFaction.Player;

        [Header("Config")]
        [SerializeField] private ShipConfig config;

        [Header("Refs")]
        [SerializeField] private ShipGrid grid;
        [SerializeField] private ShipSystems systems;
        [SerializeField] private DamageResolver damageResolver;

        private Rigidbody _rb;
        private BoxCollider _collider;
        private Vector2 _moveInput;
        private float _currentSpeed;

        // 조함 상태: 기관 전령기 단계, 타각(-1 좌 ~ 1 우), 실제 선회율(도/초)
        private int _engineOrder = -1;          // EngineOrders 인덱스. -1이면 아직 정하지 않음(정지로 시작)
        private float _rudder;
        private float _yawRate;
        private bool _gearUpHeld, _gearDownHeld;

        public float HullHp { get; private set; }
        public float HullMaxHp { get; private set; }
        public float CurrentSpeed => _currentSpeed;

        /// <summary>1 m/s = 1.943844노트. 게임 1단위 = 1m.</summary>
        public const float KnotsPerMetersPerSecond = 1.943844f;
        public static float ToKnots(float metersPerSecond) => metersPerSecond * KnotsPerMetersPerSecond;
        public ShipGrid Grid => grid;
        public ShipSystems Systems => systems;
        public bool IsAlive => HullHp > 0f;

        // --- HUD 표시용: 기관 전령기 출력(-1 후진 ~ 1 전진), 타각(-1 좌 ~ 1 우), 기준 최고속력
        public float ThrottleInput => OrderedThrottle;
        public float RudderInput => _rudder;
        /// <summary>현재 타각(도, 음수 = 좌현).</summary>
        public float RudderAngle => _rudder * (config != null ? config.MaxRudderAngle : 35f);
        public float MaxRudderAngle => config != null ? config.MaxRudderAngle : 35f;
        /// <summary>실제 선회율(도/초, 음수 = 좌현).</summary>
        public float YawRate => _yawRate;
        public int EngineOrderIndex => EnsureOrder();

        private float OrderedThrottle
        {
            get
            {
                if (config == null) return 0f;
                var orders = config.EngineOrders;
                return orders[Mathf.Clamp(EnsureOrder(), 0, orders.Length - 1)];
            }
        }

        /// <summary>처음에는 정지(0에 가장 가까운 단계)로 둔다.</summary>
        private int EnsureOrder()
        {
            if (_engineOrder >= 0 || config == null) return Mathf.Max(0, _engineOrder);
            var orders = config.EngineOrders;
            int best = 0;
            for (int i = 1; i < orders.Length; i++)
                if (Mathf.Abs(orders[i]) < Mathf.Abs(orders[best])) best = i;
            _engineOrder = best;
            return best;
        }

        /// <summary>기관 전령기를 한 칸 올리거나(+1) 내린다(-1).</summary>
        public void ShiftEngineOrder(int delta)
        {
            if (config == null) return;
            int max = config.EngineOrders.Length - 1;
            _engineOrder = Mathf.Clamp(EnsureOrder() + delta, 0, max);
        }

        /// <summary>개발·검증용: 전령기 단계를 출력 값으로 맞춘다(가장 가까운 단계).</summary>
        public void SetEngineOrder(float throttle)
        {
            if (config == null) return;
            var orders = config.EngineOrders;
            int best = 0;
            for (int i = 1; i < orders.Length; i++)
                if (Mathf.Abs(orders[i] - throttle) < Mathf.Abs(orders[best] - throttle)) best = i;
            _engineOrder = best;
        }

        /// <summary>개발·검증용: 타 입력을 대신 넣는다(null이면 해제).</summary>
        [System.NonSerialized] public float? DevRudderOverride;
        /// <summary>기준 최고속력(성장 카드 "최고 속력" 반영).</summary>
        public float BaseMaxSpeed => (config != null ? config.BaseMaxSpeed : 1f) * Game.Modules.RunUpgrades.SpeedMultiplier;
        public float ReverseSpeedRatio => config != null ? config.ReverseSpeedRatio : 0.5f;

        /// <summary>마지막으로 선체가 피해를 받은 시각. 손상 통제가 교전 중 수리를 줄이는 데 쓴다.</summary>
        public float LastHullDamageTime { get; private set; } = -999f;

        /// <summary>이 속력(m/s)보다 빠르게 섬에 부딪히면 선체 피해.</summary>
        private const float GroundingDamageSpeed = 3f;
        private const float GroundingDamagePerSpeed = 1.5f;
        private float _lastGrounding = -999f;

        /// <summary>지금 섬에 닿아 있는가(좌초).</summary>
        public bool IsAground { get; private set; }

        private float _flankUntil;
        private float _flankMultiplier = 1f;

        /// <summary>전속 중인가.</summary>
        public bool IsFlanking => Time.time < _flankUntil;

        /// <summary>전속: 잠시 최고속력·가속·선회를 올린다. 스로틀은 여전히 플레이어가 잡는다.</summary>
        public void BeginFlank(float speedMultiplier, float duration)
        {
            _flankMultiplier = Mathf.Max(1f, speedMultiplier);
            _flankUntil = Time.time + Mathf.Max(0f, duration);
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;

            // 함선 이동은 물리 시뮬레이션이 아니라 직접 제어다.
            // 동적 리지드바디로 두면 솔버가 끼어들거나 슬립 상태에 빠져 MovePosition이 무시될 수 있다.
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.constraints = RigidbodyConstraints.FreezePositionY |
                              RigidbodyConstraints.FreezeRotationX |
                              RigidbodyConstraints.FreezeRotationZ;

            _collider = GetComponent<BoxCollider>();

            if (config == null) { Debug.LogError("[ShipController] ShipConfig 미할당.", this); return; }
            HullMaxHp = config.HullMaxHp;
            HullHp = HullMaxHp;
        }

        private void OnEnable()
        {
            GameEvents.ModuleInstalled += OnModuleChanged;
            GameEvents.ModuleRemoved += OnModuleChanged;
            TargetRegistry.Register(this);
            Game.Modules.RunUpgrades.Changed += ApplyHullBonus;
            ApplyHullBonus();
        }

        /// <summary>
        /// 성장 카드 "선체 강화": 최대치를 (1+보너스)배로 바꾸고 늘어난 만큼 선체도 채운다.
        /// 지금 값에서 상대적으로 바꾸므로(이전 배율로 나누고 새 배율을 곱함) 다른 곳에서 최대치를 바꿔 두었어도 덮어쓰지 않는다.
        /// </summary>
        private void ApplyHullBonus()
        {
            float want = Game.Modules.RunUpgrades.HullMultiplier;
            if (Mathf.Approximately(want, _hullMultiplier)) return;
            float oldMax = HullMaxHp;
            HullMaxHp *= want / _hullMultiplier;
            _hullMultiplier = want;
            if (IsAlive && HullMaxHp > oldMax) HullHp += HullMaxHp - oldMax;
            HullHp = Mathf.Min(HullHp, HullMaxHp);
            GameEvents.RaiseHullHpChanged(HullHp, HullMaxHp);
        }

        private float _hullMultiplier = 1f;

        private void OnDisable()
        {
            GameEvents.ModuleInstalled -= OnModuleChanged;
            GameEvents.ModuleRemoved -= OnModuleChanged;
            Game.Modules.RunUpgrades.Changed -= ApplyHullBonus;
            TargetRegistry.Unregister(this);
        }

        private void OnModuleChanged(Game.Modules.ModuleInstance _) => RebuildCollider();

        /// <summary>
        /// 함체가 자라면 피격 판정도 같이 커져야 한다.
        /// 커진 만큼 맞기 쉬워지는 것이 무게에 대한 또 하나의 대가다.
        /// </summary>
        private void RebuildCollider()
        {
            if (_collider == null || grid == null) return;

            grid.GetExtent(out var min, out var max);

            float cell = grid.CellSize;
            float length = (max.X - min.X + 1) * cell;
            float beam = (max.Z - min.Z + 1) * cell;

            _collider.size = new Vector3(beam, 2f, length);
            _collider.center = new Vector3((min.Z + max.Z) * 0.5f * cell, 0f,
                                           (min.X + max.X) * 0.5f * cell);
        }

        private void Start()
        {
            Game.View.ShipWake.Ensure(gameObject);   // 지나간 자리의 물살·뱃머리 물보라
            Game.View.FunnelSmoke.Ensure(gameObject, () => Mathf.Abs(ThrottleInput));   // 연돌 연기(기관 명령만큼)
            GameEvents.RaiseHullHpChanged(HullHp, HullMaxHp);
            RebuildCollider();
        }

        private void Update()
        {
            _moveInput = ReadMoveInput();
            if (Time.timeScale <= 0f || config == null) return;   // 정비·일시정지 중에는 조함 입력을 받지 않는다

            // W/S(또는 스틱 위아래)는 누를 때마다 전령기를 한 칸씩 옮긴다. 떼도 출력은 유지된다.
            bool up = _moveInput.y > 0.5f, down = _moveInput.y < -0.5f;
            if (up && !_gearUpHeld) ShiftEngineOrder(+1);
            if (down && !_gearDownHeld) ShiftEngineOrder(-1);
            _gearUpHeld = up;
            _gearDownHeld = down;

            // X: 타 중앙
            if (GameSettings.Pressed(NavalControl.CenterRudder) && DevRudderOverride == null) _rudder = 0f;
        }

        /// <summary>
        /// 이동 입력을 읽는다.
        /// 키보드는 사용자 지정 키, 게임패드는 왼쪽 스틱으로 읽는다.
        /// </summary>
        private Vector2 ReadMoveInput()
        {
            Vector2 k = Vector2.zero;
            if (GameSettings.Held(NavalControl.ThrottleUp)) k.y += 1f;
            if (GameSettings.Held(NavalControl.ThrottleDown)) k.y -= 1f;
            if (GameSettings.Held(NavalControl.RudderRight)) k.x += 1f;
            if (GameSettings.Held(NavalControl.RudderLeft)) k.x -= 1f;

            if (k.sqrMagnitude < 0.0001f)
            {
                var gp = Gamepad.current;
                if (gp != null) k = gp.leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(k, 1f);
        }

        private void FixedUpdate()
        {
            if (config == null || !IsAlive) return;

            float dt = Time.fixedDeltaTime;
            float maxSpeed = BaseMaxSpeed;
            float turnRate = config.BaseTurnRateDegPerSec;
            float accelBoost = 1f;
            if (IsFlanking)
            {
                maxSpeed *= _flankMultiplier;
                turnRate *= 1.25f;
                accelBoost = _flankMultiplier * 1.5f;
            }

            // 배는 입력 방향으로 곧장 가지 않는다.
            //   W/S = 기관 전령기(단계, 떼도 유지), A/D = 조타(타각이 서서히 돌아감), 이동은 언제나 선수 방향.
            //   선회율은 타각을 따라 늦게 붙고, 타를 풀어도 한동안 계속 돈다. 크게 돌면 속력을 잃는다.
            float throttle = OrderedThrottle;
            UpdateRudder(dt);

            // --- 기관: 전령기 출력까지 가감속. 후진은 전진보다 느리다. 선회 중에는 속력을 조금 잃는다.
            float maxReverse = maxSpeed * config.ReverseSpeedRatio;
            float targetSpeed = throttle >= 0f ? throttle * maxSpeed : throttle * maxReverse;
            targetSpeed *= 1f - config.TurnSpeedLoss * Mathf.Clamp01(Mathf.Abs(_yawRate) / Mathf.Max(1f, turnRate));
            bool speedingUp = Mathf.Abs(targetSpeed) > Mathf.Abs(_currentSpeed) && Mathf.Sign(targetSpeed) == Mathf.Sign(_currentSpeed == 0f ? targetSpeed : _currentSpeed);
            float accel = speedingUp ? config.Acceleration * accelBoost : config.Deceleration;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accel * dt);

            // --- 타: 물이 흐를 때만 듣는다. 정지 상태에서는 제자리 선회가 되지 않는다.
            float fullEffectSpeed = maxSpeed * config.RudderFullEffectSpeedRatio;
            float effectiveness = fullEffectSpeed > 0.01f
                ? Mathf.Clamp01(Mathf.Abs(_currentSpeed) / fullEffectSpeed)
                : 0f;
            effectiveness = Mathf.Max(effectiveness, config.MinRudderEffect);

            // 후진 중에는 선미가 먼저 돌아가므로 타 효과가 반대로 나타난다
            float direction = _currentSpeed < -0.01f ? -1f : 1f;
            float targetYaw = _rudder * turnRate * effectiveness * direction;

            // 선회율은 타각을 곧바로 따르지 않는다(선체 관성)
            float response = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, config.TurnResponseTime));
            _yawRate = Mathf.Lerp(_yawRate, targetYaw, response);
            if (Mathf.Abs(_yawRate) > 0.001f)
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, _yawRate * dt, 0f));

            // 회전을 반영한 최신 선수 방향으로 전진/후진한다
            Vector3 forward = _rb.rotation * Vector3.forward;
            Vector3 next = _rb.position + forward * (_currentSpeed * dt);

            // 섬: 함체가 박히면 밖으로 밀어내고 속력을 잃는다. 빠르게 부딪히면 선체 피해(좌초).
            if (_collider != null && Game.World.Islands.ResolveOverlap(_collider, ref next, _rb.rotation, out Vector3 shore))
            {
                float into = Mathf.Max(0f, -Vector3.Dot(forward * _currentSpeed, shore));
                if (into > GroundingDamageSpeed && Time.time - _lastGrounding > 1f)
                {
                    _lastGrounding = Time.time;
                    float damage = (into - GroundingDamageSpeed) * GroundingDamagePerSpeed;
                    ApplyHullDamage(damage);
                    AudioManager.Play(SfxId.HullImpact, next, 0.9f, 0.7f);
                    CombatLog.Add("좌초", $"섬에 충돌 {into:0.0} m/s → 선체 {damage:0.#}");
                }
                _currentSpeed *= 0.35f;
                IsAground = true;
            }
            else IsAground = false;

            _rb.MovePosition(next);
            GameEvents.RaiseSpeedChanged(_currentSpeed);
        }

        /// <summary>
        /// A/D를 누르는 동안 타각이 최대 타각 쪽으로 서서히 돌아간다(중앙 → 최대 rudderShiftTime초).
        /// 떼면 설정에 따라 그 자리에 남거나 중앙으로 돌아간다. X는 중앙.
        /// </summary>
        private void UpdateRudder(float dt)
        {
            float step = dt / Mathf.Max(0.05f, config.RudderShiftTime);
            if (DevRudderOverride.HasValue)
            {
                _rudder = Mathf.MoveTowards(_rudder, Mathf.Clamp(DevRudderOverride.Value, -1f, 1f), step);
                return;
            }

            float helm = _moveInput.x;
            if (Mathf.Abs(helm) > 0.2f)
                _rudder = Mathf.MoveTowards(_rudder, Mathf.Sign(helm), step * Mathf.Abs(helm));
            else if (!config.RudderHoldsPosition)
                _rudder = Mathf.MoveTowards(_rudder, 0f, step);
        }

        /// <summary>충돌 지점을 아는 피해. 실제 분배는 DamageResolver가 수행한다.</summary>
        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            AudioManager.Play(SfxId.HullImpact, info.HitPoint);

            if (damageResolver != null) damageResolver.Resolve(info);
            else ApplyHullDamage(info.Amount);   // 방어 코드: 리졸버가 없으면 선체가 전부 받는다
        }

        /// <summary>수리반이 선체를 회복시킨다. 최대치를 넘지 않는다.</summary>
        public void RepairHull(float amount)
        {
            if (amount <= 0f || !IsAlive) return;
            if (HullHp >= HullMaxHp) return;

            HullHp = Mathf.Min(HullMaxHp, HullHp + amount);
            GameEvents.RaiseHullHpChanged(HullHp, HullMaxHp);
        }

        /// <summary>DamageResolver가 모듈 몫을 뗀 뒤 남은 선체 피해를 적용한다.</summary>
        public void ApplyHullDamage(float amount)
        {
            if (amount <= 0f || !IsAlive) return;

            HullHp = Mathf.Max(0f, HullHp - amount);
            LastHullDamageTime = Time.time;
            GameEvents.RaiseHullHpChanged(HullHp, HullMaxHp);

            if (HullHp <= 0f && GameManager.Instance != null) GameManager.Instance.TriggerGameOver();
        }
    }
}
