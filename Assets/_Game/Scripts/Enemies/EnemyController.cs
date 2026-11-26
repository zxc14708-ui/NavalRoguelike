using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Data;

namespace Game.Enemies
{
    /// <summary>
    /// 모든 적의 공통 뼈대: HP, 함정식 이동, 사망 처리.
    /// 종류별 차이는 파생 클래스의 UpdateBehaviour에서만 만든다.
    ///
    /// 적도 플레이어와 같은 규칙으로 움직인다: 선수 방향으로만 나아가고,
    /// 가감속에 시간이 걸리며, 타는 속력이 있어야 듣는다. 후진하지 않는다.
    /// 거리를 유지하고 싶으면 멈추거나 물러나는 대신 목표 주위를 선회한다.
    /// </summary>
    public abstract partial class EnemyController : MonoBehaviour, IPoolable, ITargetable, IDamageable, IHasHealth
    {
        [SerializeField] protected EnemyDefinition definition;

        [Header("Ship Handling")]
        [Tooltip("초당 최고속력의 몇 배만큼 가감속하는가")]
        [SerializeField] private float accelerationRatio = 0.5f;
        [Tooltip("정지에 가까울 때도 남는 최소 타 효과")]
        [SerializeField, Range(0f, 1f)] private float minRudderEffect = 0.3f;
        [Tooltip("이 거리의 몇 배 밖에서는 선회 없이 곧장 접근한다")]
        [SerializeField] private float approachDistanceRatio = 1.8f;
        [Tooltip("선회할 때 플레이어 함체 끝과 내 함체 끝 사이에 두는 여유")]
        [SerializeField] private float hullClearance = 2.5f;

        private BoxCollider _playerHull;
        private BoxCollider _ownHull;

        /// <summary>
        /// 지금 교전 중인 표적(이름은 예전 그대로). 기본은 기함이고, 호위함이 더 가깝고 노릴 만하면 호위함이 된다.
        /// 기함 고유의 값(속력 등)이 필요하면 <see cref="Flagship"/>을 쓴다 — 호위함은 기함과 같은 침로·속력으로 항해한다.
        /// </summary>
        protected Transform Player;
        protected float AttackTimer;
        private Transform _flagship;
        private ITargetable _engaged;
        private float _retargetAt;
        // 등장 때 기본으로 잡은 기함 — 아직 고른 표적이 아니라 첫 선택에서는 "20% 이상 나아야 바꾼다"를 쓰지 않는다
        private bool _defaultTarget;

        /// <summary>플레이어 기함(스폰 때 받은 것). 표적이 호위함이어도 바뀌지 않는다.</summary>
        protected Transform Flagship => _flagship;
        /// <summary>지금 노리는 표적(검증·디버그용).</summary>
        public Transform EngagedTarget => Player;
        /// <summary>호위함도 노리는가. 정찰기·보스, 대함미사일·어뢰를 쏘는 적처럼 기함만 노리는 적은 false.</summary>
        protected virtual bool TargetsEscorts => true;

        /// <summary>기함이 아닌 표적(호위함)마다 지금 노리고 있는 적 수. ITargetPriority.MaxAttackers 제한에 쓴다.</summary>
        private static readonly System.Collections.Generic.Dictionary<Transform, int> s_attackers = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAttackers() => s_attackers.Clear();
        /// <summary>이 표적을 지금 노리는 적 수(기함은 세지 않는다).</summary>
        public static int AttackersOn(Transform t) => t != null && s_attackers.TryGetValue(t, out int n) ? n : 0;
        private float _hp;
        private float _speed;

        /// <summary>선회 방향. 스폰마다 정해 적들이 한쪽으로만 도는 것을 막는다.</summary>
        private float _orbitSide = 1f;

        public float CurrentSpeed => _speed;
        /// <summary>실제로 움직이는 속도(수평). 멈춰 둔 검증용 적(DevFrozen)은 0 — 예측 사격이 앞지르지 않게.</summary>
        public Vector3 MoveVelocity => DevFrozen || !IsAlive ? Vector3.zero : transform.forward * _speed;
        public float CurrentHp => _hp;

        /// <summary>남은 체력 비율(0~1). 보스의 단계 전환에 쓴다.</summary>
        protected float HpFraction => definition != null && definition.MaxHp > 0f ? Mathf.Clamp01(_hp / definition.MaxHp) : 0f;

        // --- ITargetable
        public Transform Transform => transform;
        public virtual TargetKind Kind => definition != null ? definition.Kind : TargetKind.Surface;
        /// <summary>적 진영. 격침 보상은 적대 진영일 때만 준다(Factions.GrantsReward).</summary>
        public virtual CombatFaction Faction => CombatFaction.Hostile;
        public bool IsAlive => isActiveAndEnabled && _hp > 0f;
        public virtual bool IsRevealed => true;

        public EnemyDefinition Definition => definition;

        /// <summary>개발용: 제자리에 멈추고 공격하지 않는다(표적으로는 그대로 살아 있다). 교전 규칙 검증에 쓴다.</summary>
        [System.NonSerialized] public bool DevFrozen;

        /// <summary>스포너가 스폰 직후 호출한다.</summary>
        public virtual void Setup(EnemyDefinition def, Transform player)
        {
            ReleaseEngagement();
            definition = def;
            if (def != null) Game.Core.PlayerProfile.Discover(def.Id);   // 사전 발견 기록(점검 C05)
            // 수상함은 모델 바닥을 수면 아래로(점검 G06 수면선) — 판정 상자·위치는 그대로
            if (def != null && def.Kind == Game.Combat.TargetKind.Surface && transform.Find("ModelPivot") is { } pivot)
            {
                // 파도 흔들림(2026-11-01)은 모델에만 — 수면선을 맞추기 전에 자세를 되돌리고, 맞춘 자리를 기준으로 삼는다
                if (pivot.TryGetComponent<Game.View.ShipVisualSway>(out var sway)) sway.ResetPose();
                Game.View.Waterline.Settle(transform, pivot);
                Game.View.ShipVisualSway.Ensure(pivot).Rebase();
            }
            Player = player;
            _flagship = player;
            _engaged = player != null ? player.GetComponent<ITargetable>() : null;
            // 첫 표적은 등장하자마자 고른다(2026-11-01) — 예전 0.4~1.4초 뒤에 고르면 빠른 배는 그사이 기함 쪽으로 15m 달려 와
            // 옆 호위함을 지나친 뒤라 기함을 골랐다(호위함 표적 검사가 오락가락한 원인)
            _retargetAt = Time.time + Random.Range(0.05f, 0.25f);
            _defaultTarget = true;
            _playerHull = player != null ? player.GetComponent<BoxCollider>() : null;
            if (_ownHull == null) _ownHull = GetComponent<BoxCollider>();
            _hp = def != null ? def.MaxHp : 1f;
            AttackTimer = 0f;
            DevFrozen = false;

            // 화면 밖에서 이미 항해하던 배처럼 등장시킨다: 플레이어 쪽을 향하고 순항 속력을 갖는다
            _orbitSide = Random.value < 0.5f ? -1f : 1f;
            _orbitFlipAt = _unstickUntil = 0f;
            DevOrbitFlips = DevBoundaryHits = 0;
            ResetDetour();   // 섬 우회(2026-11-26)
            if (def != null) _speed = def.MoveSpeed * 0.6f;

            // 물살: 수상함은 항상, 잠수함은 드러났을 때만(잠항 중 물살이 위치를 알려 주면 안 된다). 항공기는 없음.
            if (this is SubmarineBase sub) Game.View.ShipWake.Ensure(gameObject, () => sub.IsRevealed ? 1f : 0f);
            else if (Kind == TargetKind.Surface) Game.View.ShipWake.Ensure(gameObject);
            // 연돌 연기: 연돌·배기구가 있는 수상함만(없으면 스스로 아무것도 안 함). 속력만큼 짙어진다.
            if (Kind == TargetKind.Surface && !(this is SubmarineBase))
                Game.View.FunnelSmoke.Ensure(gameObject, () => definition != null && definition.MoveSpeed > 0f ? _speed / definition.MoveSpeed : 0f);

            // 엘리트 표식(황금 고리·마름모): 엘리트 등급 적에게만
            Game.View.EliteMarker.Apply(this);
            // 적 도색: 위에서 보이는 갑판만 적갈색(시인성 — 바다·아군과 구별)
            Game.View.EnemyPaint.Apply(this);
            // 밤 항해등(2026-11-19): 수상함만(반잠수정 · 잠수함 · 항공기 · 기뢰 · 해안 포대는 없음)
            if (Kind == TargetKind.Surface && this is not ILowObservable && this is not SeaMine && !GetType().Name.Contains("Battery"))
                Game.View.NavLights.Ensure(gameObject);

            if (player != null)
            {
                Vector3 to = player.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
        }

        private void Update()
        {
            if (!IsAlive || definition == null || DevFrozen) return;

            float dt = Time.deltaTime;

            // 정찰기가 표적을 지시하는 동안 모든 적의 재장전이 빨라진다
            if (AttackTimer > 0f)
                AttackTimer -= dt * (ReconAircraft.ActiveCount > 0 ? ReconAircraft.SpottedAttackRate : 1f);

            UpdateTarget();
            if (Player != null) UpdateBehaviour(dt);
        }

        /// <summary>
        /// 표적 선택: 기함과 등록된 아군(적의 적, 수상함) 중 "수평 거리 ÷ 우선도"가 가장 작은 것을 노린다.
        /// 1.5~2.5초마다 다시 보고, 바꾸려면 20% 이상 나아야 한다(표적을 자꾸 바꾸며 헤매지 않게).
        /// 등장 뒤 첫 선택은 이 조건 없이 가장 나은 표적을 고른다(기본 기함은 고른 표적이 아니다).
        /// 노리던 호위함이 격침되면 곧바로 다시 고른다.
        /// </summary>
        private void UpdateTarget()
        {
            if (_flagship == null) return;
            bool lost = Player == null || _engaged == null || !_engaged.IsAlive;
            if (!lost && Time.time < _retargetAt) return;
            _retargetAt = Time.time + Random.Range(1.5f, 2.5f);
            bool sticky = !lost && !_defaultTarget;
            _defaultTarget = false;

            Transform best = _flagship;
            ITargetable bestRef = _flagship.GetComponent<ITargetable>();
            if (TargetsEscorts)
            {
                float bestScore = float.MaxValue, currentScore = float.MaxValue;
                var candidates = TargetRegistry.HostileTo(Faction, TargetKind.Surface);
                for (int i = 0; i < candidates.Count; i++)
                {
                    var t = candidates[i];
                    if (t == null || !t.IsAlive || t.Transform == null) continue;
                    // 이미 최대 수의 적이 노리는 호위함은 건너뛴다(지금 내가 노리는 중이면 계속 센다)
                    if (t is ITargetPriority cap && cap.MaxAttackers > 0 && t.Transform != Player &&
                        AttackersOn(t.Transform) >= cap.MaxAttackers) continue;
                    float score = TargetScore(t);
                    if (!lost && t.Transform == Player) currentScore = score;
                    if (score < bestScore) { bestScore = score; best = t.Transform; bestRef = t; }
                }
                // 지금 표적이 여전히 살아 있으면 새 후보가 확실히 나을 때만 바꾼다
                if (sticky && best != Player && bestScore > currentScore * 0.8f) return;
            }
            if (best == Player && !lost) return;
            ReleaseEngagement();
            Player = best;
            _engaged = bestRef;
            if (Player != null && Player != _flagship) s_attackers[Player] = AttackersOn(Player) + 1;
            _playerHull = best != null ? best.GetComponent<BoxCollider>() : null;
            OnTargetChanged();
        }

        /// <summary>호위함을 노리던 중이면 그 표적의 공격자 수에서 뺀다.</summary>
        private void ReleaseEngagement()
        {
            if (Player == null || Player == _flagship || !s_attackers.TryGetValue(Player, out int n)) return;
            if (n <= 1) s_attackers.Remove(Player); else s_attackers[Player] = n - 1;
        }

        private float TargetScore(ITargetable t)
        {
            Vector3 d = t.Transform.position - transform.position;
            d.y = 0f;
            float priority = t is ITargetPriority p ? Mathf.Max(0.05f, p.TargetPriority) : 1f;
            return d.magnitude / priority;
        }

        /// <summary>표적이 바뀌었다(Player가 새 표적). 표적 콜라이더를 따로 들고 있는 파생 클래스가 갱신한다.</summary>
        protected virtual void OnTargetChanged() { }

        // ---- 포격 겨냥 칸(2026-11-21): 기함(블록 격자)을 쏠 때 한가운데(함교)만 겨누지 않고, 이쪽을 향한 바깥 칸 하나를 1.2~2.4초마다 바꿔 겨눈다
        private Transform _gunAimFor;
        private Game.Ship.ShipGrid _gunGrid;
        private Game.Ship.GridCoord _gunCell;
        private bool _gunHasCell;
        private float _gunCellUntil;
        /// <summary>기함에 불이 났을 때 포격이 불난 블록을 노릴 확률.</summary>
        public const float BurningFocusChance = 0.4f;
        /// <summary>검증용: 적 포수가 불난 블록을 겨냥으로 고른 횟수(전체).</summary>
        public static int DevBurningPicks;

        /// <summary>
        /// 포 · 기총 · 어뢰가 겨눌 지점. 표적이 블록 격자가 있는 기함이면 이쪽을 향한 바깥 칸(높이 height), 아니면 예전처럼 표적 중심 + height.
        /// 칸은 잠깐씩 유지해 포탑이 떨리지 않게 한다(일제사격 분산 기록에는 넣지 않는다 — 미사일 · 드론 몫).
        /// </summary>
        protected Vector3 PlayerAimPoint(float height = 0.8f)
        {
            if (Player == null) return transform.position;
            Vector3 center = Player.position + Vector3.up * height;
            if (_gunAimFor != Player) { _gunAimFor = Player; _gunGrid = Game.Ship.ShipHitPoints.GridOf(Player); _gunHasCell = false; }
            if (_gunGrid == null) return center;
            if (!_gunHasCell || Time.time >= _gunCellUntil)
            {
                // 불난 곳에 쏟아붓는다(40%, 2026-11-21 손상 통제 4단계) — 불이 번지고 유폭 위험이 커진다
                bool burning = Random.value < BurningFocusChance && Game.Ship.ShipHitPoints.TryPickBurning(_gunGrid, transform.position, out _gunCell);
                if (burning) DevBurningPicks++;
                _gunHasCell = burning || Game.Ship.ShipHitPoints.TryPick(_gunGrid, transform.position, Game.Ship.HitArc.Side, out _gunCell, register: false);
                _gunCellUntil = Time.time + Random.Range(1.2f, 2.4f);
            }
            if (!_gunHasCell) return center;
            Vector3 c = Game.Ship.ShipHitPoints.Point(_gunGrid, _gunCell);
            return new Vector3(c.x, center.y, c.z);
        }

        /// <summary>플레이어가 연막에 가려 조준 사격을 할 수 없는가.</summary>
        protected static bool PlayerConcealed => SmokeScreen.IsActive;

        /// <summary>플레이어가 섬 뒤에 있어 직사(함포·어뢰·대함미사일 발사)를 할 수 없는가.</summary>
        protected bool PlayerBehindIsland => Player != null && Game.World.Islands.BlocksShipLine(transform.position, Player.position);

        /// <summary>
        /// 앞에 섬이 있으면 섬을 따라 비켜 가는 방향으로 바꾼다(가까울수록 강하게).
        /// 항공기는 섬 위로 날아가므로 쓰지 않는다.
        /// </summary>
        private Vector3 AvoidIslands(Vector3 heading)
        {
            if (Kind == TargetKind.Aircraft || Game.World.Islands.All.Count == 0 || heading.sqrMagnitude < 1e-6f) return heading;
            float half = _ownHull != null ? _ownHull.size.x * 0.5f * transform.lossyScale.x : 1.5f;
            float look = OwnHalfLength() + 8f + _speed * 2.2f;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            Vector3 probe = (fwd.normalized + heading.normalized).normalized;
            if (!Game.World.Islands.ProbeAhead(transform.position, probe, Mathf.Max(1f, half), look, out var hit)) return heading;

            Vector3 n = hit.normal; n.y = 0f;
            if (n.sqrMagnitude < 1e-6f) n = -probe;
            n.Normalize();
            Vector3 along = Vector3.ProjectOnPlane(heading, n);
            if (along.sqrMagnitude < 1e-4f) along = Vector3.Cross(Vector3.up, n) * _orbitSide;
            float urgency = 1f - Mathf.Clamp01(hit.distance / look);
            return (along.normalized + n * (0.4f + urgency)).normalized;
        }

        /// <summary>최고속력 배율(엘리트 회피 기동 등). 기본 1.</summary>
        protected virtual float SpeedMultiplier => 1f;

        /// <summary>종류별 행동. 이동과 공격을 여기서 구현한다.</summary>
        protected abstract void UpdateBehaviour(float dt);

        /// <summary>
        /// 플레이어에게 다가가 desiredDistance 반경으로 선회한다.
        /// 멀면 곧장 접근, 반경 근처에서는 접선 방향, 너무 가까우면 바깥쪽으로 돌아 나간다.
        /// </summary>
        /// <param name="throttle">0~1. 선회 중 속력을 낮추고 싶을 때.</param>
        protected void OrbitPlayer(float desiredDistance, float throttle, float dt)
        {
            Vector3 toPlayer = Player.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist < 0.01f) { Steer(transform.forward, throttle, dt); return; }

            Vector3 radial = toPlayer / dist;
            float radius = EngageRadius(desiredDistance);

            Vector3 heading;
            bool orbiting = dist <= radius * approachDistanceRatio;
            if (!orbiting)
            {
                heading = radial;
                throttle = 1f;   // 멀리 있을 땐 전속으로 따라붙는다
            }
            else heading = OrbitHeading(radial, dist, radius, _orbitSide);
            heading = ApplyDetour(heading, radius);   // 섬 뒤 · 두 섬 사이 · 오목한 해안: 열린 물길로 돌아 공격 자리를 다시 잡는다(2026-11-26)

            if (Kind != TargetKind.Aircraft && Game.World.SeaRegion.IsStrait) heading = KeepInWater(heading, radial, dist, radius, orbiting);
            Steer(heading, throttle, dt);
        }

        /// <summary>선회 방향: 반경보다 멀면 안쪽 성분(+), 가까우면 바깥쪽 성분(-)이 섞인 접선.</summary>
        private static Vector3 OrbitHeading(Vector3 radial, float dist, float radius, float side)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * side;
            float correction = Mathf.Clamp((dist - radius) / radius * 2f, -1f, 1f);
            return (tangent + radial * correction).normalized;
        }

        private float _orbitFlipAt, _unstickUntil;
        private Vector3 _unstickNormal;
        /// <summary>검증용: 경계 앞에서 선회 방향을 바꾼 횟수 · 보이지 않는 경계에 막힌 프레임 수.</summary>
        public int DevOrbitFlips { get; private set; }
        public int DevBoundaryHits { get; private set; }

        /// <summary>
        /// 좁은 수로 · 군항 만(2026-11-15): 선회 원이 기슭 밖으로 나가면 기슭에 박혀 미끄러지며 멈춰 버렸다(2스테이지 보스).
        /// 앞쪽 내다본 자리가 경계에 가까우면 ① 반대 방향 선회가 더 넓으면 선회 방향을 바꾸고(3.5초에 한 번까지)
        /// ② 아니면 물 안쪽으로 키를 섞는다. 그래서 기슭 앞에서는 호를 따라 오가며 거리를 지킨다.
        /// </summary>
        private Vector3 KeepInWater(Vector3 heading, Vector3 radial, float dist, float radius, bool orbiting)
        {
            const float margin = 9f;
            float look = OwnHalfLength() + 8f + Mathf.Max(_speed, definition.MoveSpeed * 0.5f) * 2.2f;
            Vector3 pos = transform.position;
            float room = Game.World.SeaRegion.WaterRoom(pos + heading * look);
            if (room >= margin) return heading;
            if (orbiting && Time.time >= _orbitFlipAt)
            {
                var other = OrbitHeading(radial, dist, radius, -_orbitSide);
                float otherRoom = Game.World.SeaRegion.WaterRoom(pos + other * look);
                if (otherRoom > room + 3f)
                {
                    _orbitSide = -_orbitSide;
                    _orbitFlipAt = Time.time + 3.5f;
                    DevOrbitFlips++;
                    heading = other;
                    room = otherRoom;
                    if (room >= margin) return heading;
                }
            }
            Vector3 inward = Game.World.SeaRegion.InwardNormal(pos + heading * look);
            float push = Mathf.Clamp01((margin - room) / (margin + 6f));
            return (heading * (1f - push) + inward * (push * 1.5f)).normalized;
        }

        /// <summary>
        /// 실제로 유지할 교전 거리. 원하는 거리가 함체보다 짧으면 함체 밖까지 밀어낸다.
        /// 플레이어 배는 모듈을 붙일수록 길어지므로 매번 콜라이더에서 다시 잰다.
        /// </summary>
        protected float EngageRadius(float desiredDistance)
        {
            float hull = PlayerHullRadius() + OwnHalfLength() + hullClearance;
            return Mathf.Max(1f, desiredDistance, hull);
        }

        /// <summary>플레이어 피벗(함교)에서 함체 가장 먼 모서리까지의 수평 거리.</summary>
        private float PlayerHullRadius()
        {
            if (_playerHull == null) return 0f;

            // 함체는 함교를 중심으로 앞뒤 비대칭으로 자라므로, 중심 오프셋까지 포함해 모서리를 잰다
            Vector3 c = _playerHull.center;
            Vector3 h = _playerHull.size * 0.5f;
            Vector3 s = _playerHull.transform.lossyScale;
            float x = (Mathf.Abs(c.x) + h.x) * s.x;
            float z = (Mathf.Abs(c.z) + h.z) * s.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        private float OwnHalfLength()
        {
            if (_ownHull == null) return 0f;
            return _ownHull.size.z * 0.5f * transform.lossyScale.z;
        }

        /// <summary>
        /// 모든 적 이동의 단일 경로. 원하는 방향으로 타를 쓰고, 속력은 서서히 바꾸며,
        /// 이동은 언제나 선수 방향이다.
        /// </summary>
        protected void Steer(Vector3 desiredHeading, float throttle, float dt)
        {
            float maxSpeed = definition.MoveSpeed * SpeedMultiplier;
            // 후진(2026-11-26 섬 우회): 틈에 비스듬히 끼면 잠깐 물러난다 — 그동안 키는 쓰지 않는다
            bool reversing = Time.time < _reverseUntil;
            float targetSpeed = reversing ? -0.4f * maxSpeed : Mathf.Clamp01(throttle) * maxSpeed;
            _speed = Mathf.MoveTowards(_speed, targetSpeed, maxSpeed * accelerationRatio * dt);

            desiredHeading.y = 0f;
            // 방금 섬에 박혔으면 잠깐 섬 바깥쪽으로 키를 강하게 잡는다(긴 배가 바위에 낀 채 제자리에서 멈추지 않게)
            if (Time.time < _unstickUntil && desiredHeading.sqrMagnitude > 1e-6f)
                desiredHeading = desiredHeading.normalized * 0.35f + _unstickNormal;
            desiredHeading = AvoidIslands(desiredHeading);
            if (desiredHeading.sqrMagnitude > 0.0001f && !reversing)
            {
                float rudder = maxSpeed > 0.01f ? Mathf.Clamp01(_speed / maxSpeed) : 0f;
                rudder = Mathf.Max(rudder, minRudderEffect);
                // 기슭에 막 닿았다(1초): 제자리 선회(키 75%) — 저속이라 키가 30%만 먹어 선수를 기슭에 박은 채 돌아서는 데 10초 넘게 걸렸다(2026-11-26)
                if (Time.time < _unstickUntil && !DevDetourOff) rudder = Mathf.Max(rudder, 0.75f);

                var want = Quaternion.LookRotation(desiredHeading.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want,
                                                              definition.TurnRateDegPerSec * rudder * dt);
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 next = transform.position + forward.normalized * (_speed * dt);
            if (Kind != TargetKind.Aircraft && _ownHull != null &&
                Game.World.Islands.ResolveOverlap(_ownHull, ref next, transform.rotation, out Vector3 shore))
            {
                // 섬 쪽으로 파고들면 속력을 잃지만, 이미 벗어나는 쪽을 보고 있으면 최고 속력의 35%는 남긴다(속력 0이면 키가 안 먹어 못 빠져나온다)
                bool leaving = throttle > 0.05f && Vector3.Dot(forward.normalized, shore) > -0.2f;
                if (reversing) { }   // 물러나는 중: 속력 유지
                else _speed = leaving ? Mathf.Max(_speed * 0.5f, maxSpeed * 0.35f * Mathf.Clamp01(throttle + 0.3f)) : _speed * 0.5f;
                if (shore.sqrMagnitude > 1e-6f) { _unstickNormal = shore; _unstickUntil = Time.time + 1f; }
                // 섬에 박혔다: 선회 방향을 바꿔 섬에서 돌아 나온다(같은 섬을 계속 파고들며 멈추지 않게, 3.5초에 한 번까지)
                if (Time.time >= _orbitFlipAt) { _orbitSide = -_orbitSide; _orbitFlipAt = Time.time + 3.5f; DevOrbitFlips++; }
            }
            if (Kind != TargetKind.Aircraft && Game.World.SeaRegion.ClampToChannel(ref next, -2f)) { _speed *= 0.5f; DevBoundaryHits++; }   // 좁은 수로 경계(점검 K04)
            transform.position = next;
        }

        public virtual void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            float before = _hp;
            _hp -= info.Amount * (Time.time < _markedUntil ? _markMultiplier : 1f);
            // 결과 분석: 실제로 깎은 체력과 격침(마지막 한 방)을 무장별로(점검 O02)
            if (Factions.GrantsReward(this))
            {
                RunReport.RecordDamage(info.Tag, info.Source, before - Mathf.Max(0f, _hp), _hp <= 0f);
                Game.UI.CombatNumbersUI.Note(this, before - Mathf.Max(0f, _hp), _hp <= 0f);   // 피해 숫자(2026-11-17)
            }
            if (_hp > 0f && info.Amount > 0f) HitFeedback(info);
            if (_hp <= 0f)
            {
                // 마지막 한 방(무엇에 · 어디를 맞았는지)에 따라 침몰 방식이 달라진다(ShipWreck.SinkMode)
                _killSource = info.Source;
                _killPoint = info.HitPoint;
                Die();
                _killSource = null;
            }
        }

        private DamageSource? _killSource;
        private Vector3 _killPoint;
        private Game.View.ShipVisualSway _sway;
        private bool _swayLooked;

        /// <summary>
        /// 타격감(2026-11-20): 맞으면 하얗게 번쩍이고(HitFlash), 수상함 모델은 맞은 방향으로 잠깐 기운다(ShipVisualSway.Kick).
        /// 세기는 한 발 피해로(기관포는 약하게, 76mm는 세게). 판정 · 이동에는 영향이 없다.
        /// </summary>
        private void HitFeedback(in DamageInfo info)
        {
            Game.View.HitFlash.Hit(this, info.Amount);
            if (!_swayLooked)
            {
                _swayLooked = true;
                var pivot = transform.Find("ModelPivot");
                if (pivot != null) pivot.TryGetComponent(out _sway);
            }
            if (_sway != null && info.Source != DamageSource.CookOff)
            {
                Vector3 dir = info.Direction;
                if (dir.sqrMagnitude < 1e-4f && info.HitPoint != Vector3.zero) dir = transform.position - info.HitPoint;
                _sway.Kick(dir, Game.View.HitFlash.StrengthFor(info.Amount));
            }
        }

        // --- 표적 지시(초계함 레이더망 — 예전 광역 탐색, 2026-10-11): 잠깐 받는 피해가 늘어난다
        private float _markedUntil = -1f, _markMultiplier = 1f;
        public bool IsMarked => Time.time < _markedUntil;
        public float MarkedSecondsLeft => Mathf.Max(0f, _markedUntil - Time.time);
        public void Mark(float seconds, float damageMultiplier)
        {
            _markedUntil = Mathf.Max(_markedUntil, Time.time + seconds);
            _markMultiplier = Mathf.Max(1f, damageMultiplier);
        }

        /// <summary>선체를 고친다(수리 지원정). 최대 체력을 넘지 않고, 실제로 고친 양을 돌려준다.</summary>
        public float Repair(float amount)
        {
            if (!IsAlive || definition == null || amount <= 0f) return 0f;
            float before = _hp;
            _hp = Mathf.Min(definition.MaxHp, _hp + amount);
            return _hp - before;
        }

        protected virtual void Die()
        {
            // 격침 연출: 겉모습만 복사한 잔해가 기울며 가라앉는다(장식). 항공기는 공중에서 터진다.
            if (!Game.View.ShipWreck.TrySpawnStage4(this, _speed, _killSource, _killPoint) &&
                Kind != TargetKind.Aircraft && LeavesWreck)
                Game.View.ShipWreck.Spawn(transform, _speed, _killSource, _killPoint);
            AudioManager.Play(SfxId.Explosion, transform.position);
            // 경험치·격침 수는 적대 진영이 격침될 때만(아군으로 싸우는 유닛이 잃어도 보상이 생기지 않게)
            if (Factions.GrantsReward(this)) Game.Core.GameManager.Instance?.RegisterKill(definition);
            RemoveWithoutReward();
        }

        /// <summary>격침 때 가라앉는 잔해를 남기는가(폭약을 실은 배처럼 산산조각 나면 false).</summary>
        protected virtual bool LeavesWreck => true;

        /// <summary>격침이 아닌 퇴장(자폭, 스테이지 전환 정리). 경험치·격침 수를 주지 않는다.</summary>
        public void RemoveWithoutReward()
        {
            _hp = 0f;
            EnemySpawner.Instance?.NotifyEnemyRemoved(this);
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public virtual void OnSpawned()
        {
            TargetRegistry.Register(this);
            if (this is IBossParts) AudioManager.NoteBoss(transform);                       // 보스 음악(2026-11-16)
            if (this is Game.WW2.Ww2StrikeAircraft) AudioManager.Play(SfxId.PropPass, transform.position);   // 프로펠러기 등장
        }
        public virtual void OnDespawned()
        {
            ReleaseEngagement();
            Player = _flagship;
            TargetRegistry.Unregister(this);
            TargetAllocator.ReleaseTarget(transform);   // 이 적을 노리던 유도탄 배정을 지운다
        }
    }
}
