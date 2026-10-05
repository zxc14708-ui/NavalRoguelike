using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;

namespace Game.Enemies
{
    /// <summary>
    /// 기뢰부설정(일반 수상 적). 함선 진로 앞쪽 옆으로 나아가며 함미 투하 슈트로 부유 기뢰(<see cref="SeaMine"/>)를 뿌린다.
    ///
    ///   - 이동: 함선이 지금 침로로 가면 지날 곳(leadDistance 앞) 옆 laneOffset에 붙어 같은 방향으로 달린다 — 기뢰가 진로에 깔린다.
    ///   - 투하: AttackCooldown마다 한 개, 투하점(MineDropPoint_01·02)을 번갈아. 이 배가 깐 기뢰는 동시에 maxMinesAlive개까지,
    ///     모든 기뢰는 globalMineCap개까지.
    ///   - 대응: 진로 바꾸기, 기관포·76mm로 기뢰를 쏘아 없애기, 부설정을 먼저 격침.
    ///   선수 소형 포탑은 표적을 따라 돌 뿐 쏘지 않는다.
    /// </summary>
    public class MineLayer : EnemyController
    {
        [Header("Mines")]
        [SerializeField] private EnemyDefinition mineDefinition;
        [SerializeField] private Transform[] dropPoints;
        [SerializeField] private int maxMinesAlive = 6;
        [SerializeField] private int globalMineCap = 12;
        [Tooltip("함선 앞 이 거리 지점 옆을 달린다")]
        [SerializeField] private float leadDistance = 32f;
        [SerializeField] private float laneOffset = 10f;
        [Tooltip("함선에서 이 거리 안일 때만 깐다(멀리서 까면 쓸모가 없다)")]
        [SerializeField] private float layRange = 55f;

        [Header("Bow Turret (표적 추적만)")]
        [SerializeField] private WeaponController weapon = new();
        [SerializeField] private float turretTurnRate = 140f;

        private static readonly List<SeaMine> s_allMines = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_allMines.Clear();

        private readonly List<SeaMine> _mines = new();
        private Game.Ship.ShipController _playerShip;
        private float _side;
        private int _nextDrop;

        public int MinesLaid { get; private set; }

        /// <summary>기뢰는 기함 진로에 깐다.</summary>
        protected override bool TargetsEscorts => false;

        public override void Setup(EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _playerShip = player != null ? player.GetComponent<Game.Ship.ShipController>() : null;
            _side = Random.value < 0.5f ? -1f : 1f;
            _mines.Clear();
            MinesLaid = 0;
            if (def != null) AttackTimer = def.AttackCooldown;
        }

        protected override void UpdateBehaviour(float dt)
        {
            weapon.Tick(dt);
            weapon.AimAt(Player.position + Vector3.up * 0.8f, turretTurnRate, dt);

            // 함선 진로 앞 옆 차선으로
            Vector3 fwd = Player.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float speed = _playerShip != null ? _playerShip.CurrentSpeed : 0f;
            Vector3 lane = Player.position + fwd * (leadDistance + speed * 1.5f) + right * (laneOffset * _side);
            Vector3 to = lane - transform.position; to.y = 0f;
            float d = to.magnitude;
            // 차선에 가까우면 함선과 같은 방향으로 나란히(기뢰가 진로 위에 줄지어 깔리게)
            Vector3 heading = d > 8f ? to : Vector3.Lerp(fwd, to.normalized, d / 8f);
            Steer(heading, d > 8f ? 1f : 0.7f, dt);

            _mines.RemoveAll(m => m == null || !m.IsAlive);
            s_allMines.RemoveAll(m => m == null || !m.IsAlive);
            if (AttackTimer > 0f || mineDefinition == null) return;
            if ((transform.position - Player.position).sqrMagnitude > layRange * layRange) return;
            if (_mines.Count >= maxMinesAlive || s_allMines.Count >= globalMineCap) return;
            DropMine();
            AttackTimer = Definition.AttackCooldown;
        }

        private void DropMine()
        {
            var spawner = EnemySpawner.Instance;
            if (spawner == null) return;
            Transform point = dropPoints != null && dropPoints.Length > 0 ? dropPoints[_nextDrop++ % dropPoints.Length] : null;
            Vector3 p = point != null ? point.position : transform.position - transform.forward * 3.5f;
            p -= transform.forward * 0.8f;   // 함미 슈트 뒤 수면
            p.y = 0f;
            var mine = spawner.SpawnAt(mineDefinition, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)) as SeaMine;
            if (mine == null) return;
            _mines.Add(mine);
            s_allMines.Add(mine);
            MinesLaid++;
            AudioManager.Play(Game.Data.SfxId.HullImpact, p, 0.35f, 0.6f);
        }
    }
}
