using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 기만체 발사기. 스스로 쏘지 않고, 플레이어가 기만체 스킬(Q)을 누르면 DecoySkill이 발사시킨다.
    /// 장비마다 재장전(Stats.ReloadTime)이 따로 돌기 때문에 여러 개를 달면 연달아 대응할 수 있다.
    /// </summary>
    public class DecoyLauncherModule : ModuleRuntime, IDecoySource, Game.Ship.ISmokeSource
    {
        [SerializeField] private GameObject decoyPrefab;
        [SerializeField] private Transform launchPoint;

        [Tooltip("한 번에 뿌리는 기만체 수")]
        [SerializeField, Min(1)] private int decoysPerShot = 2;

        [Tooltip("여러 발일 때 좌우로 벌어지는 전체 각도")]
        [SerializeField] private float spreadDegrees = 70f;

        [Header("Smoke [F]")]
        [Tooltip("연막이 적 조준을 막는 시간(초)")]
        [SerializeField] private float smokeDuration = 8f;
        [SerializeField] private float smokeCooldown = 28f;

        private float _cooldown;
        private float _smokeCooldown;

        /// <summary>연막탄을 싣는가. 함교의 내장 발사기는 기만체만 싣는다.</summary>
        protected virtual bool HasSmokeRounds => true;

        public bool CarriesSmoke => HasSmokeRounds;
        public bool SmokeReady => HasSmokeRounds && _smokeCooldown <= 0f && Instance != null && Instance.IsOperational;
        public float SmokeReadiness01 => smokeCooldown > 0f ? 1f - Mathf.Clamp01(_smokeCooldown / smokeCooldown) : 1f;

        public void DeploySmoke()
        {
            if (!SmokeReady) return;
            SmokeScreen.Instance?.Deploy(smokeDuration);
            _smokeCooldown = smokeCooldown;
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, transform.position, 0.35f, 0.8f);
        }

        public bool IsReady => _cooldown <= 0f && decoyPrefab != null && Instance != null && Instance.IsOperational;

        public float Readiness01 => Stats.ReloadTime > 0f
            ? 1f - Mathf.Clamp01(_cooldown / Stats.ReloadTime)
            : 1f;

        protected override void OnInitialized()
        {
            if (decoyPrefab == null) Debug.LogError($"[{GetType().Name}] decoyPrefab 미할당.", this);
        }

        protected override void Tick(float dt)
        {
            float skillDt = dt * RunUpgrades.SkillRate;   // 성장 카드: 스킬 회복 속도
            if (_cooldown > 0f) _cooldown -= skillDt;
            if (_smokeCooldown > 0f) _smokeCooldown -= skillDt;
            if (!IsReady || !ModuleSynergy.Adjacent(Grid, Instance, ModuleType.EwSuite)) return;
            Vector3 ship = Ship != null ? Ship.transform.position : transform.position;
            foreach (var target in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Missile))
            {
                if (target is not Missile missile || !missile.IsAlive || !missile.IsThreat || missile.IsDecoyed) continue;
                float tti = TargetingSystem.TimeToImpact(missile, ship);
                if (tti > 0f && tti <= 3f) { Deploy(missile.transform.position); break; }
            }
        }

        public void Deploy(Vector3? threat)
        {
            if (!IsReady) return;

            var origin = launchPoint != null ? launchPoint : transform;

            // 위협 반대편으로 흘려보내야 미사일을 함선에서 떼어놓을 수 있다. 위협이 없으면 아무 방향으로.
            Vector3 away = threat.HasValue ? origin.position - threat.Value : RandomFlat();
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            away.Normalize();

            for (int i = 0; i < decoysPerShot; i++)
            {
                float t = decoysPerShot == 1 ? 0.5f : i / (decoysPerShot - 1f);
                Vector3 dir = Quaternion.Euler(0f, Mathf.Lerp(-spreadDegrees, spreadDegrees, t) * 0.5f, 0f) * away;

                var go = PoolManager.Instance?.Spawn(decoyPrefab, origin.position, Quaternion.identity);
                go?.GetComponent<Decoy>()?.Deploy(dir + Vector3.up * 0.25f,
                    ModuleSynergy.Adjacent(Grid, Instance, ModuleType.EwSuite) ? 1.15f : 1f);
            }

            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime);
            AudioManager.Play(Game.Data.SfxId.MissileLaunch, origin.position, 0.4f, 1.7f);
        }

        private static Vector3 RandomFlat()
        {
            var v = Random.insideUnitCircle;
            return new Vector3(v.x, 0f, v.y);
        }
    }
}
