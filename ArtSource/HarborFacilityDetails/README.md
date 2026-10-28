# 군항 상호작용 시설 외형

연구소·기록실·정비창의 기존 건물에 시각 디테일을 덧붙이는 독립 프리팹이다. 기존 기지 메시가 재질별로 합쳐져 있으므로 원래 씬이나 군항 배경 프리팹을 다시 생성하지 않는다.

| 시설 | 추가한 디테일 | 기준 위치(군항 섬 좌표) |
| --- | --- | --- |
| 해군 기술연구소 | 창틀·현관 표식, 옥상 유리 실험실·패널·관측 안테나 | `(-445, 4, 330)`, 회전 90° |
| 사령부 기록실 | 중앙 유리탑 창틀·기록 표식, 출입 캐노피·옥상 구조물 | `(110, 4, 330)` |
| 함정 정비창 | 육지와 맞닿은 매립 작업장·진입로, 정비동·자재 적치장, 기존 부유 독과 대형 갠트리 크레인 | `(640, 0, 650)` |

좌표는 축소 전 `NavalBaseBuilder`의 섬 좌표(m)이며 `HarborBuildingsUI`의 시설과 일치한다. 기존 카메라·이름표·클릭·연구/기록 기능은 변경하지 않는다.

- 외형 프리팹: `Assets/_Game/Resources/Environment/HarborFacilityDetails.prefab`
- 재질별 병합 메시: `Assets/_Game/Art/HarborFacilityDetails/`
- 시작 화면 연결: `NavalBaseMenu.Initialize` → `HarborFacilityArt.Attach`. 시설 외형 프리팹을 섬의 자식으로 한 번만 붙인 뒤 기존 카메라 레이어와 그림자 정책을 적용한다.
- 생성기: `Assets/_Game/Editor/HarborFacilityDetailsBuilder.cs` (`Naval > Art > Build Interactive Harbor Facility Details`)
- `Preview/`의 `*_before.png`, `*_after.png`: 기존 군항 프리팹과 실제 시설 확대 카메라 좌표로 렌더한 비교. UI 패널은 포함하지 않는다.
- `Preview/workshop_context_after.png`: 육지 연결을 함께 확인할 수 있도록 시야를 넓힌 추가 검토 화면이다. 실제 시설 확대 카메라는 `workshop_after.png`다.
- `validation.txt`: 생성 메시·재질·위치 및 리소스 부착 검사 결과.

외형은 공용 군항 URP 재질을 재사용하며 콜라이더나 상호작용 스크립트를 포함하지 않는다. 빌더는 시설별·재질별 메시로 합쳐 매번 수백 개의 개별 오브젝트를 만들지 않는다. 원본 `NavalBaseMenu.prefab`을 다시 만들어도 외형 프리팹은 시작 화면에서 별도로 붙는다.

정비창은 기존 부유 독과 함선을 재사용하고 그 주변에 매립지와 건물을 추가한 구성이다. 원래 독이 통합 기지 메시에서 완전히 분리된 것은 아니다.

정비창 확대 중에는 빨간 대형 크레인과 노란 소형 문형 크레인의 운반 장치가 좌우로 이동하고 고리가 천천히 오르내린다. `LargeCraneTrolley`와 `SmallCraneTrolley`의 와이어·고리는 별도 메시이며, `HarborCraneMotion`을 `NavalBaseMenu`에서 구동한다. 다른 시설이나 개관 화면에서는 동작하지 않고 배경 정지 설정 및 메뉴 일시정지를 따른다. 원래 독에 합쳐진 지브 크레인은 정적 외형이다.

`prepare_validation.py`는 원본 Unity 프로젝트를 건드리지 않고 실제 군항 프리팹의 의존 에셋과 기존 재질을 복사해 임시 검증 프로젝트를 만든다. 본 게임 UI 전체 또는 연구 보상 로직의 플레이 테스트를 대신하지는 않는다.
