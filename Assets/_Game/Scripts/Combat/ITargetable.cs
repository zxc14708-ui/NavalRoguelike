using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>표적 분류. 무기마다 노리는 종류가 다르다(기관포=수상함, CIWS=미사일).</summary>
    public enum TargetKind { Surface, Missile, Submarine, Decoy, Aircraft }

    /// <summary>
    /// 수면 비행체(위그선, 2026-11-16 4스테이지). 항공 표적(TargetKind.Aircraft)이지만 수면 몇 미터 위를 난다 —
    /// 대공 무기와 함께 대함 유도탄(VLS · 유도로켓)도 노릴 수 있고, 수상 표적 전용 무기(곡사포 · 기뢰 · 충각 · 헬기)는 못 노린다.
    /// 내려앉으면(Skimming = false, Kind = Surface) 수상함과 같다.
    /// </summary>
    public interface ISkimmer
    {
        bool Skimming { get; }
    }

    /// <summary>
    /// 저피탐 수상 표적(반잠수정, 2026-11-19 낮과 밤). 물에 거의 잠겨 레이더에 늦게 잡힌다(탐지 거리 × RadarMultiplier).
    /// 소나 범위 안 · 조명탄·탐조등에 비치면 드러난다(MarkDetected). 반쯤 잠겨 있어 폭뢰도 맞는다.
    /// </summary>
    public interface ILowObservable
    {
        public const float RadarMultiplier = 0.4f;
        /// <summary>지금 숨어 있는가(드러나지 않음).</summary>
        bool Stealthy { get; }
        /// <summary>seconds초 동안 드러난다(소나 · 조명탄 · 탐조등 · 근접).</summary>
        void MarkDetected(float seconds);
    }

    /// <summary>탐지/조준의 대상이 되는 모든 것. 진영(Faction)으로 적·아군을 가른다.</summary>
    public interface ITargetable : IFactionMember
    {
        Transform Transform { get; }
        TargetKind Kind { get; }
        bool IsAlive { get; }

        /// <summary>잠수함처럼 평소 조준 불가한 대상이 소나/헬기로 드러난 상태인지.</summary>
        bool IsRevealed { get; }
    }

    /// <summary>
    /// 큰 표적(보스)의 겨냥점(2026-11-14): 무장이 표적 중심 대신 이 점을 겨눈다 — 쏘는 쪽 위치에 따라 가까운 부분(함수·함교·함미)을,
    /// 약점이 열려 있으면(엘리베이터·함미 램프) 그쪽을. topAttack = 위에서 꽂히는 유도탄(미사일·로켓).
    /// </summary>
    public interface IAimPointProvider
    {
        Vector3 AimPointFor(Vector3 shooter, bool topAttack);
    }

    public static class AimPoints
    {
        /// <summary>표적의 겨냥점(겨냥점이 없으면 중심).</summary>
        public static Vector3 Of(ITargetable target, Vector3 shooter, bool topAttack = false)
            => target is IAimPointProvider p ? p.AimPointFor(shooter, topAttack) : target.Transform.position;
    }

    /// <summary>
    /// 모든 표적의 중앙 등록소.
    /// 각 무기가 매 프레임 FindObjectsOfType을 돌지 않도록 여기서 한 번만 관리한다.
    ///
    /// 진영별 목록을 따로 들고 있어 조회에 할당이 없다.
    ///   HostileTo(내 진영, 종류) — 무기·센서·경고 UI는 반드시 이것을 쓴다(아군 호위함·기함·기만체를 표적으로 잡지 않게).
    ///   Of(진영, 종류)          — 그 진영의 것만(예: 레이더에 아군 기만체 표시).
    ///   Get(종류)               — 진영 무관 전체(섬 배치 등 위치만 보는 곳, 개발 도구).
    /// 진영은 등록 시점에 한 번 읽는다(유닛의 진영은 바뀌지 않는다).
    /// </summary>
    public static class TargetRegistry
    {
        private static readonly Dictionary<TargetKind, List<ITargetable>> _byKind = new();
        private static readonly Dictionary<(CombatFaction, TargetKind), List<ITargetable>> _byFaction = new();

        public static void Register(ITargetable t)
        {
            if (t == null) return;
            var list = ListFor(_byKind, t.Kind);
            if (list.Contains(t)) return;
            list.Add(t);
            ListFor(_byFaction, (t.Faction, t.Kind)).Add(t);
        }

        public static void Unregister(ITargetable t)
        {
            if (t == null) return;
            if (_byKind.TryGetValue(t.Kind, out var list)) list.Remove(t);
            if (_byFaction.TryGetValue((t.Faction, t.Kind), out var fl)) fl.Remove(t);
        }

        /// <summary>진영 무관 전체.</summary>
        public static IReadOnlyList<ITargetable> Get(TargetKind kind)
            => _byKind.TryGetValue(kind, out var list) ? list : System.Array.Empty<ITargetable>();

        /// <summary>그 진영의 것만.</summary>
        public static IReadOnlyList<ITargetable> Of(CombatFaction faction, TargetKind kind)
            => _byFaction.TryGetValue((faction, kind), out var list) ? list : System.Array.Empty<ITargetable>();

        /// <summary>viewer 진영이 공격할 수 있는 것(적 진영). 중립 viewer는 빈 목록.</summary>
        public static IReadOnlyList<ITargetable> HostileTo(CombatFaction viewer, TargetKind kind)
        {
            var enemy = Factions.Opposing(viewer);
            return enemy == CombatFaction.Neutral ? System.Array.Empty<ITargetable>() : Of(enemy, kind);
        }

        /// <summary>플레이 시작마다 비운다(도메인 리로드를 끈 에디터에서 지난 플레이의 참조가 남지 않게).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();

        /// <summary>씬 전환 시 반드시 호출. 파괴된 오브젝트 참조가 남지 않게 한다.</summary>
        public static void Clear()
        {
            _byKind.Clear();
            _byFaction.Clear();
        }

        private static List<ITargetable> ListFor<TKey>(Dictionary<TKey, List<ITargetable>> map, TKey key)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<ITargetable>(64);
                map[key] = list;
            }
            return list;
        }
    }
}
