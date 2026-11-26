using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 모함 위 비행 높이표(2026-11-26). 함재기가 모함 함교 · 포탑을 뚫고 지나가지 않게, 선체 길이 방향 1m 칸마다
    /// 그 칸에 걸친 메시 중 가장 높은 곳을 적어 둔다(모함 기준 좌표 — 모함이 돌아도 그대로 쓴다).
    /// 메시 경계는 모함 기준으로 바꿔 잡는다(월드 경계는 모함이 비스듬하면 부푼다).
    /// </summary>
    public sealed class HostClearance
    {
        public const float Slice = 1f;

        public Transform Host { get; }
        private readonly float[] _top;     // 칸마다 가장 높은 곳(모함 기준 y)
        private readonly float _minZ, _minX, _maxX;
        public float MinZ => _minZ;
        public float MaxZ => _minZ + _top.Length * Slice;
        public float MinX => _minX;
        public float MaxX => _maxX;

        private HostClearance(Transform host, float minZ, float minX, float maxX, float[] top)
        {
            Host = host; _minZ = minZ; _minX = minX; _maxX = maxX; _top = top;
        }

        /// <summary>root 아래 메시로 만든다. skip이 참인 노드(와 그 아래)는 뺀다.</summary>
        public static HostClearance Build(Transform host, Transform root, System.Func<Transform, bool> skip = null)
        {
            var boxes = new List<(float z0, float z1, float top)>();
            float minZ = float.MaxValue, maxZ = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            var w2l = host.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || Skipped(mf.transform, root, skip)) continue;
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null) continue;
                var b = mf.sharedMesh.bounds;
                var m = w2l * mf.transform.localToWorldMatrix;
                float z0 = float.MaxValue, z1 = float.MinValue, y1 = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var c = m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    z0 = Mathf.Min(z0, c.z); z1 = Mathf.Max(z1, c.z); y1 = Mathf.Max(y1, c.y);
                    minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                }
                boxes.Add((z0, z1, y1));
                minZ = Mathf.Min(minZ, z0); maxZ = Mathf.Max(maxZ, z1);
            }
            if (boxes.Count == 0) return null;
            int n = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) / Slice));
            var top = new float[n];
            for (int i = 0; i < n; i++) top[i] = float.MinValue;
            foreach (var (z0, z1, y1) in boxes)
            {
                int a = Mathf.Clamp(Mathf.FloorToInt((z0 - minZ) / Slice), 0, n - 1);
                int e = Mathf.Clamp(Mathf.FloorToInt((z1 - minZ) / Slice), 0, n - 1);
                for (int i = a; i <= e; i++) top[i] = Mathf.Max(top[i], y1);
            }
            return new HostClearance(host, minZ, minX, maxX, top);
        }

        private static bool Skipped(Transform t, Transform root, System.Func<Transform, bool> skip)
        {
            if (skip == null) return false;
            for (var p = t; p != null && p != root; p = p.parent) if (skip(p)) return true;
            return false;
        }

        public bool Valid => Host != null && Host.gameObject.activeInHierarchy;

        /// <summary>그 자리 위 가장 높은 곳(월드 y). 모함 밖(옆으로 margin 넘게)이면 float.MinValue.</summary>
        public float TopAt(Vector3 world, float margin)
        {
            if (!Valid) return float.MinValue;
            var l = Host.InverseTransformPoint(world);
            if (l.x < _minX - margin || l.x > _maxX + margin) return float.MinValue;
            int a = Mathf.FloorToInt((l.z - margin - _minZ) / Slice), e = Mathf.FloorToInt((l.z + margin - _minZ) / Slice);
            if (e < 0 || a >= _top.Length) return float.MinValue;
            float best = float.MinValue;
            for (int i = Mathf.Max(0, a); i <= Mathf.Min(_top.Length - 1, e); i++) best = Mathf.Max(best, _top[i]);
            return best == float.MinValue ? best : Host.TransformPoint(new Vector3(l.x, best, l.z)).y;
        }

        /// <summary>from에서 dir(수평)로 length까지 가는 길 위 가장 높은 곳(월드 y). 모함 위를 안 지나면 float.MinValue.</summary>
        public float TopAlong(Vector3 from, Vector3 dir, float length, float margin, float step = 1f)
        {
            float best = float.MinValue;
            for (float s = 0f; s <= length + 1e-3f; s += step) best = Mathf.Max(best, TopAt(from + dir * s, margin));
            return best;
        }

        /// <summary>모함 기준 좌표의 칸 높이(검증용).</summary>
        public float LocalTop(float localZ)
        {
            int i = Mathf.FloorToInt((localZ - _minZ) / Slice);
            return i >= 0 && i < _top.Length ? _top[i] : float.MinValue;
        }
    }
}
