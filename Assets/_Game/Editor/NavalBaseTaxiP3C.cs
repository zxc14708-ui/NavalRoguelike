using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 시작 화면 비행장: 평행 유도로에 서 있던 초계기(P3C_Taxi, 섬 좌표 (−470, 4, −262), 기수 동쪽)를 움직이는 물체로 떼어 낸다(2026-10-11).
    /// NavalBaseBuilder.CombineStatic이 이 초계기를 다른 정적 조각과 재질별로 합쳐 버려 유도로 위에 영원히 멈춰 있었고,
    /// 이륙하는 전투기가 그 위를 지나갔다. 전체 기지를 다시 짓지 않고(Codex 작업 보존) 이 초계기만 처리한다.
    ///   1. 항공기 재질의 합친 메시(Art/NavalBase/MESH_combined_MAT_base_*)에서 초계기 범위 안에 세 꼭짓점이 모두 있는 삼각형을 뺀다.
    ///   2. 뺀 삼각형으로 초계기 메시를 재질별로 만들어(초계기 기준 좌표, 기수 +Z) Art/NavalBase/MESH_P3C_Taxi_*에 저장한다.
    ///   3. 시작 화면 프리팹(Resources/Environment/NavalBaseMenu)과 원본 씬(Scenes/NavalBase)의 NavalAirStation 아래에
    ///      P3C_Taxi 물체를 넣는다. 움직임은 NavalBaseMenu가 맡는다(전투기와 시간을 엇갈려 유도로·활주로를 함께 쓴다).
    /// 이미 떼어 냈으면(P3C_Taxi가 있으면) 아무것도 하지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalBaseTaxiP3C.Run
    /// </summary>
    public static class NavalBaseTaxiP3C
    {
        private const string MeshDir = NavalEditorUtil.Root + "/Art/NavalBase";
        private const string MenuPrefab = NavalEditorUtil.Root + "/Resources/Environment/NavalBaseMenu.prefab";
        private const string BaseScene = NavalEditorUtil.Root + "/Scenes/NavalBase.unity";
        private const string Name = "P3C_Taxi";
        private static readonly Vector3 Pivot = new(-470f, 4f, -262f);   // NavalBaseBuilder: P3C(air, (-470, Ground, TaxiZ), 90°)
        private const float Yaw = 90f;
        // 초계기 범위(길이 35.6·날개폭 30.4·높이 10.7 + 여유) — 섬 좌표
        private static readonly Bounds Box = new(new Vector3(-471.5f, 9.4f, -262f), new Vector3(42f, 11.2f, 33f));
        private static readonly string[] Materials =
            { "MAT_base_aircraft", "MAT_base_aircraft_dark", "MAT_base_aircraft_grey", "MAT_base_cockpit", "MAT_base_prop_disc", "MAT_base_steel_dark" };

        [MenuItem("Naval/Art/Detach Taxiing P-3C (Harbor Menu)")]
        public static void Run()
        {
            var root = PrefabUtility.LoadPrefabContents(MenuPrefab);
            try
            {
                var air = root.transform.Find("NavalAirStation");
                if (air == null) { Debug.LogError("[P3C_Taxi] 시작 화면 프리팹에 NavalAirStation이 없습니다."); return; }
                if (air.Find(Name) != null) { Debug.Log("[P3C_Taxi] 이미 떼어 냈습니다."); return; }

                var parts = ExtractFromCombined(root.transform);
                if (parts.Count == 0) { Debug.LogError("[P3C_Taxi] 합친 메시에서 초계기 삼각형을 찾지 못했습니다."); return; }

                AddPlane(air, parts);
                PrefabUtility.SaveAsPrefabAsset(root, MenuPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // 원본 씬에도 같은 물체를 넣는다(합친 메시는 같은 에셋이라 씬에서도 이미 빠졌다 — 메뉴를 다시 내보내도 사라지지 않게)
            var scene = EditorSceneManager.OpenScene(BaseScene, OpenSceneMode.Additive);
            try
            {
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name != "NavalBaseIsland") continue;
                    var air = go.transform.Find("NavalAirStation");
                    if (air == null || air.Find(Name) != null) break;
                    var parts = new List<(Material, Mesh)>();
                    foreach (var mat in Materials)
                    {
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshDir}/MESH_{Name}_{mat}.asset");
                        var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(mat));
                        if (mesh != null && m != null) parts.Add((m, mesh));
                    }
                    if (parts.Count > 0) { AddPlane(air, parts); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                    break;
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>합친 메시(항공기 재질)에서 초계기 삼각형을 빼고, 그 삼각형으로 만든 초계기 메시(재질별)를 돌려준다.</summary>
        private static List<(Material, Mesh)> ExtractFromCombined(Transform root)
        {
            var result = new List<(Material, Mesh)>();
            var toPlane = Matrix4x4.TRS(Pivot, Quaternion.Euler(0f, Yaw, 0f), Vector3.one).inverse;
            var combined = root.Find("Combined");
            if (combined == null) return result;
            foreach (var mr in combined.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = mr.sharedMaterial;
                var mf = mr.GetComponent<MeshFilter>();
                if (mat == null || mf == null || mf.sharedMesh == null || System.Array.IndexOf(Materials, mat.name) < 0) continue;
                var mesh = mf.sharedMesh;
                // 합친 메시의 꼭짓점은 섬 루트 기준이다(합칠 때 toLocal = 섬 루트의 worldToLocal)
                var toIsland = root.worldToLocalMatrix * mr.transform.localToWorldMatrix;
                var verts = mesh.vertices; var normals = mesh.normals; var tris = mesh.triangles;
                var keep = new List<int>(tris.Length);
                var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new List<int>();
                var remap = new Dictionary<int, int>();
                for (int i = 0; i < tris.Length; i += 3)
                {
                    bool inside = true;
                    for (int k = 0; k < 3 && inside; k++) inside = Box.Contains(toIsland.MultiplyPoint3x4(verts[tris[i + k]]));
                    if (!inside) { keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]); continue; }
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[i + k];
                        if (!remap.TryGetValue(v, out int n))
                        {
                            n = pv.Count; remap[v] = n;
                            pv.Add(toPlane.MultiplyPoint3x4(toIsland.MultiplyPoint3x4(verts[v])));
                            pn.Add(normals.Length > v ? toPlane.MultiplyVector(toIsland.MultiplyVector(normals[v])).normalized : Vector3.up);
                        }
                        pt.Add(n);
                    }
                }
                if (pt.Count == 0) continue;
                Undo.RecordObject(mesh, "Detach P3C");
                mesh.SetTriangles(keep, 0, true);
                EditorUtility.SetDirty(mesh);

                var plane = new Mesh { name = $"{Name}_{mat.name}", indexFormat = pv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                plane.SetVertices(pv); plane.SetNormals(pn); plane.SetTriangles(pt, 0); plane.RecalculateBounds();
                string path = $"{MeshDir}/MESH_{Name}_{mat.name}.asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(plane, path);
                result.Add((mat, plane));
                Debug.Log($"[P3C_Taxi] {mesh.name}: 삼각형 {pt.Count / 3}개를 떼어 냄");
            }
            return result;
        }

        private static void AddPlane(Transform air, List<(Material mat, Mesh mesh)> parts)
        {
            var plane = new GameObject(Name).transform;
            plane.SetParent(air, false);
            // 섬 좌표의 Pivot을 NavalAirStation 기준으로(보통 같은 원점)
            plane.position = air.root.TransformPoint(Pivot);
            plane.rotation = air.root.rotation * Quaternion.Euler(0f, Yaw, 0f);
            foreach (var (mat, mesh) in parts)
            {
                var go = new GameObject(mat.name.Replace("MAT_base_", ""), typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(plane, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            foreach (var t in plane.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = air.gameObject.layer;
            Debug.Log($"[P3C_Taxi] {air.root.name}/NavalAirStation/{Name} 추가(부품 {parts.Count}개)");
        }

        private static string MaterialPath(string mat)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{mat} t:Material", new[] { MeshDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == mat) return p;
            }
            return null;
        }
    }
}
