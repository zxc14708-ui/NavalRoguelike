using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 플랫폼 포대(4스테이지 고정 적): 유전 플랫폼 갑판 가장자리 받침 위의 쌍열 대공포 + 4연장 미사일 상자.
    /// 해안 미사일 포대와 같은 예고(발사관 들어 올림) 뒤 미사일을 쏘고, 함께 붙은 근접방어포(MissileInterceptor)가 아군 유도탄을 쏘아 떨어뜨린다.
    /// 부서지면 그 플랫폼 아래 수면에 불타는 기름띠가 번진다(플랫폼마다 한 번).
    /// </summary>
    public class PlatformBattery : CoastalMissileBattery
    {
        protected override void Die()
        {
            Vector3 me = transform.position;
            Game.World.IslandInfo best = null;
            float bestD = 30f * 30f;
            foreach (var isl in Game.World.Islands.All)
            {
                if (isl == null || !isl.Platform || isl.Spilled) continue;
                float d = (new Vector2(isl.Center.x - me.x, isl.Center.z - me.z)).sqrMagnitude;
                if (d < bestD) { bestD = d; best = isl; }
            }
            if (best != null)
            {
                best.Spilled = true;
                Game.World.OilSlick.Spawn(best.OilSlickPoint);
            }
            base.Die();
        }
    }
}
