using UnityEngine;

namespace Game.Combat
{
    /// <summary>진로가 곧게 이어진다고 보고 예측 사격할 수 있는 표적(미사일, 항공기).</summary>
    public interface IHasVelocity
    {
        Vector3 Velocity { get; }
    }
}
