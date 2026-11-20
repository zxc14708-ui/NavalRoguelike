# 시작 함선 4종 — 사용 가능한 기본 구성과 지원 블록

작성일: 2026-10-07  
Unity 프로젝트: `C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike`  
제작 기준: Unity 6000.3.23f1 / URP / 기존 2m 모듈 격자

## 제작 범위와 현재 상태

실제 Unity 메시·재질·프리팹으로 함교 4종, 추가 블록 4종, 시작 배치 4종, 성장 배치 4종을 제작했다. 기존 장비 모델과 `ShipHullBuilder`를 사용해 선체를 조립했으며, Unity 카메라에서 렌더한 캡처를 제공한다.

- `StartingLoadout` 에셋 8개는 기존 데이터 형식이다. 모든 설치 순서와 완성 후 배치 제약을 검사한다.
- 함교 모듈 4개는 기존 `BridgeModule`을 사용한다. 기존 내장 기만체·레이더·손상통제·전속 기능을 유지한다.
- 조립된 `SHIP_*` 프리팹은 씬 전시/선택 화면 미리보기용 **정적 시각 프리팹**이다. 전투 플레이어 오브젝트 자체가 아니다.
- 실제 전투 함선을 만들 때에는 선택한 `StartingLoadout`을 기존 플레이어의 `ShipInitializer`/`ModuleFactory`로 설치한다.
- 출항 전 4종 선택과 실제 기본 배치 설치, 지휘함의 역할 없는 기본 고속정 2척을 구현했다. 출항 이후 함종은 변경할 수 없다.
- 새 블록 4종은 전용 런타임으로 전투 효과를 적용하며 레벨업 설치 카드·장비 현황·사전에 표시된다. 동종 최대 1개다.
- 함급별 고정 HP/속력/무기 배율·무기 제한·지휘함 최종 6척은 여전히 미적용 설계 데이터다. 전체 함급 규칙을 의미하는 `MechanicsImplemented=false`는 유지한다.
- 기존 `Prototype_Main.unity` 씬 파일과 기본 `StartingLoadout.asset`은 유지했다. `HUDView` 출항 절차에 함선 선택을 추가하고 `ShipInitializer.TrySelect`가 선택 배치를 실제 생성한다.
- 최신 구현·검증·후속 작업은 **LIVE_STARTING_SHIPS.md**를 먼저 확인한다.

## 4종의 시각적 정체성

| 함선 | 함교 실루엣 | 작은 시작 구성 | 성장 방향 |
|---|---|---|---|
| 범용 초계함 / PATROL | 단층 항해 함교, 짧은 회전 레이더, 단일 연돌, 청록 표식 | 함교 + 기관포 + CIWS + 레이더 | 함포·센서·대잠·헬기를 고르게 조합 |
| 지휘함 / COMMAND | 두 층 CIC, 쌍 SATCOM 돔, 통신 마스트, 넓은 연돌, 녹색 표식 | 함교 + 기관포 + CIWS + 헬기데크 + 통신 중계, 기본 고속정 2척 | 대공·전자전·헬기·통신. 현행 편대 최대 4척, 전시 성장형 6척은 설계 시각화만 |
| 고속 강습함 / ASSAULT | 낮은 경사 함교, 장갑 측판, 쌍 흡기/배기, 짧은 센서, 주황 표식 | 함교 + 기관포 2개 + 추진 흡기 | 기관포·76mm 함포·탄약고 중심. 미사일 제한 규칙은 미적용 |
| 미사일 구축함 / MISSILE | 경사 상부구조, 통합 위상배열 센서탑, 억제형 연돌, 청색 표식 | 함교 + VLS + 기관포 + 사격통제 + 미사일 보급 | VLS·레이더·대공·미사일 보급 확장 |

초계함·강습함은 **모듈 4개 / 점유 6칸**, 지휘함·미사일함은 **모듈 5개 / 점유 7칸**이다. 함교 1개가 앞뒤 3칸을 차지한다. 지휘함의 기본 고속정 2척은 별도 편대이며 출항 확정 때 생성한다.

함교는 공통 3×1 규격, 폭 약 1.8m × 길이 약 5.8m, 원점은 가운데 칸 바닥이다. Unity +Z가 선수다. 새 메시에는 FBX 축 변환이 필요 없다. 실제 함교 삼각형 수는 검증 파일에 기록한다.

## 새로 만든 블록

| 프리팹 | 외형 | 실제 효과 | 현재 동작 |
|---|---|---|---|
| `BLK_FleetRelay` | 무전 장비함, 경사 통신 안테나, 쌍 휩 안테나 | 자율 호위함 능력 재사용 속도 +15% | 1칸, 최대 1개, 실전 설치 카드 |
| `BLK_TurboIntake` | 쌍 흡기 하우징, 전면 그릴, 후면 배기구 | 최고속력 +10%, 가속 +15%, 기준 최고속력 50% 이상에서 기관포·76mm 피해 +10% | 1칸, 최대 1개, 실전 설치 카드 |
| `BLK_FireControlArray` | 낮은 추적 배열, 센서 받침, `FireControlOrigin` 소켓 | 동시 추적 +2, VLS·유도로켓·SAM 사거리 +10% | 1칸, 최대 1개, 실전 설치 카드 |
| `BLK_MissileLogistics` | 장갑 보급 구획, 3개 해치, 노란 경고선 | VLS·유도로켓 발사 간격/기존 셀 보급 시간 -15% | 1칸, 최대 1개, 실전 설치 카드 |

신규 타입은 기존 enum 끝에 추가하여 기존 직렬화 값을 보존한다. 전용 런타임은 `FleetRelayModule`, `TurboIntakeModule`, `FireControlArrayModule`, `MissileLogisticsModule`이다. 정의는 기존 GUID를 유지한 채 `Resources/Modules/mod_*.asset`로 옮겼고 `weight=0.65`, `maxCount=1`을 사용한다. 원본 SO를 수정하지 않으며 파괴·철거 시 효과가 원복된다. 지원 CP/스킬은 현행 게임에 없으므로 다시 도입하지 않는다.

## 의도한 능력치 초안 — 현재 미적용

| 함선 | HP | 방어력 | 속력 | 선회 | 특화 배율 |
|---|---:|---:|---:|---:|---|
| 초계함 | ×1.00 | ×1.00 | ×1.00 | ×1.00 | 기준형 |
| 지휘함 | ×1.50 | ×1.30 | ×0.75 | ×0.80 | 헬기 피해 ×1.25, 출격 쿨타임 ×0.80 |
| 강습함 | ×0.80 | ×0.75 | ×1.30 | ×1.25 | 실탄 피해 ×1.20, 사거리 ×1.15, 발사 빈도 ×1.20 |
| 미사일함 | ×1.00 | ×1.00 | ×0.90 | ×0.90 | 미사일 피해 ×1.25, 사거리 ×1.20, 재장전 시간 ×1.10 |

수치는 초기 비교용 초안이며 실전 밸런스를 검증하지 않았다. 방어력 배율을 현행 피해 감소 비율에 어떻게 변환할지도 후속 구현에서 정해야 한다. 발사 빈도 ×1.20을 발사 간격에 그대로 곱하면 오히려 느려지므로 간격에는 역수를 적용해야 한다.

무기 제한 의도:

- 지휘함: 직접 무장은 기관포·CIWS·SAM만. 헬기데크/센서/지원 블록은 별도 보조 장비로 허용한다.
- 강습함: 기관포·CIWS·76mm·폭뢰 허용, 미사일 계열 금지.
- 초계함/미사일함: 현재 무기 계열을 허용하며 미사일함은 성장 가중치로 미사일·사격통제를 유도한다.
- 지휘함: 기본 고속정 2척은 적용했다. 최대 6척은 설계 의도만이며 현재 라이브 `TaskForceEscortFormation.MaxEscorts=4`는 유지한다.

## 데이터와 프리팹 위치

```text
Assets/_Game/Scripts/Data/StartingShipConcept.cs
Assets/_Game/Scripts/Modules/Runtime/ConceptBlockModule.cs
Assets/_Game/Editor/NavalStartingShipBuilder.cs
Assets/_Game/Editor/ShipConceptCapture.cs

Assets/_Game/Data/StartingShips/
  ShipConcept_Patrol.asset
  ShipConcept_Command.asset
  ShipConcept_Assault.asset
  ShipConcept_Missile.asset
  Loadout_<이름>_Start.asset
  Loadout_<이름>_Expanded.asset
  Modules/mod_bridge_<이름>.asset

Assets/_Game/Resources/Modules/mod_<블록>.asset
Assets/_Game/Resources/StartingShips/Catalog.asset

Assets/_Game/Prefabs/StartingShips/
  Bridges/MOD_Bridge_<이름>.prefab
  Blocks/BLK_<블록>.prefab
  Assemblies/SHIP_<이름>_Start.prefab
  Assemblies/SHIP_<이름>_Expanded.prefab

Assets/_Game/Art/StartingShips/
  Meshes/*.asset
  Materials/*.mat

Assets/_Game/Scenes/StartingShipConcepts.unity
```

Unity에서 `StartingShipConcepts.unity`를 열면 성장 함선, 작은 시작 함선, 함교 모델을 함께 확인할 수 있다. 이 씬은 전시용이다. 플레이어 전투 흐름을 제공하지 않는다.

프리팹·데이터를 다시 만들고 캡처하려면 메뉴 `Naval > Art > Build Starting Ship Concepts and Capture`를 사용한다. 해당 메뉴는 전시 씬으로 전환하므로 열려 있는 작업 씬을 먼저 저장한다. 원래 전투 씬을 재생성하는 `Setup Prototype Scene`을 실행할 필요는 없다.

## 캡처 목록

- `fleet_01_starting.png`: 4종 작은 출항 구성
- `fleet_02_expanded.png`: 4종 성장 구성
- `fleet_03_bridges.png`: 함교 4종 비교
- `ship_01_patrol.png` ~ `ship_04_missile.png`: 개별 성장 함선의 사선 시점
- `bridge_01_patrol.png` ~ `bridge_04_missile.png`: 개별 함교 근접
- `layout_01_patrol.png` ~ `layout_04_missile.png`: 성장 구성의 탑다운 배치
- `blocks_01_new.png`: 새 블록 4종
- `asset_validation.txt`: 실제 배치/폴리곤 검사 결과
- `StartingShipConcepts.unitypackage`: 이번에 추가한 시제품 에셋만 묶은 패키지

패키지는 기존 NavalRoguelike 에셋을 참조한다. 완전히 빈 Unity 프로젝트에 임포트하여 독립 실행하는 패키지가 아니다.

## 후속 구현 순서

출항 선택·실제 기본 배치·초기 고속정 2척·신규 블록 전투 동작/카드/아이콘은 구현했다. 후속 작업은 다음과 같다.

1. 함급별 고정 HP/조함/화력/헬기 배율을 별도 런타임 집계로 구현하고, 블록·성장 카드 보너스와 중복 적용되지 않도록 검사한다.
2. 무기 제한을 채택할 경우 `RefitDraft` 후보뿐 아니라 `ModuleFactory` 설치 경로에도 검증을 추가한다.
3. 지휘함 최종 6척을 채택할 경우 편대 한도·슬롯·진형·카드·UI·검증을 함께 확장한다. 현재는 전 함종 최대 4척이다.
4. 새 지원 블록의 강화 I/II 수치·외형·업그레이드 프로필을 설계한다. 현재는 기본형 설치 카드만 제공하며 전용 강화 단계는 없다.
5. 4종 실제 전투에서 초반 생존성·사격각·탄약 지속력·성장 속도를 플레이 테스트한다. 현재 자동 검사 통과를 실전 밸런스 검증으로 간주하지 않는다.

## 검증과 남은 범위

Unity 컴파일, 8개 구성의 `CanPlace`/`Place`, 완성 배치 제약, 메시 참조와 그래픽 캡처를 검사한다. 별도 `StartingShipLiveVerification.RunBatch`는 실제 게임 씬에서 4종 시작 배치, 신규 블록 효과·탄약 진행 유지·파괴/철거 원복·설치 카드 후보·기본 호위함 2척·출항 잠금을 검사한다. 결과는 `live_validation.txt`를 확인한다. 함급별 고정 배율/무기 제한과 전체 전투 밸런스는 검증 범위가 아니다.

2026-10-07 센서탑 배치 수정: 미사일 구축함의 위상 배열 센서탑과 정면 패널이 함교 후방 상단에 겹치던 문제를 수정했다. 센서탑·AESA 패널·격자·마스트·회전 레이더를 `SensorAssembly`로 묶어 기존 위치보다 뒤로 0.60 Unity 단위 이동했고, 기관실 지붕과 센서탑 사이에 `SensorPedestal`을 추가했다. 기존 3×1 점유·함교 크기·굴뚝·BridgeModule 동작은 유지한다. 생성기는 메시 병합 전에 센서 묶음과 함교/굴뚝의 보수적 AABB 간격 및 받침대의 수직 연결을 검사하며 실패 시 생성을 중단한다. `asset_validation.txt`의 `GEOMETRY Missile` 행에 결과가 기록된다.

받침에 묻히는 기존 갑판 해치는 미사일 함교에 한해 받침 측면 서비스 해치로 교체했고, 기만체 발사 소켓은 받침 밖으로 이동했다. 다른 함급의 해치·소켓 배치는 변경하지 않는다.

높은 함교와 센서가 이웃 무기의 사격각을 가리는 현행 규칙이 있으므로, 설치 가능한 배치가 곧 최적 전투 배치를 의미하지 않는다. 성장 구성은 역할과 실루엣을 비교할 수 있는 예시이며 플레이어에게 강제할 완성 레시피가 아니다.

2026-10-07 현재 프로젝트에는 이미 `CombatFaction`과 피격 가능한 호위함이 있다. 예전 인수인계의 "진영 구분부터 구현"이라는 설명을 다시 적용하지 말고 최신 `ARCHITECTURE.md`와 코드를 기준으로 작업한다.
