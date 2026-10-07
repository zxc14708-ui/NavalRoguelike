using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>해군 기지 씬 빌더(작업 중) — 우선 쓸 프리팹 크기만 잰다.</summary>
    internal static class NavalBaseBuilder
    {
        public static void Probe()
        {
            string[] paths =
            {
                "Assets/_Game/Resources/TaskForce/Escorts/ESC_CAP_T3.prefab",
                "Assets/_Game/Resources/TaskForce/Escorts/ESC_ASW_T2.prefab",
                "Assets/_Game/Resources/TaskForce/Escorts/ESC_STK_T1.prefab",
                "Assets/_Game/Prefabs/Projectiles/HEL_Asw.prefab",
                "Assets/_Game/Prefabs/Enemies/ENE_Fighter.prefab",
                "Assets/_Game/Prefabs/Enemies/ENE_FastBoat.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_Jetty.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_Breakwater.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_Lighthouse.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_RadarStation.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_SupplyDepot.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_CoastShed.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_CoastalPine.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_WatchPost.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_FloatingPontoon.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_NavigationBuoy.prefab",
                "Assets/_Game/Resources/Environment/Props/ENV_CoastalBunker.prefab",
                "Assets/_Game/Resources/Environment/Islands/ENV_HarborIsland.prefab",
            };
            var sb = new StringBuilder("[BaseProbe]\n");
            foreach (var p in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) { sb.AppendLine($"MISSING {p}"); continue; }
                var go = Object.Instantiate(prefab);
                var b = new Bounds(go.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
                var comps = new StringBuilder();
                foreach (var c in go.GetComponentsInChildren<MonoBehaviour>()) comps.Append(c.GetType().Name).Append(' ');
                sb.AppendLine($"{System.IO.Path.GetFileNameWithoutExtension(p)} center={b.center} size={b.size} comps={comps}");
                Object.DestroyImmediate(go);
            }
            Debug.Log(sb.ToString());
        }
    }
}
