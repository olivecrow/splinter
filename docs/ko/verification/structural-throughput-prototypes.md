# 구조적 처리 시간 시제품

[English](../../verification/structural-throughput-prototypes.md)

**현재 처리 상태:** 두 Bee 구현을 모두 폐기·제거했다. 이 junction은 원자 게시에 실패했고 후속 [BN 후보](bee-internal-native.md)는 시간·공간 기준에 실패했다. 구현은 커밋 `93a7395`에 보존한다. 아래 결과는 과거 근거이며 당시 Bee 명령은 현재 코드에서 사용할 수 없다. 제한 로그 도구와 [제품 제한 저널](../bounded-journal.md)은 유지한다.

이 개발 전용 실험은 [TP-06](unity-throughput-optimization.md)과 [SX 고정 범위](../plans/structural-throughput-prototypes.md)의 후속이다. 제품 저널 형식·저장 기본 정책·작업공간 수명은 변경하지 않았다. 소스 기준은 `dd188e1`이며 실행 파일·원시 결과 hash로 측정 후보와 이후 빌드를 구분한다.

[고정 비교 데이터](../../../reports/benchmarks/structural-throughput-prototypes-20261015.json). SX-02는 한정한 내용·복구 실험을 통과했다. SX-01은 그래프·소스 보호 실험을 통과했지만 **실제 Bee 컴파일은 실패**했다. SX-03은 이 경계를 구분하며 SX-04는 제품 backend 채택이나 전체 작업 속도 향상을 승인하지 않는다.

## 제한 범위 로그: SX-02·SX-03

실제 현재 SJL4 구현과 작은 제한 WAL에 동일한 고정 원본·변경을 적용했다. 논리 파일 20,000개, hot 파일 256개, 최초 private 범위 48MiB, 이후 압축되지 않는 4/8/16KiB 쓰기 4,096회다. 매 성공 응답 전에 개별 durable flush를 수행한다. WAL은 현재 private 범위를 RAM에 유지하고 검증 가능한 범위 기록을 추가하며, 로그 상한에서 현재 private 전체 checkpoint로 교체한다. 불변 원본을 checkpoint에 복제하지 않는다. 마지막 통합과 이전 파일 회수를 쓰기 시간에 포함했다.

빌드·다른 시험과 겹치지 않는 직렬 한 묶음의 결과:

| 단계·비용 | SJL4 | WAL, 4MiB 상한 | WAL, 16MiB 상한 |
| --- | ---: | ---: | ---: |
| 초기화·private 범위 준비 | 70.582s | 21.949s | 11.090s |
| 최종 checkpoint 포함 4,096회 쓰기 | 92.871s | 26.970s | 13.872s |
| SJL4 대비 쓰기 시간 감소 | — | 71.0% | 85.1% |
| 현재 내용 검증 | 1.032s | 0.516s | 0.683s |
| 디스크 상태 재연결·검증 | 6.893s | 0.737s | 0.746s |
| 독립 결과 export | 0.983s | 0.298s | 0.389s |
| 결과를 보존한 소유 저장소 제거 | 1.206s | 0.010s | 0.009s |
| 위 계측 단계 합계 | 173.566s | 50.480s | 26.790s |
| 쓰기 단계까지 관측한 프로세스 최대 working set | 100.0MiB | 162.0MiB | 162.5MiB |
| 표본 최대 소유 파일 bytes | 미측정 | 101.4MiB | 113.3MiB |
| 측정 쓰기 구간의 기록 bytes | 104.1MiB | 524.0MiB | 183.9MiB |
| 개별 쓰기 응답 p50 / p95 | 20.62 / 35.38ms | 3.40 / 11.13ms | 3.07 / 3.71ms |
| 가장 긴 개별 응답 | 183.3ms | 942.9ms | 349.4ms |

합계는 fixture 생성·프로세스 시작을 제외하며 제품 `checkpoint/release`나 전체 Unity 시간이 아니다. 파일 bytes 최대값은 transaction/checkpoint 시점 표본이며 NTFS 최대 할당이 아니다. 기록 bytes는 파일시스템·장치 부가 쓰기를 제외한다. 큰 로그는 checkpoint 빈도를 낮추지만 더 많은 과거 범위를 허용한다. checkpoint 일시 정지 때문에 전체 쓰기는 빨라도 가장 긴 응답은 늘 수 있다. 16MiB 설정은 private payload 48MiB를 RAM에 유지한다. 16MiB는 로그 상한이지 메모리 상한이 아니다.

자체 시험 24개가 통과했다. 정상 내용·재연결·독립 결과, 외부 durable ACK 기록을 대조한 WAL 12개/SJL4 6개 소유 자식 중단 경계, 불완전 header 복구, 완전 frame/checkpoint 손상 거부, 다른 소유자·unknown 파일 보존을 포함한다. 측정한 모든 방식의 전체 논리 hash가 같고 불변 원본이 유지됐으며, 저장소 제거 후 독립 archive도 검증했다. 프로세스 중단 근거이며 전원 장애 근거는 아니다.

WAL은 고정 객체의 내용 범위만 구현한 축소 후보다. 파일 생성·삭제·rename, SJL4의 전체 metadata, 동시 writer, 매 live 읽기의 디스크 손상 증명, 제품 수명 연결은 아직 없다. live 읽기는 RAM 인덱스를 사용하고 재연결에서 디스크 상태를 검증한다. 따라서 이 비율은 구조적 구성 요소의 개선 가능성이며 완성 제품을 바로 교체할 때의 속도가 아니다. OS cache를 초기화하지 않았고 상한별 표본은 한 번이다. 상한 간 비율로 인과 효과를 분리하지 않는다.

원시 결과는 `artifacts/bounded-log-probe-20261015/measured-{0-sjl4,1-wal4,2-wal16}/result.json`, 자체 시험은 `selftest-02/selftest-result.json`이다. 앞선 `full-*` 실행은 진행자가 종료 확인 전에 다음 실행을 시작해 겹쳤으므로 진단 자료로만 보존하고 표에서 제외했다. SJL4 원시 `inventory`는 root 직속 항목만 나열한다. 현재 전체 저장 bytes는 그 목록 합계가 아닌 검증된 `Observe().journalBytes`를 따른다.

## Native Bee: SX-01·SX-03

정확히 소유한 `Library/Bee`만 일반 NTFS로 노출하며 소스와 나머지 Library는 현재 범위 저널을 사용한다. 고정 Unity 6000.3.22f1 입력은 assembly 3개·package 0개·IL 후처리 없음이다. Editor 없이 실제 설치된 그래프 생성기와 Csc를 사용한다. 설치 도구·과거 입력·형제 Unity 프로젝트는 편집하지 않았다. 양쪽 모두 빈 Bee 출력으로 시작하고 원본 Library payload를 복사하지 않는다.

최종 **그래프 전용** 묶음은 overlay/native/native/overlay 순서로 새 view마다 그래프를 세 번 생성했다. 빌드·다른 시험은 종료된 상태다. 방식별 새 view 두 개의 평균:

| 작은 그래프 실험 | 현재 overlay | Native Bee graft |
| --- | ---: | ---: |
| 생성 출력이 없는 첫 그래프 | 1.311s | 0.476s |
| 이후 그래프 다시 쓰기 | 1.559s | 0.494s |
| 그래프 여섯 번 전체 평균 | 1.476s | 0.488s |
| 준비 | 0.322s | 0.374s |
| 독립 보존·정상 종료 | 0.460s | 0.153s |
| view당 그래프 세 번의 계측 단계 합계 | 5.341s | 2.101s |
| provider 시작부터 종료까지 경과 시간 | 5.615s | 2.225s |
| 최종 소유 일반 파일의 NTFS 할당 | 476KiB | 636KiB |

그래프 시간은 **67.0% 감소**했으며 이 작은 입력의 최종 할당은 33.6% 증가했다. 단계 합계는 입력·소스 편집을 포함하고 evidence 복사·비교·호출 사이 간격을 제외한다. 경과 시간은 그 간격을 포함하되 마지막 할당 검사를 제외한다. 둘 다 컴파일은 포함하지 않는다. 할당은 종료 뒤 delta/native-Bee 일반 파일에 `GetFileInformationByHandleEx(FileStandardInfo).AllocationSize`를 사용한다. fixture·도구·소스 export·영수증·evidence·파일시스템 디렉터리 metadata는 제외하며 최대 임시 할당은 **NotMeasured**다.

네 view 모두 정규화한 그래프/payload hash가 같고 불변 입력·검증한 독립 소스 결과를 유지했다. 정상 unmount·핸들/context 0·provider 오류 없음도 확인했다. 최종 실제 mount `selftest-06`은 target/조상 보호, peer 보존, mapped 쓰기, live 실행 이미지 거부, 원자 rename 미지원 시 소스 유지·목적지 부재도 검증한다. 이 검사로 컴파일을 지원한다고 읽지 않는다.

원시 묶음은 `artifacts/sx-20261015/bee-measured-02/`다. 앞선 그래프 묶음은 평균 1.507s/0.530s(64.8% 감소)로 방향이 같았다. 그 `allocatedBytes`는 `GetCompressedFileSizeW`를 잘못 사용해 물리 공간 근거에서 제외한다. 마지막 harness에서 계측을 고치고 짧은 묶음을 반복했다. OS·컴파일러 cache를 초기화하지 않았으며 적은 관측만으로 통계적·전체 Unity 자격을 주장하지 않는다.

첫 독립 실행은 Unity의 parent-IPC 옵션을 유지해 컴파일 전에 거부됐다. 두 번째는 컴파일과 상수 `42 → 43`을 검증했으나 overlay에 `1900b0aE.dag_derived`의 매핑 context 하나가 남았다. 정확한 프로세스의 읽기 전용 조사에서 컴파일러 DLL이 아닌 그래프 cache 데이터 파일을 확인했다. 그 기존 파일을 write-data/read-attributes 접근으로 열되 내용·metadata를 쓰지 않고 닫자 자연 회수가 가능했다. 파일 내용·metadata와 전체 basis/delta inventory가 그대로였고 정상 unmount 뒤 독립 결과도 보존됐다. 두 실패 pilot 모두 정상 전체 시간 표본으로 사용하지 않는다. `artifacts/sx-20261015/recovery-pilot02/closure.json`을 따른다.

이후 정확성 재현에서 먼저 시제품 오류를 발견했다. 기존 보호 디렉터리를 만들려 하면 이름 충돌 대신 접근 거부를 반환했다. 기존 보호 디렉터리에만 충돌을 반환하도록 고쳐 삭제·교체·rename guard를 유지하면서 재귀 `CreateDirectoryW`를 정상화했다. 다음 재현에서는 native 쪽이 Csc를 포함해 164개 빌드 노드 중 163개를 실행했으나 `TundraBuildState.state`와 digest cache 게시에 실패했다. 성공한 빠른 컴파일이 아닌 빌드 실패다.

정확한 Win32 대조에서 남은 구조적 한계를 분리했다.

| 같은 디렉터리의 `MoveFileExW` 연산 | 관측 결과 |
| --- | --- |
| 상대·절대 view 경로, 복사 fallback 없음 | 새 이름·기존 파일 교체 모두 오류17 `ERROR_NOT_SAME_DEVICE` |
| view 경로에 `MOVEFILE_COPY_ALLOWED` 사용 | 성공하지만 file ID가 달라짐: 원자 rename이 아닌 복사·삭제 |
| 직접 absolute native target 경로 | 같은 file ID를 유지하며 성공 |

전체 WinFsp 프로젝트 root 아래의 NTFS graft는 실제 Bee backend에 필요한 원자 rename 의미를 제공하지 못한다. 앞선 `.NET File.Move` 자체 시험은 복사 fallback이 경계를 감출 수 있어 원자성 근거가 아니다. 복사 fallback이나 외부 도구 수정은 채택하지 않았다. 진단 provider 두 개와 컴파일 실패 provider는 원본·소스를 보존하고 정상 종료했다. 근거는 `artifacts/sx-20261015/native-rename-diagnostic-exact/rename.json`과 `bee-correctness-04/run-0-native-bee-failure.json`이다.

Native Bee는 SJL4의 매 쓰기 durable 저널 ACK 대신 일반 NTFS buffering과 마지막 출력 flush를 사용한다. 이 내구성 차이는 재생성 가능한 Bee 출력에 한정하며 소스 보존은 저널을 유지한다. mount-point 실험은 OS 보안 sandbox가 아니며 임의의 외부 data mapping 부재를 증명하지 않는다.

## 자격 경계: SX-04

제한 로그는 RAM·임시 공간·쓰기 증폭을 감수하면 구성 요소에서 큰 이득이 있음을 보였다. 제품 적용에는 빠진 metadata·수명·복구 의미를 연결해야 한다. NTFS graft는 그래프 생성 이득과 별개로 실제 Bee 컴파일용으로 불합격이다. 경로 guard를 완화해도 볼륨 경계를 넘는 원자 rename 실패는 해결되지 않는다.

SX 마감 당시 대안은 물리 NTFS 프로젝트 root 또는 같은 가상 볼륨 안의 native Bee payload였다. 후자는 이후 [BN](bee-internal-native.md)에서 구현·측정했으나 시간·공간 증가로 제거했다. 물리 root 대안은 구현하지 않았다. 이러한 과거 대안은 현재 선택적 경로나 승인한 후속 작업이 아니다.

이전 313.1508초 공유 Editor 대비 전체 Unity 속도 우위는 **NotRun**이다. 이 부품 수치로 README의 전체 작업 시간을 교체하거나 두 시제품의 단축률을 합산하지 않는다.

## 회귀 검증

`pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드·전체 시험이 통과했다. 총 1,639개 중 성공 1,615개·실패 0개·조건부 건너뜀 24개다. 마지막 probe 전용 mkdir/rename 후속은 별도 빌드와 실제 mount 자체 시험으로 검증하며 제품 저장 코드·기본값은 그대로다. 전체 근거는 `artifacts/sx-20261015/build.log`와 `full-regression.trx`다.
