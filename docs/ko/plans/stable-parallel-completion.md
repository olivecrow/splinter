# 안정적인 병렬 작업의 확정 범위

[English](../../plans/stable-parallel-completion.md)

물리 저장 PS·메모리 ME 뒤의 다음 개발 단위다. 질문은 **A의 명령이 진행 중일 때 B의 조회·같은 작업 재개·파일 편집이 독립적으로 진행되고, 동일 작업의 명령은 직렬화되며, 잠금 경로의 실제 오류를 점유로 오인하지 않는가**다. 기존 PV/C/S/Q 비교와 Unity 인계 자격을 다시 실행하는 계획이 아니다.

**SP-01~04를 scoped-complete-with-limitations로 마감했다.** [직접 근거](../verification/stable-parallel-completion.md)는 오류 재현·수정, 실제 겹침/64개 쓰기·독립 결과/회수와 미자격을 소유한다. 다음 비교나 전체 장애 행렬을 자동 추가하지 않는다.

## 포함과 종료 조건

| ID | 포함 | 종료 조건 |
| --- | --- | --- |
| SP-01 | 실제 잠금 오류 재현·수정 | 누락된 잠금 디렉터리를 점유로 오인하는 현재 결함을 재현. 확인한 sharing/lock violation만 기존 5초 한정 대기하며 다른 I/O·접근 오류는 `StateReadUnavailable`로 즉시 보존·거부. 입력/상태/응답 형식과 retry 분류는 유지 |
| SP-02 | 공통 수명·잠금 회귀 | 같은 key의 명령이 한 번만 생성·진행하고 소유자가 닫힌 뒤 같은 잠금을 재사용. 대기 취소는 소유 핸들을 건드리지 않음. A의 종료 대기 중 B의 status/같은 ensure가 끝남. 기존 프로젝트/기준/snapshot 보호는 유지 |
| SP-03 | 실제 명령·파일 I/O 겹침 | 기존 등록된 작은 T: 원본에 새 A/B를 한 번만 생성. 각 workspace에 다른 source payload를 쓰고, A checkpoint/release와 B status/ensure·명시적 쓰기를 별도 프로세스로 겹침. 명령 시작/종료 구간·진행 중 쓰기 hash·같은 id/digest/base/snapshot·A 회수 뒤 B 추가 편집과 독립 결과/최종 회수를 확인 |
| SP-04 | 보호·한정 마감 | 원본/Plastic 등록·기존 records/results·원본 Editor identity 보존, 소유 provider/핸들/메모리/mount/delta/두 snapshot 부재. 필수 build·응답 schema·문서 UTF-8/링크 확인 후 현재 알파의 선택 단위를 마감 |

## 고정 입력과 실행

원본 `T:\plastic-fixture-20261002`, home `T:\pt1002`, keys `sp-t-a-20261003`·`sp-t-b-20261003`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, budget 17,179,869,184B다. evidence는 `artifacts/stable-parallel-completion-20261003/`에 신규 게시하며 반환된 workspace path만 편집한다. 새 source 파일은 각 작업의 128KiB 반복 payload와 32개의 작은 진행 marker로 고정한다. 원본·peer에 파일을 쓰지 않는다.

이번 native 경계는 **Editor 없는 두 mounted 제품 작업의 CLI/파일 I/O 동시 진행**이다. 현재 Unity/도구의 과거 독립 Editor·loaded-code 자격은 그 소유 검증을 유지하며 이번 통과로 승계하지 않는다. B 쓰기는 완료된 A checkpoint 또는 release의 단순 후속만으로 대체하지 않고 실행 구간 겹침을 증거로 요구한다. snapshot 서비스의 보호된 writer 직렬화·프로젝트 등록 잠금·동일 workspace의 5초 점유 응답은 정상 경계다. 동시 속도 향상률이나 준비 시간 SLA를 새 합격 조건으로 만들지 않는다.

이 두 새 시험 작업의 명시적 checkpoint/release는 구현 검증의 일부다. 실패·unknown은 같은 identity와 evidence로 보존하며 다른 작업/비소유 snapshot을 회수하거나 기존 C: 보류를 우회하지 않는다. 원본 반영·discard·Editor/MCP/Unity CLI/Play/Test/build·설치/등록/서비스/볼륨 변경·대형/ARM/원격/강제 중단·새 전체 행렬은 제외한다. 결함 수정/선택 질문과 회귀를 완료하면 마감하고 다음 비교나 harness를 자동 추가하지 않는다.
