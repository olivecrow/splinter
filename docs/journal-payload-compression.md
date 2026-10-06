# 저널 변경 범위 압축

`JournalDeltaStore`는 overlay **v2**와 **SJR2** 저널 하나를 지원한다. write callback 하나의 최대 1MiB 변경 범위를 raw 또는 독립 `splinter-range-brotli-v1` 블록으로 저장한다. 이전 overlay/저널은 보존 후 명시적으로 거부하며 자동 변환하지 않는다. workspace 상태는 v2이고 후보는 v3다.

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

Core `WorkspaceJournalPayload`는 저장/복원 길이와 논리 fragment의 범위를 검사한다. extent는 고정 block 시작·두 길이·decoded fragment offset을 보존한다. 부분 덮어쓰기와 잘라내기는 압축 stream 내부의 물리 offset을 계산하지 않는다. 압축된 블록의 부분 읽기도 전체 bounded 블록을 검증·복원하며 블록 cache를 유지하지 않는다. snapshot은 미덮인 범위에서만 읽는다.

승인 쓰기는 기존 순서대로 frame append·개별 durable flush·index 반영·callback 반환을 유지한다. 압축을 위해 callback을 여러 승인으로 나누거나 group flush를 추가하지 않는다. private 동일 바이트/앞뒤 범위 재사용을 먼저 수행한다. journal budget은 실제 header/metadata/stored/checksum 길이로 검사한다. 재생은 외부 checksum과 내부 codec/선언 길이 일치를 확인한 뒤 논리 extent를 연결한다. 손상된 완전한 frame은 보존·거부하며 불완전한 마지막 미승인 frame만 기존 정책으로 제거한다.

Mutation JSON은 `Offset=0`·`Destination=null`·`Replace=false`를 생략한다. 현재 reader는 명시적 기본값과 생략을 같은 의미로 읽으며 비기본값·Entry·Sequence·Operation·fragment/skip 범위는 유지한다. 생성/재연결/읽기에서 저장된 저널을 다시 쓰지 않는다. 이 기본값 생략은 형식과 기존 입력 의미를 유지한다. 선택적 [기준본 복귀 정리](journal-base-reversion.md)의 추가 CLI/응답 필드는 별도 계약을 따른다.

기본 compaction/candidate는 유효 논리 fragment를 모아 현재 frame으로 표현하고 원래 저장 frame보다 작을 때만 선택한다. fragment metadata가 더 비싸면 원래 frame을 보존한다. 내용/sequence/소스 seal·같은 intent 재시도와 명시적 겹침 예산은 유지한다. 독립 결과 수집은 기존 파일/소스 provider를 통해 decoded 내용을 읽으며 회수할 저널/VSS에 의존하지 않는다.

명시적 compaction/candidate는 유지할 frame의 decoded mutation을 현재 JSON으로 직렬화해 더 작을 때만 메타데이터를 선택한다. 유지하는 payload의 raw/encoded 바이트와 두 길이는 그대로이며 외부 checksum은 새 metadata에 맞춰 작성한다. 생략된 기본값의 실제 저장 자격과 추가 직렬화 비용은 [JM 검증](verification/journal-metadata-storage.md), 한정 범위는 [JM 계획](plans/journal-metadata-storage.md)을 따른다.

`PayloadBytes`/`RetainedPayloadBytes`는 실제 stored 길이이며 codec 48B도 포함한다. `LivePrivateBytes`·`RetainedLogicalPayloadBytes`는 논리 길이다. `LiveStoredPayloadBytes`는 참조되는 block을 한 번씩 합산하고 `ObsoletePayloadBytes`는 완전히 미참조된 block의 stored 합계다. 일부만 유효한 압축 block은 전체 저장 비용을 유지하므로 논리 길이를 물리 길이에서 빼지 않는다. frame 비용은 header·metadata·외부 checksum이며 VSS·전체 workspace 비용과 구분한다.

압축 준비는 유익한 block의 최대 stored 용량(decoded−48−64B)만 scratch 하나로 대여하고 선택한 정확한 encoded 배열을 만든다. raw를 선택하면 결과 배열을 만들지 않는다. 중복 decoded+48B encoded 임시 buffer는 사용하지 않는다. codec/선택/바이트와 budget/개별 durable flush/ACK는 유지한다. read/replay의 작은/빈 요청은 16KiB chunk와 요청분 staging으로 전체 stream을 검증하며 decoded cache를 두지 않는다. 전체 요청은 전체 bounded scratch를 유지한다.

내부 상태 hash와 private 비교는 encoded extent 하나를 저장 블록 읽기 한 번·16KiB 복원 chunk 순회 한 번으로 처리한다. 정리/후보의 같은 물리 블록에 속한 유효 slice는 전체 블록을 한 번 복원해 모으며 64KiB 청크나 slice마다 전체 stream을 다시 읽지 않는다. 별도 extent/검증 단계는 다시 검사하고 decoded cache를 남기지 않는다. header/checksum·완전한 stream과 tail/선언 길이 검증이 끝나기 전의 digest/비교와 미게시 후보는 임시 결과다. 실패하면 승인 쓰기나 후보 세대를 게시하지 않는다. 공개 range read는 검증 성공 뒤 destination에 복사하는 기존 staging 규칙을 유지한다. [자동 회귀](verification/code-quality-optimization.md)는 블록 읽기 수·최대 크기/fragment의 바이트와 기존 실패 미게시 규칙을 검증하며 실제 Unity 시간·전체 RAM 절감률을 주장하지 않는다.

pool 임대 반환은 pool 잔류·native codec state·caller 요청/result/index를 제거한다는 뜻이 아니다. [ME 범위](plans/memory-completion.md)·[현재 메모리 근거](verification/memory-completion.md)는 lease·관리 할당/heap·소유 provider/Editor와 시스템 공유 비용을 구분한다. 선행 공간/수명주기 자격은 [JP 검증](verification/journal-payload-compression.md)·[JP 종료 범위](plans/journal-payload-compression.md)를 유지한다.
