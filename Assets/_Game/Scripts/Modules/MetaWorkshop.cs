using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.Modules
{
    /// <summary>
    /// 함정 정비창(2026-10-28 군항 건물 3단계). 작전 포인트로 함급마다
    ///   - 함교 강화: 그 함급 함교 패시브(레이더 · 통신 중계 · 흡기 · 사격통제)를 3단계까지(비용 3 · 5 · 8)
    ///   - 시작 배치 개조: 시작 블록 한 칸을 바꾼 개조안 2종을 열고(한 번), 출항할 배치를 고른다(기본형 포함)
    /// 함교 강화는 블록 최종 수치 계산(ModuleUpgrades.Compute)에서 함교 복사본에만 더한다. 개조안은 원본 시작 배치를 바꾸지 않고
    /// 실행 중 사본(StartingLoadout.CreateRuntime)을 만들어 ShipInitializer가 설치한다. 원본 데이터(ScriptableObject)는 건드리지 않는다.
    /// 규칙 집합마다 따로(프로필이 현대 / WWII 따로 — 지금 항목은 현대 함급만). 되돌리면 쓴 포인트를 모두 돌려받는다.
    /// 자동 검증에서는 연구와 함께 꺼 둔다(MetaResearch.Active — 검사가 DevForce로 켤 때만).
    /// </summary>
    public static class MetaWorkshop
    {
        public sealed class BridgeTrack
        {
            public StartShipKind Kind;
            public string Code, Name, Passive;
            public int[] Costs;
            public int MaxLevel => Costs.Length;
            /// <summary>단계 n까지의 누적 효과 문구.</summary>
            public Func<int, string> Effect;
            /// <summary>단계 n의 효과를 함교 수치 복사본에 더한다.</summary>
            public Apply Add;
        }

        public delegate void Apply(int level, ref ModuleStats s);

        /// <summary>시작 배치 개조안: 시작 배치의 (x, z) 칸 블록을 다른 블록으로 바꾼다.</summary>
        public sealed class Variant
        {
            public StartShipKind Kind;
            public string Id, Name, Change;
            public int X, Z, Rotation;
            public string ModuleId;
            public int Cost;
        }

        private static readonly int[] BridgeCosts = { 3, 5, 8 };

        public static readonly BridgeTrack[] Bridges =
        {
            new()
            {
                Kind = StartShipKind.Patrol, Code = "patrol", Name = "초계함 함교 레이더", Passive = "탐지 거리 · 동시 추적", Costs = BridgeCosts,
                Effect = l => $"탐지 +{4 * l} · 추적 +{l}",
                Add = (int l, ref ModuleStats s) => { s.DetectionRange += 4f * l; s.ExtraTrackedTargets += l; },
            },
            new()
            {
                Kind = StartShipKind.Command, Code = "command", Name = "지휘함 함교 통신 중계", Passive = "호위함 능력 재사용 속도", Costs = BridgeCosts,
                Effect = l => $"+{5 * l}%p",
                Add = (int l, ref ModuleStats s) => s.EscortFireRateBonus += 0.05f * l,
            },
            new()
            {
                Kind = StartShipKind.Assault, Code = "assault", Name = "강습함 함교 흡기", Passive = "최고속력 · 가속 · 항해 중 실탄 피해", Costs = BridgeCosts,
                Effect = l => $"속력 +{3 * l}%p · 가속 +{5 * l}%p · 피해 +{3 * l}%p",
                Add = (int l, ref ModuleStats s) => { s.SpeedBonus += 0.03f * l; s.AccelerationBonus += 0.05f * l; s.MovingGunDamageBonus += 0.03f * l; },
            },
            new()
            {
                Kind = StartShipKind.MissileDestroyer, Code = "missile", Name = "미사일함 함교 사격통제", Passive = "미사일 장전 · 유도 사거리", Costs = BridgeCosts,
                Effect = l => $"장전 −{4 * l}%p · 사거리 +{3 * l}%p",
                Add = (int l, ref ModuleStats s) => { s.MissileReloadReduction += 0.04f * l; s.GuidedRangeBonus += 0.03f * l; },
            },
        };

        /// <summary>개조안(함급마다 2종). 칸은 시작 배치 좌표(앞 x = 2 · 함교 −1~1 · 뒤 −2 · −3 · −4).</summary>
        public static readonly Variant[] Variants =
        {
            new() { Kind = StartShipKind.Patrol, Id = "patrol_gun", Name = "화력형", Change = "선수 기관포 → 76mm 함포", X = 2, Rotation = 0, ModuleId = "mod_gun76", Cost = 6 },
            new() { Kind = StartShipKind.Patrol, Id = "patrol_asw", Name = "대잠형", Change = "함미 기관포 → 대잠 폭뢰 발사기", X = -3, Rotation = 2, ModuleId = "mod_asw", Cost = 4 },
            new() { Kind = StartShipKind.Command, Id = "command_sam", Name = "방공형", Change = "CIWS → 함대공 미사일", X = -2, Rotation = 2, ModuleId = "mod_sam", Cost = 6 },
            new() { Kind = StartShipKind.Command, Id = "command_rocket", Name = "타격형", Change = "선수 기관포 → 유도로켓 발사기", X = 2, Rotation = 0, ModuleId = "mod_rocket", Cost = 5 },
            new() { Kind = StartShipKind.Assault, Id = "assault_gun", Name = "화력형", Change = "선수 기관포 → 76mm 함포", X = 2, Rotation = 0, ModuleId = "mod_gun76", Cost = 6 },
            new() { Kind = StartShipKind.Assault, Id = "assault_ciws", Name = "근접방어형", Change = "탄약고 → CIWS", X = -2, Rotation = 2, ModuleId = "mod_ciws", Cost = 5 },
            new() { Kind = StartShipKind.MissileDestroyer, Id = "missile_ciws", Name = "방어형", Change = "함미 기관포 → CIWS", X = -3, Rotation = 2, ModuleId = "mod_ciws", Cost = 5 },
            new() { Kind = StartShipKind.MissileDestroyer, Id = "missile_sam", Name = "방공형", Change = "뒤 VLS → 함대공 미사일", X = -2, Rotation = 0, ModuleId = "mod_sam", Cost = 5 },
        };

        /// <summary>정비가 바뀌었을 때(정비창 · 되돌리기).</summary>
        public static event Action Changed;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Changed = null; s_runtime.Clear(); }

        public static bool Active => MetaResearch.Active;

        public static BridgeTrack Bridge(StartShipKind kind) => Array.Find(Bridges, b => b.Kind == kind);
        public static Variant FindVariant(string id) => Array.Find(Variants, v => v.Id == id);
        public static IEnumerable<Variant> VariantsFor(StartShipKind kind)
        {
            foreach (var v in Variants) if (v.Kind == kind) yield return v;
        }

        // ------------------------------------------------------------ 저장(프로필 workshop: "bridge_<code>" = 단계, "layout_<id>" = 1 열림 / startLayout: 함급 code = 고른 개조안 번호, 0 = 기본형)

        private static int Get(List<PlayerProfile.ResearchLevel> list, string id)
        {
            var e = list.Find(r => r.id == id);
            return e != null ? e.level : 0;
        }

        private static void Set(List<PlayerProfile.ResearchLevel> list, string id, int level)
        {
            var e = list.Find(r => r.id == id);
            if (e == null) { e = new PlayerProfile.ResearchLevel { id = id }; list.Add(e); }
            e.level = level;
        }

        public static int BridgeLevel(StartShipKind kind)
        {
            var t = Bridge(kind);
            return t != null ? Math.Min(Get(PlayerProfile.Current.workshop, "bridge_" + t.Code), t.MaxLevel) : 0;
        }

        /// <summary>다음 함교 강화 비용(끝까지면 -1).</summary>
        public static int NextBridgeCost(StartShipKind kind)
        {
            var t = Bridge(kind);
            if (t == null) return -1;
            int l = BridgeLevel(kind);
            return l >= t.MaxLevel ? -1 : t.Costs[l];
        }

        public static bool TryUpgradeBridge(StartShipKind kind)
        {
            var t = Bridge(kind);
            int cost = NextBridgeCost(kind);
            if (t == null || cost < 0 || !PlayerProfile.SpendPoints(cost)) return false;
            Set(PlayerProfile.Current.workshop, "bridge_" + t.Code, BridgeLevel(kind) + 1);
            PlayerProfile.Save();
            Raise();
            return true;
        }

        public static bool Unlocked(Variant v) => v != null && Get(PlayerProfile.Current.workshop, "layout_" + v.Id) > 0;

        /// <summary>개조안을 연다(한 번). 열면 바로 그 배치를 고른다.</summary>
        public static bool TryUnlock(string id)
        {
            var v = FindVariant(id);
            if (v == null || Unlocked(v) || !PlayerProfile.SpendPoints(v.Cost)) return false;
            Set(PlayerProfile.Current.workshop, "layout_" + v.Id, 1);
            SelectLayout(v.Kind, id);   // 저장 · 알림 포함
            return true;
        }

        /// <summary>이 함급이 출항할 배치를 고른다(null = 기본형). 열리지 않은 개조안은 고를 수 없다.</summary>
        public static bool SelectLayout(StartShipKind kind, string id)
        {
            var t = Bridge(kind);
            if (t == null) return false;
            int index = 0;
            if (id != null)
            {
                var v = FindVariant(id);
                if (v == null || v.Kind != kind || !Unlocked(v)) return false;
                index = IndexOf(v) + 1;
            }
            Set(PlayerProfile.Current.startLayout, t.Code, index);
            PlayerProfile.Save();
            Raise();
            return true;
        }

        private static int IndexOf(Variant v)
        {
            int i = 0;
            foreach (var other in VariantsFor(v.Kind)) { if (other == v) return i; i++; }
            return -1;
        }

        /// <summary>이 함급이 고른 개조안(기본형이면 null).</summary>
        public static Variant SelectedVariant(StartShipKind kind)
        {
            var t = Bridge(kind);
            if (t == null) return null;
            int index = Get(PlayerProfile.Current.startLayout, t.Code) - 1;
            if (index < 0) return null;
            int i = 0;
            foreach (var v in VariantsFor(kind)) { if (i == index) return Unlocked(v) ? v : null; i++; }
            return null;
        }

        /// <summary>모든 정비를 되돌리고(기본형으로) 쓴 포인트를 모두 돌려준다. 돌려준 포인트.</summary>
        public static int ResetAll()
        {
            int refund = Spent();
            PlayerProfile.Current.workshop.Clear();
            PlayerProfile.Current.startLayout.Clear();
            PlayerProfile.RefundPoints(refund);   // 저장 포함
            Raise();
            return refund;
        }

        /// <summary>지금까지 정비에 쓴 포인트.</summary>
        public static int Spent()
        {
            int sum = 0;
            foreach (var t in Bridges)
            {
                int l = BridgeLevel(t.Kind);
                for (int i = 0; i < l; i++) sum += t.Costs[i];
            }
            foreach (var v in Variants) if (Unlocked(v)) sum += v.Cost;
            return sum;
        }

        private static void Raise() => Changed?.Invoke();

        // ------------------------------------------------------------ 적용

        /// <summary>함교 강화: 함교 수치 복사본에 더한다(ModuleUpgrades.Compute). 꺼져 있거나 함급 함교가 아니면 그대로.</summary>
        public static void ApplyBridge(ModuleDefinition def, ref ModuleStats s)
        {
            if (!Active || def == null || def.Type != ModuleType.Bridge || def.Id == null || !def.Id.StartsWith("mod_bridge_")) return;
            string code = def.Id.Substring("mod_bridge_".Length);
            var t = Array.Find(Bridges, b => b.Code == code);
            if (t == null) return;
            int l = BridgeLevel(t.Kind);
            if (l > 0) t.Add(l, ref s);
        }

        private static readonly Dictionary<(StartingLoadout, string), StartingLoadout> s_runtime = new();

        /// <summary>
        /// 출항할 시작 배치: 고른 개조안이 있으면 원본의 그 칸만 바꾼 실행 중 사본, 없으면(또는 꺼져 있으면) 원본 그대로.
        /// 같은 원본 · 개조안이면 같은 사본을 돌려준다.
        /// </summary>
        public static StartingLoadout ResolveLoadout(StartingShipConcept concept)
        {
            if (concept == null) return null;
            return Active ? LoadoutWith(concept, SelectedVariant(concept.Kind)) : concept.StartLoadout;
        }

        /// <summary>이 개조안을 적용한 시작 배치(null = 원본). 정비창 화면의 배치 문구도 이것으로 만든다.</summary>
        public static StartingLoadout LoadoutWith(StartingShipConcept concept, Variant v)
        {
            var source = concept != null ? concept.StartLoadout : null;
            if (source == null || v == null || v.Kind != concept.Kind) return source;
            if (s_runtime.TryGetValue((source, v.Id), out var cached) && cached != null) return cached;
            var module = FindModule(v.ModuleId);
            if (module == null) { UnityEngine.Debug.LogWarning($"[Workshop] 개조안 {v.Id}: 블록 {v.ModuleId} 없음 — 기본형으로"); return source; }
            var entries = new List<StartingLoadout.Entry>();
            bool replaced = false;
            foreach (var e in source.Entries)
            {
                if (!replaced && e.Origin.X == v.X && e.Origin.Z == v.Z && e.Module != null && e.Module.Type != ModuleType.Bridge)
                {
                    entries.Add(new StartingLoadout.Entry { Module = module, Origin = e.Origin, RotationSteps = v.Rotation });
                    replaced = true;
                }
                else entries.Add(e);
            }
            if (!replaced) { UnityEngine.Debug.LogWarning($"[Workshop] 개조안 {v.Id}: 시작 배치 ({v.X}, {v.Z})에 블록 없음 — 기본형으로"); return source; }
            var runtime = StartingLoadout.CreateRuntime($"{source.name} ({v.Name})", entries);
            s_runtime[(source, v.Id)] = runtime;
            return runtime;
        }

        public static ModuleDefinition FindModule(string id)
        {
            foreach (var def in Game.UI.CodexCatalog.CollectModules()) if (def != null && def.Id == id) return def;
            foreach (var def in UnityEngine.Resources.LoadAll<ModuleDefinition>("Modules")) if (def != null && def.Id == id) return def;
            return null;
        }

        /// <summary>앞 → 뒤 블록 이름("기관포 · 함교 · CIWS · 기관포"), 바뀐 칸은 강조.</summary>
        public static string Describe(StartingLoadout loadout, Variant changed = null)
        {
            if (loadout == null) return "";
            var list = new List<StartingLoadout.Entry>(loadout.Entries);
            list.Sort((a, b) => b.Origin.X.CompareTo(a.Origin.X));
            var parts = new List<string>();
            foreach (var e in list)
            {
                if (e.Module == null) continue;
                string name = e.Module.Type == ModuleType.Bridge ? "함교" : e.Module.DisplayName;
                bool hot = changed != null && e.Origin.X == changed.X && e.Origin.Z == changed.Z;
                parts.Add(hot ? $"<color=#e0c05a><b>{name}</b></color>" : name);
            }
            return string.Join(" · ", parts);
        }
    }
}
