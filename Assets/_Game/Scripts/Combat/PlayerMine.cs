using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Combat
{
    /// <summary>
    /// 기뢰 투하궤가 항적에 떨어뜨리는 아군 부유 기뢰(2026-10-08). 적 기뢰(SeaMine)와 달리 표적이 아니다 — 쏘아 없앨 수 없다.
    ///   - 투하 뒤 armDelay초는 안전, 그 뒤 작동(초록 등이 빨리 깜빡임).
    ///   - 적 수상함 선체가 triggerDistance 안에 오면 폭발: blastRadius 안의 적 수상함 모두에 피해(가장자리 50%, 분류별 효율).
    ///   - 아군(기함·호위함)에는 반응하지 않고 피해도 주지 않는다. lifeTime 뒤 가라앉는다.
    /// </summary>
    public class PlayerMine : MonoBehaviour, IPoolable
    {
        [SerializeField] private float armDelay = 1.2f;
        [SerializeField] private float lifeTime = 35f;
        [SerializeField] private float triggerDistance = 1.4f;
        [SerializeField] private float blastRadius = 3f;
        [SerializeField, Range(0f, 1f)] private float edgeRatio = 0.5f;
        [SerializeField] private GameObject blastEffect;

        [Header("Visual")]
        [SerializeField] private Transform bob;
        [SerializeField] private Renderer lamp;
        [SerializeField] private Color lampColor = new(0.3f, 1f, 0.55f);

        private float _age, _damage, _phase;
        private TargetEfficiency _efficiency;
        private MaterialPropertyBlock _mpb;
        private readonly List<ITargetable> _victims = new();

        /// <summary>지금 떠 있는 아군 기뢰(검증·표시용).</summary>
        public static readonly List<PlayerMine> Active = new();
        public bool Armed => _age >= armDelay;
        /// <summary>마지막 폭발에서 피해를 준 적 수(검증용).</summary>
        public static int LastHitCount { get; private set; }

        public void Drop(Vector3 position, float damage, TargetEfficiency efficiency)
        {
            transform.position = new Vector3(position.x, 0f, position.z);
            _damage = damage;
            _efficiency = efficiency;
            _age = 0f;
            _phase = Random.value * 10f;
            if (!Active.Contains(this)) Active.Add(this);
            CombatStats.RecordFire("기뢰");
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Time.time + _phase;
            if (bob != null) bob.localPosition = new Vector3(0f, Mathf.Sin(t * 1.6f) * 0.06f, 0f);
            Blink(t);
            if (_age >= lifeTime) { Despawn(); return; }
            if (!Armed) return;

            Vector3 self = transform.position;
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive || e.Transform == null || e is Game.Enemies.SeaMine) continue;   // 적 기뢰에는 반응하지 않는다
                Vector3 p = e.Transform.TryGetComponent<Collider>(out var hull) && hull.enabled ? hull.ClosestPoint(self) : e.Transform.position;
                p.y = self.y;
                if ((p - self).sqrMagnitude > triggerDistance * triggerDistance) continue;
                Explode();
                return;
            }
        }

        private void Explode()
        {
            _victims.Clear();
            Vector3 self = transform.position;
            float r2 = blastRadius * blastRadius;
            var list = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || !e.IsAlive || e.Transform == null) continue;
                Vector3 d = e.Transform.position - self; d.y = 0f;
                if (d.sqrMagnitude <= r2) _victims.Add(e);
            }
            LastHitCount = 0;
            foreach (var e in _victims)
            {
                if (e is not IDamageable dmg || !dmg.IsAlive) continue;
                float eff = _efficiency.For(TargetInfo.Category(e));
                if (eff <= 0f) continue;
                Vector3 d = e.Transform.position - self; d.y = 0f;
                float falloff = Mathf.Lerp(1f, edgeRatio, Mathf.Clamp01(d.magnitude / Mathf.Max(0.01f, blastRadius)));
                dmg.TakeDamage(new DamageInfo(_damage * eff * falloff, e.Transform.position, Vector3.up, DamageSource.Torpedo, "기뢰"));
                CombatStats.RecordHit("기뢰", e.Transform.position);
                LastHitCount++;
            }
            _victims.Clear();

            PooledEffect.Spawn(blastEffect, self, 1.2f);
            Game.View.Explosions.Spawn(self + Vector3.up * 0.4f, 1.5f, Game.View.Explosions.Kind.Charge);
            AudioManager.Play(SfxId.Explosion, self, 1f, 0.8f);
            CombatLog.Add("기뢰", $"아군 기뢰 폭발 · 적 {LastHitCount}척");
            Despawn();
        }

        private void Blink(float t)
        {
            if (lamp == null) return;
            _mpb ??= new MaterialPropertyBlock();
            float rate = Armed ? 3f : 0.8f;
            float on = Mathf.Sin(t * rate * Mathf.PI * 2f) > 0.3f ? 1f : 0.1f;
            lamp.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", lampColor * Mathf.Max(0.25f, on));
            _mpb.SetColor("_EmissionColor", lampColor * (on * 3f));
            lamp.SetPropertyBlock(_mpb);
        }

        private void Despawn()
        {
            Active.Remove(this);
            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _age = 0f; }
        public void OnDespawned() { Active.Remove(this); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active.Clear(); LastHitCount = 0; }
    }
}
