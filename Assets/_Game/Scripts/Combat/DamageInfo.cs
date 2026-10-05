using UnityEngine;

namespace Game.Combat
{
    /// <summary>피해의 출처와 지점을 함께 전달한다. 지점이 있어야 격자 셀 판정이 가능하다.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 HitPoint;
        public readonly Vector3 Direction;
        public readonly DamageSource Source;

        /// <summary>쏜 무기 이름(개발용 통계). 적 공격이면 null.</summary>
        public readonly string Tag;

        public DamageInfo(float amount, Vector3 hitPoint, Vector3 direction, DamageSource source, string tag = null)
        {
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction;
            Source = source;
            Tag = tag;
        }
    }

    public enum DamageSource { Gun, Missile, Torpedo, CookOff, Collision }

    /// <summary>피해를 받을 수 있는 대상. 플레이어 함선, 적, 미사일이 모두 구현한다.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }
}
