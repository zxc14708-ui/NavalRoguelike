using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 손상 통제 작업. 선체를 서서히 수리하고, 반경 안에서 가장 많이 상한 모듈 하나를 고친다.
    /// 손상통제반과 함교가 같은 규칙을 쓰도록 부품으로 분리했다(수치만 다르다).
    /// 파괴된(HP 0) 모듈은 복구하지 않는다.
    /// </summary>
    [System.Serializable]
    public class DamageControlCrew
    {
        [Tooltip("모듈 수리 대상을 다시 고르는 간격(초). 매 프레임 전체를 훑지 않기 위한 것.")]
        [SerializeField] private float retargetInterval = 1f;

        [Tooltip("손을 뻗을 수 있는 거리(칸). 멀리 떨어진 모듈은 못 고친다.")]
        [SerializeField] private int repairRadiusCells = 3;

        [Tooltip("선체가 맞은 뒤 이 시간 동안은 선체 수리 효율이 떨어진다(계속 맞는 동안 수리가 피해를 상쇄하지 않게)")]
        [SerializeField] private float underFireWindow = 3f;
        [SerializeField, Range(0f, 1f)] private float underFireHullRepairRatio = 0.25f;

        private ModuleInstance _target;
        private float _retargetTimer;

        public void Tick(ShipController ship, ShipGrid grid, ModuleInstance self,
                         float hullPerSecond, float modulePerSecond, float dt)
        {
            if (ship == null) return;

            if (hullPerSecond > 0f)
            {
                bool underFire = Time.time - ship.LastHullDamageTime < underFireWindow;
                ship.RepairHull(hullPerSecond * (underFire ? underFireHullRepairRatio : 1f) * dt);
            }
            if (modulePerSecond <= 0f) return;

            _retargetTimer -= dt;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = retargetInterval;
                _target = FindMostDamaged(grid, self);
            }

            _target?.Repair(modulePerSecond * dt);
        }

        private ModuleInstance FindMostDamaged(ShipGrid grid, ModuleInstance self)
        {
            if (grid == null) return null;

            ModuleInstance worst = null;
            float worstRatio = 1f;

            foreach (var m in grid.Modules)
            {
                if (m == null || m.IsDestroyed || m.Definition == null) continue;
                if (self != null && GridCoord.ManhattanDistance(self.Origin, m.Origin) > repairRadiusCells) continue;

                float ratio = m.Hp / Mathf.Max(1f, m.MaxHp);
                if (ratio >= 1f || ratio >= worstRatio) continue;

                worstRatio = ratio;
                worst = m;
            }
            return worst;
        }
    }
}
