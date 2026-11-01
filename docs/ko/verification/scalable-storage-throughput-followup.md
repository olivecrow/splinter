# 대규모 저장 처리량 후속

[English](../../verification/scalable-storage-throughput-followup.md) · [기존 진단](scalable-storage-throughput-diagnosis.md)

상태: ST-01–23 구현·작은 정확성 검증·시간을 제한한 실제 성능 시도 완료. 후속 사용자는 모든 리뷰 수정·항목별 작은 검증·시간을 제한한 마지막 성능 시도를 승인했다. 필수 전체 시험 gate는 취소했으며 **통과가 아니다**. 보존한 100k fixture를 재개하지 않으며 SCAL-03/07/08은 미자격이다.

이후 확인된 구조적 원인을 수정하라는 요청은 **아래 ST-24–28**이 소유하며, 작은 국지 검증으로 한정한다. 앞선 시간 비교 행렬이나 전체 gate를 재개하지 않는다.

이후 공격적 최적화와 수요에 따른 약 4GB 메모리 사용 승인은 **ST-29–35**가 소유한다. 이전 캐시/worker 정책을 대체하며 무결성 요구나 대형 검증 중단을 해제하지 않는다.

남은 후보를 진행하라는 후속 요청은 아래 **ST-36–40**으로 구현했으며 검증은 작은 국지 검사로 한정한다.

이후 동일 소규모 입력의 실제 시간 비교·느린 구간 수정 요청은 **ST-41–43**이 소유한다. 첫 ABBA는 `3f7d5b1`/`5dce6e0`, 둘째 ABBA는 `5dce6e0`/이번 수정 후보이며 두 블록을 하나의 교차 비교로 섞지 않는다.


## 변경

| ID | 변경과 유지한 경계 |
| --- | --- |
| ST-01 | Adoption 최대 64개 frame이 영속 batch를 공유한다. 고정/canonical 비교·live range 일치·최종 root/seal을 유지한다. |
| ST-02 | Packing relocation 63개와 해당 progress frame을 묶고 같은 intent로 부분 수행을 복구한다. |
| ST-03 | 고정 capture의 변경 없는 basis hash 또는 검증된 content summary를 재사용한다. 변경 바이트·backing proof·전체 boundary 검증은 유지한다. |
| ST-04 | Spool cursor가 64KiB 읽기 버퍼를 사용한다. |
| ST-05 | Spool은 64KiB 뒤 spill하고 영속 flush는 파일을 강제한다. Cursor마다 길이를 고정한다. |
| ST-06 | None 계획의 원본 namespace 관측에 apply 자원 검사를 합쳐 중복 scan을 없애며 root/VCS 승인은 유지한다. |
| ST-07 | 일회성 editorless proof는 정렬된 canonical metadata digest를 비교하며 전체 열거·generation 검사를 유지한다. |
| ST-08 | Git attribute batch는 경로 4,096개·UTF-8 256KiB 모두로 제한한다. Interpretation 검증은 유지한다. |
| ST-09 | Optional initial-summary 불가는 해당 inspection의 후속 요청을 멈춘다. 유효한 빈 응답은 불가가 아니다. |
| ST-10 | 동시 resize/metadata callback이 제한된 영속 batch를 공유한다. Flush 뒤 성공, 불확정 writer 차단, capture/flush의 두 queue drain을 유지한다. |
| ST-11 | Raw 보존은 native backing별로 묶어 8-handle cache 퇴출에 따른 전체 proof 반복을 줄인다. |
| ST-12 | 정확한 adoption 작업 안에서 canonical reader를 재사용하고 종료 시 해제한다. 독립 작업은 proof를 승계하지 않는다. |
| ST-13 | 기존 payload index로 전체 파일의 반복 payload 정렬을 대체하며 catalog 전체 포함·index membership을 검사한다. |
| ST-14 | 원본 계획의 before/after를 정렬 병합하고 직접 선택 집합을 일괄 생성한다. |
| ST-15 | 각 record·누락·membership·index·interpretation 일치 뒤에만 변경 없는 Git catalog를 유지한다. |
| ST-16 | Adoption의 proof chunk·영속 batch 사이에서 취소를 확인하고 고정 요청·영속 prefix를 보존한다. |
| ST-17 | 인증된 Stop은 maintenance/adoption을 취소하고 drain한 뒤 종료한다. 다른 token은 취소 권한이 없다. |
| ST-18 | Git 경로는 정렬 spool 하나를 반복·부분 cursor로 읽어 반복 정렬과 부분 정렬 잔여를 없앤다. |
| ST-19 | Raw 객체/참조 최대 64개가 게시를 공유한다. Batch의 native source fence와 link/결과 root 이전의 backing pin intent를 유지한다. |
| ST-20 | Payload hash는 제한된 1MiB 읽기 전후 취소를 확인하고 token을 합성 reader까지 전달한다. |
| ST-21 | 앱 closure/companion을 일괄 생성하며 동일 항목은 합치고 다른 철자 충돌은 거부한다. |
| ST-22 | 공유 catalog 잠금 안에서 canonical native fence를 확보하고 바이트는 밖에서 검증한다. 쓰기/삭제 차단·전체 소유/내용 proof는 유지한다. |
| ST-23 | 제한된 hash 읽기로 Git 가용성/경로 호출을 줄이며 검사 자체나 전체 read boundary는 생략하지 않는다. EOF의 불필요한 tree 읽기도 없앤다. |

Task/basis/overlay·wire 버전은 바꾸지 않았다. 새 helper는 `PayloadHash`·`FileRawRangePreparation`·`WinFspMetadataBatch`와 Core의 `IWorkspaceMetadataBatch` 경계다. Core에 OS 전용 API를 넣지 않았다. 제한된 worker마다 1MiB hash 버퍼와 raw batch당 최대 64개 source handle은 작업 단위의 제한이며 입력 총량 상한이나 전체 process 메모리 수치가 아니다.

## 증거와 중단 기록

기준 소스는 `artifacts/scalable-storage-throughput-20261101/before-source/`이고 `before-source.json`에 소스/설정 hash 616개를 기록했다. 같은 소스의 빌드를 위해 Unity 소스와 NuGet 설정을 별도로 복사했다. 기존 사용자 변경·결과·intent·중단 fixture는 보존한다.

집중 native 검증: 최초 21개, 후속 9개 통과. 저장/소유/Stop/정렬 **32개는 5.532초**, metadata/adoption/raw basis/content **48개는 16.494초**, 반복 원본 반영·응답 유실 복구·정확한 Git reader fence·paged inspection **17개는 55.445초**에 통과했다. 느렸던 2MiB Git generation 사례는 ST-20/23 뒤 **18.556초에 통과**했다. 마지막 telemetry 수정은 **1.166초에 11개 통과·선택적 비용시험 skip 1개**다. 검증 묶음은 중복되므로 합계를 고유 전체 시험 수로 읽지 않는다. 이전 중단 시도와 과거 전체 suite의 464초는 대응 개선율 측정이 아니다.

실행 문제도 보존했다. 초기 native 시험은 sandbox 기능 제한이 있었고 한 필터 조합은 시험 0개를 선택했다. 시험 두 곳은 실제 원자적 제어 파일 writer와 native 읽기 공유 검사로 고쳤다. 겹친 빌드/시험은 DLL 잠금에 걸려 이후 순차 실행했다. 지연 Git 시험은 중단하고 정확한 process 부재를 확인한 뒤 빌드했다. 제품 timeout 확대나 무결성 검사 생략으로 통과시키지 않았다.

첫 실제 pilot은 새 **128항목 Git**이었다. A 준비 **27.335초**, B는 고정한 명령별 **30초**에 도달해 `Cancelled`를 반환했다. 정확한 provider 둘에 정상 Stop을 한 번씩 보내 process/mount 부재를 확인했다. `C:/Users/olive/AppData/Local/Temp/sctp-g-b1`을 보존하며 후속 단계는 실행하지 않았다. 이후 전후 입력을 모두 **32항목**으로 줄이고 명령 한도를 유지했다.

## 실제 시간 비교

새 원본/A/B fixture는 파일 31개·디렉터리 1개, 파일당 32바이트(논리 합계 992바이트)이며 각 쌍의 고정 basis가 일치한다. 두 후보에 같은 취소 wrapper를 사용했다. 비교별 전후 각 1회이며 ABBA·cold cache·대형 입력 증거가 아니다.

| 범위 | 이전 | 이후 | 관측 절감 |
| --- | ---: | ---: | ---: |
| Git 공통 start/bulk/selected 단계, final 미실행 | 179.249초 | 168.960초 | 10.289초 / 5.740% |
| None 전체 수명주기, 최종 제품 소스 | 108.520초 | 87.694초 | 20.825초 / 19.190% |

None은 반복 수집·전체/선택 원본 반영·rename/delete/create·원본만의 편집 보존·A 회수 뒤 B 지속·두 작업 회수·관리 원본의 일반 NTFS 복원까지 포함한다. 앞선 이후 후보는 **104.964초**로 같은 기준보다 **3.555초 / 3.276%**만 짧았다. 마지막 변경은 선택적 telemetry 복구이며 추가 절감의 원인이라고 입증하지 않았다. 실행 순서/cache/환경 편차는 미확정이다. Git 시간은 마지막 실패 경로 poison guard·telemetry 수정 전 후보라 최종 바이너리나 전체 수명주기의 자격이 아니다. 32항목 bulk harness는 writer 1개로 실행했으며 실제 8-worker 처리량은 측정하지 않았다.

Git trial driver는 처음에 **140초** 예산을 단계 진입 전에만 확인했다. 진행 중 단계가 179.249초/168.960초까지 drain된 뒤 final을 실행하지 않고 정확한 provider를 정상 종료했다. 이 예산 초과를 harness 결함으로 기록한다. 이후 None은 절대 deadline을 각 CLI 호출까지 전달한다. 첫 None 기준 시도 하나는 A release 성공 뒤 관측 reader에서 실패했다. 남은 B/관리 원본 provider를 정상 Stop 한 번씩으로 종료하고 자료/intent를 보존했다. 관측 reader는 제한된 I/O 재시도와 원자적 교체를 허용하는 공유 모드로 수정했다. 실패 시도는 완료 비교에서 제외했다.

## 최종 gate와 남은 범위

`pwsh ./scripts/build.ps1`의 locked restore·전체 Release 빌드는 경고/오류 0개로 끝났다. 시험 단계는 취소했으며 마지막 진행 집계는 **1,911 통과 / 1 실패 / 27 skip**으로 완료 결과가 아니다. 에이전트 문맥 압축 중 정한 **180초** 중단 시점을 놓쳤고 로그 구간은 **423.599초**(10:13:20.747–10:20:24.347 UTC)다. 실행을 재개한 즉시 Ctrl+C를 보냈고 명령 종료·관련 빌드/시험 프로세스 부재를 읽기 전용으로 확인했다. 강제 kill이나 원본/결과/intent 삭제는 없었다. 놓친 제한은 검증 실행 관리의 실패이며 정상 예상 시간이나 전체 gate 통과로 읽지 않는다.

관측된 실패 1건은 선택적 mutation telemetry 회귀였다. 새 metadata queue가 `set-basic-info`/`set-file-size` 집계를 빠뜨렸다. Callback마다 대기와 공유 영속 batch 구간을 실패 시에도 기록하도록 고쳤고 영속 완료 경계는 유지했다. 전체 solution 재빌드는 경고/오류 0개로 **24.254초**에 끝났고 위 **11 통과/1 skip** 집중 재검증으로 수정 사항을 확인했다. 전체 suite는 재시작하지 않았다. 마지막 None 시간은 이 재빌드 후보를 사용했다.

과거 전체 suite는 **34분 26초**, 복원 한 사례는 **19분 13초**였고 백만 항목 시험도 포함했다. 비정상 비용을 정상 개발 예상으로 삼지 않는다. 최종 검증에도 짧은 명시적 중단 경계를 유지하며 중단한 gate는 전체 통과가 아니다.

[기계 판독 보고서](../../../reports/benchmarks/scalable-storage-throughput-followup-20261101.json)는 모든 시도·실패·명령 시간·Stop 영수증·집중 검증·gate 상태를 보존한다. 기준 소스/설정 **616/616**개 hash가 일치했고 최종 소스 manifest는 **626**개다. 의존성·형식·CLI schema는 추가하지 않았다. SCAL-03/07/08·17GiB/100k·실제 Plastic/Unity·OS 장애·전체 suite 자격은 미완료이며 큰 전체 속도 개선을 입증하지 않았다.

`scripts/verification/ScalabilityTimedCommand.cs`는 선택한 CLI의 기존 취소 가능 진입점을 호출한다. `scalable-storage-throughput-trial.ps1`는 기존 실제 editorless 수명주기 harness를 사용하고 실패 시 소유 provider만 정상 종료하며 모든 요청/자료를 보존한다. 쓰기 process를 강제 종료하지 않는다. 실제 Plastic·Unity·100k·17GiB 성능 자격은 별도다.

## ST-24–28: 구조적 후속, 2026-11-01

`01a188aa-d3aa-7221-b3cf-acb7796a7579` 대화와의 진단에서 보존한 None 입력은 파일 99,990개·payload 3,199,680바이트·내용 16종임을 확인했다. 과거 쓰기/수집/계획의 49–66분 구간은 대용량 복사 벤치마크가 아니다. FIX-01 이전 캐시 퇴출/회수 사고는 이미 과거 수정이며, 이번에는 현재 소스에 남은 반복 처리를 수정했다.

| ID | 구현과 유지한 경계 |
| --- | --- |
| ST-24 | 전체 객체 참조 묶음은 catalog 진입을 공유한다. `ExistingReferenceScope`는 고정 capture의 workspace 작업 안에서 canonical/alias native fence 쌍을 최대 64개 유지하며 성공한 소유권/내용 증명만 재사용한다. 다음 작업에서는 다시 검증한다. `FileRawRangePreparation`이 이 scope를 사용하며 채택 reader도 기존 작업 동안에만 최대 64개를 유지하고 해제한다. 유실/외부/손상 참조·길이 불일치·진행 중 복구 검사는 유지한다. |
| ST-25 | 이미 준비된 raw 범위 묶음은 catalog reader 하나와 서로 다른 hash별 native 증명 하나를 최대 64개 공유한다. source 증명·정확한 구성원/요청 검사는 유지하며 재시도가 ready catalog를 다시 쓰지 않는다. |
| ST-26 | Runtime의 새 `PagedMetadataBatch.cs`는 기존 키들의 탐색 경로를 합쳐 변경된 공통 상위 페이지를 한 번만 쓴다. 저널 묶음은 영속 완료 전에 같은 node의 반복 갱신을 합치며 조회는 미반영 값을 읽고 실패 시 writer를 차단한다. 명시적인 root 반환은 지연된 페이지를 flush한 뒤 수행한다. 최대 64항목·메모리 내 하강 128단계로 제한하고 삽입/더 깊은 경로는 기존 disk-spine 구현을 사용한다. 영속 root·페이지 형식·과거 reader·재생은 유지한다. |
| ST-27 | `PagedPathInterner.Import`는 정렬한 구성요소와 기존 트리를 병합하고 충돌을 확인하며 새 이름이 없으면 재구축을 생략한다. 원본 계획/선택의 반복 import가 이전의 node별 `Build(previousRoot)` 탐색을 반복하지 않는다. |
| ST-28 | Git의 이전 파일 catalog를 한 번 외부 정렬하여 현재 정렬 파일 목록과 병합한다. 파일별 catalog 탐색과 별도의 누락 개수 순회를 제거한다. catalog 구성원 검증과 cache record 검증도 같은 순회에서 수행한다. 전체 index/구성원/해석 검사·전후 source generation·native 경로 검사·게시 증명은 유지한다. |

작은 비용 검사: **256 node / 64갱신: 페이지 쓰기 573 → 164회**로 71.4% 줄었다. 제품 저널의 동일한 **32파일 / 64변경: 추가 metadata 223,302 → 138,332바이트**로 38.1% 줄었다. 이는 호출/바이트 수이며 경과 시간 개선율이 아니다. 고정 capture scope는 **16종 내용의 576참조를 native 내용 증명 16회**로 처리했고 새 scope/작업에서는 다시 검증했다. 64행/16범위 ready raw 묶음의 유지 증명은 16회다. 64경로를 세 번 동일하게 import해도 최초 구축 1회를 유지했고 새 경로가 들어오면 다시 구축했다.

최종 변경부 검사: **41통과·0실패·0skip, 7.453초**. 선택한 Git 증분/원본 게시/채택 취소 검사: **4통과, 36.427초**. Git 입력은 작은 파일 520개와 1MiB 파일이며 나머지 새 검사는 metadata 4–256행·내용 16종 또는 저널 파일 32개다. 찢어진 페이지 tail·미응답 배치 실패 후 writer 차단·같은 요청 복구·손상/외부 native 소유권·과거 root·native 핸들 해제·다음 작업의 재검증을 확인했다. 이 45개는 저장소 전체 gate가 아니다.

실행 예산: 전체 40분(읽기 5·구현 25·국지 검증/문서 10), 예상의 2배 중단 기준, 개별 검사는 30–60초로 제한했다. 최초 빌드는 새 timeout 테스트의 취소 토큰 검사 누락만으로 44.12초 뒤 실패했고 보완 후 focused 빌드는 9.48초에 통과했다. 이후 solution 빌드들은 통과했으며 최종 검사 직전 빌드는 28.89초·경고/오류 0개다. 이번 후속에서 장시간 실행을 취소할 단계는 발생하지 않았다. 과거 전체 suite/대형 입력 실패 기록은 위에 유지하며, 사용자의 작은 검증 지시에 따라 `build.ps1`의 전체 suite 단계는 재실행하지 않았다.

마지막 형식 정리 후 solution 재빌드는 **9.27초**, 경고/오류 0개로 통과했으며 집중 검사 이후 동작 변경은 없었다.

근거: [구조적 후속 보고서](../../../reports/benchmarks/scalable-storage-structural-20261101.json)와 `artifacts/scalable-storage-structural-20261101/`에 선택한 시작 소스·초기 working tree 상태·최종 소스 hash·CTRF를 보존했다. task v4·basis v3·overlay v3·wire 계약과 프로세스 공통 metadata 캐시 32MiB는 유지한다. node당 키 하나인 형식과 개별 최초 진입은 여전히 있으며 조밀한 페이지 형식으로 전면 교체했다고 주장하지 않는다. 100k/17GiB fixture·실제 Plastic/Unity 수명주기·전체 gate·대형 전체 시간 비교는 실행하지 않았다. 따라서 과거 수십 분~수 시간 현상을 해소했는지는 **아직 입증하지 않았다**.

## ST-29–35: 자원과 동시성 후속

이전 미커밋 SCAL 기반과 ST-01–28은 의존 관계에 따라 함께 `1c748c9`로 커밋했다. 이 절은 그 뒤 추가 구현을 기록한다. 앞 절의 32MiB/8-worker 수치는 각 이전 후보의 정책이다.

| ID | 구현한 변경과 유지한 보호 |
| --- | --- |
| ST-29 | 새 Runtime `StorageResources`는 가용 메모리의 1/8을 128MiB–4GiB로 제한한 프로세스별 작업 목표를 사용한다(가용량 미상: 512MiB). 5/8은 metadata 캐시, 1/8은 공통 scratch다. 관측한 호스트는 논리 CPU 12개·가용량 68,628,553,728바이트이며 목표 4GiB·캐시 2.5GiB·scratch 512MiB·저장소별 reader 핸들 48개를 선택했다. 사전 확보하지 않으며 native 캐시 허용·identity·checksum·reader fence는 유지한다. |
| ST-30 | Sort run 최대 128MiB·spool 최대 64MiB가 공통 scratch를 계상하고 merge fan-in은 8→32로 늘린다. 할당 부족 시 기다리지 않고 spill한다. 중첩 sort는 공통 scratch 밖에 64KiB fallback run(또는 더 큰 유효 record 1개)을 유지해 tiny record마다 파일을 만드는 증폭을 피한다. Durable spool flush는 실제 파일 저장을 강제한다. |
| ST-31 | 제한된 임시 treap이 삽입과 갱신을 함께 처리하고 최종 생존한 dirty node만 한 번 쓴다. 저널 배치에 변경 경로 색인도 포함한다. 최대 64행·탐색 128단계로 제한하고 더 깊으면 배치 쓰기 전에 기존 disk-spine 경로로 전환한다. 과거 root·pending 값 조회·불확정 writer 차단·영속 root/ACK 게시를 유지한다. |
| ST-32 | 새 `BoundedParallel`이 제한된 완료 queue로 열거·hash·소비를 겹쳐 실행한다. 느린 파일 하나가 전체 묶음의 완료를 지연시키지 않는다. Hash worker는 CPU 수(1–32), 독립 I/O는 CPU 수의 두 배(8–32)이며 이 호스트는 12/24개다. Plastic 고정 revision reader는 직렬이다. 오류·조기 Dispose는 접수 작업을 취소·join하고 원래 실패 원인을 유지한다. |
| ST-33 | 기존 최대 단일 파일만큼의 staging 예약은 worker가 여럿이어도 같은 크기의 파일들을 직렬화했다. 추가 병렬 staging은 worker 수·작업 목표의 1/4·관측 디스크 여유의 1/4로 제한하며 기존 복구용 최대 payload 1개는 허용한다. 전체 대상 native 잠금·파일별 여유 검사·단일 영속 진행 확정·topology/동반 파일 순서·실패 drain은 유지한다. |
| ST-34 | 디렉터리 열거가 이미 받은 불변 metadata를 보존해 자식마다 다시 `Stat`하지 않는다. 정확한 경로·가용성·overlay 우선순위·제외/hidden 경로·과거 root 의미는 유지한다. |
| ST-35 | 일반 파일의 변경 경로 검사는 전후 목록 연결·행마다 두 번의 tree 조회·개별 COW 삽입을 정렬 catalog 병합과 변경 집합 일괄 생성으로 바꾼다. 대소문자 표기·추가/삭제/metadata 변경·취소 검사를 유지한다. |

메모리 수치는 정책 예산이며 **실제 RSS나 여러 provider 합산 4GiB 보장이 아니다**. Merge 버퍼·fallback run·보존 cursor·payload/decoder 버퍼·native 핸들·GC에는 별도 여유가 필요하다. 실제 대형 프로젝트 최대 메모리와 하드웨어 활용률은 측정하지 않았다. task v4·basis v3·overlay v3·CLI schema·capabilities는 유지하며 새 공개 명령이나 의존성은 없다.

작은 비용 근거: 새 키 64개의 페이지 쓰기는 **342→64회**, 같은 제품 **32파일/64회 변경의 추가 metadata는 직렬 223,302→배치 57,988바이트**(74.0% 감소)다. 앞선 ST-26 배치의 138,332바이트보다 이 입력에서 58.1% 더 적다. 6,000행/값 1KiB 정렬을 run 예산 4MiB와 32MiB로 비교하면 **마지막 행 직전 run 파일이 2→0개**이고 결과는 같다. 32파일 목록의 추가 `Stat` 호출은 **0회**다. Native 게시 검사는 전체 대상 고정 뒤 stage 두 개 이상이 실제로 겹쳐 준비되도록 요구한다. 작업량·바이트·동시성 근거이며 경과 시간 개선율이 아니다.

검증: 최종 자원/구조/배치/spool **30개가 1.283초**, 선택한 Git 검사/원본 게시 **5개가 45.924초**, 파일 검사 증명/fake Plastic **14개가 8.220초**에 통과했다. 같은 후속의 앞선 native cache/reader/content/range **31개는 6.423초**, 게시/채택 **4개는 7.850초**에 통과했다. 이 실행들의 합집합은 **고유 81개, 실패/skip 0개**이며 겹치는 실행을 고유 검사 수에 더하지 않는다. Release solution 빌드는 **10.52초, 경고/오류 0개**로 통과했다. Git 입력은 작은 파일 520개와 1MiB 파일이며 보존한 100k fixture가 아니다.

시간 계획은 전체 55분(커밋 5·검토 10·구현 30·국지 검증/문서 10), 단계별 예상 2배와 개별 검사 30–60초 중단 기준이다. 새 검사는 취소 token을 사용하는 10–15초 timeout을 둔다. 초기 빌드에서 scratch 예산 소유 field 누락(18.11초)과 inspection token 전달 parameter 누락(6.21초)을 발견해 최종 검사 전에 수정했다. 확대한 정렬의 메모리 반환/디스크 기록 경로에 record별 취소 확인도 추가했다. 기존 spool 검사는 작은 64KiB 예산을 명시해 spill/cursor 보호를 검증하며 대형 입력 검사의 예산 assertion만 정책에 맞췄고 그 입력은 실행하지 않았다. 쓰기 중인 작업/provider를 중단할 필요는 없었다. 사용자의 작은 검증 지시에 따라 `build.ps1` 전체 시험 gate를 재개하지 않았으며 대형 fixture·실제 Plastic/Unity 수명주기·새 provider를 실행하지 않았다.

추가 검토에서 큰 후보 세 가지가 남았다. `WindowsOriginalFilePublisher.Commit`은 각 영속 진행 기록을 직렬 확정하므로 flush를 없애는 대신 장애/같은 요청 복구 경계를 정의한 묶음 확정이 필요하다. `JournalPathComponents`와 단일 키 `PagedMetadataStore.Set`은 여전히 키별 경로를 생성하고 영속 형식도 node당 키 하나여서 더 넓은 배치 또는 별도 자격 검증을 거친 조밀한 표현이 후보이다. 원본 계획과 `PagedMetadataCollection`은 보호된 inventory 전체를 순회하며 증분 생략은 전체 소스 세대·native 소유 증명이 선행되어야 한다. 실제 live-index repack 임계값은 `max(64MiB, 활성 개수 × 2048)`이며, 4MiB 후처리 maintenance 판단과 다르다. Worker만 늘려서는 이런 직렬/전체 순회 비용이 사라지지 않으며 과거 수십 분~수 시간 현상의 해소는 아직 입증하지 않았다.

근거: [자원 후속 보고서](../../../reports/benchmarks/scalable-storage-resource-20261101.json), `artifacts/scalable-storage-resource-20261101/`의 소스 manifest·CTRF. SCAL-03/07/08·실제 4GiB 압박·시스템 전체 CPU/디스크 최대 활용·대형 경과 시간 개선·전체 gate 자격은 부여하지 않는다.

## ST-36–40: 남은 직렬 처리와 순회 비용

시작 소스는 깨끗한 `development`의 `3f7d5b1`이다. 사용자가 남은 후보의 구현과 소규모 검증을 승인했다. 보존한 fixture·provider·Editor는 재시작하지 않았다.

| ID | 구현한 변경과 유지한 보호 |
| --- | --- |
| ST-36 | Windows `OriginalPublicationCommitQueue`가 대기 중인 진행 기록을 최대 64개로 묶는다. Runtime `OriginalApplyPages.SaveSteps`가 결합한 COW root의 페이지를 한 번 flush하고 제어 기록을 한 번 저장한 뒤 묶음을 ACK한다. 각 호출자는 기존 영속 경계에서 계속 기다리며 timer나 배경 writer는 없다. 저장 실패는 queue를 오염 상태로 고정하고 대기 호출 전체를 실패시키며 이후 진행 기록을 받지 않는다. 불명확한 stage 보존과 대체 nonce도 같은 직렬 그룹으로 확정한다. 원본/peer 잠금·파일별 단계 순서·같은 intent 복구·worker drain을 유지한다. |
| ST-37 | 저널 배치에 component 이름과 계층 namespace 색인을 포함한다. 현재 작업은 pending 이름을 조회하며 최대 64개 변경의 공통 조상 경로를 결합한다. 구분자가 128개 이상인 경로는 기존 반복 경로로 처리한다. Root 공개는 pending 페이지 flush를 선행하고 과거 cursor를 유지한다. WinFsp Create도 metadata queue를 사용하고 충돌은 해당 호출자에게 반환한다. 성공한 핸들은 묶음이 영속화된 뒤 mutation gate 안에서 보존하며 실패 그룹은 미확정 핸들을 보존하지 않는다. |
| ST-38 | Runtime `PagedMetadataReachability`는 공통 scratch 예산에 들어오면 정확한 작업 범위의 방문 집합을 유지해 깊이마다 누적 방문 파일을 재작성하지 않는다. 페이지당 512바이트, 현재 sort-run 예산 이하(이 호스트 128MiB)를 계상한다. 할당 실패 시 최초 frontier를 보존하고 기존 디스크 알고리즘으로 전환한다. 복사 relocation도 주소→검증한 복사 참조를 항목당 256바이트·같은 예산 아래 캐시하되 디스크 map과 fallback은 유지한다. 전체 도달 페이지/native/root 검사·복사 flush·location 게시·소유 identity 확인 후 삭제를 유지한다. |
| ST-39 | 원본 계획에서 조밀한 과거 반영 이력은 경로별 COW 대신 정렬 병합과 기준 목록 일괄 생성을 사용한다. 이력 수가 `max(64, 기준 항목 수 / 16)`보다 적으면 기존 희소 갱신을 유지한다. 삭제·대소문자 표기를 보존하고 경로 이름을 import/검증하며 과거 root는 불변이다. |
| ST-40 | 원본 namespace 전체 관측은 유지한다. 보존할 부모 집합은 행마다 전후/선택 tree를 세 번 조회하고 부모를 반복 삽입하는 대신 원본 목록과 정렬 cursor들을 함께 전진시켜 계산한다. 최종 부모 집합은 정확한 중복 확인 뒤 일괄 생성한다. 외부 자식·제외한 선택·디렉터리 삭제 충돌 검사는 유지한다. |

작은 작업량 근거이며, 현재 후보의 직렬/디스크 경로와 배치/메모리 경로를 비교한 것이지 바이너리 전후 시간 비교는 아니다.

- 진행 경계 9개의 제어 저장은 **2회**이며 저장 완료 전 호출자를 진행시키지 않는다. 두 번째 저장 실패를 주입하면 해당 그룹 전체를 ACK하지 않는다. Native Create callback 9개도 **영속 저널 flush 2회**를 공유하고 ACK 전 핸들은 없으며 마지막 context까지 해제한다.
- 공유 metadata graph 128행에서 시험 예산 1MiB일 때 **깊이별 디스크 병합 16→0회**, **relocation map 디스크 조회 699→0회**이고 검증된 메모리 hit 134회가 대신한다. 예산 0/1KiB는 디스크 fallback을 선택하며 같은 논리 root·공유 자식·빈/중복 root를 유지한다.
- 기준 파일 128개와 반영 이력 97개에서 **metadata 페이지 쓰기 704→97회**다. 이력 5개는 **35→35회**로 희소 갱신의 전체 재구축을 피한다. 이 색인 쓰기 비교는 양쪽의 경로 이름을 동일하게 미리 import한 뒤 계측했다.
- 마지막 32파일 생성·중첩 rename/delete·재생 검사는 **metadata 599,773→389,568바이트**다. 앞선 작은 실행은 562,848→385,812와 553,972→390,894였다. 매번 새 node GUID를 생성해 tree 모양/바이트 수가 달라지므로 같은 논리 동작의 비교이며 같은 task identity나 일반 경과 시간 개선율은 아니다.

최종 검증은 **고유 64개 통과, 실패/skip 0개**다. 새 12개는 **2.334초**, 회수 손상/중단/reader 20개는 **4.045초**, 원본 게시 보호 5개는 **14.452초**, 공간 오류/보존 stage 복구 5개는 **9.171초**, 저널/metadata/cleanup/고정 cursor 20개는 **2.485초**, 불명확한 stage/과거 진행 root 2개는 **2.397초**에 통과했다. 입력은 동시 callback 9개·새 파일 32개·회수 32–256행·계획 기준 파일 128개·깊이 3/129의 경로 하나다. 기존 작은 Unicode/capture/replay 검사는 구성 요소 근거다. Fake/앱 정책 검사를 실제 Plastic 서버나 Unity 수명주기 자격으로 승계하지 않는다.

첫 공간 오류 검사는 복구에 진입하기 전, staging을 12KiB 파일 하나로 제한하던 이전 assertion에서 4개 실패했다. ST-33은 이미 여러 stage를 허용한다. 현재 정책 허용량·전체 계획 payload·최대 파일 metric을 함께 검사하도록 고쳤고, 보존 stage 검사는 기존 stage 1–2개를 허용하되 새 staging 전에 완료하는 보호를 유지했다. 위 5개 전체 재실행은 통과했다. 실패 원문은 보고서에 보존했으며 제품 보호를 낮춰 검사를 통과시키지 않았다.

시간 계획은 전체 55분(검토 8·구현 33·검증/문서 14), 준비/수집/회수 검사 약 5초·반영 약 10초·빌드 약 30초와 각 예상의 2배 중단 기준이다. 최초 solution 빌드는 60초 중단 전 **53.38초**, 집중 빌드는 7.86초/8.72초, 마지막 solution 빌드는 **8.89초**이며 모두 경고/오류 0개다. 취소나 강제 종료가 필요한 단계는 없었다. `build.ps1` 전체 시험 단계와 대형 행렬은 실행하지 않았다.

키 하나당 node인 영속 표현과 필수 원본/inventory 전체 관측은 남는다. 반복 색인 작업과 깊이별 순회를 줄였으며 모든 전체 scan을 제거하거나 CPU/디스크를 최대 활용하거나 과거 수십 분~수 시간 현상을 해소했다고 주장하지 않는다. 매우 큰 graph나 동시 scratch 수요에서는 더 느린 디스크 fallback을 사용할 수 있다. 프로세스별 4GiB 작업 목표·버전·wire schema·CLI capabilities는 유지한다.

근거: [남은 후보 보고서](../../../reports/benchmarks/scalable-storage-remaining-20261101.json), `artifacts/scalable-storage-remaining-20261101/`의 최종 소스 hash·CTRF. 대형 경과 시간·최대 RSS·OS 장애·실제 Plastic/Unity·전체 gate 자격은 추가하지 않는다.

## ST-41–43: 같은 소규모 입력의 실제 시간과 고정 대기 제거

ST-41은 ST-36–40 전 `3f7d5b1`과 후 `5dce6e0`를 비교한다. ST-42는 남은 대기를 수정하고 `5dce6e0`와 새 후보를 비교하며, ST-43은 중단 기준·국지 검증을 소유한다. [시간 비교 보고서](../../../reports/benchmarks/scalable-storage-timing-20261101.json)에 정확한 소스/바이너리 hash·명령별 시간·입력·세부 계측을 보존했다. **별도 ABBA 두 묶음**이며 하나의 누적 인과 개선율로 합치지 않는다.

새 실제 None 입력은 각각 **31파일 × 32바이트 = 992바이트, 디렉터리 하나**이며 경로·내용·속성·고정 초기 mtime이 같다. 매 실행은 공유 기준본과 A/B 준비, 명시적 수집 4회, 선택/나머지를 포함한 원본 게시 4회, A 회수 중 B 지속, B 회수, 일반 NTFS 복원을 수행한다. 진단 중간 실행을 포함한 9회 모두 기준 revision과 최종 경로/길이/바이트가 같다. 최종 mtime·생성 identity 일치는 주장하지 않는다. 기존 harness의 원본 전용 편집·독립 결과·peer 지속 검사도 유지한다. 모든 A/B는 `released`, 기준본은 `restored`, 복원 후 소유 process 목록은 비었으며 추가 cleanup Stop은 필요 없었다.

아래는 후보별 2회 평균, 단위는 초다. 준비는 project prepare와 A/B ensure 합계, 수집은 명시적 checkpoint 4회 합계, 반영은 계획 4회와 게시 4회 합계, 회수는 A/B release와 project restore 합계다. **Release 자체의 최종 수집은 회수에 포함한다.** 각 행은 CLI 진입점 내부 시간이다. 전체에는 명령 27회 실행·process 시작·schema/status·편집·harness 비용이 추가되고 fixture 생성은 제외되므로 네 행의 합과 전체는 다르다.

| ST-41: 앞선 최적화 | 이전 `3f7d5b1` | 이후 `5dce6e0` | 절감 |
| --- | ---: | ---: | ---: |
| 준비 | 15.17 | 14.90 | 1.8% |
| 수집 | 17.51 | 17.09 | 2.4% |
| 반영 | 18.63 | 17.64 | 5.3% |
| 회수 | 22.92 | 20.62 | 10.0% |
| 전체 | **92.55** | **88.11** | **4.8%** |

순서는 `a1/b1/b2/a2`, 전체 범위는 각각 91.86–93.24초와 88.06–88.16초다. ST-36–40에 작은 실제 입력 시간 근거를 추가하며 대형 자격으로 승계하지 않는다.

| ST-42: 이번 추가 수정 | 이전 `5dce6e0` | 수정 후 | 절감 |
| --- | ---: | ---: | ---: |
| 준비 | 14.64 | 9.45 | 35.4% |
| 수집 | 16.66 | 17.16 | −3.0% |
| 반영 | 17.62 | 18.39 | −4.4% |
| 회수 | 19.68 | 17.92 | 8.9% |
| 전체 | **86.83** | **81.31** | **6.4%** |

순서는 `b3/d1/d2/b4`, 전체 범위는 각각 **86.30–87.37초**, **78.44–84.17초**다. 이번 묶음의 수집·반영은 개선을 확인하지 못했다. 표본 2회와 저장장치/cache/스케줄 편차로 통계적 유의성을 주장하지 않으며 좋은 `d2`만 평균 대신 쓰지 않는다. 두 묶음의 대조군 평균도 다르다. 중간 진단 `c1`(83.274초)은 양쪽 최종 비교에서 제외한다.

발견한 고정 대기는 payload 복사 비용과 무관했다.

- 최초 연결 관측이 아직 없는 launch/state 게시를 약 2초 기다렸다. `ObserveForConnectionAsync`가 Start와 같은 배타 `client.lock`을 확보한 경우에만 부재를 확정한다. 잠금 점유 시 일반 관측을 쓰며, 미해결 intent의 state 대기·소유권 불확정과 child 게시 복구는 유지한다. 작은 일반/잠금 부재 관측은 **2.011477→0.004310초**다.
- 최초 `VerifyConnectionAsync`가 자신이 아직 게시하지 않은 request를 1초 기다렸다. 기존 coordinator 작업 잠금 아래 이 읽기의 부재 대기만 생략한다. 일반 읽기·공유 오류 재시도·identity 검사는 유지한다.
- 첫 중지 저널 회수가 자신이 아직 게시하지 않은 `delta-reclaim.json`을 1초 기다렸다. 같은 작업 잠금 증명으로 부재 대기만 제거했다. 삭제 전 전체 중지 provider/seal/native inventory 증명·같은 intent 복구를 유지한다.

선택적 `SPLINTER_RESULT_GENERATION_DIAGNOSTICS=1`에 release 세부 시간을 추가했으며 순서나 복구 입력은 바꾸지 않았다. 최종 `d2`의 A 최종 수집은 여전히 **4.957초**, 마지막 metadata 회수는 **1.198초**다. 그 수집 내부는 freeze 0.696초, 결과 scan 0.477초, 범위 보존 0.620초, 독립 검사 2회 합계 0.299초, prune 0.795초, backend 완료 0.685초다. 중첩 계측을 상위 시간에 더하지 않는다. 남은 비용은 native 수집과 metadata/범위 처리에 걸쳐 있으며 추가 증명 생략은 채택하지 않았다. 수집·반영·큰 inventory 최적화는 남고, 과거 수십 분~수 시간 문제의 **해소는 입증하지 못했다**.

고정 계획은 전체 작업 55분, 수명주기 예상 90초/중단 180초, start/bulk/selected/final 예상 20/15/20/35초와 각 2배 중단이다. 명령 예상은 prepare 3초, ensure 5초, capture 5초, plan 2초, 게시 5초, release 8초, restore 3초, schema/status 1초다. Helper는 명령/단계/전체 중 가장 이른 기준에서 협력 취소하고 driver는 늦게 성공한 응답도 거부해 후속 작업을 시작하지 않는다. 정확한 소유 provider의 정상 Stop을 유지한다. Trial의 16–128항목 제한은 검증 도구 한도이며 제품 상한이 아니다. 의도한 읽기 전용 timeout은 **2.108초**에 종료했으며 provider/home 생성이나 다음 명령은 없었다. 실제 수명주기 9회는 모두 처음 정한 기준 안에 끝났다.

**검사 묶음 두 번에서 실행 지침을 지키지 못했다.** 예상 20초/중단 40초 묶음이 **67.997초**, 예상 5초/중단 10초 pilot이 **13.074초** 걸렸으며 제때 취소하지 못하고 정상 완료됐다. 허용 시간을 확대한 것으로 취급하지 않고 운영 오류로 보존한다. Launch 기록 부재가 원인이라는 가설은 pilot 개선이 없어 변경을 되돌렸다. 실제 계측은 반복된 실제 Git 준비/기준 증명/저널 준비가 한 사례에서 0.802/1.715/2.409초이고 첫 회수/재시도는 0.665/0.478초였다. Fixture를 정확한 lease 기반 Git 읽기 scope와 마지막 전체 `VerifyReadBoundary`로 바꾸고, 독립 사례를 제한 병렬·개별 10초 timeout으로 실행했다. 2개 pilot은 6.378초, 마지막 10개는 예상 20초/중단 40초 아래 **20.407초**에 통과했다. Fixture/병렬화 비교는 제품 개선율이 아니다.

검증 합집합은 **고유 43개 통과, 실패/skip 0개**다. 게시 유예·점유/미해결 launcher·snapshot/mount 관측·중지 저널 보호 10개를 포함한다. 최종 제품 solution 빌드 **18.92초**, 집중 시험 빌드 10.78초, helper 빌드 2.93초이며 경고/오류는 모두 0개다. 고정한 최종 제품 바이너리는 현재 제품 출력과 일치한다. 대형 fixture·실제 Git/Plastic/Unity 수명주기·OS 장애·최대 RSS/하드웨어 포화·`build.ps1` 전체 시험 gate는 실행하지 않았다. task v4/basis v3/overlay v3·CLI 계약을 유지하며 SCAL-03/07/08은 미완료다. 원시 기록은 `artifacts/scalable-storage-timing-20261101/`와 보고서의 정확한 임시 fixture 경로에 보존했다.
