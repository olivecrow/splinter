# 수집 성능과 저장 구조 검토 — 2026-11-02

구조 구현 후속: [ST-65–68](scalable-storage-throughput-followup.md)은 native fence 아래 catalog 요약/총량, 영속 retained 집합의 추가 갱신, 실제 과거 참조 포함 증명, 독립 재검증하는 구성 증명을 연결했다. 선택한 작은 시험 26개는 해당 경계만 확인하며 최초 inventory·최초 해시·fallback·대형 입력 문제는 남는다.

구현 후속: [소유 검증 문서](scalable-storage-throughput-followup.md)의 ST-61–64가 제한된 backing/slice proof 재사용과 retained root 재정렬·작은 입력 accounting 정렬 제거를 연결했다. 남은 후보는 그 문서에 명시하며 아래 분석은 당시 관측을 보존한다.

[English](../../verification/collection-performance-architecture-review-20261102.md)

## Astra 재검토 — 최신 판단이 우선함

이 절은 이전 분석을 삭제하지 않고 현재 코드로 다시 검토한 결과다. 아래의 최초 01:59 관측에서 “구현 중/검증 미완료”라고 한 항목은 이 절의 새 상태로 읽는다. 실행 모델은 GPT-6 Astra이며 reasoning effort 설정은 도구에서 별도로 조회하지 못했다. 목표는 실제 `create_goal`로 다시 활성화했다. 제품 실행·성능 시험 없이 코드와 메인 대화의 최신 상태를 확인했다. 메인은 작은 전체 경로 통과와 수집 16.50→12.77초, 전체 85.08→69.45초를 보고했다. 전후 각 1회라는 한계와 실제 측정 파일을 이번 분석에서 재감사하지 않았다는 한계를 유지한다. 실패 명령 하나만 관측했던 최초 상태는 최신 성공 보고를 부정하는 근거가 아니다. 공유 소유 문서가 계속 작성 중이므로 코드 구현 확인과 검증 보고를 구분한다.

### 이전 보고서에서 바로잡거나 구체화한 내용

- **별칭 정리 이동 ≠ 모든 GC 제거.** `FileSourceGenerations.cs:129`는 수집의 `PrunePayloadAliases` 호출을 제거했다. `SnapshotWorkspaces.cs:203–205`는 release에서 별칭 유지관리 후 강제 metadata 유지관리를 한다. 그러나 `WorkspaceGenerations.cs:100` → `WorkspaceMetadataMaintenance.cs:22` → `FileMetadataMaintenance.cs:51–67`은 수집 응답 전에 여전히 조건부 유지관리를 수행한다. workspace/shared/content/ranges 중 한 저장소가 새 metadata 4MiB 또는 pending collection 조건을 만족하면 `all` 범위를 요청한다. 따라서 “수집에서 정리가 없어졌다”는 말은 별칭 정리에 한정해야 한다. 작은 입력에서 임계값 미도달은 대형에서 유지관리 비용 부재의 증거가 아니다. 기존 pending intent는 같은 요청 복구를 유지하고, 새 회수만 응답 밖으로 옮길지 검토한다.
- **과거 결과 순회는 accounting에도 남는다.** `FileSourceGenerations.cs:124–129`의 `Retained()`는 과거 generation manifest를 열고, `FileWorkspaceProvider.cs:455–477`의 `Storage()`는 각 `ContentReferences`를 정렬·중복 제거한 뒤 basis/result의 전체 length 합계도 다시 계산한다. 정리 호출 제거로 이 비용까지 없어지지 않는다. 이 경로에서 호출자가 ct를 전달하지 않고 sort도 기본 ct를 사용하므로 취소 지연도 점검 대상이다. 정확한 root별 길이·참조 집계 재사용은 후보이며 단순 합산은 공유 chunk 중복 계산을 유발한다.
- **보존 축소는 최신 변경분만이라는 뜻이 아니다.** `FileReferenceResults.cs:16–48`은 직전 retained 집합 전체와 현재 기준 대비 변경 전후 및 부분 읽기 의존성을 합친다. `RequiredBasisPayloads:98–104`와 `VerificationPayloads`도 이 누적 집합을 처리한다. 세대마다 고유 payload d개가 추가되는 경우 집합 크기 H(g)=O(gd), 과거 집합을 다시 열거하는 accounting 입력은 O(dg²)까지 될 수 있다. 이는 조건부 작업량 계산이며 실측이나 모든 workload의 복잡도가 아니다. 최신 집합만으로 과거 descriptor가 참조하는 물리 backing도 모두 포함되는지는 별도 증명이 필요하므로 과거 순회를 바로 삭제할 수 없다. 같은 논리 hash라도 descriptor/root와 raw/whole 물리 참조가 다를 수 있다.
- **64개 게시의 원자성은 capture 전체가 아니다.** `ResultFileContentStore.PreservationBatch:51–77`은 이미 게시한 prefix를 보존하고 미게시 pending root를 Dispose에서 게시하지 않는다. 이전 보고서의 “묶음 전체 ACK”는 capture 전체 rollback을 의미하지 않는다. 완성 영수증은 모든 필요한 batch와 validation 이후여야 하며 65번째 실패 시 앞의 64개를 버리지 않고 같은 요청으로 재개하는 것이 최소 검증이다. private drain은 `FileSourceGenerations.cs:392–404`에서 이미 단일 iterator로 구현되었다. 이를 새 최적화로 제안하지 않는다.

### 남은 비용의 더 구체적인 근거

| ID/우선 | 현재 코드·호출 요인·판정 | 필요한 보호와 다음 최소 확인 |
|---|---|---|
| A1 / 높음 | `JournalPagedCapture.cs:187–250`: 64KiB read마다 EnsureAvailable/Resolve/Node/floor 탐색을 하고, segment-backed extent마다 OpenBacking→native identity/length 확인→닫기를 한다. 큰 한 extent를 순차 읽어도 호출마다 다시 연다. 최대 대략 ceil(B/64KiB)번/단일 extent의 열린 핸들 비용이 생길 수 있다. 순차 stream 전용 extent cursor와 제한 backing-handle 캐시가 후보다. | 같은 capture root/native identity·reader lifetime에만 캐시하고 종료 drain·GC fence·replacement 검출 유지. 작은 2MiB 연속 read와 여러 extent, 삭제/rename/native 교체를 비교하며 open/read/page 수를 센다. 숫자는 계산 예이며 현재 실측 아님. |
| A2 / 높음 | `OwnedRangeContentStore.cs:49–114,214–231`: raw 준비에서 이미 해시한 범위를 `VerifySource/HashRange`가 다시 읽는다. whole backing은 8-entry proof LRU로 hash 재사용하지만 range hash는 요청마다 재계산한다. `AlreadyReferenced`보다도 source 검증이 먼저다. 기존 raw 객체는 공용 catalog lock 안에서 `OpenLocked`로 또 hash 확인할 수 있다. source/hash 준비와 영속 참조 증명이 서로 다른 신뢰 경계이므로 중복이라는 이유만으로 제거하면 안 된다. | 검증 증명을 정확한 handle/offset/length/hash에 묶어 전달하는 설계와 현재 범위 재검증을 비교. source 변조·LRU 퇴출·기존 object 변조·응답 유실 검사를 포함. 전체 backing hash와 slice hash는 목적이 다르며 하나가 다른 하나를 자동 증명하지 않는다. |
| A3 / 높음 | 위 accounting와 누적 H, raw 준비/descriptor의 별도 전체 N 열거는 CPU·metadata 비용을 늘린다. `ScanTree`의 unchanged hash 재사용은 이미 있으므로 모든 파일을 항상 내용 hash한다고 설명하면 틀리다. | 0개/1개/여러 세대 변경에 대해 N, R, H, 읽기 바이트, sort row 수를 분리. 영속 catalog를 변화시킨 경로만 갱신하는 설계는 rename 하위 전체·삭제·metadata-only·same-byte overwrite까지 포함해야 한다. |
| A4 / 중간 | `PagedMetadataCompactProof.cs:32,41,65–71`은 논리 순회, reference 재순회/정렬, 물리 순차 reread/hash를 한다. reader cache로 일부 재독이 줄어도 마지막 물리 read는 명시적이다. ST-53은 복사를 없앴으나 proof를 O(1)로 만든 것은 아니다. | builder가 flush 후 제공하는 소유 페이지 집합과 full graph/native proof를 같은 fence에서 결합할 수 있는지 설계. unreachable tail·child·unknown·pending collection·손상 검출을 모두 유지. writer의 자기 주장만으로 independent proof를 대체하지 않는다. |
| A5 / 중간 | `NtfsSourceGenerations.cs:68–113`: live 완료는 complete-source IPC 응답을 100ms로 기다리고 10회마다 provider 확인한다. 이는 ST-55가 바꾼 source-change polling과 다른 루프다. stopped는 정확한 부재/소유 확인 뒤 직접 journal을 열어 adoption, capture complete, `MaintainLiveStorage(collectSegments:true)`까지 수행한다. | 두 대기를 혼동하지 말고 IPC wait/adoption/live maintenance를 따로 계측. 새 쓰기 조건부 교체, 마지막 flush와 idle reader 해제, pending live-repack/segment-reclaim 동일 요청 복구를 유지. 이벤트 알림은 잃어버린 wake에도 같은 요청 조회가 가능해야 한다. |
| A6 / 낮음 | `FileRawRangePreparation.cs:58–74`의 1MiB 배열 생성, JSON row 왕복과 정렬; `JournalPagedCapture.cs:229`의 encoded block 전체 임시 배열/decoder 호출이 남는다. raw 신규 쓰기와 기존 encoded 데이터 경로를 구분한다. | ArrayPool/재사용 scratch는 수명·동시성·예외 반환을 검증하고 allocation/GC time부터 측정한다. 메모리 할당 감소를 디스크 시간 감소로 주장하지 않는다. |

### 자원·병렬화와 표현 선택 재판단

`OwnedRangeContentStore`는 source proof를 공용 catalog lock 전에 수행하지만 동일 instance의 `proofGate`는 보존 batch를 직렬화한다. worker만 추가하면 이 직렬점을 통과하지 못한다. 독립 검증/읽기를 제한 병렬화하고 검증 완료된 증명만 ordered publisher에 넣는 구조를 비교해야 한다. directory depth 분할은 초기 분배일 뿐 work stealing·바이트 기준 작은 작업·결정적 merge가 함께 있어야 부하 불균형을 다룬다. Core 포트가 동시 읽기를 허용하지 않는 backend는 serial fallback을 유지한다. 검사/변환/게시를 겹칠 때도 실패 후 시작 금지·drain·ACK 선행 flush가 필요하다.

4GiB 허용은 사용할 수 있는 여유이지 반드시 써야 할 목표가 아니다. 현재 목표는 가용량의 1/8이므로 환경에 따라 4GiB보다 작다. `MetadataMembership.cs:17,27–46`은 공통 scratch뿐 아니라 SortRunBytes(최대 128MiB)로도 제한한다. key가 r/w+64hex, value가 8바이트라면 자체 계상은 entry당 650바이트여서 최대 run 예산에서 약 206k entry 뒤 spill할 수 있다. 실제 managed 메모리나 항상 spill하는 시점의 실측은 아니다. 전체 4GiB가 여유여도 이 구조가 디스크 fallback할 수 있으며, 예산 재배분은 cache hit/peak/GC/spill 동시 측정 뒤 판단한다. 임시 대용량 사전 할당은 필요 없다.

공통 immutable root 방식은 full SHA-256(file bytes) identity 요구도 검토해야 한다. 일부 범위가 바뀌면 현재 full-file SHA-256은 일반적으로 이전 digest와 변경 digest만 결합해 구할 수 없어서 파일 전체 재독이 남는다. Merkle extent/chunk root를 내부 상태 identity로 쓰고 전체 SHA를 필요한 API/반영 경계에서 계산하는 대안은 hash 계약·동일 내용의 서로 다른 chunk 경계·canonical identity·충돌/복구를 함께 바꿔야 한다. 기존 의미를 유지한 채 “persistent root만 pin하면 모든 비용이 사라진다”고 할 수 없다. 더 작은 안전한 첫 단계는 동일 capture에서 이미 계산한 full hash와 immutable reader proof를 재사용하는 것이다.

재판단 우선순위는 반복 전체 바이트 검증/핸들·metadata 조회(A1/A2), 누적 참조 accounting 및 전체 scan(A3), 조건부 maintenance와 proof(A4/A5), 마지막으로 임시 직렬화·할당(A6)이다. 우선순위는 정적 비용 경로에 따른 후보이며 현재 배타 시간 순위가 아니다. 이전의 다섯 분야 체크리스트는 이 보완과 함께 모두 답변되지만 모든 함수가 미시 최적화 완료인지 실증했다는 뜻은 아니다. 실제 활용률과 syscall/flush 수의 정확한 실행별 분해는 계측 없이는 미확인이다. 별도 대형 실행을 하지 않고도 이 경계까지는 코드로 확정할 수 있다.


## 범위와 결론

2026-11-02 01:59 KST에 HEAD `1b33995955f4e7b77b1ce416c8a99be7e6e80116`과 미커밋 작업 트리를 읽었다. 메인 대화 `01a19644-3877-7a20-a47c-65d095af8880`는 구현 중이다. 시작 시와 마감 직전 읽기 전용으로 확인했다. 최신 메시지는 변경부 검사 15개 중 14개 통과, 나머지 테스트의 핸들 수명을 수정하고 32항목 전후 경로를 실행 중이라는 내용이다. 마감 확인 시 새 명령 하나가 실패 상태였으나 내용은 확인하지 못했다. 최종 성공으로 판정하지 않는다. 이 문서의 줄 번호는 그 시점의 움직이는 작업 트리 기준이며 후속 편집으로 달라질 수 있다. 마감 시 `contracts/v1/file-source-manifest.schema.json`도 메인 작업에서 수정된 상태였다. 제품 실행·대형 fixture·build/test는 수행하지 않았다.

결론은 **현재 코드가 최적화 한계에 도달했다고 입증되지 않았고, 전체 데이터 이동량·중복 증명·직렬 순회부터 계측해 제거할 여지가 있다**는 것이다. 반대로 어떤 후보가 실제 전체 시간을 얼마나 줄일지는 아직 모른다. 전체 snapshot 보존과 원본 덮어쓰기는 별개다. 원본과 peer의 편집을 지키려면 결과·기준·현재 원본의 충돌 판단이 필요하다. Git도 snapshot 모델이며 매 편집 이력을 저장해야만 하는 것은 아니다.

메인에서 이미 다룬 전체 기준본 payload 축소, 64개 묶음 게시, 수집 응답의 참조 정리 이동, private 범위가 완전히 덮은 기준 읽기 제거는 **구현 중/검증 미완료**다. ST-53–55의 compact proof, membership, 대기 조정은 코드와 기존 보고서에서 확인했다. 아래 후보는 이 변경들을 다시 새 발견으로 제시하지 않는다. 참고: [후속 검증](scalable-storage-throughput-followup.md), [기존 진단](scalable-storage-throughput-diagnosis.md), [계측](../../../reports/benchmarks/capture-maintenance-20261102.json).

## 호출과 작업량

N=파일·디렉터리 수, D=디렉터리 수, R=private 범위 수, B=읽는 바이트, H=보존 결과 참조 수, P=metadata 페이지 수. 다음은 코드상 상한/반복 관계이지 실측 시간 모델이 아니다.

| 단계와 위치 | 반복·비용 및 경계 | 상태 |
|---|---|---|
| `WorkspaceGenerations.cs:90` → `FileSourceGenerations.cs:25–121` | 고정 요청/재시도 확인 → basis/result → 보존 → 독립 검증 → boundary → 결과/영수증 → 재검증. 재시도는 동일 identity를 확인하고 검증한다. | 관찰 |
| `FileSourceGenerations.cs:47,69–84,158–205`; `JournalPagedCapture.cs:115–147` | 첫 basis는 전체, 이후 고정 basis 재사용. 결과는 DFS 전체 N, 각 디렉터리 목록을 정렬·병합. 파일마다 layout/기준 조회; 완전 동일 범위나 인증 summary는 hash 재사용, 그 외 전체 내용 읽기/hash. 결과 디렉터리 깊이가 깊거나 넓으면 정렬·트리 탐색 부담. `EnumerateChangedPaths`는 존재하지만 전체 scan 대체가 아니다. | 잔여 비용, 호출별 실제 syscall/바이트 미계측 |
| `FileRawRangePreparation.cs:37–115` | 결과 전체를 훑고 private 범위를 최대 1MiB 단위로 분할; 전체 파일 하나와 일치하면 기존 hash, 나머지는 읽기/hash. backing identity/hash와 물리 offset 순 정렬, JSON 임시 row, 기존 whole 참조 일괄 확인, raw 보존, 다시 path/offset 색인 정렬·구축. 기존 backing 묶음은 이미 구현됨. | R·B·sort/spill; 메인과 독립된 잔여 분석 |
| `FileSourceGenerations.cs:299–407`; `ResultFileContentStore.cs:72–105,153–184,211` | 전체 결과 파일을 다시 열거, 범위 검사·adoption spool·descriptor 구성. 기존 descriptor도 composed 내용을 hash 검증; 새 descriptor도 구성 후 전체 파일 hash. 변경 root 최대 64개 뒤 flush/게시하는 코드는 메인 진행 중. adoption row는 별도 정렬/build/flush. | 파일별 재구성 B, 페이지 및 durable flush; 중복 정도 미계측 |
| `CapturedRangeAdoptionStore.cs:21–35`; `PagedMetadataCompactProof.cs:12–67` | adoption root 생성·flush·artifact durable 게시 후 전체 root/native owner/물리 연속 피복을 다시 확인. 적합하면 복사 회피, 미완료 collection/unknown 등은 기존 회수·보존/거부. proof 자체는 P와 정렬 비용이 있다. | ST-53 반영 확인; 제거할 수 있는 proof라고 단정 불가 |
| `FileSourceGenerations.cs:87–121,140–157`; `FileWorkspaceProvider.cs:479` | 독립 reader에서 선택 payload 검증, 결과 및 영수증 게시 뒤 `VerifyGenerationCoreAsync`가 다시 payload를 검증. 두 호출은 각각 최대 선택 바이트를 읽을 수 있다. 실제 중복 바이트/캐시 hit 미측정. Git은 별도 Git 검증 경로. | 잔여 후보; 실패/재시도 경계 다름 |
| `JournalRangeAdoption.cs:10–81,106–122` | 각 adoption row의 고정 block/길이 확인 후 고정 소스와 보존 내용을 64KiB씩 각각 읽고 비교. 최대 write batch로 live gate에서 여전히 같은 block인 조각만 교체; 최종 flush/root/segment, idle reader 해제. 현재 비교 루프는 직렬이며 비동시 basis는 별도 gate. | 후속 쓰기 보호 필수, 비교 중복은 가설 |
| `FileSourceGenerations.cs:426–482`; `MetadataMembership.cs:17–56` | 정리 경로는 live/pending/basis/현재 및 과거 결과 참조를 drain하여 membership을 만들고 alias를 검사한다. 메모리 예산 초과 시 정렬 디스크 트리. 매 수집인지 명시 유지관리인지는 메인 변경 완료 후 재확인. | ST-54 반영, 메인은 critical path 이동 중 |

## 질문별 판단과 안전 경계

1. **순회·변경 감지·보존.** 100k 반복만으로 수십 분을 설명할 수 없다. 위 경로는 파일별 tree lookup, 디렉터리별 sort, 필요 시 읽기·해시, 여러 root build와 영속 게시를 수반한다. 일정 깊이로 고정 분할하면 한 거대 디렉터리나 큰 파일에서 불균형이 생긴다. 동적 큐로 하위 디렉터리·큰 파일 단위를 공급하고 결과는 경로 정책에 맞게 결정적으로 병합하는 것이 후보다. 현재 `ScanTree`는 단일 DFS이며 `BoundedParallel.cs:37`의 동적 partition은 이를 자동 병렬화하지 않는다. 먼저 디렉터리당 항목·대기·파일별 바이트와 큐 깊이를 관측해야 한다.

   변경 색인은 정확한 고정 sequence까지 누락 없이 기록되고 rename의 양쪽 경로·삭제·부모 메타데이터·재시작 tail·외부 우회 쓰기까지 포함한다는 증명이 있을 때만 전체 관측을 대체할 수 있다. journal gap/unknown은 전체 재관측 또는 거부다. 크기/mtime/dirty만으로는 동일 크기 수정, 시각 복원, 외부 쓰기를 감지하지 못한다. 고정 시점의 namespace와 내용이 같은 fence에 속해야 한다. 불변 directory root hash가 같으면 해당 subtree를 건너뛰는 접근은 `FileReferenceResults.cs:51`의 차이 비교에 이미 나타나지만 전체 capture scan을 생략한다는 증거는 아니다. 기준 참조는 결과를 workspace 회수 후 읽고 반영/되돌리기에 필요한 의존성까지 pin해야 한다. 메인 v4 보존 선택의 rename/부분 쓰기와 과거 결과 보호는 진행 중이다.

2. **raw/descriptor.** backing별 정렬과 batch는 이미 있지만, range별 읽기/hash, JSON row 및 두 정렬·임시 트리와 descriptor 재구성 검증이 남는다. 고정 backing의 검증된 범위 증명을 offset/length/hash와 reader lease에 결합하면 같은 바이트의 재독을 줄일 가능성이 있다. 연속 offset을 합쳐 순차 I/O하고 여러 독립 backing을 제한 병렬화하는 방안은 디스크 특성·공유 lock·메모리 압력을 먼저 측정한다. descriptor는 basis hash/길이/가시 길이와 private chunk offset·길이를 합성하므로 완전 복사보다 표현력이 있으나 다수의 작은 범위는 key/page·hash·spool 비용이 커진다. 인접하고 같은 backing/검증 경계를 가진 범위 병합과 임시 binary row는 후보이며 바이트·충돌·복구 시험 전 효과 미상이다. 기존 descriptor 재사용 시에도 입력 iterator를 끝까지 drain해야 마지막 범위 오류, 보존과 adoption row를 놓치지 않는다. descriptor build만 생략하고 validation/adoption을 한 번 스트리밍하는 방법은 가능성이지 현재 증명이 아니다.

3. **게시와 독립 검증.** 생성된 root의 내용 hash가 같다는 사실과 실제 독립 reader가 열 수 있고 native backing이 소유·온전하다는 사실은 다르다. 같은 고정 root, reader lease, native identity 및 소유 fence가 유지되는 동안 검증한 불변 payload/page 증명을 재사용할 수 있는지 검토한다. 게시 직전/후에는 root와 요청 digest의 결합, durable data-before-root, receipt의 실제 게시, 가변 provider 가용성·소유·외부 변경 및 오류를 새로 확인해야 한다. 따라서 사전 검증과 사후 검증 전체를 무조건 합치면 안 된다. 독립 reader로 사전 검증한 결과를 봉인된 증명으로 묶고 사후에는 게시된 root/receipt와 fence 연속성만 검사하는 설계가 후보다. 대형 검증은 독립 subtree/range 단위로 bounded 병렬화 후 모든 실패를 drain하고 하나의 결정적 결과로 결합할 수 있으나 공용 reader의 동시성 및 lock은 별도 증명해야 한다. 파일별 durable publication의 64개 묶음은 메인에서 구현 중이며 실패 시 묶음 전체 ACK/재시도를 확인해야 한다.

4. **참조·별칭.** 위 membership은 lookup을 줄이나 참조 자체의 전체 열거를 없애지 않는다. 작업별 immutable root와 generation별 참조 색인, 증분 집계 또는 root-diff로 변경분만 계산하는 것은 후보다. 삭제 전 pending·live·과거 결과를 포함한 정확한 집합과 상충 길이 검증, root 고정 및 crash-safe 증분 업데이트가 필요하다. reader 고정 후 병렬 열거와 local set 병합은 가능성이 있으나 공용 저장소 잠금/색인 병합이 병목일 수 있다. GC/repack을 응답 밖으로 옮기면 지연은 줄 수 있지만 총 작업은 줄지 않으며 공간 압력과 유지관리 지연이 늘 수 있다. 실행 중 회수는 같은 요청으로 먼저 복구해야 한다. GC 없이도 결과와 peer 참조가 유지되고 누수/정리 지연이 관측되는지 확인해야 한다.

5. **backend와 전환.** 고정 원본과 보존 chunk를 매 row 대조하는 adoption은 손상·잘못된 참조 방어이지만 동일 immutable backing과 slice에 대한 검증 증명이 양쪽에 이미 연결됐다면 바이트 재비교를 줄일 여지가 있다. hash만 같다는 가정으로 생략하지 말고 backing identity, offset, 길이, hash, fence 및 원래 검증 경로를 함께 고정해야 한다. 준비·검증을 bounded pipeline으로 겹치고 독립 backing을 batch할 수 있으나 순서에 따른 publication과 실패 drain이 필요하다. live는 비교 중 새 쓰기가 가능하므로 token/offset이 여전히 일치하는 조각만 gate 안에서 교체한다. stopped 경로는 동시 writer가 없다는 정확한 종료·소유 증명이 있어야 더 단순한 경계를 고려할 수 있다. `WindowsSourceChanges.cs:51–88`은 최초 100ms 동안 20ms, 이후 100ms polling, 장기 대기 1초 provider proof다. 이벤트 wake 후보는 응답 유실과 process identity를 시험해야 하며 polling 비용이 지배적이라는 측정은 없다. Git 외부 프로세스, Plastic 직렬 revision 및 각 backend 잠금의 현재 호출 횟수·대기 분해는 미측정이다.

## 구조 대안과 우선순위

| 우선 | 후보 및 최소 검증 | 메인과 관계/미확인 |
|---|---|---|
| 1 | 고정 reader 아래 pre/post 내용 증명 공유: 작은 파일·변조·교체·receipt 실패·같은 요청 재시도에서 읽기/해시 횟수와 bytes 비교. | 메인 변경 후 재측정; 회수 후 독립 가용성 필수 |
| 2 | 정확한 root diff와 변경 저널로 전체 scan 축소: rename/delete/restart/gap/외부 쓰기·동일 mtime/크기 및 원본/peer 동시 편집을 작은 fake와 고정 fence에서 비교. 누락 시 fallback. | 현재 변경 색인과 보존 선택은 기반, 전체 대체 미구현 |
| 3 | raw 정렬/JSON/중복 읽기와 descriptor 범위 병합 비용을 각각 count; 작은 다중 backing·overlap·오류 마지막 row·중단 재개로 byte exact 비교. | 기존 backing batch와 drain을 중복 제안하지 않음 |
| 4 | 동적 directory/대형 파일 큐 및 병렬 독립 검증: 불균형 입력, 1/2/4 worker, 동시 불가 backend fallback, peak memory·I/O queue·CPU·대기 관측. | 병렬도 숫자는 활용률 증거가 아님 |
| 5 | 작업별 참조 root/diff와 유지관리 분리: 두 결과·live 후속 쓰기·동일 요청 회수 중단·공유 peer 참조 검증; 응답 시간과 수명 전체 I/O/공간을 함께 기록. | 메인 정리 이동과 구분: 총량 절감은 미검증 |
| 6 | live와 결과를 공통 불변 chunk/sealed segment + persistent 파일 상태 root로 표현하고, 데이터 durable 후 root 및 필요한 참조를 고정하는 구조를 설계 비교. | 기존 range reference·불변 root·shared backing은 이미 존재. 빠진 것은 live root를 독립 결과로 안전하게 pin하고 소유/GC/복구/원본 비교와 연결하는 계약의 입증이다. 형식 교체 아님 |

이 공통 root 모델은 전체 순회→변환→descriptor→adoption map→live 참조 교체 일부를 없앨 가능성이 있으나, 결과의 회수 후 가용성, 후속 쓰기의 copy-on-write, 원본/peer 보호, 기준·결과·현재 비교, 동일 요청 재시도와 데이터 선행 영속화를 모두 만족해야 한다. 전체 독립 payload는 논리적 요구가 아니라 구현 선택이다. 변경 파일·이전 내용·이름 변경/부분 쓰기 의존성을 보관하고 나머지를 불변 참조로 유지할 수 있지만 그 참조 수명과 원본 변화·최종 restore를 증명해야 한다. 메인 작업이 이 방향의 일부를 구현 중이며 이 문서는 완료를 주장하지 않는다.

단순 복사는 대략 N개의 namespace 처리와 B바이트 읽기·쓰기다. 여기서는 경우에 따라 추가로 전체 N scan, 선택 B의 hash/보존 및 독립 검증 2회, R개 범위의 양쪽 읽기, metadata 페이지/정렬/내구 flush와 reference inventory가 더해진다. 각각이 항상 모두 실행되는 것은 아니다. 100k 파일에 파일당 1ms의 추가 직렬 비용만 있어도 계산상 100초다. 이는 관측치나 현재 속도 예측이 아니다. 더 많은 보호가 필요해도 수십 분 지연을 당연한 것으로 정당화하지 않는다.

ST-52의 단순 canonical byte-greedy 모델은 bulk 절약과 sparse 수정 증폭을 함께 보였을 뿐 모든 B+ 설계를 반증하지 않는다. 불필요한 전체 순회·중복 바이트·게시/증명부터 제거하고 남는 metadata 비용을 측정한 뒤 packed page의 bulk/sparse 쓰기·읽기·flush·복구를 비교하는 순서가 합리적이다. 형식 재설계나 대형 시험은 자동 재개하지 않는다.

## 자원과 증거 한계

`StorageResources.cs:7–18`은 수요 기반 작업 목표 최대 4GiB, metadata 최대 2.5GiB, 공통 scratch 최대 512MiB, 최대 32 worker다. 사전 4GiB 할당이나 OS RSS/복수 process 합계 상한이 아니다. `BoundedParallel`의 동적 partition은 해당 호출에만 적용된다. 직렬 DFS, raw iterator/sort, adoption 비교, root 게시와 공용 잠금은 남는다. CPU 포화, 디스크 대역폭·랜덤 I/O, lock 대기, peak RSS 및 메모리 효율은 미측정이므로 최적이라고 말할 수 없다. 계측 후보는 단계별 파일 open/read/byte/hash, sort spill, page read/write/cache hit, flush 횟수와 지연, subprocess/lock/queue 대기, worker 이용률, process별 RSS와 scratch peak다. 카운터는 중첩 시간과 중복 바이트를 구분한다.

기존 ST-53 소형 31파일/992바이트 6개 generation의 baseline 세부 계측은 adoption build/flush 0.031초, 즉시 collection 1.413초, live 참조 관측 2.335초, 참조 index 0.524초였다. 중첩될 수 있어 합산하지 않는다. ST-41–51의 작은 ABBA는 현재 대형 시간이나 개선율이 아니다. 과거 100k/17GiB 수십 분~시간 기록은 복구·동시 실행·혼합 바이너리가 섞였고 정상 기준이나 외삽 근거가 아니다. 대형, 실제 Git/Plastic/Unity, 전체 gate와 하드웨어 포화는 미검증이다. 본 검토의 최소 확인은 읽기 전용 코드/기존 기록뿐이며 별도 실행 시간 추정이 필요한 제품 단계는 시작하지 않았다.

## 누락 점검

- [x] 전체 순회·파일 읽기/해시·트리·영속화·외부 프로세스/대기, 동적 분할과 dirty/fence/rename/delete/restart/외부 쓰기 및 보존 의존성: 호출 표와 1항. backend별 실제 syscall 횟수와 시간은 미측정.
- [x] raw 범위·backing 묶음·병렬성·descriptor·중복 row/spool/flush·drain: 호출 표와 2항. 정확한 중복 바이트 미측정.
- [x] adoption/page/회수·게시 전후 독립 검증·증명 공유·병렬화·묶음 게시: 호출 표와 3항. 새 경계는 설계 후보.
- [x] 과거 결과·공유 참조·membership/disk fallback·잠금·GC 분리와 총비용·동일 요청 복구: 4항.
- [x] backend 대조·읽기/해시·기준 읽기·polling/잠금·pipeline·live/stopped·후속 쓰기/reader/ACK: 호출 표와 5항. backend별 비용 미측정.
- [x] snapshot과 원본 충돌, 공통 불변 root, 기존 참조와 부족한 계약, 복사 대비 작업량, B+와 불필요한 작업 제거: 구조 대안과 우선순위.

## Re-review source observation

2026-11-02T02:15:30.916950+09:00; HEAD `1b33995955f4e7b77b1ce416c8a99be7e6e80116`.

```text
524bbe99b15ce7b33678d7920bfcc0415052fa288ed2b221d2c1599ca0109a41  src/Splinter.SourceControl.Files/FileSourceGenerations.cs
dec5cf2a1aed569eda3bf570cb9d76f06d656848e594f2db01729daf5ba5da29  src/Splinter.SourceControl.Files/FileReferenceResults.cs
7e8f06394175af6f9a6cc59e16179ae9eb99704b2cce456c5a89e25f33839f73  src/Splinter.SourceControl.Files/FileWorkspaceProvider.cs
02e01c81b5bdd094703d98a0f154581d82e9b604739b1216086113fa1476ae77  src/Splinter.SourceControl.Files/FileRawRangePreparation.cs
c1e52f7c38d58ec10ed3b596facf3ac430e37a621bc1e78ce768066ae7681fed  src/Splinter.SourceControl.Files/FileMetadataMaintenance.cs
725437a3d1d2623a7e2eb944cd6fbe5615ff5e9b2fa0951ec3c3e9eab933a6a6  src/Splinter.Runtime/ResultFileContentStore.cs
f6f688a7ed271caf72f6b6b25f5ff75f36b0feb1a199df188175f79af2451ca5  src/Splinter.Runtime/JournalPagedCapture.cs
d51cf3d6036be6efe9c8010010360ccb896d95f79739a7ba0b6dd8e7249e18c8  src/Splinter.Runtime/JournalRangeAdoption.cs
48d24cd46ac381635797493b37c23c7804daa99ca0328e74fd58b9926b1f0ce4  src/Splinter.Runtime/OwnedRangeContentStore.cs
33c3b0809d90eeeca9a1c4cb4d9a52e10565104a04bca3bd2e14ce153a0d68e8  src/Splinter.Runtime/WorkspaceGenerations.cs
85aab04079e7bd722389fb09fca5fe3cec04b6a292f2269c2089317f3202a2a1  src/Splinter.Runtime/PagedMetadataCompactProof.cs
4beb5b987c316784f6652ad452a758becd3351e48152701635c73a6536307867  src/Splinter.Platform.Windows/NtfsSourceGenerations.cs
862045ca726bdefce057a8a1fb97eb2dea5bfb6bfb2478ad753ffb6eb51b9d43  src/Splinter.Runtime/MetadataMembership.cs
```

Observed working tree (only the two review documents were edited by this review):

```text
 M AGENTS.md
 M contracts/v1/file-source-manifest.schema.json
 M docs/README.md
 M docs/foundation.md
 M docs/ko/README.md
 M docs/ko/foundation.md
 M docs/ko/lifecycle-optimization-map.md
 M docs/ko/repeated-original-apply.md
 M docs/ko/scalable-storage.md
 M docs/ko/verification/scalable-storage-throughput-followup.md
 M docs/lifecycle-optimization-map.json
 M docs/lifecycle-optimization-map.md
 M docs/repeated-original-apply.md
 M docs/scalable-storage.md
 M docs/verification/scalable-storage-throughput-followup.md
 M src/Splinter.Platform.Windows/WindowsSourceChanges.cs
 M src/Splinter.Runtime/CapturedRangeAdoptionStore.cs
 M src/Splinter.Runtime/JournalPagedCapture.cs
 M src/Splinter.Runtime/PagedMetadataStore.cs
 M src/Splinter.Runtime/ResultFileContentStore.cs
 M src/Splinter.Runtime/SharedMetadataPoolMaintenance.cs
 M src/Splinter.Runtime/WorkspaceStore.cs
 M src/Splinter.SourceControl.Files/FileMetadataMaintenance.cs
 M src/Splinter.SourceControl.Files/FilePagedQueries.cs
 M src/Splinter.SourceControl.Files/FileSourceGenerations.cs
 M src/Splinter.SourceControl.Files/FileWorkspaceProvider.cs
 M tests/Splinter.Tests/RawBasisReuseTests.cs
?? docs/ko/verification/collection-performance-architecture-review-20261102.md
?? docs/verification/collection-performance-architecture-review-20261102.md
?? reports/benchmarks/capture-maintenance-20261102.json
?? reports/benchmarks/reference-capture-20261102.json
?? src/Splinter.Runtime/MetadataMembership.cs
?? src/Splinter.Runtime/PagedMetadataCompactProof.cs
?? src/Splinter.SourceControl.Files/FileReferenceResults.cs
?? tests/Splinter.Tests/CaptureMaintenanceTests.cs
?? tests/Splinter.Tests/ReferenceCaptureTests.cs
```
