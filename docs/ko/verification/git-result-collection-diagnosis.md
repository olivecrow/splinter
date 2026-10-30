# Git 결과 수집·회수 원인 진단

[English](../../verification/git-result-collection-diagnosis.md)

후속 [GCO-01–04 구현·한정 비교](git-collection-optimization.md)가 아래에서 진단한 reader 경로를 바꿨다. 아래 수치는 최적화 전 원래 기록을 유지한다.

**GCD-01, 2026-10-30:** 같은 작업을 계속하는 Git 수집 reader가 고정 결과를 조회·읽을 때 기준본 가용성을 확인하는 Git 프로세스를 반복 실행한다. 최종 회수도 파일을 지우기 전에 이 수집을 다시 수행한다. 현재 작은 입력의 원인을 확인한 진단이며, 최적화 구현이나 구버전 대비 개선·회귀 비율을 입증하지 않는다.

[구조화된 측정](../../../reports/benchmarks/git-result-collection-diagnosis-20261030.json)에 소스 `f91e470`, 제품 assembly hash, 정확한 입력, 단계별 계수, 기존 이벤트 구간과 보존 근거를 고정했다. 실제 Unity 명령 시간은 기존 [RI-05](repeated-original-apply.md)가 소유한다. 과거 [LP/CO](checkpoint-release-profiling.md)는 다른 수집 경로의 반복 source inspection을, [BD/GR](unity-lifecycle-bottleneck-diagnosis.md)은 준비·payload 보존을 분석했다. 그 근거로 이번 계속 작업 수집의 진단을 대신하지 않는다.

## 기존 실제 실행의 구간 귀속

| RI-05 Git A 작업 | 전체 명령, 초 | 수집 intent → 결과 게시, 초 |
| --- | ---: | ---: |
| 첫 중간 수집 | 76.975 | 75.619 |
| 두 번째 중간 수집 | 43.113 | 41.897 |
| 정상 종료 뒤 명시적 최종 수집 | 49.408 | 48.283 |
| 최종 반영 뒤 회수 | 53.632 | 새 마감 결과 수집에 **45.171** |

마지막 구간은 회수의 **84.2%**다. 실제 로그의 `release-unmount`부터 `released`까지는 **5.640초**이며, 이 안에서 `release-delta`→`release-snapshot`은 **1.736초**다. 이 실행에서 private 자료 삭제는 회수의 작은 부분이다. 나머지에는 전체 seal, 이전 결과 검증, 소유 검사와 명령 처리가 포함되며 하나의 측정 원인에 귀속하지 않는다.

최종 반영 결과와 회수 시 새로 만든 결과의 **소스 identity가 같다**. 그런데도 [`SealGenerationForReleaseAsync`](../../../src/Splinter.Runtime/WorkspaceGenerations.cs)는 `release:<journal hash>` 요청으로 공통 수집 엔진을 호출한다. [`ReleaseSnapshotWorkspaceAsync`](../../../src/Splinter.Runtime/SnapshotWorkspaces.cs)는 generation 지원 adapter에 이 경로를 선택하여 이전의 변경 없는 결과 재사용 경로를 거치지 않는다. 종료 중 마지막 쓰기, infrastructure 제거, 전후 seal은 여전히 필수다. 사후 소스 일치가 이 보호를 생략할 근거는 아니다.

## 제한된 진단 재현

Editor·mount·provider 프로세스·새 작업공간이나 Library 복사본을 만들지 않았다. 기존 RI Git 저장소에서 작은 독립 Git object store를 만들었다. 과거 저널 pin은 이미 회수됐으므로 보존된 결과 2의 blob에서 소스 범위를 재구성했다. 현재 Release assembly의 `SourceCaptureTree`, `GitObjectSnapshotTree`, 비공개 `FileWorkspaceProvider.ScanTree`와 `GitSourceResult`를 수정 없이 사용했다. health callback은 기본 동작과 같은 `GitObjectSnapshotProvider.ObserveAsync`를 계측하고 호출자를 분류한다. 가용성을 캐시하거나 검사를 약화하지 않는다.

기준본은 **27파일 / 75,034 B**, 결과 2는 **33파일 / 87,259 B**와 디렉터리 세 개다. 두 재현 모두 보존 결과의 모든 경로·종류·길이·내용 hash와 **정확한 Git commit fingerprint**가 일치한다. 진단의 owner·경로·backing 배치·기준본 시각 metadata는 다르며, 과거 물리 I/O나 metadata identity의 재현은 아니다.

| 진단 단계 | 경과, 초 | 기준본 검사 | 검사 시간, 초 | 검사에서 실행한 Git 명령 |
| --- | ---: | ---: | ---: | ---: |
| 별도 최초 기준본 scan | 13.659 | 85 | 13.473 | 170 |
| 그 scan 뒤의 warm reader 결과 scan | 19.381 | 122 | 19.359 | 244 |
| 이후 새 reader의 결과 scan | 19.872 | 122 | 19.682 | 244 |
| Git tree 구성·첫 결과 검증, 새 reader 표본 | 3.005 | 14 | 2.203 | 28 |

두 결과 scan의 **99.89% / 99.04%**가 가용성 검사다. warm/cold는 제품의 proof/payload cache를 뜻하며 OS cache가 아니다. warm 읽기는 기준본 payload 27개에 모두 적중하고 **추가 decode가 0 B**다. 새 reader는 **75,034 B**를 한 번 증명하며 reader 프로세스 두 개를 사용한다. 두 scan 모두 decoder 대기·반복 payload decode·용량 퇴출이 0이다. 작은 입력의 가용성 경로를 입증하며 대형 입력의 payload·metadata 비용을 배제하지 않는다.

새 reader 표본의 두 번째 Git graph 검증은 **0.156초**, 보존 Git graph 세 개의 검증은 **0.470초**다. 전체 generation 검증이나 회수 시간이 아닌 Git graph 구성 요소다. 작은 Git 명령 네 종류를 두 실행에서 표본화했으며 시작을 포함해 각각 약 **0.074–0.080초**다. loader·스케줄링·Git 실행·보안 검사 비용을 나누지 않았으며 백신 원인을 확정하지 않았다.

## 호출 경로와 반복 증폭

1. [`OpenSourceCaptureAsync`](../../../src/Splinter.Platform.Windows/NtfsSourceGenerations.cs)는 [`WorkspaceSnapshotBasis.OpenTree`](../../../src/Splinter.Platform.Windows/WorkspaceSnapshotBasis.cs)로 새 기준본을 열면서 실행 중 provider가 유지하는 health observation을 전달하지 않는다. 실행 중 provider는 [`WinFspOverlayServer`](../../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs)에서 자신의 observation callback을 전달한다.
2. [`GitObjectSnapshotProvider.OpenTree`](../../../src/Splinter.SourceControl.Git/GitObjectSnapshotProvider.cs)의 기본 callback은 `EnsureAvailable`마다 `ObserveAsync`를 수행한다. 정확한 소유 기준본을 확인하는 `for-each-ref`와 고정 commit tree를 확인하는 `rev-parse --verify`가 각각 별도 프로세스다.
3. [`SourceCaptureTree`](../../../src/Splinter.Runtime/JournalSourceCapture.cs)는 목록 조회·읽기마다 가용성을 확인한다. 내부 [`GitObjectSnapshotTree`](../../../src/Splinter.SourceControl.Git/GitObjectSnapshotTree.cs)도 읽기 진입·reader 접수·증명한 바이트 게시 전에 확인하며 cache hit도 포함한다.
4. [`PreserveGenerationAsync`](../../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs)는 결과 manifest를 위해 고정 소스 전체를 hash한다. 이 scan은 capture 목록/읽기 검사 37회 + basis 목록 검사 4회 + 읽기 진입 27회 + reader 접수 27회 + 게시 전 27회 = **122회 / Git 프로세스 244개**다. 캐시에 있는 변경 없는 기준본 파일도 한 번 읽으면서 capture·basis 계층을 합쳐 명령 여덟 개를 실행한다.
5. [`GitSourceResult.PreserveAsync`](../../../src/Splinter.SourceControl.Git/GitSourceResult.cs)는 tree를 다시 순회한다. 이 단계에서 측정한 가용성 명령 28개 외에도, 기존 ref 경로에서 `ls-tree` 하나·`hash-object` 여섯·`mktree` 네 개·`commit-tree` 하나·ref 조회 하나·graph 검사 두 개로 구성/검증 명령 15개를 실행한다. 새 ref 게시에는 `update-ref`가 추가된다. 최초 결과는 결과 scan 전에 기준본도 hash하며 이후 결과는 그 기준 manifest를 보존한다.

## 진단 마감과 다음 구현 경계

첫 개선 대상은 정확한 reader lease·고정 기준 identity·적절한 재검증 경계 안에서 가용성 증명 재사용 범위를 정하는 것이다. 단순한 `available=true` 캐시는 ref 유실·변경 감지를 잃는다. 다음 대상은 metadata·rename·delete·최초 내용 복귀 의미를 유지하는 변경 없는 manifest/내용 증명 재사용이다. 최종 결과 재사용은 전체 seal·종료 중 마지막 쓰기·infrastructure 제거를 유지하며 마지막 소스의 동등성을 증명해야 한다. 모든 보존 결과 검증·조건부 원본 게시·소유 자원 보호는 유지한다. 이 진단에서 해당 최적화를 구현하거나 측정하지 않았다.

진단 helper는 경고·오류 없이 빌드됐고 내용·정확한 commit 대조가 모두 통과했다. 원본 HEAD·index·전체 ref와 **현재 원본 소스 38파일**의 바이트가 유지됐다. 측정 전 backing 경로 guard가 거부한 준비 시도 하나를 포함하여 private 진단 기준본 ref 세 개를 회수하고 capture pin을 제거했다. 작은 object store·metadata·helper·원시 근거는 ignored `.local/ri-git-diagnosis-20261030/`·`artifacts/ri-git-diagnosis-20261030/`에 남긴다.

표본은 순차 각 1회이고 관측 오버헤드와 재구성한 backing을 포함한다. 19초 scan을 기존 43초 CLI 전체 시간으로 대체하거나 예상 절감으로 주장하지 않는다. Unity 수명주기 재실행·최적화 전후 비교·전체 회귀 gate·OS 장애·대형 입력·RAM/공간 절감 검증은 수행하지 않았다. 제품 소스는 변경하지 않았으며 진단·근거·문서 탐색만 추가하거나 갱신했다.
