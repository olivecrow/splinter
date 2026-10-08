# SU 이후 실제 Unity 준비 시간 재측정

[English](../../verification/unity-startup-remeasurement.md)

구현 후속: 추가 Library/소스/게시/Bee 변경과 한정 근거는 [SA-01–05](startup-additional-optimization.md)를 따른다. 아래 수치와 분석은 당시 US 후보의 기록이며 현재 후보의 시간이 아니다.

**US-01:** 실제 Unity Editor 두 개가 최초 준비를 마치는 데 **349.7933초(5분 49.8초)**가 걸렸습니다. [UP-01](unity-startup-profile.md)의 766.9024초(12분 46.9초)보다 **417.1091초 / 54.39% 짧게 관측**됐습니다. 스캔은 약 447초에서 16초로 줄었지만 CompileScripts는 약 210초로 개선되지 않았습니다. 시작 실패를 복구한 단일 cohort이며 순수 인과적 개선율이 아닙니다. [데이터셋](../../../reports/benchmarks/unity-startup-remeasurement-20261008.json)에 시간·runtime hash·실패·계측 경계를 보존합니다.

## 후보와 동일 입력

측정 전 [SU-01–05](startup-profile-optimization.md)를 `95c813537b5bdc698f8f48e2bca63c7ae99f1d30`으로 커밋했습니다. 첫 `unity start` 두 개는 **Editor를 실행하기 전에** `SnapshotIdentityMismatch`로 실패했습니다. `ensure`가 올바르게 저장한 source-metadata 접미사를 저장 상태 reader가 허용하지 못했습니다. `9c09f04c38f5b9cc8f65f159298744f1c79d833d`에서 정확한 hash/source/provider/revision 검사를 유지하며 수정했습니다. 필수 빌드는 **1,376 통과 / 0 실패 / 조건부 20 skip**, 빌드 경고·오류 0입니다.

상태 파일 편집이나 준비 반복 없이 같은 A/B record와 mount를 재개했습니다. provider는 고정한 `95c8135` runtime을 유지하고 이후 CLI만 고정한 `9c09f04`를 사용했습니다. 수정에서 decoder/저널 알고리즘은 바뀌지 않았습니다. 따라서 US는 **서로 다른 runtime을 사용한 동일 작업 재개**이며 모든 수명주기를 `9c09f04`로 새로 실행한 자격이 아닙니다. 최초 실패 응답도 실패로 보존합니다.

Unity **6000.3.22f1**, 소스 `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, **tracked 파일 1,602개 / 2,804,772,822 bytes**, 독립 `git-snapshot` Editor 두 개와 원본 Library 참조는 UP와 같습니다. **Library 항목 44,860개**의 경로·내용 hash·길이·속성·시각이 모두 같습니다. manifest hash 차이는 capture/소유권 wrapper뿐입니다. 실행 전후 소스 바이트를 확인했으며 소스 변경·refresh 실험·Play Mode·test·game build는 실행하지 않았습니다.

준비 완료는 기존 외부 fixture로 확인합니다. 정확한 반환 경로와 소유 PID/시작 identity, 새로운 `ready` 관측, `isCompiling=false`, `isUpdating=false`, 코드 값 A=4 / B=6 / 원본=13을 확인했습니다. Splinter heartbeat는 컴파일 증거가 아닙니다. UAC·서비스·전역 설정·설치된 외부 패키지는 변경하지 않았습니다.

## 준비 시간

| 작업 | US 초 | 시간 |
| --- | ---: | ---: |
| 원본 Library 참조 준비 | 150.4625 | 2분 30.5초 |
| 작업공간 A 준비 | 242.8974 | 4분 02.9초 |
| 작업공간 B 준비 | 251.8003 | 4분 11.8초 |
| 동시 시작한 Editor 두 개의 최초 준비 | 349.7933 | 5분 49.8초 |
| 위 준비·Editor 작업 시간의 합 | 994.9535 | 16분 35.0초 |

마지막 행은 실패한 시작과 진단·빌드·커밋 중단 시간을 제외한 **선택 연산의 합**입니다. 처음부터 준비 완료까지의 연속 측정값이 아닙니다. 실제 구간은 **1,554.7921초(25분 54.8초)**이며 마지막 ensure와 재개한 시작 사이의 **559.8147초(9분 19.8초)** 간격을 포함합니다. UTC 구간은 데이터셋에 보존합니다. 성공한 `unity start` 명령 시간은 Editor 준비 구간에 포함되므로 다시 더하지 않습니다.

| Unity 단계 | UP A / B, 초 | US A / B, 초 |
| --- | ---: | ---: |
| Scan | 446.9486 / 447.5970 | 16.0673 / 16.3939 |
| CompileScripts | 189.7586 / 192.4327 | 209.6090 / 209.4719 |
| ImportInProcess | 11.8075 / 11.7899 | 10.1738 / 10.3722 |

Unity 단계는 중첩되므로 **합산하지 않습니다**. A의 최초 refresh는 330.307초입니다. Bee trace의 **Csc action은 한 개**이며 새 `Splinter.Workspace.Editor` assembly를 **13.0127초**에 컴파일했습니다. IL 후처리 68개의 **구간 합집합은 51.4177초**, 그래프 생성은 **41.8336초**, 그 안의 `Backend.Write`는 **40.7893초**, JSON 직렬화는 **3.2112초**입니다. 서로 중첩되는 경과 구간이며 배타적 CPU 비용이 아닙니다. 최초 검증의 **ILPP-Configuration에는 60.1323초**가 포함됩니다. 추가 진단은 아래에 기록합니다. CompileScripts를 C# 컴파일러 계산 210초 또는 수십 개 C# 재컴파일로 설명하면 안 됩니다.

## 남은 최적화 여지

### 0. 오래된 그래프를 거부하기 전에 시작된 ILPP의 60초 대기

A의 최초 Bee 검증은 **72.91초**로 UP의 14.58초보다 길었습니다. 상대 시간 **10.134492초**에 ILPP-Configuration, **10.155641초**에 `CheckDagSignatures`, **10.162926초**에 main thread의 `JoinBuildThread`가 시작됐습니다. DAG 검증을 미룬 실행에서 기존 graph의 작업이 검증보다 약 21ms 먼저 시작됐습니다. 입력 변경을 발견한 뒤 이미 시작한 worker를 기다렸으며 ILPP **60.132280초**와 Join **60.131790초**는 중첩됩니다. B도 같은 패턴이며 ILPP는 **60.152초**입니다. graph 재생성 뒤에는 **1.332초 / 1.197초**에 끝났고 UP의 최초 검증에는 ILPP 작업이 없었습니다.

보존된 기준 graph의 response payload는 재생성된 A/B graph와 다른 과거 ILPP endpoint를 포함합니다. 오래 실행된 Trigger의 sampled CPU는 **3.734초 / 3.891초**, read는 각각 **210,420 bytes**, other I/O는 약 **60,000회**입니다. 오래된 endpoint의 재시도/timeout 가설에 부합하지만 실제 첫 실행의 response-file 사본·Trigger stdout·내부 stack이 없으므로 정확한 연결 대상과 timeout 원인은 **미확정**입니다. 시각 재사용 때문에 그 과거 작업이 실행 가능해졌다는 것도 추론이며 분리 검증한 인과관계가 아닙니다.

다음 한정 조사는 첫 실행을 확인하고, 프로세스별 오래된 작업을 실행 전에 거부할 수 있는 지원된 방법을 확인하는 것입니다. Splinter는 독립 프로세스 시작을 위해 캡처된 `Library/ilpp.pid`를 이미 제거하지만 Bee DAG/response 파일은 다시 쓰지 않습니다. 원본 endpoint 공유·의존성 검사 약화·추측에 의한 cache 편집은 정당화되지 않습니다. 큰 추가 지연 후보이며 60초 절감이 증명된 것은 아닙니다.

### 1. Library 존재 확인과 일반 파일 Close

첫 60초 provider trace에는 **filesystem callback thread residence 243.9419초**가 있습니다. Monitor 진입은 **156.5559초**, `Directory.Exists` leaf는 **41.3988초**입니다. 해당 디렉터리 존재 확인 표본 모두에 `OwnedDirectorySnapshotTree.EnsureAvailable`이 있습니다. `JournalDeltaStore.Check`와 `GraftedSnapshotTree.EnsureAvailable`을 통해 Open·List·GetFileInfo·Close 등에서 실행됩니다. harness의 trace 이름은 `early-scan`이지만 스캔·컴파일 준비에 걸치며 순수 Scan 구간이 아닙니다.

소유 Library root와 조상 핸들을 reader 수명 동안 고정하고 identity 증명으로 중복 경로 조회를 줄이는 방향이 유력합니다. 교체·rename·reparse·임대/회수·내용·I/O 오류 guard를 유지해야 합니다. bool이나 일정 시간 동안의 존재 여부 cache만으로 대체하면 안 됩니다.

일반 연결 파일의 Close도 `ReleaseUnlinkedFile`을 호출하고, 회수할 unlink가 없다는 판단 전에 전체 availability를 확인합니다. gate 안의 disposed/poison/journal-length 검사를 유지하는 빠른 반환과 실제 회수의 전체 availability/직렬화를 분리할 수 있습니다. 수명 증명 없이 외부 retention lock을 제거하면 안 됩니다.

**Monitor 표본 150.1149초는 제품 프레임이 생략**되어 정확한 lock 객체를 확정하지 못했습니다. callback과 코드상 List/GetFileInfo/GetSecurity는 journal gate가 유력하고 Open/Close는 외부 mutation gate도 가능합니다. thread 사이 대기는 중첩되며 제거 가능한 경과 시간이 아닙니다.

### 2. 작업공간 준비의 반복 소스 작업

A의 첫 소스 검사는 **86.2119초**였습니다. 파일 1,602개, raw payload 2,804,769,928 bytes를 hash한 뒤 같은 바이트 수를 Git hash 프로세스 7개에 전달했습니다. 응답 검사는 **12.2740초**이며 hash 1,602개를 재사용하고 **payload는 0 bytes** 읽었습니다. 두 관측의 fingerprint·provider epoch·sequence 2가 같습니다. 합계 **98.4859초는 A 준비 242.8974초의 일부**이지만 모두 제거 가능한 것은 아닙니다.

`GitSourceFingerprint`는 raw SHA256과 Git clean-content hash를 별도로 구합니다. 파일별 Git attributes/EOL/ident/filter가 동일 바이트 변환임을 증명할 때만 한 번 읽기로 합칠 수 있으며 나머지는 Git 처리를 유지해야 합니다. HEAD/index/구성원/해석/provider generation이 같음을 재확인한 연산 한정 증명으로 응답의 metadata 재순회·manifest 재게시도 줄일 여지가 있습니다. 시각이나 HEAD blob identity만으로 내용 검증을 대신하면 계약이 약해집니다.

별도 A 준비 30초 trace에는 `HashRangeSourceAsync` 20.8479 sampled thread-seconds와 plain-path 검사 5.0704초가 있습니다. 파일 열기와 반복 조상 확인도 고정 핸들 재사용 후보입니다. 포함 시간은 중첩됩니다. 등록 단계 `update-index --refresh` 비용은 분리하지 못했습니다.

### 3. 컴파일 중 저널 게시와 출력 I/O

컴파일 60초 trace의 callback thread residence **44.4705초 중 Write는 38.4287초**이며 Monitor 진입은 **0.4899초**뿐입니다. 게시의 배타적 sampled residence는 **14.2740초 / 포함 20.5180초**, durable flush는 **9.1426초**, 파일 열기·핸들 닫기는 약 4.9초·4.6초입니다. 이전의 저널 조상 경로 반복 순회 병목은 두드러지지 않습니다.

`LatestJournalStorage.Publish`는 `ReadFrame` 검증 뒤 `Index`에서 같은 root를 다시 엽니다. namespace/block/hash/길이/세대 검증, 원자적 게시와 승인된 각 mutation의 durable flush를 유지하면서 한 검증 핸들을 재사용할 수 있습니다. Publish 전체가 `File.Replace` 비용이라는 근거는 없고 게시 재시도 sleep 표본도 발견되지 않았습니다. 이 근거로 group flush나 ACK 약화를 정당화하지 않습니다.

### 4. Git 읽기는 후순위

시작 중 provider transfer read는 **69.39 / 71.52GiB → 28.15 / 24.75GiB**, 관측 Git process identity는 **839 / 832 → 49 / 48**로 줄었습니다. Win32 transfer는 pipe/cache/device를 포함하고 identity는 launcher/child 관측을 포함하므로 물리 디스크 bytes나 decoder 시작 수가 아닙니다. 초기 trace에서 Git Read는 약 3.28초이며 Git Monitor 진입은 관측되지 않았습니다.

최초 전체 blob 검증 중 첫 요청 chunk 보존, 완료된 idle decoder 우선 선택, 공유 gate 밖의 reader 회수는 특정 접근 패턴에서 유용할 수 있습니다. payload/proof 메모리 상한과 종료·복구 검증이 필요합니다. 이번 입력의 chunk proof 예상 상한은 약 1.36MiB로 기존 4MiB / 4,096개보다 작으므로 cache 확대가 우선 원인은 아닙니다.

## 종료와 측정 한계

정상 stop 명령 두 개는 기존 **120초** 제한을 초과했습니다. 같은 Editor가 추가 stop이나 강제 종료 없이 **287.4339초(4분 47.4초)** 뒤 정상 종료했습니다. UP의 226.6012초보다 느리며 별도 문제로 남습니다.

A/B checkpoint는 **59.8131초 / 58.2084초**, release는 **102.0764초 / 101.3695초**이며 순차 보존·회수 cohort는 **321.4743초**입니다. 원본 Library 복원은 **114.7703초(1분 54.8초)**였습니다. 준비부터 복원까지 실제 경과는 진단·빌드·커밋 간격을 포함해 **2,279.6423초(37분 59.6초)**이므로 중단 없는 성능 기준선으로 읽지 않습니다. stop 실패·최초 시작 거부를 이후 성공과 함께 보존합니다.

두 task는 `released`이며 독립 결과 tree는 변경하지 않은 소스 tree와 같습니다. 정확한 소유 Editor/provider·mount·task delta·Git worktree 등록이 없습니다. 원본 tracked 파일 1,602개의 바이트와 HEAD/branch/기존 refs를 확인했습니다. Library는 원래 볼륨의 일반 NTFS 디렉터리이며 fixture-local Git 설정도 복원했습니다. 전역 설정과 두 고정 runtime bundle hash, **무관한 dirty 파일 27개의 작업 전 hash**를 유지합니다. 실패 네 개를 포함한 **실제 CLI 응답 14개 전체**가 현재 schema validator를 통과했습니다(집중 검사 1개 통과). 필수 build의 조건부 skip을 새 외부 입력의 자격으로 승계하지 않습니다.

원시 근거는 ignored `artifacts/unity-startup-remeasurement-20261008/`와 `.local/us8/`에 있습니다. provider는 10초, 소유 프로세스는 2초 간격으로 관측했습니다. 겹치지 않는 provider EventPipe 60초 구간 두 개와 A 준비 CLI 30초 구간 한 개에 16MiB buffer를 사용했습니다. sampled thread residence·중첩 Unity/Bee 구간·CPU/I/O 계수를 배타적 경과 시간처럼 합산하지 않습니다. 첫 trace 이름이 scan-only 자격을 의미하지 않습니다.

cache·스케줄링·프로파일러 비용을 무작위 반복으로 통제하지 않았고 수정 중단도 cache에 영향을 줄 수 있습니다. A/B는 병렬 작업이며 통계적 반복이 아닙니다. SU 변경별 효과나 일반 Unity 개선율을 증명하지 않습니다. 최초 거부나 느린 종료를 감추기 위한 추가 전체 cohort는 실행하지 않았습니다. 위 후속 후보는 분석이며 구현·측정된 개선이 아닙니다.
