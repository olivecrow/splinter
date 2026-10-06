# 제품 validation의 새 cache 반복과 비용

WF-06~08은 **scoped-complete-with-limitations**다. 같은 최종 제품 바이너리에서 서로 다른 빈 cache 두 개의 A/B/A를 완료하고, 실행 중 원시 응답 schema·실제 코드/에셋/PNG·독립 결과·소유 자원 회수를 확인했다. 각 세대는 렌더 준비를 정상 종료한 뒤 같은 source/cache에서 다시 시작하여 최종 검증하므로, A/B/A당 Editor 시작은 6회다. 앞선 두 시도의 `ValidationSourceChanged`·`EditorStopTimeout`과 진단 복구는 따로 보존하고 최종 두 실행에 합산하지 않았다. t1 종료 검사 보정과 t2의 첫 Editor 시작 전 준비 요청 재시도도 아래에 기록한다. **SF-02/03 전체는 Partial**이며 대형 프로젝트의 공간 절감이나 기본 방식 채택은 판정하지 않는다.

## 입력과 재현 경계

- 제품 기준 `3573bba78d8382f42c2e32a60745274e7599f9fa`의 작업 트리에 정상 종료 대기 수정을 적용했다. 최종 소스/계약/스크립트/회귀 시험 152개와 바이너리 21개의 hash를 실행 전후 대조했다. 종료 대기는 단조 시간 기준 최대 2분이며 미저장 상태 거부·취소·timeout 시 보존을 유지한다. source sequence 검사와 flush 정책은 바꾸지 않았다.
- Windows x64 10.0.26200, Ryzen 5 5600X 6코어/12스레드, RAM 약 64 GiB, .NET SDK 10.0.112, Unity x64 `6000.3.22f1`이다. 작은 fixture 기준은 `58f81b5`, 추적 파일 90개·payload 6,235,231 bytes이며 원본 Library는 없다. probe/render의 세 파일은 `splinter-unity` `6a54974`의 실제 바이트를 worker에 넣은 뒤 게시했다.
- A의 코드 값/텍스처 설정은 23/1024, B는 33/2048이다. A 전용 파일은 B에서 없고, 각 세대의 `Validation*.txt`는 다음 세대로 넘어가지 않는다. worker A의 후속 편집 값 24는 후보 검증 성공을 승계하지 않는다. 두 실행에서 같은 후보 라벨의 입력 파일 hash가 일치하고 네 worker의 VSS ID는 모두 다르다.
- t1의 정상 실행은 앞선 두 실패 view를 명시적으로 닫은 뒤 **다른 빈 view/cache**에서 시작했다. t1 worker/snapshot과 불변 후보의 생성·게시에는 수정 전 제품을 사용했고, 그 입력을 보존하여 최종 제품의 새 cache 반복에 재사용했다. t2는 최종 제품으로 별도 home·새 worker/snapshot·빈 cache를 만들었다. 두 정상 A/B/A의 선택 이후 실행은 같은 최종 바이너리이며, 각 trial 안에서는 같은 경로/cache를 유지하고 정상 stop·mount 부재 뒤 재시작했다. cold는 Unity Library 부재이며 OS·사용자 공용 cache가 비었다는 뜻이 아니다.
- 기존 소유 NTFS VHD를 재사용했다. 원본 위치/파일시스템을 바꾸지 않았으며 최초 데이터 복제와 읽기 cache는 provider 관측상 0이다. 관리자 helper는 이 VHD와 고정된 네 worker의 VSS 작업으로 제한했다. 일반 CLI·Git·Unity는 비관리자로 실행했다.

원시 근거는 [실행 디렉터리](../../artifacts/storage-foundation/wf-cold-20260928/), [최종 후보 manifest](../../artifacts/storage-foundation/wf-cold-20260928/final-candidate-manifest.json), [전체 교차 검증](../../artifacts/storage-foundation/wf-cold-20260928/verified-run.json), [비용 JSON](../../artifacts/storage-foundation/wf-cold-20260928/cost-summary.json)이다. 이전 `candidate-manifest.json`, harness 두 이전 판과 최종 `run-trial.ps1`, 진단·회수 스크립트 및 raw JSON/PNG/TRX를 보존한다. 고정 ID·소유권·현재 파일이 전제인 실행 기록이며 그대로 재실행하거나 기존 증거를 덮어쓰지 않는다.

[PNG 시각 확인](../../artifacts/storage-foundation/wf-cold-20260928/preview-visual-review.json)과 [회수 후 증거 파일 해시 목록](../../artifacts/storage-foundation/wf-cold-20260928/evidence-manifest.json)으로 보존 파일을 식별한다.

## 두 빈 cache 검증 실행

| 실행 | 준비 시작→ready A1 / B / A2 (초) | 검증 재시작→ready A1 / B / A2 (초) | 소유 host 파일 할당 관측 최고 (GiB) | 회수 후 해당 home (GiB) | 채택 실행 / 실패·진단 포함 전체 (초) |
| --- | --- | --- | ---: | ---: | --- |
| t1 | 949.19 / 75.06 / 71.43 | 59.23 / 61.25 / 59.50 | 2.247 | 0.003 | 2025.28 / 5346.05 |
| t2 | 929.20 / 75.91 / 73.65 | 61.43 / 59.55 / 59.48 | 2.264 | 0.002 | 2170.19 / 2170.19 |

host 합계는 C:의 두 home·전체 실행 artifacts/독립 결과와 VHD backing 파일의 시험 시작 대비 증가량이다. 두 번째 실행의 합계에는 첫 실행의 보존 결과가 남는다. t1 정상 구간은 새 cache 준비부터이며 최초 ensure/publish와 실패·진단 비용은 전체 구간과 원시 명령 시간에 남긴다. Editor ready 이후의 PNG·설정 준비 종료와 재시작·최종 refresh·보존·회수도 전체 시간에 포함한다. ready만으로 최종 검증 완료를 뜻하지 않는다.

여섯 세대 모두 모델 3 mesh·3,001 vertices·2,441 triangles, 씬의 누락 mesh 0, 원래 씬 상태 복구를 확인했다. 640×480 PNG는 요청 경로 hash·파일 SHA-256·각 pass의 foreground pixel을 검사하고 첫 유효 이미지를 채택했다. 최대 두 번 규칙은 유지하며 Unity 내부 첫 draw 원인은 여기서 확정하지 않는다. `inspect-*.json`, `preparation-*.json`, `validated-*.json`, `observe-*-1.json`과 세대별 보존 receipt가 실제 코드·domain/MVID·후보 seal·output seal을 연결한다.

최종 두 실행의 원시 응답 schema 169건, 실제 입력/후속 payload 146건을 포함한 461건이 통과했다. 앞선 실패·진단까지 합친 전체는 응답 245건, 입력/후속 217건, 총 677건이다. 실행 중 출력한 원문을 검사했고 최종 두 실행의 Accepted 후속 status를 실제 호출했다. 이전 WF의 formatter 재직렬화 결과를 새 실행으로 세지 않았다.

- t1: CLI 응답 156회, Accepted 후속 status 13회. 실패 응답의 code별 횟수: SnapshotPrivilegeRequired 9회, EditorStopTimeout 1회, ValidationSourceChanged 1회. `SnapshotPrivilegeRequired`는 비관리자 CLI에서 고정 helper로 넘기는 권한 경계다.
- t2: CLI 응답 89회, Accepted 후속 status 9회. 실패 응답의 code별 횟수: SnapshotPrivilegeRequired 8회, InputUnavailable 1회. `SnapshotPrivilegeRequired`는 비관리자 CLI에서 고정 helper로 넘기는 권한 경계다.

## 추가 파일 할당과 측정 한계

| 시점 | 해당 home (GiB) | 그 중 cache journal (GiB) | 전체 증거·독립 결과 (GiB) | VHD backing 증가 (GiB) | 내부 Git 증가 (KiB, 별도 층) |
| --- | ---: | ---: | ---: | ---: | ---: |
| t1 a1-stopped | 2.131 | 2.119 | 0.039 | 0.000 | 34.61 |
| t1 b-stopped | 2.167 | 2.145 | 0.040 | 0.000 | 39.31 |
| t1 a2-stopped | 2.202 | 2.171 | 0.042 | 0.000 | 44.02 |
| t1 results-preserved | 0.003 | 0.000 | 0.053 | 0.000 | 60.06 |
| t1 released | 0.003 | 0.000 | 0.054 | 0.000 | 34.84 |
| t2 a1-stopped | 2.131 | 2.120 | 0.056 | 0.000 | 25.20 |
| t2 b-stopped | 2.166 | 2.146 | 0.057 | 0.000 | 29.91 |
| t2 a2-stopped | 2.202 | 2.173 | 0.058 | 0.000 | 34.61 |
| t2 results-preserved | 0.002 | 0.000 | 0.070 | 0.000 | 41.09 |
| t2 released | 0.002 | 0.000 | 0.070 | 0.000 | 15.91 |

파일 할당은 Windows `FILE_STANDARD_INFO.AllocationSize`를 5초 대기와 계측 시간을 합친 주기로 관측했다. worker·candidate·세대 source·cache journal·영수증/로그·TEMP/TMP·독립 bundle/bare Git·계측 artifacts를 겹치지 않는 root에서 읽었다. 실제 mount는 순회하지 않아 읽기 hydration을 유발하지 않는다. 공유 삭제를 허용한 handle을 사용한다. 불완전한 표본은 비용 JSON에 따로 남기고 최고치 계산은 완전한 표본으로 한다. t1의 불완전 표본 1개는 열기 전에 사라진 heartbeat 게시용 임시 파일 때문에 전체 합계에서 제외했다.

- t1: 표본 1046개 중 완전 관측 1045개, 전체 표본 사이 최대 간격 5.80초, 완전 표본 사이 최대 간격 10.30초. 파일 계측 1회 중앙값 99.81 ms, 누적 벽시계 112.41초. Editor 본체 working set 관측 최고 3.071 GiB, 제품 status의 소유 앱 그룹 관측 최고 4.304 GiB. V: 공유 VSS used/allocated 관측 최고는 2,703,360 / 34,209,792 bytes.
- t2: 표본 415개 중 완전 관측 415개, 전체 표본 사이 최대 간격 6.07초, 완전 표본 사이 최대 간격 6.07초. 파일 계측 1회 중앙값 227.26 ms, 누적 벽시계 96.78초. Editor 본체 working set 관측 최고 2.945 GiB, 제품 status의 소유 앱 그룹 관측 최고 4.232 GiB. V: 공유 VSS used/allocated 관측 최고는 2,473,984 / 34,242,560 bytes.

이 값은 **파일 할당의 관측 최고치**이며 모든 순간의 물리 최고치가 아니다. MFT/디렉터리 metadata, kernel/OS cache와 작업 전부터 공유하는 Unity 사용자 cache는 별도 계측하지 않았다. 기존 VHD backing 기준 할당 5,104,467,968 bytes는 새 사용량에 다시 더하지 않는다. V: 내부 Git과 VSS를 backing 증가에 중복 합산하지 않는다. VSS는 볼륨 공유량이며 `workspaceAttribution=unknown`; C:의 비소유 snapshot과 공유 VSS 관측도 원시 기록에 남겼다. 이 작은 입력과 두 표본으로 일반 성능 분포·대형 공간 절감·C/S/Q 우위를 주장하지 않는다.

## 첫 시도의 거부와 운영 개입

첫 cold import 자체와 코드/에셋/PNG·정상 stop은 진행됐지만, 검증 요청의 source sequence 2361 뒤 `ProjectSettings/ShaderGraphSettings.asset` 저장으로 stop 전 2397, stop 후 2405가 관측되어 `observe-validation`이 `ValidationSourceChanged`를 반환했다. 임시 directory-monitor 파일과 다른 **일반 설정 파일 쓰기**이므로 검사를 완화하지 않았다. 원본과 진단 후 설정 SHA-256은 같았지만 현재 계약은 요청 이후의 쓰기 sequence를 검사한다.

첫 실패와 원시 제어 파일을 보존하고 같은 세대를 명시적으로 start/refresh/stop했다. 같은 pending observe 요청의 재시도가 성공한 뒤 독립 결과를 보존하고 source/cache를 회수했다. 두 번째 빈 cache 시도에서 렌더를 먼저 실행하고 reload해도 ShaderGraph의 `OnDisable → Save` 때문에 sequence 2401→2413의 쓰기가 남았다. 또한 Editor는 정상 종료 코드 0으로 끝났지만 `exiting`부터 약 54.05초가 걸려 기존 30초 대기의 `EditorStopTimeout`이 반환됐다. 자연 종료 확인 후 같은 stop을 재시도하고 이 결과도 독립 보존·source/cache 회수했다. 두 번째 결과에는 validation 성공을 주장하지 않는다.

종료 대기를 단조 시간 기준 최대 2분으로 수정하고 네 자동 회귀를 추가했다. 미저장 상태 거부와 취소는 즉시 반영하며 timeout은 강제 종료·회수가 아니다. 최종 두 실행에서는 준비 Editor를 정상 종료·unmount한 뒤 같은 세대를 재시작해 최종 refresh를 수행한다. ShaderGraph 설정을 불러오는 렌더 준비를 검증 뒤 반복하지 않는다. 이전 두 실패의 결과를 최종 후보의 통과로 합산하지 않았고 t1 전체 비용에는 실패·진단·수정 검증 대기·새 cache 반복을 포함한다. 단순한 세 번의 Editor 시작으로 끝나는 A/B/A보다 추가 비용이 있으며 기본 모드 적합성의 제한으로 남긴다.

최종 t1의 제품 A/B/A·보존·회수 뒤에는 종료 검증 스크립트가 재사용 worker의 임시 branch 두 개를 기존 보호 ref로 잘못 계산하여 `OriginalChanged`를 반환했다. 원본 HEAD/index/90개 파일은 같았고, 빠진 ref는 해당 두 worker의 정상 release로 삭제되는 소유 branch뿐이었다. `trial-failure.json`을 보존하고 `source-audit-closeout.json`에서 첫 ensure 전의 기존 ref와 정확한 소유 branch를 대조했다. 제품·Editor 재시작이나 파일 구조 복구 없이 나머지 종료 관측을 완료했으며, 이 점검 시간도 t1 전체 구간에 포함한다.

t2에서는 첫 Editor 시작 전 `prepare-worker`의 정상 unmount 직후 `state.json` 읽기가 한 번 `InputUnavailable`로 실패했다. 이어 확인한 상태는 같은 소유 provider의 `unmounted`, I/O 실패 0이었으며 mount는 없고 journal은 보존돼 있었다. 같은 prepare 요청 원문을 재시도하여 성공한 뒤 게시와 빈 cache A/B/A를 진행했다. 제품/입력/제어 파일을 수동 복구하지 않았고 `preparation-retry.json`, 원시 실패·재시도·이어 실행한 스크립트와 그 비용을 남겼다. 순간 읽기 실패의 정확한 원인은 미확정이며 무오류 준비 경로의 증거로 사용하지 않는다.

제품 호출 전 PowerShell `File.Replace` backup 인자 오류와 진단 대기 스크립트의 heartbeat 생성 전 읽기도 `setup-deviations.json`에 남겼다. sandbox 밖 실행은 관리자 CLI 실행과 구분한다. 고정 VSS helper 요청의 시간/횟수, 모든 CLI 상태/시간과 schema 검사를 보존했다. timeout·Editor 종료를 discard 승인으로 사용하지 않았다.

## 독립 결과와 회수

두 정상 실행의 source A1/B/A2·candidate A/B·worker A/B **14개**, 앞선 두 시도의 source **2개**를 독립 bundle/bare Git에 보존했다. VHD를 분리한 뒤 16개 결과의 객체 존재·commit hash·코드/에셋·생성 파일을 확인했다. 원본 HEAD/index/90개 추적 파일과 기존 refs, 시작 때 있던 C: 비소유 snapshot은 유지됐다.

각 trial의 소유 앱/provider 프로세스·메모리·handle·mount·Git worktree 등록·source/cache/worker journal·V: snapshot은 0/부재를 확인했다. retained metadata와 독립 결과는 삭제하지 않았고 할당 표에 포함했다. `verified-after-detach.json`, `response-finish.json`, `session-stopped.json`, `sample-detached.json`, `final-environment.json`이 종료 근거다. VHD는 삭제하지 않고 분리했고 관리자 helper와 계측기를 종료했다.

VHD 분리 직후 남은 두 home의 합은 0.006 GiB, 당시 실행 증거·독립 결과는 0.070 GiB, VHD backing의 시작 대비 증가는 0.000 GiB다. 전체 계측 구간의 host 파일 할당 관측 최고는 2.264 GiB다. 계측 종료 뒤 작성한 집계 JSON·문서·최종 해시 목록은 이 종료 표본 이후의 작은 산출물이며 당시 할당으로 소급 계산하지 않는다.

최종 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·Release 빌드와 **515 Passed / 0 Failed / 1 Skipped**다. 당시 제외 시험 root가 없는 시험은 Skipped로 유지했다. 시험 전 자동 baseline과 이번 최종 TRX를 구별해 보존했다.

제품 기본 모드, 일반 임대 복구·생성 source 반환 병합·큐/Coordination, 설치·binding·discard·권한 서비스, 대형 PV와 NR의 재부팅/전원/공간/ARM A/B는 [남은 계획](../plans/storage-priority-and-editor-modes.md)에 있다. [기존 WF-01~05](product-validation-workflow.md)의 cold 중단·복구 기록은 이 실행으로 소급 변경하지 않는다.

## 단계별 CLI 시간

모든 시작의 CLI 진입부터 Editor 프로세스 생성까지는 아래와 같다. mount/replay와 CLI·소유권 검사·프로세스 시작 준비를 합친 구간이며 순수 replay 시간은 아니다. Accepted를 반환한 시도 기준이고, 선행 권한 요청·helper·재시도는 별도 명령 목록과 전체 벽시계에 포함한다.

| 실행·세대 | 준비 Editor 생성 전 (초) | 검증 Editor 생성 전 (초) |
| --- | ---: | ---: |
| t1 a1 | 3.52 | 8.50 |
| t1 b | 8.81 | 8.89 |
| t1 a2 | 9.30 | 9.19 |
| t2 a1 | 3.55 | 8.89 |
| t2 b | 9.34 | 8.97 |
| t2 a2 | 9.29 | 9.20 |

관측한 provider 세대의 내부 store 준비와 source/cache replay는 다음과 같다. 상태 표본이 있는 세대만 기록하며 전체 `start` 응답 시간·Editor ready 시간과 구별한다. PID는 Editor가 아닌 해당 mount provider다. 같은 provider 세대의 반복 표본을 중복 합산하지 않았다. 표본이 없는 세대의 세부 replay는 추정하지 않으며 모든 start 명령 시간은 아래에 보존한다.

| 실행·관측 라벨 / provider PID | store 준비 (초) | source replay (초) | cache replay (초) |
| --- | ---: | ---: | ---: |
| t1 a1-preparation-started / 59344 | 0.05 | 0.00 | 0.00 |
| t1 b-preparation-started / 71616 | 5.34 | 0.00 | 5.29 |
| t1 a2-started / 35524 | 5.98 | 0.01 | 5.92 |
| t2 a1-preparation-started / 61364 | 0.05 | 0.00 | 0.00 |
| t2 a1-started / 39620 | 5.54 | 0.01 | 5.47 |
| t2 b-preparation-started / 54704 | 5.78 | 0.00 | 5.73 |
| t2 b-started / 73520 | 5.71 | 0.01 | 5.65 |
| t2 a2-preparation-started / 61840 | 5.79 | 0.00 | 5.74 |
| t2 a2-started / 68804 | 6.03 | 0.01 | 5.97 |

Accepted start/refresh는 명령 응답 시간이며 Editor ready·검증 완료 대기와 다르다. snapshot/helper·ensure/publish·후속 status와 재시도는 `cost-summary.json`의 전체 명령/관리자 작업 목록에서 확인한다.

| 실행·명령 | 응답 시간 (초) |
| --- | ---: |
| t1 select-a1 | 2.11 |
| t1 select-b | 7.09 |
| t1 select-a2 | 8.11 |
| t1 start-a1 | 3.72 |
| t1 start-b | 8.99 |
| t1 start-a2 | 9.51 |
| t1 preparation-stop-a1 | 32.54 |
| t1 preparation-stop-b | 30.48 |
| t1 preparation-stop-a2 | 60.45 |
| t1 validation-start-a1 | 8.70 |
| t1 validation-start-b | 9.09 |
| t1 validation-start-a2 | 9.39 |
| t1 refresh-a1 | 3.39 |
| t1 refresh-b | 2.44 |
| t1 refresh-a2 | 3.13 |
| t1 stop-a1 | 30.29 |
| t1 stop-b | 31.89 |
| t1 stop-a2 | 31.88 |
| t1 preserve-source-a1 | 6.12 |
| t1 preserve-source-b | 6.13 |
| t1 preserve-source-a2 | 6.24 |
| t1 close-view | 13.31 |
| t1 release-worker-a | 2.34 |
| t1 release-worker-b | 2.31 |
| t2 select-a1 | 2.08 |
| t2 select-b | 7.92 |
| t2 select-a2 | 8.10 |
| t2 start-a1 | 3.74 |
| t2 start-b | 9.53 |
| t2 start-a2 | 9.51 |
| t2 preparation-stop-a1 | 33.18 |
| t2 preparation-stop-b | 31.67 |
| t2 preparation-stop-a2 | 30.79 |
| t2 validation-start-a1 | 9.10 |
| t2 validation-start-b | 9.16 |
| t2 validation-start-a2 | 9.39 |
| t2 refresh-a1 | 3.34 |
| t2 refresh-b | 3.05 |
| t2 refresh-a2 | 3.52 |
| t2 stop-a1 | 54.16 |
| t2 stop-b | 39.86 |
| t2 stop-a2 | 30.36 |
| t2 preserve-source-a1 | 6.49 |
| t2 preserve-source-b | 6.17 |
| t2 preserve-source-a2 | 6.34 |
| t2 close-view | 13.64 |
| t2 release-worker-a | 2.32 |
| t2 release-worker-b | 1.55 |
