using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// Codex 시안 적용(2026-10-11): 플레이어 헬기데크·대잠 헬기 T1~T3(ArtSource/PlayerAviation_v24), 노봉 v22(ArtSource/NobongAutomated_v22).
    /// 다른 블록·씬·데이터는 건드리지 않는다.
    ///   1. FBX(Art/Models/Aviation, Art/Models/Nobong) 임포트 설정 — 축 변환 굽기, 재질은 이름으로 기존 팔레트에 연결(.001 접미사 무시)
    ///   2. 출격 헬기 프리팹 HEL_PlayerAsw_T1~T3(Prefabs/Projectiles) — 기존 HEL_Asw의 무장·수치를 그대로 복사하고 모델·로터만 바꾼다
    ///   3. MOD_HeliDeck: Visual_Level1~3 = 단계별 데크 + 착함점(HeliSpot) + 그 단계 헬기(ParkedHelicopter). 단계별 출격 헬기 연결
    ///   4. Resources/Upgrades/UPG_HeliDeck — 강화 I·II 수치(출격 간격·무장 피해·수색 반경·속력·내구)
    ///   5. MOD_Nobong: Visual_Level1~3 = v22 모델, 조준 부품(TurretPivot·ElevationPivot·Muzzle) 다시 연결. 좌우 포구 Muzzle_01·02는 런타임이 찾는다
    ///   6. 아이콘(기본·강화 I·II)
    /// 모델의 위·앞 방향과 크기는 파일마다 노드 위치로 판정한다(위: 로터·착함점·고각부가 위, 앞: 기수·포구가 +Z, 크기: 데크 1.8m 기준).
    /// 배치 실행: -executeMethod Game.EditorTools.NavalPlayerAviationArt.Run
    /// </summary>
    public static class NavalPlayerAviationArt
    {
        private const string AviationDir = Root + "/Art/Models/Aviation";
        private const string NobongDir = Root + "/Art/Models/Nobong";
        private const string MaterialDir = Root + "/Art/Models/Materials";
        private const string HeliPrefabDir = Root + "/Prefabs/Projectiles";
        private const string BaseHeliPrefab = HeliPrefabDir + "/HEL_Asw.prefab";
        private const string DeckPrefab = Root + "/Prefabs/Modules/MOD_HeliDeck.prefab";
        private const string NobongPrefab = Root + "/Prefabs/Modules/MOD_Nobong.prefab";
        private const string ProfilePath = Root + "/Resources/Upgrades/UPG_HeliDeck.asset";
        private const float DeckFootprint = 1.8f;   // Codex manifest(데크 1.8×1.8m) — 같은 내보내기의 크기 기준

        private static readonly string[] DeckFiles = { "MOD_HeliDeck_T1", "MOD_HeliDeck_T2", "MOD_HeliDeck_T3" };
        private static readonly string[] HeliFiles = { "HEL_PlayerAsw_T1", "HEL_PlayerAsw_T2", "HEL_PlayerAsw_T3" };
        private static readonly string[] NobongFiles = { "MOD_Nobong_T1", "MOD_Nobong_T2", "MOD_Nobong_T3" };
        private static readonly string[] EscortHeliFiles = { "HEL_EscortAsw_T1", "HEL_EscortAsw_T2", "HEL_EscortAsw_T3" };

        /// <summary>
        /// 호위함 시안 적용(2026-10-12): 미사일 호위함 v18(ESC_STK_T1~T3, Art/Models/TaskForce에 덮어씀) 프리팹을 다시 만들고,
        /// 대잠 호위함 헬기 v19(HEL_EscortAsw_T1~T3)를 출격 헬기 프리팹으로 만들어 EscortArmory.helicoptersByTier에 연결한다.
        /// 배치 실행: -executeMethod Game.EditorTools.NavalPlayerAviationArt.RunEscort
        /// </summary>
        [MenuItem("Naval/Art/Apply Escort STK v18 · Escort Helicopters v19", priority = 44)]
        public static void RunEscort()
        {
            NavalEscortArtBuilder.BuildAll();
            foreach (var f in EscortHeliFiles) ImportAndRemap($"{AviationDir}/{f}.fbx");
            AssetDatabase.Refresh();
            var helis = BuildHelicopters(MeasureScale(), EscortHeliFiles);
            var armory = AssetDatabase.LoadAssetAtPath<Game.TaskForce.EscortArmory>(Root + "/Resources/TaskForce/EscortArmory.asset");
            if (helis == null || armory == null) { Debug.LogError("[Player Aviation] 호위함 헬기 또는 EscortArmory가 없습니다."); return; }
            armory.helicoptersByTier = helis;
            EditorUtility.SetDirty(armory);
            AssetDatabase.SaveAssets();
            Debug.Log("[Player Aviation] 미사일 호위함 v18 · 호위함 헬기 T1~T3 적용 완료.");
        }

        [MenuItem("Naval/Art/Apply Player Aviation v24 · Nobong v22", priority = 43)]
        public static void Run()
        {
            foreach (var f in DeckFiles) ImportAndRemap($"{AviationDir}/{f}.fbx");
            foreach (var f in HeliFiles) ImportAndRemap($"{AviationDir}/{f}.fbx");
            foreach (var f in NobongFiles) ImportAndRemap($"{NobongDir}/{f}.fbx");
            AssetDatabase.Refresh();

            float scale = MeasureScale();
            var helis = BuildHelicopters(scale, HeliFiles);
            if (helis != null) ApplyHeliDeck(helis, scale);
            EnsureHeliDeckProfile();
            ApplyNobongV22();
            AssetDatabase.SaveAssets();
            BakeIcons("mod_helideck", "mod_nobong");
            Debug.Log($"[Player Aviation] 헬기데크·헬기 T1~T3·노봉 v22 적용 완료(크기 배율 {scale:0.###}).");
        }

        /// <summary>
        /// 프로토타입 셋업·아트 새로고침(NavalPrototypeSetup)이 모듈 프리팹을 다시 만든 뒤 부른다(2026-10-12 — 셋업이 헬기데크를
        /// 예전 단일 외형으로 되돌린 일이 있었다). 이미 만든 출격 헬기 프리팹으로 데크 외형·노봉 v22만 다시 넣고 아이콘을 굽는다.
        /// </summary>
        public static void ReapplyAfterSetup()
        {
            var helis = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                helis[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{HeliPrefabDir}/{HeliFiles[i]}.prefab");
                if (helis[i] == null) { Debug.LogWarning("[Player Aviation] 출격 헬기 프리팹이 없어 헬기데크 외형을 다시 넣지 못했습니다(Naval/Art/Apply Player Aviation을 실행)."); return; }
            }
            ApplyHeliDeck(helis, MeasureScale());
            ApplyNobongV22();
            AssetDatabase.SaveAssets();
            BakeIcons("mod_helideck", "mod_nobong");
        }

        // ------------------------------------------------------------ 임포트

        internal static void ImportAndRemap(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.LogError($"[Player Aviation] 모델 없음: {path}"); return; }
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.bakeAxisConversion = true;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.SaveAndReimport();

            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) names.Add(m.name);
            foreach (var kv in importer.GetExternalObjectMap())
                if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
            bool remapped = false;
            foreach (var name in names)
            {
                var target = ResolveMaterial(name);
                if (target == null) { Debug.LogWarning($"[Player Aviation] {Path.GetFileName(path)}: 연결할 재질 없음 '{name}'"); continue; }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), target);
                remapped = true;
            }
            if (remapped) importer.SaveAndReimport();
        }

        private static Material ResolveMaterial(string fbxName)
        {
            string name = Regex.Replace(fbxName, @"\.\d+$", "");
            foreach (var dir in new[] { MaterialDir, MaterialDir + "/TaskForce" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{dir}/{name}.mat");
                if (m != null) return m;
            }
            foreach (var guid in AssetDatabase.FindAssets($"t:Material {name}", new[] { MaterialDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name == name) return m;
            }
            return null;
        }

        // ------------------------------------------------------------ 방향·크기

        /// <summary>
        /// 모델을 피벗 아래에 붙이고, 위(top 노드가 bottom 노드보다 위)·앞(front가 origin보다 +Z, 90° 단위)을 맞춘 뒤
        /// 배율을 곱하고 바닥을 부모 원점 높이에 둔다. 모델 자체 트랜스폼은 건드리지 않는다.
        /// </summary>
        internal static Transform Mount(GameObject asset, Transform parent, string pivotName, float scale,
                                       string top, string bottom, string front, string frontOrigin, bool groundAtZero = true)
        {
            var pivot = new GameObject(pivotName).transform;
            pivot.SetParent(parent, false);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.name = "Model";
            inst.transform.SetParent(pivot, false);

            Quaternion[] candidates =
            {
                Quaternion.identity, Quaternion.Euler(-90f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(180f, 0f, 0f),
            };
            var topT = Deep(inst.transform, top);
            var bottomT = Deep(inst.transform, bottom);   // 없으면 메시 경계 가운데와 비교한다
            if (topT != null)
            {
                Quaternion best = Quaternion.identity;
                float bestScore = float.MinValue;
                foreach (var q in candidates)
                {
                    pivot.localRotation = q;
                    Vector3 low = bottomT != null ? parent.InverseTransformPoint(bottomT.position)
                        : LocalBounds(parent, inst.transform, out var mb) ? mb.center : Vector3.zero;
                    Vector3 d = parent.InverseTransformPoint(topT.position) - low;
                    float score = d.magnitude > 1e-4f ? d.y / d.magnitude : 0f;
                    if (score > bestScore + 1e-3f) { bestScore = score; best = q; }
                }
                pivot.localRotation = best;
            }
            var frontT = Deep(inst.transform, front);
            var originT = Deep(inst.transform, frontOrigin);
            if (frontT != null && originT != null)
            {
                Vector3 f = parent.InverseTransformPoint(frontT.position) - parent.InverseTransformPoint(originT.position);
                f.y = 0f;
                if (f.sqrMagnitude > 1e-6f)
                {
                    float yaw = Mathf.Round(Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg / 90f) * 90f;
                    pivot.localRotation = Quaternion.Euler(0f, -yaw, 0f) * pivot.localRotation;
                }
            }
            pivot.localScale = Vector3.one * scale;
            if (groundAtZero && LocalBounds(parent, inst.transform, out var b))
                pivot.localPosition += Vector3.up * -b.min.y;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return inst.transform;
        }

        private static bool LocalBounds(Transform space, Transform model, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return any;
        }

        /// <summary>데크 T1 가로·세로가 1.8m가 되는 배율(파일 단위가 달라도 같은 내보내기 전체에 같은 배율).</summary>
        private static float MeasureScale()
        {
            var deck = AssetDatabase.LoadAssetAtPath<GameObject>($"{AviationDir}/{DeckFiles[0]}.fbx");
            if (deck == null) return 1f;
            var root = new GameObject("Measure");
            try
            {
                var model = Mount(deck, root.transform, "Pivot", 1f, "LandingSpot", "BlockBase", null, null);
                if (!LocalBounds(root.transform, model, out var b)) return 1f;
                float longest = Mathf.Max(b.size.x, b.size.z);
                float s = longest > 1e-4f ? DeckFootprint / longest : 1f;
                return Mathf.Abs(s - 1f) < 0.03f ? 1f : s;
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------ 헬기

        private static GameObject[] BuildHelicopters(float scale, string[] files)
        {
            var baseHeli = AssetDatabase.LoadAssetAtPath<GameObject>(BaseHeliPrefab);
            if (baseHeli == null) { Debug.LogError("[Player Aviation] HEL_Asw 프리팹이 없습니다."); return null; }
            var result = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{AviationDir}/{files[i]}.fbx");
                if (model == null) { Debug.LogError($"[Player Aviation] {files[i]}.fbx 없음"); return null; }
                string path = $"{HeliPrefabDir}/{files[i]}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(BaseHeliPrefab, path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    root.name = files[i];
                    for (int c = root.transform.childCount - 1; c >= 0; c--) Object.DestroyImmediate(root.transform.GetChild(c).gameObject);
                    var inst = Mount(model, root.transform, "ModelPivot", scale, "Rotor", null, "ChinEOHousing", "Rotor", false);
                    CenterVertically(root.transform, inst);
                    SetLayer(root.transform, root.layer);
                    var heli = root.GetComponent<AswHelicopter>();
                    Configure(heli, so =>
                    {
                        Set(so, "rotor", Deep(inst, "Rotor"));
                        Set(so, "tailRotor", Deep(inst, "TailRotor"));
                    });
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                result[i] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return result;
        }

        /// <summary>비행 헬기는 원점이 기체 가운데 높이(착함점에서 1.5m 위로 내려앉는다)가 되게 바닥만 원점에 맞춘다.</summary>
        private static void CenterVertically(Transform root, Transform model)
        {
            if (!LocalBounds(root, model, out var b)) return;
            model.parent.localPosition += Vector3.up * -b.min.y;
        }

        // ------------------------------------------------------------ 헬기데크

        private static void ApplyHeliDeck(GameObject[] helis, float scale)
        {
            var decks = new GameObject[3];
            var parkedModels = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                decks[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{AviationDir}/{DeckFiles[i]}.fbx");
                parkedModels[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{AviationDir}/{HeliFiles[i]}.fbx");
                if (decks[i] == null || parkedModels[i] == null) { Debug.LogError($"[Player Aviation] 데크/헬기 T{i + 1} 모델 없음"); return; }
            }

            var root = PrefabUtility.LoadPrefabContents(DeckPrefab);
            try
            {
                var module = root.GetComponent<HelicopterDeckModule>();
                if (module == null) { Debug.LogError("[Player Aviation] MOD_HeliDeck에 HelicopterDeckModule이 없습니다."); return; }
                for (int c = root.transform.childCount - 1; c >= 0; c--) Object.DestroyImmediate(root.transform.GetChild(c).gameObject);

                var levels = new GameObject[3];
                Transform firstSpot = null, firstParked = null, firstRotor = null;
                for (int i = 0; i < 3; i++)
                {
                    var level = new GameObject($"Visual_Level{i + 1}").transform;
                    level.SetParent(root.transform, false);
                    var deck = Mount(decks[i], level, "DeckPivot", scale, "LandingSpot", "BlockBase", null, null);

                    // 착함점: 데크 모델의 LandingSpot 위치만 읽어 단계 외형 바로 아래에 깨끗한 점을 만든다(축 보정을 두 번 받지 않게)
                    var marker = Deep(deck, "LandingSpot");
                    var spot = new GameObject("HeliSpot").transform;
                    spot.SetParent(level, false);
                    spot.localPosition = marker != null ? level.InverseTransformPoint(marker.position) : new Vector3(0f, 0.22f * scale, 0f);
                    spot.localRotation = Quaternion.identity;

                    var parked = new GameObject("ParkedHelicopter").transform;
                    parked.SetParent(spot, false);
                    var heli = Mount(parkedModels[i], parked, "ModelPivot", scale, "Rotor", null, "ChinEOHousing", "Rotor");

                    SetLayer(level, root.layer);
                    level.gameObject.SetActive(i == 0);
                    levels[i] = level.gameObject;
                    if (i == 0) { firstSpot = spot; firstParked = parked; firstRotor = Deep(heli, "Rotor"); }
                }

                var visuals = root.GetComponent<ModuleUpgradeVisuals>() ?? root.AddComponent<ModuleUpgradeVisuals>();
                Configure(visuals, so => SetArray(so, "levels", levels[0], levels[1], levels[2]));
                Configure(module, so =>
                {
                    Set(so, "helicopterPrefab", helis[0]);
                    SetArray(so, "helicopterPrefabsByLevel", helis[0], helis[1], helis[2]);
                    Set(so, "landingSpot", firstSpot);
                    Set(so, "parkedHelicopter", firstParked.gameObject);
                    Set(so, "parkedRotor", firstRotor);
                });
                PrefabUtility.SaveAsPrefabAsset(root, DeckPrefab);
                Debug.Log("[Player Aviation] MOD_HeliDeck: Visual_Level1~3 ← 데크·헬기 T1~T3");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>
        /// 헬기데크 강화 수치(2026-10-11). 기본: 출격 간격 12초 · 헬기 속력 18 · 무장 피해 40 · 수색 반경 60m · 내구 50.
        ///   강화 I(T2, 경어뢰 2발 개량 헬기): 간격 −20% · 피해 +30% · 수색 +15% · 속력 +10% · 내구 +25%
        ///   강화 II(T3, 5엽 로터·디핑 소나): 간격 −35% · 피해 +60% · 수색 +30% · 속력 +20% · 내구 +50%
        /// 다른 무장 강화(피해 +25~50%, 간격 −15~25%)와 비슷한 폭으로, 대잠 전용 블록이라 수색 반경을 함께 늘린다.
        /// </summary>
        private static void EnsureHeliDeckProfile()
        {
            var def = Resources.Load<ModuleDefinition>("Modules/mod_helideck");
            if (def == null)
                foreach (var guid in AssetDatabase.FindAssets("t:ModuleDefinition mod_helideck"))
                {
                    var d = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (d != null && d.Id == "mod_helideck") { def = d; break; }
                }
            if (def == null) { Debug.LogError("[Player Aviation] mod_helideck 정의를 찾지 못했습니다."); return; }

            var profile = AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(ProfilePath);
            if (profile == null) profile = CreateSO<ModuleUpgradeProfile>(ProfilePath);
            var stages = new List<ModuleUpgradeStage>
            {
                new("개량 대잠 헬기", "경어뢰 2발을 다는 개량 헬기. 출격 간격 −20% · 무장 피해 +30% · 수색 반경 +15% · 속력 +10% · 내구 +25%",
                    new ModuleStatModifier { cooldownMultiplier = 0.8f, damageMultiplier = 1.3f, rangeMultiplier = 1.15f, speedMultiplier = 1.1f, maxHpMultiplier = 1.25f }),
                new("디핑 소나 헬기", "5엽 로터·디핑 소나 헬기. 출격 간격 −35% · 무장 피해 +60% · 수색 반경 +30% · 속력 +20% · 내구 +50%",
                    new ModuleStatModifier { cooldownMultiplier = 0.65f, damageMultiplier = 1.6f, rangeMultiplier = 1.3f, speedMultiplier = 1.2f, maxHpMultiplier = 1.5f }),
            };
            profile.EditorSetup(def, stages);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ 노봉

        /// <summary>노봉 프리팹 외형을 v22 모델로(블록 생성기·무기 팩 적용기가 다시 돌아도 이 함수를 부른다).</summary>
        public static bool ApplyNobongV22()
        {
            var models = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                models[i] = AssetDatabase.LoadAssetAtPath<GameObject>($"{NobongDir}/{NobongFiles[i]}.fbx");
                if (models[i] == null) return false;
            }
            float scale = MeasureScale();
            var root = PrefabUtility.LoadPrefabContents(NobongPrefab);
            try
            {
                var module = root.GetComponent<NobongModule>();
                if (module == null) { Debug.LogError("[Player Aviation] MOD_Nobong에 NobongModule이 없습니다."); return false; }
                for (int c = root.transform.childCount - 1; c >= 0; c--) Object.DestroyImmediate(root.transform.GetChild(c).gameObject);
                var levels = new GameObject[3];
                Transform baseModel = null;
                for (int i = 0; i < 3; i++)
                {
                    var level = new GameObject($"Visual_Level{i + 1}").transform;
                    level.SetParent(root.transform, false);
                    var model = Mount(models[i], level, "ModelPivot", scale, "ElevationPivot", "DeckAdapter", "Muzzle", "TurretPivot");
                    SetLayer(level, root.layer);
                    level.gameObject.SetActive(i == 0);
                    levels[i] = level.gameObject;
                    if (i == 0) baseModel = model;
                }
                var visuals = root.GetComponent<ModuleUpgradeVisuals>() ?? root.AddComponent<ModuleUpgradeVisuals>();
                Configure(visuals, so => SetArray(so, "levels", levels[0], levels[1], levels[2]));
                Configure(module, so =>
                {
                    Set(so, "weapon.turret", Deep(baseModel, "TurretPivot"));
                    Set(so, "weapon.elevationPivot", Deep(baseModel, "ElevationPivot"));
                    Set(so, "weapon.muzzle", Deep(baseModel, "Muzzle"));
                });
                PrefabUtility.SaveAsPrefabAsset(root, NobongPrefab);
                Debug.Log("[Player Aviation] MOD_Nobong: Visual_Level1~3 ← Nobong v22");
                return true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ------------------------------------------------------------ 아이콘

        private static void BakeIcons(params string[] ids)
        {
            foreach (var id in ids)
            {
                ModuleDefinition def = null;
                foreach (var guid in AssetDatabase.FindAssets($"t:ModuleDefinition {id}"))
                {
                    var d = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (d != null && d.Id == id) { def = d; break; }
                }
                if (def == null || def.Prefab == null) continue;
                var icon = NavalIconBaker.Bake(def.Prefab, def.name);
                if (icon != null) Configure(def, so => Set(so, "icon", icon));

                ModuleUpgradeProfile profile = null;
                foreach (var guid in AssetDatabase.FindAssets("t:ModuleUpgradeProfile", new[] { Root + "/Resources/Upgrades" }))
                {
                    var p = AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                    if (p != null && p.Module == def) { profile = p; break; }
                }
                if (profile == null) continue;
                var pso = new SerializedObject(profile);
                var stages = pso.FindProperty("stages");
                for (int level = 1; level <= profile.MaxLevel && level <= stages.arraySize; level++)
                {
                    var stageIcon = NavalIconBaker.Bake(def.Prefab, $"{def.name}_U{level}", level + 1);
                    if (stageIcon != null) stages.GetArrayElementAtIndex(level - 1).FindPropertyRelative("icon").objectReferenceValue = stageIcon;
                }
                pso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ 도우미

        internal static Transform Deep(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = Deep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform child in t) SetLayer(child, layer);
        }
    }
}
