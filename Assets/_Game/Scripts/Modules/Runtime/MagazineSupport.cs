using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 탄약고 근접 보너스 계산. 함내 탄약 물류를 흉내 낸다 — 탄약고에서 가까운(맨해튼 거리 SupportRadiusCells 이내) 포탄 무기일수록 빨리 채운다.
    ///   - 기관포·76mm: 발사 간격 −ReloadBonus, 탄약 용량 +AmmoCapacityBonus, 보급 속도 +ResupplyBonus
    ///   - 유도로켓: 발사 간격만(예전 규칙 유지. 미사일 계통이라 탄약 보너스는 없다)
    ///   - CIWS·VLS·함대공·폭뢰: 지원하지 않는다(탄약고를 쌓아 최종 방어를 무한히 만들지 않게)
    /// 여러 탄약고가 겹쳐도 가장 좋은 하나만 적용한다(도배해도 무한 탄약이 되지 않게).
    /// 가까이 둘수록 강하지만 파괴되면 유폭으로 그 무기까지 다친다(MagazineModule).
    /// </summary>
    public static class MagazineSupport
    {
        public readonly struct Bonus
        {
            public readonly float ReloadMultiplier;     // 발사 간격 배율(1 = 없음)
            public readonly float CapacityMultiplier;   // 탄약 용량 배율
            public readonly float ResupplyMultiplier;   // 보급 속도 배율

            public Bonus(float reload, float capacity, float resupply)
            {
                ReloadMultiplier = reload;
                CapacityMultiplier = capacity;
                ResupplyMultiplier = resupply;
            }

            public static Bonus None => new(1f, 1f, 1f);
            public bool Any => ReloadMultiplier < 1f || CapacityMultiplier > 1f || ResupplyMultiplier > 1f;
        }

        /// <summary>재장전 배율. 1.0 = 보너스 없음.</summary>
        public static float ReloadMultiplier(ShipGrid grid, ModuleInstance self) => For(grid, self, false).ReloadMultiplier;

        /// <summary>가장 좋은 탄약고 하나의 보너스. includeAmmo가 false면 발사 간격만.</summary>
        public static Bonus For(ShipGrid grid, ModuleInstance self, bool includeAmmo)
        {
            if (grid == null || self == null) return Bonus.None;

            ModuleStats best = default;
            bool found = false;
            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational || m.Definition.Type != ModuleType.Magazine) continue;

                int dist = GridCoord.ManhattanDistance(self.Origin, m.Origin);
                var s = m.EffectiveStats;   // 탄약고 강화가 반영된 지원량(무기 성능 전체가 아니라 지원량 자체만 커진다)
                if (dist > s.SupportRadiusCells) continue;

                if (!found || s.ReloadBonus + s.AmmoCapacityBonus + s.ResupplyBonus > best.ReloadBonus + best.AmmoCapacityBonus + best.ResupplyBonus)
                {
                    best = s;
                    found = true;
                }
            }
            if (!found) return Bonus.None;

            return new Bonus(1f - best.ReloadBonus,
                             includeAmmo ? 1f + best.AmmoCapacityBonus : 1f,
                             includeAmmo ? 1f + best.ResupplyBonus : 1f);
        }
    }

    /// <summary>작동 중인 블록의 실제 점유 칸을 기준으로 직교 인접을 판정한다.</summary>
    public static class ModuleSynergy
    {
        /// <summary>설치하지 않은 후보의 점유 칸으로 시너지 결과를 미리 계산한다. 실제 격자는 변경하지 않는다.</summary>
        public static string Preview(ShipGrid grid, ModuleDefinition definition, GridCoord origin, int rotation)
        {
            if (grid == null || definition == null || !grid.CanPlace(definition, origin, rotation, out _)) return null;
            var cells = new List<GridCoord>();
            ShipGrid.GetFootprint(definition, origin, rotation, cells);
            var type = definition.Type;
            var result = new StringBuilder();

            bool Has(ModuleType wanted)
            {
                foreach (var m in grid.Modules)
                    if (m != null && m.IsOperational && m.Definition.Type == wanted && Touches(cells, m.OccupiedCoords)) return true;
                return false;
            }
            bool Neighbors(ModuleInstance module) => module != null && module.IsOperational && Touches(cells, module.OccupiedCoords);
            void Add(string value) { if (result.Length > 0) result.Append("  ·  "); result.Append(value); }

            if (type == ModuleType.HelicopterDeck && Has(ModuleType.HelicopterDeck)) Add("헬기 편대: 재출격 -20%");
            if ((type == ModuleType.Sonar && (Has(ModuleType.HelicopterDeck) || Has(ModuleType.AswLauncher) || Has(ModuleType.TorpedoTube))) ||
                (type == ModuleType.HelicopterDeck || type == ModuleType.AswLauncher || type == ModuleType.TorpedoTube) && Has(ModuleType.Sonar))
                Add("대잠 연계: 접촉 확정 1.2초");
            if (type == ModuleType.EwSuite && Has(ModuleType.DecoyLauncher) ||
                type == ModuleType.DecoyLauncher && Has(ModuleType.EwSuite))
                Add("전자전 연계: 교란 +20%");

            if (type == ModuleType.Radar && Has(ModuleType.SamLauncher) && Has(ModuleType.Ciws))
                Add("통합 방공: SAM 원거리 우선");
            else if (type == ModuleType.SamLauncher || type == ModuleType.Ciws)
                foreach (var radar in grid.Modules)
                    if (radar != null && radar.IsOperational && radar.Definition.Type == ModuleType.Radar && Neighbors(radar) &&
                        Adjacent(grid, radar, type == ModuleType.SamLauncher ? ModuleType.Ciws : ModuleType.SamLauncher))
                    { Add("통합 방공: SAM 원거리 우선"); break; }

            if (type == ModuleType.RepairBay || Has(ModuleType.RepairBay))
            {
                bool repairActive = false;
                foreach (var repair in grid.Modules)
                {
                    if (repair == null || !repair.IsOperational || repair.Definition.Type != ModuleType.RepairBay || !Neighbors(repair)) continue;
                    int count = 1; // 배치 후보
                    foreach (var other in grid.Modules)
                        if (other != repair && other != null && other.IsOperational && Touches(repair, other)) count++;
                    if (count >= 2) { repairActive = true; break; }
                }
                if (type == ModuleType.RepairBay)
                {
                    int count = 0;
                    foreach (var other in grid.Modules) if (Neighbors(other)) count++;
                    if (count >= 2) repairActive = true;
                }
                if (repairActive) Add("손상통제 거점: 주변 수리 +15%");
            }

            if (type == ModuleType.NavalGun || type == ModuleType.Magazine)
            {
                ModuleInstance At(GridCoord cell)
                {
                    foreach (var occupied in cells) if (occupied.Equals(cell)) return null; // 후보 위치
                    return grid.Get(cell);
                }
                bool IsAt(GridCoord cell, ModuleType wanted)
                {
                    foreach (var occupied in cells) if (occupied.Equals(cell)) return type == wanted;
                    var m = At(cell);
                    return m != null && m.IsOperational && m.Definition.Type == wanted;
                }
                var guns = new List<GridCoord>();
                foreach (var m in grid.Modules)
                    if (m != null && m.IsOperational && m.Definition.Type == ModuleType.NavalGun) guns.Add(m.Origin);
                if (type == ModuleType.NavalGun) guns.Add(origin);
                bool battery = false;
                foreach (var gun in guns)
                {
                    foreach (var dir in GridCoord.Neighbors)
                    {
                        var mag = gun + dir;
                        var other = mag + dir;
                        if (!IsAt(mag, ModuleType.Magazine) || !IsAt(other, ModuleType.NavalGun)) continue;
                        if (type == ModuleType.Magazine && !cells.Contains(mag)) continue;
                        if (type == ModuleType.NavalGun && !cells.Contains(gun) && !cells.Contains(other)) continue;
                        battery = true;
                        break;
                    }
                    if (battery) break;
                }
                if (battery) Add("포대 연계: 교차 사격·보급 +10%p");
            }
            return result.Length > 0 ? result.ToString() : null;
        }

        /// <summary>장갑 블록은 폐지되어 손상통제반이 두 구획 사이에 놓이면 방호 거점이 된다.</summary>
        public static bool Fortified(ShipGrid grid, ModuleInstance repair)
        {
            if (grid == null || repair == null || !repair.IsOperational || repair.Definition.Type != ModuleType.RepairBay) return false;
            int neighbours = 0;
            foreach (var candidate in grid.Modules)
            {
                if (candidate == null || candidate == repair || !candidate.IsOperational) continue;
                foreach (var ac in repair.OccupiedCoords)
                {
                    bool touches = false;
                    foreach (var bc in candidate.OccupiedCoords)
                        if (GridCoord.ManhattanDistance(ac, bc) == 1) { touches = true; break; }
                    if (!touches) continue;
                    if (++neighbours >= 2) return true;
                    break;
                }
            }
            return false;
        }

        public static bool ProtectedByRepair(ShipGrid grid, GridCoord cell)
        {
            if (grid == null) return false;
            foreach (var repair in grid.Modules)
            {
                if (!Fortified(grid, repair)) continue;
                foreach (var c in repair.OccupiedCoords)
                    if (GridCoord.ManhattanDistance(c, cell) <= 1) return true;
            }
            return false;
        }

        public static bool ProtectedByRepair(ShipGrid grid, ModuleInstance module)
        {
            if (module == null) return false;
            foreach (var cell in module.OccupiedCoords)
                if (ProtectedByRepair(grid, cell)) return true;
            return false;
        }

        public static bool IntegratedAirDefense(ShipGrid grid, ModuleInstance sam)
        {
            if (grid == null || sam == null || !sam.IsOperational) return false;
            foreach (var radar in grid.Modules)
                if (radar != null && radar.IsOperational && radar.Definition.Type == ModuleType.Radar &&
                    Touches(radar, sam) && Adjacent(grid, radar, ModuleType.Ciws))
                    return true;
            return false;
        }

        public static bool Touches(ModuleInstance a, ModuleInstance b)
        {
            return Touches(a.OccupiedCoords, b.OccupiedCoords);
        }

        public static bool Touches(IReadOnlyList<GridCoord> a, IReadOnlyList<GridCoord> b)
        {
            foreach (var ac in a)
                foreach (var bc in b)
                    if (GridCoord.ManhattanDistance(ac, bc) == 1) return true;
            return false;
        }

        public static bool Adjacent(ShipGrid grid, ModuleInstance self, ModuleType type)
        {
            if (grid == null || self == null || !self.IsOperational) return false;
            foreach (var candidate in grid.Modules)
            {
                if (candidate == null || candidate == self || !candidate.IsOperational || candidate.Definition.Type != type) continue;
                if (Touches(self, candidate)) return true;
            }
            return false;
        }
    }
}
