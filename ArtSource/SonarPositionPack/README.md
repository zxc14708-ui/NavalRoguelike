# 위치별 소나 모델 3종 — 검수용 / 게임 미적용

최근 편대·무장 아트와 같은 군함 회색, 무광 미니어처 스타일입니다.
현재 프로젝트 `Assets/_Game/Art/MODELING_SPEC.md`의 한 칸 2m,
실제 모듈 최대 1.8×1.8m, 높이 0.6–1.0m, 최대 900 tris를 따릅니다.

| 모델 파일 | 배치 역할 | 형태 |
| --- | --- | --- |
| `Models/MOD_Sonar_Bow.fbx` | 앞이 트인 선수 | 앞쪽 곡면 음향창, 낮은 밀폐 쐐기형 케이싱, 뒤쪽 정비 덮개 |
| `Models/MOD_Sonar_TAS.fbx` | 뒤가 트인 선미 | 케이블 윈치, 고정 베어링·브레이스, 뒤로 향하는 회수 프레임·케이블 출구, 분리 가능한 견인체 |
| `Models/MOD_Sonar_Internal.fbx` | 그 밖의 함내 자리 | 낮은 원형 센서 장비, 처리 장치함, 소나 스코프가 있는 경사 조작 콘솔 |

각각 FBX(게임용), GLB(참고용), 원점 정규화한 Blender 파일, 개별 미리보기를 제공합니다.
`Blender/*_Review.blend`와 `Three_Sonars_Review.blend`에는 검수용 카메라·조명이
있지만, `Models`의 FBX/GLB에는 모델 및 빈 소켓만 들어 있습니다.

## 원점·축·구조

- 모델 루트: 발자국 중심 `(0,0,0)`, Blender 바닥 Z=0, 단위 미터.
- Blender: +Y 선수 / −Y 선미 / +Z 위. FBX: −Z Forward / Y Up / All Local.
- 모든 메시의 회전·스케일은 적용됩니다. `CableExit`는 뒤를 보는 방향 소켓이므로
  로컬 Z 회전 180°가 의도적으로 있습니다. 회전부 축인 `CableDrumPivot`은 중립입니다.
- 모든 모델 공통: `SonarOrigin`, `ForwardMarker`, `AttachPoint`.
- TAS: `CableDrumPivot` 아래에는 드럼만, 크래들·베어링·회수 프레임은 고정 구조입니다.
- TAS: `StowedTowBody` 아래에는 견인체와 `SonarOrigin`만 있습니다. 이후 게임 코드에서
  견인체를 전개하더라도 거치대까지 딸려 움직이지 않도록 분리했습니다.
- TAS: `CableExit`와 `TowAttachPoint`는 선미 −Y 출구입니다.
  `TowDeployTarget`은 뒤쪽 `(0,-3,-0.65)`의 참고용 전개 위치이며 **메시가 아닙니다**.
  이 안내 소켓 때문에 모듈이 한 칸보다 커졌다고 해석하지 마세요.

## 적용 범위와 게임 규칙

**Unity 프로젝트, 기존 `MOD_Sonar.fbx`, 프리팹, 탐지 코드, 성능 수치는 변경하지 않았습니다.**
새 파일을 기본 소나 이름으로 덮어쓰지 마세요. 향후 위치 판정 코드가
선수/선미/함내에 맞는 시각 모델을 골라야 세 종류를 함께 사용할 수 있습니다.

사용자가 제시한 전방 ±70°, TAS 반경 1.5배, 전속 70% 이상일 때 반경 절반,
함내형의 짧은 전방위 탐지는 **향후 구현할 게임 규칙**이며, 이번에는 모델만 제작했습니다.
급선회 페널티와 함내형의 정확한 반경은 아직 정해진 수치가 없습니다.

## 실제 장비 참조와 게임용 단순화

- [Kongsberg SS2030 공식 자료](https://www.kongsberg.com/what-we-do/defence-and-security/naval/ss2030/):
  선체 부착 원형 센서·선체 장비 및 조작/처리 장치 구성의 참고.
- [Thales CAPTAS 공식 자료](https://www.thalesdsi.com/captas/):
  예인 장비와 여러 링으로 구성된 견인체의 참고.

특정 장비를 정확한 비율로 복제한 모델이 아닙니다.
실제 선체 소나 센서는 수중에서 작동합니다. 게임 카메라에서 장비를 구분하기 위해
선수형·함내형은 갑판에서 식별 가능한 **미니어처/절개도 표현**으로 단순화했습니다.
실제 선체 소나에도 360° 탐지형이 있으므로, 앞쪽 ±70°만 본다는 설정을
현실 소나의 공통 제한으로 설명하지 않습니다.

## 검증과 재생성

`manifest.json`: 크기·팔레트·삼각형 수·소켓 부모 관계.
`validation.json`: 별도 Blender 씬에서 FBX 재임포트한 독립 형상/소켓/회전 검사.
Unity 플레이 검증은 하지 않았습니다.

재생성은 `Sources/build_sonar_positions.py`, 독립 검증은
`Sources/validate_sonar_positions.py`를 Blender background에서 실행합니다.
두 스크립트 모두 `-- --output <패키지 폴더의 절대 경로>` 인자를 지원합니다.
미리보기 배치·카메라를 위한 회전은 모델 원본에 저장되지 않습니다.
