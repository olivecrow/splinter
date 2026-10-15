# 저널 변경 범위 압축

[English](../journal-payload-compression.md)

`JournalDeltaStore`는 overlay **v2**, [기본 bounded SJR2](bounded-journal.md), 명시적 OFF의 **SJR2**와 [최적화 ON의 SJL4 현재본 저널](latest-only-journal.md)을 지원한다. write callback 하나의 최대 1MiB 변경 범위를 raw 또는 독립 `splinter-range-brotli-v1` 블록으로 저장한다. 이전 overlay/저널은 보존 후 명시적으로 거부하며 자동 이관하지 않는다. workspace 상태는 v2이고 독립 후보는 v3/SJR2다. 아래 frame append/선택 설명은 OFF의 일반 SJR2 경로이며 ON은 payload/metadata의 단일 capsule 기록·mutation마다 한 durable flush·게시/회수를 사용한다.

## 현재 frame

| 위치 | 길이 | 의미 |
| --- | ---: | --- |
| 0 | 4B | ASCII `SJR2` |
| 4 | 4B | little-endian metadata 길이, 1~65,536B |
| 8 | 4B | 실제 stored payload 길이 |
| 12 | 4B | 복원 후 decoded payload 길이, 0~MaxTransferBytes |
| 16 | metadata 길이 | 현재 Mutation JSON |
| 이후 | stored 길이 | raw 바이트 또는 codec의 완전한 SPB1 표현 |
| 마지막 | 32B | header·metadata·stored payload 전체 SHA-256 |

`stored=decoded`는 raw이고 `0<stored<decoded`는 codec이다. metadata 연산은 두 길이가 0이다. 압축 선택은 codec의 **48B 전체 비용까지 포함해 raw보다 최소 64B 작은 경우**뿐이다. raw에는 codec header/checksum을 추가하지 않는다. 새 raw frame은 이전 frame보다 header 8B가 추가되므로 이전 raw보다 항상 작다고 주장하지 않는다. encoded에는 외부 frame checksum과 codec checksum이 모두 있으며 계측에 포함한다.

Core `WorkspaceJournalPayload`는 저장/복원 길이와 논리 fragment의 범위를 검사한다. extent는 고정 block 시작·두 길이·decoded fragment offset을 보존한다. 부분 덮어쓰기와 잘라내기는 압축 stream 내부의 물리 offset을 계산하지 않는다. 첫 범위 읽기는 bounded 압축 블록 전체를 검증·복원하며 이후 읽기는 아래 RAM cache를 통해 검증한 복원 내용을 재사용할 수 있다. snapshot은 미덮인 범위에서만 읽는다.

범위 읽기 cache는 필요할 때 채우는 LRU이며 기본 예산은 **`JournalDeltaStore` 하나당 64MiB**, 항목 수는 최대 **4,096개**다. 같은 store의 private block 읽기가 이 예산을 공유한다. 항목마다 decoded 배열과 **보수적인 512B metadata 비용**을 계상한다. cache miss는 이 비용을 먼저 예약하고 필요한 항목을 퇴출한 뒤 진행 중 decoded 배열을 할당한다. cache key는 검증한 encoded SHA-256과 encoded/decoded 길이를 고정한다. cache hit마다 소유 저널에서 현재 encoded 블록을 읽고 SPB1 header·길이·SHA-256을 다시 검사한다. 반복 압축 해제만 생략한다. cache miss는 완전한 stream·tail·선언 길이 검증을 통과한 뒤에만 decoded 바이트와 cache 항목을 게시한다. 디스크 payload cache나 hydration은 없으며 `readCacheBytes=0`은 이 RAM을 계측하지 않는다.

예산이 0이거나 예산보다 큰 블록은 기존 bounded uncached codec 경로를 사용한다. 회수된 payload가 LRU에서 퇴출 전까지 남을 수 있지만 과거 물리 주소로 읽는 것을 허용하지 않는다. encoded 읽기/복원/visitor 실패·writer poison·종료 시 cache를 비운다. 예산은 살아 있는 cache/진행 중 배열과 보수적인 항목 비용을 포함하며, 임시 encoded buffer·pool 잔류·GC heap·전체 프로세스 메모리는 포함하지 않는다. 내부 압축 payload visitor도 private 비교·논리 hash·compact·SJL4 payload 검증에서 같은 cache를 사용한다. 매 방문의 현재 encoded 바이트 검증은 유지하며 전체 물리 seal/frame/prefix 검사는 cache hash로 대체하지 않는다. 디스크 형식·게시/회수 검사·개별 durable flush/ACK를 유지한다. 최초 읽기 cache 근거는 [시간·메모리 최적화 검증](verification/time-memory-optimization.md), visitor 재사용과 유한 capsule descriptor의 자격은 [LF-01–05](verification/lifecycle-finalization-optimization.md)를 따른다. 이전 Unity 시간은 이번 변경의 측정값이 아니다.

승인 쓰기는 기존 순서대로 frame append·개별 durable flush·index 반영·callback 반환을 유지한다. 압축을 위해 callback을 여러 승인으로 나누거나 group flush를 추가하지 않는다. private 동일 바이트/앞뒤 범위 재사용을 먼저 수행한다. journal budget은 실제 header/metadata/stored/checksum 길이로 검사한다. 재생은 외부 checksum과 내부 codec/선언 길이 일치를 확인한 뒤 논리 extent를 연결한다. 손상된 완전한 frame은 보존·거부하며 불완전한 마지막 미승인 frame만 기존 정책으로 제거한다.

Mutation JSON은 `Offset=0`·`Destination=null`·`Replace=false`를 생략한다. 현재 reader는 명시적 기본값과 생략을 같은 의미로 읽으며 비기본값·Entry·Sequence·Operation·fragment/skip 범위는 유지한다. 생성/재연결/읽기에서 저장된 저널을 다시 쓰지 않는다. 이 기본값 생략은 형식과 기존 입력 의미를 유지한다. 선택적 [기준본 복귀 정리](journal-base-reversion.md)의 추가 CLI/응답 필드는 별도 계약을 따른다.

기본 compaction/candidate는 유효 논리 fragment를 모아 현재 frame으로 표현하고 원래 저장 frame보다 작을 때만 선택한다. fragment metadata가 더 비싸면 원래 frame을 보존한다. 내용/sequence/소스 seal·같은 intent 재시도와 명시적 겹침 예산은 유지한다. 독립 결과 수집은 기존 파일/소스 provider를 통해 decoded 내용을 읽으며 회수할 저널/VSS에 의존하지 않는다.

명시적 compaction/candidate는 유지할 frame의 decoded mutation을 현재 JSON으로 직렬화해 더 작을 때만 메타데이터를 선택한다. 유지하는 payload의 raw/encoded 바이트와 두 길이는 그대로이며 외부 checksum은 새 metadata에 맞춰 작성한다. 생략된 기본값의 실제 저장 자격과 추가 직렬화 비용은 [JM 검증](verification/journal-metadata-storage.md), 한정 범위는 [JM 계획](plans/journal-metadata-storage.md)을 따른다.

`PayloadBytes`/`RetainedPayloadBytes`는 실제 stored 길이이며 codec 48B도 포함한다. `LivePrivateBytes`·`RetainedLogicalPayloadBytes`는 논리 길이다. `LiveStoredPayloadBytes`는 참조되는 block을 한 번씩 합산하고 `ObsoletePayloadBytes`는 완전히 미참조된 block의 stored 합계다. 일부만 유효한 압축 block은 전체 저장 비용을 유지하므로 논리 길이를 물리 길이에서 빼지 않는다. frame 비용은 header·metadata·외부 checksum이며 VSS·전체 workspace 비용과 구분한다.

압축 준비는 유익한 block의 최대 stored 용량(decoded−48−64B)만 scratch 하나로 대여하고 선택한 정확한 encoded 배열을 만든다. raw를 선택하면 결과 배열을 만들지 않는다. 중복 decoded+48B encoded 임시 buffer는 사용하지 않는다. codec/선택/바이트와 budget/개별 durable flush/ACK는 유지한다. uncached codec과 replay의 작은/빈 요청은 16KiB chunk와 요청분 staging으로 전체 stream을 검증하며 uncached 전체 요청은 전체 bounded scratch를 유지한다. cache에 넣을 범위 읽기의 miss는 미게시된 정확한 크기의 cache 배열로 직접 복원하며 두 번째 전체 decoded scratch buffer를 만들지 않는다.

visitor 기반 내부 상태 hash와 private 비교는 encoded extent 하나를 저장 블록 읽기 한 번·16KiB 복원 chunk 순회 한 번으로 처리한다. 정리/후보의 같은 물리 블록에 속한 유효 slice는 전체 블록을 한 번 복원해 모으며 64KiB 청크나 slice마다 전체 stream을 다시 읽지 않는다. 별도 visitor 순회는 현재 encoded block을 다시 읽고 검증하며, 검증한 cache hit는 완전히 복원된 stream을 재사용할 수 있다. header/checksum·완전한 stream과 tail/선언 길이 검증이 끝나기 전의 digest/비교와 미게시 후보는 임시 결과다. 실패하면 승인 쓰기나 후보 세대를 게시하지 않는다. 공개 range read는 필요한 검증에 성공한 뒤에만 destination에 복사한다. [자동 회귀](verification/code-quality-optimization.md)는 블록 읽기 수·최대 크기/fragment의 바이트와 기존 실패 미게시 규칙을 검증하며 실제 Unity 시간·전체 RAM 절감률을 주장하지 않는다.

pool 임대 반환은 pool 잔류·native codec state·caller 요청/result/index를 제거한다는 뜻이 아니다. [ME 범위](plans/memory-completion.md)·[현재 메모리 근거](verification/memory-completion.md)는 lease·관리 할당/heap·소유 provider/Editor와 시스템 공유 비용을 구분한다. 선행 공간/수명주기 자격은 [JP 검증](verification/journal-payload-compression.md)·[JP 종료 범위](plans/journal-payload-compression.md)를 유지한다.
