using TMPro;
using UnityEngine;
using Game.Core;

namespace Game.UI
{
    /// <summary>승리/패배 화면. 상태 변경 이벤트만 보고 켜진다.</summary>
    public class ResultScreenUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text detailText;

        private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnStateChanged(GameState state)
        {
            bool show = state == GameState.Victory || state == GameState.GameOver;
            if (root != null) root.SetActive(show);
            if (!show) return;

            if (titleText != null)
                titleText.text = state == GameState.Victory ? "VICTORY" : "SHIP LOST";

            if (detailText != null && GameManager.Instance != null)
                detailText.text = $"생존 시간 {GameManager.Instance.ElapsedTime:0}초";
        }
    }
}
