using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Ship
{
    /// <summary>
    /// 고속 강습함 고유 스킬 — 돌격 기동(2026-10-11). 8초 동안 최고 속력 ×1.4(전속과 같은 방식), 받는 피해 −40%,
    /// 뱃머리에 닿는 적 수상함을 들이받는다(피해 35 × 속력 비율, 같은 적은 1초에 한 번 — 자폭 보트는 터지기 전에 받힌다).
    /// 연출: 함선을 감싸는 주황 방호막(맥박), 시작 때 번지는 충격 고리, 뱃머리 물보라 번쩍임, 받을 때 폭발.
    /// </summary>
    public sealed class AssaultRunAbility : ISignatureAbility
    {
        public const float Duration = 8f, SpeedMultiplier = 1.4f, DamageScale = 0.6f, RamDamage = 35f, RamReach = 2.6f;
        private static readonly Color ShellColor = new(1f, 0.55f, 0.15f, 0.22f);   // 함선이 비쳐 보이게 옅게

        public string Title => "돌격 기동";
        public Game.UI.HudTextures.SkillIcon Icon => Game.UI.HudTextures.SkillIcon.Charge;
        public float Cooldown => 30f;

        private float _until = -1f, _sprayTimer;
        private readonly Dictionary<ITargetable, float> _lastHit = new();
        public int Rams { get; private set; }
        public bool IsRunning => Time.time < _until;

        public bool CanActivate(SignatureSkill owner, out string reason)
        {
            reason = null;
            return owner.Ship != null;
        }

        public void Activate(SignatureSkill owner)
        {
            var ship = owner.Ship;
            _until = Time.time + Duration;
            ship.BeginFlank(SpeedMultiplier, Duration);
            ship.IncomingDamageScale = DamageScale;
            _lastHit.Clear();

            var col = ship.GetComponent<BoxCollider>();
            var b = col != null ? new Bounds(col.center, col.size) : new Bounds(Vector3.zero, new Vector3(4f, 2f, 10f));
            SignatureFx.Shell(ship.transform, b, Duration, ShellColor);
            SignatureFx.Ring(ship.transform.position, 22f, 0.9f, new Color(1f, 0.7f, 0.3f, 0.9f), 0.9f, 2, 0.15f);
            Game.View.Explosions.Spawn(Bow(ship), 0.8f, Game.View.Explosions.Kind.Water);
            AudioManager.Play(Game.Data.SfxId.Explosion, ship.transform.position, 0.6f, 1.3f);
            GameEvents.RaiseBanner("돌격 기동 — 8초간 속력 +40% · 받는 피해 −40% · 충돌 공격");
        }

        public void Tick(SignatureSkill owner, float dt)
        {
            var ship = owner.Ship;
            if (ship == null) return;
            if (!IsRunning)
            {
                if (_until > 0f) { ship.IncomingDamageScale = 1f; _until = -1f; }
                return;
            }

            // 뱃머리 물보라(0.35초마다)
            _sprayTimer -= dt;
            if (_sprayTimer <= 0f && ship.CurrentSpeed > 2f)
            {
                _sprayTimer = 0.35f;
                Game.View.Explosions.Spawn(Bow(ship) + ship.transform.forward * 1.2f, 0.35f, Game.View.Explosions.Kind.Water);
            }

            // 충돌 공격: 뱃머리 앞 RamReach 안, 앞쪽에 있는 적 수상함
            float speedRatio = Mathf.Clamp01(ship.CurrentSpeed / Mathf.Max(0.1f, ship.BaseMaxSpeed * SpeedMultiplier));
            if (speedRatio < 0.2f) return;
            var col = ship.GetComponent<Collider>();
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var t = list[i];
                if (t == null || !t.IsAlive || t.Transform == null || t is not IDamageable dmg) continue;
                Vector3 p = t.Transform.position;
                Vector3 surface = col != null ? col.ClosestPoint(p) : ship.transform.position;
                Vector3 d = p - surface; d.y = 0f;
                if (d.magnitude > RamReach) continue;
                Vector3 rel = p - ship.transform.position; rel.y = 0f;
                if (Vector3.Dot(rel.normalized, fwd) < 0.25f) continue;   // 뱃머리 쪽만
                if (_lastHit.TryGetValue(t, out float at) && Time.time - at < 1f) continue;
                _lastHit[t] = Time.time;
                float damage = RamDamage * Mathf.Lerp(0.4f, 1f, speedRatio);
                dmg.TakeDamage(new DamageInfo(damage, p, fwd, DamageSource.Collision, "돌격 기동"));
                Rams++;
                Game.View.Explosions.Spawn(surface + Vector3.up * 0.5f, 1f, Game.View.Explosions.Kind.Charge);
                AudioManager.Play(Game.Data.SfxId.Explosion, surface, 0.8f, 1.1f);
                CombatLog.Add("돌격 기동", $"충돌 · 피해 {damage:0}");
            }
        }

        private static Vector3 Bow(ShipController ship)
        {
            var col = ship.GetComponent<BoxCollider>();
            float half = col != null ? col.size.z * 0.5f + col.center.z : 5f;
            return ship.transform.position + ship.transform.forward * half;
        }
    }
}
