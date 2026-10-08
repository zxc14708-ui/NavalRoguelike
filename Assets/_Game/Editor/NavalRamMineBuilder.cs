using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 충각 함수·선미 기뢰 투하궤(2026-10-08) 블록만 만든다 — 다른 프리팹·데이터·씬은 건드리지 않는다.
    ///   - Prefabs/Projectiles/PRJ_PlayerMine.prefab: 적 부유 기뢰(ENE_SeaMine) 모양을 복사, 적 스크립트·충돌체를 떼고 PlayerMine(초록 등)
    ///   - Prefabs/Modules/MOD_RamBow.prefab, MOD_MineRail.prefab(그레이박스), Resources/Modules/mod_rambow·mod_minerail, 아이콘
    /// 수치는 WEAPON_BLOCK_ROADMAP.md 3절.
    /// </summary>
    internal static class NavalRamMineBuilder
    {
        private const string SeaMinePath = Root + "/Prefabs/Enemies/ENE_SeaMine.prefab";
        private const string BlastPath = Root + "/Prefabs/VFX/FX_WaterBlast.prefab";
        private const string MinePath = Root + "/Prefabs/Projectiles/PRJ_PlayerMine.prefab";
        private const string RailPath = Root + "/Prefabs/Modules/MOD_MineRail.prefab";
        private const string RamPath = Root + "/Prefabs/Modules/MOD_RamBow.prefab";
        private const string RailDef = Root + "/Resources/Modules/mod_minerail.asset";
        private const string RamDef = Root + "/Resources/Modules/mod_rambow.asset";

        [MenuItem("Naval/Add Ram Bow and Mine Rail", priority = 3)]
        public static void Build()
        {
            var seaMine = AssetDatabase.LoadAssetAtPath<GameObject>(SeaMinePath);
            var blast = AssetDatabase.LoadAssetAtPath<GameObject>(BlastPath);
            if (seaMine == null || blast == null) { Debug.LogError("[RamMine] ENE_SeaMine·FX_WaterBlast 없음 — Setup Prototype Scene을 먼저 돌리세요."); return; }

            var mine = BuildMine(seaMine, blast);
            var rail = BuildRail(mine);
            var ram = BuildRam(blast);

            var railDef = Definition(RailDef, "mod_minerail", "기뢰 투하궤",
                "선미 레일. 함선이 나아가는 동안 뒤쪽 30 m 안에서 쫓아오는 적 수상함이 있으면 항적에 기뢰를 떨어뜨린다. " +
                "기뢰는 1.2초 뒤 작동하고 적 선체가 닿으면 반경 3 안의 적 모두에 피해를 준다(아군에는 반응하지 않음). 35초 뒤 가라앉는다. " +
                "뒤가 트인 자리에만 놓인다.",
                ModuleType.MineRail, PlacementZone.SternOnly, 30f, Rarity.Uncommon, 0.9f, 0, rail, so =>
                {
                    Set(so, "stats.Damage", 26f);
                    Set(so, "stats.ReloadTime", 2.5f);
                    Set(so, "stats.Range", 30f);
                    Set(so, "stats.AmmoFamily", (int)AmmoFamily.Asw);
                    Set(so, "stats.MagazineCapacity", 6);
                    Set(so, "stats.AmmoPerShot", 1);
                    Set(so, "stats.AmmoReloadAmount", 1);
                    Set(so, "stats.AmmoReloadTime", 6f);
                    Efficiency(so, 1f, 1f, 0.8f, 0.5f);
                });
            var ramDef = Definition(RamDef, "mod_rambow", "충각 함수",
                "맨 앞 칸에 붙이는 강화 함수. 최고 속력의 20% 이상으로 나아가는 동안 앞 2.4 m에 들어온 적 수상함을 들이받는다 — " +
                "빠를수록 세다(전속과 잘 맞음). 자폭 보트는 터지기 전에 받혀 격침된다. 대형함·보스를 받으면 충각도 반동 피해를 입는다. " +
                "앞이 트인 자리에만 놓인다.",
                ModuleType.RamBow, PlacementZone.BowOnly, 70f, Rarity.Uncommon, 0.8f, 2, ram, so =>
                {
                    Set(so, "stats.Damage", 40f);
                    Efficiency(so, 1f, 1f, 0.7f, 0.5f);
                });

            foreach (var (def, prefab, id) in new[] { (railDef, rail, "mod_minerail"), (ramDef, ram, "mod_rambow") })
            {
                var icon = NavalIconBaker.Bake(prefab, id);
                if (icon == null) continue;
                Configure(def, so => Set(so, "icon", icon));
                EditorUtility.SetDirty(def);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[RamMine] 충각 함수·기뢰 투하궤 블록을 만들었습니다: {RamPath}, {RailPath}, {MinePath}");
        }

        /// <summary>적 기뢰 모양에서 스크립트·충돌체를 떼고 아군 기뢰로.</summary>
        private static GameObject BuildMine(GameObject seaMine, GameObject blast)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(seaMine);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "PRJ_PlayerMine";
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            go.transform.localScale = Vector3.one * 0.85f;
            SetLayerRecursive(go, 0);

            var mine = go.AddComponent<PlayerMine>();
            var bob = FindDeep(go.transform, "Bob");
            var lamp = FindDeep(go.transform, "Lamp");
            Configure(mine, so =>
            {
                Set(so, "blastEffect", blast);
                Set(so, "bob", bob);
                Set(so, "lamp", lamp != null ? lamp.GetComponent<Renderer>() : null);
            });
            var saved = PrefabUtility.SaveAsPrefabAsset(go, MinePath);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static GameObject BuildRail(GameObject mine)
        {
            var hull = CreateMaterial("hull", new Color(0.45f, 0.5f, 0.55f));
            var gun = CreateMaterial("gun", new Color(0.3f, 0.32f, 0.35f), 0.3f, 0.4f);
            var mineMat = CreateMaterial("mine_rack", new Color(0.24f, 0.3f, 0.26f), 0.2f, 0.35f);
            var lampMat = CreateMaterial("mine_lamp_friendly", new Color(0.3f, 1f, 0.55f));

            var root = new GameObject("MOD_MineRail");
            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(1.7f, 0.2f, 1.7f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            // 선미(-Z)로 살짝 기운 레일 두 줄과 끝의 투하 턱
            foreach (float x in new[] { -0.35f, 0.35f })
            {
                var r = Primitive("Rail", PrimitiveType.Cube, new Vector3(0.08f, 0.08f, 1.8f), gun, root.transform);
                r.transform.localPosition = new Vector3(x, 0.32f, -0.05f);
                r.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            }
            var lip = Primitive("DropLip", PrimitiveType.Cube, new Vector3(0.95f, 0.06f, 0.18f), gun, root.transform);
            lip.transform.localPosition = new Vector3(0f, 0.22f, -0.95f);
            var guard = Primitive("Guard", PrimitiveType.Cube, new Vector3(1.1f, 0.35f, 0.08f), hull, root.transform);
            guard.transform.localPosition = new Vector3(0f, 0.38f, 0.82f);

            var racked = new List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                var m = new GameObject($"RackedMine_{i + 1:00}").transform;
                m.SetParent(root.transform, false);
                m.localPosition = new Vector3(0f, 0.52f, 0.5f - i * 0.55f);
                var body = Primitive("Body", PrimitiveType.Sphere, Vector3.one * 0.42f, mineMat, m);
                body.transform.localPosition = Vector3.zero;
                foreach (var dir in new[] { Vector3.up, Vector3.left, Vector3.right })
                {
                    var horn = Primitive("Horn", PrimitiveType.Cylinder, new Vector3(0.05f, 0.06f, 0.05f), gun, m);
                    horn.transform.localPosition = dir * 0.22f;
                    horn.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
                }
                var lamp = Primitive("Lamp", PrimitiveType.Sphere, Vector3.one * 0.08f, lampMat, m);
                lamp.transform.localPosition = new Vector3(0f, 0.27f, 0f);
                racked.Add(m);
            }
            var drop = new GameObject("DropPoint").transform;
            drop.SetParent(root.transform, false);
            drop.localPosition = new Vector3(0f, 0f, -1.6f);

            var comp = root.AddComponent<MineRailModule>();
            Configure(comp, so =>
            {
                Set(so, "minePrefab", mine);
                Set(so, "dropPoint", drop);
                SetArray(so, "rackedMines", racked.ToArray());
            });
            var saved = PrefabUtility.SaveAsPrefabAsset(root, RailPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static GameObject BuildRam(GameObject blast)
        {
            var hull = CreateMaterial("hull", new Color(0.45f, 0.5f, 0.55f));
            var steel = CreateMaterial("ram_steel", new Color(0.26f, 0.29f, 0.32f), 0.55f, 0.45f);
            var stripe = CreateMaterial("ram_stripe", new Color(0.75f, 0.18f, 0.14f));

            var root = new GameObject("MOD_RamBow");
            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(1.8f, 0.22f, 1.8f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            // 충각: 앞(+Z)으로 블록 밖 1m까지 나온 납작한 쐐기(마름모 프리즘의 앞쪽 절반이 보인다)
            var wedgeFrame = new GameObject("RamWedge").transform;
            wedgeFrame.SetParent(root.transform, false);
            wedgeFrame.localPosition = new Vector3(0f, 0.32f, 0.55f);
            wedgeFrame.localScale = new Vector3(1f, 1f, 1.55f);
            var wedge = Primitive("Wedge", PrimitiveType.Cube, new Vector3(1.25f, 0.5f, 1.25f), steel, wedgeFrame);
            wedge.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var band = new GameObject("StripeFrame").transform;
            band.SetParent(root.transform, false);
            band.localPosition = new Vector3(0f, 0.3f, 0.55f);   // 쐐기 옆면을 두르는 띠
            band.localScale = new Vector3(1f, 1f, 1.55f);
            var bandCube = Primitive("Stripe", PrimitiveType.Cube, new Vector3(1.28f, 0.08f, 1.28f), stripe, band);
            bandCube.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            // 보강 판재와 리벳 줄
            for (int i = 0; i < 3; i++)
            {
                var plate = Primitive($"Rib_{i + 1}", PrimitiveType.Cube, new Vector3(1.5f - i * 0.25f, 0.12f, 0.12f), steel, root.transform);
                plate.transform.localPosition = new Vector3(0f, 0.32f, -0.6f + i * 0.4f);
            }

            var comp = root.AddComponent<RamBowModule>();
            Configure(comp, so => Set(so, "impactEffect", blast));
            var saved = PrefabUtility.SaveAsPrefabAsset(root, RamPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static ModuleDefinition Definition(string path, string id, string name, string desc, ModuleType type, PlacementZone zone,
                                                   float hp, Rarity rarity, float weight, int maxCount, GameObject prefab,
                                                   System.Action<SerializedObject> stats)
        {
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(path);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ModuleDefinition>();
                AssetDatabase.CreateAsset(def, path);
            }
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "description", desc);
                Set(so, "type", (int)type);
                Set(so, "width", 1);
                Set(so, "height", 1);
                Set(so, "canRotate", false);   // 선수·선미 방향이 정해진 블록
                Set(so, "placement.Zone", (int)zone);
                Set(so, "heightClass", (int)ModuleHeight.Low);
                Set(so, "maxCount", maxCount);
                Set(so, "maxHp", hp);
                Set(so, "rarity", (int)rarity);
                Set(so, "weight", weight);
                Set(so, "prefab", prefab);
                stats(so);
            });
            EditorUtility.SetDirty(def);
            return def;
        }

        private static void Efficiency(SerializedObject so, float small, float medium, float large, float boss)
        {
            Set(so, "targetEfficiency.SmallSurface", small);
            Set(so, "targetEfficiency.MediumSurface", medium);
            Set(so, "targetEfficiency.LargeSurface", large);
            Set(so, "targetEfficiency.Boss", boss);
            Set(so, "targetEfficiency.Submarine", 0f);
            Set(so, "targetEfficiency.Drone", 0f);
            Set(so, "targetEfficiency.Air", 0f);
            Set(so, "targetEfficiency.Missile", 0f);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
