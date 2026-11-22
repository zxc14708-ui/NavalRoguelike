# QA 결과표

자동 전투 검증(`CombatVerificationRunner`)을 `-qaTable <이 파일>`로 돌릴 때마다 고쳐 쓴다(점검 O03). 손으로 고치지 않는다.
- **기준**: 가장 최근 실행과 같은 커밋이면 `최신`, 아니면 `예전 커밋` — 지금 코드로 다시 확인하지 않은 결과다.
- **미커밋**: 빌드할 때 커밋 안 한 변경 파일 수(0이면 그 커밋 그대로). 커밋·미커밋은 빌드 옆 `build_stamp.txt`에서 읽는다.
- 자세한 내용은 각 실행의 `report.md`(출력 폴더).

## 검사별 최신 결과

| 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드(끝) | 소요 | 환경 | 첫 실패 |
|---|---|---|---|---|---|---|---|---|---|
| -artOnly | 통과 | 최신 | 2026-11-22 01:44 | f38a35f | 23 | 472656 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -aviationOnly | 통과 | 최신 | 2026-11-22 01:49 | f38a35f | 23 | 742671 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -balanceSimOnly | 통과 | 최신 | 2026-11-22 02:09 | f38a35f | 23 | 973421 | 2:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -baseIslandsOnly | 통과 | 최신 | 2026-11-22 01:58 | f38a35f | 23 | 292781 | 0:10 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blastOnly | 통과 | 최신 | 2026-11-22 01:55 | f38a35f | 23 | 134140 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blockHitOnly | 통과 | 최신 | 2026-11-22 01:53 | f38a35f | 23 | 39890 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleBOnly | 통과 | 최신 | 2026-11-22 01:51 | f38a35f | 23 | 875125 | 0:16 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleCOnly | 통과 | 최신 | 2026-11-22 01:43 | f38a35f | 23 | 438031 | 0:15 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleDOnly | 통과 | 최신 | 2026-11-22 01:50 | f38a35f | 23 | 424242 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -burstOnly | 통과 | 최신 | 2026-11-22 02:00 | f38a35f | 23 | 448046 | 0:20 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -cardsNumbersOnly | 통과 | 최신 | 2026-11-22 01:49 | f38a35f | 23 | 774937 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -carrierBossOnly | 통과 | 최신 | 2026-11-22 01:56 | f38a35f | 23 | 207906 | 0:34 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -combatFxOnly | 통과 | 최신 | 2026-11-22 02:16 | f38a35f | 23 | 387984 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -dayNightOnly | 통과 | 최신 | 2026-11-22 01:49 | f38a35f | 23 | 751281 | 0:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -decorOnly | 통과 | 예전 커밋 | 2026-11-03 23:31 | ce3a323 | 30 | 755578 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -defenseOnly | 통과 | 예전 커밋 | 2026-11-21 23:45 | 3543688 | 116 | 339109 | 0:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -enemyOnly | 통과 | 예전 커밋 | 2026-10-13 01:01 | 565a99e | 11 | 884671 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortArmsOnly | 통과 | 최신 | 2026-11-22 01:47 | f38a35f | 23 | 669078 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortOnly | 통과 | 예전 커밋 | 2026-11-17 17:26 | 12be3eb | 10 | 167062 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -fieldEventsOnly | 통과 | 최신 | 2026-11-22 01:49 | f38a35f | 23 | 784453 | 0:40 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -fireOnly | 통과 | 최신 | 2026-11-22 02:08 | f38a35f | 23 | 942640 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -floodOnly | 통과 | 최신 | 2026-11-22 02:08 | f38a35f | 23 | 915859 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -formationOnly | 통과 | 예전 커밋 | 2026-11-17 17:14 | 8db6dae | 12 | 409375 | 1:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -growthOnly | 통과 | 예전 커밋 | 2026-11-21 22:47 | 3543688 | 108 | 834640 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborAAOnly | 통과 | 최신 | 2026-11-22 01:53 | f38a35f | 23 | 2765 | 0:20 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBossOnly | 통과 | 최신 | 2026-11-22 01:58 | f38a35f | 23 | 316609 | 1:07 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBuildingsOnly | 통과 | 최신 | 2026-11-22 02:11 | f38a35f | 23 | — | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborOpsOnly | 통과 | 최신 | 2026-11-22 01:53 | f38a35f | 23 | 994343 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -helmGuideOnly | 통과 | 최신 | 2026-11-22 14:14 | f38a35f | 40 | 667921 | 0:12 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -helmOnly | 통과 | 최신 | 2026-11-22 02:24 | f38a35f | 25 | 873453 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hitFeelOnly | 통과 | 최신 | 2026-11-22 01:49 | f38a35f | 23 | 766531 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hitMapOnly | 통과 | 최신 | 2026-11-22 02:28 | f38a35f | 25 | 101000 | 1:34 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -howitzerOnly | 통과 | 예전 커밋 | 2026-11-01 12:32 | c5376ef | 53 | 512328 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hudOnly | 통과 | 최신 | 2026-11-22 02:00 | f38a35f | 23 | 409859 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -islandOnly | 통과 | 예전 커밋 | 2026-11-03 23:32 | ce3a323 | 30 | 785171 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -juiceOnly | 통과 | 최신 | 2026-11-22 01:55 | f38a35f | 23 | 116203 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lifeOnly | 통과 | 예전 커밋 | 2026-10-31 13:39 | 594a21c | 23 | 407187 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lunPatternOnly | 통과 | 최신 | 2026-11-22 01:52 | f38a35f | 23 | 962593 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -markersOnly | 통과 | 최신 | 2026-11-22 01:52 | f38a35f | 23 | 953203 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (배치 모드) | 통과 | 예전 커밋 | 2026-11-21 23:02 | 3543688 | 110 | 745343 | 4:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (창 모드) | 통과 | 최신 | 2026-11-22 02:12 | f38a35f | 23 | 157359 | 4:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly -perfMinutes 30 (배치 모드) | 통과 | 예전 커밋 | 2026-10-12 22:38 | 6356259 | 6 | 322156 | 31:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -polishOnly | 통과 | 최신 | 2026-11-22 01:56 | f38a35f | 23 | 184968 | 0:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -radarOnly | 통과 | 최신 | 2026-11-22 13:41 | f38a35f | 27 | 670687 | 0:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeMapOnly | 통과 | 최신 | 2026-11-22 01:55 | f38a35f | 23 | 126609 | 0:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeOnly | 통과 | 최신 | 2026-11-22 01:59 | f38a35f | 23 | 388156 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -runFlowOnly | 통과 | 최신 | 2026-11-22 01:44 | f38a35f | 23 | 464750 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -seaOnly | 통과 | 최신 | 2026-11-22 01:48 | f38a35f | 23 | 700968 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -sfxOnly | 통과 | 최신 | 2026-11-22 01:58 | f38a35f | 23 | 307578 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -shoreOnly | 통과 | 최신 | 2026-11-22 01:55 | f38a35f | 23 | 161750 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -signatureOnly | 통과 | 최신 | 2026-11-22 01:51 | f38a35f | 23 | 895953 | 0:52 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -specOnly | 통과 | 최신 | 2026-11-22 01:44 | f38a35f | 23 | 483328 | 3:01 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -stage4Only | 통과 | 최신 | 2026-11-22 01:57 | f38a35f | 23 | 247234 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -threatOnly | 통과 | 예전 커밋 | 2026-10-13 01:02 | 565a99e | 11 | 916125 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -upgradeOnly | 통과 | 예전 커밋 | 2026-11-17 17:26 | 12be3eb | 10 | 158562 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -vlsOnly | 통과 | 최신 | 2026-11-22 01:54 | f38a35f | 23 | 72156 | 0:39 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -weaponPackOnly | 통과 | 최신 | 2026-11-22 01:53 | f38a35f | 23 | 27656 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2BattleOnly | 통과 | 최신 | 2026-11-22 02:03 | f38a35f | 23 | 635578 | 1:59 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2HeavyOnly | 통과 | 최신 | 2026-11-22 02:05 | f38a35f | 23 | 759500 | 0:36 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2Only | 통과 | 최신 | 2026-11-22 02:01 | f38a35f | 23 | 481203 | 2:38 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| 전체 | 통과 | 최신 | 2026-11-22 01:30 | f38a35f | 23 | 608375 | 13:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |

## 최근 실행 기록

| 실행 시각 | 검사 | 결과 | 커밋 | 소요 |
|---|---|---|---|---|
| 2026-11-22 14:14 | -helmGuideOnly | 통과 | f38a35f +40 | 0:12 |
| 2026-11-22 14:10 | -helmGuideOnly | 통과 | f38a35f +37 | 0:12 |
| 2026-11-22 14:08 | -helmGuideOnly | 실패 1 | f38a35f +37 | 0:12 |
| 2026-11-22 13:41 | -radarOnly | 통과 | f38a35f +27 | 0:18 |
| 2026-11-22 02:29 | -radarOnly | 통과 | f38a35f +25 | 0:16 |
| 2026-11-22 02:28 | -hitMapOnly | 통과 | f38a35f +25 | 1:34 |
| 2026-11-22 02:26 | -hitMapOnly | 통과 | f38a35f +25 | 1:34 |
| 2026-11-22 02:24 | -hitMapOnly | 통과 | f38a35f +25 | 1:35 |
| 2026-11-22 02:24 | -helmOnly | 통과 | f38a35f +25 | 0:25 |
| 2026-11-22 02:20 | -hitMapOnly | 실패 1 | f38a35f +23 | 1:34 |
| 2026-11-22 02:18 | -hitMapOnly | 통과 | f38a35f +23 | 1:34 |
| 2026-11-22 02:18 | -helmOnly | 통과 | f38a35f +23 | 0:25 |
| 2026-11-22 02:17 | -helmOnly | 통과 | f38a35f +23 | 0:25 |
| 2026-11-22 02:16 | -combatFxOnly | 통과 | f38a35f +23 | 0:17 |
| 2026-11-22 02:12 | -perfOnly (창 모드) | 통과 | f38a35f +23 | 4:03 |
| 2026-11-22 02:11 | -harborBuildingsOnly | 통과 | f38a35f +23 | 0:13 |
| 2026-11-22 02:09 | -balanceSimOnly | 통과 | f38a35f +23 | 2:19 |
| 2026-11-22 02:08 | -fireOnly | 통과 | f38a35f +23 | 0:24 |
| 2026-11-22 02:08 | -floodOnly | 통과 | f38a35f +23 | 0:19 |
| 2026-11-22 02:06 | -hitMapOnly | 실패 1 | f38a35f +23 | 1:46 |
| 2026-11-22 02:05 | -ww2HeavyOnly | 통과 | f38a35f +23 | 0:36 |
| 2026-11-22 02:03 | -ww2BattleOnly | 통과 | f38a35f +23 | 1:59 |
| 2026-11-22 02:01 | -ww2Only | 통과 | f38a35f +23 | 2:38 |
| 2026-11-22 02:00 | -burstOnly | 통과 | f38a35f +23 | 0:20 |
| 2026-11-22 02:00 | -helmOnly | 실패 1 | f38a35f +23 | 0:25 |
| 2026-11-22 02:00 | -hudOnly | 통과 | f38a35f +23 | 0:04 |
| 2026-11-22 01:59 | -routeOnly | 통과 | f38a35f +23 | 0:17 |
| 2026-11-22 01:58 | -harborBossOnly | 통과 | f38a35f +23 | 1:07 |
| 2026-11-22 01:58 | -sfxOnly | 통과 | f38a35f +23 | 0:04 |
| 2026-11-22 01:58 | -baseIslandsOnly | 통과 | f38a35f +23 | 0:10 |
| 2026-11-22 01:57 | -stage4Only | 통과 | f38a35f +23 | 0:41 |
| 2026-11-22 01:56 | -carrierBossOnly | 통과 | f38a35f +23 | 0:34 |
| 2026-11-22 01:56 | -polishOnly | 통과 | f38a35f +23 | 0:18 |
| 2026-11-22 01:55 | -shoreOnly | 통과 | f38a35f +23 | 0:19 |
| 2026-11-22 01:55 | -blastOnly | 통과 | f38a35f +23 | 0:23 |
| 2026-11-22 01:55 | -routeMapOnly | 통과 | f38a35f +23 | 0:03 |
| 2026-11-22 01:55 | -juiceOnly | 통과 | f38a35f +23 | 0:06 |
| 2026-11-22 01:54 | -vlsOnly | 통과 | f38a35f +23 | 0:39 |
| 2026-11-22 01:53 | -blockHitOnly | 통과 | f38a35f +23 | 0:28 |
| 2026-11-22 01:53 | -weaponPackOnly | 통과 | f38a35f +23 | 0:08 |
