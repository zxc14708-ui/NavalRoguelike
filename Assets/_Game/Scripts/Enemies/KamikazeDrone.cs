using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 자폭 드론. 체력은 낮지만 빠르게 날아와 함선에 그대로 들이받는다.
    /// 순항 고도로 접근하다가 가까워지면 내리꽂는다. 맞은 자리의 모듈이 피해를 받는다.
    ///
    /// 미사일과 달리 기만체·재밍에 속지 않는다 — 쏴서 떨어뜨려야 한다(기관포·76mm·CIWS·함대공).
    /// 스스로 터지면 경험치를 주지 않는다.
    /// </summary>
    public class KamikazeDrone : AirEnemy
    {
        [SerializeField] private GameObject blastEffect;

        [Tooltip("급강하할 때 속력 배수")]
        [SerializeField] private float diveSpeedMultiplier = 1.3f;

        [Tooltip("함체 표면에서 이 거리 안이면 폭발")]
        [SerializeField] private float detonateDistance = 1.2f;

        [Tooltip("꼬리 프로펠러(선택). 기체 진행 방향 축으로 돈다.")]
        [SerializeField] private Transform propeller;
        [SerializeField] private float propellerRpm = 1800f;

        private Collider _playerHull;
        private bool _diving;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerHull = player != null ? player.GetComponent<Collider>() : null;
            _diving = false;
            _aimFor = null;   // 풀에서 다시 나오면 칸을 새로 고른다
        }

        /// <summary>자폭 공격(한 방 큰 피해)은 호위함에 쓰지 않는다 — 위협은 기함에 모은다(대함미사일·어뢰와 같은 규칙).</summary>
        protected override bool TargetsEscorts => false;

        protected override void OnTargetChanged()
        {
            _playerHull = Player != null ? Player.GetComponent<Collider>() : null;
            _aimFor = null;   // 다음 프레임에 새 표적의 칸을 고른다
        }

        // 겨냥 칸(2026-11-21): 위에서 꽂히므로 갑판 블록 전체에서 칸마다 같은 확률 — 예전에는 늘 한가운데(함교)
        private Transform _aimFor;
        private Game.Ship.ShipGrid _grid;
        private Game.Ship.GridCoord _cell;
        private bool _hasCell;

        private Vector3 AimPoint()
        {
            if (_aimFor != Player)
            {
                // 등장 때 받은 기함은 OnTargetChanged 없이 들어오므로 여기서 처음 고른다
                _aimFor = Player;
                if (_playerHull == null && Player != null) _playerHull = Player.GetComponent<Collider>();
                _grid = Game.Ship.ShipHitPoints.GridOf(Player);
                _hasCell = _grid != null && Game.Ship.ShipHitPoints.TryPick(_grid, transform.position, Game.Ship.HitArc.Top, out _cell);
            }
            Vector3 aim = Player.position + Vector3.up * 0.8f;
            if (!_hasCell || _grid == null) return aim;
            Vector3 c = Game.Ship.ShipHitPoints.Point(_grid, _cell);
            return new Vector3(c.x, aim.y, c.z);
        }

        protected override void UpdateBehaviour(float dt)
        {
            Vector3 aim = AimPoint();
            Vector3 flat = aim - transform.position;
            flat.y = 0f;

            // PreferredRange = 급강하를 시작하는 수평 거리
            if (!_diving && flat.magnitude <= Definition.PreferredRange)
            {
                _diving = true;
                AudioManager.Play(Game.Data.SfxId.DroneBuzz, transform.position);   // 돌입 엔진음(2026-11-16)
            }

            if (_diving)
                Fly(aim, Definition.MoveSpeed * diveSpeedMultiplier, Definition.TurnRateDegPerSec, true, dt);
            else
                Fly(new Vector3(aim.x, cruiseAltitude, aim.z), Definition.MoveSpeed, Definition.TurnRateDegPerSec, false, dt);

            // 모델 노드는 Blender 축이라 로컬 축으로 돌리면 틀어진다. 월드 기준 진행 방향 축으로 돌린다.
            if (propeller != null) propeller.Rotate(transform.forward, propellerRpm * 6f * dt, Space.World);

            Vector3 surface = _playerHull != null ? _playerHull.ClosestPoint(transform.position) : Player.position;
            if ((surface - transform.position).sqrMagnitude <= detonateDistance * detonateDistance)
            {
                Detonate(surface);
                return;
            }

            // 빗나가 바다에 처박히면 그냥 사라진다
            if (transform.position.y < 0f) Crash();
        }

        private void Detonate(Vector3 hitPoint)
        {
            if (Player.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
            {
                // 겨냥 칸이 있으면 그 칸에 맞은 것으로(선체 판정 상자 표면은 칸 경계와 어긋날 수 있다)
                Vector3 at = _hasCell && _grid != null ? Game.Ship.ShipHitPoints.Point(_grid, _cell) : hitPoint;
                target.TakeDamage(new DamageInfo(Definition.AttackDamage, at, transform.forward, DamageSource.Missile));
            }

            PooledEffect.Spawn(blastEffect, transform.position);
            Game.View.Explosions.Spawn(hitPoint, 1.2f, Game.View.Explosions.Kind.Impact);
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 0.8f);
            RemoveWithoutReward();
        }

        private void Crash()
        {
            PooledEffect.Spawn(blastEffect, transform.position);
            Game.View.Explosions.Spawn(transform.position, 0.9f, Game.View.Explosions.Kind.Water);
            RemoveWithoutReward();
        }

        /// <summary>격추되면 공중에서 터진다.</summary>
        protected override void Die()
        {
            PooledEffect.Spawn(blastEffect, transform.position);
            Game.View.Explosions.Spawn(transform.position, 0.8f, Game.View.Explosions.Kind.Air);
            base.Die();
        }
    }
}
