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
        /// <summary>Codex 해안포대 모델(2026-10-13 v27 각형). 있으면 기본 도형 대신 이것을 쓴다(NavalCoastalSupplyArt가 넣는다).</summary>
        public const string ModelPath = Root + "/Art/Models/Coastal/ENE_CoastalBattery_Art.fbx";

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

                // Codex 모델(v27): 진지 바닥(다듬은 돌 기초)을 수면(y −0.9) 바로 위 y −0.55에 두어 바다에서 솟은 요새처럼,
                // 그 아래 바위 받침은 거의 물속(y −3 ~ −0.5) — 둥근 바위가 네모 진지 밑에 드러나 보이지 않게
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (art != null)
                {
                    col.center = new Vector3(0f, 0.75f, 0f);
                    Part(visual, "Rock plinth", PrimitiveType.Cylinder, new Vector3(0f, -1.75f, 0f), new Vector3(5.9f, 1.25f, 5.9f), rock);
                    var model = NavalPlayerAviationArt.Mount(art, visual, "ModelPivot", 1f, "ObserverRoof", "CutStoneFoundation", "Muzzle", "TurretPivot");
                    model.parent.localPosition += Vector3.down * 0.55f;
                    var turretArt = NavalPlayerAviationArt.Deep(model, "TurretPivot");
                    var elevationArt = NavalPlayerAviationArt.Deep(model, "ElevationPivot");
                    var muzzleArt = NavalPlayerAviationArt.Deep(model, "Muzzle");
                    if (turretArt == null || elevationArt == null || muzzleArt == null)
                        Debug.LogError("[Coastal Battery] 모델에서 TurretPivot·ElevationPivot·Muzzle을 찾지 못했습니다.");
                    else CloseMuzzleGap(elevationArt, muzzleArt);
                    SetLayer(root.transform, Factions.EnemyLayer);
                    var artBattery = root.AddComponent<CoastalBattery>();
                    Configure(artBattery, so =>
                    {
                        Set(so, "definition", def);
                        Set(so, "weapon.turret", turretArt);
                        Set(so, "weapon.elevationPivot", elevationArt);
                        Set(so, "weapon.muzzle", muzzleArt);
                        Set(so, "shellPrefab", shell);
                        Set(so, "muzzleFlash", flash);
                    });
                    return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }

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

        /// <summary>
        /// v27 모델은 포신(TaperedGunTube)을 줄이면서 포구(OpenMuzzle)는 그대로 두어 사이가 약 25cm 떠 있다.
        /// 포신 뒤끝은 그대로 두고 앞끝이 포구 안쪽 3cm까지 오도록 포신만 길이 방향으로 늘린다(틈이 없으면 그대로).
        /// </summary>
        private static void CloseMuzzleGap(Transform elevation, Transform muzzle)
        {
            var tube = NavalPlayerAviationArt.Deep(elevation, "TaperedGunTube");
            var mouth = NavalPlayerAviationArt.Deep(elevation, "OpenMuzzle");
            if (tube == null || mouth == null) return;
            Vector3 fwd = (muzzle.position - elevation.position).normalized;
            (float lo, float hi) Span(Transform t)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return (0f, 0f);
                var b = mf.sharedMesh.bounds;
                float lo = float.MaxValue, hi = float.MinValue;
                for (int i = 0; i < 8; i++)
                {
                    var c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    float d = Vector3.Dot(t.TransformPoint(c), fwd);
                    lo = Mathf.Min(lo, d);
                    hi = Mathf.Max(hi, d);
                }
                return (lo, hi);
            }
            var (tubeLo, tubeHi) = Span(tube);
            // 포구의 바깥 벽(포신 반지름의 80% 넘게 축에서 떨어진 꼭짓점) 시작점 — 안쪽 구멍 벽은 더 뒤에서 시작해서 빼야 한다
            var tubeMesh = tube.GetComponent<MeshFilter>().sharedMesh;
            var mouthMesh = mouth.GetComponent<MeshFilter>() != null ? mouth.GetComponent<MeshFilter>().sharedMesh : null;
            if (mouthMesh == null) return;
            Vector3 axisPoint = tube.TransformPoint(tubeMesh.bounds.center);
            float tubeRadius = 0f;
            foreach (var v in tubeMesh.vertices)
            {
                var w = tube.TransformPoint(v) - axisPoint;
                tubeRadius = Mathf.Max(tubeRadius, (w - fwd * Vector3.Dot(w, fwd)).magnitude);
            }
            float mouthLo = float.MaxValue;
            foreach (var v in mouthMesh.vertices)
            {
                var w = mouth.TransformPoint(v) - axisPoint;
                if ((w - fwd * Vector3.Dot(w, fwd)).magnitude < tubeRadius * 0.8f) continue;
                mouthLo = Mathf.Min(mouthLo, Vector3.Dot(mouth.TransformPoint(v), fwd));
            }
            if (mouthLo == float.MaxValue) return;
            if (mouthLo - tubeHi <= 0.005f || tubeHi <= tubeLo) return;
            float target = mouthLo + 0.03f;
            Vector3 local = tube.InverseTransformDirection(fwd);
            int axis = Mathf.Abs(local.x) > Mathf.Abs(local.y) ? (Mathf.Abs(local.x) > Mathf.Abs(local.z) ? 0 : 2) : (Mathf.Abs(local.y) > Mathf.Abs(local.z) ? 1 : 2);
            var scale = tube.localScale;
            scale[axis] *= (target - tubeLo) / (tubeHi - tubeLo);
            tube.localScale = scale;
            var (newLo, newHi) = Span(tube);
            tube.position += fwd * (tubeLo - newLo);
            Debug.Log($"[Coastal Battery] 포신-포구 틈 {mouthLo - tubeHi:0.00}m를 메움(포신 {tubeHi - tubeLo:0.00} → {newHi - newLo:0.00}m)");
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
