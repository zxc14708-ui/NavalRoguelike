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

        /// <summary>검사용 기록 한 줄(2026-11-21 피격 분포 측정 -hitMapOnly).</summary>
        public struct HitRecord
        {
            public DamageInfo Info;
            public GridCoord Cell;
            public ModuleInstance Module;
            public bool WasDestroyed, DestroyedNow;
            public float ToModule, ToHull;
        }

        /// <summary>피해 하나를 해석할 때마다(검사 · 진단용 — 평소엔 구독자 없음).</summary>
        public static event System.Action<DamageResolver, HitRecord> Resolved;

        /// <summary>검사용: 켜면 맞은 칸만 기록하고 피해는 주지 않는다(맞는 위치 분포만 잴 때).</summary>
        public static bool DevMeasureOnly;

        /// <summary>검사용: 0 이상이면 블록이 받는 몫을 이 값으로(밸런스 비교 -balanceSimOnly). 원본 BalanceConfig는 건드리지 않는다.</summary>
        public static float DevModuleShareOverride = -1f;

        public ShipGrid Grid => grid;

        private void Awake()
        {
            // 손상 통제 1단계(2026-11-21): 블록 격자가 있는 기함에 침수 · 기울기를 붙인다
            if (!TryGetComponent<ShipFlooding>(out _)) gameObject.AddComponent<ShipFlooding>();
            if (!TryGetComponent<ShipFire>(out _)) gameObject.AddComponent<ShipFire>();               // 2단계 화재 · 탄약고
            if (!TryGetComponent<ShipDamageControl>(out _)) gameObject.AddComponent<ShipDamageControl>();   // 손상 통제반 자동 배치
            if (!TryGetComponent<Game.View.DamageControlFx>(out _)) gameObject.AddComponent<Game.View.DamageControlFx>();   // 작동 연출(경광등 · 용접 불꽃 · 물줄기)
        }

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
            if (DevMeasureOnly)
            {
                Resolved?.Invoke(this, new HitRecord { Info = info, Cell = coord, Module = target, WasDestroyed = target != null && target.IsDestroyed });
                return;
            }
            float amount = info.Amount * RunUpgrades.IncomingDamageMultiplier * ship.IncomingDamageScale;   // 성장 카드: 방어력 · 스킬 배율(지금은 1)
            bool wasDestroyed = target != null && target.IsDestroyed;

            // 1) 손상 통제반이 함선 전체 피해를 먼저 줄인다
            if (systems != null) amount *= 1f - systems.DamageReduction;
            amount *= 1f - ship.HullArmor;   // 함종 장갑(WWII 패치 C1, 현대는 0)
            // 강화 구획은 유폭에만 추가 방호. 직격탄은 막지 않는다.
            if (info.Source == DamageSource.CookOff && ModuleSynergy.ProtectedByRepair(grid, target))
                amount *= 0.75f;

            // 2) 맞은 칸에 모듈이 있으면 일정 비율을 모듈이 받는다
            float toHull = amount;
            ModuleInstance spillTo = null;

            if (target != null && target.IsOperational)
            {
                float split = DevModuleShareOverride >= 0f ? DevModuleShareOverride : balance != null ? balance.ModuleDamageShare : 0.6f;
                float toModule = amount * split;
                toHull = amount - toModule;
                target.TakeDamage(toModule);
            }
            else if (target != null && target.IsDestroyed)
            {
                // 2-1) 이미 부서진 칸(2026-11-21): 예전에는 선체가 다 받아 그 칸이 "선체만 깎는 칸"이 됐다(자동 검사 실제 피해의 74%).
                //      파편 · 화염이 옆으로 번지듯 가장 가까운 살아 있는 블록(맨해튼 2칸 안)이 절반을 받는다. 없으면 예전처럼 선체가 다 받는다.
                spillTo = NearestLiveModule(coord, SpillRadius);
                if (spillTo != null)
                {
                    float toModule = amount * DestroyedCellSpill;
                    toHull = amount - toModule;
                    spillTo.TakeDamage(toModule);
                }
            }

            // 3) 남은 피해는 선체로
            ship.ApplyHullDamage(toHull);
            Resolved?.Invoke(this, new HitRecord { Info = info, Cell = coord, Module = spillTo ?? target, WasDestroyed = wasDestroyed,
                DestroyedNow = spillTo != null ? spillTo.IsDestroyed : target != null && !wasDestroyed && target.IsDestroyed, ToModule = amount - toHull, ToHull = toHull });

            CombatLog.Add("피격", $"{info.Source} {info.Amount:0.#} → 칸 ({coord.X},{coord.Z}) " +
                                  (target == null ? "빈 칸"
                                   : wasDestroyed ? $"{target.Definition.DisplayName}(이미 파괴){(spillTo != null ? $" → 옆 {spillTo.Definition.DisplayName} {amount - toHull:0.#}{(spillTo.IsDestroyed ? " → 파괴" : "")}" : "")}"
                                   : $"{target.Definition.DisplayName} {amount - toHull:0.#}{(target.IsDestroyed ? " → 파괴" : "")}") +
                                  $" · 선체 {toHull:0.#}");
        }

        /// <summary>부서진 칸에 맞았을 때 옆 블록이 받는 몫(나머지는 선체) · 찾는 거리(맨해튼 칸).</summary>
        public const float DestroyedCellSpill = 0.5f;
        public const int SpillRadius = 2;

        private static readonly System.Collections.Generic.List<ModuleInstance> s_spill = new();

        /// <summary>origin에서 맨해튼 거리 1~radius 안의 가장 가까운 작동 중 블록(같은 거리면 아무거나). 없으면 null.</summary>
        public ModuleInstance NearestLiveModule(GridCoord origin, int radius)
        {
            for (int d = 1; d <= radius; d++)
            {
                s_spill.Clear();
                for (int dx = -d; dx <= d; dx++)
                {
                    int rest = d - Mathf.Abs(dx);
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        var m = grid.Get(new GridCoord(origin.X + dx, origin.Z + sgn * rest));
                        if (m != null && m.IsOperational && !s_spill.Contains(m)) s_spill.Add(m);
                        if (rest == 0) break;
                    }
                }
                if (s_spill.Count > 0) return s_spill[Random.Range(0, s_spill.Count)];
            }
            return null;
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
            ship?.ApplyHullDamage(damage * hullShare * (ModuleSynergy.ProtectedByRepair(grid, origin) ? 0.75f : 1f), "탄약고 유폭", hostile: false);
            return (damaged, destroyed);
        }
    }
}
