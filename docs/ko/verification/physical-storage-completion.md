# 물리 저장 공간 단계의 최종 검증

[English](../../verification/physical-storage-completion.md)

[확정 범위](../plans/physical-storage-completion.md)의 PS-01~05를 **scoped-complete-with-limitations로 마감했다.** 현재 Windows x64 NTFS 알파의 물리 저장 구현·비용 판단이 완료됐으며 후속 메모리 판정은 [ME 검증](memory-completion.md)을 따른다. SF 전체·대형/OS/ARM 지원과 구분하고 실행 원시는 `artifacts/physical-storage-completion-20261003/`에 보존한다.

## 후보와 직접 근거

현재 development의 JM Runtime SHA-256은 `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17`이다. workspace v2·overlay v2/SJR2·candidate v3·서비스 protocol 1을 유지한다. 이번 프로그램 변경은 읽기 전용 개발 계측기이며 Core/공개 schema·flush·seal·회수 guard를 변경하지 않았다. 새 의존성을 추가하지 않았다.

| ID | 직접 확인 | 판정 |
| --- | --- | --- |
| PS-01 | SJR2 stored/decoded·부분 live block 전체 비용·fragment/skip/rename/resize/delete·재생성·checksum/tail/sequence·구형식/비ASCII/writer 보존 거부 10건. 기존 JP 보존 입력 두 개의 payload/live/obsolete/frame 장부를 당시 Runtime usage와 대조 | Passed, stopped ASCII 장부. Brotli 내용 복원은 제품 replay 책임 |
| PS-02 | 정확한 task 선택·공유/evidence 분리·합계·중복 root/누락 record·기존 출력 보존·실제 hardlink unknown 시험 3건 | Passed, Windows 파일 할당. MFT/디렉터리·VSS/OS cache 귀속 제외 |
| PS-03 | 아래 최종 A/B의 최신 Runtime 생성·읽기·같은 요청·소유 Editor·고정 쓰기·정리·독립 결과/회수 | Passed with limitations, 한 작은 실제 A/B와 정확한 두 snapshot의 회수 |
| PS-04 | 단계별 정확한 root 할당·저널 두 세대 겹침·결과/runner/evidence·회수 후 잔여량 | Passed with limitations, 공유 VSS 귀속/전체 순간 peak는 unknown |
| PS-05 | locked restore·Release 경고/오류 0, 884 passed·0 failed·1 skipped. Python 13 passed·실제/실패 응답 schema 35건·UTF-8/로컬 링크/diff | Passed, 현재 후보의 자동 회귀와 문서 감사 |

`python-final.log`·`python-qualified.log`·`build.log`·`automated.trx`·`response-schema-validation.json`은 최종 후보의 실행 근거다. 마지막 Python 실행은 소문자 task ID의 엄격한 거부까지 포함한다. 최초 Python 합계 시험은 OrderedDictionary의 `Measure-Object` 합계 누락을 발견했고 명시적 합산으로 수정했다. 실패 로그와 수정 전 생성/읽기 표본의 원시 row를 보존했다. `final-costs.json`은 이 두 초기 표본의 합계를 불변 root row에서 독립 재계산하고 이후 표본의 게시 합계와 root/category 합계도 대조한다. 이전 raw 저널의 부분 live bytes를 곧바로 회수 가능 payload로 읽던 장부 기대값도 현재 Runtime의 전체 참조 block 의미로 수정했다.

## 최종 제품 A/B

기존 `T:\plastic-fixture-20261002`·home `T:\pt1002`, plastic/unity/snapshot/ntfs-snapshot, 각 budget 17,179,869,184B를 사용했다. Unity `6000.3.22f1`·기존 CLI `1.0.0-beta.5`/Pipeline `0.8.0-exp.1`·등록된 제한 서비스는 유지하고 설치/grant/볼륨/UAC 변경은 없었다. ordinary CLI cost의 callerElevated는 false다.

| 작업 | key / id | 소유 Editor |
| --- | --- | --- |
| A | `ps-t-a-20261003` / `328a6239c9f51f15979bdbf3` | PID 45892, 시작 ticks `639266071054259595` |
| B | `ps-t-b-20261003` / `e7b5af5fbad8f669cdf8b013` | PID 69560, 시작 ticks `639266071083365748` |

두 ensure의 초기 payload/journal/read cache/sequence는 0이다. 반환 경로에서 기준 Bee 파일 64KiB를 읽은 뒤에도 journal/sequence/cache가 0이다. 같은 ensure는 각각 id/path/digest/기준/snapshot을 유지한다. 각 worker의 새 `Library/SplinterPhysicalQualification`에 고정 Bee 네 파일 합계 51,859,898B를 64KiB 스트림으로 기록하고 mounted 길이/hash를 확인했다. 신규 명시적 client 쓰기이며 최초 복제·hydration·파일 전체 copy-up이 아니다. 독립 source marker는 `Assets/SplinterPhysicalProbe.cs`다.

A 정상 stop·1GiB 겹침 예산의 한 번 정리 뒤 같은 snapshot으로 재연결하고 네 mounted 표본·source 검증을 확인했다. 전후 계측 복사본은 제품 compaction seal의 bytes/hash/sequence와 일치한다. B는 A 정리와 release 동안 같은 PID/시작 시각·b-one 값/MVID를 유지했다. A 회수 뒤 B를 b-two로 한 번 수정하고 외부 CLI recompile 한 번으로 같은 PID/시작 시각과 새 MVID·loaded 값·컴파일 오류 없음까지 확인했다. reload 뒤 CLI 포트는 재발견했으며 고정하지 않았다.

B도 정상 stop·같은 예산의 한 번 정리·재연결된 네 표본과 source 검증을 통과했다. A/B checkpoint는 안정된 소스를 각각 독립 64객체/245,669B로 보존했고 두 release/final status는 released·정확한 snapshot missing·mount/provider/delta false·handles/provider working/private·소유 application activeProcesses/working/private 모두 0이다. 회수 뒤 baseline/result의 모든 파일 객체 길이/SHA-256·marker와 결과 identity를 확인했다. A 결과는 `sha256:9463b77cb342190155044eab6f24b6be5606d94f06369d204b162fb6dcca7f43`, B는 `sha256:80dd86dfce8a1112fb5423e76f898b4d21428fd660bb689dc16b2545aa4409d9`다. Bee 표본은 Library/cache 입력이며 source 결과에 자동 포함하지 않는다.

`protection-and-reclamation.json`은 T: 원본의 source/관리 root 65파일·기존 records 537파일의 bytes/hash와 Plastic 등록 5개 불변을 확인한다. 기존 Unity PID 68624/85184/52688의 생성 시각도 유지했다. ordinary CIM의 이 기존 프로세스 project 인수는 관측되지 않았으므로 원본 프로젝트의 loaded 코드 자격으로 확대하지 않는다. 현재 Runtime과 실행 runner의 SHA-256이 같다.

최초 A/B eval과 B reload 뒤 최초 eval의 5초 main-thread timeout을 원시 실패로 남겼다. 각각 동일 대상 읽기 query만 한 번 재시도했고 쓰기/recompile/새 작업을 timeout으로 반복하지 않았다. Unity 코드 합격은 외부 도구/AI의 관측이며 Splinter 내부 gate를 재도입하지 않았다.

## 비용과 종료 판정

| 저널 | 정리 전 할당 B | 정리 후 할당 B | 감소 B | 정리 중 두 세대 할당 B |
| --- | ---: | ---: | ---: | ---: |
| A | 41,005,056 | 40,488,960 | 516,096 | 81,494,016 |
| B | 42,938,368 | 37,117,952 | 5,820,416 | 80,056,320 |

두 정리의 저널 할당 감소 합계는 6,336,512B다. 두 작업을 다른 시각에 정리했으므로 겹침값을 더해 동시 peak로 읽지 않는다. A/B의 after seal sequence는 26,796/28,118이며 복사본의 전체 hash와 Runtime journal/payload/metadata 계수도 대조했다. A after의 stored/live-stored/obsolete/frame 비용은 28,674,875/28,674,867/8/11,813,367B, B는 26,650,551/26,650,527/24/10,466,881B다. 논리 live는 각각 167,329,435/151,936,765B이며 부분 유효 block의 저장 비용을 차감하지 않는다. 계측기는 Brotli 내용 복원을 주장하지 않고 실제 mounted reads와 제품 state/hash/seal 검증을 별도 유지한다.

| 단계 | 두 선택 task 할당 B | 공유 runner/home root 할당 B | task에 포함된 독립 payload B |
| --- | ---: | ---: | ---: |
| 생성·기준 표본 읽기 뒤 | 111,232 | 3,449,864 | 0 |
| Editor/고정 입력 편집 표본 | 71,360,128 | 3,449,864 | 0 |
| A 정리 전 | 76,845,008 | 3,449,864 | 0 |
| A 보존, B 지속 | 76,652,376 | 3,449,864 | 310,784 |
| A 회수, B 후속 변경 표본 | 36,423,824 | 3,449,864 | 310,784 |
| B 정리 전 | 43,698,656 | 3,449,864 | 310,784 |
| B 보존 뒤 | 38,168,936 | 3,449,864 | 621,568 |
| 두 작업 회수 뒤 | 1,048,304 | 3,449,864 | 621,568 |

회수 뒤 두 task의 1,048,304B는 독립 payload 621,568B와 제어/manifest/진단 기록 426,736B다. 저널/이전 세대/mount는 없다. A 513,472B·B 534,832B이며 결과를 삭제해 잔여량을 0으로 만들지 않는다. 현재 runner는 내용 키 하나의 7파일/1,155,416B로 A/B가 공유했다. 이전 두 키 14파일/2,294,448B도 보존하므로 공유 합계는 21파일/3,449,864B다. 생성 시점에 이미 포함된 이 cache를 두 번 새 작업 비용으로 더하지 않는다. records 전체는 738파일/4,241,832B이며 기존 자료를 포함하므로 두 선택 task 합계와 다시 더하지 않는다.

원본 Library 30,748파일/1,852,496,112 allocated B는 별도 기준선이다. task 저널의 논리 Library bytes는 파일 할당에 다시 합산하지 않는다. 최종 evidence root 표본은 164,623,712B이며 네 저널 복사본 161,542,457 logical B·시험/TRX/실패/응답/계측 근거를 포함한다. 이후 감사 파일은 이 마지막 표본 밖의 소량이며 이 값을 최종 절대 상한으로 읽지 않는다. 원시 자료를 삭제하지 않는다. T: 여유량은 manifest의 13,215,363,072B와 단계별 volumeObservations로 보존하며 새 두 소유 저널이 회수돼도 공유 볼륨 쓰기량의 정확한 귀속을 제공하지 않는다.

현재 범위 저장·동일 private 쓰기/작은 변경 범위·bounded 압축·명시적 정리·작은 기본값 metadata와 소유 회수를 유지한다. 같은 raw 입력의 압축 절감은 [JP](journal-payload-compression.md), 기본값의 추가 저널 파일 절감은 [JM](journal-metadata-storage.md)의 별도 고정 대조 근거다. 이번 단계의 서로 다른 Editor 활동을 이전 후보와 직접 비교해 추가 절감률을 만들지 않는다. 현재 후보의 수명·정리·잔여 비용이 설명되고 데이터/원본/peer 보호가 통과했으므로 물리 저장 개발 단위를 닫는다.

ordinary VSS 사용량 조회는 실패를 unknown으로 남긴다. 볼륨 여유량은 다른 쓰기와 VSS가 포함되는 공유 관측이며 파일 할당의 합계나 작업별 VSS 귀속·전체 공간 절감률로 환산하지 않는다. compaction의 두 journal 세대 할당은 전체 workspace 순간 peak가 아니다. MFT/디렉터리·kernel/OS cache·전체 물리 peak와 RAM 개선은 unknown이다.

기존 C: reclaim-pending/discard-reclaim-pending·비소유 snapshot은 호출/삭제 대상으로 삼지 않는다. 이번 두 T: snapshot의 회수가 C: 보류 해소나 전체 볼륨 snapshot 0을 뜻하지 않는다. SF-02/03 Partial·SF-04~06 NotRun·대형 PV/다른 입력/VCS/ARM/OS·디스크 고갈 지원의 상태를 유지한다. 후속 메모리는 별도 ME 범위이며 새 공간 작업은 새 실사용 부족/보존 결함/명시적 지원 확대 요청으로만 연다.
