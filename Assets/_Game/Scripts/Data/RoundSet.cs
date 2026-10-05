using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// 라운드 구성. 한 라운드를 버티면 정비 페이즈로 넘어간다.
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
