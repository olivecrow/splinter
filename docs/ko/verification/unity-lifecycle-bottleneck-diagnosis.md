# 실제 Unity 수명주기 병목 진단

[English](../../verification/unity-lifecycle-bottleneck-diagnosis.md) · [후속 범위](../plans/lifecycle-bottlenecks.md) · [BO 구성 요소 검증](lifecycle-bottlenecks.md)

**BD-01, 2026-10-27:** 실제 GUI Unity 작업공간 하나에서 Library 준비, 생성, 최초 로드 코드 준비, C# 수정 한 번과 컴파일 확인, asset/render 검사, 정상 종료, 명시적 폐기, 원본 Library 복원을 **391.3053613초**에 완료했다. 캡처·관측 비용을 포함한 완료된 진단이며 전후 성능 개선이나 공유 Editor 비교가 아니다.

[기계 판독 보고서](../../../reports/benchmarks/unity-lifecycle-bottleneck-diagnosis-20261027.json)는 고정 입력, 측정 runtime, 단계 경계, 비용 구분과 종료 확인을 보존한다. 로컬 원시는 `artifacts/unity-lifecycle-followup-20261027/`이며 성공한 실행은 `01-before`다. 이 이름은 계획했던 비교 순서를 뜻할 뿐 두 번째 후보 실행 완료를 뜻하지 않는다. 고정한 측정 runtime은 **`bea4acdb95f3ea2a914587c7f5e5293b57940e7d`**이며 이미 BO-01–05를 포함한다. BO 적용 전 코드와 비교한 결과가 아니다.

## 고정 입력과 스톱워치

- Windows/NTFS, Unity **6000.3.22f1**, 제품이 소유한 GUI Editor와 현재 bounded 저널이다. 작업공간 하나이며 A/B Editor 두 개나 각 작업 시간의 합이 아니다.
- 실험 소스 커밋 **`20967120692904985ea6320ce01f93239ba136ff`**: **소스 1,602개 / 2,804,772,190바이트**, 전체 측정 Library **42,419개 / 3,142,155,813바이트**다. 입력·복원 검사는 내용, 수정 시각, 속성을 포함하며 PID/cache 파일을 제외하지 않았다. 최초 고정 뒤 원본 서버가 로그를 추가했으므로 승인된 서버 일시 종료 뒤 전체 manifest를 다시 고정했다. 제외한 사전 점검에서는 제품 작업공간이나 Editor를 만들지 않았다.
- fixture 워밍은 측정 밖이다. 과거 fixture의 비공개 package callback 차단을 제거하고 설치된 외부 package의 정상 callback은 유지했다. 최초 로드 runtime MVID **`030f3712-6d1f-4053-9ac9-990717142fd8`**는 워밍 입력과 같고 값 `(4, 6, 13)`, ready 상태, 컴파일 오류 부재를 확인했다. 한 줄 수정 뒤 `(5, 6, 13)`, 식별된 refresh 요청의 성공 응답과 새 MVID **`2bf06e82-336b-4b98-8ffe-cc11b8d78c82`**를 확인했다. 자발적 상태 관측의 `success=false`는 refresh 실패 응답이 아니다.
- 하나의 연속 단조 시계가 `library prepare` 시작부터 `library restore` 완료까지 측정하며 내부 구간을 제외하지 않는다. 전체 입력/최종 종료 증명과 별도 승인된 외부 서버 일시 종료·복구는 시계 밖이다. W1/G1 진단, 제한된 캡처 두 번과 서버 관측은 안에 포함한다. 이들의 호출 시간을 빼도 cache 영향과 다른 작업 중첩이 남으므로 일반 실행 시간을 얻었다고 주장할 수 없다.
- 작업공간 identity는 `426d9275b891ce7a1c64a942`, request digest는 `34b4d5fa83e03ed811c1de53`이며 소유 Editor는 기록된 시작 identity의 PID 21864다. 캡처, 로드 코드 관측, 종료 영수증은 반환된 프로젝트·프로세스와 일치한다. 종료 분기는 **폐기**이며 checkpoint/release나 원본 반영이 아니다.

| 측정 단계 | 초 |
| --- | ---: |
| Library 준비 | 37.6204517 |
| 작업공간 ensure | 59.6685310 |
| 시작 전 진단 캡처 | 12.6682934 |
| 시작 요청부터 최초 검증된 준비 상태 | 162.1235357 |
| 실행 중 진단 캡처 | 15.4385013 |
| 외부 서버 관측 | 1.6110659 |
| 코드 수정·명시적 refresh부터 컴파일 준비 상태 | 26.8958510 |
| asset/scene/render 검사 | 1.8176238 |
| 제품 정상 종료 호출 | 39.0148359 |
| 종료 후 명시적 폐기 | 10.0517996 |
| 원본 Library 복원 | 22.9976892 |
| **나머지 dispatch/poll 간격을 포함한 연속 수명주기** | **391.3053613** |

`unity start` 접수는 최초 준비 162.1235357초 안에서 3.9315346초가 걸렸다. 접수 자체가 준비 완료가 아니며 별도 가산 단계도 아니다. 표는 제어자의 경계를 사용한다. 비동기 provider counter 구간은 이 경계와 다르고 겹칠 수 있다. 정확한 시각은 보고서와 원시 영수증에 보존한다.

## 영속 쓰기와 callback 접수

W1의 `01-before/provider-analysis.json`·`.md`는 callback, batch, store 호출과 계측된 저널 경계를 구별한다. 최종 mount 관측은 정상 Editor 종료 뒤, 폐기 전이다. **쓰기 callback 25,798회 / batch 25,607개**, 계측된 저널 영속 flush **111.6086225초**, 그중 관측된 쓰기-store 구간의 flush **96.7305155초**다. live operation 귀속은 **107.7683288초**이며 나머지 **flush 30회 / 3.8402937초**는 이 귀속 밖의 bounded checkpoint flush 위치와 부합한다. 이 counter는 겹친다. 별도 수명주기 단계나 OS 디스크 서비스 합계, 서로 더할 수 있는 제거 가능 시간이 아니다. 계측 범위 밖 직렬화/hash/replay의 정확한 귀속은 남아 있다.

| provider 단계 관측 구간 | callback | batch | 단독 batch | W1 flush 초 | 계측된 전체 저널 flush 초 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 시작 | 13,998 | 13,884 | 99.388% | 72.381 | 86.454 |
| 정상 종료 | 10,311 | 10,310 | 99.990% | 21.311 | 21.651 |

종료에서 관측 가능한 연속 tail 표본 597쌍 중 594쌍은 앞 callback이 반환된 뒤 다음 callback이 들어왔다. 전체 10,311회를 대표하는 완전한 표본은 아니지만 쓰기 worker 증가나 인위적 batch 대기의 큰 이득 근거는 작다. callback 접수 이전의 Unity 동기 호출과 WinFsp/kernel 직렬화는 구별할 수 없다. 필요한 영속 ACK를 받아야 다음 요청을 발행하는 호출자의 요청을 기존 batching으로 미리 묶을 수는 없다. 조기 ACK, flush 생략, 새 저널 형식, 폐기한 Bee 저장 backend를 도입하지 않았다.

시작 중 Close/Open 대기 합은 **41.6159745초 / 11.4125562초**지만 호출자 사이에서 겹친다. 3.1822556초짜리 게시 구간에 누적 Close 대기 10.5857066초가 포함되어 이를 경과 시간 절감으로 더할 수 없음을 직접 보여 준다. `01-before/open-close-review.md`의 코드 검토는 다음을 확인한다.

- 마지막이 아닌 Close는 이미 context 계수만 바꾼다. 별도 context lock과 마지막 종료 대기의 재검사는 좁게 구현 가능한 후보지만 빈도와 실제 완료 경로 비용이 미계측이다.
- 마지막 linked Close는 로컬 writer 상태 검사를 유지한다. 마지막 unlinked/replaced Close는 마지막 reader가 해제된 뒤에만 회수해야 한다. 바깥 gate 제거만으로는 저널 gate에서 다시 기다릴 수 있고 release 생략은 상태/수명 보호를 바꾼다.
- Open의 namespace 조회와 참조 유지는 delete/replace에 대해 원자적이어야 한다. 바깥 lock만 제거하면 오래된 identity 경쟁이 생기고 `Stat`의 저널 gate는 여전히 남는다.

W1은 이 Close 종류를 구분하거나 경로/stack을 보존하지 않는다. 따라서 누적 대기가 Unity 초기화 구간의 Close 비용이었다고 입증하지 않는다. 비동기 close 큐, 더 넓은 lock 제거, 과거 읽기/flush 중첩 시제품의 재도입을 구현하지 않았다.

## Git 준비: decoder 대기보다 데이터 보존

성공한 attach 시도는 `738316b3652f4b4fb1785859b48b73ac`다. Git attach는 **ensure 안에서 33.7527360초**다. 최초 index warm은 **25.9653962초**, 필수 index refresh는 **32.4726862초**이며 **32.4884104초** 구간에서 겹친다. refresh는 warm보다 **6.5230142초** 늦게 끝난다. 이 시간을 ensure나 서로에게 더하지 않는다.

attach 뒤 최초 유휴 게시에서 전체 proof 해석은 **2,787,029,670바이트**, 이미 증명된 읽기의 재해석은 **2,913,936,481바이트**다. 보관했던 선행 데이터 **2,027,356,160바이트**가 최초 사용 전에 퇴출됐다. 완료한 전체 proof는 1,563개로 소스 manifest의 고유 내용 수와 같다. 이 수와 거의 같은 내용 크기는 전체 proof의 광범위한 중복보다 payload 퇴출·재해석을 뒷받침한다. manifest 고유 바이트와 2,894바이트 차이가 있으므로 파일별 identity 일치 증명은 아니다.

`g1-preparation-analysis.json`·`.md`는 호출 순서와 큰 파일을 연결한다. warm의 첫 바이트 읽기는 필수 전체 blob proof를 수행하며 선행 데이터를 index 수요와 같은 256MiB/4,096-entry LRU에 넣는다. 소비한 chunk도 MRU로 올라가므로 아직 읽지 않은 미래 chunk를 밀어낼 수 있다. 증명된 payload가 없으면 blob/앞부분을 순차 재해석한다. 작은 파일도 entry를 소비한다. 두 scan의 추론상 reuse distance는 warm 선행량의 약 두 배까지 갈 수 있지만 표본의 바이트 차이는 정확한 접근 추적이 아니다.

재해석은 **해석 바이트의 51.1%**이며 시간의 51.1%가 아니다. warm과 나머지 단계가 그대로라면 refresh만 줄여 얻는 절감은 최대 **6.5230142초**의 꼬리다. cache 정책은 warm 시간도 바꿀 수 있으므로 조건부 상한이다. 전체 32.4884104초 구간은 느슨한 구성 요소 상한이며 전체 proof를 유지하면서 달성할 수 있는 절감 추정값이 아니다.

별도로 구현한 cache hit의 decoder 대기 우회 후보는 **대상 시험 55개 통과 / 조건부 skip 1개**, 전체 검증 **1,731개 통과 / 실패 0 / 조건부 skip 30개**를 기록했다. 그러나 실제 준비에서 decoder 대기가 없었고 전체 측정 task에서도 **9회 / 0.000837초**뿐이었다. 요청한 큰 절감 기준을 충족하지 못해 제품에서 제거했다. 소스·patch·시험·고정 후보는 `rejected-cache-candidate/`와 후보 증거 파일에 보존한다. **실제 후보 GUI 실행은 없었으며 이 시험은 안전 동작의 근거이지 실측 속도 개선이 아니다.**

BD-01 종료 당시 더 큰 cache나 소비를 반영한 보존 정책은 설계 후보로만 남겼다. 구체적인 후속 probe는 reader 2개·128MiB 창을 유지한 1.5GiB/32,768-entry다. 바이트만 늘려서는 4,096-entry 제한을 넘지 못한다. provider당 보관 payload가 최대 1.25GiB 늘며 전체 프로세스/GC peak를 측정해야 한다. 정확히 2GiB는 계수 형식을 넓혀야 한다. 모든 입력 파일은 128MiB 안에 들어가며 창을 64MiB로 줄이면 파일 2개의 tail 104,267,732바이트를 처음부터 보관하지 않게 된다. BD-01 중에는 두 변경 모두 구현하지 않았다. 이후 [GR-01–03](git-payload-retention.md) 용량 실험은 실제 생성 시간 절감이 작아 후보를 제거했으며, 정책 변경은 미실측이다.

## Unity 시작과 보존한 캡처

U1의 `01-before/u1-initial-analysis.json`·`.md`는 완료한 before/live capture manifest를 정확한 workspace/provider와 live Editor 시작에 연결한다. 시작 전 **artifact 376개 / 61,313,054바이트**, 실행 중 **382개 / 62,428,564바이트**다. 제한된 파일별 관측이며 원자적인 Library snapshot은 아니다. live Editor 로그는 최종 로그의 앞부분과 같다.

최초 Bee 작업은 읽을 수 없는 첫 시작 DAG를 재생성하고 frontend 뒤 32.85초 backend에서 292개 갱신/956개 평가를 완료한다. action에는 `Splinter.Workspace.Editor.dll`의 `Csc` 1개와 ILPP 68개가 있다. bridge 컴파일 action은 trace에서 8.739초이며 runtime assembly MVID는 유지됐다. 미리 컴파일한 bridge가 자기 컴파일을 줄일 가능성은 있지만 ILPP, graph 재생성, reload 제거를 입증하지 않는다. 현재 최초 시작 DAG 무효화는 유지했다.

| Unity 보고 범위 | 초 | 경계 |
| --- | ---: | --- |
| 최초 refresh | 139.948 | 시작 작업을 포함 |
| script compilation | 50.711 | refresh 안 |
| 주 domain reload | 65.122 | 시작의 중첩 작업 |
| LoadAssemblies | 13.902 | reload 안 |
| ProcessInitializeOnLoadAttributes | 49.364 | 중첩 초기화 합계 |

이 범위는 겹치며 W1 flush 합계와도 더할 수 없다. 49.364초 초기화 합계는 아직 귀속하지 못한 가장 큰 Editor 범위이며 CPU 시간 측정값이 아니다. 보존한 buildprogram/Bee trace는 개별 초기화 class나 누적 Close 대기를 이 구간에 연결할 UTC 기준을 제공하지 않는다.

관측된 MCP HTTP 자동 시작 stack은 주 reload/최초 refresh 요약 **뒤**의 지연된 Editor update에서 실행된다. 이것만으로 이전 초기화 합계를 설명할 수 없으며 다른 package 초기화는 unknown이다. `StartupConfigRewrite refreshed 9 client config(s)`는 메모리의 상태 전환 수이며 쓰기 9회를 증명하지 않는다. 안전한 metadata/endpoint 감사와 설치 코드의 조기 반환은 변경 없는 확인을 뒷받침한다. 실행 시점의 전역 client-config 쓰기는 관측되지 않았지만 실행 전 독립 fingerprint가 없어 모든 과거 쓰기 부재를 입증하지는 못한다. 제품은 이 외부 동작을 patch하거나 관리하지 않는다.

근거가 있는 후속 귀속 작업은 이미 보존한 DAG/inputdata/응답 파일 edge의 제한된 비교, 필요 시 별도로 범위를 정한 외부 Editor profiler의 class별 초기화 관측이다. 오래된 endpoint나 비공개 callback 변경, 증명되지 않은 프로세스 종속 graph 재사용, DLL bridge가 시작 작업 전체를 없앤다는 가정은 이 증거로 뒷받침되지 않는다.

## 종료 확인과 자격

`01-before/closure.json`은 정상 Editor 종료 코드 0, 명시적 폐기, 모든 소유 provider 종료, 소스/config/ref/worktree 보존, 전체 측정 Library의 일반 NTFS 정확 복원을 확인한다. asset 검사는 model/texture/scene 존재와 예상 render hash `df94cefbc5f0d2fa6b46b878badb8848bc0221d66130778347349725a47b632d`를 기록했다. 선택한 asset/render 근거이며 Play/Test/player build 자격이 아니다.

측정 뒤 원본 sibling 프로젝트는 **`b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`**의 깨끗한 `development`로 돌아갔다. 원래 probe 내용/mtime, 다른 소스, 설정과 worktree 목록을 복원했고 기존 ref는 보존하며 fixture branch는 남겼다. Library는 정확한 워밍 후 측정 입력이며 **워밍 전 cache와 같다는 주장은 아니다**. 복원한 원래 probe는 다음 Editor 사용에서 다시 컴파일될 수 있다.

사용자는 이 실험을 위해 원본 MCP 서버의 일시 종료와 고정한 전역 handshake 값 정확히 두 개의 분리·복원을 별도 승인했다. package 코드와 나머지 설정은 보존했다. 소유 작업공간 종료·일반 Library 복원 뒤 원래 고정한 실행 인수/runtime hash를 확인하고 서버를 재시작했으며 listener/health와 설정 복원을 검증했다. PID 파일은 서버가 자연스럽게 갱신했다. 이 외부 복구는 391.3053613초 밖이며 제품 서버 관리 기능이 아니다. 최종 일반 Library는 이후 정상 서버 동작으로 바뀔 수 있다.

BD-01은 완료된 진단과 남은 비용의 근거다. **전후 개선, BO 전체 수명주기 절감률, 공유 Editor 비교, 원본 반영 시간, 새 Play/Test/build 지원, 일반 cache 이득, peak RAM/저장 절감, 전원 장애 동작**을 확정하지 않는다. 후속 최적화에는 구체적이고 안전한 설계와 실제 완료 경로의 이득 측정이 필요하며 큰 누적 counter만으로 이 기준을 충족하지 않는다.
