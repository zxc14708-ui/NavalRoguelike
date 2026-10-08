# 함교 부착물 수정 — 2026-10-08

## 현재 상태

사용자가 Unity를 종료한 뒤 프리팹/메시 재생성과 시각 검증 완료.
네 함교의 사선 비교 보드와 개별 측면 렌더를 확인했다. PNG와 unitypackage도 재생성했다.
함교별 삼각형 수: Patrol 820, Command 1088, Assault 1004, Missile 1008.

## 변경 소스

- `Assets/_Game/Editor/NavalStartingShipBuilder.cs`
  - 공통 고정 좌표 문 제거. 함교별 위치와 벽면 경사에 맞춘 문틀, 문, 손잡이, 문턱.
  - SidePanel / FrontPanel: Facet의 테이퍼와 전후 이동량으로 실제 표면 위치 계산.
  - 창문, 엔진 환기구, 역할 스트라이프, AESA 패널/격자, 흡기구, 센서 점검구도 표면 부착.
  - 지휘함: 엔진실부터 이어지는 통신 트렁크, 안테나 플랫폼, 휩 안테나 받침, SATCOM 칼라, CIC 패널.
  - 강습함: 지붕 위 레이더 재배치와 받침, 흡기 그릴, 받침 있는 광학센서.
  - 초계함: 받침 있는 탐조등, 측면 구조장비함.
  - 미사일함: 센서 냉각 루버, 지붕 전자지원 수신기.
  - 난간 기둥을 베이스에 닿도록 낮추고 안쪽으로 이동.
- `Assets/_Game/Editor/ShipConceptCapture.cs`
  - 기존 사선 렌더에 더해 `bridge_side_01_patrol.png`부터 네 측면 렌더 추가.

새 장식에 별도 게임플레이 효과는 추가하지 않는다. 3x1 점유 크기, BridgeModule, 레이더 회전/기만탄 소켓은 유지.

## 재현 절차

1. 프로젝트가 열려 있다면 사용자에게 씬 저장 및 Unity 종료를 요청한다. 강제 종료 금지.
2. 그래픽 가능한 Unity 배치에서 `Game.EditorTools.NavalStartingShipBuilder.RunBatch` 실행. `-nographics` 사용 금지.
3. `-shipConceptOutput`은 기존 작업 폴더의 `outputs/starting_ship_concepts` 지정.
4. `asset_validation.txt`와 로그 PASS 확인. 기존 센서-함교/굴뚝 간격 및 삼각형 2400개 제한 검증 유지.
5. 네 사선·측면 PNG를 실제로 열어 문 전체가 벽에 붙고 마스트/패널/난간 아래 공백이 없는지 검사. 특히 강습함 측면 문과 장갑판 경계, 지휘함 통신 트렁크와 CIC 간격 확인.
6. 필요 시 생성 좌표 수정 후 재실행. 런타임 검증은 `Game.EditorTools.StartingShipLiveVerification.RunBatch` (`-quit` 없이 실행).
7. 캡처는 작업 폴더 `outputs/starting_ship_concepts`의 `fleet_03_bridges.png`, `bridge_side_*.png` 참조.

## 수행한 검사

- 두 수정 C# 파일 `git diff --check` 통과.
- Unity 생성 배치: `work/ship-concepts/unity-bridge-mounts.log`, PASS / 종료 코드 0.
- 네 시작/확장 구성 배치 검사 통과. 센서-함교/굴뚝 보수적 간격 0.035m / 0.435m 통과.
- 런타임 회귀 검사: `work/ship-concepts/unity-bridge-live.log`, 결과 `outputs/starting_ship_concepts/live_validation.txt`.
