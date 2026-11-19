# Codex 모델링 요청 — 낮과 밤 (2026-11-19)

작성: Claude. 기능 설명은 `ARCHITECTURE.md` "낮과 밤 · 조명탄 · 반잠수정 · 밤 조명 · 소노부이 (2026-11-19)".
낮(함대함) → 해질녘 → 밤(잠수함 · 반잠수정) → 새벽(보스) 흐름이 이미 게임에 들어가 있고, 아래 대상은 지금 **임시 외형**으로 돌아간다.
코드 · 데이터 · 프리팹 연결은 Claude가 한다. Codex는 **모델(FBX) · 원본 · 비교 이미지**까지만.

우선순위: **A(반잠수정 2종) → B(부표 · 소노부이) → C(조명탄 · 선택)**. A만 와도 밤 전투 모습이 크게 좋아진다.

---

## 0. 공통 규칙 (4스테이지 요청서 `CODEX_MODEL_REQUESTS_STAGE4_2026-11-16.md` 0장과 같음)

- 현재 작업 중인 다른 코드 · 에셋을 덮어쓰지 말 것. 관련 없는 프리팹 · 씬을 일괄 재생성하지 말 것. enum 순서 변경 금지.
- **단위 · 축**: 1 유닛 = 게임 1m. 앞 = +Z, 위 = +Y. FBX는 기존 모델과 같은 방식(Blender `apply_scale_options='FBX_SCALE_NONE'`, 유니티 루트 배율 100).
- **재질**: 새로 만들지 말고 기존 팔레트 이름을 쓴다(`Assets/_Game/Art/Models/Materials`).
- **스타일**: 낮은 폴리곤 · 각진 면, 텍스처 없음(재질 색만).
- **수면**: 모델 원점 = 수면선 높이의 중심(모델 y 0이 물 높이).
- **결과물**: `ArtSource/DayNight_v1/`(blend · build 스크립트 · manifest.json · 비교 PNG) + FBX. 유니티 가져오기 검증은 별도 검증 프로젝트에서.
- **비교 이미지**: 모델마다 2장 — 비스듬한 뷰, 전투 카메라처럼 위에서 본 뷰(기함 16m 실루엣과 나란히). 반잠수정은 **기존 자폭 보트 · 경어뢰정과 나란히** 한 장 더.

### 0-1. 등불 자리(밤 조명)

게임이 밤에 빛나는 등불(빛나는 구)을 **이름 있는 빈 노드 자리**에 단다. 등불 자체는 모델에 넣지 말고, 자리만 빈 노드로 둔다.

| 빈 노드 | 쓰임 |
|---|---|
| `LampPoint` | 부표 · 소노부이 꼭대기 등불(점멸) |
| `LampPoint_Port` / `LampPoint_Starboard` / `LampPoint_Mast` | 배 항해등(빨강 · 초록 · 흰색) — 반잠수정은 **없음**(불을 끄고 다니는 배) |

---

## A. 반잠수정 2종 (밤 구간 적)

**공통 모습**: 물에 거의 잠긴 낮은 배. 실제 마약 운반용 반잠수정 · 저피탐 자폭정처럼 **건현(물 위 높이)이 매우 낮고**, 위에서 보면 길쭉한 시가형/쐐기형 선체 위에 작은 조종실 혹이 하나 있다.
게임 규칙: 레이더에 늦게 잡히고(탐지 거리 40%), 조명탄 · 탐조등 · 소나에 드러나며, 폭뢰에도 맞는다. 그래서 **위에서 봤을 때 수면과 잘 섞이는 어두운 색**이어야 한다.

### 재질 규칙 (중요 — 일반 적과 다름)

| 면 | 재질 이름 | 이유 |
|---|---|---|
| 선체 위 · 옆 | `Enemy Dark` | 어두운 회청색. 갑판 재질(`Deck Grey` · `Stealth deck` 등)은 **쓰지 말 것** — 게임이 적갈색으로 칠해 눈에 띈다 |
| 조종실 혹 · 해치 | `Enemy hull grey` | 약간 밝은 포인트(위에서 형태가 읽히게) |
| 유리 | `Blue black glazing` | 작은 관측창 |
| 배기 · 추진 | `Gunmetal` | |

- 지금 임시 외형은 기존 보트 모델을 위아래 45%로 눌러 어둡게 칠한 것이다. 모델이 오면 누르기를 끈다(Claude).

### A-1. 반잠수 자폭정 `ENE_SemiSubSuicide`

**하는 일**: 낮게 다가와 기함을 들이받고 폭발. **크기**: 길이 **7.5m** · 폭 **2.0m** · 물 위 높이 **0.35m**(조종실 혹 꼭대기 0.7m). 흘수 0.5m. **예산**: 350~600 tris.

```
ENE_SemiSubSuicide            (원점: 수면선 중심, 앞 +Z)
├─ Hull                       쐐기형 선체(뱃머리가 물을 가르는 날카로운 모양)
├─ Hump                       조종실 혹(선체 뒤쪽 1/3, 길이 1.6 · 폭 0.9)
├─ WarheadNose                뱃머리 폭약 덮개(따로 된 메시 — 색 구분용, `Enemy hull grey`)
├─ Exhaust (빈 노드)          선미 수면 높이, 뒤 −Z — 물보라 꼬리선 시작점
└─ BlastPoint (빈 노드)       뱃머리 — 폭발이 나는 점
```

### A-2. 반잠수 어뢰정 `ENE_SemiSubTorpedo`

**하는 일**: 거리를 두고 어뢰를 쏘고 빠진다. **크기**: 길이 **11m** · 폭 **2.8m** · 물 위 높이 **0.45m**(조종실 혹 꼭대기 1.0m). 흘수 0.7m. **예산**: 500~800 tris.

```
ENE_SemiSubTorpedo
├─ Hull                       시가형 선체(둥근 등)
├─ Hump                       조종실 혹 + 짧은 스노클 마스트(높이 0.5)
├─ TorpedoTube_Port / _Starboard    선체 옆 반쯤 잠긴 발사관(길이 3.5 · 지름 0.45)
├─ TorpedoLaunchPoint_Port / _Starboard (빈 노드)  발사관 입구, 앞 +Z — 어뢰가 나가는 점
├─ Exhaust (빈 노드)
└─ SmokePoint · FirePoint (빈 노드)  손상 표시
```

- 위에서 봤을 때 자폭정은 **짧고 뾰족**, 어뢰정은 **길고 둥글며 옆에 발사관 두 줄**로 구별되게.

---

## B. 부표 · 소노부이 (밤 소품)

지금은 기존 `ENV_NavigationBuoy` · `ENV_Sandbar_Marker` 위에 등불만 얹고, 조난 신호 · 소노부이는 기본 도형이다.

| 이름 | 쓰임 | 크기 · 예산 | 모습 · 재질 |
|---|---|---|---|
| `ENV_ChannelBuoy_Red` / `ENV_ChannelBuoy_Green` | 섬 둘레 항로 부표(좌현 빨강 · 우현 초록 짝) — 밤에 점멸 | 높이 2.6m · 지름 1.2m · 120~200 tris | 뜨는 통 + 격자 탑 + 꼭대기 등롱. 빨강 `Emergency Red`, 초록 `MAT_art_repair_green`, 탑 `Gunmetal`. **`LampPoint`** 꼭대기 |
| `ENV_LightBuoy_Yellow` | 모래톱 · 위험 표시 등부표(노랑 점멸) | 높이 2.4m · 120~200 tris | 노란 통 `Warning Yellow` + X자 꼭대기 표지. **`LampPoint`** |
| `ENV_DistressBeacon` | 구조 활동 구명정 옆 조난 신호 부표(빨강 점멸) | 높이 1.2m · 60~120 tris | 주황 통 + 짧은 안테나. **`LampPoint`** |
| `PRJ_Sonobuoy` | 헬기가 대잠 초계 중 떨구는 소노부이 | 높이 0.9m(물 위 0.6) · 지름 0.45m · 60~120 tris | 노란 원통 `Warning Yellow` + 위로 선 가는 안테나 + 작은 부력 고리. **`LampPoint`** 안테나 끝 |

- 원점 = 수면선 중심(통의 1/3쯤이 물에 잠기게).
- 게임이 물결에 출렁이게 하므로 움직이는 부품은 없어도 된다.

---

## C. 조명탄 (선택)

지금은 빛나는 구 + 점광원뿐이다. 없어도 기능에는 문제 없다.

| 이름 | 쓰임 | 크기 · 예산 | 노드 |
|---|---|---|---|
| `FX_ParachuteFlare` | 낙하산 조명탄(15m에서 터져 11초 동안 내려옴) | 낙하산 지름 1.4m · 줄 1.2m · 불꽃 통 0.3m · 80~160 tris | `Canopy`(낙하산, 흰색 `MAT_IslandWhite`) · `Canister`(`Gunmetal`) · **`FlarePoint`**(빈 노드, 통 아래 끝 — 불꽃 · 빛이 나는 점) |

---

## D. 모델이 오기 전의 임시 계획 (Claude — 이미 적용)

| 대상 | 임시 외형 |
|---|---|
| 반잠수 자폭정 · 어뢰정 | 자폭 보트 · 경어뢰정 모델을 위아래 45%로 눌러 `Enemy Dark` 비슷한 색으로 칠함(`LowProfileHull`) |
| 항로 부표 · 등부표 | 기존 `ENV_NavigationBuoy` · `ENV_Sandbar_Marker` + 등불 |
| 조난 신호 · 소노부이 | 기본 도형(원통) + 등불 |
| 조명탄 | 빛나는 구 + 점광원 |

## E. 받은 뒤 Claude가 하는 일

- 반잠수정: 프리팹 모델 교체, `LowProfileHull`의 누르기 끄기, 어뢰정 `launchPoints`를 `TorpedoLaunchPoint_*`에 연결.
- 부표 · 소노부이 · 조난 신호: 소품으로 바꾸고 등불을 `LampPoint` 자리에 단다.
- `-dayNightOnly` · `-artOnly`(크기 · 수면선) 검사를 돌리고, 맞지 않는 소켓 · 크기는 이 문서에 적어 되돌려 보낸다.

## F. 받은 결과 (2026-11-19 확인 — Claude)

- Codex 결과물: `ArtSource/DayNight_v1/` — FBX 8종(A 2 · B 5 · C 1), `DayNight.blend`, `build.py` · `models.py` · `validate.py`, `manifest.json`, 비교 PNG 17장.
- 검사: Blender FBX 왕복 8/8 통과, 별도 Unity 6000.3.23f1 가져오기 통과(크기 · 계층 · 소켓 축 · 재질). 삼각형 수 모두 예산 안(자폭정 588 · 어뢰정 792 · 부표 116~196 · 조명탄 160).
- 재질 이름 9종 모두 프로젝트에 있음(`MAT_IslandWhite`는 `Resources/Islands`, `MAT_art_repair_green`은 `Art/` 아래 — `Art/Models/Materials` 밖).
- **게임 연결 완료(2026-11-19 Claude)**:
  - 반잠수정: `Art/Models/DayNight` → 프리팹 `ENE_SemiSubSuicide` · `ENE_SemiSubTorpedo`의 ModelPivot(Y 180°) 아래로 교체(`Editor/NavalDayNightModels`). **배율 0.6** — 요청서 크기(7.5m · 11m)가 기존 보트(3.5m · 5.8m)의 2배라 자폭정 4.5m · 어뢰정 6.6m로 줄였다. 판정 상자도 모델에 맞춤. 어뢰정 발사점 = `TorpedoLaunchPoint_Port/_Starboard`(포탑 없음). `LowProfileHull`은 전용 모델(`Hump`)이면 누르기 · 덧칠을 하지 않는다.
  - 소품: `Resources/Environment/DayNight` → 섬 항로 부표 짝(빨강/초록) · 모래톱 노란 등부표 · 구명정 조난 신호 · 소노부이 · 조명탄 낙하산(`View/DayNightProps`). 등불은 `LampPoint`에.
  - 다음에 모델을 고칠 때: 게임 크기는 0.6배로 쓰이므로, 바꾸려면 배율(`NavalDayNightModels.SuicideScale/TorpedoScale`)을 고치거나 모델 자체를 줄여 보내면 된다(둘 다 바꾸지 말 것).
