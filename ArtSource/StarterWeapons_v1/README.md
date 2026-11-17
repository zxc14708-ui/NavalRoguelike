# 1단계 무장 블록 — 실제 3D 시안 v1

기본 함포와 다목적 기관총의 Unity용 로우폴리 아트 에셋이다. 1칸 블록이며 기존 공용 재질을 사용한다. 전직·획득·성능 데이터 연결은 포함하지 않는다.

| 모델 | 삼각형 | 메시 | 바닥 크기 | 전체 높이 |
| --- | ---: | ---: | --- | --- |
| 기본 함포 `MOD_BasicNavalGun_L1` | 660 | 3 | 1.7 × 1.7m | 1.240m |
| 다목적 기관총 `MOD_MultipurposeMG_L1` | 696 | 3 | 1.7 × 1.7m | 1.332m |

포신은 앞쪽으로 돌출된다. 함포는 방패·단포신·노출형 받침, 기관총은 가는 총열·측면 탄약 상자·고각 가능한 개방형 받침으로 구분했다.

## 사용 파일

- 원본: 이 폴더의 `MOD_BasicNavalGun_L1.blend`, `MOD_MultipurposeMG_L1.blend`.
- FBX: `Assets/_Game/Art/Models/StarterWeapons_v1/`.
- Unity 아트 프리팹: `Assets/_Game/Art/StarterWeapons_v1/*_Visual.prefab`.
- 검토 씬: `Assets/_Game/Art/StarterWeapons_v1/StarterWeapons_L1_Review.unity`.
- Unity 실물 캡처: `Preview/*_Unity.png`, 게임에 가까운 경사 시점은 `Preview/StarterWeapons_L1_GameAngle.png`.
- Blender 메시 검토 렌더: `Preview/*_Mesh.png`, `Preview/*_Top.png`. Unity 캡처와 구분해서 사용한다.

## 인게임 연결 규격

- Blender +Y가 포신 앞, +Z가 위이며 미터 단위. Unity 프리팹은 +Z를 앞으로 맞춘 래퍼를 포함한다.
- `TurretPivot → ElevationPivot → Muzzle` 계층. 함포 방패는 좌우 선회만, 총열·포구는 상하 조준과 함께 움직인다.
- 고정 받침·선회부·고각부 세 메시로 병합했다. 회전 경계를 가로질러 합치지 않았다.
- `Deck Grey`, `Naval Blue Grey`, `Gunmetal` 공용 URP Lit 재질을 참조한다. 텍스처와 추가 콜라이더는 없다.
- 프리팹은 시각용이다. 실제 무장으로 등록할 때 ModuleDefinition과 무기 런타임을 연결하고 기존 ModuleFactory 경로에서 충돌체·격자 판정을 부여한다.

## 검증 범위

Unity 6000.3.23f1의 별도 검증 프로젝트에서 실제 FBX를 임포트했다. 임시 프로젝트는 같은 프로젝트의 공용 재질·URP 설정을 사용한다.

확인 항목: 크기·바닥 원점·삼각형 예산·URP 재질 연결·피벗 계층·기존 WeaponController의 두 방향 선회/고각 조준. 결과는 `UnityValidation.txt`에 기록한다. 전투·밸런스·전직 동작 시험은 이 아트 시안 범위에 포함하지 않는다.

## 재생성

1. Blender 5.2에서 `build.py` 실행: 원본 두 개와 FBX를 생성한다.
2. Unity에서 `Naval > Art > Build Starter Weapons L1 Preview`: 임포트·검증·아트 프리팹·검토 씬·렌더를 생성한다. 저장된 씬을 연 상태에서 실행한다.
3. 별도 검증이 필요하면 `prepare_validation.ps1`이 `ArtSource/StarterWeapons_v1/ValidationProject`에 작은 프로젝트를 준비한다. 기존 WeaponController와 FireArc 구조체, 모델 임포트 함수를 복사해 검증한다.
4. `render.py`는 원본 메시를 Blender로 렌더하는 보조 검토용이다.

처음 생성했던 이미지 생성 시안은 최종 3D 에셋에 사용하지 않았다. 최종 프리뷰는 실제 메시에서 렌더한다.
