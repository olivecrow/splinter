# 작은 Unity의 미리보기 검사

형제 `splinter-unity/Assets/Editor/SplinterQualificationProbe.cs`와 `SplinterQualificationRender.cs`는 선택 Qualification 씬을 검사하는 개발 fixture다. `-splinterControlPath`로 명시한 시험 Editor에서만 명령을 받으며 제품 workspace validation API가 아니다. 실제 실행 범위와 제한은 [FR 기록](verification/first-render-qualification.md)을 따른다.

## 요청과 성공 조건

소유 control의 `qualification-command.json`에 새 `id`와 `action: "inspect"`를 원자적으로 게시한다. `qualification-response.json`의 같은 `requestId`, 실제 `projectPath`·PID·세대·assembly MVID, `ready`와 `success`를 함께 확인한다. 응답 성공만으로 과거 fixture의 PNG를 새 렌더 증거로 승계하지 않는다.

새 검사 응답의 `assets.previewContract`는 `qualification-urp-v1`이다. `assets.previewPath`는 요청 ID의 SHA-256을 포함하는 새 PNG 경로이며 고정 `qualification-preview.png`를 찾지 않는다. 다음 필드는 실제 PNG와 함께 해석한다.

| 필드 | 계약 |
| --- | --- |
| `previewPath`, `previewSha256` | 성공적으로 게시한 요청별 PNG와 SHA-256. 기존 경로가 있으면 덮어쓰지 않고 실패 |
| `previewWidth`, `previewHeight` | 640×480 읽기 결과 |
| `foregroundPixels` | 고정 단색 배경에서 RGB 차이 합이 24를 넘고 alpha가 250 이상인 픽셀 수. 최소 64개 요구 |
| `renderPipelineBefore`, `renderPipeline` | 요청 전과 지원 확인 후의 실제 pipeline 타입. 초기 없음은 `uninitialized` |
| `renderPasses`, `renderMilliseconds` | 최대 두 번의 동기 렌더 요청과 자원 정리까지의 경과 시간. Editor ready/import 시간과 별도 |
| `foregroundPixelsPerPass` | 실제로 읽은 각 pass의 전경 픽셀 수. 첫 빈 pass도 보존하며 두 번 모두 비면 실패 |
| `sceneStateRestored` | 임시로 연 Qualification 씬을 닫은 뒤 기존 씬 수와 활성 씬이 복원됨 |

메시/텍스처 import와 씬의 mesh reference를 확인한 뒤 URP `SingleCameraRequest` 지원을 확인하고 `SubmitRenderRequest`로 렌더한다. 렌더 중에만 비동기 셰이더 컴파일을 끄며 기존 값을 복원한다. 매 pass 전에 임시 target을 투명 색으로 지워 이전 요청의 GPU 픽셀을 재사용하지 않는다. 고정 배경과 모서리 검사를 통해 화면을 확인하며 첫 pass가 비면 같은 요청에서 한 번만 더 렌더한다. 각 pass의 `foregroundPixelsPerPass`를 보존하고 둘 다 비면 실패한다. 이 검사는 정해진 fixture의 빈 화면 탐지이며 범용 이미지 의미 판별이나 모든 재질의 시각적 정확성 보장이 아니다. 실제 모델 표시 여부는 저장된 PNG로도 확인한다.

씬은 별도의 additive 인스턴스로 열고 카메라의 scene을 명시한다. 이미 Qualification 씬이 열려 있거나 URP가 아니면 실패한다. 기존 미저장 씬을 닫거나 저장하지 않는다. 렌더를 시작한 요청은 성공·실패 모두 임시 이미지/RenderTexture/씬과 전역 active RenderTexture·셰이더 컴파일 설정을 정리/복원한다. 출력 충돌 등 렌더 전 거부는 `sceneStateRestored:false`의 기본값을 반환하며 씬을 건드리지 않는다. 결과 파일은 같은 디렉터리의 새 임시 파일을 flush한 뒤 덮어쓰기 없는 move로 게시한다. 게시 오류는 검사 실패이며 이전 PNG를 성공 결과로 재사용하지 않는다.

## 제품 경계

[snapshot-spike](snapshot-overlay.md)의 `unity-refresh`는 요청/fingerprint/compile 성공과 로드 코드를 확인한다. fixture의 `inspect`는 해당 Editor에서 모델·텍스처·PNG를 따로 검사한다. 두 성공 조건은 서로 대체하지 않는다. Core/Runtime journal·flush·Windows callback과 제품 schema/capabilities에는 이 fixture 명령을 추가하지 않는다.

지원 API는 Unity 6000.3의 [URP 렌더 요청](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/User-Render-Requests.html)을 따른다. 새 cold/정상 재시작/reload와 실패 경로의 실제 검증 여부는 FR 실행 기록을 확인한다.
