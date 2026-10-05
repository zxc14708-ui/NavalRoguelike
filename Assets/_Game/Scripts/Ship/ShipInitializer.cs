using UnityEngine;
using Game.Data;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 게임 시작 시 StartingLoadout대로 초기 모듈을 설치한다.
    /// 시작 구성이 코드가 아니라 에셋에 있으므로 밸런스 실험이 쉽다.
    /// </summary>
    public class ShipInitializer : MonoBehaviour
    {
        [SerializeField] private StartingLoadout loadout;

        /// <summary>시작 모듈 배치(사전이 함교 등 카드에 없는 블록을 읽는다).</summary>
        public StartingLoadout Loadout => loadout;
        [SerializeField] private ModuleFactory factory;
        [SerializeField] private ShipSystems systems;

        /// <summary>
        /// 씬에 스킬 컴포넌트가 빠져 있으면 붙인다. 예전에 세 스킬이 한 파일(SupportSkills.cs)에 있어
        /// 씬에 하나만 저장됐던 문제를 셋업을 다시 돌리지 않고도 바로잡는다.
        /// </summary>
        private void Awake()
        {
            var ship = GetComponent<ShipController>();
            var grid = ship != null ? ship.Grid : GetComponentInChildren<ShipGrid>();
            if (grid == null) return;

            EnsureSkill<RepairBurstSkill>(grid);
            EnsureSkill<SmokeSkill>(grid);
            EnsureSkill<FlankSkill>(grid);
        }

        private void EnsureSkill<T>(ShipGrid grid) where T : Component
        {
            if (GetComponent<T>() != null) return;
            var skill = gameObject.AddComponent<T>();
            switch (skill)
            {
                case RepairBurstSkill r: r.Bind(grid); break;
                case SmokeSkill s: s.Bind(grid); break;
                case FlankSkill f: f.Bind(grid); break;
            }
        }

        private void Start()
        {
            if (loadout == null || factory == null)
            {
                Debug.LogError("[ShipInitializer] loadout 또는 factory 미할당.", this);
                return;
            }

            foreach (var e in loadout.Entries)
            {
                if (e.Module == null) continue;

                var inst = factory.Install(e.Module, e.Origin, e.RotationSteps);
                if (inst == null)
                    Debug.LogError($"[ShipInitializer] 시작 모듈 '{e.Module.DisplayName}' 배치 실패 at {e.Origin}. " +
                                   "StartingLoadout 좌표와 PlacementRule을 확인하세요.", this);
            }

            systems?.Recalculate();
        }
    }
}
