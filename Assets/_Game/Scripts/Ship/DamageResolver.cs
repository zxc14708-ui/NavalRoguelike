using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Modules;
using Game.Modules.Runtime;

namespace Game.Ship
{
    /// <summary>
    /// 피격 지점 -> 격자 셀 -> 모듈/선체 피해 분배 -> 장갑 감산의 단일 경로.
    /// "어디에 맞았는가"가 결과를 바꾸는 규칙은 전부 여기에 모은다.
    /// </summary>
    public class DamageResolver : MonoBehaviour
    {
        [SerializeField] private ShipGrid grid;
        [SerializeField] private ShipController ship;
        [SerializeField] private ShipSystems systems;
        [SerializeField] private BalanceConfig balance;

        /// <summary>피해를 셀 기준으로 해석해 모듈과 선체에 나눠 적용한다.</summary>
        public void Resolve(in DamageInfo info)
        {
            if (grid == null || ship == null)
            {
                Debug.LogError("[DamageResolver] grid 또는 ship 미할당.", this);
                return;
            }

            var coord = HitCell(info.HitPoint);
            var target = grid.Get(coord);
            float amount = info.Amount * RunUpgrades.IncomingDamageMultiplier;   // 성장 카드: 방어력
            bool wasDestroyed = target != null && target.IsDestroyed;

            // 1) 손상 통제반이 함선 전체 피해를 먼저 줄인다
            if (systems != null) amount *= 1f - systems.DamageReduction;
            // 강화 구획은 유폭에만 추가 방호. 직격탄은 막지 않는다.
            if (info.Source == DamageSource.CookOff && ModuleSynergy.ProtectedByRepair(grid, target))
                amount *= 0.75f;

            // 2) 맞은 칸에 모듈이 있으면 일정 비율을 모듈이 받는다
            float toHull = amount;

            if (target != null && target.IsOperational)
            {
                float split = balance != null ? balance.ModuleDamageShare : 0.6f;
                float toModule = amount * split;
                toHull = amount - toModule;
                target.TakeDamage(toModule);
            }

            // 3) 남은 피해는 선체로
            ship.ApplyHullDamage(toHull);

            CombatLog.Add("피격", $"{info.Source} {info.Amount:0.#} → 칸 ({coord.X},{coord.Z}) " +
                                  (target == null ? "빈 칸"
                                   : wasDestroyed ? $"{target.Definition.DisplayName}(이미 파괴)"
                                   : $"{target.Definition.DisplayName} {amount - toHull:0.#}{(target.IsDestroyed ? " → 파괴" : "")}") +
                                  $" · 선체 {toHull:0.#}");
        }

        /// <summary>
        /// 맞은 칸(2026-10-09). 피격 판정 상자는 블록 격자 경계를 감싸는 직사각형이라 맞은 지점은 늘 바깥 표면에 있고,
        /// 미사일·자폭 보트·드론은 표면 밖에서 터진다. 가장 가까운 칸을 반올림만 하면 가장자리 블록 대신 바깥 빈 칸
        /// (또는 모양이 들쭉날쭉한 함체의 빈 칸)이 잡혀 피해가 전부 선체로 갔다 — 블록 내구와 손상 통제 배치가 의미를 잃었다.
        /// 반올림한 칸이 비어 있으면 맞은 지점에서 가장 가까운 블록 칸(칸 사각형까지 거리)을 쓴다 — 피해가 함선에 닿았으면
        /// 늘 어떤 블록이 받는다(판정 상자의 블록 없는 모서리에 맞아도). 파괴된 블록 칸에 맞으면 예전처럼 선체가 다 받는다.
        /// </summary>
        public GridCoord HitCell(Vector3 hitPoint)
        {
            var coord = grid.WorldToNearestCoord(hitPoint);
            if (grid.Get(coord) != null) return coord;

            var local = grid.transform.InverseTransformPoint(hitPoint);
            float half = grid.CellSize * 0.5f;
            float best = float.MaxValue;
            foreach (var c in grid.OccupiedCells)
            {
                var p = grid.CoordToLocal(c);
                float dx = Mathf.Max(0f, Mathf.Abs(local.x - p.x) - half);
                float dz = Mathf.Max(0f, Mathf.Abs(local.z - p.z) - half);
                float d = dx * dx + dz * dz;
                if (d >= best) continue;
                best = d;
                coord = c;
            }
            return coord;
        }

        /// <summary>
        /// 탄약고 유폭. 맨해튼 반경 안 모듈에 거리 감쇠 피해를 주고, 선체에는 한 번만 damage × hullShare.
        /// 같은 모듈이 여러 칸을 차지해도 한 번만 맞는다. 반환: (피해 입은 모듈 수, 이번에 파괴된 모듈 수).
        /// </summary>
        public (int damaged, int destroyed) ApplyCookOff(GridCoord origin, float damage, int radiusCells, float hullShare = 0.3f)
        {
            if (grid == null) return (0, 0);

            var hit = new System.Collections.Generic.Dictionary<ModuleInstance, float>();
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (int dz = -radiusCells; dz <= radiusCells; dz++)
                {
                    var c = new GridCoord(origin.X + dx, origin.Z + dz);
                    int dist = GridCoord.ManhattanDistance(origin, c);
                    if (dist > radiusCells) continue;

                    var m = grid.Get(c);
                    if (m == null || m.IsDestroyed) continue;

                    float dmg = damage * (1f - (float)dist / (radiusCells + 1));
                    if (!hit.TryGetValue(m, out float prev) || dmg > prev) hit[m] = dmg;
                }
            }

            int damaged = 0, destroyed = 0;
            foreach (var kv in hit)
            {
                damaged++;
                float protectedDamage = ModuleSynergy.ProtectedByRepair(grid, kv.Key) ? kv.Value * 0.75f : kv.Value;
                kv.Key.TakeDamage(protectedDamage);   // 탄약고라면 여기서 연쇄 유폭
                if (kv.Key.IsDestroyed) destroyed++;
            }
            ship?.ApplyHullDamage(damage * hullShare * (ModuleSynergy.ProtectedByRepair(grid, origin) ? 0.75f : 1f));
            return (damaged, destroyed);
        }
    }
}
