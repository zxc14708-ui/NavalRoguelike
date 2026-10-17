using System;
using UnityEngine;
using Game.Data;

namespace Game.Core
{
    /// <summary>
    /// 규칙 집합(WWII 전환 패치 A 7장 · 패치 B 2026-10-17). 지금 판이 어느 규칙으로 도는지 하나만 기억한다.
    ///   - modern_v1: 기본. 지금까지의 현대 해전(드래프트·사전·적·저장 모두 예전 그대로).
    ///   - ww2_v1: 항구의 [WWII 시험 전투]로만 들어간다. 콘텐츠는 <see cref="RulesetDefinition"/>(Resources/Rulesets/ww2_v1)의
    ///     목록만 쓰고(ContentRegistry), 프로필·키 설정은 별도 파일·이름공간을 쓴다. 현대 저장(naval_run.json)은 건드리지 않는다.
    /// 판을 다시 열면(RunRestart) 현대로 돌아간다. 같은 함정으로 재출격하면 그 함정의 규칙 집합으로 다시 들어간다(<see cref="EnterFor"/>).
    /// </summary>
    public static class Ruleset
    {
        public const string Modern = "modern_v1";
        public const string Ww2 = "ww2_v1";
        /// <summary>WWII 콘텐츠 ID 접두사. 현대 ID를 다른 뜻으로 덮어쓰지 않기 위해 모든 WWII 데이터가 이것으로 시작한다.</summary>
        public const string Ww2Prefix = "ww2_";

        public static string Current { get; private set; } = Modern;
        public static bool IsWw2 => Current == Ww2;

        /// <summary>규칙 집합이 바뀌었다(프로필·키 표시가 다시 읽는다).</summary>
        public static event Action Changed;

        private static RulesetDefinition s_ww2;

        /// <summary>WWII 규칙 집합 정의(없으면 null — 데이터를 아직 만들지 않은 프로젝트).</summary>
        public static RulesetDefinition Ww2Definition => s_ww2 != null ? s_ww2 : s_ww2 = Resources.Load<RulesetDefinition>("Rulesets/" + Ww2);

        /// <summary>지금 규칙 집합의 정의. 현대는 정의 없이 예전 데이터 경로를 그대로 쓰므로 null.</summary>
        public static RulesetDefinition Active => IsWw2 ? Ww2Definition : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = Modern;
            Changed = null;
        }

        public static bool Enter(string id)
        {
            if (id != Modern && id != Ww2) { Debug.LogError($"[Ruleset] 알 수 없는 규칙 집합: {id}"); return false; }
            if (id == Ww2 && Ww2Definition == null) { Debug.LogError("[Ruleset] Resources/Rulesets/ww2_v1 정의가 없습니다 — 메뉴 Naval/WW2/Build Patch B Content"); return false; }
            if (Current == id) return true;
            Current = id;
            Changed?.Invoke();
            return true;
        }

        /// <summary>시작 함정이 WWII 정의에 들어 있으면 WWII, 아니면 현대로 들어간다(재출격).</summary>
        public static void EnterFor(StartingShipConcept concept) => Enter(IsWw2Concept(concept) ? Ww2 : Modern);

        public static bool IsWw2Concept(StartingShipConcept concept)
            => concept != null && Ww2Definition != null && Ww2Definition.Contains(concept);

        /// <summary>파일 이름에 붙이는 꼬리(현대는 없음): naval_profile.json / naval_profile_ww2_v1.json.</summary>
        public static string FileSuffix(string ruleset) => ruleset == Ww2 ? "_" + Ww2 : "";
    }
}
