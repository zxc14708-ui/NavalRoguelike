using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Combat;
using Game.Enemies;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 그레이박스 프리팹을 전부 만든다.
    /// Cube/Cylinder만 쓰되 실루엣이 서로 구분되도록 형태를 다르게 준다.
    /// </summary>
    public static class NavalPrefabBuilder
    {
        /// <summary>생성된 프리팹 참조 모음. 이후 SO/씬 셋업에서 사용한다.</summary>
        public class Result
        {
            public GameObject ProjAutocannon, ProjCiws, ProjEnemyGun, ProjGun76, FxGunFlash;
            public GameObject MissilePlayer, MissileEnemy, RocketPlayer, MissileSam;
            public GameObject FxMuzzleSmoke, FxJamPulse, FxRepairPulse, FxSmokeScreen, MountPedestal;
            public GameObject Decoy, Helicopter, CellHighlight, Torpedo, DepthCharge, AswTorpedo;
            public GameObject DeckPlate, SideSkirt, BowFittings, BowAnchor;
            public GameObject FastBoat, MissileBoat, Submarine, Boss, Boss2, Drone, Recon, Fighter;
            public GameObject CruiseSubmarine, PccCorvette;   // 스테이지 2 추가 적(모델이 없으면 null)
            public GameObject ModernCorvette;                 // 스테이지 2 보스: 현대화 초계함(모델이 없으면 null)
            public GameObject UnmannedCraft, EwCorvette, AaFrigate, AttackSubmarine;   // 스테이지 3 적(모델이 없으면 null)
            public GameObject SuicideBoat;                    // 자폭 보트(없으면 null)
            // 일반 적 5종(Codex RecommendedNormalEnemies_5Pack) + 기뢰. 모델이 없으면 null
            public GameObject ArmoredBoat, TorpedoBoat, ArtilleryBoat, RepairBoat, MineLayer, SeaMine;

            public GameObject Bridge, Autocannon, Radar, Vls, RocketLauncher, AswLauncher, Gun76, SamLauncher, EwSuite,
                              Ciws, DecoyLauncher, Magazine, Sonar, HeliDeck, RepairBay;
        }

        private const float Cell = 2f;

        /// <summary>
        /// 이미 만들어진 프리팹을 경로로 다시 읽어온다.
        /// 씬 생성 시 인스턴스가 언로드될 수 있으므로 씬 조립 직전에 호출한다.
        /// </summary>
        public static Result Load()
        {
            string fx = $"{Root}/Prefabs/Projectiles";
            string ene = $"{Root}/Prefabs/Enemies";
            string mod = $"{Root}/Prefabs/Modules";
            string vfx = $"{Root}/Prefabs/VFX";

            return new Result
            {
                ProjAutocannon = Get($"{fx}/PRJ_Autocannon.prefab"),
                ProjCiws = Get($"{fx}/PRJ_Ciws.prefab"),
                ProjEnemyGun = Get($"{fx}/PRJ_EnemyGun.prefab"),
                ProjGun76 = Get($"{fx}/PRJ_Gun76.prefab"),
                FxGunFlash = Get($"{vfx}/FX_GunFlash.prefab"),
                MissilePlayer = Get($"{fx}/MIS_PlayerVls.prefab"),
                MissileEnemy = Get($"{fx}/MIS_EnemyAsm.prefab"),
                RocketPlayer = Get($"{fx}/MIS_PlayerRocket.prefab"),
                MissileSam = Get($"{fx}/MIS_PlayerSam.prefab"),
                FxMuzzleSmoke = Get($"{vfx}/FX_MuzzleSmoke.prefab"),
                FxJamPulse = Get($"{vfx}/FX_JamPulse.prefab"),
                FxRepairPulse = Get($"{vfx}/FX_RepairPulse.prefab"),
                FxSmokeScreen = Get($"{vfx}/FX_SmokeScreen.prefab"),
                MountPedestal = Get($"{vfx}/HULL_MountPedestal.prefab"),
                Decoy = Get($"{fx}/DEC_Decoy.prefab"),
                Torpedo = Get($"{fx}/TOR_Enemy.prefab"),
                DepthCharge = Get($"{fx}/DC_Player.prefab"),
                AswTorpedo = Get($"{fx}/TOR_PlayerAsw.prefab"),
                Helicopter = Get($"{fx}/HEL_Asw.prefab"),
                CellHighlight = Get($"{vfx}/FX_CellHighlight.prefab"),
                DeckPlate = Get($"{vfx}/HULL_DeckPlate.prefab"),
                SideSkirt = Get($"{vfx}/HULL_SideSkirt.prefab"),
                // 함수 소품은 선택이다. 모델이 없으면 프리팹도 없으니 오류로 보지 않는다.
                BowFittings = AssetDatabase.LoadAssetAtPath<GameObject>($"{vfx}/HULL_BowFittings.prefab"),
                BowAnchor = Get($"{vfx}/HULL_Anchor.prefab"),

                FastBoat = Get($"{ene}/ENE_FastBoat.prefab"),
                MissileBoat = Get($"{ene}/ENE_MissileBoat.prefab"),
                Submarine = Get($"{ene}/ENE_Submarine.prefab"),
                Boss = Get($"{ene}/ENE_Boss.prefab"),
                Boss2 = Get($"{ene}/ENE_Boss2.prefab"),
                Drone = Get($"{ene}/ENE_Drone.prefab"),
                Fighter = Get($"{ene}/ENE_Fighter.prefab"),
                Recon = Get($"{ene}/ENE_Recon.prefab"),
                // 추가 적은 선택이다. 아직 만들지 않았으면 null
                CruiseSubmarine = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_CruiseSubmarine.prefab"),
                ModernCorvette = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_BossCorvette.prefab"),
                UnmannedCraft = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_UnmannedCraft.prefab"),
                EwCorvette = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_EwCorvette.prefab"),
                AaFrigate = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_AaFrigate.prefab"),
                AttackSubmarine = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_AttackSubmarine.prefab"),
                PccCorvette = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_PccCorvette.prefab"),
                SuicideBoat = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_SuicideBoat.prefab"),
                ArmoredBoat = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_ArmoredBoat.prefab"),
                TorpedoBoat = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_TorpedoBoat.prefab"),
                ArtilleryBoat = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_ArtilleryBoat.prefab"),
                RepairBoat = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_RepairBoat.prefab"),
                MineLayer = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_MineLayer.prefab"),
                SeaMine = AssetDatabase.LoadAssetAtPath<GameObject>($"{ene}/ENE_SeaMine.prefab"),

                Bridge = Get($"{mod}/MOD_Bridge.prefab"),
                Autocannon = Get($"{mod}/MOD_Autocannon.prefab"),
                Radar = Get($"{mod}/MOD_Radar.prefab"),
                Vls = Get($"{mod}/MOD_Vls.prefab"),
                RocketLauncher = Get($"{mod}/MOD_RocketLauncher.prefab"),
                AswLauncher = Get($"{mod}/MOD_AswLauncher.prefab"),
                Gun76 = Get($"{mod}/MOD_Gun76.prefab"),
                SamLauncher = Get($"{mod}/MOD_SamLauncher.prefab"),
                EwSuite = Get($"{mod}/MOD_EwSuite.prefab"),
                Ciws = Get($"{mod}/MOD_Ciws.prefab"),
                DecoyLauncher = Get($"{mod}/MOD_DecoyLauncher.prefab"),
                Magazine = Get($"{mod}/MOD_Magazine.prefab"),
                Sonar = Get($"{mod}/MOD_Sonar.prefab"),
                RepairBay = Get($"{mod}/MOD_RepairBay.prefab"),
                HeliDeck = Get($"{mod}/MOD_HeliDeck.prefab"),
            };
        }

        /// <summary>오프셋이 필요한 조각을 피벗으로 감싼다.</summary>
        private static GameObject WrapPivot(GameObject child, string name)
        {
            var root = new GameObject(name);
            child.name = "Model";
            child.transform.SetParent(root.transform, false);
            return root;
        }

        private static GameObject Get(string path)
        {
            var a = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (a == null) Debug.LogError($"[Setup] 프리팹을 찾지 못했습니다: {path}");
            return a;
        }

        public static Result BuildAll()
        {
            string fxDir = $"{Root}/Prefabs/Projectiles";
            string eneDir = $"{Root}/Prefabs/Enemies";
            string modDir = $"{Root}/Prefabs/Modules";
            string vfxDir = $"{Root}/Prefabs/VFX";

            var r = new Result();

            // --- 머티리얼 팔레트 (실루엣 구분용)
            var mHull    = CreateMaterial("hull",    new Color(0.35f, 0.38f, 0.42f));
            var mGun     = CreateMaterial("gun",     new Color(0.18f, 0.19f, 0.21f), 0.8f, 0.4f);
            var mSensor  = CreateMaterial("sensor",  new Color(0.25f, 0.55f, 0.80f));
            var mMissile = CreateMaterial("missile", new Color(0.90f, 0.85f, 0.75f));
            var mDanger  = CreateMaterial("danger",  new Color(0.85f, 0.25f, 0.20f));
            var mSupport = CreateMaterial("support", new Color(0.75f, 0.70f, 0.30f));
            var mEnemy   = CreateMaterial("enemy",   new Color(0.70f, 0.25f, 0.25f));
            var mGhost   = CreateMaterial("ghost",   new Color(0.4f, 1f, 0.6f, 0.5f));
            CreateTransparentMaterial("fire_arc", Color.white);   // 정비 화면 사격각 부채꼴. 색은 코드가 입힌다
            var mRepair  = CreateMaterial("repair",  new Color(0.30f, 0.70f, 0.45f));

            // 함내 모듈은 정비 화면에서 나란히 보이므로 서로 확실히 달라야 한다
            var mMagazine  = CreateMaterial("mod_magazine",  new Color(0.80f, 0.20f, 0.20f));  // 빨강
            var mSonar     = CreateMaterial("mod_sonar",     new Color(0.25f, 0.55f, 0.90f));  // 파랑

            // FBX 임포트 설정과 머티리얼 매핑을 먼저 끝내야 아래 아트 프리팹 생성에서
            // 최신 모델 계층(TurretPivot/ElevationPivot/Muzzle 등)을 읽을 수 있다.
            PrepareArtMaterials();

            // ---------------------------------------------------------- 이펙트 (발사체·미사일이 참조하므로 먼저)
            r.FxGunFlash    = BuildGunFlash(vfxDir);
            r.FxMuzzleSmoke = BuildMuzzleSmoke(vfxDir);
            r.FxJamPulse    = BuildRingPulse("FX_JamPulse", new Color(0.45f, 0.9f, 1f, 0.55f), vfxDir);
            r.FxRepairPulse = BuildRingPulse("FX_RepairPulse", new Color(0.45f, 1f, 0.55f, 0.55f), vfxDir);
            r.FxSmokeScreen = BuildSmokeScreen(vfxDir);

            // ---------------------------------------------------------- 발사체
            // 기관포·CIWS: 예광탄 꼬리 + 작은 명중 불꽃(포구 화염 이펙트를 작게)
            var hitSpark = r.FxGunFlash;
            r.ProjAutocannon = BuildProjectile("PRJ_Autocannon", mMissile, 1 << LayerEnemy, fxDir, null, so =>
            {
                Set(so, "impactEffect", hitSpark);
                Set(so, "impactEffectScale", 0.3f);
                Set(so, "tracerTime", 0.08f);
                Set(so, "tracerColor", new Color(1f, 0.85f, 0.35f, 0.9f));
                Set(so, "tracerWidth", 0.12f);
            });
            // CIWS는 미사일과 항공기(적 레이어의 드론·정찰기)를 모두 맞힌다
            r.ProjCiws       = BuildProjectile("PRJ_Ciws", mDanger, (1 << LayerEnemyMissile) | (1 << LayerEnemy), fxDir, null, so =>
            {
                Set(so, "impactEffect", hitSpark);
                Set(so, "impactEffectScale", 0.22f);
                Set(so, "tracerTime", 0.06f);
                Set(so, "tracerColor", new Color(1f, 0.95f, 0.8f, 0.85f));
                Set(so, "tracerWidth", 0.08f);
            });

            // 적 포탄은 눈에 띄어야 피할 수 있다. 크고 스스로 빛나는 주황 예광탄.
            var mTracer = CreateMaterial("tracer_enemy", new Color(1f, 0.55f, 0.12f));
            if (mTracer.HasProperty("_EmissionColor"))
            {
                mTracer.EnableKeyword("_EMISSION");
                mTracer.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.08f) * 3f);
                EditorUtility.SetDirty(mTracer);
            }
            r.ProjEnemyGun = BuildProjectile("PRJ_EnemyGun", mTracer, 1 << LayerPlayerShip, fxDir,
                                             new Vector3(0.22f, 0.22f, 1.1f));

            // 미사일은 동체·탄두·띠·날개로 된 모델과 추진 연기(꼬리 + 연기 뭉치)를 갖는다.
            var mFlame = CreateMaterial("flame", new Color(1f, 0.6f, 0.2f));
            if (mFlame.HasProperty("_EmissionColor"))
            {
                mFlame.EnableKeyword("_EMISSION");
                mFlame.SetColor("_EmissionColor", new Color(1f, 0.5f, 0.1f) * 4f);
                EditorUtility.SetDirty(mFlame);
            }
            var blast = r.FxGunFlash;

            // VLS와 함대공 요격미사일은 한눈에 구별돼야 한다.
            //   VLS 대함: 굵고 긴 짙은 회색 동체 + 노란 띠 + 큰 꼬리 날개, 굵고 오래 남는 회색 연기, 낮은 발사음
            //   함대공:   가늘고 흰 동체 + 청색 탄두·가운데 날개, 가늘고 짧은 흰 꼬리선, 높은 발사음
            var mVlsBody = CreateMaterial("missile_vls_body", new Color(0.36f, 0.40f, 0.44f), 0.3f, 0.35f);
            var mVlsBand = CreateMaterial("missile_vls_band", new Color(0.95f, 0.75f, 0.10f));
            var mSamBody = CreateMaterial("missile_sam_body", new Color(0.96f, 0.97f, 0.98f), 0f, 0.5f);
            var mSamNose = CreateMaterial("missile_sam_nose", new Color(0.10f, 0.55f, 0.95f));
            var mSamFlame = CreateMaterial("flame_sam", new Color(0.75f, 0.9f, 1f));
            if (mSamFlame.HasProperty("_EmissionColor"))
            {
                mSamFlame.EnableKeyword("_EMISSION");
                mSamFlame.SetColor("_EmissionColor", new Color(0.6f, 0.85f, 1f) * 4f);
                EditorUtility.SetDirty(mSamFlame);
            }

            // VLS 대함미사일: 팝업 궤적
            r.MissilePlayer = BuildVlsMissile(blast, fxDir);

            // 적 대함미사일: 붉은 동체, 팝업 궤적
            r.MissileEnemy = BuildMissileProjectile("MIS_EnemyAsm", LayerEnemyMissile, fxDir,
                new MissileLook(1.6f, 0.28f, mDanger, mGun, mSupport, mFlame), blast, so =>
                {
                    Set(so, "profile", (int)MissileProfile.PopUp);
                    Set(so, "speed", 11f);                 // 화면 진입 후 함선까지 약 4~6초. 함선 최고속력(10)보다는 빠르게
                    Set(so, "turnRateDegPerSec", 90f);
                    Set(so, "lifeTime", 14f);
                    Set(so, "hitRadius", 1.4f);
                    Set(so, "damage", 20f);
                    Set(so, "maxHp", 3f);
                    Set(so, "isThreat", true);
                });

            // 유도로켓: 작고 붉은 탄두, 솟았다가 표적 하나에 곧게 꽂힌다
            r.RocketPlayer = BuildMissileProjectile("MIS_PlayerRocket", LayerPlayerMissile, fxDir,
                new MissileLook(1.0f, 0.16f, mMissile, mDanger, mGun, mFlame), blast, so =>
                {
                    Set(so, "profile", (int)MissileProfile.ClimbDive);
                    Set(so, "speed", 40f);
                    Set(so, "turnRateDegPerSec", 260f);
                    Set(so, "climbAltitude", 6f);
                    Set(so, "diveTurnRateDegPerSec", 720f);
                    Set(so, "lifeTime", 5f);
                    Set(so, "hitRadius", 1.1f);
                    Set(so, "damage", 14f);
                    Set(so, "maxHp", 1f);
                    Set(so, "isThreat", false);
                });

            // 함대공 요격미사일: 가늘고 빠르며 곧장 쫓는다
            r.MissileSam = BuildMissileProjectile("MIS_PlayerSam", LayerPlayerMissile, fxDir,
                new MissileLook(1.3f, 0.15f, mSamBody, mSamNose, mSamNose, mSamFlame,
                                trailTime: 0.45f, trailEndWidth: 2.5f, smokePerMeter: 1.5f,
                                trailColor: new Color(0.85f, 0.93f, 1f), midFins: true), blast, so =>
                {
                    Set(so, "launchPitch", 1.5f);
                    Set(so, "profile", (int)MissileProfile.Direct);
                    Set(so, "speed", 48f);
                    Set(so, "turnRateDegPerSec", 420f);
                    Set(so, "lifeTime", 4f);
                    Set(so, "hitRadius", 1.3f);
                    Set(so, "damage", 10f);
                    Set(so, "maxHp", 1f);
                    Set(so, "isThreat", false);
                });

            r.Decoy      = BuildDecoy(mSupport, fxDir);
            var waterBlast = BuildWaterBlast(vfxDir);

            // 76mm 고폭탄: 굵고 밝은 탄, 명중 시 파편 피해와 화염
            var gunFlash = r.FxGunFlash;
            r.ProjGun76 = BuildProjectile("PRJ_Gun76", mMissile, 1 << LayerEnemy, fxDir,
                                          new Vector3(0.2f, 0.2f, 0.9f), so =>
                                          {
                                              Set(so, "lifeTime", 1.5f);
                                              Set(so, "hitRadius", 0.45f);
                                              Set(so, "splashRadius", 2.5f);
                                              Set(so, "splashDamageRatio", 0.5f);
                                              Set(so, "impactEffect", gunFlash);
                                              Set(so, "waterEffect", waterBlast);      // 빗나간 탄 물기둥
                                              Set(so, "waterEffectScale", 0.35f);
                                          });
            r.Torpedo    = BuildTorpedo(mGun, waterBlast, fxDir);
            r.DepthCharge = BuildDepthCharge(mGun, waterBlast, fxDir);
            r.AswTorpedo = Get($"{fxDir}/TOR_PlayerAsw.prefab");
            r.Helicopter = BuildArtHelicopter(r.RocketPlayer, r.DepthCharge, fxDir)
                           ?? BuildHelicopter(mSensor, r.RocketPlayer, r.DepthCharge, fxDir);
            r.CellHighlight = BuildCellHighlight(mGhost, vfxDir);

            // 함체는 점유된 칸을 따라 코드가 조립한다. 그 재료가 되는 조각들.
            var mDeck = CreateMaterial("hull_deck", new Color(0.30f, 0.33f, 0.36f), 0.05f, 0.3f);
            var mSide = CreateMaterial("hull_side", new Color(0.20f, 0.23f, 0.26f), 0.05f, 0.3f);

            r.DeckPlate = SavePrefab(
                Primitive("HULL_DeckPlate", PrimitiveType.Cube,
                          new Vector3(Cell * 0.98f, 0.18f, Cell * 0.98f), mDeck), vfxDir);

            var skirt = Primitive("HULL_SideSkirt", PrimitiveType.Cube,
                                  new Vector3(Cell * 0.98f, 1.55f, 0.16f), mSide);
            // 윗면은 갑판판 윗면(+0.09)에 맞추고, 흔들림(±0.12)이 있어도 바다 평면(-0.9) 밑에 남도록 깊게 내린다
            skirt.transform.localPosition = new Vector3(0f, -0.685f, 0f);
            r.SideSkirt = SavePrefab(WrapPivot(skirt, "HULL_SideSkirt"), vfxDir);

            // 뱃머리는 ShipHullBuilder가 배 앞 윤곽을 따라 메시로 만든다. 여기서는 갑판 소품만(선택).
            r.BowFittings = BuildArtProp("HULL_BowFittings", "HULL_BowFittings", vfxDir);
            r.BowAnchor   = BuildArtProp("HULL_Anchor", "HULL_Anchor", vfxDir) ?? BuildAnchorGreybox(mGun, mSide, vfxDir);
            r.MountPedestal = BuildArtProp("HULL_MountPedestal", "HULL_MountPedestal", vfxDir)
                              ?? BuildPedestalGreybox(mHull, mSide, vfxDir);

            // ------------------------------------------------------------ 적
            var enemyGun = r.ProjEnemyGun;
            var torpedo = r.Torpedo;
            r.FastBoat = BuildArtFastBoat(enemyGun, eneDir)
                         ?? BuildEnemy<FastAttackBoat>("ENE_FastBoat", mEnemy,
                            new Vector3(1.2f, 0.6f, 3f), eneDir,
                            (go, comp) => Configure(comp, so => Set(so, "projectilePrefab", enemyGun)));

            r.MissileBoat = BuildArtEnemy<MissileBoat>("ENE_MissileBoat", "EnemyMissileBoat",
                            1.0f, 0f, new Vector3(3.5f, 2.0f, 8.0f), eneDir, (go, comp) =>
                            {
                                var launcher = BuildCanisterLaunchPoint(go.transform)
                                               ?? FindDeep(go.transform, "MissileLauncherPivot");
                                if (launcher == null)
                                {
                                    // 모델에 발사대가 없으면 갑판 위에서 쏜다. 수면에서 쏘면 물속을 난다.
                                    launcher = new GameObject("LaunchPoint").transform;
                                    launcher.SetParent(go.transform, false);
                                    launcher.localPosition = new Vector3(0f, 1.6f, 0f);
                                }
                                Configure(comp, so =>
                                {
                                    Set(so, "launchPoint", launcher);
                                    Set(so, "launcherPivot", FindDeep(go.transform, "MissileLauncherPivot"));
                                });
                            })
                         ?? BuildEnemy<MissileBoat>("ENE_MissileBoat", mEnemy,
                            new Vector3(1.8f, 0.9f, 4.5f), eneDir, null);

            r.Submarine = BuildArtSubmarine(torpedo, eneDir)
                         ?? BuildEnemy<Submarine>("ENE_Submarine", mEnemy,
                            new Vector3(1.6f, 1.2f, 5f), eneDir,
                            (go, comp) => Configure(comp, so => Set(so, "torpedoPrefab", torpedo)));

            // 1스테이지 보스: 연안 경비정. 좌/우현 4연장 경사식 발사대와 독립 함수/함미 포탑.
            r.Boss = BuildCoastalBoss(enemyGun, eneDir)
                         ?? BuildEnemy<BossShip>("ENE_Boss", mEnemy,
                            new Vector3(4f, 2.5f, 12f), eneDir, null);

            // 항공 위협. 아트 모델(EnemyDrone / EnemyRecon)이 있으면 그것을, 없으면 그레이박스.
            var droneBlast = r.FxGunFlash;
            r.Drone = BuildArtEnemy<KamikazeDrone>("ENE_Drone", "EnemyDrone", 1f, 0f, new Vector3(2.2f, 0.7f, 1.8f), eneDir,
                          (go, comp) =>
                          {
                              var prop = FindDeep(go.transform, "TailPropeller");
                              Configure(comp, so =>
                              {
                                  Set(so, "blastEffect", droneBlast);
                                  Set(so, "cruiseAltitude", 6f);
                                  if (prop != null) Set(so, "propeller", prop);
                              });
                          })
                      ?? BuildDroneGreybox(mEnemy, mDanger, mGun, droneBlast, eneDir);

            r.Recon = BuildArtRecon(eneDir)
                      ?? BuildReconGreybox(mEnemy, mSensor, mGun, eneDir);

            // 전투공격기: 접근 → 저공 기총 소사 → 급상승 이탈을 반복. 탄은 적 예광탄을 같이 쓴다.
            r.Fighter = BuildArtFighter(enemyGun, eneDir)
                        ?? BuildFighterGreybox(mEnemy, mGun, mSensor, enemyGun, eneDir);

            // 스테이지 2 보스: 항공전함. 주포탄(착탄 경고 원) · 대공포 예광탄을 먼저 만든다.
            var impactWarning = BuildImpactWarning(vfxDir);
            var bossShell = BuildBossShell(mGun, impactWarning, waterBlast, fxDir);
            var aaTracer = BuildProjectile("PRJ_EnemyAA", mTracer, 1 << LayerPlayerMissile, fxDir,
                                           new Vector3(0.12f, 0.12f, 0.7f), so => Set(so, "lifeTime", 0.8f));
            var bossFlash = r.FxGunFlash;

            r.Boss2 = BuildArtBoss2(bossShell, bossFlash, aaTracer, eneDir)
                      ?? BuildHybridBossGreybox(mEnemy, mGun, mSupport, bossShell, bossFlash, aaTracer, eneDir);

            BuildStage2Enemies(r, eneDir);
            r.ModernCorvette = BuildModernCorvette(r, eneDir);
            BuildStage3Enemies(r, eneDir);
            BuildSuicideBoat(r, eneDir);
            BuildNormalEnemies(r, eneDir);

            // --------------------------------------------------------- 모듈
            r.Bridge = BuildBridgeGreybox(mHull, mGun, modDir);




            r.Magazine = BuildSimpleModule<MagazineModule>("MOD_Magazine", mMagazine,
                        new Vector3(Cell * 0.8f, 1f, Cell * 0.8f), 0.5f, modDir);

            r.Sonar = BuildSimpleModule<SonarModule>("MOD_Sonar", mSonar,
                        new Vector3(Cell * 0.7f, 0.5f, 2 * Cell * 0.7f), 0.45f, modDir,
                        PrimitiveType.Capsule);

            r.RepairBay = BuildSimpleModule<RepairBayModule>("MOD_RepairBay", mRepair,
                        new Vector3(Cell * 0.75f, 0.75f, Cell * 0.75f), 0.4f, modDir,
                        PrimitiveType.Sphere);

            r.HeliDeck = BuildHeliDeck(mHull, mSensor, r.Helicopter, modDir);
            r.Ciws       = BuildTurretModule<CiwsModule>("MOD_Ciws", mGun, 0.4f, 1.1f,
                                                         r.ProjCiws, modDir);

            // 아트 모델이 있으면 그레이박스 대신 그것을 쓴다.
            // 모델이 없으면 기존 도형 버전으로 조용히 되돌아간다.
            r.Bridge     = BuildArtBridge(mHull, mGun, modDir) ?? r.Bridge;
            WireBridgeDecoy(r.Bridge, r.Decoy);
            var cookOffFx = r.FxGunFlash;
            r.Magazine   = BuildUpgradeableArtModule<MagazineModule>("MOD_Magazine", "MOD_Magazine", modDir,
                               (root, model) => Configure(root.GetComponent<MagazineModule>(), so => Set(so, "explosionVfx", cookOffFx)))
                           ?? r.Magazine;
            r.Sonar      = BuildArtModule<SonarModule>("MOD_Sonar", "MOD_Sonar", modDir, null)
                           ?? r.Sonar;
            r.RepairBay  = BuildArtModule<RepairBayModule>("MOD_RepairBay", "MOD_RepairBay", modDir, null)
                           ?? r.RepairBay;
            r.Radar      = BuildArtRadar(modDir) ?? BuildRadar(mHull, mSensor, modDir);
            r.Autocannon = BuildArtAutocannon(r.ProjAutocannon, modDir)
                           ?? BuildTurretModule<AutocannonModule>("MOD_Autocannon", mGun, 0.55f, 1.8f,
                                                                  r.ProjAutocannon, modDir);
            r.Ciws       = BuildArtCiws(r.ProjCiws, modDir) ?? r.Ciws;
            r.Vls        = BuildArtVls(r.MissilePlayer, r.MissileSam, r.AswTorpedo, modDir)
                           ?? BuildVls(mHull, mGun, r.MissilePlayer, r.MissileSam, r.AswTorpedo, modDir);
            r.RocketLauncher = BuildArtRocketLauncher(r.RocketPlayer, modDir)
                               ?? BuildRocketLauncher(mHull, mGun, r.RocketPlayer, modDir);
            r.Gun76 = BuildArtGun76(r.ProjGun76, r.FxGunFlash, modDir)
                      ?? BuildGun76Greybox(mHull, mGun, r.ProjGun76, r.FxGunFlash, modDir);
            r.AswLauncher = BuildArtAswLauncher(r.DepthCharge, modDir)
                            ?? BuildAswLauncher(mHull, mGun, r.DepthCharge, modDir);
            r.DecoyLauncher = BuildArtDecoyLauncher(r.Decoy, modDir)
                              ?? BuildDecoyLauncher(mSupport, r.Decoy, modDir);
            r.HeliDeck   = BuildArtHeliDeck(r.Helicopter, modDir) ?? r.HeliDeck;

            r.SamLauncher = BuildArtSamLauncher(r.MissileSam, modDir) ?? BuildSamLauncherGreybox(mHull, mGun, r.MissileSam, modDir);
            r.EwSuite     = BuildArtEwSuite(r.FxJamPulse, modDir) ?? BuildEwSuiteGreybox(mHull, mSensor, r.FxJamPulse, modDir);

            // 포연: 기관포·CIWS 프리팹(아트든 그레이박스든)에 연기 이펙트를 물린다
            var smoke = r.FxMuzzleSmoke;
            PatchPrefab(r.Autocannon, go => Configure(go.GetComponent<AutocannonModule>(), so => Set(so, "muzzleSmoke", smoke)));
            PatchPrefab(r.Ciws, go => Configure(go.GetComponent<CiwsModule>(), so => Set(so, "muzzleSmoke", smoke)));

            // 응급 수리 스킬의 초록 고리
            var repairPulse = r.FxRepairPulse;
            PatchPrefab(r.RepairBay, go => Configure(go.GetComponent<RepairBayModule>(), so => Set(so, "burstEffect", repairPulse)));

            AssetDatabase.SaveAssets();
            return r;
        }

        // ------------------------------------------------------- 아트 모델 모듈

        private const string ModelDir = Root + "/Art/Models";

        // ------------------------------------------------------- 적 아트 모델(미니어처 적 v9, 2026-10-03)
        // 미니어처 적 v9(Codex MiniatureEnemyFleet_v9): 고속정·자폭 보트·연안 경비정·잠수함·순항미사일 잠수함·항공전함·전투기·정찰기.
        // 게임 안 크기는 예전과 비슷하게 맞췄다(모델 크기가 달라져 배율을 다시 잡음) — 아래 각 메서드 주석.

        /// <summary>미니어처 적 v9 모델 8종.</summary>
        public static readonly string[] EnemyFleetV9Models =
        {
            "EnemyFastBoat", "EnemySuicideBoat", "PatrolBoat_CoastalBoss", "EnemySubmarine",
            "EnemyCruiseSubmarine", "EnemyHybridBoss", "EnemyFighter", "EnemyRecon",
        };

        /// <summary>
        /// 미니어처 적 v9 여덟 종의 프리팹만 다시 만든다(같은 경로라 GUID·데이터 연결 유지). 다른 적·모듈·데이터·씬은 건드리지 않는다.
        /// 메뉴 Naval/Art/Apply Enemy Fleet v9 Models.
        /// </summary>
        public static Result ApplyEnemyFleetV9Only()
        {
            PrepareArtMaterials(EnemyFleetV9Models);
            var r = Load();
            string eneDir = $"{Root}/Prefabs/Enemies";
            string fxDir = $"{Root}/Prefabs/Projectiles";
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>($"{fxDir}/PRJ_BossShell.prefab");
            var aaTracer = AssetDatabase.LoadAssetAtPath<GameObject>($"{fxDir}/PRJ_EnemyAA.prefab");

            r.FastBoat = BuildArtFastBoat(r.ProjEnemyGun, eneDir) ?? r.FastBoat;
            BuildSuicideBoat(r, eneDir);
            r.Boss = BuildCoastalBoss(r.ProjEnemyGun, eneDir) ?? r.Boss;
            r.Submarine = BuildArtSubmarine(r.Torpedo, eneDir) ?? r.Submarine;
            r.CruiseSubmarine = BuildArtCruiseSubmarine(eneDir) ?? r.CruiseSubmarine;
            if (shell != null && aaTracer != null) r.Boss2 = BuildArtBoss2(shell, r.FxGunFlash, aaTracer, eneDir) ?? r.Boss2;
            else Debug.LogWarning("[Enemy Fleet v9] PRJ_BossShell / PRJ_EnemyAA가 없어 항공전함은 건너뜁니다.");
            r.Fighter = BuildArtFighter(r.ProjEnemyGun, eneDir) ?? r.Fighter;
            r.Recon = BuildArtRecon(eneDir) ?? r.Recon;
            AssetDatabase.SaveAssets();
            return r;
        }

        /// <summary>고속정: v9 모델 5.2m × 1.0(예전 8m 모델 × 0.62 ≈ 5m). 선수 포탑으로 조준·사격.</summary>
        private static GameObject BuildArtFastBoat(GameObject enemyGun, string eneDir)
        {
            return BuildArtEnemy<FastAttackBoat>("ENE_FastBoat", "EnemyFastBoat",
                1.0f, 0f, new Vector3(1.8f, 1.5f, 5.2f), eneDir, (go, comp) =>
                {
                    var turret = FindDeep(go.transform, "PrimaryGunPivot");
                    var elevation = FindDeep(go.transform, "GunElevationPivot");
                    var muzzle = FindDeep(go.transform, "Muzzle");
                    if (turret == null || muzzle == null)
                        Debug.LogError("[Setup] 고속정 모델에서 PrimaryGunPivot/Muzzle을 찾지 못했습니다.");

                    Configure(comp, so =>
                    {
                        Set(so, "weapon.turret", turret);
                        Set(so, "weapon.elevationPivot", elevation);
                        Set(so, "weapon.muzzle", muzzle);
                        Set(so, "weapon.aimTolerance", 10f);
                        Set(so, "projectilePrefab", enemyGun);
                    });
                });
        }

        /// <summary>어뢰 잠수함: v9 모델 8m × 1.0(예전 9m 모델 × 0.9 ≈ 8m). 잠항/부상은 ModelPivot을 위아래로, 스크루 회전.</summary>
        private static GameObject BuildArtSubmarine(GameObject torpedo, string eneDir)
        {
            return BuildArtEnemy<Submarine>("ENE_Submarine", "EnemySubmarine",
                1.0f, -0.15f, new Vector3(2.0f, 1.4f, 8.0f), eneDir, (go, comp) =>
                {
                    var pivot = go.transform.Find("ModelPivot");
                    // 예전 모델의 선체 위 회색 평판(수상함 갑판처럼 보임) — v9 모델에는 없다
                    var deckPanel = FindDeep(go.transform, "FlatUpperDeck");
                    if (deckPanel != null) deckPanel.gameObject.SetActive(false);

                    Configure(comp, so =>
                    {
                        Set(so, "modelPivot", pivot);
                        Set(so, "torpedoPrefab", torpedo);
                        Set(so, "propeller", FindDeep(go.transform, "Propeller"));
                    });
                });
        }

        /// <summary>순항미사일 잠수함: v9 모델 11m × 0.85 ≈ 9.4m(예전과 같음). 미사일 해치 6개·발사점 6개.</summary>
        private static GameObject BuildArtCruiseSubmarine(string eneDir)
        {
            return BuildArtEnemy<CruiseMissileSubmarine>("ENE_CruiseSubmarine", "EnemyCruiseSubmarine",
                0.85f, 0f, new Vector3(2.0f, 1.4f, 9.4f), eneDir, (go, comp) =>
                {
                    var pivot = go.transform.Find("ModelPivot");

                    var points = new List<Transform>();
                    for (int i = 1; i <= 12; i++)
                    {
                        var t = FindDeep(go.transform, $"MissileLaunchPoint_{i:00}");
                        if (t != null) points.Add(t);
                    }
                    if (points.Count == 0) Debug.LogWarning("[Setup] 순항미사일 잠수함 모델에 MissileLaunchPoint_01이 없습니다. 선체 중심에서 쏩니다.");

                    var periscope = new List<Transform>();
                    foreach (var n in new[] { "PeriscopeStem", "PeriscopeHead" })
                    {
                        var t = FindDeep(go.transform, n);
                        if (t != null) periscope.Add(t);
                    }

                    var hatches = new List<GameObject>();
                    CollectByPrefix(go.transform, "MissileCap", hatches);

                    Configure(comp, so =>
                    {
                        Set(so, "modelPivot", pivot);
                        // 선체가 어뢰 잠수함보다 낮게 붙어 있어 같은 깊이로 보이도록 조금 올린다
                        Set(so, "submergedDepth", -0.85f);
                        Set(so, "surfacedDepth", 0.2f);
                        SetArray(so, "launchPoints", points.ToArray());
                        SetArray(so, "periscopeParts", periscope.ToArray());
                        SetArray(so, "hatchCovers", hatches.ToArray());
                        Set(so, "periscopeTravel", 0.75f);
                        Set(so, "propeller", FindDeep(go.transform, "Propeller"));
                    });
                });
        }

        /// <summary>
        /// 항공전함(스테이지 3 보스, 2026-10-05부터 현대화 이세급 v10 모델): 16m × 2.0 = 32m. 2연장 주포 4기(포구 8, 중앙 2기는 쉬는 자세가 옆을 봄),
        /// 미사일 셀 8, 양현 근접방어포, 넓은 선미 항공갑판(폭 약 9.5m). 주포 선회는 HybridBattleshipBoss가 포탑마다 쉬는 방향 기준으로.
        /// </summary>
        private static GameObject BuildArtBoss2(GameObject bossShell, GameObject bossFlash, GameObject aaTracer, string eneDir)
        {
            return BuildArtEnemy<HybridBattleshipBoss>("ENE_Boss2", "EnemyHybridBoss", 2f, 0f, new Vector3(8.6f, 8f, 32.5f), eneDir,
                (go, comp) =>
                {
                    var box = go.GetComponent<BoxCollider>();
                    if (box != null) box.center = new Vector3(0f, 1.8f, 0f);
                    WireHybridBoss(go.transform, comp, bossShell, bossFlash, aaTracer);
                    Configure(comp, so => Set(so, "cutoutZoneRadius", 36f));
                });
        }

        /// <summary>전투기: v9 모델(폭 2.8m · 동체 3.4m) × 1.0. 기총 GunMuzzle · 배기 Exhaust 비행운.</summary>
        private static GameObject BuildArtFighter(GameObject enemyGun, string eneDir)
        {
            return BuildArtEnemy<FighterJet>("ENE_Fighter", "EnemyFighter", 1f, 0f, new Vector3(2.8f, 0.9f, 3.4f), eneDir,
                (go, comp) =>
                {
                    var muzzle = FindDeep(go.transform, "GunMuzzle");
                    var exhaust = FindDeep(go.transform, "Exhaust");
                    AddJetTrail(go.transform, exhaust != null
                        ? go.transform.InverseTransformPoint(exhaust.position)
                        : new Vector3(0f, 0f, -1.7f));
                    Configure(comp, so =>
                    {
                        Set(so, "gunMuzzle", muzzle);
                        Set(so, "projectilePrefab", enemyGun);
                        Set(so, "cruiseAltitude", 12f);
                    });
                });
        }

        /// <summary>정찰기: v9 모델(폭 5m) × 1.0. 원반 레이더(RadomePivot)와 쌍발 프로펠러(Propeller_Port/Starboard) 회전.</summary>
        private static GameObject BuildArtRecon(string eneDir)
        {
            return BuildArtEnemy<ReconAircraft>("ENE_Recon", "EnemyRecon", 1f, 0f, new Vector3(5.2f, 1.2f, 3.6f), eneDir,
                (go, comp) =>
                {
                    var radome = FindDeep(go.transform, "RadomePivot");
                    var props = new List<Transform>();
                    foreach (var n in new[] { "Propeller_Port", "Propeller_Starboard" })
                    {
                        var t = FindDeep(go.transform, n);
                        if (t != null) props.Add(t);
                    }
                    Configure(comp, so =>
                    {
                        if (radome != null) Set(so, "radome", radome);
                        SetArray(so, "propellers", props.ToArray());
                        Set(so, "cruiseAltitude", 11f);
                    });
                });
        }

        /// <summary>다른 적·모듈·씬을 건드리지 않고 1스테이지 보스 프리팹만 갱신한다.</summary>
        public static GameObject BuildStage1BossOnly()
        {
            PrepareArtMaterials("PatrolBoat_CoastalBoss");
            var gun = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Projectiles/PRJ_EnemyGun.prefab");
            var boss = BuildCoastalBoss(gun, $"{Root}/Prefabs/Enemies");
            AssetDatabase.SaveAssets();
            return boss;
        }

        private static GameObject BuildCoastalBoss(GameObject enemyGun, string dir)
        {
            // 미니어처 적 v9(2026-10-03): 17m 모델 × 1.0 = 예전과 같은 17m(예전 20m 모델 × 0.85). 폭 4.3m
            return BuildArtEnemy<BossShip>("ENE_Boss", "PatrolBoat_CoastalBoss",
                1.0f, 0f, new Vector3(4.6f, 3f, 17f), dir, (go, comp) =>
                {
                    var points = new List<Transform>(8);
                    for (int i = 1; i <= 8; i++)
                    {
                        var t = FindDeep(go.transform, $"MissileLaunchPoint_{i:00}");
                        if (t != null) points.Add(t);
                    }
                    if (points.Count != 8)
                        Debug.LogError($"[Setup] 연안 경비정 미사일 소켓 8개 필요, 현재 {points.Count}개.");
                    Configure(comp, so =>
                    {
                        SetArray(so, "launchPoints", points.ToArray());
                        Set(so, "salvoCount", 4);
                        Set(so, "gunProjectilePrefab", enemyGun);
                        Set(so, "gunFlashMaterialSource", AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/VFX/FX_GunFlash.prefab"));
                    });
                });
        }

        /// <summary>
        /// 아트 모델 방향 보정. 모든 모델은 선수를 Blender +Y로 만들고 -Z Forward로 내보내는데,
        /// 이러면 Unity에서 선수가 -Z에 온다. 임포트된 루트는 건드릴 수 없으므로 부모 피벗을 돌린다.
        /// </summary>
        private static readonly Quaternion ArtForwardFix = Quaternion.Euler(0f, 180f, 0f);

        /// <summary>임포트 모델을 방향 보정 피벗 아래에 붙인다. 모델 트랜스폼 자체는 그대로 둔다.</summary>
        private static Transform AttachArtModel(GameObject modelAsset, Transform parent,
                                                string pivotName = "ModelPivot", string instanceName = "Model")
        {
            var pivot = new GameObject(pivotName).transform;
            pivot.SetParent(parent, false);
            pivot.localRotation = ArtForwardFix;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, parent.gameObject.scene);
            instance.name = instanceName;
            instance.transform.SetParent(pivot, false);
            return instance.transform;
        }

        /// <summary>
        /// 다른 적·씬·데이터를 재생성하지 않고 기관포/76mm/CIWS/탄약고의 3단계 외형만 적용한다.
        /// 기존 런타임 컴포넌트와 전투 수치/VFX 참조는 보존한다.
        /// </summary>
        [MenuItem("Naval/Art/Apply Module Upgrade Models")]
        public static void ApplyModuleUpgradeModelsOnly()
        {
            PrepareArtMaterials(
                "MOD_Autocannon", "MOD_Autocannon_U1", "MOD_Autocannon_U2",
                "MOD_Gun76", "MOD_Gun76_U1", "MOD_Gun76_U2",
                "MOD_CIWS", "MOD_CIWS_U1", "MOD_CIWS_U2",
                "MOD_Magazine", "MOD_Magazine_U1", "MOD_Magazine_U2");

            PatchUpgradeablePrefab<AutocannonModule>("MOD_Autocannon", "MOD_Autocannon", (module, model) =>
            {
                BindWeaponModel(module, model);
            });
            PatchUpgradeablePrefab<NavalGunModule>("MOD_Gun76", "MOD_Gun76", (module, model) =>
            {
                BindWeaponModel(module, model);
            });
            PatchUpgradeablePrefab<CiwsModule>("MOD_Ciws", "MOD_CIWS", (module, model) =>
            {
                var turret = FindDeep(model, "TurretPivot");
                var elevation = FindDeep(model, "ElevationPivot");
                var muzzle = FindDeep(model, "Muzzle");
                var barrels = FindDeep(model, "BarrelCluster");
                Configure(module, so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "barrelCluster", barrels);
                });
            });
            PatchUpgradeablePrefab<MagazineModule>("MOD_Magazine", "MOD_Magazine", null);

            AssetDatabase.SaveAssets();
            DeleteUpgradeMaterialDuplicates();
            AssetDatabase.Refresh();
            Debug.Log("[Module Upgrades] 기관포/76mm/CIWS/탄약고의 기본·U1·U2 외형 적용 완료.");
        }

        /// <summary>
        /// 함대공 미사일·전자전 장비·레이더·기만체 발사기·손상 통제반 5종의 아트 모델만 다시 붙인다(다른 프리팹·씬·데이터는 안 건드림).
        /// Codex NavalGreyPack v7 적용 — FBX를 Art/Models에 넣은 뒤 실행. 프리팹은 같은 경로에 다시 저장되어 GUID·데이터 참조가 유지된다.
        /// 소켓: SAM TrainPivot·LaunchPoint_01~04, EW AntennaPivot, 레이더 RadarPivot, 기만체 LaunchPoint(전체 빌드와 같은 규약).
        /// </summary>
        [MenuItem("Naval/Art/Apply Support Module Models (SAM · EW · Radar · Decoy · Repair)")]
        public static void ApplySupportModuleModelsOnly()
        {
            PrepareArtMaterials("MOD_SamLauncher", "MOD_EwSuite", "MOD_Radar", "MOD_DecoyLauncher", "MOD_RepairBay");
            var r = Load();
            string modDir = $"{Root}/Prefabs/Modules";
            if (BuildArtSamLauncher(r.MissileSam, modDir) == null) Debug.LogError("[Support Modules] MOD_SamLauncher.fbx 없음");
            if (BuildArtEwSuite(r.FxJamPulse, modDir) == null) Debug.LogError("[Support Modules] MOD_EwSuite.fbx 없음");
            if (BuildArtRadar(modDir) == null) Debug.LogError("[Support Modules] MOD_Radar.fbx 없음");
            if (BuildArtDecoyLauncher(r.Decoy, modDir) == null) Debug.LogError("[Support Modules] MOD_DecoyLauncher.fbx 없음");
            var repair = BuildArtModule<RepairBayModule>("MOD_RepairBay", "MOD_RepairBay", modDir, null);
            if (repair == null) Debug.LogError("[Support Modules] MOD_RepairBay.fbx 없음");
            var repairPulse = r.FxRepairPulse;
            PatchPrefab(repair, go => Configure(go.GetComponent<RepairBayModule>(), so => Set(so, "burstEffect", repairPulse)));
            AssetDatabase.SaveAssets();
            Debug.Log("[Support Modules] 함대공·전자전·레이더·기만체·손상 통제반 아트 모델 적용 완료.");
        }

        /// <summary>
        /// 기관포/76mm/CIWS 3종의 기본·U1·U2 외형만 다시 붙인다(탄약고·다른 프리팹은 건드리지 않는다).
        /// Codex 무장 강화 팩(v8) 교체 때 쓴다 — FBX를 같은 이름으로 덮어쓴 뒤(메타·GUID 유지) 실행.
        /// </summary>
        [MenuItem("Naval/Art/Apply Weapon Upgrade Models (CIWS · 76mm · Autocannon)")]
        public static void ApplyWeaponUpgradeModelsOnly()
        {
            PrepareArtMaterials(
                "MOD_Autocannon", "MOD_Autocannon_U1", "MOD_Autocannon_U2",
                "MOD_Gun76", "MOD_Gun76_U1", "MOD_Gun76_U2",
                "MOD_CIWS", "MOD_CIWS_U1", "MOD_CIWS_U2");

            PatchUpgradeablePrefab<AutocannonModule>("MOD_Autocannon", "MOD_Autocannon", BindWeaponModel);
            PatchUpgradeablePrefab<NavalGunModule>("MOD_Gun76", "MOD_Gun76", BindWeaponModel);
            PatchUpgradeablePrefab<CiwsModule>("MOD_Ciws", "MOD_CIWS", (module, model) =>
            {
                Configure(module, so =>
                {
                    Set(so, "weapon.turret", FindDeep(model, "TurretPivot"));
                    Set(so, "weapon.elevationPivot", FindDeep(model, "ElevationPivot"));
                    Set(so, "weapon.muzzle", FindDeep(model, "Muzzle"));
                    Set(so, "barrelCluster", FindDeep(model, "BarrelCluster"));
                });
            });

            AssetDatabase.SaveAssets();
            DeleteUpgradeMaterialDuplicates();
            AssetDatabase.Refresh();
            Debug.Log("[Weapon Upgrades] 기관포/76mm/CIWS 기본·U1·U2 외형 적용 완료.");
        }

        private static void DeleteUpgradeMaterialDuplicates()
        {
            string matDir = $"{ModelDir}/Materials";
            foreach (var baseName in new[]
                     { "Deck Grey", "Naval Blue Grey", "Gunmetal", "Radar Glass", "Sensor Glass", "Warning Yellow" })
            {
                for (int i = 1; i <= 12; i++)
                    AssetDatabase.DeleteAsset($"{matDir}/{baseName}.{i:000}.mat");
            }
        }

        private static void BindWeaponModel(Component module, Transform model)
        {
            var turret = FindDeep(model, "TurretPivot");
            var elevation = FindDeep(model, "ElevationPivot");
            var muzzle = FindDeep(model, "Muzzle");
            Configure(module, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
            });
        }

        private static void PatchUpgradeablePrefab<T>(string prefabName, string modelName,
                                                       System.Action<T, Transform> bindBase)
            where T : Component
        {
            string path = $"{Root}/Prefabs/Modules/{prefabName}.prefab";
            var baseModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}.fbx");
            var upgrade1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}_U1.fbx");
            var upgrade2 = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}_U2.fbx");
            if (baseModel == null || upgrade1 == null || upgrade2 == null)
            {
                Debug.LogError($"[Module Upgrades] {modelName} 기본/U1/U2 FBX 중 누락된 파일이 있습니다.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var module = root.GetComponent<T>();
                if (module == null)
                {
                    Debug.LogError($"[Module Upgrades] {prefabName}에 {typeof(T).Name}이 없습니다.");
                    return;
                }

                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                var baseInstance = AttachArtModel(baseModel, root.transform, "Visual_Level1", "Model_Base");
                var u1Instance = AttachArtModel(upgrade1, root.transform, "Visual_Level2", "Model_U1");
                var u2Instance = AttachArtModel(upgrade2, root.transform, "Visual_Level3", "Model_U2");
                u1Instance.parent.gameObject.SetActive(false);
                u2Instance.parent.gameObject.SetActive(false);

                var visuals = root.GetComponent<Game.Modules.ModuleUpgradeVisuals>()
                              ?? root.AddComponent<Game.Modules.ModuleUpgradeVisuals>();
                Configure(visuals, so => SetArray(so, "levels",
                    baseInstance.parent.gameObject, u1Instance.parent.gameObject, u2Instance.parent.gameObject));
                bindBase?.Invoke(module, baseInstance);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Module Upgrades] {prefabName} 적용 완료.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 모델이 함께 가져온 URP 머티리얼을 임포터에 연결한다.
        /// 이름은 Blender에서 지정된 것과 정확히 일치해야 한다.
        /// </summary>
        private static readonly string[] ArtModels =
        {
            "MOD_Bridge", "MOD_Radar", "MOD_Autocannon", "MOD_Autocannon_U1", "MOD_Autocannon_U2",
            "MOD_CIWS", "MOD_CIWS_U1", "MOD_CIWS_U2", "MOD_VLS",
            "MOD_DecoyLauncher", "MOD_Magazine", "MOD_Magazine_U1", "MOD_Magazine_U2", "MOD_RepairBay", "MOD_Sonar",
            "MOD_HeliDeck", "MOD_RocketLauncher", "MOD_AswLauncher", "MOD_Gun76", "MOD_Gun76_U1", "MOD_Gun76_U2", "MOD_SamLauncher", "MOD_EwSuite", "HEL_Asw",
            "MIS_PlayerVls", "MIS_EnemyAsm", "MIS_PlayerRocket", "MIS_PlayerSam", "HULL_MountPedestal",
            "EnemyFastBoat", "EnemyMissileBoat", "EnemySubmarine", "PatrolBoat", "PatrolBoat_CoastalBoss", "EnemyDrone", "EnemyRecon", "EnemyFighter", "EnemyHybridBoss", "HULL_BowFittings", "HULL_Anchor",
            "EnemyCruiseSubmarine", "EnemyPccCorvette", "EnemySuicideBoat", "EnemyModernCorvette",
            "EnemyUnmannedAttackCraft", "EnemyEwCorvette", "EnemyAirDefenseFrigate", "EnemyAttackSubmarine",
            "EnemyArmoredAssaultBoat", "EnemyTorpedoBoat", "EnemyArtilleryBoat", "EnemyRepairBoat", "EnemyMineLayer",
        };

        /// <param name="only">이 모델만 임포트 설정을 맞춘다. 비우면 전부.</param>
        private static void PrepareArtMaterials(params string[] only)
        {
            string matDir = $"{ModelDir}/Materials";
            var palette = new (string, Material)[]
            {
                ("Deck Grey", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Deck Grey.mat")),
                ("Naval Blue Grey", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Naval Blue Grey.mat")),
                ("Gunmetal", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Gunmetal.mat")),
                ("Radar Glass", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Radar Glass.mat")),
                ("Sensor Glass", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Sensor Glass.mat")),
                ("Warning Yellow", AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Warning Yellow.mat")),
                ("Emergency Red", CreateMaterial("art_emergency_red", new Color(0.72f, 0.035f, 0.018f))),
                ("Medical White", CreateMaterial("art_medical_white", new Color(0.82f, 0.86f, 0.84f))),
                // NavalGreyPack v7: 함대공 발사관 띠(파랑), 손상 통제반 표시(초록) — 섬 재질을 군함 블록에 쓰지 않게 따로 둔다
                ("Sam Blue", CreateMaterial("art_sam_blue", new Color(0.10f, 0.29f, 0.46f))),
                ("MAT_IslandGrass", CreateMaterial("art_repair_green", new Color(0.22f, 0.36f, 0.17f))),
                ("Enemy Hull Grey", CreateMaterial("art_enemy_hull", new Color(0.10f, 0.16f, 0.18f), 0.5f, 0.35f)),
                ("Enemy Submarine Green", CreateMaterial("art_submarine", new Color(0.045f, 0.15f, 0.105f), 0.4f, 0.3f)),
                ("Enemy Dark", CreateMaterial("art_enemy_dark", new Color(0.018f, 0.032f, 0.036f), 0.7f, 0.4f)),
            };

            // Blender/Unity가 여러 FBX에서 같은 재질명을 가져올 때 붙이는 .001 형식도
            // 모두 기존 공용 팔레트로 돌려 중복 머티리얼 생성을 막는다.
            var remaps = new List<(string name, Material mat)>();
            foreach (var (name, material) in palette)
            {
                remaps.Add((name, material));
                bool canBeNumbered = name == "Deck Grey" || name == "Naval Blue Grey" || name == "Gunmetal" ||
                                     name == "Radar Glass" || name == "Sensor Glass" || name == "Warning Yellow";
                if (canBeNumbered)
                    for (int i = 1; i <= 12; i++) remaps.Add(($"{name}.{i:000}", material));
            }

            foreach (var file in only != null && only.Length > 0 ? only : ArtModels)
                ImportModel($"{ModelDir}/{file}.fbx", remaps.ToArray());
        }

        // ------------------------------------------------------------------ 스테이지 2 추가 적

        /// <summary>
        /// 순항미사일 잠수함과 엘리트 초계함 프리팹만 만든다. 다른 프리팹·씬은 건드리지 않는다.
        /// (Naval/Add Stage 2 Enemies 메뉴, 배치 실행)
        /// </summary>
        public static Result BuildStage2EnemiesOnly()
        {
            PrepareArtMaterials("EnemyCruiseSubmarine", "EnemyPccCorvette");
            var r = Load();
            BuildStage2Enemies(r, $"{Root}/Prefabs/Enemies");
            AssetDatabase.SaveAssets();
            return r;
        }

        private static void BuildStage2Enemies(Result r, string eneDir)
        {
            var missile = r.MissileEnemy;
            var enemyGun = r.ProjEnemyGun;
            var flash = r.FxGunFlash;

            r.CruiseSubmarine = BuildArtCruiseSubmarine(eneDir);
            if (r.CruiseSubmarine == null) Debug.LogWarning("[Setup] EnemyCruiseSubmarine.fbx가 없어 순항미사일 잠수함 프리팹을 만들지 않았습니다.");

            // 엘리트 초계함: 14m 모델 × 0.85 ≈ 12m. 미사일정(8m)과 보스(17m) 사이
            r.PccCorvette = BuildArtEnemy<PccCorvette>("ENE_PccCorvette", "EnemyPccCorvette",
                0.85f, 0f, new Vector3(2.4f, 2.2f, 12f), eneDir, (go, comp) =>
                {
                    var box = go.GetComponent<BoxCollider>();
                    if (box != null) box.center = new Vector3(0f, 0.6f, 0f);

                    // 함수 포탑은 뒤쪽(함교·상부 구조물), 함미 포탑은 앞쪽을 쏘지 못한다
                    var specs = new (string name, float cutoutCenter)[]
                    {
                        ("ForeGun01", 180f), ("ForeGun02", 180f), ("AftGun01", 0f), ("AftGun02", 0f),
                    };

                    var so = new SerializedObject(comp);
                    var list = so.FindProperty("mounts");
                    list.arraySize = specs.Length;
                    for (int i = 0; i < specs.Length; i++)
                    {
                        var turret = FindDeep(go.transform, specs[i].name);
                        var muzzles = new List<Transform>();
                        if (turret != null) CollectMuzzles(turret, muzzles);
                        if (turret == null || muzzles.Count == 0)
                            Debug.LogError($"[Setup] 초계함 모델에서 {specs[i].name} 포탑 또는 포구를 찾지 못했습니다.");

                        // 쌍열포는 포구가 중심에서 벗어나 있어 조준 기준이 틀어진다. 포구들의 가운데에 기준점을 둔다.
                        Transform aimRef = null;
                        if (turret != null && muzzles.Count > 0)
                        {
                            Vector3 c = Vector3.zero;
                            foreach (var m in muzzles) c += m.position;
                            aimRef = new GameObject($"{specs[i].name}_AimRef").transform;
                            aimRef.SetParent(turret, false);
                            aimRef.position = c / muzzles.Count;
                        }

                        var e = list.GetArrayElementAtIndex(i);
                        e.FindPropertyRelative("Name").stringValue = specs[i].name;
                        e.FindPropertyRelative("Weapon.turret").objectReferenceValue = turret;
                        e.FindPropertyRelative("Weapon.elevationPivot").objectReferenceValue = null;
                        e.FindPropertyRelative("Weapon.muzzle").objectReferenceValue = aimRef;
                        e.FindPropertyRelative("Weapon.aimTolerance").floatValue = 6f;
                        e.FindPropertyRelative("Weapon.minElevation").floatValue = -10f;
                        e.FindPropertyRelative("Weapon.maxElevation").floatValue = 75f;

                        var mz = e.FindPropertyRelative("Muzzles");
                        mz.arraySize = muzzles.Count;
                        for (int k = 0; k < muzzles.Count; k++) mz.GetArrayElementAtIndex(k).objectReferenceValue = muzzles[k];

                        var cut = e.FindPropertyRelative("Cutouts");
                        cut.arraySize = 1;
                        var s = cut.GetArrayElementAtIndex(0);
                        s.FindPropertyRelative("Center").floatValue = specs[i].cutoutCenter;
                        s.FindPropertyRelative("HalfWidth").floatValue = 40f;
                        s.FindPropertyRelative("ClearElevation").floatValue = 0f;
                    }
                    Set(so, "projectilePrefab", enemyGun);
                    Set(so, "muzzleFlash", flash);
                    so.ApplyModifiedPropertiesWithoutUndo();
                });
            if (r.PccCorvette == null) Debug.LogWarning("[Setup] EnemyPccCorvette.fbx가 없어 초계함 프리팹을 만들지 않았습니다.");
        }

        // ------------------------------------------------------------------ 일반 적 5종

        /// <summary>Codex 일반 적 5종 모델(RecommendedNormalEnemies_5Pack).</summary>
        private static readonly string[] NormalEnemyModels =
            { "EnemyArmoredAssaultBoat", "EnemyTorpedoBoat", "EnemyArtilleryBoat", "EnemyRepairBoat", "EnemyMineLayer" };

        /// <summary>일반 적 5종과 기뢰 프리팹만 만든다. 다른 프리팹·씬은 건드리지 않는다(Naval/Add Normal Enemies).</summary>
        public static Result BuildNormalEnemiesOnly()
        {
            PrepareArtMaterials(NormalEnemyModels);
            var r = Load();
            BuildNormalEnemies(r, $"{Root}/Prefabs/Enemies");
            AssetDatabase.SaveAssets();
            return r;
        }

        /// <summary>
        /// 장갑 돌격정(고속정 동작) · 경어뢰정 · 포격 지원정 · 수리 지원정 · 기뢰부설정 + 부유 기뢰.
        /// 모델은 7m 안팎 설계라 0.85배(약 5.4~6.5m)로 줄여 고속정(5m)과 미사일정(8m) 사이에 둔다.
        /// 포탑은 PrimaryGunPivot → PrimaryGunElevationPivot → PrimaryGunMuzzle(모델 규약).
        /// </summary>
        private static void BuildNormalEnemies(Result r, string eneDir)
        {
            const float scale = 0.85f;
            var enemyGun = r.ProjEnemyGun;
            var torpedo = r.Torpedo;
            var flash = r.FxGunFlash;
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Projectiles/PRJ_BossShell.prefab");

            void Turret(GameObject go, SerializedObject so, string field, float tolerance)
            {
                var turret = FindDeep(go.transform, "PrimaryGunPivot");
                var elevation = FindDeep(go.transform, "PrimaryGunElevationPivot");
                var muzzle = FindDeep(go.transform, "PrimaryGunMuzzle");
                if (turret == null || muzzle == null) Debug.LogWarning($"[Setup] {go.name}: PrimaryGunPivot/PrimaryGunMuzzle 없음");
                Set(so, $"{field}.turret", turret);
                Set(so, $"{field}.elevationPivot", elevation);
                Set(so, $"{field}.muzzle", muzzle);
                Set(so, $"{field}.aimTolerance", tolerance);
            }
            Transform[] Points(GameObject go, string prefix, int max)
            {
                var list = new List<Transform>();
                for (int i = 1; i <= max; i++)
                {
                    var t = FindDeep(go.transform, $"{prefix}_{i:00}");
                    if (t != null) list.Add(t);
                }
                return list.ToArray();
            }

            // 장갑 돌격정: 고속정과 같은 동작(쌍열포 점사), 더 단단하고 느리다
            r.ArmoredBoat = BuildArtEnemy<FastAttackBoat>("ENE_ArmoredBoat", "EnemyArmoredAssaultBoat",
                scale, 0f, new Vector3(2.1f, 1.5f, 5.4f), eneDir, (go, comp) => Configure(comp, so =>
                {
                    Turret(go, so, "weapon", 10f);
                    Set(so, "projectilePrefab", enemyGun);
                    Set(so, "burstCount", 4);
                }));

            // 경어뢰정: 고정 어뢰관 4개(번갈아), 선수 포탑은 추적만
            r.TorpedoBoat = BuildArtEnemy<TorpedoBoat>("ENE_TorpedoBoat", "EnemyTorpedoBoat",
                scale, 0f, new Vector3(1.9f, 1.5f, 5.8f), eneDir, (go, comp) => Configure(comp, so =>
                {
                    Turret(go, so, "weapon", 15f);
                    Set(so, "torpedoPrefab", torpedo);
                    var tubes = Points(go, "TorpedoLaunchPoint", 4);
                    if (tubes.Length == 0) Debug.LogWarning("[Setup] 어뢰정 모델에 TorpedoLaunchPoint_01이 없습니다. 뱃머리 앞에서 쏩니다.");
                    SetArray(so, "launchPoints", tubes);
                }));

            // 포격 지원정: 큰 선수 함포 곡사 포격(보스 주포탄·경고 원 재사용)
            r.ArtilleryBoat = BuildArtEnemy<ArtilleryBoat>("ENE_ArtilleryBoat", "EnemyArtilleryBoat",
                scale, 0f, new Vector3(2.1f, 1.6f, 6.5f), eneDir, (go, comp) => Configure(comp, so =>
                {
                    Turret(go, so, "weapon", 8f);
                    Set(so, "shellPrefab", shell);
                    Set(so, "muzzleFlash", flash);
                }));
            if (shell == null) Debug.LogWarning("[Setup] PRJ_BossShell이 없어 포격 지원정이 쏘지 못합니다(전체 빌드 후 다시 실행).");

            // 수리 지원정: 크레인 + 초록 수리 빔
            var beamMat = CreateTransparentMaterial("repair_beam", new Color(0.35f, 1f, 0.5f, 0.75f));
            r.RepairBoat = BuildArtEnemy<RepairBoat>("ENE_RepairBoat", "EnemyRepairBoat",
                scale, 0f, new Vector3(2.25f, 1.6f, 6.3f), eneDir, (go, comp) =>
                {
                    var beamGo = new GameObject("Repair beam", typeof(LineRenderer));
                    beamGo.transform.SetParent(go.transform, false);
                    var line = beamGo.GetComponent<LineRenderer>();
                    line.useWorldSpace = true;
                    line.positionCount = 2;
                    line.startWidth = 0.14f;
                    line.endWidth = 0.08f;
                    line.numCapVertices = 2;
                    line.sharedMaterial = beamMat;
                    line.startColor = new Color(0.45f, 1f, 0.6f, 0.9f);
                    line.endColor = new Color(0.45f, 1f, 0.6f, 0.4f);
                    line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    line.enabled = false;
                    Configure(comp, so =>
                    {
                        Set(so, "beamOrigin", FindDeep(go.transform, "RepairBeamOrigin"));
                        Set(so, "cranePivot", FindDeep(go.transform, "CranePivot"));
                        Set(so, "beam", line);
                    });
                });

            // 부유 기뢰: 둥근 몸체 + 촉발 뿔 4개 + 깜빡이는 등
            r.SeaMine = BuildSeaMine(r.FxGunFlash, eneDir);

            // 기뢰부설정: 함미 투하 슈트 2개(기뢰 데이터는 NavalDataBuilder가 연결)
            r.MineLayer = BuildArtEnemy<MineLayer>("ENE_MineLayer", "EnemyMineLayer",
                scale, 0f, new Vector3(2.05f, 1.6f, 6.1f), eneDir, (go, comp) => Configure(comp, so =>
                {
                    Turret(go, so, "weapon", 15f);
                    var drops = Points(go, "MineDropPoint", 2);
                    if (drops.Length == 0) Debug.LogWarning("[Setup] 기뢰부설정 모델에 MineDropPoint_01이 없습니다. 함미에서 투하합니다.");
                    SetArray(so, "dropPoints", drops);
                }));

            foreach (var (name, prefab) in new[] { ("장갑 돌격정", r.ArmoredBoat), ("경어뢰정", r.TorpedoBoat), ("포격 지원정", r.ArtilleryBoat),
                                                   ("수리 지원정", r.RepairBoat), ("기뢰부설정", r.MineLayer) })
                if (prefab == null) Debug.LogWarning($"[Setup] {name} 모델(FBX)이 없어 프리팹을 만들지 않았습니다.");
        }

        private static GameObject BuildSeaMine(GameObject blast, string eneDir)
        {
            string matDir = $"{ModelDir}/Materials";
            var casing = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Mine casing.mat")
                         ?? CreateMaterial("mine_casing", new Color(0.13f, 0.15f, 0.14f), 0.4f, 0.35f);
            var dark = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Enemy weapon dark.mat") ?? casing;
            var lampMat = CreateMaterial("mine_lamp", new Color(1f, 0.2f, 0.1f));
            lampMat.EnableKeyword("_EMISSION");
            if (lampMat.HasProperty("_EmissionColor")) lampMat.SetColor("_EmissionColor", new Color(1f, 0.2f, 0.1f) * 2f);
            EditorUtility.SetDirty(lampMat);

            var root = new GameObject("ENE_SeaMine");
            var bob = new GameObject("Bob").transform;
            bob.SetParent(root.transform, false);
            var body = Primitive("Body", PrimitiveType.Sphere, Vector3.one * 0.95f, casing, bob);
            body.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var horn = Primitive($"Horn_{i}", PrimitiveType.Cylinder, new Vector3(0.09f, 0.16f, 0.09f), dark, bob);
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(50f, 0f, 0f) * Vector3.up;
                horn.transform.localPosition = new Vector3(0f, 0.12f, 0f) + dir * 0.5f;
                horn.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            }
            var lamp = Primitive("Lamp", PrimitiveType.Sphere, Vector3.one * 0.16f, lampMat, bob);
            lamp.transform.localPosition = new Vector3(0f, 0.62f, 0f);

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.0f, 1.2f);
            col.center = new Vector3(0f, 0.2f, 0f);
            col.isTrigger = true;

            var comp = root.AddComponent<SeaMine>();
            Configure(comp, so =>
            {
                Set(so, "bob", bob);
                Set(so, "lamp", lamp.GetComponent<MeshRenderer>());
                Set(so, "blastEffect", blast);
            });
            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, eneDir);
        }

        // ------------------------------------------------------------------ 스테이지 3 적(2026-10-05)
        // Codex Stage4EnemyRevision2 모델(A2/AD 함대) 중 넷을 스테이지 3 일반·엘리트 적으로 쓴다. 지휘 순양함(보스)은 스테이지 4용으로 남김.
        // 보스는 항공전함(EnemyHybridBoss — 현대화 이세급 v10 모델).

        /// <summary>스테이지 3에 쓰는 모델(항공전함 v10 + 적 4종).</summary>
        public static readonly string[] Stage3Models =
            { "EnemyHybridBoss", "EnemyUnmannedAttackCraft", "EnemyEwCorvette", "EnemyAirDefenseFrigate", "EnemyAttackSubmarine" };

        /// <summary>스테이지 3 적 4종과 항공전함 프리팹만 다시 만든다(Naval/Add Stage 3). 다른 프리팹·씬은 건드리지 않는다.</summary>
        public static Result BuildStage3Only()
        {
            PrepareArtMaterials(Stage3Models);
            var r = Load();
            string eneDir = $"{Root}/Prefabs/Enemies";
            string fxDir = $"{Root}/Prefabs/Projectiles";
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>($"{fxDir}/PRJ_BossShell.prefab");
            var aaTracer = AssetDatabase.LoadAssetAtPath<GameObject>($"{fxDir}/PRJ_EnemyAA.prefab");
            if (shell != null && aaTracer != null) r.Boss2 = BuildArtBoss2(shell, r.FxGunFlash, aaTracer, eneDir) ?? r.Boss2;
            BuildStage3Enemies(r, eneDir);
            AssetDatabase.SaveAssets();
            return r;
        }

        private static void BuildStage3Enemies(Result r, string eneDir)
        {
            var enemyGun = r.ProjEnemyGun;
            var aaTracer = AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Prefabs/Projectiles/PRJ_EnemyAA.prefab");
            var jamPulse = r.FxJamPulse;

            void WireGun(GameObject go, Component comp, int burst)
            {
                var turret = FindDeep(go.transform, "PrimaryGunPivot");
                var elevation = FindDeep(go.transform, "PrimaryGunElevationPivot");
                var muzzle = FindDeep(go.transform, "PrimaryGunMuzzle");
                if (turret == null || muzzle == null) Debug.LogError($"[Setup] {go.name}: PrimaryGunPivot/PrimaryGunMuzzle 없음");
                Configure(comp, so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "weapon.aimTolerance", 10f);
                    Set(so, "projectilePrefab", enemyGun);
                    Set(so, "burstCount", burst);
                });
            }

            // 무인 공격정: 5.5m × 1.0. 고속정보다 빠르고 조금 단단한 무리(선수 기관포, 뒤의 발사통은 외형)
            r.UnmannedCraft = BuildArtEnemy<FastAttackBoat>("ENE_UnmannedCraft", "EnemyUnmannedAttackCraft",
                1.0f, 0f, new Vector3(1.9f, 1.5f, 5.5f), eneDir, (go, comp) => WireGun(go, comp, 3));

            // 전자전 코르벳: 10.5m × 1.0. 선수포 + 방해 장비(레이더 탐지 거리 감소)
            r.EwCorvette = BuildArtEnemy<FastAttackBoat>("ENE_EwCorvette", "EnemyEwCorvette",
                1.0f, 0f, new Vector3(2.9f, 2.5f, 10.5f), eneDir, (go, comp) =>
                {
                    WireGun(go, comp, 2);
                    var jammer = go.AddComponent<EwJammer>();
                    var antennas = new List<Transform>();
                    foreach (var n in new[] { "AntennaPivot_-1", "AntennaPivot_1" })
                    {
                        var t = FindDeep(go.transform, n);
                        if (t != null) antennas.Add(t);
                    }
                    Configure(jammer, so =>
                    {
                        Set(so, "pulseEffect", jamPulse);
                        SetArray(so, "antennas", antennas.ToArray());
                    });
                });

            // 방공 프리깃: 12.5m × 1.0. 선수포 + 함미 근접방어포가 플레이어 유도탄을 쏘아 떨어뜨린다
            r.AaFrigate = BuildArtEnemy<FastAttackBoat>("ENE_AaFrigate", "EnemyAirDefenseFrigate",
                1.0f, 0f, new Vector3(3.3f, 2.8f, 12.5f), eneDir, (go, comp) =>
                {
                    WireGun(go, comp, 2);
                    var ciws = go.AddComponent<MissileInterceptor>();
                    Configure(ciws, so =>
                    {
                        Set(so, "tracerPrefab", aaTracer);
                        Set(so, "mount", FindDeep(go.transform, "RearCIWSMuzzle"));
                        Set(so, "turret", FindDeep(go.transform, "RearCIWSPivot"));
                    });
                });

            // 공격 잠수함: 9.3m × 1.0. 어뢰 잠수함 행동(예고 뒤 직선 어뢰)을 더 단단하고 자주
            var torpedo = r.Torpedo;
            r.AttackSubmarine = BuildArtEnemy<Submarine>("ENE_AttackSubmarine", "EnemyAttackSubmarine",
                1.0f, -0.15f, new Vector3(2.1f, 1.4f, 9.3f), eneDir, (go, comp) =>
                    Configure(comp, so =>
                    {
                        Set(so, "modelPivot", go.transform.Find("ModelPivot"));
                        Set(so, "torpedoPrefab", torpedo);
                    }));

            foreach (var (prefab, model) in new[] { (r.UnmannedCraft, "EnemyUnmannedAttackCraft"), (r.EwCorvette, "EnemyEwCorvette"),
                                                    (r.AaFrigate, "EnemyAirDefenseFrigate"), (r.AttackSubmarine, "EnemyAttackSubmarine") })
                if (prefab == null) Debug.LogWarning($"[Setup] {model}.fbx가 없어 스테이지 3 적 프리팹을 만들지 않았습니다.");
        }

        // ------------------------------------------------------------------ 스테이지 2 보스: 현대화 초계함

        /// <summary>현대화 초계함 프리팹만 만든다. 다른 프리팹·씬은 건드리지 않는다(Naval/Add Stage 2 Corvette Boss).</summary>
        public static Result BuildModernCorvetteOnly()
        {
            PrepareArtMaterials("EnemyModernCorvette");
            var r = Load();
            r.ModernCorvette = BuildModernCorvette(r, $"{Root}/Prefabs/Enemies");
            AssetDatabase.SaveAssets();
            return r;
        }

        /// <summary>
        /// 현대화 초계함(Codex ModernCorvette v11.1): 20m 모델 × 1.0(1보스 연안 경비정 17m보다 길다).
        /// 함포·발사관·연출 소켓은 런타임(ModernCorvetteBoss)이 이름으로 찾는다. 여기서는 탄·어뢰·섬광을 잇고
        /// 어뢰 발사점마다 붉은 경고등(발사관이 돌 때 같이 돈다)을 붙인다.
        /// </summary>
        private static GameObject BuildModernCorvette(Result r, string eneDir)
        {
            string fxDir = $"{Root}/Prefabs/Projectiles";
            var shell = AssetDatabase.LoadAssetAtPath<GameObject>($"{fxDir}/PRJ_BossShell.prefab");
            var torpedo = r.Torpedo;
            var flash = r.FxGunFlash;
            var lampMat = CreateMaterial("corvette_torpedo_lamp", new Color(1f, 0.18f, 0.08f));
            lampMat.EnableKeyword("_EMISSION");
            if (lampMat.HasProperty("_EmissionColor")) lampMat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(lampMat);

            var prefab = BuildArtEnemy<ModernCorvetteBoss>("ENE_BossCorvette", "EnemyModernCorvette",
                1.0f, 0f, new Vector3(4.2f, 3f, 20f), eneDir, (go, comp) =>
                {
                    var missiles = new List<Transform>();
                    var tubes = new List<Transform>();
                    for (int i = 1; i <= 8; i++)
                    {
                        var m = FindDeep(go.transform, $"MissileLaunchPoint_{i:00}");
                        if (m != null) missiles.Add(m);
                        var t = FindDeep(go.transform, $"TorpedoLaunchPoint_{i:00}");
                        if (t != null) tubes.Add(t);
                    }
                    if (missiles.Count < 4 || tubes.Count < 2)
                        Debug.LogError($"[Setup] 현대화 초계함 소켓 부족: 미사일 {missiles.Count}/4 · 어뢰 {tubes.Count}/6");

                    var lamps = new List<Renderer>();
                    foreach (var t in tubes)
                    {
                        var lamp = Primitive("TorpedoLamp", PrimitiveType.Sphere, Vector3.one * 0.18f, lampMat, t.parent);
                        lamp.transform.position = t.position + Vector3.up * 0.24f;
                        lamps.Add(lamp.GetComponent<Renderer>());
                    }

                    Configure(comp, so =>
                    {
                        SetArray(so, "missileLaunchPoints", missiles.ToArray());
                        SetArray(so, "torpedoLaunchPoints", tubes.ToArray());
                        SetArray(so, "torpedoLamps", lamps.ToArray());
                        Set(so, "shellPrefab", shell);
                        Set(so, "torpedoPrefab", torpedo);
                        Set(so, "gunFlashMaterialSource", flash);
                        Set(so, "radar", FindDeep(go.transform, "RadarPivot"));
                    });
                });
            if (prefab == null) Debug.LogWarning("[Setup] EnemyModernCorvette.fbx가 없어 현대화 초계함 프리팹을 만들지 않았습니다.");
            return prefab;
        }

        // ------------------------------------------------------------------ 자폭 보트

        /// <summary>자폭 보트 프리팹만 만든다. 다른 프리팹·씬은 건드리지 않는다(Naval/Add Suicide Boat).</summary>
        public static Result BuildSuicideBoatOnly()
        {
            PrepareArtMaterials("EnemyFastBoat");
            var r = Load();
            BuildSuicideBoat(r, $"{Root}/Prefabs/Enemies");
            AssetDatabase.SaveAssets();
            return r;
        }

        /// <summary>
        /// 고속정 모델을 0.62 → 0.42배로 줄이고 포탑을 떼어 낸 뒤, 선수 갑판에 붉은 폭약 상자와 노란 경고띠를 얹는다.
        /// 고속정보다 확실히 작고, 포 없이 붉은 짐을 실은 모습으로 구분된다.
        /// </summary>
        private static void BuildSuicideBoat(Result r, string eneDir)
        {
            // 미니어처 적 v9(2026-10-03): 전용 모델(3.4m 무인정 · 선수 폭약부 · 광학 센서)이 있으면 1.0배 그대로 쓴다.
            // 절차적 폭약 상자·경고띠를 더하지 않는다(모델에 이미 있다). 없으면 아래 예전 방식(고속정 0.42배 + 폭약 상자).
            var blastFx = r.FxGunFlash;
            var dedicated = BuildArtEnemy<SuicideBoat>("ENE_SuicideBoat", "EnemySuicideBoat",
                1.0f, 0f, new Vector3(1.3f, 1.0f, 3.5f), eneDir, (go, comp) =>
                    Configure(comp, so => Set(so, "blastEffect", blastFx)));
            if (dedicated != null) { r.SuicideBoat = dedicated; return; }

            string matDir = $"{ModelDir}/Materials";
            var red = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Warning red.mat")
                      ?? AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Emergency Red.mat");
            var yellow = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/Warning Yellow.mat");
            var blast = r.FxGunFlash;

            r.SuicideBoat = BuildArtEnemy<SuicideBoat>("ENE_SuicideBoat", "EnemyFastBoat",
                0.42f, 0f, new Vector3(1.5f, 1.0f, 3.4f), eneDir, (go, comp) =>
                {
                    // 포탑·포구는 쓰지 않는다
                    foreach (var n in new[] { "PrimaryGunPivot", "GunElevationPivot", "Muzzle" })
                    {
                        var t = FindDeep(go.transform, n);
                        if (t != null) t.gameObject.SetActive(false);
                    }

                    // 폭약 상자 + 경고띠(선수 갑판)
                    var charge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    charge.name = "Explosive charge";
                    Object.DestroyImmediate(charge.GetComponent<Collider>());
                    charge.transform.SetParent(go.transform, false);
                    charge.transform.localPosition = new Vector3(0f, 0.62f, 0.55f);
                    charge.transform.localScale = new Vector3(0.75f, 0.38f, 0.95f);
                    if (red != null) charge.GetComponent<MeshRenderer>().sharedMaterial = red;

                    var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    band.name = "Warning band";
                    Object.DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(charge.transform, false);
                    band.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                    band.transform.localScale = new Vector3(1.04f, 1.04f, 0.22f);
                    if (yellow != null) band.GetComponent<MeshRenderer>().sharedMaterial = yellow;

                    Configure(comp, so => Set(so, "blastEffect", blast));
                });
            if (r.SuicideBoat == null) Debug.LogWarning("[Setup] EnemyFastBoat.fbx가 없어 자폭 보트 프리팹을 만들지 않았습니다.");
        }

        /// <summary>포탑 아래 "…Muzzle…" 노드를 이름순으로 모은다.</summary>
        private static void CollectMuzzles(Transform root, List<Transform> into)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.Contains("Muzzle")) into.Add(c);
                CollectMuzzles(c, into);
            }
            into.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private static void CollectByPrefix(Transform root, string prefix, List<GameObject> into)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name.StartsWith(prefix)) into.Add(c.gameObject);
                CollectByPrefix(c, prefix, into);
            }
        }

        /// <summary>
        /// 미사일정 발사점. 모델의 소켓은 Blender 축이라 로컬 +Z가 하늘을 향한다.
        /// 발사관 뒤(MissileCanister)에서 입구(CanisterFront)로 가는 방향을 실제 발사 방향으로 쓴다.
        /// </summary>
        private static Transform BuildCanisterLaunchPoint(Transform root)
        {
            var existing = FindDeep(root, "MissileLaunchPoint");
            if (existing != null) return existing;
            var back = FindDeep(root, "MissileCanister_02") ?? FindDeep(root, "MissileCanister_01");
            var mouth = FindDeep(root, "CanisterFront_02") ?? FindDeep(root, "CanisterFront_01");
            var pivot = FindDeep(root, "MissileLauncherPivot");
            if (back == null || mouth == null || pivot == null) return null;

            Vector3 dir = mouth.position - back.position;
            if (dir.sqrMagnitude < 0.0001f) return null;

            var lp = new GameObject("MissileLaunchPoint").transform;
            var otherMouth = FindDeep(root, "CanisterFront_03");
            var exit = otherMouth != null ? (mouth.position + otherMouth.position) * 0.5f : mouth.position;
            lp.SetPositionAndRotation(exit, Quaternion.LookRotation(dir.normalized, Vector3.up));
            lp.SetParent(pivot, true);   // 발사대가 움직여도 따라가도록 소켓 아래에 둔다
            return lp;
        }

        /// <summary>
        /// 닻(그레이박스). 원점 = 닻줄 구멍 중심, +Z = 선체 바깥, 닻은 아래로 매달린다.
        /// 사진 속 전함처럼 둥근 닻줄 구멍 테와, 걸려 있는 닻(자루 + 갈고리)으로 이루어진다.
        /// </summary>
        private static GameObject BuildAnchorGreybox(Material iron, Material plate, string dir)
        {
            var root = new GameObject("HULL_Anchor");

            var hawse = Primitive("HawseRing", PrimitiveType.Cylinder, new Vector3(0.34f, 0.03f, 0.34f), plate, root.transform);
            hawse.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);    // 평평한 면이 바깥을 본다
            hawse.transform.localPosition = new Vector3(0f, 0f, 0.02f);

            var hole = Primitive("HawseHole", PrimitiveType.Cylinder, new Vector3(0.2f, 0.02f, 0.2f), iron, root.transform);
            hole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hole.transform.localPosition = new Vector3(0f, 0f, 0.05f);

            var shank = Primitive("Shank", PrimitiveType.Cube, new Vector3(0.07f, 0.42f, 0.07f), iron, root.transform);
            shank.transform.localPosition = new Vector3(0f, -0.26f, 0.1f);

            var crown = Primitive("Crown", PrimitiveType.Cube, new Vector3(0.34f, 0.07f, 0.08f), iron, root.transform);
            crown.transform.localPosition = new Vector3(0f, -0.47f, 0.1f);

            for (int side = -1; side <= 1; side += 2)
            {
                var fluke = Primitive(side < 0 ? "Fluke_L" : "Fluke_R", PrimitiveType.Cube,
                                      new Vector3(0.07f, 0.16f, 0.09f), iron, root.transform);
                fluke.transform.localPosition = new Vector3(side * 0.17f, -0.4f, 0.1f);
                fluke.transform.localRotation = Quaternion.Euler(0f, 0f, side * -25f);
            }

            return SavePrefab(root, dir);
        }

        /// <summary>승강 거치대 받침(그레이박스). 원점 바닥, 높이 1.2 m. 올린 무기 아래에 선다.</summary>
        private static GameObject BuildPedestalGreybox(Material hull, Material dark, string dir)
        {
            var root = new GameObject("HULL_MountPedestal");

            var column = Primitive("Column", PrimitiveType.Cylinder, new Vector3(0.9f, 0.6f, 0.9f), hull, root.transform);
            column.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var flange = Primitive("BaseFlange", PrimitiveType.Cylinder, new Vector3(1.3f, 0.05f, 1.3f), dark, root.transform);
            flange.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var top = Primitive("TopPlate", PrimitiveType.Cylinder, new Vector3(1.4f, 0.04f, 1.4f), dark, root.transform);
            top.transform.localPosition = new Vector3(0f, 1.16f, 0f);

            return SavePrefab(root, dir);
        }

        /// <summary>저장된 프리팹을 열어 고친다. 아트/그레이박스 어느 쪽으로 만들어졌든 같은 설정을 넣을 때 쓴다.</summary>
        private static void PatchPrefab(GameObject prefab, System.Action<GameObject> edit)
        {
            if (prefab == null) return;
            using var scope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(prefab));
            edit(scope.prefabContentsRoot);
        }

        /// <summary>
        /// 함대공 미사일 발사기(그레이박스). VLS의 평평한 셀과 헷갈리지 않게 **선회식 4연장 박스 발사기**다.
        /// 받침 위 요크가 위협 쪽으로 돌고, 흰 발사관 묶음이 40° 들려 있다. 발사관 입구는 청색.
        /// </summary>
        private static GameObject BuildSamLauncherGreybox(Material hull, Material gun, GameObject interceptor, string dir)
        {
            var root = new GameObject("MOD_SamLauncher");
            var white = CreateMaterial("missile_sam_body", new Color(0.96f, 0.97f, 0.98f), 0f, 0.5f);
            var blue = CreateMaterial("missile_sam_nose", new Color(0.10f, 0.55f, 0.95f));

            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(Cell * 0.85f, 0.15f, Cell * 0.85f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.075f, 0f);

            var pedestal = Primitive("Pedestal", PrimitiveType.Cylinder, new Vector3(0.7f, 0.2f, 0.7f), gun, root.transform);
            pedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);

            var train = new GameObject("TrainPivot").transform;
            train.SetParent(root.transform, false);
            train.localPosition = new Vector3(0f, 0.55f, 0f);

            for (int side = -1; side <= 1; side += 2)
            {
                var arm = Primitive(side < 0 ? "Yoke_L" : "Yoke_R", PrimitiveType.Cube, new Vector3(0.1f, 0.55f, 0.35f), hull, train);
                arm.transform.localPosition = new Vector3(side * 0.55f, 0.27f, 0f);
            }

            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(train, false);
            elevation.localPosition = new Vector3(0f, 0.5f, 0f);
            elevation.localRotation = Quaternion.Euler(-40f, 0f, 0f);

            var box = Primitive("CanisterBox", PrimitiveType.Cube, new Vector3(0.9f, 0.55f, 1.5f), white, elevation);
            box.transform.localPosition = Vector3.zero;

            var points = new List<Transform>();
            int n = 0;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    var pos = new Vector3((col == 0 ? -1f : 1f) * 0.22f, (row == 0 ? -1f : 1f) * 0.13f, 0.76f);
                    var cap = Primitive($"TubeCap_{n + 1:00}", PrimitiveType.Cylinder, new Vector3(0.3f, 0.02f, 0.3f), blue, elevation);
                    cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    cap.transform.localPosition = pos;

                    var lp = new GameObject($"LaunchPoint_{n + 1:00}").transform;
                    lp.SetParent(elevation, false);
                    lp.localPosition = pos + new Vector3(0f, 0f, 0.3f);
                    points.Add(lp);
                    n++;
                }
            }

            var band = Primitive("RedBand", PrimitiveType.Cube, new Vector3(0.92f, 0.57f, 0.08f), CreateMaterial("danger", new Color(0.85f, 0.25f, 0.20f)), elevation);
            band.transform.localPosition = new Vector3(0f, 0f, -0.45f);

            var comp = root.AddComponent<SamLauncherModule>();
            Configure(comp, so =>
            {
                Set(so, "interceptorPrefab", interceptor);
                SetArray(so, "launchPoints", points.ToArray());
                Set(so, "trainPivot", train);
                Set(so, "launchAlongPoint", true);
            });
            return SavePrefab(root, dir);
        }

        private static GameObject BuildArtSamLauncher(GameObject interceptor, string dir)
        {
            return BuildArtModule<SamLauncherModule>("MOD_SamLauncher", "MOD_SamLauncher", dir, (root, model) =>
            {
                var points = new List<Transform>();
                for (int i = 1; i <= 16; i++)
                {
                    var t = FindDeep(model, $"LaunchPoint_{i:00}");
                    if (t != null) points.Add(t);
                }
                var train = FindDeep(model, "TrainPivot");
                Configure(root.GetComponent<SamLauncherModule>(), so =>
                {
                    Set(so, "interceptorPrefab", interceptor);
                    SetArray(so, "launchPoints", points.ToArray());
                    if (train != null) Set(so, "trainPivot", train);
                });
            });
        }

        /// <summary>전자전 장비(그레이박스). 받침 상자, 짧은 마스트, 회전하는 막대 안테나, 작은 돔 2개.</summary>
        private static GameObject BuildEwSuiteGreybox(Material hull, Material sensor, GameObject pulse, string dir)
        {
            var root = new GameObject("MOD_EwSuite");

            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(Cell * 0.85f, 0.15f, Cell * 0.85f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.075f, 0f);

            var box = Primitive("Cabinet", PrimitiveType.Cube, new Vector3(1.0f, 0.5f, 1.0f), hull, root.transform);
            box.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            for (int i = 0; i < 2; i++)
            {
                var dome = Primitive($"Dome_{i}", PrimitiveType.Sphere, Vector3.one * 0.35f, sensor, root.transform);
                dome.transform.localPosition = new Vector3(i == 0 ? -0.35f : 0.35f, 0.7f, 0.3f);
            }

            var mast = Primitive("Mast", PrimitiveType.Cylinder, new Vector3(0.12f, 0.3f, 0.12f), hull, root.transform);
            mast.transform.localPosition = new Vector3(0f, 0.95f, -0.2f);

            var antenna = new GameObject("AntennaPivot").transform;
            antenna.SetParent(root.transform, false);
            antenna.localPosition = new Vector3(0f, 1.3f, -0.2f);
            Primitive("AntennaBar", PrimitiveType.Cube, new Vector3(1.1f, 0.12f, 0.12f), sensor, antenna);

            var comp = root.AddComponent<EwSuiteModule>();
            Configure(comp, so =>
            {
                Set(so, "pulseEffect", pulse);
                Set(so, "antenna", antenna);
            });
            return SavePrefab(root, dir);
        }

        private static GameObject BuildArtEwSuite(GameObject pulse, string dir)
        {
            return BuildArtModule<EwSuiteModule>("MOD_EwSuite", "MOD_EwSuite", dir, (root, model) =>
            {
                Configure(root.GetComponent<EwSuiteModule>(), so =>
                {
                    Set(so, "pulseEffect", pulse);
                    var antenna = FindDeep(model, "AntennaPivot");
                    if (antenna != null) Set(so, "antenna", antenna);
                });
            });
        }

        /// <summary>동작 없는 장식 소품. 모델이 없으면 null.</summary>
        private static GameObject BuildArtProp(string prefabName, string modelName, string dir)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}.fbx");
            if (model == null) return null;

            var root = new GameObject(prefabName);
            AttachArtModel(model, root.transform);
            return SavePrefab(root, dir);
        }

        /// <summary>함교 블록 길이. 앞뒤 3칸.</summary>
        private const int BridgeCells = 3;

        /// <summary>
        /// 함교(그레이박스). 원점은 가운데 칸 중심이고, 선수 쪽 칸에 함교탑, 가운데에 기관실 상부구조,
        /// 선미 쪽 칸에 연돌을 둔다. ModuleFactory가 3칸 발자국의 중심에 맞춰 놓는다.
        /// </summary>
        private static GameObject BuildBridgeGreybox(Material hull, Material dark, string dir)
        {
            var root = new GameObject("MOD_Bridge");
            AddEngineHouse(root.transform, hull, dark, includeBridgeTower: true);
            root.AddComponent<BridgeModule>();
            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 기관실 상부구조와 연돌(필요하면 함교탑까지). 그레이박스와, 1칸짜리 옛 함교 모델의 빈 칸 채우기에 함께 쓴다.
        /// </summary>
        private static void AddEngineHouse(Transform root, Material hull, Material dark, bool includeBridgeTower)
        {
            var house = Primitive("EngineHouse", PrimitiveType.Cube,
                                  new Vector3(Cell * 0.8f, 1.2f, Cell * (BridgeCells - 0.2f)), hull, root);
            house.transform.localPosition = new Vector3(0f, 0.6f, 0f);

            var funnel = Primitive("Funnel", PrimitiveType.Cylinder, new Vector3(0.9f, 1.3f, 1.1f), dark, root);
            funnel.transform.localPosition = new Vector3(0f, 2.5f, -Cell);   // 선미 쪽 칸

            if (!includeBridgeTower) return;

            var tower = Primitive("BridgeTower", PrimitiveType.Cube, new Vector3(Cell * 0.75f, 1.2f, Cell * 0.7f), hull, root);
            tower.transform.localPosition = new Vector3(0f, 1.8f, Cell);      // 선수 쪽 칸

            var mast = Primitive("Mast", PrimitiveType.Cylinder, new Vector3(0.12f, 0.8f, 0.12f), dark, root);
            mast.transform.localPosition = new Vector3(0f, 3.2f, Cell * 0.8f);
        }

        /// <summary>
        /// 아트 함교. 3칸 길이 모델이면 그대로 쓰고, 예전 1칸짜리 함교 모델이면 선수 쪽 칸에 올리고
        /// 나머지 두 칸은 기관실·연돌 그레이박스로 채운다(코덱스의 3×1 모델이 오기 전까지의 임시 처리).
        /// </summary>
        private static GameObject BuildArtBridge(Material hull, Material dark, string dir)
        {
            return BuildArtModule<BridgeModule>("MOD_Bridge", "MOD_Bridge", dir, (root, model) =>
            {
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return;

                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);

                // 3칸(약 5.8m)의 2/3보다 짧으면 옛 1칸 모델로 본다
                if (bounds.size.z >= Cell * 2f) return;

                model.parent.localPosition += new Vector3(0f, 0f, Cell);
                AddEngineHouse(root.transform, hull, dark, includeBridgeTower: false);
            });
        }

        /// <summary>
        /// 함교의 내장 기만체. 아트/그레이박스 어느 쪽이 선택됐든 저장된 프리팹을 열어 연결한다.
        /// </summary>
        private static void WireBridgeDecoy(GameObject bridgePrefab, GameObject decoy)
        {
            if (bridgePrefab == null) return;

            using var scope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(bridgePrefab));
            var root = scope.prefabContentsRoot;

            // 모델에 소켓이 있으면 그것을, 없으면 지붕 위에 만든다
            var launchPoint = FindDeep(root.transform, "DecoyLaunchPoint");
            if (launchPoint == null)
            {
                launchPoint = new GameObject("DecoyLaunchPoint").transform;
                launchPoint.SetParent(root.transform, false);
                launchPoint.localPosition = new Vector3(0f, 2.4f, 0f);   // 함교 지붕 위
            }

            Configure(root.GetComponent<BridgeModule>(), so =>
            {
                Set(so, "decoyPrefab", decoy);
                Set(so, "launchPoint", launchPoint);
                Set(so, "decoysPerShot", 2);   // 재장전은 함교 데이터(ReloadTime)에서

                // RadarPivot은 Blender 축이라 로컬 축으로 돌리면 눕는다. 런타임은 월드 위축으로 돌린다.
                var antenna = FindDeep(root.transform, "RadarPivot");
                if (antenna != null) Set(so, "radarAntenna", antenna);
            });
        }

        /// <summary>이름으로 자식을 깊이 우선 탐색한다. 모델 계층이 바뀌어도 견디도록.</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>모델을 자식으로 단 모듈 프리팹. 모델이 없으면 null.</summary>
        private static GameObject BuildArtModule<T>(string prefabName, string modelName, string dir,
                                                    System.Action<GameObject, Transform> extra)
            where T : Component
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}.fbx");
            if (model == null) return null;

            var root = new GameObject(prefabName);

            // 임포트된 모델의 트랜스폼에는 Unity가 넣은 축·단위 보정이 들어있다.
            // 절대 덮어쓰면 안 되므로, 내 변형은 항상 부모에 건다.
            var instance = AttachArtModel(model, root.transform);

            root.AddComponent<T>();
            extra?.Invoke(root, instance);

            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 기본/U1/U2 모델을 한 프리팹에 넣고 레벨에 따라 하나만 표시한다.
        /// 업그레이드 FBX가 아직 없으면 기존 단일 모델 빌더로 안전하게 되돌아간다.
        /// </summary>
        private static GameObject BuildUpgradeableArtModule<T>(string prefabName, string modelName, string dir,
                                                               System.Action<GameObject, Transform> extra)
            where T : Component
        {
            var baseModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}.fbx");
            var upgrade1 = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}_U1.fbx");
            var upgrade2 = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}_U2.fbx");
            if (baseModel == null) return null;
            if (upgrade1 == null || upgrade2 == null)
                return BuildArtModule<T>(prefabName, modelName, dir, extra);

            var root = new GameObject(prefabName);
            var baseInstance = AttachArtModel(baseModel, root.transform, "Visual_Level1", "Model_Base");
            var u1Instance = AttachArtModel(upgrade1, root.transform, "Visual_Level2", "Model_U1");
            var u2Instance = AttachArtModel(upgrade2, root.transform, "Visual_Level3", "Model_U2");

            u1Instance.parent.gameObject.SetActive(false);
            u2Instance.parent.gameObject.SetActive(false);

            root.AddComponent<T>();
            var visuals = root.AddComponent<Game.Modules.ModuleUpgradeVisuals>();
            Configure(visuals, so => SetArray(so, "levels",
                baseInstance.parent.gameObject, u1Instance.parent.gameObject, u2Instance.parent.gameObject));
            extra?.Invoke(root, baseInstance);
            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 좌우 회전과 상하 고각이 분리된 아트 무기 공통 빌더.
        /// 포신과 Muzzle은 FBX에서 ElevationPivot 아래에 있으므로 별도 재부모화하지 않는다.
        /// </summary>
        private static GameObject BuildArtWeapon<T>(string prefabName, string modelName,
                                                    GameObject projectile, string dir)
            where T : Component
        {
            return BuildArtModule<T>(prefabName, modelName, dir, (root, model) =>
            {
                var turret = FindDeep(model, "TurretPivot");
                var elevation = FindDeep(model, "ElevationPivot");
                var muzzle = FindDeep(model, "Muzzle");

                if (turret == null || elevation == null || muzzle == null)
                {
                    Debug.LogError($"[Setup] {modelName}에서 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.");
                    return;
                }

                Configure(root.GetComponent<T>(), so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    // 조준 오차가 이보다 크면 쏘지 않는다: CIWS는 더 정밀하게
                    Set(so, "weapon.aimTolerance", typeof(T) == typeof(CiwsModule) ? 5f : 4f);
                    Set(so, "projectilePrefab", projectile);
                });
            });
        }

        private static GameObject BuildUpgradeableArtWeapon<T>(string prefabName, string modelName,
                                                               GameObject projectile, string dir)
            where T : Component
        {
            return BuildUpgradeableArtModule<T>(prefabName, modelName, dir, (root, model) =>
            {
                var turret = FindDeep(model, "TurretPivot");
                var elevation = FindDeep(model, "ElevationPivot");
                var muzzle = FindDeep(model, "Muzzle");
                if (turret == null || elevation == null || muzzle == null)
                {
                    Debug.LogError($"[Setup] {modelName} 기본 모델에서 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.");
                    return;
                }

                Configure(root.GetComponent<T>(), so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "weapon.aimTolerance", typeof(T) == typeof(CiwsModule) ? 5f : 4f);
                    Set(so, "projectilePrefab", projectile);
                });
            });
        }

        private static GameObject BuildArtAutocannon(GameObject projectile, string dir)
            => BuildUpgradeableArtWeapon<AutocannonModule>("MOD_Autocannon", "MOD_Autocannon", projectile, dir);

        private static GameObject BuildArtCiws(GameObject projectile, string dir)
            => BuildUpgradeableArtWeapon<CiwsModule>("MOD_Ciws", "MOD_CIWS", projectile, dir);

        /// <summary>기존 CIWS의 밸런스·탄약·VFX 설정은 보존하고 모델과 소켓만 교체한다.</summary>
        [MenuItem("Naval/Art/Refresh CIWS Only")]
        public static void RefreshCiwsOnly()
        {
            const string path = Root + "/Prefabs/Modules/MOD_Ciws.prefab";
            PrepareArtMaterials("MOD_CIWS");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/MOD_CIWS.fbx");
            if (model == null) { Debug.LogError("[CIWS] 새 FBX를 찾지 못했습니다."); return; }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pivot = root.transform.Find("ModelPivot");
                var module = root.GetComponent<CiwsModule>();
                if (pivot == null || module == null)
                {
                    Debug.LogError("[CIWS] 기존 프리팹에 ModelPivot 또는 CiwsModule이 없습니다.");
                    return;
                }

                // 먼저 임시로 붙여 소켓을 검증한다. 실패하면 기존 모델을 그대로 둔다.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                instance.name = "Model";
                instance.transform.SetParent(pivot, false);
                var turret = FindDeep(instance.transform, "TurretPivot");
                var elevation = FindDeep(instance.transform, "ElevationPivot");
                var muzzle = FindDeep(instance.transform, "Muzzle");
                var barrels = FindDeep(instance.transform, "BarrelCluster");
                if (turret == null || elevation == null || muzzle == null || barrels == null)
                {
                    Debug.LogError("[CIWS] FBX에 필요한 TurretPivot/ElevationPivot/Muzzle/BarrelCluster가 없습니다.");
                    return;
                }
                if (!elevation.IsChildOf(turret) || !barrels.IsChildOf(elevation) ||
                    !muzzle.IsChildOf(elevation) || muzzle.IsChildOf(barrels))
                {
                    Debug.LogError("[CIWS] 선회/고각/총열 계층이 잘못되었습니다. 기존 프리팹을 유지합니다.");
                    return;
                }

                for (int i = pivot.childCount - 1; i >= 0; i--)
                    if (pivot.GetChild(i) != instance.transform)
                        UnityEngine.Object.DestroyImmediate(pivot.GetChild(i).gameObject);

                Configure(module, so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "barrelCluster", barrels);
                });
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
                Debug.Log("[CIWS] 외형·피벗 교체 완료. 기존 전투 수치와 VFX 설정은 유지했습니다.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        /// <summary>레이더. RadarPivot만 회전시킨다(받침과 마스트는 고정).</summary>
        private static GameObject BuildArtRadar(string dir)
        {
            return BuildArtModule<RadarModule>("MOD_Radar", "MOD_Radar", dir, (root, model) =>
            {
                var pivot = FindDeep(model, "RadarPivot");
                if (pivot == null) Debug.LogError("[Setup] 레이더 모델에서 RadarPivot을 찾지 못했습니다.");

                Configure(root.GetComponent<RadarModule>(), so =>
                {
                    Set(so, "antenna", pivot);
                    Set(so, "antennaRpm", 14f);
                });
            });
        }

        /// <summary>VLS. 모델에 들어있는 LaunchPoint 16개를 그대로 발사 지점으로 쓴다.</summary>
        private static GameObject BuildArtVls(GameObject missile, GameObject interceptor, GameObject charge, string dir)
        {
            return BuildArtModule<VlsModule>("MOD_Vls", "MOD_VLS", dir, (root, model) =>
            {
                var points = new List<Transform>();
                for (int i = 1; i <= 16; i++)
                {
                    var t = FindDeep(model, $"LaunchPoint_{i:00}");
                    if (t != null) points.Add(t);
                }

                if (points.Count == 0)
                    Debug.LogError("[Setup] VLS 모델에서 LaunchPoint를 찾지 못했습니다.");

                // 셀 덮개 열림 연출(2026-10-08, 8셀 4×2): 덮개마다 바깥쪽 긴 변을 경첩으로 잡는다
                var lids = new List<Transform>();
                var stripes = new List<Transform>();
                for (int i = 1; i <= points.Count; i++)
                {
                    lids.Add(FindDeep(model, $"VLSHatch_{i:00}"));
                    stripes.Add(FindDeep(model, $"HatchWarning_{i:00}"));
                }
                ComputeVlsHinges(root.transform, lids, out var hingePoints, out var hingeAxes);
                if (hingePoints != null)
                    AddVlsHingeGeometry(root.transform, lids, hingePoints, hingeAxes);

                Configure(root.GetComponent<VlsModule>(), so =>
                {
                    Set(so, "missilePrefab", missile);
                    Set(so, "interceptorPrefab", interceptor);
                    Set(so, "aswTorpedoPrefab", charge);
                    SetArray(so, "hatches", points.ToArray());
                    if (hingePoints != null)
                    {
                        SetArray(so, "hatchLids", lids.ToArray());
                        SetArray(so, "hatchStripes", stripes.ToArray());
                        SetVectors(so, "hingePoints", hingePoints);
                        SetVectors(so, "hingeAxes", hingeAxes);
                    }
                });
            });
        }

        /// <summary>
        /// VLS 덮개 경첩(모듈 기준). 덮개 중심이 모두 같은 거리(±0.42)에 놓인 수평축이 열 방향이고,
        /// 경첩은 그 축으로 바깥쪽 변, 회전축은 다른 수평축(행 방향)이다. 덮개를 하나라도 못 찾으면 null(연출 없이 바로 쏜다).
        /// </summary>
        private static void ComputeVlsHinges(Transform root, List<Transform> lids, out Vector3[] points, out Vector3[] axes)
        {
            points = axes = null;
            if (lids.Count == 0 || lids.Exists(l => l == null)) { Debug.LogWarning("[Setup] VLS 덮개(VLSHatch_xx)를 찾지 못해 열림 연출 없이 만듭니다."); return; }
            var bounds = lids.ConvertAll(l => l.GetComponent<Renderer>().bounds);
            Vector3 center = root.position;
            // 수평축 둘 중 덮개 중심 거리(절댓값)가 고른 쪽 = 열 방향
            float Spread(Vector3 axis)
            {
                float min = float.MaxValue, max = 0f;
                foreach (var b in bounds) { float d = Mathf.Abs(Vector3.Dot(b.center - center, axis)); min = Mathf.Min(min, d); max = Mathf.Max(max, d); }
                return max - min;
            }
            Vector3 col = Spread(Vector3.right) <= Spread(Vector3.forward) ? Vector3.right : Vector3.forward;
            Vector3 row = col == Vector3.right ? Vector3.forward : Vector3.right;
            points = new Vector3[lids.Count];
            axes = new Vector3[lids.Count];
            for (int i = 0; i < lids.Count; i++)
            {
                var b = bounds[i];
                float side = Mathf.Sign(Vector3.Dot(b.center - center, col));
                Vector3 edge = b.center + col * (side * Vector3.Dot(b.extents, col));
                points[i] = root.InverseTransformPoint(edge);
                axes[i] = root.InverseTransformDirection(row);
            }
        }

        /// <summary>
        /// Visible hinge hardware for every hatch. The shaft and two bearing blocks stay on the
        /// launcher; the two leaves are children of the lid and follow its existing hinge animation.
        /// Positions derive from the same pivot/bounds used by VlsModule, so art and motion agree.
        /// </summary>
        private static void AddVlsHingeGeometry(Transform root, List<Transform> lids, Vector3[] points, Vector3[] axes)
        {
            var steel = CreateMaterial("vls_hinge_steel", new Color(.13f,.17f,.2f), .65f, .38f);
            var bearing = CreateMaterial("vls_hinge_bearing", new Color(.37f,.43f,.47f), .45f, .34f);
            for(int i=0;i<lids.Count;i++)
            {
                var lid=lids[i];
                var renderer=lid.GetComponent<Renderer>();
                if(renderer==null) throw new System.InvalidOperationException($"VLS lid {i+1} has no renderer");
                Vector3 axis=axes[i].normalized;
                var b=renderer.bounds;
                float length=2f*Vector3.Dot(b.extents,new Vector3(Mathf.Abs(axis.x),Mathf.Abs(axis.y),Mathf.Abs(axis.z)));
                if(length<.15f || length>.8f) throw new System.InvalidOperationException($"VLS lid {i+1} hinge span {length:F3} is outside its cell");
                Vector3 center=points[i];
                var shaft=Primitive($"VLSHingeShaft_{i+1:00}",PrimitiveType.Cylinder,new Vector3(.062f,length*.5f,.062f),steel,root);
                shaft.transform.localPosition=center;
                shaft.transform.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
                for(int end=-1;end<=1;end+=2)
                {
                    Vector3 p=center+axis*(end*length*.44f);
                    var cap=Primitive($"VLSHingeBearing_{i+1:00}_{(end<0?"A":"B")}",PrimitiveType.Cylinder,
                        new Vector3(.10f,.024f,.10f),bearing,root);
                    cap.transform.localPosition=p;
                    cap.transform.localRotation=shaft.transform.localRotation;
                    var foot=Primitive($"VLSHingeFoot_{i+1:00}_{(end<0?"A":"B")}",PrimitiveType.Cube,
                        new Vector3(.11f,.095f,.12f),bearing,root);
                    foot.transform.localPosition=p+Vector3.down*.055f;
                    var leaf=Primitive($"VLSHingeLeaf_{i+1:00}_{(end<0?"A":"B")}",PrimitiveType.Cube,
                        new Vector3(.075f,.025f,.11f),steel,root);
                    leaf.transform.localPosition=p+Vector3.up*.028f;
                    leaf.transform.SetParent(lid,true);
                }
            }
            Debug.Log($"[VLS] Added {lids.Count} visible hinge shafts, bearing pairs and lid-mounted leaves.");
        }

        private static void SetVectors(SerializedObject so, string field, Vector3[] values)
        {
            var prop = so.FindProperty(field);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).vector3Value = values[i];
        }

        /// <summary>
        /// VLS만 다시 만든다(2026-10-08, 8셀 모델·덮개 열림 연출). 모델 임포트 설정을 맞추고 MOD_Vls 프리팹을 새로 저장한 뒤
        /// mod_vls 데이터가 새 프리팹을 가리키게 한다. 다른 프리팹·씬·데이터는 건드리지 않는다.
        /// </summary>
        public static void RebuildVlsOnly()
        {
            PrepareArtMaterials("MOD_VLS", "MIS_PlayerVls");
            var r = Load();
            string modDir = $"{Root}/Prefabs/Modules";
            string fxDir = $"{Root}/Prefabs/Projectiles";
            r.MissilePlayer = BuildVlsMissile(r.FxGunFlash, fxDir) ?? r.MissilePlayer;
            var vls = BuildArtVls(r.MissilePlayer, r.MissileSam, r.AswTorpedo, modDir);
            if (vls == null) { Debug.LogError("[VLS] MOD_VLS 모델이 없어 다시 만들지 못했습니다."); return; }
            var def = AssetDatabase.LoadAssetAtPath<Game.Modules.ModuleDefinition>($"{Root}/Data/Modules/mod_vls.asset");
            if (def != null) Configure(def, so => Set(so, "prefab", vls));
            AssetDatabase.SaveAssets();
            Debug.Log("[VLS] 8셀 모델·덮개 열림 연출로 MOD_Vls 프리팹(+ 축소한 VLS 미사일)을 다시 만들었습니다.");
        }

        /// <summary>
        /// 아트 유도로켓 발사기. TurretPivot/ElevationPivot/Muzzle과 LaunchPoint_01~ 을 쓴다.
        /// 발사 소켓은 Blender 축이라 회전을 믿지 않고, 발사체 방향은 ElevationPivot → Muzzle로 잡는다.
        /// </summary>
        private static GameObject BuildArtRocketLauncher(GameObject rocket, string dir)
        {
            return BuildArtModule<GuidedRocketModule>("MOD_RocketLauncher", "MOD_RocketLauncher", dir, (root, model) =>
            {
                var turret = FindDeep(model, "TurretPivot");
                var elevation = FindDeep(model, "ElevationPivot");
                var muzzle = FindDeep(model, "Muzzle");
                if (turret == null || elevation == null || muzzle == null)
                {
                    Debug.LogError("[Setup] 유도로켓 발사기 모델에서 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.");
                    return;
                }

                var points = new List<Transform>();
                for (int i = 1; i <= 16; i++)
                {
                    var t = FindDeep(model, $"LaunchPoint_{i:00}");
                    if (t != null) points.Add(t);
                }
                if (points.Count == 0) points.Add(muzzle);

                Configure(root.GetComponent<GuidedRocketModule>(), so =>
                {
                    Set(so, "weapon.turret", turret);
                    Set(so, "weapon.elevationPivot", elevation);
                    Set(so, "weapon.muzzle", muzzle);
                    Set(so, "weapon.minElevation", 0f);
                    Set(so, "weapon.maxElevation", 35f);
                    Set(so, "weapon.aimTolerance", 12f);
                    Set(so, "rocketPrefab", rocket);
                    SetArray(so, "launchPoints", points.ToArray());
                });
            });
        }

        /// <summary>아트 대잠 폭뢰 발사기. LaunchPoint_01~ 을 발사 지점으로 쓴다(방향은 코드가 계산).</summary>
        private static GameObject BuildArtAswLauncher(GameObject charge, string dir)
        {
            return BuildArtModule<AswLauncherModule>("MOD_AswLauncher", "MOD_AswLauncher", dir, (root, model) =>
            {
                var points = new List<Transform>();
                for (int i = 1; i <= 16; i++)
                {
                    var t = FindDeep(model, $"LaunchPoint_{i:00}");
                    if (t != null) points.Add(t);
                }
                if (points.Count == 0) Debug.LogWarning("[Setup] 대잠 발사기 모델에 LaunchPoint가 없어 모듈 중심에서 발사합니다.");

                Configure(root.GetComponent<AswLauncherModule>(), so =>
                {
                    Set(so, "chargePrefab", charge);
                    SetArray(so, "launchPoints", points.ToArray());
                });
            });
        }

        private static GameObject BuildArtDecoyLauncher(GameObject decoy, string dir)
        {
            return BuildArtModule<DecoyLauncherModule>("MOD_DecoyLauncher", "MOD_DecoyLauncher", dir,
                (root, model) =>
                {
                    var launchPoint = FindDeep(model, "LaunchPoint");
                    if (launchPoint == null)
                        Debug.LogError("[Setup] 기만체 모델에서 LaunchPoint를 찾지 못했습니다.");

                    Configure(root.GetComponent<DecoyLauncherModule>(), so =>
                    {
                        Set(so, "decoyPrefab", decoy);
                        Set(so, "launchPoint", launchPoint);
                        Set(so, "decoysPerShot", 2);
                    });
                });
        }

        private static GameObject BuildArtHelicopter(GameObject rocket, GameObject depthCharge, string dir)
        {
            return BuildArtModule<AswHelicopter>("HEL_Asw", "HEL_Asw", dir, (root, model) =>
            {
                // 갑판에 서 있던 헬기와 같은 크기로 뜬다. 이륙 순간 커져 보이지 않게.
                FitToFootprint(model.parent, ParkedHelicopterFootprint);

                var rotor = FindDeep(model, "Rotor");
                var tailRotor = FindDeep(model, "TailRotor");
                if (rotor == null || tailRotor == null)
                    Debug.LogError("[Setup] 헬기 모델에서 Rotor 또는 TailRotor를 찾지 못했습니다.");

                Configure(root.GetComponent<AswHelicopter>(), so =>
                {
                    Set(so, "rotor", rotor);
                    Set(so, "tailRotor", tailRotor);
                    Set(so, "rocketPrefab", rocket);
                    Set(so, "depthChargePrefab", depthCharge);
                });
            });
        }

        /// <summary>갑판 헬기가 차지할 최대 가로·세로(m). 칸(2m) 안의 착함 패드(1.75m)에 들어가야 한다.</summary>
        private const float ParkedHelicopterFootprint = 1.6f;

        private static GameObject BuildArtHeliDeck(GameObject helicopterPrefab, string dir)
        {
            var deckAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/MOD_HeliDeck.fbx");
            var helicopterAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/HEL_Asw.fbx");
            if (deckAsset == null || helicopterAsset == null) return null;

            var root = new GameObject("MOD_HeliDeck");
            var deck = AttachArtModel(deckAsset, root.transform);

            var landingMarker = FindDeep(deck, "LandingSpot");
            if (landingMarker == null)
            {
                Debug.LogError("[Setup] 헬기데크 모델에서 LandingSpot을 찾지 못했습니다.");
                Object.DestroyImmediate(root);
                return null;
            }

            // FBX 안쪽 노드는 임포트 배율·축 회전을 품고 있다. 거기에 자식을 달면 그 보정을 한 번 더 받아
            // 헬기가 수십 배로 커지고 옆으로 눕는다. 위치만 읽어 프리팹 루트 아래에 깨끗한 착함점을 만든다.
            var spot = new GameObject("HeliSpot").transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = root.transform.InverseTransformPoint(landingMarker.position);
            spot.localRotation = Quaternion.identity;

            // 출격용 헬기와 같은 FBX를 패드 크기에 맞게 줄여 세워 둔다.
            var parkedRoot = new GameObject("ParkedHelicopter");
            parkedRoot.transform.SetParent(spot, false);
            AttachArtModel(helicopterAsset, parkedRoot.transform);
            FitToFootprint(parkedRoot.transform, ParkedHelicopterFootprint);

            var parkedRotor = FindDeep(parkedRoot.transform, "Rotor");
            var comp = root.AddComponent<HelicopterDeckModule>();
            Configure(comp, so =>
            {
                Set(so, "helicopterPrefab", helicopterPrefab);
                Set(so, "landingSpot", spot);
                Set(so, "parkedHelicopter", parkedRoot);
                Set(so, "parkedRotor", parkedRotor);
            });

            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 렌더러 경계를 실제로 재서 가로·세로 중 긴 쪽이 maxSize가 되도록 균일하게 줄인다.
        /// 모델 파일의 단위나 크기가 바뀌어도 칸을 넘치지 않는다. 바닥은 부모 원점에 맞춘다.
        /// </summary>
        private static void FitToFootprint(Transform target, float maxSize)
        {
            target.localScale = Vector3.one;

            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest < 0.0001f) return;

            float scale = maxSize / longest;
            target.localScale = Vector3.one * scale;

            // 스케일 후 바닥이 착함점 높이에 오도록 올리거나 내린다
            float bottom = (bounds.min.y - target.position.y) * scale;
            target.localPosition -= new Vector3(0f, bottom, 0f);
        }

        // -------------------------------------------------------- 아트 모델 적

        /// <summary>
        /// 아트 모델을 쓴 적 프리팹. 모델이 없으면 null을 돌려주고 그레이박스로 되돌아간다.
        ///
        /// 새 FBX는 Blender +Y를 Unity +Z로 내보냈으므로 추가 방향 보정이 필요 없다.
        /// 크기는 플레이어 함선(20m)을 기준으로 맞춘 배율을 곱한다.
        /// </summary>
        private static GameObject BuildArtEnemy<T>(string prefabName, string modelName,
                                                   float scale, float modelY, Vector3 colliderSize,
                                                   string dir, System.Action<GameObject, T> extra)
            where T : EnemyController
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{modelName}.fbx");
            if (model == null) return null;

            var root = new GameObject(prefabName);

            // 임포트 보정(축 변환·단위)이 들어있는 모델 트랜스폼은 그대로 두고,
            // 크기·잠항 높이·방향 보정을 부모 피벗에 건다.
            var pivot = new GameObject("ModelPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, modelY, 0f);
            pivot.localRotation = ArtForwardFix;
            pivot.localScale = Vector3.one * scale;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Model";
            instance.transform.SetParent(pivot, false);

            // 포탄 판정(SphereCast)은 트리거도 잡는다. 물리로 밀어내면 서로 엉킨다.
            var col = root.AddComponent<BoxCollider>();
            col.size = colliderSize;
            col.isTrigger = true;

            var comp = root.AddComponent<T>();
            extra?.Invoke(root, comp);

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        // ------------------------------------------------------------------ 발사체

        private static GameObject BuildProjectile(string name, Material mat, int hitMask, string dir,
                                                  Vector3? size = null, System.Action<SerializedObject> extra = null)
        {
            var go = Primitive(name, PrimitiveType.Cube, size ?? new Vector3(0.12f, 0.12f, 0.6f), mat);
            var p = go.AddComponent<Projectile>();
            Configure(p, so =>
            {
                Set(so, "lifeTime", 3f);
                Set(so, "hitRadius", 0.35f);
                Set(so, "hitMask", hitMask);
                extra?.Invoke(so);
            });
            return SavePrefab(go, dir);
        }

        /// <summary>미사일 외형 수치.</summary>
        /// <summary>
        /// VLS 대함미사일. 굵고 긴 짙은 회색 동체 + 노란 띠 + 큰 꼬리 날개, 굵고 오래 남는 회색 연기, 낮은 발사음.
        /// 2026-10-08: VLS 셀(정사각형 0.36m)보다 굵어 보여 모델을 0.72배로 줄였다(길이 2.1 → 1.5m, 지름 0.36 → 0.26m).
        /// </summary>
        private static GameObject BuildVlsMissile(GameObject blast, string fxDir)
        {
            var mVlsBody = CreateMaterial("missile_vls_body", new Color(0.36f, 0.40f, 0.44f), 0.3f, 0.35f);
            var mVlsBand = CreateMaterial("missile_vls_band", new Color(0.95f, 0.75f, 0.10f));
            var mGun = CreateMaterial("gun", new Color(0.18f, 0.19f, 0.21f), 0.8f, 0.4f);
            var mFlame = CreateMaterial("flame", new Color(1f, 0.6f, 0.2f));
            const float scale = 0.72f;
            return BuildMissileProjectile("MIS_PlayerVls", LayerPlayerMissile, fxDir,
                new MissileLook(2.1f * scale, 0.36f * scale, mVlsBody, mGun, mVlsBand, mFlame,
                                trailTime: 1.6f, trailEndWidth: 7f, smokePerMeter: 6f,
                                trailColor: new Color(0.55f, 0.55f, 0.57f), modelScale: scale), blast, so =>
                {
                    Set(so, "launchPitch", 0.8f);
                    Set(so, "profile", (int)MissileProfile.PopUp);
                    Set(so, "speed", 30f);
                    Set(so, "turnRateDegPerSec", 140f);
                    Set(so, "lifeTime", 14f);
                    Set(so, "hitRadius", 1.4f);
                    Set(so, "damage", 30f);
                    Set(so, "maxHp", 1f);
                    Set(so, "isThreat", false);
                });
        }

        private readonly struct MissileLook
        {
            public readonly float Length, Diameter;
            /// <summary>아트 모델 배율(모델 축 보정 위의 피벗에 건다). 1 = 원래 크기.</summary>
            public readonly float ModelScale;
            public readonly Material Body, Nose, Band, Flame;

            /// <summary>연기 꼬리 지속 시간, 끝 폭(지름 배수), 이동 1m당 연기 뭉치 수.</summary>
            public readonly float TrailTime, TrailEndWidth, SmokePerMeter;
            public readonly Color TrailColor;

            /// <summary>동체 중간에 긴 조종 날개(요격미사일). 끄면 꼬리 날개만 크게(대함미사일).</summary>
            public readonly bool MidFins;

            public MissileLook(float length, float diameter, Material body, Material nose, Material band, Material flame,
                               float trailTime = -1f, float trailEndWidth = 5f, float smokePerMeter = 5f,
                               Color? trailColor = null, bool midFins = false, float modelScale = 1f)
            {
                Length = length; Diameter = diameter; Body = body; Nose = nose; Band = band; Flame = flame;
                ModelScale = modelScale;
                TrailTime = trailTime > 0f ? trailTime : (length > 1.2f ? 0.9f : 0.6f);
                TrailEndWidth = trailEndWidth;
                SmokePerMeter = smokePerMeter;
                TrailColor = trailColor ?? new Color(0.7f, 0.7f, 0.72f);
                MidFins = midFins;
            }
        }

        /// <summary>
        /// 미사일 프리팹. 아트 모델(`Art/Models/{name}.fbx`, 꼬리에 `Exhaust` 소켓)이 있으면 그것을,
        /// 없으면 동체·탄두·띠·날개 그레이박스를 쓴다. 꼬리에 불꽃과 추진 연기(꼬리선 + 연기 뭉치)를 단다.
        /// 원점이 동체 중앙, +Z가 비행 방향.
        /// </summary>
        private static GameObject BuildMissileProjectile(string name, int layer, string dir, MissileLook look,
                                                         GameObject blast, System.Action<SerializedObject> config)
        {
            var root = new GameObject(name);
            float tailZ = -look.Length * 0.5f;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelDir}/{name}.fbx");
            if (model != null)
            {
                var art = AttachArtModel(model, root.transform);
                art.parent.localScale = Vector3.one * look.ModelScale;   // 모델 축 보정은 건드리지 않고 피벗에 배율
                var socket = FindDeep(art, "Exhaust");
                if (socket != null) tailZ = root.transform.InverseTransformPoint(socket.position).z;
            }
            else
            {
                BuildMissileGreybox(root.transform, look);
            }

            var flame = Primitive("Flame", PrimitiveType.Capsule,
                                  new Vector3(look.Diameter * 0.6f, look.Length * 0.07f, look.Diameter * 0.6f), look.Flame, root.transform);
            flame.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flame.transform.localPosition = new Vector3(0f, 0f, tailZ - look.Length * 0.05f);

            var exhaust = new GameObject("Exhaust").transform;
            exhaust.SetParent(root.transform, false);
            exhaust.localPosition = new Vector3(0f, 0f, tailZ - look.Length * 0.08f);

            var trail = exhaust.gameObject.AddComponent<TrailRenderer>();
            trail.time = look.TrailTime;
            trail.minVertexDistance = 0.2f;
            trail.widthCurve = AnimationCurve.Linear(0f, look.Diameter * 1.2f, 1f, look.Diameter * look.TrailEndWidth);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.material = CreateVertexColorMaterial("wake");
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.85f), 0f), new GradientColorKey(look.TrailColor, 1f) },
                      new[] { new GradientAlphaKey(0.75f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;

            var smoke = look.SmokePerMeter > 0f ? AddParticles(exhaust, "Smoke", CreateSoftParticleMaterial("fx_water"), 90) : null;
            if (smoke != null) ConfigureExhaustSmoke(smoke, look);

            // CIWS 탄·요격 파편은 물리 스윕으로 맞힌다. 충돌체가 없으면 탄이 미사일을 그대로 통과한다.
            // 동체를 따라 눕힌 캡슐(트리거) + 트랜스폼으로 움직이므로 키네마틱 강체.
            var col = root.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.direction = 2;   // Z축
            col.radius = Mathf.Max(look.Diameter, 0.3f);
            col.height = Mathf.Max(look.Length, col.radius * 2f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var m = root.AddComponent<Missile>();
            Configure(m, so =>
            {
                config(so);
                Set(so, "exhaustTrail", trail);
                if (smoke != null) Set(so, "exhaustSmoke", smoke);
                Set(so, "blastEffect", blast);
            });

            SetLayerRecursive(root, layer);
            return SavePrefab(root, dir);
        }

        private static void ConfigureExhaustSmoke(ParticleSystem smoke, MissileLook look)
        {
            smoke.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 꼬리 쪽으로 뿜는다
            var sm = smoke.main;
            sm.loop = true;
            sm.duration = 5f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            sm.startSize = new ParticleSystem.MinMaxCurve(look.Diameter * 2.5f, look.Diameter * 4f);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.85f, 0.85f, 0.5f), new Color(0.65f, 0.65f, 0.68f, 0.4f));
            var se = smoke.emission;
            se.rateOverDistance = look.SmokePerMeter;
            var ss = smoke.shape;
            ss.shapeType = ParticleSystemShapeType.Cone;
            ss.angle = 8f;
            ss.radius = look.Diameter * 0.3f;
            FadeOut(smoke, 1f, 3f);
        }

        /// <summary>동체(원통) + 뾰족한 탄두(늘린 구) + 색 띠 + 십자 날개.</summary>
        private static void BuildMissileGreybox(Transform root, MissileLook look)
        {
            float L = look.Length, d = look.Diameter;

            var body = Primitive("Body", PrimitiveType.Cylinder, new Vector3(d, L * 0.4f, d), look.Body, root);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localPosition = new Vector3(0f, 0f, -L * 0.1f);

            var nose = Primitive("Nose", PrimitiveType.Sphere, new Vector3(d, d, L * 0.4f), look.Nose, root);
            nose.transform.localPosition = new Vector3(0f, 0f, L * 0.3f);

            var band = Primitive("Band", PrimitiveType.Cylinder, new Vector3(d * 1.06f, L * 0.03f, d * 1.06f), look.Band, root);
            band.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            band.transform.localPosition = new Vector3(0f, 0f, L * 0.12f);

            // 대함미사일은 꼬리 날개가 크고, 요격미사일은 몸통 가운데 긴 날개 + 작은 꼬리 날개
            float tailSpan = look.MidFins ? 1.8f : 2.8f;
            var finH = Primitive("Fins_H", PrimitiveType.Cube, new Vector3(d * tailSpan, d * 0.12f, L * 0.18f), look.Nose, root);
            finH.transform.localPosition = new Vector3(0f, 0f, -L * 0.4f);
            var finV = Primitive("Fins_V", PrimitiveType.Cube, new Vector3(d * 0.12f, d * tailSpan, L * 0.18f), look.Nose, root);
            finV.transform.localPosition = new Vector3(0f, 0f, -L * 0.4f);

            if (!look.MidFins) return;
            var midH = Primitive("MidFins_H", PrimitiveType.Cube, new Vector3(d * 2.2f, d * 0.1f, L * 0.3f), look.Band, root);
            midH.transform.localPosition = new Vector3(0f, 0f, L * 0.02f);
            var midV = Primitive("MidFins_V", PrimitiveType.Cube, new Vector3(d * 0.1f, d * 2.2f, L * 0.3f), look.Band, root);
            midV.transform.localPosition = new Vector3(0f, 0f, L * 0.02f);
        }

        /// <summary>
        /// 수중 폭발 물기둥. 위로 치솟았다 떨어지는 물보라와, 수면에 번지는 흰 거품 고리.
        /// 파티클은 풀에서 꺼낼 때마다 PooledEffect가 처음부터 다시 튼다.
        /// </summary>
        private static GameObject BuildWaterBlast(string dir)
        {
            var root = new GameObject("FX_WaterBlast");
            var mat = CreateSoftParticleMaterial("fx_water");

            // --- 물보라 기둥
            var spray = AddParticles(root.transform, "Spray", mat, 220);
            spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // 원뿔을 위로
            var main = spray.main;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.gravityModifier = 1.7f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.95f),
                                                                new Color(0.75f, 0.88f, 1f, 0.85f));
            var em = spray.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70), new ParticleSystem.Burst(0.08f, 40) });
            var shape = spray.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.7f;
            FadeOut(spray, 0.7f, 1.6f);

            // --- 수면 거품 고리
            var foam = AddParticles(root.transform, "Foam", mat, 4);
            var foamMain = foam.main;
            foamMain.duration = 0.1f;
            foamMain.startLifetime = 1.4f;
            foamMain.startSpeed = 0f;
            foamMain.startSize = 2.5f;
            foamMain.startColor = new Color(1f, 1f, 1f, 0.7f);
            var foamEm = foam.emission;
            foamEm.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var foamShape = foam.shape;
            foamShape.enabled = false;
            foam.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            foam.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            FadeOut(foam, 1f, 4f);

            var fx = root.AddComponent<PooledEffect>();
            Configure(fx, so => Set(so, "lifeTime", 1.8f));
            return SavePrefab(root, dir);
        }

        private static ParticleSystem AddParticles(Transform parent, string name, Material mat, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0f;   // 버스트로만 뿜는다

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        /// <summary>수명 동안 커지면서 투명해진다.</summary>
        private static void FadeOut(ParticleSystem ps, float startScale, float endScale)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            color.color = g;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, startScale, 1f, endScale));
        }

        /// <summary>작은 포연. 기관포·CIWS가 몇 발마다 뿜는다. 작은 섬광 + 옅은 연기.</summary>
        private static GameObject BuildMuzzleSmoke(string dir)
        {
            var root = new GameObject("FX_MuzzleSmoke");
            var mat = CreateSoftParticleMaterial("fx_water");

            var flash = AddParticles(root.transform, "Flash", mat, 4);
            var fm = flash.main;
            fm.duration = 0.05f;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            fm.startSpeed = 0.5f;
            fm.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            fm.startColor = new Color(1f, 0.8f, 0.4f, 1f);
            var fe = flash.emission;
            fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });
            var fs = flash.shape;
            fs.enabled = false;

            var smoke = AddParticles(root.transform, "Smoke", mat, 8);
            var sm = smoke.main;
            sm.duration = 0.1f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            sm.gravityModifier = -0.04f;
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.8f, 0.8f, 0.45f), new Color(0.6f, 0.6f, 0.62f, 0.35f));
            var se = smoke.emission;
            se.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });
            var ss = smoke.shape;
            ss.shapeType = ParticleSystemShapeType.Sphere;
            ss.radius = 0.12f;
            FadeOut(smoke, 1f, 2.5f);

            var fx = root.AddComponent<PooledEffect>();
            Configure(fx, so => Set(so, "lifeTime", 1.1f));
            return SavePrefab(root, dir);
        }

        /// <summary>재밍 전파. 함선 주위로 수평 고리가 크게 퍼진다.</summary>
        /// <summary>
        /// 연막. 함선에 붙어 켜져 있는 동안 월드 공간에 큰 연기 뭉치를 뿜어 배 뒤로 길게 남긴다.
        /// 풀 이펙트가 아니라 SmokeScreen이 켜고 끈다.
        /// </summary>
        private static GameObject BuildSmokeScreen(string dir)
        {
            var root = new GameObject("FX_SmokeScreen");
            var ps = AddParticles(root.transform, "Smoke", CreateSoftParticleMaterial("fx_water"), 500);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.gravityModifier = -0.02f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.86f, 0.88f, 0.8f),
                                                                new Color(0.68f, 0.7f, 0.73f, 0.65f));
            var em = ps.emission;
            em.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 3f;
            FadeOut(ps, 0.6f, 2.2f);
            ps.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            return SavePrefab(root, dir);
        }

        /// <summary>착탄 경고 원: 반투명 붉은 바깥 원(착탄 반경) + 시간이 지나며 차오르는 안쪽 원.</summary>
        private static GameObject BuildImpactWarning(string dir)
        {
            var root = new GameObject("FX_ImpactWarning");
            var outer = Primitive("Outer", PrimitiveType.Cylinder, new Vector3(1f, 0.01f, 1f),
                                  CreateTransparentMaterial("warning_outer", new Color(1f, 0.2f, 0.15f, 0.22f)), root.transform);
            var inner = Primitive("Inner", PrimitiveType.Cylinder, new Vector3(0.01f, 0.012f, 0.01f),
                                  CreateTransparentMaterial("warning_inner", new Color(1f, 0.35f, 0.2f, 0.45f)), root.transform);
            inner.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var marker = root.AddComponent<ImpactMarker>();
            Configure(marker, so =>
            {
                Set(so, "outer", outer.transform);
                Set(so, "inner", inner.transform);
            });
            return SavePrefab(root, dir);
        }

        /// <summary>전함 주포탄: 어두운 포탄 + 짧은 연기 꼬리. 포물선으로 날아가 착탄점에 물기둥.</summary>
        private static GameObject BuildBossShell(Material shellMat, GameObject warning, GameObject blast, string dir)
        {
            var root = new GameObject("PRJ_BossShell");
            Primitive("Shell", PrimitiveType.Capsule, new Vector3(0.35f, 0.45f, 0.35f), shellMat, root.transform)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.35f, 1f, 0f);
            trail.material = CreateVertexColorMaterial("wake");
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.7f, 0.35f), 0f), new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;

            var shell = root.AddComponent<ArtilleryShell>();
            Configure(shell, so =>
            {
                Set(so, "warningMarker", warning);
                Set(so, "impactEffect", blast);
            });
            return SavePrefab(root, dir);
        }

        /// <summary>항공전함 소켓 연결. 아트 모델은 이름으로, 그레이박스는 만들 때 붙인 이름 그대로 찾는다.</summary>
        private static void WireHybridBoss(Transform root, HybridBattleshipBoss comp, GameObject shell, GameObject flash, GameObject aaTracer)
        {
            var muzzles = new List<Transform>();
            var missiles = new List<Transform>();
            for (int i = 1; i <= 8; i++)
            {
                var m = FindDeep(root, $"MainGun_Muzzle_{i:00}");
                if (m != null) muzzles.Add(m);
                var l = FindDeep(root, $"MissileLaunchPoint_{i:00}");
                if (l != null) missiles.Add(l);
            }
            // 주포 선회부(v9 모델: MainGun_01/02 아래 MainGun_Elevation_01/02 — 선회만 돌린다)
            var turrets = new List<Transform>();
            for (int i = 1; i <= 4; i++)
            {
                var t = FindDeep(root, $"MainGun_{i:00}");
                if (t != null) turrets.Add(t);
            }
            var deck = FindDeep(root, "FlightDeckLaunch");
            var aa = FindDeep(root, "AAMount");
            var radar = FindDeep(root, "RadarPivot");
            if (muzzles.Count == 0) Debug.LogWarning("[Setup] 항공전함에서 MainGun_Muzzle_01~ 소켓을 찾지 못했습니다(선체 중앙에서 발사).");

            Configure(comp, so =>
            {
                Set(so, "shellPrefab", shell);
                Set(so, "muzzleFlash", flash);
                SetArray(so, "gunMuzzles", muzzles.ToArray());
                SetArray(so, "missileLaunchPoints", missiles.ToArray());
                SetArray(so, "mainTurrets", turrets.ToArray());
                Set(so, "flightDeckLaunch", deck);
                Set(so, "aaMount", aa);
                Set(so, "aaProjectilePrefab", aaTracer);
                if (radar != null) Set(so, "radar", radar);
                // 주포 사격 금지 구역(= 플레이어의 안전 지대)을 수면에 옅은 초록으로
                Set(so, "cutoutZoneMaterial", CreateTransparentMaterial("boss_blindspot", new Color(0.35f, 1f, 0.55f, 0.13f)));
            });
        }

        /// <summary>
        /// 항공전함(그레이박스). 약 32m. 앞: 대함미사일 발사기 2 + 3연장 주포 2기(뒤 포탑이 한 단 높음),
        /// 가운데: 함교탑·연돌·우현 대공포, 뒤: 노란 중심선이 그어진 비행갑판.
        /// </summary>
        private static GameObject BuildHybridBossGreybox(Material hull, Material dark, Material marking,
                                                         GameObject shell, GameObject flash, GameObject aaTracer, string dir)
        {
            var root = new GameObject("ENE_Boss2");
            Transform R = root.transform;

            Primitive("Hull", PrimitiveType.Cube, new Vector3(3.2f, 1.2f, 16f), hull, R);

            // 선수 대함미사일 발사기
            for (int side = -1, n = 1; side <= 1; side += 2, n++)
            {
                var box = Primitive($"MissileLauncher_{n:00}", PrimitiveType.Cube, new Vector3(0.6f, 0.5f, 1.6f), dark, R);
                box.transform.localPosition = new Vector3(side * 0.8f, 0.85f, 6.6f);
                var lp = new GameObject($"MissileLaunchPoint_{n:00}").transform;
                lp.SetParent(R, false);
                lp.localPosition = new Vector3(side * 0.8f, 1.2f, 7.2f);
            }

            // 주포 2기(3연장). 뒤 포탑은 앞 포탑 너머로 쏘도록 한 단 높다.
            int muzzle = 1;
            foreach (var (z, y) in new[] { (4.6f, 0.9f), (2.8f, 1.4f) })
            {
                var turret = new GameObject($"MainGun_{muzzle / 2 + 1:00}").transform;
                turret.SetParent(R, false);
                turret.localPosition = new Vector3(0f, y, z);

                Primitive("Barbette", PrimitiveType.Cylinder, new Vector3(1.6f, 0.2f, 1.6f), hull, turret);
                var house = Primitive("Turret", PrimitiveType.Cube, new Vector3(1.4f, 0.6f, 1.6f), dark, turret);
                house.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                for (int b = -1; b <= 1; b++)
                {
                    var barrel = Primitive($"Barrel_{b + 2}", PrimitiveType.Cube, new Vector3(0.12f, 0.12f, 2.2f), dark, turret);
                    barrel.transform.localPosition = new Vector3(b * 0.35f, 0.45f, 1.7f);
                }
                for (int b = -1; b <= 1; b += 2)
                {
                    var m = new GameObject($"MainGun_Muzzle_{muzzle:00}").transform;
                    m.SetParent(turret, false);
                    m.localPosition = new Vector3(b * 0.35f, 0.45f, 2.8f);
                    muzzle++;
                }
            }

            // 함교탑·마스트·연돌
            var tower = Primitive("BridgeTower", PrimitiveType.Cube, new Vector3(2.0f, 2.8f, 2.0f), hull, R);
            tower.transform.localPosition = new Vector3(0f, 2.0f, 0.8f);
            var mast = Primitive("Mast", PrimitiveType.Cylinder, new Vector3(0.18f, 1.0f, 0.18f), dark, R);
            mast.transform.localPosition = new Vector3(0f, 4.4f, 0.8f);
            var funnel = Primitive("Funnel", PrimitiveType.Cube, new Vector3(1.0f, 1.6f, 1.2f), dark, R);
            funnel.transform.localPosition = new Vector3(0f, 1.6f, -0.9f);

            // 우현 대공포
            var aaBase = Primitive("AAMount_Base", PrimitiveType.Cylinder, new Vector3(0.8f, 0.25f, 0.8f), dark, R);
            aaBase.transform.localPosition = new Vector3(1.1f, 0.85f, -1.8f);
            var aaBarrel = Primitive("AAMount_Barrel", PrimitiveType.Cube, new Vector3(0.1f, 0.1f, 0.9f), dark, R);
            aaBarrel.transform.localPosition = new Vector3(1.1f, 1.2f, -1.4f);
            aaBarrel.transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            var aa = new GameObject("AAMount").transform;
            aa.SetParent(R, false);
            aa.localPosition = new Vector3(1.1f, 1.5f, -1.1f);

            // 비행갑판 + 노란 중심선 + 이륙 지점(갑판 앞끝, 선수 방향)
            var deck = Primitive("FlightDeck", PrimitiveType.Cube, new Vector3(3.8f, 0.2f, 5.8f), dark, R);
            deck.transform.localPosition = new Vector3(0f, 1.3f, -5.1f);
            var line = Primitive("DeckLine", PrimitiveType.Cube, new Vector3(0.12f, 0.22f, 5.4f), marking, R);
            line.transform.localPosition = new Vector3(0f, 1.32f, -5.1f);
            var launch = new GameObject("FlightDeckLaunch").transform;
            launch.SetParent(R, false);
            launch.localPosition = new Vector3(0f, 1.9f, -2.6f);

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(7.2f, 8f, 34f);
            col.center = new Vector3(0f, 2f, 0f);
            col.isTrigger = true;

            // Art 모델이 없어도 외형과 소켓을 같은 비율로 확대한다.
            var visual = new GameObject("ModelPivot").transform;
            visual.SetParent(R, false);
            for (int i = R.childCount - 2; i >= 0; i--)
                R.GetChild(i).SetParent(visual, false);
            visual.localScale = Vector3.one * 2f;

            var comp = root.AddComponent<HybridBattleshipBoss>();
            WireHybridBoss(R, comp, shell, flash, aaTracer);
            Configure(comp, so => Set(so, "cutoutZoneRadius", 36f));

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 제트 배기 꼬리선. 빠르게 지나가는 전투기의 진로가 눈에 남아 공격 진입 방향을 읽을 수 있게 한다.
        /// 모델 노드(Blender 축)가 아니라 프리팹 루트에 붙인다.
        /// </summary>
        private static void AddJetTrail(Transform root, Vector3 localPosition)
        {
            var t = new GameObject("JetTrail").transform;
            t.SetParent(root, false);
            t.localPosition = localPosition;

            var trail = t.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.minVertexDistance = 0.25f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.22f, 1f, 0.02f);
            trail.material = CreateVertexColorMaterial("wake");
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.75f, 0.45f), 0f), new GradientColorKey(new Color(0.75f, 0.75f, 0.78f), 1f) },
                      new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = g;
        }

        /// <summary>전투공격기(그레이박스). 뾰족한 기수 + 후퇴익 + 쌍수직꼬리 + 어두운 캐노피, 기수에 기관포 포구.</summary>
        private static GameObject BuildFighterGreybox(Material body, Material dark, Material glass, GameObject projectile, string dir)
        {
            var root = new GameObject("ENE_Fighter");
            Transform R = root.transform;

            var fuselage = Primitive("Fuselage", PrimitiveType.Capsule, new Vector3(0.45f, 1.6f, 0.4f), body, R);
            fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var nose = Primitive("Nose", PrimitiveType.Sphere, new Vector3(0.3f, 0.26f, 0.9f), dark, R);
            nose.transform.localPosition = new Vector3(0f, 0f, 1.55f);
            var canopy = Primitive("Canopy", PrimitiveType.Sphere, new Vector3(0.3f, 0.25f, 0.7f), glass, R);
            canopy.transform.localPosition = new Vector3(0f, 0.22f, 0.7f);

            for (int side = -1; side <= 1; side += 2)
            {
                // 후퇴익: 바깥쪽이 뒤로 젖혀진다
                var wing = Primitive(side < 0 ? "Wing_L" : "Wing_R", PrimitiveType.Cube, new Vector3(1.5f, 0.06f, 0.8f), body, R);
                wing.transform.localPosition = new Vector3(side * 0.8f, -0.02f, -0.25f);
                wing.transform.localRotation = Quaternion.Euler(0f, side * -28f, 0f);

                var stab = Primitive(side < 0 ? "Stabilizer_L" : "Stabilizer_R", PrimitiveType.Cube, new Vector3(0.6f, 0.05f, 0.4f), body, R);
                stab.transform.localPosition = new Vector3(side * 0.45f, 0f, -1.45f);
                stab.transform.localRotation = Quaternion.Euler(0f, side * -25f, 0f);

                var fin = Primitive(side < 0 ? "Fin_L" : "Fin_R", PrimitiveType.Cube, new Vector3(0.05f, 0.5f, 0.45f), dark, R);
                fin.transform.localPosition = new Vector3(side * 0.22f, 0.3f, -1.3f);
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, side * -12f);
            }

            var nozzle = Primitive("Nozzle", PrimitiveType.Cylinder, new Vector3(0.3f, 0.12f, 0.3f), dark, R);
            nozzle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            nozzle.transform.localPosition = new Vector3(0f, 0f, -1.75f);

            var muzzle = new GameObject("GunMuzzle").transform;
            muzzle.SetParent(R, false);
            muzzle.localPosition = new Vector3(0.2f, -0.05f, 1.9f);
            AddJetTrail(R, new Vector3(0f, 0f, -1.85f));

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2.8f, 0.9f, 3.4f);
            col.isTrigger = true;

            var comp = root.AddComponent<FighterJet>();
            Configure(comp, so =>
            {
                Set(so, "gunMuzzle", muzzle);
                Set(so, "projectilePrefab", projectile);
                Set(so, "cruiseAltitude", 12f);
            });

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        /// <summary>자폭 드론(그레이박스). 삼각 날개 동체 + 붉은 탄두 + 꼬리 프로펠러.</summary>
        private static GameObject BuildDroneGreybox(Material body, Material warhead, Material dark, GameObject blast, string dir)
        {
            var root = new GameObject("ENE_Drone");

            Primitive("Body", PrimitiveType.Cube, new Vector3(0.32f, 0.26f, 1.3f), body, root.transform);
            var wing = Primitive("Wing", PrimitiveType.Cube, new Vector3(2.0f, 0.05f, 0.7f), body, root.transform);
            wing.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            var fin = Primitive("Fin", PrimitiveType.Cube, new Vector3(0.05f, 0.45f, 0.35f), body, root.transform);
            fin.transform.localPosition = new Vector3(0f, 0.22f, -0.5f);
            var nose = Primitive("Warhead", PrimitiveType.Sphere, new Vector3(0.3f, 0.3f, 0.45f), warhead, root.transform);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.65f);
            var prop = Primitive("Prop", PrimitiveType.Cylinder, new Vector3(0.6f, 0.02f, 0.6f), dark, root.transform);
            prop.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            prop.transform.localPosition = new Vector3(0f, 0f, -0.68f);

            // 작고 빨라 맞히기 어려우므로 판정은 몸체보다 넉넉히
            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2.2f, 0.7f, 1.8f);
            col.isTrigger = true;

            var comp = root.AddComponent<KamikazeDrone>();
            Configure(comp, so =>
            {
                Set(so, "blastEffect", blast);
                Set(so, "cruiseAltitude", 6f);
            });

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        /// <summary>정찰기(그레이박스). 쌍발 직선 날개 기체 + 등 위의 회전 레이돔.</summary>
        private static GameObject BuildReconGreybox(Material body, Material sensor, Material dark, string dir)
        {
            var root = new GameObject("ENE_Recon");

            var fuselage = Primitive("Fuselage", PrimitiveType.Capsule, new Vector3(0.5f, 1.6f, 0.5f), body, root.transform);
            fuselage.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var wing = Primitive("Wing", PrimitiveType.Cube, new Vector3(5.0f, 0.08f, 0.8f), body, root.transform);
            wing.transform.localPosition = new Vector3(0f, 0.1f, 0.2f);
            var tail = Primitive("Tailplane", PrimitiveType.Cube, new Vector3(1.8f, 0.06f, 0.5f), body, root.transform);
            tail.transform.localPosition = new Vector3(0f, 0.1f, -1.4f);
            var fin = Primitive("Fin", PrimitiveType.Cube, new Vector3(0.06f, 0.7f, 0.55f), body, root.transform);
            fin.transform.localPosition = new Vector3(0f, 0.4f, -1.4f);

            for (int side = -1; side <= 1; side += 2)
            {
                var engine = Primitive(side < 0 ? "Engine_L" : "Engine_R", PrimitiveType.Cylinder,
                                       new Vector3(0.28f, 0.35f, 0.28f), dark, root.transform);
                engine.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                engine.transform.localPosition = new Vector3(side * 1.1f, 0.05f, 0.35f);
            }

            var strut = Primitive("RadomeStrut", PrimitiveType.Cube, new Vector3(0.1f, 0.35f, 0.3f), dark, root.transform);
            strut.transform.localPosition = new Vector3(0f, 0.4f, -0.3f);
            var radome = new GameObject("RadomePivot").transform;
            radome.SetParent(root.transform, false);
            radome.localPosition = new Vector3(0f, 0.6f, -0.3f);
            var disc = Primitive("Radome", PrimitiveType.Cylinder, new Vector3(1.6f, 0.06f, 1.6f), sensor, radome);
            disc.transform.localPosition = Vector3.zero;
            var stripe = Primitive("RadomeStripe", PrimitiveType.Cube, new Vector3(1.5f, 0.13f, 0.12f), dark, radome);
            stripe.transform.localPosition = Vector3.zero;

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(5.2f, 1.2f, 3.6f);
            col.isTrigger = true;

            var comp = root.AddComponent<ReconAircraft>();
            Configure(comp, so =>
            {
                Set(so, "radome", radome);
                Set(so, "cruiseAltitude", 11f);
            });

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        /// <summary>함선을 중심으로 수면에 번지는 고리 두 겹(재밍 청색, 응급 수리 초록).</summary>
        private static GameObject BuildRingPulse(string name, Color color, string dir)
        {
            var root = new GameObject(name);
            var mat = CreateSoftParticleMaterial("fx_water");

            for (int i = 0; i < 2; i++)
            {
                var ring = AddParticles(root.transform, $"Ring_{i}", mat, 2);
                var rm = ring.main;
                rm.duration = 0.2f;
                rm.startLifetime = 0.9f;
                rm.startSpeed = 0f;
                rm.startSize = 4f;
                rm.startColor = color;
                var re = ring.emission;
                re.SetBursts(new[] { new ParticleSystem.Burst(i * 0.15f, 1) });
                var rs = ring.shape;
                rs.enabled = false;
                ring.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                FadeOut(ring, 1f, 18f);   // 반경 약 35m까지 번진다
            }

            var fx = root.AddComponent<PooledEffect>();
            Configure(fx, so => Set(so, "lifeTime", 1.3f));
            return SavePrefab(root, dir);
        }

        /// <summary>포구 화염과 연기. 76mm 발사와 고폭탄 명중에 함께 쓴다.</summary>
        private static GameObject BuildGunFlash(string dir)
        {
            var root = new GameObject("FX_GunFlash");
            var mat = CreateSoftParticleMaterial("fx_water");   // 부드러운 원형 입자, 색은 파티클이 입힌다

            var flash = AddParticles(root.transform, "Flash", mat, 12);
            var fm = flash.main;
            fm.duration = 0.1f;
            fm.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            fm.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            fm.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f, 1f), new Color(1f, 0.5f, 0.15f, 1f));
            var fe = flash.emission;
            fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 8) });
            var fs = flash.shape;
            fs.shapeType = ParticleSystemShapeType.Sphere;
            fs.radius = 0.15f;
            FadeOut(flash, 1f, 1.8f);

            var smoke = AddParticles(root.transform, "Smoke", mat, 16);
            var sm = smoke.main;
            sm.duration = 0.2f;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            sm.gravityModifier = -0.05f;   // 살짝 떠오른다
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.75f, 0.75f, 0.55f), new Color(0.55f, 0.55f, 0.58f, 0.45f));
            var se = smoke.emission;
            se.SetBursts(new[] { new ParticleSystem.Burst(0.02f, 9) });
            var ss = smoke.shape;
            ss.shapeType = ParticleSystemShapeType.Sphere;
            ss.radius = 0.25f;
            FadeOut(smoke, 1f, 3f);

            var fx = root.AddComponent<PooledEffect>();
            Configure(fx, so => Set(so, "lifeTime", 1.4f));
            return SavePrefab(root, dir);
        }

        /// <summary>적 어뢰. 어두운 몸체에 하얀 항적이 길게 남아 멀리서도 진로가 읽힌다.</summary>
        private static GameObject BuildTorpedo(Material body, GameObject impactEffect, string dir)
        {
            var root = new GameObject("TOR_Enemy");

            var visual = Primitive("Model", PrimitiveType.Capsule, new Vector3(0.28f, 0.55f, 0.28f), body, root.transform);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 1.6f;
            trail.minVertexDistance = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 0.9f, 1f, 0.1f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.material = CreateVertexColorMaterial("wake");
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.8f, 0.9f, 1f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;

            var t = root.AddComponent<Torpedo>();
            Configure(t, so =>
            {
                Set(so, "speed", 16f);   // 최소 사격 거리 40m에서도 움직이는 함선을 따라잡게(2026-10-01)
                Set(so, "lifeTime", 9f);
                Set(so, "hitRadius", 0.8f);
                Set(so, "runDepth", -0.35f);
                Set(so, "hitMask", 1 << LayerPlayerShip);
                Set(so, "wake", trail);
                Set(so, "impactEffect", impactEffect);
            });

            return SavePrefab(root, dir);
        }

        /// <summary>플레이어 폭뢰. 굴러가며 날아가는 짧은 원통.</summary>
        private static GameObject BuildDepthCharge(Material body, GameObject blastEffect, string dir)
        {
            var root = new GameObject("DC_Player");
            var visual = Primitive("Model", PrimitiveType.Cylinder, new Vector3(0.4f, 0.22f, 0.4f), body, root.transform);
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var dc = root.AddComponent<DepthCharge>();
            Configure(dc, so =>
            {
                Set(so, "flightTime", 1.1f);
                Set(so, "arcHeight", 5f);
                Set(so, "sinkTime", 0.9f);   // 입수 물보라와 수중 폭발이 구별되게
                Set(so, "blastRadius", 4.5f);
                Set(so, "blastEffect", blastEffect);
            });

            return SavePrefab(root, dir);
        }

        private static GameObject BuildDecoy(Material mat, string dir)
        {
            var go = Primitive("DEC_Decoy", PrimitiveType.Sphere, Vector3.one * 0.8f, mat);
            var d = go.AddComponent<Decoy>();
            Configure(d, so =>
            {
                Set(so, "lifeTime", 7f);
                Set(so, "lureRadius", 35f);
                Set(so, "lureChance", 0.9f);
                Set(so, "driftSpeed", 3f);
            });
            SetLayerRecursive(go, LayerDecoy);
            return SavePrefab(go, dir);
        }

        /// <summary>
        /// 대잠 헬기. 동체·꼬리·로터를 나눠 로터만 돌 수 있게 한다.
        /// 같은 형태를 갑판 위 주기용으로도 재사용한다.
        /// </summary>
        private static GameObject BuildHelicopterModel(string name, Material body, Material dark,
                                                       out Transform rotor, out Transform tailRotor)
        {
            var root = new GameObject(name);

            var hull = Primitive("Body", PrimitiveType.Cube, new Vector3(0.75f, 0.6f, 1.5f), body, root.transform);
            hull.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var cockpit = Primitive("Cockpit", PrimitiveType.Cube, new Vector3(0.6f, 0.42f, 0.5f), dark, root.transform);
            cockpit.transform.localPosition = new Vector3(0f, 0.5f, 0.8f);

            var boom = Primitive("TailBoom", PrimitiveType.Cube, new Vector3(0.18f, 0.18f, 1.3f), body, root.transform);
            boom.transform.localPosition = new Vector3(0f, 0.52f, -1.2f);

            var fin = Primitive("TailFin", PrimitiveType.Cube, new Vector3(0.08f, 0.5f, 0.35f), body, root.transform);
            fin.transform.localPosition = new Vector3(0f, 0.75f, -1.8f);

            // 착함 스키드
            for (int i = 0; i < 2; i++)
            {
                var skid = Primitive($"Skid_{i}", PrimitiveType.Cube,
                                     new Vector3(0.08f, 0.08f, 1.2f), dark, root.transform);
                skid.transform.localPosition = new Vector3(i == 0 ? -0.35f : 0.35f, 0.1f, 0f);
            }

            // 메인 로터: 마스트 위에서 수평으로 돈다
            var rotorGo = new GameObject("Rotor");
            rotorGo.transform.SetParent(root.transform, false);
            rotorGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            rotor = rotorGo.transform;

            for (int i = 0; i < 2; i++)
            {
                var blade = Primitive($"Blade_{i}", PrimitiveType.Cube,
                                      new Vector3(0.1f, 0.03f, 2.6f), dark, rotor);
                blade.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }

            // 꼬리 로터: 세로로 선 채 돈다
            var tailGo = new GameObject("TailRotor");
            tailGo.transform.SetParent(root.transform, false);
            tailGo.transform.localPosition = new Vector3(0.12f, 0.75f, -1.85f);
            tailRotor = tailGo.transform;

            var tailBlade = Primitive("TailBlade", PrimitiveType.Cube,
                                      new Vector3(0.04f, 0.7f, 0.06f), dark, tailRotor);
            tailBlade.transform.localPosition = Vector3.zero;

            return root;
        }

        private static GameObject BuildHelicopter(Material mat, GameObject rocket, GameObject depthCharge, string dir)
        {
            var dark = CreateMaterial("gun", new Color(0.18f, 0.19f, 0.21f), 0.8f, 0.4f);
            var root = BuildHelicopterModel("HEL_Asw", mat, dark, out var rotor, out var tailRotor);

            var comp = root.AddComponent<AswHelicopter>();
            Configure(comp, so =>
            {
                Set(so, "rotor", rotor);
                Set(so, "tailRotor", tailRotor);
                Set(so, "rocketPrefab", rocket);
                Set(so, "depthChargePrefab", depthCharge);
            });

            return SavePrefab(root, dir);
        }

        private static GameObject BuildCellHighlight(Material mat, string dir)
        {
            var go = Primitive("FX_CellHighlight", PrimitiveType.Cube,
                               new Vector3(Cell * 0.92f, 0.08f, Cell * 0.92f), mat);
            return SavePrefab(go, dir);
        }

        // ---------------------------------------------------------------- 적

        private static GameObject BuildEnemy<T>(string name, Material mat, Vector3 size,
                                                string dir, System.Action<GameObject, T> extra)
            where T : EnemyController
        {
            var root = new GameObject(name);
            var body = Primitive("Model", PrimitiveType.Cube, size, mat, root.transform);
            body.transform.localPosition = Vector3.zero;

            // 포탄 판정(SphereCast)은 트리거도 감지한다. 물리 충돌로 서로 밀어내면
            // 적들이 엉키고 함선 이동까지 방해하므로 트리거로 둔다.
            var col = root.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = true;

            var comp = root.AddComponent<T>();
            extra?.Invoke(root, comp);

            SetLayerRecursive(root, LayerEnemy);
            return SavePrefab(root, dir);
        }

        // --------------------------------------------------------------- 모듈

        /// <summary>단순 상자형 모듈. 자체 Tick이 없는 지원 모듈용.</summary>
        private static GameObject BuildSimpleModule<T>(string name, Material mat, Vector3 size,
                                                       float yOffset, string dir,
                                                       PrimitiveType shape = PrimitiveType.Cube)
            where T : Component
        {
            var root = new GameObject(name);
            var body = Primitive("Model", shape, size, mat, root.transform);
            body.transform.localPosition = new Vector3(0f, yOffset, 0f);

            root.AddComponent<T>();
            return SavePrefab(root, dir);
        }

        /// <summary>회전 포탑 + 포신을 가진 무기 모듈(기관포, CIWS).</summary>
        private static GameObject BuildTurretModule<T>(string name, Material mat, float radius,
                                                       float barrelLength, GameObject projectile, string dir)
            where T : Component
        {
            var root = new GameObject(name);

            var baseRing = Primitive("Base", PrimitiveType.Cylinder, new Vector3(radius * 2f, 0.15f, radius * 2f),
                                     mat, root.transform);
            baseRing.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var turret = new GameObject("Turret").transform;
            turret.SetParent(root.transform, false);
            turret.localPosition = new Vector3(0f, 0.4f, 0f);

            Primitive("House", PrimitiveType.Cube,
                                  new Vector3(radius * 1.8f, 0.5f, radius * 2.2f), mat, turret);

            var barrel = Primitive("Barrel", PrimitiveType.Cylinder,
                                   new Vector3(0.12f, barrelLength * 0.5f, 0.12f), mat, turret);
            barrel.transform.localPosition = new Vector3(0f, 0.05f, barrelLength * 0.5f);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(turret, false);
            muzzle.localPosition = new Vector3(0f, 0.05f, barrelLength);

            var comp = root.AddComponent<T>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "projectilePrefab", projectile);
            });

            return SavePrefab(root, dir);
        }

        private static GameObject BuildRadar(Material hull, Material sensor, string dir)
        {
            var root = new GameObject("MOD_Radar");

            var mast = Primitive("Mast", PrimitiveType.Cylinder, new Vector3(0.15f, 1.2f, 0.15f),
                                 hull, root.transform);
            mast.transform.localPosition = new Vector3(0f, 1.2f, 0f);

            var antenna = new GameObject("Antenna").transform;
            antenna.SetParent(root.transform, false);
            antenna.localPosition = new Vector3(0f, 2.5f, 0f);

            Primitive("Panel", PrimitiveType.Cube, new Vector3(0.1f, 0.7f, 1.6f),
                                  sensor, antenna);

            var comp = root.AddComponent<RadarModule>();
            Configure(comp, so =>
            {
                Set(so, "antenna", antenna);
                Set(so, "antennaRpm", 25f);
            });

            return SavePrefab(root, dir);
        }

        private static GameObject BuildVls(Material hull, Material gun, GameObject missile, GameObject interceptor, GameObject charge, string dir)
        {
            var root = new GameObject("MOD_Vls");

            // 2x2 모듈: 원점은 좌하단 셀이고 ModuleFactory가 중심 오프셋을 더한다
            var deck = Primitive("Deck", PrimitiveType.Cube,
                                 new Vector3(Cell * 1.8f, 0.3f, Cell * 1.8f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var hatches = new Transform[4];
            int i = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    var cellGo = Primitive($"Hatch_{i}", PrimitiveType.Cube,
                                           new Vector3(Cell * 0.7f, 0.25f, Cell * 0.7f), gun, root.transform);
                    cellGo.transform.localPosition = new Vector3(x * Cell * 0.45f, 0.4f, z * Cell * 0.45f);
                    hatches[i] = cellGo.transform;
                    i++;
                }
            }

            var comp = root.AddComponent<VlsModule>();
            Configure(comp, so =>
            {
                Set(so, "missilePrefab", missile);
                Set(so, "interceptorPrefab", interceptor);
                Set(so, "aswTorpedoPrefab", charge);
                SetArray(so, "hatches", hatches);
            });

            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 유도로켓 발사기(그레이박스). 선회 받침 위에 좌우 2개의 4연장 발사관 묶음.
        /// 아트 모델이 오면 BuildArtRocketLauncher가 대신한다.
        /// </summary>
        private static GameObject BuildRocketLauncher(Material hull, Material gun, GameObject rocket, string dir)
        {
            var root = new GameObject("MOD_RocketLauncher");

            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(Cell * 0.85f, 0.2f, Cell * 0.85f),
                                 hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            var turret = new GameObject("TurretPivot").transform;
            turret.SetParent(root.transform, false);
            turret.localPosition = new Vector3(0f, 0.2f, 0f);

            var ring = Primitive("Ring", PrimitiveType.Cylinder, new Vector3(1.1f, 0.12f, 1.1f), gun, turret);
            ring.transform.localPosition = new Vector3(0f, 0.12f, 0f);

            // 발사관을 떠받치는 좌우 기둥
            for (int side = -1; side <= 1; side += 2)
            {
                var post = Primitive($"Post_{(side < 0 ? "L" : "R")}", PrimitiveType.Cube,
                                     new Vector3(0.14f, 0.7f, 0.5f), hull, turret);
                post.transform.localPosition = new Vector3(side * 0.62f, 0.55f, -0.1f);
            }

            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(turret, false);
            elevation.localPosition = new Vector3(0f, 0.85f, 0f);

            var points = new List<Transform>();
            int index = 1;
            for (int side = -1; side <= 1; side += 2)
            {
                var pod = Primitive($"Pod_{(side < 0 ? "L" : "R")}", PrimitiveType.Cube,
                                    new Vector3(0.5f, 0.5f, 1.2f), hull, elevation);
                pod.transform.localPosition = new Vector3(side * 0.3f, 0f, 0f);

                // 2x2 발사관 입구
                for (int row = 0; row < 2; row++)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        var mouth = Primitive($"Tube_{index:00}", PrimitiveType.Cube,
                                              new Vector3(0.18f, 0.18f, 0.04f), gun, elevation);
                        Vector3 pos = new(side * 0.3f + (col - 0.5f) * 0.22f, (row - 0.5f) * 0.22f, 0.61f);
                        mouth.transform.localPosition = pos;

                        var lp = new GameObject($"LaunchPoint_{index:00}").transform;
                        lp.SetParent(elevation, false);
                        lp.localPosition = pos + new Vector3(0f, 0f, 0.1f);
                        points.Add(lp);
                        index++;
                    }
                }
            }

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(elevation, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.7f);

            var comp = root.AddComponent<GuidedRocketModule>();
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "weapon.minElevation", 0f);
                Set(so, "weapon.maxElevation", 35f);
                Set(so, "weapon.aimTolerance", 12f);
                Set(so, "rocketPrefab", rocket);
                SetArray(so, "launchPoints", points.ToArray());
            });

            return SavePrefab(root, dir);
        }

        /// <summary>
        /// 76mm 함포(그레이박스). 둥근 포탑 돔에서 긴 포신이 나온다. 기관포와 같은 조준 계층.
        /// </summary>
        private static GameObject BuildGun76Greybox(Material hull, Material gun, GameObject shell, GameObject flash, string dir)
        {
            var root = new GameObject("MOD_Gun76");

            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(Cell * 0.85f, 0.15f, Cell * 0.85f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.075f, 0f);

            var ring = Primitive("Ring", PrimitiveType.Cylinder, new Vector3(1.3f, 0.1f, 1.3f), gun, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            var turret = new GameObject("TurretPivot").transform;
            turret.SetParent(root.transform, false);
            turret.localPosition = new Vector3(0f, 0.25f, 0f);

            var dome = Primitive("Dome", PrimitiveType.Sphere, new Vector3(1.2f, 0.9f, 1.4f), hull, turret);
            dome.transform.localPosition = new Vector3(0f, 0.35f, -0.1f);

            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(turret, false);
            elevation.localPosition = new Vector3(0f, 0.45f, 0.45f);

            var barrel = Primitive("Barrel", PrimitiveType.Cylinder, new Vector3(0.12f, 0.8f, 0.12f), gun, elevation);
            barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            barrel.transform.localPosition = new Vector3(0f, 0f, 0.8f);

            var brake = Primitive("MuzzleBrake", PrimitiveType.Cylinder, new Vector3(0.18f, 0.06f, 0.18f), gun, elevation);
            brake.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            brake.transform.localPosition = new Vector3(0f, 0f, 1.58f);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(elevation, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.65f);

            var comp = root.AddComponent<NavalGunModule>();
            WireGun76(comp, turret, elevation, muzzle, shell, flash);
            return SavePrefab(root, dir);
        }

        /// <summary>아트 76mm 함포. TurretPivot/ElevationPivot/Muzzle 필수. 파일명 MOD_Gun76.fbx.</summary>
        private static GameObject BuildArtGun76(GameObject shell, GameObject flash, string dir)
        {
            return BuildUpgradeableArtModule<NavalGunModule>("MOD_Gun76", "MOD_Gun76", dir, (root, model) =>
            {
                var turret = FindDeep(model, "TurretPivot");
                var elevation = FindDeep(model, "ElevationPivot");
                var muzzle = FindDeep(model, "Muzzle");
                if (turret == null || elevation == null || muzzle == null)
                    Debug.LogError("[Setup] 76mm 함포 모델에서 TurretPivot/ElevationPivot/Muzzle을 찾지 못했습니다.");

                WireGun76(root.GetComponent<NavalGunModule>(), turret, elevation, muzzle, shell, flash);
            });
        }

        private static void WireGun76(NavalGunModule comp, Transform turret, Transform elevation, Transform muzzle,
                                      GameObject shell, GameObject flash)
        {
            Configure(comp, so =>
            {
                Set(so, "weapon.turret", turret);
                Set(so, "weapon.elevationPivot", elevation);
                Set(so, "weapon.muzzle", muzzle);
                Set(so, "weapon.minElevation", -5f);
                Set(so, "weapon.maxElevation", 60f);
                Set(so, "weapon.aimTolerance", 3f);
                Set(so, "projectilePrefab", shell);
                Set(so, "muzzleFlash", flash);
            });
        }

        /// <summary>대잠 폭뢰 발사기(그레이박스). 낮은 받침 위에 앞으로 비스듬한 발사관 6개.</summary>
        private static GameObject BuildAswLauncher(Material hull, Material gun, GameObject charge, string dir)
        {
            var root = new GameObject("MOD_AswLauncher");

            var deck = Primitive("Deck", PrimitiveType.Cube, new Vector3(Cell * 0.85f, 0.2f, Cell * 0.85f), hull, root.transform);
            deck.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            var rack = Primitive("Rack", PrimitiveType.Cube, new Vector3(1.2f, 0.25f, 0.9f), hull, root.transform);
            rack.transform.localPosition = new Vector3(0f, 0.32f, -0.1f);

            var points = new System.Collections.Generic.List<Transform>();
            int index = 1;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    var tube = Primitive($"Tube_{index:00}", PrimitiveType.Cylinder,
                                         new Vector3(0.22f, 0.3f, 0.22f), gun, root.transform);
                    Vector3 pos = new((col - 1) * 0.36f, 0.62f + row * 0.18f, -0.2f + row * 0.3f);
                    tube.transform.localPosition = pos;
                    tube.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);   // 앞으로 기울어진 관

                    var lp = new GameObject($"LaunchPoint_{index:00}").transform;
                    lp.SetParent(root.transform, false);
                    lp.localPosition = pos + tube.transform.up * 0.32f;
                    points.Add(lp);
                    index++;
                }
            }

            var comp = root.AddComponent<AswLauncherModule>();
            Configure(comp, so =>
            {
                Set(so, "chargePrefab", charge);
                SetArray(so, "launchPoints", points.ToArray());
            });

            return SavePrefab(root, dir);
        }

        private static GameObject BuildDecoyLauncher(Material mat, GameObject decoy, string dir)
        {
            var root = new GameObject("MOD_DecoyLauncher");

            var body = Primitive("Body", PrimitiveType.Cube, new Vector3(1.1f, 0.4f, 0.8f), mat, root.transform);
            body.transform.localPosition = new Vector3(0f, 0.3f, 0f);

            // 비스듬히 선 발사관 묶음으로 실루엣을 만든다
            for (int i = 0; i < 3; i++)
            {
                var tube = Primitive($"Tube_{i}", PrimitiveType.Cylinder,
                                     new Vector3(0.12f, 0.45f, 0.12f), mat, root.transform);
                tube.transform.localPosition = new Vector3(-0.3f + i * 0.3f, 0.75f, 0f);
                tube.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            }

            var lp = new GameObject("LaunchPoint").transform;
            lp.SetParent(root.transform, false);
            lp.localPosition = new Vector3(0f, 1.2f, 0f);

            var comp = root.AddComponent<DecoyLauncherModule>();
            Configure(comp, so =>
            {
                Set(so, "decoyPrefab", decoy);
                Set(so, "launchPoint", lp);
                Set(so, "decoysPerShot", 2);
            });

            return SavePrefab(root, dir);
        }

        /// <summary>헬기데크 1x1. 갑판 위에 헬기가 한 대 서 있다.</summary>
        private static GameObject BuildHeliDeck(Material hull, Material marking, GameObject heli, string dir)
        {
            var root = new GameObject("MOD_HeliDeck");

            var pad = Primitive("Pad", PrimitiveType.Cube,
                                new Vector3(Cell * 0.9f, 0.14f, Cell * 0.9f), hull, root.transform);
            pad.transform.localPosition = new Vector3(0f, 0.07f, 0f);

            // 착함 원 마킹
            var ring = Primitive("Mark", PrimitiveType.Cylinder,
                                 new Vector3(Cell * 0.6f, 0.02f, Cell * 0.6f), marking, root.transform);
            ring.transform.localPosition = new Vector3(0f, 0.15f, 0f);

            var spot = new GameObject("LandingSpot").transform;
            spot.SetParent(root.transform, false);
            spot.localPosition = new Vector3(0f, 0.2f, 0f);

            // 갑판에 서 있는 헬기. 출격하면 숨겨진다.
            var dark = CreateMaterial("gun", new Color(0.18f, 0.19f, 0.21f), 0.8f, 0.4f);
            var parked = BuildHelicopterModel("ParkedHelicopter", marking, dark,
                                              out var parkedRotor, out _);
            parked.transform.SetParent(root.transform, false);
            parked.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            parked.transform.localScale = Vector3.one * 0.55f;

            var comp = root.AddComponent<HelicopterDeckModule>();
            Configure(comp, so =>
            {
                Set(so, "helicopterPrefab", heli);
                Set(so, "landingSpot", spot);
                Set(so, "parkedHelicopter", parked);
                Set(so, "parkedRotor", parkedRotor);
            });

            return SavePrefab(root, dir);
        }
    }
}
