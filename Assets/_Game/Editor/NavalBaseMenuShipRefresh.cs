using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 시작 화면 항구 프리팹의 정박 함정만 다시 옮겨 온다(2026-10-12 점검 O05).
    /// NavalBaseMenu 프리팹은 원본 씬(Scenes/NavalBase)을 통째로 복사해 만든 것이라, 호위함 모델(ESC_*)을 새로 바꾸면
    /// 원본 씬의 프리팹 인스턴스는 따라가지만 메뉴 프리팹의 복사본은 옛 메시를 가리킨 채 끊긴다(함정이 안 보임).
    /// 전체를 다시 내보내면(Prepare Harbor Menu) 프리팹 전체가 바뀌므로, 메시가 끊긴 Berth_*·DockedShip_*만
    /// 원본 씬에서 새로 복사해 같은 자리에 넣는다. 모델 크기가 바뀌었을 수 있어 원본 씬에서 흘수(3.5m)를 다시 맞춘 뒤 복사한다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalBaseMenuShipRefresh.Run
    /// </summary>
    public static class NavalBaseMenuShipRefresh
    {
        private const string MenuPrefab = NavalEditorUtil.Root + "/Resources/Environment/NavalBaseMenu.prefab";
        private const string BaseScene = NavalEditorUtil.Root + "/Scenes/NavalBase.unity";
        private const float K = 0.08f;               // NavalBaseBuilder.K(게임 단위 / m)
        private const float Draft = 3.5f * K;        // NavalBaseBuilder.SitOnWater
        private const float DockLift = 5.5f;         // 부유식 독 위로 들어 올린 높이(NavalBaseBuilder)

        [MenuItem("Naval/Art/Refresh Harbor Menu Ships")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(BaseScene, OpenSceneMode.Additive);
            var menu = PrefabUtility.LoadPrefabContents(MenuPrefab);
            var replaced = new List<string>();
            bool sceneDirty = false;
            try
            {
                var island = scene.GetRootGameObjects().First(g => g.name == "NavalBaseIsland").transform;
                var ships = island.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("Berth_") || t.name.StartsWith("DockedShip_")).ToList();
                foreach (var src in ships)
                {
                    string path = RelativePath(src, island);
                    var dst = menu.transform.Find(path);
                    if (dst == null) { Debug.LogWarning($"[HarborShips] 메뉴 프리팹에 {path}이 없음 — 건너뜀"); continue; }
                    if (!HasBrokenMesh(dst)) continue;

                    // 원본 씬에서 흘수를 다시 맞춘다(새 모델의 바닥 높이)
                    float lift = src.name.StartsWith("DockedShip_") ? src.parent.TransformVector(Vector3.up * DockLift).y : 0f;
                    var renderers = src.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length > 0)
                    {
                        float minY = renderers.Min(r => r.bounds.min.y);
                        float delta = island.position.y - Draft + lift - minY;
                        if (Mathf.Abs(delta) > 0.01f) { src.position += Vector3.up * delta; sceneDirty = true; }
                    }

                    var copy = Object.Instantiate(src.gameObject, dst.parent);
                    copy.name = src.name;
                    copy.transform.SetSiblingIndex(dst.GetSiblingIndex());
                    copy.transform.localPosition = src.localPosition;
                    copy.transform.localRotation = src.localRotation;
                    copy.transform.localScale = src.localScale;
                    // 시작 화면 배경은 그림만 쓴다(Prepare Harbor Menu와 같이)
                    foreach (var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
                    foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    foreach (var c in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
                    Object.DestroyImmediate(dst.gameObject);
                    replaced.Add(path);
                }
                if (replaced.Count > 0) PrefabUtility.SaveAsPrefabAsset(menu, MenuPrefab);
                if (sceneDirty) EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Debug.Log($"[HarborShips] 다시 옮긴 함정 {replaced.Count}척: {string.Join(", ", replaced)} · 원본 씬 흘수 조정 {(sceneDirty ? "예" : "아니오")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(menu);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool HasBrokenMesh(Transform root)
        {
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh != null) continue;
                var p = new SerializedObject(f).FindProperty("m_Mesh");
                if (p != null && p.objectReferenceInstanceIDValue != 0) return true;
            }
            return false;
        }

        private static string RelativePath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
