# 저장공간 우선 설계와 Editor 실행 방식

대형 Unity 프로젝트의 작업공간 용량과 생성·편집 준비·재개 시간을 함께 판단하고 Editor 동시성은 그 뒤에 둔다. 제품은 작업별 독립 Editor와 명시적 `validation` 단일 Editor 검증을 제공한다. 기본 모드 자동 선택은 미확정이다.

첫 한정 실사용 버전 [M0](implementation-plan.md)는 **폐기·최소 AI 연결·대표 사용 흐름과 비용 판단**을 마감했다. [대표 사용](../verification/representative-agent-workflow.md)은 작은 warm 입력의 지속 독립 작업에 한정 채택하며, 준비/메모리 비용과 총 물리 공간·대형 효율 미확인을 유지한다. Library/validation의 기존 구현과 명시적 실행 경로를 사용한다. SF 전체 대형 지원·일반 임대 인수·큐/Coordination·source 반환 병합·자동 모드 선택은 별도 후속이다.

## 후속 개발 우선순위

| 순위 | 목표 | 판단할 내용 |
| --- | --- | --- |
| 1 | 물리 공간 절감 | 원본 전체 복제 없이 작업 수와 누적 편집에 따른 총 추가 공간·회수 후 잔여량을 줄임. 파일 할당·Library·Git/임시 결과와 공유 VSS를 아래 공통 정책에 따라 구분 |
| 2 | 메모리 절감 | provider·Editor·검증 cache의 메모리와 동시 작업 수에 따른 부담을 줄임. 프로세스 working set/private bytes와 시스템 전체 사용량을 구분하며 단순 합을 물리 메모리 절감으로 읽지 않음 |
| 3 | 안정적인 병렬 작업 | 작업별 기준·변경분·Editor를 격리하고 같은 작업 재개·명시적 보존/반영/폐기·타 작업 지속을 유지 |

원본·타 작업·승인 쓰기와 결과 보존은 모든 절감 판단의 필수 조건이다. 기존 SF/NR/SW 범위를 유지하며 위 순서로 실제 사용 입력의 필요한 경계만 선택한다.

새 Editor 준비가 **몇 분 이내**인 것은 허용 비용이며 높은 우선순위의 최적화 대상이 아니다. 에이전트는 준비 중 코딩 계획·소스 분석을 진행할 수 있다. workspace 사용 가능 상태와 Editor ready·현재 검증 성공은 기존 판정으로 확인하며 새로운 Runner/스케줄러를 추가하지 않는다. **수십 분 이상**의 지연, 반복되는 정체 또는 진행 불능이 실제 작업을 막을 때 원인을 확인하고 해당 병목만 개선한다. 준비 시간 계측은 유지하되 몇 분의 단축을 위해 물리 공간·메모리·안정성 작업보다 먼저 새 비교나 반복 벤치마크를 열지 않는다.

## 별도 지원 확대의 남은 범위

| 단위 | 현재 상태 | 남은 완료 조건 |
| --- | --- | --- |
| SF-01 변경 기반 소스 검증 | 정의한 소스 검증 범위 Passed | 일반 파일/입력 지원은 NR 검증과 별도 |
| SF-02 journal·저장량 | 작은 입력의 Library 수명주기 LB 마감. 전체 Partial | 대형 입력의 용량 목표와 정리 중 전체 순간 물리 peak |
| SF-03 단일 Library 검증 | 제품 CLI/provider·cold/기존 Library·A/B/A/정리 후 재실행의 WF/LB 범위 마감. 전체 Partial | 일반 임대 복구·source 반환 병합·큐/Coordination 계약과 모드 채택 판단 |
| SF-04 작업 재개·독립 결과 | NotRun | 후보 검증 후 계속 편집, VSS 소실·새 CLI·외부 원본 변경에서도 결과 보존 |
| SF-05 대형 입력 적합성 | NotRun | 파일 수·에셋·LFS/filter·패키지 조건과 NR 장애 관문 |
| SF-06 기본 방식 선택 | NotRun | 같은 입력의 신규/재개/누적 편집/정리 비용 비교 후 채택 여부 결정 |

개발용 후보와 현재 계약은 [validation view](../validation-view.md)를 따른다. 완료된 작은 시험과 제한은 [검증 안내](../verification/README.md)에서 확인하며 새 대형 PV를 자동 시작하지 않는다.

## 다음 판단에 필요한 근거

- [BQ 저장량](../verification/journal-budget-and-growth-qualification.md): 작은 fixture 유효 payload의 약 99.5%가 Library다. 대체 기록 정리만으로 용량 목표를 달성할 수 없다.
- [정리 크기 회귀](../verification/journal-compaction-size.md): 작은 덮어쓰기의 범위 메타데이터가 공간을 늘리는 조건을 보존한다. 더 작은 기존 frame을 유지해 증가를 막는 범위이며 대형 Library 전체 절감으로 환산하지 않는다.
- [동일 private 쓰기](../verification/duplicate-private-writes.md): 이미 저장한 같은 범위/바이트의 반복 저장 억제와 개별 flush를 대조한다. 합성 반복 입력의 실제 저널 할당 감소이며 실제 Library의 동일 쓰기 비중·전체 절감은 미측정이다.
- [VC](../verification/immutable-validation-view.md): 후보 게시 약 1.3초와 반복 replay 21~29초·Editor ready 75~84초·정상 stop 31~33초를 구분한다. 게시 시간만으로 작업 준비 시간을 대표하지 않는다.
- [JW](../verification/live-journal-write-cost.md): 첫 import commit 내부 848.16초 중 flush 828.38초를 관측했다. Windows callback의 선행 직렬화 때문에 Runtime 단독 group flush는 미선정이다. callback 원자성·대기/완료 경계·acknowledged write 내구성을 별도로 검토해야 한다.
- [WI](../verification/cumulative-write-range-index.md), [RI](../verification/replay-index-cost.md), [RU](../verification/unity-replay-comparison.md): 같은 닫힌 저널·합성·누적 Editor 재개를 구별한다. 합성 개선이나 warm cache 소수 표본을 전체 Editor 개선·Library 물리 절감으로 환산하지 않는다.
- [VSS 보호 근거](../verification/storage-recovery.md): 정확한 ID 삭제만으로 타 snapshot 보존이 보장되지 않는다. 현재 격리 guard와 명시적 지연 회수 계약을 유지한다.

## SF-03 제품 연결과 남은 관문

제품 `validation` CLI와 실제 Windows/Git/Unity provider, 서로 다른 VSS의 A/B/A와 같은 경로/캐시, worker 게시의 쓰기 차단, 독립 Git 결과, 제품 snapshot 회수 의존성 검사는 [WF 실행](../verification/product-validation-workflow.md)에서 선택 범위 완료로 판정했다. 기존 첫 cold import 중단·복구 기록은 유지한다. 최종 후보의 두 빈 cache 반복·원시 응답 schema·전체 비용과 보존/회수는 [WF-06~08](../verification/product-validation-cold-cost.md)의 별도 근거로 판정한다.


공통 수명주기와 실제 provider·CLI는 [검증 후보 수명주기](../validation-workflow.md)를 따른다. 연결 자격은 [WF 추적표](validation-matrix.md#sf-03-제품-workflow-연결)에서 아래 범위를 판정한다.

- 등록된 Git 포인터의 분리·worker 쓰기 차단·같은 owner/request 게시 복구를 실제 VSS에서 확인한다. 기존 `ExportCandidate`의 `.git` 거부와 사용자 파일 보호는 유지한다.
- 독립 A/B snapshot에 의존하는 candidate/source 세대와 view 소유 cache를 구분한다. cache는 최초 후보의 Library snapshot을 유지한다. 동일 프로젝트·compatibility·partition·예산·owner일 때 다른 snapshot 후보로 전환하며 최초 전체 복제·읽기 hydration을 사용하지 않는다.
- 독립 Git 결과의 객체 존재와 제품 snapshot 의존성 차단을 확인한다. 실제 CLI 중단과 응답 유실을 시험하며 fake나 예외 주입의 성공을 실제 프로세스 결과로 승계하지 않는다.
- 최종 후보의 작은 실제 A/B/A에서 정상 stop·mount 부재·같은 경로 재시작, 실제 코드/에셋·결과 독립성·원본/타 작업 보존·소유 자원 회수를 확인한다. 전체 추가 파일 할당과 최고치, replay·준비·재개 시간을 구분한다.

SF-03 전체는 Partial이다. FIFO/Coordination, 일반 임대 인수, 생성 source 반환 병합, 원본 Editor 공유, 기본 모드 선택, group flush, 대형 PV 재개, NR 전체 시험과 설치·자동 binding은 별도 후속이다. discard와 최소 AI 연결을 포함한 한정 M0 마감은 SF 전체 통과를 뜻하지 않는다. 제한 권한 서비스의 등록된 NTFS/Git 실제 반복 검증은 [UA 범위](../verification/storage-privilege-service.md), Unity 연결은 아래 보존한 WF/LB 범위에서 확인했다. 현재 계약의 같은 요청 중단·응답 유실·재시작 복구는 계속 유지한다.

## 다음 범위 판단

[작은 Unity warm 비교](../verification/unity-duplicate-private-writes.md)는 같은 등록 입력의 두 편집·같은 ensure·정상 Editor 재시작·독립 결과 보존/release와 고정 Runtime 전후 파일 할당을 소유한다. 이 한 쌍을 마감했으며 종료 직전 저널의 관측 차이가 작아 동일 private 쓰기만으로 Library 용량 목표를 달성했다고 판정하지 않는다. 유효 private 데이터의 대부분이 Library라 후속 공간 작업은 남은 새/변경 Library 기록에서 실제 개선 대상 하나를 고른다. 같은 비교의 반복·대형/cold/원격·새 장애 행렬·설치/권한/볼륨 변경·원본 통합을 자동 추가하지 않는다. 합성 96.1%·시간 SLA·메모리 절감을 실제 결과로 승계하지 않는다.

Library의 마감 뒤 확정한 [NR-01/02 준비 단계 중단 복구](ntfs-recovery-and-support.md#확정-범위-nr-0102-준비-단계-중단-복구)는 [현재 후보의 실제 경계 26개](../verification/preparation-recovery.md)로 제한을 유지해 마감했다. 동일 요청 재개·독립 결과·원본/타 작업 보존을 확인했으며, 게시 실패 원인 미확정과 대형 용량 목표·모드 채택의 SF 관문은 유지한다.

[WF-06~08](../verification/product-validation-cold-cost.md)은 최종 후보의 두 빈 cache A/B/A·소유 파일 할당/시간·독립 결과/회수 근거를 제공한다. 렌더 준비에서 발생하는 설정 저장을 마친 뒤 같은 세대를 다시 시작해 검증하므로 이 추가 Editor 시작/종료도 반복 비용에 포함한다. 첫 두 시도의 실패와 관측 사이 순간 물리 최고치·공유 VSS 귀속의 한계를 유지한다.

작은 입력의 Library 유효량·정리·재증가와 신규/재개 비용은 [WF/LB 실제 실행](../verification/product-validation-unelevated.md)에서 확인했다. 명시적 discard·최소 AI 연결과 M0-08의 고정 입력 비용 판단은 [한정 납품](../verification/m0-delivery.md)으로 마감했다. 후속 비용 작업은 실제 사용할 입력과 실행 경로를 먼저 정하고, 관측한 실용성 결함/병목만 대상으로 삼는다. [IPC 검증](../verification/control-file-read-races.md)의 과거 t2 정확한 발생 지점과 새 실행의 일시적 heartbeat 게시 오류 원인은 미확정으로 유지한다. 대형 공간 목표·새 backend/flush 정책·전체 NR·설치/자동 binding·대형 PV 재개는 후속이며 작은 fixture 통과로 완료하지 않는다.

## 보존한 한정 범위: 일반 권한 Unity 검증 흐름

아래 조건은 WF/LB의 기존 마감과 재현 경계를 설명한다. 다음 작업 지시가 아니며 코드 변경이 해당 경계에 영향을 주거나 실제 결함이 재발할 때 필요한 하위 조건만 선택해 다시 실행한다.

실행 단위는 **제한 권한 서비스와 IPC 수정을 포함한 제품에서 작은 Unity A/B/A의 준비·검증·재개·독립 결과 보존·회수를 일반 사용자 권한으로 완주하고 Library 저장 수명주기를 구현·실측하는 것**이다. `development @ 183118b`에 적용한 고정 후보로 아래 다섯 Library 기준과 WF-09~12를 실행했고 [검증 근거](../verification/product-validation-unelevated.md)에 따라 scoped-complete-with-limitations로 마감했다. 아래는 이 마감의 범위와 재검증 기준이다.

### 이번 단계의 Library 완료 기준

1. 첫 검증 view가 고정한 불변 snapshot의 기존 Library를 직접 읽고 작업별 Library 전체 복제·읽기 hydration·전체 파일 copy-up을 하지 않는다. 원본 Library가 없을 때의 실제 cold import는 별도 비용으로 기록한다. 후보 A/B가 달라도 cache 기준 snapshot을 바꾸지 않는다.
2. 고정 경로의 Library 하나를 세대와 새 CLI에 걸쳐 재사용한다. 기준 Library의 원본 변경과 작업 간 쓰기를 격리하며, 정확한 snapshot/read proof와 수명 의존성을 보존한다. cache가 남아 있으면 그 기준 snapshot의 회수를 차단한다.
3. 반복 쓰기의 현재 유효 범위와 대체 기록을 구분하고, Editor/provider/mount가 없는 경계에서 예산·중단 복구·내용 동등성을 검증하는 cache 정리를 제공한다. 정리 전후와 재개 후 증가량·겹침 최고치를 측정하며, 오래된 기록만 줄이고 필요한 Library 데이터를 임의 삭제하지 않는다.
4. 같은 고정 입력의 cold와 기존 Library 재사용, A/B/A 및 재개/반복 편집을 실제 Unity로 확인한다. 총 추가 할당·Library·VSS 공유량·Git/임시/결과와 준비/재개/정리 시간을 공개하고, Library 존재가 단지 새 경로의 전체 재생성으로 바뀌지 않았는지 확인한다. 작은 합성 범위 시험만으로 완료하지 않는다.
5. source/후보 결과를 독립 보존한 뒤 cache와 기준 snapshot을 정확한 순서로 회수한다. 새 계약·schema·fake·복구/손상 거부 시험·capability·에이전트 절차와 지원표를 함께 갱신한다. 위 기준이 미충족이면 Library 완료로 표시하지 않는다.

### 선택 근거

- WF-06~08은 작은 두 빈 cache의 비용과 결과를 확인했지만 IPC 수정·권한 서비스 도입 전 후보의 증거다. 최신 제품의 실행 성공으로 승계할 수 없다.
- IPC-01~03은 실제 Windows 파일 경합과 합성 client의 보존을 확인했다. 새 후보의 실제 `prepare-worker`와 Unity 연결은 남아 있다.
- UA-01~04는 일반 CLI의 NTFS/Git A/B와 추가 UAC 0을 확인했다. Unity는 실행하지 않았고 worker 결과도 `validationClaimed=false`로 보존했다.
- 이 변경들이 함께 동작하는 근거와 Library 개선 전후 근거를 함께 만든다. 기존 작은 입력의 동일 조건 비교를 우선하고, 이미 마감된 대형 PV를 자동 재개하지 않는다.

### 입력과 자원 경계

- Windows x64·NTFS·VSS·WinFsp와 기존 작은 `splinter-unity` fixture의 Unity `6000.3.22f1`을 사용한다. 실제 사용 시 형제 저장소의 AGENTS.md를 읽고 기준 revision·입력 hash를 고정한다. 기존 모델의 3개 mesh와 텍스처/코드 차이를 재사용하며 유료 에셋·대표 대형 프로젝트·새 ARM Editor를 추가하지 않는다.
- cold와 기존 Library trial을 순차 실행한다. 각 trial은 서로 다른 key와 VSS를 가진 worker A/B 두 개, 고정 view/cache 하나, A→B→A 세 source generation을 사용한다. 기존 Library trial에는 정리 뒤 새 A 세대를 추가해 실제 재증가를 확인한다. 원본 Library 준비 비용을 따로 기록하며 cold/warm 소스 내용·Unity 버전은 같게 고정한다. 각 generation의 준비/최종 검증에 필요한 정상 stop/restart와 재시도도 비용에 포함한다. 동시 Unity Editor는 검증 view의 하나이며 Codex 별도 worktree·Coordination·새 조정 에이전트를 추가하지 않는다.
- UA에서 기록한 기존 project/home grant는 재사용 후보일 뿐 현재 사용 가능하다고 가정하지 않는다. 실행 전에 읽기 전용으로 설치 패키지/현재 바이너리·프로토콜·grant identity·서비스 상태·Editor 점유·원본/비소유 snapshot과 회수 guard를 확인한다. 문서만 바뀐 경우 불필요한 서비스 업데이트를 하지 않는다.
- 정확한 project/home과 소스 볼륨, 여유 공간·저널 예산·계측 위치를 실행 manifest에 확정한다. 기존 C: snapshot이나 UA의 T: fixture·독립 결과를 초기화하지 않는다. 승인된 환경으로 완전 회수가 불가능하면 환경 차단을 기록하고 새 자원 생성을 시작하지 않는다. 새 VHD·포맷·VSS 예산·grant/서비스 변경이 필요하면 구체적인 준비안을 별도 작업으로 분리한다.
- 원본 위치·파일시스템·계속 편집 가능한 조건을 유지한다. 최초 전체 복제·읽기 hydration·전체 파일 copy-up으로 준비를 우회하지 않는다. 원본과 타 작업의 기존 변경·결과 refs를 사전 기록하고 보호한다.

### 실행 단위와 완료 기준

| ID | 포함하는 작업 | 완료에 필요한 증거 |
| --- | --- | --- |
| WF-09 | 현재 후보와 설치 서비스의 일반 권한 연결 | 최종 source/CLI/provider/bridge 및 설치 서비스 package hash·환경 manifest, 실제 A/B 생성·게시·검증·회수에서 호출 CLI/Git/Unity의 비승격 증거와 정상 반복 추가 UAC 0. 서비스의 SYSTEM 권한과 최초 관리자 구성을 별도 기록 |
| WF-10 | 빈 cache 하나의 실제 A/B/A와 세대별 Unity 검증 | 같은 view 경로/cache, 정상 stop·실제 mount 부재 뒤 전환, A/B의 기대 코드·모델·텍스처와 fingerprint/domain/MVID·요청/sequence/seal 일치. A 전용 파일·이전 generation 출력의 유입 없음, 실행 중 원시 입력/응답 schema 및 Accepted 후속 관측 |
| WF-11 | 정상 unmount 경계와 같은 작업 재개 | 실제 `prepare-worker` 정상 unmount 직후 관측과 동일 owner/request 재호출, 새 CLI에서 같은 key/ID/snapshot/delta 재연결. 게시 뒤 A의 후속 편집은 원래 후보·B를 바꾸지 않고 후보 검증 성공도 승계하지 않음. 같은 snapshot의 reader가 있으면 정상 stop/unmount 뒤 재시도 |
| WF-12 | 독립 결과·원본/타 작업 보존·회수와 전체 비용 | candidate·source generation·worker 후속 편집을 정확한 seal의 독립 Git 결과로 보존. 의존 artifact→view/cache→worker 순으로 회수, 필요한 같은 workspace의 명시적 release 재시도. snapshot 없이 결과 객체/내용/hash 재확인, 소유 자원 0/부재·원본/타 작업/비소유 snapshot 보존 및 전체 할당/시간/실패 비용 |

미리보기는 기존 [fixture 계약](../unity-render-qualification.md)에 따라 요청당 최대 두 번 중 첫 유효 PNG를 사용하고 경로·hash·pass별 픽셀을 남긴다. 준비 Editor가 설정을 저장하면 정상 stop 뒤 같은 세대를 재시작해 최종 검증한다. 일반 설정 파일을 검증에서 제외하거나 source sequence 검사를 완화하지 않는다.

WF-11은 정상 경계·동일 요청의 재호출·새 CLI 재연결에 한정한다. 일시적 경합을 이번 실행에서 관측하지 못하면 재현하지 못했다고 기록한다. 과거 t2의 정확한 원인 해결, 강제 종료·응답 유실의 모든 경계, 재부팅 복구까지 검증했다고 하지 않는다. 손상·접근 거부·다른 owner를 재시도로 숨기지 않는 자동 회귀는 계속 유지한다.

### 수정 허용 범위와 산출물

Library 완료 기준의 구현과 이를 막는 재현 가능한 결함을 기존 Runtime·CLI/Windows workflow·Unity 어댑터/브리지·IPC·권한 서비스 연결 및 해당 시험에서 수정한다. 원인 확인 없이 timeout·권한·보존 조건을 완화하지 않는다. 공개 계약이 바뀌면 schema·fake·적합성·현재 기능 문서를 함께 갱신한다. 새 backend·flush 정책·범용 임대/큐 설계를 바꿔야 하면 별도 범위를 먼저 정한다.

원시 증거는 `artifacts/storage-foundation/library-20260929/`에 보존한다. 요청·응답 원문, command/운영 개입, 준비/재시작/검증/재개/보존/회수 시간, PNG·TRX, 원본/작업/독립 결과 hash와 자원 관측을 연결한다. 기존 UA/WF/IPC artifacts를 덮어쓰거나 삭제하지 않는다. 해석과 제한은 `docs/verification/product-validation-unelevated.md`가 소유하며 추적표에서 연결한다.

저장량은 worker·후보·source·Library/cache·Git/독립 결과·임시/증거 파일의 추가 파일 할당과 관측 최고치를 구분한다. 초기 준비·A/B/A·A 후속 편집·회수 후를 비교하고 Library 재증가를 기록한다. 공유 VSS는 볼륨 관측으로 분리하며 workspace 귀속은 unknown이다. 파일 할당 표본을 순간 물리 peak나 공간 절감 목표 달성으로 표현하지 않는다. 이번에는 성능 우위나 기본 모드를 선정하지 않는다.

최종 실행 후보에 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드·자동 시험과 문서 UTF-8·diff 검사를 수행한다. 실행 파일이 바뀌면 이전 trial의 통과를 승계하지 않고 최종 후보로 필요한 전체 흐름을 다시 확인한다. 실패·진단 시도도 원시 기록과 전체 비용에 포함한다.

### 마감과 제외 범위

WF-09~12의 모든 증거가 있을 때 이 작은 실행 단위만 `scoped-complete-with-limitations`로 마감한다. `reclaim-pending`, 관측 unknown, 환경 차단이나 미보존 결과가 남으면 완료로 처리하지 않고 남은 정확한 조건을 기록한다. 비소유 snapshot 삭제·자동 sweep·강제 프로세스 종료로 완료 조건을 맞추지 않는다.

SF-02/03과 SF-04~06 및 SW-01~10의 전체 자격은 각 소유 관문에 따라 판정한다. Library 완료는 위 다섯 조건의 실제 근거로 별도 판정하고 일반 cold 안정성·SF-04 전체·대형 프로젝트 전체 지원으로 확대하지 않는다. C/S/Q·대형 PV 재개, 기본 모드 자동 선택, source 반환 병합·FIFO/Coordination·일반 임대 인수, 제품 설치·대화 binding·discard, 실제 전원/재부팅/공간 고갈·ARM A/B, 일반 에셋 저작/Play/Unity 테스트 확대와 원격 게시·릴리스는 제외한다.

이번 Library 구현·검증의 마감은 위 다섯 기준에 한정한다. 새 입력이나 후보에서 보존 결함이 드러나면 해당 경계를 함께 수정·검증한다. 별도 NR 전체 장애나 공개 운영 범위를 자동으로 실행하지 않는다.

## 공통 저장소와 Editor 정책

작업자는 Editor 없이 독립 소스를 편집하고 불변 후보를 게시할 수 있어야 한다. 검증 view는 같은 경로의 단일 Library를 사용하되 정상 Editor stop·mount 부재 뒤 재시작하며 replay·재시작·큐 대기를 반복 비용에 포함한다. 사용자 원본 Editor를 별도로 열면 총 Editor 수는 둘이다. 원본 Editor 자체를 공유하려면 짧은 임대 동안 미저장 상태·후보 적용·원복을 보호하는 별도 계약이 필요하다. 작업 기간 전체의 원본 쓰기를 막지 않는다.

전체 논리 파일 트리와 GUID/.meta·패키지 의존성을 유지하며 범위 읽기와 실제 변경만 저장한다. 현재 씬이 참조하는 파일만 선택하는 방식이나 파일 전체 복사로 대체하지 않는다. 도구가 전체 파일을 다시 쓰면 그 쓰기는 누적되므로 정리·독립 Git 결과의 materialization 비용까지 포함한다.

첫 비교 지표는 **원본을 제외한 총 추가 물리 공간과 최고치**다. worker delta, 검증 view/Library, Git·LFS·독립 결과, 임시 파일, 공유 VSS를 분리한다. VSS는 볼륨 공유 관측이며 workspace 비용으로 귀속하거나 중복 합산하지 않는다. 논리 길이·누적 관리 할당·working set은 물리 공간과 다르고 unknown을 0으로 바꾸지 않는다.

원본·결과 보존과 입력 적합성을 전제로, 위 우선순위인 물리 공간 → 메모리 → 안정적인 병렬 작업을 따른다. 독립 Editor는 병렬 실행 이익이 추가 비용을 감당하는 경우의 선택지로 평가하며 몇 분 이내 준비 시간 자체를 채택 실패나 최우선 병목으로 보지 않는다.
