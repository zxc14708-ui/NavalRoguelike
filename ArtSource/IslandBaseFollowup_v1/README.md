# 기지 섬·해안 후속 모델

[프로젝트 요청 후보](../../Docs/NEXT_TASKS_2026-11-14.md) 7개 항목을 모델 16종으로 제작했다. `Models/`에는 개별 FBX, `IslandBaseFollowup.blend`에는 모델별 편집 컬렉션, `manifest.json`에는 부품 계층·위치·재질과 바운드가 있다.

| 용도 | FBX | 배치·연결 기준 |
|---|---|---|
| 기지 섬 부두 | `ENV_BaseBreakwater`, `ENV_BaseMooringPier` | 돌제 갑판 22×5.8m, 계류 부두 갑판 19×9m. 원점 높이는 수면, `ShoreConnection`은 육지 쪽 끝 기준. `MooringPoint_Port`, `MooringPoint_Starboard`는 계류 위치 표시. |
| 파괴된 시설 | `MIL_Ruins_Battery`, `MIL_Ruins_Radar`, `MIL_Ruins_Outpost` | 각각 기존 시설 바닥 중심과 발자국을 기준으로 교체. `SmokePoint`에서 연기 연결. |
| 기지 섬 지형 | `MIL_Island_{S,M,L}_{B,C}` | 규모마다 추가 변형 2개. 기존 A형과 함께 3개씩 사용 가능. 원본 평평한 부지 좌표, 수면 높이 −0.9m와 재질 분할을 유지했다. |
| 헬기 착륙장 | `MIL_Helipad` | 13×13m. 바닥 중심으로 배치하고 갑판 위 `LandingSpot`에 착륙 위치 연결. |
| 탐조등 | `MIL_Searchlight` | `SearchlightYaw → SearchlightPitch → BeamOrigin`. 빛은 렌즈 앞 `BeamOrigin`에 연결. |
| 해안 미사일 포대 | `MIL_MissileBattery_2` | 기존 `TurretPivot`, `ElevationPivot`, `Muzzle`, `LaunchPoint_01/02`의 계층과 위치 유지. `LauncherHatchPivot_01/02` 아래에 덮개 메시를 두고, 뒤쪽에 `BackblastPoint_01/02`를 추가했다. |
| 무인도 소품 | `ENV_DriftingSupplyCrate`, `NAT_SOSMarker` | 상자는 원점 높이 수면, `PickupPoint`는 뚜껑 위. SOS는 지면에 얹는 가로 약 7m의 표식. |

## 연결 메모

- FBX는 미터 단위로 내보냈다. 새 방향성 소품은 프로젝트 4스테이지 모델과 같은 Blender +Y 전방, +Z 상향 규격이다. Unity에서 모델을 직접 배치할 때 전방 방향이 중요하면 4스테이지 에셋과 같은 180° Y 보정을 적용한다. 부두를 `PlaceAnchored` 방식으로 붙일 때는 `ShoreConnection` 위치로 회전을 계산하므로 별도 180° 보정 없이 계류 위치를 확인한다.
- 기존 섬과 포대에서 파생된 모델은 원본 FBX 방향을 맞췄다. 섬 모델을 게임에 연결할 때 `Read/Write Enabled`를 켜고 기존 섬처럼 해안선과 충돌체를 해당 변형 메시에서 계산한다. 같은 변형으로 해안 거품, 항해 경계, 부두 위치를 확인한다.
- 포대 FBX 기본 자세는 덮개 닫힘이다. 시안에서 덮개는 Blender `LauncherHatchPivot_*`의 X축으로 110° 열었다. Unity에서는 가져온 경첩 방향 기준으로 열림 자세를 연결한다. 발사 타이밍, 후염 VFX, 탐조등 광원, 공중기 출격, 잔해 치환과 상자 보급은 게임 쪽 연결 항목이다.
- 이번 팩은 `ArtSource`에 저장한 모델 납품이다. Unity 게임 프리팹 연결은 후속 단계다.

## 4스테이지 확인

[4스테이지 모델 요청서](../../Docs/CODEX_MODEL_REQUESTS_STAGE4_2026-11-16.md)의 룬급 보스, 적 6종, 관련 소품·탄 5종은 [Stage4Ekranoplan_v1](../Stage4Ekranoplan_v1/README.md)에 이미 제작돼 있다. 프로젝트 `Assets`의 FBX 12종이 원본과 SHA-256까지 일치했고, 원본 팩 Blender 재임포트 검사도 12종 통과했다.

새 팩은 Blender 5.2에서 `blender -b --python build.py`로 재생성하고 `blender -b --python-exit-code 1 --python validate.py`로 검사한다. Blender 재임포트로 16종의 계층·재질·삼각형 수와 섬 평지 보존, 기존 포대 소켓의 변환, 열린 덮개의 개구부를 확인했다.

별도 Unity 프로젝트의 검증은 라이선스 클라이언트 연결 시간 초과로 결과를 얻지 못했다.
