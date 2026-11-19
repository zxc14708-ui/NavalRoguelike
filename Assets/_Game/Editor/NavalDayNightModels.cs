using System.Linq;
using UnityEditor;
using UnityEngine;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 낮과 밤 Codex 모델 연결(2026-11-19, ArtSource/DayNight_v1). 반잠수정 프리팹 두 개의 임시 모델(눌러 놓은 보트)을 전용 모델로 바꾼다.
    ///   - 프리팹의 스크립트 값(속도 · 폭발 · 어뢰 등)은 그대로 두고 ModelPivot 아래 모델만 바꾼다.
    ///   - 배율 Scale: 요청서 크기(7.5m · 11m)가 기존 보트(3.5m · 5.8m)보다 커서 줄여 쓴다 — 판정 상자도 모델에 맞춘다.
    ///   - 어뢰정: launchPoints를 TorpedoLaunchPoint_Port / _Starboard로, 포탑 없음.
    /// 부표 · 소노부이 · 조명탄 · 조난 신호는 Resources/Environment/DayNight FBX를 게임이 직접 불러온다(DayNightProps).
    /// 배치 실행: -executeMethod Game.EditorTools.NavalDayNightModels.Run
    /// </summary>
    public static class NavalDayNightModels
    {
        private const string ModelDir = Root + "/Art/Models/DayNight";
        private const string PrefabDir = Root + "/Prefabs/Enemies";
        public const float SuicideScale = 0.6f, TorpedoScale = 0.6f;

        [MenuItem("Naval/Connect Day-Night Models", priority = 6)]
        public static void Run()
        {
            Swap("ENE_SemiSubSuicide", SuicideScale, null);
            Swap("ENE_SemiSubTorpedo", TorpedoScale, (root, model) =>
            {
                var so = new SerializedObject(root.GetComponent<Game.Enemies.TorpedoBoat>());
                var points = new[] { "TorpedoLaunchPoint_Port", "TorpedoLaunchPoint_Starboard" }
                    .Select(n => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n)).Where(t => t != null).ToArray();
                var lp = so.FindProperty("launchPoints");
                lp.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++) lp.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
                so.FindProperty("weapon.turret").objectReferenceValue = null;
                so.FindProperty("weapon.elevationPivot").objectReferenceValue = null;
                so.FindProperty("weapon.muzzle").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[DayNight models] 어뢰정 발사점 {points.Length}개");
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[DayNight models] 완료");
        }

        private static void Swap(string name, float scale, System.Action<GameObject, GameObject> extra)
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (model == null) { Debug.LogError($"[DayNight models] 모델 없음: {name}"); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pivot = root.transform.Find("ModelPivot");
                if (pivot == null) { Debug.LogError($"[DayNight models] ModelPivot 없음: {name}"); return; }
                for (int i = pivot.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pivot.GetChild(i).gameObject);
                pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);   // Codex 모델 앞을 +Z로
                pivot.localScale = Vector3.one * scale;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(pivot, false);

                // 판정 상자: 모델 범위(루트 좌표)
                var b = new Bounds();
                bool any = false;
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    var m = root.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    var mb = mf.sharedMesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)));
                        if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                    }
                }
                var col = root.GetComponent<BoxCollider>();
                if (col != null && any)
                {
                    col.size = new Vector3(b.size.x, Mathf.Max(1f, b.size.y), b.size.z);
                    col.center = new Vector3(0f, 0f, b.center.z);
                }
                extra?.Invoke(root, inst);
                SetLayerRecursive(root, LayerEnemy);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[DayNight models] {name}: 배율 {scale} · 크기 {b.size.x:0.00}×{b.size.y:0.00}×{b.size.z:0.00}m");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
