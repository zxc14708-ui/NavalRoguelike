using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 응급 차폐 장갑(2026-11-22, 지원 블록). 한 번에 큰 피해(최대 선체의 8% 이상, 감쇠 뒤)를 받으면 그 한 방부터
    /// 3초 동안 받는 피해 −40% 보호막이 함선을 감싼다. 재사용 Lv1 40초 · Lv2 30초 · Lv3 20초(보호막이 끝난 뒤부터 센다).
    /// 블록이 부서지면 작동하지 않는다. 여러 개를 달아도 한 번에 하나만 켜진다(먼저 준비된 것).
    /// </summary>
    public sealed class EmergencyShieldModule : ModuleRuntime, ShipController.IIncomingDamageFilter, ISupportTimer
    {
        public const float TriggerFraction = 0.08f;
        public const float Duration = 3f;
        public const float Reduction = 0.4f;
        public static float CooldownFor(int level) => level >= 3 ? 20f : level == 2 ? 30f : 40f;

        private static readonly Color ShieldColor = new(0.35f, 0.85f, 1f, 0.55f);

        private float _until = -1f, _cooldownLeft;
        private ShipController _registered;

        private int Level => Instance != null ? Instance.Level : 1;
        public bool ShieldUp => Time.time < _until;
        public float CooldownLeft => _cooldownLeft;
        /// <summary>검증용: 켜진 횟수 · 줄여 준 피해 합.</summary>
        public int Triggers { get; private set; }
        public float Absorbed { get; private set; }

        protected override void OnInitialized()
        {
            if (Ship != null) { Ship.AddDamageFilter(this); _registered = Ship; }
            SupportTimers.Register(this);
        }

        private void OnDestroy()
        {
            if (_registered != null) _registered.RemoveDamageFilter(this);
            SupportTimers.Unregister(this);
        }

        public float Filter(ShipController ship, in DamageInfo info, float amount)
        {
            if (Instance == null || !Instance.IsOperational || !isActiveAndEnabled || amount <= 0f) return amount;
            if (ShieldUp) return Absorb(amount);
            if (_cooldownLeft > 0f || OtherShieldUp(ship)) return amount;
            if (amount < ship.HullMaxHp * TriggerFraction) return amount;

            _until = Time.time + Duration;
            _cooldownLeft = Duration + CooldownFor(Level);   // 보호막이 끝난 뒤부터 재사용 시간
            Triggers++;
            var grid = ship.Grid;
            float cell = grid != null ? grid.CellSize : 2f;
            var bounds = new Bounds(Vector3.zero, grid != null
                ? new Vector3((grid.MaxHalfBeam * 2 + 1) * cell, 3f, (grid.MaxHalfLength * 2 + 1) * cell)
                : new Vector3(6f, 3f, 16f));
            SignatureFx.Shell(ship.transform, bounds, Duration, ShieldColor);
            AudioManager.Play(Game.Data.SfxId.SonarPing, ship.transform.position, 0.9f, 1.6f);
            GameEvents.RaiseBanner($"응급 차폐 장갑 — {Duration:0}초 동안 받는 피해 −{Reduction * 100f:0}%");
            CombatLog.Add(LogName, $"큰 피해 {amount:0}(선체 {amount / ship.HullMaxHp * 100f:0}%) → 차폐 {Duration:0}초");
            return Absorb(amount);
        }

        private float Absorb(float amount)
        {
            Absorbed += amount * Reduction;
            return amount * (1f - Reduction);
        }

        private bool OtherShieldUp(ShipController ship)
        {
            if (ship.Grid == null) return false;
            foreach (var m in ship.Grid.Modules)
                if (m?.Runtime is EmergencyShieldModule e && e != this && e.ShieldUp) return true;
            return false;
        }

        protected override void Tick(float dt)
        {
            if (_cooldownLeft <= 0f) return;
            // 보호막 동안은 게임 시간대로, 끝난 뒤에는 스킬 회복 속도(성장 카드)가 듣는다
            _cooldownLeft = Mathf.Max(0f, _cooldownLeft - dt * (ShieldUp ? 1f : RunUpgrades.SkillRate));
        }

        /// <summary>검증용: 재사용 대기를 비운다.</summary>
        public void DevReset() { _cooldownLeft = 0f; _until = -1f; }

        // --- 지원 판
        public string TimerLabel => "차폐 장갑";
        public string TimerStatus => Instance == null || !Instance.IsOperational ? "<color=#e0956a>파손</color>"
            : ShieldUp ? $"작동 · {_until - Time.time:0.0}초"
            : _cooldownLeft > 0f ? $"재충전 {_cooldownLeft:0}초" : "대기 · 큰 피해 시 자동";
        public bool TimerActive => ShieldUp;
    }
}
