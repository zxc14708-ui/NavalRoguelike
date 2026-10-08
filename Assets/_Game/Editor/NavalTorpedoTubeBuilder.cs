using UnityEditor;
using UnityEngine;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 경어뢰 발사관(2026-10-08) 블록만 만든다 — 다른 프리팹·데이터·씬은 건드리지 않는다.
    ///   1. Prefabs/Modules/MOD_TorpedoTube.prefab: 낮은 받침 + 도는 3연장 발사관(그레이박스). 아트가 오면 교체.
    ///   2. Resources/Modules/mod_torpedotube.asset: RefitDraft가 Resources/Modules에서 읽어 설치 카드·사전에 넣는다.
    ///   3. Art/Icons/ICON_mod_torpedotube.png
    /// 수치는 WEAPON_BLOCK_ROADMAP.md 0절.
    /// </summary>
    internal static class NavalTorpedoTubeBuilder
    {
        private const string PrefabPath = Root + "/Prefabs/Modules/MOD_TorpedoTube.prefab";
        private const string DefPath = Root + "/Resources/Modules/mod_torpedotube.asset";
        private const string TorpedoPath = Root + "/Prefabs/Projectiles/TOR_PlayerAsw.prefab";

        [MenuItem("Naval/Add Torpedo Tube", priority = 3)]
        public static void Build()
        {
            var torpedo = AssetDatabase.LoadAssetAtPath<GameObject>(TorpedoPath);
            if (torpedo == null) { Debug.LogError($"[TorpedoTube] {TorpedoPath} 없음 — Setup Prototype Scene을 먼저 돌리세요."); return; }

            var prefab = BuildPrefab(torpedo);
            var def = BuildDefinition(prefab);
            var icon = NavalIconBaker.Bake(prefab, "mod_torpedotube");
            if (icon != null)
            {
                Configure(def, so => Set(so, "icon", icon));
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[TorpedoTube] 경어뢰 발사관 블록을 만들었습니다: {PrefabPath}, {DefPath}");
        }

        private static GameObject BuildPrefab(GameObject torpedo)
        {
            var hull = CreateMaterial("hull", new Color(0.45f, 0.5f, 0.55f));
            var gun = CreateMaterial("gun", new Color(0.3f, 0.32f, 0.35f), 0.3f, 0.4f);
            var tubeMat = CreateMaterial("torpedo_tube", new Color(0.62f, 0.68f, 0.72f), 0.2f, 0.35f);

            var root = new GameObject("MOD_TorpedoTube");
            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(1.7f, 0.2f, 1.7f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var pedestal = Primitive("Pedestal", PrimitiveType.Cylinder, new Vector3(0.6f, 0.15f, 0.6f), gun, root.transform);
            pedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            // 3연장 발사관: 아래 2개 + 위 1개(삼각 배열), 발사관 묶음은 쏘는 현측으로 돈다
            var mount = new GameObject("TubeMount").transform;
            mount.SetParent(root.transform, false);
            mount.localPosition = new Vector3(0f, 0.55f, 0f);
            var cradle = Primitive("Cradle", PrimitiveType.Cube, new Vector3(0.75f, 0.12f, 0.6f), gun, mount);
            cradle.transform.localPosition = new Vector3(0f, -0.05f, -0.1f);

            Vector3[] tubes = { new(-0.2f, 0.12f, 0f), new(0.2f, 0.12f, 0f), new(0f, 0.42f, 0f) };
            var points = new Transform[tubes.Length];
            for (int i = 0; i < tubes.Length; i++)
            {
                var tube = Primitive($"Tube_{i + 1:00}", PrimitiveType.Cylinder, new Vector3(0.3f, 0.65f, 0.3f), tubeMat, mount);
                tube.transform.localPosition = tubes[i];
                tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 원통 축 = 묶음 앞(+Z)
                var cap = Primitive($"Muzzle_{i + 1:00}", PrimitiveType.Cylinder, new Vector3(0.32f, 0.03f, 0.32f), gun, mount);
                cap.transform.localPosition = tubes[i] + new Vector3(0f, 0f, 0.66f);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                var lp = new GameObject($"LaunchPoint_{i + 1:00}").transform;
                lp.SetParent(mount, false);
                lp.localPosition = tubes[i] + new Vector3(0f, 0f, 0.75f);
                points[i] = lp;
            }

            var comp = root.AddComponent<TorpedoTubeModule>();
            Configure(comp, so =>
            {
                Set(so, "torpedoPrefab", torpedo);
                Set(so, "mount", mount);
                SetArray(so, "launchPoints", points);
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
                Set(so, "id", "mod_torpedotube");
                Set(so, "displayName", "경어뢰 발사관");
                Set(so, "description",
                    "소나로 확인한 잠수함(또는 어뢰 발사 흔적) 쪽으로 경어뢰 3발을 부채꼴로 차례로 쏜다(8~45). " +
                    "어뢰는 접촉 지점 둘레를 찾다가 가까운 잠수함을 추적하며, 잠항 중에도 맞는다. " +
                    "좌현이나 우현이 트인 자리에만 놓이고 트인 현측으로만 쏜다. 대잠 전용.");
                Set(so, "type", (int)ModuleType.TorpedoTube);
                Set(so, "width", 1);
                Set(so, "height", 1);
                Set(so, "canRotate", true);
                Set(so, "placement.Zone", (int)PlacementZone.SideOnly);
                Set(so, "heightClass", (int)ModuleHeight.Low);
                Set(so, "maxCount", 0);
                Set(so, "maxHp", 30f);
                Set(so, "rarity", (int)Rarity.Uncommon);
                Set(so, "weight", 1f);
                Set(so, "prefab", prefab);

                Set(so, "stats.Range", 45f);
                Set(so, "stats.MinRange", 8f);
                Set(so, "stats.Damage", 15f);
                Set(so, "stats.ReloadTime", 10f);          // 부채꼴 사이 간격
                Set(so, "stats.FireArcDegrees", 150f);     // 현측 정횡 ±75°(표시용, 판정은 TorpedoTubeModule)
                Set(so, "stats.AmmoFamily", (int)AmmoFamily.Asw);
                Set(so, "stats.MagazineCapacity", 6);      // 부채꼴 2회
                Set(so, "stats.AmmoPerShot", 1);
                Set(so, "stats.AmmoReloadAmount", 1);
                Set(so, "stats.AmmoReloadTime", 9f);       // 9초마다 +1

                Set(so, "targetEfficiency.SmallSurface", 0f);
                Set(so, "targetEfficiency.MediumSurface", 0f);
                Set(so, "targetEfficiency.LargeSurface", 0f);
                Set(so, "targetEfficiency.Boss", 0f);
                Set(so, "targetEfficiency.Submarine", 1f);
                Set(so, "targetEfficiency.Drone", 0f);
                Set(so, "targetEfficiency.Air", 0f);
                Set(so, "targetEfficiency.Missile", 0f);
            });
            EditorUtility.SetDirty(def);
            return def;
        }
    }
}
