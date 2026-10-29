# 남은 수명주기 최적화

[English](../../plans/remaining-lifecycle-optimization.md)

앞선 행의 문단은 해당 근거 당시 상태를 보존하며 현재 행 표와 마지막 MX10 마감 절이 최종 상태를 소유한다. MX-01–10 프로그램을 마감했다. MX-01은 진단 귀속으로 마감했고 MX-02–04·MX-06/07·MX-08B·MX-09는 미채택했다. MX-05 A·guard 적용 B2와 MX-08A를 유지한다. 최종 전체 gate와 동일 입력 Git/Unity 반영·폐기 4회의 정확성은 통과했지만 실제 전체 시간의 큰 개선이나 개별 변경의 인과 절감은 입증하지 못했다.

사용자는 2026-10-28 이 대화에서 미시도하거나 일부만 진행한 기회를 모두 처리하도록 승인했다. 시작 제품/문서 커밋은 `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`, 브랜치는 `development`다. 승인된 구현·측정 프로그램은 마감했으며 모든 후보의 성능 개선을 주장하지 않는다. 이전 채택 구현과 폐기 실험은 각각의 근거를 유지한다.

## 필수 범위와 순서

아래 행을 순서대로 처리한다. 진행 중인 행 안의 독립 분석은 병행할 수 있지만 후보 빌드와 성능 시험은 다른 빌드·시험과 경쟁하지 않는다. 쉽거나 성공한 행만 끝내고 프로그램을 마감하지 않는다.

사용자의 2026-10-29 지시에 따라 남은 MX-07–09 구현과 변경 경로의 의미 있는 국소 정확성·내구성·원본/peer 보호·구성 요소 성능 검증을 우선한다. 저장소 전체 `scripts/build.ps1` gate와 MX-10의 동일 입력 Unity 전체 비교는 남은 구현을 모은 뒤로 미룬다. 실행 순서 변경이며 행의 범위나 최종 자격 주장에 필요한 근거를 줄이지 않는다. 진단 분야를 새로 확대하지 않는다.

| ID | 기존에 검토한 기회 | 필수 판단 근거 | 현재 상태 |
| --- | --- | --- | --- |
| MX-01 | Unity 시작 초기화, ILPP/DAG, asset 재import 원인 | 보존한 입력/의존성을 분석하고 지원되는 시작 profiler를 수집한다. 큰 초기화 원인을 찾아 구체적인 제품 후보를 해당 구현 행에 연결한다. 계측 비용과 일반 실행을 구별한다. | 진단 원인 분석 완료: [검증](../verification/remaining-lifecycle-optimization.md). 제품 후보는 MX-02/04/08에 연결했으며 이 행이 성능 수정을 완료한 것은 아님 |
| MX-02 | 소유 Editor bridge 사전 컴파일 | 버전 호환 bridge/import metadata 경로를 시제품으로 만들고 identity/수명을 검증하며 최초 시작·컴파일을 비교한다. 전체 시간 이득이 클 때만 유지한다. | 완료, 폐기·제거: 같은 입력 ABBA 평균 전체 시간 300.47118335→302.33802860초. 큰 이득 없음 |
| MX-03 | Git demand 우선 보존/선행 읽기와 reader 일정 | 소비 순서를 반영한 보존 정책과 근거 있는 제한 reader/warm 구성을 현재 두 reader와 비교한다. 전체 증명·메모리 상한·drain을 유지한다. 용량만 늘린 GR은 이미 폐기했으며 새 이득으로 재사용하지 않는다. | 완료: MX-03A·MX-03B 모두 측정 후 폐기·제거. 큰 ensure 시간 절감 기준에 미달 |
| MX-04 | Open/Close 접수와 context 잠금 | 마지막/비마지막 Close와 완료 경로 대기를 구분하고 안전한 분리를 시제품으로 만든다. delete/replace/reader 수명과 실제 경과 비용을 검증한다. | 완료, 폐기·제거: native 부하 5개 평균 합 5.62433035→5.49832905초. 절감 0.12600130초 / 2.24028982%로 기준 미달 |
| MX-05 | 일반 변경의 root 우선 Library 복원 | 검토한 root 이동을 디렉터리 삭제/rename·변경 범위/truncate/새 파일로 확장한다. 정확한 구조·identity·replay·중단 복구·소유 회수를 입증하고 전체 복원을 측정한다. | 완료: A와 게시 수 제한 guard를 적용한 B2 채택. 최종 fixed 46.05306965→14.73430160초, bulk 45.36571795→14.74108300초; leaf 5.02044795→5.00588275초로 사실상 동일. 전체 gate 1,941개 통과/실패 0개/skip 33개. 일반 NTFS 복원 자격이며 MX10에서는 변경 Library 경로가 선택되지 않았다. |
| MX-06 | bounded 저널 checkpoint 증분 게시 | 현재 private 상태의 반복 전체 직렬화를 피하는 설계·시제품을 만들되 제한된 복구·성공 ACK의 영속성·소유권·정확한 회수를 유지한다. 쓰기/checkpoint/재열기/공간 비용을 비교한다. | 완료·미채택: 제품 비용과 별도 시제품 근거 보존. 새 window ABBA 절감 6.85647005초 / 20.22796985%로 절대 10초 미달; 제품 통합 없음 |
| MX-07 | 기존 호출 범위를 넘어선 결과/소스 증명 재사용 | 관련 세대·외부 Git 상태·독립 payload의 유효성을 입증하거나 재사용이 계약을 보존하지 못하는 이유를 증명한다. 결과 수집/회수 비용을 측정한다. | 완료, 폐기·제거: A는 8.45773340초 / 15.47910650% 절감으로 절대 10초 미달; B 국소 proof 평균은 1.17969225→1.44745290초로 증가 |
| MX-08 | 같은 callback commit 결합과 혼합 mutation batching | Overwrite resize/metadata 결합과 가능한 순서 보장 metadata/write 배치를 시험한다. 현재 영속 ACK·요청별 오류·접수 drain을 유지하며 실제 연산 구성으로 측정한다. | 완료: A 최종 채택; B 폐기·제거. 국소 비용·최종 전체/실제 정확성 근거이며 큰 전체 시간 개선은 미입증 |
| MX-09 | 소유 정상 종료와 불변 세대 결과 수집 중첩 | 정확한 소스 세대와 늦은 쓰기/복구/게시 의미를 확정한다. 결과·소유 계약을 유지할 수 있으면 시제품을 만들고 전체 최종화를 측정한다. | 완료: 실제 Git/Unity 최종화 기준 미달, 후보 폐기·제거; 최종 판정 유지 |
| MX-10 | 최종 실제 Unity 전체 수명주기, 반영과 폐기 | 기준/최종 runtime·전체 source/Library bytes·mtime·속성·초기 loaded 상태를 고정한다. 준비부터 정상 종료·명시적 반영 또는 폐기·회수·Library 복원까지 단일 스톱워치로 측정하고 결과·원본/peer 보존을 검증한다. | 완료: 실제 반영 338.51645460→326.31757210초, 폐기 281.90245510→286.53377360초; 각 전후 1회, 큰 전체 절감 미입증 |

이 목록에는 큰 후보뿐 아니라 작은 이득 예상으로 미뤘던 항목도 포함한다. 각 행은 구체적 채택/폐기 판단·정확한 근거·한계가 있어야 완료다. 구현 불가 판단은 충돌하는 불변 조건이나 측정한 기회 상한을 입증해야 한다. 우려를 반복하거나 계측이 없는 것은 완료가 아니다. 안전한 후보의 효과가 불확실하면 계획 메모로 대체하지 말고 구현·측정한다.

MX-01의 [진단 검증](../verification/remaining-lifecycle-optimization.md)과 [추적 요약](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)은 원인 분석만 마감한다. 동일 raw를 지원되는 API로 다시 분석해 **로드된 sample 15,628,788개 전체를 순회**하고 최초 512MiB export에서 빠진 조상을 회수했다. 요약은 여전히 partial이다. 개별 행은 4,019개만 보존하고 434개 sample의 marker identity는 unknown이며 원시 capture 손실 여부도 unknown이다. unknown marker는 초기화 context에 없다. **22.72712704초** 초기화 중 Rider는 **12.503079936초**, Burst는 **9.06021888초**, 소유 bridge 생성자는 **0.080975696초**다. 메인 스레드 File.Open은 **12.951948684초**이며 Rider의 반복 상태 저장은 정확한 경로 근거가 있다. 이를 trigger·Open·쓰기 후보인 **MX-02, MX-04, MX-08**에 연결한다. 외부 패키지·전역 IDE 설정 변경 승인이나 제거 가능한 시간의 확정이 아니다.

로컬 원시 근거는 ignored `artifacts/remaining-optimization-20261028/`에 보존한다. 외부 관측기의 root Editor/asset worker 구분을 바로잡은 뒤 동일 시험을 정상 종료·명시적 폐기하고 소스/Library 내용·수정 시각·속성의 완전 일치를 확인했다. 원본 외부 서버와 보관한 설정도 복원하고 health를 확인했다. 후속 package-free 분석기는 raw와 외부 상태를 보존했다. 이 진단·복구 실행은 중단 없는 성능 비교가 아니다. MX-01은 진단으로 완료했으며 후보 구현·측정과 전체 프로그램은 계속 진행한다.

MX-02는 별도의 동일 입력 전후후전 GUI 비교에서 네 번 모두 정상 종료·폐기·복원을 완료했다. 준비부터 복원까지 평균 연속 시간은 **300.47118335→302.33802860초**, 즉 **1.86684525초 증가**했다. 초기 준비 완료는 **102.10364775→103.16568670초**, 한 번의 편집·컴파일은 **26.18967645→28.88893990초**였다. 최초 Csc는 1→0, ILPP는 69→68로 줄었지만 나머지 pipeline/reload는 계속 실행했다. 후보는 집중 시험 37개와 패키지 없는 최초/재시작의 identity·Editor 전용 import·정상 종료를 통과했으나 사전에 정한 큰 이득 기준은 충족하지 못했다. 제품/source/test 변경은 바이트 단위로 복원했으며 폐기 patch/binary는 ignored 근거로만 보존한다. 외부 서버/설정도 복원하고 health를 확인했다. 계측 비용과 한계는 [검증](../verification/remaining-lifecycle-optimization.md#mx-02-precompiled-bridge-rejected)에 기록했다. MX-02는 폐기로 마감하며 전체 프로그램은 계속 진행한다.

MX-03A는 reader 2개·warm worker 1개·256MiB/4,096-entry payload cache·128MiB 최초 proof window를 유지하고 선행 읽기 항목의 최초 소비 시 보존만 변경했다. 동일 입력 editorless ABBA의 평균 `ensure`는 **51.28742100→50.53104045초**로 **0.75638055초 / 1.47478765%** 줄었으며 사전 기준 **10초 이상이면서 15% 이상**에 미달했다. proved-read 재해독 bytes는 **305,237,082→11,628,972**로 감소했지만 큰 경과 시간 이득으로 이어지지 않았고, provider 수명 peak working set 평균은 **739,239,936→743,534,592 bytes**였다. 네 task 모두 명시적으로 폐기하고 정확한 source/Library/Git metadata 증명과 최종 Library 복원을 완료했다. 최초 CRLF/LF 관측 오류와 같은 task 복구는 새 동일 조건 cohort에서 제외한다. [검증](../verification/remaining-lifecycle-optimization.md#mx-03a-first-consume-retention-rejected)에 시험 55개 통과·조건부 비용 시험 1개 skip·별도 준비/복원·폐기 source/test 복원을 기록했다. MX-03B는 A를 채택해 확대한 것이 아닌 별도 후보였다.

MX-03B의 reader 4개/warm worker 3개·reader별 64MiB proof window는 editorless ABBA 평균 `ensure` **56.82863030→61.11054815초**, 즉 **4.28191785초 / 7.53478982% 증가**를 기록했다. Warm 평균은 **22.69030445→9.29738000초**로 줄었지만 겹쳐 실행하는 index refresh는 **26.72628210→32.84063510초**로 늘었으며 두 구간은 합산하지 않는다. 실제 reader 표본은 후보 두 번 모두 4개에 도달했다. 재해독 bytes와 미사용 선행 읽기 퇴출은 늘었다. [검증](../verification/remaining-lifecycle-optimization.md#mx-03b-reader-warm-concurrency-rejected)에 표본 한계·native 실행 전 의존 재빌드 setup 수정과 제한적인 메서드 행 비교·시험 80개 통과/조건부 비용 skip 1개·네 번 정확한 종료·source/test 7파일의 바이트 단위 복원을 기록했다. A와 B 모두 폐기·제거했으므로 **MX-03은 폐기로 완료한다.**

MX-04는 Open/retain의 원자성과 마지막 release/삭제 보호를 유지하며 비마지막 Close의 참조 수 처리만 mutation gate에서 분리했다. 고정 native ABBA에서 순차 실행한 부하 5개 스톱워치의 평균 합은 **5.62433035→5.49832905초**, 절감은 **0.12600130초 / 2.24028982%**로 사전 기준인 **10초 이상이면서 15% 이상**에 미달했다. 직접 callback 8개 부하 절감도 **0.01217985초 / 0.49737491%**에 그쳤다. 실제 anchor 사례는 매 실행에서 비마지막 Close 1,000회를 사용했지만 관측 절감은 **26.648ms**였다. native 20개 사례 모두 정상 close/unmount와 파일 9개의 전체 hash replay를 확인했고 열린 handle/context는 0이었다. [검증](../verification/remaining-lifecycle-optimization.md#mx-04-nonfinal-close-rejected)에 공통 계측·중첩 counter 한계·시험 84개 통과/조건부 비용 skip 3개·후보와 진단 코드의 정확한 제거를 기록했다. 이 행에서는 실제 Unity 시험을 하지 않았다. **MX-04는 폐기로 완료한다.**

이전 MX-05A 후보의 고정 디렉터리 구조 일반 NTFS ABBA에서 전체 복원 평균은 **79.86088015→30.23888355초**, 절감은 **49.62199660초 / 62.13554935%**로 사전 기준인 **10초 이상이면서 15% 이상**을 넘었다. 전후후전 개별 시간은 **60.5269537 / 21.1528534 / 39.3249137 / 99.1948066초**이며 편차가 크다. 평균 누적 관리형 할당은 **312,891,128→349,844,748 bytes**로 늘었으며 peak RAM이 아니다. 동일 입력 네 번 모두 최종 내용·metadata·구성 목록·살아남은 파일 identity와 정확한 basis/delta 회수를 검증했다. 후보 root 이동은 디렉터리 identity도 보존하며 기준본은 새 디렉터리를 만든다. [검증](../verification/remaining-lifecycle-optimization.md#mx-05a-fixed-topology-restore-provisional)에 집중 시험 228개 통과/조건부 비용 skip 2개와 이후 확인한 제어 파일 게시 중단 문제를 기록했다. 당시 A는 잠정 유지했으며 아래 최종 후보 비교와 전체 gate를 거쳐 채택했다. 이 이전 후보 수치를 최종 측정으로 대체하지 않는다. Unity나 최종 조합의 개선을 입증한 결과는 아니다.

MX-05 안전성 후속에서는 게시되지 않은 제어 임시 파일 문제와 A/B 새 leaf의 절대 경로 검사·생성 사이 틈을 수정했다. 최초 집중 실패 실행(**277개 통과 / 68개 실패 / 3개 skip**)과 runtime은 보존한다. 보강 소스는 좁은 시험 **21개**에 이어 집중 시험 **358개 통과 / 조건부 비용 skip 3개**를 기록했다. 이후 정확한 부모를 통한 native 파일 생성 소스는 선택 시험 **18개**를 통과했다. 서로 겹치고 소스가 다른 결과를 고유 시험 합계로 더하지 않는다. 시험한 빈 부모 경계에서는 속성 쓰기만으로 junction 전환이 실제 성공했다. 부모 핸들 기준 게시·현재 타입/경로 검사·실패 보존이 외부 경로 영향을 막으며 GENERIC_WRITE 공유 거부는 별도 근거다. [검증](../verification/remaining-lifecycle-optimization.md#mx-05-safety-progress)에 실제 코드·source/runtime hash·미자격 경계를 기록한다.

B 최초 bulk 비용 시도는 복원 전에 fixture identity helper가 동결 baseline에 없는 인자 5개 `DirectoryHandle`을 호출해 준비 단계에서 실패했다. 실패 시도는 보존하며 그 시도의 후보 실행·비교 시간은 없다. 공통 identity fixture를 바로잡은 별도 cost2 runtime/코호트는 정상 완료했으며 실패 시도 자체를 제품 성능 결과로 읽지 않는다.

B1은 fixture를 바로잡은 별도 `cost2` bulk 구조 ABBA 네 번에서 정확한 종료·회수를 완료했다. 전체 복원 평균은 **85.56445735→70.76198725초**, 절감은 **14.80247010초 / 17.29978844%**였고 전후후전 개별 시간은 **82.4396773 / 73.0181388 / 68.5058357 / 88.6892374초**였다. 평균 누적 관리형 할당은 **312,922,832→496,934,536 bytes**, 즉 **184,011,704 bytes 증가**했으며 peak RAM이 아니다. 시간 screen만 통과한 결과다. 동일 동결 B1 runtime의 개별 leaf 이동 ABBA는 무조건 topology 선택을 폐기했으며, B2의 반복 조상 검사 제거·공유 forest proof·최대 8-worker 전체 hash는 앞선 빌드와 좁은 안전 시험을 통과했으며 강화 guard·전체 gate를 통과했으며 최종 동일 조건 측정과 채택 판단은 아래에 별도로 기록한다. [검증](../verification/remaining-lifecycle-optimization.md#mx-05b1-bulk-topology-screen)에 cost1 준비 실패·runtime/입력/결과 hash·구성 요소 한계를 분리 보존한다.

완료된 B1 개별 leaf screen은 **8.57284805→43.97373460초**, 즉 **35.40088655초 / 412.94195749% 증가**와 평균 누적 할당 **37,300,224→145,608,832 bytes**를 기록했다. 네 번 모두 올바르게 종료했지만 **B1 topology 무조건 선택은 폐기한다**. 최초 신규 요청 선택 guard는 `1 + 2 × 살아남는 non-root unit + 삭제 non-root unit < 살아남는 source-backed 파일`을 요구했으나 후속 검토에서 게시 비용 한도 누락을 확인했다. 이미 시작한 topology intent는 기존 재개 프로토콜을 유지한다. 강화한 guard는 아래에 기록하며 최종 동일 조건 성능·전체 gate를 거쳐 채택했다. [검증](../verification/remaining-lifecycle-optimization.md#mx-05b1-leaf-regression-and-guard)에 bulk 이득과 이 회귀·한정 읽기 리뷰를 분리했다. 이전 B1의 무조건 선택 폐기는 유지하며 최종 A/B2 채택으로 MX-05를 마감했다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.

B2의 앞선 소스는 **19.64초에 경고/오류 0개**로 빌드했고 좁은 시험 **14개를 5.034초에 실패/skip 0개**로 통과했다. 최초 xUnit1051 analyzer 실패는 별도 빌드 근거로 보존한다. 이 결과는 강화 guard 이전 소스이며 새 guard의 검증이 아니다. 이전 이동 수만의 guard에서 첫 전체 gate는 **전체 1,958개 / 통과 1,924개 / 실패 1개 / skip 33개**와 시험 실행 시간 **25m 31.649초**로 종료했다. 유일한 sparse 디렉터리 삭제 기대값은 `F=0, T=2`에서 general을 선택하므로 실패했으며 실행/runtime을 보존한다. 강화 게시 guard는 **48.38초에 경고/오류 0개**로 빌드했고 집중 정확성 시험은 **107.228초에 234개 통과·실패/skip 0개**를 기록했고 두 번째 전체 gate는 시험 실행 **10m 34.813초**에 **전체 1,974개 / 통과 1,941개 / 실패 0개 / skip 33개**로 성공했다. 추가로 발견한 미실측 구조 사례는 생존 파일 1,000개 중 독립 rename 499개로, 기존 이동 조건(`T=999 < F=1000`)을 통과하지만 topology cursor 998회와 general 묶음 8회의 차이가 있다. 강화한 신규 요청 규칙은 `T < F && O + N <= G`이며 `F`는 생존 source 파일, `T`는 root를 포함한 보존 이동, `O`는 topology 연산, `N`은 새 디렉터리 영수증, `G=ceil(F/128)`이다. Bulk 입력(`F=9997, T=16, O=17, N=2, G=79`)은 이 구조 screen을 통과한다. [검증](../verification/remaining-lifecycle-optimization.md#mx-05b2-validation-progress)은 이전 빌드/시험 소스·두 번째 gate 성공·guard 산술을 경과시간 근거와 분리한다. 최종 후보 전체 gate와 큰 구성 요소 이득·자원 비용 판단을 통과했다. 제어자가 해당 배경 작업 부재를 확인한 뒤 동결한 fixed/bulk/leaf ABBA를 완료했으며 최종 채택 근거는 아래와 같다.

MX-05는 **A와 게시 수 제한 guard를 적용한 B2를 채택하며 완료**했다. 최종 별도 ABBA에서 fixed 평균 **46.05306965→14.73430160초**, 절감 **31.31876805초 / 68.00582087%**, bulk 평균 **45.36571795→14.74108300초**, 절감 **30.62463495초 / 67.50611769%**였다. Leaf는 general 경로를 선택해 **5.02044795→5.00588275초**로 사실상 같았다. 12회 모두 정확한 결과·정상 회수와 전체 gate **1,941개 통과 / 실패 0개 / skip 33개**를 확인했다. 전체 시험 process의 OS peak working set 평균 증가는 fixed **26,806,272 bytes**, bulk **40,529,920 bytes**이며 각각 약 31.3초·30.6초의 복원 절감에 비해 이 입력에서는 합당하다고 판단했다. 누적 allocation·250ms Private 표본·setup/assert 포함 process CPU/메모리의 한계는 [최종 검증](../verification/remaining-lifecycle-optimization.md#mx-05-final-adoption)에 기록한다. 과거 A/B1·실패 원시 근거는 유지하며 실제 Unity 전체 시간은 미확정이다. **MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.**

MX-06은 고정 입력 두 개에서 현재 제품 비용 **8회**를 측정했다. 자동 checkpoint 평균은 DF tail에서 **observer 2.47461490초 / plain 2.74308515초**, Private-128m에서 **4.00545070초 / 3.69543230초**였고 관측한 상태 출력+candidate flush는 **0.54136015초 / 0.73128705초**였다. 양쪽은 같은 변경 없는 제품이며 쓰기/flush 편차와 사전 검증의 cache warming이 있으므로 최적화 전후 비교가 아니다. 8회 모두 정확한 재연결·회수 증명을 통과했다. 별도 시제품 관측과 최종 미채택 판단은 아래에 구분한다. [검증](../verification/remaining-lifecycle-optimization.md#mx-06-current-product-cost)에 입력·원시 행·중첩 단계 비용·자원 한계와 Unity 미실행을 구분한다.

별도 MX-06 BCL 모델은 **작은 self-test 44개**와 두 입력의 각 4회 ABBA를 완료했다. Cold/hot 전체 측정은 **52.58108920→27.08835650초**, 마지막 강제 consolidation·재연결/증명·정확한 회수를 포함해 **25.49273270초 / 48.48270184%**를 절감했다. Fragmentation은 **12.04742565→8.21393290초**, 절감 **3.83349275초 / 31.82001584%**로 이 control은 절대 10초 기준에 못 미쳤다. 공유하는 append-flush 경로에도 큰 편차가 있어 전체 관측 차이를 checkpoint만의 인과 효과나 제품/Unity 이득으로 읽지 않는다. 같은 full seed가 timer 밖에서 cache를 예열하며 mode당 두 관측과 setup 포함 process 자원의 한계를 유지한다. [별도 시제품 근거](../verification/remaining-lifecycle-optimization.md#mx-06-standalone-prototype)에 원시 행·hash·한계를 보존했다. 이 최초 cold/hot 관측은 그대로 보존하며 뒤의 독립 window 측정과 최종 판단을 구분한다.

## 채택과 증거

- 작업공간 하나의 경과 시간을 최적화한다. 겹치는 누적 counter를 합하지 않는다. 원시 전후 입력·runtime/source hash·순서·단계 구간·실패·최종 결과를 보존한다.
- 같은 입력의 전후후전 반복을 우선한다. 구성 요소와 실제 mount Unity 근거를 구별한다. 구성 요소 비율을 합하거나 다른 과거 Library와 비교해 인과 개선으로 제시하지 않는다.
- 큰 절대 시간 절감을 유지한다. 미미한 이득, 다른 의미 있는 효용 없는 작은 이득, RAM/CPU/저장/복구/유지보수 비용이 더 큰 후보는 폐기한다. 큰 누적 counter 자체는 절감이 아니다. 후보별 실제 트레이드오프를 명시한다.
- 폐기 source/patch와 원시 측정은 ignored `artifacts/remaining-optimization-20261028/`에 보존한다. 폐기 제품 코드·전용 시험·설정·호환 잔재는 제거하고 추적하는 검증 보고서에는 재현 가능한 요약을 남긴다.
- 전체 내용/identity/seal/result 증명, 영속화 뒤 성공 ACK, 제한된 접수/drain, 원본/peer 보호, 동일 intent 복구를 유지한다. 공개/디스크 계약 변경은 schema/fake/시험·양 언어 문서를 함께 바꾼다. 미지원 구버전은 보존·거부하며 자동 이관하지 않는다.
- 검토한 조기 ACK/생성 데이터 내구성 완화는 다른 보장이며 RAM/CPU 추가 사용으로 승인된 동등 최적화가 아니다. 계약 경계를 명시적으로 판단하고 성공한 쓰기의 내구성을 몰래 낮추지 않는다. 폐기한 Bee/VSS/ReFS는 유지하지 않는다.
- 외부 도구는 에이전트가 담당한다. 전용 fixture와 정확한 process/project identity를 사용하고 원본·기존 서버/설정을 보존한다. 과거 서버/두 handshake의 일시 분리 승인은 무관한 현재 프로세스나 설정을 변경할 권한이 아니다. 해당 절차 전 현재 identity를 재검증한다.
- fixture source/Library 준비는 제품 측정 밖이며 제품의 전체 복사 경로가 아니다. 진단 파일·자원을 제한하고 원본 입력을 보존하며 명시한 종료 방식에 따라 시험 소유 task만 회수한다.
- 유지하는 제품 변경은 의미 있는 경계 시험과 필수 `pwsh ./scripts/build.ps1`을 실행한다. 최종 source·양 언어 문서/색인·회수를 확인한다. 로컬 시험은 원격 CI·전원 장애·일반 Unity 지원의 증명이 아니다.

모든 행이 근거 있는 판단에 도달하고 MX-10이 유지한 조합을 검증할 때까지 사용자 목표는 진행 중이다. 자동 push·release·tag·게시는 이 프로그램에 포함하지 않는다.

MX-06은 **미채택으로 완료**했다. 별도 cold/hot window ABBA는 **33.89598710→27.03951705초**, 절감 **6.85647005초 / 20.22796985%**로 **15%**는 통과하지만 절대 **10초** 기준에 미달했다. 앞선 **25.49273270초** 관측은 보존한다. 당시 공통 append-flush 차이 **20.40909535초**는 재현되지 않았으며(**17.45667015→17.40476080초**, 차이 **0.05190935초**) 원인은 미확정이다. Loop checkpoint 절감은 **5.16928925초**, 마지막 강제 consolidation 차이는 **1.63236080초**이며 full mode 내부 편차가 컸다. 현재 제품의 기회 규모와 새 다중 파일 형식·복구·복원·회수 계약 비용을 고려해 이번 범위에서 제품 연결을 채택하지 않는다. [최종 검증](../verification/remaining-lifecycle-optimization.md#mx-06-final-disposition)은 **제품 8회**, 모델 시험 **44개**, 최초 모델 측정 **8회**, 새 진단 **4회**를 구분한다. 선택적 제품 비용 시험은 byte-exact 보관 후 제거했고 checkpoint 소스 5개 hash는 같다. 시제품·설계·원시 근거는 ignored 과거 기록으로 남기며 선택적 제품 backend나 자동 재개할 구현이 아니다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.


### MX-07A 소스 cache 판단

제한된 변경 세대 guard를 사용한 소스 내용 hash cache를 구현하고 실제 Editor 없는 None NTFS/WinFsp ABBA 뒤 제거했다. 연속 **ensure → 고정 편집 → checkpoint → 별도 release** 평균은 **54.63967445→46.18194105초**, 절감 **8.45773340초 / 15.47910650%**다. 상대 **15%**는 통과하지만 필수 **10초 AND 15%** 기준에는 미달한다. 공통 기준본 준비 **3.69286070초**와 복원 **12.16613020초**는 primary timer 밖이다. Checkpoint 편차가 컸으며 원인을 계측하지 않아 flush나 cache 동작으로 귀속하지 않는다. 네 실행 모두 전체 소스/결과/소유 회수와 마지막 일반 NTFS 복원을 통과했다. 후보 10파일을 복원 또는 제거했고 기준 소스/시험 **323개** hash가 모두 일치한다. 원시 **소스 327개 / runtime 45파일** 후보 근거와 수정한 소스별 시험은 보관한다. [검증](../verification/remaining-lifecycle-optimization.md#mx-07a-source-cache-rejected)에 개별 행·namespace/fixture 수정·한계를 보존한다. **2.125GiB**, 한 호스트 구성 요소 관측이며 Unity나 실제 Plastic 자격이 아니다. MX-07B와 행의 마감은 아래에 기록한다. MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다.

### MX-07B 결과 proof 판단

제한된 호출 수명 핸들 유지 proof는 **집중 시험 50개, 실패/skip 0개(6.051초)**를 통과했다. 같은 빌드에서 이미 released 상태인 독립 결과 **2,114개 object / 2,315,321,344바이트**를 읽기 전용 ABBA로 비교한 기존 반복 전체 proof와 선택적 유지 proof의 평균은 **1.17969225→1.44745290초**, **0.26776065초 / 22.69750013% 증가**였다. 공통 전체 proof warming은 측정 밖이며 소스 scan·unmount·Unity 전체 수명주기는 포함하지 않았다. 후보는 폐기했고 기존 6파일을 byte-exact 복원하고 추가 8파일을 보관 뒤 제거했다. [검증](../verification/remaining-lifecycle-optimization.md#mx-07b-result-proof-rejected)은 수정한 build/filter 시도와 단일 국소 비용 시험을 구분한다. **MX-07A/B를 폐기하며 MX-07을 마감한다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증이다.** 뒤로 미룬 전체 저장소 gate와 Unity 전체 비교는 유지한 조합의 검증 의무로 남는다.

### MX-08A resize/metadata 결합: 잠정 유지

`SetLengthAndMetadata`는 SJR2 크기 변경을 완전한 entry 1회로 기록하고 길이가 같으면 metadata commit 0/1회를 수행한다. WinFsp Overwrite와 SourceView 전달을 연결했으며 SJL4는 기존 별도 영속 capsule 단계를 유지한다. 빌드와 **집중 시험 30개**가 통과했다. 실제 파일 기반 bounded 저널의 **8,192회 저장** ABBA는 **87.90405840→43.59400240초**, 관측 절감 **44.31005600초**였으며 영속 mutation은 **24,576→16,384회**로 줄었다. 기존 경로 두 실행은 **123.00958680 / 52.79853000초**로 편차가 커 평균 비율을 단독 인과 효과로 읽지 않는다. 과거 Unity Overwrite **260회**보다 **31.50769231배** 증폭한 부하이며 mount나 Unity 수명주기는 포함하지 않는다. [국소 검증](../verification/remaining-lifecycle-optimization.md#mx-08a-combined-resize-local)에 원시 행과 한계를 기록한다. **A는 후속 조합 검증을 위해 잠정 유지하며 최종 채택이 아니다. MX-08B는 폐기·제거 완료, MX-09는 대기, MX-10은 마지막이다.**

### MX-08B 혼합 batching: 폐기·제거

실제 영속 bounded SJR2를 사용하는 직접 callback **8 worker × 256라운드 × 3 callback = 6,144회** ABBA 평균은 **12.31529745→3.18759780초**, 절감 **9.12769965초 / 74.11676159%**였다. 사전 **10초 AND 15%** 기준에는 미달했으며 이를 넘기려고 부하를 증폭해 반복하지 않았다. 직렬 control은 **0.43005645→0.43726895초**다. [검증](../verification/remaining-lifecycle-optimization.md#mx-08b-mixed-batching-rejected)은 보관 후보의 수정된 국소 시험·같은 test DLL runtime 비교·정확한 제거를 기록한다. **MX-08 구현과 국소 판단은 완료했다. A는 잠정 유지, B는 폐기·제거이며 MX-09는 남아 있고 MX-10은 마지막이다.** B 제거 당시 `bin/Release`에는 폐기한 B가 남아 있었다. 이후 MX09의 현재 소스 Release 재빌드로 교체했으며 동결 A runtime은 별도의 당시 자격을 유지한다.

<a id="mx-09-unpublished-preparation"></a>

### MX-09: 제거한 후보의 구현·국소 검증 기록

**아래는 제거 전 후보의 역사적 계약·국소 검증 기록이다. 당시 미확정 상태를 보존하며 현재 제품 경로나 최종 상태를 뜻하지 않는다. 마지막 실제 측정·폐기 절이 현재 판정을 소유한다.**

새 range-journal task의 `ResultRevision`이 null이고 소유 Editor가 실행 중이며 공급자가 capability를 지원할 때만 정상 `StopAsync`와 `PrepareCheckpointAsync`를 겹친다. 기존 source proof cache와 검증한 미참조 content object만 준비하며 collection/result manifest, index, commit 또는 ref를 게시하지 않는다. Files는 전체 source generation과 owner/basis를 확인하고 변경 payload를 hash·metadata로 검증한 RAM에서만 게시한다. 서로 다른 변경 payload가 **256MiB** 상한을 넘으면 준비를 생략한다. 이 상한은 전체 process RAM 제한이 아니다. Git은 기존 owner/source/해석 증명과 경로별 clean filter를 유지하며 변경 파일의 content object만 쓴다.

양쪽 작업은 실패·취소에도 모두 drain한다. 둘 다 성공한 뒤 `RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal`과 기존 게시·회수 guard를 따른다. 늦은 쓰기는 최종 수집이 새로 판단한다. 기존 결과, release 재개, Editor 없는 작업과 미지원 공급자는 기존 경로다. MX-08A는 잠정 유지하고 MX-08B는 폐기·제거했다. MX-09 구현과 국소 정확성 검증은 완료했으며 성능·채택·전체 검증은 미확정이다. MX-10은 마지막 조합 검증으로 남는다. 아직 수행하지 않은 시험이나 시간 측정을 통과로 기록하지 않는다.


최종 국소 실행 `mx09-focused4.trx/log`만 **55개 통과 / 0개 실패 / 0개 skip**으로 확정했다(신규 45개·기존 보호 10개). 앞선 중단·시험 fixture 수정은 [검증 기록](../verification/remaining-lifecycle-optimization.md#mx-09-unpublished-preparation)에 분리한다. 제품 6개·시험 3개 및 runtime 129개를 고정했으며 전체 프로그램과 MX09 행은 성능·채택 판단 전까지 미완료다.

<a id="mx-09-final-disposition"></a>

### MX-09 실제 Git/Unity 최종화: 폐기·제거

동일 소스/Library 파일 bytes·mtime·attributes와 최초 loaded MVID의 ABBA 각 2회에서, 실행 중인 소유 Editor에 `workspace checkpoint --release`를 호출하여 정상 종료·독립 결과·소유 회수까지 잰 평균은 **69.76183845→70.28309595초**다. 전−후 평균 절감은 **-0.52125750초 / -0.74719576%**로 사전 **10초 이상 AND 15% 이상** 기준에 미달했다. MX09 후보는 미채택으로 마감하고 제품·시험을 보관 후 제거/복원했다. MX08A는 잠정 유지, MX05는 유지하며 전체 프로그램과 MX10은 미완료다.

별도 `unity stop`을 먼저 호출하지 않았다. Library 복원은 별도 시간이며 연속 전체 관찰에는 준비·시작·편집/컴파일·최종화·복원과 관측 비용이 모두 포함된다. 초기 ready는 MX09 준비가 시작되기 전이고 편차가 크므로 전체 관찰시간 감소를 MX09 효과로 주장하지 않는다. 아래 구간은 중첩되어 합산하지 않는다.

| 순서 | checkpoint/release 초 | 초기 ready 초 | Library 복원 초 | 연속 전체 관찰 초 |
|---|---:|---:|---:|---:|
| 01-before | 70.59876170 | 175.74334200 | 22.89105400 | 394.47825940 |
| 02-after | 69.99108030 | 114.17107240 | 22.79306420 | 322.22756980 |
| 03-after | 70.57511160 | 100.91895940 | 22.88731850 | 311.21626180 |
| 04-before | 68.92491520 | 119.48113130 | 22.69723040 | 325.83617140 |

네 번 모두 정상 종료·독립 Git 결과·소유 회수 및 원본 source/Library/index/HEAD/config/등록 보존을 확인했다. 이전 ref를 모두 보존하고 각 작업의 정확한 task branch/result ref만 추가했다. 공유 Git object DB와 보존 결과는 회차 사이 증가하므로 저장소 전체 바이트가 동일한 입력이라고 주장하지 않는다. None/Plastic 속도, 일반 Unity 개선, 원본 반영/폐기 전체 수명주기, peak RAM 또는 전원 장애 자격은 없다. 전체 `build.ps1` gate와 MX10 반영/폐기 비교는 여전히 마지막에 남는다. 현재 제품 흐름은 `Stop → RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal → publish/reclaim`이다.

`mx09-performance-analysis.json` SHA256 `10f7a13a995b61680b353d9e4bea58cdb9fbef26c2c4cb610b70b905dbb3b84b`; `mx09-removal.json` SHA256 `96621f7841db9792cbf69e8dc3e8a5222a333afc84edb295108b5c539f656e06`. 제거 영수증: 기존 4파일 복원, 후보 전용 5파일 제거; `retainedMx08AExact=true`, `verified=true`. 원시 run/후보/55-case 및 build·중단 기록은 보존한다.

<a id="mx-10-final-closeout"></a>

## MX-10 최종 조합 검증과 프로그램 마감

MX-01–10 프로그램을 마감했다. MX-01은 진단 귀속으로 마감했고 MX-02–04·MX-06/07·MX-08B·MX-09는 미채택했다. MX-05 A·guard 적용 B2와 MX-08A를 유지한다. 최종 전체 gate와 동일 입력 Git/Unity 반영·폐기 4회의 정확성은 통과했지만 실제 전체 시간의 큰 개선이나 개별 변경의 인과 절감은 입증하지 못했다.

| 경로 | 이전 전체 초 | 최종 전체 초 | 전−후 절감 초 | 전−후 절감 % |
|---|---:|---:|---:|---:|
| apply | 338.51645460 | 326.31757210 | 12.19888250 | 3.60363059 |
| discard | 281.90245510 | 286.53377360 | -4.63131850 | -1.64287980 |

MX08A는 국소 44.31005600초 절감·24,576→16,384 영속 mutation 및 30개 국소/1,966개 최종 시험·실제 4회 정확성으로 채택한다. 최종 전체 gate는 총 2,000개 중 1,966 통과·0 실패·34 조건부 skip이며 과거 비용 시험의 재실행은 아니다. 최종45 runtime 파일은 MX08A 동결본과 동일하다. 실제4회는 원본 Library sequence0/unchanged-root를 사용했으므로 MX05 변경 Library 경로의 전체 이점은 미입증이다. 적용/폐기 각 전후1회·고정 순서·cache 편차와 원본 Editor 미실행 한계를 유지한다. 정상 회수·독립 결과/원본 반영·폐기 및 외부 서버/설정 복원 근거는 [최종 검증](../verification/remaining-lifecycle-optimization.md#mx-10-final-closeout)에 있다. 프로그램 마감은 큰 전체 성능 개선의 선언이 아니며 추가 실행은 시작하지 않는다.
