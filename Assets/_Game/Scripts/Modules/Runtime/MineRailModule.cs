using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 선미 기뢰 투하궤(2026-10-08). 뒤가 트인 자리에만 놓인다(PlacementZone.SternOnly).
    /// 함선이 움직이는 동안 뒤쪽(선미 ±chaseHalfArc)에서 쫓아오는 적 수상함이 chaseRange 안에 있으면
    /// 항적에 아군 기뢰(PlayerMine)를 떨어뜨린다 — 추격해 오는 자폭 보트·고속정을 막는 후방 함정형 무기.
    /// 뒤에 다른 블록이 붙어 뒤가 막히면 떨어뜨리지 않는다(그런 설치는 ShipGrid가 거부한다).
    /// </summary>
    public class MineRailModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject minePrefab;
        [Tooltip("레일 끝(기뢰가 떨어지는 곳). 없으면 블록에서 선미 쪽으로 dropBehind")]
        [SerializeField] private Transform dropPoint;
        [SerializeField] private float dropBehind = 1.6f;
        [Tooltip("이 반경 안 뒤쪽 적이 있을 때만 떨어뜨린다")]
        [SerializeField] private float chaseRange = 30f;
        [Tooltip("선미 기준 이 각도 안을 '뒤쪽'으로 본다")]
        [SerializeField] private float chaseHalfArc = 70f;
        [Tooltip("이 속력(최고 속력 대비) 이상으로 나아갈 때만 — 멈춰 있으면 제 기뢰 위에 떠 있게 된다")]
        [SerializeField] private float minSpeedRatio = 0.15f;
        [Tooltip("레일 위에 놓인 기뢰 모형(떨어뜨릴 때마다 하나씩 숨겼다가 보급되면 다시 보인다)")]
        [SerializeField] private Transform[] rackedMines;

        private readonly AmmoMagazine _ammo = new();
        private float _cooldown, _lastDrop = -999f;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastDrop < 1.5f;
        public int Drops { get; private set; }

        private Vector3 ShipForward
        {
            get
            {
                var f = Grid != null ? Grid.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        public Vector3 DropPosition => dropPoint != null ? dropPoint.position : transform.position - ShipForward * dropBehind;

        protected override void OnInitialized()
        {
            if (minePrefab == null) Debug.LogError("[MineRail] minePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
        }

        /// <summary>뒤가 트여 있는가(뒤에 블록이 붙으면 레일이 막힌다).</summary>
        public bool SternOpen => Grid == null || Instance == null ||
                                 PlacementRuleEvaluator.IsOpen(Grid, Instance.OccupiedCoords, -1, 0);

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            UpdateRack();
            if (_cooldown > 0f) { _cooldown -= dt; return; }
            if (minePrefab == null || !_ammo.CanFire || !SternOpen) return;
            if (Ship == null || Ship.CurrentSpeed < Ship.BaseMaxSpeed * minSpeedRatio) return;
            if (!Chased()) { _cooldown = 0.3f; return; }

            _ammo.Consume();
            var go = PoolManager.Instance?.Spawn(minePrefab, DropPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            go?.GetComponent<PlayerMine>()?.Drop(DropPosition, Stats.Damage,
                Definition != null ? Definition.TargetEfficiency : TargetEfficiency.Neutral);
            Drops++;
            _lastDrop = Time.time;
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime);
            AudioManager.Play(Game.Data.SfxId.EscortTorpedoLaunch, transform.position, 0.6f, 0.5f);   // 레일을 굴러 떨어지는 소리
            CombatLog.Add("기뢰", $"{LogName} 기뢰 투하");
        }

        /// <summary>뒤쪽에서 chaseRange 안의 적 수상함(적 기뢰 제외)이 있는가.</summary>
        private bool Chased()
        {
            Vector3 origin = transform.position;
            Vector3 back = -ShipForward;
            float r2 = chaseRange * chaseRange;
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive || e.Transform == null || e is SeaMine) continue;
                Vector3 d = e.Transform.position - origin;
                d.y = 0f;
                if (d.sqrMagnitude > r2 || d.sqrMagnitude < 0.01f) continue;
                if (Vector3.Angle(back, d) <= chaseHalfArc) return true;
            }
            return false;
        }

        private void UpdateRack()
        {
            if (rackedMines == null) return;
            int shown = _ammo.Infinite ? rackedMines.Length : Mathf.Min(rackedMines.Length, _ammo.Current);
            for (int i = 0; i < rackedMines.Length; i++)
                if (rackedMines[i] != null && rackedMines[i].gameObject.activeSelf != (i < shown))
                    rackedMines[i].gameObject.SetActive(i < shown);
        }
    }
}
