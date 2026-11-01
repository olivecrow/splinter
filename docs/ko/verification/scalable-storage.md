# 대규모 저장 검증

[English](../../verification/scalable-storage.md) · [계획](../plans/scalable-storage.md) · [구현](../scalable-storage.md)

상태: **InProgress**. 후보는 준비·실행 중 인덱스·고정 수집·독립 결과·원본 반영·회수 전체에 task v4·관리형 basis v3·제품 overlay v3를 연결했다. 이전의 기반 구성 요소만 구현된 상태를 대체하지만 SCAL-01–08 완료는 아니다. 기준 소스는 development `b68eaaa9e7e78930ff7b4b0b368f137bc3e528c6`이다. 과거 RI/GCO/MX 통과는 새 후보 자격을 대신하지 않는다.

## Gate 원장

현재 metadata 저장소 계열은 `SPM2`(ST-44–48)이며 외부 task/basis/overlay 버전이 같아도 이전 `SPM1` 저장소는 보존·거부한다. 레코드 바이트 검사와 작은 native 복구 검증은 [처리량 후속](scalable-storage-throughput-followup.md#st-4448-metadata-레코드-중복-검토)이 소유한다. 아래 보존 대형 입력/전체 gate 자격을 재실행하거나 승계하지 않는다.

2026-11-01 사용자가 남은 검증 축소와 느린 구간 기록을 지시했다. 대형 입력은 작업·기준본·결과·미완료 상태를 보존하고 제한 종료했다. 소유 provider 7개는 각각 인증된 정상 Stop 한 번으로 종료했다. 읽기 전용 None 계획과 Plastic B 초기 준비만 중단했고 10만 항목 원본 게시는 시작하지 않았다. 실제 Git 고정 수집 CLI 중단은 같은 결과와 후속 편집을 보존해 복구했다. 이는 **SCAL-01–08 전체 완료가 아닌 제한 마감**이다. [느린 구간 진단](scalable-storage-throughput-diagnosis.md)을 따른다. 실제 17GiB·세 입력의 10만 항목 전체 수명주기·정상 ABBA·최신 전체 build gate는 해당 지시로 생략했으며 통과나 과거 근거로 대체하지 않는다.

아래 “Passed”는 기록한 자동/로컬 파일 시험 범위에 한정한다. 실제 공급자/Unity 수명주기·비용은 별도 gate다. [통합 기계 판독 근거](../../../reports/benchmarks/scalable-storage-integrated-20261101.json)는 시험과 실행을 연결한다. 중복되는 실행의 통과 수를 합산하지 않는다.

| ID | 상태 | 근거와 남은 의무 |
| --- | --- | --- |
| SCAL-01 | Passed (automated scope) | 논리 identity·고정/재연결 root·손상/잘림/순환/전방 참조·중복·native 교체·구형 상태 보존/거부 시험. 최종 소스 전체 gate는 SCAL-08에 남음. |
| SCAL-02 | Passed (fixed catalog matrix) | 계층형/단일 디렉터리/긴 접두사와 1,024 / 32,769 / 262,145 / 1,048,576항목 12조합의 스트리밍 생성·전체 열거·조회·비교/재사용·재연결 통과. 실제 프로젝트 파일 100만 개가 아닌 catalog 입력이다. |
| SCAL-03 | Partial | 제품 영속 인덱스 모델이 node/whiteout 250,001개·extent 1,000,001개를 넘겼고 32,769항목 계획/복원·64KiB 초과 제어 파일 통과. 실제 큰 파일/저널 예산 초과는 64비트 논리 offset 모델과 별도이며 아직 자격을 확보하지 못했다. |
| SCAL-04 | Passed (automated/local scope) | 새 overlay의 고정 재시도·동시 batch·강제 provider 종료 복구·후속 쓰기/rename/delete/unlink/truncate·디렉터리 basis·교체·채택·선택 반영이 로컬 시험에서 통과했으며 current17의 추가 overlay3 행을 포함한다. Overlay2 통과를 승계하지 않으며 실제 source 자격은 SCAL-07에 남는다. |
| SCAL-05 | Passed (automated/local NTFS scope) | 원본 후속 편집·불변 과거 결과·canonical 내용 공유·현재/미완료 수집 참조·reader fence·마지막 참조의 소유 회수 통과. 실제 세 공급자/Unity 근거는 SCAL-07에 남음. |
| SCAL-06 | Passed (automated/local NTFS scope) | 새 형식 killed-provider 수집 경계·root/수집·결과/이력·부분 게시·metadata/segment/packing/회수 중단과 공간 부족 주입 통과. worker 실패는 drain하고 고정 요청을 재시도한다. Coordinator actor와 제품 CLI/실제 source 실행은 아래에서 구분하며 최종 소스 전체 gate는 SCAL-08에 남음. |
| SCAL-07 | Partial; 사용자 검증 축소로 제한 마감 | Git/None 10만 A/B 준비와 Plastic A 준비 통과. None은 99,990파일 편집·고정 수집1회, Git은 실제 고정 수집 CLI 중단 복구 통과. 10만 전체 게시·선택·회수는 미실행. 작은 실제 Unity 세 입력의 수명주기·별도 정리는 기록한 복구·혼합 고정 바이너리를 포함해 통과. 검증 provider7개는 정상 종료했고 작업·기준본·결과는 회수하지 않고 보존. |
| SCAL-08 | Partial; 사용자 검증 축소로 제한 마감 | 세 입력의 기준 trial0은 있지만 정상 ABBA·작은 입력 성능 유지는 미입증. 지시 후 전체 build/test gate는 새로 실행하지 않음. Current30/R19의186통과/0실패/actor진입점 skip1은 focused gate이며 소스·계약·시험548개 해시가 일치한다. 검증 스크립트 구문·최종 문서 검사는 별도다. 비용·미측정 경계는 진단 문서에 기록. |

## 실행 단위

최종 축소 검사는 새 시험 실행 단위가 아니다. `minimum-closeout-source-script-check-20261101.json`에서 current30 소스/계약/시험548개 해시 일치·검증 스크립트16개 구문 유효를 확인했다. 기존 R19 focused gate의 정확한 재사용 근거일 뿐 새 전체 시험 통과 주장이 아니다. 과거 실패한 전체 gate는 아래에 그대로 기록한다.

종료한 10만 입력은 `C:/Users/olive/AppData/Local/Temp/sscal1g`·`sscal1n`·`sscal1p`다. Git A/B·None A/B/원본·Plastic A/B provider는 정상 종료했다. release/discard와 None project restore는 실행하지 않아 작업 record·delta·고정 basis·부분 소유 검토 artifact·성공 결과는 각 home에 남는다. None의 빈 `evidence/apply-1.json`은 사용자 지시를 반영한 명시적 harness barrier이며 CLI 응답이 아니다. 이후 복구 전 통합 보고서와 입력별 종료 영수증을 확인한다.

아래 경로는 `artifacts/scalable-storage-tests/`의 artifact 이름(`.trx`, `.log`)이며 시간은 실행 로그 기준이다. 각 행은 독립 실행이다. current5 전체 회귀는 이후 packing·긴 제어 파일·proof·잔여 복구 변경보다 앞서며 **최종 gate가 아니다**.

| Artifact | 전체 | 통과 | 실패 | Skip | 초 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `catalog-matrix-20261101` | 12 | 12 | 0 | 0 | 655.925 |
| `integrity-reconnect-current5-20261101` | 124 | 124 | 0 | 0 | 79.781 |
| `regression-current5-20261101` | 2,273 | 2,237 | 0 | 36 | 684.030 |
| `packing-sharing-current6-20261101` | 75 | 51 | 24 | 0 | 71.090 |
| `packing-basis-current7-20261101` | 173 | 173 | 0 | 0 | 75.576 |
| `native-candidate-current8-20261101` | 138 | 138 | 0 | 0 | 42.347 |
| `restore-inventory-query-current9-20261101` | 17 | 17 | 0 | 0 | 500.572 |
| `long-control-query-current10-20261101` | 26 | 25 | 1 | 0 | 36.948 |
| `native-initialization-reopen-20261101` | 29 | 29 | 0 | 0 | 6.200 |
| `focus-current12-20261101` | 94 | 81 | 12 | 1 | 68.765 |
| `focus-current13-20261101` | 27 | 16 | 11 | 0 | 66.099 |
| `focus-current14-20261101` | 23 | 23 | 0 | 0 | 24.021 |
| `source-fault-current16-20261101` | 51 | 49 | 0 | 2 | 59.253 |
| `full-current17-20261101` | 2,372 | 2,323 | 12 | 37 | 2,306.080 |
| `native-apphost-current18b-20261101` | 27 | 27 | 0 | 0 | 268.095 |
| `native-apphost-current19-20261101` | 110 | 110 | 0 | 0 | 1,052.659 |
| `native-apphost-current20-20261101` | 28 | 28 | 0 | 0 | 103.428 |
| `full-current22-20261101` | 2,412 | 2,372 | 3 | 37 | 2,067.872 |
| `native-apphost-current23-20261101` | 221 | 221 | 0 | 0 | 86.571 |
| `native-apphost-current24-20261101` | 24 | 24 | 0 | 0 | 1.553 |
| `native-apphost-current25-20261101` | 34 | 33 | 0 | 1 | 128.098 |
| `native-apphost-current26-cache-20261101` | 50 | 50 | 0 | 0 | 25.441 |
| `native-apphost-current27-20261101` | 107 | 106 | 0 | 1 | 99.581 |
| `native-apphost-current28-20261101` | 85 | 84 | 1 | 0 | 92.784 |
| `native-apphost-current28-git-20261101` | 35 | 35 | 0 | 0 | 92.211 |
| `native-apphost-current29-20261101` | 187 | 185 | 1 | 1 | 93.575 |
| `native-apphost-current30-20261101` | 187 | 186 | 0 | 1 | 93.003 |

과거 [기반 보고서](../../../reports/benchmarks/scalable-storage-foundation-20261101.json)는 구성 요소 단계의 전체 2,100개/2,064 통과/36 skip gate·앞선 child 핸들 공유 실패·hash·한계를 보존한다. 그 형식/미완성 구현은 당시 단계를 뜻하며 현재 후보가 아니다. 해당 gate나 current5가 최종 후보 gate를 대신하지 않는다.

## 근거의 경계

- `ScalableCatalogMatrixTests`: 네 크기 × 세 형태. 긴 접두사는 Unicode 디렉터리 component 8개이며 파일시스템 최대 경로 프로젝트가 아니다. 불변 내용을 다시 생성하면 페이지를 재사용하고 재연결 후 전체 열거한다. 계상 프로세스 metadata 캐시는 32MiB 이내이며 전체 private bytes·peak RAM 측정이 아니다.
- `FormerJournalTotalBoundaryTests`: 실제 제품 영속 root를 스트리밍 생성해 private 파일 250,001개·hidden 경로 250,001개·실제 고정 payload 1바이트를 공유하는 extent 1,000,001개를 담는다. 제품 checkpoint/재연결·마지막 offset 읽기·추가 생성/삭제·truncate/재쓰기가 통과한다. 영속 checkpoint 모델이며 개별 durable ACK 100만 번/실제 파일 100만 개가 아니다. 별도로 `PagedManagedDirectoryRestoreTests`는 작은 basis에서 **실제 NTFS private 파일 32,769개**를 생성/복원하고 파일 하나의 private 범위 1,025개를 복원한다. WinFsp mount·Unity 없는 중지 native 파일 시험이다.
- `PagedJournalTests`/`JournalRangeAdoptionTests`: 불변 root·후속 편집 뒤 같은 요청 재시도·제어 파일 누락/잘림 뒤 접수된 freeze 복구·reader 보호 완료·인덱스 회수 뒤 materialized 영수증. 수집 freeze/완료·증명한 범위 채택·저장 위치 변경은 접수된 전체 논리 seal을 보존하고 새로운 접수 쓰기는 바꾼다. 물리 삭제에는 전체 head/container/native identity/hash/inventory proof가 계속 필요하다. 작은 header 조회가 constructor의 전체 root proof와 reader lease를 대체하지 않는다.
- Current16은 overlay3의 `capture.freeze-accepted`·`capture.intent-published`에서 실제 child를 강제 종료하고 정확한 PID/시작 시각/nonce·복구한 native pin identity·고정 요청 digest·후속 편집·다음 결과·reader 보호 완료를 검증했다. Overlay3 접수 batch 행도 통과했다. Skip 2행은 통과한 부모 시험이 소유 subprocess로 실행하는 actor 진입점이다. 별도로 `GenerationProcessRecoveryTests`의 task4/plan3 coordinator actor 13행은 current5에서 통과했으며 통제한 backend actor를 제품 CLI 실행으로 읽지 않는다. 실제 CLI 중단 자격은 native 수명주기와 별도로 기록한다.
- `OriginalApplyPagesTests`/`RepeatedOriginalApplyTests`/`ParallelOriginalApplyTests`: 32,769항목 계획·페이지 선택/이력/진행·선택/되돌림/삭제·원본 후속 편집·완료 요청 재시도·전체 대상 admission·최대 8 worker·최대 단일 파일 준비량의 reservation·실패 drain. 통제한 source/backend fixture와 실제 로컬 NTFS 게시이며 실제 VCS 세 종류의 수명주기가 아니다.
- `LiveCapturedContentReclamationTests`와 내용/범위 저장소 시험은 내용을 공유하면서 불변 과거 결과를 보존한다. 기준 내용 복귀/같은 전체 파일 identity의 다른 구성으로 과거 descriptor에 포함되지 않는 canonical slice도 현재 source와 미완료 capture root가 보호한다. 마지막 보호 참조가 사라진 소유 데이터만 회수하며 수정 가능한 원본 경로를 불변 저장의 대체물로 쓰지 않는다.
- `JournalPartialSegmentPackingTests` 19행 전체 통과: 실제 16MiB 고정 payload를 반복해 1바이트만 남기기·정확한 prefix/suffix packing·gate 밖 복사 중 동시 write/truncate/rename/capture·미완료 수집/reader 보호·10단계 중단·내용 같은 foreign 교체 거부·metadata GC/일반 roll 재생·64비트 offset. 27MiB 사례는 고정 디스크 계획 하나를 여러 대상으로 끝까지 처리하며 16MiB는 처리 단위다. 미완료 요청이 끝날 때까지 COW 임시 metadata가 쌓일 수 있어 일정한 임시 디스크 사용량을 주장하지 않는다.
- `PagedMetadataCollectionTests`·`GenerationStatePublicationRecoveryTests`·범위/packing/게시 fault 시험은 영속 root·payload/결과/이력 게시·원본 부분 쓰기·위치 변경·회수 경계를 검증한다. 같은 요청 복구는 고정 결과와 후속 편집을 구분하며 미완료 intent가 다른 요청을 막는다. `SegmentedJournalReclamationRecoveryTests`는 잘린 미게시 inventory prefix 네 경우를 검증한다. I/O/공간 부족 주입과 재연결은 provider 강제 종료·실제 디스크 채우기·OS 전원 장애와 구분한다.
- `JournalLongControlTests`: 실제 약 12K UTF-16 길이 확장 NTFS root·64KiB를 넘는 제어 파일/inventory header·codec 전용 약 32K root. 검증한 chunk bundle이 제어/고정 child root를 보존한다. `PagedMetadataVerificationTests`는 checksum이 유효한 순서/중복/순환/전방 참조 손상과 별도 조회/rank 적합성을 검증한다. 선형 전체 cursor proof는 페이지 CRC/논리 hash/subtree count·순서/중복·최종 개수 검사를 유지한다.

## 실패한 시도와 정확한 복구

Current6은 서명한 segment 회수 계획을 게시한 뒤 메모리의 서명 없는 계획을 다시 검증한 결함을 드러냈다. 이제 영속 서명 계획을 다시 읽으며 current7/current8 관련 복구/packing 행이 통과했다. Current10의 query 실패는 query 시험의 JSON property 조회였고 수정한 초기화/query 실행 29행이 통과했다.

Current12/13 인덱스 보존 복구는 NTFS 디렉터리 이동의 native error 5로 실패했다. Share-delete 하위 핸들과 그다음 읽기 전용 marker만으로는 해결되지 않았다. Current14는 정확한 디렉터리/부모 guard를 유지하고 원자적 native 이동 직전에 하위 핸들을 해제한 뒤 목적지 writer/reader 배제를 다시 얻어 모든 inventory 이름/native identity/hash를 재검증한 다음 알려진 소유 metadata만 삭제한다. 짧은 인계 경계에 들어온 reader는 이동/admission을 막는다. 같은 요청 복구에 `index-preserve.descendants-released`를 포함했고 current14 23행 모두 통과했다.

Current15는 새 spine 시험 네 호출이 analyzer가 요구하는 cancellation token을 생략해 시험 전에 멈췄고 수정한 current16 Release build는 경고/오류 0개다. Current16에서 native-owned 깊은 생성 spine의 I/O 실패·실제 child 강제 종료 복구도 통과했다. Private create-new spine scratch는 정확히 열린 핸들의 delete-on-close로 지워 이전 root/native container identity를 보존하며 후속 회수를 막는 unknown `build-*.pending`을 남기지 않는다. 교체한 파일을 경로로 삭제하지 않는다. 최종 전체 gate는 남아 있다.

가능한 빈/헤더 크기 퇴역 인덱스 생성 gap은 활성 root proof 뒤에만 바이트 그대로 소유 외부 잔여 원장으로 이동한다. payload를 가진 unknown은 계속 거부한다. 불명확 identity/바이트를 채택/삭제하지 않는다. 원장/CLI는 보존 파일·미완료/미게시 제어·검증 불가 inventory를 구분하며 완료 전에 잔여 namespace를 재검사한다.

첫 실제 현재 형식 Git 초기화 `48dbbaff824a9c07c427f5ed`는 native pin 주입 누락으로 attach/접수된 source 쓰기 전에 실패했다. 잘못된 디렉터리/identity/hash를 보존하고 정확히 같은 task/요청으로 복구·회수했다. 현재 초기화는 provider 재연결 전에 native head/container 소유권을 영속화한다. 실패/복구 시도는 정상 비용에서 제외하며 비소유 prefix를 삭제/채택해 대체하지 않는다. `artifacts/scalable-storage-tests/first-native-initialization-preservation.json`을 따른다.

Current17 전체 gate는 대규모 입력 검증과 동시 실행 중 실패했다. 6행은 깊어진 apphost scratch root에서 native 경로에 확장 접두사가 없는 결함, 2행은 선택 반영 시험이 최초 실행에 검토한 경로 범위를 생략한 문제였다. 이를 수정한 current18b의 native apphost 27행은 디렉터리 packet 열거·Git subprocess 취소·긴 native 경로를 포함해 모두 통과했다. 나머지 subtree reader 실패 1행·fixture Git timeout 2행·process cleanup 접근 실패 1행은 별도 실행 재검증을 위해 보존한다. Current17은 최종 gate 통과가 아니다.

Current19의 native apphost 110행은 current17 실패 12행 전체와 강제 GC 없는 source-view/inspection-proof의 명시적 해제를 포함해 통과했다. Current20의 28행은 reader 복사/cursor·같은 프로세스 회수·캐시 없는 공유 container 핸들 재사용/손상 검사·기존 16개 핸들 한도·packet 9행 전체를 검증했다. Current21은 새 Plastic wrapper 수명 시험 3행의 CS0728로 시험 전에 멈췄고 해당 시험 helper만 수정했다. Wrapper가 과거 TRX를 잘못 복사한 파일은 무효 evidence임을 명시해 보존하며 current21 시험 실행 수에 포함하지 않는다. Current22는 대규모 입력과 동시 실행 중 실패했다. 새 IDisposable source-view 시험의 caller 하나가 reader를 유지했고 두 시험은 기존 시간 경계를 넘겼다. Current23은 caller 소유권을 수정하고 빌린/소유 summary 복사·Plastic wrapper 해제·최초 proof 인계를 포함한 221행을 통과했다. 타임아웃을 늘리지 않았다. Current24의 24행은 큰 snapshot 증명을 먼저 끝낸 뒤 최신 state/control identity를 읽고 재검증한다. 실제 같은 Plastic B 요청은 mount-connected·attach-intent까지 진행했고 이후 동시 A의 공통 pool writer 배제로 WorkspaceBusy를 반환했다. 대규모 수명주기 완료는 아니다.

실제 100,000항목 준비는 WinFsp packet마다 디렉터리 앞부분을 다시 훑는 문제를 드러냈다. 이제 핸들이 고정 root의 cursor와 overflow lookahead를 유지하며 reset·취소·해제를 검증한다. 데이터 크기에 비례하는 Git 명령의 제품 절대 10분 제한을 제거했고 caller 취소 시 소유 subprocess 종료·drain은 유지한다. 실험의 timeout으로 남은 `index.lock`은 read/write 배제 lease를 잡고 native identity/hash·원본 HEAD/index/branch를 검사하며 소유 evidence로 바이트 그대로 이동했다. 이는 되돌릴 수 있는 실험 복구이며 제품의 자동 잠금 삭제가 아니다.

작은 Unity 세 입력의 작업공간 수명주기는 모두 완료했다. 중간 반영 동안 원본·peer Editor를 유지했고 작업공간 회수까지 정확한 원본 Editor를 유지했다. 최종 실제 로드 코드/씬/에셋은 104/103/103이며 원본의 무관한 편집을 보존했다. Plastic 최종 helper 복구·구 provider 정상 종료 기한 뒤 같은 요청 복구·단계별 혼합 고정 바이너리는 입력별 근거에 명시한다. 이는 기능 근거이며 동일 입력 성능 표본이 아니다. 원본 종료와 None/Plastic 복원은 그 뒤 별도 실험 정리로 수행했다.

Current25는 독립된 읽기 전용 재생을 검증한다. 실제 96MiB 접수 metadata tail이 scratch의 64MiB container 경계를 넘는 동안 원본 소스 바이트·native identity를 유지했다. Cursor/basis lease·정확한 native 디렉터리 생성·긴 경로·killed-scope 복구·생성 gap 보존·페이지 복원 재개·root-bound 조회가 통과했다. Skip 행은 통과한 필수 부모가 실행한 subprocess actor 진입점이다. 아직 남아 있는 실제 Git 정리 index 복구의 자격은 아니다.

Git의 별도 Library 정리는 offline replay가 native owner receipt 없이 소스 인덱스에 COW metadata를 추가하던 결함을 드러냈다. R14는 소스 불변성을 수정했고 R17은 전체 stopped-seal/control/binary-root/native inventory 감사와 전체 인덱스 보존을 검증했다. 같은 계획의 복원 1회가 성공해 검증된1477파일 cursor를 유지했고 과거 결과6개를 독립 검증했다. 원래 인덱스7파일은 소유 container0과 여전히 ownerless인1/2까지 모두 그대로 남는다. 실제 할당156,778,744바이트와 별도 보존 제어20,632바이트이며 전체 basis-control 잔여169,801,528바이트는 다른 범위다. 소유권 채택·payload 제거·잔여0 주장은 없다. 원본 Editor 수명 증명 뒤 별도 실험 정리를 수행했다.

Current26-cache의50행은 명시적16핸들 LRU·cache-hit 최근 접근·32MiB 안에 계상한 scope/container 직접 무효화·커서가 있을 때 비싼 live GC를 미루는 동작을 검증했다. Current28 결합 실행은 새 시험 fixture가 일반 일회성 scratch에 native 영수증을 추가한 한 행만 실패했고 보수적인 정리를 바꾸지 않고 fixture를 수정했다. 별도 Git35행은 실제31초 무출력과 취소/drain을 포함해 통과했다. 데이터에 비례하는 status/diff는 변경 없는 전체 mount tree를 확인한 뒤에야 record를 출력할 수 있으므로 이 record stream에 blob protocol 출력 타이머를 적용하지 않는다. 고정 object reader 보호는 유지한다.

Current29는 비어 있지 않은 회수의 부정 시험 구성이 잘못됐음을 보였다. Delete만으로 unlinked extent를 유지해 정상 정리가 payload segment를 보존했다. Fixture는 unlinked 파일을 해제하고 비어 있지 않은 segment가 실제 회수됐음을 증명한 뒤 거부를 검사한다. Current30은187행 중186개 통과이며 skip1은 통과한 필수 부모가 실행한 actor 진입점이다. Killed-child live-copy 복구·엄격한 빈 header-only catalog 대조·native로 고정한 Plastic 관측 범위·reader/namespace/소유권 거부·기존 packing/adoption/인덱스 보호를 포함한다. Native 범위 관측은 소유 영수증을 쓰거나 수정 가능한 소스로 대체하지 않으며 전체 접수·게시 경계 증명을 유지한다.

실제 Git10만 A provider-loss는 영속 복사 root 뒤 정확한 provider 하나만 중단했다. 저장한118개 native 결합과 고정 prefix3,680,692,041바이트가 그대로였고 접수 소스는 소유 Git 인프라의 순번2/payload92바이트뿐이다. 최초 R17 재연결은 진행 중 복사보다 startup segment GC가 catalog를 먼저 바꾼 상태를 안전하게 거부했다. R19는 같은 요청을63.4216358초에 복구했다. Payload segment identity/hash·접수 seal·원본 HEAD/index/branch·B 수명을 유지했고 진행 중 복사를 마치고 이전 소유 인덱스를 회수했다. 영속 read-admission 캐시38803은 이전 휘발성100002와 다르며 재구성 가능한 읽기는 사용자 소스 ACK가 아니다. 이 실제 저장소 GC 프로세스 중단은 계획한 제품 CLI 고정 수집 중단과 별도다.

같은 기존 Plastic10만 snapshot에서 읽기 전용 R19 관측 범위 측정도 통과했다. 일반 전체 관측5.5830412초·전체 접수5.4950772초·고정 범위 관측100회 합계0.9958472초·전체 경계4.6148969초·이후 일반 관측5.0921995초다. 전체 manifest 검증 횟수는0→1→2→2→3→4로 고정 범위 반복 중 전체 순회가 없고 명시적 전체 경계는 유지됐다. Native metadata fence8개가 논리21,390,437바이트를 보호했고 dispose 뒤0개다. Probe 프로세스 핸들은280→290→290→280, 페이지 캐시 최대32MiB·최종 계상0이다. Payload/소스/owner 쓰기와 강제 GC가 없는 구성 요소 측정이며 전체 metadata RAM의 분리 측정이나 전체 수명주기 속도 개선이 아니다. 실제 R19 provider 스택1회에서도 이전 전체 catalog heartbeat 반복이 관측되지 않았다. 원시 근거는 `artifacts/scalable-storage-tests/native-plastic/r19-same-snapshot-scope-component.json`·`r19-provider-single-managed-stack.txt`다.

사용자 검증 축소에 따라 실제100,000항목 전체 수명주기·실제17GiB·정상 동일 입력 비용·최신 소스 전체 build는 미실행으로 남긴다. 최종 축소 문서 검사는26문서·1,438링크·지도 소스139개 해시에서 누락 대상/소스 문제0을 확인했으며 생략한 전체 gate를 대신하지 않는다. 작은 Unity 자격은 기록한 혼합 고정 바이너리·복구를 포함해 완료했으며 동일 입력 성능 근거가 아니다. 전체 완료·일반 대형 Unity 지원·속도/private memory 절감·다른 OS/앱/Unity 버전·Play/Player build·OS 전원 장애 자격을 주장하지 않는다.
