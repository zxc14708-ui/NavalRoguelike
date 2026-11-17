using System.Text;
using Game.Modules;
using Game.Ship;

namespace Game.UI
{
    /// <summary>
    /// 빌드 방향 안내(2026-10-11 점검 K01). 정비 카드 화면 제목 아래에 지금 함선의 역할 구성(대공·대함·대잠 무장 수)과 빠진 역할,
    /// 카드마다 "이 카드를 고르면 함선이 어떻게 달라지는지"(보유 수, 역할 수 변화, 빠진 역할 채움, 성장 카드가 닿는 무장 수)를 한 줄로.
    /// 역할은 사전과 같은 분류(CodexCatalog.RolesOf). 지원 블록은 역할 수에 넣지 않는다.
    /// </summary>
    public static class BuildGuide
    {
        private const string Good = "#7fd08a";
        private const string Warn = "#e0956a";
        private const string Muted = "#9fb3c0";

        public readonly struct Coverage
        {
            public readonly int Air, Surface, Sub;
            public Coverage(int air, int surface, int sub) { Air = air; Surface = surface; Sub = sub; }
        }

        /// <summary>작동 중인 무장·센서가 맡는 역할 수(한 블록이 여러 역할이면 각각 센다).</summary>
        public static Coverage Count(ShipGrid grid)
        {
            int air = 0, surface = 0, sub = 0;
            if (grid != null)
                foreach (var m in grid.Modules)
                {
                    if (m?.Definition == null || m.IsDestroyed || !IsArmament(m.Definition.Type)) continue;
                    var roles = CodexCatalog.RolesOf(m.Definition.Type);
                    if ((roles & CodexRole.Air) != 0) air++;
                    if ((roles & CodexRole.Surface) != 0) surface++;
                    if ((roles & CodexRole.Sub) != 0) sub++;
                }
            return new Coverage(air, surface, sub);
        }

        /// <summary>직접 적을 공격하는 블록(센서·지원 블록은 역할 수에 넣지 않는다).</summary>
        public static bool IsArmament(ModuleType t) => t is ModuleType.Autocannon or ModuleType.Nobong or ModuleType.NavalGun
            or ModuleType.Howitzer or ModuleType.GuidedRocket or ModuleType.Vls or ModuleType.SamLauncher or ModuleType.Ciws
            or ModuleType.AswLauncher or ModuleType.TorpedoTube or ModuleType.HelicopterDeck or ModuleType.RamBow or ModuleType.MineRail;

        /// <summary>카드 화면 제목 아래: "지금 함선  대공 3 · 대함 5 · 대잠 0 — 대잠 수단 없음".</summary>
        public static string Summary(ShipGrid grid)
        {
            var c = Count(grid);
            string Part(string name, int n) => n == 0 ? $"<color={Warn}>{name} 0</color>" : $"{name} {n}";
            var sb = new StringBuilder($"<color={Muted}>지금 함선</color>  {Part("대공", c.Air)}  ·  {Part("대함", c.Surface)}  ·  {Part("대잠", c.Sub)}");
            string missing = Missing(c);
            if (missing != null) sb.Append($"   <color={Warn}>— {missing} 수단 없음</color>");
            return sb.ToString();
        }

        private static string Missing(Coverage c)
        {
            var list = new System.Collections.Generic.List<string>();
            if (c.Air == 0) list.Add("대공");
            if (c.Surface == 0) list.Add("대함");
            if (c.Sub == 0) list.Add("대잠");
            return list.Count > 0 ? string.Join("·", list) : null;
        }

        /// <summary>설치 카드: 보유 수와 역할 변화("보유 1 → 2 · 대잠 0 → 1 빠진 역할 채움").</summary>
        public static string InstallImpact(ModuleDefinition def, ShipGrid grid)
        {
            if (def == null || grid == null) return null;
            int owned = grid.CountOf(def);
            var c = Count(grid);
            var roles = CodexCatalog.RolesOf(def.Type);
            var sb = new StringBuilder($"<color={Muted}>함선 변화</color>  보유 {owned} → {owned + 1}");
            if (IsArmament(def.Type))
            {
                void Role(CodexRole r, string name, int n, bool counts)
                {
                    if ((roles & r) == 0 || !counts) return;
                    sb.Append(n == 0 ? $"  ·  <color={Good}>{name} 0 → 1 빠진 역할 채움</color>" : $"  ·  {name} {n} → {n + 1}");
                }
                Role(CodexRole.Air, "대공", c.Air, true);
                Role(CodexRole.Surface, "대함", c.Surface, true);
                Role(CodexRole.Sub, "대잠", c.Sub, true);
            }
            else sb.Append(def.Type is ModuleType.Radar or ModuleType.Sonar ? "  ·  센서(탐지 거리)" : "  ·  지원 블록(이웃·함선 전체 강화)");
            if (def.Type == ModuleType.Sonar && c.Sub == 0) sb.Append($"  ·  <color={Warn}>소나만으로는 공격 못 함 — 폭뢰·경어뢰·헬기와 함께</color>");
            return sb.ToString();
        }

        /// <summary>성장 카드: 닿는 무장 수("무장 6문에 적용").</summary>
        public static string GrowthImpact(RunStat stat, ShipGrid grid)
        {
            if (grid == null) return null;
            int weapons = 0;
            foreach (var m in grid.Modules)
                if (m?.Definition != null && !m.IsDestroyed && RunUpgrades.IsWeapon(m.Definition.Type)) weapons++;
            return stat switch
            {
                RunStat.Damage or RunStat.FireRate or RunStat.Range or RunStat.Magazine =>
                    $"<color={Muted}>함선 변화</color>  지금 무장 {weapons}문에 적용",
                _ => null,
            };
        }
    }
}
