# 첫 미리보기 검사 검증

[English](../../verification/first-render-qualification.md)

[FR-01~04 확정 범위](../unity-render-qualification.md)를 **선택 범위 완료·제한 유지 (`scoped-complete-with-limitations`)**로 마감했다. 원시 증거는 `artifacts/storage-foundation/fr-20260927/`, 집계는 `qualification-summary.json`, 보존·회수와 VHD 분리 후 재검증은 `closeout.json`·`final-environment-check.json`에 있다. 작은 URP fixture의 검사 성공 계약을 닫았으며 Unity 내부의 첫 draw 차이 원인은 미확정이다.

| ID | 상태 | 증거 |
| --- | --- | --- |
| FR-01 | Passed, 제한된 원인 분리 | 기준 cold 첫/후속 PNG와 warm domain의 API/셰이더/연속 draw 비교. 초기 진단 화면 판독 정정과 native 원인 미확정 유지 |
| FR-02 | Passed, fixture 범위 | 단일 요청의 첫 유효 화면 채택·최대 2회·요청별 게시·픽셀/해시. 새 cold의 `[0,9457]`을 기록하고 유효 PNG 반환 |
| FR-03 | Passed, fixture 범위 | 새 cold·reload·정상 재시작 첫 요청 3건, 실패 주입 3종과 정상 복구 3건, 원래 씬·이전 PNG 보존 |
| FR-04 | Passed | 독립 결과 2개, 원본 90개 파일·기존 ref 25개·타 볼륨 snapshot 보존, 소유 자원 0, VHD 분리 뒤 결과 재검증 |

## 기준과 공통 실행 파일

제품 작업 기준은 `a1c3f91`, canonical fixture 기준은 `ae89b9f`다. 새 실행 원본은 기존 작은 Unity의 clean `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 추적 파일 90개와 기존 ref 25개이며 Library는 없다. 기존 결과의 파일/ref 수를 새 기준으로 재사용하지 않는다. 같은 새 소유 snapshot에 `fr27-baseline`과 `fr27-candidate`의 독립 delta를 두고 Editor를 순차 실행한다. Unity는 6000.3.22f1, URP는 17.3.0이다.

실제 두 작업은 JW에서 고정한 같은 CLI/Windows/Git/Core/Runtime 실행 파일을 사용한다(`binary-manifest.json`). 해당 파일은 JW 커밋 전 빌드라 informational version에 `091a5fc`가 남아 있다. `a1c3f91` 이후 재빌드와 PE hash는 다르며 8개 공통 관리 DLL의 메서드 본문/필드 값 hash는 일치했다(`post-commit-version-stamps.json`, `post-commit-method-body-comparison.json`). 이 비교는 PE 전체 hash 동등성 주장이 아니다.

표준 `scripts/build.ps1`의 locked restore·Release 빌드·시험은 **303 통과, 0 실패, 당시 제외 시험 1 skip**이다. 제한 환경의 첫 restore는 상세 진단 없이 exit 1이었고 권한이 허용된 동일 진입점 재실행이 통과했다. 두 로그와 TRX를 보존한다. 기준 Unity의 최초 준비와 제품 자동 시험이 일부 겹쳤으므로 최초 준비 시간을 성능 비교로 사용하지 않는다.

## 재현과 수정 근거

기준의 새 Library 첫 ready는 1,084.00초였다. 같은 Editor PID 22880의 첫 PNG는 배경만인데 기존 응답은 `success:true`였고, 다음 PNG에는 모델이 보였다. 메시 3개·텍스처 2048×2048·compile ready는 유효 화면을 보장하지 않았다.

그 뒤 같은 Editor에서 새 코드 값과 MVID의 domain reload마다 첫 draw를 비교했다. `Camera.Render`, 지원 확인 뒤 `Camera.Render`, `SingleCameraRequest`, 셰이더 비동기 옵션의 명시 설정과 연속 draw를 포함한 **진단 9건의 최종 PNG와 저장한 중간 pass PNG는 모두 모델이 있는 같은 이미지**였다. SHA-256은 `e208015437e22a9b9df0fa800e90dd0f57f61e238b4f667ad9c9fcd3dfe396dc`이며 기준 두 번째 PNG와 같다. pipeline은 관측 전후 URP였고 비동기 옵션의 관측값은 false였다. 이미 준비된 Editor의 진단은 cold의 빈 화면을 재현하거나 API/셰이더 변형을 구별하지 못했다.

중간 육안 판독에서 일부 진단 화면을 빈 화면 또는 연속 pass 차이로 분류한 것은 최종 원시 이미지·해시 대조로 정정했다(`diagnostic-evidence-reconciliation.json`). 원시 PNG는 변경하지 않았다. 따라서 API 교체나 셰이더 설정을 확정 원인으로 삼지 않는다. 실제 빈 화면 증거는 기준 cold 첫 PNG와 수정본 cold 요청의 전경 픽셀 `[0,9457]`이며, Unity 내부 원인은 미확정이다.

확인한 fixture 결함은 **compile-ready·메시 로드·PNG 저장을 화면의 성공으로 판정한 것**이다. 새 검사는 고정 단색 배경·별도 camera scene·URP 수동 요청을 사용하고, GPU target을 먼저 지운 뒤 실제 픽셀을 확인한다. 최대 2회 중 첫 유효 화면을 채택하고 회차별 계수를 기록한다. 둘 다 비거나 참조/배경/게시가 잘못되면 실패한다. 요청 ID에 묶인 새 PNG와 SHA-256을 게시하며 이전 결과는 보존한다. 현재 계약은 [미리보기 검사](../unity-render-qualification.md)를 따른다.

수정안을 같은 기준 Editor에서 새 코드 63·MVID로 로드한 첫 검사에서 모델과 전경 픽셀 9,457개, render pass 1회와 기존 씬 복원을 확인했다. 이 관측은 별도 새 Library 검증을 대체하지 않는다. 기준 종료의 첫 CLI 호출은 31.78초 뒤 `EditorStopTimeout`이었다. 이후 같은 Editor의 자연 종료·소유 프로세스/메모리 0을 재확인했으며 강제 종료하지 않았다. 기준 결과 `8f409c11f23bc146d1df56670d9595d6ea77de20`의 독립 bundle/저장소를 보존하고 mount/Git 등록을 해제했다.

## 수정본의 실제 첫 요청과 실패 경로

별도 candidate delta에 Library가 없음을 확인하고 공통 probe·render helper·meta의 SHA-256을 고정했다. 같은 세 파일을 canonical fixture와 두 독립 결과에서도 확인했다. canonical Editor는 이번 실행에서 열지 않았다.

재사용 fixture의 로컬 커밋은 `c154a7f7e38c09fb7f16373f8437e6549772af8b`다. 커밋 직전 staged 구현 3개 파일의 blob이 실제 candidate 독립 결과와 같음을 확인했으며, 시험용 코드 값 43/44/63은 canonical의 기본값 13에 반영하지 않았다.

| 조건 | Editor PID / 로드 코드 | ready 또는 변경 검증 | 첫 요청의 pass별 전경 픽셀 | 렌더·정리 시간 |
| --- | --- | ---: | --- | ---: |
| 새 private Library cold | 22580 / 43 | 첫 ready 1,057.09초 | `[0,9457]` | 2,893.67ms |
| 코드 변경/domain reload | 22580 / 44 | 편집→검증 완료 21.24초 | `[9457]` | 49.10ms |
| 정상 종료 뒤 재시작 | 46932 / 44 | 첫 ready 63.38초 | `[9457]` | 88.85ms |

각 조건은 새 요청 ID와 실제 projectPath·PID·generation·MVID를 확인했다. reload의 MVID는 `e7b70a7d-527f-4639-8c5c-33ed0df86e32`에서 `0bcfdb1c-7437-4679-b700-5da0f62651f5`로 바뀌었고, 재시작은 같은 코드/MVID와 새 PID·generation을 확인했다. 세 PNG 모두 640×480·모델 표시·SHA-256 `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`와 `sceneStateRestored:true`였다. 단일 요청 안에서 처음 유효한 화면을 채택했으며 cold 첫 draw 0을 숨기지 않았다. 원시 응답은 `candidate-{cold,reload,warm}-first-response.json`, PNG 내용/해시는 `preview-analysis.json`에 있다.

재시작은 provider를 계속 mount한 상태에서 Editor만 정상 종료/시작했다. stop은 29.78초였고 mount/replay 비교는 아니다. 후보 최종 stop은 31.77초 뒤 `EditorStopTimeout`을 반환했지만 로그와 실제 PID 부재를 확인한 뒤 같은 lease의 stop 재확인에서 소유 프로세스·working set·private bytes 0을 확인했다. 기준과 후보의 타임아웃 2건·최초 실패 응답을 보존했으며 강제 종료는 없었다.

| 실제 실패 주입 | 결과 | 복구·보존 |
| --- | --- | --- |
| 소유 씬 camera culling mask 0 | 두 pass `[0,0]`, `contains no visible geometry`, PNG 경로 없음 | 씬 raw 바이트 복원/import 후 유효 PNG, 임시 씬 복원 |
| 소유 씬의 model GUID를 누락 참조로 변경 | `Scene mesh references missing`, PNG 경로 없음 | 씬 raw 바이트 복원/import 후 유효 PNG, 임시 씬 복원 |
| 새 요청의 출력 경로에 기존 PNG 배치 | 씬을 열기 전 `Preview destination already exists`, PNG 경로 없음 | 기존 파일 hash 보존, 소유 충돌 파일만 제거한 뒤 새 요청 성공 |

세 복구 PNG는 정상 이미지와 같은 hash였다. `failure-checks.json`은 원래 씬 hash, 실패 주입 전 PNG 보존, 임시 게시 파일 0을 확인한다. 출력 충돌은 scene 작업 전 거부이므로 그 실패 응답의 `sceneStateRestored:false`를 scene 복원 실패로 해석하지 않는다. 모든 shader/렌더 pipeline/에셋 오류를 시험한 것은 아니다.

## 시간과 저장량

최초 준비와 재시작·렌더를 분리한다. 기준 1,084.00초와 후보 1,057.09초는 서로 다른 fixture 코드이고 기준 import는 제품 자동 시험과 일부 겹쳤다. 작은 표본의 준비 관측이며 성능 개선율로 환산하지 않는다.

| 분류 | 관측 |
| --- | --- |
| 기준 delta/control 최고 | 2,401,734,552B |
| 후보 delta/control 최고 | 2,294,144,368B |
| 두 작업+snapshot 영수증의 소유 파일 할당 최고 | 4,695,916,248B, 약 4.696GB; 두 독립 cold Library를 보존한 시험 전체 |
| 소유 파일 할당 회수 뒤 | 0B |
| 기존 결과 포함 공유 Git | 회수 전/후 6,145,760B; 생성 직전 기준은 unknown, 이번 증가량으로 사용하지 않음 |
| C:의 실행 묶음·PNG·bundle·독립 Git·닫힌 control 증거 | 회수 뒤 관측 33,018,224B; 이후 문서/영수증은 추가될 수 있음 |
| V: 공유 VSS, 회수 직전 | 사용 27,426,816B / 할당 100,663,296B |
| V: 공유 VSS, 회수 뒤 | 사용/할당 0B |

파일 수치는 `space-*.json`의 실제 할당 관측이며 순간 peak는 미계측이다. VSS는 볼륨 공유 값으로 두 작업에 나눠 귀속하거나 파일 값과 중복 합산하지 않는다. 원본 Library는 없었고 전체 Library 복사·읽기 hydration·저널 되감기는 수행하지 않았다. Library 물리 절감·대형 프로젝트의 용량 목표를 증명하지 않는다.

## 독립 결과와 회수

| 작업 | 독립 결과 | 보존 |
| --- | --- | --- |
| baseline | `8f409c11f23bc146d1df56670d9595d6ea77de20` | `baseline-result.bundle`·`baseline-result.git`, 최종 수정본 코드 63 |
| candidate | `6b94595b402683ca6028de95fd73888d13ee2c5b` | `candidate-result.bundle`·`candidate-result.git`, 최종 수정본 코드 44 |

각 결과는 probe/helper/meta/로드 코드의 4개 파일만 바뀐다. baseline 결과는 진단 후 설치한 최종 수정본이며, 최초 옛 검사와 진단 소스는 별도 `diagnostic-source/`·응답·PNG로 보존한다. bundle과 bare 저장소는 alternates 없이 생성했고 fsck·ref·변경 blob·bundle hash를 회수 전과 VHD 분리 후 확인했다.

소유 Editor 3회·provider 2개, mount·Git 등록·delta·소유 snapshot을 모두 회수했다. provider 종료 상태의 열린 핸들과 I/O 실패는 0이었다. 원본 clean HEAD·90개 추적 파일 raw hash·기존 ref 25개와 타 볼륨 snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`를 보존했다. 소유 결과 ref 4개는 추가로 유지한다. 제한 관리자 세션은 종료했고 VHD detached·V: 부재와 독립 결과 재검증을 확인했다. 기존 VHD 파일과 원본 fixture는 보존한다.

## 운영 기록과 판정 경계

- 제한 환경의 첫 제품 restore 실패와 허용된 동일 진입점 재실행, 실제 두 실행의 공통 frozen 바이너리와 post-commit version stamp 차이를 보존한다.
- 별도 관측용 `Get-Content` 1회에서 live provider state의 sharing violation이 났다. `FileShare.ReadWrite|Delete`로 재조회했고 provider I/O 실패는 0이었다. 실제 watcher/검사기는 공유 읽기를 사용했다(`observer-sharing-retry.json`).
- 기준/후보의 stop timeout 2건과 자연 종료 재확인, 진단 PNG의 중간 판독 정정을 유지한다. 이전 VC/VR/RU/WI/JW의 빈 PNG와 VR-03 Partial는 변경하지 않는다.
- 고정 작은 Unity 6000.3.22f1·URP 17.3.0의 첫 요청/실패 복구 계약이다. Unity 내부의 native 첫 draw 원인, 범용 이미지 의미 판정·모든 에셋/재질/플랫폼 지원과 제품 workspace 검증 API는 포함하지 않는다.
- 전체 코드 품질 검토·리팩토링·최적화에 넘길 경계는 마련했다. 제품 Runtime·flush·기본 모드·대형 PV와 Library 물리 절감은 이번 결과에서 승계하지 않는다.
