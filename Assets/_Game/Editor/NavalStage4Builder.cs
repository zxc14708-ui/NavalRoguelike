using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Enemies;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 4스테이지 "내해의 괴물"(2026-11-16, 1챕터 최종): Codex Stage4Ekranoplan_v1 모델로 적 프리팹 · 적 데이터 · 웨이브를 만들고
    /// 스테이지 3 다음에 잇는다(RoundSet_Stage3.nextStage — 씬은 그대로). 다른 적 · 스테이지는 건드리지 않는다.
    ///   - 모델: Art/Models/Stage4/*.fbx(적 · 미사일), Resources/Environment/Stage4/*.fbx(플랫폼 · 바지선 · 표지 — 실행 중에 불러 쓴다)
    ///   - 프리팹: ENE_LightWig · ENE_AssaultWig · ENE_Hovercraft · ENE_Hydrofoil · ENE_MissileCorvette · ENE_PlatformBattery · ENE_Boss4_Lun, MIS_Moskit
    ///   - 데이터: Data/Enemies/ene_light_wig · ene_assault_wig · ene_hovercraft · ene_hydrofoil · ene_missile_corvette · ene_boss4,
    ///     Resources/Enemies/ene_platform_battery(플랫폼 포대 — CoastalBatteryField가 경로로 읽는다), Data/Waves/RoundSet_Stage4
    /// 배치 실행: -executeMethod Game.EditorTools.NavalStage4Builder.Run
    /// </summary>
    public static class NavalStage4Builder
    {
        private const string ModelDir = Root + "/Art/Models/Stage4";
        private const string EnvDir = Root + "/Resources/Environment/Stage4";
        private const string EneDir = Root + "/Data/Enemies";
        private const string WavDir = Root + "/Data/Waves";
        private const string PrefabDir = Root + "/Prefabs/Enemies";
        private const string ProjDir = Root + "/Prefabs/Projectiles";
        public const float BossHp = 3600f;

        private static readonly string[] EnemyModels =
            { "ENE_Boss4_Lun", "ENE_LightWig", "ENE_AssaultWig", "ENE_Hovercraft", "ENE_Hydrofoil", "ENE_MissileCorvette", "ENE_PlatformBattery", "MIS_Moskit" };
        private static readonly string[] EnvModels = { "ENV_OilPlatform_L", "ENV_OilPlatform_S", "ENV_RearmBarge", "ENV_Sandbar_Marker" };
        private static readonly string[] EnemyPalette =
        {
            "Blue black glazing", "Emergency Red", "Enemy Dark", "Enemy hull grey", "Enemy superstructure grey",
            "Enemy weapon dark", "Gunmetal", "Medical White", "Naval cool grey", "Radar Glass", "Stealth deck",
            "Warning red", "Warning Yellow",
        };

        [MenuItem("Naval/Art/Refresh Stage 4 Enemy Materials")]
        public static void RefreshEnemyArt()
        {
            const string mats = Root + "/Art/Models/Materials";
            var palette = new List<(string name, Material mat)>();
            foreach (string name in EnemyPalette)
                palette.Add((name, AssetDatabase.LoadAssetAtPath<Material>($"{mats}/{name}.mat")));
            palette.Add(("ENV_Concrete", AssetDatabase.LoadAssetAtPath<Material>($"{mats}/Environment/ENV_Concrete.mat")));
            palette.Add(("MAT_IslandWhite", AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Resources/Islands/MAT_IslandWhite.mat")));
            foreach (string model in EnemyModels)
            {
                if (model == "MIS_Moskit") continue;
                string path = $"{ModelDir}/{model}.fbx";
                // Only store remaps for materials the current FBX actually uses.
                var art = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (art == null) throw new System.InvalidOperationException($"Missing Stage 4 model: {path}");
                var names = new HashSet<string>();
                foreach (var renderer in art.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var mat in renderer.sharedMaterials)
                        if (mat != null) names.Add(mat.name);
                var used = palette.FindAll(entry => names.Contains(entry.name));
                if (used.Count == 0 || used.Count != names.Count || used.Exists(entry => entry.mat == null))
                    throw new System.InvalidOperationException($"Incomplete Stage 4 art palette: {model}");
                ImportModel(path, used.ToArray());
            }
        }

        [MenuItem("Naval/Add Stage 4 (Inland Sea · Lun)", priority = 5)]
        public static void Run()
        {
            EnsureFolder(EneDir);
            EnsureFolder(WavDir);
            // 재질: 모델의 재질 이름이 프로젝트 재질(Art/Models/Materials · Environment · Resources/Islands)과 같다 — 이름으로 찾아 붙인다
            RefreshEnemyArt();
            ImportModel($"{ModelDir}/MIS_Moskit.fbx", System.Array.Empty<(string, Material)>());
            foreach (var m in EnvModels) ImportModel($"{EnvDir}/{m}.fbx", System.Array.Empty<(string, Material)>());

            EnemyDefinition Def(string path) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjDir}/PRJ_EnemyGun.prefab");
            var aaTracer = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjDir}/PRJ_EnemyAA.prefab");
            var asm = AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjDir}/MIS_EnemyAsm.prefab");
            var artilleryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/ENE_ArtilleryBoat.prefab");
            GameObject shell = null, flash = null;
            if (artilleryPrefab != null && artilleryPrefab.TryGetComponent<ArtilleryBoat>(out var ab))
            {
                var so = new SerializedObject(ab);
                shell = so.FindProperty("shellPrefab").objectReferenceValue as GameObject;
                flash = so.FindProperty("muzzleFlash").objectReferenceValue as GameObject;
            }
            var boat = Def($"{EneDir}/ene_suicide_boat.asset");
            var moskit = BuildMoskit(asm);

            // ---- 프리팹
            var lightWig = Build<LightWig>("ENE_LightWig", new Vector3(5f, 2f, 7.5f), new Vector3(0f, 0.8f, 0f), true, (go, c) =>
            {
                Configure(c, so =>
                {
                    Set(so, "gunMuzzle", Deep(go, "GunMuzzle"));
                    Set(so, "projectilePrefab", gun);
                    Set(so, "projectileSpeed", 60f);
                    Set(so, "burstRounds", 6);
                    Set(so, "cruiseAltitude", 2.6f);
                    Set(so, "attackAltitude", 1.9f);
                    Set(so, "extendDistance", 42f);
                    Set(so, "breakClimb", 0.6f);
                });
                Spin(go, Axis(PartAxis.Forward), 900f, "Propeller");
            });
            var assaultWig = Build<AssaultWig>("ENE_AssaultWig", new Vector3(4.2f, 3.6f, 19f), new Vector3(0f, 1.6f, 0f), true, (go, c) =>
                Configure(c, so =>
                {
                    Set(so, "boatDefinition", boat);
                    Set(so, "noseDoor", Deep(go, "NoseDoorPivot"));
                    Set(so, "boatExit", Deep(go, "BoatExit"));
                    Set(so, "propeller", Deep(go, "TailPropeller"));
                    Set(so, "cruiseAltitude", 2.8f);
                }));
            var hovercraft = Build<Hovercraft>("ENE_Hovercraft", new Vector3(8.5f, 3.4f, 18f), new Vector3(0f, 1.4f, 0f), true, (go, c) =>
            {
                Configure(c, so =>
                {
                    Set(so, "weapon.turret", Deep(go, "RocketLauncher_Port_Pivot"));
                    Set(so, "weapon.elevationPivot", Deep(go, "RocketLauncher_Port_Elevation"));
                    Set(so, "weapon.muzzle", Deep(go, "RocketLauncher_Port_Muzzle"));
                    Set(so, "shellPrefab", shell);
                    Set(so, "muzzleFlash", flash);
                    Set(so, "shellsPerSalvo", 6);
                    Set(so, "salvoSpread", 7f);
                    Set(so, "shellSpacing", 0.15f);
                    Set(so, "shellBlastRadius", 2.4f);
                    Set(so, "shellFlightTime", 2.8f);
                    Set(so, "minFireRange", 18f);
                    Set(so, "extraRange", 12f);
                });
                Spin(go, Axis(PartAxis.Forward), 500f, "FanBlade_01", "FanBlade_02", "FanBlade_03");
            });
            var hydrofoil = Build<Hydrofoil>("ENE_Hydrofoil", new Vector3(4.4f, 2.6f, 13.5f), new Vector3(0f, 1.3f, 0f), true, (go, c) =>
            {
                Configure(c, so =>
                {
                    Set(so, "launchPoint", Deep(go, "MissileLaunchPoint_Port"));
                    Set(so, "hullPivot", Deep(go, "HullPivot"));
                });
                Spin(go, Axis(PartAxis.Up), 40f, "RadarPivot");
            });
            var corvette = Build<MissileCorvette>("ENE_MissileCorvette", new Vector3(3.8f, 3f, 15f), new Vector3(0f, 1.5f, 0f), true, (go, c) =>
            {
                Configure(c, so =>
                {
                    SetArray(so, "launchPoints", Deep(go, "MissileLaunchPoint_Port_01"), Deep(go, "MissileLaunchPoint_Starboard_01"),
                             Deep(go, "MissileLaunchPoint_Port_02"), Deep(go, "MissileLaunchPoint_Starboard_02"));
                    Set(so, "weapon.turret", Deep(go, "BowGun_TurretPivot"));
                    Set(so, "weapon.elevationPivot", Deep(go, "BowGun_ElevationPivot"));
                    Set(so, "weapon.muzzle", Deep(go, "BowGun_Muzzle"));
                    Set(so, "projectilePrefab", gun);
                    Set(so, "radar", Deep(go, "RadarPivot"));
                });
                var ciws = go.AddComponent<MissileInterceptor>();
                Configure(ciws, so =>
                {
                    Set(so, "tracerPrefab", aaTracer);
                    Set(so, "mount", Deep(go, "Ciws_Port_Muzzle"));
                    Set(so, "turret", Deep(go, "Ciws_Port_Pivot"));
                });
            });
            // 플랫폼 포대: 갑판 위 고정 — 수면선 맞춤(Waterline)을 받지 않게 ModelPivot 대신 Visual
            var platformBattery = Build<PlatformBattery>("ENE_PlatformBattery", new Vector3(3f, 3.2f, 3f), new Vector3(0f, 1.6f, 0f), false, (go, c) =>
            {
                Configure(c, so =>
                {
                    Set(so, "weapon.turret", Deep(go, "TurretPivot"));
                    Set(so, "weapon.muzzle", Deep(go, "Muzzle"));
                    Set(so, "elevationPivot", Deep(go, "ElevationPivot"));
                    SetArray(so, "launchPoints", Deep(go, "LaunchPoint_Port"), Deep(go, "LaunchPoint_Starboard"));
                    Set(so, "muzzleFlash", flash);
                    Set(so, "raiseElevation", 18f);
                    Set(so, "raiseSeconds", 1.2f);
                });
                var ciws = go.AddComponent<MissileInterceptor>();
                Configure(ciws, so =>
                {
                    Set(so, "tracerPrefab", aaTracer);
                    Set(so, "mount", Deep(go, "Muzzle"));
                    Set(so, "interceptRange", 26f);
                });
                Spin(go, Axis(PartAxis.Up), 30f, "RadarPivot");
            });
            var lun = Build<LunBoss>("ENE_Boss4_Lun", new Vector3(6f, 5f, 30f), new Vector3(0f, 2.5f, 0f), true, (go, c) =>
                Configure(c, so =>
                {
                    Set(so, "gunProjectilePrefab", gun);
                    Set(so, "muzzleFlash", flash);
                    Set(so, "cruiseAltitude", 3f);
                    // 폭탄 · 로켓(2026-11-21 Codex 점검): 다시 저장해도 통과 공격 패턴이 빠지지 않게 전용 탄 프리팹을 채운다(없으면 비워 둠 — 룬급이 미사일 통과로 대신)
                    Set(so, "bombPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjDir}/PRJ_LunBomb.prefab"));
                    Set(so, "rocketPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{ProjDir}/PRJ_LunRocket.prefab"));
                }));

            // ---- 적 데이터
            var defLight = Enemy($"{EneDir}/ene_light_wig.asset", "ene_light_wig", "경위그선", TargetKind.Aircraft, lightWig, TargetCategory.Air, 2, EnemyRank.Normal,
                hp: 22f, speed: 22f, turn: 130f, range: 24f, dmg: 9f, cd: 1.5f, xp: 3, maxAlive: 8);
            var defAssault = Enemy($"{EneDir}/ene_assault_wig.asset", "ene_assault_wig", "강습 위그선", TargetKind.Aircraft, assaultWig, TargetCategory.Air, 4, EnemyRank.Normal,
                hp: 120f, speed: 20f, turn: 60f, range: 0f, dmg: 0f, cd: 1f, xp: 10, maxAlive: 2);
            var defHover = Enemy($"{EneDir}/ene_hovercraft.asset", "ene_hovercraft", "공기부양 상륙정", TargetKind.Surface, hovercraft, TargetCategory.MediumSurface, 4, EnemyRank.Normal,
                hp: 170f, speed: 6f, turn: 45f, range: 40f, dmg: 7f, cd: 9f, xp: 9, maxAlive: 2);
            var defHydro = Enemy($"{EneDir}/ene_hydrofoil.asset", "ene_hydrofoil", "수중익 미사일정", TargetKind.Surface, hydrofoil, TargetCategory.MediumSurface, 3, EnemyRank.Normal,
                hp: 60f, speed: 15f, turn: 80f, range: 40f, dmg: 22f, cd: 10f, xp: 8, maxAlive: 3, missile: moskit, salvo: 2, spacing: 0.3f);
            var defCorvette = Enemy($"{EneDir}/ene_missile_corvette.asset", "ene_missile_corvette", "미사일 초계함", TargetKind.Surface, corvette, TargetCategory.MediumSurface, 5, EnemyRank.Elite,
                hp: 170f, speed: 8f, turn: 60f, range: 34f, dmg: 26f, cd: 12f, xp: 14, maxAlive: 1, missile: moskit, salvo: 2, spacing: 0.4f);
            EnsureFolder(Root + "/Resources/Enemies");
            Enemy($"{Root}/Resources/Enemies/ene_platform_battery.asset", "ene_platform_battery", "플랫폼 포대", TargetKind.Surface, platformBattery, TargetCategory.LargeSurface, 3, EnemyRank.Normal,
                hp: 90f, speed: 0f, turn: 0f, range: 48f, dmg: 16f, cd: 11f, xp: 6, maxAlive: 0, missile: asm, salvo: 2, spacing: 0.45f);
            var defBoss = Enemy($"{EneDir}/ene_boss4.asset", "ene_boss4", "룬급 위그선", TargetKind.Aircraft, lun, TargetCategory.Boss, 10, EnemyRank.Boss,
                hp: BossHp, speed: 34f, turn: 32f, range: 0f, dmg: 30f, cd: 1f, xp: 120, maxAlive: 1, missile: moskit, salvo: 2, spacing: 0.3f);

            // ---- 웨이브
            var fast = Def($"{EneDir}/ene_fastboat.asset");
            var usv = Def($"{EneDir}/ene_usv.asset");
            var msl = Def($"{EneDir}/ene_missileboat.asset");
            var drone = Def($"{EneDir}/ene_drone.asset");
            var fighter = Def($"{EneDir}/ene_fighter.asset");
            var ew = Def($"{EneDir}/ene_ew_corvette.asset");
            var attackSub = Def($"{EneDir}/ene_attack_submarine.asset");
            var stage4 = CreateSO<RoundSet>($"{WavDir}/RoundSet_Stage4.asset");
            Configure(stage4, so =>
            {
                var list = so.FindProperty("rounds");
                list.arraySize = 7;
                Round(list.GetArrayElementAtIndex(0), "내해 진입", 60f, 0.95f, 24, null, (fast, 2f), (usv, 1.5f), (defLight, 1.2f));
                Round(list.GetArrayElementAtIndex(1), "수면 습격", 60f, 1.10f, 28, null, (defLight, 3.5f), (drone, 1.5f), (usv, 1f));
                Round(list.GetArrayElementAtIndex(2), "유전 지대", 60f, 1.10f, 28, null, (defHydro, 1f), (fast, 1.5f), (usv, 1.5f), (defLight, 1f));
                Round(list.GetArrayElementAtIndex(3), "상륙 선단", 60f, 1.15f, 30, null, (defHover, 0.5f), (defAssault, 0.4f), (boat, 1.2f), (fast, 1.5f), (defLight, 1f));
                Round(list.GetArrayElementAtIndex(4), "미사일 포화", 60f, 1.25f, 32, null, (defHydro, 1f), (defCorvette, 0.35f), (ew, 0.4f), (msl, 0.6f), (defLight, 1.2f));
                Round(list.GetArrayElementAtIndex(5), "총공세", 60f, 1.45f, 38, null, (fast, 1.5f), (usv, 1.5f), (defLight, 2f), (defHydro, 0.8f), (defHover, 0.4f),
                      (defAssault, 0.3f), (defCorvette, 0.3f), (fighter, 1f), (drone, 1.5f), (ew, 0.3f), (attackSub, 0.15f));
                Round(list.GetArrayElementAtIndex(6), "룬급 위그선", 90f, 0.45f, 10, defBoss, (fast, 1f), (drone, 0.5f), (boat, 0.4f), (defLight, 0.5f));
                var bossRound = list.GetArrayElementAtIndex(6);
                bossRound.FindPropertyRelative("Escort").objectReferenceValue = defLight;
                bossRound.FindPropertyRelative("EscortCount").intValue = 4;
            });
            var stage3 = AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage3.asset");
            if (stage3 != null) Configure(stage3, so => so.FindProperty("nextStage").objectReferenceValue = stage4);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Stage 4] 프리팹 {Count(lightWig, assaultWig, hovercraft, hydrofoil, corvette, platformBattery, lun)}/7 · 미사일 {(moskit != null ? "예" : "아니오")} · RoundSet_Stage4 · 스테이지 3 → 4 연결 {(stage3 != null ? "예" : "아니오")}");
        }

        private static int Count(params GameObject[] g) { int n = 0; foreach (var x in g) if (x != null) n++; return n; }

        private enum PartAxis { Forward, Up }
        private static Game.View.PartSpin.Axis Axis(PartAxis a) => a == PartAxis.Up ? Game.View.PartSpin.Axis.Up : Game.View.PartSpin.Axis.Forward;

        private static void Spin(GameObject go, Game.View.PartSpin.Axis axis, float rpm, params string[] names)
        {
            var list = new List<Transform>();
            foreach (var n in names) { var t = Deep(go, n); if (t != null) list.Add(t); }
            if (list.Count == 0) return;
            var spin = go.AddComponent<Game.View.PartSpin>();
            Configure(spin, so =>
            {
                SetArray(so, "parts", list.ToArray());
                Set(so, "axis", (int)axis);
                Set(so, "rpm", rpm);
            });
        }

        private static Transform Deep(GameObject go, string name)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// 적 프리팹: 루트(판정 상자 · 스크립트) → 피벗(Y 180° — Codex 모델 앞을 +Z로) → 모델. waterline이면 피벗 이름을 ModelPivot으로 해
        /// 수상함 수면선 맞춤을 받는다(고정 포대는 Visual).
        /// </summary>
        private static GameObject Build<T>(string name, Vector3 colliderSize, Vector3 colliderCenter, bool waterline, System.Action<GameObject, T> extra)
            where T : EnemyController
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (model == null) { Debug.LogError($"[Stage 4] 모델 없음: {name}"); return null; }
            var root = new GameObject(name);
            try
            {
                var pivot = new GameObject(waterline ? "ModelPivot" : "Visual").transform;
                pivot.SetParent(root.transform, false);
                pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.name = "Model";
                inst.transform.SetParent(pivot, false);
                var col = root.AddComponent<BoxCollider>();
                col.size = colliderSize;
                col.center = colliderCenter;
                col.isTrigger = true;
                var comp = root.AddComponent<T>();
                extra?.Invoke(root, comp);
                SetLayerRecursive(root, LayerEnemy);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>모스킷 미사일: 적 대함미사일 프리팹(동작 · 연기 · 불꽃)을 그대로 쓰고 몸체 외형만 Codex 모델로, 더 빠르게(요격 시간이 짧다).</summary>
        private static GameObject BuildMoskit(GameObject asm)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/MIS_Moskit.fbx");
            if (asm == null || model == null) return asm;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(asm);
            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                root.name = "MIS_Moskit";
                foreach (var n in new[] { "Body", "Nose", "Band", "Fins_H", "Fins_V" })
                {
                    var t = Deep(root, n);
                    if (t != null && t != root.transform && t.TryGetComponent<MeshRenderer>(out var r)) r.enabled = false;
                }
                var pivot = new GameObject("MoskitPivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                pivot.localScale = Vector3.one * 0.8f;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.SetParent(pivot, false);
                foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                SetLayerRecursive(inst, root.layer);
                if (root.TryGetComponent<Missile>(out var m))
                    Configure(m, so => { Set(so, "speed", 15f); Set(so, "damage", 22f); });
                return PrefabUtility.SaveAsPrefabAsset(root, $"{ProjDir}/MIS_Moskit.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static EnemyDefinition Enemy(string path, string id, string name, TargetKind kind, GameObject prefab, TargetCategory category, int value, EnemyRank rank,
            float hp, float speed, float turn, float range, float dmg, float cd, int xp, int maxAlive, GameObject missile = null, int salvo = 1, float spacing = 0.15f)
        {
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path) ?? CreateSO<EnemyDefinition>(path);
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "kind", (int)kind);
                Set(so, "prefab", prefab);
                Set(so, "category", (int)category);
                Set(so, "targetValue", value);
                Set(so, "rank", (int)rank);
                Set(so, "maxAlive", maxAlive);
                Set(so, "maxHp", hp);
                Set(so, "moveSpeed", speed);
                Set(so, "turnRateDegPerSec", turn);
                Set(so, "preferredRange", range);
                Set(so, "attackDamage", dmg);
                Set(so, "attackCooldown", cd);
                Set(so, "missilePrefab", missile);
                Set(so, "salvoSize", salvo);
                Set(so, "salvoSpacing", spacing);
                Set(so, "xpReward", xp);
            });
            return def;
        }

        private static void Round(SerializedProperty round, string title, float duration, float rate, int maxAlive, EnemyDefinition boss,
                                  params (EnemyDefinition enemy, float weight)[] entries)
        {
            round.FindPropertyRelative("Title").stringValue = title;
            round.FindPropertyRelative("Duration").floatValue = duration;
            round.FindPropertyRelative("SpawnRate").floatValue = rate;
            round.FindPropertyRelative("MaxAlive").intValue = maxAlive;
            round.FindPropertyRelative("Boss").objectReferenceValue = boss;
            round.FindPropertyRelative("Escort").objectReferenceValue = null;
            round.FindPropertyRelative("EscortCount").intValue = 0;
            var picks = new List<(EnemyDefinition, float)>();
            foreach (var e in entries) if (e.enemy != null) picks.Add((e.enemy, e.weight));
            var list = round.FindPropertyRelative("Entries");
            list.arraySize = picks.Count;
            for (int i = 0; i < picks.Count; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Enemy").objectReferenceValue = picks[i].Item1;
                e.FindPropertyRelative("Weight").floatValue = picks[i].Item2;
            }
        }
    }
}
