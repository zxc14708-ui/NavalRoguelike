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
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/NavalBase.unity",OpenSceneMode.Additive);
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
