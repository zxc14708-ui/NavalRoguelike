# WWII 시험 전투 아트 세트 v29

제작일: 2026-10-17. 모델 검토용 산출물입니다. Unity `Assets`·프리팹·코드·데이터에는 적용하지 않았습니다.

## 확인한 전용 파일

- `WWII_PATCH_A_DESIGN.md`: WWII 기술 범위, 가상 해군, 무장 역할, 현대 모드 분리.
- `WWII_TRANSITION_PLAN.md`: B 구현 결과와 C~E 미착수. 이번 세트는 현재 B 콘텐츠를 대상으로 합니다.
- `Assets/_Game/Editor/NavalWw2Builder.cs`: 블록 4종, 적 5종, 탄 생성 및 기존 임시 모델의 크기/이름.
- `Assets/_Game/Scripts/WW2/`: 양용포·대공포·현측 어뢰·두 공격기·순양함 동작과 연결할 소켓.

해안포 v27·보급선 v28은 문서 9.7에서 재사용 대상으로 지정되어 이번 세트에 중복 제작하지 않았습니다. C~E의 잠수함·대잠 장비·전함·플레이어 항모는 역할/수치가 아직 확정되지 않아 포함하지 않았습니다.

## 산출물

| FBX | 연결할 콘텐츠 ID | 형태 |
|---|---|---|
| MOD_Ww2Bridge | ww2_mod_bridge_destroyer | 3×1 함교·기관실·연돌, 광학 측거기, 삼각 마스트와 시대형 레이더 |
| MOD_Ww2DualGun | ww2_mod_dual_purpose_gun | 5인치/38 쌍열 양용포 참고, 각진 폐쇄 포탑과 독립 고각축 |
| MOD_Ww2QuadAa | ww2_mod_quad_aa | Bofors 40mm 참고, 4개 평행 포신, 노출 급탄클립과 조준기 |
| MOD_Ww2TorpedoMount | ww2_mod_broadside_torpedo | 21인치 4연장 참고, 회전 받침·관 고정대·후부 캡·설정함 |
| ENE_Ww2FastGunboat | ww2_enemy_fast_gunboat | 낮은 포함, 선수·선미 단장 소구경포. 어뢰 무장 없음 |
| ENE_Ww2Gunboat | ww2_enemy_gunboat | 선수 단장 함포와 선미 기관포, 조타실과 짧은 연돌 |
| ENE_Ww2TorpedoBomber | ww2_enemy_torpedo_bomber | Avenger 계열의 방사형 엔진·폭탄창·후방 기총탑 참고 |
| ENE_Ww2DiveBomber | ww2_enemy_dive_bomber | Ju 87 계열의 역갈매기 날개·고정 각·급강하 브레이크·투하 받침 참고 |
| ENE_Ww2GunneryCruiser | ww2_boss_gunnery_cruiser | 현재 B 코드의 A/B/X 쌍열 3포탑, 2연돌, 광학 사격통제와 양현 부포 |
| TOR_Ww2Player | TOR_Ww2Player.prefab의 시각부 | 직진 대수상 어뢰 |
| TOR_Ww2Air | 뇌격기용 적 어뢰 시각부 | 항공어뢰 |
| BOMB_Ww2 | BOMB_Ww2.prefab의 시각부 | 꼬리날개가 있는 자유낙하 폭탄 |
| PRJ_Ww2DualGun | PRJ_Ww2DualGun.prefab의 시각부 | 양용포탄 |
| PRJ_Ww2QuadAa | PRJ_Ww2QuadAa.prefab의 시각부 | 대공포탄 |

각 모델은 특정 국가 함정의 정밀 복제품이 아니라, 자료에서 확인한 WWII 기술의 구조를 현재 게임 규격으로 압축한 가상 해군 디자인입니다. 실물 사거리·속력은 모델이나 게임 데이터에 덮어쓰지 않았습니다. 뇌격기 색표시는 게임 가독성용이며 실물의 국가별 도장 고증을 표방하지 않습니다.

`Preview/WWII_PlayerDestroyer.png`는 현재 시작 배치(양용포2·4연장 어뢰1·대공포1·3칸 함교)를 조합한 검토용 이미지입니다. 고정 플레이어 선체를 게임용 모델로 추가한 것이 아닙니다.

## 실제 자료와 적용한 특징

1. US Naval History and Heritage Command, **The Most Difficult Antiaircraft Problem Yet Faced By the Fleet**
   https://www.history.navy.mil/browse-by-topic/wars-conflicts-and-operations/world-war-ii/1945/battle-of-okinawa/antiaircraft-problem.html
   쌍열 5인치/38 양용포·4연장 Bofors·대공 사격지휘 장비의 조합. 근접신관 논의는 역사 참고이며 이 게임 대공포 동작에 추가하지 않았습니다.
2. NHHC, **Ordeal of USS Laffey**
   https://www.history.navy.mil/about-us/leadership/director/directors-corner/h-grams/h-gram-045/h-045-1.html
   선수 중첩 포탑과 함미 포탑, 40mm 대공포의 배치 참고. 실제 Laffey의 어뢰관은 5연장이므로 이 게임의 4연장과 다릅니다.
3. San Francisco Maritime National Park Association, **NARA Bureau of Ordnance finding aid**
   https://www.maritime.org/doc/nara/ordnancedata.php
   1939년 21인치 수상발사 어뢰관 Mk XII/Mod4·Mk XIII/Mod4의 4연장 기록. 모델을 특정 Mk의 정밀 복원으로 명명하지 않았습니다.
4. National Naval Aviation Museum, **TBM Avenger**
   https://www.history.navy.mil/content/history/museums/nnam/explore/collections/aircraft/t/tbm-avenger.html
   3인승 방사형 엔진 뇌격기, 내부 어뢰/폭탄창과 후방 방어 기총 참고.
5. National Museum of the US Air Force, **Ju 87 Stuka Dive Bomber**
   https://www.nationalmuseum.af.mil/Upcoming/Photos/igsearch/ju-87/
   급강하폭격기 외형 사진 참고.
6. Smithsonian National Air and Space Museum, **Junkers Jumo 211, Inverted V-12 Engine**
   https://airandspace.si.edu/collection-objects/junkers-jumo-211-inverted-v-12-engine/nasm_A19670122000
   Ju 87의 수랭식 역V12 엔진을 참고해 뇌격기의 둥근 방사형 카울과 다른 좁은 기수·턱 흡기구 사용.

## 파일과 규격

- `Models/*.fbx`: 개별 에셋, 단일 루트, scale 1.0. Blender +Y 앞/+Z 위, FBX -Z Forward/Y Up.
- 블록 바닥 Z=0, 함교 1.8×5.8 이내, 나머지 받침 1.8×1.8 이내. 포신·어뢰관은 규격서의 돌출 허용을 사용합니다.
- 적 함선은 수면 Z=0에 흘수가 들어갑니다. 항공기는 동체 중심 기준입니다.
- `WWII_ModePack.blend`: 원본, 개별 모델을 X축으로 22m 간격 정리. FBX는 각각 원점 기준으로 내보냈습니다.
- `manifest.json`: 모델별 삼각형 수·크기·소켓·연결 ID.
- `validation.json`: FBX 재임포트 검사 결과. 단일 루트·단위 스케일·원점·블록 크기·핵심 소켓 계층 검사.

대공포(약1,616 tris)와 어뢰관(약1,356 tris)은 기존 일반 모듈 목표 300~900보다 상세합니다. 적용 시 이 두 모델은 성능 기준에 따라 소형 부품 생략이나 LOD를 판단해야 합니다. 순양함 약4,208 tris. 정확한 최종 수치는 manifest를 기준으로 합니다.

## 적용할 때 연결할 항목

런타임을 포함하지 않는 모델 세트입니다. `NavalWw2Builder.Run`을 그대로 다시 실행하면 기존 도형으로 덮어쓰므로, 채택 후 모델 시각부 연결 로직을 별도로 넣어야 합니다.

- 함포: `TurretPivot/ElevationPivot/Muzzle`, `Muzzle_01/02`.
- 대공포: 같은 피벗과 `Muzzle_01~04`. 이전 도형의 2×2 배열을 그대로 재사용하지 않습니다.
- 어뢰관: `TubeMount`, `LaunchPoint_01~04`. 현재 코드의 발사 위치는 소켓을 읽지 않고 계산하므로 소켓과 실제 생성점의 일치는 적용 시 확인해야 합니다.
- 함교: `RadarPivot`, 연막용 `LaunchPoint_01~04`, `BridgeGunMount`. 내장 기관총은 이 소켓에 붙이도록 연결해야 마스트 위에 자동 배치되는 문제를 피할 수 있습니다.
- 순양함: `A/B/X_TurretPivot`, 각 포탑 아래 `A/B/X_ElevationPivot`, 각 `Muzzle_01/02`. 현재 보스는 선회만 하며 고각축 연결은 별도입니다. 양현 부포는 `Secondary_P/S_Muzzle`.
- 항공기: `Propeller` 축은 Blender Y. `TorpedoDropPoint` 또는 `BombDropPoint`. 현재 코드가 모델 소켓으로 프로펠러/투하점을 자동 연결하지 않으므로 적용 시 연결합니다.
- 충돌체·레이어·공격 수치·탄속·이펙트·오디오·피격 AI는 기존 WWII 프리팹에서 유지해야 합니다.

전체 포탑 선회와 고각의 모든 조합, 전투 카메라 가독성, Unity 게임 플레이는 이번 검토용 파일 제작에서 검증하지 않았습니다.
