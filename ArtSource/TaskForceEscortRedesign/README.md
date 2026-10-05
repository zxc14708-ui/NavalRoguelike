# 편대 함선 재디자인 — 검토용 16척

새 모델만 제작했습니다. 기존 Unity `Assets`, Resources 프리팹, 게임 코드, 스킬 효과는 변경하지 않았습니다.
현재 게임에 적용된 기존 편대 모델은 그대로 남아 있습니다.

## 디자인 방향

| 역할 | 기존 색상 RGB (변경 없음) | 새 외형의 중심 |
| --- | --- | --- |
| CAP 방공 호위 | 0.35, 1.00, 0.58 | 방공 탐색레이더·AESA 통합 마스트, VLS, 함미 SAM, 최상위 CIWS |
| ASW 대잠 호위 | 0.22, 0.68, 1.00 | 함수 소나, 양현 경어뢰관, 후미 헬기 갑판·격납고, 예인소나 |
| EW 전자전 호위 | 0.83, 0.37, 1.00 | 전자장비실, 벽면 재머 배열, 연속 ESM 마스트, 방탐·기만체 |
| STK 대함 타격 | 1.00, 0.52, 0.16 | 선수 주포·함미 화기, 낮은 함교와 연돌, 사통센서, 소수 대함 발사관 |

회색 군함 팔레트에 기존 역할색을 식별띠·장비 표식으로 유지했습니다. 텍스처 없는 로우폴리 게임용 해석이며 실물의 축척 복제품이나 실제 무장 구성의 정확한 재현은 아닙니다.

### 대함 타격함 변화

- 기존 T3의 경사 발사관 20개 + VLS 8셀 형태에서 **대함 발사관 최대 4개**, 함포와 사통장비가 주역인 디자인으로 변경했습니다.
- T0: 참수리 PKM의 작은 함체·쌍열 화기·지지된 마스트에서 영감을 얻은 고속 전투정.
- T1: PKMR의 밀폐 함교·선수 주포·후방 선회식 유도로켓 발사기 구성을 게임 비례로 해석. 유도로켓은 별도의 8셀 박스 장비입니다.
- T2: PCC 초계함의 선수·함미 함포 배치와 컴팩트한 경사 대함 발사대를 강조.
- T3: T2 화력 배치를 유지하면서 교량 날개 원격화기, 광학 사통장비·통신장비·탄약함을 개량. 추가 예비 발사대와 대함 VLS는 넣지 않았습니다.

다른 역할도 T0 기본 고속정 → T1 개량형 → T2 초계함 → T3 호위함의 장비 발전을 표현했습니다. 실제 게임 수치·능력·탄종·발사량을 바꾸지는 않았습니다.

## 파일

- `Models/ESC_<CAP|ASW|EW|STK>_T<0~3>.fbx`: 개별 모델 16개.
- `ESC_*.blend`: 납품 FBX를 재임포트해서 검사한 모델 단독 파일 16개.
- `TaskForceEscortRedesign.blend`: 분리 배치된 16척 편대 검토 원본. 검토를 위해 루트 위치를 옮겼으므로 실제 적용에는 중심 원점의 개별 FBX를 사용하세요.
- `Preview/TaskForce_Redesign_T3.png`: 역할별 최상위 4척 전체 모습.
- `Preview/TaskForce_<역할>_Tiers.png`: 역할별 T0~3 성장 비교. 기존 런타임 배율로 표시합니다.
- `Preview/ESC_*.png`, `_Side.png`: 각 함선 사선·측면 캡처.
- `Preview/STK_T3_AimingRig.png`: 주포·함미 포탑의 방위·고각 변경 검토. 게임 애니메이션이 아닙니다.
- `manifest.json`, `validation.json`: 삼각형 수, 역할색, 단계, 필수 소켓 및 실제 FBX 검사 결과.

## 좌표·규격

Blender +Y가 선수, +Z가 위입니다. 미터 단위, 단일 루트, 중앙 흘수선 원점, 오브젝트 단위 스케일, 메시 회전 적용 및 닫힌 메시/바깥 노멀을 사용했습니다.
FBX 내보내기는 -Z Forward / Y Up / Apply Scalings All Local입니다. 카메라·라이트·콜라이더·본·애니메이션은 FBX에 넣지 않았습니다.

명목 선체는 길이 9m, 폭 CAP/ASW/EW 2.9m, STK 2.78m입니다. 발사관·포신·소나·장비는 기준 함체 밖으로 조금 돌출될 수 있습니다.
모델 자체를 단계마다 확대하지 않았습니다. 기존 런타임 배율 `T0 0.60 / T1 0.76 / T2 0.92 / T3 1.08`을 한 번만 적용해야 합니다.

## 납품 검사

16개 FBX를 Blender에 개별 재임포트해 검증했습니다. 기존 역할색 RGB, 중심 루트, 스케일 1, 메시 회전 0, 닫힌 메시와 바깥 노멀, 선수 표식, 전체 피벗·발사구 이름, 함포 포신/포구 정렬을 확인합니다. 함선당 1,304~2,916 tris이며 3,000 tris 상한을 지킵니다.
검증 결과는 `validation.json`에 기록했습니다. Unity 임포트/플레이 검사는 아직 하지 않았습니다.

## 회전/효과 연결 준비

- 공통: `SupportOrigin`, `RolePlateBow`, `RadarPivot`, `PrimaryGunPivot` → `PrimaryGunElevation` → `PrimaryGunMuzzle`.
- 함포: 받침은 선회 피벗 바깥 고정. 포신과 포구는 고각 피벗을 공유합니다.
- CAP: `AirSearchRadarPivot` 또는 `IntegratedAesaMast`, `SAM_TrainPivot` → `SAM_ElevationPivot` → `SAMLaunchPoint_01~02`. T3 CIWS는 선회/고각/총열 회전부 분리.
- ASW: `HelicopterLaunch`, `SonarOrigin`, `TorpedoMount_P/S`, `TorpedoLaunchPoint_01~02`, 상위형 `SonarTowPoint`.
- EW: `AntennaPivot`, `EWArrayPivot_P/S`, `EWOrigin`, 개량형 기만체 발사구.
- STK: `MissileBankPortPivot`, `MissileBankStarboardPivot`, 각 발사관의 `LaunchPoint_MissileBank...`; T1 `RocketTrainPivot` → `RocketElevationPivot` → `RocketLaunchPoint_01~08`; 다른 단계의 `AftGunPivot`/`AftGunElevation`/`AftGunMuzzle`.

**이것은 움직일 수 있도록 분리한 모델 리그이지, 게임 작동 패치는 아닙니다.** 현재 편대 코드는 개별 포구/피벗 대신 주로 `SupportOrigin`에서 지원 효과를 생성합니다. 실제 포구 발사·조준·레이더 회전은 이후 적용 요청 시 별도 연결해야 합니다.

### 나중에 적용할 때 축 확인

현재 프리팹의 `Model` 자식에는 X+90° 보정이 남아 있으며, 에디터 빌더의 방향 보정과 일치하지 않을 수 있습니다. 이번 작업에서는 변경하지 않았습니다.
교체 시에는 Unity에서 `RolePlateBow`, bounds, 수면 높이를 확인하여 선수 +Z / 위 +Y를 검증하세요. 새 FBX에 기존 보정값을 무조건 더하면 안 됩니다.

## 참고 자료

사진·장비 구성은 형태와 역할 구분의 참고로만 사용했고, 외부 사진이나 텍스처를 모델에 포함하지 않았습니다.

- [서울시 — 참수리 고속정 전시 사진과 조타실·갑판](https://mediahub.seoul.go.kr/archives/2000842): 작은 밀폐 함교와 선수·함미 화기 실루엣 참고.
- [HJ중공업 — PKMR 신형고속정 통합진수식 자료](https://www.hjsc.co.kr/mobile/pcenter/press_release.asp?artistidx=&idx=1967&mode=view&page=3&search=1&searchStr=&serboardsort=1&seritemidx=): 현대 고속정, 유도로켓과 전자장비 역할 참고.
- [한국관광공사 — 포항함체험관](https://korean.visitkorea.or.kr/detail/ms_detail.do?cotid=3d61eef8-6d41-494c-8b36-c1f16a8ec4ba): PCC 초계함 실루엣 참고.
- [Thales — NS50 레이더 공식 자료](https://www.thalesgroup.com/en/print/pdf/node/13416): 소형함의 다기능 탐색레이더 형태 참고.
- [Thales — CAPTAS 가변심도 소나](https://www.thalesgroup.com/en/solutions-catalogue/defence/naval/captas-unrivalled-performance-variable-depth-sonar): 후미 예인소나·함수 소나·항공 대잠 지원의 역할 구분 참고.
- [Saab — 해군 전자전](https://www.saab.com/products/naval/electronic-warfare): ESM/신호 수신·교란·기만체 장비를 중심으로 한 외형 해석.

위 장비와 실제 함정의 성능 수치를 이 모델의 게임 능력치로 옮긴 것은 아닙니다.
