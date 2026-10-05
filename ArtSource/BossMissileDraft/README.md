# 1스테이지 보스 미사일 발사관 원본

2026-09-27: 시안 FBX를 `Assets/_Game/Art/Models/PatrolBoat_CoastalBoss.fbx`로 복사해
`ENE_Boss.prefab`에 적용했다. 외형에는 좌·우현 각각 4관(총 8관)이 있지만
한 번의 일제사격은 표적 쪽 현측에서 4발이다. 구형 `PatrolBoat.fbx`는
다른 참조를 보존하기 위해 그대로 두었다.

기존 `PatrolBoat.fbx`의 함체·함교·함수/함미 포탑을 유지하고, 열린 함미 갑판 양쪽에 **4연장 경사식 원통 발사대**를 각각 추가했다. 한 기마다 발사관을 2×2로 쌓아 사용자가 제공한 사진의 둥근 발사구·지지 프레임 느낌을 반영했다. **좌현 발사대는 좌현 바깥쪽, 우현 발사대는 우현 바깥쪽으로** 약간 상향 발사한다. 함미 쪽 구명환 두 개와 용도를 알기 어려웠던 검은 원통 두 개는 이 시안에서 제거했다. 함교 옆 구명환은 유지했다.

- 결과물: `PatrolBoat_MissileBoss_Draft.fbx`, `PatrolBoat_MissileBoss_Draft.blend`
- 미리보기: `PatrolBoat_MissileBoss_3q.png`, `PatrolBoat_MissileBoss_top.png`
- 회전 확인: `PatrolBoat_AftGun_turn_90.png` (`AftGun_TurretPivot`를 후방 기준 90° 선회한 별도 렌더)
- 루트: `PatrolBoat_MissileBoss_Draft`, 원점은 함 중앙 흘수선, Blender +Y가 선수, FBX 내보내기는 `-Z Forward / Y Up`
- 새 소켓: `MissileLaunchPoint_01`~`MissileLaunchPoint_08`, 각 발사관의 **측면 방향 입구 앞**. 좌현 소켓 방향 약 `(-0.96, -0.05, +0.29)`, 우현 약 `(+0.96, -0.05, +0.29)` (Blender 좌표)
- 기존 소켓 `BowGun_Muzzle`, `AftGun_Muzzle` 유지
- 메시: `MissileTube`, `MissileTube.001`~`.007`
- 폴리곤: 원본 9,376 tris → 시안 11,392 tris (+2,016 tris)

## 함교 마스트 / 함미 포탑 간섭 수정

기존 마스트 중심은 함미 포탑 피벗에서 앞쪽으로 0.75 m뿐이어서 사다리·하부 지지대가 포탑 공간에 들어왔다. `Main_mast`와 사다리, 가새, 레이더 피벗·안테나, 케이블과 비콘을 **선수 방향으로 1.30 m** 함께 옮겨 함교 지붕 뒤쪽에 안착시켰다. 함미 포탑의 고정 받침은 제자리에 두었다.

`AftGun_TurretPivot` 아래에 포탑 장갑, 포신, 포신 슬리브, 조준기와 `AftGun_Muzzle`이 모두 들어 있다. 피벗을 돌리면 포구도 같이 움직인다. `verify_aft_gun.py`로 후방 정면(0°), 좌우 90°/120°/135°를 확인했을 때 마스트·함교와 메시 교차가 없었다. **좌우 150° 이상 앞쪽을 향하면 함교와 교차**하므로, 나중에 회전 애니메이션을 구현한다면 후방 정면 기준 약 **±130°** 범위로 제한하는 편이 안전하다.

이 폴더는 Blender 원본·렌더 보관용이며, 게임에서는 위의 `Assets` 복사본을 사용한다.
`BossShip.cs`가 함수/함미 포탑을 별도로 조준하며 함교 방향 ±130° 바깥 선회를 제한한다.
