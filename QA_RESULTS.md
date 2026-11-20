# QA 결과표

자동 전투 검증(`CombatVerificationRunner`)을 `-qaTable <이 파일>`로 돌릴 때마다 고쳐 쓴다(점검 O03). 손으로 고치지 않는다.
- **기준**: 가장 최근 실행과 같은 커밋이면 `최신`, 아니면 `예전 커밋` — 지금 코드로 다시 확인하지 않은 결과다.
- **미커밋**: 빌드할 때 커밋 안 한 변경 파일 수(0이면 그 커밋 그대로). 커밋·미커밋은 빌드 옆 `build_stamp.txt`에서 읽는다.
- 자세한 내용은 각 실행의 `report.md`(출력 폴더).

## 검사별 최신 결과

| 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드(끝) | 소요 | 환경 | 첫 실패 |
|---|---|---|---|---|---|---|---|---|---|
| -artOnly | 통과 | 최신 | 2026-11-20 22:51 | 6e57dad | 4 | 678984 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -aviationOnly | 통과 | 최신 | 2026-11-20 22:27 | 6e57dad | 0 | 273062 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -baseIslandsOnly | 통과 | 최신 | 2026-11-20 22:36 | 6e57dad | 0 | 789984 | 0:09 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blastOnly | 통과 | 최신 | 2026-11-20 22:33 | 6e57dad | 0 | 635796 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blockHitOnly | 통과 | 최신 | 2026-11-20 22:32 | 6e57dad | 0 | 544484 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleBOnly | 통과 | 최신 | 2026-11-20 22:29 | 6e57dad | 0 | 386609 | 0:16 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleCOnly | 통과 | 최신 | 2026-11-20 22:22 | 6e57dad | 0 | 955671 | 0:15 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleDOnly | 통과 | 최신 | 2026-11-20 22:51 | 6e57dad | 4 | 424242 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -burstOnly | 통과 | 최신 | 2026-11-20 22:38 | 6e57dad | 0 | 939468 | 0:21 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -cardsNumbersOnly | 통과 | 최신 | 2026-11-20 22:28 | 6e57dad | 0 | 303984 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -carrierBossOnly | 통과 | 최신 | 2026-11-20 22:35 | 6e57dad | 0 | 707609 | 0:33 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -dayNightOnly | 통과 | 최신 | 2026-11-20 22:27 | 6e57dad | 0 | 281312 | 0:10 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -decorOnly | 통과 | 예전 커밋 | 2026-11-03 23:31 | ce3a323 | 30 | 755578 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -defenseOnly | 통과 | 예전 커밋 | 2026-11-04 00:11 | ce3a323 | 35 | 135281 | 0:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -enemyOnly | 통과 | 예전 커밋 | 2026-10-13 01:01 | 565a99e | 11 | 884671 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortArmsOnly | 통과 | 최신 | 2026-11-20 22:26 | 6e57dad | 0 | 200937 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortOnly | 통과 | 예전 커밋 | 2026-11-17 17:26 | 12be3eb | 10 | 167062 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -fieldEventsOnly | 통과 | 최신 | 2026-11-20 22:28 | 6e57dad | 0 | 312656 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -formationOnly | 통과 | 예전 커밋 | 2026-11-17 17:14 | 8db6dae | 12 | 409375 | 1:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -growthOnly | 통과 | 예전 커밋 | 2026-11-17 17:27 | 12be3eb | 10 | 195437 | 0:10 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborAAOnly | 통과 | 최신 | 2026-11-20 22:31 | 6e57dad | 0 | 509562 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBossOnly | 통과 | 최신 | 2026-11-20 22:36 | 6e57dad | 0 | 811437 | 1:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBuildingsOnly | 통과 | 최신 | 2026-11-20 22:45 | 6e57dad | 0 | — | 0:12 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborOpsOnly | 통과 | 최신 | 2026-11-20 22:31 | 6e57dad | 0 | 502015 | 0:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -helmOnly | 통과 | 최신 | 2026-11-20 22:38 | 6e57dad | 0 | 910906 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hitFeelOnly | 통과 | 최신 | 2026-11-20 22:28 | 6e57dad | 0 | 295671 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -howitzerOnly | 통과 | 예전 커밋 | 2026-11-01 12:32 | c5376ef | 53 | 512328 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hudOnly | 통과 | 최신 | 2026-11-20 22:38 | 6e57dad | 0 | 902578 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -islandOnly | 통과 | 예전 커밋 | 2026-11-03 23:32 | ce3a323 | 30 | 785171 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -juiceOnly | 통과 | 최신 | 2026-11-20 22:33 | 6e57dad | 0 | 619125 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lifeOnly | 통과 | 예전 커밋 | 2026-10-31 13:39 | 594a21c | 23 | 407187 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lunPatternOnly | 통과 | 최신 | 2026-11-20 22:31 | 6e57dad | 0 | 471140 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -markersOnly | 통과 | 최신 | 2026-11-20 22:31 | 6e57dad | 0 | 462750 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (창 모드) | 통과 | 최신 | 2026-11-20 22:45 | 6e57dad | 0 | 374625 | 4:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly -perfMinutes 30 (배치 모드) | 통과 | 예전 커밋 | 2026-10-12 22:38 | 6356259 | 6 | 322156 | 31:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -polishOnly | 통과 | 최신 | 2026-11-20 22:34 | 6e57dad | 0 | 685515 | 0:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeMapOnly | 통과 | 최신 | 2026-11-20 22:33 | 6e57dad | 0 | 629125 | 0:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeOnly | 통과 | 최신 | 2026-11-20 22:37 | 6e57dad | 0 | 881953 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -runFlowOnly | 통과 | 최신 | 2026-11-20 22:22 | 6e57dad | 0 | 981171 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -seaOnly | 통과 | 최신 | 2026-11-20 22:27 | 6e57dad | 0 | 231968 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -sfxOnly | 통과 | 최신 | 2026-11-20 22:36 | 6e57dad | 0 | 802937 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -shoreOnly | 통과 | 최신 | 2026-11-20 22:34 | 6e57dad | 0 | 662937 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -signatureOnly | 통과 | 최신 | 2026-11-20 22:30 | 6e57dad | 0 | 406812 | 0:52 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -specOnly | 통과 | 최신 | 2026-11-20 22:23 | 6e57dad | 0 | 998625 | 3:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -stage4Only | 통과 | 최신 | 2026-11-20 22:35 | 6e57dad | 0 | 744968 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -threatOnly | 통과 | 예전 커밋 | 2026-10-13 01:02 | 565a99e | 11 | 916125 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -upgradeOnly | 통과 | 예전 커밋 | 2026-11-17 17:26 | 12be3eb | 10 | 158562 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -vlsOnly | 통과 | 최신 | 2026-11-20 22:32 | 6e57dad | 0 | 575921 | 0:39 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -weaponPackOnly | 통과 | 최신 | 2026-11-20 22:32 | 6e57dad | 0 | 532796 | 0:07 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2BattleOnly | 통과 | 최신 | 2026-11-20 23:12 | 6e57dad | 6 | 978093 | 2:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2HeavyOnly | 통과 | 최신 | 2026-11-20 22:44 | 6e57dad | 0 | 296578 | 0:36 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2Only | 통과 | 최신 | 2026-11-20 23:10 | 6e57dad | 6 | 817437 | 2:44 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| 전체 | 통과 | 최신 | 2026-11-20 22:08 | 6e57dad | 0 | 127562 | 13:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |

## 최근 실행 기록

| 실행 시각 | 검사 | 결과 | 커밋 | 소요 |
|---|---|---|---|---|
| 2026-11-20 23:12 | -ww2BattleOnly | 통과 | 6e57dad +6 | 2:25 |
| 2026-11-20 23:10 | -ww2Only | 통과 | 6e57dad +6 | 2:44 |
| 2026-11-20 23:05 | -ww2Only | 실패 1 | 6e57dad +4 | 2:30 |
| 2026-11-20 23:01 | -ww2Only | 실패 1 | 6e57dad +4 | 2:40 |
| 2026-11-20 22:56 | -ww2Only | 실패 1 | 6e57dad +4 | 2:46 |
| 2026-11-20 22:52 | -ww2Only | 실패 1 | 6e57dad +4 | 2:44 |
| 2026-11-20 22:51 | -bundleDOnly | 통과 | 6e57dad +4 | 0:41 |
| 2026-11-20 22:51 | -artOnly | 통과 | 6e57dad +4 | 0:06 |
| 2026-11-20 22:45 | -perfOnly (창 모드) | 통과 | 6e57dad | 4:03 |
| 2026-11-20 22:45 | -harborBuildingsOnly | 통과 | 6e57dad | 0:12 |
| 2026-11-20 22:44 | -ww2HeavyOnly | 통과 | 6e57dad | 0:36 |
| 2026-11-20 22:42 | -ww2BattleOnly | 통과 | 6e57dad | 2:48 |
| 2026-11-20 22:39 | -ww2Only | 실패 2 | 6e57dad | 2:36 |
| 2026-11-20 22:38 | -burstOnly | 통과 | 6e57dad | 0:21 |
| 2026-11-20 22:38 | -helmOnly | 통과 | 6e57dad | 0:24 |
| 2026-11-20 22:38 | -hudOnly | 통과 | 6e57dad | 0:04 |
| 2026-11-20 22:37 | -routeOnly | 통과 | 6e57dad | 0:17 |
| 2026-11-20 22:36 | -harborBossOnly | 통과 | 6e57dad | 1:06 |
| 2026-11-20 22:36 | -sfxOnly | 통과 | 6e57dad | 0:04 |
| 2026-11-20 22:36 | -baseIslandsOnly | 통과 | 6e57dad | 0:09 |
| 2026-11-20 22:35 | -stage4Only | 통과 | 6e57dad | 0:41 |
| 2026-11-20 22:35 | -carrierBossOnly | 통과 | 6e57dad | 0:33 |
| 2026-11-20 22:34 | -polishOnly | 통과 | 6e57dad | 0:18 |
| 2026-11-20 22:34 | -shoreOnly | 통과 | 6e57dad | 0:19 |
| 2026-11-20 22:33 | -blastOnly | 통과 | 6e57dad | 0:23 |
| 2026-11-20 22:33 | -routeMapOnly | 통과 | 6e57dad | 0:03 |
| 2026-11-20 22:33 | -juiceOnly | 통과 | 6e57dad | 0:06 |
| 2026-11-20 22:32 | -vlsOnly | 통과 | 6e57dad | 0:39 |
| 2026-11-20 22:32 | -blockHitOnly | 통과 | 6e57dad | 0:28 |
| 2026-11-20 22:32 | -weaponPackOnly | 통과 | 6e57dad | 0:07 |
| 2026-11-20 22:31 | -harborAAOnly | 통과 | 6e57dad | 0:19 |
| 2026-11-20 22:31 | -harborOpsOnly | 통과 | 6e57dad | 0:03 |
| 2026-11-20 22:31 | -lunPatternOnly | 통과 | 6e57dad | 0:27 |
| 2026-11-20 22:31 | -markersOnly | 통과 | 6e57dad | 0:05 |
| 2026-11-20 22:30 | -signatureOnly | 통과 | 6e57dad | 0:52 |
| 2026-11-20 22:29 | -bundleBOnly | 통과 | 6e57dad | 0:16 |
| 2026-11-20 22:28 | -bundleDOnly | 실패 1 | 6e57dad | 0:41 |
| 2026-11-20 22:28 | -fieldEventsOnly | 통과 | 6e57dad | 0:25 |
| 2026-11-20 22:28 | -cardsNumbersOnly | 통과 | 6e57dad | 0:05 |
| 2026-11-20 22:28 | -hitFeelOnly | 통과 | 6e57dad | 0:04 |
