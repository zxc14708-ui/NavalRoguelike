using UnityEngine;
using Game.Core;

namespace Game.Combat
{
    /// <summary>
    /// 대잠 폭뢰. 포물선으로 날아가(투척) 물에 떨어진 뒤(입수: 작은 물보라) 가라앉다가 터진다(수중 폭발: 큰 물기둥).
    /// 세 단계가 눈에 구별되도록 입수와 폭발 사이에 뚜렷한 지연을 둔다.
    ///
    /// 잠항 중인 잠수함에도 피해를 주는 유일한 함포계 무기다.
    /// 폭발 충격으로 잠수함이 잠깐 떠오르므로, 다른 무기가 이어서 공격할 수 있다.
    /// </summary>
    public class DepthCharge : MonoBehaviour, IPoolable
    {
        [SerializeField] private float flightTime = 1.1f;
        [SerializeField] private float arcHeight = 5f;
        [Tooltip("입수 후 폭발까지 지연. 짧으면 입수 물보라와 폭발이 겹쳐 보인다")]
        [SerializeField] private float sinkTime = 0.9f;
        [SerializeField] private float sinkSpeed = 1.8f;
        [SerializeField] private float blastRadius = 4.5f;
        [Tooltip("폭발 시 수면에 솟는 물기둥")]
        [SerializeField] private GameObject blastEffect;
        [Tooltip("입수 물보라 크기(수중 폭발 물기둥 대비)")]
        [SerializeField] private float entrySplashScale = 0.3f;

        private Vector3 _from, _to;
        private string _statsKey;
        private float _t, _sinkTimer, _damage;
        private bool _sinking;

        public void Launch(Vector3 from, Vector3 to, float damage)
        {
            _from = from;
            _to = new Vector3(to.x, 0f, to.z);   // 수면에 떨어진다
            _damage = damage;
            _t = 0f;
            _sinkTimer = 0f;
            _sinking = false;
            transform.position = from;
            _statsKey = CombatStats.KeyFor(gameObject);
            CombatStats.RecordFire(_statsKey);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (!_sinking)
            {
                _t += dt / Mathf.Max(0.05f, flightTime);
                float t = Mathf.Clamp01(_t);
                transform.position = Vector3.Lerp(_from, _to, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));
                transform.Rotate(Vector3.right, 540f * dt, Space.Self);   // 굴러가며 날아가는 모습

                if (_t >= 1f)
                {
                    _sinking = true;
                    PooledEffect.Spawn(blastEffect, new Vector3(_to.x, 0f, _to.z), entrySplashScale);
                    AudioManager.Play(Game.Data.SfxId.HullImpact, _to, 0.25f, 1.6f);
                }
                return;
            }

            _sinkTimer += dt;
            transform.position += Vector3.down * (sinkSpeed * dt);
            if (_sinkTimer >= sinkTime) Detonate();
        }

        private void Detonate()
        {
            Vector3 center = transform.position;
            float sqr = blastRadius * blastRadius;

            var subs = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine);
            for (int i = subs.Count - 1; i >= 0; i--)
            {
                var s = subs[i];
                if (s == null || !s.IsAlive || s.Transform == null) continue;

                Vector3 d = s.Transform.position - center;
                d.y = 0f;
                if (d.sqrMagnitude > sqr) continue;

                if (s is Game.Enemies.SubmarineBase sub) sub.ConfirmContact(5f);
                if (s is IDamageable dmg && dmg.IsAlive)
                {
                    dmg.TakeDamage(new DamageInfo(_damage, center, Vector3.up, DamageSource.Torpedo, _statsKey));
                    CombatStats.RecordHit(_statsKey, new Vector3(center.x, 0f, center.z));
                }
            }

            PooledEffect.Spawn(blastEffect, new Vector3(center.x, 0f, center.z));
            AudioManager.Play(Game.Data.SfxId.Explosion, center, 0.8f, 0.55f);

            if (PoolManager.Instance != null) PoolManager.Instance.Despawn(gameObject);
            else gameObject.SetActive(false);
        }

        public void OnSpawned() { _t = 0f; _sinking = false; }
        public void OnDespawned() { }
    }
}
