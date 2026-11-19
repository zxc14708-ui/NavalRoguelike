using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 한 스테이지의 구간(라운드) 구성. 구간은 쉬지 않고 이어지고, 마지막 구간의 보스를 격침하면 스테이지가 끝난다.
    /// 정비(카드·배치)는 구간이 끝날 때가 아니라 레벨업과 보스 보상 때 열린다(StageDirector · ExperienceSystem).
    /// 난이도 곡선을 코드가 아니라 이 에셋에서 조절한다.
    /// 난이도는 체력을 올리기보다 "동시에 풀어야 하는 서로 다른 문제"를 늘려 올린다:
    /// 고속정(기관포·함포) → +드론(기관포·CIWS) → +미사일정(VLS 조기 제거 또는 기만체+CIWS) → 미사일정+잠수함(소나·헬기) → 전부 → 보스+포화+호위.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Round Set", fileName = "RoundSet")]
    public class RoundSet : ScriptableObject
    {
        [Serializable]
        public struct SpawnEntry
        {
            public EnemyDefinition Enemy;
            [Tooltip("이 라운드에서의 상대 등장 비율")]
            public float Weight;
        }

        [Serializable]
        public struct Round
        {
            [Tooltip("화면에 표시할 이름. 비우면 '라운드 N'")]
            public string Title;
            [Tooltip("이 라운드를 버텨야 하는 시간(초)")]
            public float Duration;
            [Tooltip("초당 스폰 수")]
            public float SpawnRate;
            [Tooltip("동시에 존재할 수 있는 최대 적 수")]
            public int MaxAlive;
            public List<SpawnEntry> Entries;
            [Tooltip("있으면 라운드 시작과 함께 등장한다")]
            public EnemyDefinition Boss;
            [Tooltip("보스와 함께 보스 주변에 나오는 호위함(동시 최대 수 제한과 무관)")]
            public EnemyDefinition Escort;
            [Min(0)] public int EscortCount;
        }

        [SerializeField] private List<Round> rounds = new();

        [Tooltip("이 스테이지 다음 스테이지(2026-10-05). StageDirector가 씬에 적힌 스테이지 목록 뒤에 이어 붙인다 — 씬을 고치지 않고 스테이지를 늘린다.")]
        [SerializeField] private RoundSet nextStage;

        /// <summary>다음 스테이지(없으면 null).</summary>
        public RoundSet NextStage => nextStage;

        [Header("낮과 밤(2026-11-19)")]
        [Tooltip("이 구간(0부터)부터 밤. 앞 구간 끝 24초가 해질녘, 마지막(보스) 구간은 새벽. -1이면 늘 낮(WWII 시험 전투 등)")]
        [SerializeField] private int nightFromRound = -1;
        [Tooltip("낮 구간(보스 구간 포함)에서 잠수함 가중치에 곱하는 값 — 0이면 낮엔 잠수함이 나오지 않는다")]
        [SerializeField, Min(0f)] private float daySubmarineWeight = 1f;
        [Tooltip("밤 구간에서 구간 목록의 수상함·항공기 가중치에 곱하는 값(잠수함은 그대로)")]
        [SerializeField, Min(0f)] private float nightSurfaceShare = 0.4f;
        [Tooltip("밤 구간에 더해 내는 적(잠수함·반잠수정·어뢰정·기뢰부설정)")]
        [SerializeField] private List<SpawnEntry> nightEntries = new();
        [Tooltip("밤 구간에서 잠수함 종류별 동시 최대 수에 더하는 값")]
        [SerializeField, Min(0)] private int nightExtraSubmarines;

        public int NightFromRound => nightFromRound;
        public int NightExtraSubmarines => nightExtraSubmarines;
        public IReadOnlyList<SpawnEntry> NightEntries => nightEntries;

        /// <summary>
        /// 낮·밤에 맞춘 구간 사본(원본 에셋은 바꾸지 않는다). 낮: 잠수함 가중치 × daySubmarineWeight.
        /// 밤: 수상함·항공기 가중치 × nightSurfaceShare, 잠수함 그대로, 밤 적 목록을 더한다.
        /// </summary>
        public Round ForTimeOfDay(int index, bool night)
        {
            var r = Get(index);
            if (nightFromRound < 0) return r;
            var list = new List<SpawnEntry>();
            if (r.Entries != null)
                foreach (var e in r.Entries)
                {
                    if (e.Enemy == null) continue;
                    bool sub = e.Enemy.Kind == Game.Combat.TargetKind.Submarine;
                    float w = night ? (sub ? e.Weight : e.Weight * nightSurfaceShare) : (sub ? e.Weight * daySubmarineWeight : e.Weight);
                    if (w > 0f) list.Add(new SpawnEntry { Enemy = e.Enemy, Weight = w });
                }
            if (night)
                foreach (var e in nightEntries)
                {
                    if (e.Enemy == null || e.Weight <= 0f) continue;
                    int at = list.FindIndex(x => x.Enemy == e.Enemy);
                    if (at >= 0) list[at] = new SpawnEntry { Enemy = e.Enemy, Weight = list[at].Weight + e.Weight };
                    else list.Add(e);
                }
            r.Entries = list;
            return r;
        }

        public IReadOnlyList<Round> Rounds => rounds;
        public int Count => rounds.Count;

        public Round Get(int index) => rounds[Mathf.Clamp(index, 0, rounds.Count - 1)];

        /// <summary>가중치에 따라 적 한 종류를 고른다.</summary>
        /// <summary>가중치 추첨. allowed가 주어지면 거기서 false인 적(동시 최대 수에 걸린 적 등)은 빼고 뽑는다.</summary>
        public static EnemyDefinition PickWeighted(in Round round, System.Func<EnemyDefinition, bool> allowed)
        {
            if (round.Entries == null || round.Entries.Count == 0) return null;
            float total = 0f;
            foreach (var e in round.Entries) if (e.Enemy != null && allowed(e.Enemy)) total += Mathf.Max(0f, e.Weight);
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            EnemyDefinition last = null;
            foreach (var e in round.Entries)
            {
                if (e.Enemy == null || !allowed(e.Enemy)) continue;
                last = e.Enemy;
                roll -= Mathf.Max(0f, e.Weight);
                if (roll <= 0f) return e.Enemy;
            }
            return last;
        }

        /// <summary>가중치에 배율(해역별 적 구성, 점검 K04)을 곱해 뽑는다. 원본 가중치는 바꾸지 않는다.</summary>
        public static EnemyDefinition PickWeighted(in Round round, System.Func<EnemyDefinition, bool> allowed, System.Func<EnemyDefinition, float> scale)
        {
            if (round.Entries == null || round.Entries.Count == 0) return null;
            float total = 0f;
            foreach (var e in round.Entries) if (e.Enemy != null && allowed(e.Enemy)) total += Mathf.Max(0f, e.Weight * scale(e.Enemy));
            if (total <= 0f) return PickWeighted(round, allowed);
            float roll = UnityEngine.Random.value * total;
            EnemyDefinition last = null;
            foreach (var e in round.Entries)
            {
                if (e.Enemy == null || !allowed(e.Enemy)) continue;
                last = e.Enemy;
                roll -= Mathf.Max(0f, e.Weight * scale(e.Enemy));
                if (roll <= 0f) return e.Enemy;
            }
            return last;
        }

        public static EnemyDefinition PickWeighted(in Round round)
        {
            if (round.Entries == null || round.Entries.Count == 0) return null;

            float total = 0f;
            foreach (var e in round.Entries) total += Mathf.Max(0f, e.Weight);
            if (total <= 0f) return round.Entries[0].Enemy;

            float roll = UnityEngine.Random.value * total;
            foreach (var e in round.Entries)
            {
                roll -= Mathf.Max(0f, e.Weight);
                if (roll <= 0f) return e.Enemy;
            }
            return round.Entries[^1].Enemy;
        }
    }
}
