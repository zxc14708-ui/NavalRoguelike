using System.Collections.Generic;
using UnityEngine;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 함체 격자. 고정 크기가 아니라 함교에서부터 자라난다.
    ///
    /// 모든 모듈은 이미 놓인 모듈과 맞닿아야 설치할 수 있다.
    /// 그래서 플레이어가 붙이는 방식에 따라 배의 모양 자체가 달라지고,
    /// 선체는 점유된 칸을 따라 자동으로 만들어진다.
    /// </summary>
    public class ShipGrid : MonoBehaviour
    {
        [Header("Extent")]
        [Tooltip("함교 기준 선수/선미 방향 최대 칸 수")]
        [SerializeField] private int maxHalfLength = 10;
        [Tooltip("함교 기준 좌현/우현 방향 최대 칸 수. 좁게 둬야 배 모양이 유지된다.")]
        [SerializeField] private int maxHalfBeam = 3;

        [SerializeField] private float cellSize = 2f;
        [SerializeField] private float deckHeight = 0.74f;

        public int MaxHalfLength => maxHalfLength;
        public int MaxHalfBeam => maxHalfBeam;
        public float CellSize => cellSize;
        public float DeckHeight => deckHeight;

        private readonly Dictionary<GridCoord, ModuleInstance> _occupied = new();
        private readonly List<ModuleInstance> _modules = new();
        private readonly List<GridCoord> _scratch = new();
        /// <summary>CanPlace가 "이 자리에 놓으면"을 따져 볼 때 잠깐 점유로 치는 칸(이웃 규칙 재검사용).</summary>
        private readonly HashSet<GridCoord> _probe = new();

        /// <summary>설치된 모든 모듈(중복 없음). 순회 전용.</summary>
        public IReadOnlyList<ModuleInstance> Modules => _modules;

        /// <summary>점유된 칸 전체. 선체 생성과 질량 계산이 읽는다.</summary>
        public IReadOnlyCollection<GridCoord> OccupiedCells => _occupied.Keys;

        public bool InBounds(GridCoord c)
            => Mathf.Abs(c.X) <= maxHalfLength && Mathf.Abs(c.Z) <= maxHalfBeam;

        public bool IsFree(GridCoord c) => !_occupied.ContainsKey(c) && !_probe.Contains(c);

        public ModuleInstance Get(GridCoord c)
            => _occupied.TryGetValue(c, out var m) ? m : null;

        // ------------------------------------------------------------ 좌표 변환

        /// <summary>격자 좌표 -> 함선 로컬 위치. 선수는 로컬 +Z다.</summary>
        public Vector3 CoordToLocal(GridCoord c)
            => new(c.Z * cellSize, deckHeight, c.X * cellSize);

        public Vector3 CoordToWorld(GridCoord c) => transform.TransformPoint(CoordToLocal(c));

        /// <summary>월드 위치에서 가장 가까운 칸. 피격 지점 판정에 쓴다.</summary>
        public GridCoord WorldToNearestCoord(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new GridCoord(Mathf.RoundToInt(l.z / cellSize), Mathf.RoundToInt(l.x / cellSize));
        }

        // ------------------------------------------------------------ 배치 판정

        /// <summary>회전(0~3단계)을 반영한 점유 좌표 목록.</summary>
        public static void GetFootprint(ModuleDefinition def, GridCoord origin, int rotationSteps,
                                        List<GridCoord> outCoords)
        {
            outCoords.Clear();
            if (def == null) return;

            int w = def.Width, h = def.Height;
            if ((rotationSteps & 1) == 1) { int t = w; w = h; h = t; }

            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    outCoords.Add(new GridCoord(origin.X + i, origin.Z + j));
        }

        /// <summary>배치 가능 여부와 실패 사유.</summary>
        public bool CanPlace(ModuleDefinition def, GridCoord origin, int rotationSteps, out string reason)
        {
            reason = null;
            if (def == null) { reason = "모듈 정의 없음"; return false; }

            if (def.MaxCount > 0 && CountOf(def) >= def.MaxCount)
            {
                reason = $"최대 {def.MaxCount}개까지만 설치할 수 있음";
                return false;
            }

            GetFootprint(def, origin, rotationSteps, _scratch);

            foreach (var c in _scratch)
            {
                if (!InBounds(c)) { reason = "함체가 늘어날 수 있는 범위를 벗어남"; return false; }
                if (!IsFree(c)) { reason = "이미 점유된 자리"; return false; }
            }

            if (!IsConnected(_scratch)) { reason = "다른 모듈과 붙어 있어야 함"; return false; }

            if (!PlacementRuleEvaluator.Evaluate(def.Placement, this, _scratch, out reason)) return false;
            return !EnclosesNeighbor(_scratch, out reason);
        }

        /// <summary>
        /// 이 발자국을 놓으면 이미 놓인 "옆이나 뒤가 트여야 하는" 블록(폭뢰)이 사방이 막히는가.
        /// 형태는 위치로 계속 다시 판정하므로, 막혀서 쓸 수 없게 되는 배치를 미리 거부한다.
        /// </summary>
        private bool EnclosesNeighbor(List<GridCoord> footprint, out string reason)
        {
            reason = null;
            foreach (var m in _modules)
            {
                if (m?.Definition == null || m.Definition.Placement.Zone != PlacementZone.SideOrStern) continue;
                bool touches = false;
                foreach (var c in m.OccupiedCoords)
                {
                    foreach (var d in GridCoord.Neighbors)
                        if (footprint.Contains(c + d)) { touches = true; break; }
                    if (touches) break;
                }
                if (!touches) continue;

                foreach (var c in footprint) _probe.Add(c);
                bool ok;
                try { ok = PlacementRuleEvaluator.Evaluate(m.Definition.Placement, this, m.OccupiedCoords, out _); }
                finally { _probe.Clear(); }
                if (!ok)
                {
                    reason = $"{m.Definition.DisplayName}의 옆·뒤를 모두 막음";
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 위치로 형태가 정해지는 블록(소나·폭뢰)의 형태를 다시 판정한다. 설치·철거 때마다 부른다.
        /// 형태가 바뀐 블록은 런타임이 외형·동작을 바꾼다(ModuleInstance.SetVariant).
        /// </summary>
        public void RefreshVariants()
        {
            foreach (var m in _modules)
            {
                if (m?.Definition == null || !ModuleVariants.HasVariants(m.Definition.Type)) continue;
                var v = ModuleVariants.Resolve(m.Definition.Type, this, m.OccupiedCoords, out var sides);
                m.SetVariant(v, sides);
            }
        }

        /// <summary>
        /// 기존 함체와 맞닿아 있는지. 함선이 비어 있을 때(첫 모듈)는 언제나 허용한다.
        /// 이 규칙이 배를 하나로 이어진 덩어리로 유지한다.
        /// </summary>
        public bool IsConnected(IReadOnlyList<GridCoord> footprint)
        {
            if (_occupied.Count == 0) return true;

            foreach (var c in footprint)
                foreach (var d in GridCoord.Neighbors)
                    if (_occupied.ContainsKey(c + d)) return true;

            return false;
        }

        public int CountOf(ModuleDefinition def)
        {
            if (def == null) return 0;

            int n = 0;
            foreach (var m in _modules)
                if (m != null && m.Definition == def) n++;

            return n;
        }

        // ------------------------------------------------------------ 설치/철거

        /// <summary>격자에 등록한다. 프리팹 생성은 ModuleFactory가 맡는다.</summary>
        public bool Place(ModuleInstance instance, GridCoord origin, int rotationSteps)
        {
            if (instance == null) return false;
            if (!CanPlace(instance.Definition, origin, rotationSteps, out _)) return false;

            var coords = new List<GridCoord>();
            GetFootprint(instance.Definition, origin, rotationSteps, coords);
            instance.SetPlacement(origin, rotationSteps, coords);

            foreach (var c in coords) _occupied[c] = instance;
            _modules.Add(instance);
            RefreshVariants();
            return true;
        }

        public void Remove(ModuleInstance instance)
        {
            if (instance == null) return;

            foreach (var c in instance.OccupiedCoords)
                if (_occupied.TryGetValue(c, out var m) && m == instance) _occupied.Remove(c);

            _modules.Remove(instance);
            RefreshVariants();
        }

        /// <summary>이 모듈을 놓을 자리가 하나라도 있는지. 카드 후보를 고를 때 쓴다.</summary>
        public bool HasAnyValidPlacement(ModuleDefinition def)
        {
            if (def == null) return false;
            int rotations = def.CanRotate ? 4 : 1;

            for (int r = 0; r < rotations; r++)
                for (int x = -maxHalfLength; x <= maxHalfLength; x++)
                    for (int z = -maxHalfBeam; z <= maxHalfBeam; z++)
                        if (CanPlace(def, new GridCoord(x, z), r, out _)) return true;

            return false;
        }

        /// <summary>점유 칸의 경계. 콜라이더와 카메라 프레이밍에 쓴다.</summary>
        public void GetExtent(out GridCoord min, out GridCoord max)
        {
            if (_occupied.Count == 0)
            {
                min = max = default;
                return;
            }

            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;

            foreach (var c in _occupied.Keys)
            {
                minX = Mathf.Min(minX, c.X); maxX = Mathf.Max(maxX, c.X);
                minZ = Mathf.Min(minZ, c.Z); maxZ = Mathf.Max(maxZ, c.Z);
            }

            min = new GridCoord(minX, minZ);
            max = new GridCoord(maxX, maxZ);
        }
    }
}
