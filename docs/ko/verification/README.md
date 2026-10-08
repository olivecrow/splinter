# 검증 안내와 보존 근거

[English](../../verification/README.md)

[외부 도구 결합 제거](external-tool-boundaries.md)는 MCP 부재/내부 API 변경에 독립적인 bridge 초기화·정상 종료와 패키지 run-state 보존의 자동 회귀를 소유한다. MCP·Unity CLI의 이름/버전·패치·연결 성공은 제품 전제가 아니다. 아래 도구별 실행 기록은 당시 후보/입력의 사실이며 현재 지원이나 설치 요구로 승계하지 않는다.

[현재본 쓰기 경로 최적화](latest-journal-write-optimization.md)는 현재 SJL4 capsule의 단일 payload/metadata 기록·mutation마다 한 durable flush·정확한 이전 payload/metadata 회수와 동일 8/32 PNG 반복 입력의 ON 쓰기 -29.5%/-30.7%와 ON/OFF 파일 할당 -91.75%/-93.61%를 소유한다. ON은 OFF보다 5.6–5.8배 느리고 관리 누적 할당은 늘었다. 사용자가 1,536회 전체 22.389s·개별 평균 약 14.6ms와 파일 할당 감소를 근거로 새 작업의 기본 ON을 선택했으며 기존 OFF/ON은 유지한다. SJL2 당시 비용과 SJL3 중간 후보의 7~8% 시간 개선은 별도 보존한다. 파일/소유 프로세스 중단·WinFsp fake 기준 자격이며 Unity/VSS/OS 전원/전체 peak 자격은 승계하지 않는다.

[보안 경계 검증](security-boundaries.md)은 Plastic private reader·MCP 인증·서비스 ACL/잠금/수신 제한의 변경된 경계만 다룬다. 과거 Unity A/B 자격은 유지하며, 현재 reader의 Unity 실행과 설치된 서비스 업데이트를 자동 통과로 표시하지 않는다. MCP v2는 현재 제공 범위에서 제거한 과거 외부 후보의 기록이다.

[C: 회수/MCP 응답 복구](vss-and-mcp-remediation.md)는 과거 동일 A release/B discard의 회수 종결과 외부 Coplay 패치의 실행·중복 방지·reload 상세 결과를 소유한다. C: 회수 보류는 종결됐으며 최초 실패의 재현 조건·소유권과 복구 근거는 해당 검증에 남긴다. MCP의 과거 후보 결과와 현재 인증/transport의 미실행 경계는 [보안 검증](security-boundaries.md)과 구분한다.

[Plastic 원본 반영](plastic-original-apply.md)은 recorded 기준의 전체/선택 게시, 등록·잠금·Editor 보호와 같은 intent 복구의 별도 자격이다.

[README Unity 비교](readme-unity-comparison.md)는 RB-01–05를 소유한다. 실제 Codex 앱 워크트리와 기본 ON인 현재 Splinter·원본 Library 기준본에서 방식별 독립 Editor 두 개와 동일 A/B 편집, 추가 파일 할당, 별도로 측정한 준비·Editor 시간을 비교했다. 한 번의 실행에서 편집·컴파일 완료 시점의 할당은 98.65% 줄었고 Editor 준비·재컴파일은 더 오래 걸렸다. 실패·정리·한 묶음/캐시 제한·RAM/전체 디스크 peak 미측정·Play/Test/build 미실행을 유지한다.

[Checkpoint/release 프로파일링](checkpoint-release-profiling.md)은 LP-01–04를 소유한다. Unity 단계 기록 재사용, 복사본만 계측한 작은 Editor 없는 Git/Library task, 한정 fence 검색 microprobe다. 반복 검증/재수집·제곱 비례 회수를 확인했으며 최적화나 지원 확대 통과를 주장하지 않는다.

[Checkpoint/release 최적화](checkpoint-release-optimization.md)는 CO-01–08을 소유한다. 속성 묶음·결과 재사용·통합 checkpoint/보존·live 계수·inventory 인덱스·요청 wake를 연결했다. 후속 소형 비교는 앞서 최적화한 배포본 대비 checkpoint 50.84%·release 29.39% 감소다. Guard/schema 회귀·원본 보존/소유 회수를 통과했다. CO는 소형 fixture 자격이며 후속 실제 Unity 재측정은 UR-01–04에 별도로 기록한다. 지원 확대는 NotRun이다.

[Unity 수명주기 재측정](unity-lifecycle-remeasurement.md)은 UR-01–04를 소유한다. 같은 소스·Library 입력에서 A/B checkpoint·release 421.3806초, 초기 컴파일 1,041.8831초와 재컴파일 76.9초를 기록했다. 실제 종료 명령의 시간 제한·같은 작업 지속·계측 부하·순서 차이·원본 복원과 후속 개선 후보를 구분한다.

[수명주기 읽기·복원 최적화](lifecycle-read-optimization.md)는 LO-01–11를 소유한다. UR 병목의 제한된 Git proof·reader 세션 재사용과 기준 읽기 gate 분리·seal 읽기 통합·경로 검사·변경 없는 복원/cursor 묶음·전체 Library 복원·불변 목록 재사용을 구현했다. 작은 실제 NTFS 비용과 전체 손상·복구 회귀를 기록하며 새 Unity 시간으로 승계하지 않는다.

[현재 Unity checkpoint/release 측정](unity-checkpoint-release-remeasurement.md)은 UC-01을 소유한다. 커밋한 후보 `168081c`와 같은 실제 소스·Library 입력에서 A/B 순차 checkpoint/release는 304.9초, 원본 Library 복원은 180.9초였다. 독립 결과·원본 보호·소유 자원 회수와 실제 응답 12개의 schema를 검증했다. 종료 명령은 정상 종료 전에 여전히 시간 제한에 걸렸으며, 이전 프로파일 포함/현재 미포함 단일 실행의 차이를 순수 최적화 배율로 읽지 않는다.

[시작 단계 저널 최적화](journal-startup-optimization.md)는 JS-01–04를 소유한다. 회수된 footer 참조·변경 파일의 항목 조회·bounded metadata 검증과 작은 일반 배포본 이어 쓰기 비용, 바이트·seal·재연결·손상 회귀를 기록한다. UC-01을 미측정 Unity 준비 시간 개선으로 대체하지 않는다.

[준비 시간 최적화](startup-time-optimization.md)는 ST-01–04를 소유한다. 독립 불변 읽기·SJN2 이어 쓰기 metadata 페이지·같은 소유 bridge 재사용의 현재 형식 경계, 한정 시간 비교와 동시성·복구 회귀를 기록한다. UC-01은 과거 근거로 유지한다.

[실제 Unity 준비 프로파일](unity-startup-profile.md)은 UP-01을 소유한다. 커밋한 JS/ST 후보에서 같은 소스와 Library 항목으로 최초 준비 766.9초를 측정했다. 거의 그대로인 Git 읽기 중심 스캔과 빨라진 컴파일/출력을 구분하고, CPU/I/O 표본·정확한 Bee 입력 차이·소유 회수/원본 복원을 기록한다. 추적을 포함한 한 실행으로 고립된 개선율을 주장하지 않으며 추가 최적화는 분석 단계다.

[준비 프로파일 후속 최적화](startup-profile-optimization.md)는 SU-01–05를 소유한다. UP에서 확인한 Git 직렬 읽기·반복 저널 경로 검사·불필요한 Cleanup gate·bridge 재컴파일·소스 시각 문제를 구현하고 한정 비용/guard를 검증한다. 소형 실제 Unity assembly 검증과 읽기 전용 원본 capture 비용은 후속 US-01 cohort와 구분한다.

[SU 이후 Unity 준비 재측정](unity-startup-remeasurement.md)은 US-01을 소유한다. 최초 준비는 UP의 766.9초에서 349.8초, 스캔은 16초이며 컴파일 준비는 약 210초다. 최초 시작 판독 실패·커밋 수정·같은 작업공간의 서로 다른 runtime 재개·수정 중단·느린 종료를 보존한다. 남은 분석 대상은 ILPP 검증 지연·Library 존재 확인·소스 증명·저널 게시다. 응답 14개와 원본/소유 회수를 확인했으며 단일 실행을 순수 인과적 개선율로 읽지 않는다.

[Unity 실제 C/S 비교](unity-live-comparison.md)와 [HTML 보고서](../../../reports/unity-live-comparison-20261003.html)는 Codex 앱 worktree와 제품 workspace의 지정 입력·파일 할당·Editor/provider 메모리·격리와 재개를 비교한다. 과거 home 기록 누락과 승인한 후속 회수의 근거는 소유 검증에서 구분한다. 전체 물리/VSS 귀속·최소 RAM은 unknown이며 SF/PV 전체 지원으로 확대하지 않는다.

[Unity 병렬 편집 검증](unity-parallel-editor-validation.md)은 C/S A/B의 정확한 MCP 대상·리소스/씬·현재 상태 시험·원본 반영과 회수의 한정 자격을 소유한다. 실제 테스트 겹침·reload 결과 보존/재연결·소유 회수와 초기 실패/복구 비용은 텍스트 검증을 따른다. [HTML](../../../reports/unity-parallel-editor-validation-20261003.html)은 S까지의 기록이다. Editor RAM 절감 미확인·전체 물리 peak/VSS 귀속 unknown은 유지한다.

[물리 저장 단계 최종 검증](physical-storage-completion.md)은 고정 JM 후보의 T: A/B·초기 복제/읽기 hydration 0·정리·독립 결과/회수·잔여 파일 할당과 장부/회귀를 scoped-complete-with-limitations로 판정한다. SF/NR의 별도 지원 확대와 공유 VSS/전체 순간 peak unknown은 유지한다.

[메모리 단계 최종 검증](memory-completion.md)은 현재 Runtime의 압축 쓰기/부분·빈 read buffer·바이트/미게시/재생 의미·같은 실제 저널과 새 제품 A/B의 소유 비용/독립 결과/회수를 scoped-complete-with-limitations로 마감했다. 전체 RAM·native codec/순간 peak unknown을 유지한다.

[병렬 안정성의 선택 단위](stable-parallel-completion.md)는 잠금 경로 오류의 잘못된 busy 안내 수정, 실제 A checkpoint/release 중 B 조회/같은 요청·64개 쓰기와 독립 결과/회수를 scoped-complete-with-limitations로 마감했다. Editor 없는 CLI·파일 I/O 경계이며 전역 VSS 목록은 unknown, 기존 Unity/PV/대형/OS 자격은 승계하지 않는다.

일반 폴더 원본 반영의 현재 후보는 [별도 검증](plain-folder-original-apply.md)을 따른다. 일반 권한 파일 시험과 작은 Editor 없는 실제 제품 VSS·WinFsp A/B 한정 마감을 구분한다.

현재 기본 CLI는 NTFS workspace 수명과 Editor start/stop·checkpoint를 제공한다. 자체 refresh·컴파일/포커스 처리·내부 성공 gate는 제공하지 않는다. 선택적 view/Library cache·probe는 [개발용 CLI](../development-cli.md)의 경로다. 아래 M0/WF/LB·시나리오 상태는 당시 후보의 증거이며 변경된 계약의 native 실행 통과로 자동 승계하지 않는다. 현재 자동 회귀의 한도는 [범위 검증](workspace-product-scope.md)을 따른다. 검증 상태의 뜻은 [용어집](../glossary.md#응답과-검증-상태)을 참고한다.

## 현재 변경의 검증

[SJL2 당시 현재본 저널](latest-only-journal.md)은 SJL2/root v2·SJT2·v3 compact intent의 다수 파일 반복 쓰기와 현재 payload 회수를 소유한다. 실제 PNG 8/32파일 OFF/ON 각 3회에서 최종 파일 할당은 -91.81%/-93.74%이고 ON은 모든 cycle에서 일정했다. 32파일 쓰기 중앙값은 OFF 3.998s/ON 33.351s로 당시에는 기본 OFF를 유지했다. 현재 기본값은 후속 쓰기 경로 최적화 검증을 따른다. 필요한 WinFsp 정상 close/reconnect·소유 자식 중단·composite seal/accounting과 전체 회귀를 구분하고 Unity/VSS/대형 자격을 승계하지 않는다.

[선택적 기준본 복귀 정리](journal-base-reversion.md)는 이전 append/v2 intent 후보의 실제 2.8GB Unity 프로젝트 recorded Git 입력과 합성 OFF/ON 각 3회, 고정 7경계의 실제 자식 종료 각 2기준, WinFsp editorless A/B의 독립 결과/소유 회수를 소유한다. 당시 최종 저널 할당 -74.76%와 전체 측정 시간 +49.41%를 함께 기록해 기본 OFF를 유지했다. 최초 runner 접근 거부·명시적 같은 작업 재개와 Unity import/전체 순간 peak 미확인을 보존하며 새 SJL2 자격과 구분한다.

[다수 파일 시간 최적화](journal-base-reversion-optimization.md)는 별도 실제 PNG 8/32파일 반복 변경·전후 36개 stopped NTFS 저널의 정리 시간과 hash/metadata/sequence를 소유한다. ON 정리는 8파일 -79.47%, 32파일 -90.19%이며 원본/ref와 최종 할당을 보존했다. 복귀 없는 입력의 전체 비용·쓰기 변동을 함께 기록해 당시 기본 OFF를 유지했으며 이전 Unity/WinFsp 자격을 새 후보로 승계하지 않는다.

[OS와 앱 확장 계약](extensibility-boundaries.md)은 불변 경로 정책·권한 metadata와 주입한 앱별 동반 파일/원본 reservation/생성 디렉터리 descriptor의 자동 적합성을 소유한다. 실제 macOS/Linux·다른 앱·새 Unity/VSS A/B는 NotRun이다.

[코드 최적화 회귀](code-quality-optimization.md)는 `8873ba6` 위의 여섯 검토 항목에 대한 계약/파일·fake 저널 시험이다. 다음 행동의 정확한 basis/선택 인자·공유 객체 재검사·복원 검색·rename 재생·블록 순회 비용을 검증한다. 새 Unity/VSS/서비스 실행이나 닫힌 단계의 전체 지원 자격을 포함하지 않는다.

[VCS 없는 프로젝트 기준본](plain-project-basis.md)은 새 `project-snapshot`·승인한 whole-project 원본 view·source/Library 범위 분리와 NTFS 복원, 작은 파일/실제 Unity MCP·씬·리소스·현재 상태 테스트·비용의 후보별 자격을 소유한다. 기존 VSS 일반 폴더나 Git/Plastic 성공을 승계하지 않는다.

[Git 프로젝트 기준본](git-project-basis.md)은 기록된 commit/tree/blob을 VSS 없이 범위 저널에 연결하는 계약과 작은 Editor 없는 Git A/B의 자격을 소유한다. 초기 payload 복제/읽기 disk cache 0과 shared 객체/runner·full-blob 해독 비용을 분리하며 Plastic·Unity Library/Editor/MCP·대형/ARM/OS 장애는 이 경로의 자격으로 승계하지 않는다.

[Git Unity 실제 검증](git-unity-validation.md)은 별도로 승인한 현재 `splinter-unity`의 새 Git 기준 A/B와 cold Library, exact HTTP MCP·리소스/씬/현재 상태 시험·소유 회수와 관측한 준비 병목을 소유한다. 약 88분의 cold import·reload disconnect/실패 상세 소실·RAM 절감 미확인과 과거 VSS 자격을 구분한다.

[Git 원본 Library 참조](git-library-reference.md)는 승인한 Library 전용 관리형 mount·원본/A/B의 고정 기준과 범위 쓰기·마지막 일반 NTFS 복원, 새 warm Unity MCP/RAM 검증을 소유한다. cold 실행과 작은 파일 중단 시험을 실제 warm Unity 자격으로 승계하지 않는다.

[Plastic 프로젝트 기준본](plastic-project-basis.md)은 fixed changeset/blob reader·VSS 없는 범위 overlay·공통 원본 Library와 고정 실제 Unity A/B의 자동/실제 자격을 소유한다. 정확히 승인한 시험 입력 check-in, 최초 import·도구 timeout·일반 사용자 revision helper·외부 cache/전송 unknown을 구분하며 RAM 절감은 합격 조건에서 제외한다.

[저널 기본값 메타데이터](journal-metadata-storage.md)는 현재 SJR2 의미·개별 flush를 유지하는 구현과 실제 Unity A/B의 보존된 저널을 사용한 오프라인 NTFS 전후 비교를 소유한다. 기존 payload 정리 효과·추가 직렬화 비용·새 native 수명주기의 미실행과 전체 workspace/RAM 미확인을 구분한다.

[압축 저널 제품 연결](journal-payload-compression.md)은 overlay v2/SJR2·candidate v3의 회귀·작은 T: Plastic A/B·동일 입력 journal raw 대조의 실제 NTFS 할당·독립 결과/회수의 한정 자격을 소유한다. 전체 공간/RAM·대형/OS 자격과 독립 codec의 논리 표본/이전 native 자격을 구분한다.

[Unity CLI A/B 인계](unity-cli-handoff.md)는 기존 CLI/Pipeline·작은 T: Plastic 입력에서 정확한 반환 경로의 catalog/loaded 편집·A stop/release 뒤 B 후속 컴파일·두 독립 결과/마지막 snapshot 회수와 원본 보호를 scoped-complete-with-limitations로 마감했다. 프로그램 변경 없이 사용 절차를 보완했으며 목록 누락·첫 eval 타임아웃·MCP/다른 입력/Play/Test/build 미자격은 유지한다.

[Plastic 호환](plastic-compatibility.md)은 regular/static 원본의 관리 정보/등록 격리·독립 파일 결과의 자동 적합성과 고정 후보의 실제 T: A/B·마지막 snapshot 회수를 별도로 추적한다. 외부 CLI worker 발견 실패/전체 인계 미자격은 유지한다. C: 회수의 현재 종결은 위 VR-01을 따른다.

[VCS 독립 수명주기](vcs-independent-workspaces.md)는 일반 폴더의 자동 파일/fake·Git 회귀와 실제 VSS/Unity A/B·수정 후 동일 요청 회수의 scoped-complete-with-limitations를 구분한다. 후보별 실행·목록 누락·공간·미실행 경계를 유지한다. 새 상태는 v2이며 이전 상태를 자동 이관하지 않는다.

[한정 M0 납품 판정](m0-delivery.md)은 기존 개발 CLI·소스 스킬·[AI 시작 안내](../agent-start.md)의 한정 버전을 scoped-complete-with-limitations로 마감한다. 현재 복구 회귀와 과거 native의 source hash/변경 경계를 대조했고 현재 전체 native 중단 통과로 승계하지 않는다. NR/SF/SW 전체 미완료는 후속 지원 확대이며 M0의 진척 분모가 아니다.

[대표 AI A/B 사용·비용](representative-agent-workflow.md)은 독립 Editor 오류/수정·같은 작업 재개·독립 결과/dirty guard/통합·A release 중 B 지속·현재 컴파일 실패 B discard와 실제 회수를 한정 마감했다. 첫 seal/포커스 거부·같은 작업 재시도와 분 단위 준비·메모리 비용을 포함한다. 물리 peak/VSS 귀속은 unknown, native 강제 중단·OS 장애·ARM·대형 입력은 해당 실행에 포함하지 않았다.

[명시적 폐기 검증](workspace-discard.md)은 자동 자격과 승인한 서비스 Update·로컬 Editor 없는 dirty A/B의 실제 VSS 폐기·같은 요청 재개·peer 지속을 구분한다. 그 실행 자체는 scoped-complete-with-limitations이며 현재 M0의 대표 Unity 자격과 구별한다. SW-09 전체 Partial·강제 중단/SW-10 미실행은 유지한다.

[최소 AI 작업 연결](agent-workflow.md)은 스킬 원본 직접 읽기·CLI 발견·반환 절대 경로 편집·새 CLI 동일 작업 재개·explicit/direct와 명시적 폐기를 확인했다. Editor 없는 단위는 scoped-complete-with-limitations로 마감하며 SW-03/06 전체 Partial이다. 현재 Editor 자격은 위 대표 실행을 따르고 자동 스킬 발견/설치/binding·실제 busy/unknown 점유와 새/동시 대화로 확대하지 않는다.

표준 진입점은 `pwsh ./scripts/build.ps1`이다. locked restore, Release 빌드, 자동 시험을 수행하며 결과는 `artifacts/test-results/`에 기록한다. 현재 계약은 VSS v2 capture identity, 완전한 overlay 검증 tuple, `stable-journal-v1` 결과, 필수 계측 필드를 요구한다. 지원하지 않는 형식은 자동 이관하지 않고 보존 후 거부한다.

과거 후보의 시험 수와 제외 수는 당시 TRX의 기록이다. 폐기한 backend 시험의 예전 제외 수는 현재 실행 조건이나 남은 필수 검증이 아니다. [ReFS 폐기 결정](../plans/refs-retirement.md)과 [현재 제거 검증](refs-retirement.md)을 구분한다.

자동 시험은 현재 형식의 생성·읽기, 불완전한 입력 거부, checkpoint 중단 후 재시도, 소유권 불명·snapshot 소실·응답 유실에서의 보존을 다룬다. 개발 스크립트의 요청 생성 시험은 합성 영수증과 파일만 사용한다. 실제 VSS·WinFsp mount·Unity 실행을 대신하지 않는다.

## 지원 경계와 남은 관문

첫 실사용 버전의 완료 기준은 [M0](../plans/validation-matrix.md#첫-실사용-버전-m0)다. 아래 NR/SF/SW·48개 카탈로그는 기존 지원 근거와 미완료 조건을 보존한다. 전체 지원 확대를 M0의 완료 분모로 삼지 않으며, 기존 하위 성공을 새 대표 A/B 흐름의 통과로 승계하지 않는다. [제품 범위 정리 검증](workspace-product-scope.md)은 제외한 작업 그래프의 발견/거부 계약과 자동 회귀를 다룬다.

| 영역 | 확인한 범위 | 남은 작업·해석 제한 |
| --- | --- | --- |
| NI/LV 제품 NTFS | 작은 fixture의 VSS·Git·Windows x64 Unity·재연결·독립 결과·회수 | NR 전체 지원은 미완료. 실제 재부팅·전원·물리 공간 장애와 모든 명령 경계는 별도 |
| NR | 상태/저널 손상 보호, 파일 속성 거부, 현재 boot의 live identity, ARM 단일 workspace와 [준비 CLI 중단 26개](preparation-recovery.md) | NR-02는 scoped-complete-with-limitations. 게시 실패 원인 미확정·명시적 재개/회수와 fresh-key 재실행 구분. 나머지 파일·에셋, 쓰기/결과/회수 중단, ARM A/B와 OS 장애는 미완료 |
| NR-03 공통 저널 | [fake 불변 기준의 실제 자식 종료 58개](ntfs-recovery.md#nr-03-공통-저널-경계의-실제-프로세스-종료), 승인 prefix·flush 이후 transaction·tail·두 번째 재생 | 공통 단위 scoped-complete-with-limitations. 실제 WinFsp callback/mmap·VSS·동시 reader/writer·OS 장애는 미실행이며 NR-03 전체 InProgress |
| NR-03 로컬 kernel | [별도 후보의 실제 WinFsp callback/mmap/새 provider 19개](ntfs-recovery.md#nr-03-로컬-winfsp-callbackmmap새-provider-연결), 승인 데이터·동일 경로 재연결·미리 열린 다른 파일 Read·자원 회수 | 로컬 단위 scoped-complete-with-limitations. 최초 reader 종료 타임아웃·새 open/close 동시 응답 미자격 보존. 실제 VSS server/CLI·ARM·OS 장애 NotRun, NR-03 전체 InProgress |
| NR-03 제품 client | [별도 후보의 실제 VSS/CLI 외부 경계 13개](ntfs-recovery.md#nr-03-제품-vsscli의-클라이언트-경계), 같은 snapshot/delta/Git 등록·새 read proof/provider·독립 결과·일반 회수 | 제품 외부 경계 단위 scoped-complete-with-limitations. 준비 상태 게시 원인 unknown·Git text fixture 실패 보존. VSS 내부 callback/OS flush·동시 I/O·Unity/ARM·재부팅/전원/공간 NotRun, NR-03 전체 InProgress |
| NR-01 상태 쓰기 진단 | [실패 단계·게시 상태·기계용 오류와 실제 파일 시험 16개](state-write-diagnostics.md) | 진단 단위 scoped-complete-with-limitations. 실제 Windows 파일 I/O와 새 소유 diagnostic Git CLI·공통/schema/fake 범위. 이전 NR-02/03 상태 게시 실패 원인은 unknown, NR-01 전체 InProgress. VSS/WinFsp·전원/공간 관문의 자격과 구분 |
| PV-01/02 | 대표 fixture와 사용자 선택 비교 범위는 scoped-complete-with-limitations | PV-02 전체 Passed 아님. S4 FailedInputQualification, 소수 표본, 전체 팩 raw 입력 동등성 unknown |
| SF-01/02 | 변경 기반 소스 검증, 명시적 정리·중첩 예산·계측의 하위 범위 | 공통 저장소 최초 실행의 FailedPreservation은 유지. SF-02 전체 Partial, 대형 용량 목표 미달성 |
| SF-03/VC/VR | 개발용 작은 A/B/A, 정상 cold import와 재연결 | SF-03 전체 Partial·VR-03 Partial 유지. 큐/일반 임대 인수는 구현 제외, source 반환 병합은 AI 절차. 현재 제품 연결은 WF로 구분 |
| SF-03/VW | [공통 수명주기](../view-workflow.md)의 지속 상태·영수증 재조정·fake 적합성 | 합성 파일/영수증이며 실제 VSS·WinFsp·Unity 지원 증거가 아님. 자동 실행 상태는 VW, 제품 연결 자격은 WF를 따름 |
| SF-03/VG | [등록 증거가 있는 관리 파일 분리](../source-view.md#등록-증거가-있는-관리-파일-분리)의 실제 임시 Git 등록·합성 불변 트리 적합성 | 후보 v2 게시의 자동 시험 범위. 실제 provider 실행은 WF의 현재 후보 증거로 별도 판정 |
| SF-03/VP | [같은 요청 게시 복구](../source-view.md#같은-요청의-게시-복구)의 영구 intent·부분 저널 prefix·완료 receipt 재조정 | 합성 트리·실제 임시 파일·예외 주입 범위. 실제 CLI 중단은 WF, 재부팅·전원/공간 장애는 NR의 별도 관문 |
| SF-03/WF | [제품 연결·복구](product-validation-workflow.md), [두 빈 cache](product-validation-cold-cost.md), [현재 후보의 일반 권한 cold/기존 Library](product-validation-unelevated.md)와 독립 결과·회수 | scoped-complete-with-limitations. 작은 fixture·준비/검증 재시작·포커스 재시도와 heartbeat 오류 기록의 제한 유지. 대형 용량·일반 cold 안정성·NR 전체를 증명하지 않음 |
| LB-01~05 | [실제 Library snapshot 재사용·정리·재증가와 회수](product-validation-unelevated.md) | 고정 후보의 Library 수명주기 범위 scoped-complete-with-limitations. 전체 순간 물리 peak·VSS 귀속은 unknown이며 SF-02/03 전체는 Partial |
| RI/RU/WI | 합성 또는 동일 닫힌 저널의 제한된 replay 비교 | warm cache·소수 표본. 누적 Editor 전체 개선·Library 물리 절감으로 환산하지 않음 |
| JW/FR | 고정 쓰기 계측과 작은 URP fixture의 요청별 유효 PNG 검사 | Windows 선행 직렬화·내구성 경계 유지. Unity 내부 첫 draw 원인 미확정, group flush 미선정 |
| SW-09 폐기 | [로컬 dirty A/B의 서비스 적용·동일 요청 재개](workspace-discard.md)와 [대표 Unity 실패 worker stop/폐기](representative-agent-workflow.md) | 하위 실행 scoped-complete-with-limitations, SW-09 전체 Partial. native 강제 중단·SW-10 미실행 |
| M0, SW-03/06 | [최소 AI 지침·direct](agent-workflow.md)·[대표 Editor 사용/종료·비용](representative-agent-workflow.md)와 [현재 복구 회귀/한정 납품](m0-delivery.md) | 한정 M0 마감·현재 모든 native/OS 장애 통과 아님. SW-03/06 전체 Partial, busy/unknown 점유·새/동시 대화·자동 설치/binding은 미실행 |
| SF-04~06, SW-01/02/04/05/07/08/10 | 보존한 시나리오·후속 책임 분류 | NotRun 유지. 대형/특수 지원은 필요 시 검증, 모드/설치/인계는 AI 절차, 자동 모드/전용 installer/binding은 구현 제외. 전체 대화/Editor 흐름은 미자격 |
| UA-01~04 | [제한 권한 서비스](storage-privilege-service.md)의 실제 설치·일반 CLI A/B 추가 UAC 0·회수·서비스 재시작/응답 유실 | 등록된 Windows x64 NTFS/Git fixture 범위 Passed. 일반 Unity·ARM·실제 전원/재부팅과 native VSS 호출 도중 강제 종료로 확대하지 않음 |

새 후보에서 필요한 실제 환경 시험을 실행하기 전에는 아래 기록을 현재 후보의 통과로 승계하지 않는다. GUI Unity·VSS·격리 디스크 시험은 정의된 범위와 소유 자원을 확인한 뒤 실행한다. 현재 원본·다른 작업·기존 결과 refs와 비소유 snapshot을 보존한다.

[WF-09~12와 LB-01~05](product-validation-unelevated.md)는 해당 기록이 고정한 후보의 실제 실행으로 마감했다. [NR-02](preparation-recovery.md)는 별도 최종 후보의 준비 경계이며 이전 Unity/Library 자격을 자동 승계하지 않는다. 실행 범위와 재검증 기준은 [저장공간 우선 계획](../plans/storage-priority-and-editor-modes.md#보존한-한정-범위-일반-권한-unity-검증-흐름), 남은 일반 지원·대형 용량·설치 관문은 [추적표](../plans/validation-matrix.md)가 소유한다.

## 후속 작업에 필요한 기록

아래 자료는 변경 이력이 아니라 재현 조건·실패 근거·비교 기준으로 보존한다. 각 파일의 후보·입력·날짜와 제한을 함께 읽는다. 원시 artifacts는 로컬에 있을 수 있으며 문서 정리로 지우지 않는다.

| 기록 | 보존 이유 |
| --- | --- |
| [한정 M0 납품](m0-delivery.md) | 현재 복구 계약 회귀·기존 native 소스 대조/재사용 한계와 시작 명령의 실제 관측, 개발 CLI 전제의 한정 완료 |
| [대표 AI A/B 사용·비용](representative-agent-workflow.md) | 작은 warm 입력의 오류 격리·동일 작업 재개·독립 결과/dirty guard/통합·실패 폐기, 종료 뒤 Library lock 쓰기/포커스 거부·같은 작업 재시도와 시간/파일 할당 제한 |
| [제품 범위 정리](workspace-product-scope.md) | 작업 그래프 제외의 명령/schema 발견·오류 응답 계약, 현재 자동 시험과 실환경 자격의 구분 |
| [제한 권한 서비스](storage-privilege-service.md) | 보호된 상태·IPC 경계·최초 설치·반복 UAC·서비스 중단/응답 유실을 구분한 완료 판정 |
| [VSS 보존 실패](storage-foundation.md), [삭제 차단과 복구](storage-recovery.md), [지연 회수](deferred-snapshot-reclamation.md) | exact-ID 삭제의 타 snapshot 영향, 격리 guard, 같은 작업의 명시적 재시도와 회수 순서 |
| [NTFS 제품 통합](ntfs-workspace-integration.md), [live identity](ntfs-live-vss-identity.md), [NR 복구](ntfs-recovery.md) | 실제 Windows/ARM 조합과 미완료 장애·파일 관문, 환경별 재현 조건 |
| [준비 CLI 중단 복구](preparation-recovery.md) | 같은 최종 후보의 실제 경계 26개·동시 재개·자식 실행 세대·독립 결과/원본 보존·회수, 게시 실패 원인 미확정과 명시적 복구 |
| [병렬 비교](parallel-workflow.md) | C/S/Q의 동일 입력 조건·결과 refs·Library/큐/전환 비용, S4 입력 실패와 표본 한계 |
| [초기 overlay 시험](snapshot-overlay.md) | NTFS 파일 API·소유권의 제한된 실환경 증거. NTFS 전체 지원으로 합산하지 않음 |
| [폐기한 ReFS의 과거 근거](storage-and-resources.md), [제거 검증](refs-retirement.md) | 과거 SR 증거 보존과 현재 기능/계약 제거의 자동 회귀를 구분 |
| [저널 정리](journal-compaction.md), [Unity와 정리 비용](journal-compaction-qualification-and-cost.md), [예산과 재증가](journal-budget-and-growth-qualification.md) | 중단 복구, 두 세대 겹침·전체 할당 peak와 Library 유효량, 재개 후 재증가 |
| [저널 정리 크기 회귀](journal-compaction-size.md) | 작은 덮어쓰기가 오히려 저널을 키우는 조건·기존 frame 유지·동일 합성 입력의 NTFS 파일 할당 비교 |
| [동일 private 쓰기](duplicate-private-writes.md) | byte 일치·전체 private 범위의 중복 저장 억제·metadata/flush·예산/복구·작은 로컬 WinFsp client 완료 |
| [작은 Unity 동일 쓰기 비교](unity-duplicate-private-writes.md) | 실제 제품 CLI/VSS/WinFsp/Unity의 warm 편집·재시작 한 쌍·저널/Library·동일 결과/회수와 관측 차이 한계 |
| [부분 쓰기·버퍼·시작 개선](resource-efficiency-followup.md) | private 일치 앞뒤 재사용·요청 크기 버퍼의 고정 입력 대조·로컬 시작/재시작·현재 검증/보존/회수 |
| [Library payload 압축 후보 선정](library-block-compression-feasibility.md) | 최근 UH 누적 비용·Bee 4개 파일/24개 조합의 정확한 복원·크기 모델. 다음 bounded codec 후보이며 제품/물리 공간 절감은 미자격 |
| [독립 범위 codec](workspace-payload-codec.md) | Core/fake·raw/Brotli·손상/trailing 거부·부분 읽기/임대 반환·동시 caller와 실제 Bee 표현 대조. product journal 미연결 |
| [공통 Library A/B/A](immutable-validation-view.md), [준비와 replay](validation-view-readiness-and-replay.md) | 불변 후보와 검증 세대의 분리, 정상 stop/remount/restart 비용·최초 import 제한 |
| [Library 수명주기와 일반 권한 실행](product-validation-unelevated.md) | 고정 snapshot Library·범위 쓰기·정리/재증가·cold 비교·15개 독립 결과와 회수·실패/복구 한계 |
| [제품 validation workflow](product-validation-workflow.md) | 실제 CLI 연결, 서로 다른 VSS와 한 view/cache, 코드·에셋·PNG·세대 분리, CLI 중단 복구, 독립 결과와 VHD 분리 후 검증 |
| [새 cache 반복과 전체 비용](product-validation-cold-cost.md) | 최종 후보의 두 빈 cache A/B/A, 2분 정상 종료 대기, 준비/검증 재시작 비용, 원시 응답 schema·실패 포함 할당·16개 독립 결과·소유 자원 회수 |
| [제어 파일 읽기 경합](control-file-read-races.md) | Windows 동시 Replace/guard/open의 부재·공유 오류, 유한 읽기 복구와 지속 실패·손상·owner 보존. 실제 WinFsp/VSS/Unity 재자격과 구분 |
| [Replay index](replay-index-cost.md), [실제 Unity 비교](unity-replay-comparison.md), [범위 index](cumulative-write-range-index.md) | 고정 입력·후보 hash·단계별 replay와 실제 Editor 비용을 비교하는 기준 |
| [Live 쓰기](live-journal-write-cost.md) | flush 내부 비용, callback 직렬화·대기/완료 경계와 향후 내구성 판단 |
| [첫 미리보기](first-render-qualification.md) | 첫 draw 미확정 원인, cold/reload/재시작과 실패 주입, PNG 해시 기반 판독 기준 |

시나리오의 실행 상태와 후속 책임 분류는 [검증 추적표](../plans/validation-matrix.md), 축소한 작업/종료 기준은 [구현 계획](../plans/implementation-plan.md), 비용 판단은 [저장공간 우선 계획](../plans/storage-priority-and-editor-modes.md)을 따른다. 구현 제외를 Passed로 바꾸지 않고 전체 NotRun을 개발 backlog로 읽지 않는다. 완료 단계별 계획과 반복 CHANGE LOG는 유지하지 않는다.
