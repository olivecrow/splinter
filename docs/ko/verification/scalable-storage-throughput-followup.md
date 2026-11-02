# 대규모 저장 처리량 후속

[English](../../verification/scalable-storage-throughput-followup.md) · [기존 진단](scalable-storage-throughput-diagnosis.md)

상태: ST-01–23 구현·작은 정확성 검증·시간을 제한한 실제 성능 시도 완료. 후속 사용자는 모든 리뷰 수정·항목별 작은 검증·시간을 제한한 마지막 성능 시도를 승인했다. 필수 전체 시험 gate는 취소했으며 **통과가 아니다**. 보존한 100k fixture를 재개하지 않으며 SCAL-03/07/08은 미자격이다.

이후 확인된 구조적 원인을 수정하라는 요청은 **아래 ST-24–28**이 소유하며, 작은 국지 검증으로 한정한다. 앞선 시간 비교 행렬이나 전체 gate를 재개하지 않는다.

이후 공격적 최적화와 수요에 따른 약 4GB 메모리 사용 승인은 **ST-29–35**가 소유한다. 이전 캐시/worker 정책을 대체하며 무결성 요구나 대형 검증 중단을 해제하지 않는다.

남은 후보를 진행하라는 후속 요청은 아래 **ST-36–40**으로 구현했으며 검증은 작은 국지 검사로 한정한다.

이후 동일 소규모 입력의 실제 시간 비교·느린 구간 수정 요청은 **ST-41–43**이 소유한다. 첫 ABBA는 `3f7d5b1`/`5dce6e0`, 둘째 ABBA는 `5dce6e0`/이번 수정 후보이며 두 블록을 하나의 교차 비교로 섞지 않는다.

metadata 중복 리뷰 다섯 항목은 **ST-44–48**이 소유한다. 작은 레코드 변경 네 가지를 구현했고 다중 항목 페이지 재설계는 명시적으로 보류했다. metadata 저장소 codec 계열을 `SPM2`로 변경하며 대형 검증을 재개하지 않는다.


metadata 대화 완료 후 요청한 수집/반영 조사·수정은 아래 **ST-49–51**이 소유한다. 비교 양쪽 모두 SPM2이며 앞선 SPM1 시간과 합치지 않는다.

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

## ST-44–48: metadata 레코드 중복 검토

시작 소스는 변경 없는 `development`의 `72601d6d4c9fee80c48dc15452b5caf7e345d84f`다. 사용자는 지정한 리뷰 다섯 항목·불필요한 중복 제거·JSON보다 효율적인 형식 검토와 항목별 작은 검증을 승인했다. 시작 시 표시된 다른 저장소 대화는 유휴 상태였으며 기존 변경이나 보존 fixture를 덮어쓰지 않았다. CLI 조회 JSON은 이미 바이너리인 대량 영속 자료와 구분한다.

| ID | 결과와 유지한 경계 |
| --- | --- |
| ST-44 | **보류·미구현:** 다중 항목 정렬 트리 페이지. 현재 결정적 불변 treap는 키 하나당 node다. Null link는 이미 flag로 생략하며 61바이트 참조 세 개를 무조건 저장하지 않는다. 64개 키/16바이트 값 bulk build는 1,280바이트 key/value에 64페이지·8,259바이트를 쓰며 48바이트 container header는 제외했다. 페이지마다 checksum header 40바이트·길이/flag 9바이트와 null이 아닌 참조당 61바이트를 기록한다. 묶은 leaf/internal 페이지는 페이지 수/깊이를 줄일 수 있지만 결정적 논리 identity·희소 COW split/merge·rank/select·디스크 spine·graph relocation·회수/중단 proof·과거 cursor를 함께 재설계해야 한다. 논리 treap를 유지한 물리 packing도 인증한 subpage 주소/relocation이 필요하다. 어느 설계도 이번 작은 레코드 변경으로 자격화하지 않았고 깊이/페이지 수 감소를 주장하지 않는다. |
| ST-45 | 게시 변경은 Before 전체를 보존하고 After/Expected는 같은 kind의 완전한 Before/After 중 더 작은 참조에 대해 필드 차이를 기록하되 전체 값보다 작을 때만 선택한다. 길이·전체 SHA-256·속성·mtime ticks·nullable Unix mode를 독립 복원한다. Expected native identity·AlreadyMatches도 별도다. 같은 내용이 같은 metadata를 뜻하지 않는다. 미지원 tag/mask·없는 참조·잘린 필드·후행 바이트는 거부한다. inline/internal 변경 tag는 4/5다. |
| ST-46 | `FileApplyIndex.Build`/`ApplyVersions`는 원래 경로를 key에 한 번만 기록하고 type/길이/ticks/속성/hash/Unix mode의 바이너리 값과 별도 삭제 tag를 사용한다. 기존 spool envelope가 길이·SHA-256을 검증하고 codec이 flag·정확한 크기·경로/type/metadata·nullable 필드를 검사한다. 조밀 병합/희소 갱신·interning·경로 대소문자·영속 index 인코딩은 유지한다. 이 임시 레코드의 JSON 재파싱은 제거했다. |
| ST-47 | 디렉터리 record v2는 flag를 묶고 0인 생성/접근 시각을 생략하며 0이 아닌 모든 값·mtime·속성·Unix mode·내용/child 참조·source reference·경로 정책을 유지한다. 선택 시각을 명시적 0으로 기록하면 비정규 형식으로 거부한다. 정확한 새 값 바이트로 논리 root가 달라지며 물리 배치는 여전히 논리 identity에서 제외한다. |
| ST-48 | 목록 scratch는 원래 대소문자의 이름을 정렬 key 한 곳에 두고 바이너리 고정 metadata·overlay 전용 tag를 기록한다. 불변 경로 정책 comparer를 메모리 정렬·모든 spill run·모든 merge 단계에 적용한다. 중복 전체 경로를 버리기 전에 기준본 목록의 정확한 부모/경로를 확인하고 기준본 metadata를 반환하기 전에 snapshot 가용성을 다시 검사한다. 고정 node/name/hidden root·overlay 우선순위·원래 대소문자·숨김/제외·metadata 재사용을 유지한다. |

**현재 형식 하나:** metadata identity/container 계열은 형식 2인 `SPM2`다. 저장소 열기·identity 읽기·회수/중단 audit·읽기 전용 파생 replay·Plastic native admission이 같은 magic을 쓴다. 직전 task-v4 후보의 `SPM1`도 append/회수 전에 보존·거부하므로 일치하는 바이너리로 마무리한다. 이전 디렉터리/변경 tag도 거부한다. 이관·변환·fallback·이전 저장소 정리는 제공하지 않는다. Task v4/basis v3/overlay v3·metadata root schema v1·treap page 배치·`SPN1` 논리 hash 알고리즘은 유지한다. 정확한 root/digest는 새 값 바이트를 결합하고 native hash·전체 inventory·소유권·reader fence·같은 요청 복구·영속 root/ACK 순서는 계속 필수다. 제어/CLI JSON은 유지한다.

작은 바이트 비용 근거이며 **실제 전체 시간이나 대형 성능 개선율이 아니다**.

| 고정 입력 / 측정 단위 | 이전 | 이후 |
| --- | ---: | ---: |
| Unicode 경로·After 내용 변경·Expected 속성만 다른 게시 변경 1개의 inline 바이너리 값 | 215 B | 155 B |
| 생성/접근 시각 모두 0인 디렉터리 값 | 기존 값 크기 | 20 B 감소(시각 16 B + flag 통합 4 B) |
| 선택 시각 둘 다 0이 아닌 디렉터리 값 | 기존 값 크기 | 4 B 감소 |
| Unicode 90-component 경로·파일 hash·mode의 비교 값 | JSON 12,150 B | 경로는 key에만 둔 binary 57 B |
| 같은 비교의 key 포함 checksum spool 전체 레코드 | 23,624 B | 11,531 B |
| 시각 세 개와 Unix mode를 보유한 고정 목록 값 1개 | JSON 159 B | 이름은 key에만 둔 binary 41 B |

게시의 이전 크기는 이전 바이너리 encoder의 정확한 시험 전용 복사본, 비교/목록은 이전 JSON 레코드 형태를 사용했다. 같은 고정 레코드 비교이며 독립적인 후보 전체 실행이 아니다. 참조 없는 전체 게시 version은 selector 1바이트가 추가되며 차등은 이 전체 표현보다 작을 때만 사용한다. 키 하나당 node/header 비용은 여전히 크다.

검증은 **고유 113개 통과**, 미해결 실패/skip 0개다. 새 codec/비용 묶음은 **13개·1.721초**이며 Before/After/Expected 216개 조합·대표 레코드의 잘린 모든 prefix·이전 tag/magic·없는 참조·선택 시각/mode·Unicode 대소문자를 유지한 강제 다단계 spill/merge를 포함한다. 기존 root/reader는 **37개·2.936초** 통과하고 환경 실패 3개는 이후 native 묶음에서 통과했다. 회수/손상/같은 요청 중단은 **28개·10.111초**, native reader/Plastic admission은 **21개·10.070초**, native 게시/충돌/재시도/이력은 재실행 3개를 포함해 **14개·32.257초**다. 합집합에서 ID를 한 번만 센다. 실제 Plastic client/server·Git/Unity 수명주기·OS crash·대형 전체 시간/RSS는 미자격이다.

최초 새 묶음은 sandbox 기본 임시 폴더의 rename 거부로 8개 오류가 났다. 저장소 소유 scratch에서는 당시 12개 모두 통과했다. root 묶음은 저장소 조상의 VCS guard 1건·sandbox hardlink 거부 2건에 걸려 제품 보호를 바꾸지 않고 새 일반 임시 fixture의 native 실행에서 통과했다. 잘못 결합한 class/method filter는 0개를 선택하여 수정했으며 증거에 포함하지 않는다. 실패/성공 원시 CTRF는 모두 보존했다. 최초 병렬 빌드는 3.91초에 진단 없이 실패했고 직렬 빌드 37.03초에서 시험 코드의 nullability/취소 analyzer 오류 4개를 확인·수정했다. 이후 집중 빌드는 8.76/8.51초 통과했고 최종 Release solution 빌드는 **18.68초**, 경고/오류 0개다.

최초 시간 범위는 읽기/조율 약 3분·항목당 구현 5–15분·검증 묶음 예상 최대 30초와 각 예상의 두 배 중단이었다. 위험한 페이지 재설계를 시도하기 전에 범위를 좁혔다. 빌드는 앞선 작은 빌드 기록을 근거로 예상 30초/중단 60초를 사용했다. 실행한 묶음/빌드는 모두 중단 경계 안에서 끝났고 쓰기 중 provider의 강제 종료는 없었다. 보존 100k/17GiB fixture·Editor·실제 수명주기는 재개하지 않았다. 사용자 작은 검증 지시에 따라 `build.ps1` 전체 gate는 의도적으로 미실행이며 SCAL-03/07/08은 미완료다. 근거는 [레코드/형식 보고서](../../../reports/benchmarks/metadata-encoding-20261101.json)와 `artifacts/metadata-encoding-20261101/`의 소스 hash·CTRF다.

## ST-49–51: metadata 최적화 이후 수집·반영

대화 `01a19778-a47d-72f1-8c1f-0008fc633412`의 완료를 확인한 뒤 구현했다. 그 대화의 미커밋 ST-44–48 변경은 기준 patch에 따로 보존했다. 비교 양쪽은 `72601d6` 위 같은 SPM2 변경과 같은 선택적 diagnostics를 사용한다. 이번 후속은 task/basis/overlay·metadata codec·CLI/wire schema·게시 내구성 계약을 바꾸지 않는다.

| ID | 원인·수정·유지한 보호 |
| --- | --- |
| ST-49 | 계획이 target마다 `WindowsOriginalFilePublisher.Observe`를 호출하면서 `ManagedDirectoryBasis.AcquireOriginal`의 provider snapshot 가용성 증명·root/조상 핸들·VCS 검사를 반복했다. 전체 기준본 작업이 선택 파일 수만큼 곱해지는 구조였다. Core 선택 port `IOriginalFileObservationProvider`/`IOriginalFileObservationScope`가 한 계획 동안 정확한 root/조상/관리형 reader를 유지하고 target마다 다시 읽은 뒤 해제하며 `SavePlan` 전에 가용성/관리 경계를 재검증한다. 명령 간 관측 재사용이나 입력 크기만큼의 핸들 캐시는 없다. 선택 port가 없는 publisher는 기존 관측을 유지하고 실제 게시는 여전히 전체 대상을 독립 잠금·재검증한다. 16파일 native 시험에서 resolver 호출 **17→2**와 후속 편집·root 교체 차단·취소·새 VCS 관리를 확인했다. 확장 비용의 코드 근거이며 대형 절감 실측이 아니다. |
| ST-50 | `PreserveRangePayloads`는 파일마다 private 범위 spool을 썼다가 다시 읽었고 기준본과 같은 payload는 descriptor 목록을 소비하지도 않았다. 이제 descriptor builder에 직접 전달하고, 기존 descriptor 또는 기준본 동일 payload여도 나머지 행을 끝까지 drain한다. 기준본 동일 payload의 불필요한 raw-basis 조회도 생략한다. 모든 구간/chunk 검사·보존·adoption 행·독립 결과 증명·영속 root/ACK를 유지한다. 새/기존/기준본 동일 payload 각각 정상 및 잘못된 마지막 범위를 검사했고, 거부 시 이전 결과를 유지하고 새 adoption/결과 영수증을 게시하지 않았다. 메모리는 스트리밍을 유지한다. |
| ST-51 | 기존 선택적 diagnostics에 raw 준비·descriptor 처리·adoption 게시·원본 관측·계획·게시를 분리했다. 로그는 복구 입력이나 성공 영수증이 아니다. 새 작은 ABBA 두 블록의 중간/최종 후보를 구분하고 합산하지 않는다. |

기존과 같은 **31파일 / 디렉터리 1개 / 992바이트 / payload 16종**이며 seed 내용·속성·고정 mtime이 일치한다. 8회 최종 경로/길이/내용도 일치하고 후속 편집 시각·생성 ID는 실행마다 다르다. 매 실행은 27명령·명시적 수집 4회·계획/게시 4회·release 중 마지막 A/B 수집·원본 복원이며 Editor는 없다. 아래 구간은 CLI 진입점 내부 합계이고 전체 시간에는 dispatch·PowerShell·sampling이 포함되므로 행을 더해 전체로 해석하지 않는다.

| 최종 블록 `a3/c1/c2/a4`, 초 | 수정 전 평균 | 최종 평균 |
| --- | ---: | ---: |
| 준비 | 8.95 | 8.53 |
| 수집 | 18.46 | 16.47 |
| 계획 포함 반영 | 18.61 | 16.42 |
| 회수/복원 | 21.90 | 18.32 |
| 전체 | 86.32 | 77.47 |

전체 범위는 이전 **76.98–95.66초**, 이후 **75.48–79.46초**다. 관측 평균은 8.85초/10.3% 줄었지만 대조군 변동이 바꾸지 않은 adoption 게시/회수에도 나타나므로 **전체 인과 개선율로 확정하지 않는다**. 중간 `a1/b1/b2/a2`는 평균 83.52→80.15초, 범위 78.50–88.53초 / 78.69–81.60초이며 새로 합성한 descriptor는 당시 spool을 사용했다. 앞선 SPM1 실행과 비교하거나 가장 빠른 실행만 고르지 않는다.

더 반복적으로 확인된 구간은 target 관측 76회다. 최종 평균 **1.737→0.340초(80.4% 감소)**이며 중간 블록도 1.656→0.357초다. 반영 내부 재계획을 포함한 계획 8회의 합계는 최종 블록에서 **5.808→4.045초**다. 결과 6회의 descriptor 처리는 **1.007→0.951초**로, 작은 입력에서 **의미 있는 수집 속도 개선은 입증하지 못했다**. 최종 후보의 결과 6회 합산 평균에는 별칭 정리 **3.194초**, backend 완료 **3.020초**, freeze **2.890초**, adoption 게시 **1.708초**가 남았다. 현재/진행 중/과거 참조 inventory·영속 metadata·native reader 경계의 비용이며 제거하지 않았다. 중첩 계측은 부모와 합산하지 않는다.

최종 소스 집중 검증은 **28개 통과·실패/skip 0개**다. 원본 관측/게시·portable fallback·충돌·같은 intent 복구 15개는 **9.503초**(예상 12초/중단 24초), 수집/되돌림/기존 descriptor·취소/후속 편집/reader 보호 13개는 **8.617초**(예상 15초/중단 30초)였다. 앞선 streaming 10개 묶음은 예상 10초/중단 20초에서 18.274초였다. 최초 시험 빌드는 새 시험의 취소 analyzer xUnit1051 하나로 실패(35.36초)했고 수정 빌드 7.45초에 통과했다. 최종 증분 시험/제품 빌드 **10.780초**, 경고/오류 0개, 예상 15초/중단 30초다. 고정한 최종 바이너리는 현재 출력과 일치한다.

의존 작업 완료 뒤 실제 작업 예상은 45분이고 구현은 14:42:05 UTC에 시작했다. 매 수명주기 **예상 90초/중단 180초**, start/bulk/selected/final 단계 **20/15/20/35초**, prepare/ensure/capture/plan/apply/release/restore/schema-status 명령 예상 **3/5/5/2/5/8/3/1초**를 유지하며 각 두 배 또는 더 이른 상위 deadline에서 중단하도록 했다. **216명령·32단계·8실행** 모두 고정 중단 경계 안에 완료했고 최대 명령 비율은 A release **14.471초 / 8초 = 1.809배**다. 한도 확대·쓰기 강제 종료·추가 Stop은 없었다. 정확한 PID/시작 identity로 기록된 **provider 24개가 모두 부재**하고 8쌍 A/B 회수 및 원본의 일반 디렉터리 복원을 확인했다.

[보고서](../../../reports/benchmarks/scalable-storage-capture-apply-20261101.json)에 소스/바이너리 hash·두 비교 블록·명령/진단/시험·정확한 보존 fixture 경로를 결합했다. 원시 자료는 `artifacts/scalable-storage-capture-apply-20261101/`에 보존했다. OS 캐시는 비우지 않았다. 대형 입력·실제 Git/Plastic/Unity 수명주기·OS crash·최대 RSS/하드웨어 포화·`build.ps1` 전체 gate는 실행하지 않았다. ST-44 다중 항목 페이지와 SCAL-03/07/08은 미완료이며 과거 수십 분/몇 시간 문제의 해결은 **미입증**이다.

## ST-52: 다중 항목 페이지 채택 검토

2026-11-02 KST 사용자는 보류한 불변 B+트리 계획이 시간 최적화에 유리한지 검토하고 타당하면 구현하도록 요청했다. **판단은 현재 계획으로 범용 제품 metadata 트리를 교체하지 않는 것이다.** 다중 항목 페이지의 bulk metadata 절감 여지는 확인했지만 정규 경계를 국소 갱신하는 규칙은 여전히 미정이다. 모든 묶음 트리를 거부하거나 빨라질 수 없다고 판단한 것은 아니다. ST-44는 보류를 유지하며 SPM2와 현재 구현은 바꾸지 않았다.

코드상 `WriteNode`는 이미 작은 페이지를 공통 container에 연속 기록하고 `Build`는 결정적 treap node를 한 번씩 쓴다. `Flush`는 root 게시의 내구성 경계다. 키마다 페이지가 있어도 **키마다 물리 파일이나 영속 flush가 생기는 것은 아니다**. 작은 probe의 256키 bulk는 256페이지와 명시적 page-store 영속 호출 1회였다. 소유권/catalog/provider의 flush는 probe 바깥이며 이 설계로 제거되지 않는다. `ReadNodeCore`의 cache miss는 아직 header/body를 따로 읽으므로 물리 읽기 감소 가능성은 있지만 페이지 수 감소를 읽기/시간 감소와 같다고 볼 수는 없다.

폐기 가능한 probe는 현재 DLL로 **64/256키 × 16/256바이트 값**의 bulk/동일 재구축·값 하나 수정·최소 키 삽입/삭제·과거 root·bulk/수정 논리 identity·재개방 목록/Rank/EntryAt을 확인했다. 별도 **비용 모델**은 B+ 저장소 구현이 아니라 계획의 미정 경계 규칙을 가장 단순한 정규 byte-greedy packing으로 채웠다. 최대 512/4,096/16,384바이트, checksum header 40바이트, kind/count, 길이 결합 leaf 값과 child key/주소/count/hash 레코드를 사용한다. 정방향/역방향/섞인 전체 입력의 root는 같다. 이 fixture에는 항목 소유 child가 없다.

| 256키·16바이트 값, data-page 기록 바이트 | 현재 treap 실제 기록 | 정규 4KiB packing 모델 |
| --- | ---: | ---: |
| Bulk | 34,243 | 8,844 |
| 같은 길이 값 하나 수정 | 1,694 | 4,332 |
| 최소 키 하나 삽입 | 1,974 | 8,877 |
| 최소 키 하나 삭제 | 1,633 | 8,811 |

모델의 bulk 기록은 **74.2% 감소**하지만 값 수정은 **2.56배**, 삽입은 **4.50배**다. 값이 256바이트이면 같은 4KiB 모델의 최소 키 삽입은 **4,854→72,429바이트(14.92배)**다. 처음부터 채우는 방식에서는 뒤쪽 leaf 경계까지 밀린다. 최대 512바이트로 줄여도 이 경계 규칙은 해결되지 않아 16바이트 값 삽입이 **1,974→11,217바이트**다. 이는 단순 규칙의 반례이며 **모든 B+ 설계의 결과가 아니고, 사용자가 제시한 계획이 이 규칙을 이미 선택했다는 뜻도 아니다**.

다음 설계의 선행 조건은 결정적이면서 국소 갱신 가능한 경계 규칙이다. 삽입/삭제·값 길이 변경 때 뒤쪽 전체를 보통 다시 묶지 않아야 한다. 키 기반 경계나 정규 radix 분할은 검토 가능한 후보이며 채택한 설계가 아니다. 논리 hash는 순서 있는 모든 key/value/항목 소유 child hash·count를 유지해야 한다. 일반 B+의 분할 모양에 따른 root는 기존 삽입 순서 독립 계약을 만족하지 못한다. `CopyRootsToCore`·도달성·회수·중단 audit는 branch child와 모든 항목 소유 child를 함께 순회하고 과거 root/reader fence·재배치 주소와 논리 identity를 검증해야 한다. Hash 약화·작은 수정마다 전체 재해시·복구 증명 생략으로 우회하지 않는다.

채택 전 작은 입력으로 **bulk 바이트·희소/묶음 삽입/수정/삭제 바이트·cold/warm 물리 읽기·영속 flush 수·경과 시간**을 소유권/catalog/회수까지 포함해 비교해야 한다. ST-51의 결과 6회 잔여에는 inventory·native proof·영속 adoption 게시가 포함되며 이번 data-page 모델은 그중 실제 시간이 얼마나 줄어드는지 입증하지 않는다.

분리 helper 빌드 **4.465초**(예상 10초/중단 20초), 실제 data-page probe **0.850초**(3초/6초), 모델 **0.022초**(1초/2초)였다. 검토/원형 예상은 각 10분·중단은 각 두 배이며 한도 초과는 없다. 제품 소스 513개와 최종 제품 DLL hash는 앞선 후보와 일치한다. Provider·대형 fixture·전체 gate·제품 저장 형식 변경은 시작하지 않았다. [검토 보고서](../../../reports/benchmarks/metadata-multipage-review-20261102.json)에 실제 기록·모델 가정·소스 hash·한계를 보존하고 원형은 `artifacts/multipage-review-20261102/`와 `.local/multipage-review-20261102/`에 둔다.

## ST-53–55: 수집 참조 정리와 영속 adoption metadata

시작 소스는 `1b33995`이며 SPM2/task v4/basis v3/overlay v3를 유지한다. 사용자는 같은 작은 입력과 고정 예상의 두 배 안전 중단을 조건으로 추가 수집 최적화를 승인했다. 비교 양쪽에는 같은 선택적 diagnostics를 넣었다. 앞선 보고서와 보존 대형 fixture는 바꾸지 않았다.

| ID | 수정과 유지한 경계 |
| --- | --- |
| ST-53 | `CapturedRangeAdoptionStore.Publish`는 새로 bulk-build한 불변 flat tree도 즉시 회수했다. 새 `PagedMetadataStore.VerifyCompactFlatTree`가 writer/회수를 제외하는 reader fence 아래 정렬 root와 모든 page hash/count·native container 소유권을 검증한 뒤 물리 참조를 정렬하여 모든 container payload 바이트의 정확한 연속 피복을 요구한다. 이 증명이 성공한 경우에만 페이지 복사와 collection intent/relocation/receipt 생성을 생략한다. 기존 페이지와 adoption artifact는 그 전에 이미 flush됐다. 가용성 cache가 아니라 매번 수행하는 전체 증명이다. Reader는 공존하고 writer는 계속 배제한다. |
| ST-53 fallback | Child tree·collection/relocation/receipt 상태·미확인 제어 파일·미참조/잘린 꼬리·native 소유권 접수 미완료는 기존 회수 또는 보존/거부를 따른다. 진행 중 collection은 같은 요청으로 복구한다. 도달 가능한 손상은 거부하며 미소유 header 흔적은 소유하지 않고 보존한다. Pending intent·unknown 항목·영속 ACK·native/content 증명을 우회하지 않는다. |
| ST-54 | `MetadataMembership`는 live·진행 중 adoption·기준본/현재·과거 결과 참조를 모두 drain한 뒤 별칭을 삭제한다. 정확한 key/value 사전은 공통 scratch 예산과 sort-run 한도 안에서 사용한다. 모순된 길이는 회수 전에 거부한다. 예산 부족은 기존 정렬 metadata tree로 전환하며 사전 capacity를 버린 뒤 메모리 계상을 반환한다. 입력 전체 배열이나 입력 거부 상한을 추가하지 않는다. 결합한 live-source reader는 정리까지, manifest/adoption reader는 해당 참조 소비 동안 유지한다. |
| ST-55 | `WindowsSourceChanges`는 첫 빈 응답에서 방금 끝낸 provider 전체 증명을 중복 실행하지 않는다. 최초/응답 경계 증명·고유 token/request/PID/시작/snapshot 검증·결합한 change-reader 보호는 유지한다. 대기 중 전체 증명은 1초 시계로 유지한다. 응답 재조회는 최초 100ms에만 20ms 간격이고 이후 기존 100ms다. 장시간 무제한 고빈도 polling 없이 짧은 응답의 지연을 줄인다. |

기준 하위 구간 계측은 결과 6회 합산으로 adoption 생성/flush **0.031초**, 직후 adoption 회수 **1.413초**, live 참조 관측 **2.335초**, 필수 참조 색인 **0.524초**, whole 별칭 정리 **0.064초**, raw 정리 **0.012초**였다. 참조 색인 계측에는 소스/진행 중/과거 목록의 열거도 포함되며 사전이 그 증명을 없애는 것은 아니다. `ResultGenerationDiagnostics`는 선택적·비권위 로그를 유지한다.

최종 소스 검사는 **25개 통과·실패/skip 0개**다. 새 12개는 **1.925초**(예상 8초/중단 16초)이며 메모리/디스크/부분 spill/공통 예산 부족·전체 drain·모순·취소·빈/비어 있지 않은 전체 피복·reader 공존/writer 배제·고아 page/미확인 제어·도달 가능 손상/잘림/native 소유권·미소유 header 보존·root seal/relocation 게시 후 같은 요청 복구를 확인했다. 기존 수집/adoption/되돌림/후속 편집/reader 검사 13개는 **7.182초**(12초/24초)다. 최종 시험/제품 빌드 **8.47초**, 경고/오류 0개(15초/30초)이며 앞선 빌드 9.12/11.25/7.16초도 각 원래 한도 안에 완료했다. `build.ps1` 전체 gate는 실행하지 않았다.

기존과 같은 Editor 없는 None 입력은 **31파일 / 디렉터리 1개 / 992바이트 / payload 16종**이다. 새 실행 8개의 seed(고정 시각/속성 포함)와 최종 경로/길이/내용이 일치하며 후속 편집 시각·생성 identity는 다르다. 매 실행은 27명령·명시적 수집 4회·계획/게시 4회·release 중 마지막 A/B 수집·원본 복원이다. CLI 진입점 내부 구간 합계에는 driver/sampling/dispatch가 빠져 있으므로 전체 시간과 가산하지 않는다.

| 최종 ABBA 블록 `a3/c1/c2/a4`, 초 | 수정 전 평균 | 최종 평균 |
| --- | ---: | ---: |
| 준비 | 8.011 | 7.399 |
| 수집 | 15.602 | 15.086 |
| 계획 포함 반영 | 15.342 | 18.047 |
| 회수/복원 | 16.790 | 18.722 |
| 전체 | 72.590 | 76.116 |

**전체 시간의 개선은 입증하지 못했고 최종 평균은 3.526초/4.9% 늘었다.** 이전은 72.541/72.639초, 최종은 69.642/82.590초다. 수집 평균 감소도 0.515초/3.3%에 그쳤다. 중간 `a1/b1/b2/a2` 블록은 평균 80.423→70.630초, 범위 72.096–88.750초 / 70.548–70.711초다. 최종 polling/capacity/header 수정 전이므로 최종 블록과 합산하거나 불리한 최종 결과를 대체하지 않는다. OS 캐시는 비우지 않았고 최종 후보별 표본은 각 2회다.

| 중첩 진단, 실행마다 결과 6회 합산의 평균 | 이전 초 | 최종 초 | 관측 감소 |
| --- | ---: | ---: | ---: |
| Adoption 유지보수: 회수 대 전체 compact 증명 | 1.405 | 0.160 | 88.6% |
| Live-source 참조 관측 | 2.366 | 1.519 | 35.8% |
| 필수 참조 열거/색인 | 0.544 | 0.491 | 9.7% |
| 앞 두 행을 포함한 별칭 정리 전체 | 3.024 | 1.980 | 34.5% |
| 중첩 작업을 포함한 결과 보존 전체 | 12.671 | 11.207 | 11.6% |

국소 효과는 최종 두 실행에서 반복됐다. Adoption 유지보수는 0.112/0.208초로 대조군 1.388/1.422초보다 작고, 별칭 정리는 1.884/2.076초로 대조군 3.020/3.029초보다 작다. 중첩 비용끼리는 합산하지 않는다. 새 tree 생성/flush는 유지하며 작은 비용(0.031→0.066초)이고, 전체 참조 열거와 영속 게시를 제거하지 않았다.

느린 최종 `c2`의 원본 게시는 **6.363초**, 마지막 metadata 회수는 **4.676초**로 `c1`의 **1.858/1.749초**보다 컸다. 두 구간이 전체 12.95초 차이 중 약 7.43초를 차지한다. 중간 대조군 `a2`도 바꾸지 않은 같은 경로에서 4.124/4.502초로 느려졌고 기준본 보존도 증가했다. 변동 위치를 확인한 것이며 **근본 원인이나 수정본의 영향 배제를 확정한 것은 아니다**. 디스크/백신 문제로 단정하지 않는다. 수집 backend 완료(최종 평균 **3.187초**)와 freeze(**2.781초**)도 남았다. 유리한 전체 결과를 고르기 위한 추가 반복은 하지 않았다.

실행 전 전체 작업 예상은 **40분**, 조사/구현/작은 검증/기록 **8/15/12/5분**, 각 예상의 두 배 중단으로 정했다. 작업은 16:02:45 UTC에 시작했다. 매 실행 **예상 90초/중단 180초**, start/bulk/selected/final 단계 **20/15/20/35초**, prepare/ensure/capture/plan/apply/release/restore/schema-status 명령 **3/5/5/2/5/8/3/1초**를 유지하며 각 두 배 또는 더 이른 상위 deadline에서 중단한다. **216명령 / 32단계 / 8실행** 모두 고정 한도 안에 끝났고 최대 명령 비율은 대조군 `a2` A release **12.510초 / 8초 = 1.564배**다. 한도 확대·쓰기 강제 종료·추가 Stop은 없었다. 8쌍 A/B 회수와 원본의 일반 디렉터리 복원을 완료했고 정확한 PID/시작 시각 audit에서 기록된 **provider 24개 모두 부재**를 확인했다.

[수집 유지보수 보고서](../../../reports/benchmarks/capture-maintenance-20261102.json)에 최종 **516파일** 소스 목록·고정 바이너리·기준 diagnostics patch·두 블록·시험/예산·보존 fixture 경로를 결합했다. 원시 증거는 `artifacts/capture-maintenance-20261102/`에 있다. 대형 입력·보존 100k/17GiB fixture·실제 Git/Plastic/Unity 수명주기·OS crash·최대 RSS/하드웨어 포화·전체 gate는 실행하지 않았다. ST-44와 SCAL-03/07/08은 미완료이며 과거 수십 분/몇 시간 문제의 해결은 미입증이다.

## ST-56–60: 참조 작업공간 중심 수집

제품의 목적은 병렬 참조 작업공간과 명시적 원본 반영이다. 결과 기록은 전체 프로젝트 보관소가 아니라 재개·되돌리기를 지원한다. `1b33995`와 보존한 ST-53–55 미커밋 변경에서 시작했으며 이전 보고서는 변경하지 않았다.

| ID | 구현과 유지 경계 |
| --- | --- |
| ST-56 | 파일 manifest **v4**는 전체 비교 catalog를 유지하되 변경 전후 payload·범위 기준 의존성·이전 보존 내용만 보관한다. 디렉터리 root를 직접 비교하고 동일 subtree를 건너뛰어 평탄한 작업 인덱스 두 개를 다시 만들지 않는다. Generation fingerprint가 보관 집합과 descriptor 논리 root를 결합한다. Native None/Plastic에 적용하며 Git은 보호한 기준본을, native 범위 수집이 없는 provider는 전체 payload 경로를 유지한다. v3 manifest는 보존·거부하며 이관하지 않는다. Task v4/basis v3/overlay v3/SPM2·공개 명령 필드는 유지한다. 변경된 파일은 최초 기준 payload 복사가 여전히 필요할 수 있다. |
| ST-57 | 결과 descriptor는 하나의 writable page session에서 변경 항목 최대 64개씩 영속 게시한다. 수집 ACK 전에 `Complete`하며 실패 후 Dispose는 마지막 게시 prefix를 권위로 남긴다. 파일 데이터·descriptor 내용 해시·native 소유권 검사는 유지한다. |
| ST-58 | 수집과 고정 결과 재시도에서 별칭 정리를 제거한다. Release의 기존 보존 결과 유지관리가 live/pending/과거 결과 참조를 정확히 표시하고 회수한다. 그때까지 추가 별칭을 보수적으로 유지하며 4MiB metadata 유지관리 판단과 진행 중 요청 보호는 유지한다. |
| ST-59 | 고정 소스 읽기는 private 범위로 덮이지 않은 기준 gap만 읽는다. 전체 private 읽기는 기준 payload를 읽지 않는다. |
| ST-60 | 고정 목록은 검증한 metadata를 정렬 과정에 유지하고 overlay 우선순위·hidden/excluded·경로 정책을 적용한다. 미변경 하위 항목의 기준 `Stat` 반복을 제거한다. |

파일 manifest schema·유지관리 reader·결과 이력 검사·양 언어 계약·수명주기 지도를 갱신했다. `WorkspaceStore`는 revision과 별개인 SHA-256 fingerprint의 형식을 검사하며, 소스 provider가 결과 수락/사용 전에 실제 catalog·보관 집합·descriptor 결합을 고정 영수증과 검증한다. 무결성 검사와 영속 쓰기 ACK는 제거하지 않았다.

### 최소 검증과 관측 비용

**서로 다른 국소 검사 20개가 통과**했다. 무관한 payload 미보관, 기준본 유실·되돌리기, schema/v3 거부·보관 목록 변조, descriptor 묶음 실패, raw peer 재사용, private 범위 drain, live 참조 회수, 고정 이름 변경/재연결과 게시 중단 경계 3개를 확인했다. 앞선 실패에는 raw descriptor의 이동한 위치를 반영하지 않은 기대값 2개와 reader를 닫기 전 회수를 시도한 테스트 1개가 있었으며 수정한 해당 검사가 통과했다. 필터가 일치하지 않아 0개를 실행한 호출은 집계하지 않는다. 추가 65바이트 이하 검사에서 64개 게시 직후 실패해도 prefix를 보존하고 65번째를 같은 내용으로 재시도하는 경계를 확인했다. 제품 코드는 완료한 실제 비교 뒤 바꾸지 않았다. 최종 build는 통과했으며 `build.ps1`·전체 gate는 실행하지 않았다.

첫 실제 후보에서 연결 누락을 발견했다. 이력 reader가 fingerprint와 revision의 동일성을 요구해 수집이 **2.971초**에 `StateCorrupt`로 끝났다. 정상 Stop 클라이언트도 같은 검사에 걸렸으며 그 전에 provider 하나는 종료된 상태였다. Writer 강제 종료나 기록 편집은 하지 않았다. 이력 검사를 수정한 뒤 나머지 두 provider를 인증된 정상 Stop(**0.317/0.232초**)했고, 같은 `capture-1` 요청이 **1.624초**에 고정 결과를 반환했다. 진단 fixture `b1`은 보존·중지 상태이며 완료 시간 비교에서 제외한다.

동일한 32항목 Editor 없는 None 입력의 완료한 기준 `a1`·최종 후보 `b2` 각 1회:

| CLI 단계 합계 | 이전 초 | 이후 초 |
| --- | ---: | ---: |
| 준비 | 7.341 | 7.522 |
| 수집 | 16.498 | 12.769 |
| 계획/반영 | 17.502 | 16.688 |
| 회수/복원 | 26.307 | 14.934 |
| harness 비용을 포함한 전체 | 85.083 | 69.445 |

**이 한 쌍에서만** 수집 **22.6%**, 전체 **18.4%** 감소를 관측했다. 6세대의 중첩 결과 보존은 **13.286→8.257초**, descriptor **1.050→0.777초**, 결과 scan **2.448→1.967초**, 첫째/둘째 독립 검증 **1.050/1.042→0.534/0.607초**였다. 수집 중 정리 **2.114초**는 없어졌고 release 보존 결과 유지관리는 **0.203→0.109초**였다. 새 보관 선택 비용은 **0.695초**다. Release metadata 유지관리가 **5.928→2.118초**로 달라졌으므로 전체/회수 차이를 모두 이번 수정의 인과 효과로 보지 않는다. 중첩 구간은 합산할 수 없다. Seed metadata·내용과 최종 내용의 일치를 확인했고 완료한 두 A/B의 회수와 일반 디렉터리 원본 복원이 끝났다. 실패 진단 fixture를 포함한 **기록된 provider 수명 9개 모두 최종 관측에서 부재**였다.

사전 예상은 전체 **48분**, 설계/구현/최소 검증/기록 **8/25/10/5분**이며 실제 전체 작업은 전체 예상 이내였다. 각 trial은 **90초 예상/180초 중단**, 단계 **20/15/20/35초**, 기존 명령별 **3/5/5/2/5/8/3/1초** 예상을 유지하고 각각 2배 또는 더 이른 상위 deadline에 중단한다. 국소 검사 묶음은 **8/16초**, build는 **15/30초**였다. 측정 실행의 제한 초과는 없었으며 실제 후보는 timeout이 아닌 기능 실패로 중단했다. 제한 확대·상태 재작성·provider 강제 종료는 없었다.

[원시 보고서](../../../reports/benchmarks/reference-capture-20261102.json)는 현재 소스·고정 바이너리·명령별 시간·하위 진단·실패/복구·provider 관측을 결합한다. 증거는 `artifacts/reference-capture-20261102/`다. 남은 작업은 전체 catalog scan·독립 payload 반복 검증·선택 반영의 넓은 비교·공통 잠금 안 전체 객체/참조 순회와 packing·쓰기 비교/flush 비용이다. 검사를 제거하려면 작업 범위에 묶인 증명이나 의존성을 반영한 순회가 필요하며 이번에 우회하지 않았다. 대형/100k/17GiB·실제 Unity/Git/Plastic 수명주기·전체 gate·OS 장애·최대 RAM·하드웨어 최대 활용 자격은 추가하지 않는다.

## ST-61–64: 제한된 수집 reader와 참조 처리

시작 HEAD는 `8709f902dd14d188f82f1e8cd170e96a2514a9bb`다. 메인 대화는 ST-56–60을 커밋하고 종료 상태였으므로 그 변경을 반복하지 않았다. 시작 시 미추적 파일은 별도 구조 검토 문서 두 개뿐이었다. 제품은 병렬 실행과 명시적 원본 반영을 위한 참조 작업공간이며 전체 프로젝트 보관소가 아니다.

| ID | 구현과 보호 경계 |
| --- | --- |
| ST-61 | `CaptureBackingReaders`는 고정 paged reader마다 최대 8개 sealed segment 핸들을 재사용한다. 정확한 native identity/길이를 확인한 핸들이 퇴출/종료까지 쓰기·삭제를 차단한다. 실제 positional 읽기는 캐시 잠금 밖에서 병렬 수행하며 사용 중 핸들은 퇴출하지 않는다. 8개가 모두 사용 중이면 추가 읽기는 임시 핸들을 사용하므로 8은 보관 캐시 한도이지 전체 핸들 강제 상한이 아니다. 종료는 새 lease를 거부하고 진행 중 lease를 drain한다. 비segment append/bounded backing 동작은 유지한다. |
| ST-62 | `OwnedRangeContentStore`의 기존 fenced backing proof 최대 8개마다 검증한 `(offset,length,hash)`를 최대 1,024개 기억한다. 정확한 쓰기/삭제 차단 핸들이 계속 열린 동안에만 같은 범위 해시를 재사용한다. 퇴출/Dispose 때 증명을 버리며 backing hit도 길이를 검사한다. 최초 검증·source identity·canonical object 검증·공용 catalog 잠금·pin intent 내구성과 ACK는 유지한다. 상위의 fence 없는 hash나 다시 연 핸들로 증명을 넘기지 않는다. |
| ST-63 | 게시 전후 두 독립 payload 검증에서 인증된 hash-keyed `RetainedPayloads` 트리를 직접 순회한다. decoder가 각 row를 검증하고 트리가 hash별 길이를 하나만 제공하므로 재정렬/중복 제거가 불필요하다. 다른 payload 입력은 기존 중복 제거를 유지한다. 실제 payload 읽기/해시·영수증·게시 boundary 검사는 유지한다. |
| ST-64 | 논리 저장량 계산은 `MetadataMembership`으로 모든 참조를 검증/중복 제거한 뒤 집계한다. 공통 scratch 예산과 기존 정렬 디스크 fallback을 유지하고 작은 입력은 sort/spool을 피한다. raw/whole 구분과 상충 길이 거부를 유지한다. 수집 취소를 accounting·전체 길이 순회·일반 payload 정렬까지 전달한다. 과거 참조와 basis/result 전체 길이 순회는 여전히 남는다. |

### 최소 검증·실패·한계

최종 선택 시험은 **23 통과·실패/skip 0, 9.293초**(예상 8초/중단 16초)다. 128KiB 고정 파일을 4KiB씩 두 번 읽을 때 backing 검증은 한 번이었고 후속 live 쓰기는 고정 내용을 바꾸지 않았다. 이는 연산 수 관측이며 시간 개선율이 아니다. 이전 코드는 segment read마다 다시 열고 검증하므로 같은 시험의 64회는 정적 계산이며 전후 실측은 아니다. 같은 raw slice 두 번 보존은 backing/slice proof 각각 한 번, 새 store reader에서는 다시 한 번 확인했다. 쓰기/삭제 차단·idle 퇴출/교체 거부·사용 중 overflow/close drain·메모리/디스크/부분 spill/예산 부족/취소·descriptor 실패 prefix·기준본 유실 후 결과·private drain·raw peer 재사용·기존 참조 복구도 확인했다.

첫 병렬 build는 compiler 오류 설명 없이 3.92초에 실패했으며 원인은 미확정이다. verbose 직렬 재시도는 변경하지 않은 의존성까지 재컴파일하여 취소했고 제품 writer/provider는 실행하지 않았다. 정확한 취소 경과 시간을 보관하지 못했으므로 이 시도를 제한 내 성공으로 기록하지 않는다. 기존 의존 출력 재사용으로 줄인 Files build 0.85초와 test build 7.35초는 경고/오류 0으로 통과했다. 중간 test build는 timeout cancellation-token 참조 누락으로 6.16초에 실패했고 수정했다. Runtime은 중단된 의존 build에서 컴파일됐으며 Runtime/Files 출력과 최종 test 폴더 DLL의 SHA-256 일치를 확인했다. 전체 solution build 성공이 아니다.

class와 method 필터를 함께 지정한 첫 호출은 0개 선택으로 통과 수에 포함하지 않는다. 초기 샌드박스 시험은 임시 파일 rename 거부/후속 scratch 정리로 8개 중 5개 실패했다. 임시 경로를 저장소 안으로 옮긴 뒤에는 native hardlink 생성 거부 2개가 남았다. 최종 선택 시험은 이 제한 밖에서 `artifacts/capture-read-followup-20261102/tmp` 아래 새 작은 입력만 사용했다. 기존 fixture·Editor·원본 프로젝트·provider 수명주기·대형 실행은 사용하지 않았다. 권한 실패를 제품 guard 변경으로 우회하지 않았다.

사전 예상은 조사 8분/구현 15분/검증 3분/문서 4분, 각 2배에서 중단이었다. build 15/30초, 선택 시험 8/16초를 사용했다. 전체 `build.ps1`, 100k/17GiB, 실제 Git/Plastic/Unity 수명주기, 자원 활용률·peak memory·전체 시간 개선은 미검증이다. 원시 결과와 소스/바이너리 hash는 [수집 읽기 후속](../../../reports/benchmarks/capture-read-followup-20261102.json)에 있다.

### 남은 후보와 쓰기 검토

- 게시 전후 전체 파일 검증은 남는다. 현재 독립 reader는 파일별 native fence를 닫으므로 root hash만으로 그 사이 바이트 불변을 증명하지 못한다. 제한된 연산별 native proof나 동등한 불변 저장소 소유 계약이 먼저 필요하다. 입력 수만큼 핸들을 계속 붙잡는 우회는 채택하지 않았다.
- 전체 catalog 순회·누적 보존·과거 accounting은 남는다. 최신 논리 payload 집합만으로 과거 descriptor의 물리 참조 closure를 증명할 수 없다. 영속 집계/참조 root는 crash-safe 갱신과 세대 삭제 의미가 필요하다. 이번 메모리 집합은 정렬을 줄이며 과거 row 열거 수는 줄이지 않는다.
- 4MiB 조건부 metadata 유지관리와 backend live-repack/segment 복구는 남는다. 새 GC 지연은 응답을 줄여도 비용을 이동하고 공간을 더 보존하며, 진행 중 같은 요청 복구는 미룰 수 없다. 별도 background collector를 추가하지 않았다.
- `JournalDeltaStore.cs`의 `ChangedSlice`/484행 부근 raw 덮어쓰기 비교와 850–916행 부근 commit batching, `WinFspWriteBatch.cs:86,176`은 private 비교와 기존 최대 64개/16MiB 영속 묶음을 유지한다. 순차 callback의 ACK를 다음 호출과 공유할 수는 없다. 기존 raw edge-search는 전체 쓰기 이득 없이 제거됐으므로(`raw-write-batching.md`) 재도입하지 않았다. 신규 쓰기는 이미 raw 저장이다. 개별 쓰기 ACK·충돌·재생 보호를 약화하지 않았다.
- raw 임시 JSON/배열 재사용, 병렬 디렉터리 작업 분배, compact proof 순회 결합, live/result 공통 persistent root는 이번에 채택하지 않은 후보다. 공통 root는 부분 수정 후 full-file SHA-256과 참조 수명을 해결해야 변환/전체 읽기를 없앨 수 있다. 다중 항목 B+ 페이지는 보류한다. 이는 남은 설계/성능 질문이며 현재 비용이 불가피하다는 주장이 아니다.

## ST-65–68: catalog 증명 공유·영속 보존 집합·독립 구성 증명

시작 상태는 `8709f90`과 미커밋 ST-61–64 변경이다. 구현 전후 메인 대화가 해당 커밋에서 대기 상태인 것을 확인했다. 이전 변경을 버리지 않았고 새 workspace·Editor·제품 provider를 시작하지 않았다. 매 checkpoint마다 보관소를 재구축하는 대신 병렬 작업과 명시적 반영을 위한 불변 참조로 결과를 다룬다.

| ID | 변경과 보호 경계 |
| --- | --- |
| ST-65 | `PagedMetadataStore.CatalogReadScope`는 전체 catalog 검증과 논리 바이트 총량을 한 구조 순회로 결합한다. 정확한 reader의 native container fence와 고정 identity/location-header fence 아래 최대 4,096개 하위 트리 요약을 재사용한다. 엄격한 키 순서·디렉터리 decode·child membership·논리/page checksum·catalog count를 유지한다. Container 퇴출 또는 쓰기 공유 읽기는 증명 epoch를 무효화하고 증명이 없으면 정상 순회한다. 전역 writer lock으로 peer 쓰기를 막지 않는다. 범위는 metadata 구성 후 시작하여 게시·집계 동안만 유지한다. |
| ST-66 | `BindRetainedPayloads`는 이전 불변 retained root에서 시작하여 새 변경 전후·범위 기준 의존성의 경로만 복사한다. 기존 길이는 일치해야 한다. 누적 이력 전체를 열거·정렬하여 union을 다시 만들지 않는다. 결과의 전체 retained stream 독립 검증은 유지하며 별도 영속 집계나 복구 상태를 추가하지 않았다. |
| ST-67 | 과거 집계는 최신 retained 집합이 과거 것을 포함하고, 과거 retained 파일 모두의 실제 descriptor가 같은지 먼저 증명한다. Whole→range 전환도 구분한다. 영속 트리 차이 비교는 공유 하위 트리를 실제 native graph 전체 검증 후에만 생략하며 최대 4,096개 작업 범위 비교 root 증명도 같은 fence epoch로 무효화한다. 다르거나 빠진 descriptor·비참조 manifest는 기존 과거 열거를 유지한다. 과거 header마다 소유자·catalog·기록된 generation fingerprint를 확인한다. 중복 집계 기여만 생략하며 GC·별칭 정리·보존/삭제 정책은 바꾸지 않는다. |
| ST-68 | `ResultFileContentStore.VerificationScope`는 먼저 구성 파일 전체 SHA-256을 증명하고 같은 요청에서 최대 1,024개 정확한 hash/descriptor 쌍을 기억한다. 두 번째 검사는 descriptor/range tree를 독립 decode하고 선언된 기준/chunk 모두를 현재 native identity·길이·소유/reference·ready 상태 검사로 다시 연다. `ImmutableReadProofs`가 정확한 쓰기/삭제 배제 backing fence를 계속 소유한 경우만 의존성 해시를 재사용한다. 최대 64개 backing·4,096개 범위 증명이며 fence를 퇴출하고 낡은 증명을 남기지 않는다. 예산 밖은 기존 해시 검사다. 따라서 두 번째 검사에서 전체 파일을 재구성·재해시하지 않고 이미 증명한 구성 해시를 재사용할 수 있다. 외부 Git 기준 구성은 전체 파일 경로를 유지한다. 소유 저장소가 전체 해시를 검증한 whole 객체에는 외부 SHA 순회를 중복하지 않는다. |

Manifest hash 일치만으로 payload를 증명하지 않는다. 독립 게시 영수증·요청/root 결합·수집 경계·게시 순서·영속 ACK·원본/peer 보호·같은 요청 재시도는 유지한다. 새 호출/재시도에는 새 메모리 증명을 사용한다. Canonical byte fence가 계속 있어도 별칭/reference가 남아 있다는 뜻은 아니므로 현재 소유권 검사는 다시 수행한다. 파일 manifest/task/basis/overlay/SPM2/wire/CLI 버전이나 명령을 변경하지 않았다.

### 최소 검증과 관측한 작업량

최종 변경부 시험은 **23/23 통과, 10.087초**, 실패/skip 0이다. 기존 게시 경계 복구 3개는 **3/3 통과, 4.158초**로, **서로 다른 선택 시험 총 26개**다. 전체 gate나 실제 provider/Unity/Git/Plastic 수명주기 자격은 아니다.

새 검사는 31파일 catalog의 반복 증명/총량 재사용·한 leaf 변경 뒤 방문 감소·count 불일치·쓰기 배제·닫은 뒤 손상 거부, writable-container 증명 재사용 없는 fallback, 삽입/변경/삭제 트리 차이, 실제 descriptor/whole→range/retention 누락 집계 구분, whole/raw 구성 파일 증명, byte 증명을 보유해도 현재 소유 reference 제거 거부, 65번째 backing의 해시 fallback과 이후 변경된 바이트 거부를 포함한다. 기존 검사는 private iterator drain·peer raw basis 재사용·기준본 유실 뒤 반복 결과·descriptor prefix 실패/재시도를 유지한다. 추가 복구 3개는 기존 비-native-content fixture이며 native 참조 수집과 basis header 전 실패는 변경부 시험에서 별도로 확인했다.

작은 whole/raw 구성 사례마다 두 번 검증하면서 **구성 파일 해시 1회·의존성 해시 총 2회·구성 증명 재사용 1회**를 기록했다. 동일 catalog 반복은 **추가 node 방문 0회**이며 한 leaf 변경은 최초 31개보다 적게 방문했다. 전체 시간 개선율이 아닌 작업량 계수/assertion이다.

개발 중 실패도 보존한다. Runtime 최초 빌드는 dictionary lookup 뒤 nullable 흐름 오류로 실패한 뒤 정상 컴파일했다. 최초 10개 시험 중 2개는 fixture 오류였다(시험 metadata flush 누락, canonical이 고정되면 별도 소유 hardlink의 unlink도 반드시 실패한다는 잘못된 기대). 현재 검사는 소유권 제거 뒤 재검증이 거부되는 실제 불변조건을 확인한다. 이를 고치기 위해 제품 보호를 약화하지 않았다. 복구 3개는 처음 저장소 내부 임시 폴더를 사용하여 기존 상위 VCS 보호에 수집 전 거부됐다. 새 기본 시스템 임시 입력에서 재실행하여 통과했으며 실패 보고서도 보존한다.

최종 제한 빌드는 기존 의존성 출력을 사용했다. Runtime **5.50초**, Files **3.87초**, tests **7.54초**, 경고/오류 0이다. Runtime/Files DLL과 시험에서 사용한 복사본 hash를 대조했다. 예상/중단 기준은 조사 **8/16분**, 구현 **20/40분**, 선택 검증 **3/6분**, 개별 빌드 **15/30초**, 시험 **8/16초**다. 실행 명령이 중단 경계에 도달하지 않았고 종료할 provider도 없었다. 전체 `build.ps1`·보존 대형 fixture·장시간 성능 실행은 하지 않았다. 원시 보고서와 소스·바이너리 hash는 [참조 증명 근거](../../../reports/benchmarks/reference-proof-20261102.json)에 있다.

### 남은 비용과 자격 경계

최초 소스 namespace 관측·catalog/index 구성·처음 보는 metadata 전체 검증·변경 파일의 최초 SHA-256·현재 소유권 검사·독립 보존에 필요한 모든 payload 의존성은 유지한다. 이력 header와 변경된 트리 경로의 비용도 남고, 실제 참조 구조가 다른 이력은 기존 union 경로를 사용한다. 캐시 부족·writer 공유 container·새 호출에서는 읽기가 반복될 수 있다. 이번 변경은 반복 순회/해시/union 작업을 줄이지만 입력/이력 무관 비용·물리 유지관리 제거·대형 지연/RAM을 입증하지 않는다. 공유 writer 경합·100k/17GiB·전체 gate·실제 Git/Plastic/Unity·OS 중단·전체 시간은 미자격이다. 중단된 대형 fixture는 재개하지 않았다.

## ST-69: ST-65–68 변경 전후 동일 작은 입력 비교

**작업 횟수는 줄었지만 일관된 수집 시간 개선은 입증하지 못했다.** 변경 전은 `8709f90`+ST-61–64, 변경 후는 여기에 ST-65–68을 적용한 상태다. 직전 근거에 기록된 변경 Runtime/Files 소스 6개의 SHA-256을 복원한 기준 소스와 정확히 대조했다. 측정으로 제품 소스를 바꾸지 않았으며 현재 소스는 ST-65–68 근거와 일치한다. 분리한 소스 복사본/바이너리만 빌드했고 checkout·커밋·제품 빌드 출력을 교체하지 않았다.

각 실행은 새로운 **31파일·992바이트** 합성 불변 기준본, 고정 metadata, 같은 파일 offset 2의 2바이트를 여섯 번 연속 수정하는 입력을 사용했다. 실제 `JournalDeltaStore`·native content pin·`FileWorkspaceProvider`로 고정 수집·독립 보존/게시·수집 마무리를 수행했다. **인프로세스 수집 비교**이며 mount된 WinFsp/CLI/provider/Unity 수명주기는 아니다. 편집·최초 fixture 준비·최종 검증은 수집 시간에서 제외했다. 10회(시간 8회·계측 2회) 모두 입력 digest·6개 결과 내용/fingerprint·저장량이 일치했고, 기준본 가용성을 제거한 뒤에도 모든 보존 결과를 독립적으로 읽었다.

### 시간: 비계측 바이너리, 분리한 ABBA 두 블록

A는 변경 전, B는 변경 후이며 아래 표본 하나는 여섯 번 수집의 합계다. 계수와 선택적 generation 로그를 껐다. 각 실행은 새 process/물리 fixture이며 OS 캐시와 배경 부하는 통제하지 않았다.

| 블록/순서 | A 첫째 | B 첫째 | B 둘째 | A 둘째 | 변경 전 평균 | 변경 후 평균 | 차이 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| 1: A1 B1 B2 A2 | 7.178935초 | 6.741428초 | 6.819520초 | 5.379348초 | 6.279142초 | 6.780474초 | **7.98% 더 오래 걸림** |
| 2: A3 B3 B4 A4 | 3.523875초 | 3.403257초 | 3.489949초 | 3.481885초 | 3.502880초 | 3.446603초 | **1.61% 단축**, 0.056277초 |

첫 블록에서 큰 순서 연관 변동을 확인한 뒤 마지막 추가 블록 하나를 먼저 정했고 이후 반복은 늘리지 않았다. 변동의 원인은 분리하지 못했다. 두 블록을 합쳐 일반 개선율로 만들거나 유리한 블록만 보고하지 않는다. 두 번째 절감도 **여섯 번 합계 약 56ms**다.

### 횟수: 후보별 별도 계측 실행 각 한 번

분리한 진단 복사본에만 동일 계측 지점을 추가했고, 해당 실행 시간은 위 표에서 제외했다. 범위는 동일한 수집 여섯 번이며 그 실행에서 누적된 모든 과거 결과를 포함한다.

| 측정 작업 | 변경 전 | 변경 후 | 감소 |
| --- | ---: | ---: | ---: |
| 검사한 catalog 항목 | 2,635 | 1,696 | 35.64% |
| 중복 제거 전 인코딩한 retention 입력 행 | 38 | 18 | 52.63% |
| 집계에 들어간 현재/과거 실제 참조 행 | 133 | 48 | 63.91% |
| Metadata cursor가 반환한 행 | 3,667 | 2,389 | 34.85% |
| 캐시 miss 뒤 metadata page decode | 17,696 | 16,670 | 5.80% |
| 계측한 내용 SHA 계산 | 230 | 128 | 44.35% |
| 해당 SHA가 처리한 바이트 | 15,730 | 13,091 | 16.78% |

Catalog 방문은 일반 catalog 순회와 전용 증명 순회가 실제 방문한 node를 세며 가상 root와 재사용한 하위 트리는 제외한다. Metadata cursor 행과 catalog 방문은 겹치므로 더하지 않는다. Page decode는 OS 읽기 syscall 횟수가 아니다. SHA 계수는 stream payload hash·구성 파일 검증·raw range·불변 증명 miss·source scan·private range 준비를 포함하고 metadata/control/address hash와 별도 journal segment seal hash는 제외한다. 명시한 지점의 실제 계수이며 process 전체 SHA 호출 수가 아니다. 변경 후 catalog 하위 트리 재사용 75회·고정 byte 증명 재사용 69회·구성 증명 재사용 21회를 기록했다. 개별 hash helper 분류가 구현 사이에서 이동했으므로 동일 범주의 합계를 비교하고 helper 하나만 따로 비교하지 않는다.

줄어든 해시 처리량은 **2,639바이트**뿐이고 page decode 감소는 논리 순회 행 감소보다 작다. 진단 귀속에는 결과 scan·raw 준비·descriptor/adoption·영속 게시가 여전히 남는다. 필요 시 남은 비용을 살필 근거지만 flush·CPU·cache warmup·배경 활동이 시간 결과의 원인이라고 입증한 것은 아니다. 중첩 진단 시간은 보고서에 보존하며 더하거나 세 번째 시간 비교로 사용하지 않는다.

### 시간 예산·근거·제한

사전 전체 예상 **18분**, 중단 **36분**: 준비 **5/10분**, 계측 연결 **8/16분**, 비교 실행 **2/4분**, 기록 **3/6분**이다. 개별 빌드 **15/30초**, fixture 준비 **2/4초**, 각 수집 **2/4초**, 여섯 번 합계 **6/12초**, 최종 확인 **2/4초**를 사용했다. 가장 느린 수집은 **2.083215초**였고 10회 모두 각 실행 중단 경계 안에서 완료했다. 취소 token과 단계 경계 검사는 실패 시 후속 단계를 막고 상태를 보존하며 강제 종료·타임아웃 확대는 없었다. Native 실행은 새 시험 hardlink를 위한 sandbox escalation만 사용했다. Provider/Editor를 생성하지 않았고 기존 대형 fixture도 재개하지 않았다.

작은 비교는 정상 완료했다. 보고서 생성기는 처음 Windows 기본 cp949 decode에 실패하여 명시적 UTF-8로 고쳤고 측정 파일은 바꾸지 않았다. 전체 gate·실제 Git/Plastic/Unity·최대 메모리/장치 계측·대형 성능 자격은 추가하지 않는다. 측정에 따른 제품 코드/형식 변경은 없다. Fixture·정확한 소스 manifest·비계측/진단 바이너리 hash·harness/계측 script·원시 결과는 `artifacts/reference-proof-timing-20261102/`에 보존한다. [기계 판독 근거](../../../reports/benchmarks/reference-proof-timing-20261102.json).


## ST-70–72: 수집 비용 분리와 metadata reader 재사용

ST-69에서 줄어든 작은 입력의 계측 해시량은 2,639바이트뿐이었다. 후속 분리 계측에서는 여섯 수집의 **전체 상위 경로 검사 3,186회가 누적 작업 1.993613초**, **영속 flush 256회가 0.793511초**, **계측 파일 열기 2,507회가 0.218703초**였다. Metadata reader 생성은 430회이며 경로/열기를 포함해 0.616842초였다. 페이지 읽기/디코드는 포함 시간 0.619969초, 별도 계측한 하위 작업 제외 0.304777초이며 node 인코딩/쓰기는 0.006729초였다. 작은 해시·논리 순회 감소만으로 시간이 크게 줄지 않은 원인을 반복 metadata 진입/경로 검사와 영속화 비용으로 좁혔다.

이 수치는 귀속 계측이며 wall time의 가산 분할이 아니다. Metadata 포함 시간은 경로/열기와 겹치며 하위 작업 제외 시간도 스레드별 작업의 합이다. 파일 열기/flush 계측은 격리 Runtime/Files의 명시적 FileStream 호출과 지정 metadata 생성자를 대상으로 하며 Platform의 모든 native open이나 전체 process syscall 수가 아니다. 호출 위치 계측은 stack trace 비용이 추가되어 성능 표본으로 사용하지 않는다. 제품 소스와 최종 시간 바이너리에는 계측을 넣지 않았다.

### 구현과 보호

- **ST-70:** 변경 전 소스를 고정하고 상위 경로·metadata 생성/읽기/디코드/쓰기·파일 열기·영속 flush·atomic publication을 분리했다. 메인 대화는 작업 전후 idle/변경 없음이며 기존 ST-61–69 작업을 보존했다.
- **ST-71:** `ImmutableMetadataReaders`가 요청 내 내용 검증 scope에서 metadata reader를 최대 4개 유지한다. Identity와 존재하는 location header의 쓰기/삭제 fence, 기존 reader lease, native container 검증을 유지하며 예산 밖은 기존 단기 reader를 사용한다. 매 content/range open에서 소유 잠금 아래 현재 catalog·object/reference phase·alias/native identity·범위 membership을 다시 읽는다. 불변 페이지 reader만 재사용하며 변경 가능한 소유 판정을 캐시하지 않는다. Peer가 추가한 root를 읽고 보유 store 교체/회수는 차단한다.
- **ST-72:** 읽기 전용 `PagedMetadataStore`는 핸들을 열기 전에 `identity.bin`부터 모든 상위 경로를 한 번 검사하여 바로 앞의 중복 디렉터리 순회를 없앤다. Writer는 디렉터리 생성 전과 identity 접근 전 검사를 유지한다. `PagedSnapshotCatalog`에 호출자 소유 reader를 빌리는 경로를 추가해 파일 manifest catalog/payload index가 같은 reader/cache를 사용한다. 빌린 catalog를 dispose해도 소유 reader는 유지되며 foreign root는 거부한다.

Reader만 재사용한 중간 후보는 열기 수를 줄였지만 시간 개선을 입증하지 못했다. 진단 합계는 기준 3.629636초 대비 3.851095초였으며 이 부정적 관측을 보존한다. 최종 후보는 중복 상위 경로 검사와 catalog/index reader까지 결합했다. 현재 경로/native 검사, metadata hash/count, 같은 요청 복구, 독립 결과, reader/writer fence, 개별 내구성 ACK와 형식은 유지하며 flush/atomic publication을 제거하지 않았다.

### 같은 작은 입력의 최종 비교

새 **31파일·992바이트**, 동일 파일 offset 2의 2바이트 변경 6회, 고정 metadata, 실제 인프로세스 JournalDeltaStore/native pins/FileWorkspaceProvider를 사용했다. 실제 WinFsp mount/CLI/provider/Editor 수명주기는 아니다. 준비·편집·최종 검증은 수집 시간에서 제외했다. 선택적 generation diagnostics도 끈 **비계측 ABBA 한 묶음**의 6회 수집 합계는 **A1 3.388032초, B1 3.157480초, B2 3.157714초, A2 3.321417초**다. 전후 평균 **3.354725→3.157597초**, **0.197128초(5.88%) 감소**다. OS cache/백그라운드 부하는 통제하지 않았으며 이 작은 입력의 제한된 개선을 대형/일반 성능으로 확대하지 않는다.

| 별도 계측한 6회 수집 작업 | 변경 전 | 최종 |
| --- | ---: | ---: |
| 전체 상위 경로 검사 | 3,186회 / 1.993613초 | 2,492회 / 1.538372초 |
| Metadata reader 생성, 하위 작업 포함 | 430회 / 0.616842초 | 280회 / 0.216830초 |
| 계측 파일 열기 | 2,507회 / 0.218703초 | 2,087회 / 0.195904초 |
| Metadata 페이지 읽기/디코드, 하위 작업 제외 | 16,673회 / 0.304777초 | 16,258회 / 0.295833초 |
| 영속 flush | 256회 / 0.793511초 | 256회 / 0.785565초 |
| Atomic publication | 67회 / 0.074369초 | 67회 / 0.073321초 |

최종 진단 합계 3.405184초는 ABBA 시간과 섞지 않는다. 새 물리 identity로 일부 treap node 쓰기/디코드 수가 달라지므로 그 작은 차이를 논리 쓰기 제거로 주장하지 않는다. **8회 실행**(진단 4·시간 4)의 입력 digest·6개 결과 내용/fingerprint·저장량이 모두 같고, 합성 기준본을 unavailable로 바꾼 뒤 과거 결과 전체가 독립 검증됐다.

### 최소 검증·시간 기준·남은 비용

**선택 22개 통과, 실패/skip 0개**다. Reference-proof/raw-basis 21개 8.367초와 reparse-parent 1개 0.258초로 identity/회수 fence·peer 새 root·4-reader fallback·빌린 reader 수명/foreign root·현재 소유권 제거 거부·raw 의존성·보존 내용을 확인했다. 시험이 사용한 Runtime/Files DLL은 빌드 산출물 hash와 일치한다. 기존 의존성 산출물을 쓴 최종 제한 빌드는 Runtime 5.34초·Files 3.95초·시험 7.17초이며 경고/오류 0개다. 개발 중 시험의 cancellation token 및 namespace 누락 컴파일 오류를 수정했다. 클래스/메서드 교집합 필터의 최초 0개 선택은 통과로 세지 않았고 수정한 선택으로 위 시험을 실행했다.

사전 예상/중단: 전체 **32/64분**, 상태/설계 **5/10분**, 진단 **5/10분**, 구현 **15/30분**, 변경부 검증 **4/8분**, 기록 **3/6분**; 빌드 **15/30초**, 시험 명령 **8/16초**, 준비 **2/4초**, 수집 1회 **2/4초**, 6회 합계 **6/12초**, 마지막 확인 **2/4초**다. 가장 긴 수집은 **0.968464초**이며 실행 중 중단 경계 도달은 없었다. 새 fixture/증거를 보존했고 강제 종료·타임아웃 확대·provider/Editor 작업·보존 대형 fixture 재개는 없었다.

남은 실제 비용은 필수 현재 경로/native 검사, 처음 보는 metadata, 최초 catalog/index 관측과 영속 게시다. 추가 절감에는 별도로 입증된 수명/게시 경계가 필요하며 변경 가능한 소유권 캐시나 내구성 약화로 대체하지 않았다. 전체 `build.ps1`, 대형 입력, 실제 Git/Plastic/Unity, OS 중단·경합·메모리 자격은 실행하지 않았다. 원시 소스/probe/harness/결과는 `artifacts/capture-cost-20261102/`에 있고 [기계 판독 근거](../../../reports/benchmarks/capture-cost-20261102.json)가 변경 소스/시험 hash와 결과를 고정한다.


## ST-73–74: 요청 범위 경로 증명과 metadata flush 결합

정확한 ST-70–72 작업 소스에서 이어서 이전 작업을 보존했다. 진입 시 메인 대화는 idle/변경 없음이었다. 보호된 요청 수명 안에서만 증명을 공유하며 기억한 경로나 변경 가능한 소유 catalog를 증명으로 재사용하지 않는다.

**ST-73 — 경로 검사.** 선택적 Runtime `IRequestPathGuard` / `IJournalFilePins.PinMetadataPaths`가 기존 Windows journal 디렉터리 guard를 metadata의 기존 `identity.bin` marker에 적용한다. Native 디렉터리 핸들로 모든 상위 경로의 rename을 막고, 삭제할 수 없는 marker로 전체 경로를 비어 있지 않게 유지하여 reparse 전환을 막는다. 대소문자를 구분하는 NTFS 하위에서도 정확한 경로 표기만 일치시킨다. 변경 가능한 leaf는 매번 검사한다. 이미 고정한 디렉터리 또는 고정한 부모의 직접 자식만 다루고 나머지는 전체 순회한다. Native proof를 얻지 못하면 null/기존 경로로 fallback하며, leaf의 reparse나 접근 오류는 계속 거부한다. 새 소유 marker는 만들지 않는다.

Guard는 `ImmutableReadProofs`의 기존 **최대 4개** metadata reader 중 하나가 소유한다. 매 open의 content/range 소유 잠금·현재 catalog/object/reference phase·alias/native identity·범위 membership은 유지한다. Reader 종료 때 경로 증명을 해제하며 ambient/global 경로 캐시는 없다. 일반 호출과 선택 port가 없는 플랫폼은 전체 경로 검사를 유지한다. `PagedMetadataStore`의 기존 공개 생성자와 wire/CLI capabilities는 유지한다.

**ST-74 — flush 결합.** 새 metadata container는 헤더 쓰기 전에 native owner를 먼저 영속 기록한다. 헤더는 일반 buffered flush로 reader에게 보이게 하고, **root 승인 전 필수 영속 flush**에서 헤더와 페이지를 함께 기록한다. 헤더는 별도로 영속화한 identity의 정확한 사본이며 중단된 소유 prefix를 재개할 수 있다. 성공한 영속 flush는 해당 열린 writer 안에서만 기록한다. 변경 없는 다음 `Flush()`는 이를 재사용하되 기존 `metadata.pages-durable` 승인 경계를 호출한다. 쓰기는 바이트를 내보내기 전에 dirty로 표시하며 flush 실패는 완료 위치를 진전시키지 않는다. 재개한 writer는 새 쓰기가 없어도 첫 flush를 강제한다. Container 전환 전 이전 dirty output을 flush한다. Native owner 영수증, identity prefix/checksum flush, 제어 게시 순서와 개별 작업 ACK는 미루거나 제거하지 않는다.

### 같은 작은 입력 비교

새 process/fixture마다 **31파일·992바이트**, 동일 파일의 2바이트 편집 6회, 고정 metadata를 사용했다. 실제 인프로세스 journal 수집/native pins/파일 결과 보존을 실행했으며 WinFsp mount/CLI/provider/Editor는 만들지 않았다. 준비·편집·마지막 검증은 수집 시간에서 제외하고 ABBA에서는 선택 진단/계측을 껐다.

| 후보 / ABBA 표본(각 6회 수집 합계) | 변경 전 평균 | 변경 후 평균 | 감소 |
| --- | ---: | ---: | ---: |
| 최초 구현: A1 3.227898, B1 3.150454, B2 3.223370, A2 3.288069초 | 3.257984초 | 3.186912초 | 0.071072초 / 2.18% |
| 최종 정확 표기/fallback 보수화: A3 3.995403, C1 3.676519, C2 3.679434, A4 3.755306초 | **3.875355초** | **3.677976초** | **0.197378초 / 5.09%** |

두 번째 묶음은 최종 경로 보호 변경 때문에 실행했다. 후보/묶음을 구분하며 합산하거나 보호 보수화가 더 큰 개선율의 원인이라고 주장하지 않는다. 백그라운드/OS cache는 통제하지 않았다. 절대 이득은 작으며 일반/대형 프로젝트 성능 자격은 아니다.

별도 진단의 6회 수집 작업량/비용:

| 작업 | 변경 전 | 최종 |
| --- | ---: | ---: |
| 전체 상위 경로 순회 | 2,492회 / 1.595034초 | 2,129회 / 1.458398초 |
| 영속 flush | 256회 / 0.788842초 | 240회 / 0.735610초 |
| Atomic 게시 | 67회 | 67회 |
| 추가 native 경로 고정 scope | 0회 | 18회 / 0.015803초 |
| 고정 경로 검사 시도, 범위 밖 포함 | 0회 | 916회 / 0.021957초 |

이 입력에서 줄어든 **flush 16회는 전부 metadata container 헤더의 별도 flush**다. 같은 writer의 변경 없는 반복 flush 재사용은 작은 시험으로 확인했으며 이 입력의 추가 감소량으로 주장하지 않는다. 전체 상위 순회 **363회(14.57%)**가 줄었고 mutable leaf 검사는 남는다. Native guard 자체도 핸들을 열며 비용을 계측했다. FileStream 열기 횟수는 전체 native syscall 수가 아니다. Metadata 포함 시간은 하위 경로/열기와 겹치며 스레드별 합을 wall time 분할로 더하지 않는다. 최종 진단 실행은 **3.550311초**, 기준은 **3.475808초**로 진단 wall time 자체는 개선을 입증하지 못했으며 ABBA에 넣지 않았다.

**11회 실행**(진단 3·시간 8)의 입력 digest·6개 결과 내용/fingerprint·저장량이 모두 일치했다. 합성 기준본을 unavailable로 바꾼 후 보존한 과거 결과 전체가 독립 검증됐다. 중간/최종 원시 결과는 구분해 보존한다.

### 보호 검증과 한계

**고유 변경부 20개 통과, 실패/skip 0개**다. 최종 batching/reference-proof **14개 1.490초**와 관련 복구 **6개 0.639초**이며 최초 13개 결과도 보존한다. 상위 경로/marker rename·삭제 거부, mutable leaf junction 거부, 정확 표기 fallback, 소유 기록을 만들지 않는 proof 부재/미지원, 현재 소유권 제거 거부, 기존/native root, 소유 헤더 prefix 재개, 페이지 쓰기 중단과 최초 native 영수증 gap을 확인했다. 부분 헤더 시험은 중단 모델이며 실제 OS 전원 장애 자격이 아니다. 시험이 사용한 최종 Runtime/Files/Windows DLL은 빌드 hash와 일치한다. 제한 빌드는 기존 의존성을 사용했으며 최종 Windows/시험 빌드는 4.44/7.76초, 경고/오류 0개다.

사전 예상/중단: 전체 **29/58분**, 검토 **6/12분**, 구현 **15/30분**, 비교/검증 **5/10분**, 기록 **3/6분**. 개별 빌드 **15/30초**, 시험 **8/16초**, 준비/최종 검증 **2/4초**, 수집 1회 **2/4초**, 6회 합계 **6/12초**다. 가장 긴 수집 **1.104481초**이며 단계/실행 명령이 중단 기준에 도달하지 않았다. 취소/경계 검사로 상태를 보존하고 후속 작업을 중단하도록 유지했다. 강제 종료·타임아웃 확대는 없었다.

변경 가능한 namespace 가지, 고정 범위 밖의 최초 진입, 영속 owner/control 영수증, identity 초기화와 data-before-root 게시 비용은 남는다. 서로 다른 내구성 경계를 묶으려면 새 복구 증명이 필요하므로 유지했다. 형식 변경·전체 `build.ps1`·대형 fixture·실제 Git/Plastic/Unity·OS 중단·시스템 경합 자격은 추가하지 않았다. [근거](../../../reports/benchmarks/capture-batching-20261102.json)에 소스/바이너리 hash가 있고 원시 fixture/script는 `artifacts/capture-batching-20261102/`에 보존한다.

## ST-75–78: 준비 경로와 node 조회 비용

사용자가 [RECHECK-01/02](scalable-storage-throughput-diagnosis.md)에서 확인한 실제 준비 경로의 병목 수정을 요청했다. 앞선 예상 기반 120초 중단은 과거 절반 시간 기준으로 명시 정정됐다. 시작 소스는 `62c29ed`이며 미커밋 진단/보고서 갱신을 보존했다. Task/basis/overlay·SPM2·manifest·root/hash·명령 형식은 바꾸지 않았다.

| ID | 변경과 유지한 경계 |
| --- | --- |
| ST-75 | 관리형 None 준비는 상위 VCS 검사를 유지하고, 중첩 VCS·`.splinter-apply-` 거부를 `SnapshotInputChecks` admission에 결합한다. 원본 이동 전에 ordinary native root/상위/하위 디렉터리를 고정하여 별도의 파일별 rootward 순회를 대체한다. ADS/hardlink/reparse/type 검사와 상위/결합 순회의 취소 확인을 유지한다. |
| ST-76 | 파일 소스 순회는 열거 metadata를 사용하기 전에 새로 관측한다. 현재 leaf 한 번의 관측으로 속성/길이/mtime과 첫 경로 검사를 결합하며 상위 구성요소는 여전히 각각 현재 속성을 조회한다. `Plain`의 사전 Exists 조회를 없앴다. 내용 hash는 파일을 열어 다시 읽고 새 metadata 관측·stream 길이를 비교한다. Metadata만으로 hash를 재사용하지 않는다. |
| ST-77 | `PagedNodeMap`은 활성 node 복원을 512개·추정 계상 1MiB로 제한해 재사용한다. Save가 항목/계상을 갱신하며 root 변경·게시하지 않고 버린 admission은 무효화한다. 고정 root 열거는 자신의 record를 해석하고 가용성/poison 검사는 읽기 전에 유지한다. 단일 writable·non-derived 저장소의 native reader-fence 세대가 같을 때만 허용하며, 퇴출/append writer rollover는 무효화하고 write-shared 읽기는 해당 store 수명 동안 재사용을 끈다. |
| ST-78 | 후보별 block·소스/바이너리 hash·새 작은 실제 입력·정확한 보존 요청 재개·집중 보존 시험·인증된 정상 Stop을 구분한다. 타임아웃 확대·원본 게시·상태 편집·이관·release/discard/restore·전체 gate는 없다. |

최종 집중 검증은 **고유 54개 통과, 실패/skip 0개**다. 준비/cache/admission·저널 동시 읽기·batch/metadata reader **38개 3.128초**, 파일 source proof **11개 5.044초**, 기존 수집/flush pin **5개 0.455초**다. 캐시 퇴출·rename/write/replay·고정 cursor·poison/중단한 읽기 admission·동시 기준 읽기 세대·예약 이름·ADS/hardlink·취소·native 핸들 퇴출·derived/write-shared 손상 검출·기존 응답 proof 소비/변경 검출을 확인했다. 최초 시험 빌드는 취소 analyzer·nullability 오류가 있었으며 수정한 Release 빌드는 경고/오류 0개로 통과했다. OS 장애·전체 시험 자격은 아니다.

새 실제 None 준비 입력은 **1,000항목·32바이트 파일 999개·디렉터리 1개·내용 16종**이며 seed 생성식/시각/속성은 같다. 최종 순서는 **A4 / D1 / D2 / A5**로 각 trial은 정확한 소유 provider를 정상 Stop하고 데이터를 보존한다. A 준비는 **19.217666 / 9.274589 / 8.995229 / 19.804333초**였다. 전/최종 평균은 **19.510999→9.134909초(53.180721% 감소)**, 기준본 준비는 **2.967092→2.670940초(9.981228% 감소)**다. Editor는 없고 schema/입력 생성/정상 Stop은 CLI 진입점 시간 밖이다. 작은 진단 명령의 취소 상한은 60초로 고정했으며 도달한 명령은 없다. OS cache/배경 부하·생성 native identity가 달라 작은 동일 형태 입력 block의 관측이며 일반 처리량 자격이 아니다.

앞선 두 후보도 별도로 보존한다. A1/B1의 A는 **19.593774→10.599584초**였으나 기준본 **2.998803→3.241405초**는 개선하지 못했다. Native epoch 보강 전 A2/C1/C2/A3 block은 A 평균 **19.797007→9.623646초**, 기준본 **2.928511→2.562497초**였다. Block을 합치거나 빠른 표본을 대신 고르지 않는다.

보존한 100k fixture도 명시적으로 재개했다. Epoch 보강 전 후보에서 같은 A는 원본 재연결 포함 **347.940047초**에 `ready`가 됐지만 이미 끝난 사전검사/baseline을 재사용했으므로 **새 A 전체 준비나 제품 변경만의 전후 비교가 아니다.** 같은 basis의 첫 B 전체 준비는 **521.000061초**로 과거 복구 명령 **2,446.228702초**의 절반(**1,223.114351초**)보다 짧았다. A/B의 basis revision·첫 파일 내용 일치 뒤 provider 3개 모두 정상 Stop했다. 작은 검증/빌드의 동시 활동과 앞선 fixture/cache 이력이 있어 한정 기능/시간 관측이며 해당 patch만의 개선율이 아니다.

**최종 epoch 보호 후보**는 같은 보존 100k basis의 첫 작업 C(`scal-c`, `afdd57d034cacd3c4e37b800`)를 **516.449302초**, 원본 재연결 포함 **524.913097초**에 완료했다. 둘 다 과거 B의 절반 기준 **1,223.114351초**보다 짧다. 전체 작업 사전검사/baseline/내용 검사·응답 proof를 실행했고 C의 basis revision·내용은 A/B와 일치했다. 사용 이력이 있는 basis/원본의 새 task이며 새 기준본 준비나 과거 복구 B와 통제한 인과 비교가 아니다. 최종 C는 앞선 A/B 바이너리 결과와 구분한다. C·원본 인증 Stop 영수증은 **20:10:25 / 20:10:26 +09:00**에 반환했고, 최종 감사는 A/B/C/원본 mount·일치하는 CLI/provider 부재와 사용자 commit 0개를 확인했다. 데이터/key/digest는 모두 보존했으며 release/discard/restore는 없다.

전체 원본/기준본/작업공간 순회와 최초 node 진입은 남는다. C의 보존 index 페이지 파일 길이는 append/COW 이력을 포함해 **1,674,894,227바이트**이며 node 복원 캐시는 조회 비용을 줄일 뿐 이 진입 쓰기 증폭·물리 할당/RSS를 해결한 것은 아니다. 100k 전체 쓰기/수집/계획/반영·17GiB·실제 Git/Plastic/Unity·전체 gate·OS 장애·peak RSS 자격은 추가하지 않는다. [기계 보고서](../../../reports/benchmarks/preparation-bottlenecks-20261102.json)는 최종 소스/바이너리 hash·후보별 block·정확한 보존 경로·54개 통과·Stop 감사를 연결한다. 실패/중간 trial을 포함한 원시 자료는 `artifacts/preparation-bottlenecks-20261102/`에 보존한다.

이후 명시적으로 요청한 [새 100k 재측정(RECHECK-03)](scalable-storage-throughput-diagnosis.md#recheck-03)은 새 기준본/A/B 준비 **43.443820 / 611.553764 / 530.251476초**가 모두 과거 절반 중단선을 통과한 것을 기록한다. 일괄 쓰기는 **1,472.0838445초**에 중단하고 drain 뒤 **1,472.2870604초**에 반환했으며 **98,178 / 99,990파일**을 완료했다. 수집/계획은 NotRun이며 provider 3개 모두 정상 Stop하고 상태를 보존했다. 파일별 flush/index/회수 비용 분석·새 fixture의 한계는 앞선 ST-78 측정과 분리하며 대형 수명주기를 마감하지 않는다.

<a id="st-7982"></a>

## ST-79–82: 혼합 callback 영속화와 제한된 shadow 재생

사용자가 RECHECK-03 병목 수정과 국소 검증 우선을 요청했다. 기존 준비 수정·보존 100k 상태는 유지한다. Task/basis/overlay·SPM2·journal·CLI/wire 형식은 그대로이며 Core 선택 port를 추가했을 뿐 새 공개 CLI capability는 없다.

| ID | 최종 변경 |
| --- | --- |
| ST-79 | `IWorkspaceMutationBatch`가 같은 소유 journal의 동기 범위/metadata 효과를 결합한다. WinFsp는 write·크기/시간·Overwrite·Open을 mutation gate 앞의 같은 FIFO에 넣는다. 최대 callback/chunk 단위 64개·요청 staging 16MiB가 ACK를 공유한다. 큰 callback은 단독으로 기존 chunk 경계를 사용하고 미지원/복합 store는 fallback을 유지한다. 여러 핸들이 열려 있을 때 변경 leader만 100µs monotonic 기한까지 yield하여 다음 변경을 모으며 mutation/journal gate를 잡거나 OS timer sleep을 하지 않는다. 협력적 스케줄링이며 실시간 지연 보장은 아니다. |
| ST-80 | `ExtentList.Truncate`는 private extent root가 그대로이면 추가 node 저장을 생략한다. Entry/base-length/시각 commit과 잘린 tail의 0 채움은 유지한다. Overwrite도 기존 metadata/index batch를 사용한다. |
| ST-81 | Private shadow tail 재생에 기존 제한 node/name/namespace/change-index batch를 적용한다. 전체 catchup/root 비교·native/소유 증명·영속 rotation 뒤에만 게시하며 private 재생은 승인 ACK를 게시하지 않는다. |
| ST-82 | 접수한 입력을 dequeue하기 전에 제한 배열을 할당하고 다음 leader보다 먼저 write 완료 watermark를 게시한다. 중단된 호출자도 drain까지 native 버퍼를 소유한다. 계측은 외부 공유 flush 뒤에 완료하며 비용을 한 번만 계상하고 실패를 유지한다. 작은 literal/runtime·실패·cleanup·전송·packing 복구·reader/cache 검증으로 한정한다. |

최종 Release 빌드는 **16.72초·경고/오류 0개**다. **고유 106개 통과·실패 0개·선택 비용 측정 3개 skip**이며 callback/ACK/resize/cleanup/전송은 **65통과+3skip·4.151초**, 작은 packing 복구 선택은 **13개·14.751초**, cache/native fence/동시 읽기는 **28개·2.792초**다. 새 검증은 실제 열린 핸들·혼합 범위/metadata의 ACK 전 실패·Open 보유·truncate/확장·shadow rename/delete·중단 뒤 같은 요청 복구를 포함한다. 기존 interruption·명시 drain·큰 callback 검증도 통과했다. 전체 gate·OS 장애 자격은 아니다.

작은 실제 native 입력은 fake 고정 기준본·사전 진입한 index·32바이트 파일/16종·같은 쓰기/mtime 작업·준비된 전용 worker 8개다. 매 view를 정상 unmount하고 모든 파일과 영속 seal을 replay에서 확인했다. 준비·진입·dispatch·unmount·replay 검사는 쓰기 시간 밖이다.

**최종 1,000파일 교대 protocol**은 두 새 프로세스가 64파일 단위(마지막 40) 사이에 대기하며 AB/BA를 번갈아 실행한다. 측정 writer는 한 번에 하나다. 실제 쓰기 구간 합계는 **7.566090→6.026501초(20.348538% 감소)**, flush는 **2,829→2,114회(25.273948% 감소)**이며 양쪽 1,000파일 완료와 application 동시 작업 8개를 확인했다. 중단 없는 CLI/전체 수명주기 시간이나 과거 100k 개선율은 아니다.

최종 일반 1,000파일 ABBA도 보존한다. **8.007170 / 9.445883 / 15.730611 / 14.067202초**, 전/후 평균 **11.037186→12.588247초(14.053047% 악화)**이며 flush 수는 줄었다. 동기 주입 편집을 포함한 최종 512파일 shadow 전체 블록도 **12.459031→15.706208초(26.062840% 악화)**였다. 따라서 일관된 전체 블록 시간 개선은 **미입증**이다. 같은 shadow 블록에서 직접 중첩한 편집 callback의 별도 측정 시간을 제외한 회수 평균은 **1.558859→1.041613초(33.181115% 감소)**였다. 새 shadow index 페이지 파일 길이는 양쪽 반복 모두 **15,346,896→1,673,443바이트(89.095886% 감소)**다. 보존 파일 길이이며 누적 쓰기나 물리 할당은 아니다.

중간 근거는 분리한다. 최초 256파일 A/B/C 후보는 native flush 수의 큰 감소 없이 작은 시간 차이만 보였고, 계측 블록의 겉보기 60.4% 개선은 느린 첫 기준 표본의 영향이 커 대표값으로 쓰지 않았다. 1ms timer 후보는 flush를 줄여도 **14.351234%** 느려져 기각했다. 보존한 100µs 후보는 256파일에서 **18.119117%**, 첫 1,000파일 블록은 기준 **19.616421 vs 7.907237초**의 편차와 함께 **53.337436%**, 반복 블록은 **18.826729%** 감소했다. 최종 소스/교대 protocol과 합치거나 불리한 관측을 버리지 않는다.

첫 빌드는 새 시험의 `Rename` 인수 누락으로 **81.275736초** 뒤 실패하여 선언한 **40초** 중단선을 놓쳤다. Probe 재빌드도 **20초** 한도를 넘어 **23.31초**였으며 관측 전에 완료해 종료한 프로세스는 없다. 둘 다 절차 실패로 보존하고 이후 통과로 지우지 않는다. 이후 명령은 build-server 재사용을 끄고 필요 시 시험 참조 재빌드를 제외했다. Sandbox의 원자 rename 거부·중간 계측 record 접근성 오류·flush 전 계측 실패 5개도 최종 검증 전에 바로잡았다. 제품 처리량 개선으로 계상하지 않는다.

**새 probe fixture 56개**에 잔여 view가 없고 최종 감사에 일치하는 probe 프로세스도 없다. 보존 100k/basis/task/journal/요청 identity는 재개·편집·release·discard·restore하지 않았다. 수집/원본 반영·17GiB·Git/Plastic/Unity·전체 gate·peak RSS·일반 단독 writer 지연 자격을 추가하지 않는다. [기계 보고서](../../../reports/benchmarks/write-bottlenecks-20261102.json)는 최종 소스/바이너리 hash·모든 후보 블록·교대 단계·106개 통과·실패/중단선 누락·종료 감사를 연결한다. 원시 근거는 `artifacts/write-bottlenecks-20261102/`에 보존한다.

## 이후 명시적으로 요청한 100k 재개(RECHECK-04)

앞선 ST-79–82 국소 검증에서는 보존 fixture를 재개하지 않았다. 이후 사용자가 [2026-11-02–03 재개](scalable-storage-throughput-diagnosis.md#recheck-04)를 명시적으로 요청했다. 같은 최종 후보의 **소스/config hash 328개·바이너리 hash 32개가 일치**한다. **99,990파일 전체**에 새 쓰기 한 회를 실행하여 쓰기 **1,254.7332609초**, **최초 실패/재연결 비용까지 보수적으로 포함한 수집 707.5361660초**(성공한 수집 명령 **440.5851863초**), 읽기 전용 원본 계획 **464.9044448초**에 완료했다. 각각 과거 절반 중단선 안이며 과거 계획 시간은 중단 하한이다.

기존 private 범위/이력/준비를 재사용하여 새 ABBA나 패치 인과 개선율은 아니다. 최초 `SourceCaptureUncertain`은 실험 직접 재시작이 작업 기록의 이전 PID/시작 lease를 유지해서 발생했으며, 정확한 같은 ensure·불변 요청으로 복구했다. 쓰기 반복이나 수동 상태 편집은 없다. 합산 재연결 **304.9382314초**는 새 기준본/A 준비 시간이 아니다. 잦은 영속 flush·범위 재배치/index 증폭·보존 선택에서 파일마다 `baseline.Find`가 catalog를 다시 여는 비용·현재 원본 metadata 순회/index 구성이 남은 후보다. 정확한 counter·stack·자격·identity는 [RECHECK-04](scalable-storage-throughput-diagnosis.md#recheck-04)와 [기계 보고서](../../../reports/benchmarks/throughput-resume-st79-20261103.json)가 소유한다.

계획은 **변경 99,990개·충돌 0개**이며 이후 원본 반영은 실행하지 않았다. 인증된 정상 Stop 뒤 Provider/view 3개가 모두 없고 불변 결과 1개를 보존하며 미완료 capture/원본 반영 intent는 없다. 관리형 원본은 중단 상태이지 복원한 것이 아니다. Release/discard/restore·전체 수명주기/gate·고유 대형 asset/17GiB·Git/Plastic/Unity·OS 장애 자격을 추가하지 않는다.
