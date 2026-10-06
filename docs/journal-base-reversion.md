# 저널 정리의 선택적 기준본 복귀

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.compact
pwsh ./scripts/splinter.ps1 workspace compact --id <id> --home <home> --revert-base-ranges true
```

기본값은 **false**다. `true`는 명시적 정리에서 현재 private 범위와 작업 시작 때 고정한 불변 기준을 비교하고, 같은 부분의 private payload를 제거한다. 현재 수정 가능한 원본과 비교하지 않는다. 실행 중 append·개별 flush/ACK·정리 전 누적은 유지하며 자동 정리·상주 처리·전역 dedup은 제공하지 않는다. 일반 정리/개발 candidate/cache는 기존 선택 규칙을 사용한다.

기준 비교는 현재 파일 incarnation의 `BasePath`·`BaseLength` 안에서만 수행한다. 읽기는 최대 64KiB와 workspace transfer 한도 안에서 처리하고 4KiB 단위 또는 끝의 짧은 단위가 모두 같은 경우에 제거한다. 한 단위 안에 다른 바이트가 있으면 그 단위를 유지하므로 모든 같은 바이트를 최소 표현으로 만드는 알고리즘은 아니다. 부분 복귀/혼합 쓰기를 지원하고 truncate 후 늘린 tail·신규/삭제 재생성 파일을 기존 기준 내용으로 복원하지 않는다. 기준 없는 private 파일은 유지한다.

파일 저장 순서가 교차해도 비교는 같은 `BasePath`의 논리 offset 순으로 진행하고, 후보 작성 전에 남은 범위를 원래 저널의 물리 순서로 정렬한다. Git reader는 하나의 blob 증명과 64KiB chunk만 유지한다. 부분 읽기의 남은 stdout은 정확한 소유 파이프를 닫아 종료하며, 버린 내용을 drain하거나 cache하지 않는다. 전체 blob hash와 요청 chunk 검증·30초 무진행 제한은 유지한다. 이 변경의 [비교 범위](plans/journal-base-reversion-optimization.md)와 전후 계측은 기존 실제 A/B 자격과 구분한다.

내용이 같아도 파일 ID·경로/rename·길이·속성/UnixMode·시간·삭제/재생성·hidden·변경 path/sequence는 유지한다. 마지막 payload 쓰기가 전부 제거되면 기존 `metadata` frame으로 그 쓰기의 entry를 적용한다. 이전 무효 쓰기는 기존 sequence/skip 표현으로 유지한다. 기준본 복귀가 필요한 frame에는 제거한 범위를 다시 넣는 원본 payload fallback을 사용하지 않는다. 그래서 ON은 OFF의 더 작은 frame 선택과 비용이 다르고 일부 입력에서 더 많은 metadata/CPU/관리 할당이 필요할 수 있다. 한도·fragment metadata 제한에 걸리면 원본을 보존하고 거부한다.

## 검증과 같은 요청 복구

원래 저널의 구조 hash와, 확인한 같은 범위를 제거한 **기대 구조 hash**를 구분한다. 기대 hash는 같은 파일 entry·hidden/path changes·sequence와 남길 private 범위의 경계/내용을 포함한다. 제거 범위의 내용 동등성은 고정 기준 읽기/codec 검증으로 확인한다. 전체 파일이나 변경 없는 기준을 hash하기 위해 copy-up·전체 복제·읽기 hydration·payload disk cache를 만들지 않는다.

새 후보 전체 checksum·flush·재생·기대 구조 hash/sequence·전후 source seal을 확인한 뒤 원자적으로 게시한다. 재연결과 게시 seal 확인 전에는 이전 세대를 회수하지 않는다. 기준 부재/손상/unknown·예상 밖 short read·손상 intent/candidate는 데이터를 보존하고 거부한다. 응답 없는 완전한 쓰기가 재생에서 살아 있을 수 있는 기존 계약을 유지한다.

현재 `compaction-intent.json`은 **v2 하나만** 지원하며 원래/기대 hash·선택한 bool·기준 비교량·제거량·두 seal을 고정한다. 구형 v1 intent는 보존·거부하고 자동 이관하지 않는다. workspace v2·overlay v2/SJR2·candidate v3는 그대로다. CLI 요청 `revertBaseRanges`는 optional boolean이며 생략한 재시도는 저장된 선택을 사용한다. 진행 중 다른 선택은 `CompactionIntentMismatch`다. 이미 마감한 같은 seal/선택은 읽기 전용 재시도이고, 완료 후 다른 선택을 명시하면 별도 정리를 시작할 수 있다.

응답 `snapshot.compaction`의 `revertBaseRanges`, `baseComparedBytes`, `revertedPayloadBytes`는 각각 선택·기준 reader가 반환한 비교 bytes·제거한 논리 private bytes다. `cost`의 `last-write-index`·`base-range-compare`·`expected-state-hash`와 기존 stage를 함께 본다. stage `readBytes`는 해당 저널 읽기와 반환 기준 bytes의 합계이며 Git 내부 blob 해독·OS 물리 I/O 총량이 아니다. 논리 제거량을 NTFS 할당이나 VSS 절감량으로 해석하지 않는다. 정리 중에는 old/new 세대가 함께 있으며 metadata/checksum은 남는다.

[확정 비교/수치 기준](plans/journal-base-reversion.md)과 [실제 비교·복구·기본값 판단](verification/journal-base-reversion.md)을 따른다. 더 작은 최종 payload만으로 작업 전체 속도·전체 RAM·Unity import 비용 개선을 주장하지 않는다.
