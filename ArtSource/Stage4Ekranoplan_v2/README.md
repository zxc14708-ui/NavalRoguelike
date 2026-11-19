# 4스테이지 적 모델 개정

미사일 초계함, 수중익정, 공기부양정에는 밝은 함교 지붕, 어두운 관측창, 갑판 표식과 난간을 넣어 기존 초계함의 도색과 디테일에 맞췄다. 위그선 3종은 동체와 날개에 점검 패널을 넣었고, 룬급은 엔진 흡입구 테두리와 발사관 색을 분리했다. 플랫폼 포대는 포탑과 장비 상부, 받침 가장자리에 색을 나눴다.

`Models/`에 FBX 7종이 있다. 같은 파일을 `Assets/_Game/Art/Models/Stage4/`에 적용하고 GUID, 부품 계층, 소켓 원점과 방향을 유지했다. `Stage4EnemyRefresh.blend`에는 편집용 컬렉션 7개가 들어 있다. `Preview/`의 그림은 Blender 렌더이며, 게임에서는 기존 URP 팔레트와 적 도색 규칙이 적용된다.

- Blender 5.2에서 `blender -b --python-exit-code 3 --python ArtSource/Stage4Ekranoplan_v2/build.py`로 재생성한다. 형상과 내보내기 코드는 `Stage4Ekranoplan_v1`을 사용한다.
- `validate.py`는 기존 버전과 부품 이름·계층·피벗이 같은지 확인한 뒤 FBX 왕복 검사를 실행한다. 삼각형 수는 `BlenderValidation.txt`에 기록한다. 난간과 갑판 디테일을 추가한 뒤 초계함 1,774, 수중익정 1,260, 플랫폼 포대 524 삼각형으로 1차 아트 예산보다 늘었다.
- Unity에서 재질만 다시 연결하려면 **Naval > Art > Refresh Stage 4 Enemy Materials**를 실행한다.
- 파괴 모션은 게임의 `ShipWreck.Stage4.cs`가 재생한다. 방금 파괴된 모델의 메시를 복사해 잔해를 만들며, 위그선 착수 충돌, 공기부양정 스커트 압축, 수중익 선체 하강, 플랫폼 포대 파편을 각각 처리한다. 미사일 초계함은 기존 함교 파괴와 유폭 모션을 사용한다. 최종 룬급 격파를 포함한 동작은 `-stage4Only`에서 확인한다.

