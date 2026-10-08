# 시작 함선 4종 · 실제 장비 적용 인수인계

작성 기준: 2026-10-07. 대상 프로젝트: C:/Users/최병욱/UnityProjects/NavalRoguelike/NavalRoguelike.

이 문서는 시작 함선 선택과 신규 블록의 실제 적용 범위를 기록한다. 함선 디자인 문서의 예정 능력치와 현재 동작을 구분해서 읽는다. 검증 결과는 [live_validation.txt](C:/Users/최병욱/Documents/Codex/2026-09-29/referenced-chatgpt-conversation-this-is-an/outputs/starting_ship_concepts/live_validation.txt)를 확인한다. 여기에서는 진행 중인 검증을 성공으로 미리 단정하지 않는다.

## 1. 현재 실제로 구현한 시작 구성

모든 함교는 3×1이며 기존 BridgeModule을 재사용한다. 내장 항해 레이더, 기만체, 손상 통제, 전속 기능을 유지한다. 다른 시작 블록은 1×1이다.

| 시작 함선 | 실제 시작 모듈 | 모듈 / 점유 칸 | 출항 시 호위함 |
|---|---|---|---|
| 범용 초계함 / Patrol | 전용 함교 + 기관포 + CIWS + 레이더 | 4개 / 6칸 | 0척 |
| 지휘함 / Command | 전용 함교 + 기관포 + CIWS + 편대 통신 중계 + 헬기데크 | 5개 / 7칸 | 역할 없는 기본 고속정 2척 |
| 고속 강습함 / Assault | 전용 함교 + 기관포 2개 + 강습 추진 흡기 | 4개 / 6칸 | 0척 |
| 미사일 구축함 / MissileDestroyer | 전용 함교 + VLS + 기관포 + 다중 표적 사격 통제 + 미사일 재장전 구획 | 5개 / 7칸 | 0척 |

시작 배치는 Assets/_Game/Data/StartingShips/Loadout_{Patrol,Command,Assault,Missile}_Start.asset에 있다. 성장 구성은 같은 위치의 Expanded.asset이며 성장 후 조합을 보여 주는 디자인 자료다. 선택 확정 시에는 StartLoadout만 설치한다.

좌표는 X가 선미(-)→선수(+), Z가 좌현(-)→우현(+)이다. 함교 origin=(-1,0), rotation=0이므로 (-1,0), (0,0), (1,0)을 점유한다. 현행 cellSize=2에서 로컬 위치는 (Z×2, deckHeight, X×2)이며 ModuleFactory.GetFootprintOffset이 여러 칸 블록의 시각 중심을 보정한다.

## 2. 출항 선택과 고정

메인 메뉴의 출항 버튼은 StartingShipSelectorUI.Show를 연다. 선택 화면은 네 함선의 기본형 네이티브 3D 프리뷰와 실제 시작 모듈을 표시한다.

확정 흐름은 다음과 같다.

1. ShipInitializer.TrySelect(concept, out reason) 호출.
2. 빈 검증용 ShipGrid에서 전체 배치, 범위, 중복, 연결, 회전, 배치 규칙, ModuleRuntime 프리팹을 검증.
3. 기존 모듈을 비활성화·철거하고 선택한 StartLoadout을 ModuleFactory로 설치.
4. ShipInitializer.LockForLaunch() 호출.
5. 지휘함의 InitialEscortCount=2를 기본 고속정으로 배치하고 실제 게임 출항 처리.

공개 API는 Game.Ship.ShipInitializer.SelectedConcept, SelectionLocked, TrySelect(StartingShipConcept, out string), LockForLaunch()이다. LockForLaunch는 반복 호출해도 추가 호위함을 생성하지 않는다. GameState.Playing 진입 시에도 잠금을 보장한다. 출항 후 TrySelect는 false를 반환하며 이유를 알려 준다.

카탈로그는 Resources/StartingShips/Catalog.asset이다. 네 원본 StartingShipConcept 에셋은 Data/StartingShips에 있으며 카탈로그가 이를 참조한다. Game.Data.StartingShipCatalog.All과 Get(StartShipKind)로 조회한다.

현재 실제 호위함 한도는 TaskForceEscortFormation.MaxEscorts=4다. 지휘함의 시작 2척은 EscortRole.None이며 편대 강화 카드로 역할을 정한다. 최종 6척 설계는 아직 적용하지 않았다.

## 3. 신규 블록 4종의 실제 효과

| 블록 / ModuleType | 데이터 필드 | 현재 효과와 적용 대상 |
|---|---|---|
| 편대 통신 중계 / FleetRelay=17 | EscortFireRateBonus=0.15 | 호위함 자율 무장 재사용 속도 +15%. 간격을 1.15로 나눈다. CP나 호위함 슬롯을 늘리지 않는다. |
| 강습 추진 흡기 / TurboIntake=18 | SpeedBonus=0.10, AccelerationBonus=0.15, MovingGunDamageBonus=0.10 | 최고 속력 +10%, 가속 +15%. 최고 속력의 50% 이상으로 항해할 때 기관포·76mm 함포 피해 +10%. |
| 다중 표적 사격 통제 / FireControlArray=19 | ExtraTrackedTargets=2, GuidedRangeBonus=0.10 | 동시 추적 +2, VLS·유도로켓·함대공 미사일 사거리 +10%. 레이더 탐지거리 자체는 늘리지 않는다. |
| 미사일 재장전 구획 / MissileLogistics=20 | MissileReloadReduction=0.15 | VLS·유도로켓 발사 간격과 셀 보급 시간을 각각 15% 단축. 함대공 미사일에는 적용하지 않고 탄약을 추가 생성하지 않는다. |

신규 블록은 모두 MaxCount=1, Weight>0인 설치 카드 장비다. 함급별 전용 장비로 제한하지 않았으므로 다른 시작 함선도 정비 카드에서 획득할 수 있다. 각 블록에 ModuleRuntime 파생 동작, 전투 프리팹, 아이콘이 연결되어 있다.

동종 지원이 여러 개 존재하는 잘못된 데이터에서도 ShipSystems는 각 보너스의 최댓값만 집계한다. 합산으로 무한 연사나 과도한 추적 수가 생기지 않도록 상한도 적용한다.

효과는 모듈이 작동 중일 때 유지한다. 파괴(HP=0) 또는 철거 시 설치·파괴·철거 이벤트로 ShipSystems.Recalculate가 다시 계산되어 원복한다. HP가 남아 있는 손상 상태에서는 현행 다른 모듈과 같이 기능을 유지한다. 재배치도 기존 ModuleFactory 설치·철거 경로를 사용한다.

ModuleDefinition 원본 ScriptableObject를 수정하지 않는다. ModuleInstance.EffectiveStats는 기본·장비 강화·성장 카드 수치를 보관하고, ModuleRuntime.Stats가 복사본에 함선 지원 배율을 적용한다. 실제 무기 수치를 확인할 때는 Runtime.Stats를 사용한다.

지원 변경 후 작동 중인 탄창은 AmmoMagazine.Reconfigure로 새 셀 보급 간격을 반영한다. 현재 탄약 수와 보급 진행률을 보존한다.

## 4. 정비 카드·현황판·사전 연결

RefitDraft.CollectInstallDefinitions(ProgressionConfig)는 기존 ProgressionConfig.Pool에 Resources/Modules의 신규 네 타입을 병합한다. 같은 에셋은 한 번만 넣고 Weight=0인 함교·전시용 정의는 설치 카드에서 제외한다. 현재 배치할 수 있는 블록을 우선하며, 기존 최대 설치 수와 중복 카드 규칙을 유지한다.

카드에는 신규 장비의 효과 수치, 적용 대상, 조건, 중복 규칙, 최대 1개 제한, 실제 아이콘을 표시한다. 선택·설치·재배치·영구 철거는 기존 RefitUI와 ModuleFactory 경로를 그대로 사용한다. 신규 지원은 함선 전체 효과이므로 인접 시너지 하이라이트를 추가하지 않았다.

ModuleStatusUI의 탄약 / 지원 열에 설치된 지원 효과를 표시한다. 파괴된 모듈에는 작동 중 지원 효과를 표시하지 않는다. CodexCatalog.CollectModules는 같은 설치 후보 목록과 네 StartingShipConcept의 StartLoadout을 읽어 신규 장비와 네 전용 함교를 사전에 노출한다.

추가 공개 API는 RefitDraft.IsConceptSupport(ModuleType), ModuleCardText.BuildSupportStatus(ModuleInstance)다. 새 지원 타입을 향후 추가한다면 IsConceptSupport의 화이트리스트와 카드 분류·설명도 함께 갱신해야 한다.

## 5. 주요 파일과 데이터 위치

| 역할 | 경로 |
|---|---|
| 함선 명세 / 카탈로그 | Assets/_Game/Scripts/Data/StartingShipConcept.cs, StartingShipCatalog.cs |
| 선택 UI / 메인 메뉴 연결 | Assets/_Game/Scripts/UI/StartingShipSelectorUI.cs, HUDView.cs |
| 출항 전 교체 / 출항 잠금 | Assets/_Game/Scripts/Ship/ShipInitializer.cs |
| 지원 집계 / 실제 이동 | Assets/_Game/Scripts/Ship/ShipSystems.cs, ShipController.cs |
| 지원 스탯 복사본 / 탄창 보급 | Assets/_Game/Scripts/Modules/ModuleRuntime.cs, Assets/_Game/Scripts/Combat/AmmoMagazine.cs |
| 지원 정의 / enum | Assets/_Game/Scripts/Modules/ModuleDefinition.cs, ModuleType.cs |
| 신규 블록 동작 | Assets/_Game/Scripts/Modules/Runtime/FleetRelayModule.cs, TurboIntakeModule.cs, FireControlArrayModule.cs, MissileLogisticsModule.cs |
| 호위함 적용 | Assets/_Game/Scripts/TaskForce/EscortDefense.cs |
| 카드 후보 / 설명 / 현황 / 사전 | Assets/_Game/Scripts/Refit/RefitDraft.cs, Assets/_Game/Scripts/UI/ModuleCardText.cs, RefitUI.Cards.cs, ModuleStatusUI.cs, CodexCatalog.cs |
| 네 컨셉 / Start·Expanded 배치 / 전용 함교 정의 | Assets/_Game/Data/StartingShips |
| 실제 지원 정의 | Assets/_Game/Resources/Modules/mod_fleetrelay.asset, mod_turbointake.asset, mod_firecontrolarray.asset, mod_missilelogistics.asset |
| 런타임 카탈로그 | Assets/_Game/Resources/StartingShips/Catalog.asset |
| 함교·블록·조립 프리팹 | Assets/_Game/Prefabs/StartingShips/Bridges, Blocks, Assemblies |
| 네이티브 디자인 생성 / 캡처 | Assets/_Game/Editor/NavalStartingShipBuilder.cs, ShipConceptCapture.cs |
| 실제 메인 씬 검증 | Assets/_Game/Editor/StartingShipLiveVerification.cs |

이전 컨셉용 신규 블록 정의는 GUID를 보존해서 Resources/Modules로 이동했다. 기존 배치·컨셉의 참조를 끊지 않도록 같은 ID의 새 에셋을 따로 만들어 중복시키지 않는다.

ShipSystems 공개 조회값은 EscortFireRateMultiplier, SpeedMultiplier, AccelerationMultiplier, MovingGunDamageMultiplier, GuidedWeaponRangeMultiplier, MissileReloadMultiplier, MissileResupplyMultiplier다.

## 6. 아직 적용하지 않은 설계

StartingShipConcept의 HP·방어·속력·선회·실탄·미사일·헬기 배율과 AllowedWeapons, EscortMax는 후속 설계 자료다. MechanicsImplemented는 false이며 전체 함급 특성이 구현되었다는 뜻으로 바꾸지 않는다.

현재 함선별 차이는 전용 함교 외형, 시작 설치 장비, 출항 시 기본 호위함 수에서 발생한다. TurboIntake 같은 장비의 실제 보너스는 적용하지만 함급 자체의 추가 고정 배율을 더 적용하지 않았다.

지휘함의 고정 고체력·고방어·저속, 헬기 피해·쿨타임 특성, 무장 제한, 최종 6척은 미구현이다. 강습함의 함급 전용 미사일 금지와 실탄 전반의 고정 피해·사거리·연사력 배율도 미구현이다. 다른 함급의 고정 특성·제한도 동일하다.

Expanded 배치와 완성형 캡처는 성장 목표를 검토하는 자료다. 그 배치 전체를 플레이 시작 시 지급하지 않는다. 신규 지원 블록의 별도 장비 강화 단계는 이번에 추가하지 않았다.

## 7. 검증 자료와 재실행 방법

산출물 폴더는 C:/Users/최병욱/Documents/Codex/2026-09-29/referenced-chatgpt-conversation-this-is-an/outputs/starting_ship_concepts이다.

- start_01_patrol.png부터 start_04_missile.png: 기본형 조립 에셋 캡처.
- live_start_01_patrol.png부터 live_start_04_missile.png: 실제 메인 씬에서 ModuleFactory로 설치된 기함 캡처.
- blocks_01_new.png: 신규 블록 네 종류.
- asset_validation.txt: 디자인·배치 에셋 생성 검증.
- live_validation.txt: 시작 구성, 실제 장비 효과, 카드 풀, 탄창 보존, 파괴·철거 원복, 지휘함 2척과 출항 잠금 검증.
- STARTING_SHIP_DESIGN.md, index.html: 외형과 성장형 자료.

검증 진입점은 Game.EditorTools.StartingShipLiveVerification.RunBatch다. 캡처가 있으므로 그래픽이 필요한 검증이며 -nographics를 쓰지 않는다. 로그·산출물은 작업자의 쓰기 허용 경로를 명시한다.

~~~powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe' -batchmode -projectPath 'C:/Users/최병욱/UnityProjects/NavalRoguelike/NavalRoguelike' -executeMethod Game.EditorTools.StartingShipLiveVerification.RunBatch -shipConceptOutput '<검증 산출물 폴더>' -logFile '<검증 로그 파일>'
~~~

최종 판단은 새 실행의 종료 코드, Unity 로그의 StartingShipLiveVerification PASS/FAIL, live_validation.txt를 함께 확인한다. 기존 파일이 있다는 것만으로 새 변경의 검증 완료를 판정하지 않는다. 이 문서 작성 과정에서는 Unity를 실행하지 않았다.

자동 검증은 런타임 객체와 수치·구성을 확인한다. 선택 UI의 해상도별 잘림, 실제 플레이 밸런스, 장시간 편대 행동, 함교와 센서의 시각적 간섭은 수동 플레이로 추가 확인한다.

## 8. 이어서 할 권장 작업 단위

| 작업 | 구현 대상과 범위 | 완료 기준 |
|---|---|---|
| S01 함급 고정 배율 | 선택 컨셉→런타임 능력치 경로를 정하고 ShipController, 무기 스탯 계산, 헬기 동작에 적용. 원본 SO와 현재 장비 보너스의 중복 적용을 피한다. | 네 함급별 HP·속력·선회·공격이 명세와 일치. 새 출격에 초기화. 장비 파괴로 함급 기본 특성은 사라지지 않음. |
| S02 무장 제한 | AllowedWeapons를 카드 후보, 실제 배치, 시작 배치 검증에서 공통 판정. 센서·보조 장비는 무장 제한과 구분. | 강습함 미사일 카드/설치 차단, 지휘함 허용 무장만 제공. 제한 이유를 표시. 막힌 대잠 수단에는 대체 선택지를 설계. |
| S03 지휘함 최종 6척 | 편대 한도를 컨셉별 런타임 값으로 전환하고 슬롯 UI·키입력·진형·카드·배열·호위함 회복을 함께 확장. | 지휘함 2척 시작→카드로 최대6척, 다른 함급 현행 한도 유지. 빈 슬롯·손실·복귀·최대치에서 오류 없음. |
| S04 신규 장비 성장 | 네 블록의 장비 강화 명세와 ModuleUpgradeProfile·외형 단계·효과 설명을 작성. | 강화 카드가 적용 가능한 블록만 제공. 한 번 강화 시 배율이 한 번만 반영되고 재배치 후 단계 유지. |
| S05 플레이 가시성 | Turbo의 항해 피해 조건, 편대 중계 효과, 실제 유도 사거리·보급 시간 변경을 작은 상태표시에 연결. | 전투 정보를 가리지 않는 표시. 조건 종료·장비 파괴·철거 시 즉시 상태 변화. |
| S06 선택·밸런스 검증 | 선택 화면 해상도·비율, 네 기본형 실제 전투, 잠수함 대응, 카드 진행을 플레이 점검. | 기본 장비만으로 초반 생존 가능. 함급별 무장 구성과 성장 선택의 차이를 확인하고 기록. |

S02를 구현할 때 기존 대잠 보장 로직 EnsureAntiSub도 같이 검토한다. 지휘함의 무장 제한과 대잠 폭뢰 보장 규칙이 충돌할 수 있으므로 허용되는 헬기 등으로 대체 규칙을 먼저 결정한다.

## 9. 회귀 방지 기준

- 직접 조종은 기함 한 척이다. 선택과 편대 성장은 기존 플레이 흐름을 유지한다.
- 출항 후 시작 함선 선택을 풀지 않는다. 재선택·중복 확정으로 호위함이 추가 생성되지 않게 한다.
- 장비 지원은 설치한 장비의 생존 상태를 따라 계산하고 원본 SO를 변경하지 않는다.
- 카드 최대 수, 최대 설치 수, 동일 카드 중복 제외, 탄약 수·보급 진행률 보존을 유지한다.
- 함급 전용 고정 특성이 실제로 구현되기 전에는 설계 배율·최종6척을 현행 기능처럼 안내하지 않는다.
- 새 블록 enum은 기존 직렬화 번호를 변경하지 않고 끝에 추가한다.

