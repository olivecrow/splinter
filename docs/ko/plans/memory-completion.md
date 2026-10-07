# 메모리 단계의 최종 범위

[English](../../plans/memory-completion.md)

사용자 요청에 따라 **현재 Windows/NTFS 알파의 메모리 구현·검증 단위를 이번 작업으로 마감**한다. 물리 저장 작업은 `2bb3d06`으로 커밋했다. 질문은 **압축 저널의 중복 쓰기 버퍼와 작은/빈 읽기의 전체 decoded 버퍼를 줄이면서 같은 바이트·실패 시 미게시·재생/결과/회수 계약을 유지하는가**다. 기존 준비 함수의 할당 개선을 전체 provider/Editor RAM 절감으로 승계하지 않는다.

**ME-01~05를 scoped-complete-with-limitations로 마감했다.** [직접 근거·비용·회수 판정](../verification/memory-completion.md)이 소유 문서다. 다음 일반 개발은 안정적인 병렬 작업이며 새로운 메모리 비교나 최종 A/B를 자동 반복하지 않는다.

## 포함과 완료 조건

| ID | 포함 | 종료 조건 |
| --- | --- | --- |
| ME-01 | 비용과 고정 입력 | 관리 누적 할당·GC 후 살아 있는 heap·동시 pool lease·프로세스 working set/private bytes·시스템 사용량을 분리. 현재 Runtime과 실제 보존 A/B 저널의 hash를 고정하고 같은 probe를 별도 baseline/candidate 프로세스에서 실행 |
| ME-02 | journal 압축 쓰기의 중복 버퍼 | 내부 Runtime 경로에서 유익한 압축에 필요한 scratch와 정확한 결과 배열만 사용. standalone codec 계약·quality/window·선택 기준·encoded bytes·budget/개별 flush/ACK를 유지. raw/경계 입력을 포함해 이전 경로와 바이트/선택 일치 |
| ME-03 | bounded 부분/빈 읽기 | 작은 읽기/재생 검증은 16KiB chunk와 요청분 staging으로 전체 압축 stream을 검증한 뒤 게시. 전체/작은 block은 기존 bounded 전체 복원 유지. 잘림/초과 복원/trailing/재서명 손상·EOF·chunk 경계·동시 호출과 임대 반환 확인. decoded cache를 유지하지 않음 |
| ME-04 | 최종 제품 A/B 한 묶음 | 고정 새 작업 두 개로 생성·읽기 hydration 0·독립 소유 Editor·Bee 입력 쓰기/해시·정상 stop/동일 ensure·독립 결과·A release 중 B 지속·최종 회수. provider와 Editor/children의 비용·시스템 공유 값을 분리해 기록하고 소유 프로세스/핸들/메모리/mount/delta/snapshot 0을 확인 |
| ME-05 | 회귀와 단계 마감 | 필수 locked restore/Release build/tests·응답 schema·UTF-8/링크/diff. 개선된 호출의 lease와 실제 입력 비용/남은 index·Editor 비용을 설명하고 현재 알파의 일반 메모리 개발 목록을 닫음 |

## 고정 실행

오프라인 입력은 `artifacts/journal-metadata-storage-20261003/candidate-a-qualified`·`candidate-b-qualified`의 실제 Unity 저널/identity다. 읽기 전용 handle로 원본을 고정하고 전후 hash·sequence·private state와 내용을 대조한다. 작은 합성 호출은 같은 codec/내부 journal 함수를 사용하는 buffer 경계 대조이며 실제 RAM 자격으로 확대하지 않는다. 새 evidence는 `artifacts/memory-completion-20261003/`에만 신규 게시한다. probe의 명시적 GC는 살아 있는 managed 객체를 구분하는 계측이며 제품에는 추가하지 않는다.

제품 실행은 기존 등록 원본 `T:\plastic-fixture-20261002`, home `T:\pt1002`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, journal budget 17,179,869,184B, keys **`mem-t-a-20261003`·`mem-t-b-20261003`**다. 기존 서비스/등록·Unity `6000.3.22f1`·CLI/Pipeline을 유지한다. 현재 후보 바이너리 hash·원본/Plastic 등록·기존 records·원본 Editor identity를 먼저 고정한다. 반환 workspace path에만 쓰고 소유 Editor receipt의 PID/시작 시각을 매 명령과 대조한다.

입력은 PS에서 고정한 원본 Bee 네 파일(`1900b0aE.dag`·`.dag.json`·`.dag.payloads`·`1900b0aE-inputdata.json`)의 같은 bytes다. 새 `Library/SplinterMemoryQualification`에 64KiB stream으로 명시적 쓰기하고 mounted hash를 확인한다. A/B source marker와 128KiB 반복 문자열 source 파일은 compressed source 결과 수집/읽기를 검증한다. 새로운 전체 clone·read hydration이 아니다. B는 A release 뒤 marker를 한 번 더 바꾸며 독립 결과를 보존한다. 공간 compaction·원본 apply·discard·MCP/Play/Test/build는 이 작업에 추가하지 않는다.

이 두 새 작업의 stop/checkpoint/release와 정확한 소유 자원 회수는 요청한 구현 검증 범위다. unknown/실패는 같은 key/id/home/intent와 evidence로 보존한다. read-only query의 첫 main-thread timeout은 원시 실패 뒤 같은 query 한정 재시도를 허용하며 쓰기를 timeout만으로 반복하지 않는다. 회수 보류나 내용/peer 보호 오류가 남으면 완료로 표시하지 않는다.

## 종료와 재개 조건

ME-01~05의 직접 근거로 **현재 알파의 메모리 단계**를 scoped-complete-with-limitations로 마감하고 다음 일반 개발을 안정적인 병렬 작업으로 이동한다. lease 감소는 pool 잔류·Brotli native state·provider/Editor 전체 RAM 감소율이 아니다. index·동시 Editor·검증 cache의 유효 데이터와 시스템 file cache/다른 앱은 별도 비용으로 남긴다. 프로세스 private/working set의 합을 물리 RAM으로 부르지 않고 미관측 peak는 unknown이다.

새 메모리 작업은 실제 입력의 누수·메모리 부족/지속 증가·작업을 막는 부담 또는 명시적 지원 확대 요청에서만 해당 입력/질문/종료 조건으로 연다. 대형/cold/ARM/원격/OS 강제 중단과 이전 SF/NR 미자격을 자동 재개하지 않는다. worker pool/큐/자동 모드/원본 Editor 공유·OS cache/working-set trimming·제품 강제 GC·flush/group commit·daemon/DB·새 backend·설치/grant/서비스/볼륨 변경·타 snapshot/기존 결과 삭제는 제외한다.
