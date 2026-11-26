# QA 결과표

자동 전투 검증(`CombatVerificationRunner`)을 `-qaTable <이 파일>`로 돌릴 때마다 고쳐 쓴다(점검 O03). 손으로 고치지 않는다.
- **기준**: 가장 최근 실행과 같은 커밋이면 `최신`, 아니면 `예전 커밋` — 지금 코드로 다시 확인하지 않은 결과다.
- **미커밋**: 빌드할 때 커밋 안 한 변경 파일 수(0이면 그 커밋 그대로). 커밋·미커밋은 빌드 옆 `build_stamp.txt`에서 읽는다.
- 자세한 내용은 각 실행의 `report.md`(출력 폴더).

## 검사별 최신 결과

| 검사 | 결과 | 기준 | 실행 시각 | 커밋 | 미커밋 | 시드(끝) | 소요 | 환경 | 첫 실패 |
|---|---|---|---|---|---|---|---|---|---|
| -artOnly | 통과 | 예전 커밋 | 2026-11-22 01:44 | f38a35f | 23 | 472656 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -aviationOnly | 통과 | 예전 커밋 | 2026-11-22 01:49 | f38a35f | 23 | 742671 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -balanceSimOnly | 통과 | 예전 커밋 | 2026-11-22 02:09 | f38a35f | 23 | 973421 | 2:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -baseIslandsOnly | 통과 | 최신 | 2026-11-26 19:44 | c5d7863 | 50 | 63265 | 0:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blastOnly | 통과 | 예전 커밋 | 2026-11-22 01:55 | f38a35f | 23 | 134140 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -blockHitOnly | 통과 | 예전 커밋 | 2026-11-22 01:53 | f38a35f | 23 | 39890 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -broadsideOnly | 통과 | 최신 | 2026-11-26 19:42 | c5d7863 | 50 | 923171 | 0:16 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleBOnly | 통과 | 예전 커밋 | 2026-11-22 15:08 | 0f8fbcb | 10 | 931875 | 0:16 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleCOnly | 통과 | 최신 | 2026-11-26 19:44 | c5d7863 | 50 | 48593 | 0:14 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -bundleDOnly | 통과 | 예전 커밋 | 2026-11-22 01:50 | f38a35f | 23 | 424242 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -burstOnly | 통과 | 예전 커밋 | 2026-11-22 02:00 | f38a35f | 23 | 448046 | 0:20 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -cardsNumbersOnly | 통과 | 예전 커밋 | 2026-11-22 01:49 | f38a35f | 23 | 774937 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -carrierBossOnly | 통과 | 최신 | 2026-11-26 21:54 | c5d7863 | 57 | 859890 | 0:34 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -carrierLaunchOnly | 통과 | 최신 | 2026-11-26 21:53 | c5d7863 | 57 | 792359 | 0:15 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -codexOnly | 통과 | 예전 커밋 | 2026-11-22 18:58 | 0f8fbcb | 174 | 704906 | 0:10 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -combatFxOnly | 통과 | 예전 커밋 | 2026-11-22 20:37 | 9a1ffd7 | 50 | 638750 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -dayNightOnly | 통과 | 예전 커밋 | 2026-11-22 20:51 | 9a1ffd7 | 50 | 482421 | 0:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -decorOnly | 통과 | 예전 커밋 | 2026-11-03 23:31 | ce3a323 | 30 | 755578 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -defenseOnly | 통과 | 예전 커밋 | 2026-11-21 23:45 | 3543688 | 116 | 339109 | 0:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -enemyOnly | 통과 | 예전 커밋 | 2026-10-13 01:01 | 565a99e | 11 | 884671 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortArmsOnly | 통과 | 예전 커밋 | 2026-11-22 01:47 | f38a35f | 23 | 669078 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -escortOnly | 통과 | 예전 커밋 | 2026-11-17 17:26 | 12be3eb | 10 | 167062 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -fieldEventsOnly | 통과 | 예전 커밋 | 2026-11-22 01:49 | f38a35f | 23 | 784453 | 0:40 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -fireOnly | 통과 | 예전 커밋 | 2026-11-22 02:08 | f38a35f | 23 | 942640 | 0:24 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -floodOnly | 통과 | 예전 커밋 | 2026-11-22 02:08 | f38a35f | 23 | 915859 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -formationOnly | 통과 | 예전 커밋 | 2026-11-17 17:14 | 8db6dae | 12 | 409375 | 1:11 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -growthOnly | 통과 | 예전 커밋 | 2026-11-22 18:54 | 0f8fbcb | 174 | 496218 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborAAOnly | 통과 | 예전 커밋 | 2026-11-22 01:53 | f38a35f | 23 | 2765 | 0:20 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBossOnly | 통과 | 최신 | 2026-11-26 19:43 | c5d7863 | 50 | 975593 | 1:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborBuildingsOnly | 통과 | 예전 커밋 | 2026-11-22 02:11 | f38a35f | 23 | — | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -harborOpsOnly | 통과 | 예전 커밋 | 2026-11-22 01:53 | f38a35f | 23 | 994343 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -helmGuideOnly | 통과 | 예전 커밋 | 2026-11-22 15:01 | 0f8fbcb | 5 | 524156 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -helmOnly | 통과 | 예전 커밋 | 2026-11-22 02:24 | f38a35f | 25 | 873453 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hitFeelOnly | 통과 | 예전 커밋 | 2026-11-22 01:49 | f38a35f | 23 | 766531 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hitMapOnly | 통과 | 예전 커밋 | 2026-11-22 02:28 | f38a35f | 25 | 101000 | 1:34 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -howitzerOnly | 통과 | 예전 커밋 | 2026-11-01 12:32 | c5376ef | 53 | 512328 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -hudOnly | 통과 | 예전 커밋 | 2026-11-22 02:00 | f38a35f | 23 | 409859 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -islandOnly | 통과 | 예전 커밋 | 2026-11-03 23:32 | ce3a323 | 30 | 785171 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -islandStuckOnly | 통과 | 최신 | 2026-11-26 19:29 | c5d7863 | 44 | 151921 | 1:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -juiceOnly | 통과 | 예전 커밋 | 2026-11-22 01:55 | f38a35f | 23 | 116203 | 0:06 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lifeOnly | 통과 | 예전 커밋 | 2026-10-31 13:39 | 594a21c | 23 | 407187 | 0:25 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -lunPatternOnly | 통과 | 예전 커밋 | 2026-11-22 01:52 | f38a35f | 23 | 962593 | 0:27 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -markersOnly | 통과 | 예전 커밋 | 2026-11-22 01:52 | f38a35f | 23 | 953203 | 0:05 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (배치 모드) | 통과 | 예전 커밋 | 2026-11-21 23:02 | 3543688 | 110 | 745343 | 4:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly (창 모드) | 통과 | 예전 커밋 | 2026-11-22 02:12 | f38a35f | 23 | 157359 | 4:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -perfOnly -perfMinutes 30 (배치 모드) | 통과 | 예전 커밋 | 2026-10-12 22:38 | 6356259 | 6 | 322156 | 31:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -polishOnly | 통과 | 최신 | 2026-11-26 21:53 | c5d7863 | 57 | 826703 | 0:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -radarOnly | 통과 | 예전 커밋 | 2026-11-22 13:41 | f38a35f | 27 | 670687 | 0:18 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeMapOnly | 통과 | 예전 커밋 | 2026-11-22 01:55 | f38a35f | 23 | 126609 | 0:03 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -routeOnly | 통과 | 예전 커밋 | 2026-11-22 01:59 | f38a35f | 23 | 388156 | 0:17 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -runFlowOnly | 통과 | 예전 커밋 | 2026-11-22 01:44 | f38a35f | 23 | 464750 | 0:13 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -seaOnly | 통과 | 예전 커밋 | 2026-11-22 01:48 | f38a35f | 23 | 700968 | 0:37 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -sfxOnly | 통과 | 예전 커밋 | 2026-11-22 01:58 | f38a35f | 23 | 307578 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -shoreOnly | 통과 | 예전 커밋 | 2026-11-22 01:55 | f38a35f | 23 | 161750 | 0:19 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -signatureOnly | 통과 | 예전 커밋 | 2026-11-22 01:51 | f38a35f | 23 | 895953 | 0:52 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -specOnly | 통과 | 예전 커밋 | 2026-11-22 01:44 | f38a35f | 23 | 483328 | 3:01 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -stage4Only | 통과 | 예전 커밋 | 2026-11-22 20:50 | 9a1ffd7 | 50 | 435656 | 0:41 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -supportOnly | 통과 | 최신 | 2026-11-23 22:20 | c5d7863 | 39 | 91828 | 0:28 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -threatOnly | 통과 | 예전 커밋 | 2026-10-13 01:02 | 565a99e | 11 | 916125 | 0:23 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -upgradeOnly | 통과 | 예전 커밋 | 2026-11-22 18:54 | 0f8fbcb | 174 | 488015 | 0:04 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -variantOnly | 통과 | 예전 커밋 | 2026-11-22 15:26 | 0f8fbcb | 27 | 13109 | 0:21 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -vlsOnly | 통과 | 예전 커밋 | 2026-11-22 01:54 | f38a35f | 23 | 72156 | 0:39 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -weaponPackOnly | 통과 | 예전 커밋 | 2026-11-22 01:53 | f38a35f | 23 | 27656 | 0:08 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2BattleOnly | 통과 | 예전 커밋 | 2026-11-22 02:03 | f38a35f | 23 | 635578 | 1:59 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2HeavyOnly | 통과 | 예전 커밋 | 2026-11-22 02:05 | f38a35f | 23 | 759500 | 0:36 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| -ww2Only | 통과 | 예전 커밋 | 2026-11-22 02:01 | f38a35f | 23 | 481203 | 2:38 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |
| 전체 | 통과 | 예전 커밋 | 2026-11-22 01:30 | f38a35f | 23 | 608375 | 13:42 | Windows 11  (10.0.26300) · NVIDIA GeForce RTX 3070 · 1920×1080 · Unity 6000.3.23f1 | — |

## 최근 실행 기록

| 실행 시각 | 검사 | 결과 | 커밋 | 소요 |
|---|---|---|---|---|
| 2026-11-26 21:54 | -carrierBossOnly | 통과 | c5d7863 +57 | 0:34 |
| 2026-11-26 21:53 | -polishOnly | 통과 | c5d7863 +57 | 0:18 |
| 2026-11-26 21:53 | -carrierLaunchOnly | 통과 | c5d7863 +57 | 0:15 |
| 2026-11-26 19:46 | -polishOnly | 통과 | c5d7863 +51 | 0:18 |
| 2026-11-26 19:44 | -baseIslandsOnly | 통과 | c5d7863 +50 | 0:11 |
| 2026-11-26 19:44 | -bundleCOnly | 통과 | c5d7863 +50 | 0:14 |
| 2026-11-26 19:43 | -harborBossOnly | 통과 | c5d7863 +50 | 1:06 |
| 2026-11-26 19:42 | -polishOnly | 실패 6 | c5d7863 +50 | 0:18 |
| 2026-11-26 19:42 | -broadsideOnly | 통과 | c5d7863 +50 | 0:16 |
| 2026-11-26 19:39 | -broadsideOnly | 통과 | c5d7863 +47 | 0:15 |
| 2026-11-26 19:37 | -broadsideOnly | 통과 | c5d7863 +47 | 0:15 |
| 2026-11-26 19:29 | -islandStuckOnly | 통과 | c5d7863 +44 | 1:42 |
| 2026-11-26 19:26 | -islandStuckOnly | 실패 1 | c5d7863 +44 | 1:49 |
| 2026-11-26 19:23 | -islandStuckOnly | 통과 | c5d7863 +44 | 2:00 |
| 2026-11-26 19:19 | -islandStuckOnly | 실패 3 | c5d7863 +44 | 1:51 |
| 2026-11-26 19:16 | -islandStuckOnly | 실패 2 | c5d7863 +44 | 1:54 |
| 2026-11-26 19:12 | -islandStuckOnly | 실패 3 | c5d7863 +44 | 1:52 |
| 2026-11-26 19:09 | -islandStuckOnly | 실패 2 | c5d7863 +44 | 1:52 |
| 2026-11-26 19:06 | -islandStuckOnly | 실패 4 | c5d7863 +44 | 1:53 |
| 2026-11-26 19:03 | -islandStuckOnly | 실패 4 | c5d7863 +44 | 1:54 |
| 2026-11-26 18:59 | -islandStuckOnly | 실패 5 | c5d7863 +44 | 2:01 |
| 2026-11-26 18:56 | -islandStuckOnly | 실패 5 | c5d7863 +44 | 2:01 |
| 2026-11-26 18:52 | -islandStuckOnly | 실패 6 | c5d7863 +44 | 2:16 |
| 2026-11-26 18:49 | -islandStuckOnly | 실패 3 | c5d7863 +44 | 2:00 |
| 2026-11-26 18:45 | -islandStuckOnly | 실패 5 | c5d7863 +44 | 2:11 |
| 2026-11-26 18:42 | -islandStuckOnly | 실패 4 | c5d7863 +44 | 2:02 |
| 2026-11-26 18:37 | -islandStuckOnly | 실패 7 | c5d7863 +44 | 2:09 |
| 2026-11-23 22:20 | -supportOnly | 통과 | c5d7863 +39 | 0:28 |
| 2026-11-23 22:18 | -supportOnly | 통과 | c5d7863 +39 | 0:28 |
| 2026-11-23 22:00 | -supportOnly | 통과 | c5d7863 +38 | 0:28 |
| 2026-11-23 21:58 | -supportOnly | 통과 | c5d7863 +38 | 0:28 |
| 2026-11-23 21:55 | -supportOnly | 실패 1 | c5d7863 +37 | 0:28 |
| 2026-11-23 20:02 | -supportOnly | 통과 | 9a1ffd7 +58 | 0:28 |
| 2026-11-23 20:01 | -supportOnly | 통과 | 9a1ffd7 +58 | 0:28 |
| 2026-11-23 20:01 | -supportOnly | 통과 | 9a1ffd7 +58 | 0:28 |
| 2026-11-23 19:59 | -supportOnly | 실패 1 | 9a1ffd7 +58 | 0:28 |
| 2026-11-23 19:58 | -bundleCOnly | 통과 | 9a1ffd7 +58 | 0:15 |
| 2026-11-22 20:51 | -dayNightOnly | 통과 | 9a1ffd7 +50 | 0:11 |
| 2026-11-22 20:50 | -stage4Only | 통과 | 9a1ffd7 +50 | 0:41 |
| 2026-11-22 20:37 | -stage4Only | 통과 | 9a1ffd7 +50 | 0:41 |
