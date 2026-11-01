# 문서 지도

[처리량 리뷰 후속](verification/scalable-storage-throughput-followup.md)은 배치/자원 정책·정확한 게시 차등·작은 디렉터리 metadata·비교/목록용 바이너리 scratch·metadata 저장소 형식 2(`SPM2`)를 포함한 ST-01–60을 추적한다. 다중 항목 페이지는 보류다. 작은 native 검사·제한한 시간/gate 시도는 보존한 대형 입력 행렬을 자격화하거나 재개하지 않는다. 이어진 수집/반영 후속은 계획 안의 원본 root 관측 공유와 private 범위 직접 전달을 연결하며 별도의 작은 SPM2 시간 근거로 한정한다.

[대규모 저장의 느린 구간](verification/scalable-storage-throughput-diagnosis.md)은 기존 10만 항목 실행 비용·확인된 수정·미측정 원인을 기록한다. 2026-11-01 사용자가 검증을 축소해 대형 입력은 상태를 보존하고 종료했으며 17GiB/10만 전체 수명주기/ABBA/최신 전체 gate는 미완료다. 이 실행을 일반 속도·사용성 자격으로 승계하지 않는다.

[대규모 저장](scalable-storage.md)·[구현 계획](plans/scalable-storage.md)·[SCAL-01–08 검증](verification/scalable-storage.md)이 현재 task v4/basis v3/overlay v3 후보를 소유한다. 바이너리 디렉터리/페이지·순차 provider/저널·불변 공통 내용/범위·전체 잠금 게시·같은 요청 metadata 유지보수를 연결하며 필수 자격 검증은 진행 중이다. 기록하지 않은 대형·성능 자격은 주장하지 않는다.

[Git 수집 최적화](plans/git-collection-optimization.md)·[GCO-01–04 검증](verification/git-collection-optimization.md)이 게시 경계 전체 재검증을 유지하는 Git 범위 reader·새 결합 최종화의 수집 한 번 공유를 소유한다. 작은 Editor 없는 전후 비교·전체 gate를 과거 Unity 시간과 구분한다.

[Git 결과 수집·회수 진단](verification/git-result-collection-diagnosis.md)이 GCD-01을 소유한다. 기존 RI 입력 재현에서 계속 작업 reader의 반복 기준본 검사에 비용을 귀속했고 실제 로그에서 A 회수의 84.2%가 최종 재수집에 속함을 확인했다. 제품 최적화나 구버전 대비 속도 비교는 수행하지 않았다.

대규모 저장 후보는 task v4·관리형 basis v3·제품 overlay v3를 사용한다. 현재 표현과 진행 중인 SCAL-01–08 자격은 [대규모 저장](scalable-storage.md)이 소유한다. [반복 원본 반영](repeated-original-apply.md)의 불변 수집·명시적 결과·경로별 이력·외부 도구의 실행 중 Editor 협력은 유지한다. 과거 [RI](verification/repeated-original-apply.md)·MX·GCO 근거는 당시 task v3/basis v2 후보에 결속한다. 이전 상태는 보존·거부하고 일치하는 바이너리를 사용하며 자동 이관·회수하지 않는다. RI-01–08은 Git/None/Plastic 각각 작은 고정 실제 Unity 원본/A/B 한 묶음·당시 최종 소스 회귀·명시한 미검증 한계로 한정 완료했다.

마감한 [남은 수명주기 최적화](plans/remaining-lifecycle-optimization.md)는 MX-01–10으로 기존 미시도/부분 진행 후보 전체, 근거 있는 채택/폐기, 최종 동일 입력 Unity 반영/폐기 비교를 추적한다. 과거 대기 상태는 근거로 보존하며 최종 MX10 마감 절이 현재 상태를 소유한다. MX-05는 동일 입력 일반 NTFS 복원과 전체 gate 검증 뒤 root-delta A·게시 수 제한 guard의 topology B2를 채택해 마감했다. MX10은 변경 Library 복원 경로를 사용하지 않아 MX05의 실제 전체 절감은 미입증이다. MX-06은 독립 모델 window 측정의 6.85647005초 절감이 절대 10초 기준에 미달하고 앞선 큰 공통 flush 차이가 재현되지 않아 미채택으로 마감했다. 제품 checkpoint 코드는 같다. MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08A는 국소 영속 저널 비용·최종 전체/실제 정확성 근거로 채택했으며 MX-08B는 폐기·제거 완료, MX-09는 실제 Git/Unity 최종화 ABBA가 사전 큰 절감 기준에 미달하여 폐기·제거로 마감했다. MX-10 실제 반영·폐기 비교와 최종 전체 검증도 마감했다. MX-07A는 고정된 Editor 없는 None 입력에서 8.45773340초 / 15.47910650%를 절감했지만 절대 10초 기준에 미달해 제거했고 기준 소스/시험 323개 hash가 모두 일치한다. 그 MX07A 결과는 Unity나 실제 Plastic 성능 자격을 추가하지 않는다.

최종 전체 gate는 **1,966 통과 / 0 실패 / 34 조건부 skip**이다. 같은 입력의 단일 전체 stopwatch는 반영 **338.51645460→326.31757210초**, 폐기 **281.90245510→286.53377360초**를 기록했다. 각 경로 전후 1회이며 큰 전체 개선·개별 인과 효과는 입증하지 못했다. 자세한 구간·복구·제한은 [MX10 마감](verification/remaining-lifecycle-optimization.md#mx-10-final-closeout)을 따른다.

[수명주기 병목 후속](plans/lifecycle-bottlenecks.md)과 [BO-01–05 검증](verification/lifecycle-bottlenecks.md)은 sparse Library 디렉터리 복원·선택적 쓰기/Git 계측·제한된 Unity 시작 캡처를 소유한다. 복원 구성 요소 증거와 전체 Unity 시간은 구분한다.

[실제 Unity 병목 진단](verification/unity-lifecycle-bottleneck-diagnosis.md)이 BD-01을 소유한다. GUI 작업공간 하나의 정상 종료·폐기·정확한 Library 복원까지 진단 캡처를 포함해 391.3초를 관측했다. 실제 대기가 미미한 캐시 대기 우회 후보는 제외하고 영속 쓰기·시작 reload·Git payload 재해석 비용을 구분한다. 전후 성능 비교나 원본 반영 자격은 아니다.

[GR-01–03](verification/git-payload-retention.md)은 Git payload cache 1.5GiB/32,768-entry 후보를 시험했다. 동일 입력의 실제 생성 평균은 **50.661 → 48.550초(2.111초 / 4.2%)**였다. 큰 절감 기준에 미달해 후보를 제거하고 **256MiB/4,096개를 유지**한다. Editor 없는 생성 근거이며 Unity 전체 수명주기 개선은 아니다. [고정 범위](plans/git-payload-retention.md) 참조.


[English](../README.md)

[README Unity 시간 재측정](verification/unity-readme-recheck.md)이 RR-01을 소유한다. 현재 `2dff94d` 428.6초 / 공유 102.0초, 동일 입력의 준비·복원 75.1초 절감과 과거 Library 차이·구버전 종료 복구·변경 Library 진단을 구분한다.

[영문 소개](../../README.md) · [한국어 소개](../../README.ko.md) · [서드파티 고지](../../THIRD_PARTY_NOTICES.md)

기능·아키텍처·계획·검증 문서의 기본 언어는 영어입니다. 이 폴더는 한국어 버전이며 각 문서의 언어 링크로 전환할 수 있습니다. 원시 검증 자료와 과거 HTML 보고서는 기록된 내용과 언어를 보존합니다.

[Plastic 전체/선택 원본 반영](plastic-original-apply.md)의 현재 계약, [고정 범위](plans/plastic-original-apply.md), [자격](verification/plastic-original-apply.md)을 구분한다.

현재 구현은 아래 기능 문서에서, 후속 책임은 계획 문서에서 확인한다. 계획은 프로그램 구현·AI 사용 절차·필요 시 지원 검증·구현 제외를 구분한다. 검증 기록은 지정한 입력·후보의 근거이며 현재 코드의 자동 통과 선언이나 미완성 기능의 수가 아니다. 용어가 낯설면 [용어집](glossary.md)에서 의미와 서로의 차이를 먼저 확인한다.

## 현재 계약과 사용법

C20 후속은 [파일 결과 수집 처리량](plans/result-collection-throughput.md)과 [RT-01–04 검증](verification/result-collection-throughput.md)이 소유한다. seal 안에서 소스를 한 번 읽고 로컬 payload 작업을 제한 병렬화하며 실제 시간 측정과 Unity/Plastic 자격을 구분한다.

준비 후속은 [준비·관측 처리량](plans/preparation-throughput.md)과 [PR-01–04 검증](verification/preparation-throughput.md)이 소유한다. 게시 조건에 따른 부재 대기·제한 병렬 파일 scan·1회성 최초 검사 증명을 Editor 없는 NTFS/WinFsp 입력에서 측정한다.

| 문서 | 소유하는 내용 |
| --- | --- |
| [수명주기 최적화 지도](lifecycle-optimization-map.md), [기계 판독 색인](../lifecycle-optimization-map.json) | 코드 기반 AI 탐색: 작업공간 하나의 19단계·파일 결과 수집을 포함한 도식 6개, Git/파일 반영과 폐기, 잠금·내구성·복구, 코드 symbol·계측 경계. 고정 구현 스냅샷이며 새 실측이나 프로젝트 작업 실행 승인이 아님. |
| [쓰기 경로 처리량](plans/write-path-throughput.md), [검증](verification/write-path-throughput.md) | WP-01–05: 변경 없는 SJR2 영속 flush 재사용·payload 읽기 없는 raw 상태 크기 계산. 전체 시간 이점 없는 양끝 탐색 후보 제거. 동일 입력 구성 요소 비교와 내구성/replay 유지. |
| [Raw 쓰기와 묶음 영속 flush](raw-write-batching.md), [범위](plans/raw-write-batching.md), [검증](verification/raw-write-batching.md) | RW-01–05: 압축 writer 폐기·현재 압축 reader 보존·순서 보장 SJR2 묶음 flush와 직렬화 전 WinFsp 요청 수집. 영속 완료 뒤 성공하며 조기 ACK 없음. |
| [영속 flush/종료 실험](plans/durable-flush-throughput.md), [검증](verification/durable-flush-throughput.md) | DF-01–05: 개별 쓰기 I/O 대안과 bounded checkpoint RAM 후보를 평가했으나 큰 절감 기준을 충족하지 못해 제품 변경 없음. 빠른 write-through-only 결과는 동등 내구성 근거가 아님. |
| [작업공간 생성 처리량](plans/workspace-creation-throughput.md), [검증](verification/workspace-creation-throughput.md) | Git 작업공간 하나의 생성: 제한 proof window 재사용과 종료를 함께 기다리는 최초 선행 읽기. 같은 입력의 Editor 없는 비교이며 전체 blob 증명·필수 index refresh·lock·폐기 보호 유지. |
| [단일 작업공간 수명주기 처리 시간](plans/lifecycle-throughput.md), [구성 요소 검증](verification/lifecycle-throughput.md), [실제 Unity LU-01](verification/unity-single-lifecycle.md) | Library capture/admission/변경 없는 복원 병렬화. 동일 입력 단일 수명주기 359.5 → 300.0초; 준비·복원 50.6초 절감, Editor 시작 편차. 명시적 폐기와 양쪽 공통 bounded 폐기 수정이며 원본 반영은 미측정. |
| [VSS 폐기 결정](plans/vss-retirement.md), [검증](verification/vss-retirement.md) | 프로젝트/볼륨 범위 불일치와 회수 제약. 세 프로젝트 기준본 유지, VSS 서비스·진입점 제거, 구형 상태 보존·거부. |
| [Unity 종료 지연 진단](verification/unity-shutdown-diagnosis.md) | SD-01: 시작 소스/Library/로드된 어셈블리가 동일한 일반 NTFS 5개 사례. 16,384개 메서드의 단일/64개 클래스 종료 17.34초/1.60초. 현재 종료/회수 경계·미확정 관리 함수·마지막 디버거 시도 회수 미확인이며 완료된 작업량별 성능 자격은 아님. |
| [이전 bounded 저널 작업량 실험](verification/unity-bounded-scaling.md) | BS-01: 세션 유지 관측·이전 상태 편차·5바이트 stale PID 예외·복원 비용을 보존한다. SD-01에서 합성 코드 배치의 한계도 확인해 표와 조건부 손익분기를 현재 README 주장에서는 제외했다. |
| [현재 bounded 저널 Unity 비교](verification/unity-bounded-comparison.md) | BC-01: `c4fa506`, 같은 입력의 새 양쪽 측정, Splinter 537.6초 / 공유 Editor 82.1초. 추가 수명주기 추정 449.6초·조건부 손익분기·별도 진단 복구와 한 묶음/캐시 한계. |
| [제품 제한 로그](bounded-journal.md), [범위](plans/bounded-journal.md), [검증](verification/bounded-journal.md) | BL-01–05: mutation별 durable SJR2·제한 suffix/체크포인트 복구·provider 선택·원본/결과 보호와 작은 제품 비용 근거. |
| [폐기한 Bee 저장 설계](plans/bee-native-storage-design.md), [과거 검증](verification/bee-internal-native.md) | BN-01–05: 더 느리고 공간도 증가한 native 후보와 이전 junction 구현을 제거했다. 안전성·복구·실제 Bee 측정 기록을 보존하며 현재 실행 경로는 없다. |
| [구조적 처리량 실험](plans/structural-throughput-prototypes.md), [검증](verification/structural-throughput-prototypes.md) | SX-01–04: 과거 Bee 실험과 유지하는 제한 durable 범위 로그 도구. 고정한 작은 입력·원본/결과 보호와 구성 요소 시간 측정의 경계. |
| [Unity 작업 시간 최적화](verification/unity-throughput-optimization.md) | TP-01–06: Git 증명 재사용과 제한된 저널 개선. cache 유지 실험은 실제 Unity 회수 실패 뒤 철회했다. 구성 요소 근거·복구·공유 Editor 비교를 구분한다. |
| [다단계 Unity 미로 작업 비교](verification/unity-maze-workflow.md) | MW-01: 실제 미로 생성·장면 저장·캐릭터/입력·Play 검증을 병렬 A/B와 공유 Editor로 수행. 복구를 포함한 완료 기록과 정상 단계 비용·동등한 결과·시간 해석 한계를 구분한다. |
| [병렬 통합 종료를 사용한 Unity 전체 작업](verification/unity-finalization-rerun.md) | FU-01: 커밋 `07bc2de`, 같은 현재 입력의 Splinter 전체 768.3초와 공유 Editor 94.0초. 병렬 `checkpoint --release`, 과거 입력 차이와 회수 검증. |
| [최종화와 압축 해제 데이터 재사용](verification/lifecycle-finalization-optimization.md) | LF-01–05: 유한 경로/capsule RAM 재사용, 명시적 checkpoint와 release 통합, payload visitor cache, guard 회귀와 작은 fixture 비용. |
| [현재 Unity 병렬 작업·저장 비용 비교](verification/unity-current-comparison.md) | RC-01: 같은 입력의 일반 Codex/Git 워크트리 819.7s와 Splinter 935.2s, 현재 파일 할당, 동일 병합 결과, 측정 제외 구간·빈 Codex 폴더 잔여 한계. |
| [병렬 코드 작성과 공유 Unity Editor](verification/unity-shared-editor.md) | SE-01: Editor 하나가 독립 A/B 후보를 검증하는 계측 작업 합계 102.5s, B 대기 16.0s. 입력/실행 파일 대조 후 RC-01 Splinter를 재사용하며 Editor 대기열과 코드 병렬화·원본 복구·긴 작업 손익분기 미측정을 구분한다. |
| [시간·RAM 최적화](verification/time-memory-optimization.md) | Library 검증 범위 고정, 제한된 Git/저널 RAM cache, 독립 ensure 병렬 실행, 보호 회귀와 작은 입력 비용. 전체 Unity 시간은 후보별 [UF-01](verification/unity-final-lifecycle.md)·후속 [UT-01](verification/unity-time-memory.md)과 구분한다. |
| [용어집](glossary.md) | 작업/기준/저장·식별자·보존/반영/회수·응답과 검증 상태의 쉬운 설명과 소유 문서 |
| [현재 기반](foundation.md) | 모듈·의존성·명령·빌드와 시험 |
| [대규모 저장](scalable-storage.md)·[계획](plans/scalable-storage.md)·[SCAL 검증](verification/scalable-storage.md) | task4/basis3/overlay3 후보: 디렉터리/페이지·불변 내용/범위·segment accepted-source seal·root-bound `workspace read`·같은 키 `workspace maintain`·보호 잔여·현재 자격 |
| [OS와 앱 확장 경계](extensibility-boundaries.md), [검증](verification/extensibility-boundaries.md) | 불변 경로 정책·Unix 권한 metadata·앱별 원본/동반 파일/생성 디렉터리 정책, 실제 다른 OS·앱 미구현과 합성 시험의 구분 |
| [보안 경계 검증](verification/security-boundaries.md) | Plastic private reader·서비스 상속 ACL·보호된 회수 잠금·짧은 IPC 수신의 자동/한정 읽기와 미실행 배포 경계, 과거 외부 MCP 인증 기록 |
| [코드 최적화 회귀](verification/code-quality-optimization.md) | 구조화한 다음 행동·파일 rename/복원 검색·고유 객체 검사·원본 계획 조상 집합·압축 블록 순회의 자동 검증과 실측 한계 |
| [선택적 외부 Unity 도구](unity-mcp-response-recovery.md), [검증](verification/external-tool-boundaries.md) | 에이전트의 도구 선택·대상/요청 확인·필요 시 연결 복구와 제품 결합 제거. 외부 패치 제작/운영·버전 gate는 제외 |
| [개발용 CLI](development-cli.md) | 기본 CLI에서 분리한 diagnostic checkout·저장소 view·spike/probe의 진입점 |
| [저장 view 계약 검증](verification/storage-view-contract.md) | 현재 후보·소스 세대·Library cache·독립 결과 계약과 schema/미지원 필드·같은 요청 복구·소유 회수의 자동 회귀 |
| [폐기한 ReFS 기준본 경로](plans/refs-retirement.md) | 대형 프로젝트의 최초 전체 기준본 준비 부담으로 폐기한 기능과 과거 근거의 경계 |
| [현재 후보 AI 시작 안내](agent-start.md) | 기본 CLI의 저장 경로별 전제·진입·정확한 경로/같은 작업 재개·명시적 종료·실패의 다음 행동 |
| [최소 AI 사용 스킬](../../skills/splinter/SKILL.md) | 저장소 지침 원본·직접/독립 작업 판단·CLI 발견·반환 경로·같은 작업 재개·명시적 종료/실패 처리. 설치/자동 binding은 없음 |
| [VCS 없는 작업공간](vcs-independent-workspaces.md) | 명시적 소스/application 선택·파일 기준/결과·현재 형식·회수와 실제 검증의 구분 |
| [VCS 없는 프로젝트 기준본](plain-project-basis.md), [범위](plans/plain-project-basis.md), [검증](verification/plain-project-basis.md) | 승인한 whole-project 관리형 원본·VSS 없는 None source/Library 공통 기준·원본/A/B private 범위·전체/선택 게시·task 참조 회수와 마지막 일반 NTFS 복원. 파일/실제 Unity·비용/미자격을 구분 |
| [Plastic 사용 환경](plastic-compatibility.md), [호환 범위](plans/plastic-compatibility.md), [검증](verification/plastic-compatibility.md), [C: 후속 회수](verification/vss-and-mcp-remediation.md#vr-01-정확한-기존-c-plastic-회수) | 관리 정보/등록 격리·무 checkout 파일 작업공간·독립 결과와 회수. C: 회수는 종결했으며 외부 도구 발견 제한은 유지 |
| [일반 폴더 원본 반영](plain-folder-original-apply.md) | 읽기 전용 계획·고정 대상/intent·조건부 Windows 게시·중단/회수와 비용 경계 |
| [원본 반영 검증](verification/plain-folder-original-apply.md) | Core/fake·실제 일반 권한 NTFS 파일 시험과 작은 Editor 없는 제품 A/B 한정 마감·계측 |
| [VCS 독립 검증](verification/vcs-independent-workspaces.md) | 자동 파일/fake·Git 회귀와 실제 A/B·서비스 업데이트·동일 요청 회수 복구의 한정 판정/비용 |
| [일반 폴더 실제 실행 계획](plans/vcs-independent-validation.md) | 고정 fixture·서비스 업데이트/grant의 검토 가능한 계획·한 A/B 묶음과 중단 조건 |
| [NTFS 작업공간](ntfs-workspaces.md) | 프로젝트 기준 identity·범위 저널·Git·경로 보호·제어 파일 게시/읽기·복구·결과 보존·회수 |
| [Plastic 프로젝트 기준본](plastic-project-basis.md), [검증](verification/plastic-project-basis.md) | fixed changeset/revision·VSS 없는 범위 저장·공통 원본 Library·독립 파일 결과와 소유 회수. 구현/reader·native·실제 Unity 자격을 구분 |
| [Git 프로젝트 기준본](git-project-basis.md), [reader/Editor 없는 검증](verification/git-project-basis.md), [Unity 검증](verification/git-unity-validation.md) | `git-snapshot`의 fixed Git commit/tree/blob·VSS 없는 WinFsp 범위 저장·소유 ref/fence·독립 결과/회수. ignored/Library 기준 재사용과 후보별 Unity 자격을 구분 |
| [Git 원본 Library 참조](git-library-reference.md), [검증](verification/git-library-reference.md) | 명시적 Library prepare/status/restore·원본 관리형 mount·Git 소스와 불변 Library graft·작업 의존성·파일 이동/범위 복원과 MCP/RAM 자격 |
| [Git Unity 실제 실행 범위](plans/git-unity-validation.md), [검증](verification/git-unity-validation.md) | 현재 원본 Git/Unity의 새 기준 A/B·cold Library·exact HTTP MCP·리소스/씬/현재 상태 시험과 소유 회수. 준비 병목/동일 요청 복구와 이전 VSS 자격을 구분 |
| [명시적 폐기](workspace-discard.md) | Core/Runtime intent·Git/Windows provider·CLI·소유 기준본 참조 회수·중단 재개 |
| [Unity 알파 작업공간](alpha-workspaces.md) | 외부 도구로 편집·검증, 같은 작업 재개·독립 결과·통합과 Editor 수명 |
| [Unity CLI A/B 인계 검증](verification/unity-cli-handoff.md) | 정확한 반환 경로의 catalog/loaded 코드·A stop/release 뒤 B 후속 작업·두 독립 결과/회수의 작은 T: 한정 자격과 목록 누락/타임아웃 제한 |
| [저장소와 자원](storage-and-resources.md) | 프로세스 그룹·자원 관측 |
| [실행 파일 묶음의 메모리 검증](verification/runner-bundle-memory.md) | 동일 빌드의 디스크 재사용·스트림 준비의 관리 할당·손상/소실 거부·동시 게시와 실제 프로세스 회귀 |
| [쓰기 경로 최적화](plans/latest-journal-write-optimization.md)·[검증](verification/latest-journal-write-optimization.md) | SJL4 payload/metadata capsule·mutation마다 한 durable flush·이전 현재본 회수·동일 PNG 반복 입력의 전후 비교 |
| [현재본 저널 ON](latest-only-journal.md)·[현재 범위](plans/latest-journal-write-optimization.md)·[검증](verification/latest-journal-write-optimization.md) | SJL4 현재 범위·단일 capsule 기록/개별 mutation durable flush/게시/회수·명시적 ON·새 provider 기본 [bounded](bounded-journal.md)·기존 OFF/ON 유지·생성 중단 복구·v3 compact intent·실제 PNG 반복 저장 및 필요한 WinFsp/프로세스 경계 |
| [기준본 복귀 정리](journal-base-reversion.md), [이전 범위](plans/journal-base-reversion.md), [당시 검증](verification/journal-base-reversion.md), [이전 시간 최적화](plans/journal-base-reversion-optimization.md)·[당시 계측](verification/journal-base-reversion-optimization.md) | 명시적 compact의 기준 동일 범위 제거·원래/기대 hash·과거 v2 intent/append 구현의 OFF/ON 및 강제 중단/peer 근거 |
| [저널 정리 크기 회귀](verification/journal-compaction-size.md) | 작은 덮어쓰기의 메타데이터 증가 재현·더 작은 frame 선택·같은 입력의 NTFS 할당 비교 |
| [동일 private 쓰기](verification/duplicate-private-writes.md) | 승인된 private 범위의 중복 payload 억제·메타데이터/flush 유지·NTFS 할당과 작은 WinFsp client 검증 |
| [작은 Unity 동일 쓰기 공간 비교](verification/unity-duplicate-private-writes.md) | 고정 Runtime의 warm 편집/재개·Library/저널 분류·실제 파일 할당·동일 독립 결과와 회수·작은 관측 차이의 한계 |
| [부분 쓰기·버퍼·시작 개선](verification/resource-efficiency-followup.md) | private 덮어쓰기의 일치 앞뒤 재사용·요청 크기 버퍼·시작/소스 검증 분리와 고정 입력·로컬 Unity 자격 |
| [Library payload 압축 후보 선정](verification/library-block-compression-feasibility.md) | UH 비용 재계산·현재 Bee 4개 입력의 읽기 전용 block 모델/복원·다음 codec 범위 선정. 실제 저널/물리 절감 미자격 |
| [현재 압축 payload reader](workspace-payload-codec.md), [과거 검증](verification/workspace-payload-codec.md) | 현재 압축 block 읽기·손상 거부·bounded 부분 읽기. Encoder는 폐기했고 새 쓰기는 RW raw 정책이다. |
| [소스 검증](source-verification.md) | 변경 경로·hash cache·status freshness·현재 계측 형식 |
| [불변 소스 후보와 고정 view](source-view.md) | 개발용 후보 게시·같은 요청 중단 복구·검증된 Git 포인터 분리·고정 view·공통 Library |
| [개발용 소스 view 수명주기](view-workflow.md) | 개발 CLI의 view·snapshot Library·정지 cache 정리·독립 결과·회수 의존성. Unity 검증은 외부 도구 책임 |
| [Unity 미리보기](unity-render-qualification.md) | fixture의 요청별 PNG·픽셀·해시·실패/복구 조건 |
| [Snapshot overlay 시험](snapshot-overlay.md) | 개발용 준비·mount·파일 API·장애·결과·회수 스크립트와 내부 계측/I/O 모듈 |
| [준비 중단 절차](ntfs-workspaces.md#개발용-준비-중단-시험), [준비 복구 검증](verification/preparation-recovery.md) | 개발용 gate/harness와 같은 요청의 실제 CLI 종료·재개·원본 보존·회수 자격 |
| [Replay probe](replay-profiling.md) | 고정 합성 입력의 replay 비용 |
| [Write probe](write-profiling.md) | 고정 합성 쓰기·flush·대기 비용 |
| [저널 중단 시험](journal-recovery.md) | 합성 불변 기준의 58개 실제 자식 종료·외부 ACK·불완전 tail·재생 관문 |
| [로컬 kernel 복구 시험](kernel-recovery.md) | 실제 WinFsp/fake 기준의 callback·mmap·동시 reader·같은 경로 재연결 개발 관문 |
| [제품 쓰기 복구 시험](product-write-recovery.md) | 일반 VSS/WinFsp/Git CLI의 외부 client 경계·동일 작업 재연결·독립 보존/회수 개발 관문 |
| [검증 안내](verification/README.md) | 지원 경계, 검증 진입점, 보존한 실측 근거 |
| [최소 AI 작업 연결 검증](verification/agent-workflow.md) | source skill 직접 사용·반환 경로 편집·새 CLI 재개·direct/명시적 폐기 한정 실행 |
| [대표 AI A/B 사용·비용 검증](verification/representative-agent-workflow.md) | 독립 Editor의 오류/수정·동일 작업 재개·보존/통합/release와 실패 작업 discard·단계별 파일 할당/시간·재시도 제한 |
| [한정 M0 납품 판정](verification/m0-delivery.md) | 현재 복구 회귀·기존 native 소스 대조와 재사용 한계·실제 시작 명령 확인·한정 완료와 지원 확대의 구분 |
| [Raw 쓰기 / 기존 압축 payload](journal-payload-compression.md), [과거 범위](plans/journal-payload-compression.md), [과거 검증](verification/journal-payload-compression.md) | 새 payload는 raw이며 기존 압축 길이/fragment·현재 형식의 복구를 보존한다. JP는 과거 압축 근거다. |
| [상태 쓰기 진단 검증](verification/state-write-diagnostics.md) | workspace 저장 단계·게시 완료 여부·실제 파일 잠금과 기계용 오류 계약의 자격 |
| [저널 기본값 저장 범위](plans/journal-metadata-storage.md), [검증](verification/journal-metadata-storage.md) | 현재 SJR2 기본값 생략·명시적 정리/후보의 더 작은 메타데이터 선택·고정 실제 저널의 오프라인 NTFS 할당과 추가 비용. 새 native 수명주기 자격은 구분 |
| [물리 저장 단계 최종 범위](plans/physical-storage-completion.md), [검증](verification/physical-storage-completion.md) | 현재 SJR2 장부·정확한 task 파일 할당·최종 제품 A/B·정리/독립 결과/회수와 물리 저장 단계의 종료 경계 |
| [메모리 단계 최종 범위](plans/memory-completion.md), [검증](verification/memory-completion.md) | 압축 쓰기 중복 buffer·부분/빈 읽기 scratch·같은 실제 저널 대조와 소유 provider/Editor·공유 시스템 비용/회수의 종료 경계 |
| [병렬 안정성의 확정 범위](plans/stable-parallel-completion.md), [검증](verification/stable-parallel-completion.md) | 잠금 오류의 점유 오인 수정·실제 A 수집/회수와 B 조회/재개·파일 쓰기 겹침·독립 결과/소유 회수. Editor 없는 경계와 기존 자격 구분 |
| [이전 Unity 실측 시간(UF)](verification/unity-final-lifecycle.md), [데이터](../../reports/benchmarks/unity-final-lifecycle-20261008.json) | UF-01: `116fd44` 전체 1,083.0초·Editor 준비 216.1초·보존/회수 172.0초·Library 복원 126.0초, 최초와 같은 편집·입력 및 정상 종료/원본/소유 회수 검증. |
| [과거 Codex/공간 비교 기준 (RB)](verification/readme-unity-comparison.md), [데이터](../../reports/benchmarks/unity-workspace-comparison-20261007.json) | 실제 Codex 앱 워크트리와 당시 기본 ON Splinter·원본 Library 기준본의 동일 A/B 편집, 추가 파일 할당, 별도 준비·Editor 비용, 한정 컴파일·에셋 검증 |
| [Checkpoint/release 프로파일링](verification/checkpoint-release-profiling.md), [데이터](../../reports/benchmarks/checkpoint-release-profile-20261007.json) | 기존 Unity 단계 귀속·작은 Editor 없는 task; 반복 검증/재수집·저널 inventory·제곱 비례 fence 검색; 제품 최적화 없는 진단 복사본 |
| [Checkpoint/release 최적화](verification/checkpoint-release-optimization.md), [첫 데이터](../../reports/benchmarks/checkpoint-release-optimization-20261007.json), [후속 데이터](../../reports/benchmarks/checkpoint-release-followup-20261007.json) | CO-01–08: Git 묶음/통합 보존·seal 결과 재사용·fence/inventory 인덱스·구분한 live 계수·요청 wake; 작은 일반 배포본 비용·guard 회귀·소유 회수 |
| [Unity 수명주기 재측정](verification/unity-lifecycle-remeasurement.md) | 과거 UR-01–04: 같은 실제 입력의 최적화 후 A/B 시간·제한된 프로파일, 종료 시간 제한과 같은 작업 지속, 독립 결과·원본 복원, 남은 병목 |
| [수명주기 읽기·복원 최적화](verification/lifecycle-read-optimization.md) | LO-01–11: 제한된 Git proof·reader 세션 재사용과 기준 읽기 gate 분리, SJL4 검증·seal 읽기 통합, 경로 조회·변경 없는 파일 복원·cursor 묶음·전체 Library 복원·불변 목록 재사용, 한정 NTFS 비용과 손상·복구 회귀 |
| [과거 checkpoint/release 측정 (UC)](verification/unity-checkpoint-release-remeasurement.md) | UC-01: 커밋한 최적화 후보의 같은 실제 Unity A/B 입력, checkpoint/release 304.9초·Library 복원 180.9초, 종료 시간 제한과 결과·원본·자원 검증. |
| [시작 단계 저널 최적화](verification/journal-startup-optimization.md) | JS-01–04: 회수된 metadata 수명·파일별 object 인덱스·bounded footer 비교, 작은 이어 쓰기 비용과 손상·재연결 회귀. 결합 후보의 실제 준비는 UP-01 참조. |
| [준비 시간 최적화](verification/startup-time-optimization.md) | ST-01–04: 독립 불변 읽기, SJN2 이어 쓰기 metadata 페이지, 동일 bridge 재사용과 한정 경과 시간·guard 검증. 실제 준비는 UP-01 참조. |
| [실제 Unity 준비 프로파일](verification/unity-startup-profile.md) | 과거 UP-01: 최초 준비 766.9초, 거의 그대로인 약 447초 스캔, Git reader 대기·Bee 출력 비용, 정확한 입력·소유 회수 확인과 후속 우선순위 분석. |
| [준비 프로파일 후속 최적화](verification/startup-profile-optimization.md) | SU-01–05: 두 Git decoder의 독립 진행, 고정 저널 경로, Cleanup 잠금 제거, bridge assembly 분리와 내용으로 증명한 소스 시각; 한정 비용과 회귀 검증. |
| [SU 이후 Unity 준비 재측정](verification/unity-startup-remeasurement.md) | 과거 US-01: 준비 349.8초·스캔 16초·컴파일 준비 210초, 시작 판독 오류 복구와 서로 다른 runtime, 남은 ILPP/Library/소스 증명/게시 비용 및 원본 복원 확인. |
| [추가 준비 최적화](verification/startup-additional-optimization.md) | SA-01–05: Library 수명 고정·로컬 Close·단일 핸들 게시·단일 읽기 Git hash/ensure 증명·첫 시작 Bee 그래프 무효화, 한정 검증과 합친 후보의 실제 Unity 시간은 UF-01에 별도 기록. |
| [종료와 준비 단계 비용](verification/lifecycle-cost-optimization.md) | LC-01–03: 작은 쓰기 메타데이터 제한·root 전용 client 초기화·seal/회수 검사 재사용·작은 NTFS 비용/복구 근거, 합친 후보의 실제 Unity 시간은 UF-01에 별도 기록. |
| [Unity 실제 C/S 비교 범위](plans/unity-live-comparison.md), [검증](verification/unity-live-comparison.md), [HTML](../../reports/unity-live-comparison-20261003.html) | Codex worktree와 제품 workspace의 코드/에셋·파일 할당/메모리·격리/재개·회수 비교. 입력/버전과 실패·후속 복구의 근거는 소유 검증에서 구분 |
| [Unity 병렬 편집 확정 범위](plans/unity-parallel-editor-validation.md), [검증](verification/unity-parallel-editor-validation.md), [S HTML](../../reports/unity-parallel-editor-validation-20261003.html) | C/S A/B의 정확한 MCP 대상·리소스/씬/테스트·원본 반영·소유 회수와 비용 비교. HTML은 S까지이며 후속 실행과 제한은 텍스트 검증이 소유 |

## 설계 범위와 검증 기준

| 문서 | 용도 |
| --- | --- |
| [제품 요구사항](../../plan.ko.html) | 제품이 달성할 목표 |
| [구현 계획](plans/implementation-plan.md) | 프로그램/에이전트 책임·VCS 무관한 제품 요구·다시 확정한 일반 폴더 기본 수명주기와 원본 반영·외부 VCS 호환 처리의 후속 순서·마감한 M0 |
| [한정 마감: 일반 폴더 원본 반영](plans/plain-folder-original-apply.md) | 명시적 대상·기준/결과/현재 원본 비교·조건부 게시·동일 intent/부분 수행 복구·Editor 없는 NTFS A/B 한 묶음과 제외/종료 기준. 구현·일반 권한 파일 검증과 작은 Editor 없는 제품 A/B의 자격을 구분 |
| [원본 반영 제품 A/B 준비](plans/plain-folder-original-apply-validation.md) | 고정 작은 입력·seed·서비스/등록·승인한 관리자 Apply와 한 실행 묶음의 경계 |
| [검증 추적표](plans/validation-matrix.md) | M0와 기존 ID의 실제 증거 상태·후속 책임 분류·보존한 48개 카탈로그 |
| [NTFS 복구·지원](plans/ntfs-recovery-and-support.md) | NR-01/02 준비 복구·NR-03 공통 저널/로컬 kernel/제품 client 경계와 재부팅·전원·공간·파일 관문의 완료 기준 |
| [저장공간 우선 설계](plans/storage-priority-and-editor-modes.md) | SF-02~06·비용 판단과 WF-09~12/LB-01~05의 완료 범위·재검증 기준 |
| [VCS 프로젝트 단위 기준본 전환](plans/project-scoped-basis-and-reclamation-review.md) | Git·Plastic의 고정 버전 재사용·프로젝트 범위·기존 범위 저널 연결의 VP-01~05. Git/Plastic은 Library·고정 실제 Unity까지 한정 마감하고 VCS 없는 새 기준본은 별도 승인한 NP 관리형 원본 계약을 따름. 사전 CLI 접근과 실제 제품 자격을 구분 |
| [Plastic 프로젝트 기준본·Library 범위](plans/plastic-project-basis.md) | fixed changeset reader·VSS 없는 범위 저장·공통 Library와 실제 Unity A/B·MCP/리소스/씬/현재 상태 tests·독립 결과/회수/원본 복원의 PPB-01~06 한정 마감. 초기 준비/복원·MCP 응답/실행 겹침 제한과 원본 반영/기존 VSS/비VCS 후속을 구분 |
| [Git 원본 Library 참조·MCP/RAM](plans/git-library-reference.md) | 승인한 Library 전용 관리형 mount·원본/peer 복구와 GL-01~05 한정 마감. 실제 RAM 절감 미확인·MCP 실패 상세 소실·준비/복원 비용은 검증에 유지 |
| [대화별 설치·사용](plans/session-workflow-and-installation.md) | AI의 설치 연결/인계/선택적 도구 사용/종료 절차·제품과 외부 도구의 책임·[과거 Unity CLI A/B 인계 범위](plans/session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계)·SW 증거 분류 |
| [병렬 비교 방법](plans/parallel-workflow-qualification.md) | C/S/Q 입력·작업·비용 비교를 재사용할 때 |

현재 기능 문서는 구조·동작·진입점·제약만 설명한다. 검증과 계획에서 보존할 과거 기록은 지원 범위를 입증하는 후보/입력/환경·결과, 미해결 제한·재현 조건, 복구·소유권 판단과 비교에 필요한 근거다. 같은 근거는 소유 문서에 한 번만 두고 지도에서는 연결한다. 후속 판단에 쓰이지 않는 완료 순서·작업 일지·중복 시험 숫자·해결된 상태의 반복 요약은 제거하며 단순 변경 이력은 Git에서 확인한다. 완료한 계획에 남은 승인 범위·재현 조건·제외 조건이 필요하면 유지하되 현재 미완료 작업으로 표시하지 않는다. 원시 artifacts·독립 결과·snapshot은 문서 정리 대상으로 삭제하지 않는다.

첫 버전의 실행 범위는 M0로 확정하고 NR/SF/SW 전체 미완료를 첫 버전 완료의 분모로 사용하지 않는다. [제품 범위 정리 검증](verification/workspace-product-scope.md)은 삭제한 DAG/Unity 검증 진입점·기본/개발 CLI 분리와 실제 자동 회귀의 경계를 소유한다.
