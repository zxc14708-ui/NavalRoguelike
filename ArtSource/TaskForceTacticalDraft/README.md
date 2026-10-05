# Task Force Tactical Draft

이 폴더는 전투단 호위함과 전술 지원 이펙트의 **적용 전 검토용 ArtSource**다. Unity의 `Assets`·프리팹·스크립트에는 아직 연결하지 않는다.

## 산출물

- `TaskForceTacticalAssets.blend`: 16척 + 이펙트 5종 원본. 각 에셋은 독립 컬렉션과 단일 루트를 가진다.
- `Models/ESC_<ROLE>_T0~T3.fbx`: CAP·ASW·EW·STK 기본형과 3단계 개량형.
- `Effects/FX_<CODE>_Tactical.fbx`: AEW·CAP·ASW·EW·STK 전술 표시용 분리 메시.
- `Preview/*.png`: 전체·역할별·이펙트 검토 렌더.
- `manifest.json`: 파일, 역할색, 단계, 현재 코드 배율, 삼각형 수.

## 역할과 외형 언어

| 코드 | 기존 색상 | 식별 실루엣 | 개량 표현 |
|---|---|---|---|
| CAP | 녹색 `(0.35, 1.00, 0.58)` | 높은 방공 레이더, VLS, 후방 SAM/CIWS | VLS 증가 → 회전 레이더 → 4면 AESA·후방 CIWS |
| ASW | 청색 `(0.22, 0.68, 1.00)` | 함수 소나돔, 격납고, 비행갑판, 어뢰관, 예인소나 | 소노부이 → 음향처리 장비 → 확장 비행갑판 |
| EW | 자주색 `(0.83, 0.37, 1.00)` | 측면 광대역 배열, 레이돔, 십자 안테나 | 재머 돔 → 대형 측면 배열 → 방향탐지 혼 |
| STK | 주황색 `(1.00, 0.52, 0.16)` | 좌우 4연장 경사 발사대와 사통 레이더 | 후방 발사대 → VLS·데이터링크 → 예비 발사대 증설 |

T0~T3 FBX의 명목 함체는 모두 약 3×9m다. 현재 코드의 표시 배율 `0.60 / 0.76 / 0.92 / 1.08`을 유지하면 약 1.8×5.4m에서 3.24×9.72m까지 성장한다. 따라서 적용할 때 모델 자체와 런타임 배율을 동시에 두 번 키우면 안 된다.

## 좌표·루트·재질

- Blender `+Y` = 선수, `+Z` = 위, 원점 = 함 중앙 흘수선.
- FBX: `-Z Forward / Y Up / Apply Scalings: All Local`.
- 모든 모델은 단일 `<파일명>_Root` 아래 Mesh/Empty만 포함한다.
- 기본 팔레트 이름은 `Deck Grey`, `Naval Blue Grey`, `Gunmetal`, `Radar Glass`, `Sensor Glass`, `Warning Yellow`를 유지한다.
- 역할색은 `Role CAP`, `Role ASW`, `Role EW`, `Role STK`로 분리했다.

## 연결용 소켓

- 공통: `SupportOrigin`, `RadarPivot`, `PrimaryGunPivot`, `PrimaryGunElevation`, `PrimaryGunMuzzle`.
- CAP: `AirSearchRadarPivot` 또는 `IntegratedAesaMast`, `SAM_TrainPivot`, `SAMLaunchPoint_01~02`.
- ASW: `HelicopterLaunch`, `SonarOrigin`, `TorpedoMount_P/S`, `TorpedoLaunchPoint_01~02`.
- EW: `AntennaPivot`, `EWArrayPivot_P/S`, `EWOrigin`.
- STK: 각 `MissileBank*Pivot`, `LaunchPoint_MissileBank*_*`, `FireControlRadarPivot`.

## 이펙트 리그

이펙트는 애니메이션이 구워진 프리팹이 아니라, Unity에서 회전·확대·페이드하기 좋은 **분리된 3D 전술 표시 요소**다.

- AEW: `SweepPivot`, `ScanSector`, `SweepLine`, `TrackPip`, `TrackBracket`, 데이터링크 벡터.
- CAP: 방공 우산, 요격 벡터, 위협 브래킷.
- ASW: 다중 소나 링, `BearingLinePivot`, 잠수함 접촉 브래킷, 수심 눈금.
- EW: 방사형 재밍선, 노이즈 바, 끊긴 락온 브래킷.
- STK: 표적 링, 조준 브래킷, 접근 벡터, 명중시간 눈금.

권장 재생은 `0.6~1.8초`, 밝기 페이드 `1 → 0`, 링 스케일 `0.2 → 1.0`, 스캔 피벗은 월드 Y축 회전이다. 현재 `TaskForceWorldFeedback`의 실제 판정과 색을 그대로 따랐으며, 모델과 이펙트는 판정·콜라이더를 포함하지 않는다.

## 적용 전 주의

검토 승인 전에는 이 폴더의 FBX를 `Assets/_Game/Art/Models`로 복사하거나 `TaskForceEscortFormation`을 수정하지 않는다. 적용 요청을 받으면 런타임 그레이박스 생성 대신 역할·단계별 FBX를 불러오고, 기존 배율과 `SupportOrigin`을 유지하는 별도 빌더/프리팹 연결 작업이 필요하다.

## 적용 상태 (2026-09-30, Claude)

사용자 요청으로 게임에 연결했다. 이 폴더의 FBX는 원본으로 그대로 두고, 사본을 `Assets/_Game/Art/Models/TaskForce/`(+`Effects/`)에 두었다.

- `Naval/Art/Build Task Force Escort Prefabs`(`Assets/_Game/Editor/NavalEscortArtBuilder.cs`)가 임포트 설정(축 변환 굽기, 애니메이션·카메라·조명 끔), 재질 이름 연결, 프리팹 생성을 한다. 모델을 다시 내보내면 사본을 덮어쓰고 이 메뉴를 다시 실행하면 된다.
- 재질: `Naval Blue Grey`·`Deck Grey`·`Gunmetal`·`Radar Glass`·`Sensor Glass`·`Warning Yellow`는 기존 프로젝트 재질, `Naval Superstructure` → `Naval cool grey`, `Deck Marking White` → `Medical White`, `Role *`·`FX *`는 `Art/Models/Materials/TaskForce/`에 새로 만들었다(FX는 URP Unlit 가산 투명·양면).
- 프리팹: `Resources/TaskForce/Escorts/ESC_<ROLE>_T<n>`, `Resources/TaskForce/Effects/FX_<CODE>_Tactical`. 런타임 축척 0.60/0.76/0.92/1.08은 그대로(모델은 3×9m 명목 크기 유지).
- 소켓은 `SupportOrigin`만 쓴다(지원 발원점). Blender 중복 접미사 `.001`은 런타임이 무시한다. 리그 회전 피벗은 `SweepPivot`·`BearingLinePivot`.
- Unity 임포트 때 `ArtModelPostprocessor`가 각 파일의 `Hull`·`MainDeck`·`BridgeBlock`·`BridgeCrown`·`PrimaryGunHouse` 메시를 "안쪽을 향한 면"으로 판정해 뒤집는다. 다음 내보내기에서 이 메시들의 면 방향(노멀)을 바깥으로 맞춰 주면 좋다.
