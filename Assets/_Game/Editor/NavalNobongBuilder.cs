using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 노봉 40mm 쌍열포(2026-10-08) 블록만 만든다 — 다른 프리팹·데이터·씬은 건드리지 않는다.
    ///   1. Prefabs/Projectiles/PRJ_Nobong.prefab: 기관포탄(PRJ_Autocannon)을 복사, 파편 반경·근접신관·물기둥을 더한다
    ///   2. Prefabs/Modules/MOD_Nobong.prefab: 받침 + 둥근 포탑 + 고각부 + 쌍열 포신(그레이박스). 아트가 오면 교체.
    ///   3. Resources/Modules/mod_nobong.asset(설치 카드·사전), 4. Art/Icons/ICON_mod_nobong.png
    /// 수치 근거는 WEAPON_BLOCK_ROADMAP.md 5절.
    /// </summary>
    internal static class NavalNobongBuilder
    {
        private const string AutocannonShellPath = Root + "/Prefabs/Projectiles/PRJ_Autocannon.prefab";
        private const string Gun76ShellPath = Root + "/Prefabs/Projectiles/PRJ_Gun76.prefab";
        private const string ShellPath = Root + "/Prefabs/Projectiles/PRJ_Nobong.prefab";
        private const string PrefabPath = Root + "/Prefabs/Modules/MOD_Nobong.prefab";
        private const string DefPath = Root + "/Resources/Modules/mod_nobong.asset";
        private const string SmokePath = Root + "/Prefabs/VFX/FX_MuzzleSmoke.prefab";

        [MenuItem("Naval/Add Nobong 40mm", priority = 3)]
        public static void Build()
        {
            var baseShell = AssetDatabase.LoadAssetAtPath<GameObject>(AutocannonShellPath);
            var gun76Shell = AssetDatabase.LoadAssetAtPath<GameObject>(Gun76ShellPath);
            if (baseShell == null || gun76Shell == null) { Debug.LogError("[Nobong] PRJ_Autocannon·PRJ_Gun76 없음 — Setup Prototype Scene을 먼저 돌리세요."); return; }

            var shell = BuildShell(baseShell, gun76Shell);
            var prefab = BuildPrefab(shell, AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath));
            var def = BuildDefinition(prefab);
            var icon = NavalIconBaker.Bake(prefab, "mod_nobong");
            if (icon != null)
            {
                Configure(def, so => Set(so, "icon", icon));
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Nobong] 노봉 40mm 쌍열포 블록을 만들었습니다: {PrefabPath}, {DefPath}, {ShellPath}");
        }

        private static GameObject BuildShell(GameObject baseShell, GameObject gun76Shell)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(baseShell);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "PRJ_Nobong";
            go.transform.localScale *= 1.35f;
            GameObject water = null;
            var src = new SerializedObject(gun76Shell.GetComponent<Projectile>());
            water = src.FindProperty("waterEffect").objectReferenceValue as GameObject;
            Configure(go.GetComponent<Projectile>(), so =>
            {
                Set(so, "splashRadius", 1.2f);
                Set(so, "splashDamageRatio", 0.5f);
                Set(so, "impactEffectScale", 0.55f);
                Set(so, "proximityRadius", 2.5f);   // 표적 중심에서 — 드론 선체(폭 2.2)보다 약 1m 바깥까지
                Set(so, "proximityArmDistance", 3f);
                Set(so, "waterEffect", water);
                Set(so, "waterEffectScale", 0.18f);
                Set(so, "tracerTime", 0.1f);
                Set(so, "tracerColor", new Color(1f, 0.62f, 0.25f, 0.9f));   // 기관포(노랑)와 구별되는 주황 예광
                Set(so, "tracerWidth", 0.16f);
                Set(so, "lifeTime", 2.2f);
            });
            var saved = PrefabUtility.SaveAsPrefabAsset(go, ShellPath);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildPrefab(GameObject shell, GameObject smoke)
        {
            var hull = CreateMaterial("hull", new Color(0.45f, 0.5f, 0.55f));
            var gun = CreateMaterial("gun", new Color(0.3f, 0.32f, 0.35f), 0.3f, 0.4f);
            var shield = CreateMaterial("nobong_turret", new Color(0.62f, 0.66f, 0.7f), 0.15f, 0.4f);

            var root = new GameObject("MOD_Nobong");
            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(1.6f, 0.2f, 1.6f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var ring = Primitive("Ring", PrimitiveType.Cylinder, new Vector3(1.15f, 0.1f, 1.15f), gun, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.27f, 0f);

            // 포탑: 낮고 둥근 방패(원통 + 앞쪽 상자), 선회는 TurretPivot
            var turret = new GameObject("TurretPivot").transform;
            turret.SetParent(root.transform, false);
            turret.localPosition = new Vector3(0f, 0.35f, 0f);
            var drum = Primitive("Shield", PrimitiveType.Cylinder, new Vector3(1.05f, 0.28f, 1.05f), shield, turret);
            drum.transform.localPosition = new Vector3(0f, 0.28f, -0.08f);
            var front = Primitive("Front", PrimitiveType.Cube, new Vector3(0.78f, 0.5f, 0.5f), shield, turret);
            front.transform.localPosition = new Vector3(0f, 0.3f, 0.3f);
            var sight = Primitive("Sight", PrimitiveType.Cube, new Vector3(0.16f, 0.14f, 0.2f), gun, turret);
            sight.transform.localPosition = new Vector3(0.3f, 0.6f, 0.15f);

            // 고각부 + 쌍열 포신(좌우 0.14m)
            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(turret, false);
            elevation.localPosition = new Vector3(0f, 0.34f, 0.5f);
            foreach (float x in new[] { -0.14f, 0.14f })
            {
                var barrel = Primitive("Barrel", PrimitiveType.Cylinder, new Vector3(0.09f, 0.62f, 0.09f), gun, elevation);
                barrel.transform.localPosition = new Vector3(x, 0f, 0.62f);
                barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var flash = Primitive("FlashHider", PrimitiveType.Cylinder, new Vector3(0.13f, 0.08f, 0.13f), gun, elevation);
                flash.transform.localPosition = new Vector3(x, 0f, 1.22f);
                flash.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(elevation, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.3f);

            var comp = root.AddComponent<NobongModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "weapon.aimTolerance", 5f);
                Set(so, "projectilePrefab", shell);
                Set(so, "muzzleSmoke", smoke);
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
                Set(so, "id", "mod_nobong");
                Set(so, "displayName", "노봉 40mm 쌍열포");
                Set(so, "description",
                    "국산 40mm 쌍열 함포. 수상 0~28, 대공 20 — 기관포보다 멀리, 76mm보다 빠르게 쏜다. " +
                    "두 포신이 함께 쏘고(4회 쏘고 잠깐 쉼), 근접신관 파편탄이라 드론·항공기 곁을 지나기만 해도 공중에서 터져 주변 1.2 안에 파편 피해를 준다. " +
                    "드론·항공기를 먼저 노린다. 자체 센서가 없어 레이더가 잡은 표적만 쏜다. 붙은 블록·상부 구조물 방향으로는 쏘지 못한다. 탄약고 보너스를 받는다.");
                Set(so, "type", (int)ModuleType.Nobong);
                Set(so, "width", 1);
                Set(so, "height", 1);
                Set(so, "canRotate", true);
                Set(so, "placement.Zone", (int)PlacementZone.Anywhere);
                Set(so, "heightClass", (int)ModuleHeight.Mid);
                Set(so, "maxCount", 0);
                Set(so, "maxHp", 40f);
                Set(so, "rarity", (int)Rarity.Uncommon);
                Set(so, "weight", 1f);
                Set(so, "prefab", prefab);

                Set(so, "stats.Range", 28f);
                Set(so, "stats.MinRange", 0f);
                Set(so, "stats.Damage", 3.5f);           // 한 발(쌍열이라 한 번에 두 발)
                Set(so, "stats.ReloadTime", 0.45f);
                Set(so, "stats.ProjectileSpeed", 85f);
                Set(so, "stats.TurretTurnRate", 150f);
                Set(so, "stats.FireArcDegrees", 360f);
                Set(so, "stats.AmmoFamily", (int)AmmoFamily.Gun);
                Set(so, "stats.MagazineCapacity", 96);   // 96발 = 48회
                Set(so, "stats.AmmoPerShot", 2);
                Set(so, "stats.AmmoReloadAmount", 12);
                Set(so, "stats.AmmoReloadTime", 4f);

                Set(so, "targetEfficiency.SmallSurface", 0.9f);
                Set(so, "targetEfficiency.MediumSurface", 0.8f);
                Set(so, "targetEfficiency.LargeSurface", 0.5f);
                Set(so, "targetEfficiency.Boss", 0.4f);
                Set(so, "targetEfficiency.Submarine", 0.6f);
                Set(so, "targetEfficiency.Drone", 1f);
                Set(so, "targetEfficiency.Air", 1f);
                Set(so, "targetEfficiency.Missile", 0.4f);
            });
            EditorUtility.SetDirty(def);
            return def;
        }
    }
}
