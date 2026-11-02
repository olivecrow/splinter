# 대규모 저장 처리량 진단

RECHECK-03 병목의 사용자 요청 국소 수정·제한/변동 시간 검증은 [ST-79–82](scalable-storage-throughput-followup.md#st-7982)가 소유한다. 이후 명시적으로 요청한 보존 100k 재개는 아래 [RECHECK-04](#recheck-04)가 소유한다.

커밋 이후 파일 쓰기 분석은 [WRITE-COST-01](#write-cost-01)이 소유하며 영속 barrier 지연·공유 index 저장 계측의 빈 구간·packing 증폭을 구분한다.

후속 사용자가 승인한 구현과 제한 검증은 [처리량 후속](scalable-storage-throughput-followup.md)에서 추적한다. 아래 기존 관측은 당시 증거로 유지한다.

사용자가 요청한 준비 병목 수정과 최종 epoch 보호 후보의 검증은 [ST-75–78](scalable-storage-throughput-followup.md#st-7578-준비-경로와-node-조회-비용)이 소유한다. 아래 과거 실패/복구 시간과 새 측정은 구분한다.

[English](../../verification/scalable-storage-throughput-diagnosis.md) · [SCAL 검증](scalable-storage.md) · [기계 판독 보고서](../../../reports/benchmarks/scalable-storage-slowpoints-20261101.json)

실제 100,000항목의 작은 파일 입력에서 **준비·쓰기·수집·계획에 개발 중 사용하기 어려울 만큼 긴 시간**이 걸렸다. 총량 상한 제거만으로 실용적인 처리량이 확보된 것은 아니다. 이 실행에는 복구 이력·동시 host 부하·단계별 혼합 고정 바이너리가 포함되므로, 대응 전후 벤치마크나 일반 대규모 프로젝트의 예상 시간으로 읽지 않는다.

상태: **처리량 문제 미해결.** 2026-11-01 절은 축소 종료한 과거 증거를 보존하며 자동 재개를 승인하지 않는다. 아래 제한 재시도·재개는 사용자가 2026-11-02–03 명시적으로 요청했다. SCAL-03·SCAL-07·SCAL-08은 [소유 원장](scalable-storage.md)에서 미완료로 남는다.

<a id="recheck-04"></a>

## 2026-11-02–03 쓰기 수정 뒤 보존 100k 재개(RECHECK-04)

사용자가 보존 100k 작업을 재개하고 과거 기록의 절반을 넘으면 안전하게 중단하도록 명시했다. 최종 ST-79–82를 실행 내내 그대로 사용했으며 **소스/config hash 328개·고정 바이너리 hash 32개가 일치**한다. RECHECK-03의 같은 None 기준본/A/B와 요청 identity를 유지했다. 이번에는 남은 1,812개만이 아니라 **99,990파일 전체에 새 쓰기 한 회**를 실행했으며 과거의 worker 8개·32바이트 변형·고정 mtime driver를 사용했다. 기존 private 범위/이력·index·시작 packing·host/cache 조건이 과거/새 입력과 다르므로, 이는 단계별 관측 시간이며 **통제된 패치 인과 개선율은 아니다**.

| 단계 | 과거 초 | 절반 중단선 초 | 현재 초 / 결과 |
| --- | ---: | ---: | --- |
| 99,990파일 전체 쓰기 | 2,944.1676890 | 1,472.0838445 | **1,254.7332609**, 전체 파일·3,199,680바이트 완료/drain; 과거 경과 시간의 **42.62%** |
| 재연결까지 보수적으로 포함한 첫 수집 | 3,869.3736515 | 1,934.68682575 | **707.5361660**, 완료; 과거 경과 시간의 **18.29%** |
| 읽기 전용 원본 반영 계획 | **3,945.0931925 중단 하한** | 1,972.54659625 | **464.9044448**, 완료; 변경 99,990개·충돌 0개 |

어느 측정 단계도 과거 절반 중단선에 도달하지 않았다. 과거 계획은 완료하지 않았으므로 과거의 완료 시간이나 대응 인과 개선율을 계산하지 않는다. 쓰기 시계에서 dispatch/type loading·정상 Stop은 제외한다. 수집은 최초 실패 명령 **0.5130345초**와 원본/A 재연결·같은 ensure를 포함한 실제 재개 **707.0231315초**를 보수적으로 합산했다. 성공한 수집 명령만의 시간은 **440.5851863초**다. 중단 상태의 진단·사용자 대기 간격은 실제 명령 시간이 아니다. 읽기 전용 stack 관측은 각 측정 시계 안에 포함했고 보정하여 빼지 않았다. 측정 중 별도 build/test는 실행하지 않았다.

중단 상태의 진단 간격과 정상 Stop까지 포함해도 최초 수집 시작 **00:04:26.2271448**부터 성공 완료 **00:22:16.6873225 +09:00**까지 관측 시각 차이는 **1,070.4601777초(17분 50.46초)**로 **1,934.68682575초** 중단선 안이다. 실제 명령 합계와 전체 시각 차이는 구분하며, 진단 간격을 제외해야만 통과하는 결과가 아니다.

최초 원본/A/B 재시작은 **38.6976785초**였다. 이후 원본/A 정상 재연결과 제품 `workspace ensure`는 **266.2405488초**(ensure 진입점 **259.3072815초**)였으며, 합산 재연결 **304.9382314초**는 준비 한도 **1,009.3601973초** 안이었다. 완료한 준비를 재사용했으므로 **새 기준본/A/B 준비 시간이 아니다**. 새 준비 관측은 RECHECK-03의 **43.4438196 / 611.5537642 / 530.2514760초**로 유지한다.

최초 수집은 시간 초과가 아니라 안전 재시도 가능한 `SourceCaptureUncertain`으로 실패했다. 실험 재시작 helper가 새 소유 Provider를 시작했지만 작업 기록의 이전 PID/시작 lease를 갱신하지 않았다. 수집 freeze는 이미 영속 완료했다. Provider 3개를 정상 Stop한 뒤 같은 제품 ensure로 A의 현재 lease를 저장하고 **정확히 같은 `capture-1` 요청**을 재시도했다. 전체 파일 쓰기 반복이나 기록 수동 편집은 없었다. 결과 **`13b3a5724ada7f8306edf2b0`**·소스 sequence **594,504**·수집 digest **`2acaf50ba2efeaebb47fb2a034a45093d345f791a282fef4d71a055921acde04`**는 고정됐다. 이는 harness 재연결 결함과 복구 비용이며 절반 시간 기준의 처리량 실패가 아니다.

남은 관측 비용과 효율화 후보는 다음과 같다.

1. **영속화·metadata 증폭은 여전히 크다.** 쓰기 전부터 최초 수집/정상 Stop까지 같은 epoch 관측 차이는 **flush 220,716회·누적 flush 623.8871018초**, **commit 644,046회**, **journal append 336,687,984바이트**다. 회수와 작은 freeze/Stop 비용도 포함하므로 순수 쓰기 전용 시간이나 할당 디스크 크기가 아니다. **사용자 commit 299,970회** 외에 **범위 재배치 338,643회**가 추가됐다. 유효 쓰기 stack은 `ApplyMutationBatch → CommitStorageBatch → FlushPendingWrites`와 paged node 저장을 보인다. 영속성·GC·native 증명을 유지하며 callback ACK 결합·index 경로 재사용·packing의 live/회수 비율을 더 분리할 수 있다. Thread 대기/operation 시간은 전체 경과와 겹친다.
2. **파일마다 catalog를 다시 여는 경로는 코드로 확인했다.** 수집 stack 1은 `BindRetainedPayloads → baseline.Find → new PagedSnapshotCatalog → ReadNodeCore → EnsurePlainPath`다. `FileSourceManifest.Find`는 제한된 작업 내 `OpenReader`가 이미 있는데도 매 호출 reader를 새로 연다. 보존 선택 내 fenced reader 재사용이 구체적인 후보다. 단일 stack은 배타적 시간이나 호출 횟수를 입증하지 않는다. 수집 stack 2는 CLI의 idle/awaiting thread만 보이며 비동기 Provider 비용을 귀속하지 않는다.
3. **현재 원본 전체 관측/index 구성은 남는다.** 계획 stack은 `Scan.Metadata → CheckedOriginalEntries → FileApplyIndex.Build`다. 반복 metadata 진입은 국소 비용 분리 대상이며 현재 소스 전체 검증은 유지해야 한다. 별도 수집 adoption은 **99,990 commit을 flush 1,563회**로 완료했으며 범위마다 flush한 것은 아니다. 이 operation 수치로 전체 수집 시간을 분할하지 않는다.

최종 계획 digest는 **`sha256:c417a0b91af205f6528e66a1df5649c328af7a08ec86b16b5ae566ef38871680`**다. 이후 **원본 반영은 실행하지 않았다**. A·원본은 11월 3일 **00:30:02–06 +09:00**에 인증된 정상 Stop을 했고 B는 이미 정상 중단 상태였다. 정확한 fixture 프로세스 감사와 view 3개 검사에서 일치하는 CLI/Provider·mount가 없다. A/B는 `ready`, A는 결과 1개를 보존하며 양쪽에 미완료 capture/원본 반영 intent가 없다. Task/기준본/private journal/결과/요청 근거를 보존했으며 관리형 원본은 **중단 상태이지 일반 NTFS로 복원한 것이 아니다**. 강제 종료·release·discard·restore는 없었다.

보존한 작은 파일 None 입력 한 회의 재개이며 새 ABBA·고유 대형 asset/17GiB·원본 전체 반영/release·Git/Plastic/Unity·전체 gate·peak RSS·OS 장애 자격은 아니다. [기계 보고서](../../../reports/benchmarks/throughput-resume-st79-20261103.json)는 identity·한도·hash·단계 시간·실패/복구·counter·종료 근거를 연결한다. 원시 근거는 `C:/Users/olive/AppData/Local/Temp/sscal3n-20261102-remeasure/evidence/resume-st79-20261102`와 `artifacts/throughput-resume-st79-20261102/`에 있다.

<a id="write-cost-01"></a>

## 2026-11-03 커밋 이후 파일 쓰기 비용 분석(WRITE-COST-01)

사용자가 완료한 작업을 먼저 커밋하고 파일 쓰기를 분석하도록 요청했다. **`c6e0b17`**은 ST-75–82·집중 시험·RECHECK-04를 포함한다. 이번 분석은 같은 epoch의 기존 counter, **저장 관측 14개·인접 구간 13개**, Provider stack 1개와 현재 코드를 사용했다. **100k/Provider/CLI 재시작·payload 접근·새 벤치마크·제품 수정 없이** 분석했다. [기계 보고서](../../../reports/benchmarks/write-cost-diagnosis-20261103.json)는 입력/표본 hash·전체 구간 계산·operation 차이의 합계 대조·미확정 범위를 보존한다.

주요 관측 병목은 여전히 영속 journal barrier이며, 그다음은 아직 분리하지 못한 index 저장이다. Packing은 작업량 증폭이 크지만 직접 계측한 barrier 시간 비중은 작다. 관측 간격에는 동시 회수와 작은 최초 capture/Stop 비용이 있어 배타적인 순수 쓰기 전용 시간은 아니다.

| 비용 묶음 | 영속 commit | 귀속 flush | flush 초 | journal append 바이트 |
| --- | ---: | ---: | ---: | ---: |
| 사용자 resize/write/metadata | 299,970 | 215,200 | 611.6502652 | 117,188,280 |
| Packing 진행/재배치/완료 | 344,075 | 5,515 | 12.2348547 | 219,498,852 |
| 최초 수집 freeze | 1 | 1 | 0.0019819 | 852 |
| 전체 차이 | 644,046 | 220,716 | 623.8871018 | 336,687,984 |

**Barrier 지연과 결합 정도.** 누적 flush는 쓰기 경과 1,254.7332609초의 **49.72%**, 평균 flush는 **2.826651ms**다. 사용자 flush 한 번당 사용자 commit은 **1.393913개**다. 공유 barrier를 마지막 operation에 귀속한 집계 지표이며 native callback batch 크기는 아니다. 64개 단위 한도는 동기 callback 64개의 동시 접수를 보장하지 않으며 Journal `waitingMutations`는 앞단 WinFsp FIFO를 포함하지 않는다. 최대 1이라는 수치로 실제 application worker가 하나였다고 판단할 수 **없다**. 이번 100k 실행에는 native 접수 histogram을 켜지 않았다.

저장 관측의 **23:56:54.8445189–23:57:56.8111595 +09:00** 구간은 **61.9666406초**이며 **flush 4,543회·54.4972177초**다. 회당 **11.995866ms**, 구간의 **87.95%**를 차지했다. 사용자 처리량은 **103.700958 commit/s**로, 가장 빠른 저장 구간의 **346.785536 commit/s**보다 낮았다. 빠른 구간 평균 flush는 **2.064368ms**다. 두 구간 모두 기록된 재배치·packing 완료 증가는 없었다. 영속 barrier 정체는 확인하지만 저장 장치·OS·다른 프로세스·동시 index 활동 중 무엇이 지연을 만들었는지는 귀속하지 못했다. 여러 thread의 대기 합계를 경과 시간에 더하지 않는다.

**Index 저장 계측의 빈 구간.** `Commit`은 mutation별 `Apply`를 재지만, `FlushPendingWrites`는 마지막 barrier timer를 시작하기 **전에** `FlushIndexBatch`를 호출한다. 보류한 node/path/name 변경은 `SetBatch → WriteNode`로 저장하므로 공유 저장 비용이 개별 `ApplyTicks`와 마지막 barrier 구간 밖에 있다. 사용자 operation의 Apply 합계 **97.9749945초**는 전체 index 비용이 아니다. Provider stack은 이 경로의 `SetBatch/Persist/WriteNode`와 대기 중인 Close/GetFileInfo·다른 callback을 직접 보인다. `CompleteMetadata`는 index 처리와 영속화 동안 mutation gate를 유지한다. Gate/queue 대기는 뒤따르는 현상이며 미분리 나머지 시간을 모두 index 시간으로 부를 수는 없다. `WriteNode`는 용량 확인과 append 위치 지정 전 output 길이도 각각 조회하지만 그 syscall의 배타적 비용은 미계측이다.

**Packing 증폭과 counter 귀속.** Packing은 journal append의 **65.193551%**, 직접 flush 시간의 **1.961069%**다. **재배치 338,643회·진행 기록 5,426회·완료 기록 6회**를 추가한다. 보통 한 묶음은 최대 **재배치 63개·진행 기록 1개**이므로 공유 flush가 `packing-state`에 귀속된다. 재배치 항목만의 **flush 83회**가 packing 전체 barrier 수는 아니며 전체는 **5,515회**다. Journal append는 변경 payload 3,199,680바이트의 **105.225518배**이며 metadata page 쓰기는 별도이고 물리 할당/대역폭 수치는 아니다. 작업별 실제 순수 회수 바이트와 복사/index 비용은 기록하지 않았다.

`BuildPackingPlan`은 `descriptor.Length - liveUnion >= 4096 && liveUnion > 0`이면 후보를 채택하며 live/dead 비율이나 재배치 행 비용 조건은 없다. 최초 journal→raw packing은 frame overhead 제거에 유익할 수 있다. 반면 packed→packed 반복은 작은 dead 영역을 회수하려고 거의 모든 live 행을 다시 옮길 수 있다. **산술 예시일 뿐인 경우:** packed payload 3,199,680바이트 중 dead 4,096바이트(32바이트 파일 128개)라면 **live 3,195,584바이트·파일 범위 99,862개**가 남으며 dead 1바이트당 약 **780.17바이트**를 복사할 수 있다. Header를 보수적으로 제외한 예시이며 RECHECK-04의 실제 작업이나 실측 낭비량은 아니다.

다음 국소 분석은 먼저 FIFO/묶음 접수량·mutation gate 점유·`FlushIndexBatch` 시간/page 바이트를 나눠 재고, 과거 `Parallel.For` driver와 준비된 전용 worker를 구분해야 한다. 그다음 반복 node/path 저장을 줄일 지점을 확인하고, 마지막으로 순수 회수량 대비 이동 바이트/행을 잰 뒤 packed object의 비용 기반 접수/재실행 간격을 시험한다. Callback별 영속 ACK·입력/handle lease·poison/drain·현재 native/root/cursor 증명·미완료 같은 요청 복구·검증된 dead 회수·최종 강제 정리는 유지한다. Timeout 확대·ACK 제거·근거 없는 cache/worker 확대·GC 비활성화·SPM2 전체 교체는 이번 분석으로 정당화하지 않는다. 새 수정이나 개선율을 주장하지 않는다.

<a id="recheck-03"></a>

## 2026-11-02 준비 수정 뒤 새 100k 재측정(RECHECK-03)

사용자가 다시 측정하고 **과거 소요 시간의 절반 이상에 도달하면** 안전하게 중단한 뒤 병목을 분석하도록 명시했다. 최종 ST-75–78 epoch 보호 바이너리를 그대로 사용했으며 기록된 manifest의 **소스/config 327개·바이너리 32개 hash**가 모두 일치한다. 새 None fixture는 같은 **디렉터리 10개·32바이트 일반 파일 99,990개·내용 16종·고정 시각/속성**이다. 입력 생성 **51.947688초**는 제품 시간 밖이다. 새 기준본과 새 A/B로 시작하여 A는 이전에 완료한 사전검사/baseline을 재사용하지 않았다. 이번 명시적 과거 절반 한도를 일반 예상의 두 배 규칙으로 단축하지 않았다.

| 단계 | 과거 초 | 절반 중단선, 초 | 현재 초 / 결과 |
| --- | ---: | ---: | --- |
| 기준본 준비 | 189.7678915 | 94.88394575 | **43.4438196**, 완료 |
| A 준비 | 2,018.7203946 | 1,009.3601973 | **611.5537642**, 완료 |
| B 준비 | 2,446.2287019 | 1,223.11435095 | **530.2514760**, 완료 |
| 8 worker 일괄 쓰기 | 2,944.1676890 | **1,472.0838445** | **1,472.2870604**, 진행 중 작업 drain 후 중단; **98,178 / 99,990파일** 완료 |
| 첫 수집 / 읽기 전용 원본 계획 | 별도 과거 기록 | 1,934.68682575 / 1,972.54659625 | 쓰기 한도 도달 뒤 **NotRun** |

준비 세 명령 모두 응답 proof를 포함해 중단선 안에서 `Succeeded`를 반환했다. 관측 소요 차이는 각각 77.106865%·69.705871%·78.323716% 감소다. 과거 A는 마지막 복구 성공 호출이며 후보/cache/host 조건도 달라 **patch만의 통제한 인과 개선율이 아니다.** 과거 계획 시간은 중단한 하한이지 완료 시간 분모가 아니다. B는 기존 순차 시나리오처럼 A 뒤에 이번 새 기준본을 공유한다.

쓰기 driver는 동일한 8 worker의 32바이트 변경·고정 last-write-time 작업이다. **1,472.0838445초**에 새 파일 진입을 막고 이미 시작한 파일의 쓰기/mtime 작업을 끝까지 drain했다. 초과 **0.2032159초**는 drain/반환 시간이며 한도를 늘린 것이 아니다. **1,812파일이 미작성**으로 남았으므로 완료 시간을 외삽하거나 전체 쓰기 50% 개선으로 보고하지 않는다. 일괄 쓰기 시계는 driver 내부이며 type 적재·입력 준비·schema·dispatch·정상 Stop은 별도다. 한도 뒤 original-only 편집·수집·계획·원본 게시를 시작하지 않았다.

중단한 writer의 카운터와 유효한 실제 provider 스택 한 개가 다음 비용을 확인한다.

1. **잦은 영속 journal flush.** `JournalDeltaStore.DurableFlush`는 `journal.Flush(true)`를 호출한다. 정상 Stop을 포함한 최종 카운터는 **284,804회·771.5042324초**다. **resize 98,178건**은 매번 flush하고, **write 98,178건**은 **91,784회**, **metadata 98,178건**은 **93,338회** flush했다. 기존 제한 batch가 파일 callback ACK를 공유하는 비율이 낮다. 스택의 `FlushToDisk → FlushPendingWrites → WriteBatch`와 다른 WinFsp callback의 monitor 대기도 확인했다. 내구성 지연/진입이 주 측정 비용이며 ACK 제거는 해결이 아니다.
2. **Index 변경과 동시 회수의 작업 증폭.** Resize `Apply` 누적은 **176.5685430초**, write는 **37.4178832초**, metadata는 **26.5799147초**다. 범위 truncate·namespace/path-change·node 갱신을 포함하며 개별 tree 함수의 배타 시간은 아니다. 같은 스택의 다른 thread는 `MaintainLiveStorage → CollectLiveMetadataCore → Replay → Apply → SetPathChange → PagedMetadataStore.Set/ReadNodeCore`를 실행했다. **사용자 작업 commit 294,534건** 외에 background packing의 **range 재배치 89,753건 + packing-state 1,475건 + 완료 6건 = 내부 영속 commit 91,234건**이 추가됐다. Range 재배치는 이미 **23회 flush**로 묶였으므로 추가 비용 전체를 재배치의 개별 flush 탓으로 돌릴 수 없다. 변경 payload **3,141,696바이트**에 journal append는 **172,965,613바이트**였다. 기록된 journal 작업량이며 물리 할당이나 payload 대역폭 자격은 아니다.
3. **잠금 대기는 더해서 계산하는 별도 단계가 아니다.** Journal gate 대기는 여러 thread 합계 **2,705.9978122초**, mutation-gate 대기는 **301.9938545초**다. 다른 작업·서로와 겹치므로 wall time에 합산하지 않는다. 동시 회수 경로와 추가 작업량은 확인했으나 회수의 배타 시간 비중·각 대기 callback 잠금의 소유자는 분리하지 못했다.

다음 최소 재현에서는 실제 native callback의 batch 진입/ACK 공유, resize index 갱신, 동시 packing/shadow replay를 분리해야 한다. 개별 내구 완료·현재 소유/native 증명·같은 요청 복구를 유지하며 timeout 확대·검증 생략·참조 데이터 보호 해제로 우회하지 않는다. 이번 측정은 제품 코드를 바꾸지 않았고 한도 뒤 다른 대형 단계를 시작하지 않았다. 뒤의 스택 도구 호출 한 번은 exit 0이지만 출력이 비어 **스택 근거로 사용하지 않았다.** A 응답 표본은 성공 종료와 경합해 attach하지 못했다. 두 시도 모두 유효한 표본과 분리해 보존한다.

정확한 fixture는 `C:/Users/olive/AppData/Local/Temp/sscal3n-20261102-remeasure`, 기준본 **`a1702646add993f92b08fb4e`**, A **`21d5b629c1f07ce9042dd2ee`** / `scal-a` / digest **`e505ea6676fe69d4bf741c72`**, B **`03e3b4cd9bc29da39605118b`** / `scal-b`다. Provider 3개는 **21:52:48–51 +09:00**에 인증된 정상 Stop을 마쳤고 최종 PID/시작 시각 감사에서 일치하는 CLI/provider·mount 부재를 확인했다. Task·basis·private journal·요청 identity·증거는 모두 보존한다. 관리형 원본은 **중단 상태이며 일반 NTFS로 복원한 것이 아니다.** 강제 종료·release·discard·restore는 없었다. 수집/반영·17GiB·Git/Plastic/Unity·전체 gate·전체 수명주기 성능은 미자격이다. [기계 보고서](../../../reports/benchmarks/throughput-remeasure-20261102.json)는 정책·시간·소스/바이너리 manifest·정확한 id·분석·Stop 감사를 연결하고, 원시/공개 증거와 진단 도구는 `artifacts/throughput-remeasure-20261102/`에 보존한다.

## 2026-11-02 절반 시간 기준으로 정정한 재개(RECHECK-02)

사용자가 아래 조기 중단을 정정했다. **새 예상의 두 배가 아니라 과거 소요 시간의 절반을 기준으로 삼는다.** 같은 고정 후보·fixture·basis·A id/key/digest로 재개했고 기존 기준본 준비는 반복하지 않았다. 이번 명시적 재개는 이 시험에 한해 일반적인 예상/수십 분 중단 규칙보다 우선한다. **A는 실제 과거 절반 시간 안에서도 완료되지 않았다.**

| 단계 | 과거 관측 | 현재 관측 |
| --- | ---: | --- |
| None 기준본 준비 | 189.7678915초, 완료 | 98.7819962초, 완료: 47.945885% 단축; 절반보다 3.898050초 초과 |
| A 준비 | 2,018.7203946초, 마지막 복구 성공 호출 | **누적 1,009.578185초**, 미완료; 절반 기준 **1,009.3601973초**에서 취소 |
| B·쓰기·수집·원본 계획 | 별도 과거 증거 | A가 한도에 도달하여 NotRun |

A 누적에는 앞선 호출 120.0255507초, 원본 정상 재연결/Stop을 포함한 재개 wrapper 실패 8.6153063초, 마지막 재개에서 재연결·dispatch·취소/drain 반환까지 880.937328초를 보수적으로 포함했다. 사용자와의 대화/중간 대기는 제외한다. Wrapper 실패는 A 호출 전에 발생한 PowerShell `Cli`/`Clear-Item` 별칭 충돌이었으며 `Invoke-HalfCli`로 고치고 실패 증거를 보존했으며 identity는 바꾸지 않았다. 마지막 실제 CLI 진입점은 남은 한도 **875.6104484초**에 대해 **875.6331665초**였다. 새 예상 기한이나 예상 진행률에 따른 조기 중단은 적용하지 않았다. 누적 초과 **0.217988초**는 취소/반환 비용이며 한도를 늘린 것이 아니다.

실제 스택과 파일 게시 경계는 재개한 A 준비에서 연속하는 큰 순회 세 개를 확인했다.

1. Live 원본 metadata 사전검사: `PreflightFilesAsync → Scan.Metadata → Plain`; CLI 시작부터 `files/owner.json` 게시까지 연결 경계를 포함해 약 **478.245초**였다. 원본 provider는 **root 포함 basis node 100,001개**에 도달했다.
2. 불변 기준본: `EstablishBaselineAsync → Manifest → PagedSnapshotCatalog.Build`에서 파일별 `Plain`/hash 작업을 수행했고, `files/baseline.json` 게시까지 추가 **175.555초**였다.
3. Live 작업공간 내용: `InspectWithProofAsync → Manifest(ScanWorkspace(...)) → PagedSnapshotCatalog.Build`; 기준본 게시부터 취소 반환까지 **221.941초**가 지났지만 미완료였다. 정상 Stop 시 A는 **node 31,364개**, **base-read 2,006,912바이트**, **사용자 commit 0개**, **index 페이지 파일 길이 합계 449,721,841바이트**를 기록했다. Provider 스택에도 파일 정보 조회 중 paged node/경로 구성요소 재구성이 잡혔다.

이 구간은 artifact/스택에 근거한 관측이며 **함수별 배타 시간이나 완료 파일 비율이 아니다.** 남은 병목은 원본·기준본·작업공간의 반복 inventory/내용 순회와 파일별 상위 경로 검사·paged metadata 진입/조회다. 이후 결과 수집 최적화가 이 최초 준비 경로를 없애지는 않는다. 기존 native/reader/소유권/변경 fence 아래 정확한 현재 원본/기준본 증명 공유와 제한된 최초 node 진입을 조사해야 하며, live 원본 검사를 빼거나 오래된 기준본을 현재 내용으로 간주하면 안 된다. 이번 측정 중 제품은 수정하지 않았다.

**19:10:02 +09:00**에 A가 `creating`/`attach-intent` 상태로 `Cancelled`를 반환했다. Record에 남은 이전 mount 관측은 과거 관측이며 현재 생존의 증거가 아니다. A·원본의 인증된 정상 Stop은 **19:10:04 / 19:10:05**에 각각 process/mount 부재를 반환했고, 최종 감사에서도 일치하는 CLI/provider와 두 mount가 없었다. Task·baseline·basis·delta·요청 identity·기존 증거는 보존했으며 게시·release·discard·project restore는 없었다. 고정 제품 파일 **32개의 hash**도 모두 일치했다. 이번 재개와 과거 복구 호출은 새 입력의 중단 없는 A 벤치마크가 아니므로 완료한 A 개선율·쓰기/수집/계획 개선·전체 gate·SCAL 완료는 주장하지 않는다. [갱신 보고서](../../../reports/benchmarks/scalable-storage-recheck-20261102.json)는 RECHECK-01을 별도로 보존하고 `artifacts/scalable-storage-recheck-20261102/continuation/`의 정책·명령별 기한·진행 관측·스택·시간 계상·실패·최종 Stop 증거를 연결한다.

## 2026-11-02 최초 재시도: 잘못 적용한 준비 조기 중단(RECHECK-01)

**과거 쓰기·수집·계획 시간이 절반 이하가 됐다는 증거는 확보하지 못했다.** 현재 `62c29ed`는 관리형 None 기준본 준비를 **98.781996초**에 완료했지만, workspace A는 내용 읽기 없는 원본 metadata 사전검사에서 **120.025551초**까지 머물러 협력 취소했다. 준비 명령별 예상은 60초, 취소 경계는 120초로 고정했다. 기한 뒤 B 준비·전체 쓰기·수집·원본 계획/게시를 시작하지 않았다. 이는 그보다 앞선 준비 단계의 중단이며, **실행하지 않은 후속 단계가 50% 기준에 실패했다고 측정한 것은 아니다.**

사용자는 현재 재시도와 절반 이하로 줄지 않았을 때 안전한 중단·진단을 요청했다. 과거 쓰기·수집·계획의 절반 기준은 각각 **1,472.0838445 / 1,934.68682575 / 1,972.54659625초**이며, 마지막은 중단한 과거 기록의 하한을 쓴다. 최초 시도는 이 작업의 지시보다 저장소의 더 짧은 예상 두 배 기준을 잘못 우선 적용했다. 위 정정과 재개가 현재 결과를 소유한다. 과거 Git/Plastic 준비에는 복구가 섞여 있어 새 None 준비와 비교하지 않는다.

새 fixture는 과거와 같은 **디렉터리 10개·32바이트 일반 파일 99,990개·논리 합계 3,199,680바이트·최초 내용 16종**이다. 경로·바이트 생성식·고정 생성/접근/수정 시각·Archive 속성은 보존 시나리오를 따른다. Release 빌드 성공 뒤 task v4/basis v3/overlay v3·`SPM2`·결과 manifest v4 바이너리를 고정했다. 이전에 중단한 fixture는 형식이 호환되지 않아 재개·이관·편집·삭제하지 않았다. 현재 실행은 실제 None 1회의 제한 시도이며, 대응 전후나 ABBA 제품 벤치마크가 아니다.

| 현재 관측 | 시간·결과 |
| --- | --- |
| 관리형 `project prepare` | 98.781996초, 성공; basis `9373cc048772da946046ebd1` |
| A `workspace ensure` | 120.025551초, `Cancelled`; workspace `fdc0ca2d1c0e48d1e057ebb8`, key `scal-a`, phase `creating`, snapshot step `requested` |
| 준비 wrapper | schema 호출·dispatch·표본 수집 포함 225.967358초; 이후 정상 Stop 제외 |
| 전체 쓰기·수집·원본 계획 | NotRun; 50% 개선율 계산 불가 |

실제 CLI/provider 스택 표본과 현재 코드는 다음 남은 경로를 확인했다.

1. `ManagedDirectoryBasis.RequireNoVcs`는 모든 항목을 열거하고 `WorkspaceStore.EnsurePlainPath`를 호출하여 파일마다 볼륨 root까지 `File.GetAttributes`를 반복한다. 이 반복문에는 **취소 토큰·확인이 없다.** 이번에는 기한 전에 끝났으므로 이 함수의 취소 초과를 관측했다고 주장하지 않는다. 그다음 전체 순회인 `SnapshotInputChecks.VerifyTree`는 manifest 수집 전에 속성·대체 스트림·native 핸들·hardlink 수를 검사한다.
2. A의 취소 단계는 관리형 원본의 live mount를 통한 `FileWorkspaceProvider.PreflightFilesAsync → Scan.Metadata → Plain`이었다. `Plain`은 상위 경로의 `Directory.Exists`/`File.Exists`/속성을 반복하고, `Scan`은 속성·길이·mtime도 따로 조회한다. 이후 수집 최적화가 이 최초 원본 사전검사를 없애지는 않는다.
3. Provider 표본에는 `GetSecurityByName → Stat → Lookup/AddBaseNode → PathComponents.Intern → PagedNodeMap.Save`와 `GetFileInfo`의 경로 구성요소 재구성이 잡혔다. 변경 없는 기준 파일을 조회하는 것만으로 node를 진입시키고 paged metadata를 갱신한다. 정상 Stop 시 **indexed node 25,459개**, index 페이지 파일 6개의 길이 합계 **357,329,537바이트**가 누적됐다. Payload/base-read 바이트·사용자 commit·durable sequence는 모두 **0**이다. 사용자 편집 전 읽기 진입/인덱스 작업이 크다는 근거이며 payload 복사 비용이 아니다. Node 수는 완료 파일의 정확한 비율이 아니고, 페이지 파일 길이는 물리 할당이나 필수 최소량이 아니다.

중단한 실제 basis의 256파일에서 현재 제품 함수를 역순으로 각각 두 번 호출한 읽기 전용 작은 측정도 수행했다. `EnsurePlainPath`는 **0.174140 / 0.168443초**였으며 더 깊은 basis 경로에서 관측당 **상위 속성 조회 3,328회**를 수행한다. `SnapshotInputChecks.Verify`는 **0.049589 / 0.040483초**, leaf 속성 1회 조회는 **0.023302 / 0.019957초**였다. 반복 진입 비용을 분리한 값이며 전체 준비의 배타 시간·mount 시간·100k 외삽이 아니다. Provider의 journal flush는 정상 close 1회(**0.001938초**)뿐이어서 이 카운터로 취소한 120초 사전검사를 flush 대기로 설명할 수 없다. 잠금 카운터·metadata 캐시 계측·스택 표본은 배타 시간 분해나 RSS 측정이 아니다.

다음 조사 대상은 범위에 고정한 **현재 원본** 상위 경로/metadata 증명과 변경 없는 basis node의 제한된 진입·인덱스 구성이다. 원본 변경 검출·native/reparse/소유권 fence·내구 게시 경계를 유지해야 하며, 오래된 불변 view로 대체하거나 검사를 빼는 것은 해결이 아니다. `RequireNoVcs` 취소 확인은 별도의 응답성 문제다. 이번 재시도에서 제품 코드는 바꾸지 않았고 취소 뒤 새 대형 단계를 시작하지 않았다.

실행 한도·사고도 보존한다. 전체 작업 예상 25분/중단 50분, 입력 생성 30초/60초, 준비 단계 180초/360초, 개별 준비 명령 60초/120초였다. 최초 병렬 빌드는 진단 오류 없이 3.74초에 실패했고, 직렬 Release는 경고·오류 0개로 **93.17초**에 성공하여 빌드의 30초/60초 중단선을 놓쳤다. 이는 절차 실패다. 최초 시험 입력 생성은 **60.004764초·74,469파일**에서 안전하게 멈췄다. 별도 256파일 ABBA로 동일 바이트/시각/속성을 열린 핸들 하나와 결합 native metadata 설정으로 유지하며 준비 도구 비용이 줄어드는 것(**블록 평균 0.181619→0.101966초**)을 검증한 뒤에만, 같은 부분 입력의 나머지 25,521파일을 **10.227985초**에 완료했다. 이는 시험 준비 도구 수정이며 Splinter 처리량 개선이 아니고, 원래 예상은 늘리지 않았다.

정확한 소유 identity의 인증된 정상 Stop은 **18:17:21 +09:00**에 `processAlive=false`·`mountExists=false`를 반환했고 이후 process 감사에서도 일치하는 provider/CLI가 없었다. Task·basis·index·요청 key/digest와 모든 과거 증거는 보존했다. 새 fixture의 관리형 원본은 **중단 상태이며 일반 NTFS로 복원한 것이 아니다.** 강제 종료·release·discard·원본 게시는 없었다. Git/Plastic 재시도·17GiB·Unity·전체 gate·대형 성능 자격은 추가하지 않는다. [기계 보고서](../../../reports/benchmarks/scalable-storage-recheck-20261102.json)는 고정 바이너리/소스 hash·정확한 fixture/identity·시간·Stop 영수증·제한을 연결하며, 원시 스크립트·스택·관측은 `artifacts/scalable-storage-recheck-20261102/`에 보존했다.

## 입력과 시간의 경계

고정한 Git/None/Plastic 입력은 각각 디렉터리 10개와 32바이트 일반 파일 99,990개다. 총 100,000항목·파일 크기 합계 3,199,680바이트이며 최초 내용은 16종류다. 따라서 기준본의 **고유 논리 payload는 512바이트**뿐이다. 파일별 metadata·증명·소유 처리에 부하를 주는 입력이며, 대부분이 서로 다른 대형 에셋이나 payload 대역폭 자격이 아니다.

아래 시각은 모두 2026-11-01이다. Artifact 시각은 관측 구간이며 개별 함수의 배타 시간계가 아니다. 복구 호출·부분 단계·겹치는 프로세스 표본을 하나의 전체 수명주기 시간으로 합산하지 않는다. 과거 RI/GCO/MX 시간도 이 후보의 대응 기준 측정이 아니다.

| ID | 기존 관측 | 초와 결과 |
| --- | --- | --- |
| OBS-N1 | None: drain을 확인한 8 worker가 99,990파일을 쓰고 고정 mtime 설정 | 2,944.167689; 쓰기 3,199,680바이트 |
| OBS-N2 | None: 첫 `capture-1` | 3,869.3736515; 불변 결과 `60864f835b308dccc6c2ec96`, 고정 source sequence 299,970 |
| OBS-N3 | None: 첫 읽기 전용 `integrate --plan` | 3,945.0931925 뒤 15:36:54 +09:00에 명시 중단; 완료 계획 응답·원본 게시 없음 |
| OBS-G1 | Git: record timer 수정 뒤 같은 B 준비 | 2,476.9713499; 복구를 포함한 기능 준비 성공 |
| OBS-G2 | Git: 마지막 canonical B 확인 | 명령 전체 766.1348281; 내부 증분 source inspection 760.2953989, 99,990파일 재사용, payload/Git hash 입력 0 |
| OBS-G3 | Git: 실제 고정 수집 CLI 유실 복구 | 7,221.1475198; 같은 요청/root/digest 복구, 독립 결과·후속 편집 보존 |
| OBS-P1 | Plastic: 복구·재검증을 포함한 A 같은 key 준비 | 10,488.1015401; 기존 승인 node 58,332개를 재검증한 뒤 성공. 새 정상 trial 아님 |
| OBS-P2 | Plastic: 저장된 READY부터 A 응답 | 최초 READY 관측 14:37:42 +09:00, process 반환 약15:20:02 +09:00, 약42분. metadata 재검증 한 함수의 배타 시간 아님 |

원시 근거: None [쓰기](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/bulk-edit-metrics.json)·[수집](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1.process-metrics.json)·[계획](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/apply-1-plan.process-metrics.json), Git [B 준비](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b-initial-r18-git.process-metrics.json)·[canonical 명령](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/ensure-b.process-metrics.json)·[inspection 기록](C:/Users/olive/AppData/Local/Temp/sscal1g/h/records/b87030261d7b7141d5bc66f2/source-inspections.jsonl)·[복구](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-recovered-continuation-metrics.json), Plastic [A 준비](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/ensure-a-same-request-resumed-r19.process-metrics.json)·[지연 인계](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/slow-phase-handoff-r19.json). 로컬 원시 파일은 이 host에 한정하며, 저장소의 기계 보고서에 정확한 경로를 보존했다.

## 긴 수집 시간이 발생한 구간

**None, OBS-N2**, +09:00:

| 기존 경계 | 시각 |
| --- | --- |
| CLI process 시작 | 13:26:39.6417783 |
| 고정 수집 제어 파일 | 13:26:40.4788448 |
| Generation basis header | 13:27:46.6939741 |
| 보존 range/adoption map, 99,990행 | 13:56:36.4086447 |
| Generation header | 13:57:01.4549226 |
| Workspace 결과 상태 | 13:57:57.7433953 |
| Source materialized marker | 14:30:06.1286749 |
| 응답 파일 | 14:31:09.1516585 |

별도로 측정한 store mutation 정지는 0.0833245초, callback 정지는 0.0862686초이며 서로 겹친다. 한 시간가량의 수집 비용은 이 짧은 freeze 밖에 있다. Workspace 결과 상태부터 materialization까지 약1,928초가 관측되지만 그 전체가 flush 시간은 아니다. [None 인계](../../../artifacts/scalable-storage-tests/native-none-slowpoint-handoff-20261101.md)·[materialization 관측](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/capture-1-source-materialized-observation.json)을 참조한다.

**Git, OBS-G3**, UTC:

| 기존 경계 | 시각 |
| --- | --- |
| 복구 CLI 시작 | 04:25:29.8438305Z |
| Generation basis header | 05:04:34.2288105Z |
| 소유 result-sort 출력이 사라진 것을 관측 | 06:08:09.2391176Z |
| Result / generation receipt | 06:24:28.1574743Z / 06:24:28.4375879Z |
| 복구 응답 | 06:25:51.078383Z |
| Wrapper가 고정 바이트·재시도·후속 편집 확인 | 06:26:30.6125413Z |

Sort 관측은 계측된 정렬 종료 경계가 아니다. Store/callback 정지는 각각 0.1068701 / 0.1104373초이며 역시 겹친다. 실제 CLI 유실은 통과했다. Actor를 정확히 한 번 중단하고 원래 요청/root/digest, 독립 보존한 고정 바이트, 완료 요청 재시도와 후속 편집을 확인했다. 복구 호출 전에 발생한 harness alias binding 실패는 보존했으며 actor나 후속 쓰기를 반복하지 않았다. [완료 증거](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-complete.json)·[소유 출력 진행](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/native-cli-loss-owned-artifact-progress-2.json).

## 이미 확인·수정한 결함

[통합 보고서](../../../reports/benchmarks/scalable-storage-integrated-20261101.json)의 집중 검증을 통과했다. 이 수정들이 전체 수명주기 성능 유지를 입증하지는 않는다.

| ID | 확인한 원인과 한정 수정 |
| --- | --- |
| FIX-01 | R16-cache: 삭제된 슬롯이 재사용될 때 `Dictionary.First`는 최근 사용 순서가 아니었고, 퇴출 때 전역 metadata cache를 순회했다. 명시적 16-handle LRU와 scope/container 직접 무효화로 바꿨다. 열린 디렉터리 cursor가 있으면 비싼 live GC 복사/replay 전에 유예하며 최종 reader guard와 같은 intent는 유지한다. |
| FIX-02 | R19 Plastic: 실제 R16 managed stack에서 서버 heartbeat가 전체 manifest/catalog 검증에 있었다. 이미 전체 승인한 불변 reader가 정확한 Native identity/경로/길이·쓰기/삭제 fence를 유지하고, scoped heartbeat는 물리 binding을 확인한다. 일반 observation과 전체 `VerifyReadBoundary`는 유지한다. |
| FIX-03 | R18 Git: 입력 규모에 따라 달라지는 clean status 순회는 30초 넘게 출력이 없을 수 있다. Record stream에 고정 객체용 blocked-read timer가 잘못 적용돼 있었다. 해당 record timer만 제거했고 취소·소유 child/stderr drain·record 검증·고정 객체 reader deadline은 유지한다. |
| FIX-04 | R19 GC 복구: 시작 시 segment 회수가 미완료 live repack보다 먼저 segment catalog를 변경했다. 같은 미완료 repack을 먼저 복구하도록 바꿨다. 다른 모든 root/counter/ACK seal이 일치할 때 증명된 빈 normal header 부재만 조정하며 내용 있는 항목 삭제나 다른 proof 불일치는 거부한다. 실제 killed-child와 같은 native 요청 복구가 통과했다. |

FIX-02의 같은 기존 100k snapshot 측정은 전체 observation 5.5830412초, 전체 scope 승인 5.4950772초, **scoped observation 100회 합계 0.9958472초**, 전체 boundary 4.6148969초, 이후 전체 observation 5.0921995초였다. 전체 manifest 검증 수는 0 → 1 → 2 → 2 → 3 → 4다. Fence 8개가 **논리 metadata 21,390,437바이트**를 고정했고 Dispose 뒤 fence handle은 0이었다. [컴포넌트 측정](../../../artifacts/scalable-storage-tests/native-plastic/r19-same-snapshot-scope-component.json) 1회이며 실제 할당·서버 처리량·분리한 RAM·전체 수명주기 개선율이 아니다.

## 코드와 기존 카운터에서 확인한 남은 비용

| ID | 근거 수준과 남은 비용 |
| --- | --- |
| COST-01 | **코드 확인:** 첫 `FileWorkspaceProvider.PreserveGenerationAsync`는 header가 없으면 basis를 순회하고 고정 결과 전체도 순회한다. `ScanTree`는 스트리밍으로 모든 일반 파일의 hash를 계산한다. 이후 고정 basis header는 최초 basis 재생성을 피하지만 전체 결과 열거/hash는 남는다. Git artifact 구간은 해당 경로의 위치이며 CPU/I/O별 시간 배분이 아니다. |
| COST-02 | **코드 확인·시간 귀속 추정:** `FileInspectionProof.RevalidateAsync`는 Editor 없는 응답을 위해 workspace metadata 전체를 다시 순회하고 디스크 인덱스 root를 비교한다. Plastic READY→응답의 주요 부분일 가능성이 있지만 배타 시간으로 측정하지 않았다. Git도 payload 0인 증분 inspection이 760.2953989초여서 payload 이외 비용이 상당했다. |
| COST-03 | **코드 확인·정확한 계획 시간 귀속 미확정:** 원본 계획은 보존 generation 검증, basis/result/selection 인덱스 생성, source 보호 획득, 원본 namespace 관측과 선택 target 버전 읽기를 수행한다. None 계획은 계획 게시 전에 중단했으므로 관측 시간은 완료 시간의 하한이며 파일 게시 실패가 아니다. |
| COST-04 | **카운터·코드 확인:** range adoption은 고정 바이트와 canonical 내용을 대조하고 일치하는 live range를 내구성 있게 확정한다. None은 `adopt-range` 99,990 commit, 누적 operation 361.542초·flush 282.786초를 기록했다. 이 카운터로 약1,928초의 backend 관측 구간 전체를 설명할 수 없다. |
| COST-05 | **정적 후보만:** paged Git inspection은 256파일 summary batch를 사용한다. 재시작 뒤 optional initial-content proof가 없더라도 null summary가 후속 batch 요청을 멈추지는 않는다. 따라서 전체 99,990파일 순회에서 fallback 읽기 전/중에 summary RPC 약391회가 발생할 수 있다. 실제 실행의 횟수·시간은 측정하지 않았고 capability/음성 결과 최적화도 구현하지 않았다. |

코드: [generation 순회/보존](../../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs)·[응답 proof](../../../src/Splinter.SourceControl.Files/FileInspectionProof.cs)·[원본 계획](../../../src/Splinter.SourceControl.Files/FileOriginalApply.cs)·[range adoption](../../../src/Splinter.Runtime/JournalRangeAdoption.cs)·[paged Git inspection](../../../src/Splinter.SourceControl.Git/GitPagedSourceFingerprint.cs)·[optional summary](../../../src/Splinter.Runtime/JournalInitialContent.cs).

None의 영속 카운터에는 write·resize·metadata 각각 99,990 commit, packing-state 91,627회, range relocation 91,571회, 완료 packing 7건, 전체 583,167 commit·573,403 flush도 있다. Operation/flush 카운터는 누적이며 다른 작업과 겹친다. 배타적인 전체 시간 분해나 자동으로 불필요한 작업 수로 읽지 않는다.

32MiB page-cache peak는 전체 metadata 메모리가 아니다. None capture의 표본 최대는 CLI private 156,463,104바이트, 추적 process 합계 private 508,272,640바이트·handle 401,986개이고 `samplingErrors=[]`다. 100ms 표본의 하한이며 추적한 process group을 포함한다. Native 소유/전체 target 보호 비용은 cache 예산 밖에서 파일 수에 비례할 수 있다.

None materialization 뒤 source-private payload는 0, journal은 671바이트였고 불변 결과는 계속 보존됐다. 이는 해당 source의 채택/저장 정리 증거이며 home 전체 할당이 0이라는 뜻이 아니다. 사용자의 검증 축소 뒤 새 post-bulk 할당 순회는 실행하지 않았다.

## 저장 수명의 별도 비용

관리형 None 준비는 payload를 복사하지 않고 원래 basis를 이동하고 인덱스를 만든다. 원본은 나중에 다시 수정 가능한 일반 NTFS가 되어야 하므로 독립 불변 결과 저장소를 대신할 수 없다. 최초 독립 보존 때 고유 basis 버전마다 canonical CAS로 복사될 수 있고, A/B와 이후 결과는 그 객체를 공유한다. Basis/CAS 공존은 수명을 분리하기 위한 추가 비용이며 중복이 모두 제거됐거나 그 바이트 전부가 필수라는 증거가 아니다.

기존 basis Native identity와 새 CAS identity·metadata/제어 파일을 구분해 계산해야 한다. 최초 고유 payload가 512바이트인 이 입력으로 대부분이 고유한 대형 에셋의 보존 비용을 검증할 수 없고, 새 편집 버전은 불변 바이트를 추가할 수 있다. 논리 payload 크기로 단계별 할당이나 절감률을 추정하지 않는다.

## 우선순위별 후속 질문

구현하지 않은 질문이며 **새 실행 승인이 아니다**.

1. **Q-01:** 같은 reader lease·generation proof 아래 고정 basis/result metadata와 검증한 canonical identity를 재사용해 중복 순회/hash를 줄이면서 전체 게시 boundary 검증을 유지할 수 있는가?
2. **Q-02:** 순서가 보장된 제한 단위 adoption/packing batch로 행별 metadata/flush 비용을 줄이면서 정확한 내구 복구와 접수된 쓰기의 ACK 의미를 유지할 수 있는가?
3. **Q-03:** 원본 전체 관측/인덱스 생성 중 어떤 순회가 반복되며, 전체 target/조상 잠금·조건부 게시·동반 파일/소유 검사를 유지한 채 스트리밍 비교와 독립 I/O 제한 병렬화로 반복을 줄일 수 있는가?
4. **Q-04:** 재시작 뒤 optional initial-summary를 batch마다 묻는 대신 정확한 provider 수명에 결합해 한 번 불가를 확인할 수 있는가? 없는 proof를 검증된 내용으로 취급해서는 안 된다.
5. **Q-05:** 검증한 cache 수정 뒤에도 metadata-only 작업을 지배하는 active-container proof/page 연산은 무엇인가? 향후 작은 표적 진단도 Native 교체 검출·불변 reader fence·전체 hash proof를 유지해야 하며 cache만 보고 가용성을 인정하는 우회는 정당화되지 않는다.

## 사용자가 요청한 중단 지점

비싼 읽기 전용 CLI 2개(None 계획·Plastic B ensure)만 정확한 PID/start로 종료했다. 소유 provider 7개(Git A/B, None A/B/원본, Plastic A/B)는 각각 저장된 인증 요청으로 정상 Stop을 한 번 보냈고 모두 process/mount 부재를 보고했다. Task·basis·private delta·불변 결과·요청 identity·소유 증거는 보존했다. 정상 Stop은 release·discard·관리형 원본 복원이 아니다.

Git은 통과한 CLI 유실 결과와 후속 편집을 보존했고 100k bulk 본문을 시작하지 않았다. None은 capture 1개·원본 apply 0개를 보존했다. 빈 `apply-1.json`은 명시적 harness barrier이며 CLI 응답이 아니다. Plastic A 준비는 성공했고 B는 승인 node 427개·source ACK sequence 0·결과 없음에서 중단했다.

이후 새 100k 단계·선택/게시/query 행렬·실제 17GiB 시험·대응 ABBA·할당 전체 순회·최신 소스 전체 build gate는 실행하지 않았다. 작은 실제 Unity 세 입력의 수명주기·별도 정리 완료는 별도 근거이며 이 대형 입력/성능 gate를 대신하지 않는다.

정리 증거: [Git](C:/Users/olive/AppData/Local/Temp/sscal1g/evidence/minimal-closeout-normal-stop-r19.json)·[None](C:/Users/olive/AppData/Local/Temp/sscal1n/evidence/user-closeout-stopped-preserved.json)·[Plastic](C:/Users/olive/AppData/Local/Temp/sscal1p/evidence/minimum-closeout-r19.json). 이 문서는 남은 실험이나 fixture 삭제를 자동 승인하지 않는다.

