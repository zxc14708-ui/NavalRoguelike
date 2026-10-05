using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>섬 하나(레이더·배치 판정용 요약).</summary>
    public sealed class IslandInfo
    {
        public Vector3 Center;
        public float Radius;          // 수면에서의 대략 반경
        public float Height;
        public bool Rocky;            // 바위섬이면 true, 무인도면 false
        public Vector2[] Rim;         // 수면 윤곽 표본(월드 xz)
        public GameObject Root;
        /// <summary>장식: 등대가 있는가, 해안에 얹힌 난파선이 있는가, 물개 수.</summary>
        public bool Lighthouse, Wreck;
        public int Seals;
        /// <summary>Codex 모델 섬이면 그 프리팹 이름(ENV_…), 절차형이면 null.</summary>
        public string Art;
        /// <summary>둘레에 놓인 해안 소품(어선·부잔교·방파제·해식 아치·어구·부표) 수.</summary>
        public int CoastProps;
    }

    /// <summary>
    /// 섬(바위섬·무인도)의 공용 판정. 섬은 "Terrain" 레이어(13)의 볼록 메시 콜라이더로 되어 있다.
    ///   - 사선(엄폐): 두 점 사이를 섬이 가리는가. 수면 가까운 함포·어뢰·미사일 발사는 막히고, 높이 나는 항공기는 넘겨 본다.
    ///   - 배치: 그 자리에 섬이 없는가(적 스폰).
    ///   - 충돌: 함체 상자가 섬에 박혔으면 밖으로 밀어낸다(좌초).
    /// </summary>
    public static class Islands
    {
        public const int Layer = 13;
        public const int Mask = 1 << Layer;

        private static readonly List<IslandInfo> s_all = new();
        private static readonly Collider[] s_overlap = new Collider[16];

        public static IReadOnlyList<IslandInfo> All => s_all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_all.Clear();

        internal static void Register(IslandInfo info) { if (!s_all.Contains(info)) s_all.Add(info); }
        internal static void Unregister(IslandInfo info) => s_all.Remove(info);

        /// <summary>a에서 b를 볼 때 섬이 가리는가.</summary>
        public static bool BlocksLine(Vector3 a, Vector3 b)
            => s_all.Count > 0 && Physics.Linecast(a, b, Mask, QueryTriggerInteraction.Ignore);

        /// <summary>함정끼리의 사선: 양쪽 모두 갑판 높이(1.5m)에서 본다.</summary>
        public static bool BlocksShipLine(Vector3 from, Vector3 to)
            => BlocksLine(new Vector3(from.x, 1.5f, from.z), new Vector3(to.x, 1.2f, to.z));

        /// <summary>그 자리(수평 반경 radius)에 섬이 없는가.</summary>
        public static bool IsClear(Vector3 position, float radius)
        {
            if (s_all.Count == 0) return true;
            var p = new Vector3(position.x, 0.5f, position.z);
            return !Physics.CheckCapsule(p + Vector3.down * 2f, p + Vector3.up * 4f, radius, Mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 함체 상자(box)를 position·rotation에 두었을 때 섬에 박혀 있으면 밖으로 밀어낸다.
        /// 밀어냈으면 true, normal은 섬에서 바깥쪽(수평).
        /// </summary>
        public static bool ResolveOverlap(BoxCollider box, ref Vector3 position, Quaternion rotation, out Vector3 normal)
        {
            normal = Vector3.zero;
            if (box == null || s_all.Count == 0) return false;

            Vector3 scale = box.transform.lossyScale;
            Vector3 half = Vector3.Scale(box.size, scale) * 0.5f;
            Vector3 center = position + rotation * Vector3.Scale(box.center, scale);
            int n = Physics.OverlapBoxNonAlloc(center, half, s_overlap, rotation, Mask, QueryTriggerInteraction.Ignore);
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                var col = s_overlap[i];
                if (col == null) continue;
                if (!Physics.ComputePenetration(box, position, rotation, col, col.transform.position, col.transform.rotation,
                                                out Vector3 dir, out float dist)) continue;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                position += dir * (dist + 0.02f);
                normal += dir;
                moved = true;
            }
            if (moved) normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.zero;
            return moved;
        }

        /// <summary>진행 방향 앞(length)에 섬이 있으면 그 법선(수평)을 준다. 적 회피 조타용.</summary>
        public static bool ProbeAhead(Vector3 position, Vector3 direction, float radius, float length, out RaycastHit hit)
        {
            hit = default;
            if (s_all.Count == 0 || direction.sqrMagnitude < 1e-6f) return false;
            var origin = new Vector3(position.x, 0.6f, position.z);
            return Physics.SphereCast(origin, radius, direction.normalized, out hit, length, Mask, QueryTriggerInteraction.Ignore);
        }
    }
}
