# 준비·관측 처리량

[English](../../plans/preparation-throughput.md) · [검증](../verification/preparation-throughput.md)

2026-10-26 수명주기 검토 후속. 기준: `448f029a31a7c6f29d14d868c5e5f9e11aff5b19`.

## 범위와 채택 기준

반복되는 제어 파일 부재 대기와 None/recorded Plastic 소스 scan을 최적화한다. Start의 배타 client 잠금은 다른 start를 배제하며 같은 intent/receipt 관측을 재사용하고 부재 대기를 생략한다. 기존 intent의 자식은 receipt를 나중에 게시할 수 있으므로 intent가 있는데 receipt가 없으면 unknown으로 보존하고 같은 요청의 재시도로 다시 관측한다. 일반 관측은 게시 유예를 유지하며 동시 receipt/intent 읽기를 모두 drain한다. 살아 있는 project task는 release/discard authorization이 없을 때 release receipt 부재를 즉시 확인한다. task capture/release와 basis dependent attach는 기존 잠금으로 동시 게시를 배제한다. 공유 오류·손상·소유/세대 불일치·미해결 launch의 거부/unknown은 유지한다.

파일 목록과 한도를 먼저 확정하고 None은 최대 8 worker로 전체 내용을 hash한다. Plastic mount scan은 reader가 revision 증명/stream 하나를 유지하므로 파일 간 교차 읽기가 반복 퇴출을 유발하지 않도록 직렬을 유지한다. 실패/취소에도 모두 join한다. 권위 있는 source-change provider가 있을 때만 None/recorded Plastic 최초 검사에서 연산 한정·1회성 증명을 반환한다. 최초 전체 scan과 후속 metadata-only 목록 scan 각각의 전후에 완전한 변경 세대가 같아야 한다. owner/basis/mount/request/policy·epoch·sequence·history를 재검증하고 Plastic 등록 격리도 다시 검사한다. Editor 시작 시 증명을 버린다. tracker 없음/audit는 전체 검사를 유지한다. 영속 cache·형식/schema 변경은 없다.

PR-03은 실제 NTFS/WinFsp CLI에서 Editor 없는 `project-snapshot`/None 작업공간 하나씩 측정한다. 새로운 임시 2.125 GiB·2,112파일(64 × 32 MiB + 2,048 × 64 KiB), 같은 내용/mtime, 하나의 공통 프로젝트 기준본, 고정 8바이트 수정 + 64 KiB 추가, 전→후→후→전 각 2회를 사용한다. ensure·edit·checkpoint·별도 release를 측정하고 소스/결과와 최종 일반 NTFS 복원을 독립 검증한다. 실행 파일 두 묶음을 고정한다. 채택 기준은 ensure 평균 20% 및 10초 이상 절감과 동일 입력/결과·소유 회수 완료다. 비교별 약 13 GiB, 예비 근거 보존을 포함해 약 26 GiB 임시 입력/결과를 사용하고 측정 중 build/tests를 겹치지 않는다. OS cache를 강제 비우지 않는다. 준비/내용 증명·공통 prepare/restore는 별도 기록한다.

| ID | 작업 / 근거 |
| --- | --- |
| PR-01 | 게시 배제·한정 관측 대기·미해결 launch/소유 보호 유지. |
| PR-02 | 제한 병렬 전체 scan·1회성 검사 증명; 변경·epoch·metadata·목록·identity·Plastic 격리·fallback 시험. |
| PR-03 | 같은 입력의 실제 ABBA 제품 시간·정확한 회수/복원. |
| PR-04 | locked restore/build/전체 시험·양 언어 문서·수명주기 지도/색인. |

독립 결과 payload/영속 flush·저널 ACK/seal·원본 반영·폐기·회수 보호는 유지한다. Unity 시작/컴파일/Play/Test/build·실제 Plastic 서버·원본 반영/폐기 시간·전체 peak RAM·전원 장애 자격은 측정하지 않는다. 폐기 backend나 과거 실험을 재개하지 않는다. 증분 저널 checkpoint·복원 재설계·호출 간 결과 증명 cache는 별도 작업이다.
