using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 전자전 장비. 재밍 스킬(E)을 누르면 반경(Stats.Range) 안의 적 미사일마다 확률로 추적을 교란한다.
    /// 교란된 미사일은 jamDuration 동안 둔하게 엉뚱한 곳을 향해 크게 빗나가며 돌아 들어온다 — 회피·CIWS 대응 시간을 번다.
    /// 기만체(목표를 미끼로 바꿈)와 달리 표적을 없애지 않는다.
    /// 재장전은 Stats.ReloadTime. 여러 개를 달면 연달아 쓸 수 있다.
    /// </summary>
    public class EwSuiteModule : ModuleRuntime, IJammerSource
    {
        [Tooltip("미사일 하나당 재밍 성공 확률")]
        [SerializeField, Range(0f, 1f)] private float jamChance = 0.75f;

        [Tooltip("교란 지속 시간(초). 끝나면 미사일이 다시 추적한다")]
        [SerializeField] private float jamDuration = 4f;

        [Tooltip("작동 시 함선 주위로 퍼지는 전파 이펙트")]
        [SerializeField] private GameObject pulseEffect;

        [Tooltip("회전하는 안테나(선택)")]
        [SerializeField] private Transform antenna;
        [SerializeField] private float antennaRpm = 12f;

        private float _cooldown;

        public bool IsReady => _cooldown <= 0f && Instance != null && Instance.IsOperational;

        public float Readiness01 => Stats.ReloadTime > 0f
            ? 1f - Mathf.Clamp01(_cooldown / Stats.ReloadTime)
            : 1f;

        protected override void Tick(float dt)
        {
            if (_cooldown > 0f) _cooldown -= dt * RunUpgrades.SkillRate;   // 성장 카드: 스킬 회복 속도
            if (antenna != null) antenna.Rotate(Vector3.up, antennaRpm * 6f * dt, Space.World);
        }

        public void Jam()
        {
            if (!IsReady) return;

            Vector3 center = Ship != null ? Ship.transform.position : transform.position;
            float sqr = Stats.Range * Stats.Range;

            var missiles = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile);
            for (int i = missiles.Count - 1; i >= 0; i--)
            {
                if (missiles[i] is not Missile m || !m.IsAlive || !m.IsThreat) continue;
                if ((m.transform.position - center).sqrMagnitude > sqr) continue;
                float duration = jamDuration * (ModuleSynergy.Adjacent(Grid, Instance, ModuleType.DecoyLauncher) ? 1.2f : 1f);
                if (Random.value <= jamChance) m.Jam(duration);
                else CombatLog.Add("전자전", $"{m.LogName} 교란 실패 (확률 {jamChance * 100f:0}%)");
            }

            PooledEffect.Spawn(pulseEffect, new Vector3(center.x, 0.5f, center.z));
            AudioManager.Play(Game.Data.SfxId.SonarPing, center, 0.7f, 1.6f);
            _cooldown = Mathf.Max(1f, Stats.ReloadTime);
        }
    }
}
