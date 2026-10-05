using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 전투공격기. 높은 고도로 접근해 낮게 내려와 기총 소사를 퍼붓고, 급상승해 멀리 빠졌다가 다시 돌아온다.
    ///
    ///   접근(Approach): 순항 고도로 플레이어에게 기수를 돌린다.
    ///   공격(Attack):   공격 고도까지 내려와 정면에 들어오면 한 번 점사한다. 머리 위를 지나치면 이탈.
    ///   이탈(Break):    그대로 앞으로 빠지며 상승, 충분히 멀어지면 다시 접근.
    ///
    /// 공격 진입 때 낮고 가까워지므로 기관포·CIWS가 잡을 기회가 생긴다. 멀리 빠질 때는 함대공·76mm 몫이다.
    /// 자폭 드론과 달리 살아서 반복 공격한다. 연막 중에는 조준 소사를 하지 못한다.
    /// </summary>
    public class FighterJet : AirEnemy
    {
        private enum State { Approach, Attack, Break }

        [Header("Gun")]
        [SerializeField] private Transform gunMuzzle;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileSpeed = 60f;
        [Tooltip("한 번 소사할 때 발 수. 한 점사의 총 피해가 EnemyDefinition.AttackDamage다.")]
        [SerializeField, Min(1)] private int burstRounds = 8;
        [SerializeField] private float roundInterval = 0.07f;
        [Tooltip("기수와 표적 사이가 이 각도 안이면 쏜다")]
        [SerializeField] private float fireConeDegrees = 14f;

        [Header("Attack Run")]
        [SerializeField] private float attackAltitude = 4.5f;
        [Tooltip("공격을 마치고 이만큼 멀어지면 다시 돌아온다")]
        [SerializeField] private float extendDistance = 38f;
        [Tooltip("이 수평거리 안으로 지나치면 이탈")]
        [SerializeField] private float breakDistance = 6f;
        [SerializeField] private float breakClimb = 4f;

        private State _state;
        private bool _firedThisRun;
        private int _roundsLeft;
        private float _roundTimer;
        private Vector3 _breakDirection;
        private Game.Ship.ShipController _playerShip;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _state = State.Approach;
            _firedThisRun = false;
            _roundsLeft = 0;
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
        }

        protected override void UpdateBehaviour(float dt)
        {
            Vector3 toPlayer = Player.position - transform.position;
            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            float flatDist = flat.magnitude;
            float speed = Definition.MoveSpeed;
            float turn = Definition.TurnRateDegPerSec;

            switch (_state)
            {
                case State.Approach:
                    Fly(new Vector3(Player.position.x, cruiseAltitude, Player.position.z), speed, turn, false, dt);

                    // PreferredRange = 공격 진입 거리. 기수가 대략 플레이어를 향해야 진입한다.
                    Vector3 fwd = transform.forward; fwd.y = 0f;
                    if (flatDist <= Definition.PreferredRange && Vector3.Angle(fwd, flat) < 40f)
                    {
                        _state = State.Attack;
                        _firedThisRun = false;
                    }
                    break;

                case State.Attack:
                    Fly(new Vector3(Player.position.x, attackAltitude, Player.position.z), speed * 1.15f, turn * 1.2f, false, dt);
                    TryStartBurst(flatDist);

                    // 머리 위를 지나쳤거나 표적이 뒤로 넘어가면 이탈
                    if (flatDist < breakDistance || Vector3.Dot(transform.forward, toPlayer) < 0f)
                    {
                        _state = State.Break;
                        _breakDirection = transform.forward;
                        _breakDirection.y = 0f;
                        if (_breakDirection.sqrMagnitude < 0.01f) _breakDirection = Vector3.forward;
                        _breakDirection.Normalize();
                    }
                    break;

                case State.Break:
                    Vector3 away = transform.position + _breakDirection * 20f;
                    away.y = cruiseAltitude + breakClimb;
                    Fly(away, speed * 1.2f, turn, false, dt);
                    if (flatDist >= extendDistance) _state = State.Approach;
                    break;
            }

            UpdateBurst(dt);
        }

        private void TryStartBurst(float flatDist)
        {
            if (_firedThisRun || _roundsLeft > 0 || PlayerConcealed) return;
            if (flatDist > Definition.PreferredRange) return;

            Vector3 aim = AimPoint();
            var muzzle = gunMuzzle != null ? gunMuzzle : transform;
            if (Vector3.Angle(transform.forward, aim - muzzle.position) > fireConeDegrees) return;

            _firedThisRun = true;
            _roundsLeft = burstRounds;
            _roundTimer = 0f;
        }

        private void UpdateBurst(float dt)
        {
            if (_roundsLeft <= 0) return;
            _roundTimer -= dt;
            if (_roundTimer > 0f) return;

            _roundTimer = roundInterval;
            _roundsLeft--;
            FireRound();
        }

        /// <summary>플레이어 배가 움직이는 만큼 앞을, 선체 중앙 약간 위를 겨눈다.</summary>
        private Vector3 AimPoint()
        {
            Vector3 target = Player.position + Vector3.up * 0.8f;
            if (_playerShip == null) return target;

            var muzzle = gunMuzzle != null ? gunMuzzle : transform;
            return Ballistics.PredictIntercept(muzzle.position, target, Player.forward * _playerShip.CurrentSpeed, projectileSpeed);
        }

        private void FireRound()
        {
            var muzzle = gunMuzzle != null ? gunMuzzle : transform;
            Vector3 dir = (AimPoint() - muzzle.position).normalized;
            dir = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f) * dir;

            var go = projectilePrefab != null
                ? PoolManager.Instance?.Spawn(projectilePrefab, muzzle.position, Quaternion.LookRotation(dir))
                : null;
            go?.GetComponent<Projectile>()?.Launch(dir, projectileSpeed, Definition.AttackDamage / burstRounds, DamageSource.Gun);

            AudioManager.Play(Game.Data.SfxId.AutocannonShot, muzzle.position, 0.35f, 1.35f);
        }
    }
}
