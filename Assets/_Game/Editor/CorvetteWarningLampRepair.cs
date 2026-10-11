using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    // Repair existing prefabs as well as the builder: no full scene setup required.
    [InitializeOnLoad]
    internal static class CorvetteWarningLampRepair
    {
        static CorvetteWarningLampRepair() => EditorApplication.delayCall += Repair;

        [MenuItem("Naval/Art/Repair Corvette Warning Lamps")]
        private static void Repair()
        {
            const string path = "Assets/_Game/Prefabs/Enemies/ENE_BossCorvette.prefab";
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
            var root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            try
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "TorpedoLamp" || t.parent == null) continue;
                    var s = t.parent.lossyScale;
                    var expected = new Vector3(.18f / Mathf.Max(Mathf.Abs(s.x), .0001f),
                        .18f / Mathf.Max(Mathf.Abs(s.y), .0001f), .18f / Mathf.Max(Mathf.Abs(s.z), .0001f));
                    if ((t.localScale - expected).sqrMagnitude < 1e-10f) continue;
                    t.localScale = expected;
                    changed = true;
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[Art] Corvette warning lamps repaired to 0.18m world diameter.");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
