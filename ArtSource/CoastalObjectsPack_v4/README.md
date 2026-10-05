# 해안·해상 소품 추가팩 v4

기존 EnvironmentVarietyPack_v3와 같은 미니어처 재질·색상 계열의 **분리형 3D 소품 5종**입니다. Unity 프로젝트에는 적용하지 않았고 기존 모델도 덮어쓰지 않았습니다.

| FBX | 역할 / 형태 | 배치 기준 |
| --- | --- | --- |
| ENV_Breakwater | 12m 모듈 방파제, 각진 소파석, 계류 볼라드, 항로등 | 루트 수면 Z=0 / Snap_Start·Snap_End 연결 |
| ENV_FishingBoat | 작은 어선, 조타실, 마스트, 작업갑판, 어구 | 루트 수면 Z=0 / 선수 +Y |
| ENV_FloatingPontoon | 부력체, 목재 계류 갑판, 펜더, 접근 경사로 | 루트 수면 Z=0 / ShoreConnection 참조 |
| ENV_SeaRockArch | 실제 통로가 뚫린 각진 해식 아치 | 루트 수면 Z=0 / 해저 부분 포함 |
| ENV_BeachFishingGear | 지지대가 있는 그물 건조대, 어구 상자, 통발, 밧줄 | 루트 지면 Z=0 / 마른 해안 배치 |

## 파일

- Models: 기본 5종 + `_LOD1` 5종 = FBX 10개.
- 모델별 `.blend` 5개: 실제 FBX 재임포트 검증 후 저장한 파일.
- CoastalObjectsPack.blend: 전체 검토용 원본.
- Preview: 각 소품 개별 이미지, 전체 모음, 조합 예시.
- manifest.json: 크기, 삼각형 수, 배치·소켓 정보.
- validation.json: 실제 FBX 재임포트 검증 결과. `all_passed`를 확인하세요.
- ZIP의 Source: 재생성용 Blender Python 스크립트.

## 축·재질·임포트

미터 단위, 단일 루트 원점, scale=1. Blender 제작 좌표는 Z Up이며 FBX는 -Z Forward / Y Up / All Local 규약으로 내보냅니다. 기존 모델과 같은 Unity 임포터 규칙을 사용하세요. Blender `manifest` 좌표를 Unity의 XYZ로 그대로 복사하면 안 됩니다.

물에 뜨는 모델은 바닥이 아니라 **수면이 원점**입니다. 현 게임 수면이 Unity Y=-0.9이면 변환된 모델의 수면 기준을 그 높이에 맞춥니다. 해저 기반·선체 흘수·폰툰이 수면 아래로 내려가는 것은 정상입니다. BeachFishingGear만 지면 기준입니다.

텍스처 없는 기존 단색 팔레트입니다. Unity에서는 URP/Lit 불투명 재질로 연결하고 기존 색상을 유지하세요. Preview의 바다·지면·문자·조명·카메라는 FBX에 들어가지 않습니다.

LOD는 얇은 펜더·밧줄·그물의 구멍이 깨지지 않는 안전한 메시만 줄입니다. 그래서 일부 소품은 LOD에서도 삼각형 감소율이 작습니다. LODGroup·Collider·램프 발광·물리 부력·어선 이동·경사로 애니메이션은 포함하지 않았습니다. Empty 소켓은 연결 기준만 제공하며 동작 코드가 아닙니다.

## 배치 권장

방파제는 항구 외곽에, 부잔교와 어선은 보호된 항내에, 어구는 마른 해변이나 부두에 배치하면 역할이 잘 읽힙니다. 바위 아치는 항구 입구나 암초 군집에 소수만 배치하세요. 소품 간 실제 메시 간격을 확인하고, 좁은 항로를 Collider로 전부 막지 않도록 주의합니다.

랜덤 배치용으로 각각 분리했지만 기존 섬 생성기나 Unity 런타임에 새 소품을 연결하지는 않았습니다. 조합 예시는 확인용 렌더이며 새 섬 프리팹이 아닙니다.

## 재생성

ZIP을 풀고 Source 폴더에서 실행합니다. 기존팩 빌더를 유틸리티로 임포트하므로 함께 들어 있는 Source 파일들을 유지하세요.

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --python .\build_coastal_objects.py -- --output ..\RebuiltCoastalObjects
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --python .\validate_environment_detail.py -- --output ..\RebuiltCoastalObjects
```

실제 Unity 임포트 및 플레이 성능은 미검증입니다.

## 게임 적용 상태 (2026-10-02, Claude)

- FBX(LOD1 제외)를 `Assets/_Game/Art/Models/Environment/Islands·Props`에 복사, 에디터 `NavalEnvironmentArtBuilder`(메뉴 Naval/Art/Build Environment Prefabs)가 `Resources/Environment/Islands·Props/ENV_*.prefab`을 만든다.
- 자동 생성 섬의 일부(바위섬 35%·무인도 65%)가 이 모델 섬으로 나온다. 판정은 지형 셸 정점으로 만든 볼록 조각 60개(환초 석호는 둘러싸인 못이라 메움), 잔교·방파제·어선·부잔교·해식 아치는 상자 판정.
- 해안 소품(어선·부잔교·방파제·해식 아치·해변 어구·부표)은 자동 생성 섬 둘레에 확률로 놓인다. 레이더·풍속계 회전, 탐조등 흔들림, 부표·어선 출렁임, 등대 불빛 연결.
- 레이더 기지·벙커의 게임 효과(탐지·파괴·보상)와 LODGroup은 아직 없다. 자세한 내용은 `ARCHITECTURE.md` "섬(엄폐)" 절.