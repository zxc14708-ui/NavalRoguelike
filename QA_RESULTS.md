# QA 결과표

자동 전투 검증(`CombatVerificationRunner`)을 `-qaTable <이 파일>`로 돌릴 때마다 고쳐 쓴다(점검 O03). 손으로 고치지 않는다.
- **기준**: 가장 최근 실행과 같은 커밋이면 `최신`, 아니면 `예전 커밋` — 지금 코드로 다시 확인하지 않은 결과다.
- **미커밋**: 빌드할 때 커밋 안 한 변경 파일 수(0이면 그 커밋 그대로). 커밋·미커밋은 빌드 옆 `build_stamp.txt`에서 읽는다.
- 자세한 내용은 각 실행의 `report.md`(출력 폴더).

## 검사별 최신 결과

| 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드(끝) | 소요 | 환경 | 첫 실패 |
|---|---|---|---|---|---|---|---|---|---|
| -artOnly | 통과 | 예전 커밋 | 2026-10-30 00:07 | f9c9bb8 | 9 | 405343 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -aviationOnly | 통과 | 예전 커밋 | 2026-10-30 00:12 | f9c9bb8 | 9 | 688156 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blastOnly | 통과 | 예전 커밋 | 2026-10-31 13:40 | 594a21c | 23 | 435812 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blockHitOnly | 통과 | 예전 커밋 | 2026-10-30 00:15 | f9c9bb8 | 9 | 843921 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleBOnly | 통과 | 예전 커밋 | 2026-10-30 00:13 | f9c9bb8 | 9 | 743125 | 0:16 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleCOnly | 통과 | 예전 커밋 | 2026-10-29 23:44 | f9c9bb8 | 9 | 10734 | 0:14 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleDOnly | 통과 | 예전 커밋 | 2026-10-30 00:12 | f9c9bb8 | 9 | 424242 | 0:43 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -burstOnly | 통과 | 예전 커밋 | 2026-10-30 00:21 | f9c9bb8 | 12 | 250171 | 0:21 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -decorOnly | 통과 | 예전 커밋 | 2026-10-30 00:44 | 594a21c | 14 | 600828 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -defenseOnly | 통과 | 예전 커밋 | 2026-10-30 00:22 | f9c9bb8 | 12 | 274546 | 0:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -enemyOnly | 통과 | 예전 커밋 | 2026-10-13 01:01 | 565a99e | 11 | 884671 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortArmsOnly | 통과 | 예전 커밋 | 2026-10-30 00:11 | f9c9bb8 | 9 | 616750 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortOnly | 실패 1 | 예전 커밋 | 2026-10-30 00:23 | f9c9bb8 | 12 | 374875 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | 호위함 바로 옆 적이 호위함을 노리지 않음 |
| -formationOnly | 통과 | 예전 커밋 | 2026-10-30 00:25 | f9c9bb8 | 12 | 486328 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -growthOnly | 통과 | 최신 | 2026-10-31 14:01 | d3784c1 | 10 | 700109 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborAAOnly | 통과 | 예전 커밋 | 2026-10-30 00:14 | f9c9bb8 | 9 | 809843 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBuildingsOnly | 통과 | 예전 커밋 | 2026-10-30 00:28 | f9c9bb8 | 12 | — | 0:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lifeOnly | 통과 | 예전 커밋 | 2026-10-31 13:39 | 594a21c | 23 | 407187 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (창 모드) | 통과 | 예전 커밋 | 2026-10-29 23:54 | f9c9bb8 | 9 | 645390 | 4:02 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly -perfMinutes 30 (배치 모드) | 통과 | 예전 커밋 | 2026-10-12 22:38 | 6356259 | 6 | 322156 | 31:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeOnly | 통과 | 예전 커밋 | 2026-10-30 00:27 | f9c9bb8 | 12 | 568203 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -runFlowOnly | 통과 | 최신 | 2026-10-31 14:00 | d3784c1 | 10 | 693593 | 0:12 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -seaOnly | 통과 | 예전 커밋 | 2026-10-30 00:11 | f9c9bb8 | 9 | 647437 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -signatureOnly | 통과 | 예전 커밋 | 2026-10-30 00:13 | f9c9bb8 | 9 | 762921 | 0:43 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -specOnly | 통과 | 예전 커밋 | 2026-10-30 00:07 | f9c9bb8 | 9 | 414968 | 3:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -threatOnly | 통과 | 예전 커밋 | 2026-10-13 01:02 | 565a99e | 11 | 916125 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -vlsOnly | 통과 | 예전 커밋 | 2026-10-30 00:15 | f9c9bb8 | 9 | 875093 | 0:39 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -weaponPackOnly | 통과 | 예전 커밋 | 2026-10-30 00:14 | f9c9bb8 | 9 | 832906 | 0:07 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2BattleOnly | 통과 | 예전 커밋 | 2026-10-30 00:01 | f9c9bb8 | 9 | 23859 | 5:22 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2HeavyOnly | 통과 | 예전 커밋 | 2026-10-30 00:06 | f9c9bb8 | 9 | 349750 | 0:36 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2Only | 통과 | 예전 커밋 | 2026-10-29 23:58 | f9c9bb8 | 9 | 882875 | 2:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| 전체 | 실패 6 | 예전 커밋 | 2026-10-29 23:31 | f9c9bb8 | 9 | 221312 | 13:02 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | CIWS가 미사일을 한 번도 격추하지 못함 |

## 최근 실행 기록

| 실행 시각 | 검사 | 결과 | 커밋 | 소요 |
|---|---|---|---|---|
| 2026-10-31 14:01 | -growthOnly | 통과 | d3784c1 +10 | 0:08 |
| 2026-10-31 14:00 | -runFlowOnly | 통과 | d3784c1 +10 | 0:12 |
| 2026-10-31 13:40 | -blastOnly | 통과 | 594a21c +23 | 0:08 |
| 2026-10-31 13:39 | -lifeOnly | 통과 | 594a21c +23 | 0:25 |
| 2026-10-30 00:44 | -decorOnly | 통과 | 594a21c +14 | 0:24 |
| 2026-10-30 00:43 | -decorOnly | 통과 | 594a21c +14 | 0:24 |
| 2026-10-30 00:41 | -decorOnly | 통과 | 594a21c +13 | 0:23 |
| 2026-10-30 00:28 | -harborBuildingsOnly | 통과 | f9c9bb8 +12 | 0:11 |
| 2026-10-30 00:27 | -decorOnly | 실패 1 | f9c9bb8 +12 | 0:24 |
| 2026-10-30 00:27 | -decorOnly | 실패 1 | f9c9bb8 +12 | 0:24 |
| 2026-10-30 00:27 | -routeOnly | 통과 | f9c9bb8 +12 | 0:17 |
| 2026-10-30 00:26 | -routeOnly | 통과 | f9c9bb8 +12 | 0:17 |
| 2026-10-30 00:26 | -routeOnly | 통과 | f9c9bb8 +12 | 0:17 |
| 2026-10-30 00:25 | -formationOnly | 통과 | f9c9bb8 +12 | 0:37 |
| 2026-10-30 00:25 | -formationOnly | 통과 | f9c9bb8 +12 | 0:37 |
| 2026-10-30 00:24 | -formationOnly | 통과 | f9c9bb8 +12 | 0:37 |
| 2026-10-30 00:23 | -escortOnly | 실패 1 | f9c9bb8 +12 | 0:27 |
| 2026-10-30 00:23 | -escortOnly | 통과 | f9c9bb8 +12 | 0:23 |
| 2026-10-30 00:22 | -escortOnly | 통과 | f9c9bb8 +12 | 0:24 |
| 2026-10-30 00:22 | -defenseOnly | 통과 | f9c9bb8 +12 | 0:42 |
| 2026-10-30 00:21 | -burstOnly | 통과 | f9c9bb8 +12 | 0:21 |
| 2026-10-30 00:16 | -routeOnly | 실패 1 | f9c9bb8 +9 | 0:18 |
| 2026-10-30 00:15 | -vlsOnly | 통과 | f9c9bb8 +9 | 0:39 |
| 2026-10-30 00:15 | -blockHitOnly | 통과 | f9c9bb8 +9 | 0:28 |
| 2026-10-30 00:14 | -weaponPackOnly | 통과 | f9c9bb8 +9 | 0:07 |
| 2026-10-30 00:14 | -harborAAOnly | 통과 | f9c9bb8 +9 | 0:19 |
| 2026-10-30 00:13 | -signatureOnly | 통과 | f9c9bb8 +9 | 0:43 |
| 2026-10-30 00:13 | -bundleBOnly | 통과 | f9c9bb8 +9 | 0:16 |
| 2026-10-30 00:12 | -bundleDOnly | 통과 | f9c9bb8 +9 | 0:43 |
| 2026-10-30 00:12 | -aviationOnly | 통과 | f9c9bb8 +9 | 0:04 |
| 2026-10-30 00:11 | -seaOnly | 통과 | f9c9bb8 +9 | 0:37 |
| 2026-10-30 00:11 | -escortArmsOnly | 통과 | f9c9bb8 +9 | 0:27 |
| 2026-10-30 00:07 | -specOnly | 통과 | f9c9bb8 +9 | 3:18 |
| 2026-10-30 00:07 | -artOnly | 통과 | f9c9bb8 +9 | 0:06 |
| 2026-10-30 00:07 | -runFlowOnly | 통과 | f9c9bb8 +9 | 0:13 |
| 2026-10-30 00:06 | -ww2HeavyOnly | 통과 | f9c9bb8 +9 | 0:36 |
| 2026-10-30 00:01 | -ww2BattleOnly | 통과 | f9c9bb8 +9 | 5:22 |
| 2026-10-29 23:58 | -ww2Only | 통과 | f9c9bb8 +9 | 2:25 |
| 2026-10-29 23:54 | -perfOnly (창 모드) | 통과 | f9c9bb8 +9 | 4:02 |
| 2026-10-29 23:44 | -bundleCOnly | 통과 | f9c9bb8 +9 | 0:14 |
