using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 표식(2026-11-15): 이 아래 메시는 자산(FBX · Codex 병합 메시 · 기본 도형)이라 섬을 치울 때 지우면 안 된다.
    /// IslandField.Remove는 런타임에 만든 메시만 지우고, 이 표식(또는 EnvironmentArt) 아래는 건너뛴다.
    /// 예전엔 군항 외형(기본 상자 · Codex 시설 메시)을 치울 때 공유 메시까지 지울 수 있었다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KeepSharedMeshes : MonoBehaviour { }
}
