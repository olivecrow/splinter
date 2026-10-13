# ON일 때의 현재본 저널

[English](../latest-only-journal.md)

`workspace compact --optimize-journal true`는 정상 stop/unmount 뒤 현재 저널을 현재본 저장으로 선택한다. `--revert-base-ranges true`도 같은 입력 `revertBaseRanges`를 선택한다. 새 작업의 최초 기본값은 ON이다. 기존 작업은 저장된 OFF/ON을 유지하며, 같은 ID/home의 writer 재연결도 그 선택을 유지한다. 이후 `compact`에서 옵션을 생략하면 현재 선택을 유지하며 `false`를 명시하면 OFF로 전환한다. 진행 중에는 저장된 선택을 바꿀 수 없다.

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.compact
pwsh ./scripts/splinter.ps1 workspace compact --id <id> --home <home> --optimize-journal true
pwsh ./scripts/splinter.ps1 workspace compact --id <id> --home <home> --optimize-journal false
```

기본 ON은 2026-10-06 사용자의 명시적 선택이다. 실제 32 PNG의 128KiB 범위 쓰기 1,536회 전체 22.389s(평균 약 14.6ms)와 파일 데이터 할당 93.61% 감소를 함께 판단했다. 이 값은 Unity Editor의 저장/import 시간이 아니며 관리 누적 할당/RAM 개선을 뜻하지 않는다. 개발용 Library cache와 고정 SJR2 쓰기 probe는 별도 계약의 OFF를 유지한다.

새 ON 저널은 `journal-initialization.json`의 정확한 identity/attempt 선택을 먼저 고정한다. 빈 object seed·identity·SJL4 root를 같은 디렉터리에서 각각 flush 후 최초 move로 게시하고 intent를 제거한 뒤 쓰기를 허용한다. 완료 전에 중단하면 같은 identity/attempt의 생성만 재개한다. 기존 빈 SJR2도 OFF 선택을 유지한다. 손상 seed·다른 owner·unknown object는 보존·거부하며 초기화 intent는 필수 NodeFormat=3을 포함한 v1만 지원한다. root·초기화 intent의 node 형식 식별자가 없거나 이전 값이면 복구·게시 전에 보존·거부하며 이관하지 않는다.

ON의 목적은 디스크에 불변 원본 기준과 **현재 private 변경 범위**만 남기는 것이다. B를 C로 대체하는 도중에는 B와 미완료 C의 복구 기록이 함께 있다. C의 durable 게시와 이전 payload 회수가 끝나야 쓰기 성공을 반환한다. 완료 뒤에는 디스크에 B의 superseded payload나 이전 metadata 버전을 남기지 않는다. 다른 파일·peer·원본 기준은 회수하지 않는다. 작업 전체의 완료를 추정하지 않으며 개별 mutation의 승인 경계를 사용한다.

## 현재 표현과 쓰기

overlay identity/workspace는 v2, ON 저널은 **SJL4/root v4와 필수 NodeFormat=3, SJN3 node·SJP2 metadata page**, OFF와 독립 candidate v3의 저널은 **SJR2**다. 현재 compaction intent는 **v3 하나만** 지원한다. 이전 NodeFormat=2/SJN2/SJP1·SJL1/SJL2/SJL3·SJT2/SJT3와 다른 pending/root/intent 버전은 보존·거부하며 자동 이관하지 않는다. OFF 저널을 ensure나 읽기만으로 ON으로 변환하지 않는다.

SJL4의 최초 `delta.journal` root는 같은 delta 안의 `latest-<attempt GUID>` 디렉터리와 현재 seed node/bucket/block을 지정한다. 첫 mutation 이후 root 자체가 현재 mutation의 capsule이다. capsule에는 128B owner/generation/이전 상태/데이터 hash header, bounded private block·그 뒤의 SJM2 metadata-page block, 현재 node/bucket metadata와 전체 checksum이 있다. 각 SJB1 block은 최대 transfer 한도(최대 1MiB)의 raw/Brotli payload와 80B identity·block ID·길이·hash header를 포함한다. 현재 범위의 위치를 가리키는 node와 마지막 path sequence/whiteout은 현재 상태이며 편집 이력 stack이 아니다.

payload 위치는 물리 주소·길이와 선택적인 현재 metadata만 유지하며 과거 capsule frame을 참조하지 않는다. metadata 검증은 해당 물리 파일의 현재 frame을 조회한다. 메모리의 파일별 object 인덱스로 게시·회수의 항목 검색을 변경된 파일에 한정하고, 재연결에서는 검증한 저장소에서 다시 구성한다. footer 비교는 최대 64KiB의 임시 버퍼를 쓰고 metadata hash는 검증한 byte 배열을 복제하지 않는다. SJL4 형식·전체 검증·개별 mutation flush/ACK를 유지하며 [시작 단계 저널 최적화 검증](verification/journal-startup-optimization.md)을 따른다.

SJN3 node는 이어 쓰기와 덮어쓰기 모두에서 내용이 같은 논리 페이지를 재사용한다. 페이지는 extent 1–32개이며 inline tail은 32개 미만이다. 변경한 짧은 앞부분은 다음 불변 페이지와 합계가 32개 이하면 합쳐 역순 삽입이 매번 페이지 하나를 만들지 않도록 한다. SJP2 metadata page는 별도 `page-*.bin` 이름과 SJM2 block header를 쓴다. 모든 capsule에서 user payload가 metadata page보다 먼저 온다. page를 교체할 때 앞의 private 바이트는 그 자리에 유지하고 최초 변경 object 뒤의 현재 object만 옮긴다. 남기는 prefix는 소유 backup 이름에 원래 header hash를 고정하며 각 완전한 block의 identity·경계·payload hash를 다시 검증한다. node/page 참조와 정확한 inventory도 별도로 검증한다. 부분 block과 변경·소실 prefix는 거부한다. 옮기는 object는 한 번 읽은 같은 바이트를 해시한다. 회수 계획의 전체 파일·보존 prefix hash는 같은 스트림 읽기에서 계산하되 최종 회수 시 대상을 다시 해시한다. page 참조 배열은 page 수에 비례하므로 쓰기당 전체 metadata가 상수라는 의미는 아니다. 정확한 소유권·현재본만의 회수·개별 flush/ACK·독립 후보 export·복구를 유지한다. 비용과 제한은 [LC-01](verification/lifecycle-cost-optimization.md)을 따르며 이전 [SJN2 이어 쓰기 비교](verification/startup-time-optimization.md)는 과거 근거다.

쓰기 순서는 다음과 같다.

1. 기존 private 범위만 이용해 C의 extents를 준비한다. 부분 대체 block은 남은 private slice를 bounded block으로 다시 묶는다. capsule 일부가 대체되면 그 capsule에 함께 있던 아직 유효한 private 범위/metadata만 옮겨서 이전 버전을 남기지 않는다. 원본 읽기·전체 파일 copy-up·읽기 hydration은 없다.
2. 하나의 `transaction.pending`에 C payload와 현재 metadata, exact put/remove inventory, 이전 소유 파일의 hash와 회수 계획을 한 번 기록한다. capsule 한도는 256MiB다. 전체 checksum을 기록한 뒤 **그 파일에 한 번 `Flush(true)`**한다. 다른 mutation의 flush/ACK를 합치지 않는다.
3. `File.Replace`로 pending을 현재 root에 게시하고 기존 root는 같은 소유 디렉터리의 generation별 backup으로 이동한다. 기존 root를 복사하거나 C payload를 다시 기록하지 않는다.
4. 정확한 hash의 이전 파일을 삭제하거나, 아직 유효한 private block만 남으면 교체된 metadata-page/footer suffix를 truncate한다. 대체된 payload와 metadata는 성공 반환 전에 회수한다. private 읽기/root 핸들을 게시 전에 닫고 성공 뒤 현재 root로 다시 연다. 디스크 payload cache는 만들지 않는다.

범위 읽기는 필요할 때 채우는 decoded payload LRU를 RAM에 두며, 기본 예산은 **`JournalDeltaStore` 하나당 64MiB**, 항목 수는 최대 **4,096개**다. 예산은 decoded 배열과 **항목당 보수적 512B 비용**을 포함하며, cache miss의 진행 중 배열도 할당 전에 계상한다. 전체 프로세스·GC·working set의 한도가 아니며 읽기 scratch와 다른 provider 메모리는 별도다. cache hit마다 소유 저널에서 현재 encoded 블록을 다시 읽고 SPB1 header·길이·SHA-256을 재검증한다. 검증한 내용이 일치할 때 압축 해제 결과만 재사용한다. 회수된 내용이 한정 LRU에서 퇴출 전까지 남을 수 있지만, 물리 주소만으로 재사용을 허용하거나 현재 저장소 검사를 대체하지 않는다.

encoded 읽기/복원 실패·writer poison·종료 시 cache를 비운다. replay·seal·visitor 기반 전체 stream 검증은 cache를 거치지 않으며 게시·회수·형식·개별 durable flush/ACK를 유지한다. `readCacheBytes=0`은 계속 디스크 hydration 부재를 뜻하며 RAM 사용 부재를 뜻하지 않는다. cache 비용과 제한은 [시간·메모리 최적화 검증](verification/time-memory-optimization.md)을 따르며 과거 Unity 시간을 이 새 cache의 자격으로 승계하지 않는다.

원자적 게시 뒤 capsule 검증과 block/참조 인덱싱은 정확한 게시 root의 짧은 수명 읽기 핸들 하나를 사용하여 두 번째 open/close를 줄인다. Windows의 공유 모드는 두 검사 사이 쓰기와 경로 교체를 막는다. 검증 실패·중단을 포함해 회수 전에 핸들을 닫는다. 전체 checksum·owner·형식·namespace·metadata·길이·범위·generation 검사를 유지하며 검증 단계나 mutation별 durable flush/ACK를 생략하지 않는다. 실패한 writer는 같은 저널을 재연결할 때까지 poison 상태를 유지한다. [SA-02](verification/startup-additional-optimization.md#변경과-보호-조건)를 따르며 이 변경만의 전체 Unity 준비 시간 절감은 분리 측정하지 않았다. 후속 최적화를 합친 후보의 실제 시간은 [UF-01](verification/unity-final-lifecycle.md)에 기록한다.

한 번의 flush로 **데이터와 metadata 모두** durable해진 뒤 게시한다. 압축 선택·checksum·전체 encoded stream 검증·개별 mutation ACK를 유지한다. 회수의 삭제/truncate는 기존 durable C에 의해 재시도 가능한 소유 정리이며 별도의 payload 복사나 flush를 추가하지 않는다. 파일 게시/회수와 남은 private slice 재작성 비용 때문에 OFF보다 느릴 수 있다. 소유 writer의 예산/ACK용 바이트 수는 최초 inventory에서 고정하고 게시·회수한 정확한 길이만 증분 갱신한다. 명시적 관측·seal·재연결은 실제 파일 inventory를 검사하며 metadata 장부로 내용 hash 검증을 대체하지 않는다.

같은 writer 재연결은 generation 순서와 정확한 owner·이전 상태 hash·capsule bounds/checksum을 확인한다. known pending의 미완료 tail과 아직 B인 root에서는 pending만 회수하고 B를 유지한다. 완전한 C는 재flush·게시·소유 회수를 마무리하며, 응답 전에 중단한 완전한 쓰기가 살아 있을 수 있는 기존 계약을 유지한다. 게시 뒤에는 root의 정확한 회수 계획만 재개한다. 이미 metadata를 잘라낸 backup은 retained payload prefix hash를 확인한다. 완전한 checksum 손상·다른 identity·unknown 파일·게시 뒤 현재 root 누락·변경된 회수 대상은 보존·거부한다. root가 없을 때 과거 backup을 B로 선택하거나 OFF로 새로 만들지 않는다. 이전 SJL1/2/3·SJT2/3는 보존·거부하며 자동 이관하지 않는다. Windows 게시의 확인된 sharing/lock/replace-remove 오류(32/33/1175)는 같은 flushed 임시 파일만 최대 1초 재시도한다. OS 전원 손실에서의 디렉터리/삭제/truncate 내구성은 NotRun이다.

## 삭제, 결과와 관측

삭제된 파일은 마지막 열린 파일 context가 남은 동안 현재 내용으로 읽을 수 있다. WinFsp는 파일 ID별 context를 세고 마지막 Close에서 `ReleaseUnlinkedFile`을 호출한다. ON은 삭제된 현재 ID의 payload/node만 회수하고 whiteout·경로의 마지막 변경 sequence를 유지한다. 아직 열린 교체 대상도 같은 원칙을 따른다. Runtime 직접 사용자는 마지막 context 종료를 명시해야 한다. 중단 때문에 닫힘 통지를 받지 못한 unlinked 기록은 다음 명시적 stopped compact/release에서 정리하며 자동 sweep하지 않는다.

일반 연결 파일이나 unknown ID의 `ReleaseUnlinkedFile`은 mutation gate 안에서 로컬 disposed/poison/journal-length 상태를 검사한 뒤 회수 대상이 없으므로 기준본 availability를 조회하지 않고 반환한다. 실제 unlinked 회수에는 전체 기준본 availability 검사와 직렬화된 commit을 유지한다. WinFsp의 외부 mutation/retention 잠금도 유지한다. [SA-02](verification/startup-additional-optimization.md#변경과-보호-조건)의 빠른 반환은 읽기·mutation·seal·복구 증명을 면제하지 않는다.

seal의 bytes/hash는 root **및 전체 현재 object 파일**을 포함한다. 관측 `journalOptimizationEnabled`는 실제 writer 저장 형식을 나타낸다. `snapshot.compaction.journalOptimizationEnabled`는 완료한 선택의 receipt다. `JournalBytes`·payload/metadata·allocation은 다중 파일 합계이며 root 하나의 길이를 비용으로 보고하지 않는다. read-only restore, checkpoint의 stable seal, release/discard의 정확한 inventory·중단 재개, peer·dirty/target guard를 유지한다.

SJL4 seal은 capsule 검증 때 읽는 정확한 바이트를 물리 파일명 ordinal 순서대로 함께 해시한다. root 바이트와 기존 이름·길이 framing, 전체·보존 prefix 검증, seed 파일, 마지막 head/구성원/pending 검사와 별도의 전후·unmount 후 seal은 유지한다. 이전 seal이나 writer 계수로 현재 내용 읽기를 대체하지 않는다. Windows seal과 회수 inventory 관측은 연산 동안 object 디렉터리 조상을 고정하고 각 leaf를 검사한다. 회수는 fence 전에 계획 경로를 검증하고 fence 뒤 내용을 검증한다. fence가 delete sharing을 허용하므로 삭제 직전 hash는 유지한다. 이미 회수된 owner lock이 없는 재개 inventory는 전체 경로 검사를 사용한다. 한정 자격은 [LC-03](verification/lifecycle-cost-optimization.md)을 따른다.

독립 candidate와 명시적 OFF 전환에는 SJR2의 단일 **현재 상태 초기화 prefix**를 쓴다. node/write/hidden/path sequence/projection 상태와 완전한 footer로 현재 상태를 복원하고, OFF의 후속 mutation은 원래 append 방식으로 이어간다. footer 없는 부분 초기화는 손상으로 보존·거부한다. ON에서 OFF로 전환하는 표현은 metadata 비용 때문에 더 클 수 있다.

[기준본 복귀](journal-base-reversion.md)는 명시적 compact에서만 고정 기준과 동일한 4KiB 단위를 제거한다. 매 저장에서 원본 기준을 다시 비교하지 않는다. 따라서 live B→C→원본 bytes로 돌아와도 최신 private 범위 하나는 다음 명시적 compact 전까지 남을 수 있다. 반복 저장 횟수와 관계없이 현재 payload만 유지하는 것과 기준본과 같은 payload를 없애는 것은 별도의 비용이다.

[현재 쓰기 경로 범위](plans/latest-journal-write-optimization.md)와 [실제 입력·복구·비용 검증](verification/latest-journal-write-optimization.md)을 따른다. [SJL2 당시 비용](verification/latest-only-journal.md)은 별도 보존하며 기존 Unity/VSS/대형/전체 RAM 자격을 새 SJL4의 통과로 승계하지 않는다.
