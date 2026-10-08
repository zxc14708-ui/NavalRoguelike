using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 충각 함수(강화 함수, 2026-10-08). 앞이 트인 자리(맨 앞 칸)에만 놓인다(PlacementZone.BowOnly).
    /// 함선이 앞으로 나아가는 동안 블록 앞쪽 충돌 구역에 들어온 적 수상함을 들이받아 피해를 준다.
    ///   - 피해 = Damage × (minSpeedFactor ~ 1, 지금 속력 / 기준 최고 속력) × 분류별 효율 — 전속(C)과 잘 맞는다.
    ///   - 자폭 보트는 함체 1.3m에서 터지기 전에 충돌 구역(앞쪽 rammingReach)에서 먼저 받혀 격침된다 → 함선 피해 없음.
    ///   - 같은 적은 hitInterval초에 한 번만. 큰 배(대형·보스)를 받으면 충각도 반동 피해(selfDamageRatio)를 입는다.
    /// 앞에 다른 블록이 붙으면(뱃머리가 아니면) 작동하지 않는다(그런 설치는 ShipGrid가 거부한다).
    /// </summary>
    public class RamBowModule : ModuleRuntime
    {
        [Tooltip("블록 앞면에서 앞으로 이만큼까지가 충돌 구역(m)")]
        [SerializeField] private float rammingReach = 2.4f;
        [Tooltip("충돌 구역 반폭(m) — 블록 폭보다 조금 넓다")]
        [SerializeField] private float halfWidth = 1.6f;
        [Tooltip("이 속력(최고 속력 대비) 이상으로 나아갈 때만 들이받는다")]
        [SerializeField] private float minSpeedRatio = 0.2f;
        [SerializeField] private float minSpeedFactor = 0.4f;
        [SerializeField] private float hitInterval = 1f;
        [Tooltip("대형·보스를 받을 때 충각이 입는 피해 비율(준 피해 대비)")]
        [SerializeField] private float selfDamageRatio = 0.25f;
        [SerializeField] private GameObject impactEffect;

        private const float CellHalf = 1f;   // 격자 한 칸 2m
        private readonly Dictionary<ITargetable, float> _lastHit = new();
        private readonly List<ITargetable> _scratch = new();

        public int Rams { get; private set; }
        public float LastRamDamage { get; private set; }

        private Vector3 ShipForward
        {
            get
            {
                var f = Grid != null ? Grid.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>앞이 트여 있는가(맨 앞 칸인가).</summary>
        public bool BowOpen => Grid == null || Instance == null ||
                               PlacementRuleEvaluator.IsOpen(Grid, Instance.OccupiedCoords, +1, 0);

        protected override void Tick(float dt)
        {
            if (Ship == null || !BowOpen) return;
            float maxSpeed = Mathf.Max(0.1f, Ship.BaseMaxSpeed);
            float speed = Ship.CurrentSpeed;
            if (speed < maxSpeed * minSpeedRatio) return;

            Vector3 fwd = ShipForward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 face = transform.position + fwd * CellHalf;

            _scratch.Clear();
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive || e.Transform == null || e is SeaMine) continue;   // 기뢰는 기뢰대로 터진다
                Vector3 p = e.Transform.TryGetComponent<Collider>(out var hull) && hull.enabled
                    ? hull.ClosestPoint(face + fwd * (rammingReach * 0.5f)) : e.Transform.position;
                Vector3 d = p - face;
                d.y = 0f;
                float ahead = Vector3.Dot(d, fwd), side = Mathf.Abs(Vector3.Dot(d, right));
                if (ahead < -0.3f || ahead > rammingReach || side > halfWidth) continue;
                if (_lastHit.TryGetValue(e, out float at) && Time.time - at < hitInterval) continue;
                _scratch.Add(e);
            }

            float factor = Mathf.Lerp(minSpeedFactor, 1f, Mathf.Clamp01(speed / maxSpeed));
            foreach (var e in _scratch) Ram(e, factor, face);
        }

        private void Ram(ITargetable e, float speedFactor, Vector3 face)
        {
            _lastHit[e] = Time.time;
            if (e is not IDamageable dmg || !dmg.IsAlive) return;
            float eff = EfficiencyAgainst(e);
            if (eff <= 0f) return;
            float damage = Stats.Damage * speedFactor * eff;
            dmg.TakeDamage(new DamageInfo(damage, e.Transform.position, ShipForward, DamageSource.Collision, "충각"));
            CombatStats.RecordHit("충각", e.Transform.position);
            Rams++;
            LastRamDamage = damage;

            var cat = TargetInfo.Category(e);
            if ((cat == TargetCategory.LargeSurface || cat == TargetCategory.Boss) && Instance != null)
                Instance.TakeDamage(damage * selfDamageRatio);

            PooledEffect.Spawn(impactEffect, face + ShipForward * 0.6f);
            Game.View.Explosions.Spawn(face + ShipForward * 0.6f + Vector3.up * 0.5f, 0.9f, Game.View.Explosions.Kind.Charge);
            AudioManager.Play(Game.Data.SfxId.Explosion, face, 0.8f, 1.15f);
            CombatLog.Add("충각", $"{LogName} 충돌 · 피해 {damage:0}");
        }
    }
}
