# 낮과 밤 모델 팩

[2026-11-19 Claude 모델 요청서](../../Docs/CODEX_MODEL_REQUESTS_DAYNIGHT_2026-11-19.md)의 A–C 항목을 제작했다. `Models/`에 개별 FBX 8종, `DayNight.blend`에 편집용 컬렉션 8개가 있다. Blender 원본은 반잠수 자폭정부터 열리며, 아웃라이너에서 나머지 컬렉션을 켤 수 있다.

| 모델 | 크기 | 삼각형 |
| --- | --- | ---: |
| `ENE_SemiSubSuicide` 반잠수 자폭정 | 7.5 × 2.0m, 건현 0.35m, 조종실 0.7m | 588 |
| `ENE_SemiSubTorpedo` 반잠수 어뢰정 | 11 × 2.8m, 건현 0.45m, 마스트 1.0m | 792 |
| `ENV_ChannelBuoy_Red` 적색 항로 부표 | 지름 1.2m, 통합 높이 2.6m | 196 |
| `ENV_ChannelBuoy_Green` 녹색 항로 부표 | 지름 1.2m, 통합 높이 2.6m | 196 |
| `ENV_LightBuoy_Yellow` 모래톱 등부표 | 높이 약 2.4m | 188 |
| `ENV_DistressBeacon` 조난 신호 부표 | 높이 약 1.2m | 116 |
| `PRJ_Sonobuoy` 소노부이 | 지름 0.45m, 높이 0.9m, 수면 위 0.6m | 116 |
| `FX_ParachuteFlare` 낙하산 조명탄 | 낙하산 지름 1.4m, 줄 길이 1.2m, 불꽃통 0.3m | 160 |

`Preview/`의 `*_Oblique.png`는 가까이서 본 뷰, `*_Comparison.png`는 16m 기함 실루엣과 같은 축척의 상부 뷰다. [반잠수정 수면선 비교](Preview/SemiSub_SurfaceBoat_Comparison.png)에는 프로젝트의 기존 자폭 보트와 경어뢰정 FBX를 해당 프리팹의 배율 1 및 0.85로 배치했다. 이미지의 수면과 조명은 Blender에서 검토용으로 배치했다.

## 클로드 연결 메모

- 기존 모델과 동일한 FBX 설정을 썼다. Unity 임포트 루트 배율은 100이며, 바깥 `ModelPivot`에 Unity Y 180° 회전을 적용하면 앞 +Z, 위 +Y, 우현 +X가 된다. FBX 루트 배율을 포함한 실제 크기는 이미 미터 단위다.
- 반잠수정과 부표의 원점은 수면선 중앙이다. 반잠수정 FBX는 지정된 게임 크기로 제작했다. 교체할 때 `LowProfileHull`의 임시 Y축 압축을 끄고, 기존 경어뢰정의 임시 모델 배율 0.85가 전용 모델에 적용되는지도 확인한다. 반잠수정 선체에는 `Enemy Dark`를 사용했다.
- 반잠수 어뢰정의 `TorpedoLaunchPoint_Port` / `_Starboard`는 각 `TorpedoTube_*` 아래에 있다. 기존 방향 보정이 적용된 상태에서 발사점 `Transform.forward`는 전방 +Z, 두 배의 `Exhaust`는 후방 −Z다.
- 항로 부표, 노란 등부표, 조난 신호 부표, 소노부이에는 `LampPoint`가 있다. 불꽃통의 바닥과 조명탄 원점은 모두 `FlarePoint`에 맞췄으며 `Canopy`와 `Canister`는 각각 별도 메시다. 등불, 점멸, 불빛은 게임 쪽 연출에서 연결한다.
- `manifest.json`에 모델별 삼각형 수, 재질 이름, Blender 기준 바운드, 부품의 부모와 방향을 기록했다. 재질 이름은 프로젝트에 있는 팔레트에 맞췄다. 야간 밝기, 재질 연결, 조명탄과 탐조등 아래 가독성은 실제 프리팹 연결 후 확인한다.

## 재생성과 검증

저장소의 Blender 5.2에서 `blender -b --python-exit-code 3 --python ArtSource/DayNight_v1/build.py`를 실행한다. 메시 도구는 `ArtSource/Stage4Ekranoplan_v1/geometry.py`를 사용하고, 비교 렌더는 게임의 기존 보트 FBX 두 종을 읽는다. `validate.py`는 Blender FBX 왕복 검사 결과를 `BlenderValidation.txt`에 쓴다.

별도 Unity 6000.3.23f1 프로젝트에 `Models/*.fbx`를 `Assets/Models`로 복사한다. `blender -b --python-exit-code 3 --python ArtSource/DayNight_v1/validate.py -- "<임시 Unity 프로젝트 경로>"`로 `expected.tsv`를 만든다. `ArtSource/Stage4Ekranoplan_v1/VerifyStage4Models.cs`를 임시 프로젝트 `Assets/Editor`에 복사해 `-batchmode -nographics -quit -executeMethod VerifyStage4Models.Run`으로 임포트를 검사한다. 결과는 `UnityValidation.txt`에 있다.

