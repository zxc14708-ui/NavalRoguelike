using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 함 현황 패널 가운데의 위에서 본 함선 도식. 선수가 로컬 위(+Y).
    /// 실제 배치 칸을 그대로 그려 모듈마다 상태 색(정상·손상·위험·파괴)을 입히고, 무장 칸에는 포탑 점을 찍는다.
    /// 모듈 구성이나 내구 구간이 바뀔 때만 메시를 다시 만든다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ShipDiagramGraphic : MaskableGraphic
    {
        private static readonly Color HullColor = new(0.16f, 0.30f, 0.34f, 0.95f);
        private static readonly Color HullEdge = new(0.30f, 0.88f, 0.85f, 0.55f);
        private static readonly Color Healthy = new(0.62f, 0.86f, 0.84f, 1f);
        private static readonly Color Damaged = new(0.98f, 0.73f, 0.28f, 1f);
        private static readonly Color Critical = new(1f, 0.42f, 0.18f, 1f);
        private static readonly Color Destroyed = new(0.85f, 0.16f, 0.12f, 0.85f);
        private static readonly Color TurretDot = new(1f, 0.62f, 0.2f, 1f);

        private ShipGrid _grid;
        private int _signature;
        private readonly List<GridCoord> _cells = new(64);

        public void Bind(ShipGrid grid)
        {
            _grid = grid;
            _signature = 0;
            SetVerticesDirty();
        }

        /// <summary>모듈 구성·상태 구간이 바뀌었으면 다시 그린다. HUD가 주기적으로 부른다.</summary>
        public void Refresh()
        {
            int sig = ComputeSignature();
            if (sig == _signature) return;
            _signature = sig;
            SetVerticesDirty();
        }

        private int ComputeSignature()
        {
            if (_grid == null) return 1;
            unchecked
            {
                int h = 17;
                foreach (var c in _grid.OccupiedCells)
                {
                    var m = _grid.Get(c);
                    h = h * 31 + c.X * 7919 + c.Z * 104729 + (m != null ? StateOf(m) : 0);
                }
                return h == 0 ? 2 : h;
            }
        }

        private static int StateOf(ModuleInstance m)
        {
            if (m.IsDestroyed) return 4;
            float f = m.MaxHp > 0f ? m.Hp / m.MaxHp : 1f;
            return f < 0.3f ? 3 : f < 0.6f ? 2 : 1;
        }

        private static bool IsWeapon(ModuleType t) => t is ModuleType.Autocannon or ModuleType.Ciws or ModuleType.NavalGun
            or ModuleType.GuidedRocket or ModuleType.Vls or ModuleType.SamLauncher or ModuleType.AswLauncher;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_grid == null || _grid.OccupiedCells.Count == 0) return;

            _cells.Clear();
            _cells.AddRange(_grid.OccupiedCells);
            _grid.GetExtent(out var min, out var max);

            // 칸 단위 크기: 길이(선수 방향 X)에 뱃머리 1.4칸, 폭(Z)에 여유 0.6칸
            float lengthUnits = max.X - min.X + 1f + 1.4f;
            float beamUnits = max.Z - min.Z + 1f + 0.6f;
            Rect r = GetPixelAdjustedRect();
            float s = Mathf.Min(r.height / lengthUnits, r.width / beamUnits);
            float cx = (min.Z + max.Z) * 0.5f;
            float cy = (min.X + max.X + 1.4f) * 0.5f;
            Vector2 Map(float z, float x) => r.center + new Vector2((z - cx) * s, (x - cy) * s);

            // --- 선체: 줄마다 폭을 따라가는 계단형 + 뱃머리 삼각형
            int rows = max.X - min.X + 1;
            var rowMin = new float[rows];
            var rowMax = new float[rows];
            for (int i = 0; i < rows; i++) { rowMin[i] = float.MaxValue; rowMax[i] = float.MinValue; }
            foreach (var c in _cells)
            {
                int i = c.X - min.X;
                rowMin[i] = Mathf.Min(rowMin[i], c.Z);
                rowMax[i] = Mathf.Max(rowMax[i], c.Z);
            }
            for (int i = 0; i < rows; i++)
            {
                if (rowMin[i] != float.MaxValue) continue;
                int prev = Mathf.Max(0, i - 1);
                rowMin[i] = rowMin[prev] == float.MaxValue ? cx : rowMin[prev];
                rowMax[i] = rowMax[prev] == float.MinValue ? cx : rowMax[prev];
            }

            const float pad = 0.3f;
            for (int i = 0; i < rows; i++)
            {
                float x = min.X + i;
                float l = rowMin[i] - 0.5f - pad, rr = rowMax[i] + 0.5f + pad;
                // 테두리 느낌: 바깥에 옅은 청록, 안쪽에 선체색
                Quad(vh, Map(l - 0.08f, x - 0.5f), Map(rr + 0.08f, x + 0.5f), HullEdge);
                Quad(vh, Map(l, x - 0.5f), Map(rr, x + 0.5f), HullColor);
            }
            {
                int top = rows - 1;
                float x = max.X + 0.5f;
                float l = rowMin[top] - 0.5f - pad, rr = rowMax[top] + 0.5f + pad;
                Tri(vh, Map(l - 0.08f, x), Map((l + rr) * 0.5f, x + 1.5f), Map(rr + 0.08f, x), HullEdge);
                Tri(vh, Map(l, x), Map((l + rr) * 0.5f, x + 1.35f), Map(rr, x), HullColor);
            }

            // --- 모듈 칸
            const float inset = 0.14f;
            foreach (var c in _cells)
            {
                var m = _grid.Get(c);
                if (m == null) continue;
                Color col = StateOf(m) switch { 4 => Destroyed, 3 => Critical, 2 => Damaged, _ => Healthy };
                if (m.Definition != null && m.Definition.Type == ModuleType.Bridge && !m.IsDestroyed) col = Color.Lerp(col, Color.white, 0.25f);
                Quad(vh, Map(c.Z - 0.5f + inset, c.X - 0.5f + inset), Map(c.Z + 0.5f - inset, c.X + 0.5f - inset), col);

                if (m.Definition != null && IsWeapon(m.Definition.Type) && !m.IsDestroyed && m.Origin.Equals(c))
                    Quad(vh, Map(c.Z - 0.2f, c.X - 0.2f), Map(c.Z + 0.2f, c.X + 0.2f), TurretDot);
            }
        }

        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Color c)
        {
            Color32 col = c * color;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(a.x, a.y), col, Vector4.zero);
            vh.AddVert(new Vector2(a.x, b.y), col, Vector4.zero);
            vh.AddVert(new Vector2(b.x, b.y), col, Vector4.zero);
            vh.AddVert(new Vector2(b.x, a.y), col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        private void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 d, Color c)
        {
            Color32 col = c * color;
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero);
            vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(d, col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }
    }
}
