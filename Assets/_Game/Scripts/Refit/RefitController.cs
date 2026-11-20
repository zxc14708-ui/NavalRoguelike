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
            RestoreDestroyedBlocks();

            int cards = progression != null ? progression.CardCount : 3;
            OpenSafely(() =>
            {
                var choices = draft != null ? draft.DrawCards(cards, level) : null;
                if (ui == null) { Resume(); return; }
                ui.SetReroll(draft != null ? () => draft.DrawCards(cards, level) : null);
                ui.Open(level, choices, Resume);
            });
        }

        /// <summary>정비 화면에 들어가면 부서진 블록을 최대 내구의 이 비율로 다시 살린다(2026-11-21).</summary>
        public const float RefitRestoreFraction = 0.5f;

        /// <summary>
        /// 부서진 블록 정비 수리(2026-11-21): 예전에는 안내만 "정비에서 수리된다"였고 실제로는 블록을 집었다 다시 놓아야만 살아났다.
        /// 맞는 위치가 퍼지면서 무장 블록이 자주 부서지므로, 정비 화면에 들어갈 때마다 절반 내구로 되살려 한 판이 한 번의 파괴로 무너지지 않게 한다.
        /// </summary>
        private void RestoreDestroyedBlocks()
        {
            var grid = ship != null ? ship.Grid : null;
            if (grid == null) return;
            int n = 0;
            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsDestroyed) continue;
                m.Restore(RefitRestoreFraction);
                n++;
            }
            if (n > 0) Game.Combat.CombatLog.Add("정비 수리", $"부서진 블록 {n}개를 내구 {RefitRestoreFraction:P0}로 복구");
        }

        /// <summary>
        /// 보스 격침 보상: 카드 중 하나가 에픽 성장 카드로 고정된다. 일반 정비와 같은 화면(카드 → 배치)을 쓴다.
        /// 끝나면 onFinished를 부른다(상태 전환은 호출한 쪽이 이어서 한다).
        /// </summary>
        public void BeginBossReward(int level, Action onFinished)
        {
            _onFinished = onFinished;

            GameManager.Instance.SetState(GameState.Refit);
            RestoreDestroyedBlocks();

            int cards = progression != null ? progression.CardCount : 3;
            OpenSafely(() =>
            {
                var choices = draft != null ? draft.DrawCards(cards, level, bossReward: true) : null;
                RefitDraft.GrantReroll();   // 보스 격침: 다시 뽑기 1회 회복
                if (ui == null) { Resume(); return; }
                ui.SetReroll(draft != null ? () => draft.DrawCards(cards, level, bossReward: true) : null);
                ui.Open(level, choices, Resume, bossReward: true);
            });
        }

        /// <summary>
        /// 정비 화면 열기. 카드 뽑기·화면 준비 중 예외가 나면 정비 상태(시간 정지)에 갇히지 않게 기록만 남기고 전투로 돌아간다
        /// (2026-10-31: 레벨업 때 예외로 게임이 멈춘 채 남던 문제의 안전장치).
        /// </summary>
        private void OpenSafely(Action open)
        {
            try { open(); }
            catch (Exception e)
            {
                Debug.LogException(e, this);
                Resume();
            }
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
