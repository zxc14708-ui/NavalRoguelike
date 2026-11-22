using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Modules;
using Game.Modules.Runtime;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 지원 블록 개편(2026-11-22) 빌더 — 시작 함정 빌더의 재질 · 메시 도구를 같이 쓴다.
    ///   - 새 블록 3종: 포격 지원 요청 · 정찰 드론 격납고 · 응급 차폐 장갑(임시 모델 · 정의 · 아이콘, Resources/Modules).
    ///   - 전투단 통신 중계: 지원 함정 호출 블록으로 모델(신호 마스트 · 신호기 · 내릴 보트)과 설명을 바꾼다(같은 경로 · GUID 유지).
    ///   - 강화 단계(2단계, Resources/Upgrades): 손상 통제반 · 강습 추진 흡기 · 통신 중계 · 새 3종 — 지원 블록 "Lv3까지 강화해야 하나 더" 규칙에 필요.
    /// 다른 블록 · 함교 · 시작 배치는 건드리지 않는다. 배치 실행: -executeMethod Game.EditorTools.NavalStartingShipBuilder.BuildSupportBlocksBatch (그래픽 필요 — 아이콘)
    /// </summary>
    public static partial class NavalStartingShipBuilder
    {
        public static void BuildSupportBlocksBatch()
        {
            try { BuildSupportBlocks(); Debug.Log("[SupportBlocks] PASS"); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Naval/Art/Build Support Blocks (Fire Support · Recon Drone · Shield · Fleet Relay)")]
        public static void BuildSupportBlocks()
        {
            Report.Clear();
            Palette();
            var orange = OwnMaterial("Role_FireSupport", new Color(.98f, .49f, .24f));
            var green = OwnMaterial("Role_ReconDrone", new Color(.6f, .86f, .38f));
            var cyan = OwnMaterial("Role_Shield", new Color(.4f, .82f, 1f));
            var teal = OwnMaterial("Role_FleetRelay", new Color(.35f, .88f, .67f));

            // ---- 새 블록 3종
            var fire = SupportBlock("FireSupport", "mod_firesupport", "포격 지원 요청", ModuleType.FireSupport,
                "일정 시간마다 적 수상함이 가장 많이 모인 곳에 경고 원을 띄우고 화면 밖 순양함이 포격한다. Lv1·2·3 = 30·27·24초마다 6·9·12발(한 발 피해 18 · 반경 3.5m), 낙탄 반경 7·8.5·10m.",
                t =>
                {
                    Facet("FireDirectionCabin", t, new Vector3(0, .5f, -.1f), new Vector3(1.2f, .75f, 1.1f), .88f, 0, Hull);
                    Box("CabinWindows", t, new Vector3(0, .7f, .42f), new Vector3(.95f, .16f, .03f), Glass);
                    Pipe("DirectorPost", t, new Vector3(0, 1.0f, -.1f), .09f, .3f, Dark);
                    Box("Rangefinder", t, new Vector3(0, 1.2f, -.1f), new Vector3(1.05f, .14f, .16f), Dark);
                    for (int s = -1; s <= 1; s += 2) Pipe("RangefinderLens", t, new Vector3(s * .55f, 1.2f, -.1f), .09f, .2f, Glass).transform.localRotation = Quaternion.Euler(0, 0, 90);
                    Pipe("RadioWhip", t, new Vector3(.52f, 1.25f, -.45f), .022f, 1.3f, Dark);
                    Box("ShellMark", t, new Vector3(0, .32f, .5f), new Vector3(.9f, .08f, .02f), orange);
                }, orange);

            var drone = SupportBlock("ReconDrone", "mod_recondronebay", "정찰 드론 격납고", ModuleType.ReconDroneBay,
                "일정 시간마다 정찰 드론이 날아가 둘레 24m의 적을 드러내고(잠항 잠수함 · 반잠수정 포함) 가장 큰 적에 약점 표시(받는 피해 +20%)를 붙인다. Lv1·2·3 = 25·22·18초마다 1·2·3기, 6·8·10초 정찰.",
                t =>
                {
                    Facet("Hangar", t, new Vector3(0, .42f, -.15f), new Vector3(1.4f, .6f, 1.2f), .94f, 0, Hull);
                    Box("HangarDoor", t, new Vector3(0, .4f, .46f), new Vector3(1.0f, .46f, .03f), Dark);
                    for (int n = 0; n < 4; n++) Box("DoorRib", t, new Vector3(0, .22f + n * .12f, .48f), new Vector3(1.0f, .02f, .02f), Deck);
                    var rail = Box("LaunchRail", t, new Vector3(0, .86f, -.05f), new Vector3(.14f, .06f, 1.25f), Dark);
                    rail.transform.localRotation = Quaternion.Euler(-10, 0, 0);
                    Box("DroneFuselage", t, new Vector3(0, .98f, .1f), new Vector3(.14f, .12f, .62f), White);
                    Box("DroneWing", t, new Vector3(0, 1.0f, .14f), new Vector3(1.05f, .025f, .16f), White);
                    Box("DroneTail", t, new Vector3(0, 1.0f, -.18f), new Vector3(.36f, .02f, .1f), White);
                    Pipe("DataLinkDome", t, new Vector3(-.5f, .82f, -.55f), .14f, .14f, White);
                }, green);

            var shield = SupportBlock("EmergencyShield", "mod_emergencyshield", "응급 차폐 장갑", ModuleType.EmergencyShield,
                "한 번에 선체의 8% 이상 피해를 받으면 그 한 방부터 3초 동안 받는 피해 −40% 보호막이 켜진다. 재사용 Lv1·2·3 = 40·30·20초.",
                t =>
                {
                    Facet("ArmorCasemate", t, new Vector3(0, .36f, 0), new Vector3(1.5f, .5f, 1.5f), .86f, 0, Hull);
                    Pipe("EmitterBase", t, new Vector3(0, .72f, 0), .42f, .2f, Dark);
                    Pipe("EmitterCore", t, new Vector3(0, .92f, 0), .26f, .22f, Glass);
                    Pipe("EmitterCap", t, new Vector3(0, 1.08f, 0), .34f, .07f, White);
                    for (int s = 0; s < 4; s++)
                    {
                        float a = s * 90f * Mathf.Deg2Rad;
                        Box("CapacitorPanel", t, new Vector3(Mathf.Cos(a) * .62f, .36f, Mathf.Sin(a) * .62f), new Vector3(.3f, .32f, .3f), cyan)
                            .transform.localRotation = Quaternion.Euler(0, -s * 90f, 0);
                    }
                }, cyan);

            // ---- 전투단 통신 중계: 지원 함정 호출
            var relay = SupportBlock("FleetRelay", "mod_fleetrelay", "전투단 통신 중계", ModuleType.FleetRelay,
                "일정 시간마다 보급선과 고속정 편대를 부른다. 보급선은 탄약 전량 보급 · 블록 수리 1·2·3개 · 재사용 중인 스킬 −30·40·50%, 고속정 2·4·6척은 둘레 26m 적을 기관포로 쏜다. Lv1·2·3 = 45·40·35초마다 15·18·20초 지원. 호위함 자율 무장 재사용 +15%는 그대로.",
                t =>
                {
                    Facet("RadioCabinet", t, new Vector3(-.2f, .5f, -.2f), new Vector3(1.05f, .75f, 1.1f), .88f, 0, Hull);
                    Pipe("SignalMast", t, new Vector3(-.2f, 1.35f, -.2f), .06f, 1.0f, Dark);
                    Box("Yardarm", t, new Vector3(-.2f, 1.62f, -.2f), new Vector3(1.1f, .04f, .04f), Dark);
                    Box("SignalFlag", t, new Vector3(-.62f, 1.45f, -.2f), new Vector3(.02f, .2f, .26f), Warning);
                    Box("SignalFlag", t, new Vector3(.22f, 1.45f, -.2f), new Vector3(.02f, .2f, .26f), teal);
                    Box("SignalFlag", t, new Vector3(-.62f, 1.22f, -.2f), new Vector3(.02f, .2f, .26f), White);
                    var dish = Pipe("CommsDish", t, new Vector3(-.2f, 1.02f, .38f), .3f, .08f, White); dish.transform.localRotation = Quaternion.Euler(55, 0, 0);
                    // 내릴 보트(보급 연락정)와 대빗
                    Box("TenderHull", t, new Vector3(.55f, .4f, .2f), new Vector3(.36f, .2f, .9f), White);
                    Box("TenderStripe", t, new Vector3(.55f, .44f, .2f), new Vector3(.37f, .04f, .82f), teal);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        Pipe("Davit", t, new Vector3(.75f, .62f, .2f + s * .32f), .03f, .5f, Dark);
                        Box("DavitArm", t, new Vector3(.66f, .86f, .2f + s * .32f), new Vector3(.22f, .03f, .03f), Dark);
                    }
                }, teal, keepCount: true);

            // ---- 강화 단계(없을 때만 만든다 — 손으로 고친 값은 덮어쓰지 않음)
            Profile(LoadModule("repairbay"), "UPG_RepairBay", new[]
            {
                ("손상 통제반 강화 I", "수리 속도 +25% · 내구 +20%", new ModuleStatModifier { repairMultiplier = 1.25f, maxHpMultiplier = 1.2f }),
                ("손상 통제반 강화 II", "수리 속도 +50% · 내구 +40%", new ModuleStatModifier { repairMultiplier = 1.5f, maxHpMultiplier = 1.4f }),
            });
            Profile(AssetDatabase.LoadAssetAtPath<ModuleDefinition>(LiveModules + "/mod_turbointake.asset"), "UPG_TurboIntake", new[]
            {
                ("강습 추진 흡기 강화 I", "속력 · 가속 보너스 +25% · 내구 +20%", new ModuleStatModifier { speedMultiplier = 1.25f, maxHpMultiplier = 1.2f }),
                ("강습 추진 흡기 강화 II", "속력 · 가속 보너스 +50% · 내구 +40%", new ModuleStatModifier { speedMultiplier = 1.5f, maxHpMultiplier = 1.4f }),
            });
            Profile(relay, "UPG_FleetRelay", new[]
            {
                ("전투단 통신 중계 강화 I", "40초마다 · 보급선 Lv2 + 고속정 4척 · 수리 2개 · 스킬 −40% · 18초 지원", new ModuleStatModifier { maxHpMultiplier = 1.2f }),
                ("전투단 통신 중계 강화 II", "35초마다 · 보급선 Lv3 + 고속정 6척 · 수리 3개 · 스킬 −50% · 20초 지원", new ModuleStatModifier { maxHpMultiplier = 1.4f }),
            });
            Profile(fire, "UPG_FireSupport", new[]
            {
                ("포격 지원 요청 강화 I", "27초마다 9발 · 낙탄 반경 8.5m", new ModuleStatModifier { maxHpMultiplier = 1.2f }),
                ("포격 지원 요청 강화 II", "24초마다 12발 · 낙탄 반경 10m", new ModuleStatModifier { maxHpMultiplier = 1.4f }),
            });
            Profile(drone, "UPG_ReconDrone", new[]
            {
                ("정찰 드론 격납고 강화 I", "22초마다 드론 2기 · 8초 정찰", new ModuleStatModifier { maxHpMultiplier = 1.2f }),
                ("정찰 드론 격납고 강화 II", "18초마다 드론 3기 · 10초 정찰", new ModuleStatModifier { maxHpMultiplier = 1.4f }),
            });
            Profile(shield, "UPG_EmergencyShield", new[]
            {
                ("응급 차폐 장갑 강화 I", "재사용 30초 · 내구 +20%", new ModuleStatModifier { maxHpMultiplier = 1.2f }),
                ("응급 차폐 장갑 강화 II", "재사용 20초 · 내구 +40%", new ModuleStatModifier { maxHpMultiplier = 1.4f }),
            });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            foreach (var line in Report) Debug.Log("[SupportBlocks] " + line);
        }

        /// <summary>1×1 지원 블록: 갑판 받침 + 모델 + 역할 띠, 런타임 · 정의 · 아이콘. 같은 경로면 덮어써 GUID를 지킨다.</summary>
        private static ModuleDefinition SupportBlock(string code, string id, string title, ModuleType type, string description,
                                                     Action<Transform> model, Material role, bool keepCount = false)
        {
            _meshPrefix = "Block_" + code; _meshIndex = 0;
            var go = new GameObject("BLK_" + code);
            switch (type)
            {
                case ModuleType.FireSupport: go.AddComponent<FireSupportModule>(); break;
                case ModuleType.ReconDroneBay: go.AddComponent<ReconDroneBayModule>(); break;
                case ModuleType.EmergencyShield: go.AddComponent<EmergencyShieldModule>(); break;
                case ModuleType.FleetRelay: go.AddComponent<FleetRelayModule>(); break;
                default: throw new Exception("지원 블록이 아님: " + type);
            }
            var t = new GameObject("Model").transform; t.SetParent(go.transform, false);
            Facet("DeckBase", t, new Vector3(0, .12f, 0), new Vector3(1.78f, .24f, 1.78f), .97f, 0, Deck);
            model(t);
            Box("RoleMark", t, new Vector3(.89f, .22f, 0), new Vector3(.019f, .07f, 1.15f), role);
            Combine(t, null); PersistMeshes(go, _meshPrefix);
            var prefab = SavePrefab(go, Prefabs + "/Blocks");

            string path = LiveModules + "/" + id + ".asset";
            bool existed = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(path) != null;
            var def = CreateSO<ModuleDefinition>(path);
            NavalIconBaker.Bake(prefab, id);             // 배치 실행의 첫 렌더는 어둡게 나올 수 있어 한 번 버리고
            var icon = NavalIconBaker.Bake(prefab, id);  // 다시 굽는다
            if (icon == null) throw new Exception("아이콘을 굽지 못함: " + id);
            Configure(def, so =>
            {
                Set(so, "id", id); Set(so, "displayName", title); Set(so, "description", description);
                Set(so, "type", (int)type); Set(so, "prefab", prefab); Set(so, "icon", icon);
                if (!existed || !keepCount)
                {
                    Set(so, "weight", .65f); Set(so, "maxHp", 40f); Set(so, "heightClass", 1);
                    Set(so, "width", 1); Set(so, "height", 1);
                }
            });
            Report.Add($"BLOCK {code}: {id} {(existed ? "updated" : "created")} · prefab {AssetDatabase.GetAssetPath(prefab)} · icon ok");
            return def;
        }

        private static void Profile(ModuleDefinition def, string name, (string title, string desc, ModuleStatModifier mod)[] stages)
        {
            if (def == null) throw new Exception("강화 단계 대상 없음: " + name);
            string path = "Assets/_Game/Resources/Upgrades/" + name + ".asset";
            foreach (var guid in AssetDatabase.FindAssets("t:ModuleUpgradeProfile", new[] { "Assets/_Game/Resources/Upgrades" }))
            {
                var existing = AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (existing != null && existing.Module == def && AssetDatabase.GetAssetPath(existing) != path)
                { Report.Add($"PROFILE {name}: 이미 {AssetDatabase.GetAssetPath(existing)} 있음 — 건너뜀"); return; }
            }
            var profile = AssetDatabase.LoadAssetAtPath<ModuleUpgradeProfile>(path);
            if (profile != null && profile.MaxLevel > 0) { Report.Add($"PROFILE {name}: 있음 — 그대로"); return; }
            profile ??= CreateSO<ModuleUpgradeProfile>(path);
            var list = new List<ModuleUpgradeStage>();
            foreach (var s in stages) list.Add(new ModuleUpgradeStage(s.title, s.desc, s.mod));
            profile.EditorSetup(def, list);
            EditorUtility.SetDirty(profile);
            Report.Add($"PROFILE {name}: {def.Id} 2단계 생성");
        }
    }
}
