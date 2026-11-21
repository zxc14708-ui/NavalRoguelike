using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 손상 통제 작업. 선체를 서서히 수리하고, 반경 안에서 가장 많이 상한 모듈 하나를 고친다.
    /// 손상통제반과 함교가 같은 규칙을 쓰도록 부품으로 분리했다(수치만 다르다).
    /// 부서진 블록도 고친다 — 쌓인 수리가 최대 내구의 30%에 닿으면 다시 작동한다(2026-11-21, ModuleInstance.Repair).
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
        /// <summary>지금 고치고 있는 블록(다 찼으면 null) — 작동 연출이 읽는다.</summary>
        public ModuleInstance Target => _target != null && _target.Hp < _target.MaxHp - 0.01f ? _target : null;
        private float _retargetTimer;

        /// <param name="hullCap">선체 자동 회복 상한(최대 선체 대비 0~1). 넘으면 고치지 않는다(2026-10-11 손상통제 개편).</param>
        public void Tick(ShipController ship, ShipGrid grid, ModuleInstance self,
                         float hullPerSecond, float modulePerSecond, float dt, float hullCap = 1f)
        {
            if (ship == null) return;

            float capHp = ship.HullMaxHp * Mathf.Clamp01(hullCap);
            if (hullPerSecond > 0f && ship.HullHp < capHp)
            {
                bool underFire = Time.time - ship.LastHullDamageTime < underFireWindow;
                float amount = hullPerSecond * (underFire ? underFireHullRepairRatio : 1f) * dt;
                ship.RepairHull(Mathf.Min(amount, capHp - ship.HullHp));
            }
            if (modulePerSecond <= 0f) return;

            _retargetTimer -= dt;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = retargetInterval;
                _target = FindMostDamaged(grid, self);
            }

            // 2026-11-21: 블록 수리도 교전 중에는 선체와 같은 비율로 줄인다 — 맞는 위치가 퍼진 뒤 0.8/초 수리가 블록마다 오는 피해를 거의 다 지워
            //             자동 검사에서 6차례 일제사격(블록이 받은 피해 1,122)에도 블록이 하나도 부서지지 않았다. 교전이 끝나면 예전 속도로 고친다.
            bool moduleUnderFire = Time.time - ship.LastHullDamageTime < underFireWindow;
            _target?.Repair(modulePerSecond * (moduleUnderFire ? underFireHullRepairRatio : 1f) * dt);
        }

        private ModuleInstance FindMostDamaged(ShipGrid grid, ModuleInstance self)
        {
            if (grid == null) return null;

            ModuleInstance worst = null;
            float worstRatio = 1f;

            foreach (var m in grid.Modules)
            {
                if (m == null || m.Definition == null) continue;   // 부서진 블록도 고친다(30% 차면 다시 작동, 2026-11-21)
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
