# 기준본 복귀 비교 시간 최적화

[English](../../plans/journal-base-reversion-optimization.md)

아래는 `f67f217`의 명시적 정리 비용을 소유한다. 후속 [현재본 저널 ON](latest-only-journal.md)은 live 쓰기의 이전 payload 회수를 별도로 구현하며, 아래 v2 intent/append 수치를 현재 후보의 자격으로 승계하지 않는다.

2026-10-06 사용자 요청으로 이전 구현 `25f4fa1` 다음에 진행한다. 고정 비율의 비용 증가인지 확인하고 실제 다수 파일의 반복 변경에서 병목을 줄인다. 기존 기본 OFF와 v2 intent·바이트/metadata/sequence·개별 flush/ACK·전체 blob/검증 chunk 증명·실패 미게시를 유지한다.

범위는 Git 소유 cat-file reader의 부분 읽기 종료와 Runtime 기준 비교 순서다. 읽지 않은 stdout 때문에 종료마다 1초를 기다리는 것을 피하며, 같은 기준 파일의 범위를 논리 순서로 비교한 뒤 기존 journal 물리 순서로 돌려놓는다. payload cache·추가 전체 복제·자동 정리·service/설치·Unity Editor·새 A/B/전체 장애 행렬은 추가하지 않는다.

입력은 `splinter-unity`의 고정 commit `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`에서 서로 다른 blob의 실제 PNG 32개다. 실제 NTFS의 독립 stopped journal을 사용한다. 8파일×2범위×6순환, 32파일×3범위×8순환, 8파일×2범위×6순환의 최종 복귀 없는 입력을 고정한다. 범위는 각각 128KiB, 64KiB부터 256KiB 간격이다. 파일/범위를 교차해 B→C→base를 반복하고 마지막에 파일별 전체 복귀·반쪽 복귀·변경 유지 또는 전부 변경 유지를 적용한다. 이는 파일 저장 패턴의 오프라인 비교이며 Unity 저장/import·라이브 WinFsp 부하를 측정하지 않는다.

전후 각각 OFF/ON 3회, 순서는 OFF/ON→ON/OFF→OFF/ON이다. OS cache는 비우지 않는다. 비교 reader는 매 정리 새로 열며 소유 immutable ref/lease·고정 health를 사용한다. 전체 파일 streaming hash·entry·변경 목록/sequence를 검증하고 seed 준비·내용 검증은 시간에서 제외한다. 쓰기, reader 열기/종료를 포함한 정리, reader 열기/종료를 포함한 재생, 누적 관리 할당, 해독 blob bytes, NTFS 최종 할당을 보고한다. 전체 RAM/순간 peak/OS 물리 I/O는 unknown이다. 원본 내용·HEAD·비소유 ref는 보존하고 마지막에 자기 기준 ref만 release한다.

종료는 동일 harness/baseline 바이너리 보존, 두 병목의 의미 있는 회귀 시험, 전후 18표본씩 바이트/metadata 통과, 필수 `pwsh scripts/build.ps1`, 수치/제한 문서화다. 기본 ON은 기존 [판정](journal-base-reversion.md)의 조건을 모두 만족할 때만 고려하며 작은 유리한 입력만으로 변경하지 않는다.

[검증 기록](../verification/journal-base-reversion-optimization.md)은 전후 36표본과 원본/ref 보존을 소유한다. ON 정리 시간은 8파일 혼합 -79.47%, 32파일 혼합 -90.19%, 복귀 없는 8파일 -87.18%다. 최종 할당/선택은 동일하고, 복귀 없는 입력은 최적화 후에도 공간 절감 0%·전체 시간 약 43% 회귀이므로 기본 OFF를 유지한다. 쓰기/전체 시간 변동과 이전 바이너리 측정 중 병행 작업을 기록하며 쓰기 속도 개선으로 해석하지 않는다.

2026-10-06 **scoped-complete-with-limitations**. 필수 locked restore/전체 build와 1,079 통과·0 실패·9 선택 시험 skipped, 문서/UTF-8/diff 검사 및 측정 Runtime/Git DLL과의 hash 일치를 확인했다. 기존 바이트/metadata/ACK·원본/ref 보호와 기본 OFF를 유지한다. Unity/WinFsp·OS/다른 provider/전체 peak의 미측정은 위 기록대로 남기며 후속 전체 행렬을 자동 확대하지 않는다.

기본값의 후속 결정: 이 기록의 OFF는 당시 후보의 선택이다. 현재 SJL4 쓰기 경로와 새 작업의 기본 ON 결정은 [현재 계약](../latest-only-journal.md) 및 [후속 검증](../verification/latest-journal-write-optimization.md)을 따른다. 당시 입력·수치·미자격은 그대로 보존한다.
