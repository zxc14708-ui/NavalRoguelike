using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Data;

namespace Game.Enemies
{
    /// <summary>
    /// 기뢰부설정이 뿌리는 부유 기뢰. 움직이지 않고 떠 있다가, 함체(기함·호위함)가 가까이 오면 터진다.
    ///
    ///   - 투하 뒤 armDelay초는 안전(불이 느리게 깜빡임) → 그 뒤 빠르게 깜빡이며 작동.
    ///   - 기함 함체 표면에서 triggerDistance 안, 또는 호위함 중심에서 escortTriggerDistance 안이면 폭발(EnemyDefinition.AttackDamage).
    ///   - 소형 수상 표적이라 기관포·76mm가 쏘아 없앨 수 있다(체력 낮음, 경험치 없음). 격침되면 제자리에서 터진다(피해 없음).
    ///   - lifeTime 뒤에는 저절로 가라앉는다(보상 없음). 스테이지 전환 때 다른 적과 함께 치워진다.
    /// </summary>
    public class SeaMine : EnemyController
    {
        [SerializeField] private float armDelay = 1.5f;
        [SerializeField] private float lifeTime = 45f;
        [SerializeField] private float triggerDistance = 1.4f;
        [SerializeField] private float escortTriggerDistance = 3.2f;
        [SerializeField] private GameObject blastEffect;
        [SerializeField] private float blastScale = 1.2f;

        [Header("Visual")]
        [SerializeField] private Transform bob;
        [SerializeField] private Renderer lamp;
        [SerializeField] private Color lampColor = new(1f, 0.2f, 0.1f);

        private float _age;
        private Collider _flagshipHull;
        private MaterialPropertyBlock _mpb;
        private float _phase;

        public bool Armed => _age >= armDelay;

        /// <summary>기뢰는 움직이지 않으므로 표적 선택(호위함 포함)은 의미가 없다 — 판정은 아래에서 따로 한다.</summary>
        protected override bool TargetsEscorts => false;
        protected override bool LeavesWreck => false;

        public override void Setup(EnemyDefinition def, Transform player)
        {
            var rot = transform.rotation;
            base.Setup(def, player);
            transform.rotation = rot;   // 기뢰는 플레이어 쪽으로 돌지 않는다
            _flagshipHull = player != null ? player.GetComponent<Collider>() : null;
            _age = 0f;
            _phase = Random.value * 10f;
        }

        protected override void UpdateBehaviour(float dt)
        {
            _age += dt;
            float t = Time.time + _phase;
            if (bob != null) bob.localPosition = new Vector3(0f, Mathf.Sin(t * 1.6f) * 0.06f, 0f);
            Blink(t);

            if (_age >= lifeTime) { RemoveWithoutReward(); return; }
            if (!Armed) return;

            // 기함: 함체 표면까지
            Vector3 self = transform.position;
            if (_flagshipHull != null && _flagshipHull.gameObject.activeInHierarchy)
            {
                Vector3 surface = _flagshipHull.ClosestPoint(self);
                surface.y = self.y;
                if ((surface - self).sqrMagnitude <= triggerDistance * triggerDistance &&
                    _flagshipHull.TryGetComponent<IDamageable>(out var ship))
                {
                    Detonate(ship, surface);
                    return;
                }
            }

            // 호위함(아군 수상 표적): 중심 거리
            var friends = TargetRegistry.Of(CombatFaction.Player, TargetKind.Surface);
            for (int i = 0; i < friends.Count; i++)
            {
                var f = friends[i];
                if (f == null || !f.IsAlive || f.Transform == null || f.Transform == Flagship) continue;
                Vector3 d = f.Transform.position - self; d.y = 0f;
                if (d.sqrMagnitude > escortTriggerDistance * escortTriggerDistance) continue;
                if (f is IDamageable dmg) { Detonate(dmg, f.Transform.position); return; }
            }
        }

        private void Blink(float t)
        {
            if (lamp == null) return;
            _mpb ??= new MaterialPropertyBlock();
            float rate = Armed ? 4f : 1f;
            float on = Mathf.Sin(t * rate * Mathf.PI * 2f) > 0.3f ? 1f : 0.08f;
            Color c = lampColor * (on * 3f);
            lamp.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", lampColor * Mathf.Max(0.25f, on));
            _mpb.SetColor("_EmissionColor", c);
            lamp.SetPropertyBlock(_mpb);
        }

        private void Detonate(IDamageable target, Vector3 hitPoint)
        {
            if (target.IsAlive)
                target.TakeDamage(new DamageInfo(Definition.AttackDamage, hitPoint, Vector3.up, DamageSource.Torpedo));
            PooledEffect.Spawn(blastEffect, transform.position, blastScale);
            Game.View.Explosions.Spawn(hitPoint + Vector3.up * 0.4f, 1.5f, Game.View.Explosions.Kind.Charge);
            AudioManager.Play(SfxId.Explosion, transform.position, 1f, 0.8f);
            CombatLog.Add("기뢰", $"기뢰 폭발 · 피해 {Definition.AttackDamage:0}");
            RemoveWithoutReward();
        }

        /// <summary>쏘아 맞히면 제자리에서 터진다(함선 피해 없음).</summary>
        protected override void Die()
        {
            PooledEffect.Spawn(blastEffect, transform.position, blastScale * 0.8f);
            Game.View.Explosions.Spawn(transform.position + Vector3.up * 0.4f, 1.1f, Game.View.Explosions.Kind.Charge);
            base.Die();
        }
    }
}
