using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Ship;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 함급 블록 → 함교 패시브(2026-10-23). 하나만 있으면 끝인 함급 블록의 효과를 각 함교가 기본으로 낸다.
    ///   - 범용 초계함 함교: 레이더(탐지 52 · 추적 +3 → 함교 추적 4)
    ///   - 지휘함 함교: 전투단 통신 중계(호위함 능력 재사용 +15%)
    ///   - 고속 강습함 함교: 강습 추진 흡기(속력 +10% · 가속 +15% · 항해 중 실탄 피해 +10%)
    ///   - 미사일 구축함 함교: 다중 표적 사격통제(추적 +2 · 유도 사거리 +10%) + 미사일 재장전 구획(장전 −15%), 탐지 48(VLS 사거리)
    /// 시작 배치는 한 줄(앞 → 뒤):
    ///   - 범용 초계함: 기관포 · 함교 · CIWS · 기관포
    ///   - 지휘함: 기관포 · 함교 · CIWS · 기관포 · 헬기데크(헬기데크는 선미만 — 7칸)
    ///   - 고속 강습함: 기관포 · 함교 · 탄약고 · 기관포
    ///   - 미사일 구축함: VLS · 함교 · VLS · 기관포
    /// 블록(통신 중계·흡기·사격통제·재장전 구획)은 그대로 남긴다 — 그 효과를 이미 내는 함교에는 카드로 나오지 않고(RefitDraft),
    /// 다른 함급은 지금처럼 얻을 수 있다. 시작 화면 미리보기(SHIP_*_Start)는 같은 조립 함수로 다시 만든다.
    /// 배치 실행: -executeMethod Game.EditorTools.NavalClassBridgePassives.Run
    /// </summary>
    public static class NavalClassBridgePassives
    {
        private const string Data = Root + "/Data/StartingShips";
        private static readonly string[] Codes = { "Patrol", "Command", "Assault", "Missile" };

        [MenuItem("Naval/Starting Ships/Class Bridge Passives + Line Start", priority = 47)]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ModuleDefinition M(string id) => AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{Root}/Data/Modules/{id}.asset")
                                             ?? throw new Exception(id + " 없음");
            var gun = M("mod_autocannon"); var ciws = M("mod_ciws"); var deck = M("mod_helideck"); var mag = M("mod_magazine"); var vls = M("mod_vls");

            // ---- 함교 패시브(값은 예전 블록과 같다)
            Bridge("patrol", "레이더 내장 — 탐지 52 · 동시 추적 +3", so =>
            {
                Set(so, "stats.DetectionRange", 52f);
                Set(so, "stats.ExtraTrackedTargets", 4);   // 함교 1 + 레이더 3
            });
            Bridge("command", "전투단 통신 중계 내장 — 호위함 능력 재사용 속도 +15%", so => Set(so, "stats.EscortFireRateBonus", 0.15f));
            Bridge("assault", "강습 추진 흡기 내장 — 최고속력 +10% · 가속 +15% · 항해 중 실탄 피해 +10%", so =>
            {
                Set(so, "stats.SpeedBonus", 0.1f);
                Set(so, "stats.AccelerationBonus", 0.15f);
                Set(so, "stats.MovingGunDamageBonus", 0.1f);
            });
            Bridge("missile", "다중 표적 사격통제 · 미사일 재장전 구획 내장 — 동시 추적 +2 · 유도 사거리 +10% · 미사일 장전 −15% · 탐지 48", so =>
            {
                Set(so, "stats.DetectionRange", 48f);
                Set(so, "stats.ExtraTrackedTargets", 3);   // 함교 1 + 사격통제 2
                Set(so, "stats.GuidedRangeBonus", 0.1f);
                Set(so, "stats.MissileReloadReduction", 0.15f);
            });

            // ---- 시작 배치: 한 줄
            Loadout(0, (gun, 2, 0, 0), (ciws, -2, 0, 2), (gun, -3, 0, 2));
            Loadout(1, (gun, 2, 0, 0), (ciws, -2, 0, 2), (gun, -3, 0, 2), (deck, -4, 0, 0));
            Loadout(2, (gun, 2, 0, 0), (mag, -2, 0, 0), (gun, -3, 0, 2));
            Loadout(3, (vls, 2, 0, 0), (vls, -2, 0, 0), (gun, -3, 0, 2));

            // ---- 시작 화면 문구
            Summary(0, "기관포 · CIWS · 기관포\n함교 레이더 내장(탐지 52 · 동시 추적 +3). 작은 범용 기본형, 카드로 확장.");
            Summary(1, "기관포 · CIWS · 기관포 · 헬기데크\n기본 고속정 2척. 함교 통신 중계 내장(호위함 능력 재사용 속도 +15%).");
            Summary(2, "기관포 · 탄약고 · 기관포\n함교 흡기 내장(속력 +10% · 가속 +15% · 항해 중 실탄 피해 +10%).");
            Summary(3, "VLS · VLS · 기관포\n함교 사격통제·재장전 구획 내장(추적 +2 · 유도 사거리 +10% · 미사일 장전 -15% · 탐지 48).");

            AssetDatabase.SaveAssets();

            // ---- 미리보기 조립품(같은 조립 함수 — 개념 에셋의 참조 유지)
            var assemble = typeof(NavalStartingShipBuilder).GetMethod("Assemble", BindingFlags.NonPublic | BindingFlags.Static)
                           ?? throw new Exception("NavalStartingShipBuilder.Assemble 없음");
            for (int k = 0; k < Codes.Length; k++)
            {
                var loadout = AssetDatabase.LoadAssetAtPath<StartingLoadout>($"{Data}/Loadout_{Codes[k]}_Start.asset");
                var prefab = assemble.Invoke(null, new object[] { k, false, loadout }) as GameObject;
                Debug.Log($"[Class Bridge] {Codes[k]}: 미리보기 {(prefab != null ? AssetDatabase.GetAssetPath(prefab) : "실패")}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Class Bridge] 함교 패시브 · 한 줄 시작 배치 적용 완료");
        }

        private static void Bridge(string code, string passive, Action<SerializedObject> stats)
        {
            var def = AssetDatabase.LoadAssetAtPath<ModuleDefinition>($"{Data}/Modules/mod_bridge_{code}.asset")
                      ?? throw new Exception($"mod_bridge_{code} 없음");
            Configure(def, so =>
            {
                stats(so);
                Set(so, "description", $"3칸 함교. 지휘·손상통제·기만체·전속·항해 레이더에 함급 패시브: {passive}. 함교가 부서지면 함께 잃는다.");
            });
        }

        private static void Loadout(int kind, params (ModuleDefinition def, int x, int z, int rot)[] modules)
        {
            var loadout = AssetDatabase.LoadAssetAtPath<StartingLoadout>($"{Data}/Loadout_{Codes[kind]}_Start.asset")
                          ?? throw new Exception($"Loadout_{Codes[kind]}_Start 없음");
            var so = new SerializedObject(loadout);
            var entries = so.FindProperty("entries");
            // 함교(첫 항목)는 그대로 두고 나머지를 새로
            var bridge = entries.GetArrayElementAtIndex(0).FindPropertyRelative("Module").objectReferenceValue as ModuleDefinition;
            if (bridge == null || bridge.Type != ModuleType.Bridge) throw new Exception($"{loadout.name}: 첫 항목이 함교가 아님");
            entries.arraySize = 1 + modules.Length;
            for (int i = 0; i < modules.Length; i++)
            {
                var e = entries.GetArrayElementAtIndex(i + 1);
                e.FindPropertyRelative("Module").objectReferenceValue = modules[i].def;
                e.FindPropertyRelative("Origin.X").intValue = modules[i].x;
                e.FindPropertyRelative("Origin.Z").intValue = modules[i].z;
                e.FindPropertyRelative("RotationSteps").intValue = modules[i].rot;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loadout);
        }

        private static void Summary(int kind, string text)
        {
            var concept = AssetDatabase.LoadAssetAtPath<StartingShipConcept>($"{Data}/ShipConcept_{Codes[kind]}.asset")
                          ?? throw new Exception($"ShipConcept_{Codes[kind]} 없음");
            Configure(concept, so => Set(so, "startSummary", text));
        }
    }
}
