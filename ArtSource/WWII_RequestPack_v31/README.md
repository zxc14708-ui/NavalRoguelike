# CODEX_MODEL_REQUESTS.md — 남은 모델 요청 제작 결과

작성: 2026-10-17. 요청 문서의 적용 완료 기록과 §5의 새 요청을 대조했습니다. Unity Assets·프리팹·씬·전투 코드에는 적용하지 않았습니다. 수정 중인 요청 문서 자체도 덮어쓰지 않았습니다.

## 제공 파일: 8종

| 파일 | 상태/용도 | 메시 수 |
|---|---|---|
| `ENE_Ww2TorpedoBoat.fbx` | 신규. ww2_enemy_torpedo_boat용, 약6.82×1.94m, 흘수0.35m. 양현 고정 전방 어뢰관2기와 선수 소구경포 | 3: 고정 선체/포탑/포신 |
| `HULL_Ww2DeckPlate_Steel.fbx` | 신규. 2×2m 도장 강철 갑판판. 구축함 기본 권장 | 1 |
| `HULL_Ww2DeckPlate_Timber.fbx` | 신규. 2×2m 목재판 이음매 대안. 소형정/화물 작업부용 | 1 |
| `HULL_Ww2SideSkirt.fbx` | 신규. 너비2m 현측 띠. 갑판에서 아래로 내려가는 플레어, 현측 보강 띠와 어두운 수면 띠 | 1 |
| `ENE_Ww2TorpedoBomber_01.fbx` | 선택 요청. 흰 식별 띠/꼬리번호01 | 2: 동체/프로펠러 |
| `ENE_Ww2TorpedoBomber_02.fbx` | 선택 요청. 황토색 식별 띠/꼬리번호02 | 2 |
| `ENE_Ww2TorpedoBomber_03.fbx` | 선택 요청. 회색 식별 띠/꼬리번호03 | 2 |
| `ENE_CoastalBattery_Art.fbx` | 수정본. v27 포신-포구25cm 틈을 메시에서 해결, 물 아래 기초 추가, 고정/선회/고각별 병합 | 3 |

현재 v27 해안포와 v28 보급선은 요청 문서에서 이미 적용 완료로 표시돼 있습니다. 보급선 완성본을 새 버전으로 중복 제작하지 않았습니다. 보호 대상 갑판 표시는 문서의 선택 메모로 남아 있으며 이번 WWII 남은 모델 세트에는 포함하지 않았습니다.

## 어뢰정 연결

- 원점은 설계 수면선, 선체 바닥 Blender Z=-0.35m. `ModelPivot` 아래에 모든 메시와 무기 피벗이 들어갑니다.
- `TurretPivot/ElevationPivot/Muzzle`: 선수 기관총. 회전 피벗의 부모 관계 보존.
- `LaunchPoint_01/02`: 좌/우 고정 어뢰관 앞끝. 두 관 모두 선수 방향(+Y Blender → +Z Unity)입니다.
- 현재 `TorpedoBoat` 코드 주석과의 호환을 위해 같은 위치의 `TorpedoLaunchPoint_01/02`도 제공합니다.
- 현재 `NavalWw2Builder.cs`의 ww2_enemy_torpedo_boat는 현대 `ENE_TorpedoBoat`를 가리킵니다. Claude가 WWII 전용 프리팹을 만들고 기존 AI·충돌체·탄 프리팹을 복제한 뒤 시각부와 위 소켓을 연결하면 됩니다.
- 현재 `NavalWw2Builder.Art.cs`는 v30 폴더만 임포트합니다. v30의 기존14종을 유지하면서 v31을 추가로 읽는 방식으로 연결해야 신규 어뢰정·변형을 깨끗한 프로젝트에서도 함께 받을 수 있습니다. `ArtSourceDir`을 v31 하나로 대체하면 기존14종 원본이 누락됩니다.
- 현대 프리팹의 실제 메시 폭·길이에 맞춘 추가 배율을 그대로 상속하지 말고, 새 모델의 최종 길이6.82m와 바닥-0.35m를 기준으로 확인합니다. `ModelPivot`은 다른 WWII 적과 같은 WaterY(-0.9)에 연결합니다.

참고: [US Naval History and Heritage Command — U.S. Navy PT Boats](https://www.history.navy.mil/research/histories/ship-histories/u-s-navy-pt-boats.html). 초기 Elco의 목조 고속정·2/4기 어뢰관·소구경 방어포를 참고한 가상2관형입니다. PT-109의 정확한 복제 모델은 아닙니다. 게임의 소형 포함보다 약21% 긴 실루엣으로 정리했습니다.

## 갑판·현측을 기존 자동 선체에 연결하는 제안

현재 `ShipHullBuilder`를 확인했습니다. `deckPlatePrefab`, `sideSkirtPrefab`, `bowDeckMaterial`, `bowSideMaterial`로 시각부가 이미 분리되어 있어 고정 선체를 추가할 필요가 없습니다.

1. WWII 시작 시 사용하는 ShipHullBuilder의 `deckPlatePrefab`에 강철판 모델을 사용합니다. 수평 중심 원점이며 폭·길이2m입니다. 갑판 윗면은 Blender Z=0.09m, 체결구만0.0935m입니다. 기존 `deckTopOffset=0.095`의 함수 메시와 높이가 맞습니다.
2. `sideSkirtPrefab`에 현측 띠 모델을 사용합니다. 원점은 칸 외곽선, 바깥 방향은 Blender +Y(내보내기 후 +Z). 폭2m, 상단0.08m, 하단-1.52m입니다. `Rebuild`의 이웃 없는 방향 배치와 Quaternion.LookRotation을 그대로 쓸 수 있습니다.
3. WWII의 `bowDeckMaterial`은 기존 `WW2 Deck`, `bowSideMaterial`은 `WW2 Hull`로 맞춥니다. 새 재질을 생성할 필요가 없습니다.
4. 구축함의 기본 갑판은 강철판을 권장합니다. 목재판은 어뢰정이나 소품용 대안입니다. 이번 샘플은 기존의 무텍스처 미니어처 스타일을 유지하도록 얕은 기하학 이음매를 사용합니다. 비트맵 텍스처는 포함하지 않습니다.
5. 함수·함미가 점유 격자에 맞춰 계속 자라므로 고정된 완성 선체 FBX로 덮어쓰지 않습니다. `Preview/WWII_PlayerHull_Style.png`는 실제 v30 블록을 위 부품 위에 놓은 검토용 장면입니다.
6. 반복 칸은 동일 메시/재질을 공유하고 배치 변경 시에만 재생성합니다. 더 큰 선체에서는 생성한 정적 갑판을 합치는 방식도 가능하지만 모델 적용 뒤 실제 성능을 보고 판단합니다.

현측 조각은 갑판 기준 원점을 사용하므로 선박용 수면선 원점 규칙과 혼동하지 않습니다. 기존 hullDepth1.6m에 맞는 조각이며, 함선 전체의 물 높이를 추가로 내리는 용도가 아닙니다.

## 뇌격기 변형

기체 크기·Propeller·TorpedoDropPoint는 v30과 같습니다. 흰색/황토색/회색 재질은 모두 기존 WW2 팔레트입니다. 꼬리번호는 메시로 굽혀 있어 폰트나 별도 텍스처를 임포트할 필요가 없습니다.

세 파일은 시각적 식별을 위한 것입니다. 편대가 물리적으로 겹치는 이동/스폰 간격은 모델 교체만으로 바뀌지 않습니다. Claude가 편대 생성 시 01→02→03을 순환 배정하고 기체별 출발 간격·측방 오프셋을 함께 적용해야 겹침까지 줄어듭니다.

## 해안포 수정

- 기존 포신 앞끝은 ElevationPivot 좌표Y=2.46m, 포구 바깥벽 시작은2.71m였습니다.
- 포신 앞쪽 링을2.74m까지 연장해0.03m 겹치도록 수정했습니다. 포구 끝과 `Muzzle` 위치는 보존했습니다.
- 포구 슬리브 바깥 반경은0.115m에서0.123m로 늘려, 포신과 겹친3cm 구간의 표면이 같은 자리에 놓여 깜빡이지 않도록 했습니다.
- 고각축에 속한 부품을 한 메시로 병합했습니다. 기존 임시 보정 `CloseMuzzleGap`에 의존하지 않습니다. v31 채택 시 보정 함수를 빼는 것이 명확합니다(기존 `TaperedGunTube/OpenMuzzle` 이름의 메시가 없어 보정은 자동 건너뜀).
- 물 아래로 박힌 기초를 모델에 포함했습니다(Z=-1.9m). 적용 시 빌더가 별도로 만들던 `Rock plinth`는 제거하고, 기존 모델 배치 오프셋-0.55m를 먼저 유지해 점검합니다. 물속 기초가 중복되지 않게 합니다.
- 빌더의 방향 판정용 `ObserverRoof`, `CutStoneFoundation`는 빈 소켓으로 보존했습니다. `TurretPivot/ElevationPivot/Muzzle` 계층은 그대로입니다.
- 기존 환경 팔레트(`ENV_Concrete`, `ENV_RockStrata`)와 WW2 팔레트에 매핑했습니다.

## 파일/검사

- Models: FBX8종. 개별 원점, 단일 루트, scale1, -Z Forward/Y Up, FBX_SCALE_NONE.
- Preview: 어뢰정 사선/탑뷰/크기비교, 뇌격기3변형, 갑판부품, 플레이어 선체 시안, 포대/포구근접/전투시점 비교.
- `manifest.json`: 크기·재질·메시 수·소켓·수정량.
- `validation.json`: 재임포트로 단일 루트·스케일·기존 재질 이름·어뢰정 흘수와 발사점·갑판2m규격·포신 연결·메시 병합을 확인한 결과.
- `WWII_RequestPack.blend`: 모든 제작 원본, 모델별12m 간격으로 배치.

프리팹 연결·실전 발사·편대 이동·군도 물가 배치는 Claude 적용 후 검사 대상입니다.
