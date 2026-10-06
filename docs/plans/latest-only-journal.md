# ON의 현재 변경본 저장과 범위 회수

2026-10-06 사용자 의도 수정에 따른 구현 범위다. 기준본 복귀만 제공하던 `f67f217` 다음에 진행한다. ON은 원본 불변 기준 + 현재 private 변경 범위 + 완료되지 않은 한 쓰기의 복구 정보만 유지한다. 완료는 기존 개별 mutation의 durable flush/성공 응답 경계다. Unity 작업 전체의 완료를 추정하거나 원본/Library/Editor/서비스를 변경하지 않는다. 기본 OFF는 유지한다.

기존 compact 선택 ON을 명시적으로 적용하면 이후 writer/reconnect가 현재본 저장을 사용한다. 표시 명칭은 journal optimization이며 기존 `--revert-base-ranges` 입력은 같은 현재 명령의 alias로 유지한다. 기준본 내용 비교는 명시적 compact에서만 수행하고 매 쓰기에서 읽지 않는다. 현재 OFF의 SJR2와 ON의 current-root 표현을 구분하며 오래된 intent/root 형식은 보존·거부한다. 기존 상태를 ensure에서 자동 변경하지 않는다.

ON은 bounded private payload 블록과 파일 ID별 현재 metadata·경로 bucket을 사용한다. 변경한 block/node/bucket만 새로 쓴다. 한 SJT2 redo를 먼저 flush하고 해당 파일과 작은 SJL2/root v2를 각각 flush·원자 게시한 뒤 미참조 이전 payload만 회수한다. 완전한 redo가 없으면 B를 유지하고, durable redo가 있으면 C root 게시 전 중단도 C로 마무리할 수 있다. 부분 대체 블록은 남은 private slice만 bounded block으로 다시 저장하므로 작은 편집도 해당 private block의 재기록 비용이 들 수 있다. 원본 바이트를 읽어 채우거나 기준 파일의 전체 변경본을 materialize하지 않는다. known 미완료 tail만 제거하고 unknown/손상·다른 identity 파일을 sweep하지 않는다. compaction intent는 v3만 지원한다.

같은 version의 응답 유실/각 publication 경계 복구, 최신 bytes/metadata/path/sequence/seal, rename/delete/truncate/new incarnation, partial overlap·압축·raw, 한 writer·peer 보호를 검증한다. checkpoint/candidate/restore/seal/관측과 OFF 전환에 새 표현을 연결하며 오래된 native 자격을 승계하지 않는다.

고정 계측은 실제 PNG 8/32파일에 128KiB 범위를 교차해 여러 번 저장하는 기존 입력과 신규 작은 부분 덮어쓰기다. 순환 중 저장 공간을 여러 지점에서 기록하고 OFF의 이력 증가와 ON의 현재 변경량 상한을 비교한다. root만이 아닌 reachable index/payload·pending/NTFS 실제 할당을 합산한다. 초기 복제/읽기 cache는 0, 전체 물리 peak/RAM과 공유 귀속은 unknown을 유지한다. 기존 closed A/B/전체 장애 행렬을 반복하지 않고 변경한 writer의 파일/프로세스 경계와 필요한 실제 WinFsp fixture만 선택한다.

종료는 common runtime·CLI/Windows 연결과 schema/fake 회귀, 정확한 소유 파일의 중단 복구, 실제 다수 파일 반복 저장의 공간/시간 수치, `pwsh scripts/build.ps1`, 문서/검증 기록이다. 이번 사용자 요청에는 추가 commit/push·설치/ACL·비소유 프로세스 종료·원본 프로젝트 전환은 포함하지 않는다.

현재 writer의 후속 변경은 [쓰기 경로 최적화](latest-journal-write-optimization.md)가 소유한다. 아래 마감/수치는 SJL2/SJT2 당시 후보의 기록이다.

## 마감

2026-10-06 **scoped-complete-with-limitations**. 현재본 저장/개별 durable redo/이전 payload 회수와 선택 유지, Last Close·candidate/OFF 전환·복원/회수·schema/CLI를 연결했다. 실제 PNG 8/32파일 OFF/ON 각 3회, 필요한 로컬 WinFsp 정상 저장/삭제/reconnect, 정확한 소유 자식 3경계와 자동 복구를 확인했다. 필수 locked restore·Release build는 경고/오류 0, 전체 시험 1,128개 중 1,118 통과·실패 0·명시적 opt-in 10개 skip이다. [검증](../verification/latest-only-journal.md)이 수치와 실패/수정 이력을 소유한다. 32파일 최종 할당 -93.74%와 cycle 중 일정한 현재 payload를 확인했지만 쓰기 중앙값은 OFF 3.998s/ON 33.351s로 기본 ON 조건에 미달한다. 기본 OFF를 유지하며 새 Unity/VSS/전체 장애 행렬은 열지 않는다.

기본값의 후속 결정: 이 기록의 OFF는 당시 후보의 선택이다. 현재 SJL4 쓰기 경로와 새 작업의 기본 ON 결정은 [현재 계약](../latest-only-journal.md) 및 [후속 검증](../verification/latest-journal-write-optimization.md)을 따른다. 당시 입력·수치·미자격은 그대로 보존한다.
