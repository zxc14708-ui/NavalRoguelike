using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 곡사포(2026-10-08) 블록만 만든다 — 다른 프리팹·데이터·씬은 건드리지 않는다.
    ///   1. Prefabs/Projectiles/PRJ_Howitzer.prefab: 적 주포탄(PRJ_BossShell)을 복사하고 경고 원만 뺀다
    ///   2. Prefabs/Modules/MOD_Howitzer.prefab: 받침 + 도는 포탑 + 45° 들린 긴 포신(그레이박스). 아트가 오면 교체.
    ///   3. Resources/Modules/mod_howitzer.asset(설치 카드·사전), 4. Art/Icons/ICON_mod_howitzer.png
    /// 수치는 WEAPON_BLOCK_ROADMAP.md 2절.
    /// </summary>
    internal static class NavalHowitzerBuilder
    {
        private const string BossShellPath = Root + "/Prefabs/Projectiles/PRJ_BossShell.prefab";
        private const string ShellPath = Root + "/Prefabs/Projectiles/PRJ_Howitzer.prefab";
        private const string PrefabPath = Root + "/Prefabs/Modules/MOD_Howitzer.prefab";
        private const string DefPath = Root + "/Resources/Modules/mod_howitzer.asset";
        private const string FlashPath = Root + "/Prefabs/VFX/FX_GunFlash.prefab";

        [MenuItem("Naval/Add Howitzer", priority = 3)]
        public static void Build()
        {
            var bossShell = AssetDatabase.LoadAssetAtPath<GameObject>(BossShellPath);
            if (bossShell == null) { Debug.LogError($"[Howitzer] {BossShellPath} 없음 — Setup Prototype Scene을 먼저 돌리세요."); return; }

            var shell = BuildShell(bossShell);
            var prefab = BuildPrefab(shell, AssetDatabase.LoadAssetAtPath<GameObject>(FlashPath));
            var def = BuildDefinition(prefab);
            var icon = NavalIconBaker.Bake(prefab, "mod_howitzer");
            if (icon != null)
            {
                Configure(def, so => Set(so, "icon", icon));
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Howitzer] 곡사포 블록을 만들었습니다: {PrefabPath}, {DefPath}, {ShellPath}");
        }

        private static GameObject BuildShell(GameObject bossShell)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(bossShell);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "PRJ_Howitzer";
            go.transform.localScale = Vector3.one * 0.8f;
            Configure(go.GetComponent<ArtilleryShell>(), so => Set(so, "warningMarker", (Object)null));
            var saved = PrefabUtility.SaveAsPrefabAsset(go, ShellPath);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildPrefab(GameObject shell, GameObject flash)
        {
            var hull = CreateMaterial("hull", new Color(0.45f, 0.5f, 0.55f));
            var gun = CreateMaterial("gun", new Color(0.3f, 0.32f, 0.35f), 0.3f, 0.4f);
            var turretMat = CreateMaterial("howitzer_turret", new Color(0.55f, 0.6f, 0.64f), 0.15f, 0.35f);

            var root = new GameObject("MOD_Howitzer");
            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(1.7f, 0.2f, 1.7f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var ring = Primitive("Ring", PrimitiveType.Cylinder, new Vector3(1.3f, 0.08f, 1.3f), gun, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.24f, 0f);

            // 포탑: 각진 상자 + 앞쪽 경사 방패(+Z가 포구 쪽)
            var turret = new GameObject("TurretPivot").transform;
            turret.SetParent(root.transform, false);
            turret.localPosition = new Vector3(0f, 0.3f, 0f);
            var house = Primitive("House", PrimitiveType.Cube, new Vector3(1.15f, 0.55f, 1.25f), turretMat, turret);
            house.transform.localPosition = new Vector3(0f, 0.3f, -0.1f);
            var mantlet = Primitive("Mantlet", PrimitiveType.Cube, new Vector3(0.75f, 0.45f, 0.25f), turretMat, turret);
            mantlet.transform.localPosition = new Vector3(0f, 0.36f, 0.55f);
            mantlet.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            var hatch = Primitive("Hatch", PrimitiveType.Cylinder, new Vector3(0.28f, 0.04f, 0.28f), gun, turret);
            hatch.transform.localPosition = new Vector3(0.3f, 0.6f, -0.35f);

            // 포신: 45° 들린 긴 포신(곡사). Barrel이 쏠 때 뒤로 밀린다.
            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(turret, false);
            elevation.localPosition = new Vector3(0f, 0.42f, 0.5f);
            elevation.localRotation = Quaternion.Euler(-45f, 0f, 0f);
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(elevation, false);
            var tube = Primitive("Tube", PrimitiveType.Cylinder, new Vector3(0.17f, 0.8f, 0.17f), gun, barrel);
            tube.transform.localPosition = new Vector3(0f, 0f, 0.75f);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var brake = Primitive("MuzzleBrake", PrimitiveType.Cube, new Vector3(0.26f, 0.2f, 0.22f), gun, barrel);
            brake.transform.localPosition = new Vector3(0f, 0f, 1.55f);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(barrel, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.7f);

            var comp = root.AddComponent<HowitzerModule>();
            Configure(comp, so =>
            {
                Set(so, "shellPrefab", shell);
                Set(so, "muzzleFlash", flash);
                Set(so, "turret", turret);
                Set(so, "barrel", barrel);
                Set(so, "muzzle", muzzle);
            });

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static ModuleDefinition BuildDefinition(GameObject prefab)
        {
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(DefPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ModuleDefinition>();
                AssetDatabase.CreateAsset(def, DefPath);
            }
            Configure(def, so =>
            {
                Set(so, "id", "mod_howitzer");
                Set(so, "displayName", "곡사포");
                Set(so, "description",
                    "높은 포물선으로 쏘는 함포(14~44). 함교·레이더 너머와 섬 위로도 넘겨 쏘며 사격 금지 구역이 없다. " +
                    "착탄까지 1.4~3초 — 적이 진로를 유지하면 있을 곳을 겨누고, 반경 4 안의 적 수상함 모두에 피해를 준다. " +
                    "몰려 있는 고속정 무리를 먼저 노린다. 항공기·잠항 잠수함은 노리지 않는다.");
                Set(so, "type", (int)ModuleType.Howitzer);
                Set(so, "width", 1);
                Set(so, "height", 1);
                Set(so, "canRotate", true);
                Set(so, "placement.Zone", (int)PlacementZone.Anywhere);
                Set(so, "heightClass", (int)ModuleHeight.Mid);
                Set(so, "maxCount", 0);
                Set(so, "maxHp", 45f);
                Set(so, "rarity", (int)Rarity.Rare);
                Set(so, "weight", 0.9f);
                Set(so, "prefab", prefab);

                Set(so, "stats.Range", 44f);
                Set(so, "stats.MinRange", 14f);
                Set(so, "stats.Damage", 18f);
                Set(so, "stats.ReloadTime", 4f);
                Set(so, "stats.TurretTurnRate", 70f);
                Set(so, "stats.FireArcDegrees", 360f);
                Set(so, "stats.AmmoFamily", (int)AmmoFamily.Gun);
                Set(so, "stats.MagazineCapacity", 10);
                Set(so, "stats.AmmoPerShot", 1);
                Set(so, "stats.AmmoReloadAmount", 2);
                Set(so, "stats.AmmoReloadTime", 8f);

                Set(so, "targetEfficiency.SmallSurface", 1f);
                Set(so, "targetEfficiency.MediumSurface", 1f);
                Set(so, "targetEfficiency.LargeSurface", 0.8f);
                Set(so, "targetEfficiency.Boss", 0.6f);
                Set(so, "targetEfficiency.Submarine", 0.5f);
                Set(so, "targetEfficiency.Drone", 0f);
                Set(so, "targetEfficiency.Air", 0f);
                Set(so, "targetEfficiency.Missile", 0f);
            });
            EditorUtility.SetDirty(def);
            return def;
        }
    }
}
