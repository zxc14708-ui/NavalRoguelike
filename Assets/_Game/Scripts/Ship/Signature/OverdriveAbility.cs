using UnityEngine;
using Game.Core;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 고속 강습함 고유 스킬 — 오버드라이브(2026-11-20, 예전 돌격 기동을 바꿈). 기관 한계를 풀어 Duration초 동안:
    ///   - 모든 무장 연사 ×FireRate(기함 블록 최종 수치 — RunUpgrades.TempFireRate, 편대 호위함은 그대로)
    ///   - 최고 속력 ×SpeedMultiplier(ShipController.BeginSpeedBoost — 전속과 겹치면 큰 쪽)
    ///   - 능동 스킬(기만체 · 재밍 · 연막 · 응급 수리 · 전속 · 조명탄) 쿨타임이 ×SkillRate 빠르게 흐른다(= 쿨타임 −50%). 고유 스킬 자기 쿨타임은 제외.
    /// 연출(OverdriveFx): 함선이 붉게 달아올라 맥박 치고, 지나간 자리에 붉은 잔상 · 선미 붉은 입자, 시작 때 붉은 충격 고리와 번쩍임.
    /// 끝나면 과열(OverheatSeconds초): 연사 ×OverheatFireRate · 최고 속력 ×OverheatSpeed(전속도 이만큼 깎임), 스킬 회복은 평소대로. 연출: 흰 증기 · 식어 가는 주황빛.
    /// </summary>
    public sealed class OverdriveAbility : ISignatureAbility
    {
        public const float Duration = 10f, FireRate = 1.5f, SpeedMultiplier = 1.5f, SkillRate = 2f;
        /// <summary>과열(2026-11-20): 오버드라이브가 끝난 뒤 3초 동안 연사 · 속력 −20%.</summary>
        public const float OverheatSeconds = 3f, OverheatFireRate = 0.8f, OverheatSpeed = 0.8f;

        public string Title => "오버드라이브";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Charge;
        public float Cooldown => 40f;

        private float _until = -1f, _overheatUntil = -1f;
        private Game.View.OverdriveFx _fx;
        public bool IsRunning => Time.time < _until;
        /// <summary>검증용: 남은 시간(초).</summary>
        public float Remaining => Mathf.Max(0f, _until - Time.time);
        /// <summary>과열 중인가.</summary>
        public bool IsOverheated => Time.time < _overheatUntil;

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            return owner.Ship != null;
        }

        public void Activate(SignatureSkill owner)
        {
            var ship = owner.Ship;
            _until = Time.time + Duration;
            _overheatUntil = -1f;
            ship.BeginSpeedBoost(SpeedMultiplier, Duration);
            RunUpgrades.SetTemporary(FireRate, SkillRate);
            _fx = Game.View.OverdriveFx.Begin(ship, Duration);
            AudioManager.Play(Game.Data.SfxId.Explosion, ship.transform.position, 0.55f, 1.45f);
            AudioManager.Play(Game.Data.SfxId.JetPass, ship.transform.position, 0.8f, 1.25f);
            GameEvents.RaiseBanner($"오버드라이브 — {Duration:0}초간 연사 +{(FireRate - 1f) * 100f:0}% · 속력 +{(SpeedMultiplier - 1f) * 100f:0}% · 스킬 쿨타임 −{(1f - 1f / SkillRate) * 100f:0}%");
        }

        public void Tick(SignatureSkill owner, float dt)
        {
            if (_until >= 0f && !IsRunning)
            {
                // 오버드라이브 끝 → 과열
                _until = -1f;
                _overheatUntil = Time.time + OverheatSeconds;
                RunUpgrades.SetTemporary(OverheatFireRate, 1f);
                owner.Ship?.BeginSpeedPenalty(OverheatSpeed, OverheatSeconds);
                if (_fx != null) _fx.Overheat(OverheatSeconds);
                GameEvents.RaiseBanner($"과열 — {OverheatSeconds:0}초간 연사 · 속력 −{(1f - OverheatFireRate) * 100f:0}%");
                return;
            }
            if (_overheatUntil >= 0f && !IsOverheated)
            {
                _overheatUntil = -1f;
                RunUpgrades.SetTemporary(1f, 1f);
                _fx = null;
                GameEvents.RaiseBanner("기관 냉각 완료");
            }
        }
    }
}
