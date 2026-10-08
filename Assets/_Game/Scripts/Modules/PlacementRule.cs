using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// "기관포는 뱃머리에, 기관은 선미에" 처럼 물리적으로 납득 가능한 배치 제약.
    ///
    /// 함체 크기가 고정이 아니므로 절대 좌표로 구역을 나눌 수 없다.
    /// 대신 "앞이 비어 있어야 한다 / 뒤가 비어 있어야 한다"로 판정한다.
    /// 배가 어떤 모양으로 자라든 뱃머리와 선미가 자동으로 따라온다.
    /// </summary>
    public enum PlacementZone
    {
        Anywhere,
        BowOnly,    // 앞쪽이 막히지 않은 자리 = 뱃머리
        SternOnly,  // 뒤쪽이 막히지 않은 자리 = 선미
        SideOrStern, // 옆이나 뒤가 막히지 않은 자리(사방이 막힌 함내는 불가) — 폭뢰(위치에 따라 투하대/발사대)
        SideOnly,    // 좌현이나 우현이 막히지 않은 자리 — 경어뢰 발사관(트인 현측으로 쏜다)
    }

    [Serializable]
    public struct PlacementRule
    {
        public PlacementZone Zone;

        public static PlacementRule Anywhere => new() { Zone = PlacementZone.Anywhere };
    }

    /// <summary>PlacementRule을 실제 격자에 대해 검사한다. 규칙 추가는 여기만 고치면 된다.</summary>
    public static class PlacementRuleEvaluator
    {
        /// <summary>
        /// 놓은 뒤에도 이웃 블록 때문에 막힐 수 있는 규칙(폭뢰·경어뢰). 이런 블록의 트인 쪽을
        /// 모두 막는 설치는 ShipGrid가 거부한다.
        /// </summary>
        public static bool NeedsOpenSide(PlacementZone zone) => zone is PlacementZone.SideOrStern or PlacementZone.SideOnly;

        /// <summary>
        /// 위 규칙 + 앞·뒤가 트여 있어야 작동하는 블록(충각 함수·기뢰 투하궤). 헬기데크(선미만)는 예전처럼 뒤를 막아도 된다.
        /// </summary>
        public static bool NeedsOpenSide(ModuleDefinition def) => def != null &&
            (NeedsOpenSide(def.Placement.Zone) || def.Type is ModuleType.RamBow or ModuleType.MineRail);

        public static bool Evaluate(PlacementRule rule, ShipGrid grid,
                                    IReadOnlyList<GridCoord> coords, out string reason)
        {
            reason = null;
            if (grid == null || coords == null || coords.Count == 0) { reason = "잘못된 배치"; return false; }

            switch (rule.Zone)
            {
                case PlacementZone.Anywhere:
                    return true;

                case PlacementZone.BowOnly:
                    if (!IsOpenTowards(grid, coords, +1)) { reason = "앞이 트인 자리에만 설치 가능"; return false; }
                    return true;

                case PlacementZone.SternOnly:
                    if (!IsOpenTowards(grid, coords, -1)) { reason = "뒤가 트인 자리에만 설치 가능"; return false; }
                    return true;

                case PlacementZone.SideOrStern:
                    if (!IsOpen(grid, coords, -1, 0) && !IsOpen(grid, coords, 0, -1) && !IsOpen(grid, coords, 0, +1))
                    { reason = "옆이나 뒤가 트인 자리에만 설치 가능(사방이 막힌 자리 불가)"; return false; }
                    return true;

                case PlacementZone.SideOnly:
                    if (!IsOpen(grid, coords, 0, -1) && !IsOpen(grid, coords, 0, +1))
                    { reason = "좌현이나 우현이 트인 자리에만 설치 가능"; return false; }
                    return true;

                default:
                    return true;
            }
        }

        private static bool IsOpenTowards(ShipGrid grid, IReadOnlyList<GridCoord> coords, int dirX) => IsOpen(grid, coords, dirX, 0);

        /// <summary>
        /// 발자국의 (dirX, dirZ) 방향 끝 칸 바깥이 모두 비어 있는지(X = 선수, Z = 우현).
        /// 그 방향으로 다른 모듈이 가로막고 있으면 뱃머리(선미·현측)가 아니다.
        /// </summary>
        public static bool IsOpen(ShipGrid grid, IReadOnlyList<GridCoord> coords, int dirX, int dirZ)
        {
            foreach (var c in coords)
            {
                var ahead = new GridCoord(c.X + dirX, c.Z + dirZ);

                // 자기 발자국 안이면 계속 진행
                if (Contains(coords, ahead)) continue;

                if (!grid.IsFree(ahead)) return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<GridCoord> coords, GridCoord c)
        {
            for (int i = 0; i < coords.Count; i++)
                if (coords[i].Equals(c)) return true;

            return false;
        }
    }
}
