using System;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.Ship;
using Game.UI;

namespace Game.Refit
{
    /// <summary>
    /// 레벨업 흐름을 조율한다: 시간 정지 → 선체 소량 수리 → 카드 추첨 → 배치 화면 → 전투 재개.
    /// 실제 배치 조작은 RefitUI가, 격자 변경은 ModuleFactory가 맡는다.
    /// </summary>
    public class RefitController : MonoBehaviour
    {
        [SerializeField] private RefitDraft draft;
        [SerializeField] private RefitUI ui;
        [SerializeField] private ShipController ship;
        [SerializeField] private ProgressionConfig progression;

        private Action _onFinished;

        private void Awake()
        {
            if (draft == null) Debug.LogError("[RefitController] RefitDraft 미할당.", this);
            if (ui == null) Debug.LogError("[RefitController] RefitUI 미할당.", this);
            if (progression == null) Debug.LogError("[RefitController] ProgressionConfig 미할당.", this);
        }

        /// <summary>레벨이 올랐을 때 StageDirector가 호출한다.</summary>
        public void BeginLevelUp(int level, Action onFinished)
        {
            _onFinished = onFinished;

            GameManager.Instance.SetState(GameState.Refit);

            if (ship != null && progression != null) ship.RepairHull(progression.HullRepairOnLevelUp);

            int cards = progression != null ? progression.CardCount : 3;
            var choices = draft != null ? draft.DrawCards(cards, level) : null;

            if (ui == null) { Resume(); return; }
            ui.Open(level, choices, Resume);
        }

        /// <summary>
        /// 보스 격침 보상: 카드 중 하나가 에픽 성장 카드로 고정된다. 일반 정비와 같은 화면(카드 → 배치)을 쓴다.
        /// 끝나면 onFinished를 부른다(상태 전환은 호출한 쪽이 이어서 한다).
        /// </summary>
        public void BeginBossReward(int level, Action onFinished)
        {
            _onFinished = onFinished;

            GameManager.Instance.SetState(GameState.Refit);

            int cards = progression != null ? progression.CardCount : 3;
            var choices = draft != null ? draft.DrawCards(cards, level, bossReward: true) : null;

            if (ui == null) { Resume(); return; }
            ui.Open(level, choices, Resume, bossReward: true);
        }

        /// <summary>재개 버튼. 전투로 돌아간다.</summary>
        private void Resume()
        {
            ui?.Close();

            var cb = _onFinished;
            _onFinished = null;
            cb?.Invoke();
        }
    }
}
