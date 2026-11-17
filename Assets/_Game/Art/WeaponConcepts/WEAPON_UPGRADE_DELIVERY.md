# 무기 강화 외형 납품 (2026-10-11)

근거: 프로젝트 상위 폴더 `CODEX_무기강화외형_메모.md`. 원본 무기 프리팹, 모듈/강화 데이터, 전투 스크립트는 수정하지 않았다.

## 파일

- `Nobong_UpgradeVisuals.prefab`, `Howitzer_UpgradeVisuals.prefab`, `TorpedoTube_UpgradeVisuals.prefab`, `RamBow_UpgradeVisuals.prefab`: 각각 `Visual_Level1`, `Visual_Level2`, `Visual_Level3`를 포함한다. 기본형만 활성화돼 있다.
- `{Nobong,Howitzer,TorpedoTube,RamBow}_T{0,1,2}.prefab`: 단계별 단독 외형. 어뢰 T2는 방호판 대신 개방형 정비 장치.
- `Previews/*_stages.png`: 단계별 비스듬한 시점. `Previews/TopDownGunComparison.png`: 노봉·곡사포·76mm·기관포를 같은 카메라/축척으로 비교.
- 생성기 `Assets/_Game/Editor/WeaponUpgradeConceptBuilder.cs`, Unity 메뉴 `Naval/Art/Preview Weapon Upgrade Concepts`.

## 연결 소켓과 구성

- 노봉: 각 단계 `TurretPivot/ElevationPivot/Muzzle`(직접 자식), `Barrel`은 `ElevationPivot`의 별도 자식. 포신 중심 X = −0.14m, +0.14m. 기존 `barrelOffset=0.14m`와 맞는다.
- 곡사포: 각 단계 `TurretPivot/ElevationPivot/Muzzle`(직접 자식), 반동용 `Barrel`은 `ElevationPivot`의 별도 자식. 피벗은 수평 자세다.
- 어뢰: 각 단계 `TubeMount/LaunchPoint_01~03`. 세 발사관이 한 줄이고 발사점은 각 관의 앞(+Z)에 있다.
- 충각: 별도 소켓 없이 앞이 +Z. 완충기는 쐐기 뒤쪽 아래에 있으며 상부에는 짧은 점검 덮개만 보인다.
- 메시를 고정부/선회부/고각부/포신 또는 발사관 묶음별, 재질별로 결합했다. 콜라이더 없음.

## 치수 및 렌더러 수

단위 m. `가로 × 앞뒤 × 높이`는 렌더러의 월드 바운드. 앞 돌출은 모듈 앞면 `Z=+1.00` 기준. 메시는 렌더러 수이다.

| 무기 | 기본형: 치수 / 메시 / 재질 | 강화 I | 강화 II | 최대 앞 돌출 |
|---|---|---|---|---:|
| 노봉 | 1.70×2.50×1.01 / 12 / 5 | 1.70×2.50×1.01 / 12 / 5 | 1.70×2.50×1.43 / 14 / 6 | 0.65 |
| 곡사포 | 1.70×2.55×1.27 / 13 / 5 | 1.70×2.55×1.27 / 13 / 5 | 1.70×2.55×1.75 / 14 / 6 | 0.70 |
| 충각 | 1.70×1.98×0.52 / 4 / 4 | 1.96×1.98×0.52 / 4 / 4 | 1.96×2.08×0.52 / 5 / 5 | 0.23 |
| 어뢰 | 1.70×1.70×0.62 / 6 / 4 | 1.70×1.70×0.62 / 6 / 4 | 1.70×1.70×0.66 / 8 / 6 | 없음 |

모든 단계의 좌우·뒤 바운드는 격자 ±1.00m 안이다. 어뢰는 높이 0.90m 제한 안에 있다. 검증 로그: `Logs/weapon-upgrade-delivery-final.log`.

## Claude 연결 시

각 `*_UpgradeVisuals.prefab`의 `Visual_Level1~3`을 해당 `MOD_*.prefab`으로 옮겨 `ModuleUpgradeVisuals.levels`에 순서대로 할당한다. 기존 기본형 메시를 중복으로 남기지 않는다. 조준/발사 런타임 참조를 새 소켓으로 재연결하고 게임 안에서 강화 전환·포구/발사점·아이콘을 확인한다. 기존 강화 수치는 이 납품에서 바꾸지 않았다.
