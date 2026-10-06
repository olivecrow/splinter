# 현재본 저널 쓰기 경로 최적화 검증

2026-10-06 사용자의 [명시적 범위](../plans/latest-journal-write-optimization.md)와 [현재 계약](../latest-only-journal.md)이다. 현재 후보는 SJL4/root v4 capsule·SJB1 80B owner header이며 overlay/workspace v2·candidate v3/SJR2·compaction intent v3를 유지한다. 이전 SJL1/2/3·SJT2/3는 보존·거부하며 자동 이관하지 않는다. 현재 파일/프로세스/fake 기준 WinFsp 자격을 과거 Unity/VSS/대형/OS 전원 자격으로 확대하지 않는다.

## 저장과 복구 경계

C payload와 metadata를 한 capsule에 한 번 기록하고 mutation마다 한 `Flush(true)` 뒤 현재 root에 게시한다. 이전 root는 `File.Replace`의 소유 backup으로 이동하며 복사하지 않는다. 현재 private block만 남으면 이전 metadata footer를 truncate하고, 참조가 없으면 소유 hash 검증 뒤 삭제한다. 대체된 payload/metadata 회수 뒤 성공을 반환한다. 다른 mutation의 flush/ACK를 합치지 않는다.

writer의 예산 검사·ACK 관측용 전체 디렉터리/파일 길이 재합산을 제거했다. 최초 검증한 실제 합계에서 새 capsule 게시와 정확한 소유 파일 회수 길이를 증분 갱신한다. 명시적 관측·seal·재연결에서는 실제 파일 inventory·내용 checksum을 검사한다. metadata 장부는 payload cache나 소스 내용 검사 대체가 아니다.

`JournalLatestOnlyTests`는 incomplete C의 B 유지, complete C의 게시/회수, complete capsule 손상/unknown 파일 보존, 게시 뒤 현재 root 누락 거부, 이전 format 거부를 검증한다. 512KiB C를 한 번 포함한 pending 길이와 복구, payload/metadata mutation당 flush 1회, 여러 private block의 일부 교체/metadata truncate/재연결 후 증분 바이트 수의 실제 합계 일치를 확인한다. `JournalLatestProcessTests`는 exact nonce/PID/start 소유 자식의 intent prefix·block prefix·stage 완료·root 게시·이전 회수 뒤 종료/재연결 5개 경계다. fake immutable basis이며 OS 전원 손실 자격은 아니다.

중간 SJL3는 payload 단일 기록과 독립 파일 4개 flush 병행으로 시간 7~8%만 개선했다. 이어 단일 flush SJL4만 적용한 후보도 32파일 쓰기 중앙값이 이전보다 개선되지 않았다. 기존 benchmark의 단계 계측으로 예산/ACK의 전체 파일 길이 합산 비용을 확인하고 증분 장부를 적용했다. 진단 `artifacts/latest-capsule-profile-20261006/`의 boundary 합계는 마지막 고정 metadata 작업도 포함하며 정규 쓰기 latency로 사용하지 않는다.

## 고정 입력과 측정

`splinter-unity @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1` committed PNG 32개(47,854,812B)를 사용한다. 프로필은 8파일/2범위/6회 B→C(192 writes), 32파일/3범위/8회 B→C(1,536 writes)이며 범위는 128KiB, offset은 65,536 + range×262,144다. 최종본은 기준 bytes로 돌아가지 않는다. cycle 도중 compact하지 않는다.

각 runner OFF/ON 3회, OFF/ON→ON/OFF→OFF/ON으로 실행했다. 세대는 이전 SJL2→중간 SJL3→SJL4 단일 flush→최종 SJL4 증분 장부 순서이며 각 immutable runner hashes와 실제 입력을 보존한다. build/test와 측정을 겹치지 않는다. seed/activation/할당 계측/마지막 고정 metadata/전체 SHA 검증은 timed writes 밖이다. 재연결 시간은 lease/tree/replay/composite seal을 포함한다. 전체 현재 저널의 NTFS 파일 데이터 할당을 합산하고 cycle별 ON payload/할당과 최종 path/metadata/전체 파일 hash/seal을 확인한다. OS cache를 비우지 않았고 실제 OS physical I/O/MFT/전체 순간 peak/공유 귀속은 unknown이다.

각 3회 중앙값이다. 이전 ON은 SJL2/SJT2이며 현재 OFF는 최종 runner의 같은 입력이다.

| 항목 | 8파일/192 writes | 32파일/1,536 writes |
| --- | ---: | ---: |
| 이전 ON 쓰기 | 3.947s | 32.299s |
| 최종 ON 쓰기 | 2.783s | 22.389s |
| ON 시간 감소 | **29.5%** | **30.7%** |
| 최종 OFF 쓰기 | 0.498s | 3.834s |
| 최종 ON/OFF 비율 | 5.58× | 5.84× |
| ON p50 이전 → 최종 | 20.457 → 14.431ms | 20.780 → 14.282ms |
| ON p95 이전 → 최종 | 22.483 → 17.431ms | 23.503 → 17.159ms |
| 최종 OFF 파일 데이터 할당 | 26,214,400B | 203,030,528B |
| 최종 ON 파일 데이터 할당 | 2,163,040B | 12,976,480B |
| 최종 ON/OFF 할당 감소 | **91.75%** | **93.61%** |
| ON 재연결 이전 → 최종 | 1.138 → 1.110s | 1.392 → 1.387s |
| timed write별 durable flush | 1회 | 1회 |

최종 ON 쓰기 3회 범위는 8파일 2.768–2.793s, 32파일 22.113–22.478s다. 최종 OFF는 각각 0.485–0.555s/3.681–3.864s다. 이전 32파일 ON에는 42.754s의 느린 한 trial이 있고 중앙값은 32.299s다. 버전 순서와 OS cache 차이가 있으므로 결과는 이 로컬 입력·시점의 관측이다.

최종 ON은 6/8 cycle 모두 같은 파일 데이터 할당이며 current payload는 각각 2,097,152B/12,582,912B다. 8배 writes에서 전체 ON 시간도 약 8.05배이고 p50은 14.43/14.28ms다. 이 입력에서는 반복 이력이 쌓여 마지막 저장이 점점 느려지는 비용 대신 개별 쓰기의 게시/회수 비용이 남는다. 서로 다른 파일/범위/cycle 프로필 두 개이므로 모든 입력에 대한 복잡도 증명은 아니다. 모든 12 stopped accounting의 bytes/hash/sequence가 product seal과 일치하고 6 ON 저널의 대체 payload는 0이다. 원본 HEAD/status/전체 refs/32 파일 hash는 전후 동일하고 소유 basis는 blocking 없이 released다.

관리 누적 할당은 이전 ON 32,335,768B/259,586,160B에서 최종 139,502,608B/1,135,364,408B로 늘었다. capsule/block 검증용 배열과 metadata 작업도 관리 할당을 만들지만 항목별 귀속은 따로 계측하지 않았다. 시간/파일 할당 개선을 메모리 절감으로 읽지 않는다. 이 값은 누적 관리 할당이고 retained heap/전체 RAM/순간 peak는 별도 미측정이다.

필수 `pwsh ./scripts/build.ps1`: locked restore·Release build 경고 0/오류 0, 전체 1,144개 중 1,134 통과·실패 0·명시적 opt-in 10 skip이다. 최종 source/runner hashes, build log/TRX를 보존했다. `git diff --check`, UTF-8 문서 검사, Python syntax 확인을 통과했다.

**scoped-complete-with-limitations**. 쓰기 경로 최적화와 현재본/복구 경계를 확인했다. 처음에는 ON/OFF 배수를 이유로 기본 OFF를 유지했으나, 사용자가 1,536회 전체 22.389s와 개별 쓰기 평균 약 14.6ms를 개발 과정에서 허용할 비용으로 판단해 새 작업의 기본 ON을 명시적으로 선택했다. 기존 작업의 저장된 OFF/ON은 유지한다. 이 판단은 Unity 저장/import 시간을 측정했다는 뜻은 아니다.

## 새 작업 기본 ON

후속 요청의 기본값은 새 제품 저널에만 적용한다. 기존 빈/비어 있지 않은 SJR2 OFF와 SJL4 ON은 재연결 입력의 초기 기본값과 관계없이 저장된 선택을 유지한다. nullable compact 옵션을 생략하면 현재 선택을 유지하고 명시적 false만 OFF를 선택한다. CLI 설명·request schema·capabilities·에이전트 진입 지침을 함께 갱신했다. 개발용 Library cache와 고정 SJR2 write/kernel probe의 별도 계약은 명시적 OFF로 유지한다.

`JournalDefaultTests`는 새 기본 ON의 반복 저장/현재 payload·재연결 seal, 기존 빈/비어 있지 않은 OFF, 초기 intent·identity·objects·root flush·게시의 7개 중단 경계, 다른 owner·unknown object·손상 root 보존, ON의 옵션 생략 compact·명시적 OFF·이후 생략/재연결을 검증한다. ON→ON compact가 이미 소유한 current writer guard를 다시 열던 오류도 수정했다. 후보 내보내기는 composite source seal로 보존을 대조하며 Windows 폐기 시험은 OFF/ON 각각의 sealed inventory와 부분 회수 receipt를 확인한다.

기본 ON을 따로 활성화하지 않은 새 저널의 실제 WinFsp/fake immutable basis 검증은 8파일/4회 저장·2 context unlinked 읽기·마지막 Close·정상 unmount/reconnect 1/1 통과다. `native/accounting.json`은 product composite seal과 bytes/hash/sequence가 일치하고 현재 payload 65,536B·대체 payload 0이다. 생성 중단 시험은 파일 수준의 명시적 observer 경계이며 OS 전원이나 실제 생성 중 프로세스 종료 자격으로 확대하지 않는다.

기본 ON 변경 후 필수 `pwsh ./scripts/build.ps1`의 locked restore·전체 Release build는 경고/오류 0이며 전체 1,165개 중 1,155 통과·실패 0·명시적 opt-in 10 skip이다. 초기 실행의 OFF 단일 파일 hash/frame/회수 receipt 가정은 해당 표현의 시험으로 고정하거나 ON의 composite seal·형식별 receipt로 보강한 뒤 전체 회귀를 통과했다. 최종 `build.log`/`build.trx`·현재 소스 snapshot/hash·native source/runtime hash와 accounting을 별도 근거 폴더에 보존했다. 기존 성능 수치는 다시 측정하지 않았다.

## 근거와 한계

기본 ON 후속 검증은 `artifacts/latest-default-on-20261006/`에 별도로 보존한다. 위 8/32파일 시간·할당 수치는 기본값 변경 전 동일 SJL4 쓰기 경로의 frozen runner 측정이다. 기본값 변경을 이유로 같은 전체 성능 측정이나 기존 제품 A/B를 다시 실행하지 않는다.

최종 근거는 `artifacts/latest-capsule-indexed-write-20261006/`의 input, runner hashes, source before/after, result/cycle samples, basis release, stopped accounting·seal 대조, build log/TRX, native result다. SJL2/SJL3는 `artifacts/latest-write-path-20261006/`, 단일 flush만 적용한 SJL4는 `artifacts/latest-capsule-write-20261006/`로 보존한다. 과거 형식은 당시 frozen runner에서만 읽으며 현재 제품에 fallback reader를 추가하지 않는다.

8파일/4회 저장·2 context unlinked 읽기와 마지막 Close·정상 unmount/reconnect의 실제 WinFsp/fake immutable basis 검증은 최종 후보 1/1 통과다. native stopped accounting과 product composite seal의 bytes/hash/sequence가 일치하고 현재 payload 65,536B·대체 payload 0이다. 원본 Unity Editor/서비스/ACL/설치·closed A/B를 변경하지 않는다. 새 작업 기본 ON과 기존 저장 선택 유지, 별도 선택/상태·candidate/OFF/restore/seal·source/peer/target guard를 유지한다. 원본 HEAD/status/전체 refs/32개 파일 hash는 전후 대조하고 새 basis의 소유 ref만 release한다.

이 입력은 실제 committed PNG의 NTFS Runtime 범위 쓰기다. Unity Editor/import/게임 검증·VSS·대형 프로젝트/OS 전원/전체 RAM 개선은 NotRun 또는 unknown이며 기존 자격을 승계하지 않는다. 원자 게시/소유 hash 검증·회수와 부분 private block 재작성 비용이 남는다. 보존 파일 데이터 할당 감소를 실제 장치 쓰기량 감소와 동일하게 읽지 않는다.
