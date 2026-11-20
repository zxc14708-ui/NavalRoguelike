# 군항 운영 팩 v1

Unity 6000.3.23f1의 시작 화면 군항용 추가 프리팹과 연출 코드입니다. 현재 군항 프리팹의 복사본으로 제작·검수했습니다. 원본 게임 프로젝트는 수정하지 않았습니다.

## 제작 범위

| 구역 | 추가 요소 |
| --- | --- |
| 함정 정비창 | 안벽과 정비창을 잇는 두 굽이 진입로, 램프와 회차장, 난간, 작업장 캐노피와 셔터 |
| 화물 부두와 야적장 | 서쪽 부두에서 야적장까지 이어지는 차량 동선, 야적장 게이트, 화물 사무실 캐노피와 적재 구역 |
| 비행장 | 서비스 도로와 보안초소, 헬기 착륙장과 윈드삭, 지원 차량 주행과 헬기 이착륙 |
| 부두 운영 | 크레인으로 컨테이너를 트레일러에 싣고 돌려받는 작업, 예인정의 접근·예인줄 연결·출항·입항·재계류, 두 선박의 항적 |

170초 주기에서 크레인이 먼저 하역합니다. 26초에 예인정이 선수에 줄을 연결하고, 화물선이 부두 바깥 수역으로 이동합니다. 복귀 시 예인정은 화물선 측면에서 입항을 돕습니다. 화물선이 136초에 다시 계류하면 트레일러가 가져온 컨테이너를 싣습니다.

## 원본에 병합할 때

1. 작업 중인 원본 파일의 병합 시점에 Unity에서 `HarborOperations_v1.unitypackage`를 가져옵니다. 추가되는 경로는 `Assets/HarborOperations`입니다. 기존 프로젝트의 `MAT_base_*` 팔레트와 군항 리소스 두 개를 참조합니다.
2. `NavalBaseMenu`에 아래 필드를 추가합니다.

   ```csharp
   private HarborOperationsPack.HarborOperations _harborOperations;
   ```

3. `Initialize`의 `HarborFacilityArt.Attach(_island)` 직후에 실행합니다. 카메라 레이어를 설정하기 전에 추가 프리팹도 같은 섬에 들어갑니다.

   ```csharp
   _harborOperations = HarborOperationsPack.HarborOperations.Attach(_island);
   ```

4. `LateUpdate`에서 배경 정지 설정을 반영한 기존 `dt`로 갱신합니다.

   ```csharp
   _harborOperations?.Tick(dt);
   ```

크레인과 선박의 일부 정적 디테일은 실행 중 생성된 메시 복사본에서 교체합니다. 기존 프리팹과 원본 메시를 직접 편집할 필요가 없습니다. 별도 폴더의 `Source`에 제작 및 검수 코드가 있습니다. 모델을 재생성하는 메뉴는 `Tools > Harbor Operations > Build Overlay`입니다.

## 검수 범위

`UnityValidation.txt`에는 복사한 군항 프리팹에서 확인한 검수 결과가 있습니다. 선박 경로는 지정된 외곽 범위로 0.1초마다 검사했습니다. 전체 타이틀 UI에서의 최종 병합, 기존 도로 차량과의 동시 운행, 실제 메뉴 카메라와 화질 설정은 병합 후 확인할 항목입니다.
