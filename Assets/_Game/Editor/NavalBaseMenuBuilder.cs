using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class NavalBaseMenuBuilder
    {
        [MenuItem("Naval/Art/Prepare Harbor Menu")]
        public static void RunBatch()
        {
            const string path="Assets/_Game/Scenes/NavalBase.unity";
            if(!System.IO.File.Exists(path))
            {
                // 2026-11-21: 원본 장면은 저장소에서 뺐다. 시작 화면은 Build Harbor Menu Backdrop으로 바로 만든다
                Debug.LogError("[HarborMenu] Scenes/NavalBase.unity가 없습니다 — Naval/Art/Build Harbor Menu Backdrop을 쓰세요(장면이 필요하면 Build Naval Base Scene).");
                if(Application.isBatchMode)EditorApplication.Exit(1);
                return;
            }
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                var island=scene.GetRootGameObjects().First(g=>g.name=="NavalBaseIsland");
                SaveBackdrop(island);
                Debug.Log("[HarborMenu] PASS: exported existing naval base to runtime backdrop.");
            }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
            finally
            {
                EditorSceneManager.CloseScene(scene,true);
            }
        }

        internal static void SaveBackdrop(GameObject island)
        {
            var clone=UnityEngine.Object.Instantiate(island);
            try
            {
                clone.name="NavalBaseMenu";
                foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(c);
                NavalEditorUtil.EnsureFolder("Assets/_Game/Resources/Environment");
                PrefabUtility.SaveAsPrefabAsset(clone,"Assets/_Game/Resources/Environment/NavalBaseMenu.prefab");
                AssetDatabase.SaveAssets();
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }
    }
}
