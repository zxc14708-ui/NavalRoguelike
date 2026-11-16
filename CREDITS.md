# 크레딧 · 출처 기록

게임 안 설정 › 정보 탭은 `Assets/_Game/Resources/Credits.txt`를 그대로 보여 준다. 출처가 바뀌면 두 곳을 함께 고친다.

| 분류 | 내용 | 출처 · 라이선스 | 상태 |
|---|---|---|---|
| 엔진 | Unity 6000.3, URP 17.3, TextMesh Pro, Input System 1.20 | Unity 라이선스 | 확인 |
| 글꼴 | Noto Sans KR Regular (`Assets/_Game/Art/Fonts/NotoSansKR-Regular.otf`) | Adobe, SIL Open Font License 1.1 — `NotoSansKR_OFL.txt` 동봉 | 확인 |
| 3D 모델 | 함정·무장·호위함·적·섬·항구 | 프로젝트 자체 제작(Codex로 만든 원본 `ArtSource/*`, 빌더가 `Assets/_Game/Art`로 가져옴) | 확인 |
| 3D 모델 | 항공대 전투기 F/A-18F 슈퍼호넷 · 조기경보기 E-2D 호크아이(저폴리) | 프로젝트 자체 제작 — Blender 스크립트 `ArtSource/SuperHornet_v1/build.py` · `ArtSource/HawkeyeE2_v1/build.py`(2026-10-21), 빌더 `NavalAirWingArt` | 확인 |
| 코드로 만든 형태 | 바다 표면, 섬 장식, 예광탄, HUD 아이콘, 이펙트 | 코드(`Scripts/View`, `Scripts/Ship/Signature`, `UI/HudTextures`) | 확인 |
| 효과음 | `Assets/_Game/Audio/SFX/*.wav` 6종(기관·폭발·피격·미사일 발사·미사일 경보·소나) | **기록 없음** — 2026-10-05 첫 기록 때부터 있던 파일. 출시 전 출처·라이선스 확인 필요 | 확인 필요 |
| 기함 피격음 | `SFX_HullHit_Gun/Shell/Missile/Torpedo/Collision` — 2026-11-15 코드로 합성(`ArtSource/Audio/HullHitSfx_v1/synth.py`, numpy · scipy · soundfile) | 프로젝트 자체 제작 | 문제 없음 |
| 전투 효과음 | `SFX_EnemyGun_Shot`(적 포탑 사격) · `SFX_Missile_Launch`(미사일 발사 — 출처 기록 없던 예전 파일을 대체) · `SFX_Missile_Hit` · `SFX_Missile_Burst` · `SFX_Shell_Impact`, 다시 만든 `SFX_HullHit_Gun/Shell` — 2026-11-16 코드로 합성(`ArtSource/Audio/CombatSfx_v1/synth.py`, 파이썬 기본 모듈만) | 프로젝트 자체 제작 | 문제 없음 |
| 전투 효과음 2차 · 환경음 · 배경음악 | `SFX_Torpedo_Launch` · `SFX_Underwater_Blast` · `SFX_Sink_Capsize` · `SFX_Sink_Groan` · `SFX_Wreck_Blast` · `SFX_EnemyAutocannon_Shot` · `SFX_Jet_Pass` · `SFX_Prop_Pass` · `SFX_Dive_Siren` · `SFX_Drone_Buzz` · `SFX_Shell_Splash` · `SFX_Depth_Drop` · `SFX_Chaff_Launch` · `SFX_Jam_Pulse` · `SFX_Sea_Ambience` · `BGM_Battle` · `BGM_Boss` — 2026-11-16 코드로 합성(`ArtSource/Audio/CombatSfx_v2/synth.py`) | 프로젝트 자체 제작 | 문제 없음 |
| 무장 효과음 | `SFX_Autocannon_Shot`(기관포) · `SFX_CIWS_Burst`(CIWS) · `SFX_NavalGun76_Shot`(76mm) · `SFX_TwinGun40_Shot`(40mm 쌍열포) — 2026-11-15 사용자 제공 원본(`ciws.flac`, `76mm 함포.mp3`, `기관포.wav`, `40mm 쌍열포.wav`)을 잘라 48kHz 모노로 변환(`ArtSource/Audio/WeaponSfx_v1/convert.py`), 기관포 · 40mm는 같은 날 사용자 저음 보강판(`autocannon_heavy_48k_mono.wav`, `twin40_heavy_48k_mono.wav`)으로 교체 | **출처 기록 필요** — 사용자가 내려받은 파일. 출시 전 출처·라이선스 확인 필요 | 확인 필요 |
| 코드 라이브러리 | FastNoiseLite 1.1.1(`Scripts/ThirdParty/FastNoiseLite`) — 섬 해안선·섬 밀도 노이즈 | https://github.com/Auburn/FastNoiseLite · MIT(Copyright 2023 Jordan Peck, Contributors) · 소스 원본 그대로, 머리 문구 보존 · 2026-10-31 | 확인 |
| 코드 라이브러리 | PrimeTween 1.5.0(npm 패키지 `com.kyrylokuzyk.primetween`) — 정비 카드·HUD·화면 흔들림 연출 | https://github.com/KyryloKuzyk/PrimeTween · 자체 라이선스(게임 바이너리 상업 사용 허용, 소스·tarball 재배포 금지 — 공개 저장소이므로 반드시 npm 레지스트리로만 설치, 소스를 저장소에 넣지 않음) · 2026-10-31 | 확인 |
| 참고 자료 | 미 해군 Phalanx CIWS, AN/SQQ-89 공개 자료 | 역할 설명에만 참고(점검 문서 `GAME_AUDIT_2026-10-11.md`) | 확인 |

새 에셋을 들이면 이 표에 한 줄을 더한다(출처 URL, 라이선스, 들인 날짜).
