using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 경위그선(4스테이지): 수면 위를 스치며 다가와 기관포를 소사하고 지나쳐 멀어졌다가 다시 온다 — 전투기와 같은 흐름을 낮은 고도로.
    /// 떼로 나온다. 체력이 낮아 기관포 · CIWS로 잡는다.
    /// </summary>
    public class LightWig : FighterJet, ISkimmer
    {
        public bool Skimming => true;
    }
}
