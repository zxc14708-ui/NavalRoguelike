using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Enemies;

namespace Game.View
{
    /// <summary>
    /// 적 도색: 위에서 보이는 갑판만 적갈색(키로프급 같은 테라코타)으로 칠해 바다·아군과 한눈에 구별되게 한다.
    /// 선체 옆면·상부 구조·무장은 원래 색 그대로라 어두운 옆면이 실루엣을 잡아 준다(2026-10-19 시안 D).
    ///   - 수상함: 갑판 재질만
    ///   - 잠수함: 드러났을 때 보이는 동체 윗면(동체 재질)까지
    ///   - 항공기(2026-11-15 사용자 요청으로 바꿈): 통째로 적갈색이던 것을 회색 동체 + 붉은 포인트로 — 동체·날개 재질은 통일된 적기 회색,
    ///     모델에 원래 있는 붉은 포인트 재질(Warning red · Emergency Red · WW2 Enemy Red)은 선명한 적색. 갑판 규칙은 항공기에 쓰지 않는다
    ///   - 예외 부위(렌더러 이름, <see cref="KeepOriginal"/>): 3스테이지 항공전함 비행갑판은 칠하지 않고 원래 회색(2026-11-15)
    /// 모델 파일·프리팹·재질 원본은 건드리지 않는다. 원본 재질마다 칠한 사본을 한 번만 만들어 모든 적이 함께 쓴다.
    /// 무장 사거리 원(노랑)·위험 원(밝은 빨강)·엘리트 표식(호박색)과 겹치지 않는 차분한 적갈색.
    /// </summary>
    public static class EnemyPaint
    {
        /// <summary>갑판 적갈색.</summary>
        public static readonly Color Deck = new(0.64f, 0.29f, 0.22f, 1f);
        /// <summary>잠수함 동체 윗면 — 갑판보다 조금 어둡게.</summary>
        public static readonly Color SubmarineHull = new(0.52f, 0.23f, 0.18f, 1f);
        /// <summary>적 항공기 동체 회색(2026-11-15) — 아군 회청색보다 조금 따뜻하고 밝은 중간 회색.</summary>
        public static readonly Color AircraftGrey = new(0.50f, 0.51f, 0.52f, 1f);
        /// <summary>적 항공기 붉은 포인트.</summary>
        public static readonly Color AircraftAccent = new(0.80f, 0.13f, 0.10f, 1f);

        /// <summary>칠하지 않는 부위(렌더러 이름 앞부분) — 항공전함 비행갑판.</summary>
        public static readonly string[] KeepOriginal = { "FlightDeck" };

        // 수상함·잠수함·항공기 모두: 갑판
        private static readonly HashSet<string> DeckNames = new()
        {
            "stealth deck", "deck grey", "nonslip deck", "submarine deck", "ww2 deck", "ww2 enemy deck",
            "mat_hull_deck", "mat_deck_plate", "mat_ww2_deck", "mat_ww2_enemy_deck",
        };
        // 잠수함 동체
        private static readonly HashSet<string> SubmarineNames = new() { "submarine green", "enemy submarine green", "mat_art_submarine" };
        // 항공기에서만: 동체·날개(회색으로) · 붉은 포인트(선명한 적색으로)
        private static readonly HashSet<string> AirframeNames = new()
        {
            "enemy hull grey", "naval blue grey", "mat_art_enemy_hull", "ww2 air dark", "ww2 olive", "mat_ww2_air_dark", "mat_ww2_air_olive",
        };
        private static readonly HashSet<string> AccentNames = new()
        {
            "warning red", "emergency red", "mat_art_emergency_red", "ww2 enemy red", "mat_ww2_enemy_red",
        };

        private static readonly Dictionary<Material, Material> s_surface = new();
        private static readonly Dictionary<Material, Material> s_air = new();
        private static readonly HashSet<Material> s_painted = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            s_surface.Clear();
            s_air.Clear();
            s_painted.Clear();
        }

        /// <summary>적대 진영 적을 칠한다. 풀에서 다시 나와도 같은 사본을 그대로 쓴다(여러 번 불러도 같다).</summary>
        public static void Apply(EnemyController enemy)
        {
            if (enemy == null || enemy.Faction != CombatFaction.Hostile) return;
            Apply(enemy.gameObject, enemy.Kind == TargetKind.Aircraft);
        }

        /// <summary>모델 묶음을 칠한다(사전 미리보기처럼 적 동작 없이 겉모습만 쓸 때도).</summary>
        public static void Apply(GameObject root, bool aircraft)
        {
            if (root == null) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer) continue;
                if (Keep(r.name)) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var painted = Painted(mats[i], aircraft);
                    if (painted == mats[i]) continue;
                    mats[i] = painted;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        private static bool Keep(string rendererName)
        {
            foreach (var k in KeepOriginal) if (rendererName.StartsWith(k, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>이 재질이 칠한 사본인가(검증용).</summary>
        public static bool IsPainted(Material m) => m != null && s_painted.Contains(m);

        private static Material Painted(Material src, bool aircraft)
        {
            if (src == null || s_painted.Contains(src)) return src;
            var cache = aircraft ? s_air : s_surface;
            if (cache.TryGetValue(src, out var done)) return done;

            Color? color = ColorFor(Key(src.name), aircraft);
            Material result = src;
            if (color.HasValue)
            {
                result = new Material(src) { name = src.name + " (enemy paint)" };
                if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color.Value);
                if (result.HasProperty("_Color")) result.SetColor("_Color", color.Value);
                s_painted.Add(result);
            }
            cache[src] = result;
            return result;
        }

        private static Color? ColorFor(string key, bool aircraft)
        {
            if (aircraft)
            {
                if (AirframeNames.Contains(key)) return AircraftGrey;
                if (AccentNames.Contains(key)) return AircraftAccent;
                return null;   // 갑판 규칙은 항공기에 쓰지 않는다(예전엔 위에서 보이는 면이 모두 적갈색)
            }
            if (DeckNames.Contains(key)) return Deck;
            if (SubmarineNames.Contains(key)) return SubmarineHull;
            return null;
        }

        /// <summary>"Deck Grey.003 (Instance)" → "deck grey".</summary>
        private static string Key(string name)
        {
            int cut = name.IndexOf(" (", System.StringComparison.Ordinal);
            if (cut >= 0) name = name.Substring(0, cut);
            int dot = name.LastIndexOf('.');
            if (dot > 0 && dot < name.Length - 1)
            {
                bool digits = true;
                for (int i = dot + 1; i < name.Length; i++) if (!char.IsDigit(name[i])) { digits = false; break; }
                if (digits) name = name.Substring(0, dot);
            }
            return name.Trim().ToLowerInvariant();
        }
    }
}
