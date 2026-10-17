using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Modules;

namespace Game.Data
{
    /// <summary>
    /// 규칙 집합 하나의 콘텐츠 목록(WWII 전환 패치 A 7장 "RulesetDefinition", 패치 B 2026-10-17).
    /// 이 목록에 없는 시작 함정·블록·적·성장 카드는 이 규칙 집합의 드래프트·사전·적 생성에 나오지 않는다(ContentRegistry).
    /// 현대(modern_v1)는 정의 없이 예전 경로(ProgressionConfig·Resources/Modules·씬의 RoundSet)를 그대로 쓴다.
    /// 데이터는 메뉴 Naval/WW2/Build Patch B Content가 만든다. 원본 수치는 실행 중에 바꾸지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Ruleset Definition", fileName = "Ruleset_")]
    public sealed class RulesetDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string id = "ww2_v1";
        [SerializeField, Min(1)] private int schemaVersion = 1;
        [Tooltip("콘텐츠 판(저장 헤더의 contentVersion). 콘텐츠 목록이 바뀌면 올린다.")]
        [SerializeField, Min(1)] private int contentVersion = 1;
        [SerializeField] private string title = "WWII 시험 전투";
        [Tooltip("모든 콘텐츠 ID가 이것으로 시작해야 한다(현대 ID와 겹치지 않게).")]
        [SerializeField] private string idPrefix = "ww2_";

        [Header("Content")]
        [SerializeField] private StartingShipConcept[] hulls = Array.Empty<StartingShipConcept>();
        [SerializeField] private ModuleDefinition[] modules = Array.Empty<ModuleDefinition>();
        [SerializeField] private EnemyDefinition[] enemies = Array.Empty<EnemyDefinition>();
        [Tooltip("스테이지 순서(패치 B는 시험 전투 하나)")]
        [SerializeField] private RoundSet[] stages = Array.Empty<RoundSet>();

        [Header("Rules")]
        [Tooltip("정비 성장 카드로 나올 수 있는 능력. 현대 계열 카드(미사일·방공계 등)는 넣지 않는다.")]
        [SerializeField] private RunStat[] allowedStats = Array.Empty<RunStat>();
        [Tooltip("편대(호위함) 카드를 쓰는가. 패치 B는 호위함 없이 시험한다(설계 9.2).")]
        [SerializeField] private bool allowsEscorts;
        [Tooltip("중단·재개 저장 파일 이름(패치 B는 한 판짜리라 저장하지 않는다 — 자리만)")]
        [SerializeField] private string saveFile = "naval_run_ww2_v1.json";

        public string Id => id;
        public int SchemaVersion => schemaVersion;
        public int ContentVersion => contentVersion;
        public string Title => title;
        public string IdPrefix => idPrefix;
        public IReadOnlyList<StartingShipConcept> Hulls => hulls;
        public IReadOnlyList<ModuleDefinition> Modules => modules;
        public IReadOnlyList<EnemyDefinition> Enemies => enemies;
        public IReadOnlyList<RoundSet> Stages => stages;
        public IReadOnlyList<RunStat> AllowedStats => allowedStats;
        public bool AllowsEscorts => allowsEscorts;
        public string SaveFile => saveFile;

        public bool Contains(StartingShipConcept concept) => concept != null && Array.IndexOf(hulls, concept) >= 0;
        public bool Contains(ModuleDefinition module) => module != null && Array.IndexOf(modules, module) >= 0;
        public bool Contains(EnemyDefinition enemy) => enemy != null && Array.IndexOf(enemies, enemy) >= 0;
        public bool AllowsStat(RunStat stat) => Array.IndexOf(allowedStats, stat) >= 0;

        /// <summary>
        /// 목록 점검(패치 A 7장 "콘텐츠 레지스트리: ID 중복/누락 검증"): 빈 칸·ID 중복·접두사·프리팹·
        /// 웨이브에 나오는데 목록에 없는 적·시작 배치에 있는데 목록에 없는 블록을 찾는다. 문제가 없으면 빈 목록.
        /// </summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var ids = new HashSet<string>();
            void CheckId(string kind, string contentId, UnityEngine.Object owner)
            {
                if (string.IsNullOrEmpty(contentId)) { problems.Add($"{kind} {owner.name}: ID 없음"); return; }
                if (!ids.Add(contentId)) problems.Add($"{kind} ID 중복: {contentId}");
                if (!string.IsNullOrEmpty(idPrefix) && !contentId.StartsWith(idPrefix, StringComparison.Ordinal))
                    problems.Add($"{kind} {contentId}: '{idPrefix}'로 시작하지 않음(현대 ID 재사용 금지)");
            }

            if (string.IsNullOrEmpty(id)) problems.Add("규칙 집합 ID 없음");
            foreach (var h in hulls)
            {
                if (h == null) { problems.Add("시작 함정 빈 칸"); continue; }
                CheckId("시작 함정", h.Id, h);
                if (h.StartLoadout == null) { problems.Add($"시작 함정 {h.Id}: 시작 배치 없음"); continue; }
                foreach (var e in h.StartLoadout.Entries)
                    if (e.Module == null) problems.Add($"시작 함정 {h.Id}: 빈 블록");
                    else if (!Contains(e.Module)) problems.Add($"시작 함정 {h.Id}: 블록 {e.Module.Id}가 목록에 없음");
            }
            foreach (var m in modules)
            {
                if (m == null) { problems.Add("블록 빈 칸"); continue; }
                CheckId("블록", m.Id, m);
                if (m.Prefab == null || m.Prefab.GetComponent<ModuleRuntime>() == null) problems.Add($"블록 {m.Id}: 전투 프리팹 없음");
            }
            foreach (var e in enemies)
            {
                if (e == null) { problems.Add("적 빈 칸"); continue; }
                CheckId("적", e.Id, e);
                if (e.Prefab == null || e.Prefab.GetComponent<Game.Enemies.EnemyController>() == null) problems.Add($"적 {e.Id}: 프리팹 없음");
            }
            foreach (var stage in stages)
            {
                if (stage == null) { problems.Add("스테이지 빈 칸"); continue; }
                foreach (var r in stage.Rounds)
                {
                    if (r.Entries != null)
                        foreach (var entry in r.Entries)
                            if (entry.Enemy != null && !Contains(entry.Enemy)) problems.Add($"{stage.name} '{r.Title}': 적 {entry.Enemy.Id}가 목록에 없음");
                    if (r.Boss != null && !Contains(r.Boss)) problems.Add($"{stage.name} '{r.Title}': 보스 {r.Boss.Id}가 목록에 없음");
                    if (r.Escort != null && !Contains(r.Escort)) problems.Add($"{stage.name} '{r.Title}': 호위 {r.Escort.Id}가 목록에 없음");
                }
            }
            if (stages.Length == 0) problems.Add("스테이지 없음");
            if (hulls.Length == 0) problems.Add("시작 함정 없음");
            return problems;
        }
    }
}
