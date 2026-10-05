using System.Collections.Generic;
using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 함재 헬기. 반복 교전하며 표적 소멸 시 재탐색하고, 적이 없을 때만 귀함한다.
    /// 첫 프로토타입에서는 비행 물리를 쓰지 않고 단순 보간으로 움직인다.
    /// </summary>
    public class AswHelicopter : MonoBehaviour, IPoolable
    {
        private static readonly HashSet<AswHelicopter> ActiveSorties = new();
        public static int AssignedCount(ITargetable target)
        {
            if (target == null) return 0;
            int count = 0;
            foreach (var heli in ActiveSorties)
                if (heli != null && heli.isActiveAndEnabled && ReferenceEquals(heli._target, target)) count++;
            return count;
        }

        private enum Phase { Outbound, Orbit, Attack, Patrol, Return, Landed }

        [SerializeField] private float speed = 18f;
        [SerializeField] private float orbitRadius = 6f;
        [SerializeField] private float orbitDuration = 2.5f;
        [SerializeField] private float attackDamage = 40f;
        [SerializeField] private float cruiseAltitude = 6f;
        [SerializeField] private float attackInterval = 3f;
        [SerializeField] private float idleReturnDelay = 8f;
        [SerializeField] private float searchInterval = 0.35f;
        private float _weaponTimer, _idleTimer, _searchTimer, _orbitAngle;

        [Header("Weapons")]
        [Tooltip("수상함 공격용 유도로켓")]
        [SerializeField] private GameObject rocketPrefab;
        [Tooltip("잠수함 공격용 폭뢰(투하)")]
        [SerializeField] private GameObject depthChargePrefab;

        [Header("Visual")]
        [Tooltip("비행 중 도는 메인 로터")]
        [SerializeField] private Transform rotor;
        [SerializeField] private Transform tailRotor;
        [SerializeField] private float rotorRpm = 260f;

        private Phase _phase;
        private Transform _home;
        private ITargetable _target;
        private float _phaseTimer;
        private Vector3 _searchPoint;
        private HelicopterPolicy _lastPolicy;

        /// <summary>돌아갈 격납고. 착함하면 다시 갑판에 세워준다.</summary>
        private Game.Modules.Runtime.HelicopterDeckModule _deck;

        /// <summary>헬기데크가 출격시킬 때 호출한다.</summary>
        public void Sortie(Transform home, ITargetable target,
                           Game.Modules.Runtime.HelicopterDeckModule deck = null, Vector3 searchPoint = default)
        {
            _home = home;
            _target = target;
            _searchPoint = searchPoint;
            _deck = deck;
            _lastPolicy = deck != null ? deck.Policy : HelicopterPolicy.Automatic;
            _phase = target != null ? Phase.Outbound : Phase.Patrol;
            _phaseTimer = 0f;
            _weaponTimer = 0f;
            _idleTimer = 0f;
            _searchTimer = 0f;
            if (deck != null && deck.Stats.HelicopterSpeed > 0f)
                speed = deck.Stats.HelicopterSpeed;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (_home == null || _deck == null || !_deck.CanOperate)
            {
                Despawn();
                return;
            }
            _phaseTimer += dt;
            _weaponTimer -= dt;
            _searchTimer -= dt;

            if (_lastPolicy != _deck.Policy)
            {
                _lastPolicy = _deck.Policy;
                _target = null;
                _searchPoint = _deck.PatrolPoint();
                _phase = Phase.Patrol;
                _searchTimer = 0f;
                _idleTimer = 0f;
            }

            // 출격 당시 표적이 하나뿐이어서 함께 쫓았더라도, 새 표적이 나타나면 분산한다.
            if ((_phase == Phase.Outbound || _phase == Phase.Orbit) && TargetValid() &&
                AssignedCount(_target) > 1 && _searchTimer <= 0f)
            {
                _searchTimer = Mathf.Max(0.1f, searchInterval);
                var alternative = _deck.FindTarget();
                if (alternative != null && !ReferenceEquals(alternative, _target) &&
                    AssignedCount(alternative) + 1 < AssignedCount(_target))
                {
                    _target = alternative;
                    _phase = Phase.Outbound;
                    _phaseTimer = 0f;
                }
            }

            if (_phase != Phase.Landed && !TargetValid() && _searchTimer <= 0f)
            {
                _searchTimer = Mathf.Max(0.1f, searchInterval);
                if (_target is Game.Enemies.SubmarineBase lost) _searchPoint = lost.LastKnownPosition;
                _target = _deck.FindTargetFromHelicopter(transform.position) ?? _deck.FindTarget();
                if (_deck.TryGetSearchCue(out var cue)) _searchPoint = cue;
                if (TargetValid())
                {
                    _phase = Phase.Outbound;
                    _phaseTimer = 0f;
                    _idleTimer = 0f;
                }
            }

            SpinRotors(dt);

            switch (_phase)
            {
                case Phase.Outbound:
                    if (!TargetValid()) { _phase = Phase.Patrol; break; }
                    if (MoveTowards(TargetPoint(), dt)) { _phase = Phase.Orbit; _phaseTimer = 0f; }
                    break;

                case Phase.Orbit:
                    if (!TargetValid()) { _phase = Phase.Patrol; break; }

                    if (_target is Game.Enemies.SubmarineBase sub &&
                        (sub.transform.position - transform.position).sqrMagnitude < 160f)
                        sub.ConfirmContact(2f);

                    Orbit(dt);
                    if (_phaseTimer >= orbitDuration && _weaponTimer <= 0f) _phase = Phase.Attack;
                    break;

                case Phase.Attack:
                    if (TargetValid())
                    {
                        Attack();
                        _weaponTimer = Mathf.Max(0.1f, attackInterval);
                    }
                    _target = null;
                    _phase = Phase.Patrol;
                    _searchTimer = 0f;
                    break;

                case Phase.Patrol:
                    Vector3 search = _searchPoint + Vector3.up * cruiseAltitude;
                    if (Vector3.Distance(transform.position, search) > orbitRadius * 1.5f)
                    {
                        MoveTowards(search, dt);
                        break;
                    }
                    _idleTimer += dt;
                    _orbitAngle += dt * 45f;
                    Vector3 patrol = search
                        + new Vector3(Mathf.Cos(_orbitAngle * Mathf.Deg2Rad), 0f,
                                      Mathf.Sin(_orbitAngle * Mathf.Deg2Rad)) * orbitRadius;
                    MoveTowards(patrol, dt);
                    if (_idleTimer >= (_deck != null ? _deck.SearchPersistence : idleReturnDelay)) _phase = Phase.Return;
                    break;

                case Phase.Return:
                    if (_home == null) { Despawn(); break; }
                    if (MoveTowards(_home.position + Vector3.up * 1.5f, dt))
                    {
                        _phase = Phase.Landed;
                        Despawn();   // 착함 = 데크로 회수. 쿨다운은 모듈이 관리한다.
                    }
                    break;
            }
        }

        /// <summary>
        /// 잠수함에는 폭뢰를 떨어뜨리고, 수상함에는 유도로켓을 쏜다.
        /// 즉시 피해를 주면 헬기가 무엇을 했는지 화면에서 보이지 않는다.
        /// </summary>
        private void Attack()
        {
            Vector3 from = transform.position;

            if (_target.Kind == TargetKind.Submarine && depthChargePrefab != null)
            {
                var go = PoolManager.Instance?.Spawn(depthChargePrefab, from, Quaternion.identity);
                if (go != null && go.TryGetComponent<DepthCharge>(out var charge))
                {
                    var point = _target is Game.Enemies.SubmarineBase sub ? sub.LastKnownPosition : _target.Transform.position;
                    charge.Launch(from, point, attackDamage);
                    return;
                }
            }
            else if (_target.Kind != TargetKind.Submarine && rocketPrefab != null)
            {
                Vector3 dir = _target.Transform.position - from;
                var rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : transform.rotation;
                var go = PoolManager.Instance?.Spawn(rocketPrefab, from, rot);
                if (go != null && go.TryGetComponent<Missile>(out var rocket))
                {
                    rocket.Launch(_target.Transform, attackDamage);
                    return;
                }
            }

            // 수중 표적에는 반드시 실제 폭뢰가 도착해야 피해를 준다.
            if (_target.Kind == TargetKind.Submarine) return;
            // 수상 표적용 무장 프리팹이 없을 때의 기존 대체 동작
            if (_target is IDamageable d && d.IsAlive)
            {
                var source = _target.Kind == TargetKind.Submarine ? DamageSource.Torpedo : DamageSource.Missile;
                d.TakeDamage(new DamageInfo(attackDamage, _target.Transform.position, Vector3.down, source));
            }
        }

        /// <summary>로터가 돌아야 헬기로 보인다.</summary>
        private void SpinRotors(float dt)
        {
            if (rotor != null) rotor.Rotate(Vector3.up, rotorRpm * 6f * dt, Space.Self);
            if (tailRotor != null) tailRotor.Rotate(Vector3.right, rotorRpm * 8f * dt, Space.Self);
        }

        private bool TargetValid() => _deck != null && _deck.IsTargetInRange(_target) &&
            (_target is not Game.Enemies.SubmarineBase sub || sub.IsContactConfirmed || sub.IsRevealed);

        private Vector3 TargetPoint()
            => (_target is Game.Enemies.SubmarineBase sub ? sub.LastKnownPosition : _target.Transform.position)
               + Vector3.up * cruiseAltitude;

        /// <summary>도착하면 true.</summary>
        private bool MoveTowards(Vector3 point, float dt)
        {
            Vector3 next = Vector3.MoveTowards(transform.position, point, speed * dt);
            Vector3 dir = next - transform.position;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            transform.position = next;
            return (point - transform.position).sqrMagnitude < 0.5f;
        }

        private void Orbit(float dt)
        {
            Vector3 center = TargetPoint();
            _orbitAngle += 120f * dt;
            Vector3 offset = new Vector3(Mathf.Cos(_orbitAngle * Mathf.Deg2Rad), 0f,
                                         Mathf.Sin(_orbitAngle * Mathf.Deg2Rad)) * orbitRadius;
            MoveTowards(center + offset, dt);
        }

        private void Despawn()
        {
            // 착함 보고. 갑판에 다시 헬기가 서고 쿨다운이 시작된다.
            if (_deck != null) _deck.OnHelicopterReturned();
            _deck = null;

            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned()
        {
            ActiveSorties.Add(this);
            _phase = Phase.Outbound;
            _phaseTimer = _weaponTimer = _idleTimer = _searchTimer = _orbitAngle = 0f;
        }
        public void OnDespawned() { ActiveSorties.Remove(this); _target = null; _home = null; _deck = null; }
        private void OnDestroy() => ActiveSorties.Remove(this);
    }
}
