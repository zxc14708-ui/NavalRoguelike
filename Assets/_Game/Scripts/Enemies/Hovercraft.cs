using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 공기부양 상륙정(4스테이지, 모티브 주브르급). 느리고 단단하다. 멀리서 돌며 로켓을 한꺼번에 쏟는다
    /// (포격 지원정과 같은 예고 원 — 발 수와 산포가 크다). 수면 위에 떠 있어 기뢰가 닿지 않고 모래톱을 그대로 지난다.
    /// </summary>
    public class Hovercraft : ArtilleryBoat
    {
        /// <summary>기뢰가 닿지 않는다(PlayerMine이 확인).</summary>
        public const bool RidesAboveWater = true;
    }
}
