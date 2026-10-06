# 현재본 저널 ON의 검증과 비용

이 문서는 쓰기 경로 최적화 전 SJL2/SJT2 후보의 보존된 측정이다. 현재 형식은 SJL3/SJT3이며 [쓰기 경로 검증](latest-journal-write-optimization.md)이 현재 자격과 비교 수치를 소유한다. 아래 과거 결과를 현재 형식의 통과로 승계하지 않는다.

2026-10-06 사용자 요청의 [범위](../plans/latest-only-journal.md)·[현재 계약](../latest-only-journal.md)이다. `f67f217`의 stopped 정리 최적화 다음에 SJL2/root v2·SJT2와 v3 compact intent를 구현했다. 과거 JR/JP/M0/PV·Unity 자격은 현재 표현의 통과로 승계하지 않는다. 당시 최초 기본 OFF를 유지했다. 현재 기본값은 [후속 쓰기 경로 검증](latest-journal-write-optimization.md)을 따른다.

## 실제 파일 반복 변경

고정 실제 Unity 프로젝트 `splinter-unity @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`의 committed PNG 32개(47,854,812B)를 사용했다. 기준은 Git raw blobs이며 원본 파일을 편집하지 않았다. 입력·runner hashes·원본 전후 HEAD/status/전체 refs/32개 SHA-256, 각 cycle 표본과 소유 Git basis release는 `artifacts/latest-only-journal-20261006/final2-*`에 있다.

- 8파일: 파일마다 128KiB의 2범위, B→C를 6회 교차 저장, 총 192 writes.
- 32파일: 파일마다 128KiB의 3범위, B→C를 8회 교차 저장, 총 1,536 writes.
- B/C는 기준 bytes의 xor 0x55/0xaa로 만들었다. 최종본은 기준본으로 돌아가지 않는다. ON은 빈 저널에서 선택 후 시작하며 쓰기 사이 compact를 실행하지 않았다.
- 각 입력 OFF/ON 3회, 순서 OFF/ON→ON/OFF→OFF/ON. 실제 NTFS에서 Runtime을 직접 사용했다. 파일 seed/hash 준비, 선택/초기화, cycle allocation 계측, 마지막 고정 metadata와 전체 파일 hash 검증은 쓰기 시간에 포함하지 않았다.
- 재연결 시간은 Git lease/현재 관측/tree 준비, journal open/replay와 composite seal이다. 전체 파일 SHA-256 검증은 별도로 완료했다. OS cache를 비우지 않아 warm/cold는 unknown이다. benchmark 중 build/test를 겹치지 않았다.

아래는 각 3회의 중앙값이다. 할당은 마지막 write cycle 직후 root와 전체 현재 object 파일의 **NTFS 파일 데이터 할당 합계**이며 마지막 고정 metadata 부여 전 표본이다. MB는 1,000,000B다.

| 입력 | OFF 최종 할당 | ON 최종 할당 | 감소 | OFF 쓰기 | ON 쓰기 | OFF 재연결/seal | ON 재연결/seal |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 8파일 / 192 writes | 26.018MB | 2.131MB | 91.81% | 0.503s | 4.040s | 1.040s | 1.153s |
| 32파일 / 1,536 writes | 203.162MB | 12.715MB | 93.74% | 3.998s | 33.351s | 1.358s | 1.347s |

ON은 모든 cycle/모든 반복에서 8파일 **2,130,600B**, 32파일 **12,714,672B**로 일정했다. OFF는 첫 cycle 중앙값 4,390,912B→26,017,792B, 26,148,864B→203,161,600B로 증가했다. stopped SJL2 6개 모두 obsolete payload 0, 현재 raw payload는 각각 2,097,152B/12,582,912B다. 최종 metadata 부여 뒤 journal bytes는 2,105,474B/12,621,544B이며 파일 수는 27/131개다. 최종 전체 파일 내용·entry·경로별 sequence를 재연결 후 대조했다.

쓰기 시간 범위는 8파일 OFF 0.491~0.602s/ON 3.992~4.133s, 32파일 OFF 3.862~18.887s/ON 32.914~65.879s다. 32파일 마지막 반복에서 두 모드 모두 큰 변동이 있었으며 원인은 확정하지 않았다. 유리한 반복만 제외하지 않았다. write별 P50 중앙값은 8파일 OFF 2.440ms/ON 20.956ms, 32파일 OFF 2.373ms/ON 21.002ms, P95는 3.724/23.571ms, 3.604/24.663ms다. 작업량 증가에 따른 누적 이력 재생 부담은 제거했지만, 현재 표현은 매 mutation의 redo·metadata/root flush·파일 게시·회수 비용으로 쓰기 시간이 증가한다.

관리 누적 할당 중앙값은 8파일 OFF 290,104B/ON 32,337,056B, 32파일 OFF 2,274,328B/ON 259,621,496B다. 이는 모든 writes 동안의 누적 관리 할당이며 peak heap/RAM이 아니다. byte cache·전체 source copy-up은 0이나 ON의 관리 할당 개선을 주장하지 않는다. MFT·전체 순간 공간/메모리·OS 물리 I/O·공유/VSS 귀속은 unknown이다.

초기 SJL1 prototype은 `run/`과 frozen `runner/`에 따로 보존했다. 새 입력을 반복해 같은 파일과 순서로 확인한 SJL2는 JSON-base64 pending을 raw bytes로 바꾸고 파일별 검사/읽기 핸들 중복을 줄여 prototype ON 중앙값 7.750s→4.040s(8파일), 60.985s→33.351s(32파일)로 감소했다. 환경 변동과 형식 차이가 있어 통제된 단일 원인 속도 비율로 주장하지 않는다. prototype은 현재 reader의 지원 형식이 아니다.

초기 SJL2 `final-run/`은 32파일 metadata `File.Replace` 거부로 중단했다. pending/임시/저널을 보존했고 같은 ID/기준 writer reconnect로 redo·seal을 검증한 `recovery.json`과 `basis-release.json`을 남겼다. 실패한 측정은 위 표에 넣지 않았다. 현재는 provider IPC와 같은 Windows 32/33/1175에만 같은 flushed 임시 파일로 최대 1초 게시 재시도를 적용하며, 접근 거부·손상·다른 소유권은 거부한다. 별도 reader-without-delete-sharing 회귀를 확인했다. 최종 `final2-run/basis-release.json`은 released이며 원본 HEAD/status/전체 refs/32개 hashes는 전후 동일하다.

## 복구와 연결 경계

| 행 | 확인 범위 | 증거 / 자격 |
| --- | --- | --- |
| LJ-01 | 매 ACK 뒤 현재 payload만 유지·16파일 12회·raw/Brotli 부분 대체/truncate·metadata/rename/whiteout·OFF/ON 전환·candidate·restore ranges | `JournalLatestOnlyTests`·`JournalBaseReversionTests`; fake immutable basis / 실제 NTFS 파일 |
| LJ-02 | incomplete redo의 B 유지·완전 redo의 C 마무리·11 observer 경계·checksum/metadata 손상 거부·unlinked 마지막 close의 같은 source sequence 복구 | `JournalLatestOnlyTests`; injected file 경계, OS 전원 손실 자격 아님 |
| LJ-03 | 정확한 nonce/PID/start의 자기 테스트 자식 종료: redo prefix / root 게시 / 이전 object 회수 뒤 | `JournalLatestProcessTests`, 3경계; 실제 로컬 process / fake basis, WinFsp kill 아님 |
| LJ-04 | 실제 WinFsp 8파일 4회 저장·두 context를 가진 unlinked 파일 읽기·마지막 Close 회수·정상 unmount/reconnect | 최종 `native-final2/result.json`·동일 이름 `.log`; installed WinFsp / fake immutable basis, VSS/Git/Unity 제품 A/B 아님 |
| LJ-05 | stopped journal 12개의 현재/obsolete accounting·전체 object composite SHA/bytes/sequence를 Runtime seal과 대조 | `final2-accounting/`; 현재 6개 obsolete 0, Brotli 내용 복원은 Runtime 검증 |
| LJ-06 | exact-inventory release/이전 세대 부분 회수·writer/wrong-seal/unknown 거부·projection 보호 집계·관리형 원본의 이동/범위 복원·schema/CLI/저장된 ON | 관련 자동 회귀와 필수 `pwsh scripts/build.ps1`; 최종 실행 결과는 마감 기록에 연결 |

새 Unity import/Play/Test/build·원본 Editor 반영·VSS/전체 제품 A/B·대형·ARM/macOS/Linux·OS 전원 장애는 NotRun이다. fixture 통과를 일반 지원으로 확대하지 않는다. 최종 목표는 현재본 유지이고, 쓰기+재연결 시간 10% 초과 회귀를 허용하지 않던 기본 ON 조건에는 미달한다. **공간 목적은 달성했지만 기본 OFF를 유지한다.**

## 최종 마감

`pwsh scripts/build.ps1`의 locked restore·전체 Release build는 경고/오류 0, 전체 시험은 **1,128 total / 1,118 passed / 0 failed / 10 skipped**다. `build-final2.log`·`build-final2.trx`가 최종 근거다. 명시적 외부/자식/native entrypoint 10개는 일반 전체 실행에서 skip하며, 선택한 WinFsp entrypoint는 별도 `native-final2.log`에서 1/1 통과했다. 관련 경계만 먼저 재검증한 `recheck-tests.log`는 148/148 통과다.

앞선 `build-final.log`·`build-final-failed.trx`의 27개 실패는 capabilities의 고정 schema 값, OFF projection 거부의 기존 오류 우선순위, basis reader 종료 뒤 restore의 private-only seal 검사 연결에서 발생했다. 각 원인을 수정하고 ON↔OFF projection 보호와 현재본 복원/중단, 변경된 pending temporary 보존 회귀를 추가해 최종 전체 통과를 확인했다. 실패 실행을 최종 자격으로 합산하지 않았다.

시간/할당 수치는 `final2-runner`의 hash 고정 바이너리를 사용한다. 이후 수정한 projection 거부·private-only restore seal·손상 temporary prefix guard의 기능 자격은 최종 전체 시험이 소유하며, frozen 측정과 구분한다. 원본·peer·기존 보호/독립 결과를 보존했다. 이번 요청의 구현/고정 측정은 한정 마감하며 추가 commit/push·기본값 변경·Unity/VSS/A/B 확대를 수행하지 않았다.
