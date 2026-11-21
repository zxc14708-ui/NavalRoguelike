# 룬급 투사체 모델 D-4·D-5

4스테이지 룬급에 사용할 로우폴리 게임 모델 두 종입니다. 동체와 꼬리날개를 분리했고, 프로젝트의 기존 재질 이름을 사용했습니다.

| 파일 | 길이 · 동체 지름 · 꼬리 폭 | 삼각형 | 빈 노드 |
| --- | --- | --- | --- |
| `Models/PRJ_LunBomb.fbx` | 1.8m · 0.38m · 0.60m | 172 | 기수의 `Fuze`, 게임 +Z 방향 |
| `Models/PRJ_LunRocket.fbx` | 1.6m · 0.16m · 0.40m | 124 | 꼬리의 `Exhaust`, 게임 −Z 방향 |

폭탄은 `Enemy Dark`, `Warning Yellow`, `Enemy hull grey`를 사용합니다. 로켓은 `Enemy hull grey`, `Emergency Red`, `Enemy Dark`를 사용합니다. 폭탄의 노란 띠는 동체 면에 직접 배정한 재질입니다.

## 작업물과 검수

- `Stage4LunOrdnance.blend`의 모델 컬렉션 두 개를 개별 편집할 수 있습니다.
- `Preview/`에 두 모델을 같은 축척으로 비교한 화면, 각각의 사선 뷰, 기함 및 항공전함과의 크기 비교가 있습니다.
- `manifest.json`에는 메시 수치, 재질, 계층과 소켓 방향이 들어 있습니다.
- Blender 5.2에서 `blender -b --python-exit-code 1 --python build_lun_ordnance.py`로 다시 생성합니다. 상위 `Stage4Ekranoplan_v1`의 메시 및 내보내기 코드를 사용합니다.
- `verify_lun_ordnance.py`는 FBX 왕복 검사입니다. `Review/VerifyLunOrdnance.cs`는 복제 Unity 프로젝트의 임포트 검사이며 `BlenderValidation.txt`와 `UnityValidation.txt`에 결과를 기록했습니다.

## 프리팹 연결

기존 4스테이지 FBX와 같은 가져오기 규칙을 사용합니다. 투사체 루트 아래 기존 `ModelPivot`과 같은 Y 180° 보정 피벗에 모델을 배치하면 게임에서 기수가 +Z를 향합니다. 가져온 모델 루트의 배율 100은 기존 FBX 단위 변환입니다. 다시 배율을 나누면 게임에 표시되는 크기가 달라집니다.

런타임 연결은 요청서 D-4·D-5의 인수인계대로 기존 투사체 동작에 두 모델을 각각 붙이는 단계입니다.
