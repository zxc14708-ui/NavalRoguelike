# 프로젝트 문서 안내

- 현재 구현과 작업 방식은 `ARCHITECTURE.md`를 참고한다.
- 함대 AI, 포격 탄도, UI 연출, 해수면, 섬 생성, 분기 항로, 인스펙터 편의 기능을 작업하거나 외부 코드 활용을 검토할 때 `Docs/GITHUB_REFERENCES.md`의 관련 항목을 먼저 확인한다.
- 참고 목록은 검토 후보이며 설치·호환성 검증 완료 목록이 아니다. 현재 코드와 비교해 필요한 부분을 선택하고 적용 시점의 라이선스·의존성·Unity 호환성을 확인한다.
- 실제로 도입하면 사용 버전/커밋, 적용 위치, 검증 결과를 참고 문서에 기록하고 `CREDITS.md`의 출처 기록 규칙을 따른다.

## 문서 위치 (2026-11-20 정리)

| 위치 | 문서 | 상태 |
|---|---|---|
| 루트 | `ARCHITECTURE.md` | 현재 구조 · 수치 · 변경 기록 — **기준 문서** |
| 루트 | `QA_RESULTS.md` | 자동 검증이 `-qaTable`로 고쳐 쓴다(손으로 고치지 않음) |
| 루트 | `CODEX_MODEL_REQUESTS.md` | Codex 모델 요청(기본 · WWII 6장 대기) — Codex가 이 경로를 읽는다 |
| 루트 | `CREDITS.md` | 외부 자료 · 코드 출처 |
| `Docs/` | `NEXT_TASKS_2026-11-14.md` | 다음 작업 · 결정 필요 항목(최신) |
| `Docs/` | `CODEX_MODEL_REQUESTS_STAGE4_2026-11-16.md` · `CODEX_MODEL_REQUESTS_DAYNIGHT_2026-11-19.md` | 기능별 Codex 모델 요청 · 연결 기록 |
| `Docs/` | `STAGE4_EKRANOPLAN_PLAN_2026-11-16.md` · `BLOCK_EVOLUTION_PLAN_2026-10-27.md` | 기획안(4스테이지 구현됨 · 블록 전직 결정 대기) |
| `Docs/` | `SESSION_LOG_DAMAGE_PLAN_2026-11-21.md` | 11-20 ~ 11-21 작업 기록 · 손상 모델 기획(손상 통제 · 함교 집중 피격 · 블록 파괴 확인) |
| `Docs/` | `LIVE_STARTING_SHIPS.md` · `STARTING_SHIP_DESIGN.md` | 시작 함선 설계 · 남은 결정(S01 · S02) |
| `Docs/` | `GITHUB_REFERENCES.md` | 외부 코드 참고 목록 |
| `Docs/WWII/` | `WWII_TRANSITION_PLAN.md` · `WWII_PATCH_A_DESIGN.md` · `patch-a-content-map.json` | WWII 모드 계획 · 설계 |
| `Docs/archive/` | `TASKFORCE_HANDOFF.md` · `GAME_AUDIT_2026-10-11.md` · `BRIDGE_MODEL_REWORK.md` | 끝난 인수인계 · 점검 기록(점검 번호 O03 등은 여기) |
| `ArtSource/` | Codex 납품 원본(모델 · 스크립트 · 미리보기) | 게임 연결 여부는 각 README · `ARCHITECTURE.md` |
