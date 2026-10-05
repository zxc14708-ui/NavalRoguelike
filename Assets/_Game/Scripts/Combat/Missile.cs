using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>비행 궤적 종류.</summary>
    public enum MissileProfile
    {
        /// <summary>순항 고도로 날다가 가까워지면 급강하 (적 대함미사일, VLS).</summary>
        PopUp,
        /// <summary>일정 고도까지 솟은 뒤 표적 하나를 향해 직선으로 내리꽂는다 (유도로켓).</summary>
        ClimbDive,
        /// <summary>곧장 표적을 쫓는다 (함대공 요격미사일).</summary>
        Direct,
    }

    /// <summary>
    /// 유도 미사일. 방어 체계의 핵심 대상이므로 스스로 HP를 갖는다.
    /// 방어 순서: 레이더 탐지 -> 함대공 요격 -> 기만체 유인 / 재밍 -> CIWS 요격 -> 명중.
    /// 적/아군 양쪽이 같은 클래스를 쓰고 대상과 궤적만 다르게 준다.
    /// </summary>
    public class Missile : MonoBehaviour, IPoolable, ITargetable, IDamageable, IHasVelocity, IHasHealth
    {
        [Header("Flight")]
        [SerializeField] private MissileProfile profile = MissileProfile.PopUp;
        [SerializeField] private float speed = 24f;
        [SerializeField] private float turnRateDegPerSec = 120f;
        [SerializeField] private float lifeTime = 12f;
        [SerializeField] private float hitRadius = 1.2f;

        [Header("Pop-up Profile")]
        [Tooltip("순항 고도. 수면에 붙어 날면 CIWS가 수평으로 누워서 쏘게 된다.")]
        [SerializeField] private float cruiseAltitude = 9f;
        [Tooltip("목표까지 수평거리가 이보다 가까워지면 급강하한다")]
        [SerializeField] private float diveDistance = 16f;

        [Header("Climb-Dive Profile")]
        [Tooltip("이 고도까지 솟은 뒤 급강하로 전환한다")]
        [SerializeField] private float climbAltitude = 7f;
        [Tooltip("급강하 중 선회 속도. 크게 잡아 표적 주위를 맴돌지 않고 곧게 꽂히게 한다")]
        [SerializeField] private float diveTurnRateDegPerSec = 720f;

        [Header("Common")]
        [Tooltip("이 고도 아래로는 내려가지 않는다. 바다 평면은 -0.9, 함선 피벗은 0.")]
        [SerializeField] private float minAltitude = 0.6f;
        [Tooltip("목표의 피벗(수면)이 아니라 선체 중앙 높이를 겨눈다")]
        [SerializeField] private float aimHeight = 0.8f;

        [Header("Warhead / Durability")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private float maxHp = 3f;   // CIWS가 몇 발 맞혀야 떨어지는지

        [Tooltip("적이 쏜 미사일인가. 이것이 켜진 미사일만 위협으로 등록되어 " +
                 "CIWS/기만체/재밍의 대상이 된다. 아군 미사일은 꺼둔다.")]
        [SerializeField] private bool isThreat = true;

        [Header("Visual")]
        [Tooltip("추진 연기 꼬리")]
        [SerializeField] private TrailRenderer exhaustTrail;
        [Tooltip("추진 연기 뭉치")]
        [SerializeField] private ParticleSystem exhaustSmoke;
        [Tooltip("명중·요격 시 폭발")]
        [SerializeField] private GameObject blastEffect;
        [Tooltip("발사음 높낮이. 요격미사일은 높고 짧게, 대함미사일은 낮고 묵직하게")]
        [SerializeField] private float launchPitch = 1f;

        [Header("Jammed (전자전)")]
        [Tooltip("재밍 중 선회 속도 배수. 추적이 둔해져 크게 돌아 들어온다")]
        [SerializeField, Range(0.05f, 1f)] private float jammedTurnMultiplier = 0.3f;
        [Tooltip("재밍 중 표적 주변을 헤매는 반경")]
        [SerializeField] private float jammedWanderRadius = 10f;

        /// <summary>
        /// 종말 유도 여유 배율. 남은 거리를 가는 동안 필요한 각도를 이 배율만큼 빨리 돌 수 있게 선회 속도를 올린다.
        /// 선회 속도가 고정이면 표적이 선회 반경 안에 들어왔을 때 영원히 맴돈다.
        /// </summary>
        private const float TerminalGain = 2f;
        private const float MaxTurnRateDegPerSec = 1440f;

        private Transform _target;
        private ITargetable _targetable;
        private float _hp;
        private float _age;
        private bool _decoyed;
        private bool _threatEnded;
        private bool _diving;
        // 접근 방향 바꾸기(2026-10-03, 현대화 초계함): 먼저 표적 둘레의 경유점으로 돌아 들어간 뒤 평소처럼 유도한다
        private bool _approachActive;
        private float _approachAngle, _approachRadius, _approachUntil;
        private Vector3 _approachFrom;
        private float _aimOffset;   // 함정은 선체 중앙 높이를, 미사일·기만체는 그 자체를 겨눈다
        private bool _aswDelivery;
        private Vector3 _aswPoint;
        private GameObject _aswTorpedo;

        private float _jamUntil;
        private float _jamWanderTimer;
        private Vector3 _jamOffset;

        // 표적 분배(아군 유도탄만)
        private int _claimId;
        private Transform _claimTarget;

        // 개발용 통계
        private string _statsKey;
        private float _screenEnteredAt = -1f;

        // --- ITargetable
        public Transform Transform => transform;

        /// <summary>지금 날아가는 속도 벡터. 요격 무기의 예측 사격에 쓴다.</summary>
        public Vector3 Velocity => transform.forward * speed;
        public TargetKind Kind => TargetKind.Missile;
        /// <summary>적 미사일(위협)은 적대 진영, 아군 유도탄·요격탄은 아군 진영.</summary>
        public CombatFaction Faction => isThreat ? CombatFaction.Hostile : CombatFaction.Player;
        public bool IsAlive => isActiveAndEnabled && _hp > 0f;
        public bool IsRevealed => true;
        public bool IsThreat => isThreat;

        /// <summary>전투 기록 번호(적 미사일만).</summary>
        public int LogId { get; private set; }
        public string LogName => isThreat ? $"미사일 #{LogId}" : $"{name.Replace("(Clone)", "")}";
        public float CurrentHp => _hp;

        /// <summary>기만체에 이미 속았는지. 한 번 속으면 다시 판정하지 않는다.</summary>
        public bool IsDecoyed => _decoyed;

        /// <summary>재밍으로 추적이 흐트러진 상태인지(일정 시간 뒤 다시 추적한다).</summary>
        public bool IsJammed => Time.time < _jamUntil;

        /// <summary>발사 직후 1회 설정한다.</summary>
        public void Launch(Transform target, float damageOverride = -1f)
        {
            _aswDelivery = false;
            _aswTorpedo = null;
            SetTarget(target);

            // 함정은 선체 중앙 높이를, 공중에 떠 있는 것(미사일·기만체·항공기)은 그 자체를 겨눈다
            bool airborne = _targetable != null &&
                            (_targetable.Kind == TargetKind.Missile || _targetable.Kind == TargetKind.Decoy ||
                             _targetable.Kind == TargetKind.Aircraft);
            _aimOffset = airborne ? 0f : aimHeight;
            _hp = maxHp;
            _age = 0f;
            _decoyed = false;
            _threatEnded = false;
            _jamUntil = 0f;
            _diving = false;
            _approachActive = false;
            _screenEnteredAt = -1f;
            if (damageOverride > 0f) damage = damageOverride;

            // 아군 유도탄은 표적에 예상 피해를 배정해 다른 발사기가 같은 표적에 몰리지 않게 한다
            ReleaseClaim();
            if (!isThreat && _targetable != null)
            {
                _claimId = TargetAllocator.ClaimTarget(_targetable, damage, lifeTime);
                _claimTarget = _target;
            }

            _statsKey = CombatStats.KeyFor(gameObject);
            if (!isThreat) CombatStats.RecordFire(_statsKey);

            RestartExhaust();
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, transform.position, isThreat ? 1f : 0.7f, launchPitch);

            // 경고 표시는 적 미사일에만 띄운다
            if (isThreat)
            {
                LogId = CombatLog.NextMissileId();
                GameEvents.RaiseIncomingMissile(transform);
                CombatLog.Add("적 미사일", $"#{LogId} 발사 · 표적까지 {(target != null ? Vector3.Distance(transform.position, target.position) : 0f):0}m");
            }
            else if (_targetable != null)
            {
                CombatLog.Add("아군 유도탄", $"{LogName} 발사 → {CombatLog.Describe(_targetable)}");
            }
        }

        /// <summary>
        /// Launch 직후 부른다: 표적으로 곧장 오지 않고, 발사 위치→표적 선을 angleDeg만큼 돌린 방향의 표적 둘레 radius m 경유점으로
        /// 먼저 돌아 들어간 뒤(최대 maxSeconds초) 평소처럼 유도한다. 여러 발이 서로 다른 방향에서 들어오게 할 때 쓴다(현대화 초계함).
        /// </summary>
        public void SetApproach(float angleDeg, float radius, float maxSeconds = 5f)
        {
            if (_target == null || Mathf.Abs(angleDeg) < 1f) return;
            _approachActive = true;
            _approachAngle = angleDeg;
            _approachRadius = Mathf.Max(6f, radius);
            _approachUntil = _age + maxSeconds;
            _approachFrom = transform.position;
        }

        /// <summary>경유점: 표적에서 (발사 위치 쪽 방향을 _approachAngle만큼 돌린) 방향으로 _approachRadius m, 순항 고도.</summary>
        private bool SteerApproach(Vector3 hitPoint, float dt)
        {
            if (!_approachActive) return false;
            Vector3 back = _approachFrom - hitPoint; back.y = 0f;
            if (back.sqrMagnitude < 1f) back = -transform.forward;
            Vector3 dir = Quaternion.Euler(0f, _approachAngle, 0f) * back.normalized;
            Vector3 waypoint = hitPoint + dir * _approachRadius;
            waypoint.y = Mathf.Max(minAltitude + 1f, cruiseAltitude * 0.6f);
            Vector3 flat = waypoint - transform.position; flat.y = 0f;
            if (flat.magnitude < 5f || _age >= _approachUntil || IsJammed || _decoyed)
            {
                _approachActive = false;
                return false;
            }
            HomeOn(waypoint, turnRateDegPerSec, dt);
            return true;
        }

        /// <summary>마지막 소나 접촉 위치로 비행해 수중 폭뢰를 투하한다. 잠수함 현재 위치는 추적하지 않는다.</summary>
        public void LaunchAsw(Vector3 lastKnownPoint, GameObject torpedoPrefab, float damageOverride)
        {
            Launch(null, damageOverride);
            _aswPoint = new Vector3(lastKnownPoint.x, 0f, lastKnownPoint.z);
            _aswTorpedo = torpedoPrefab;
            _aswDelivery = true;
        }

        /// <summary>기만 성공. 목표를 기만체로 바꾼다.</summary>
        public void RedirectToDecoy(Transform decoy)
        {
            if (_decoyed || decoy == null) return;
            _decoyed = true;
            SetTarget(decoy);
            CombatLog.Add("기만체", $"{LogName} 유인 성공 → 기만체로 선회");
            _aimOffset = 0f;
        }

        private void SetTarget(Transform target)
        {
            if (target != _claimTarget) ReleaseClaim();
            _target = target;
            _targetable = target != null ? target.GetComponent<ITargetable>() : null;
        }

        private void ReleaseClaim()
        {
            if (_claimId != 0) TargetAllocator.Release(_claimTarget, _claimId);
            _claimId = 0;
            _claimTarget = null;
        }

        /// <summary>표적이 격침·요격돼 풀로 돌아갔는지. 풀에서 재사용된 엉뚱한 대상을 쫓지 않게 한다.</summary>
        private bool TargetLost()
        {
            if (_target == null) return true;
            if (!_target.gameObject.activeInHierarchy) return true;
            return _targetable != null && !_targetable.IsAlive;
        }

        /// <summary>
        /// 재밍. 표적은 그대로지만 duration 동안 추적 성능이 떨어진다: 표적 주변 엉뚱한 곳을 향해 둔하게 돌아
        /// 크게 빗나가며 돌아온다. 기만체처럼 즉시 목표를 바꾸지는 않는다 — 벌어진 시간에 CIWS·조함으로 대응한다.
        /// </summary>
        public bool Jam(float duration)
        {
            if (!isThreat || _decoyed || _target == null) return false;

            _jamUntil = Mathf.Max(_jamUntil, Time.time + Mathf.Max(0.1f, duration));
            CombatLog.Add("전자전", $"{LogName} 추적 교란 {duration:0.#}초");
            _jamWanderTimer = 0f;
            transform.rotation *= Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(-30f, 30f), 0f);
            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= lifeTime) { Kill(false); return; }

            if (_target != null && TargetLost()) SetTarget(null);
            if (isThreat && _screenEnteredAt < 0f && CombatStats.IsOnScreen(transform.position)) _screenEnteredAt = Time.time;

            if (_aswDelivery)
            {
                Vector3 release = _aswPoint + Vector3.up * 5f;
                HomeOn(release, turnRateDegPerSec, dt);
                Vector3 aswStep = transform.forward * (speed * dt);
                if (SegmentHits(transform.position, aswStep, release, out Vector3 dropPoint))
                {
                    transform.position = dropPoint;
                    var torpedo = PoolManager.Instance?.Spawn(_aswTorpedo, dropPoint, Quaternion.identity);
                    torpedo?.GetComponent<AswTorpedo>()?.Launch(dropPoint, _aswPoint, damage);
                    Kill(false);
                    return;
                }
                transform.position += aswStep;
                return;
            }

            Vector3 step;
            if (_target != null)
            {
                Vector3 hitPoint = AimPoint();
                if (!SteerApproach(hitPoint, dt)) Steer(hitPoint, dt);
                step = transform.forward * (speed * dt);

                if (HitsIsland(step)) return;

                // 이번 프레임 이동 구간 전체로 판정한다. 빠른 미사일이 한 프레임에 표적을 건너뛰면 되돌아와 맴돈다.
                if (SegmentHits(transform.position, step, hitPoint, out Vector3 impact))
                {
                    transform.position = impact;
                    Detonate(_target);
                    return;
                }
            }
            else
            {
                // 표적을 잃은 미사일(재밍·표적 격침)은 기수를 떨구고 바다에 떨어진다
                Vector3 fall = transform.forward;
                fall.y = 0f;
                TurnTowards((fall.sqrMagnitude > 0.0001f ? fall.normalized : Vector3.forward) + Vector3.down, turnRateDegPerSec * 0.5f, dt);
                step = transform.forward * (speed * dt);
                if (HitsIsland(step)) return;
            }

            transform.position += step;
            KeepAboveWater();
        }

        /// <summary>이번 이동 구간에 섬이 있으면 거기서 터진다(아무도 맞지 않음).</summary>
        private bool HitsIsland(Vector3 step)
        {
            if (Game.World.Islands.All.Count == 0 || step.sqrMagnitude < 1e-8f) return false;
            if (!Physics.Raycast(transform.position, step.normalized, out var land, step.magnitude,
                                 Game.World.Islands.Mask, QueryTriggerInteraction.Ignore)) return false;
            transform.position = land.point;
            if (isThreat) CombatLog.Add("섬", $"{LogName} 섬에 충돌");
            Detonate(null);
            return true;
        }

        /// <summary>함정은 선체 중앙 높이를, 미사일·기만체는 그 자체를 겨눈다. 수면 아래는 겨누지 않는다.</summary>
        private Vector3 AimPoint()
        {
            Vector3 p = _target.position;
            p.y = Mathf.Max(p.y + _aimOffset, minAltitude);
            return p;
        }

        private bool SegmentHits(Vector3 from, Vector3 step, Vector3 point, out Vector3 impact)
        {
            float len2 = step.sqrMagnitude;
            float t = len2 > 0.000001f ? Mathf.Clamp01(Vector3.Dot(point - from, step) / len2) : 0f;
            impact = from + step * t;
            return (point - impact).sqrMagnitude <= hitRadius * hitRadius;
        }

        private void Steer(Vector3 hitPoint, float dt)
        {
            if (IsJammed)
            {
                _jamWanderTimer -= dt;
                if (_jamWanderTimer <= 0f)
                {
                    _jamWanderTimer = 0.7f;
                    Vector2 r = Random.insideUnitCircle.normalized * jammedWanderRadius;
                    _jamOffset = new Vector3(r.x, Random.Range(1f, 4f), r.y);
                }
                TurnTowards(hitPoint + _jamOffset - transform.position, turnRateDegPerSec * jammedTurnMultiplier, dt);
                return;
            }

            switch (profile)
            {
                case MissileProfile.ClimbDive:
                    SteerClimbDive(hitPoint, dt);
                    break;

                case MissileProfile.Direct:
                    HomeOn(LeadPoint(hitPoint), turnRateDegPerSec, dt);
                    break;

                default:
                    HomeOn(PopUpAimPoint(hitPoint), turnRateDegPerSec, dt);
                    break;
            }
        }

        /// <summary>
        /// 움직이는 미사일을 쫓을 때는 뒤꽁무니가 아니라 만날 지점을 겨눈다.
        /// 꽁무니를 쫓으면 옆으로 지나가는 표적 주위를 돌게 된다.
        /// </summary>
        private Vector3 LeadPoint(Vector3 hitPoint)
        {
            if (_targetable is not IHasVelocity moving) return hitPoint;
            return Ballistics.PredictIntercept(transform.position, hitPoint, moving.Velocity, speed);
        }

        /// <summary>
        /// 상승: 표적 쪽으로 기울어진 가파른 각으로 솟는다.
        /// 급강하: 표적 하나를 향해 곧게 내리꽂는다. 한번 내려가기 시작하면 다시 오르지 않는다.
        /// </summary>
        private void SteerClimbDive(Vector3 hitPoint, float dt)
        {
            Vector3 flat = hitPoint - transform.position;
            flat.y = 0f;

            if (!_diving && (transform.position.y >= climbAltitude || flat.magnitude < 2f))
                _diving = true;

            if (!_diving)
            {
                Vector3 climb = (flat.sqrMagnitude > 0.01f ? flat.normalized : transform.forward) * 0.6f + Vector3.up;
                TurnTowards(climb, turnRateDegPerSec, dt);
                return;
            }

            HomeOn(hitPoint, diveTurnRateDegPerSec, dt);
        }

        /// <summary>
        /// 표적(또는 경유점)으로 기수를 돌린다. 기본 선회 속도로 부족하면 남은 거리 안에 돌아설 수 있을 만큼 올린다.
        /// 멀리서는 완만하게 돌고, 가까울수록 날카롭게 꺾여 선회 반경 안에서 맴돌지 않는다.
        /// </summary>
        private void HomeOn(Vector3 point, float baseRate, float dt)
        {
            Vector3 to = point - transform.position;
            float dist = to.magnitude;
            if (dist < 0.001f) return;

            float angle = Vector3.Angle(transform.forward, to);
            float needed = angle * speed / Mathf.Max(dist, 0.5f) * TerminalGain;
            TurnTowards(to, Mathf.Min(Mathf.Max(baseRate, needed), MaxTurnRateDegPerSec), dt);
        }

        private void TurnTowards(Vector3 direction, float rate, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            var want = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
        }

        /// <summary>
        /// 최저 고도에 닿으면 높이를 붙잡고 기수를 수평으로 편다(수면 아래로 파고들지 않게).
        /// 표적을 잃은 미사일은 그대로 바다에 떨어져 사라진다.
        /// </summary>
        private void KeepAboveWater()
        {
            var p = transform.position;
            if (p.y >= minAltitude) return;

            if (_target == null) { Kill(false); return; }

            transform.position = new Vector3(p.x, minAltitude, p.z);

            Vector3 f = transform.forward;
            if (f.y >= 0f) return;
            f.y = 0f;
            if (f.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        /// <summary>
        /// 팝업 공격: 멀리서는 순항 고도로 날다가 가까워지면 목표로 내리꽂는다.
        /// 날아오는 미사일이 머리 위에 보여야 CIWS가 올려다보며 요격하는 그림이 나온다.
        /// </summary>
        private Vector3 PopUpAimPoint(Vector3 hitPoint)
        {
            Vector3 flat = hitPoint - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude <= diveDistance * diveDistance) return hitPoint;
            return new Vector3(hitPoint.x, hitPoint.y + cruiseAltitude, hitPoint.z);
        }

        /// <summary>CIWS·요격미사일에 맞는다. HP가 0이면 공중에서 폭발한다.</summary>
        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;
            _hp -= info.Amount;
            if (_hp > 0f) return;

            // 공중 요격은 명중 폭발보다 크게, 높은 음으로 — 수면 명중과 구별된다
            PooledEffect.Spawn(blastEffect, transform.position, 1.5f);
            Game.View.Explosions.Spawn(transform.position, 0.9f, Game.View.Explosions.Kind.Air);
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 0.45f, 1.35f);
            if (isThreat)
            {
                CombatStats.RecordIntercept(info.Tag, transform.position);
                EndThreat(ThreatOutcome.Intercepted);
                CombatLog.Add("요격", $"{LogName} 격추 ({(string.IsNullOrEmpty(info.Tag) ? info.Source.ToString() : info.Tag)})");
            }
            Kill(true);   // true = 요격 성공
        }

        private void Detonate(Transform hit)
        {
            bool damaged = false;
            if (hit != null && hit.TryGetComponent<IDamageable>(out var dmg) && dmg.IsAlive && Factions.CanDamage(Faction, dmg))
            {
                dmg.TakeDamage(new DamageInfo(damage, transform.position, transform.forward, DamageSource.Missile, _statsKey));
                damaged = true;
            }

            if (isThreat)
            {
                EndThreat(damaged ? ThreatOutcome.HitShip : ThreatOutcome.Lost);
                CombatLog.Add(damaged ? "명중" : "소멸", damaged
                    ? $"{LogName} 함선 명중 (피해 {damage:0.#})"
                    : $"{LogName} {(_decoyed ? "기만체에서 폭발" : "표적 없이 폭발")}");
            }
            // 요격(표적이 미사일)은 맞은 미사일이 요격으로 센다 — 여기서 또 세면 두 번 센다
            else if (damaged && !(hit.TryGetComponent<Missile>(out _))) CombatStats.RecordHit(_statsKey, transform.position);

            PooledEffect.Spawn(blastEffect, transform.position);
            // 폭발 연출: 미사일끼리(요격)는 공중 폭발, 함체·표적은 명중 폭발, 표적 없이 떨어지면 물기둥
            float blast = Mathf.Clamp(0.55f + damage / 40f, 0.6f, 1.4f);
            var kind = hit != null && hit.TryGetComponent<Missile>(out _) ? Game.View.Explosions.Kind.Air
                     : hit == null && transform.position.y < 0.6f ? Game.View.Explosions.Kind.Water
                     : Game.View.Explosions.Kind.Impact;
            Game.View.Explosions.Spawn(transform.position, kind == Game.View.Explosions.Kind.Air ? 0.7f : blast, kind);
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 0.75f);

            Kill(false);
        }

        /// <summary>소멸 공통 경로.</summary>
        private void EndThreat(ThreatOutcome outcome)
        {
            if (!isThreat || _threatEnded) return;
            _threatEnded = true;
            CombatStats.RecordThreatEnd(outcome, _screenEnteredAt);
        }

        private void Kill(bool intercepted)
        {
            EndThreat(ThreatOutcome.Lost);   // 수명 만료·바다 추락·기만체에 터짐
            ReleaseClaim();
            if (isThreat) GameEvents.RaiseIncomingMissileCleared(transform);
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        /// <summary>풀에서 꺼낼 때 이전 비행의 연기 꼬리가 발사 지점까지 이어져 그려지지 않게 지운다.</summary>
        private void RestartExhaust()
        {
            if (exhaustTrail != null) exhaustTrail.Clear();
            if (exhaustSmoke != null)
            {
                exhaustSmoke.Clear(true);
                exhaustSmoke.Play(true);
            }
        }

        public void OnSpawned()
        {
            _aswDelivery = false;
            _aswTorpedo = null;
            _hp = maxHp;
            _age = 0f;
            _decoyed = false;
            _threatEnded = false;
            _jamUntil = 0f;
            _diving = false;
            _screenEnteredAt = -1f;
            RestartExhaust();

            // 아군 미사일까지 등록하면 우리 CIWS가 우리 미사일을 쏜다. 아군 미사일은 적 대공포 전용 목록에 따로 둔다.
            if (isThreat) TargetRegistry.Register(this);
            else if (!s_friendly.Contains(this)) s_friendly.Add(this);
        }

        public void OnDespawned()
        {
            _aswDelivery = false;
            _aswTorpedo = null;
            SetTarget(null);
            ReleaseClaim();
            TargetAllocator.ReleaseTarget(transform);   // 이 미사일을 노리던 요격탄들의 배정도 지운다
            if (exhaustSmoke != null) exhaustSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (isThreat) TargetRegistry.Unregister(this);
            else s_friendly.Remove(this);
        }

        private static readonly System.Collections.Generic.List<Missile> s_friendly = new(32);

        /// <summary>날아가는 중인 아군 미사일·로켓. 적 보스의 대공포가 요격 표적으로 쓴다.</summary>
        public static System.Collections.Generic.IReadOnlyList<Missile> ActiveFriendly => s_friendly;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_friendly.Clear();
    }
}
