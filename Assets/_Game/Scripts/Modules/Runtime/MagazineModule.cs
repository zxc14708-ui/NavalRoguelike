using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 탄약고. 가까운 기관포·76mm의 발사 간격·탄약 용량·보급 속도를 올리지만(MagazineSupport), 파괴되면 유폭한다.
    /// 지원 효과는 각 무기 모듈이 자기 쪽에서 조회하므로 여기서는 유폭만 책임진다.
    ///
    /// 유폭: 파괴된 칸을 중심으로 맨해튼 반경 cookOffRadiusCells 안의 모듈에 거리 감쇠 피해(CookOffDamage × (1 - 거리/(반경+1))),
    /// 선체에는 한 번만 CookOffDamage × hullShare. 옆 탄약고가 부서지면 연쇄 유폭한다.
    /// "무기 옆에 붙이면 재장전이 빠르지만, 터지면 그 무기까지 잃는다" — 배치 트레이드오프의 핵심.
    /// </summary>
    public class MagazineModule : ModuleRuntime
    {
        [SerializeField] private int cookOffRadiusCells = 1;
        [Tooltip("유폭 이펙트(풀링). 비어 있으면 생략")]
        [SerializeField] private GameObject explosionVfx;
        [SerializeField] private float explosionScale = 2.5f;
        [Tooltip("유폭 피해 중 선체가 받는 비율(한 번만)")]
        [SerializeField, Range(0f, 1f)] private float hullShare = 0.3f;

        private DamageResolver _resolver;

        /// <summary>개발용: 마지막 유폭 결과(피해 입은 모듈 수, 파괴된 모듈 수).</summary>
        public static (int damaged, int destroyed) LastCookOff { get; private set; }

        protected override void OnInitialized()
        {
            _resolver = GetComponentInParent<DamageResolver>();
            if (_resolver == null) Debug.LogWarning("[Magazine] DamageResolver를 찾지 못해 유폭이 적용되지 않습니다.", this);
        }

        public override void OnModuleDestroyed()
        {
            PooledEffect.Spawn(explosionVfx, transform.position + Vector3.up * 0.8f, explosionScale);
            AudioManager.Play(Game.Data.SfxId.Explosion, transform.position, 1f, 0.55f);
            if (_resolver == null || Instance == null) return;

            LastCookOff = _resolver.ApplyCookOff(Instance.Origin, Stats.CookOffDamage, cookOffRadiusCells, hullShare);
            CombatLog.Add("유폭", $"탄약고 ({Instance.Origin.X},{Instance.Origin.Z}) 폭발: 모듈 {LastCookOff.damaged}개 피해, {LastCookOff.destroyed}개 파괴");
        }
    }
}
