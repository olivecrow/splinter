# 제품 제한 로그 검증

[English](../../verification/bounded-journal.md)

**2026-10-15: scoped-complete-with-limitations.** [고정 범위](../plans/bounded-journal.md)와 [제품 계약](../bounded-journal.md)의 BL-01–05를 소유한다. 이전 고정 object 시제품은 `a8eb778`로 커밋했으며 SX 시간은 과거 기록으로 유지한다. 이번 후보는 실제 mutation/state/복구 구현과 새 일반 Windows provider 기본값을 사용한다. BL 마감 당시 Bee native backing은 [미구현 설계](../plans/bee-native-storage-design.md)였다. 이후 [BN 실험](bee-internal-native.md)에서 개발 후보를 구현했으며 성능 기준 실패는 별도로 기록한다.

## 근거 상태

| ID | 근거 | 상태 |
| --- | --- | --- |
| BL-01 | Mutation metadata·Unix mode·범위/resize·디렉터리/교체 rename·whiteout·열린 unlink ID·byte/frame 제한·불변 읽기 지속·cache·원본 보호 | 명시한 범위 통과 |
| BL-02 | 같은 owner 초기화/체크포인트 복구·poison writer·손상/unknown 보존·정확한 회수 inventory·회수 후 독립 결과·외부 프로세스 종료 | 명시한 범위 통과 |
| BL-03 | Provider 기본값·schema·생략/명시 compact·정확한 회수·변경 없는 Library v2 디렉터리 plan·실제 editorless A/B | 명시한 범위 통과 |
| BL-04 | 동일 입력 SJL4/제품 bounded private 범위의 직렬 비용 | 직렬 구성 요소 한 쌍 통과 |
| BL-05 | 필수 locked restore·Release build/전체 회귀·영/한 문서 | 명시한 범위 통과 |

## 안전성 근거

최종 필수 `pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드는 경고/오류 0개이며, 전체 **1,691 통과 / 실패 0 / 조건부 skip 24 (합계 1,715)**다. 새 집중 검사는 bounded 저널 36개와 정확한 회수·native 모드 전환을 포함한다. 최종 Runtime hash `a8c03203733f23edbd01437ef4656d02165efcc5fcc987a29edeac7ee5cc2f89`는 테스트·probe·CLI·실제 A/B runner에서 동일하다. 앞선 전체 실행은 추가 복구/native 전환 수정 전에 1,686 통과 / 실패 0 / skip 24였다.

최종 editorless 실제 제품 fixture는 커밋한 1MiB 파일과 text sentinel을 사용한다. A가 64KiB 범위 쓰기를 384회 각각 flush해 16MiB suffix 한도를 넘긴 뒤 새 파일을 rename한다. 관측한 mutation append는 25,320,509B이고 남은 suffix는 8,571,804B / 130 frame이다. B와 원본의 초기 데이터 hash는 유지된다. 옵션 없는 compact는 bounded를 유지하고 명시적 `--optimize-journal false`는 B를 append로 바꾼다.

A/B는 이어서 `workspace checkpoint --release`를 사용한다. A 회수 후 B가 다시 쓰고, 양쪽 mount/delta와 소유 basis ref를 회수한 뒤에도 독립 Git 결과를 읽을 수 있다. 원본 HEAD·status·payload hash는 동일하다. 최종 task ID는 `ca62657d74bfb6f4b3351886`, `8de9b0357ce77381b761c55e`다. 자동 회귀와 겹쳐 실행했으므로 정확성 근거이며 성능 수치는 아니다. 앞선 성공 A/B fixture도 별도로 보존했다.

`product-selftest`는 정확히 소유한 자식을 append 5경계와 자동 checkpoint 게시 7경계에서 외부 종료한다. **12/12**에서 외부 관측 ACK prefix를 복구하며, 완전히 기록됐지만 외부 ACK 기록에 도달하지 못한 마지막 mutation 하나만 추가 허용한다. 원본 내용은 유지됐다. 최종 원시 기록은 `artifacts/bl-20261015/process-recovery-03/product-selftest-result.json`이다.

실패한 프로세스 실행에서 실제 복구 결함을 발견했다. 읽기 전용 `Stat/List` cache node가 저널에 기록되지 않았는데도 checkpoint 상태 hash에 들어갔다. Bounded 상태는 이제 기록한 identity를 별도로 추적하고, 살아 있는 조회 핸들은 유지하면서 재시작 때 변경 없는 파일을 불변 기준본에서 재구성한다. 중단 7경계 모두 기준 파일 15개를 먼저 조회하며, 별도 검사로 rename한 cached 자식·metadata 승격·교체 뒤 unlink된 불변 파일의 후속 쓰기를 다룬다. 실패 fixture는 보존했다. 그보다 앞선 검사 도구 오류는 자식 종료 직후 ACK 파일 읽기의 공유 위반으로, 정확한 그 읽기에서 Windows 공유/잠금 오류만 최대 2초 재시도하도록 수정했다.

Native append/latest 전환 검사는 게시 동안 Windows 조상/owner pin을 유지하고 Files/Seal → reconnect → Complete 순서를 검증한다. Unknown 파일·경합하는 세대 영수증·손상 frame·다른 owner·실행 중 writer/reader·안전하지 않은 경로 전환은 계속 거부한다. 회수와 변경 없는 Library 복원 검사는 bounded의 정확한 inventory와 v2 디렉터리 plan을 사용한다. 이 작은 검사는 새 실제 Unity Library 복원 시간의 근거가 아니다.

## 구성 요소 비용 (BL-04)

[정확한 데이터와 provenance](../../../reports/benchmarks/bounded-journal-product-20261015.json)에 모든 build/test 종료 후 SJL4 → bounded 순서로 한 번씩 실행한 값을 기록했다. 같은 제품 Runtime과 불변 바이트를 사용한다. 논리 object 20,000개·hot 파일 256개·초기 private 범위 48MiB(3,072회 쓰기)에 이어 결정적 비압축성 4/8/16KiB를 4,096회 쓴다. Runtime의 directory guard factory와 WinFsp callback은 이 구성 요소 fixture 밖이다. 양쪽 모두 원본 hash·전체 논리 내용·재연결 sequence·store 회수 후 독립 결과 검증을 통과했다.

| 측정 단계 | SJL4 | Bounded |
| --- | ---: | ---: |
| 초기화·private 상태 준비 | 47.310초 | 9.504초 |
| 자동 checkpoint 포함 4,096회 쓰기 | 53.214초 | 11.335초 |
| 전체 내용 검증 | 0.728초 | 0.752초 |
| 재연결·검증 | 2.973초 | 0.625초 |
| 독립 결과 내보내기 | 0.492초 | 0.296초 |
| 소유 store 회수 | 0.548초 | 0.006초 |
| 회수 후 독립 결과 검증 | 0.104초 | 0.102초 |
| **측정 단계 합계** | **105.370초** | **22.619초** |

쓰기는 **78.7%**, 측정 단계 합계는 **78.5%** 감소했다. Fixture 생성·최초 입력 hash admission·프로세스 실행·보고서 생성은 이 합계 밖이다. OS cache 초기화·순서 무작위화 없는 방식별 한 번의 측정이며 전체 작업공간이나 Unity 결과가 아니다.

| 비용·트레이드오프 | SJL4 | Bounded |
| --- | ---: | ---: |
| 쓰기 ACK 중앙값 / p95 | 10.843 / 21.345ms | 2.065 / 2.501ms |
| 최대 쓰기 ACK | 148.676ms | 1,211.853ms |
| 쓰기 후 저널 논리 크기, `current-files` | 49.825MiB | 58.258MiB |
| 쓰기까지 관측한 프로세스 peak working set | 99.422MiB | 82.125MiB |
| 쓰기 중 관리 메모리 누적 할당 | 662.876MiB | 300.218MiB |
| 4,096회 쓰기 중 durable flush 호출 | 4,096 | 4,102 |

Bounded는 준비 중 3회·측정 쓰기 중 2회, 총 5회 자동 checkpoint를 수행했다. 측정 구간의 두 번은 **2.412초**이며 일반 mutation frame **40,735,128B** 외에 state-prefix frame **104,983,951B**를 기록했다. Prefix 계수는 intent/control 쓰기를 제외하며 전체 장치 쓰기량이 아니다. 마지막 정상 suffix는 8,406,086B / 845 frame이며 벤치마크만을 위한 강제 최종 정리를 추가하지 않았다.

평균 쓰기 시간을 줄이는 대신 가끔 약 1.2초의 지연이 발생한다. 최종 저널 길이는 **16.9% 증가**했고 이 fixture의 working set은 **17.4% 감소**했지만, checkpoint에서는 현재 private 상태 두 세대를 임시로 유지한다. 물리 할당과 전체 store peak 바이트는 측정하지 않았다. Probe의 진단 `inventory`는 최상위 파일만 나열해 SJL4 하위 object를 제외하므로 **저장 총량으로 사용하지 않는다**. 표는 해당 object를 포함하는 제품 `current-files` 저널 관측값을 사용한다. 전체 저장량이나 RAM 상한으로 확대하지 않는다.

## 한계

16MiB/4,096-frame 제한은 추가 suffix에 적용한다. 현재 상태 prefix와 한 checkpoint의 임시 중복은 별도이며 뒤의 쓰기로 불필요해진 prefix 바이트가 다음 checkpoint까지 남을 수 있다. 매 mutation의 개별 durable flush/ACK를 유지한다. 자동 checkpoint는 현재 private 범위와 전체 논리 metadata만 복사하며 빈 범위를 채우려고 불변 원본 payload를 읽지 않는다.

구성 요소 측정은 초기화·쓰기/자동 checkpoint·검증·재연결·독립 결과 내보내기·소유 정리를 포함해야 한다. 마지막 정상 bounded suffix는 남기며 추가 강제 최종 정리는 일반 bounded 동작에 포함되지 않는다. checkpoint prefix 기록 바이트는 control/intent 바이트를 제외하며 mutation append 바이트와 별도로 기록한다. Working set·관리 할당·논리 파일 길이·샘플 바이트·실제 NTFS 할당은 서로 다른 값이다.

예외 주입과 소유 프로세스의 외부 종료는 OS 전원 장애·kernel crash·악의적 동시 변경·모든 파일시스템 경합을 인증하지 않는다. 작은 제품 파일 검증은 새 전체 Unity 작업·Editor 컴파일·Play/build·대형 프로젝트 절감·모든 외부 도구 호환의 근거가 아니다. 이번 검증은 기존 Unity 프로젝트와 Library를 사용하지 않는다. 서비스·설치·UAC·ACL·외부 도구 변경은 포함하지 않는다.
