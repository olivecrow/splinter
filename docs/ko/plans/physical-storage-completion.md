# 물리 저장 공간 단계의 최종 범위

[English](../../plans/physical-storage-completion.md)

사용자 요청에 따라 **현재 Windows/NTFS 알파의 물리 저장 공간 구현·검증 단위를 이번 작업으로 마감**하는 것이 목표다. 질문은 **최종 후보의 두 작업이 전체 복제/읽기 hydration 없이 생성·편집·재개되고, 명시적 정리와 독립 결과/회수 뒤 추가 파일 할당이 설명 가능한 잔여량으로 돌아오는가**다. 압축/동일 private 쓰기/작은 변경 범위/기본값 metadata의 기존 구현을 유지한다. 새 공간 최적화를 무한히 이어 가지 않고 아래 조건으로 종료한다.

**PS-01~05를 scoped-complete-with-limitations로 마감했다.** [직접 실행·비용·보호/회수 판정](../verification/physical-storage-completion.md)이 소유 근거다. 후속 메모리 단계는 [ME 계획](memory-completion.md)·[검증](../verification/memory-completion.md)이 소유하며 물리 저장 작업을 자동으로 이어 가지 않는다.

## 완료 조건

| ID | 포함 | 종료 조건 |
| --- | --- | --- |
| PS-01 | 현재 SJR2의 읽기 전용 공간 장부 | 기존 stopped journal 계측을 현재 형식 하나에 맞춤. stored/decoded·논리 live fragment·참조 stored block·완전히 obsolete block·frame 비용을 분리하고 부분 live block을 전체 저장 비용으로 셈. checksum·sequence·fragment/rename/resize/delete·중단 tail/active writer·unsupported format은 보존 거부. 실제 보존 저널의 Runtime usage와 대조 |
| PS-02 | 생성부터 회수까지의 실제 추가 파일 할당 | 기존 `measure-workspace-storage.ps1`에 정확한 workspace 선택·중복 root 거부·원본/evidence/shared 비용 분리·누락/오류의 unknown·신규 출력 보존을 연결. 제품 root 합계와 하위 category를 중복 합산하지 않음. 볼륨 여유/VSS 관측은 별도 공유 값으로 기록하며 작업별 귀속을 만들지 않음 |
| PS-03 | 최종 제품 A/B 한 묶음 | 아래 고정 입력/keys로 최신 Runtime의 생성·초기 payload/read cache 0·원본 표본 읽기 뒤 hydration 0·동일 ensure·독립 소유 Editor·작은 source 변경과 실제 Bee 고정 쓰기·정상 stop·명시적 정리·재연결/내용·두 독립 결과와 A release 중 B 후속 변경/회수. 원본/Plastic 등록/기존 결과·소유 snapshot 보호 |
| PS-04 | 정리·보존·회수의 전체 비용 판정 | 단계별 정확한 task/control·shared runner/home·file result·원본 Library·evidence 파일 할당, 정리 전후/두 세대 겹침·회수 후 잔여량. 유효 Library bytes를 journal에 다시 더하지 않음. 같은 journal raw 대조는 필요 표본에만 사용하고 전체 workspace 절감률로 확대하지 않음. 순간 peak·VSS attribution을 관측하지 못하면 unknown 유지 |
| PS-05 | 회귀·문서·단계 마감 | `pwsh ./scripts/build.ps1`·Python 계측 시험·실제 응답 schema·UTF-8/링크/diff. 각 요구를 구현/직접 실행/별도 지원 확대/제외로 대조해 물리 저장 개발의 일반 후속 목록을 닫고, 다음 개발 우선순위를 메모리로 이동 |

## 고정 실행

기존 등록 원본 `T:\plastic-fixture-20261002`, home `T:\pt1002`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, 각 journal budget 17,179,869,184B, keys **`ps-t-a-20261003`·`ps-t-b-20261003`**를 사용한다. Unity `6000.3.22f1`·설치된 CLI/Pipeline·기존 제한 서비스/등록을 그대로 사용한다. Runtime은 JM 후보를 포함한 현재 development 작업 내용으로 고정한다. 실행 전 source/home/바이너리 hash·기준·요청·여유 공간·원본 Editor/등록·기존 records를 보존한다. 응답의 정확한 workspace path만 편집한다.

입력은 원본 Bee의 `1900b0aE.dag`·`.dag.json`·`.dag.payloads`·`1900b0aE-inputdata.json` 네 파일이다. 각 worker의 새 `Library/SplinterPhysicalQualification`에 bounded 64KiB 스트림으로 명시적 쓰기하고 실제 mounted hash를 대조한다. 신규 개발 client 입력이며 source clone·읽기 hydration·copy-up fallback이 아니다. A/B의 작은 source marker와 B의 A 종료 후 후속 변경으로 격리를 확인한다. 각 작업은 정리를 한 번 실행하고 소유 이전 세대의 검증된 회수·현재 source/표본 내용을 확인한다.

근거는 `artifacts/physical-storage-completion-20261003/`에 신규 파일로 보존한다. 새 작업 두 개의 checkpoint/release와 그 소유 자원의 회수는 이 시험의 승인 범위다. 실패는 같은 key/id/home/intent와 증거로 유지한다. stop 뒤 지연 쓰기/읽기 query timeout은 원시 실패와 안정 관측을 남긴 같은 작업의 한정 재시도만 허용한다. 쓰기를 타임아웃만으로 반복하지 않는다. `reclaim-pending`·unknown·내용/원본/peer 오류가 남으면 완료로 표시하지 않는다.

## 종료와 별도 지원 범위

위 다섯 조건의 직접 근거로 **현재 알파의 물리 저장 단계**를 scoped-complete-with-limitations로 마감한다. 남겨야 할 결과/로그/현재 runner cache를 유출·회수 실패로 오인하지 않고 종류/량을 설명한다. 현재 결과 보존형 회수와 snapshot 공유 보호를 절감을 위해 약화하지 않는다.

지원 확대의 SF-02 전체 Partial·대형 PV/SF-05·ARM·다른 입력/VCS·OS/전원/실제 디스크 고갈·파일 특성 행렬은 기존 상태를 보존한다. 원본 위치/파일시스템 유지·전체 복제 금지라는 제품 요구를 축소하지 않으며 이번 fixture의 통과를 범용 지원으로 표시하지 않는다. 관측하지 못한 VSS 귀속/순간 최고치는 unknown이고 메모리 작업의 완료 조건으로 이동시키지 않는다. 다시 공간 작업을 여는 조건은 새 실사용 입력의 공간 부족·데이터 보존 결함·명시적 지원 확대 요청이다.

새 codec/backend·자동 정리/모드/큐·Git GC·기존 결과/evidence 삭제·타 snapshot sweep·원본 반영/check-in·서비스/설치/grant/볼륨/UAC 변경·MCP/Play/Test/build·반복 성능/새 장애 harness·원격은 제외한다. 실행 전 필요한 서비스/등록이 없으면 안전하게 보존하고 구체적 준비안만 분리한다.
