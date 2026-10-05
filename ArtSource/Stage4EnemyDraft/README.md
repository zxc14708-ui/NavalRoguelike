# Stage 4 Enemy Fleet — ArtSource Draft

검토용 모델 산출물이다. Unity `Assets`, 프리팹, 스테이지 데이터와 코드에는 아직 연결하지 않는다.

## 포함 모델

| 파일 | 등급 | 명목 크기 | 식별 요소 |
|---|---|---:|---|
| `EnemyUnmannedAttackCraft.fbx` | 일반 | 5.5×1.75m | 낮은 무인 센서 시타델, 광학 센서, 양현 미사일 포드 |
| `EnemyMineLayer.fbx` | 일반 | 7.2×2.15m | 넓은 함미 작업갑판, 쌍열 기뢰 레일, 6개 기뢰, 투하 슈트 |
| `EnemyEwCorvette.fbx` | 엘리트 | 10.5×2.75m | 대형 측면 재머 패널, ESM 돔, 십자 간섭계, 최소 무장 |
| `EnemyAirDefenseFrigate.fbx` | 엘리트 | 12.5×3.15m | 4면 AESA, 16셀 VLS, 양현 SAM 발사대, 후방 CIWS |
| `EnemyAttackSubmarine.fbx` | 엘리트 | 9.5×2.32m | 함수 어뢰관, 세일·잠망경, 측면 소나, 펌프제트 |
| `EnemyCommandCruiserBoss.fbx` | 보스 | 24×5.15m | 전후 VLS, 통합마스트, 양현 대함 발사대, 이중 CIWS, 헬기갑판 |

## 공통 규격

- Blender `+Y`가 선수, `+Z`가 위다.
- 원점은 함 중앙의 흘수선이다.
- FBX는 `-Z Forward / Y Up / Apply Scalings: All Local`로 내보냈다.
- 단일 `<파일명>_Root` 아래 Mesh와 Empty만 포함한다.
- 콜라이더, 라이트, 카메라, 아마추어와 게임 스크립트는 포함하지 않는다.
- 기존 적 팔레트 이름 `Enemy hull grey`, `Stealth deck`, `Enemy weapon dark`, `Warning red` 등을 유지한다.

## 연결용 주요 소켓

- 무인 공격정: `PrimaryGunPivot/ElevationPivot/Muzzle`, `RadarPivot`, `MissileLaunchPoint_01~02`.
- 기뢰 부설정: `MineDropPoint_01~02`, `PrimaryGun*`, `RadarPivot`.
- 전자전 초계함: `AntennaPivot`, `AntennaPivot_Port/Starboard`, `EwEmitter_01~02`, `RearCIWS*`.
- 방공 호위함: `LaunchPoint_01~16`, `RadarPivot`, `SAMBank_*Pivot`, `RearCIWS*`, `AirDefenseOrigin`.
- 공격 잠수함: `PeriscopePivot`, `TorpedoLaunchPoint_01~02`.
- 지휘순양함: `PrimaryGun*`, `ForwardLaunchPoint_01~16`, `AftLaunchPoint_01~16`, `AntiShipBank_*`, `ForwardCIWS*`, `RearCIWS*`, `HelicopterLaunch`, `AAMount`.
- 보스 호환 일제사격 소켓: `MissileTube`, `MissileTube.001~.007`.
- 보스 검토용 위치: `Weakpoint_Radar`, `Weakpoint_ForwardVLS`, `Weakpoint_AftVLS`, `Weakpoint_C2`, `DamageFx_75/50/25`.

## 적용 전

현재 파일명과 소켓은 모델 준비 단계의 제안안이다. 사용자 검토 후 확정된 적 행동 코드에 맞춰 필요한 이름을 한 차례 조정하고, 그 다음에만 `Assets/_Game/Art/Models`와 프리팹에 연결한다.
