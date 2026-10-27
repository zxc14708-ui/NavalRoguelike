using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using static Game.EditorTools.NavalEditorUtil;

namespace Game.EditorTools
{
    /// <summary>
    /// 시작 무장 보강(2026-10-11). 이 데이터·프리팹만 바꾼다 — 시작 함선 전체를 다시 짓지 않는다(Codex 작업 보존).
    ///   1. 시작 배치: 함수·함미에 무기가 하나씩 있게 — 범용 초계함 함미 끝(−3, 0)·지휘함 헬기갑판 좌현 옆(−3, −1)에 기관포(뒤를 봄 — 헬기갑판은 뒤가 트여야 한다).
    ///      강습함(함수·함미 기관포)·미사일 구축함(함수 VLS·함미 기관포)은 이미 그렇다.
    ///   2. 시작 화면 미리보기 조립품(SHIP_Patrol_Start·SHIP_Command_Start)만 같은 방식(NavalStartingShipBuilder.Assemble)으로 다시 만든다.
    ///   3. 함교 프리팹 다섯(시작 함선 넷 + 예전 MOD_Bridge)에 응급 수리 이펙트(FX_RepairPulse)를 연결한다(원격 기관총은 2026-10-21 삭제).
    ///   4. 손상통제반 설명 문구(손상통제 개편 — 선체 회복·응급 수리는 함교가, 블록은 함교를 키운다).
    /// 배치 실행: -executeMethod Game.EditorTools.NavalStarterArmament.Run
    /// </summary>
    public static class NavalStarterArmament
    {
        private const string Data = Root + "/Data/StartingShips";

        [MenuItem("Naval/Starting Ships/Add Stern Guns + Bridge Repair FX", priority = 46)]
        public static void Run()
        {
            // 새 장면을 연 뒤에 불러온다 — 장면을 새로 열면 참조 없는 에셋이 내려가 앞서 불러온 참조가 비어 버린다
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gun = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(Root + "/Data/Modules/mod_autocannon.asset");
            if (gun == null) throw new Exception("mod_autocannon 없음");
            // 2026-10-23: 시작 배치(함미 기관포 포함)는 NavalClassBridgePassives가 한 줄로 정한다 — 여기서는 더 넣지 않는다

            var repairFx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/VFX/FX_RepairPulse.prefab");
            foreach (var path in new[]
            {
                Root + "/Prefabs/StartingShips/Bridges/MOD_Bridge_Patrol.prefab",
                Root + "/Prefabs/StartingShips/Bridges/MOD_Bridge_Command.prefab",
                Root + "/Prefabs/StartingShips/Bridges/MOD_Bridge_Assault.prefab",
                Root + "/Prefabs/StartingShips/Bridges/MOD_Bridge_Missile.prefab",
                Root + "/Prefabs/Modules/MOD_Bridge.prefab",
            })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var bridge = root.GetComponent<BridgeModule>();
                    if (bridge == null) { Debug.LogWarning($"[Starter] {path}: BridgeModule 없음"); continue; }
                    Configure(bridge, so =>
                    {
                        if (repairFx != null) Set(so, "burstEffect", repairFx);
                    });
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[Starter] {System.IO.Path.GetFileName(path)}: 응급 수리 이펙트 연결");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var bay = AssetDatabase.LoadAssetAtPath<ModuleDefinition>(Root + "/Data/Modules/mod_repairbay.asset");
            if (bay != null) Configure(bay, so => Set(so, "description", NavalDataBuilder.RepairBayDescription));
            AssetDatabase.SaveAssets();
        }

        private static void Patch(int kind, string code, ModuleDefinition gun, GridCoord at)
        {
            var loadout = AssetDatabase.LoadAssetAtPath<StartingLoadout>($"{Data}/Loadout_{code}_Start.asset");
            if (loadout == null) throw new Exception($"Loadout_{code}_Start 없음");
            bool has = false;
            foreach (var e in loadout.Entries)
                if (e.Module == gun && e.Origin.Equals(at)) has = true;
            if (has) Debug.Log($"[Starter] {code}: 함미 기관포가 이미 있습니다.");
            else AddEntry(loadout, gun, at, code);

            // 미리보기 조립품: Codex 생성기의 같은 조립 함수(같은 이름·경로로 저장 — 개념 에셋의 참조 유지)
            var assemble = typeof(NavalStartingShipBuilder).GetMethod("Assemble", BindingFlags.NonPublic | BindingFlags.Static);
            if (assemble == null) throw new Exception("NavalStartingShipBuilder.Assemble 없음");
            var prefab = assemble.Invoke(null, new object[] { kind, false, loadout }) as GameObject;
            Debug.Log($"[Starter] {code}: 미리보기 {(prefab != null ? AssetDatabase.GetAssetPath(prefab) : "실패")}");
        }

        private static void AddEntry(StartingLoadout loadout, ModuleDefinition gun, GridCoord at, string code)
        {
            var so = new SerializedObject(loadout);
            var entries = so.FindProperty("entries");
            int i = entries.arraySize;
            entries.InsertArrayElementAtIndex(i);
            var el = entries.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("Module").objectReferenceValue = gun;
            el.FindPropertyRelative("Origin").FindPropertyRelative("X").intValue = at.X;
            el.FindPropertyRelative("Origin").FindPropertyRelative("Z").intValue = at.Z;
            el.FindPropertyRelative("RotationSteps").intValue = 2;   // 뒤를 본다
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(loadout);
            Debug.Log($"[Starter] {code}: 함미 기관포 ({at.X},{at.Z}) 추가");
        }
    }
}
