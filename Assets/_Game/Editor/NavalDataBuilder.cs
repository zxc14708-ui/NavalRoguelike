using System;
using UnityEditor;
using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Ship;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// ScriptableObject 에셋을 전부 만든다.
    /// 여기 적힌 숫자가 명세의 "Prototype 기본 밸런스" 출발점이다.
    /// 이후 조정은 이 스크립트가 아니라 생성된 에셋에서 한다.
    /// </summary>
    public static class NavalDataBuilder
    {
        public class Result
        {
            public ShipConfig Ship;
            public BalanceConfig Balance;
            public ProgressionConfig Progression;
            public StartingLoadout Loadout;
            public RoundSet Rounds, Rounds2, Rounds3;

            public ModuleDefinition Bridge, Autocannon, Radar, Vls, Rocket, Asw, Gun76, Sam, Ew,
                                    Ciws, Decoy, Magazine, Sonar, HeliDeck, RepairBay;
        }

        private const string CfgDir = Root + "/Data/Config";
        private const string ModDir = Root + "/Data/Modules";
        private const string EneDir = Root + "/Data/Enemies";
        private const string WavDir = Root + "/Data/Waves";

        /// <summary>
        /// 이미 만들어진 에셋을 경로로 다시 읽어온다.
        /// 씬을 새로 만들면 참조되지 않은 에셋 인스턴스가 언로드되므로,
        /// 씬에 값을 넣기 직전에 반드시 이걸로 다시 잡아야 한다.
        /// </summary>
        public static Result Load()
        {
            var r = new Result
            {
                Ship = Get<ShipConfig>($"{CfgDir}/ShipConfig.asset"),
                Balance = Get<BalanceConfig>($"{CfgDir}/BalanceConfig.asset"),
                Progression = Get<ProgressionConfig>($"{CfgDir}/ProgressionConfig.asset"),
                Loadout = Get<StartingLoadout>($"{CfgDir}/StartingLoadout.asset"),
                Rounds = Get<RoundSet>($"{WavDir}/RoundSet.asset"),
                Rounds2 = Get<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"),
                Rounds3 = AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage3.asset"),

                Bridge = Get<ModuleDefinition>($"{ModDir}/mod_bridge.asset"),
                Autocannon = Get<ModuleDefinition>($"{ModDir}/mod_autocannon.asset"),
                Radar = Get<ModuleDefinition>($"{ModDir}/mod_radar.asset"),
                Vls = Get<ModuleDefinition>($"{ModDir}/mod_vls.asset"),
                Rocket = Get<ModuleDefinition>($"{ModDir}/mod_rocket.asset"),
                Asw = Get<ModuleDefinition>($"{ModDir}/mod_asw.asset"),
                Gun76 = Get<ModuleDefinition>($"{ModDir}/mod_gun76.asset"),
                Sam = Get<ModuleDefinition>($"{ModDir}/mod_sam.asset"),
                Ew = Get<ModuleDefinition>($"{ModDir}/mod_ew.asset"),
                Ciws = Get<ModuleDefinition>($"{ModDir}/mod_ciws.asset"),
                Decoy = Get<ModuleDefinition>($"{ModDir}/mod_decoy.asset"),
                Magazine = Get<ModuleDefinition>($"{ModDir}/mod_magazine.asset"),
                Sonar = Get<ModuleDefinition>($"{ModDir}/mod_sonar.asset"),
                HeliDeck = Get<ModuleDefinition>($"{ModDir}/mod_helideck.asset"),
                RepairBay = Get<ModuleDefinition>($"{ModDir}/mod_repairbay.asset"),
            };
            return r;
        }

        private static T Get<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError($"[Setup] 에셋을 찾지 못했습니다: {path}");
            return a;
        }

        public static Result BuildAll(NavalPrefabBuilder.Result p)
        {
            string cfgDir = CfgDir;
            string modDir = ModDir;
            string eneDir = EneDir;
            string wavDir = WavDir;

            var r = new Result();

            // ------------------------------------------------------- 함선 기본값
            r.Ship = CreateSO<ShipConfig>($"{cfgDir}/ShipConfig.asset");
            Configure(r.Ship, so =>
            {
                Set(so, "hullMaxHp", 100f);
                Set(so, "baseMaxSpeed", 15.4333f);        // 30노트(1 m/s = 1.943844 kn)
                Set(so, "baseTurnRateDegPerSec", 40f);    // 최대 타각·최고속력 선회율. 선회 반경 약 14m
                Set(so, "acceleration", 7.7f);            // 정지 → 최고속력 90% 약 1.8초
                Set(so, "deceleration", 5.4f);            // 전령기를 내려도 한동안 미끄러진다(약 2.8초)
                Set(so, "reverseSpeedRatio", 0.4f);
                Set(so, "rudderFullEffectSpeedRatio", 0.35f);
                Set(so, "minRudderEffect", 0.12f);
                Set(so, "maxRudderAngle", 35f);
                Set(so, "rudderShiftTime", 1.2f);
                Set(so, "rudderHoldsPosition", true);
                Set(so, "turnResponseTime", 0.9f);
                Set(so, "turnSpeedLoss", 0.2f);
                Set(so, "baseDetectionRange", 20f);
                Set(so, "baseTrackedTargets", 3);
                Set(so, "baseCommandCapacity", 6);
            });

            // --------------------------------------------------------- 전역 밸런스
            r.Balance = CreateSO<BalanceConfig>($"{cfgDir}/BalanceConfig.asset");
            Configure(r.Balance, so =>
            {
                Set(so, "moduleDamageShare", 0.6f);
            });

            // ------------------------------------------------------------ 모듈
            r.Bridge = Module("mod_bridge", "함교",
                "함 지휘소이자 기관실. Q로 기만체를 뿌리고, 손상 통제로 선체와 주변 모듈을 서서히 수리한다. " +
                "항해 레이더로 32까지 탐지한다(전용 레이더 52보다 짧다). 속력은 모듈 수와 상관없이 일정하다.",
                ModuleType.Bridge, 3, 1, false, PlacementZone.Anywhere,   // 앞뒤 3칸(함교+기관실+연돌). 배의 척추라 돌리지 않는다
                hp: 120f, Rarity.Common, 0f, p.Bridge, modDir, so =>
                {
                    Set(so, "stats.ReloadTime", 14f);             // 기만체 재장전 (전용 발사기보다 느림)
                    Set(so, "stats.HullRepairPerSecond", 0.5f);   // 손상통제반(0.9)의 절반쯤
                    Set(so, "stats.ModuleRepairPerSecond", 0.8f);
                    Set(so, "stats.DamageReduction", 0.05f);
                    Set(so, "stats.DetectionRange", 32f);         // 76mm(34)·유도로켓(38) 끝은 레이더가 있어야 쓴다
                    Set(so, "stats.ExtraTrackedTargets", 1);
                });

            r.Autocannon = Module("mod_autocannon", "기관포", "근거리(0~22) 속사 화기. 가까운 고속정·드론·전투기를 먼저 노려 짧은 점사를 끊어 쏜다. 레이더 없이도 가까운 적과 교전한다. 붙어 있는 중간·높은 블록이 가리는 방향으로는 쏘지 못한다.",
                ModuleType.Autocannon, 1, 1, true, PlacementZone.Anywhere,
                hp: 40f, Rarity.Common, 1f, p.Autocannon, modDir, so =>
                {
                    Set(so, "stats.Range", 22f);
                    Set(so, "stats.Damage", 2f);
                    Set(so, "stats.MinRange", 0f);
                    Set(so, "stats.ReloadTime", 0.13f);
                    Set(so, "stats.ProjectileSpeed", 80f);
                    Set(so, "stats.TurretTurnRate", 200f);
                    Ammo(so, AmmoFamily.Gun, 200, 30, 4f);    // 200발, 4초마다 +30
                    Efficiency(so, 1f, 0.6f, 0.3f, 0.3f, 0.5f, 1f, 0.8f, 1f);
                });

            r.Radar = Module("mod_radar", "레이더", "탐지거리(52)와 동시 추적 수를 늘린다. 탐지 밖의 적은 조준할 수 없다 — VLS(48)를 끝까지 쓰려면 필요하다.",
                ModuleType.Radar, 1, 1, true, PlacementZone.Anywhere,
                hp: 25f, Rarity.Uncommon, 1f, p.Radar, modDir, so =>
                {
                    Set(so, "stats.DetectionRange", 52f);         // VLS 최대 사거리(48)를 덮는다
                    Set(so, "stats.ExtraTrackedTargets", 3);
                });

            r.Vls = Module("mod_vls", "VLS", "오른쪽 VLS 콘솔에서 발사기마다 대함·대공·대잠 탄종을 선택한다. 지정한 단축키로 발사기를 전환한다. 세 탄종은 8셀 탄약을 공유하며 전환에 2초가 걸린다. 대잠탄은 소나 접촉이 필요하다.",
                ModuleType.Vls, 1, 1, true, PlacementZone.Anywhere,
                hp: 45f, Rarity.Rare, 0.8f, p.Vls, modDir, so =>
                {
                    Set(so, "stats.Range", 48f);
                    Set(so, "stats.Damage", 60f);
                    Set(so, "stats.ReloadTime", 1.5f);   // 발사 간격. 지속 화력은 셀 장전(15초)이 정한다
                    Set(so, "stats.MinRange", 16f);
                    Set(so, "stats.ProjectileSpeed", 30f);
                    Ammo(so, AmmoFamily.Missile, 8, 1, 15f);   // 8셀, 15초마다 1셀 장전
                    Efficiency(so, 0.3f, 1f, 1.2f, 1.3f, 0.8f, 0f, 0f, 0f);
                    Set(so, "stats.FireArcDegrees", 360f);
                });

            r.Rocket = Module("mod_rocket", "유도로켓 발사기", "중거리(10~38) 130mm 유도로켓. 여러 발을 차례로 쏘며 고속정마다 한 발씩 나눠 맞힌다. 탄약고 보너스를 받는다. 붙은 중간·높은 블록이 가리는 방향으로는 쏘지 못한다.",
                ModuleType.GuidedRocket, 1, 1, true, PlacementZone.Anywhere,
                hp: 35f, Rarity.Uncommon, 1f, p.RocketLauncher, modDir, so =>
                {
                    Set(so, "stats.Range", 38f);
                    Set(so, "stats.Damage", 14f);
                    Set(so, "stats.MinRange", 10f);
                    Set(so, "stats.ReloadTime", 1.0f);
                    Set(so, "stats.ProjectileSpeed", 40f);
                    Set(so, "stats.TurretTurnRate", 120f);
                    Ammo(so, AmmoFamily.Missile, 12, 3, 6f);
                    Efficiency(so, 1f, 0.9f, 0.6f, 0.5f, 0.6f, 0f, 0f, 0f);
                });

            r.Gun76 = Module("mod_gun76", "76mm 함포",
                "중거리(6~34) 속사 함포. 수상함과 몰려 있는 적을 먼저 노려 진로를 앞질러 고폭탄을 쏘고, 2.5 안의 다른 적에게도 파편 피해(50%)를 준다. " +
                "선회가 느리다. 탄약고 보너스를 받는다.",
                ModuleType.NavalGun, 1, 1, true, PlacementZone.Anywhere,
                hp: 45f, Rarity.Uncommon, 1.1f, p.Gun76, modDir, so =>
                {
                    Set(so, "stats.Range", 34f);
                    Set(so, "stats.MinRange", 6f);
                    Set(so, "stats.Damage", 24f);
                    Set(so, "stats.ReloadTime", 1.6f);
                    Set(so, "stats.ProjectileSpeed", 85f);
                    Set(so, "stats.TurretTurnRate", 90f);
                    Ammo(so, AmmoFamily.Gun, 20, 3, 6f);
                    Efficiency(so, 0.8f, 1f, 0.8f, 0.7f, 0.8f, 0.6f, 0.7f, 1f);
                });

            r.Sam = Module("mod_sam", "함대공 미사일",
                "적 미사일과 항공기를 8~42에서 요격미사일로 격추한다. 함선에 먼저 닿을 위협부터 쏘는 방어 1단계. " +
                "탐지 범위 밖은 볼 수 없으니 레이더와 함께 쓴다.",
                ModuleType.SamLauncher, 1, 1, true, PlacementZone.Anywhere,
                hp: 35f, Rarity.Rare, 1f, p.SamLauncher, modDir, so =>
                {
                    Set(so, "stats.Range", 42f);
                    Set(so, "stats.MinRange", 8f);
                    Set(so, "stats.Damage", 10f);
                    Set(so, "stats.ReloadTime", 3f);
                    Set(so, "stats.ProjectileSpeed", 48f);
                    Ammo(so, AmmoFamily.Missile, 4, 1, 10f);
                    Efficiency(so, 0f, 0f, 0f, 0f, 0f, 1f, 1f, 1f);
                });

            r.Ew = Module("mod_ew", "전자전 장비",
                "E를 누르면 반경 35 안의 적 미사일마다 75% 확률로 4초 동안 추적을 교란한다. 교란된 미사일은 크게 빗나가며 돌아 들어온다 — 그 사이 CIWS와 조함으로 대응한다. " +
                "기만체(Q)와 번갈아 쓴다.",
                ModuleType.EwSuite, 1, 1, true, PlacementZone.Anywhere,
                hp: 30f, Rarity.Uncommon, 1f, p.EwSuite, modDir, so =>
                {
                    Set(so, "stats.Range", 35f);
                    Set(so, "stats.ReloadTime", 18f);
                });

            r.Asw = Module("mod_asw", "대잠 폭뢰 발사기",
                "소나로 확인한 잠수함 또는 어뢰 발사 흔적에 폭뢰 2발을 던진다. 8m 이내에서는 자체 센서로도 찾는다. " +
                "잠항 중에도 피해를 주며, 명중 시 수중 접촉을 다시 확정한다.",
                ModuleType.AswLauncher, 1, 1, true, PlacementZone.SideOrStern,   // 옆이 트이면 폭뢰 발사대, 뒤가 트이면 폭뢰 투하대
                hp: 30f, Rarity.Uncommon, 1.2f, p.AswLauncher, modDir, so =>
                {
                    Set(so, "stats.Range", 22f);
                    Set(so, "stats.Damage", 16f);
                    Set(so, "stats.ReloadTime", 4.5f);
                    Ammo(so, AmmoFamily.Asw, 6, 1, 8f);
                    Efficiency(so, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f);
                });

            r.Ciws = Module("mod_ciws", "CIWS", "근접방어(0~20). 자체 레이더로 충돌이 임박한 미사일부터 요격하고, 없으면 가까운 항공기를 쏜다. 함교 등 상부 구조물 방향은 쏘지 못한다.",
                ModuleType.Ciws, 1, 1, true, PlacementZone.Anywhere,
                hp: 30f, Rarity.Uncommon, 1f, p.Ciws, modDir, so =>
                {
                    Set(so, "stats.Range", 20f);           // 10칸
                    Set(so, "stats.Damage", 0.75f);       // 흩어진 탄 여러 발로 떨어뜨린다(미사일 4발 명중)
                    Set(so, "stats.ReloadTime", 0.04f);        // 초당 25발(예광탄) · 점사·흩어짐은 CiwsModule
                    Set(so, "stats.ProjectileSpeed", 140f);
                    Set(so, "stats.FireArcDegrees", 360f);
                    Set(so, "stats.TurretTurnRate", 400f);
                    Ammo(so, AmmoFamily.Gun, 450, 0, 8f);     // 비면 8초 전량 재장전
                    Set(so, "stats.AmmoPerShot", 3);           // 예광탄 한 발 = 실제 3발(분당 4500발). 최소 점사 약 45발
                    Efficiency(so, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.7f, 0.8f, 1f);
                });

            r.Decoy = Module("mod_decoy", "기만체 발사기",
                "Q를 누르면 함교 기만체와 함께 발사한다. 기만체 2발이 7초 동안 반경 35m 안의 미사일을 90% 확률로 유인한다.",
                ModuleType.DecoyLauncher, 1, 1, true, PlacementZone.Anywhere,
                hp: 25f, Rarity.Uncommon, 1f, p.DecoyLauncher, modDir, so =>
                {
                    Set(so, "stats.ReloadTime", 8f);
                });


            r.Magazine = Module("mod_magazine", "탄약고",
                "2칸 안의 기관포·76mm: 발사 간격 −25%, 탄약 용량 +50%, 보급 25% 빠르게. 유도로켓은 발사 간격만. " +
                "여러 개를 붙여도 가장 좋은 하나만 적용된다. 파괴되면 유폭해 붙어 있는 모듈에 큰 피해(60, 한 칸 떨어지면 절반)를 준다.",
                ModuleType.Magazine, 1, 1, true, PlacementZone.Anywhere,
                hp: 25f, Rarity.Common, 1f, p.Magazine, modDir, so =>
                {
                    Set(so, "stats.SupportRadiusCells", 2);
                    Set(so, "stats.ReloadBonus", 0.25f);
                    Set(so, "stats.AmmoCapacityBonus", 0.5f);
                    Set(so, "stats.ResupplyBonus", 0.25f);
                    Set(so, "stats.CookOffDamage", 60f);
                });

            r.Sonar = Module("mod_sonar", "소나", "32m 안의 잠수함을 2초 동안 탐색해 위치를 확정한다. 능동 핑은 쿨타임마다 자동 발동해 즉시 접촉을 확정한다. 대잠 무기와 헬기에 접촉을 공유한다.",
                ModuleType.Sonar, 1, 1, true, PlacementZone.Anywhere,
                hp: 30f, Rarity.Rare, 1.6f, p.Sonar, modDir, so =>
                {
                    Set(so, "stats.DetectionRange", 32f);
                });

            r.HeliDeck = Module("mod_helideck", "헬기데크", "소나 접촉이나 어뢰 발사 흔적으로 출격해 현장에서 잠수함을 수색한다. 잠항 표적에는 폭뢰, 수상함에는 로켓을 사용한다. 함미 전용.",
                ModuleType.HelicopterDeck, 1, 1, true, PlacementZone.SternOnly,
                hp: 50f, Rarity.Rare, 1.3f, p.HeliDeck, modDir, so =>
                {
                    Set(so, "stats.SortieCooldown", 12f);
                    Set(so, "stats.HelicopterSpeed", 18f);
                });

            r.RepairBay = Module("mod_repairbay", "손상 통제반",
                "방수·소화로 받는 피해를 10% 줄이고, 선체와 손상된 모듈을 서서히 수리한다. " +
                "파괴된 모듈은 복구하지 못한다. 최대 3개.",
                ModuleType.RepairBay, 1, 1, true, PlacementZone.Anywhere,
                hp: 30f, Rarity.Uncommon, 1.3f, p.RepairBay, modDir, so =>
                {
                    Set(so, "stats.HullRepairPerSecond", 0.9f);
                    Set(so, "stats.ModuleRepairPerSecond", 1.5f);
                    Set(so, "stats.DamageReduction", 0.10f);
                }, maxCount: 3);

            // -------------------------------------------------------- 시작 함선
            r.Loadout = CreateSO<StartingLoadout>($"{cfgDir}/StartingLoadout.asset");
            Configure(r.Loadout, so =>
            {
                var list = so.FindProperty("entries");
                list.arraySize = 4;

                // 함교가 원점이고 배는 여기서부터 자란다. 함교는 앞뒤 3칸(함교+기관실+연돌)이다.
                // 순서대로 설치되므로 앞 항목과 반드시 맞닿아 있어야 한다.
                //
                //   X:    -2     -1    0    1      2      3
                //     [기관포][함교·기관·연돌][CIWS][기관포]   → 선수(+X)
                //
                // 함교 원점은 가장 선미 쪽 칸(-1)이라 -1, 0, 1을 차지한다.
                // 기관포는 사격각이 뒤쪽 블록에 가리므로 선미포는 뒤를 보게 돌려 단다.
                SetEntry(list.GetArrayElementAtIndex(0), r.Bridge, -1, 0);           // 뿌리 (-1~1)
                SetEntry(list.GetArrayElementAtIndex(1), r.Ciws, 2, 0);              // 함교 앞 CIWS
                SetEntry(list.GetArrayElementAtIndex(2), r.Autocannon, 3, 0);        // 선수포(앞)
                SetEntry(list.GetArrayElementAtIndex(3), r.Autocannon, -2, 0, rot: 2); // 선미포(뒤)
            });

            // ---------------------------------------------------------- 성장
            r.Progression = CreateSO<ProgressionConfig>($"{cfgDir}/ProgressionConfig.asset");
            Configure(r.Progression, so =>
            {
                SetArray(so, "pool",
                    r.Radar, r.Vls, r.Rocket, r.Gun76, r.Sam, r.Ew, r.Asw, r.Ciws, r.Decoy,
                    r.Magazine, r.Sonar, r.HeliDeck, r.Autocannon, r.RepairBay);

                // 필요 경험치 5, 9, 13, 17 ... — 8분 스테이지에서 대략 12레벨 안팎
                Set(so, "xpBase", 5);
                Set(so, "xpStep", 4);
                Set(so, "hullRepairOnLevelUp", 8f);
                Set(so, "cardCount", 3);
            });

            // ------------------------------------------------------------ 적
            var fast = Enemy("ene_fastboat", "고속정", Game.Combat.TargetKind.Surface, p.FastBoat,
                hp: 12f, speed: 11f, turn: 200f, range: 6f, dmg: 3.5f, cd: 1.6f,
                missile: null, reveal: 0f, xp: 1, dir: eneDir);

            var msl = Enemy("ene_missileboat", "미사일정", Game.Combat.TargetKind.Surface, p.MissileBoat,
                hp: 55f, speed: 9f, turn: 120f, range: 36f, dmg: 26f, cd: 11f,   // 엘리트(2026-10-01). 기관포(22)·76mm(34) 밖, 유도로켓(38)·VLS(48) 안에서 돈다. 11초마다 2연발
                missile: p.MissileEnemy, reveal: 0f, xp: 7, dir: eneDir);

            var sub = Enemy("ene_submarine", "잠수함", Game.Combat.TargetKind.Submarine, p.Submarine,
                hp: 70f, speed: 5f, turn: 80f, range: 52f, dmg: 55f, cd: 16f,  // 엘리트(2026-10-01).  // 드물게 등장하지만 한 발이 위협적. 예고 뒤 침로·속력 변경으로 회피 가능
                missile: null, reveal: 10f, xp: 10, dir: eneDir);

            var boss = Enemy("ene_boss", "연안 경비정", Game.Combat.TargetKind.Surface, p.Boss,
                hp: 1000f, speed: 6f, turn: 60f, range: 33f, dmg: 28f, cd: 3f,
                missile: p.MissileEnemy, reveal: 0f, xp: 0, dir: eneDir);
            Configure(boss, so => { Set(so, "salvoSize", 4); Set(so, "salvoSpacing", 0.24f); });

            // 항공 위협 (스테이지 2부터)
            //   자폭 드론: 체력 낮고 빠름. PreferredRange = 급강하 시작 수평거리. 기만체·재밍에 속지 않는다.
            //   정찰기:   공격하지 않고 PreferredRange 반경을 높이 돈다. 살아 있으면 적 재장전 35% 가속.
            var drone = Enemy("ene_drone", "자폭 드론", Game.Combat.TargetKind.Aircraft, p.Drone,
                hp: 6f, speed: 15f, turn: 140f, range: 12f, dmg: 14f, cd: 0f,
                missile: null, reveal: 0f, xp: 1, dir: eneDir);

            var recon = Enemy("ene_recon", "정찰기", Game.Combat.TargetKind.Aircraft, p.Recon,
                hp: 30f, speed: 9f, turn: 45f, range: 26f, dmg: 0f, cd: 0f,   // 기관포 사거리(20) 밖을 돈다
                missile: null, reveal: 0f, xp: 3, dir: eneDir);

            //   전투기:   고도 12m로 접근해 22m 안에서 저공 소사(점사 한 번에 12), 급상승 이탈 후 반복.
            var fighter = Enemy("ene_fighter", "전투기", Game.Combat.TargetKind.Aircraft, p.Fighter,
                hp: 18f, speed: 19f, turn: 110f, range: 22f, dmg: 12f, cd: 0f,
                missile: null, reveal: 0f, xp: 2, dir: eneDir);

            // 항공전함(2026-10-05부터 스테이지 3 보스 — 체력은 BuildStage3에서 Stage3BossHp로). 주포·드론 편대·정찰기·미사일 일제사격·대공포(HybridBattleshipBoss).
            var boss2 = Enemy("ene_boss2", "항공전함", Game.Combat.TargetKind.Surface, p.Boss2,
                hp: 1600f, speed: 6f, turn: 50f, range: 26f, dmg: 25f, cd: 3f,
                missile: p.MissileEnemy, reveal: 0f, xp: 0, dir: eneDir);
            Configure(boss2, so =>
            {
                Set(so, "launchedDrone", drone);
                Set(so, "launchedRecon", recon);
                Set(so, "launchedFighter", fighter);
            });

            // 표적 분류·가치(무기 효율과 VLS 표적 규칙의 기준), 포화 공격
            Classify(fast, Game.Combat.TargetCategory.SmallSurface, 1);
            Classify(msl, Game.Combat.TargetCategory.MediumSurface, 3, EnemyRank.Elite);
            Configure(msl, so => { Set(so, "salvoSize", 2); Set(so, "salvoSpacing", 0.15f); Set(so, "maxAlive", 2); });
            Configure(sub, so => Set(so, "maxAlive", 1));
            Classify(sub, Game.Combat.TargetCategory.Submarine, 3, EnemyRank.Elite);
            Classify(boss, Game.Combat.TargetCategory.Boss, 10, EnemyRank.Boss);
            Classify(drone, Game.Combat.TargetCategory.Drone, 1);
            Classify(recon, Game.Combat.TargetCategory.Air, 3);
            Classify(fighter, Game.Combat.TargetCategory.Air, 2);
            Classify(boss2, Game.Combat.TargetCategory.Boss, 10, EnemyRank.Boss);

            // 스테이지 2 추가 적(모델이 없으면 null — 웨이브에서 빠진다)
            var (cruiseSub, pcc) = BuildStage2EnemyData(p.CruiseSubmarine, p.PccCorvette, p.MissileEnemy, eneDir);
            var suicide = BuildSuicideBoatData(p.SuicideBoat, eneDir);
            var normal = BuildNormalEnemyData(p, eneDir);
            var corvetteBoss = BuildCorvetteBossData(p, eneDir);

            // --------------------------------------------------------- 라운드
            // 스테이지 1: 8개 구간을 쉬지 않고 이어서 진행한다(약 8분). 성장은 레벨업으로.
            // 마지막 구간은 시간이 지나도 끝나지 않고, 보스를 격침하면 스테이지 클리어.
            r.Rounds = CreateSO<RoundSet>($"{wavDir}/RoundSet.asset");
            Configure(r.Rounds, so =>
            {
                var list = so.FindProperty("rounds");
                list.arraySize = 8;

                //        구간  시간   스폰/초  최대  적 구성
                SetRound(list.GetArrayElementAtIndex(0), "초계", 60f, 0.50f, 14, null, (fast, 1f));
                SetRound(list.GetArrayElementAtIndex(1), "접촉", 60f, 0.70f, 18, null, (fast, 3f), (drone, 0.8f));
                SetRound(list.GetArrayElementAtIndex(2), "교전", 60f, 0.80f, 20, null, (fast, 3f), (msl, 0.4f));
                SetRound(list.GetArrayElementAtIndex(3), "대함 위협", 60f, 0.90f, 22, null, (fast, 2f), (msl, 0.6f), (drone, 0.8f));
                SetRound(list.GetArrayElementAtIndex(4), "포화 공격", 60f, 1.00f, 26, null, (fast, 3f), (msl, 0.8f));
                SetRound(list.GetArrayElementAtIndex(5), "수중 접촉", 60f, 1.10f, 28, null, (fast, 1f), (msl, 0.6f), (sub, 0.175f));
                SetRound(list.GetArrayElementAtIndex(6), "총력전", 60f, 1.20f, 32, null, (fast, 4f), (msl, 0.6f), (sub, 0.175f), (drone, 1f));
                SetRound(list.GetArrayElementAtIndex(7), "적 기함", 90f, 0.80f, 24, boss, (fast, 2f), (msl, 0.4f));
                SetEscort(list.GetArrayElementAtIndex(7), fast, 4);
            });

            // 스테이지 2: 항공 위협이 더해진다. 정찰기가 먼저 나와 표적을 지시하고, 드론 비율이 점점 오른다.
            // 함선·모듈·레벨은 1스테이지에서 그대로 이어진다.
            r.Rounds2 = CreateSO<RoundSet>($"{wavDir}/RoundSet_Stage2.asset");
            Configure(r.Rounds2, so =>
            {
                var list = so.FindProperty("rounds");
                list.arraySize = 7;

                //        구간  시간   스폰/초  최대  적 구성
                SetRound(list.GetArrayElementAtIndex(0), "공중 정찰", 60f, 0.70f, 20, null, (fast, 3f), (msl, 0.4f), (recon, 0.4f));
                SetRound(list.GetArrayElementAtIndex(1), "드론 습격", 60f, 0.90f, 24, null, (fast, 2f), (drone, 3f));
                SetRound(list.GetArrayElementAtIndex(2), "복합 위협", 60f, 1.00f, 26, null, (fast, 2f), (msl, 0.6f), (drone, 2f), (recon, 0.3f), (fighter, 0.6f));
                SetRound(list.GetArrayElementAtIndex(3), "수중·공중", 60f, 1.10f, 28, null, (msl, 0.4f), (sub, 0.175f), (drone, 2.5f));
                SetRound(list.GetArrayElementAtIndex(4), "제공권 상실", 60f, 1.40f, 34, null, (fast, 1f), (drone, 4f), (recon, 0.3f), (fighter, 1.2f));
                SetRound(list.GetArrayElementAtIndex(5), "총공세", 60f, 1.30f, 36, null, (fast, 2f), (msl, 0.8f), (sub, 0.175f), (drone, 3f), (recon, 0.4f), (fighter, 1f));
                // 보스: 현대화 초계함(2026-10-03, 모델이 없으면 예전 항공전함). 일반 스폰은 수상함만 가볍게
                SetRound(list.GetArrayElementAtIndex(6), corvetteBoss != null ? "현대화 초계함" : "항공전함", 90f, 0.60f, 20,
                         corvetteBoss ?? boss2, (fast, 2f), (msl, 0.4f));
                SetEscort(list.GetArrayElementAtIndex(6), fast, 3);
            });
            AddStage2Enemies(r.Rounds2, cruiseSub, pcc);
            AddSuicideBoat(r.Rounds, r.Rounds2, suicide);
            AddNormalEnemies(r.Rounds, r.Rounds2, normal);
            FocusBossRounds(fast, suicide, drone, r.Rounds, r.Rounds2);   // 마지막에: 보스 구간은 고속정·자폭 드론·자폭 보트만
            r.Rounds3 = BuildStage3(p);   // 스테이지 3(2026-10-05) — 스테이지 2 다음에 이어 붙는다

            AssetDatabase.SaveAssets();
            return r;
        }

        // ------------------------------------------------------------ 내부 헬퍼

        /// <summary>
        /// 갑판 위로 솟은 정도. 낮은 장비는 사선 아래라 무기를 가리지 않는다.
        /// 헬기데크는 이착함 공간과 격납 구조물 때문에 높음으로 둔다.
        /// </summary>
        private static ModuleHeight HeightFor(ModuleType type) => type switch
        {
            ModuleType.Magazine or ModuleType.RepairBay or ModuleType.Sonar or ModuleType.DecoyLauncher
                or ModuleType.AswLauncher
                => ModuleHeight.Low,
            ModuleType.Bridge or ModuleType.Radar or ModuleType.HelicopterDeck
                => ModuleHeight.High,
            _ => ModuleHeight.Mid,
        };

        /// <summary>탄약. reloadAmount 0 = 비면 전량 재장전(CIWS).</summary>
        private static void Ammo(SerializedObject so, AmmoFamily family, int capacity, int reloadAmount, float reloadTime)
        {
            Set(so, "stats.AmmoFamily", (int)family);
            Set(so, "stats.MagazineCapacity", capacity);
            Set(so, "stats.AmmoPerShot", 1);
            Set(so, "stats.AmmoReloadAmount", reloadAmount);
            Set(so, "stats.AmmoReloadTime", reloadTime);
        }

        private static ModuleDefinition Module(string id, string name, string desc, ModuleType type,
            int w, int h, bool canRotate, PlacementZone zone,
            float hp, Rarity rarity, float weight, GameObject prefab,
            string dir, Action<SerializedObject> stats, int maxCount = 0)
        {
            var def = CreateSO<ModuleDefinition>($"{dir}/{id}.asset");
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "description", desc);
                Set(so, "type", (int)type);
                Set(so, "width", w);
                Set(so, "height", h);
                Set(so, "canRotate", canRotate);
                Set(so, "placement.Zone", (int)zone);
                Set(so, "maxCount", maxCount);
                Set(so, "heightClass", (int)HeightFor(type));
                Set(so, "maxHp", hp);
                Set(so, "rarity", (int)rarity);
                Set(so, "weight", weight);

                // 손으로 연결한 아트 프리팹은 덮어쓰지 않는다.
                // 그레이박스(MOD_*)일 때만 다시 물려준다.
                if (ShouldReplacePrefab(so)) Set(so, "prefab", prefab);

                stats?.Invoke(so);
            });
            return def;
        }

        /// <summary>
        /// 현재 연결된 프리팹을 셋업이 다시 덮어써도 되는지 판단한다.
        /// 비어 있거나 자동 생성된 그레이박스면 교체하고,
        /// 사람이 연결한 아트 프리팹이면 그대로 둔다.
        /// </summary>
        private static bool ShouldReplacePrefab(SerializedObject so)
        {
            var current = so.FindProperty("prefab")?.objectReferenceValue;
            if (current == null) return true;

            string path = AssetDatabase.GetAssetPath(current);
            return string.IsNullOrEmpty(path) ||
                   path.StartsWith($"{Root}/Prefabs/Modules/MOD_");
        }

        // ------------------------------------------------------------ 스테이지 2 추가 적

        /// <summary>
        /// 순항미사일 잠수함·엘리트 초계함 데이터만 만들고 RoundSet_Stage2에 넣는다. 다른 에셋은 건드리지 않는다.
        /// (Naval/Add Stage 2 Enemies 메뉴, 배치 실행)
        /// </summary>
        public static void BuildStage2EnemiesOnly(NavalPrefabBuilder.Result p)
        {
            EnsureFolder(EneDir);
            var (cruiseSub, pcc) = BuildStage2EnemyData(p.CruiseSubmarine, p.PccCorvette, p.MissileEnemy, EneDir);
            AddStage2Enemies(AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"), cruiseSub, pcc);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ 스테이지 3(2026-10-05)

        /// <summary>
        /// 스테이지 3 "A2/AD 해역": 적 데이터 4종을 만들고 RoundSet_Stage3를 만든 뒤 스테이지 2 다음에 잇는다(RoundSet.nextStage — 씬은 그대로).
        /// 보스는 항공전함(ene_boss2, 스테이지 3용으로 체력 2200). 다른 스테이지의 구간은 건드리지 않는다.
        /// 기존 적 데이터는 경로로 읽는다(전체 생성에서는 바로 앞에서 만든 것).
        /// </summary>
        public static RoundSet BuildStage3(NavalPrefabBuilder.Result p)
        {
            EnsureFolder(EneDir);
            EnsureFolder(WavDir);
            EnemyDefinition Def(string id) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EneDir}/{id}.asset");

            // 새 적 4종 — 모델이 없으면 null(웨이브에서 빠진다)
            EnemyDefinition usv = null, ew = null, aa = null, attackSub = null;
            if (p.UnmannedCraft != null)
            {
                usv = Enemy("ene_usv", "무인 공격정", Game.Combat.TargetKind.Surface, p.UnmannedCraft,
                    hp: 18f, speed: 13f, turn: 210f, range: 7f, dmg: 4.5f, cd: 1.5f,
                    missile: null, reveal: 0f, xp: 2, dir: EneDir);
                Classify(usv, Game.Combat.TargetCategory.SmallSurface, 1);
            }
            if (p.EwCorvette != null)
            {
                ew = Enemy("ene_ew_corvette", "전자전 코르벳", Game.Combat.TargetKind.Surface, p.EwCorvette,
                    hp: 120f, speed: 8f, turn: 70f, range: 34f, dmg: 6f, cd: 3.2f,
                    missile: null, reveal: 0f, xp: 8, dir: EneDir);
                Classify(ew, Game.Combat.TargetCategory.MediumSurface, 4, EnemyRank.Elite);
                Configure(ew, so => Set(so, "maxAlive", 1));
            }
            if (p.AaFrigate != null)
            {
                aa = Enemy("ene_aa_frigate", "방공 프리깃", Game.Combat.TargetKind.Surface, p.AaFrigate,
                    hp: 150f, speed: 7f, turn: 60f, range: 26f, dmg: 7f, cd: 2.8f,
                    missile: null, reveal: 0f, xp: 9, dir: EneDir);
                Classify(aa, Game.Combat.TargetCategory.MediumSurface, 4, EnemyRank.Elite);
                Configure(aa, so => Set(so, "maxAlive", 1));
            }
            if (p.AttackSubmarine != null)
            {
                attackSub = Enemy("ene_attack_submarine", "공격 잠수함", Game.Combat.TargetKind.Submarine, p.AttackSubmarine,
                    hp: 95f, speed: 6f, turn: 85f, range: 48f, dmg: 45f, cd: 13f,
                    missile: null, reveal: 10f, xp: 11, dir: EneDir);
                Classify(attackSub, Game.Combat.TargetCategory.Submarine, 4, EnemyRank.Elite);
                Configure(attackSub, so => Set(so, "maxAlive", 1));
            }

            var fast = Def("ene_fastboat");
            var msl = Def("ene_missileboat");
            var sub = Def("ene_submarine");
            var drone = Def("ene_drone");
            var recon = Def("ene_recon");
            var fighter = Def("ene_fighter");
            var pcc = Def("ene_pcc_corvette");
            var cruise = Def("ene_cruise_submarine");
            var boat = Def("ene_suicide_boat");
            var torpedoBoat = Def("ene_torpedo_boat");
            var mineLayer = Def("ene_minelayer");
            var artillery = Def("ene_artillery_boat");
            var repair = Def("ene_repair_boat");
            var boss = Def("ene_boss2");
            if (boss != null) Configure(boss, so => Set(so, "maxHp", Stage3BossHp));

            var stage3 = CreateSO<RoundSet>($"{WavDir}/RoundSet_Stage3.asset");
            Configure(stage3, so =>
            {
                var list = so.FindProperty("rounds");
                list.arraySize = 7;
                //        구간  시간   스폰/초  최대  적 구성(모델이 없는 적은 빠진다)
                SetRound(list.GetArrayElementAtIndex(0), "전자전 해역", 60f, 0.85f, 22, null,
                         Pick((fast, 2f), (usv, 2f), (msl, 0.4f), (ew, 0.45f)));
                SetRound(list.GetArrayElementAtIndex(1), "무인정 떼", 60f, 1.00f, 26, null,
                         Pick((usv, 4f), (boat, 1.2f), (drone, 1.5f)));
                SetRound(list.GetArrayElementAtIndex(2), "방공망", 60f, 1.05f, 26, null,
                         Pick((usv, 2f), (msl, 0.6f), (aa, 0.45f), (pcc, 0.35f), (recon, 0.3f), (drone, 1.5f)));
                SetRound(list.GetArrayElementAtIndex(3), "잠수함 매복", 60f, 1.10f, 28, null,
                         Pick((usv, 1.5f), (attackSub, 0.25f), (sub, 0.175f), (torpedoBoat, 0.5f), (mineLayer, 0.35f), (ew, 0.3f)));
                SetRound(list.GetArrayElementAtIndex(4), "제공권 쟁탈", 60f, 1.35f, 34, null,
                         Pick((usv, 1.5f), (drone, 3f), (fighter, 1.4f), (recon, 0.4f), (aa, 0.4f)));
                SetRound(list.GetArrayElementAtIndex(5), "총공세", 60f, 1.45f, 38, null,
                         Pick((fast, 2f), (usv, 2f), (msl, 0.8f), (pcc, 0.6f), (ew, 0.4f), (aa, 0.4f), (attackSub, 0.2f),
                              (cruise, 0.3f), (drone, 2.5f), (fighter, 1f), (artillery, 0.5f), (repair, 0.4f)));
                SetRound(list.GetArrayElementAtIndex(6), "항공전함", 90f, BossRoundSpawnRate, BossRoundMaxAlive, boss,
                         Pick((fast, 1f), (drone, 0.5f), (boat, 0.4f)));
                SetEscort(list.GetArrayElementAtIndex(6), usv ?? fast, 4);
            });
            FocusBossRounds(fast, boat, drone, stage3);

            // 스테이지 2 다음에 잇는다(StageDirector가 씬 목록 뒤에 붙인다)
            var stage2 = AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset");
            if (stage2 != null) Configure(stage2, so => so.FindProperty("nextStage").objectReferenceValue = stage3);
            AssetDatabase.SaveAssets();
            return stage3;
        }

        /// <summary>스테이지 3 보스(항공전함) 체력. 스테이지 2 보스 현대화 초계함은 1500.</summary>
        public const float Stage3BossHp = 2200f;

        /// <summary>null(모델이 없는 적)을 뺀 구성.</summary>
        private static (EnemyDefinition, float)[] Pick(params (EnemyDefinition enemy, float weight)[] entries)
        {
            var list = new System.Collections.Generic.List<(EnemyDefinition, float)>();
            foreach (var e in entries) if (e.enemy != null) list.Add((e.enemy, e.weight));
            return list.ToArray();
        }

        // ------------------------------------------------------------ 보스 구간 집중(2026-10-03)

        /// <summary>보스 구간의 일반 스폰: 초당 0.45마리, 동시 10마리까지(호위는 따로).</summary>
        public const float BossRoundSpawnRate = 0.45f;
        public const int BossRoundMaxAlive = 10;

        /// <summary>
        /// 지금 있는 두 스테이지 웨이브 에셋의 보스 구간만 고친다(Naval/Apply Boss Round Focus). 다른 구간·적 데이터는 그대로.
        /// </summary>
        public static void ApplyBossRoundFocus()
        {
            EnemyDefinition Def(string id) => AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{EneDir}/{id}.asset");
            var fast = Def("ene_fastboat");
            var boat = Def("ene_suicide_boat");
            var drone = Def("ene_drone");
            FocusBossRounds(fast, boat, drone,
                            AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet.asset"),
                            AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"),
                            AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage3.asset"));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 보스 구간은 보스전에 집중한다: 일반 스폰은 고속정 1 · 자폭 드론 0.5 · 자폭 보트 0.4만, 조금씩(초당 0.45 · 동시 10).
        /// 미사일정·잠수함·엘리트 초계함·지원정 같은 다른 적은 넣지 않는다(보스와 이름이 비슷한 엘리트 초계함 포함). 호위 고속정은 그대로.
        /// 웨이브 생성의 마지막에 부른다 — 앞의 Add…(가중치 더하기)가 보스 구간에 넣은 적도 여기서 덮어쓴다.
        /// </summary>
        private static void FocusBossRounds(EnemyDefinition fast, EnemyDefinition boat, EnemyDefinition drone, params RoundSet[] stages)
        {
            foreach (var set in stages)
            {
                if (set == null) continue;
                Configure(set, so =>
                {
                    var list = so.FindProperty("rounds");
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        var round = list.GetArrayElementAtIndex(i);
                        if (round.FindPropertyRelative("Boss").objectReferenceValue == null) continue;
                        round.FindPropertyRelative("SpawnRate").floatValue = BossRoundSpawnRate;
                        round.FindPropertyRelative("MaxAlive").intValue = BossRoundMaxAlive;
                        var entries = round.FindPropertyRelative("Entries");
                        var picks = new System.Collections.Generic.List<(EnemyDefinition, float)>();
                        if (fast != null) picks.Add((fast, 1f));
                        if (drone != null) picks.Add((drone, 0.5f));
                        if (boat != null) picks.Add((boat, 0.4f));
                        entries.arraySize = picks.Count;
                        for (int k = 0; k < picks.Count; k++)
                        {
                            var e = entries.GetArrayElementAtIndex(k);
                            e.FindPropertyRelative("Enemy").objectReferenceValue = picks[k].Item1;
                            e.FindPropertyRelative("Weight").floatValue = picks[k].Item2;
                        }
                    }
                });
            }
        }

        // ------------------------------------------------------------ 스테이지 2 보스: 현대화 초계함

        /// <summary>현대화 초계함 데이터만 만들고 스테이지 2 마지막 구간의 보스로 바꾼다(Naval/Add Stage 2 Corvette Boss).</summary>
        public static EnemyDefinition BuildCorvetteBossOnly(NavalPrefabBuilder.Result p)
        {
            EnsureFolder(EneDir);
            var corvette = BuildCorvetteBossData(p, EneDir);
            SetStage2Boss(AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"), corvette);
            AssetDatabase.SaveAssets();
            return corvette;
        }

        /// <summary>
        /// 현대화 초계함(2026-10-03): 체력 1500(1보스 1000 · 예전 2보스 항공전함 1600), 7 m/s, 선회 55°/초.
        /// 미사일 피해 24(4발, 접근 방향을 바꿔서), 함포 예고 사격 12 · 어뢰 30은 프리팹(ModernCorvetteBoss) 값.
        /// </summary>
        private static EnemyDefinition BuildCorvetteBossData(NavalPrefabBuilder.Result p, string eneDir)
        {
            if (p.ModernCorvette == null) return null;
            var def = Enemy("ene_boss_corvette", "현대화 초계함", Game.Combat.TargetKind.Surface, p.ModernCorvette,
                hp: 1500f, speed: 7f, turn: 55f, range: 30f, dmg: 24f, cd: 3f,
                missile: p.MissileEnemy, reveal: 0f, xp: 0, dir: eneDir);
            Classify(def, Game.Combat.TargetCategory.Boss, 10, EnemyRank.Boss);
            return def;
        }

        /// <summary>
        /// 스테이지 2 마지막 구간(7번째)의 보스를 바꾼다. 다른 구간·가중치는 그대로 둔다.
        /// 항공전함(ene_boss2) 데이터·프리팹은 지우지 않는다(이후 스테이지용).
        /// </summary>
        private static void SetStage2Boss(RoundSet stage2, EnemyDefinition boss)
        {
            if (stage2 == null || boss == null) return;
            Configure(stage2, so =>
            {
                var list = so.FindProperty("rounds");
                if (list == null || list.arraySize < 7) return;
                var round = list.GetArrayElementAtIndex(6);
                round.FindPropertyRelative("Title").stringValue = "현대화 초계함";
                round.FindPropertyRelative("Boss").objectReferenceValue = boss;
            });
        }

        // ------------------------------------------------------------ 일반 적 5종

        /// <summary>일반 적 5종(+기뢰) 데이터만 만들고 두 스테이지 웨이브에 넣는다(Naval/Add Normal Enemies).</summary>
        public static void BuildNormalEnemiesOnly(NavalPrefabBuilder.Result p)
        {
            EnsureFolder(EneDir);
            var defs = BuildNormalEnemyData(p, EneDir);
            AddNormalEnemies(AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet.asset"),
                             AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"), defs);
            AssetDatabase.SaveAssets();
        }

        private readonly struct NormalEnemyDefs
        {
            public readonly EnemyDefinition Armored, Torpedo, Artillery, Repair, MineLayer, Mine;
            public NormalEnemyDefs(EnemyDefinition armored, EnemyDefinition torpedo, EnemyDefinition artillery,
                                   EnemyDefinition repair, EnemyDefinition mineLayer, EnemyDefinition mine)
            {
                Armored = armored; Torpedo = torpedo; Artillery = artillery; Repair = repair; MineLayer = mineLayer; Mine = mine;
            }
        }

        /// <summary>
        /// 일반 적 5종 수치(2026-10-03). 고속정(체력 12·11 m/s)과 엘리트 미사일정(55) 사이.
        ///   장갑 돌격정: 고속정보다 3배 단단하고 느린 근접 점사.        경어뢰정: 40m 선회, 13초마다 어뢰 24(경고 2초).
        ///   포격 지원정: 40m 밖 곡사 2발(발당 10, 경고 원 2.6초), 9초마다. 수리 지원정: 공격 없음, 다친 아군을 초당 4 수리.
        ///   기뢰부설정: 진로 앞에 3.5초마다 기뢰(배당 6개까지). 기뢰: 체력 4, 폭발 22, 경험치 없음.
        /// 공격력이 있는 적의 공격 피해(dmg)는 한 번 공격(점사·어뢰·포탄 한 발) 기준.
        /// </summary>
        private static NormalEnemyDefs BuildNormalEnemyData(NavalPrefabBuilder.Result p, string eneDir)
        {
            EnemyDefinition armored = null, torpedo = null, artillery = null, repair = null, layer = null, mine = null;
            if (p.ArmoredBoat != null)
            {
                armored = Enemy("ene_armored_boat", "장갑 돌격정", Game.Combat.TargetKind.Surface, p.ArmoredBoat,
                    hp: 34f, speed: 8.5f, turn: 140f, range: 8f, dmg: 6f, cd: 1.9f,
                    missile: null, reveal: 0f, xp: 2, dir: eneDir);
                Classify(armored, Game.Combat.TargetCategory.SmallSurface, 2);
            }
            if (p.TorpedoBoat != null)
            {
                torpedo = Enemy("ene_torpedo_boat", "경어뢰정", Game.Combat.TargetKind.Surface, p.TorpedoBoat,
                    hp: 26f, speed: 10f, turn: 110f, range: 40f, dmg: 24f, cd: 13f,
                    missile: null, reveal: 0f, xp: 3, dir: eneDir);
                Classify(torpedo, Game.Combat.TargetCategory.SmallSurface, 2);
                Configure(torpedo, so => Set(so, "maxAlive", 2));
            }
            if (p.ArtilleryBoat != null)
            {
                artillery = Enemy("ene_artillery_boat", "포격 지원정", Game.Combat.TargetKind.Surface, p.ArtilleryBoat,
                    hp: 36f, speed: 6.5f, turn: 70f, range: 40f, dmg: 10f, cd: 9f,
                    missile: null, reveal: 0f, xp: 4, dir: eneDir);
                Classify(artillery, Game.Combat.TargetCategory.MediumSurface, 3);
                Configure(artillery, so => Set(so, "maxAlive", 2));
            }
            if (p.RepairBoat != null)
            {
                repair = Enemy("ene_repair_boat", "수리 지원정", Game.Combat.TargetKind.Surface, p.RepairBoat,
                    hp: 32f, speed: 8f, turn: 90f, range: 40f, dmg: 0f, cd: 0f,
                    missile: null, reveal: 0f, xp: 4, dir: eneDir);
                Classify(repair, Game.Combat.TargetCategory.MediumSurface, 3);
                Configure(repair, so => Set(so, "maxAlive", 1));
            }
            if (p.SeaMine != null)
            {
                mine = Enemy("ene_sea_mine", "부유 기뢰", Game.Combat.TargetKind.Surface, p.SeaMine,
                    hp: 4f, speed: 0f, turn: 0f, range: 0f, dmg: 22f, cd: 0f,
                    missile: null, reveal: 0f, xp: 0, dir: eneDir);
                Classify(mine, Game.Combat.TargetCategory.SmallSurface, 1);
            }
            if (p.MineLayer != null)
            {
                layer = Enemy("ene_minelayer", "기뢰부설정", Game.Combat.TargetKind.Surface, p.MineLayer,
                    hp: 34f, speed: 9f, turn: 90f, range: 30f, dmg: 0f, cd: 3.5f,
                    missile: null, reveal: 0f, xp: 4, dir: eneDir);
                Classify(layer, Game.Combat.TargetCategory.MediumSurface, 3);
                Configure(layer, so => Set(so, "maxAlive", 1));
                // 부설정 프리팹에 기뢰 데이터를 잇는다
                if (mine != null)
                {
                    using var scope = new PrefabUtility.EditPrefabContentsScope(AssetDatabase.GetAssetPath(p.MineLayer));
                    var comp = scope.prefabContentsRoot.GetComponent<Game.Enemies.MineLayer>();
                    if (comp != null) Configure(comp, so => Set(so, "mineDefinition", mine));
                }
            }
            return new NormalEnemyDefs(armored, torpedo, artillery, repair, layer, mine);
        }

        /// <summary>
        /// 웨이브 가중치(기존 적은 그대로 두고 더한다). 구간 번호는 1부터.
        ///   스테이지 1: 교전(3)부터 돌격정, 대함 위협(4)·수중 접촉(6)에 어뢰정, 포화 공격(5)부터 포격·수리정, 수중 접촉(6)부터 기뢰부설정.
        ///   스테이지 2: 수상함이 나오는 구간에 고루(드론 습격·제공권 상실 같은 항공 위주 구간은 뺀다).
        /// </summary>
        private static void AddNormalEnemies(RoundSet stage1, RoundSet stage2, NormalEnemyDefs d)
        {
            PutWeights(stage1, d.Armored, (3, 0.8f), (5, 1.0f), (7, 1.2f), (8, 0.6f));
            PutWeights(stage1, d.Torpedo, (4, 0.4f), (6, 0.5f), (7, 0.4f));
            PutWeights(stage1, d.Artillery, (5, 0.4f), (7, 0.5f));
            PutWeights(stage1, d.Repair, (5, 0.25f), (7, 0.35f));
            PutWeights(stage1, d.MineLayer, (6, 0.3f), (7, 0.3f));

            PutWeights(stage2, d.Armored, (1, 0.8f), (3, 0.8f), (6, 1.0f));
            PutWeights(stage2, d.Torpedo, (1, 0.4f), (4, 0.5f), (6, 0.4f));
            PutWeights(stage2, d.Artillery, (3, 0.5f), (6, 0.6f), (7, 0.4f));
            PutWeights(stage2, d.Repair, (3, 0.3f), (6, 0.4f), (7, 0.3f));
            PutWeights(stage2, d.MineLayer, (4, 0.35f), (6, 0.3f));
        }

        // ------------------------------------------------------------ 자폭 보트

        /// <summary>자폭 보트 데이터만 만들고 두 스테이지 웨이브에 넣는다(Naval/Add Suicide Boat).</summary>
        public static void BuildSuicideBoatOnly(NavalPrefabBuilder.Result p)
        {
            EnsureFolder(EneDir);
            var suicide = BuildSuicideBoatData(p.SuicideBoat, EneDir);
            AddSuicideBoat(AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet.asset"),
                           AssetDatabase.LoadAssetAtPath<RoundSet>($"{WavDir}/RoundSet_Stage2.asset"), suicide);
            AssetDatabase.SaveAssets();
        }

        /// <summary>자폭 보트: 고속정(12·11 m/s)보다 약하고 빠르다. 19 m/s ≈ 37노트로 함선 최고 30노트보다 빠르다. 부딪히면 30.</summary>
        private static EnemyDefinition BuildSuicideBoatData(GameObject prefab, string eneDir)
        {
            if (prefab == null) return null;
            var def = Enemy("ene_suicide_boat", "자폭 보트", Game.Combat.TargetKind.Surface, prefab,
                hp: 6f, speed: 19f, turn: 170f, range: 0f, dmg: 30f, cd: 0f,
                missile: null, reveal: 0f, xp: 1, dir: eneDir);
            Classify(def, Game.Combat.TargetCategory.SmallSurface, 1);
            return def;
        }

        /// <summary>
        /// 웨이브 가중치(기존 적은 그대로 두고 더함). 구간 번호는 1부터.
        /// 스테이지 1: 교전 0.5 · 포화 공격 0.8 · 총력전 1.0 / 스테이지 2: 드론 습격 0.8 · 총공세 1.0.
        /// </summary>
        private static void AddSuicideBoat(RoundSet stage1, RoundSet stage2, EnemyDefinition boat)
        {
            if (boat == null) return;
            PutWeights(stage1, boat, (3, 0.5f), (5, 0.8f), (7, 1.0f));
            PutWeights(stage2, boat, (2, 0.8f), (6, 1.0f));
        }

        private static void PutWeights(RoundSet rounds, EnemyDefinition enemy, params (int round, float weight)[] weights)
        {
            if (rounds == null || enemy == null) return;
            Configure(rounds, so =>
            {
                var list = so.FindProperty("rounds");
                foreach (var (roundNumber, weight) in weights)
                {
                    if (roundNumber < 1 || roundNumber > list.arraySize) continue;
                    var entries = list.GetArrayElementAtIndex(roundNumber - 1).FindPropertyRelative("Entries");
                    bool found = false;
                    for (int i = 0; i < entries.arraySize && !found; i++)
                    {
                        var e = entries.GetArrayElementAtIndex(i);
                        if (e.FindPropertyRelative("Enemy").objectReferenceValue != enemy) continue;
                        e.FindPropertyRelative("Weight").floatValue = weight;
                        found = true;
                    }
                    if (found) continue;
                    entries.arraySize++;
                    var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    added.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
                    added.FindPropertyRelative("Weight").floatValue = weight;
                }
            });
        }

        private static (EnemyDefinition cruiseSub, EnemyDefinition pcc) BuildStage2EnemyData(
            GameObject cruiseSubPrefab, GameObject pccPrefab, GameObject missile, string eneDir)
        {
            EnemyDefinition cruiseSub = null, pcc = null;

            // 순항미사일 원자력 잠수함: 52m 밖에서 맴돌며 10초 재장전 후 대함 순항미사일. 발사 뒤 5.5초 노출.
            if (cruiseSubPrefab != null)
            {
                cruiseSub = Enemy("ene_cruise_submarine", "순항미사일 잠수함", Game.Combat.TargetKind.Submarine, cruiseSubPrefab,
                    hp: 80f, speed: 4f, turn: 60f, range: 52f, dmg: 26f, cd: 10f,
                    missile: missile, reveal: 10f, xp: 6, dir: eneDir);
                Classify(cruiseSub, Game.Combat.TargetCategory.Submarine, 4, EnemyRank.Elite);
                Configure(cruiseSub, so => { Set(so, "salvoSize", 1); Set(so, "salvoSpacing", 0.15f); });
            }

            // 엘리트 초계함: 20m 교전 원, 4초 재장전 후 4문 일제사격(한 발 5). 체력 절반에서 한 번 전속 회피.
            if (pccPrefab != null)
            {
                pcc = Enemy("ene_pcc_corvette", "엘리트 초계함", Game.Combat.TargetKind.Surface, pccPrefab,
                    hp: 160f, speed: 8f, turn: 75f, range: 20f, dmg: 20f, cd: 4f,
                    missile: null, reveal: 0f, xp: 8, dir: eneDir);
                Classify(pcc, Game.Combat.TargetCategory.MediumSurface, 4, EnemyRank.Elite);
            }
            return (cruiseSub, pcc);
        }

        /// <summary>
        /// 스테이지 2 구간에 추가 적 가중치를 넣는다(이미 있으면 값만 고친다). 기존 적 가중치는 그대로 둔다.
        /// 구간 번호는 1부터: 잠수함 4·6·7구간, 초계함 3·5·6·7구간.
        /// </summary>
        private static void AddStage2Enemies(RoundSet rounds, EnemyDefinition cruiseSub, EnemyDefinition pcc)
        {
            if (rounds == null) { Debug.LogError("[Setup] RoundSet_Stage2가 없습니다."); return; }
            Configure(rounds, so =>
            {
                var list = so.FindProperty("rounds");
                void Put(int roundNumber, EnemyDefinition enemy, float weight)
                {
                    if (enemy == null || roundNumber < 1 || roundNumber > list.arraySize) return;
                    var entries = list.GetArrayElementAtIndex(roundNumber - 1).FindPropertyRelative("Entries");
                    for (int i = 0; i < entries.arraySize; i++)
                    {
                        var e = entries.GetArrayElementAtIndex(i);
                        if (e.FindPropertyRelative("Enemy").objectReferenceValue != enemy) continue;
                        e.FindPropertyRelative("Weight").floatValue = weight;
                        return;
                    }
                    entries.arraySize++;
                    var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    added.FindPropertyRelative("Enemy").objectReferenceValue = enemy;
                    added.FindPropertyRelative("Weight").floatValue = weight;
                }

                Put(4, cruiseSub, 0.25f);
                Put(6, cruiseSub, 0.45f);
                Put(7, cruiseSub, 0.55f);
                Put(3, pcc, 0.35f);
                Put(5, pcc, 0.6f);
                Put(6, pcc, 0.8f);
                Put(7, pcc, 1.0f);
            });
        }

        private static void Classify(EnemyDefinition def, Game.Combat.TargetCategory category, int value,
                                     EnemyRank rank = EnemyRank.Normal)
            => Configure(def, so =>
            {
                Set(so, "category", (int)category);
                Set(so, "targetValue", value);
                Set(so, "rank", (int)rank);
            });

        /// <summary>표적 분류별 효율(소형, 중형, 대형, 보스, 잠수함, 드론, 항공, 미사일). 0 = 공격하지 않음.</summary>
        private static void Efficiency(SerializedObject so, float small, float medium, float large, float boss,
                                       float sub, float drone, float air, float missile)
        {
            Set(so, "targetEfficiency.SmallSurface", small);
            Set(so, "targetEfficiency.MediumSurface", medium);
            Set(so, "targetEfficiency.LargeSurface", large);
            Set(so, "targetEfficiency.Boss", boss);
            Set(so, "targetEfficiency.Submarine", sub);
            Set(so, "targetEfficiency.Drone", drone);
            Set(so, "targetEfficiency.Air", air);
            Set(so, "targetEfficiency.Missile", missile);
        }

        private static EnemyDefinition Enemy(string id, string name, Game.Combat.TargetKind kind,
            GameObject prefab, float hp, float speed, float turn, float range, float dmg, float cd,
            GameObject missile, float reveal, int xp, string dir)
        {
            var def = CreateSO<EnemyDefinition>($"{dir}/{id}.asset");
            Configure(def, so =>
            {
                Set(so, "id", id);
                Set(so, "displayName", name);
                Set(so, "kind", (int)kind);
                Set(so, "prefab", prefab);
                Set(so, "maxHp", hp);
                Set(so, "moveSpeed", speed);
                Set(so, "turnRateDegPerSec", turn);
                Set(so, "preferredRange", range);
                Set(so, "attackDamage", dmg);
                Set(so, "attackCooldown", cd);
                Set(so, "missilePrefab", missile);
                Set(so, "revealDuration", reveal);
                Set(so, "xpReward", xp);
            });
            return def;
        }

        private static void SetEntry(SerializedProperty e, ModuleDefinition module,
                                    int x, int z, int rot = 0)
        {
            e.FindPropertyRelative("Module").objectReferenceValue = module;
            e.FindPropertyRelative("Origin").FindPropertyRelative("X").intValue = x;
            e.FindPropertyRelative("Origin").FindPropertyRelative("Z").intValue = z;
            e.FindPropertyRelative("RotationSteps").intValue = rot;
        }

        /// <summary>보스와 함께 나오는 호위함.</summary>
        private static void SetEscort(SerializedProperty round, EnemyDefinition escort, int count)
        {
            round.FindPropertyRelative("Escort").objectReferenceValue = escort;
            round.FindPropertyRelative("EscortCount").intValue = count;
        }

        private static void SetRound(SerializedProperty round, string title, float duration,
                                     float spawnRate, int maxAlive, EnemyDefinition boss,
                                     params (EnemyDefinition enemy, float weight)[] entries)
        {
            round.FindPropertyRelative("Title").stringValue = title;
            round.FindPropertyRelative("Duration").floatValue = duration;
            round.FindPropertyRelative("SpawnRate").floatValue = spawnRate;
            round.FindPropertyRelative("MaxAlive").intValue = maxAlive;
            round.FindPropertyRelative("Boss").objectReferenceValue = boss;

            var list = round.FindPropertyRelative("Entries");
            list.arraySize = entries.Length;

            for (int i = 0; i < entries.Length; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Enemy").objectReferenceValue = entries[i].enemy;
                e.FindPropertyRelative("Weight").floatValue = entries[i].weight;
            }
        }
    }
}
