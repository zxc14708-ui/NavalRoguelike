using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Progression;
using Game.Refit;
using Game.Ship;
using Game.TaskForce;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// 데이터 동기화 검사(2026-10-12 점검 O05). 빌더 코드로 만든 에셋·프리팹·씬이 지금 게임 코드가 기대하는 모양과 맞는지
    /// 빌드 전에 본다 — 빌더나 코드를 고치고 에셋을 다시 만들지 않았을 때 생기는 빈 참조·빠진 파일·어긋난 설명을 잡는다.
    ///   오류(빌드를 멈춤): 스크립트 빠짐 · 끊긴 참조 · 프리팹 없는 블록/적 · 겹치는 id · 코드가 읽는 Resources 경로 없음 ·
    ///                    씬 필수 구성 빠짐 · 강화 외형 칸 비어 있음 · 강화 데이터가 블록을 안 가리킴
    ///   경고(기록만): 아이콘 없음 · 게임에서 안 쓰는 블록/적 · 강화 설명 수치 ≠ 실제 배율 · "최대 N개" ≠ 최대 개수 · 강화 외형 단계 부족
    /// 메뉴 Naval/Check/Data Sync, 모든 플레이어 빌드 직전(끄기: Naval/Check/Data Sync Before Build 또는 -skipDataSync),
    /// 배치: -executeMethod Game.EditorTools.DataSyncCheck.RunBatch [-dataSyncOut 파일].
    /// 결과는 Logs/DataSync.md에 남는다. 에셋을 고치지 않는다(읽기만).
    /// </summary>
    public static class DataSyncCheck
    {
        private const string Root = NavalEditorUtil.Root;
        private const string MainScene = Root + "/Scenes/Prototype_Main.unity";
        private const string GatePref = "Naval.DataSync.BeforeBuild";
        private const string GateMenu = "Naval/Check/Data Sync Before Build";

        public sealed class Report
        {
            public readonly List<(string text, UnityEngine.Object context)> Errors = new(), Warnings = new();
            public readonly List<string> Notes = new();
            public void Error(string text, UnityEngine.Object context = null) => Errors.Add((text, context));
            public void Warn(string text, UnityEngine.Object context = null) => Warnings.Add((text, context));

            public string ToMarkdown()
            {
                var sb = new StringBuilder();
                sb.AppendLine("# 데이터 동기화 검사(O05)");
                sb.AppendLine($"- 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss} · Unity {Application.unityVersion}");
                foreach (var n in Notes) sb.AppendLine($"- {n}");
                sb.AppendLine();
                sb.AppendLine($"## 오류 {Errors.Count}");
                foreach (var (t, _) in Errors) sb.AppendLine($"- {t}");
                sb.AppendLine();
                sb.AppendLine($"## 경고 {Warnings.Count}");
                foreach (var (t, _) in Warnings) sb.AppendLine($"- {t}");
                sb.AppendLine();
                sb.AppendLine(Errors.Count == 0 ? "## 결과: 통과" : "## 결과: 실패 — 위 오류를 고치거나 해당 빌더를 다시 실행하세요");
                return sb.ToString();
            }
        }

        public static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "DataSync.md"));

        // ------------------------------------------------------------ 진입점

        [MenuItem("Naval/Check/Data Sync (O05)", priority = 90)]
        public static void RunFromMenu()
        {
            var r = Run();
            Write(r, ReportPath);
            foreach (var (t, c) in r.Errors) Debug.LogError($"[DataSync] {t}", c);
            foreach (var (t, c) in r.Warnings) Debug.LogWarning($"[DataSync] {t}", c);
            Debug.Log($"[DataSync] 오류 {r.Errors.Count} · 경고 {r.Warnings.Count} → {ReportPath}");
        }

        [MenuItem(GateMenu, priority = 91)]
        private static void ToggleGate() => EditorPrefs.SetBool(GatePref, !GateEnabled);

        [MenuItem(GateMenu, true)]
        private static bool ToggleGateValidate()
        {
            Menu.SetChecked(GateMenu, GateEnabled);
            return true;
        }

        /// <summary>검증 빌드가 BuildPlayer 직전에 전체 검사를 했으면 true — 빌드 게이트가 한 번 더 돌지 않는다.</summary>
        internal static bool CheckedForBuild;

        /// <summary>검증 빌드 직전: 씬까지 전체 검사, 결과 기록. 오류가 있으면 false.</summary>
        public static bool RunBeforeVerificationBuild()
        {
            var r = Run();
            Write(r, ReportPath);
            foreach (var (t, c) in r.Errors) Debug.LogError($"[DataSync] {t}", c);
            Debug.Log($"[DataSync] 검증 빌드 전 검사: 오류 {r.Errors.Count} · 경고 {r.Warnings.Count} → {ReportPath}");
            CheckedForBuild = r.Errors.Count == 0;
            return r.Errors.Count == 0;
        }

        public static bool GateEnabled => EditorPrefs.GetBool(GatePref, true);

        /// <summary>배치 진입점: 결과 파일을 쓰고 오류가 있으면 종료 코드 1.</summary>
        public static void RunBatch()
        {
            string output = ReportPath;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-dataSyncOut") output = Path.GetFullPath(args[i + 1]);
            int code = 1;
            try
            {
                var r = Run();
                Write(r, output);
                Debug.Log($"[DataSync] 오류 {r.Errors.Count} · 경고 {r.Warnings.Count} → {output}");
                code = r.Errors.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { Debug.LogException(e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        private static void Write(Report r, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, r.ToMarkdown(), new UTF8Encoding(false));
        }

        /// <summary>openScene: 메인 씬이 안 열려 있으면 잠깐 열어서 본다(빌드 중에는 false — 열린 경우에만 본다).</summary>
        public static Report Run(bool openScene = true)
        {
            var r = new Report();
            var build = BuildAssetSet();
            var modules = Assets<ModuleDefinition>();
            var enemies = Assets<EnemyDefinition>();
            CheckModules(r, modules);
            CheckUpgrades(r);
            CheckEnemies(r, enemies, build);
            CheckResourcePaths(r);
            int prefabs = CheckPrefabsAndData(r, build);
            CheckScene(r, openScene);
            r.Notes.Add($"블록 {modules.Count} · 적 {enemies.Count} · 프리팹 {prefabs} · 빌드에 들어가는 에셋 {build.Count} · 씬 {Path.GetFileName(MainScene)}");
            r.Notes.Add("빌드에 안 들어가는 에셋(씬·Resources 어디서도 안 쓰는 옛 프리팹 등)의 문제는 경고로만 적는다");
            return r;
        }

        /// <summary>빌드에 실제로 들어가는 에셋: 빌드 씬과 Resources 폴더, 그리고 그것들이 가리키는 모든 것.</summary>
        private static HashSet<string> BuildAssetSet()
        {
            var roots = new List<string> { MainScene };
            foreach (var s in EditorBuildSettings.scenes) if (s.enabled && !roots.Contains(s.path)) roots.Add(s.path);
            foreach (var path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/") && path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path)) roots.Add(path);
            return new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        }

        // ------------------------------------------------------------ 블록

        private static void CheckModules(Report r, List<ModuleDefinition> modules)
        {
            // 게임이 실제로 읽는 곳: 시작 배치 · 정비 카드 풀 · 시작 함정 · Resources/Modules
            var used = new HashSet<ModuleDefinition>();
            foreach (var l in Assets<StartingLoadout>()) foreach (var e in l.Entries) if (e.Module != null) used.Add(e.Module);
            foreach (var p in Assets<ProgressionConfig>()) foreach (var d in RefitDraft.CollectInstallDefinitions(p)) used.Add(d);
            foreach (var c in Assets<StartingShipConcept>()) if (c.BridgeDefinition != null) used.Add(c.BridgeDefinition);
            foreach (var d in Resources.LoadAll<ModuleDefinition>("Modules")) used.Add(d);

            var ids = new Dictionary<string, ModuleDefinition>();
            var maxWord = new Regex(@"최대\s*(\d+)\s*개");
            foreach (var def in modules)
            {
                string at = AssetDatabase.GetAssetPath(def);
                bool inGame = used.Contains(def);
                // 게임에서 안 쓰는 옛 블록은 경고로만(빌드를 막지 않는다)
                void Problem(string text, UnityEngine.Object c)
                {
                    if (inGame) r.Error(text, c);
                    else r.Warn($"{text} — 게임에서 안 쓰는 블록", c);
                }
                if (!inGame) r.Warn($"블록 {def.Id}은 시작 배치·정비 카드·시작 함정 어디에도 없음(만들고 연결하지 않은 블록?): {at}", def);
                if (string.IsNullOrWhiteSpace(def.Id)) Problem($"블록 id가 비어 있음: {at}", def);
                else if (ids.TryGetValue(def.Id, out var other))
                    r.Error($"블록 id '{def.Id}'가 겹침: {AssetDatabase.GetAssetPath(other)} · {at} (이어하기가 엉뚱한 블록을 깐다)", def);
                else ids[def.Id] = def;

                if (def.Prefab == null) Problem($"블록 {def.Id}의 프리팹이 없음: {at}", def);
                else if (def.Prefab.GetComponent<ModuleRuntime>() == null)
                    Problem($"블록 {def.Id}의 프리팹 {def.Prefab.name}에 ModuleRuntime이 없음", def.Prefab);
                if (inGame && def.Icon == null) r.Warn($"블록 {def.Id}의 아이콘이 없음(카드·사전에 빈칸)", def);

                if (!string.IsNullOrEmpty(def.Description) && maxWord.Match(def.Description) is { Success: true } m &&
                    int.Parse(m.Groups[1].Value) != def.MaxCount)
                    r.Warn($"블록 {def.Id}: 설명은 \"최대 {m.Groups[1].Value}개\"인데 최대 개수는 {(def.MaxCount > 0 ? def.MaxCount.ToString() : "제한 없음")}", def);
            }
        }

        // ------------------------------------------------------------ 강화

        /// <summary>설명 낱말 → 배율 필드. "−"는 줄어드는 쪽(배율 = 1 − N%).</summary>
        private static readonly (string word, string field)[] UpgradeWords =
        {
            ("무장 피해", "damageMultiplier"), ("유폭 피해", "explosionDamageMultiplier"), ("피해", "damageMultiplier"),
            ("탄창", "magazineMultiplier"), ("포탑 선회", "turnSpeedMultiplier"), ("추적 속도", "turnSpeedMultiplier"),
            ("재장전", "ammoReloadMultiplier"), ("발사 간격", "cooldownMultiplier"), ("출격 간격", "cooldownMultiplier"),
            ("사거리", "rangeMultiplier"), ("수색 반경", "rangeMultiplier"), ("탐지", "detectionMultiplier"),
            ("지원 효과", "supplyMultiplier"), ("수리", "repairMultiplier"), ("속력", "speedMultiplier"), ("내구", "maxHpMultiplier"),
        };

        private static void CheckUpgrades(Report r)
        {
            var byModule = new Dictionary<ModuleDefinition, ModuleUpgradeProfile>();
            foreach (var profile in Assets<ModuleUpgradeProfile>())
            {
                string at = AssetDatabase.GetAssetPath(profile);
                if (!at.Contains("/Resources/Upgrades/"))
                    r.Warn($"강화 데이터 {profile.name}이 Resources/Upgrades 밖에 있어 게임이 읽지 않음: {at}", profile);
                if (profile.Module == null) { r.Error($"강화 데이터 {profile.name}이 가리키는 블록이 없음", profile); continue; }
                if (byModule.TryGetValue(profile.Module, out var dup))
                    r.Error($"블록 {profile.Module.Id}의 강화 데이터가 둘: {dup.name} · {profile.name}(뒤 것은 무시됨)", profile);
                else byModule[profile.Module] = profile;

                var so = new SerializedObject(profile);
                var stages = so.FindProperty("stages");
                if (stages == null) continue;
                if (stages.arraySize > ModuleUpgradeProfile.MaxSupportedLevel)
                    r.Warn($"강화 데이터 {profile.name}: 단계 {stages.arraySize}개 중 {ModuleUpgradeProfile.MaxSupportedLevel}개만 쓰임", profile);
                for (int i = 0; i < stages.arraySize; i++)
                    CheckStageText(r, profile, i + 1, stages.GetArrayElementAtIndex(i));

                // 외형: 강화 외형을 가진 블록은 기본형 + 단계 수만큼 칸이 있어야 한다
                var prefab = profile.Module.Prefab;
                var visuals = prefab != null ? prefab.GetComponentInChildren<ModuleUpgradeVisuals>(true) : null;
                if (visuals == null) continue;
                var levels = new SerializedObject(visuals).FindProperty("levels");
                for (int i = 0; levels != null && i < levels.arraySize; i++)
                    if (levels.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        r.Error($"블록 {profile.Module.Id}의 강화 외형 {i}번 칸이 비어 있음({prefab.name})", prefab);
                if (visuals.VariantCount < profile.MaxLevel + 1)
                    r.Warn($"블록 {profile.Module.Id}: 강화 {profile.MaxLevel}단계인데 외형은 {visuals.VariantCount}개({prefab.name})", prefab);
            }
        }

        private static void CheckStageText(Report r, ModuleUpgradeProfile profile, int level, SerializedProperty stage)
        {
            string desc = stage.FindPropertyRelative("description")?.stringValue ?? "";
            var modifier = stage.FindPropertyRelative("modifier");
            if (modifier == null) return;
            var values = new Dictionary<string, float>();
            var it = modifier.Copy();
            var end = modifier.GetEndProperty();
            if (it.NextVisible(true))
                do
                {
                    if (SerializedProperty.EqualContents(it, end)) break;
                    if (it.propertyType == SerializedPropertyType.Float) values[it.name] = it.floatValue;
                } while (it.NextVisible(false));

            var mentioned = new HashSet<string>();
            string label = $"강화 데이터 {profile.name} {level}단계";
            // "피해 +25%" · "발사 간격 −10%"
            foreach (Match m in Regex.Matches(desc, @"([가-힣]+(?: [가-힣]+)?)\s*([+\-−])\s*(\d+(?:\.\d+)?)%"))
            {
                string field = FieldFor(m.Groups[1].Value);
                if (field == null || !values.TryGetValue(field, out float actual)) continue;
                float pct = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / 100f;
                float want = m.Groups[2].Value == "+" ? 1f + pct : 1f - pct;
                mentioned.Add(field);
                if (Mathf.Abs(want - actual) > 0.005f)
                    r.Warn($"{label}: 설명 \"{m.Value}\"인데 {field} = {actual:0.###}", profile);
            }
            // "보급 15% 빠르게"
            foreach (Match m in Regex.Matches(desc, @"보급\s*(\d+)%\s*빠르게"))
                if (values.TryGetValue("ammoReloadMultiplier", out float actual))
                {
                    mentioned.Add("ammoReloadMultiplier");
                    float want = 1f - int.Parse(m.Groups[1].Value) / 100f;
                    if (Mathf.Abs(want - actual) > 0.005f) r.Warn($"{label}: 설명 \"{m.Value}\"인데 ammoReloadMultiplier = {actual:0.###}", profile);
                }
            // "반동 피해 25% → 10%"
            foreach (Match m in Regex.Matches(desc, @"반동 피해\s*(\d+)%\s*→\s*(\d+)%"))
                if (values.TryGetValue("selfDamageMultiplier", out float actual))
                {
                    mentioned.Add("selfDamageMultiplier");
                    float want = float.Parse(m.Groups[2].Value) / float.Parse(m.Groups[1].Value);
                    if (Mathf.Abs(want - actual) > 0.005f) r.Warn($"{label}: 설명 \"{m.Value}\"인데 selfDamageMultiplier = {actual:0.###}", profile);
                }
            foreach (var (field, value) in values)
                if (field.EndsWith("Multiplier") && Mathf.Abs(value - 1f) > 0.001f && !mentioned.Contains(field))
                    r.Warn($"{label}: {field} = {value:0.###}인데 설명에 없음(\"{desc}\")", profile);
        }

        private static string FieldFor(string words)
        {
            // 긴 낱말부터: "무장 피해"가 "피해"보다 먼저
            foreach (var (word, field) in UpgradeWords)
                if (words.EndsWith(word, StringComparison.Ordinal)) return field;
            return null;
        }

        // ------------------------------------------------------------ 적

        private static void CheckEnemies(Report r, List<EnemyDefinition> enemies, HashSet<string> build)
        {
            var ids = new Dictionary<string, EnemyDefinition>();
            foreach (var def in enemies)
            {
                string at = AssetDatabase.GetAssetPath(def);
                // 빌드에 들어가는 적만 오류(스테이지 구간·보스·기뢰부설정 같은 프리팹·Resources가 가리키는 것)
                bool inGame = build.Contains(at);
                void Problem(string text, UnityEngine.Object c)
                {
                    if (inGame) r.Error(text, c);
                    else r.Warn($"{text} — 빌드에 안 들어가는 적", c);
                }
                if (!inGame) r.Warn($"적 {def.Id}은 씬·스테이지·Resources 어디서도 안 씀(만들고 연결하지 않은 적?): {at}", def);
                if (string.IsNullOrWhiteSpace(def.Id)) Problem($"적 id가 비어 있음: {at}", def);
                else if (ids.TryGetValue(def.Id, out var other))
                    r.Error($"적 id '{def.Id}'가 겹침: {AssetDatabase.GetAssetPath(other)} · {at}", def);
                else ids[def.Id] = def;
                if (def.Prefab == null) Problem($"적 {def.Id}의 프리팹이 없음: {at}", def);
                else if (def.Prefab.GetComponent<EnemyController>() == null)
                    Problem($"적 {def.Id}의 프리팹 {def.Prefab.name}에 EnemyController가 없음", def.Prefab);
            }

            foreach (var set in Assets<RoundSet>())
            {
                if (!build.Contains(AssetDatabase.GetAssetPath(set))) continue;
                for (int i = 0; i < set.Rounds.Count; i++)
                {
                    var round = set.Rounds[i];
                    if (round.Entries == null) continue;
                    foreach (var e in round.Entries)
                        if (e.Enemy == null) r.Error($"{set.name} {i + 1}구간({round.Title})에 적이 비어 있는 출현 항목이 있음", set);
                }
            }
        }

        // ------------------------------------------------------------ 코드가 읽는 Resources 경로

        private static void CheckResourcePaths(Report r)
        {
            var paths = new SortedDictionary<string, (bool all, string from)>(StringComparer.Ordinal);
            var load = new Regex(@"Resources\.(Load|LoadAll)(?:<[\w\.]+>)?\(\s*(?:""([^""{}]+)""|([A-Z]\w*))\s*[,)]");
            var constant = new Regex(@"const\s+string\s+(\w+)\s*=\s*""([^""]+)""");
            foreach (var file in Directory.GetFiles(Path.Combine(Application.dataPath, "_Game", "Scripts"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Dev/")) continue;   // 검증 코드는 빼고 게임이 읽는 것만
                string text = File.ReadAllText(file);
                var consts = new Dictionary<string, string>();
                foreach (Match c in constant.Matches(text)) consts[c.Groups[1].Value] = c.Groups[2].Value;
                foreach (Match m in load.Matches(text))
                {
                    string path = m.Groups[2].Success ? m.Groups[2].Value
                        : consts.TryGetValue(m.Groups[3].Value, out var v) ? v : null;
                    if (path != null) paths[path] = (m.Groups[1].Value == "LoadAll", Path.GetFileName(file));
                }
            }
            // 이름을 조립하는 경로: 호위함 역할·단계, 전술 리그, 블록 형태 모델
            foreach (EscortRole role in Enum.GetValues(typeof(EscortRole)))
            {
                if (role == EscortRole.None) continue;
                string code = TaskForceEscortFormation.RoleCode(role);
                for (int t = 0; t <= TaskForceEscortFormation.MaxUpgradeLevel; t++)
                    paths[$"TaskForce/Escorts/ESC_{code}_T{t}"] = (false, "TaskForceEscortFormation.cs");
            }
            foreach (var code in new[] { "AEW", "CAP", "ASW", "EW", "STK" })
                paths[$"TaskForce/Effects/FX_{code}_Tactical"] = (false, "TaskForceWorldFeedback.cs");
            foreach (ModuleVariant v in Enum.GetValues(typeof(ModuleVariant)))
                if (ModuleVariants.ModelName(v) is { } name) paths[$"Modules/Variants/{name}"] = (false, "ModuleVariant.cs");

            int ok = 0;
            foreach (var (path, (all, from)) in paths)
            {
                bool exists = all ? Resources.LoadAll(path).Length > 0 : Resources.Load(path) != null;
                if (exists) ok++;
                else r.Error($"코드({from})가 읽는 Resources/{path}{(all ? "/*" : "")}이 없음 — 해당 빌더를 다시 실행했는지 확인");
            }
            r.Notes.Add($"코드가 읽는 Resources 경로 {paths.Count}개 중 {ok}개 있음");
        }

        // ------------------------------------------------------------ 프리팹 · 데이터 에셋: 스크립트 빠짐 · 끊긴 참조

        /// <summary>한 에셋(프리팹·데이터·씬)의 문제를 모아 한 줄로 적는다 — 모델 하나가 바뀌면 끊긴 메시가 수백 개라서.</summary>
        private sealed class Tally
        {
            public int Missing, Broken;
            public readonly List<string> Examples = new();
            public UnityEngine.Object Context;

            public void Note(string example, UnityEngine.Object context)
            {
                if (Examples.Count < 3) Examples.Add(example);
                if (Context == null) Context = context;
            }

            public void Flush(Report r, string where, bool inBuild)
            {
                if (Missing == 0 && Broken == 0) return;
                var what = new List<string>();
                if (Missing > 0) what.Add($"빠진 스크립트 {Missing}개");
                if (Broken > 0) what.Add($"끊긴 참조 {Broken}개(가리키던 에셋이 지워졌거나 모델이 바뀜)");
                int total = Missing + Broken;
                string text = $"{where}: {string.Join(" · ", what)} — 예: {string.Join(" / ", Examples)}{(total > Examples.Count ? " 외" : "")}";
                if (inBuild) r.Error(text, Context);
                else r.Warn($"{text} (빌드에 안 들어가는 에셋)", Context);
            }
        }

        private static int CheckPrefabsAndData(Report r, HashSet<string> build)
        {
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                count++;
                var tally = new Tally();
                CheckHierarchy(tally, go);
                tally.Flush(r, path, build.Contains(path));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                var tally = new Tally();
                if (so == null)
                {
                    if (path.EndsWith(".asset") && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(MonoBehaviour))
                    {
                        tally.Missing++;
                        tally.Note("데이터 에셋의 스크립트", null);
                    }
                }
                else CheckReferences(tally, so, so.name);
                tally.Flush(r, path, build.Contains(path));
            }
            return count;
        }

        private static void CheckHierarchy(Tally tally, GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (missing > 0)
                {
                    tally.Missing += missing;
                    tally.Note($"{PathOf(t, root.transform)} 스크립트", t.gameObject);
                }
                foreach (var c in t.GetComponents<Component>())
                    if (c != null) CheckReferences(tally, c, $"{PathOf(t, root.transform)}.{c.GetType().Name}");
            }
        }

        private static void CheckReferences(Tally tally, UnityEngine.Object target, string what)
        {
            var so = new SerializedObject(target);
            var p = so.GetIterator();
            while (p.NextVisible(true))
                if (p.propertyType == SerializedPropertyType.ObjectReference &&
                    p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0)
                {
                    tally.Broken++;
                    tally.Note($"{what}.{p.propertyPath}", target);
                }
        }

        private static string PathOf(Transform t, Transform root)
        {
            if (t == root) return root.name;
            var parts = new List<string>();
            for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
            parts.Add(root.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ------------------------------------------------------------ 씬

        private static void CheckScene(Report r, bool openScene)
        {
            if (!File.Exists(MainScene)) { r.Error($"메인 씬이 없음: {MainScene}"); return; }
            var scene = SceneManager.GetSceneByPath(MainScene);
            bool opened = false;
            if (!scene.isLoaded && !openScene)
            {
                r.Notes.Add("씬 검사 건너뜀(빌드 중이고 메인 씬이 열려 있지 않음 — 메뉴나 검증 빌드에서 본다)");
                return;
            }
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                var sceneTally = new Tally();
                foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(sceneTally, root);
                sceneTally.Flush(r, Path.GetFileName(MainScene), true);

                T One<T>(string what) where T : Component
                {
                    var found = new List<T>();
                    foreach (var root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
                    if (found.Count == 0) r.Error($"씬에 {typeof(T).Name}({what})이 없음");
                    else if (found.Count > 1) r.Error($"씬에 {typeof(T).Name}({what})이 {found.Count}개");
                    return found.Count > 0 ? found[0] : null;
                }
                One<GameManager>("게임 상태");
                One<EnemySpawner>("적 출현");
                One<HUDView>("HUD");
                One<ResultScreenUI>("결과 화면");
                One<ModuleFactory>("블록 설치");
                var director = One<StageDirector>("스테이지 진행");
                if (director != null)
                {
                    var stages = new SerializedObject(director).FindProperty("stages");
                    int n = stages != null ? stages.arraySize : 0;
                    if (n == 0) r.Error("StageDirector의 스테이지 목록이 비어 있음", director);
                    for (int i = 0; i < n; i++)
                        if (stages.GetArrayElementAtIndex(i).objectReferenceValue == null)
                            r.Error($"StageDirector 스테이지 {i + 1}이 비어 있음", director);
                }
                var init = One<ShipInitializer>("기함 초기화");
                if (init != null && init.Loadout == null) r.Error("ShipInitializer의 시작 배치가 비어 있음", init);
                var xp = One<ExperienceSystem>("경험치·정비 카드");
                if (xp != null && xp.Config == null) r.Error("ExperienceSystem의 ProgressionConfig가 비어 있음", xp);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        // ------------------------------------------------------------ 공통

        private static List<T> Assets<T>() where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { Root }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null && !list.Contains(a)) list.Add(a);
            }
            return list;
        }
    }

    /// <summary>모든 플레이어 빌드 직전에 데이터 동기화 검사를 돌리고, 오류가 있으면 빌드를 멈춘다(O05).</summary>
    internal sealed class DataSyncBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!DataSyncCheck.GateEnabled || Array.IndexOf(Environment.GetCommandLineArgs(), "-skipDataSync") >= 0)
            {
                Debug.Log("[DataSync] 빌드 전 검사를 건너뜀(메뉴 또는 -skipDataSync)");
                return;
            }
            if (DataSyncCheck.CheckedForBuild) { DataSyncCheck.CheckedForBuild = false; return; }   // 검증 빌드가 바로 앞에서 씬까지 봤다
            var r = DataSyncCheck.Run(openScene: false);
            string path = DataSyncCheck.ReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, r.ToMarkdown(), new UTF8Encoding(false));
            foreach (var (t, c) in r.Warnings) Debug.LogWarning($"[DataSync] {t}", c);
            if (r.Errors.Count == 0)
            {
                Debug.Log($"[DataSync] 빌드 전 검사 통과 · 경고 {r.Warnings.Count} → {path}");
                return;
            }
            foreach (var (t, c) in r.Errors) Debug.LogError($"[DataSync] {t}", c);
            throw new BuildFailedException($"데이터 동기화 검사 오류 {r.Errors.Count}건 — {path} 참고(끄기: Naval/Check/Data Sync Before Build)");
        }
    }
}
