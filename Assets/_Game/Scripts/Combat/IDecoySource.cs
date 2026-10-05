using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 플레이어가 기만체 스킬(Q)을 누르면 함께 발사되는 장비(함교, 기만체 발사기).
    /// 장비마다 재장전을 따로 갖고, 준비된 것만 발사한다.
    /// </summary>
    public interface IDecoySource : Game.Ship.ISkillSource
    {
        /// <summary>threat가 있으면 그 반대쪽으로 흩뿌린다. 없으면 사방으로.</summary>
        void Deploy(Vector3? threat);
    }

    /// <summary>플레이어가 재밍 스킬(E)을 누르면 작동하는 전자전 장비.</summary>
    public interface IJammerSource : Game.Ship.ISkillSource
    {
        void Jam();
    }
}
