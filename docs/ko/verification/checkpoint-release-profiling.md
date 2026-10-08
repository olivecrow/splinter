# Checkpoint와 release 프로파일링

[English](../../verification/checkpoint-release-profiling.md)

> **현재 시간 기준:** 아래는 해당 후보/단계에서 기록한 과거 근거이며, 수치와 당시 NotRun 경계는 그대로 보존합니다. 이후 최적화를 합친 `116fd44`의 [UF-01 실측](unity-final-lifecycle.md)은 전체 **18분 3.0초**, Editor 준비 **3분 36.1초**, A/B 보존·회수 **2분 52.0초**, Library 복원 **2분 6.0초**입니다. UF가 개별 최적화의 효과나 별도 미실행 항목을 모두 검증한 것은 아닙니다.

이 문서는 [README Unity 비교](readme-unity-comparison.md) 이후 요청한 2026-10-07 한정 진단 **LP-01–04**를 소유한다. checkpoint·release와 내부 비용을 구분한다. [공개 계측](../../../reports/benchmarks/checkpoint-release-profile-20261007.json)에 단계 합계와 진단 binary identity를 기록했다. 제품 최적화·새 Unity 실행·장애 행렬·지원 통과를 주장하지 않는다.

이후 구현과 일반 배포본의 전후 측정은 [CO-01–04](checkpoint-release-optimization.md)를 따른다. 아래 진단 수치는 그대로 보존한다.

## 범위와 방법

- **LP-01:** 마감한 대형 Unity 실행의 명령 구간·소스 검사/seal timer·내구성 event를 재사용한다. 대형 에셋을 다시 읽지 않았다.
- **LP-02:** 새 Git fixture의 Editor 없는 작업공간 두 개를 순서대로 실행한다. **추적 파일 64개 / 5,510 B**, **합성 파일 256개 / 65,536 B**의 관리형 Library 기준본, `git-snapshot`, Unity 디렉터리 정책, 기본 SJL4 최적화다. 각각 소스 한 줄을 바꾸고 두 번째는 Library 파일 128개의 한 바이트도 바꾼다. Library는 소스 결과에서 제외한다.
- **LP-03:** Git/Runtime assembly의 Git 제외 복사본에만 Stopwatch scope를 추가해 명령 분류·저널 seal·회수 구간을 계측한다. inventory counter는 메모리에 누적해 프로세스 정상 종료 때 게시한다. 양쪽은 같은 진단 binary이며 원래 소스/배포 파일은 유지한다.
- **LP-04:** 실제 NTFS 1바이트 파일 256/512/1,024개로 현재 fence 점유 확인 루프를 계측한다. 파일 32개 warmup 후 크기별 한 번이며 probe 파일을 닫고 제거한다.

진단 build는 locked restore를 사용해 경고·오류 0개로 통과했고 나머지 복사 배포 파일 41개는 원본과 같았다. `Bytes` getter는 LINQ `Sum` 대신 동등한 명시적 열거로 파일 수를 센다. 계측/log 부가 비용은 포함하며 별도로 계측하지 않았다. 진단 근거이며 원래 binary의 정확한 성능 측정이 아니다. Git은 상속한 `GIT_*` 변수를 제거하므로 외부 Trace2 적용을 가정하지 않고 중앙 명령 scope를 사용했다.

각 native 명령의 관측 상한은 240초이며 자동 강제 종료/재시도는 없다. 상한에 도달한 명령도 없다. Unity Editor·import·컴파일·Play Mode·시험·게임 build·VSS·설치 서비스 변경은 필요하지 않았다. 순서대로 한 번씩 실행하고 OS cache를 유지했다. 별도 microprobe 약 4.4초가 첫 작업의 준비와 겹쳤으며 checkpoint 시작 전이었다. provider counter는 프로세스 전체 수명이며 명령의 배타적 시간이 아니다. 부모 총합에 중첩 scope를 다시 더하지 않는다.

## Checkpoint: 반복 검증이 대부분을 차지한다

| 작은 작업 | 명령, 초 | 소스 검사 횟수 | 검사 시간, 초 | 비중 | Git 호출 / 시간, 초 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 소스 한 줄 변경 | 41.9986 | 7 | 33.9070 | 80.73% | 190 / 20.6472 |
| 같은 변경 + Library private 128개 변경 | 41.9940 | 7 | 33.6543 | 80.14% | 190 / 21.9802 |

Git 시간은 검사 시간과 **겹친다**. Git 15.9413 / 17.0960초가 검사 구간 안에 있다. 검사의 나머지 비-Git 시간은 17.9657 / 16.5582초이며 mounted path 검사·source barrier·manifest 게시 등을 포함한다. 잠금 대기 비중의 계측은 아니다. checkpoint마다 소스 89 B만 hash했다.

검사 일곱 번은 최초 verified View, Git checkpoint의 staging 전·후·commit 후, collection 검사, 독립 보존 검사, 최종 verified View다. [조정](../../../src/Splinter.Runtime/WorkspaceCoordinator.cs), [Git checkpoint](../../../src/Splinter.SourceControl.Git/GitWorkspaceProvider.cs), [collection](../../../src/Splinter.Runtime/SnapshotWorkspaces.cs), [독립 보존](../../../src/Splinter.SourceControl.Git/GitSnapshotWorkspaces.cs)을 따른다.

hash를 재사용해도 membership/attribute/configuration·경로별 metadata를 확인한다. 경로 64개는 50개 단위 두 묶음이며 checkpoint의 **순차 attribute 명령은 68회**였다. all-attribute 28회, current filter/encoding 20회, cached filter/encoding 20회다. membership이 같을 때 구조는 `34 × ceil(N / 50)`이며 이전 1,602개에서는 **1,122회**다. 대형 수치는 코드에서 유도했으며 별도로 trace하지 않았다.

[`GitSourceFingerprint`](../../../src/Splinter.SourceControl.Git/GitSourceFingerprint.cs)는 검사 전후 source barrier를 사용하며 hash를 재사용한 파일도 plain path·존재를 확인한다. [provider 루프](../../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs)는 source/flush 요청을 처리한 뒤 1초 쉰다. 별도 대기 비중은 계측하지 않았다. 고정 대기와 이전에 관측한 느린 mounted metadata 접근이 반복 검사 비용을 증폭한다.

| 보존한 Unity 구간 | 명령, 초 | 검사 횟수 / 초 | 검사 비중 | 별도 seal 시간, 초 |
| --- | ---: | ---: | ---: | ---: |
| A checkpoint | 5,723.6083 | 7 / 4,673.1192 | 81.65% | 295.6585 |
| 외부 소스 commit 이후 B checkpoint | 2,914.6685 | 5 / 2,450.3633 | 84.07% | 61.7758 |

대형 첫 검사는 A/B 각각 143 B / 152 B만 hash했다. 이후 검사는 본문 0 B를 hash하고 1,602개 hash를 재사용했다. 반복 지연을 대형 에셋 hashing으로 설명할 수 없다. B는 commit이 이미 있어 변경 파일 staging/commit을 건너뛰었으며 검사 다섯 번인 경로를 A의 최적화 버전으로 표시하지 않는다.

**최종 응답 구성도 검증을 반복한다.** A의 내구성 `checkpoint-completed` event 이후 응답까지 756.7852초이며 최종 검사 750.4229초를 포함했다. B는 374.6007초 간격에 검사 372.2619초를 포함했다. 위 명령 총합에 이미 들어 있는 시간이다.

## Release: 자원 회수 전에 결과를 다시 수집한다

| 작은 작업 | 명령, 초 | 검사 횟수 / 초 | Git 호출 / 초 | SJL4 회수 core, 초 |
| --- | ---: | ---: | ---: | ---: |
| 소스 한 줄 변경 | 20.0513 | 2 / 10.0731 | 69 / 7.2679 | 0.0495 |
| 같은 변경 + Library private 128개 변경 | 20.5481 | 2 / 9.8088 | 69 / 7.6594 | 0.5111 |

checkpoint 뒤 첫 release는 사용 가능한 basis 분기에서 **결과를 다시 collect**한다. 소스 검사 두 번, ignored tree scan 한 번, collection seal 두 번이다. unmount 전과 unregister 안에서도 결과 object graph를 확인한다. 작은 release의 **attribute 명령은 20회**이며 구조는 `10 × ceil(N / 50)`, 이전 입력은 330회다. [release/collection](../../../src/Splinter.Runtime/SnapshotWorkspaces.cs), [Git 보존/결과 확인](../../../src/Splinter.SourceControl.Git/GitSnapshotWorkspaces.cs)을 따른다.

ignored scan은 `git ls-files --others --ignored --exclude-standard` 반환 뒤 관리 경로를 필터링해 Library를 순회할 수 있다. 대형 실행에서 개별 기여는 계측하지 않았다. 두 release 검사는 모두 hash를 재사용하고 본문 hashing은 0 B다.

| 보존한 Unity 구간 | 명령, 초 | 검사, 초 / 비중 | seal 시간, 초 | release-delta event 단계, 초 |
| --- | ---: | ---: | ---: | ---: |
| A release | 1,662.4729 | 1,041.2644 / 62.63% | 133.3495 | 264.3112 |
| B release | 1,163.3742 | 729.9124 / 62.74% | 95.1730 | 187.6621 |

최초 `release-unmount` event는 A/B 시작 이후 1,356.4047 / 946.3805초다. 대부분은 **unmount 시작 전**에 걸렸다. 이후 event 구간은 unmount/post-stop seal 38.4430 / 27.4285초, Git unregister 2.1364 / 1.3227초, 최종 basis release 0.9310 / 0.4277초다. delta 단계는 외부 seal/상태 게시를 포함해 seal 열과 겹치므로 다시 더하지 않는다.

## Private 저널 회수에는 제곱 비례 검사가 있다

[`LatestJournalReclamation`](../../../src/Splinter.Runtime/LatestJournalReclamation.cs)은 inventory 준비 중 파일 fence를 열고 파일마다 `fences.Any(f => WorkspaceStore.SamePath(f.Name, file))`로 확인한다. 최초 경로에서 해당 fence는 이미 있다. 선형 검색마다 [SamePath](../../../src/Splinter.Runtime/WorkspaceStore.cs)의 전체 경로 정규화를 반복한다.

| Microprobe 파일 수 | 정확한 비교 횟수 | 현재 루프, ms | 추가 fence |
| --- | ---: | ---: | ---: |
| 256 | 32,896 | 53.5754 | 0 |
| 512 | 131,328 | 211.4900 | 0 |
| 1,024 | 524,800 | 449.5459 | 0 |

비교 횟수가 `N × (N + 1) / 2`와 같아 중복된 제곱 비례 작업을 입증한다. 이전 inventory 약 16,259개에서는 약 1억 3,200만 회 비교 규모다. cache/JIT 변동으로 시간을 외삽하거나 과거 delta 단계의 나머지를 모두 이 루프에 귀속하지 않는다.

회수는 내부 seal·파일별 hash inventory·inventory 검사 두 번·삭제 직전 hash도 수행한다. Library private 128개 변경에서 내부 seal은 0.1556초, 검사 0.1205 / 0.1354초, fence 검색 0.0133초, hash/delete 0.0508초이며 0.5111초 core에 포함된다. [Windows 외부 delta 회수](../../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs)는 core 전에 다른 seal·소유권 관측·내구성 intent를 추가한다.

## 저널 관측도 metadata 비용을 추가한다

[`LatestJournalStorage.Bytes`](../../../src/Splinter.Runtime/LatestJournalStorage.cs)는 [`JournalDeltaStore.Observe`](../../../src/Splinter.Runtime/JournalDeltaStore.cs)의 store gate 안에서 파일/길이를 열거한다. provider 루프와 [volume-info callback](../../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs)이 반복 호출한다. provider 전체 수명 합계는 733회 / 177.039 ms와 992회 / 3,897.345 ms다. inventory 증가가 달라 동일 길이 비교나 명령별 배타적 비중이 아니다. 이전 16,259개 probe는 한 번 열거에 0.8914초였다. 반복 잠금 내부 작업은 확인했지만 이전 명령의 정확한 기여율은 미계측이다.

## 판단과 종결

checkpoint의 주된 관측 비용은 최종 응답 검사까지 포함한 반복 소스 검증이다. release는 회수 전 collection을 반복하며 delta 제거에 여러 검증/hash 순회와 제곱 비례 fence 검색을 추가한다. Unity Editor가 필요 없는 경로다. 이후 대상은 동등한 검사/attribute 순회 줄이기, 같은 generation/seal/본문 guard 아래의 결과 재사용, 소유권/경로 guard를 유지한 fence 인덱스, callback의 중요한 작업을 반복 점유하지 않는 한정 inventory 관측이다. guard를 제거하거나 최적화를 구현하지 않았다.

작은 task 두 개 모두 `released`이고 소스 파일 하나의 독립 결과를 보존했으며 소유 자원 0·mount/delta/Git 등록 부재를 확인했다. 작은 Library는 일반 NTFS로 복원했다. 소스 64개·Library 256개의 원본 hash 일치·원본 HEAD 불변·소유 basis ref 부재를 확인했다. 실행 후 원본 소스/설정 hash 45개·원본 Git/Runtime DLL/PDB hash 네 개도 일치했다. 기존 Unity 프로젝트는 수정하지 않았다. 원시 scope/receipt·계측 patch·probe는 Git 제외 `artifacts/lifecycle-profile-20261007/`과 `.local/lifecycle-profiler-20261007/`에 보존하며 공개 데이터에는 사용자/구매 에셋 경로·이미지·token을 넣지 않는다.
