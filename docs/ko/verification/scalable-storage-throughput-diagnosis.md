# 대규모 저장 처리량 진단

후속 사용자가 승인한 구현과 제한 검증은 [처리량 후속](scalable-storage-throughput-followup.md)에서 추적한다. 아래 기존 관측은 당시 증거로 유지한다.

[English](../../verification/scalable-storage-throughput-diagnosis.md) · [SCAL 검증](scalable-storage.md) · [기계 판독 보고서](../../../reports/benchmarks/scalable-storage-slowpoints-20261101.json)

실제 100,000항목의 작은 파일 입력에서 **준비·쓰기·수집·계획에 개발 중 사용하기 어려울 만큼 긴 시간**이 걸렸다. 총량 상한 제거만으로 실용적인 처리량이 확보된 것은 아니다. 이 실행에는 복구 이력·동시 host 부하·단계별 혼합 고정 바이너리가 포함되므로, 대응 전후 벤치마크나 일반 대규모 프로젝트의 예상 시간으로 읽지 않는다.

상태: **처리량 문제 미해결; 사용자 요청으로 검증 축소.** 기존 증거와 코드 확인만 정리했다. 새 제품 변경·프로파일·실험이나 검증 재개 승인을 추가하지 않는다. SCAL-03·SCAL-07·SCAL-08은 [소유 원장](scalable-storage.md)에서 미완료로 남는다.

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

