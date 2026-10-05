using System;
using UnityEngine;

namespace Game.Enemies
{
    /// <summary>
    /// 적 함정의 사격 금지 구역 한 조각. 선체 선수 방향 기준 방위(도, 시계 방향: 0 = 선수, 90 = 우현, 180 = 선미, 270 = 좌현).
    /// </summary>
    [Serializable]
    public struct BearingSector
    {
        [Tooltip("금지 구역 중심 방위. 0 = 선수, 90 = 우현, 180 = 선미, 270 = 좌현")]
        public float Center;
        [Tooltip("중심에서 양쪽으로 벌어지는 각도")]
        public float HalfWidth;
        [Tooltip("표적 앙각이 이보다 높으면 구조물 너머로 쏠 수 있다. 0이면 앙각과 상관없이 금지.")]
        public float ClearElevation;

        public BearingSector(float center, float halfWidth, float clearElevation = 0f)
        {
            Center = center;
            HalfWidth = halfWidth;
            ClearElevation = clearElevation;
        }
    }

    /// <summary>
    /// 적 함정도 자기 상부 구조물(함교·격납고)을 맞추는 방향으로는 쏘지 않는다.
    /// 플레이어는 이 사각으로 파고들어 포화를 피할 수 있다.
    /// </summary>
    public static class EnemyFireCutout
    {
        /// <summary>선체 기준 방위(0~360).</summary>
        public static float Bearing(Transform ship, Vector3 from, Vector3 target)
        {
            Vector3 fwd = ship.forward; fwd.y = 0f;
            Vector3 dir = target - from; dir.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f || dir.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Repeat(Vector3.SignedAngle(fwd, dir, Vector3.up), 360f);
        }

        /// <summary>from에서 target을 쏘는 것이 금지 구역에 걸리는가.</summary>
        public static bool IsBlocked(Transform ship, Vector3 from, Vector3 target, BearingSector[] sectors)
        {
            if (sectors == null || sectors.Length == 0) return false;

            float bearing = Bearing(ship, from, target);
            Vector3 d = target - from;
            float horizontal = new Vector2(d.x, d.z).magnitude;
            float elevation = Mathf.Atan2(d.y, Mathf.Max(horizontal, 0.001f)) * Mathf.Rad2Deg;

            foreach (var s in sectors)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(s.Center, bearing)) > s.HalfWidth) continue;
                if (s.ClearElevation > 0f && elevation >= s.ClearElevation) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 금지 구역을 수면 위 옅은 부채꼴로 보여 준다(플레이어에게는 안전 지대). 선체에 붙여 함께 돈다.
        /// </summary>
        public static MeshRenderer BuildIndicator(Transform ship, BearingSector[] sectors, float radius, Material material)
        {
            if (sectors == null || sectors.Length == 0 || material == null) return null;

            var go = new GameObject("FireCutoutZone", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(ship, false);
            go.transform.localPosition = new Vector3(0f, 0.15f, 0f);   // 원점 = 흘수선, 수면 바로 위

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            foreach (var s in sectors)
            {
                int segments = Mathf.Max(2, Mathf.CeilToInt(s.HalfWidth * 2f / 5f));
                int center = verts.Count;
                verts.Add(Vector3.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float yaw = (s.Center - s.HalfWidth + s.HalfWidth * 2f * i / segments) * Mathf.Deg2Rad;
                    verts.Add(new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * radius);
                    if (i == 0) continue;
                    tris.Add(center); tris.Add(center + i); tris.Add(center + i + 1);
                }
            }

            var mesh = new Mesh { name = "FireCutoutZone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }
    }
}
