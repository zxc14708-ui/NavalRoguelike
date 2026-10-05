# NavalRoguelike 전투단·호위함 시스템 인수인계

> **2026-10-02 편대 개편 — 이 문서의 0장이 최신이다.** 1~7장의 출항 편성(FORCE PACKAGE·CP)·지원 스킬(AEW/CAP/ASW/EW/STK)·C2 지원 버튼·`TaskForceController`·`KillChainService`는 **삭제된 예전 구조**의 기록이다. 8장의 진영 규칙·생존 규칙(전투 불능 복귀·표적 우선도)은 그대로 유효하다.

## 0. 현재 구조(편대 개편, 2026-10-02)

- **흐름**: 출항 버튼 → 바로 전투(편성 화면 없음). 편대는 비어서 시작하고, 레벨업 정비 카드의 3번 자리에 나오는 **편대 배치**(고속정 1척 합류, 최대 4척, 편대가 비면 레벨 3부터 보장)와 **편대 강화**(역할 없는 고속정 → 역할 선택 패널에서 방공·대잠·전자전·미사일 지정 / 역할이 있으면 T1~T3 개량) 카드로 꾸린다.
- **코드**: `TaskForceEscortFormation`(편대 상태·카드 API: `Deploy`, `AssignRole`, `Upgrade`, `PickUpgradeTarget`, `GetInfo`, `Changed`), `EscortDefense`(자율 능력), `TaskForceWorldFeedback`(전술 리그 재생 도구), `TaskForcePanelUI`(편대 현황 패널, 표시 전용), `RefitCard.FleetDeploy/FleetUpgrade`, `RefitDraft`(편대 카드 추첨), `RefitUI`(편대 카드 표시·역할 선택 패널).
- **자율 능력**: 모두 함포(24m) · 방공 함대공 요격(42m) · 전자전 근접 교란(36m) · 대잠 자동 대잠 타격(40m, 잠항 중도) · 미사일 대함 타격(60m, 가치 높은 표적). 수치·단계별 성장은 `ARCHITECTURE.md` "편대" 절.
- **모델**: 역할별 T0~T3은 Codex **TaskForceEscortRedesign**(2026-10-02 적용, T0 고속정 → T3 호위함). 포탑·레이더는 `EscortTurrets`가 움직이고 발사음은 호위함 전용 `SfxId.Escort*`. 역할 없는 고속정 전용 모델 `ESC_PB_T0`은 없다(사용자: 필요 없음) — `ESC_STK_T0`의 역할색 재질(`Role *`)을 무채색으로 바꿔 쓴다. Codex가 `ESC_PB_T0.fbx`를 만들어 `Art/Models/TaskForce`에 넣고 `Naval/Art/Build Task Force Escort Prefabs`를 돌리면 자동으로 그 모델을 쓴다.
- **검증**: `TaskForceVerification.RunBatch`(편대 카드 흐름으로 갱신 — 성공 문구 `[TaskForceVerification] PASS — 편대 카드(배치·역할 지정·개량)·편대 패널·출항 편성 없음`), 플레이어 검증 `-escortOnly`.
- **남은 일**: 호위함 섬 회피, 편대 카드 등장 빈도·자율 능력 수치 밸런스, 호위함 카드 아이콘(지금은 역할 코드 글자 타일).


업데이트: 2026-09-29  
프로젝트: `C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike`  
Unity: `6000.3.23f1` / URP  
기준 씬: `Assets/_Game/Scenes/Prototype_Main.unity`

이 문서는 전투단 시스템을 이어서 수정할 에이전트가 현재 구현 상태와 다음 우선순위를 빠르게 파악하도록 만든 작업 인수인계서다. 구현된 사실과 미구현 항목을 구분해서 기록했다.

## 1. 제품 방향과 고정된 설계 원칙

- 장르의 중심은 계속 **플레이어 기함 1척을 직접 조종하는 액션 로그라이크**다.
- 호위함, 항공기, 잠수함, 전자전 자산은 RTS식 개별 조작 유닛이 아니라 자동 운용되는 전투단 전력 또는 전술 지원 능력으로 취급한다.
- 전투단 편성은 아무 때나 바꾸지 않는다. 게임 시작 전 출항 화면에서 선택하고, 출항 확정 후 해당 런 동안 잠근다.
- 함대 규모는 Command Point(CP)로 제한한다.
- 탐지 → 추적 → 사격통제 → 교전의 Kill Chain은 기존 표적·레이더 시스템 위에 얹는 단순 상태 계층으로 유지한다.
- 호위함은 처음에는 적 고속정 정도 크기의 기본 성능으로 시작하고, 레벨 종료 카드에서 개량되며 외형과 효과가 단계적으로 커진다.
- 지원 능력 사용 알림은 전장 정보를 가리면 안 된다. 중앙 대형 상태창 대신 우측 상단의 짧은 알림과 C2 패널의 지속 상태 표시를 사용한다.

## 2. 현재 구현된 플레이 흐름

1. 타이틀/시작 화면에서 출항을 누르면 `TaskForcePanelUI.ShowPreflight`가 전체 화면 편성 UI를 연다.
2. 사용자는 최대 CP 안에서 지원 전력을 선택한다.
3. 확정 시 `TaskForceController.LockPackage()`가 호출되고 편성이 잠긴다.
4. 전투 중에는 C2 패널에서 편성된 지원 능력만 사용할 수 있다.
5. 지원 능력 사용 시 쿨다운·활성 시간이 갱신되고, 월드 피드백과 짧은 우측 상단 알림이 나온다.
6. 배정된 CAP/ASW/EW/STK 역할에는 대응하는 소형 수상 호위함이 기함 주변 편대 슬롯에 생성된다. AWACS는 오프맵 지원이므로 수상 호위함을 만들지 않는다.
7. 레벨 종료 `RefitUI`에서 개량 가능한 호위함이 있으면 세 번째 장비 카드가 `ESCORT UPGRADE` 카드로 바뀐다. 단 연속되지 않게 한 레벨 걸러 나온다(2026-09-30, 모듈 카드가 매번 2장 이상 남게).
8. 호위함 개량 카드를 선택하면 즉시 해당 역할의 레벨이 1 올라가고, 모델 규모·세부 파츠·지원 효과가 강화된다.

기본 편성은 AEW + CAP + EW이며 총 5/6 CP다. 따라서 기본 출항 시 수상 호위함은 CAP와 EW 두 척이다.

## 3. 현재 지원 능력 수치

| 코드 | 역할 | CP | 쿨다운 | 지속 | 기본 배정 | 현재 효과 |
|---|---|---:|---:|---:|---|---|
| AEW | 조기경보 통제 | 2 | 52초 | 20초 | 예 | 탐지 거리 +22, 동시 추적 +4 |
| CAP | 전투초계 | 2 | 48초 | 18초 | 예 | 1.25초 간격으로 항공기·미사일 자동 요격 |
| ASW | 대잠 초계 | 2 | 42초 | 즉시 | 아니오 | 접촉 확정 후 원격 대잠 타격 |
| EW | 전자전 지원 | 1 | 38초 | 10초 | 예 | 접근 중인 유도탄 지속 교란 |
| STK | 대함 타격 | 3 | 62초 | 즉시 | 아니오 | 추적 중인 고가치 수상 표적 정밀 타격 |

기본 Command Capacity는 `ShipConfig.baseCommandCapacity`에서 읽어 `ShipSystems.CommandCapacity`로 제공하며 현재 기본값은 6이다. CP 초과 편성은 거부된다.

## 4. 호위함 성장 상태

`TaskForceEscortFormation.MaxUpgradeLevel`은 3이며 총 L0~L3 네 단계다.

| 단계 | 모델 스케일 | 의도 |
|---|---:|---|
| L0 | 0.60 | 기본 고속정급 호위함 |
| L1 | 0.76 | 개량 고속정 |
| L2 | 0.92 | 초계함급 |
| L3 | 1.08 | 소형 호위함급 |

L0의 대략적인 외형 치수는 1.8 × 5.3m이며, 적 고속정 콜라이더 2.2 × 5m와 비슷한 체감 크기를 목표로 했다.

역할별 개량 효과는 현재 코드에 다음과 같이 고정되어 있다.

- CAP: 레벨마다 요격 피해 +25%
- ASW: 레벨마다 피해 +8, 접촉 유지 +2초
- EW: 레벨마다 범위 +5, 교란 시간 +0.55초
- STK: 레벨마다 타격 피해 +15

개량 단계는 현재 **런타임 전용**이며 새 런에서 초기화된다. 영구 저장은 구현하지 않았다.

## 5. 주요 파일과 책임

### 전투단 코어

- `Assets/_Game/Scripts/TaskForce/TaskForceController.cs`
  - `SupportId`: `Awacs`, `Cap`, `AntiSubmarine`, `ElectronicWarfare`, `SurfaceStrike`
  - CP 계산, 편성 변경, 편성 잠금, 쿨다운/활성 시간, 지원 효과 실행
  - `SupportActivated`, `SupportEffectApplied`, `Changed` 이벤트 제공
- `Assets/_Game/Scripts/TaskForce/KillChainService.cs`
  - `TrackState`: `Detected`, `Tracked`, `FireControl`, `Engaged`
  - 기존 표적 정보를 바탕으로 단순화된 Kill Chain 상태 계산
- `Assets/_Game/Scripts/TaskForce/TaskForceBootstrap.cs`
  - 기존 씬을 재작성하지 않고 런타임에 필요한 전투단 컴포넌트를 연결
- `Assets/_Game/Scripts/TaskForce/TaskForceWorldFeedback.cs`
  - AEW 청록 펄스, CAP 녹색 요격선/공중 폭발, ASW 파란 소나/수중 피격, EW 보라 펄스/링크, STK 주황 타격선/폭발 표시
- `Assets/_Game/Scripts/TaskForce/TaskForceEscortFormation.cs`
  - 배정된 지원 역할을 수상 호위함으로 시각화
  - 편대 슬롯, 부드러운 추종, 흔들림, 항적, 역할별 모델 파츠
  - `GetUpgradeOffer(int levelSeed)`, `ApplyUpgrade(SupportId role)`, L0~L3 성장 관리
  - 지원 효과의 시각적 발사 원점을 해당 호위함 위치로 전달

### UI와 기존 시스템 연결

- `Assets/_Game/Scripts/UI/TaskForcePanelUI.cs`
  - 출항 전 FORCE PACKAGE 선택 화면
  - 전투 중 패널(2026-09-30 간소화): **편성한 지원만** 줄로 — 역할색 코드 · 이름(호위함 개량 단계) · 준비/작동 N초/재사용 N초 · 진행 막대, 클릭하여 요청. 폭 270, 오른쪽 열에서 VLS·헬기·교리 콘솔 아래에 HUDView가 쌓는다. CP·Kill Chain·SCREEN·시계·편성 잠금 표시는 출항 편성 화면에만 둔다.
  - 지원 사용 알림은 우측 상단 350 × 50, 2.8초 표시
- `Assets/_Game/Scripts/UI/HUDView.cs`
  - 기존 Launch 동작을 출항 전 편성 화면으로 연결하고 취소 시 시작 화면으로 복귀
- `Assets/_Game/Scripts/UI/RefitUI.cs`
  - 레벨 종료 세 카드 중 세 번째 카드를 조건부 호위함 개량 카드로 대체(한 레벨 걸러). 카드 그림 자리는 역할 코드 타일 + 개량 단계
  - 선택 즉시 `TaskForceEscortFormation.ApplyUpgrade` 호출
- `Assets/_Game/Scripts/UI/RadarScopeUI.cs`
  - 전투단으로 확장된 실효 탐지 거리 사용
- `Assets/_Game/Scripts/Combat/TargetingSystem.cs`
  - 외부 탐지 거리/동시 추적 보너스를 받을 수 있도록 확장
- `Assets/_Game/Scripts/Combat/ITargetable.cs`
  - `TargetRegistry` 포함. 현재 진영 구분이 없는 구조라 다음 단계에서 특히 주의해야 한다.
- `Assets/_Game/Scripts/Combat/TargetAllocator.cs`
  - 기존 표적 분배 로직. 호위함을 실제 피격 대상으로 만들기 전에 진영 분리를 먼저 해야 한다.

### 데이터와 검증

- `Assets/_Game/Scripts/Data/ShipConfig.cs`
- `Assets/_Game/Scripts/Ship/ShipSystems.cs`
- `Assets/_Game/Data/Ships/ShipConfig.asset`
  - Command Capacity 데이터 경로
- `Assets/_Game/Editor/NavalDataBuilder.cs`
  - 데이터 생성/갱신 도구. 현재 전투단 컴포넌트는 런타임 부착이므로 별도 씬 재생성이 필요 없다.
- `Assets/_Game/Editor/TaskForceVerification.cs`
  - 전투단, 출항 편성 잠금, 소형 호위함, 개량 카드, 축소 알림을 검증하는 배치 스모크 테스트
- `ARCHITECTURE.md`
  - 프로젝트 전체 구조와 전투단 섹션, 변경 이력

## 6. 런타임 관계 요약

```text
HUDView Launch
  └─ TaskForcePanelUI.ShowPreflight
       └─ 편성 확정 → TaskForceController.LockPackage

TaskForceController
  ├─ KillChainService / TargetingSystem에 지원 보너스 적용
  ├─ TaskForcePanelUI에 상태 변경 알림
  ├─ TaskForceWorldFeedback에 지원 발동 이벤트 전달
  └─ TaskForceEscortFormation에 배정 역할·지원 효과 전달

RefitUI.Open
  └─ TaskForceEscortFormation.GetUpgradeOffer(level)
       └─ 카드 선택 → ApplyUpgrade(role)
```

## 7. 현재 자동 검증 방법

Unity 에디터가 프로젝트를 점유하지 않은 상태에서 다음 명령으로 검증한다. 로그 경로는 쓰기 가능한 위치로 지정한다.

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.23f1\Editor\Unity.exe' `
  -batchmode -nographics `
  -projectPath 'C:\Users\최병욱\UnityProjects\NavalRoguelike\NavalRoguelike' `
  -executeMethod Game.EditorTools.TaskForceVerification.RunBatch `
  -logFile '<writable-log-path>'
```

현재 기대하는 성공 문구:

```text
[TaskForceVerification] PASS — 소형 호위함·개량 카드 기반·축소 알림·출항 편성·지원
```

마지막 확인 로그는 이 작업 대화의 `work/unity-escort-upgrade-ui.log`였다. 스모크 테스트는 다음을 확인한다.

- 전투단 Controller/KillChain/Feedback/Formation/UI 생성
- 알림 최대 크기 350 × 50
- 기본 6 CP, 기본 편성 5 CP, CP 초과 거부
- 출항 전 편성 UI와 확정 후 편성 잠금
- 출항 후 편성 변경 거부
- 기본 CAP/EW 호위함 두 척 생성
- L0 스케일 0.60, 개량 후 L1 스케일 0.76
- 실제 `RefitUI`에 `ESCORT UPGRADE` 카드가 표시됨
- AEW 발동 시 탐지 +22 / 추적 +4
- 지원 월드 피드백 생성

주의: 배치 테스트는 객체 상태와 UI 수치를 검사하지만 최종 화면의 미적 품질까지 보장하지 않는다.

## 8. 알려진 한계와 위험

### 반드시 먼저 해결할 구조 위험

현재 `ITargetable`/`TargetRegistry`에는 아군과 적군을 나누는 진영 또는 팀 개념이 없다. 적 AI도 플레이어 기함을 직접 가리키는 구조가 섞여 있다. 따라서 현 상태에서 호위함에 HP와 `ITargetable`만 붙이면 다음 문제가 발생할 수 있다.

- 아군 무기가 호위함을 적으로 선택
- 적 무기가 기존처럼 기함만 노리거나, 반대로 잘못된 대상을 선택
- 아군 호위함 격침이 적 처치/XP 보상으로 계산
- 미사일·투사체·충돌 판정의 소유자 구분 실패

**호위함을 실제 피격 대상으로 만드는 작업보다 Faction/Team 추상화를 먼저 구현해야 한다.** → 2026-09-30 작업 A로 해결(아래 "진영 규칙").

### 진영(아군/적) 규칙 — 작업 A 완료(2026-09-30)

구현: `Combat/CombatFaction.cs`(`CombatFaction` Player/Hostile/Neutral, `IFactionMember`, `Factions`), `ITargetable : IFactionMember`, `TargetRegistry`(진영별 목록).

| 대상 | 진영 | 등록 |
|---|---|---|
| 기함 `ShipController` | Player | OnEnable/OnDisable에서 Surface로 등록 |
| 적 함정·항공기·잠수함 `EnemyController` | Hostile | 스폰/반환 |
| 적 미사일(`isThreat`) · 적 어뢰 | Hostile | 미사일만 등록(어뢰는 `Torpedo.Active`) |
| 아군 유도탄·요격탄(`Missile`, isThreat=false) | Player | 등록 안 함(`Missile.ActiveFriendly`) |
| 기만체 `Decoy` | Player | Decoy로 등록(적 미사일이 노림) |

- **조회**: 플레이어 무기·센서·지원·경고 UI는 모두 `TargetRegistry.HostileTo(CombatFaction.Player, kind)`만 본다(TargetingSystem, CIWS, 폭뢰·대잠 어뢰, 전자전, 헬기, 기만체 발사기, 전투단 AEW/CAP/ASW/EW/STK·Kill Chain, 무장 패널, 미사일 경고, 레이더). 레이더의 기만체만 `Of(Player, Decoy)`(내가 뿌린 채프). 적 AI가 플레이어 쪽을 찾을 때는 `HostileTo(CombatFaction.Hostile, kind)` — 기함과 등록된 아군이 나온다. `Get(kind)`(진영 무관)는 섬 배치처럼 위치만 보는 곳에만.
- **피해**: 탄(`Projectile`)은 맞히는 레이어로 소유 진영을 정하고(`Factions.OwnerFromHitMask` — 적 몸체·적 미사일을 맞히면 Player, 기함·아군 미사일을 맞히면 Hostile), 같은 진영 몸체에는 피해를 주지 않는다(직격·파편). 적 어뢰(Hostile)·미사일(`Faction`)도 같은 진영은 맞히지 않는다. 물리 레이어 규칙도 그대로: **아군 몸체 = PlayerShip(6), 적 몸체 = Enemy(7)**.
- **보상**: 경험치·격침 수는 `EnemyController.Die`에서 `Factions.GrantsReward`(적대 진영)일 때만.
- **검증**: `-factionOnly`(아군 가짜 호위함으로 등록소·탐지·무장 사격·아군 탄 피해·보상 확인), `TaskForceVerification.RunBatch` 통과.

**작업 B에서 호위함을 실제 피격 대상으로 만들 때 지킬 것**
1. 호위함 컴포넌트가 `ITargetable`(Kind Surface, Faction Player)과 `IDamageable`을 구현하고, 활성화 때 `TargetRegistry.Register`, 격침·비활성화 때 `Unregister`.
2. 콜라이더를 **PlayerShip(6) 레이어**에 둔다 — 적 포탄·어뢰(PlayerShip을 맞힘)에 맞고 아군 탄에는 맞지 않는다.
3. 적 AI의 표적 선택은 `HostileTo(CombatFaction.Hostile, TargetKind.Surface)`에서 고른다(지금은 모두 `Player` 트랜스폼 고정).
4. 호위함 격침 경로에서 `GameManager.RegisterKill`을 부르지 않는다(`Factions.GrantsReward`가 false).


### 호위함 생존성과 적 표적 선택 — 작업 B 완료(2026-09-30, 같은 날 "방패" 문제로 후방 대기·전투 불능 복귀·표적 제한 조정)

처음 구현(기함 옆 고정 대형 + 격침 시 레벨 끝까지 지원 중단)은 호위함이 적과 기함 사이에서 피해를 다 받고 가라앉는 방패가 됐다. 그래서 평소엔 뒤에 두고, 지원을 쓸 때만 앞으로 나오며, 잃어도 잠시 이탈했다 돌아오게 바꿨다.


| 항목 | 규칙 |
|---|---|
| 선체 | `TaskForceEscortFormation.MaxHullFor(level)` = 60 + 20×단계(60/80/100/120, 기함 100). 선체 50% 아래 연기, 25% 아래 불길 |
| 판정 | 호위함 루트 = `ITargetable`(Surface, Player) + `IDamageable` + `ITargetPriority`(우선도 0.4, 동시 공격자 최대 2). PlayerShip(6) 레이어 `BoxCollider`(명목 3×2.2×9m × 단계 축척). OnEnable/OnDisable에서 등록소 등록·해제 |
| 적 표적 선택 | `EnemyController.UpdateTarget`: 1.5~2.5초마다 `HostileTo(Hostile, Surface)`에서 "수평 거리 ÷ 우선도" 최소를 고른다. 바꾸려면 20% 이상 나아야 함(헤매지 않게), 노리던 표적이 죽으면 즉시 다시 고름. `Player` 필드 = 지금 표적, `Flagship` = 기함. 이미 `MaxAttackers`만큼 노리는 적이 있는 호위함은 후보에서 뺀다(`EnemyController.AttackersOn`). 기함만 노리는 적(`TargetsEscorts => false`): 정찰기, 보스 2종, 대함미사일정·순항미사일 잠수함·어뢰 잠수함, 자폭 보트·자폭 드론(한 방 큰 피해는 기함에만). 호위함을 노리는 적은 고속정·초계함 함포와 전투기 기총뿐 |
| 후방 대기 | 평소 슬롯은 기함 뒤 25~35m(`StandbyOffset`, 적 교전 반경 밖). 그 역할 지원을 요청하면 지속시간(즉발 ASW·STK는 8초) 동안 기함 옆 전방 슬롯(`FormationOffset`)으로 나왔다가 돌아간다 |
| 전투 불능 | 선체 0이면 가라앉지 않고 폭발과 함께 기울어 기함 뒤 60m로 이탈(표적 해제·콜라이더 끔), 그 지원 `SupportState.Offline`(요청 거부·작동 중이던 효과 즉시 종료), C2 줄 "호위함 전투 불능 — 복귀 N초", 보상 없음. `RecoverSeconds`(45초) 뒤 선체 50%로 복귀 |
| 정비(Refit 진입) | 모든 호위함 선체 100%, 전투 불능이던 호위함 즉시 복귀(`RepairAll`) |
| 개량 | 선체 최대치가 늘어난 만큼 선체도 늘어남, 모델을 그 단계 Codex 모델로 교체 |
| C2 패널 | 호위함이 있는 역할 줄 상태 오른쪽에 "선체 N%"(50% 아래 황색, 25% 아래 적색) |
| 자체 방어(2026-10-01) | `EscortDefense`(호위함 루트에 붙음, 전투 불능이면 쉼): 모든 호위함 소형 함포(24m, 2.5×(1+0.2×단계) 피해/0.5초, 수상함·항공기, 섬 너머 못 쏨) · CAP 함대공 요격(42m 안 적 미사일 중 기함에 가장 가까운 것 격추, 재장전 4.0−0.6×단계초) · EW 근접 교란(36m 안 교란 안 된 미사일 1발, 1.6+0.3×단계초, 재장전 6−단계초) |

**Codex 모델·이펙트 적용**: `ArtSource/TaskForceTacticalDraft`의 FBX를 `Assets/_Game/Art/Models/TaskForce`(+`/Effects`)로 복사하고 `Naval/Art/Build Task Force Escort Prefabs`(`NavalEscortArtBuilder`)로 임포트 설정·재질 연결(기존 팔레트: Deck Grey·Naval Blue Grey·Gunmetal·Radar/Sensor Glass·Warning Yellow, Naval Superstructure→Naval cool grey, Deck Marking White→Medical White, 역할색·FX는 `Materials/TaskForce`에 새로)·프리팹(`Resources/TaskForce/Escorts/ESC_역할_T단계`, `Resources/TaskForce/Effects/FX_코드_Tactical`)을 만든다. 모델은 모든 단계가 명목 3×9m이므로 런타임 축척(0.60/0.76/0.92/1.08) 하나로만 키운다. 프리팹이 없으면 예전 회색박스로 돌아간다. 지원 발원점은 모델의 `SupportOrigin` 소켓. 전술 리그는 `TaskForceWorldFeedback`이 발동 때 펼친다(0.2→1 크기, 밝기 1→0, `SweepPivot`·`BearingLinePivot` Y축 회전): AEW 기함 반경 28m, CAP 18m·ASW 14m·EW 20m는 담당 호위함, STK는 명중 지점 8m.

**검증**: `-escortOnly`(모델·등록·후방 대기 · 지원 요청 전방 전개/복귀 · 표적 선택 · 공격자 2척 제한 · 미사일정은 기함만 · 적 포탄 피해 · 전투 불능 표적/판정 해제·지원 중단·이탈·보상 없음·재조준 · 선체 절반 복귀 · 정비 수리 · 개량 모델/축척/선체 · 전술 리그), 캡처 `escort_*.png`.

### 편대 개편 결정(2026-10-01 결정 → 2026-10-02 구현 완료, 0장 참고)

사용자 결정: ① **전투단 지원 편성(출항 편성 화면·CP)을 없애고** 인게임 **편대 배치 카드**(고속정 1척 합류)와 **편대 강화 카드**로 대체한다. ② **호위함 역할(방공·대잠·전자전·미사일)은 편대 강화 카드로 고른다**(Codex 모델 CAP/ASW/EW/STK × T0~T3 재활용: 강화 단계 = 모델 단계). ③ **지원 스킬(쿨다운 버튼)은 지운다** — 효과는 호위함 **자율 능력**으로 대체(CAP 요격·EW 교란은 `EscortDefense`에 이미 있음, ASW 자동 대잠 타격·STK 주기적 대함 타격 추가, AEW는 "탐지 거리" 성장 카드로 흡수). ④ 호위함의 "후방 대기 / 지원 요청 시 전방 전개"는 스킬을 전제로 한 규칙이라, 스킬 삭제 때 "측후방 자율 교전"으로 바꾼다. 정리 대상: `TaskForcePanelUI`(출항 편성·C2 패널), `TaskForceController`의 지원·CP, `TaskForceVerification`(Codex 테스트 수정 필요).

### 그 밖의 현재 한계

- 섬/장애물 회피가 없어 해안 가까이에서 지형을 관통해 보일 수 있다(적 탄은 섬에 막히지만 호위함 이동은 섬을 무시).
- 호위함은 스스로 사격하지 않는다(지원 능력으로만 싸운다). 레이더 화면에 아군 호위함 표시는 없다.
- 역할·업그레이드 수치가 `TaskForceEscortFormation`과 `TaskForceController` 코드에 고정되어 있다.
- 개량 가능 시 한 레벨 걸러 세 번째 장비 카드를 대체한다(최대 레벨 역할은 `GetUpgradeOffer`가 제외). 희귀도·가중치는 아직 없다.
- 지원 및 호위함 행동에 전용 음향과 무전 콜아웃이 없다.
- AWACS는 오프맵 효과만 있고 실제 항공기 모델은 없다.
- 개량 단계의 세이브/로드 및 메타 진행은 없다.
- 낮은 해상도나 넓지 않은 화면에서 우측 상단 알림이 다른 콘솔과 겹칠 가능성이 있으므로 별도 화면비 검증이 필요하다.
- 기존 `HUDView`의 `rowHeight`, `flankText`, `flankBar` 미사용 경고는 이번 기능과 직접 관련 없는 기존 경고다.

## 9. 권장 다음 구현 순서

### 작업 A — 진영/팀 기반 표적 시스템 (완료 2026-09-30, 위 "진영 규칙" 참고)

목표: 플레이어, 아군 호위함, 적을 명시적으로 구분한다.

범위:

- `CombatFaction` 또는 동등한 최소 타입 추가
- `ITargetable`에 진영 정보 제공
- `TargetRegistry`가 적대 대상만 질의할 수 있게 확장
- `TargetAllocator`, 플레이어 무기, 적 AI, Missile, Projectile, 충돌 피해의 표적/소유자 판정 감사
- 기존 적 및 플레이어 프리팹에 진영값 연결

완료 기준:

- 플레이어 무기는 아군 호위함을 선택하지 않는다.
- 적은 플레이어와 아군 호위함을 적대 대상으로 인식할 수 있다.
- 아군 손실은 적 처치/XP/보상 이벤트를 발생시키지 않는다.
- 기존 단함 전투가 회귀하지 않는다.

### 작업 B — 실제 호위함 생존성과 적 표적 선택 (완료 2026-09-30, 8장 "호위함 생존성" 참고)

선행: 작업 A 완료.

범위:

- 호위함 HP, 피해, 침몰/퇴각 수명주기
- 위협도·거리 기반 적 표적 선택
- 호위함 손실 시 편대 슬롯 재정렬
- C2 패널에 상태/손상 표시
- 런 종료 및 씬 전환 시 등록 해제 확인

완료 기준:

- 적이 조건에 따라 기함과 호위함 사이에서 표적을 선택한다.
- 호위함 격침이 게임을 즉시 종료하지 않고 해당 지원 전력 상태에 일관되게 반영된다.
- 파괴된 호위함이 레지스트리와 UI에 남지 않는다.

### 작업 C — 호위함 정의와 업그레이드의 데이터화

목표: 역할별 수치와 외형을 코드 수정 없이 조정할 수 있게 한다.

범위:

- `EscortDefinition` ScriptableObject 제안: 역할, CP 연동, 프리팹, 기본 크기, 단계별 모델/파츠, 단계별 효과, 카드 문구/아이콘
- 하드코딩된 성장 수치와 색상/명칭 이동
- `RefitUI` 카드 선택 규칙에 가중치·최대 레벨·대체 실패 처리를 명시

완료 기준:

- 새 호위함 역할 또는 L1~L3 수치를 ScriptableObject만으로 조정 가능
- 최대 레벨 역할은 제안 후보에서 제외
- 데이터 누락 시 명확한 경고와 안전한 폴백이 있다.

### 작업 D — 정식 모델·편대 항법·전투정보실 UI 마감

범위:

- 절차적 회색박스를 정식 프리팹으로 교체하되 L0는 적 고속정급 크기 유지
- L1~L3에서 실루엣이 명확히 달라지도록 센서/마스트/무장/선체 변화
- 섬 회피 또는 최소한의 슬롯 재배치/클램프
- 16:9, 16:10, 울트라와이드, 낮은 해상도에서 C2 패널·토스트·Refit 카드 수동 QA
- CIC 색상 체계와 아이콘, 무전/음향 추가

완료 기준:

- 업그레이드 전후 실루엣을 줌아웃 상태에서도 구분할 수 있다.
- 호위함이 섬을 장시간 관통하거나 화면 밖으로 이탈하지 않는다.
- 지원 발동 중에도 HP, 경고, 레이더, 표적 정보를 가리지 않는다.

### 작업 E — 밸런스와 런 진행

범위:

- 전체 런을 통한 CP, 쿨다운, 카드 등장 빈도, 호위함 성장 속도 측정
- 전투단 편성이 단함 빌드를 대체하지 않고 보완하는지 확인
- 필요 시 런 저장/복구 범위 정의

완료 기준:

- 최소 3개 서로 다른 유효 편성 빌드가 존재한다.
- 특정 지원 하나가 모든 상황의 정답이 되지 않는다.
- 호위함 개량 카드와 기함 장비 카드 사이에 실제 선택 비용이 있다.

## 10. 다음 에이전트가 바로 시작할 수 있는 작업 단위

가장 안전한 다음 작업은 아래 순서다.

1~4(진영 설계·구현·테스트)는 2026-09-30 완료 — 8장 "진영 규칙"과 검증 `-factionOnly`.
5. 작업 B(호위함 HP/피격/격침·적 표적 선택)와 Codex 호위함 모델·전술 이펙트 적용은 2026-09-30 완료 — 8장, 검증 `-escortOnly`.
6. 다음: 작업 C — 호위함 정의·개량 수치의 데이터화(`EscortDefinition`), 또는 작업 D의 섬 회피.

각 작업은 가능하면 한 번에 한 축만 바꾸고, `Prototype_Main.unity` 전체를 재생성하거나 기존 씬 오브젝트를 대량 교체하지 않는다.

## 11. 회귀시키면 안 되는 항목

- 직접 조종 대상은 기함 한 척이어야 한다.
- 편성은 출항 전에만 바꿀 수 있고 확정 후 잠겨야 한다.
- 기본 편성은 5/6 CP이며 CP 초과가 허용되면 안 된다.
- 지원 활성 상태는 C2 패널에서 계속 보여야 하지만, 대형 중앙 팝업이 전장을 가리면 안 된다.
- L0 호위함은 적 고속정급의 작은 크기를 유지해야 한다.
- 호위함 개량은 레벨 종료 카드 선택을 통해 이루어져야 한다.
- AWACS를 억지로 수상 호위함으로 표현하지 않는다.
- 기존 무장 배치, 업그레이드, 레이더, 표적 시스템을 불필요하게 재작성하지 않는다.

## 12. 작업 시작 전 빠른 체크리스트

- `ARCHITECTURE.md`의 전투단 섹션과 최신 변경 이력을 먼저 읽는다.
- 작업 중인 Unity 에디터가 있다면 사용자의 세션을 강제 종료하지 않는다.
- 변경 전 관련 파일과 자동 테스트를 함께 읽는다.
- 코드 변경 뒤 `TaskForceVerification.RunBatch`를 실행한다.
- 배치 성공 뒤 실제 1920×1080 플레이 화면에서 출항 편성, C2 패널, 지원 알림, 호위함 크기, 개량 카드까지 수동 확인한다.
- 새 한계나 결정이 생기면 이 문서와 `ARCHITECTURE.md` 변경 이력을 함께 갱신한다.

