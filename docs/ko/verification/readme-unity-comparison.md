# README Unity 저장 공간·시간 비교

[English](../../verification/readme-unity-comparison.md)

이 문서는 2026-10-07 README 비교 **RB-01–05**를 소유한다. 실제 Codex 앱 관리 워크트리와 원본 Library 기준본을 참조하는 현재 Splinter 구현을 한 묶음으로 측정했다. 과거 [Unity 비교](unity-live-comparison.md), SF/PV 행렬, 마감한 공간·메모리 단위를 다시 여는 작업이 아니다.

편집·컴파일 완료 시점의 추가 파일 할당은 **11,297,124,336 B에서 152,543,856 B로 98.65% 감소**했다. Splinter의 Editor 준비와 후속 코드 refresh·컴파일은 더 오래 걸렸다. [공개 수치 데이터](../../../reports/benchmarks/unity-workspace-comparison-20261007.json)를 README 그래프의 입력으로 사용하며, [생성 스크립트](../../../scripts/plot-unity-workspace-comparison.py)가 PNG와 SVG를 만든다.

## 고정 입력과 조건

| 항목 | 기록한 입력 |
| --- | --- |
| 프로젝트 | 별도 Git/Unity fixture `splinter-unity`; development revision `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1` |
| 소스 | 추적 파일 1,602개, 실제 working-file 2,804,772,822 B; 약 2.8 GB Medieval 에셋 입력 포함 |
| Unity | 6000.3.22f1, URP 17.3.0; 기존 커밋된 qualification probe와 설치된 프로젝트 패키지 |
| 호스트 | Windows (보고된 OS 버전 10.0.26200), NTFS, AMD Ryzen 5 5600X, 설치 RAM 68,628,553,728 B |
| 제품 | HEAD `74518fd453ea9e191c5b5efc1e2ad82a17ffe427`; 공개 데이터의 SHA-256 10개로 고정한 Release assembly, 측정 중 재빌드 없음 |
| Codex | 사용자가 명시적으로 승인한 측정 대화 두 개를 통해 만든 실제 앱 관리 워크트리; 각각 Library 없이 시작 |
| Splinter | 같은 커밋의 `git-snapshot` / Git / Unity 작업공간 두 개, 준비한 원본 Library 기준본 참조; 양쪽 provider에서 기본 SJL4 최적화 ON 확인 |
| 실행 순서 | Codex A/B 한 묶음 이후 Splinter A/B 한 묶음; 각 묶음은 독립 Editor 두 개, OS 캐시 초기화 없음 |

준비 시 원본 Editor는 없었다. Library만 같은 NTFS 볼륨에서 기존 본문을 복사하지 않고 관리형 기준본으로 이동했다. 소스는 원래 경로에 유지했다. Library 재사용은 사용자가 선택한 비교 조건이며 cold Library 비교가 아니다.

시간을 측정한 Editor 묶음 실행 전에 Splinter 작업공간 두 개를 준비했다. Codex 묶음 실행 중에도 해당 mount와 원본 Library view가 유지됐으며 Splinter Editor는 아직 실행하지 않았다. 이 준비 순서와 cache·자원 영향도 단일 실행 조건에 포함된다.

Git의 전역 `core.autocrlf=true`는 유지했다. 같은 raw committed bytes를 사용하도록 각 Codex checkout의 줄바꿈만 다른 파일 31개를 정규화하고, Editor 시작 전 전체 1,602개 파일을 Git blob identity와 대조했다. 원본 working-file 바이트는 보존했다. Splinter의 clean/raw 입력 제한을 위해 fixture에 로컬 `core.autocrlf=input`을 임시 적용했으며, 최종 정리에서 이 로컬 override를 제거한다.

## 작업과 증거 기준

소스 작업은 작업자마다 한 줄 변경이며 새 기능·에셋 편집·Play Mode·게임 빌드를 포함하지 않는다.

| 작업자 | 파일 | 원래 계산 | 변경한 계산 | 입력 3의 결과 |
| --- | --- | --- | --- | --- |
| A | `Assets/SplinterAlphaA.cs` | `value + 1` | `value + 2` | 4 → 5 |
| B | `Assets/SplinterAlphaB.cs` | `value + value` | `value + value + value` | 6 → 9 |

양쪽 방식에서 같은 변경을 수행하고 컴파일된 값·peer/원본 값 불변·선택한 기존 에셋을 확인했다. 보존·회수 구간 9,367.0703초는 이 작업과 Editor 정상 종료 이후부터다. 작은 소스 결과 두 개를 보존하는 추가 비용이며 대규모 기능 구현에 쓴 시간이 아니다. 이 지연은 측정 입력에서 대화형 개발의 실사용을 가로막는 성능 문제다. 저장 공간 감소만으로 실용성을 입증하지 않는다.

| ID | 고정 기준 | 결과 |
| --- | --- | --- |
| RB-01 | 실제 Codex 워크트리, 불변 공통 입력, 고정 제품 binary, 정확한 반환 Splinter 경로·소유 Editor PID/시작 identity | 관측. 최초 Splinter ensure는 mount 전 줄바꿈 입력을 거부했고 같은 작업 입력을 수정 |
| RB-02 | 두 독립 Editor가 기준 값 A=4, B=6, original=13으로 준비 | 두 방식 모두 관측 |
| RB-03 | A는 자기 계산만 5로, B는 자기 계산만 9로 변경. 성공 refresh·새 domain generation/assembly MVID·현재 값·original=13 | 네 Editor 모두 관측, 응답 errors 비어 있음 |
| RB-04 | 같은 선택 model·texture·scene·한정 URP preview, 실제 추가 파일 할당·단계별 시간 | 관측. mesh 3개, vertex 3,001개, triangle 2,441개, 2,048×2,048 texture, 누락 scene mesh 0, foreground pixel·씬 상태 복원 |
| RB-05 | 소유 Editor 정상 종료, 독립 결과, 소유 Splinter 회수, 원본 Library 복원·원본 소스 불변 | 결과·정리 항목에서 종결 증거를 기록. Codex 대화 소유 워크트리는 보존 |

기존 fixture probe는 정확한 project path에서 PID·generation·MVID·컴파일/updating flag·로드된 값을 새 상태로 게시한다. 최초 준비는 최신 상태와 컴파일/updating 부재를 요구한다. 코드 변경 준비는 추가로 해당 refresh의 성공 응답과 새 generation/MVID를 요구한다. 에셋 관측은 선택한 커밋 리소스와 한정 렌더링을 검사하며 에셋 팩 전체의 자격이 아니다.

Play Mode·Test Runner·게임 빌드·새 도구 설치·원본 소스 반영·Editor 강제 종료·확대 장애 행렬은 실행하지 않았다. 선택적 외부 패키지는 변경하지 않았다. probe는 외부 컴파일 증거이며 Splinter checkpoint/release는 컴파일 성공을 주장하지 않는다.

## 추가 파일 할당

| 측정 단계 | Codex, B | Splinter, B | 해석 |
| --- | ---: | ---: | --- |
| 편집·컴파일 완료, 두 Editor 준비 | 11,297,124,336 | 152,543,856 | README 비교: 10.5213 대 0.1421 GiB; 11,144,580,480 B 절감, 98.6497% 감소 |
| Editor 종료 후, 결과 수집 전 | 11,288,856,928 | 197,453,080 | 별도 단계. 종료 중 생성/private 파일이 바뀌었으므로 편집 완료 시점의 분자가 아님 |

논리 mount 크기 대신 실제 파일의 Windows `FILE_STANDARD_INFO`를 사용했다. 선택한 모든 root의 관측이 완료됐고 hardlink/reparse/읽기 실패로 인한 unknown 합계는 없었다. Codex는 source/Library root 두 개, observer control root 두 개, 소유 Git 등록 root 두 개를 포함한다. Splinter는 양쪽 task record/private delta·소유 Git 등록과 공유 runner·lock·Library metadata·원본 delta를 한 번씩 포함한다. 이동한 기존 Library `base` 본문은 제외하며 논리 workspace mount를 별도의 물리 복제본으로 순회하지 않는다.

기존 원본 source·Library 본문·공유 기존 Git 객체·무관한 작업/evidence·전역 앱 cache·directory/MFT metadata·OS cache page·전체 디스크 순간 peak는 이 수치의 범위 밖이다. 새 `git-snapshot` 실행에는 VSS를 사용하지 않았다. 공유 참조와 `readCacheBytes=0`은 RAM 절감이나 provider 비용 0을 입증하지 않는다.

## 시간

A/B 묶음의 시간은 두 작업 모두 기준을 만족할 때까지다. Editor 시작과 refresh는 병렬이며 Splinter ensure 두 번은 순차 수행했다. Editor 시간은 작업공간이 이미 생성된 상태에서 시작한다. 아래 비용은 서로 바꿔 쓸 수 있는 하나의 전체 시간 값이 아니다.

| 단계 | Codex, 초 | Splinter, 초 | Splinter 추가 시간 |
| --- | ---: | ---: | ---: |
| Editor 시작부터 최초 컴파일 준비 | 887.1241 | 1,753.4893 | +866.3652 (+14분 26초) |
| 코드 편집·refresh부터 컴파일 준비 | 19.0121 | 554.4164 | +535.4043 (+8분 55초) |
| 최초 Library 기준본 준비 | 해당 없음 | 272.9366 | 별도 준비 비용, 4분 33초 |
| 작업공간 ensure 두 번 | 앱 전체 완료 시간 unknown | 766.4307 | 별도 준비 비용, 12분 46초; A 381.1842 / B 385.2465 |
| Editor 종료 후 A checkpoint | 같은 전체 절차를 측정하지 않음 | 5,723.6083 | 별도 보존 비용, 95분 23초; 모든 제품 검증과 최종 응답 포함 |
| A release | 같은 전체 절차를 측정하지 않음 | 1,662.4729 | 27분 42초; B 보존과 겹침 |
| B 병렬 checkpoint | 같은 전체 절차를 측정하지 않음 | 2,914.6685 | 외부 소스 커밋 후 48분 35초; A와 준비 순서가 다르며 A 검사/release와 겹침 |
| B release | 같은 전체 절차를 측정하지 않음 | 1,163.3742 | 19분 23초 |
| 기록한 A/B 보존·회수 순서 전체 | 같은 전체 절차를 측정하지 않음 | 9,367.0703 | 실제 경과 2시간 36분 7초. 겹침·복구·동시 진단을 포함하며 위 checkpoint/release 호출을 포함 |
| 최종 원본 Library 복원 | 해당 없음 | 1,473.2436 | 별도 복원 비용, 24분 33초 |

Codex 앱은 queued client ID를 반환했다. Git index 시각은 약 46초의 checkout 대리 관측값일 뿐이다. Codex 대화 생성의 전체 시간을 측정한 값이 아니며 시간 그래프에 포함하지 않았다. Library 준비 시간은 짧은 소스 감사와 겹쳤으므로 독립 microbenchmark가 아니다. 해당 Editor 단계의 polling·시작·refresh는 포함하고 준비·별도 에셋 검사·결과 정리·그래프 생성은 막대의 범위 밖이다. Splinter 최초 준비 시계는 이미 실행 중인 같은 Editor를 대상으로 observer를 고치는 동안에도 계속됐으며, 관측을 재개할 때 두 Editor 모두 준비되지 않은 상태였다. 관측한 실제 경과 구간이지 정확한 Unity 내부 이벤트 시각이 아니다. 방식별 한 묶음이므로 cache·순서 효과와 변동 범위는 unknown이다.

Splinter 초기화는 로그가 계속 진행하는 상태에서 observer의 최초 20분 대기 한도를 넘겼다. 같은 Editor를 45분 한도로 계속 관측해 준비를 확인했으며 대체 Editor나 새 묶음을 시작하지 않았다. A 초기화의 한 로그 구간에 약 456초의 `InitializeOnLoad`가 기록됐다. 이는 관측값이며 전체 지연 원인의 진단은 아니다. 아래 보존 단계 진단은 별도의 병목을 확인한 것으로, 앞선 Unity 초기화·refresh 시간의 원인별 비율을 측정한 것은 아니다.

## 보존 단계 지연 원인

사용자가 A checkpoint 실행 중 지연 원인 확인을 요청했다. 아래 한정 읽기 전용 진단은 그래프의 Editor 단계가 끝난 뒤 같은 종료된 task에서 수행했다. Editor 재시작·편집 반복·제품 상태 수정·고정 구현 변경은 하지 않았다. 공개 데이터에는 비교 막대와 별도로 진단 시간을 기록한다.

현재 보존 경로에서 다음 세 실측 비용이 겹친다.

1. **본문 hash를 재사용해도 전체 metadata 검사를 반복한다.** 소스 1,602개에 대해 `InspectSourceAsync` 한 번은 working-tree와 index의 filter/encoding 검사 각 33묶음, 검사 전후 전체 attribute 각 33묶음을 수행한다. 소속/configuration/path 검사 외에도 순차 `check-attr` 명령 **132번**이다. checkpoint·수집·독립 보존이 여러 경계에서 inspection을 다시 호출한다. 완료된 A inspection은 1,602개 hash를 모두 재사용하고 content-hash 입력이 0인데도 382.1333·405.2459·478.3267·1,429.7086초가 걸렸다. 재사용은 본문 hashing을 줄이며 나머지 metadata 작업까지 없애지는 않는다. [소스 검사](../../../src/Splinter.SourceControl.Git/GitSourceFingerprint.cs)·[Git checkpoint](../../../src/Splinter.SourceControl.Git/GitWorkspaceProvider.cs)·[결과 수집](../../../src/Splinter.Runtime/SnapshotWorkspaces.cs)을 따른다.
2. **관측한 상태에서 mount 경로 조회가 느리다.** 같은 추적 경로 50개의 Git attribute 출력 크기가 같았고, 일반 NTFS는 0.2277초, Splinter A는 12.8337초로 약 56배 차이가 났다. index 전용 filter/encoding 조회는 0.2274 대 12.5749초였다. 단순 revision 조회도 0.1738 대 5.8935초였고 Splinter 파일 하나의 attribute 조회는 7.7605초였다. attribute 처리 외에 명령 시작·경로 조회의 고정 비용도 크다. 기존 파일 10개 stat은 0.0025 대 24.0626초였다. 진행 중 checkpoint와 동시에 수행한 단일 진단이므로 경합/cache 영향은 unknown이며 일반 파일시스템 benchmark나 모든 Git 호출의 시간 분해가 아니다.
3. **저장량 관측이 공통 store 잠금 안에서 현재 journal 전체 파일을 순회한다.** [`LatestJournalStorage.Bytes`](../../../src/Splinter.Runtime/LatestJournalStorage.cs)는 현재 journal 각 파일의 길이를 조회하고, [`JournalDeltaStore.Observe`](../../../src/Splinter.Runtime/JournalDeltaStore.cs)는 `EnterGate` 잠금 안에서 이를 호출한다. provider의 주기적 loop와 파일시스템 volume-info callback도 `Observe`를 호출한다. A journal은 16,259개 파일이었다. 같은 .NET 조회 본문을 provider 잠금 밖에서 실행했을 때 논리 36,694,054바이트의 조회에 0.8914초가 걸렸다. metadata 조회 중 반복적인 잠금 점유 비용을 만드는 구체적 경로다. 누적 lock wait는 여러 호출자의 합이므로 checkpoint 경과 시간으로 읽지 않는다. [provider loop](../../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs)·[volume-info callback](../../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs)을 따른다.

독립 보존 경로는 추가로 `git ls-files --others --ignored --exclude-standard`를 실행하며, Git이 목록을 반환한 **뒤에** 관리 경로를 걸러낸다. 따라서 이 guard 중 Library 같은 생성 데이터도 순회할 수 있다. 코드에서 확인한 추가 비용 경로이며 별도 경과 시간 기여율은 측정하지 않았다.

진단은 느린 mount metadata 접근·반복 검증·비용이 큰 잠금 내 관측 경로를 확인했다. 정확한 함수별 시간 기여율·메모리 누수·paging이 주원인이라는 판단·Unity package 결함·앞선 모든 Editor 지연의 원인은 입증하지 않았다. 소스 처리는 계속 진행했고 provider 관측의 I/O 실패는 없었다. 측정한 구현은 변경하지 않았다. 최적화 후보는 소유권·본문의 전후 guard를 유지하면서 중복 metadata 검사를 줄이고 일상 저장량 관측이 callback 잠금을 반복 점유하지 않게 하는 것이다. 구현·검증은 별도 작업으로 남긴다.

최종 복원은 별도 비용 경계다. [`ManagedDirectoryRestore`](../../../src/Splinter.Platform.Windows/ManagedDirectoryRestore.cs)는 고정 파일 identity·본문을 확인하고 기준 파일을 이동한 뒤 최종 본문/metadata를 검증하며 파일마다 cursor를 내구성 있게 게시한다. 복원이 계속되는 동안 cursor의 완료 파일 40,487개를 관측했다. hashing·이동·flush의 개별 기여율은 계측하지 않았다. 복원은 1,473.2436초에 완료됐으며 앞선 Editor 막대나 A/B 보존·회수 묶음의 일부가 아니다.

## 실패와 복구 경계

- 최초 Splinter A ensure는 mount 전 지원하지 않는 Git 줄바꿈 조건을 거부했다. 같은 key/id/home/base로 수정했으며 실패 증거를 보존했다.
- 측정 driver가 비동기 `unity start`의 **Accepted**를 처음에 실패로 해석했다. 이미 소유한 Editor identity에서 관측을 재개했고 start를 반복하지 않았다.
- Codex observer 파일 게시는 대기 중 inspect 요청 게시 전 Windows error 5로 실패했다. 적절한 일반 사용자 atomic replace로 같은 요청을 게시했고 코드 편집/refresh를 반복하지 않았다. harness 증거이며 Splinter 결함을 입증하지 않는다.
- Codex 결과 보존은 정규화한 파일의 stat flag가 남은 상태에서 빈 status 목록을 요구했다. 실제 content diff는 비었고 raw 결과 파일 1,602개 모두 보존 커밋과 일치했다. 편집/Editor stop을 반복하지 않고 B 보존을 재개했다.
- Splinter `unity stop` 두 번은 모두 **EditorStopTimeout**을 반환했다. 이후 Editor는 강제 종료 없이 정상 종료했고, 두 process-exit receipt 모두 exit code 0·active job process 0을 기록했다. 결과 수집 전에 Editor 부재와 mount 열린 핸들 0을 관측했다. 기록한 종료 묶음 741.3975초는 **관측 상한**이며 정확한 exit 시각이나 최초 CLI 응답의 성공이 아니다. 남아 있던 `editor-starting` phase를 Editor 생존 증거로 사용하지 않았다.
- B의 병렬 checkpoint가 task 잠금을 점유한 상태에서 기존 정리 driver가 B checkpoint를 재호출했다. CLI가 5.8595초 뒤 **WorkspaceBusy**를 반환해 driver가 종료됐다. 점유 거부 응답은 보존하며 병렬 checkpoint 성공 receipt를 확인한 뒤 일반 B release를 호출해 복구한다. orchestration 충돌과 task 잠금의 예상된 거부이며 코드 재편집이나 checkpoint guard 우회가 아니다.

## 결과와 정리

Codex 독립 결과는 benchmark ref의 revision `022376235d10ec293fe05cb69aa3dfd4910da0ca` (A), `3194c94e9fb7500de30801b0ec759188c748295b` (B)에 보존했다. 두 Editor와 기록한 자식 프로세스 61개가 종료됐다. 워크트리 두 개는 측정 대화의 소유로 유지한다. 다른 대화 소유의 attachment가 거부됐으므로 이 대화에서 삭제하거나 archive하지 않았다.

Splinter 결과 `3d4eb870ddb3e469d4feb32c838bf97775fa6af1` (A), `2bbfe63731eecd83d1af91dd8fc529c28c6f2521` (B)의 tree는 해당 Codex 결과와 같다. 두 task는 `released`에 도달했고 소유 delta·Git 등록·Editor·provider가 없다. 원본 Library는 일반 NTFS 폴더로 복원했다. 원본 추적 파일 1,602개·HEAD/branch·기존 ref 불변을 대조했으며 임시 로컬 `core.autocrlf` 항목을 제거하고 전역 설정을 유지했다. 필수 build 전에 고정 binary 10개의 hash가 일치했다. 이 검사는 공개 데이터의 cleanup 항목에 기록했다. 원시 JSON·process identity·log·journal·preview는 Git에서 제외한 로컬 `artifacts/readme-unity-comparison-20261007/`과 `.local/rb7/`에 보존한다. 구매 에셋의 경로/이미지와 session token은 공개 데이터에서 제외한다.

시간 측정과 Editor 정상 종료 후 A는 Splinter checkpoint로 변경 소스를 커밋했다. A의 검사가 진행되는 동안 B의 정확한 소스 파일 한 개를 외부 Git으로 커밋하고 Codex B와 결과 tree를 대조했다. 이후 A의 남은 검사/release와 병렬로 B의 일반 Splinter checkpoint를 시작했다. 기존 driver의 B 재호출은 WorkspaceBusy로 거부됐다. 병렬 checkpoint가 성공한 뒤 기록한 결과에서 일반 B release를 재개했다. 제품 상태를 편집하거나 소스/seal/회수 guard를 건너뛰지 않고 독립 보존 단계를 겹친 것이다. 정리 시간에는 기록한 복구와 동시에 수행한 읽기 전용 진단도 포함한다. 동일 정리 절차의 전체 비교 시간이 아니며 겹치는 개별 호출 비용을 묶음 시간에 다시 더하지 않는다.

측정과 정리 뒤 필수 `pwsh ./scripts/build.ps1`의 locked restore, 경고·오류 0개의 Release build, 자동 시험 1,134개가 통과했다. 명시적으로 실행 조건을 제한한 시험 19개는 건너뛰었고 실패는 없었다. 고정한 측정 binary를 대조한 이후 기존의 무관한 변경도 포함한 작업 checkout을 검증한 것이다. 기록한 Unity 관측을 대체하거나 건너뛴 native 실행을 자격화하지 않는다. 문서 검증은 변경 페이지 9개·로컬 링크 538개·양쪽 언어의 수치/자격 일치·PNG/SVG 파일 6개를 확인했으며 끊긴 링크나 공개 데이터의 민감 필드를 발견하지 않았다.

README 비교 그림은 편집 완료 시점의 할당과 공통 Editor 단계 두 개다. 세 번째 그림은 Splinter의 별도 준비·보존/release·최종 복원 구간을 보여주며 같은 경계의 Codex 전체 비용은 확인하지 못했다. 종료는 별도 관측 상한으로 유지한다. 통계적 성능·전체 RAM/저장 peak·대형 프로젝트 지원·일반 Unity 에셋 호환성·native 중단/OS 장애 보장을 입증하지 않는다.
