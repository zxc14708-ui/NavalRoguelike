using Game.Data;
using Game.Modules;

namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 공통 조회(WWII 전환 패치 A 7장 · 패치 B). 시작 함정 선택·드래프트·사전·적 생성이 모두 여기에 묻는다 —
    /// 폴더만 나눠서는 RefitDraft가 Resources/Modules를 합치므로 부족하기 때문이다.
    ///   - 현대: 예전 그대로 다 허용하되, WWII ID(ww2_)는 들이지 않는다.
    ///   - WWII: <see cref="Ruleset.Active"/> 목록에 있는 것만. 현대 미사일·헬기·호위함이 섞여 들지 않는다.
    /// </summary>
    public static class ContentRegistry
    {
        /// <summary>목록으로 거르는 중인가(WWII).</summary>
        public static bool Filtering => Ruleset.Active != null;

        private static bool IsWw2Id(string id) => id != null && id.StartsWith(Ruleset.Ww2Prefix, System.StringComparison.Ordinal);

        public static bool Allows(ModuleDefinition module)
        {
            if (module == null) return false;
            var active = Ruleset.Active;
            return active != null ? active.Contains(module) : !IsWw2Id(module.Id);
        }

        public static bool Allows(EnemyDefinition enemy)
        {
            if (enemy == null) return false;
            var active = Ruleset.Active;
            return active != null ? active.Contains(enemy) : !IsWw2Id(enemy.Id);
        }

        public static bool Allows(StartingShipConcept concept)
        {
            if (concept == null) return false;
            var active = Ruleset.Active;
            return active != null ? active.Contains(concept) : !IsWw2Id(concept.Id);
        }

        /// <summary>성장 카드: WWII는 목록에 있는 능력만(미사일계·방공계·스킬 회복 등 현대 문구 카드는 빠진다).</summary>
        public static bool AllowsStat(RunStat stat)
        {
            var active = Ruleset.Active;
            return active == null || active.AllowsStat(stat);
        }

        /// <summary>편대(호위함) 카드·출항 호위함.</summary>
        public static bool AllowsEscorts
        {
            get
            {
                var active = Ruleset.Active;
                return active == null || active.AllowsEscorts;
            }
        }
    }
}
