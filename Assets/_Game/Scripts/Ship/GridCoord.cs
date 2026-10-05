using System;
using UnityEngine;

namespace Game.Ship
{
    /// <summary>
    /// 함체 격자 좌표. X는 선미(-)에서 선수(+), Z는 좌현(-)에서 우현(+).
    /// 함교가 원점(0,0)이고 배는 여기서부터 자라난다. 그래서 음수 좌표가 정상이다.
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Z;

        public GridCoord(int x, int z) { X = x; Z = z; }

        /// <summary>탄약고 지원, 손상 통제 범위 등에 쓰는 맨해튼 거리.</summary>
        public static int ManhattanDistance(GridCoord a, GridCoord b)
            => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Z - b.Z);

        /// <summary>상하좌우 네 방향. 연결 판정에 쓴다.</summary>
        public static readonly GridCoord[] Neighbors =
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        };

        public static GridCoord operator +(GridCoord a, GridCoord b) => new(a.X + b.X, a.Z + b.Z);

        public bool Equals(GridCoord o) => X == o.X && Z == o.Z;
        public override bool Equals(object o) => o is GridCoord c && Equals(c);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => $"({X},{Z})";
    }
}
