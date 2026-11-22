using UnityEngine;
using Game.Core;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 전투단 통신 중계(2026-11-22 개편 — 사용자: 해상 보급 호출을 넣고 보급선 + 고속정 편대가 지원 온다).
    /// 일정 시간마다 지원 함정을 부른다(SupportCallFleet):
    ///   - 보급선(단계별 모델 Lv1 소형 군수 지원정 · Lv2 중형 보급함 · Lv3 대형 군수 지원함)이 기함 왼현에 붙어
    ///     탄약 전량 보급 · 부서진(없으면 가장 상한) 블록 수리 1 · 2 · 3개 · 재사용 중인 스킬 하나를 30 · 40 · 50% 줄인다.
    ///   - 고속정 편대 2 · 4 · 6척이 기함 둘레에 자리 잡고 26m 안의 적 수상함 · 항공기를 기관포로 쏜다.
    /// 단계별: 호출 간격 45 · 40 · 35초, 머무는 시간 15 · 18 · 20초. 지원 함정은 표적이 아니다(격침되지 않음).
    /// 예전 효과(호위함 자율 무장 재사용 속도 +15%)는 그대로 낸다.
    /// </summary>
    public sealed class FleetRelayModule : ModuleRuntime, ISupportTimer
    {
        public static float IntervalFor(int level) => level >= 3 ? 35f : level == 2 ? 40f : 45f;
        public static float StayFor(int level) => level >= 3 ? 20f : level == 2 ? 18f : 15f;
        public static int BoatsFor(int level) => level >= 3 ? 6 : level == 2 ? 4 : 2;
        public static int RepairsFor(int level) => Mathf.Clamp(level, 1, 3);
        public static float SkillCutFor(int level) => level >= 3 ? 0.5f : level == 2 ? 0.4f : 0.3f;

        private float _timer;
        private SupportCallFleet _fleet;

        private int Level => Instance != null ? Instance.Level : 1;
        public SupportCallFleet Fleet => _fleet;
        public int Calls { get; private set; }
        public float TimeLeft => Mathf.Max(0f, _timer);

        public override void ContributeToShipSystems(ShipSystems systems)
            => systems.RaiseEscortFireRateBonus(Stats.EscortFireRateBonus);

        protected override void OnInitialized()
        {
            _timer = IntervalFor(Level) * 0.5f;   // 첫 호출은 절반만 기다린다
            SupportTimers.Register(this);
        }

        private void OnDestroy()
        {
            SupportTimers.Unregister(this);
            if (_fleet != null) _fleet.Dismiss();
        }

        protected override void Tick(float dt)
        {
            if (_fleet != null) return;   // 지원 함정이 떠난 뒤부터 센다
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || Ship == null) return;
            _timer -= dt;
            if (_timer > 0f) return;
            Call();
        }

        /// <summary>지금 바로 부른다(검증용 · 이미 와 있으면 false).</summary>
        public bool DevCallNow() { if (_fleet != null || Ship == null) return false; Call(); return true; }

        private void Call()
        {
            int level = Level;
            Calls++;
            _fleet = SupportCallFleet.Summon(Ship, level, BoatsFor(level), StayFor(level), RepairsFor(level), SkillCutFor(level), LogName);
            _timer = IntervalFor(level);
            GameEvents.RaiseBanner($"전투단 통신 중계 — 보급선 + 고속정 {BoatsFor(level)}척 지원 출발");
        }

        // --- 지원 판
        public string TimerLabel => "지원 함정";
        public string TimerStatus => Instance == null || !Instance.IsOperational ? "<color=#e0956a>파손</color>"
            : _fleet != null ? _fleet.StatusText : $"다음 호출 {_timer:0}초";
        public bool TimerActive => _fleet != null;
    }
}
