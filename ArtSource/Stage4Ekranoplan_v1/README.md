# 4스테이지 「내해의 괴물」 모델 납품

2026-11-16. [Claude의 모델 사양서](../../Docs/CODEX_MODEL_REQUESTS_STAGE4_2026-11-16.md) A–D 항목 기준으로 만든 저폴리 FBX 12종이다. `Models/`에는 각 모델이 따로 들어 있다. `Stage4Ekranoplan.blend`에서는 컬렉션마다 한 모델을 편집할 수 있다. 원본을 처음 열면 룬급 컬렉션이 표시되며 나머지는 아웃라이너의 컬렉션 체크박스로 켠다.

| 우선순위 | 모델 | 삼각형 |
| --- | --- | ---: |
| A | `ENE_Boss4_Lun` 룬급 보스 | 5,828 |
| B | `ENE_LightWig` 경위그선 | 550 |
| B | `ENV_OilPlatform_L` 대형 유전 플랫폼 | 1,316 |
| B | `ENV_OilPlatform_S` 소형 유전 플랫폼 | 672 |
| C | `ENE_AssaultWig` 강습 위그선 | 1,562 |
| C | `ENE_Hovercraft` 공기부양 상륙정 | 1,528 |
| C | `ENE_Hydrofoil` 수중익 미사일정 | 1,158 |
| C | `ENE_MissileCorvette` 엘리트 미사일 초계함 | 1,598 |
| C | `ENE_PlatformBattery` 플랫폼 포대 | 412 |
| D | `ENV_RearmBarge` 보급 바지선 | 592 |
| D | `MIS_Moskit` 모스킷 게임 발사체 | 196 |
| D, 선택 | `ENV_Sandbar_Marker` 모래톱 표지 | 112 |

`Preview/`에는 모델당 비스듬한 뷰와 같은 축척의 상부 비교 뷰가 있다. 비교 뷰의 기함 16m·항공전함 32m는 크기 비교용 실루엣이다. 위그선의 붉은 포인트와 적 수상함의 적갈색 갑판은 미리보기에도 반영했다. 게임의 조명과 틴트는 통합 후 확인한다.

## Claude 연결 메모

- 미터 단위와 수면선 원점 기준이다. 함정/위그선 원점을 게임 수면인 월드 Y −0.9m에 배치한다. `ENV_OilPlatform_*` 갑판 상면은 모델 Y 9m 및 6m다. 플랫폼 전체 바운드에는 플레어 붐 돌출과 수면 아래 기둥이 포함된다. 포대 원점은 3×3m 베이스 바닥 중앙이다.
- Blender +Y 앞, +Z 위. FBX는 기존 `FBX_SCALE_NONE` 규격이며 Unity 임포트 루트의 배율은 100이다. 기존처럼 **바깥 `ModelPivot`에 Unity Y 180°를 한 번** 적용하면 앞 +Z, 위 +Y, 우현 +X가 된다. 발사·배기 등 방향 소켓은 이 상태에서 로컬 +Z(`Transform.forward`)를 사용한다. 물리 크기는 루트 배율 100이 포함된 상태에서 이미 미터 단위다.
- 룬급은 엔진 8개와 발사관 3쌍의 메시를 분리했다. 발사 덮개 6개의 원점은 위쪽 경첩에 있다. 기준 자세에서 게임 기체 오른쪽 축 둘레로 −70°를 적용하면 위로 들린다. 강습 위그선 `NoseDoorPivot`은 기체 위축 둘레로 +70°를 적용하면 우현으로 열린다. 각도는 기체 기준이며 FBX의 내부 노드 로컬 축으로 가정하면 안 된다.
- 수중익 `Foil_Bow`·`Foil_Stern`은 루트 직속이다. `HullPivot`을 게임 좌표에서 1.2m 올릴 때 그 아래의 `SlidingStruts_*`가 올라온다. 공기부양정의 `Fan_*`는 고정 덕트이고 `FanBlade_*`만 회전시킨다. 위그선 프로펠러 메시의 회전축은 기체의 앞뒤 축이다.
- `manifest.json`에는 정확한 노드 이름과 부모, 부품 원점, 방향, 재질명, Blender 바운드를 기록했다. 프로젝트 안의 재질 13종과 이름이 일치한다. 실제 URP 재질 바인딩과 적 틴트는 프리팹 연결 뒤 확인한다.
- 프리팹 연결 후 수면선과 이동, 충돌체, 각 발사점으로 발사되는 위치, 파괴 연출, 웨이브를 확인한다. 이 팩의 범위는 시각 모델과 손잡이 역할의 노드다.

## 재생성과 검증

Blender 5.2에서 `blender -b --python build.py`를 실행하면 FBX, BLEND, 미리보기, manifest를 다시 만든다. `blender -b --python-exit-code 3 --python validate.py`는 FBX 왕복 검사와 보스의 상부 적색 면적을 검증한다. 여기에 `-- <임시 Unity 프로젝트 경로>`를 추가하면 Unity 검증용 `expected.tsv`를 그 경로에 쓴다.

`VerifyStage4Models.cs`를 임시 프로젝트 `Assets/Editor`에 두고 `Models/*.fbx`를 임시 프로젝트 `Assets/Models`에 복사한다. `-batchmode -nographics -quit -executeMethod VerifyStage4Models.Run`으로 검사한다. 결과는 `BlenderValidation.txt`와 `UnityValidation.txt`에서 확인할 수 있다. 게임 플레이와 URP는 검증 범위 밖이다.

모델과 리뷰 이미지는 자체 제작했다. 외부 사진이나 메시를 팩에 포함하지 않았다. 기체 실루엣은 데르벤트의 룬급과 박물관의 A-90 사진을 참고했다. [룬급 사진](https://commons.wikimedia.org/wiki/File:%D0%AD%D0%BA%D1%80%D0%B0%D0%BD%D0%BE%D0%BF%D0%BB%D0%B0%D0%BD_%D0%9B%D1%83%D0%BD%D1%8C%2C_%D0%94%D0%B5%D1%80%D0%B1%D0%B5%D0%BD%D1%82.jpg) · [A-90 사진](https://commons.wikimedia.org/wiki/File:A-90_Orlyonok_in_museum_2016-05-08_Pic_1.JPG)


## 2026-11-20 수정(Claude)

- `ENV_OilPlatform_L` · `ENV_OilPlatform_S`: `Deck`의 금속(Gunmetal) 상자 윗면과 콘크리트 윗면이 같은 높이(9.0m · 6.0m)라 게임에서 갑판이 깜박였다(z-fighting). 금속 윗면만 3cm 낮춰 다시 내보냈다(같은 FBX 설정, 다른 부품 · 소켓 · 재질은 그대로 — Blender 재임포트 비교로 확인). `structures.py`도 같게 고쳤다. 그래서 `Models/`의 두 파일은 처음 납품본과 SHA가 다르다.
