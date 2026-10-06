# 메모리 단계의 직접 검증

[ME-01~05 확정 범위](../plans/memory-completion.md)를 **scoped-complete-with-limitations로 마감했다.** 현재 Windows x64 NTFS 알파의 내부 저널 buffer 절감·같은 입력 의미/실패 계약·소유 provider/Editor 비용/회수 검증이다. 다음 일반 개발은 안정적인 병렬 작업이다. 누적 할당·pool lease·살아 있는 heap·process working set/private bytes·시스템 공유 사용량을 서로 대체하지 않는다.

## 고정 대조

기준 Runtime은 물리 저장 마감 커밋 `2bb3d06`의 `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17`이다. 같은 artifact probe를 별도 baseline/candidate child process에서 실행하고 실제 보존 Unity A/B journal/identity를 read-only handle로 고정했다. source evidence를 재작성/정리하지 않는다. `artifacts/memory-completion-20261003/`가 원시 근거다.

현재 Runtime hash는 `90d37ae0a1fd4a77542e4fb15425e9e8f8da92d06ebb3b9a78333d9d17e39cb4`다. CLI/서비스·Core/schema/codec identity·overlay v2/SJR2/candidate v3·quality 1/window 22·압축 선택 기준과 개별 durable flush/ACK는 유지했다. 공통 API/fake를 유지하는 내부 Runtime 변경이며 새 명령/모듈/설치는 없다.

## ME-01~03: 같은 입력과 bounded buffer

같은 probe의 별도 child process를 입력당 한 번 실행했다. 고정 identity와 fake 불변 기준으로 실제 보존 저널의 private state를 대조한 오프라인 재생이며 live VSS/원본 전체 상태 검증과 구분한다. 원본 hash·identity·sequence·private state/usage·node/extent/hidden/path-change 수와 base payload read 0이 전후 같았다. 작은 고정 함수 입력의 raw/encoded 선택과 선택된 bytes/hash도 같았다. 112B 경계부터 최대 1MiB와 raw·압축/선택 경계를 자동 시험으로 대조했다.

| 직접 관측 | 기준 → 현재 |
| --- | --- |
| journal 1MiB 압축 준비의 동시 Shared pool lease | 3,145,728 → 1,048,576B, 66.7% 감소. caller 입력/선택 결과 배열은 별도 |
| journal 64KiB 압축 준비의 동시 lease | 196,608 → 65,536B. raw 선택도 중복 encoded buffer가 없음 |
| codec 1MiB block의 빈/EOF 검증 lease | 1,048,576 → 16,384B |
| 같은 block의 7B/4KiB 읽기 실제 lease | 1,048,576 → 32,768B. 요청 크기는 16,384+count이며 pool 반올림 포함 |
| 전체 block 읽기 lease | 1,048,576B 유지. 출력 게시 전 전체 검증과 bounded 상한 유지 |
| 실제 A/B replay의 최대 동시 pool lease | 각각 2,162,688 → 1,130,496B, 47.7% 감소. 모든 rent/return 일치 |

chunk 방식은 전체 stream을 검증하며 read amplification을 없애지 않는다. 16KiB 경계/EOF/int.MaxValue·반복/동시 호출·재서명된 trailing/선언 길이/잘림·초과 복원과 실패 시 caller 출력 불변/lease 반환을 확인했다. decoded/persistent cache나 제품 강제 GC를 추가하지 않았다.

| 실제 보존 입력 | A | B |
| --- | ---: | ---: |
| journal bytes / durable sequence | 37,188,067 / 27,970 | 53,212,331 / 61,687 |
| 고정 journal SHA-256 | `279faf6afe24070916732d4ab9fe7a81542bbb7ee64610133931e31f84cba1db` | `147b0ffdac9e38bdbfa02973e38dca91cd6edb01f419b59a2723661f73352ad2` |
| replay 관리 누적 할당, 기준 → 현재 B | 106,260,984 → 104,698,920 | 174,994,000 → 173,451,112 |
| probe GC 후 heap, 기준 → 현재 B | 6,788,208 → 5,228,776 | 8,845,968 → 7,293,760 |
| index nodes / extents | 982 / 20,880 | 2,188 / 33,252 |
| replay 시간, 기준 → 현재 ms | 673 → 718 | 1,063 → 1,132 |
| process private bytes, 기준 → 현재 B | 42,799,104 → 43,798,528 | 43,765,760 → 42,934,272 |

GC는 probe에서만 실행했다. heap에는 index뿐 아니라 공유 pool 잔류·probe overhead가 포함되고 위 차이를 index 감소율로 읽지 않는다. 관리 할당 감소는 약 1.5MiB이며 index 수는 그대로다. private bytes는 A에서 오히려 증가했다. 시간은 한 관측의 약 6~7% 증가로 SLA/유의성 판정이 아니다. 함수 lease 감소와 process/전체 RAM 감소가 같다는 주장을 하지 않는다.

## ME-04: 현재 후보의 제품 A/B

기존 `T:\plastic-fixture-20261002`·`T:\pt1002`, Plastic/Unity/snapshot/NTFS, budget 17,179,869,184B, Unity `6000.3.22f1`·기존 CLI `1.0.0-beta.5`/Pipeline `0.8.0-exp.1`·제한 서비스를 일반 사용자 토큰으로 사용했다. 명령 비용 기록의 callerElevated는 모두 false다.

| 작업 | 고정 key / id | loaded 값과 독립 결과 |
| --- | --- | --- |
| A | `mem-t-a-20261003` / `90390d3a165005fb166e6a15` | `ME-a-one`, Editor PID 60668/정확한 시작 ticks. 결과 `sha256:467b1862df8372ba72647c9ef0e81f37a78281c789022c40cc525464076aef27` |
| B | `mem-t-b-20261003` / `309595a36609254dfcb64c8c` | `ME-b-one` → A release 뒤 `ME-b-two`, PID 12744/같은 시작 ticks·새 MVID. 결과 `sha256:81eed32b46d4ae1a078e57cfa583572126cd69caecd66729e76a3c17b8ba2777` |

두 생성/64KiB 원본 표본 읽기 뒤 initial payload/read cache/payload/sequence가 0이었다. 같은 PS 고정 Bee 네 파일 51,859,898B를 각 새 Library 경로에 64KiB stream으로 명시적 쓰기하고 정상 stop/같은 ensure 뒤 mounted hash를 재검증했다. 원본 읽기 hydration/전체 clone이 아니다. 각 worker의 128KiB 반복 문자열 source와 marker를 읽어 독립 파일 결과로 보존했다. same ensure의 id/path/key/digest/base/snapshot이 그대로였다.

처음 catalog는 준비 중 `No Pipeline instance found`였고 reload 관측 뒤 같은 경로 catalog에 연결했다. A/B의 첫 eval과 B의 후속 reload 첫 eval은 5초 main-thread timeout이었다. 각 원시 실패를 보존하고 같은 읽기 query 한 번만 재시도해 actual project/PID/ticks/값/MVID·compiling/updating/failed=false를 확인했다. B recompile은 한 번 요청한 뒤 완료 상태만 읽었다. reload 후 B port가 7802→7801로 바뀌어 매 명령 exact path를 유지했다. 발견/최초 응답 제한은 해소한 것으로 표시하지 않는다.

| 소유 비용의 시점 표본 | A | B |
| --- | ---: | ---: |
| 생성 후 provider private / working set B | 25,677,824 / 64,184,320 | 27,197,440 / 65,798,144 |
| loaded 후 provider private / working set B | 94,478,336 / 136,040,448 | 95,522,816 / 135,811,072 |
| loaded 후 Editor 소유 group private / working set B | 3,932,237,824 / 3,497,758,720 | 3,725,754,368 / 3,188,903,936 |
| 정상 stop 명령 ms | 60,710 | 31,110 |
| release 후 provider/Editor group process·handle·private/working set | 0 | 0 |

group은 root/children/supervisor의 관측 합이며 물리 RAM 합이 아니다. Editor 비용이 provider보다 크다는 관측은 남기지만 Editor 수/validation view 선택은 AI 절차다. 작업을 막는 수십 분 정체는 이번 입력에서 관측하지 않았다. 설치·pool/큐·자동 모드·원본 Editor 공유를 메모리 기능으로 추가하지 않는다.

시스템 공유 available physical/committed/commit limit/cache를 단계별로 별도 기록했다. 생성 후 available=41,087,598,592B/commit=50,550,927,360B, loaded 후 34,919,989,248B/58,339,336,192B, 회수 후 42,497,175,552B/48,774,729,728B다. 다른 앱/원본 Editor·OS cache가 포함돼 작업 귀속이나 전후 절감률을 만들지 않는다. lifetime peak와 시점 표본·전체 순간 peak를 구분한다.

최종 두 소유 snapshot은 missing이며 mount/provider/delta와 소유 자원이 0이다. 각 result의 66개 파일/376,897 logical B와 baseline의 62개/245,523B의 object 길이/hash를 회수 후 전부 검증했고 marker/128KiB source도 일치했다. 원본 65개 source/admin 파일, 기존 738개 record 파일의 bytes/hash, Plastic 등록 5개와 원본 Unity PID 68624/85184/52688의 생성 identity를 보존했다. 원본 Editor의 로드된 프로젝트/코드 자격을 PID 존재만으로 주장하지 않는다. 기존 C: 보류와 비소유 snapshot에는 쓰기/회수 요청을 하지 않았다.

## ME-05: 회귀와 종료

`pwsh ./scripts/build.ps1`의 locked restore·Release 빌드 경고/오류 0, **900 passed / 0 failed / 1 skipped (901 total)**를 확인했다. 추가 16개는 codec chunk/미게시 경계와 journal 선택/바이트 대조다. 최초 focused 실행의 네 실패는 시험 입력의 중복 출력 directory였으며 Distinct로 수정한 뒤 전체 필수 실행이 통과했다. 그 실패 evidence도 보존했다.

현재 제품 `ContractCatalog`로 실제 응답 **44개**의 schema를 검증했다. UTF-8 92개 파일·문서 링크 414개와 diff 검사를 통과했다. 현재 source/binary 증거의 최종 계수/판정은 `completion-final.json`에 고정한다. 현재 메모리 구현과 이 한 묶음은 마감했으며 최종 A/B나 새 benchmark/harness를 자동 반복하지 않는다.

전체 순간 peak·Brotli native memory·pool 잔류/시스템 RAM의 workspace 귀속은 unknown이다. 대형/cold/ARM/원격/OS·동시 개수 전체와 개발용 view/cache의 새로운 실제 자격·이전 SF/NR 상태는 승계하지 않는다. 새로운 실사용 메모리 부족/누수/지속 증가·작업 차단 또는 명시적 지원 확대 요청에서만 해당 입력과 종료 기준으로 다시 연다.
