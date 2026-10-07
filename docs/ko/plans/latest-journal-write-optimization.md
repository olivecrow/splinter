# 현재본 저널의 쓰기 경로 최적화

[English](../../plans/latest-journal-write-optimization.md)

2026-10-06 사용자의 명시적 쓰기 경로 최적화 요청이다. 원본 + 최신 변경본과 개별 mutation의 durable ACK를 유지하면서 SJL2/SJT2의 중복 C 기록과 직렬 파일 flush 대기를 줄인다. 초기 구현에서는 기본 OFF를 유지했으며 commit/push·서비스/ACL/설치·원본/Unity Editor·기존 closed A/B를 변경하지 않는다.

- 현재 후보는 SJL4/root v4 capsule·SJB1 owner header다. 이전 SJL1/2/3·SJT2/3는 보존·거부한다. overlay/workspace v2·candidate v3/SJR2·compact intent v3는 유지한다.
- C payload와 metadata를 한 pending capsule에 한 번 기록하고 개별 mutation마다 한 durable flush한 뒤 현재 root에 게시한다. 이전 root는 복사 없이 소유 backup으로 이동하고 대체된 payload/metadata를 회수한다. 다른 mutation의 flush/ACK를 묶지 않는다.
- 불완전 C/no publication은 B 유지, 완전 C는 같은 writer 복구, 손상/unknown/게시 뒤 누락은 보존·거부한다. 변경한 file/process 경계·candidate/OFF/restore/seal·WinFsp 정상 callback만 검증한다. OS 전원/Unity/VSS/대형 장애 행렬은 열지 않는다.
- 동일 8/32 committed PNG, 128KiB 범위 B→C 반복 입력에서 ON/OFF 각 3회를 비교한다. 파일 hash·경로 sequence·metadata·seal·현재 payload/NTFS 할당과 쓰기 latency를 확인한다. hash/할당 계측은 timed writes 밖에 있으며 benchmark와 build/test를 겹치지 않는다.

중간 SJL3 후보는 payload 한 번 기록과 독립 파일 4개 flush 병행으로 시간 7~8%만 개선했다. 해당 runner/입력/수치는 보존하고 단일 flush capsule로 개선한다. 종료 기준은 단일 payload 기록의 복구 근거, 같은 입력의 공간/시간 개선과 잔여 비용, 필수 `pwsh scripts/build.ps1`·현재 계약/capabilities/계측 문서 갱신이다. [검증](../verification/latest-journal-write-optimization.md)이 입력·수치와 남은 제한을 소유한다.

## 마감

사용자의 후속 요청으로 새 작업의 초기 기본값을 ON으로 바꾼다. 기존 작업과 옵션을 생략한 compact는 저장된 선택을 유지하고 명시적 false는 OFF를 선택한다. 새 ON 생성의 identity/seed/root 게시 중단 복구, 기존 빈/비어 있지 않은 OFF 재연결, 기본 ON의 반복 저장·compact·OFF 전환과 필요한 WinFsp callback을 검증한다. 서비스/설치/원본/기존 A/B는 변경하지 않는다.

기본값 후속 변경도 마감했다. 필수 locked restore·전체 Release build 경고/오류 0, 전체 1,165개 중 1,155 통과·실패 0·opt-in 10 skip과 기본 ON의 실제 WinFsp/fake 기준 1/1·composite seal/accounting 일치를 확인했다. [후속 검증](../verification/latest-journal-write-optimization.md#새-작업-기본-on)의 현재 소스/로그와 기존 성능 입력의 근거를 구분한다.

2026-10-06 scoped-complete-with-limitations. 단일 payload/metadata capsule과 mutation마다 한 durable flush, 증분 writer 바이트 장부·실제 관측/seal/재연결 검사·이전 payload/metadata 회수를 연결했다. 같은 실제 PNG 8/32파일 각 3회의 ON 쓰기는 29.5%/30.7% 감소했고 ON/OFF 파일 데이터 할당은 -91.75%/-93.61%, 모든 cycle의 ON 할당은 일정했다. 12 accounting/product seal과 원본 보존/소유 basis release, 필요한 WinFsp 1/1과 전체 1,144개 중 1,134 통과·실패 0·opt-in 10 skip을 확인했다. ON은 OFF의 5.6–5.8배이며 관리 누적 할당은 늘었다. 당시에는 기본 OFF를 유지했다. 이후 사용자가 개별 쓰기의 절대 시간과 파일 할당 감소를 근거로 새 작업의 기본 ON을 명시적으로 선택했다. RAM 절감은 주장하지 않는다. [검증](../verification/latest-journal-write-optimization.md)에 전후 수치·원시 근거·미자격을 남겼다.
