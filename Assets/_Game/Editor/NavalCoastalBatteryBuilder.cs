using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Enemies;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 해안포대(2026-10-11 점검 K04, 고정 적) 프리팹과 적 데이터를 만든다. 다른 적·씬은 건드리지 않는다.
    ///   - Prefabs/Enemies/ENE_CoastalBattery.prefab: 바위 받침 위 팔각 콘크리트 진지 · 모래주머니 · 포탑(TurretPivot) · 포신(ElevationPivot·Muzzle).
    ///     모델은 기본 도형과 기존 적·섬 재질로 짓는다(Codex 모델이 생기면 Visual만 바꾸면 된다). 포탄·포구 섬광은 포격정 것을 쓴다.
    ///   - Resources/Enemies/ene_coastal_battery.asset: 체력 110 · 사거리 45 · 포탄 7×2발 · 5초 · 경험치 5 · 대형 수상 표적
    /// 배치 실행: -executeMethod Game.EditorTools.NavalCoastalBatteryBuilder.Run
    /// </summary>
    public static class NavalCoastalBatteryBuilder
    {
        private const string PrefabPath = Root + "/Prefabs/Enemies/ENE_CoastalBattery.prefab";
        private const string DefPath = Root + "/Resources/Enemies/ene_coastal_battery.asset";
        private const string MatDir = Root + "/Art/Models/Materials";

        [MenuItem("Naval/Enemies/Build Coastal Battery", priority = 60)]
        public static void Run()
        {
            EnsureFolder(Root + "/Resources/Enemies");
            var artilleryDef = FindEnemy("ene_artillery_boat");
            var artillery = artilleryDef != null && artilleryDef.Prefab != null ? artilleryDef.Prefab.GetComponent<ArtilleryBoat>() : null;
            if (artillery == null) { Debug.LogError("[Coastal Battery] 포격정 프리팹을 찾지 못했습니다(포탄을 빌린다)."); return; }
            var aso = new SerializedObject(artillery);
            var shell = aso.FindProperty("shellPrefab").objectReferenceValue as GameObject;
            var flash = aso.FindProperty("muzzleFlash").objectReferenceValue as GameObject;

            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DefPath) ?? CreateSO<EnemyDefinition>(DefPath);
            var prefab = BuildPrefab(def, shell, flash);
            Configure(def, so =>
            {
                Set(so, "id", "ene_coastal_battery");
                Set(so, "displayName", "해안포대");
                Set(so, "kind", (int)TargetKind.Surface);
                Set(so, "prefab", prefab);
                Set(so, "category", (int)TargetCategory.LargeSurface);
                Set(so, "rank", (int)EnemyRank.Normal);
                Set(so, "maxHp", 110f);
                Set(so, "moveSpeed", 0f);
                Set(so, "turnRateDegPerSec", 0f);
                Set(so, "preferredRange", 45f);   // 기본 레이더(해역 배율 후 42~47m) 안 — 보이지 않는 곳에서 쏘지 않게
                Set(so, "attackDamage", 7f);
                Set(so, "attackCooldown", 5f);
                Set(so, "xpReward", 5);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[Coastal Battery] ENE_CoastalBattery · ene_coastal_battery 생성 완료.");
        }

        private static GameObject BuildPrefab(EnemyDefinition def, GameObject shell, GameObject flash)
        {
            var concrete = Mat("Enemy superstructure grey");
            var hull = Mat("Enemy hull grey");
            var dark = Mat("Enemy Dark");
            var gun = Mat("Enemy weapon dark");
            var red = Mat("Warning red");
            var rock = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Resources/Islands/MAT_IslandRock.mat");

            var root = new GameObject("ENE_CoastalBattery");
            try
            {
                root.layer = Factions.EnemyLayer;
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(4.6f, 3f, 4.6f);
                col.center = new Vector3(0f, 1.2f, 0f);

                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                Part(visual, "Rock plinth", PrimitiveType.Cylinder, new Vector3(0f, -1.2f, 0f), new Vector3(5.6f, 1.8f, 5.6f), rock);
                Part(visual, "Emplacement", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(4.4f, 0.25f, 4.4f), concrete);
                Part(visual, "Emplacement lip", PrimitiveType.Cylinder, new Vector3(0f, 1.12f, 0f), new Vector3(3.6f, 0.05f, 3.6f), dark);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    if (a < 30f || a > 330f) continue;   // 앞쪽(+Z)은 포신이 지나가게 비운다
                    var bag = Part(visual, "Sandbags", PrimitiveType.Cube, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 1.35f, 2.0f), new Vector3(1.4f, 0.5f, 0.5f), dark);
                    bag.localRotation = Quaternion.Euler(0f, a, 0f);
                }

                var turret = new GameObject("TurretPivot").transform;
                turret.SetParent(visual, false);
                turret.localPosition = new Vector3(0f, 1.1f, 0f);
                Part(turret, "Turret ring", PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(1.9f, 0.15f, 1.9f), gun);
                Part(turret, "Gun house", PrimitiveType.Cube, new Vector3(0f, 0.75f, -0.15f), new Vector3(1.7f, 0.95f, 2.0f), hull);
                Part(turret, "Gun house roof", PrimitiveType.Cube, new Vector3(0f, 1.27f, -0.2f), new Vector3(1.5f, 0.12f, 1.6f), concrete);
                Part(turret, "Front plate", PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.82f), new Vector3(1.55f, 0.8f, 0.14f), hull);
                Part(turret, "Range finder", PrimitiveType.Cube, new Vector3(0f, 1.42f, -0.55f), new Vector3(1.2f, 0.16f, 0.22f), gun);
                Part(turret, "Warning band", PrimitiveType.Cube, new Vector3(0.86f, 0.75f, -0.15f), new Vector3(0.03f, 0.18f, 1.6f), red);
                Part(turret, "Warning band", PrimitiveType.Cube, new Vector3(-0.86f, 0.75f, -0.15f), new Vector3(0.03f, 0.18f, 1.6f), red);

                var elevation = new GameObject("ElevationPivot").transform;
                elevation.SetParent(turret, false);
                elevation.localPosition = new Vector3(0f, 0.72f, 0.85f);
                var barrel = Part(elevation, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 1.3f), new Vector3(0.24f, 1.3f, 0.24f), gun);
                barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(elevation, "Barrel jacket", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.45f), new Vector3(0.36f, 0.45f, 0.36f), gun).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(elevation, "Muzzle brake", PrimitiveType.Cube, new Vector3(0f, 0f, 2.55f), new Vector3(0.36f, 0.32f, 0.3f), dark);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(elevation, false);
                muzzle.localPosition = new Vector3(0f, 0f, 2.75f);

                SetLayer(root.transform, Factions.EnemyLayer);
                var battery = root.AddComponent<CoastalBattery>();
                Configure(battery, so =>
                {
                    Set(so, "definition", def);
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "shellPrefab", shell);
                    Set(so, "muzzleFlash", flash);
                });
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");

        private static EnemyDefinition FindEnemy(string id)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (d != null && d.Id == id) return d;
            }
            return null;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
