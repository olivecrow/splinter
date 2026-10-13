# 시간·RAM 최적화

[English](../../verification/time-memory-optimization.md)

TM-01–04는 [UF-01](unity-final-lifecycle.md)의 후속 최적화다. 전체 Unity 작업 시간은 **`116fd44`에서 기록한 1,083.0s**이며, 새 후보의 Unity Editor 두 개 실행 시간은 측정하지 않았다. [데이터](../../../reports/benchmarks/time-memory-optimization-20261014.json)에 작은 입력 비용, runtime/source hash, 검증과 한계를 기록한다. 소스 기준은 `5fab578`, 실제 전후 비교의 고정 기준 runtime은 UF의 `116fd44`다.

## 변경과 유지한 보호

| ID | 변경 | 한도와 보호 |
| --- | --- | --- |
| TM-01 | 캡처·Library 검증·변경 없는 디렉터리 복원에서 하위 디렉터리를 한 번 고정하고, 고정 부모를 통해 각 leaf를 검증한다. Provider admission은 최대 두 worker로 hash한다. | Native identity/type/hardlink/reparse/final-path 검사를 유지한다. 실패 시 모든 worker가 끝난 뒤 핸들을 닫는다. 캡처·최종 복원 hash는 직렬이며 복원 인계는 이전 proof를 닫고 이동 후 새로운 전체 proof를 만든다. 추가 디렉터리 핸들은 디렉터리 수에 비례한다. |
| TM-02 | Git tree마다 요청한 검증 청크를 16MiB·4,096-entry RAM LRU에 지연 보관한다. | 첫 접근은 전체 blob을 증명한다. 미게시 청크도 같은 예산에 포함하며 proof 게시 전에 재사용하지 못한다. 정확한 object/길이/SHA를 대조하고 proof 퇴출은 payload도 회수한다. 오류·close는 reader 종료를 기다리고 cache를 비운다. 기존 4MiB proof·decoder 프로세스 두 개·256KiB window·최대 1MiB 요청 buffer 두 개는 유지한다. 보관한 불변 바이트는 매번 전체 object의 현재 손상을 감사한 증거가 아니다. |
| TM-03 | Journal store마다 해제한 압축 block을 64MiB·4,096-entry RAM LRU에 지연 보관한다. | 예산에 진행 중 할당과 entry당 512바이트 여유를 포함한다. 매 hit에서도 현재 encoded bytes를 읽고 형식/길이/SHA를 검증한다. 전체 replay/seal/export 순회와 mutation마다 durable flush·ACK는 유지한다. 읽기 오류·poison·close 시 cache를 비운다. 교체된 decoded bytes는 한정 LRU 안에 남을 수 있으나 물리 offset만으로 hit를 허용하지 않는다. |
| TM-04 | 에이전트가 공유 Library를 한 번 준비한 후 독립 `ensure` 요청을 병렬 실행한다. | 다른 고정 key, 같은 source/home/base/basis, 개별 응답 검사와 작업별 단일 작성자를 유지한다. Fake barrier로 실행 겹침과 한쪽 attach 응답 유실 후 같은 작업 복구를 증명한다. 기존 task lock·명령을 사용하며 scheduler·queue·자동 모드를 추가하지 않는다. |

Cache 예산은 보관하는 관리 payload/entry 한도이며 전체 프로세스 메모리나 GC 잔류량이 아니다. Workspace에 store/tree가 여러 개 있을 수 있으므로 전체 workspace 한도도 아니다. `readCacheBytes = 0`은 계속 디스크 payload cache를 뜻한다. 형식/schema·읽기 hydration·파일 전체 copy-up·서비스 설정·외부 Unity 도구·flush 정책은 변경하지 않았다.

## 한정 비용

Git은 4MiB packed blob 두 개에 대해 역방향/교차 4KiB 요청 128회를 실행한다. 최초 전체 proof는 별도로 기록하며 반복 읽기에는 정확한 내용 assertion을 포함한다. 저널은 1MiB block 8개에서 부분 4KiB 범위를 512회 읽는다. Library admission은 warm admission 뒤 파일 512개·64MiB를 사용한다. 비용 측정 중 다른 빌드나 성능 probe를 동시에 실행하지 않았다. OS cache는 비우지 않았으며 소수 작은 표본을 일반·cold disk 절감률로 해석하지 않는다.

| 작업 | 이전/꺼짐, 초 | 현재/켜짐, 초 | 관측 단축률 |
| --- | ---: | ---: | ---: |
| Git 반복 역방향 읽기 | 7.751726 | 0.757531 | 90.23% |
| 저널 반복 부분 읽기: 0/64MiB 두 회 평균 | 0.674372 | 0.027294 | 95.95% |
| Library 전체 admission: 이전/두 worker 세 회 평균 | 0.366782 | 0.123999 | 66.19% |

Git 최초 전체 proof는 0.255684s / 0.254837s다. 반복 구간 해독량은 48,234,496 → 6,029,312바이트, reader 시작 횟수는 64 → 8이며 동시 reader 상한은 두 개다. 이 표본의 추가 cache payload peak는 1MiB다. 저널 해제 횟수는 512 → 8이고 매 실행 encoded 읽기는 동일한 1,099,648바이트다. Decoded payload는 8MiB, entry 포함 계상 peak는 8,392,704바이트다.

Library 새 1/2/4-worker 평균은 각각 0.151393s / 0.123999s / 0.109777s다.

저널 32/64/128MiB cache가 모두 같은 8MiB 입력을 보관하므로 이 표본은 64MiB가 최적임을 증명하지 않는다. 제품 기본값은 한정된 최초 선택이다. Library 1/2/4-worker 비교도 작은 warm 입력이며, 기본 두 worker는 provider별 동시 I/O를 제한하면서 경로 proof 개선을 유지한다. Scratch buffer는 합계 128KiB이며 디렉터리 핸들과 metadata는 별도다.

## 실제 독립 A/B

일반 사용자 WinFsp 시험은 전용 clean local Git 입력을 사용한다. 소스 payload 64파일·1MiB, 설정 파일 4개, Library 512파일·32MiB다. Unity를 열거나 사용자 원본 프로젝트를 조작하지 않는다. 매 새 실행에서 Library 준비, A/B ensure, 서로 다른 파일 작성, A checkpoint/release 중 B 읽기 지속, B checkpoint/release, 원본 Library 복원을 수행한다. 독립 Git 결과, 원본 전체 파일 hash, 동일 HEAD·clean checkout, 성공한 terminal 응답, 양쪽 release의 소유 자원 0을 검증한다.

| 단계, 초 | 이전 순차 | 현재 순차 | 현재 병렬 |
| --- | ---: | ---: | ---: |
| Library 준비 | 9.118 | 8.801 | 8.859 |
| Ensure A/B 묶음 | 58.308 | 57.870 | 32.191 |
| A checkpoint | 18.507 | 18.801 | 19.562 |
| A release | 10.977 | 12.310 | 12.237 |
| B checkpoint | 18.117 | 18.528 | 19.293 |
| B release | 11.024 | 11.815 | 12.233 |
| Library 복원 | 1.749 | 1.019 | 1.157 |
| 전체 작업 | 128.257 | 129.665 | 106.004 |

새 코드만으로는 한 번씩 처리하는 작은 전체 작업이 줄지 않았다(128.257s / 129.665s). 현재 ensure 두 개를 겹쳐 실행하면 묶음 시간이 57.870s → 32.191s로 44.37% 줄었고, 전체 작업은 106.004s로 기준보다 17.35% 짧았다. 호출 조율의 절감과 별도 반복 읽기 cache의 이득을 구분한다.

입력 생성과 마지막 전체 hash assertion은 전체 작업 타이머 밖이다. 타이머에는 CLI 호출, 응답 저장, 명령 사이의 독립 결과·peer 검사를 포함한다. Ensure 묶음은 순차 두 호출 전체 또는 병렬 두 호출의 시작부터 모두 완료할 때까지다. 실행 순서는 기준 순차/현재 순차/추가 현재 순차/현재 병렬이며 무작위 비교가 아니다. 추가 실행 `after-parallel-1`은 `--parallel`을 실수로 생략했으므로 기록된 `parallel=false` 결과(133.270s)를 순차 근거로 보존하고 병렬 측정에 사용하지 않는다. 병렬 열은 `after-parallel-2 --parallel`만 사용한다. 새 후보의 전체 Unity import·compile·Editor 종료·전체 프로세스 peak RAM은 **NotRun**이다.

## 검증과 관리자 경계

필수 `pwsh -NoProfile -File scripts/build.ps1`의 locked restore, 경고/오류 0개인 Release build, **성공 1,531 / 실패 0 / 조건부 생략 23**을 통과했다(전체 1,554, 시험 시간 434.028s). 선택 비용 시험 3개와 최종 Library 비용 재측정도 통과했다. 실제 네 묶음의 **terminal CLI 응답 32개**가 현재 응답 schema를 통과했다. Workspace release 8개 모두 소유 자원/핸들/mount/provider/delta/basis 부재와 독립 결과 보존을 확인했고, Git 등록은 primary worktree만 남고 task basis ref는 모두 회수됐다. 모든 묶음에서 원본 전체 파일 hash와 clean HEAD를 복원했다.

Cache 예산·퇴출, 최초 proof 미게시, 불변 보관 바이트와 cache 미적중 시 손상 감지 구분, cache hit 후 실제 encoded 손상, overwrite/resize/rename/unlink/reopen, 읽기 실패·동시 close, 빈/하위 디렉터리 고정, junction 교체 거부, 실패/취소한 hash worker 종료 대기, 복원 인계, 준비 응답 유실 후 같은 작업 재시도를 검증했다. 초기 집중 시험에는 공유 위반 오류 종류 회귀 1건이 있었다. 읽기 전용 캡처/admission에서 기존 `IOException`과 native HRESULT를 보존하도록 수정하고 해당 assertion을 유지한 최종 전체 시험을 통과했다. 초기 빌드·집중 시험 실패 로그는 원시 근거에 보존한다.

별도 관리자 **읽기 전용** 점검에서 C:의 기존 snapshot 3개, 실행 중인 storage-authority 서비스, grant 7개를 확인했다. 이전 T: 시험 볼륨은 없으며 새 입력은 VSS grant 대상이 아니다. 기존 snapshot/회수 보호를 유지했고 snapshot을 삭제·변경하지 않았다. 새로운 C: VSS 비교나 서비스 업데이트는 하지 않았다. VSS 실험에는 격리되고 명시적으로 허용한 입력/볼륨과 별도 검증이 필요하며, 관리자 사용 가능 여부만으로 현재 Git 경로가 빨라지지는 않는다.

세 선택 비용 probe는 `SPLINTER_GIT_PAYLOAD_COST_OUTPUT`, `SPLINTER_JOURNAL_DECODE_CACHE_COST_OUTPUT`, `SPLINTER_OWNED_DIRECTORY_ADMISSION_EVIDENCE`에 **새** 출력 경로를 설정하고 Release assembly의 해당 `RepeatedPackedBackwardReadCost`, `RepeatedPartialReadCostEvidence`, `AdmissionCostForOneTwoAndFourHashWorkers` 시험을 실행한다. Probe는 순차 실행한다. 데이터에는 실제 A/B harness 경로/hash와 정확한 명령 근거를 보존하며 이전 근거를 덮어쓰지 않는다.
