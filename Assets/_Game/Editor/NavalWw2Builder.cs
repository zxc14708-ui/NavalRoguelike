using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using Game.WW2;
using static Game.EditorTools.NavalEditorUtil;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// WWII 패치 B 콘텐츠(2026-10-17). 현대 프리팹·데이터·씬은 건드리지 않고 WWII 것만 만든다(다시 돌리면 같은 경로에 덮어쓴다).
    ///   - Prefabs/WW2: 양용포·4연장 대공포·현측 어뢰 발사관·WWII 함교(기만체 없음·연막), 뇌격기·급강하폭격기·임시 포격 순양함(회색 실루엣),
    ///     양용포탄·대공포탄·플레이어 직진 어뢰·급강하 폭탄. 기존 탄(76mm·기관포·적 어뢰·보스 포탄)을 복제해 고친다.
    ///   - Data/WW2: 블록 4·적 5·시작 배치·시작 함정(구축함)·시험 전투 웨이브. 모든 ID는 ww2_로 시작한다.
    ///   - Resources/Rulesets/ww2_v1: 규칙 집합 정의(현대 드래프트·사전은 이것을 읽지 않는다).
    /// 고속정·포격함·어뢰정은 현대 프리팹(ENE_FastBoat·ENE_ArtilleryBoat·ENE_TorpedoBoat)의 행동을 복제하고 Codex 모델이 있으면 겉모습만 바꾼다(NavalWw2Builder.Art).
    /// 규칙 집합에는 WWII 선체 모양(갑판판·현측 띠·뱃머리 재질)도 담는다 — ShipHullBuilder가 WWII 판에서 쓴다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalWw2Builder.Run
    /// </summary>
    public static partial class NavalWw2Builder
    {
        private const string PrefabDir = Root + "/Prefabs/WW2";
        private const string DataDir = Root + "/Data/WW2";
        private const string RulesetPath = Root + "/Resources/Rulesets/ww2_v1.asset";

        private static Material _hull, _deck, _gun, _dark, _air, _airDark, _torpedoWarn, _bombWarn, _enemyHull, _enemyDeck;

        [MenuItem("Naval/WW2/Build Patch B Content")]
        public static void Run()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(DataDir);
            EnsureFolder(Root + "/Resources/Rulesets");
            _hull = CreateMaterial("ww2_hull", new Color(0.46f, 0.5f, 0.53f), 0.1f, 0.3f);
            _deck = CreateMaterial("ww2_deck", new Color(0.36f, 0.33f, 0.28f), 0f, 0.15f);
            _gun = CreateMaterial("ww2_gun", new Color(0.33f, 0.36f, 0.38f), 0.3f, 0.35f);
            _dark = CreateMaterial("ww2_dark", new Color(0.13f, 0.14f, 0.15f), 0.2f, 0.3f);
            _air = CreateMaterial("ww2_air_olive", new Color(0.34f, 0.38f, 0.26f), 0.1f, 0.3f);
            _airDark = CreateMaterial("ww2_air_dark", new Color(0.18f, 0.2f, 0.16f), 0.1f, 0.25f);
            _torpedoWarn = CreateMaterial("ww2_torpedo_warn", new Color(0.95f, 0.55f, 0.15f), 0.2f, 0.4f);
            _bombWarn = CreateMaterial("ww2_bomb_warn", new Color(0.85f, 0.18f, 0.12f), 0.2f, 0.4f);
            _enemyHull = CreateMaterial("ww2_enemy_hull", new Color(0.4f, 0.41f, 0.44f), 0.1f, 0.3f);
            _enemyDeck = CreateMaterial("ww2_enemy_deck", new Color(0.3f, 0.28f, 0.25f), 0f, 0.15f);
            ImportArt();   // Codex WWII 모델(있으면) — NavalWw2Builder.Art

            // --- 탄
            var gunShell = Clone("Prefabs/Projectiles/PRJ_Gun76.prefab", "PRJ_Ww2DualGun", root => SkinProjectile(root, "PRJ_Ww2DualGun"));
            var aaRound = Clone("Prefabs/Projectiles/PRJ_Autocannon.prefab", "PRJ_Ww2QuadAa", root => SkinProjectile(root, "PRJ_Ww2QuadAa"));
            var bossShellPrefab = Load<GameObject>("Prefabs/Projectiles/PRJ_BossShell.prefab");
            var enemyTorpedo = Load<GameObject>("Prefabs/Projectiles/TOR_Enemy.prefab");
            var enemyGun = Load<GameObject>("Prefabs/Projectiles/PRJ_EnemyGun.prefab");
            var playerTorpedo = BuildPlayerTorpedo(enemyTorpedo);
            var bomb = Clone("Prefabs/Projectiles/PRJ_BossShell.prefab", "BOMB_Ww2", root =>
            {
                var shell = root.GetComponent<ArtilleryShell>();
                Configure(shell, so => { Set(so, "warningMarker", null); Set(so, "apexHeight", 0.4f); });
                SkinProjectile(root, "BOMB_Ww2");
            });
            // 뇌격기 어뢰: 현대 적 어뢰(TOR_Enemy) 동작 그대로, 겉모습만 항공어뢰
            var airTorpedo = Clone("Prefabs/Projectiles/TOR_Enemy.prefab", "TOR_Ww2Air", root => SkinProjectile(root, "TOR_Ww2Air")) ?? enemyTorpedo;
            GameObject warningMarker = null, flash = null;
            if (bossShellPrefab != null)
            {
                var so = new SerializedObject(bossShellPrefab.GetComponent<ArtilleryShell>());
                warningMarker = so.FindProperty("warningMarker").objectReferenceValue as GameObject;
            }
            var gun76 = Load<GameObject>("Prefabs/Modules/MOD_Gun76.prefab");
            if (gun76 != null && gun76.TryGetComponent<NavalGunModule>(out var g76))
                flash = new SerializedObject(g76).FindProperty("muzzleFlash").objectReferenceValue as GameObject;

            // --- 블록
            var dualGun = BuildDualGun(gunShell, flash);
            var quadAa = BuildQuadAa(aaRound);
            var torpedoMount = BuildTorpedoMount(playerTorpedo);
            var bridge = BuildBridge();
            // 선체 모양(v31): 구축함은 도장 강철판(목재판은 소형정·화물용 대안이라 쓰지 않음)
            var deckPlate = BuildHullPiece("HULL_Ww2DeckPlate_Steel", false);
            var sideSkirt = BuildHullPiece("HULL_Ww2SideSkirt", true);
            var bowDeck = deckPlate != null ? AssetDatabase.LoadAssetAtPath<Material>($"{ArtMaterialDir}/WW2 Deck.mat") : null;
            var bowSide = sideSkirt != null ? AssetDatabase.LoadAssetAtPath<Material>($"{ArtMaterialDir}/WW2 Hull.mat") : null;

            // --- 적
            var torpedoBomber = BuildTorpedoBomber(airTorpedo);
            var diveBomber = BuildDiveBomber(bomb, warningMarker);
            var cruiser = BuildCruiser(bossShellPrefab, enemyGun, flash);

            // --- 데이터
            var defBridge = ModuleDef("ww2_mod_bridge_destroyer", "구축함 함교", "WWII 구축함의 함교·기관실·연돌. 항해 레이더·손상 통제·전속·연막을 맡는다. 기만체는 없다(패치 A 9.1).",
                ModuleType.Bridge, 3, 1, false, PlacementZone.Anywhere, ModuleHeight.High, 110f, Rarity.Common, 0f, 3f, bridge, s =>
                {
                    s.DetectionRange = 46f; s.ExtraTrackedTargets = 2; s.HullRepairPerSecond = 0.5f; s.ModuleRepairPerSecond = 0.6f;
                    s.DamageReduction = 0.05f; s.ReloadTime = 14f;
                }, Eff(0, 0, 0, 0, 0, 0, 0, 0));
            var defGun = ModuleDef("ww2_mod_dual_purpose_gun", "양용포",
                "쌍열 양용포. 수상함을 주로 쏘고, 공격 방향을 정한 항공기가 가까우면 그쪽으로 돌린다. 한 번에 한 표적. 포탑이 느리게 돌고 탄이 날아가는 시간이 있다.",
                ModuleType.Ww2DualPurposeGun, 1, 1, true, PlacementZone.Anywhere, ModuleHeight.Mid, 50f, Rarity.Uncommon, 1.1f, 2f, dualGun, s =>
                {
                    s.Range = 46f; s.MinRange = 6f; s.Damage = 22f; s.ReloadTime = 2f; s.ProjectileSpeed = 42f; s.FireArcDegrees = 360f; s.TurretTurnRate = 45f;
                    s.AmmoFamily = AmmoFamily.Gun; s.MagazineCapacity = 24; s.AmmoPerShot = 1; s.AmmoReloadAmount = 3; s.AmmoReloadTime = 6f;
                }, Eff(0.9f, 1f, 1f, 1f, 0f, 0.4f, 0.45f, 0f));
            var defAa = ModuleDef("ww2_mod_quad_aa", "40mm급 4연장 대공포",
                "네 포신이 번갈아 쏘는 대공포. 항공기를 먼저(공격 방향을 정한 기체부터), 없으면 가까운 소형 수상함. 근접신관이 없어 직격해야 맞고, 큰 배에는 거의 듣지 않는다.",
                ModuleType.Ww2QuadAa, 1, 1, true, PlacementZone.Anywhere, ModuleHeight.Mid, 35f, Rarity.Common, 1f, 1f, quadAa, s =>
                {
                    s.Range = 32f; s.MinRange = 0f; s.Damage = 1.8f; s.ReloadTime = 0.09f; s.ProjectileSpeed = 70f; s.FireArcDegrees = 360f; s.TurretTurnRate = 140f;
                    s.AmmoFamily = AmmoFamily.Gun; s.MagazineCapacity = 160; s.AmmoPerShot = 1; s.AmmoReloadAmount = 16; s.AmmoReloadTime = 2f;
                }, Eff(0.6f, 0.25f, 0.1f, 0.05f, 0f, 1f, 1f, 0f));
            var defTorpedo = ModuleDef("ww2_mod_broadside_torpedo", "현측 어뢰 발사관",
                "4연장 발사관. 직접 쏜다 — 키를 누르고 있으면 현측 사격각(정횡 ±30°)과 예상 경로, 떼면 일제 발사. 어뢰는 곧게만 가고(유도 없음) 가까우면 불발. 재장전이 길다. 좌현이나 우현이 트인 자리에만 놓인다.",
                ModuleType.Ww2BroadsideTorpedo, 1, 1, true, PlacementZone.SideOnly, ModuleHeight.Low, 30f, Rarity.Uncommon, 0.9f, 2f, torpedoMount, s =>
                {
                    s.Range = 75f; s.MinRange = 9f; s.Damage = 55f; s.ProjectileSpeed = 15f; s.FireArcDegrees = 60f;
                    s.AmmoFamily = AmmoFamily.Asw; s.MagazineCapacity = 4; s.AmmoPerShot = 1; s.AmmoReloadAmount = 0; s.AmmoReloadTime = 22f;
                }, Eff(1f, 1f, 1f, 1f, 0f, 0f, 0f, 0f));

            var eFast = EnemyDef("ww2_enemy_fast_gunboat", "소형 포함", TargetKind.Surface, BuildGunboat(true) ?? Load<GameObject>("Prefabs/Enemies/ENE_FastBoat.prefab"),
                TargetCategory.SmallSurface, EnemyRank.Normal, 1, 0, hp: 14f, speed: 11f, turn: 190f, range: 7f, damage: 3.5f, cooldown: 1.7f, xp: 1);
            var eGunboat = EnemyDef("ww2_enemy_gunboat", "포격함", TargetKind.Surface, BuildGunboat(false) ?? Load<GameObject>("Prefabs/Enemies/ENE_ArtilleryBoat.prefab"),
                TargetCategory.MediumSurface, EnemyRank.Normal, 3, 2, hp: 42f, speed: 6f, turn: 70f, range: 38f, damage: 10f, cooldown: 9f, xp: 4);
            // 2026-10-17 플레이 피드백: 뇌격기 3기 편대 · 급강하 2기 편대(동시 최대 = 편대 둘) · 어뢰정 추가 · 보스 체력 +20%(900 → 1080)
            var eTorpBomber = EnemyDef("ww2_enemy_torpedo_bomber", "뇌격기", TargetKind.Aircraft, torpedoBomber,
                TargetCategory.Air, EnemyRank.Normal, 2, 6, hp: 12f, speed: 22f, turn: 90f, range: 0f, damage: 30f, cooldown: 0f, xp: 3, group: 3);
            var eDiveBomber = EnemyDef("ww2_enemy_dive_bomber", "급강하폭격기", TargetKind.Aircraft, diveBomber,
                TargetCategory.Air, EnemyRank.Normal, 2, 4, hp: 11f, speed: 24f, turn: 110f, range: 0f, damage: 22f, cooldown: 0f, xp: 3, group: 2);
            var eTorpBoat = EnemyDef("ww2_enemy_torpedo_boat", "어뢰정", TargetKind.Surface, BuildTorpedoBoat() ?? Load<GameObject>("Prefabs/Enemies/ENE_TorpedoBoat.prefab"),
                TargetCategory.SmallSurface, EnemyRank.Normal, 2, 3, hp: 26f, speed: 10f, turn: 110f, range: 40f, damage: 24f, cooldown: 13f, xp: 3);
            var eBoss = EnemyDef("ww2_boss_gunnery_cruiser", "적 순양함(임시)", TargetKind.Surface, cruiser,
                TargetCategory.Boss, EnemyRank.Boss, 10, 0, hp: 1080f, speed: 5.5f, turn: 30f, range: 36f, damage: 14f, cooldown: 0f, xp: 0);

            // --- 함종(패치 C1, 2026-10-18): 실제 이동·방호·격자·탑재 예산. 구축함은 B 시험 전투 값 그대로(기준), 순양함은 느리고 무겁고 단단하다.
            //     톤수: 함교 3 · 양용포 2 · 현측 어뢰 2 · 4연장 대공포 1
            var hullDestroyer = Hull("Hull_WW2_Destroyer", maxSpeed: 15.4333f, turn: 40f, accel: 7.7f, decel: 5.4f, response: 0.9f,
                hp: 100f, armor: 0f, halfLength: 5, halfBeam: 1, budget: 14f);
            var hullCruiser = Hull("Hull_WW2_Cruiser", maxSpeed: 13.4f, turn: 30f, accel: 5.8f, decel: 4.2f, response: 1.25f,
                hp: 170f, armor: 0.1f, halfLength: 7, halfBeam: 2, budget: 26f);

            var loadout = Loadout("Loadout_WW2_Destroyer",
                (defBridge, -1, 0, 0), (defGun, 2, 0, 0), (defTorpedo, -2, 0, 0), (defAa, -3, 0, 2), (defGun, -4, 0, 2));
            // 순양함: 함수 양용포 2 · 함미 양용포 2 · 함교 양옆 대공포 2 · 좌현 현측 어뢰 1 (15 / 26t)
            var loadoutCruiser = Loadout("Loadout_WW2_Cruiser",
                (defBridge, -1, 0, 0), (defGun, 2, 0, 0), (defGun, 3, 0, 0), (defGun, -2, 0, 2), (defGun, -3, 0, 2),
                (defAa, 0, 1, 0), (defAa, 0, -1, 0), (defTorpedo, -2, -1, 0));

            var concept = CreateSO<StartingShipConcept>($"{DataDir}/ShipConcept_WW2_Destroyer.asset");
            Configure(concept, so =>
            {
                Set(so, "id", "ww2_hull_destroyer");
                Set(so, "title", "구축함 (WWII 시험)");
                Set(so, "description", "WWII 기술의 구축함. 함수·함미 양용포, 함교 뒤 현측 어뢰 발사관(4연장), 함미 4연장 대공포. 빠르게 돌고 어뢰로 큰 배를 잡는다.");
                Set(so, "kind", (int)StartShipKind.Ww2Destroyer);
                Set(so, "color", new Color(1f, 0.78f, 0.42f));
                Set(so, "startLoadout", loadout);
                Set(so, "expandedLoadout", loadout);
                Set(so, "bridgeDefinition", defBridge);
                Set(so, "startSummary", "양용포 2 · 4연장 대공포 1 · 현측 어뢰 4연장 1 · 호위함 없음");
                Set(so, "initialEscortCount", 0);
                Set(so, "hullProfile", hullDestroyer);
            });

            var conceptCruiser = CreateSO<StartingShipConcept>($"{DataDir}/ShipConcept_WW2_Cruiser.asset");
            Configure(conceptCruiser, so =>
            {
                Set(so, "id", "ww2_hull_cruiser");
                Set(so, "title", "순양함 (WWII 시험)");
                Set(so, "description", "WWII 기술의 순양함. 양용포 4문과 대공포 2문으로 포격·방공을 고루 맡는다. 구축함보다 느리고 무겁게 돌지만 선체가 단단하고 장갑이 있으며 더 크게 자란다.");
                Set(so, "kind", (int)StartShipKind.Ww2Cruiser);
                Set(so, "color", new Color(0.78f, 0.86f, 1f));
                Set(so, "startLoadout", loadoutCruiser);
                Set(so, "expandedLoadout", loadoutCruiser);
                Set(so, "bridgeDefinition", defBridge);
                Set(so, "startSummary", "양용포 4 · 4연장 대공포 2 · 현측 어뢰 4연장 1 · 호위함 없음");
                Set(so, "initialEscortCount", 0);
                Set(so, "hullProfile", hullCruiser);
            });

            var waves = CreateSO<RoundSet>($"{DataDir}/RoundSet_WW2_Test.asset");
            Configure(waves, so =>
            {
                var rounds = so.FindProperty("rounds");
                // 2026-10-17 플레이 피드백 "난이도가 낮아 앞·옆 차이를 확인하기 어렵다": 스폰·동시 수를 올리고 어뢰정·편대를 섞는다.
                // 편대는 한 번 스폰에 3기(뇌격)·2기(급강하)가 나오므로 가중치는 낮게 둔다.
                var spec = new (string title, float dur, float rate, int max, (EnemyDefinition e, float w)[] entries, EnemyDefinition boss, EnemyDefinition escort, int escortCount)[]
                {
                    // 소형 포함 기총이 피해 대부분을 차지하지 않게(첫 시험: 소형 포함 137 · 뇌격 47) 포함 비중은 낮추고 어뢰·항공 위협 비중을 둔다
                    ("초계 접촉", 55f, 0.45f, 6, new[] { (eFast, 1f) }, null, null, 0),
                    ("포격함·어뢰정", 70f, 0.5f, 8, new[] { (eFast, 0.7f), (eGunboat, 0.45f), (eTorpBoat, 0.45f) }, null, null, 0),
                    ("뇌격기 편대", 75f, 0.5f, 10, new[] { (eFast, 0.5f), (eTorpBomber, 0.45f), (eTorpBoat, 0.35f) }, null, null, 0),
                    ("급강하 폭격", 75f, 0.55f, 11, new[] { (eFast, 0.4f), (eGunboat, 0.4f), (eDiveBomber, 0.55f), (eTorpBomber, 0.3f) }, null, null, 0),
                    ("적 순양함", 60f, 0.35f, 8, new[] { (eFast, 0.5f), (eTorpBoat, 0.35f), (eTorpBomber, 0.3f), (eDiveBomber, 0.25f) }, eBoss, eTorpBoat, 2),
                };
                rounds.arraySize = spec.Length;
                for (int i = 0; i < spec.Length; i++)
                {
                    var r = rounds.GetArrayElementAtIndex(i);
                    r.FindPropertyRelative("Title").stringValue = spec[i].title;
                    r.FindPropertyRelative("Duration").floatValue = spec[i].dur;
                    r.FindPropertyRelative("SpawnRate").floatValue = spec[i].rate;
                    r.FindPropertyRelative("MaxAlive").intValue = spec[i].max;
                    var entries = r.FindPropertyRelative("Entries");
                    entries.arraySize = spec[i].entries.Length;
                    for (int j = 0; j < spec[i].entries.Length; j++)
                    {
                        var e = entries.GetArrayElementAtIndex(j);
                        e.FindPropertyRelative("Enemy").objectReferenceValue = spec[i].entries[j].e;
                        e.FindPropertyRelative("Weight").floatValue = spec[i].entries[j].w;
                    }
                    r.FindPropertyRelative("Boss").objectReferenceValue = spec[i].boss;
                    r.FindPropertyRelative("Escort").objectReferenceValue = spec[i].escort;
                    r.FindPropertyRelative("EscortCount").intValue = spec[i].escortCount;
                }
            });

            var ruleset = CreateSO<RulesetDefinition>(RulesetPath);
            Configure(ruleset, so =>
            {
                Set(so, "id", "ww2_v1");
                Set(so, "schemaVersion", 1);
                Set(so, "contentVersion", 5);   // 2026-10-17: 어뢰정 · 편대 · 웨이브 강화 → Codex 모델 v30 · 2026-10-18: v31(어뢰정 모델·선체·뇌격기 변형) → C1 함종(순양함·톤수)
                Set(so, "title", "WWII 시험 전투");
                Set(so, "idPrefix", "ww2_");
                SetArray(so, "hulls", concept, conceptCruiser);
                SetArray(so, "modules", defBridge, defGun, defAa, defTorpedo);
                SetArray(so, "enemies", eFast, eGunboat, eTorpBoat, eTorpBomber, eDiveBomber, eBoss);
                SetArray(so, "stages", waves);
                var stats = so.FindProperty("allowedStats");
                var allowed = new[] { RunStat.Damage, RunStat.FireRate, RunStat.Range, RunStat.Speed, RunStat.HullMax, RunStat.Defense };
                stats.arraySize = allowed.Length;
                for (int i = 0; i < allowed.Length; i++) stats.GetArrayElementAtIndex(i).enumValueIndex = (int)allowed[i];
                Set(so, "allowsEscorts", false);
                Set(so, "saveFile", "naval_run_ww2_v1.json");
                Set(so, "hullDeckPlate", deckPlate);
                Set(so, "hullSideSkirt", sideSkirt);
                Set(so, "hullBowDeck", bowDeck);
                Set(so, "hullBowSide", bowSide);
            });

            // 카드·사전 아이콘
            foreach (var (def, prefab) in new[] { (defGun, dualGun), (defAa, quadAa), (defTorpedo, torpedoMount), (defBridge, bridge) })
            {
                var icon = NavalIconBaker.Bake(prefab, def.Id);
                if (icon != null) Configure(def, so => Set(so, "icon", icon));
            }

            AssetDatabase.SaveAssets();
            var problems = ruleset.Validate();
            Debug.Log(problems.Count == 0
                ? "[WW2] 패치 B 콘텐츠를 만들었습니다 — 규칙 집합 점검 문제 없음"
                : "[WW2] 규칙 집합 점검 문제:\n" + string.Join("\n", problems));
        }

        // ------------------------------------------------------------ 데이터

        private static TargetEfficiency Eff(float small, float medium, float large, float boss, float sub, float drone, float air, float missile)
            => new() { SmallSurface = small, MediumSurface = medium, LargeSurface = large, Boss = boss, Submarine = sub, Drone = drone, Air = air, Missile = missile };

        private static ModuleDefinition ModuleDef(string id, string name, string description, ModuleType type, int width, int height, bool canRotate,
            PlacementZone zone, ModuleHeight heightClass, float hp, Rarity rarity, float weight, float tonnage, GameObject prefab, Action<ModuleStatsBox> stats, TargetEfficiency eff)
        {
            var def = CreateSO<ModuleDefinition>($"{DataDir}/{id}.asset");
            var box = new ModuleStatsBox();
            stats(box);
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "description", description);
                Set(so, "type", (int)type);
                Set(so, "width", width);
                Set(so, "height", height);
                Set(so, "canRotate", canRotate);
                Set(so, "placement.Zone", (int)zone);
                Set(so, "heightClass", (int)heightClass);
                Set(so, "maxCount", 0);
                Set(so, "maxHp", hp);
                Set(so, "rarity", (int)rarity);
                Set(so, "weight", weight);
                Set(so, "tonnage", tonnage);
                Set(so, "prefab", prefab);
                box.Write(so);
                Set(so, "targetEfficiency.SmallSurface", eff.SmallSurface);
                Set(so, "targetEfficiency.MediumSurface", eff.MediumSurface);
                Set(so, "targetEfficiency.LargeSurface", eff.LargeSurface);
                Set(so, "targetEfficiency.Boss", eff.Boss);
                Set(so, "targetEfficiency.Submarine", eff.Submarine);
                Set(so, "targetEfficiency.Drone", eff.Drone);
                Set(so, "targetEfficiency.Air", eff.Air);
                Set(so, "targetEfficiency.Missile", eff.Missile);
            });
            return def;
        }

        private static HullProfile Hull(string name, float maxSpeed, float turn, float accel, float decel, float response,
            float hp, float armor, int halfLength, int halfBeam, float budget)
        {
            var hull = CreateSO<HullProfile>($"{DataDir}/{name}.asset");
            Configure(hull, so =>
            {
                Set(so, "maxSpeed", maxSpeed);
                Set(so, "turnRate", turn);
                Set(so, "acceleration", accel);
                Set(so, "deceleration", decel);
                Set(so, "turnResponseTime", response);
                Set(so, "hullHp", hp);
                Set(so, "armor", armor);
                Set(so, "halfLength", halfLength);
                Set(so, "halfBeam", halfBeam);
                Set(so, "tonnageBudget", budget);
            });
            return hull;
        }

        private static StartingLoadout Loadout(string name, params (ModuleDefinition def, int x, int z, int rot)[] list)
        {
            var loadout = CreateSO<StartingLoadout>($"{DataDir}/{name}.asset");
            Configure(loadout, so =>
            {
                var entries = so.FindProperty("entries");
                entries.arraySize = list.Length;
                for (int i = 0; i < list.Length; i++)
                {
                    var e = entries.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Module").objectReferenceValue = list[i].def;
                    e.FindPropertyRelative("Origin").FindPropertyRelative("X").intValue = list[i].x;
                    e.FindPropertyRelative("Origin").FindPropertyRelative("Z").intValue = list[i].z;
                    e.FindPropertyRelative("RotationSteps").intValue = list[i].rot;
                }
            });
            return loadout;
        }

        /// <summary>ModuleStats를 이름으로 채우는 상자(쓰지 않은 칸은 0).</summary>
        private sealed class ModuleStatsBox
        {
            public float Range, MinRange, Damage, ReloadTime, ProjectileSpeed, FireArcDegrees, TurretTurnRate, AmmoReloadTime, DetectionRange;
            public float HullRepairPerSecond, ModuleRepairPerSecond, DamageReduction;
            public int MagazineCapacity, AmmoPerShot, AmmoReloadAmount, ExtraTrackedTargets;
            public AmmoFamily AmmoFamily;

            public void Write(SerializedObject so)
            {
                Set(so, "stats.Range", Range); Set(so, "stats.MinRange", MinRange); Set(so, "stats.Damage", Damage);
                Set(so, "stats.ReloadTime", ReloadTime); Set(so, "stats.ProjectileSpeed", ProjectileSpeed);
                Set(so, "stats.FireArcDegrees", FireArcDegrees); Set(so, "stats.TurretTurnRate", TurretTurnRate);
                Set(so, "stats.AmmoFamily", (int)AmmoFamily); Set(so, "stats.MagazineCapacity", MagazineCapacity);
                Set(so, "stats.AmmoPerShot", AmmoPerShot); Set(so, "stats.AmmoReloadAmount", AmmoReloadAmount);
                Set(so, "stats.AmmoReloadTime", AmmoReloadTime); Set(so, "stats.DetectionRange", DetectionRange);
                Set(so, "stats.ExtraTrackedTargets", ExtraTrackedTargets); Set(so, "stats.HullRepairPerSecond", HullRepairPerSecond);
                Set(so, "stats.ModuleRepairPerSecond", ModuleRepairPerSecond); Set(so, "stats.DamageReduction", DamageReduction);
            }
        }

        private static EnemyDefinition EnemyDef(string id, string name, TargetKind kind, GameObject prefab, TargetCategory category, EnemyRank rank,
            int value, int maxAlive, float hp, float speed, float turn, float range, float damage, float cooldown, int xp, int group = 1)
        {
            var def = CreateSO<EnemyDefinition>($"{DataDir}/{id}.asset");
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "kind", (int)kind);
                Set(so, "prefab", prefab);
                Set(so, "category", (int)category);
                Set(so, "rank", (int)rank);
                Set(so, "targetValue", value);
                Set(so, "maxAlive", maxAlive);
                Set(so, "maxHp", hp);
                Set(so, "moveSpeed", speed);
                Set(so, "turnRateDegPerSec", turn);
                Set(so, "preferredRange", range);
                Set(so, "attackDamage", damage);
                Set(so, "attackCooldown", cooldown);
                Set(so, "xpReward", xp);
                Set(so, "groupSize", group);
            });
            return def;
        }

        // ------------------------------------------------------------ 프리팹 공통

        private static T Load<T>(string relative) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{Root}/{relative}");

        /// <summary>기존 프리팹을 WW2 폴더에 새 이름으로 복제해 고친다(다시 돌리면 같은 경로·GUID에 덮어쓴다).</summary>
        private static GameObject Clone(string sourceRelative, string name, Action<GameObject> edit)
        {
            string src = $"{Root}/{sourceRelative}";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null) { Debug.LogError($"[WW2] 원본 없음: {src}"); return null; }
            var root = PrefabUtility.LoadPrefabContents(src);
            try
            {
                root.name = name;
                edit?.Invoke(root);
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static GameObject Save(GameObject root)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{root.name}.prefab");
            Object.DestroyImmediate(root);
            return saved;
        }

        private static Transform Node(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3? euler = null)
        {
            var go = Primitive(name, PrimitiveType.Cube, size, mat, parent);
            go.transform.localPosition = pos;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            return go;
        }

        /// <summary>Z축으로 누운 원통(포신·발사관·동체). length = 전체 길이.</summary>
        private static GameObject Tube(string name, Transform parent, Vector3 center, float diameter, float length, Material mat)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, new Vector3(diameter, length * 0.5f, diameter), mat, parent);
            go.transform.localPosition = center;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return go;
        }

        private static GameObject Disk(string name, Transform parent, Vector3 pos, float diameter, float thickness, Material mat)
        {
            var go = Primitive(name, PrimitiveType.Cylinder, new Vector3(diameter, thickness * 0.5f, diameter), mat, parent);
            go.transform.localPosition = pos;
            return go;
        }

        // ------------------------------------------------------------ 탄

        private static GameObject BuildPlayerTorpedo(GameObject enemyTorpedo)
        {
            if (enemyTorpedo == null) { Debug.LogError("[WW2] TOR_Enemy 없음"); return null; }
            return Clone("Prefabs/Projectiles/TOR_Enemy.prefab", "TOR_Ww2Player", root =>
            {
                var old = root.GetComponent<Torpedo>();
                var so = new SerializedObject(old);
                var wake = so.FindProperty("wake").objectReferenceValue as TrailRenderer;
                var impact = so.FindProperty("impactEffect").objectReferenceValue as GameObject;
                Object.DestroyImmediate(old);
                foreach (var c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var t = root.AddComponent<Ww2StraightTorpedo>();
                Configure(t, s =>
                {
                    Set(s, "wake", wake);
                    Set(s, "impactEffect", impact);
                    Set(s, "hitMask", 1 << LayerEnemy);
                });
                SetLayerRecursive(root, LayerPlayerMissile);
                SkinProjectile(root, "TOR_Ww2Player");
            });
        }

        // ------------------------------------------------------------ 블록(그레이박스)

        private static GameObject BuildDualGun(GameObject shell, GameObject flash)
        {
            if (Art("MOD_Ww2DualGun") is { } art) return BuildDualGunArt(art, shell, flash);
            var root = new GameObject("MOD_Ww2DualGun");
            var R = root.transform;
            Disk("Base", R, new Vector3(0f, 0.1f, 0f), 1.6f, 0.2f, _deck);
            var turret = Node("TurretPivot", R, new Vector3(0f, 0.22f, 0f));
            Box("Gunhouse", turret, new Vector3(0f, 0.38f, -0.15f), new Vector3(1.25f, 0.72f, 1.5f), _gun);
            Box("Gunhouse_Roof", turret, new Vector3(0f, 0.78f, -0.25f), new Vector3(1.15f, 0.08f, 1.2f), _hull);
            Box("Gunhouse_Face", turret, new Vector3(0f, 0.38f, 0.62f), new Vector3(1.1f, 0.6f, 0.08f), _hull, new Vector3(-12f, 0f, 0f));
            var elevation = Node("ElevationPivot", turret, new Vector3(0f, 0.42f, 0.5f));
            Tube("Barrel_L", elevation, new Vector3(-0.22f, 0f, 0.8f), 0.13f, 1.6f, _dark);
            Tube("Barrel_R", elevation, new Vector3(0.22f, 0f, 0.8f), 0.13f, 1.6f, _dark);
            var muzzle = Node("Muzzle", elevation, new Vector3(0f, 0f, 1.65f));
            var muzzleA = Node("Muzzle_01", elevation, new Vector3(-0.22f, 0f, 1.65f));
            var muzzleB = Node("Muzzle_02", elevation, new Vector3(0.22f, 0f, 1.65f));
            var comp = root.AddComponent<Ww2DualPurposeGunModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "weapon.minElevation", -5f);
                Set(so, "weapon.maxElevation", 70f);
                Set(so, "weapon.aimTolerance", 4f);
                Set(so, "projectilePrefab", shell);
                Set(so, "muzzleFlash", flash);
                Set(so, "muzzleA", muzzleA);
                Set(so, "muzzleB", muzzleB);
            });
            return Save(root);
        }

        private static GameObject BuildQuadAa(GameObject round)
        {
            if (Art("MOD_Ww2QuadAa") is { } art) return BuildQuadAaArt(art, round);
            var root = new GameObject("MOD_Ww2QuadAa");
            var R = root.transform;
            Disk("Tub_Floor", R, new Vector3(0f, 0.08f, 0f), 1.75f, 0.16f, _deck);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var wall = Box($"Tub_Wall_{i}", R, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.32f, 0.82f), new Vector3(0.7f, 0.45f, 0.08f), _hull);
                wall.transform.localRotation = Quaternion.Euler(0f, a, 0f);
            }
            var turret = Node("TurretPivot", R, new Vector3(0f, 0.2f, 0f));
            Box("Carriage", turret, new Vector3(0f, 0.25f, -0.05f), new Vector3(0.9f, 0.35f, 0.7f), _gun);
            Box("Shield", turret, new Vector3(0f, 0.55f, 0.35f), new Vector3(1.0f, 0.5f, 0.06f), _gun);
            var elevation = Node("ElevationPivot", turret, new Vector3(0f, 0.55f, 0.15f));
            var muzzles = new List<Transform>();
            int n = 0;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 2; col++)
                {
                    float x = (col == 0 ? -1f : 1f) * (row == 0 ? 0.12f : 0.32f);
                    float y = row == 0 ? 0.06f : -0.02f;
                    Tube($"Barrel_{++n:00}", elevation, new Vector3(x, y, 0.65f), 0.07f, 1.2f, _dark);
                    muzzles.Add(Node($"Muzzle_{n:00}", elevation, new Vector3(x, y, 1.27f)));
                }
            var muzzle = Node("Muzzle", elevation, new Vector3(0f, 0.02f, 1.27f));
            var comp = root.AddComponent<Ww2QuadAaModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "weapon.minElevation", -5f);
                Set(so, "weapon.maxElevation", 85f);
                Set(so, "weapon.aimTolerance", 6f);
                Set(so, "projectilePrefab", round);
                SetArray(so, "muzzles", muzzles.ToArray());
            });
            return Save(root);
        }

        private static GameObject BuildTorpedoMount(GameObject torpedo)
        {
            if (Art("MOD_Ww2TorpedoMount") is { } art) return BuildTorpedoMountArt(art, torpedo);
            var root = new GameObject("MOD_Ww2TorpedoMount");
            var R = root.transform;
            Box("Deck", R, new Vector3(0f, 0.08f, 0f), new Vector3(1.8f, 0.16f, 1.8f), _deck);
            Disk("Pedestal", R, new Vector3(0f, 0.25f, 0f), 0.7f, 0.2f, _gun);
            var mount = Node("TubeMount", R, new Vector3(0f, 0.45f, 0f));
            Box("Cradle", mount, new Vector3(0f, -0.05f, 0f), new Vector3(1.25f, 0.12f, 0.9f), _gun);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.3f;
                Tube($"Tube_{i + 1:00}", mount, new Vector3(x, 0.12f, 0.15f), 0.26f, 2.1f, _hull);
                Disk($"TubeCap_{i + 1:00}", mount, new Vector3(x, 0.12f, 1.21f), 0.27f, 0.04f, _dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var comp = root.AddComponent<Ww2BroadsideTorpedoModule>();
            Configure(comp, so =>
            {
                Set(so, "torpedoPrefab", torpedo);
                Set(so, "mount", mount);
            });
            return Save(root);
        }

        /// <summary>
        /// WWII 함교: 범용 초계함 함교 프리팹(BridgeModule 설정·발사점·레이더 노드)을 복제해, 현대 겉모습(렌더러)을 걷어 내고
        /// 회색 상자로 함교탑(앞)·연돌(뒤)을 세운다. 기만체 끔 · 연막 켬(패치 A 9.1).
        /// 함교 기관총은 렌더러 윤곽 맨 위 앞쪽에 붙으므로 연돌을 함교탑보다 낮게 둔다.
        /// </summary>
        private static GameObject BuildBridge()
        {
            const string src = "Prefabs/StartingShips/Bridges/MOD_Bridge_Patrol.prefab";
            return Clone(src, "MOD_Ww2Bridge", root =>
            {
                // 안에 든 모델 프리팹(FBX)은 풀어서 평범한 오브젝트로 만든 뒤 렌더러를 걷어 낸다
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t != root.transform && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                        PrefabUtility.UnpackPrefabInstance(t.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    Object.DestroyImmediate(r);
                    if (mf != null) Object.DestroyImmediate(mf);
                }
                var bridgeArt = Art("MOD_Ww2Bridge");
                if (bridgeArt != null)
                {
                    MountBridgeArt(bridgeArt, root);
                    var bm = root.GetComponent<BridgeModule>();
                    Configure(bm, so => { Set(so, "decoyEnabled", false); Set(so, "smokeRounds", true); });
                    return;
                }
                var R = Node("Ww2Visual", root.transform, Vector3.zero);
                Box("Deckhouse", R, new Vector3(0f, 0.45f, 0.2f), new Vector3(1.6f, 0.9f, 4.6f), _hull);
                Box("Bridge_Lower", R, new Vector3(0f, 1.25f, 1.6f), new Vector3(1.5f, 0.75f, 1.5f), _hull);
                Box("Bridge_Upper", R, new Vector3(0f, 1.85f, 1.75f), new Vector3(1.35f, 0.5f, 1.1f), _hull);
                Box("Bridge_Windows", R, new Vector3(0f, 1.92f, 2.31f), new Vector3(1.2f, 0.16f, 0.04f), _dark);
                Box("Rangefinder", R, new Vector3(0f, 2.22f, 1.65f), new Vector3(1.0f, 0.16f, 0.22f), _gun);
                var funnel = Primitive("Funnel", PrimitiveType.Cylinder, new Vector3(0.75f, 0.8f, 0.95f), _hull, R);
                funnel.transform.localPosition = new Vector3(0f, 1.6f, -1.2f);
                funnel.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                var cap = Primitive("FunnelCap", PrimitiveType.Cylinder, new Vector3(0.8f, 0.06f, 1.0f), _dark, R);
                cap.transform.localPosition = new Vector3(0f, 2.38f, -1.32f);
                cap.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
                var bridge = root.GetComponent<BridgeModule>();
                Configure(bridge, so =>
                {
                    Set(so, "decoyEnabled", false);
                    Set(so, "smokeRounds", true);
                });
            });
        }

        // ------------------------------------------------------------ 적(그레이박스)

        private static GameObject Plane(string name, out Transform R, Vector3 colliderSize)
        {
            var root = new GameObject(name);
            R = root.transform;
            var col = root.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.isTrigger = true;
            return root;
        }

        private static void Airframe(Transform R, float span, float length, bool gullWing)
        {
            Tube("Fuselage", R, new Vector3(0f, 0f, 0f), 0.42f, length, _air);
            Disk("Cowling", R, new Vector3(0f, 0f, length * 0.5f), 0.5f, 0.3f, _airDark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Disk("Propeller", R, new Vector3(0f, 0f, length * 0.5f + 0.18f), 1.1f, 0.02f, _airDark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Canopy", R, new Vector3(0f, 0.25f, length * 0.12f), new Vector3(0.3f, 0.22f, 0.8f), _airDark);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(side < 0 ? "Wing_L" : "Wing_R", R, new Vector3(side * span * 0.27f, gullWing ? 0.08f : -0.12f, length * 0.08f),
                    new Vector3(span * 0.5f, 0.07f, 0.85f), _air, new Vector3(0f, 0f, side * (gullWing ? -6f : 3f)));
                Box(side < 0 ? "Stabilizer_L" : "Stabilizer_R", R, new Vector3(side * 0.45f, 0.05f, -length * 0.45f), new Vector3(0.8f, 0.05f, 0.4f), _air);
            }
            Box("Fin", R, new Vector3(0f, 0.32f, -length * 0.46f), new Vector3(0.06f, 0.55f, 0.5f), _air);
        }

        private static GameObject BuildTorpedoBomber(GameObject torpedo)
        {
            if (Art("ENE_Ww2TorpedoBomber") is { } art) return BuildTorpedoBomberArt(art, torpedo);
            var root = Plane("ENE_Ww2TorpedoBomber", out var R, new Vector3(3.2f, 1f, 2.8f));
            Airframe(R, 3.6f, 2.6f, false);
            Tube("Torpedo", R, new Vector3(0f, -0.35f, 0.1f), 0.2f, 1.6f, _torpedoWarn);   // 배에 단 주황 어뢰 — 뇌격기 표시
            var comp = root.AddComponent<Ww2TorpedoBomber>();
            Configure(comp, so =>
            {
                Set(so, "torpedoPrefab", torpedo);
                Set(so, "cruiseAltitude", 9f);
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        private static GameObject BuildDiveBomber(GameObject bomb, GameObject marker)
        {
            if (Art("ENE_Ww2DiveBomber") is { } art) return BuildDiveBomberArt(art, bomb, marker);
            var root = Plane("ENE_Ww2DiveBomber", out var R, new Vector3(3f, 1.1f, 2.6f));
            Airframe(R, 3.2f, 2.4f, true);
            for (int side = -1; side <= 1; side += 2)
                Box(side < 0 ? "Gear_L" : "Gear_R", R, new Vector3(side * 0.45f, -0.42f, 0.35f), new Vector3(0.12f, 0.5f, 0.22f), _airDark);   // 고정식 다리
            Disk("Bomb", R, new Vector3(0f, -0.32f, 0.05f), 0.3f, 0.7f, _bombWarn).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 붉은 폭탄
            var comp = root.AddComponent<Ww2DiveBomber>();
            Configure(comp, so =>
            {
                Set(so, "bombPrefab", bomb);
                Set(so, "warningMarker", marker);
                Set(so, "cruiseAltitude", 14f);
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        private static GameObject BuildCruiser(GameObject shell, GameObject secondaryRound, GameObject flash)
        {
            if (Art("ENE_Ww2GunneryCruiser") is { } art) return BuildCruiserArt(art, shell, secondaryRound, flash);
            var root = new GameObject("ENE_Ww2GunneryCruiser");
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(3.6f, 3f, 18f);
            col.center = new Vector3(0f, 0.8f, 0f);
            col.isTrigger = true;
            var P = Node("ModelPivot", root.transform, Vector3.zero);
            Box("Hull", P, new Vector3(0f, 0f, -0.6f), new Vector3(3.2f, 1.6f, 15f), _enemyHull);
            Box("Bow", P, new Vector3(0f, 0.05f, 7.4f), new Vector3(2.3f, 1.5f, 2.3f), _enemyHull, new Vector3(0f, 45f, 0f));
            Box("Deck", P, new Vector3(0f, 0.82f, -0.4f), new Vector3(3.0f, 0.05f, 14.6f), _enemyDeck);
            Box("Superstructure", P, new Vector3(0f, 1.55f, 1.6f), new Vector3(2.0f, 1.4f, 3.2f), _enemyHull);
            Box("Bridge", P, new Vector3(0f, 2.55f, 2.4f), new Vector3(1.6f, 0.7f, 1.4f), _enemyHull);
            Box("Mast", P, new Vector3(0f, 3.6f, 1.6f), new Vector3(0.18f, 2.2f, 0.18f), _dark);
            var funnel = Primitive("Funnel", PrimitiveType.Cylinder, new Vector3(1.0f, 0.9f, 1.3f), _enemyHull, P);
            funnel.transform.localPosition = new Vector3(0f, 2.2f, -1.0f);
            var cap = Primitive("FunnelCap", PrimitiveType.Cylinder, new Vector3(1.05f, 0.06f, 1.35f), _dark, P);
            cap.transform.localPosition = new Vector3(0f, 3.1f, -1.0f);
            Box("AftHouse", P, new Vector3(0f, 1.25f, -3.2f), new Vector3(1.8f, 0.8f, 2.0f), _enemyHull);

            var turrets = new List<Transform>();
            var muzzles = new List<Transform>();
            void Turret(string name, float z, float height, bool aft)
            {
                var t = Node($"{name}_TurretPivot", P, new Vector3(0f, height, z));
                if (aft) t.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Box("Gunhouse", t, new Vector3(0f, 0.3f, -0.2f), new Vector3(1.5f, 0.6f, 1.6f), _gun);
                for (int s = -1; s <= 1; s += 2)
                {
                    Tube($"Barrel_{(s < 0 ? "L" : "R")}", t, new Vector3(s * 0.28f, 0.32f, 1.25f), 0.16f, 2.1f, _dark);
                    muzzles.Add(Node($"Muzzle_{(s < 0 ? "L" : "R")}", t, new Vector3(s * 0.28f, 0.32f, 2.3f)));
                }
                turrets.Add(t);
            }
            Turret("A", 5.2f, 0.85f, false);
            Turret("B", 3.6f, 1.25f, false);
            Turret("X", -5.2f, 0.85f, true);
            var secondary = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var m = Node(s < 0 ? "Secondary_P" : "Secondary_S", P, new Vector3(s * 1.35f, 1.15f, -0.2f));
                Box("Mount", m, new Vector3(0f, 0.1f, 0f), new Vector3(0.5f, 0.3f, 0.6f), _gun);
                Tube("Barrel", m, new Vector3(0f, 0.15f, 0.5f), 0.08f, 0.8f, _dark);
                secondary.Add(Node("Muzzle", m, new Vector3(s * 0.1f, 0.15f, 0.95f)));
            }

            var comp = root.AddComponent<Ww2GunneryBoss>();
            Configure(comp, so =>
            {
                SetArray(so, "turrets", turrets.ToArray());
                SetArray(so, "muzzles", muzzles.ToArray());
                Set(so, "shellPrefab", shell);
                Set(so, "muzzleFlash", flash);
                SetArray(so, "secondaryMuzzles", secondary.ToArray());
                Set(so, "secondaryProjectile", secondaryRound);
                Set(so, "shellsPhase1", 6);
            });
            SetLayerRecursive(root, LayerEnemy);
            return Save(root);
        }

        [MenuItem("Naval/WW2/Validate Ruleset")]
        public static void Validate()
        {
            var ruleset = AssetDatabase.LoadAssetAtPath<RulesetDefinition>(RulesetPath);
            if (ruleset == null) { Debug.LogError($"[WW2] {RulesetPath} 없음"); return; }
            var problems = ruleset.Validate();
            Debug.Log(problems.Count == 0 ? "[WW2] 규칙 집합 점검: 문제 없음" : "[WW2] 규칙 집합 점검 문제:\n" + string.Join("\n", problems));
        }
    }
}
