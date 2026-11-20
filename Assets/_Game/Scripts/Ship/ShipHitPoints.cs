using System.Collections.Generic;
using UnityEngine;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>적 공격이 블록 격자의 어느 칸을 겨누는가.</summary>
    public enum HitArc
    {
        /// <summary>옆에서 날아오는 공격(해면 미사일 · 포격 · 어뢰): 쏜 쪽을 향한 바깥 칸 중 하나.</summary>
        Side,
        /// <summary>위에서 꽂히는 공격(급강하 미사일 · 자폭 드론): 갑판 블록 전체에서 칸마다 같은 확률.</summary>
        Top,
        /// <summary>함교 저격(레이더 전파 추적 미사일): 작동 중인 함교 · 레이더 칸. 없으면 Top.</summary>
        Command,
    }

    /// <summary>
    /// 적 공격의 겨냥 칸(2026-11-21). 예전에는 미사일 · 드론 · 포격이 모두 기함 기준점(= 한가운데 함교 칸)을 겨눠
    /// 자동 검사 로그에서 적 미사일의 95%가 함교에 맞았고, 함교가 부서진 뒤에는 같은 칸이 선체 피해만 받아 다른 블록은 거의 부서지지 않았다.
    /// 공격마다 칸 하나를 골라 그 칸의 현재 위치를 겨누게 한다 — 배가 돌면 겨냥 칸도 같이 돈다.
    /// 일제사격 분산: 최근 2.5초 안에 고른 칸은 다른 칸이 남아 있으면 피한다(register=true인 공격만 — 미사일 · 드론).
    /// </summary>
    public static class ShipHitPoints
    {
        private const float RecentWindow = 2.5f;
        private static readonly Dictionary<ShipGrid, Dictionary<GridCoord, float>> s_recent = new();
        private static readonly List<GridCoord> s_candidates = new();
        private static readonly List<GridCoord> s_second = new();   // 옆 공격: 줄마다 두 번째 바깥 칸(바깥 칸이 다 최근에 맞았을 때)
        private static readonly List<GridCoord> s_fresh = new();
        private static readonly Dictionary<int, (GridCoord cell, float depth, GridCoord second, float secondDepth, bool hasSecond)> s_lines = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_recent.Clear();

        /// <summary>블록 격자가 있는 함선(기함)이면 그 격자. 호위함 · 기만체 · 적이면 null — 예전처럼 중심을 겨눈다.</summary>
        public static ShipGrid GridOf(Transform target)
        {
            if (target == null || !target.TryGetComponent<ShipController>(out var ship)) return null;
            var grid = ship.Grid;
            return grid != null && grid.OccupiedCells.Count > 0 ? grid : null;
        }

        /// <summary>옆 공격의 겨냥 칸이 아직 쏜 쪽을 향하나(배가 돌아 반대편이 됐으면 false — 다시 고른다).</summary>
        public static bool StillFacing(ShipGrid grid, GridCoord cell, Vector3 shooter)
        {
            var t = grid.transform;
            Vector3 toShooter = t.InverseTransformDirection(shooter - t.position); toShooter.y = 0f;
            Vector3 p = grid.CoordToLocal(cell); p.y = 0f;
            return Vector3.Dot(p, toShooter) >= -0.5f * grid.CellSize * toShooter.magnitude;
        }

        /// <summary>칸 위치(월드). 높이는 호출한 쪽이 정한다.</summary>
        public static Vector3 Point(ShipGrid grid, GridCoord cell) => grid.CoordToWorld(cell);

        public static bool TryPick(ShipGrid grid, Vector3 shooter, HitArc arc, out GridCoord cell, bool register = true)
        {
            cell = default;
            if (grid == null || grid.OccupiedCells.Count == 0) return false;
            s_candidates.Clear();
            s_second.Clear();

            if (arc == HitArc.Command)
            {
                foreach (var c in grid.OccupiedCells)
                {
                    var m = grid.Get(c);
                    if (m != null && m.IsOperational && m.Definition != null &&
                        (m.Definition.Type == ModuleType.Bridge || m.Definition.Type == ModuleType.Radar))
                        s_candidates.Add(c);
                }
                if (s_candidates.Count == 0) arc = HitArc.Top;
            }
            if (arc == HitArc.Top)
                foreach (var c in grid.OccupiedCells) s_candidates.Add(c);
            else if (arc == HitArc.Side)
                CollectFacing(grid, shooter);
            if (s_candidates.Count == 0) return false;

            // 최근에 고른 칸은 피한다(남는 칸이 있으면)
            s_fresh.Clear();
            Dictionary<GridCoord, float> recent = null;
            if (register && !s_recent.TryGetValue(grid, out recent)) s_recent[grid] = recent = new Dictionary<GridCoord, float>();
            float now = Time.time;
            if (recent != null)
                foreach (var c in s_candidates)
                    if (!recent.TryGetValue(c, out float t) || now - t > RecentWindow) s_fresh.Add(c);
            if (s_fresh.Count == 0 && recent != null)
                foreach (var c in s_second)
                    if (!recent.TryGetValue(c, out float t) || now - t > RecentWindow) s_fresh.Add(c);
            var pool = s_fresh.Count > 0 ? s_fresh : s_candidates;
            cell = pool[Random.Range(0, pool.Count)];
            if (recent != null) recent[cell] = now;
            return true;
        }

        /// <summary>쏜 쪽에서 보이는 바깥 칸: 쏜 방향과 수직인 줄마다 쏜 쪽으로 가장 튀어나온 칸 하나.</summary>
        private static void CollectFacing(ShipGrid grid, Vector3 shooter)
        {
            var t = grid.transform;
            Vector3 d = t.InverseTransformDirection(shooter - t.position);
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) d = Vector3.forward;
            d.Normalize();
            Vector3 perp = new(-d.z, 0f, d.x);
            float size = Mathf.Max(0.01f, grid.CellSize);
            s_lines.Clear();
            foreach (var c in grid.OccupiedCells)
            {
                Vector3 p = grid.CoordToLocal(c);
                p.y = 0f;
                int line = Mathf.RoundToInt(Vector3.Dot(p, perp) / size);
                float depth = Vector3.Dot(p, d);
                if (!s_lines.TryGetValue(line, out var e)) { s_lines[line] = (c, depth, default, 0f, false); continue; }
                if (depth > e.depth) s_lines[line] = (c, depth, e.cell, e.depth, true);
                else if (!e.hasSecond || depth > e.secondDepth) s_lines[line] = (e.cell, e.depth, c, depth, true);
            }
            foreach (var kv in s_lines)
            {
                s_candidates.Add(kv.Value.cell);
                if (kv.Value.hasSecond) s_second.Add(kv.Value.second);
            }
        }
    }
}
