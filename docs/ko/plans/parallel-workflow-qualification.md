# 병렬 작업과 비용 비교 방법

[English](../../plans/parallel-workflow-qualification.md)

이 문서는 후속 비교에서 입력·C/S/Q 조건·작업·계측을 맞추기 위한 방법을 보존한다. 사용자 선택 실행은 마감했으며 새 실행 지시가 아니다. 판정과 남은 한계는 [검증 안내](../verification/README.md)를 따른다.

## 확정한 목표

**현재 NTFS 내부 알파로 독립 대화 A/B가 여러 차례 스크립트를 수정·검증·재개하고, A를 먼저 원본에 반영·회수한 뒤에도 B가 같은 작업을 계속할 수 있는지 확인한다. 실제 3D 모델·텍스처를 포함한 같은 Unity 프로젝트에서 Codex 기본 Git worktree의 독립 Editor 방식 및 Coordination을 통한 단일 Editor 순차 검증 방식과 각각 비교하고, 시간·읽기·저장·메모리·호출·운영 개입 비용으로 다음 개선 대상을 결정한다.**

[기획서](../../../plan.ko.html#next)의 범위 안에서 기존 PV 비교의 입력·실행/종료 조건을 보존한다. 이 비교는 자동 다음 작업이 아니며 [NR](ntfs-recovery-and-support.md)의 선택 검증과 [SW](session-workflow-and-installation.md)의 AI 절차를 전체 선행 조건으로 열지 않는다. 한정 결과를 공개 지원이나 NR/SW 전체 완료로 올리지 않는다.

## 제품 목표·구조와 현재 도달점

제품 목표는 각 대화의 AI가 필요할 때 자기 개발 환경을 확보하고 같은 작업을 지속하는 것이다. Windows/NTFS 원본의 위치와 파일시스템을 유지하고, 시작 시점의 불변 스냅샷과 작업별 범위 변경분을 사용한다. 최초 전체 데이터 복제·읽기 hydration·파일 전체 copy-up fallback 없이 원본은 작업 중에도 계속 수정할 수 있어야 한다. 명시적 반영·보존·폐기는 서로 다른 사용자 의도다.

현재 구조는 **CLI → Runtime → Core의 공급자 계약 → Git/Unity/Windows 어댑터**다. CLI가 구현체를 조립하고 Runtime이 atomic JSON·작업/저장소 잠금·결과/회수를 조정한다. Core에는 Unity·Win32·Git·Codex 전용 API를 넣지 않는다. Host는 읽기 전용 in-process 계층이며, 제품 .NET 어셈블리 대신 작은 Editor 브리지가 작업별 Unity와 통신한다. 이번에도 이 경계를 유지한다. 실제 프로젝트 참조와 책임은 [현재 기반](../foundation.md#구조와-의존성)에 있다.

## 포함하는 작업

### 1. 실행 준비와 관측 고정

- 로컬 Windows x64의 기존 NTFS/WinFsp·Unity fixture를 기본 대상으로 한다. 실제 OS/도구 버전, 소스·Library 파일 수/크기, 여유 공간·RAM, 원본 Editor/MCP와 기존 작업의 소유권을 실행 전에 기록한다. ARM A/B와 재부팅 시험은 별도 NR 범위다.
- Splinter 측 모든 대화가 같은 짧은 상태 home을 사용하고, A/B는 서로 다른 key와 반환된 절대 workspace 경로를 사용한다. 짧은 경로의 실제 쓰기 권한·파일 도구·CLI·Editor 대상 일치를 확인한다. Codex 기본 worktree 비교군은 별도 실행으로 만들며 Splinter workspace와 중첩하거나 소유권을 공유하지 않는다.
- 새 캡처의 원본은 clean 저장소 루트와 저장·import/compile 완료 상태여야 한다. 캡처 이후의 원본 편집을 막지 않는다. 실행에 필요한 관리자 helper는 제품의 정확한 `nextActions`와 기존 권한 절차를 따른다.
- A/B는 각각 자기 대화에서 편집·검증·재개·종료를 수행한다. 대화별 project/key/home/workspace/path를 시험 증거에 기록하되 이를 제품의 자동 binding 구현으로 표시하지 않는다. 별도 조정 AI·자동 Runner는 요구하지 않는다. 이번 계획 작성 중 새 대화나 작업자를 만들지 않는다.
- Splinter S와 Codex 독립 Editor C에는 **Unity Agent Coordination을 사용하지 않는다**. A/B가 각 작업공간의 독립 Unity 인스턴스에서 동시에 작업한다. 사용자가 추가한 Q에서만 Codex worktree의 병렬 코드 작업과 Coordination의 단일 Editor 순차 검증을 수행한다. 원본과 작업 Editor를 절대 경로·PID·시작 시각으로 식별하고 각 방식의 동시 Editor 수를 기록한다. Q의 후보 전환과 공용 Editor 임대는 Q 실행 구간에만 적용한다.

### 2. PV-01: 여러 차례 작업과 한쪽 선행 완료

| 순서 | 실행할 사례 | 완료 증거 |
| --- | --- | --- |
| 1 | 같은 clean 원본 기준에서 A/B를 생성하고 각 Editor 준비 후 첫 스크립트 수정·검증 | 서로 다른 key·workspace·Git index·PID/시작 시각, snapshot/delta identity, 각 요청의 fingerprint와 실제 로드된 코드 |
| 2 | A에 컴파일 오류를 넣은 상태에서 B가 두 번째 수정·검증을 완료하고, A는 후속 요청에서 오류를 수정 | A 실패 구간과 B 성공 구간의 시각·요청 ID. 오류/변경이 상대 작업과 원본에 전파되지 않음 |
| 3 | A/B 생성 이후 원본도 별도의 시험 스크립트를 수정·검증하고, A/B는 이전 기준을 유지 | 원본의 새 내용과 loaded-code, A/B의 변하지 않은 기준·해당 파일 hash. 원본 변경은 시험 소유 변경으로 기록하고 이후 통합 전 명시적으로 커밋해 clean 상태로 만듦 |
| 4 | 새 CLI 호출과 같은 대화의 후속 요청에서 기존 작업을 재개하고 추가 수정·검증 | 같은 project/key/home/workspace/snapshot/delta, 기존 변경 유지, 중복 workspace/Editor 없음. 실제 앱 재시작·문맥 압축을 하지 않았다면 그 복구까지 통과로 기록하지 않음 |
| 5 | A의 명시적 완료 요청에 최종 검증 → checkpoint → integrate → 원본 Unity 확인 → release. 이 동안 B는 작업을 유지 | A 결과 ref/tree/manifest와 안정된 저널 seal, 원본 반영·실행 값, A 소유 자원 회수. B의 PID·시작 시각·snapshot/delta와 기존 변경 유지, 원본 Editor/MCP 유지 |
| 6 | A 회수 뒤 B가 한 번 더 수정·검증하고, 별도 완료 요청에 checkpoint → 최신 원본에 integrate → 원본 Unity 확인 → release | B가 새 workspace 없이 진전한 증거, 원본에 A/B와 원본 자체 변경이 함께 존재, 두 독립 결과 보존, 이번 실행의 소유 자원 0/부재 |

A/B/원본 변경은 서로 다른 스크립트 또는 충돌하지 않는 위치로 한정한다. 충돌 해결·미커밋 입력 상속·새 통합 후보 기능을 함께 개발하는 시험으로 확대하지 않는다. 실행 중 예상하지 못한 원본 변경이 있으면 그대로 보존하고 통합을 중단한다.

**현재 checkpoint는 편집 중간의 저장 지점이 아니라 최종 결과 수집 경계다.** `WorkspaceCoordinator`는 결과 수집 뒤 start/refresh를 `ResultAlreadyCollected`로 거부한다. 따라서 각 작업의 여러 차례 편집과 재개는 checkpoint 전에 수행하고 B를 미리 checkpoint하지 않는다. 이 제한을 풀어 같은 수집 결과에서 다시 편집하는 기능은 이번에 추가하지 않는다.

현재 `integrate`는 검증한 결과와 clean 원본/대상 branch를 확인해 Git merge한다. 최신 원본과 합친 후보를 **적용 전에 Unity로 검증하는 제품 기능은 없다**. 기존 PV는 통합 뒤 원본 refresh/loaded-code를 확인하고 실패하면 결과/상태와 실패를 보존하며 자동 원복·사전 검증 통과로 위장하지 않는다. 사전 후보/통합 후 게임 검증은 AI가 Git·외부 Unity 도구로 수행할 절차다. Splinter의 통합 검증 플랫폼은 구현 제외이며 현재 gate는 유지한다.

loaded-code는 PID·프로젝트 경로·관측 시각·domain generation·assembly MVID와 실행 값을 연결한다. 공통 Qualification fixture의 새 `inspect` 응답과 요청 ID가 일치해야 하며, fixture 초기 `snapshot-probe.json`만으로는 최신 증거가 충분하지 않다. 제품 heartbeat는 fixture를 호출하지 않는다. A/B에서 실제 바꾼 함수의 값을 기존 probe로 관측하고, 부족할 때만 fixture 계측을 보완한다. 원본은 기존 MCP로 해당 함수를 실행한다. 실제 실행하지 않은 함수는 assembly MVID만으로 동작 확인을 대신하지 않는다.

### 3. PV-02: Codex 기본 worktree와 실제 에셋 프로젝트 직접 비교

**Codex 기본 worktree 방식과의 직접 비교, 실제 모델·텍스처가 들어 있는 Unity 프로젝트 시험을 PV-02의 필수 범위로 포함한다.** Splinter 단독 계측이나 합성 파일 시험만으로 PV-02를 완료하지 않는다. 아래 비교 방식 C/S/Q와 각 방식 안의 작업 대화 A/B를 구별한다.

#### 비교 방식과 공정한 출발점

| 방식 | 작업공간과 Unity 준비 | 비교 경계 |
| --- | --- | --- |
| C: Codex 기본 worktree | 실제 Codex 앱에서 같은 clean 기준 커밋의 독립 worktree 대화 A/B를 만들고 각 경로의 Unity를 실행 | 앱의 기본 Git checkout·설정된 setup·Editor/Library 준비·재개·결과 보존·정리 비용을 포함. 단순 `git worktree add` 실행만으로 Codex 세션 비교를 했다고 기록하지 않음 |
| Q: Codex worktree + Unity Agent Coordination | 실제 앱 worktree A/B에서 코드 편집은 병렬로 진행하고 Coordination의 공용 Editor 큐로 검증을 순차 수행 | Splinter를 사용하지 않는다. 작업 worktree에는 Editor/Library를 만들지 않고 공용 Editor에 후보를 전달한다. 후보 commit·checkout/전달·큐 대기·compile/import·원본 복귀 비용, 공용 Library 재작성과 두 소스 checkout의 공간을 모두 포함 |
| S: Splinter NTFS | 같은 커밋·원본 프로젝트에서 각 대화가 `workspace ensure --storage ntfs-snapshot`으로 A/B를 만들고 Unity를 실행 | snapshot·범위 저널·Git 등록·검증/결과/회수 및 필요한 helper를 포함. 원본 Library 재사용의 실제 성공·재작성 비용을 관측 |

Q는 사용자가 2026-09-23 실행 도중 명시적으로 추가한 비교군이다. 앞서 제거한 Coordination 사용은 원본 준비용이었으며 Q 실행 증거가 아니다. Q 전용 기준에는 이전에 사용한 `v0.3.0` 패키지를 복원하고 실제 resolved revision을 기록한다. 공통 소스·에셋·작업은 C/S와 동일하게 유지하며 Coordination 패키지와 단일 Editor는 의도한 차이로 공개한다. 전역 스킬이 없어도 패키지 저장소의 스킬을 명시적으로 참조할 수 있다. S에 스킬이나 패키지를 적용하지 않는다.

Q에서는 코드 수정 완료, 큐 등록·획득, 후보 전달/checkout, compile/import 완료, loaded-code/화면 확인, 임대 반환 시각을 연결한다. Coordination 자체가 worktree 변경을 공용 Editor로 복사한다고 가정하지 않는다. exact-SHA 검증에는 커밋한 후보와 clean 공용 checkout을 사용하고 commit 전 에셋 작업에는 일반 access lease를 사용한다. 실제 transport API로 상태·임대·종료를 처리하며 coordination 상태 파일을 직접 수정하지 않는다. 원본 변경은 별도 ref에 보존하고 후보 전환 뒤 돌아올 원본 ref를 기록한다. A 오류에서 B 후보로 전환할 때의 지연도 포함한다. Q에는 두 Editor 동시 성공 조건을 적용하지 않고 같은 최종 결과·보존과 대기 포함 전체 완료 시간으로 비교한다.

Q의 중간 코드 수정도 commit-optional 일반 FIFO 큐를 사용하고 최종 결과에 exact-SHA 검증을 적용한다. integrator가 공용 checkout을 알려진 기준으로 준비하고, 각 작업자는 자기 임대 안에서 자기 변경 파일만 전달·검증·회수한다. 생성된 `.meta`/씬 변경은 자기 worktree에 hash를 확인하며 보존하고 임시 전달본만 복원한다. 원본 ref 전환은 integrator가 임대가 없는 경계에서 수행하고 시간을 기록한다. 단일 Editor가 원본/A/B의 실행 상태를 동시에 유지할 수 없는 차이를 명시한다.

- 세 방식은 같은 PC·NTFS 저장 장치 조건·Unity 패치·공통 패키지·빌드 타깃·에셋/임포트 설정·작업 대화 수 2·작업 내용과 완료 조건을 사용한다. Q에만 추가하는 Coordination 패키지와 Editor 직렬 사용은 의도한 차이다. Codex 버전·모델·추론 설정·공통 도구와 원본 Editor의 실행 조건을 맞춘다. 경로 길이와 저장 볼륨 차이는 기록하고 필요하면 별도 변수로 분리한다.
- 공식 문서상 Codex managed worktree는 Git checkout으로 시작하며 `.worktreeinclude`로 지정한 ignored 파일의 추가 복사와 local environment setup이 가능하다. 현재 fixture의 `Library`는 Git ignore이고 `.worktreeinclude`는 없다. 실행 시 앱 버전·실제 설정·생성 직후 파일과 Library 유무를 확인한다. [OpenAI Worktrees](https://learn.chatgpt.com/docs/environments/git-worktrees), [Local environments](https://learn.chatgpt.com/docs/environments/local-environment).
- 주 비교군은 사용자가 요청한 기본 worktree 흐름이다. Library를 사전에 수동 복사하거나 공유하는 최적화를 몰래 넣지 않는다. 기존 환경에 그런 설정이 있으면 이를 공개하고 기본 조건과 구별한다. Library 복사를 추가로 비교할 경우에는 별도 보조군으로 두고 복사 시간·공간·정합성 확인 비용을 모두 포함한다.
- 원본의 최초 에셋 확보·임포트·Git 기준 준비는 세 방식에 공통인 환경 준비 비용으로 별도 보고한다. 그 뒤 새 작업의 생성부터 실제 편집 가능까지를 주 비교 구간으로 삼는다. 작업별 Library의 시작 상태를 기록하며 Splinter의 기준 Library 읽기 재사용은 검증할 제품 동작이다. Q의 패키지 준비도 별도 기록한다. 어느 방식의 준비 비용도 타이머 밖에 숨기지 않는다.
- C/S/Q를 동시에 돌려 서로의 디스크·RAM을 경쟁시키지 않는다. C/S 내부 A/B Editor는 병렬이고 Q의 코드 작업은 병렬·Editor는 직렬이다. 순서는 C→S→Q, S→Q→C, Q→C→S처럼 교차하며 실제 순서와 예외를 기록한다. OS 파일 캐시·Unity Accelerator·패키지 캐시 상태를 기록하고 전역 캐시를 임의 삭제하지 않는다. 통제하지 못한 조건은 해당 환경의 관측으로 표시한다.
- 기존 작업 재개는 이미 준비한 작업공간을 재사용한다. Q는 같은 공용 Editor/Library의 다음 후보 검증과 Editor 재시작을 구분한다. 핵심 생성/Editor 준비·반복 작업은 방식별 3회 이상 측정해 개별값·중앙값·범위를 보고한다. 전체 오류·에셋·선행 완료 흐름은 각 방식의 대표 실행에서 수행하고 나머지 반복의 공통 핵심 작업을 미리 고정한다. 미달하면 소수 실행 관측으로 표시하고 확정적인 성능 개선율을 제시하지 않는다.

#### 실제 에셋과 시험 규모

기존 작은 스크립트 fixture를 대조 기준으로 유지하고 **임포트 가능한 실제 3D 모델·텍스처·재질·이를 참조하는 확인용 씬**을 가진 대표 프로필을 추가한다. Unity는 원본 에셋에서 별도의 import artifact를 만들고 Asset Database를 Library에 유지하므로 Assets의 바이트 수와 Library 생성·재작성 비용을 나눠 측정한다. [Unity 6.3 Asset Database](https://docs.unity3d.com/6000.3/Documentation/Manual/asset-database-contents.html).

| 프로필 | 구성 | 판정 |
| --- | --- | --- |
| 기본 | 기존 스크립트 fixture·현재 패키지와 공통 관측용 지정 모델/텍스처/씬. Medieval village 전체 팩 제외 | C/S/Q에서 작은 프로젝트의 고정 비용을 확인하는 필수 대조 |
| 대표 에셋 | 사용자가 제공한 Medieval village의 실제 모델, 1K·2K·4K 텍스처, 재질·씬 참조. 원본 에셋 합계 2~5GiB를 초기 목표로 삼고 실제 리소스 구성을 고정 | C/S/Q 직접 비교의 필수 프로필. 실제 크기·개수·mesh 정점/삼각형·텍스처 해상도/포맷·import 설정·Library 크기를 manifest로 확정. 이 패키지에는 8K 원본 텍스처가 없어 8K 지원 증거로 확대하지 않음 |
| 확대 | 대표 프로필 결과와 여유 공간/RAM을 확인한 뒤 10~20GiB 수준의 다양한 에셋으로 확대 | 선택적 추가 관측. 미실행을 필수 대표 프로필의 성공이나 대형 프로젝트 일반 지원으로 바꾸지 않음 |

- 사용자가 임포트한 Medieval village를 대표 프로필로 사용한다. 현재 원본 폴더의 1,512개 파일 합계는 2,798,526,062바이트(2.61GiB)다. FBX 151개, 텍스처·재질·프리팹·씬이 포함된다. 패키지 원본은 보존하고 모델·텍스처 변경에는 이 에셋에서 만든 지정 시험 사본을 사용한다. 추가 구매나 다운로드를 전제로 하지 않는다.
- 파일 패딩, 같은 모델의 무의미한 중복, 임의 바이트를 이미지 확장자로 바꾸는 방식으로 목표 용량을 채우지 않는다. 큰 파일과 많은 중소 파일을 포함하고, 확인용 씬에서 실제 mesh·texture 참조와 표시가 정상인지 확인한다. 단순히 파일이 디스크에 존재하는 것은 Unity 에셋 시험의 완료 증거가 아니다.
- 에셋과 `.meta`의 동일 hash/GUID, 같은 공통 Git 기준을 세 방식에 제공한다. 현재 Splinter가 거부하는 LFS/filter 등은 이번 비교에 섞지 않는다. 큰 바이너리의 Git 객체 증가와 checkout 비용도 측정하며, 공유 Git 저장소를 worktree마다 중복 합산하지 않는다. 유료/개인 에셋과 대형 fixture의 원격 공개는 이 범위가 아니다.
- 원본 Assets·Git·Library와 C의 두 checkout/Library, S의 VSS/delta·결과, 측정 파일의 실제 예상 할당을 합산해 준비 예산을 정한다. 논리 에셋 용량만 보고 여유 공간을 판단하지 않는다. 여유가 부족하면 규모와 미실행 범위를 기록하고, 실제 디스크 고갈 시험으로 확대하지 않는다.

#### 공통 작업 시나리오

| 사례 | 공통 목표 | 주요 비교 |
| --- | --- | --- |
| 새 작업 A/B | 같은 기준에서 작업 2개 생성 → Unity ready → 확인용 씬의 모델/텍스처 로드 확인 | 생성과 첫 임포트/캐시 재사용, C/S의 두 Editor 또는 Q의 순차 검증이 사용 가능해질 때까지의 전체 시간·추가 공간·RAM |
| 에셋 유지·코드만 변경 | 큰 에셋은 그대로 두고 PV-01의 여러 차례 수정·오류 격리·재개 실행 | 변경하지 않은 에셋의 읽기/복제·Library 재사용, compile/reload와 실제 코드 실행 비용 |
| 같은 작업 재개 | C/S는 각자의 기존 작업/Library로 Editor를 다시 열고 추가 수정. Q는 같은 작업 대화와 공용 Editor에서 다음 후보를 검증 | 최초 준비와 반복 작업 비용의 분리, 같은 작업 연결 유지. Q의 큐 재사용을 Editor 재시작으로 표시하지 않음 |
| 텍스처 변경 | 지정한 유효 텍스처 한 개의 내용 변경, 별도 사례로 import 설정 한 개 변경 | 실제 재임포트 완료·해상도/포맷·표시 확인, 소스와 Library 쓰기·저널/할당 증가 |
| 모델 변경 | 지정 모델 한 개를 유효한 다른 revision으로 교체하고 참조 씬에서 확인 | mesh/참조의 실제 변경과 재임포트 완료, 파일 전체 재작성과 범위 저장 비용 |
| 한쪽 먼저 완료 | A 결과 보존·원본 반영·확인·정리, B 추가 작업 후 완료 | 정상 결과를 얻는 전체 시간, 반영/정리·남은 저장량, 상대 작업과 원본 보존 |

에셋만 바뀐 경우 외부 MCP/Unity CLI의 컴파일·도메인 reload 관측을 import 성공으로 대신하지 않는다. Splinter 자체 refresh·컴파일 증거는 제공하지 않는다. fixture의 import 완료·Asset Database/실제 로드한 mesh·texture·씬 참조와 화면 증거를 수집한다. 코드 검증을 위한 추가 변경이 필요하면 양쪽에서 같은 단계로 수행하고 순수 재임포트 구간과 따로 측정한다. 이번 제한된 관측은 범용 에셋 저작 API나 Play/Test 기능의 구현을 뜻하지 않는다. [Unity refresh 절차](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetDatabaseRefreshing.html).

#### 단계별 계측과 해석

PV-01의 같은 Splinter 후보에 관측을 붙이고, C/S 공통 구간과 제품 전용 하위 단계를 연결한다. Codex에는 없는 `status`/seal 명령을 억지로 실행시키지 않고 같은 사용자 목표를 끝내는 실제 비용으로 비교한다. Unity 밖의 합성 저장소 사례는 원인을 분리하는 보조 시험으로 유지한다.

| 관측 구간 | 반드시 구분할 값 |
| --- | --- |
| 생성·Editor 준비 | 공통 환경 최초 준비와 C/S 작업별 준비, checkout 또는 VSS/메타데이터/Git 등록/mount, import/Editor ready 시간, source/Library 파일 수·크기, 초기 payload와 metadata |
| 일반 status | 새 pending 응답이 없는 상태에서 반복 10회, pending 대기와 새 결과 반영을 별도 기록. 호출별 시간·읽기량과 sourceVerification; 새 결과 반영의 정당한 소스 검사 포함 |
| 명시 확인·refresh | `--verify-source`, 검증 요청, compile/reload와 결과 조정의 시간·읽기량. `workspace.validation.success`와 현재 fingerprint 일치 확인 |
| checkpoint·integrate·release | 전체 소스/저널 읽기, 결과 commit/ref, seal, 원본 반영·확인, unmount/Git/delta/VSS 회수별 시간·저장량·호출 수 |
| 실행 자원 | 원본/A/B Editor·자식/provider의 working set/private bytes/핸들 표본과 관측 시각. 표본 최댓값을 측정하지 않은 전체 실행 peak로 표시하지 않음 |
| 저장소 규모 대조 | 비희소·비압축 16MiB/256MiB/1GiB 합성 파일의 같은 4KiB 쓰기와 반복 읽기. 파일 수가 다른 작은 파일 트리도 크기·개수를 기록해 metadata 비용과 payload 비용을 분리 |
| 사용자·AI 운영 | 단계·대화별 CLI/도구 호출·재시도·조회 수, 일회 권한 처리, 키 입력·수동 복사·Editor 재연결 같은 운영 개입. 정상 수정 요청과 명시적 종료 지시는 별도 집계. 세션별 토큰/과금 정보가 제공될 때만 별도 기록하고 장비 비용과 혼합하지 않음 |

- 최초 전체 payload 복사 0, 읽기 hydration 0, 작은 쓰기의 범위 저장은 필수 저장 계약이다. 소스 fingerprint·저널 seal 계산의 읽기 I/O를 0이라고 주장하지 않는다.
- 읽기 바이트는 수집한 계수의 출처·대상 프로세스·시간 구간을 명시한다. 논리 읽기/캐시/물리 디스크 I/O를 구별하고, 분리 관측이 안 되는 항목은 unknown과 원인을 남긴다. 필요한 계측이 없으면 작은 개발용 계측을 추가하되 새 제품 backend나 상주 서비스로 확대하지 않는다.
- journal payload/metadata/파일 길이, NTFS 실제 할당, VSS diff-area, Git 객체와 Unity의 실제 Library 재작성 비용을 따로 기록한다. VSS의 볼륨 전체 증가에는 원본·다른 프로세스 쓰기가 포함될 수 있으므로 작업별 값으로 임의 배분하지 않는다. 지원되지 않는 수치는 0으로 채우지 않는다.
- 합성 파일 결과는 저장소 비용이며 실제 모델·텍스처의 import 성능 증거를 대체하지 않는다. C/S 각각 병렬 2작업의 차이를 비교하며, 별도 순차 1작업 비교군 없이 병렬화 자체의 속도 향상률로 표현하지 않는다. 단계별 절대값·차이·비율과 전체 작업 시간을 함께 보고한다. 짧은 지연이나 단일 표본만으로 일반 성능 우위를 주장하지 않는다.
- 작업공간 생성 직후·첫 ready·반복 변경 후·두 작업 동시 실행 중·A 회수 후·최종 회수 후의 물리 할당을 기록한다. 정상 보존한 결과·Codex의 복구 snapshot·공용 캐시와 회수하지 못한 작업 자원을 구별하고 남은 보존 비용을 숨기지 않는다. RAM 절감이나 import 생략은 가정하지 않는다.
- 측정 완료와 성능 우위를 구별한다. 양쪽이 동일 결과/보존 조건을 만족하고 비교 증거가 완결돼야 PV-02를 통과로 판정한다. Splinter가 느리거나 더 많은 자원을 사용한 구간도 그대로 보고하고 후속 판단에 사용한다. 임의의 속도 SLA나 비용 감소율을 합격 조건으로 만들지 않는다.

### 4. 발견 결함의 수정 한도

현재 수명주기로 위 사례를 수행할 수 없거나 원본·다른 작업·유일한 결과를 훼손할 수 있는 결함은 재현 근거를 확보한 뒤 해당 경계에서 수정한다. 담당 모듈은 Runtime의 상태/결과 조정, Git의 등록/통합, Windows의 snapshot/mount/관측, Unity의 검증 연결이며 Core/Protocol 계약 변경이 필요하면 schema·fake·적합성 시험·현재 계약 문서를 함께 갱신한다.

계측과 실행 결과를 얻기 전에 새 최적화를 선행하지 않는다. 설치 연결/인계는 SW의 AI 절차이고 자동 binding은 구현 제외다. 기존 discard 자격과 새 사용 환경의 필요한 경계는 해당 소유 문서를 따른다. 결함 수정으로 후보가 바뀌면 이전 run을 보존하고 최종 후보에서 영향을 받는 사례와 A/B 보존·회수를 연결한다.

## 산출물과 완료 기준

후속 실행의 원시 증거는 `artifacts/parallel-workflow/<runId>/`에, 검증 보고서는 `docs/verification/parallel-workflow.md`에 작성한다. 검증 보고서는 실제 실행 기록과 최종 선택 범위 판정·실패·입력/표본 제한을 함께 보존한다. run에는 환경·소스/바이너리·에셋 manifest, C/S/Q 방식·프로필·반복/캐시 조건, 실제 Codex 대화별 요청/작업 연결, 명령과 응답, source/loaded-code와 에셋 import/표시 증거, 결과 ref/tree/seal, 단계별 비용, 시작·종료 자원 관측과 미실행 이유를 포함한다. home은 증거 폴더와 별도의 짧은 경로를 사용한다.

완료는 다음을 모두 만족할 때 판정한다.

1. 같은 최종 후보의 PV-01 전체 순서에서 독립 대화 A/B·원본 편집·오류 격리·재개·A 선행 완료 뒤 B 지속·최종 원본 동작을 실제 확인한다.
2. 일반 status의 가벼운 조회와 명시적 현재 소스 검증을 구분하고, 성공한 validation·현재 fingerprint·실제 로드된 코드가 같은 변경을 가리킨다.
3. PV-02는 기본/대표 에셋 프로필에서 실제 Codex worktree의 독립 Editor C, Coordination 단일 Editor Q, Splinter S의 새 작업·반복 작업·완료/회수를 직접 비교하고, 대표 프로필에서는 지정 텍스처/모델 변경도 비교한다. Splinter의 필수 저장 계약과 양쪽 단계별 시간·읽기·저장·메모리·호출/개입 수, 같은 결과를 원시 증거에 연결한다. 비교군·대표 에셋·필수 값이 빠지면 PV-02 전체 Passed로 표시하지 않고 관측한 하위 사례와 unknown/미실행을 나눈다.
4. A/B의 독립 결과를 보존하고 소유 프로세스·메모리·핸들 0, mount·Git 등록·delta·마지막 소유 snapshot 부재를 실제 확인한다. A의 회수 시점에는 B와 공유 의존성을 유지한다. unknown은 0이 아니다.
5. 원본 Editor/MCP·비소유 작업·기존 snapshot과 사용자의 변경을 보존한다. 재시도는 기존 identity/영수증을 사용하고, 실패를 임의 강제 삭제나 새 캡처로 덮지 않는다.
6. 코드가 바뀌면 표준 build와 관련 실환경 회귀를 완료하고, 검증 추적표·문서 지도·관련 현재 계약·UTF-8을 동기화한다. 문서만 바뀌면 문서 검증으로 구분한다.

PV-01과 PV-02는 독립 판정한다. 한쪽 통과로 다른 쪽의 누락을 해소하지 않는다. 다음 개선 제안은 측정된 병목·운영 개입과 그 근거를 제시하며, 이번 작업을 끝없이 최적화하는 단계로 바꾸지 않는다.

## 이번에 포함하지 않는 범위

- SW의 설치/인계/도구 선택 절차와 기존 discard의 새 실행 자격. 자동 binding/작성자 인수는 프로그램 구현 제외다.
- NR 전체의 준비/회수 중단 행렬, 실제 재부팅·강제 전원 종료·물리 공간 고갈·offline, ACL/특수 파일/긴 경로 지원 확대와 ARM A/B.
- 이번 대표 모델·텍스처의 임포트/제한된 변경을 넘는 범용 Unity 에셋 저작·패키지·Play·EditMode/PlayMode 테스트, dirty/LFS/linked-worktree 입력 상속과 사전 통합 후보 검증.
- 선택적 Runner·DAG·daemon/SQLite 전환, 측정 전 성능 최적화, 유료 에셋 구매·사용자 실사용 프로젝트 전체의 무제한 투입, 공개 릴리스·원격 push.

일반 지원을 선언하려는 입력/환경의 NR 경계만 선택한다. PV의 미실행/실패를 보존하며 전체 SW/NR을 다음 필수 개발 목록으로 열지 않는다. 후속은 [축소한 구현 계획](implementation-plan.md#남길-후속-작업과-종료-기준)의 책임 분류·실제 비용·종료 기준에 따른다.
