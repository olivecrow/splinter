# 원자적 게시를 유지하는 Bee 저장 설계

[English](../../plans/bee-native-storage-design.md)

커밋한 구조 시제품(`a8eb778`) 이후의 설계다. 당시 실측과 실패는 [SX 기록](../verification/structural-throughput-prototypes.md)이 소유한다. 제한 로그의 제품 적용은 별도 커밋 `d0ac0a2`다. 이후 사용자가 이 설계의 구현과 실제 성능 측정을 승인했다. 첫 구현은 가상 볼륨 하나에서 native backing을 사용하는 개발 전용 후보이며, 제품 기본값 적용과 전체 Unity Editor 벤치마크는 별도 자격 경계다.

## 권고

WinFsp 프로젝트 볼륨 하나를 유지하고 **새로 생성되는 Bee 파일의 내부 native backing**을 검토한다. backing을 junction으로 노출하지 않는다. 논리 namespace, 파일 identity, 소스 변경 추적과 자원 소유권은 provider가 관리한다. Bee의 buffered write는 별도로 선언한 재생성 데이터 내구성 정책이며 소스 쓰기 ACK 정책을 바꾸지 않는다.

첫 단일 볼륨 후보를 구현하고 [BN-01–05](../verification/bee-internal-native.md)에서 측정했다. 작은 namespace/소스/매핑/복구 검증은 통과했지만 성능 기준은 실패했다. ABBA 평균 단계 합계는 11.18초에서 18.88초로, 최종 할당은 0.92MiB에서 2.13MiB로 늘었다. 개발 전용으로 유지하며 제품 채택 후보로 선정하지 않는다. 물리 NTFS root와 소스 overlay 대안은 미구현이며 아래의 더 큰 소스 경계 재설계가 필요하다.

## 근거와 비교

기존 junction은 graph 생성에는 성공하지만 실제 Bee가 `TundraBuildState.state`와 `tundra.digestcache`를 게시하지 못한다. 상대·절대 view 경로의 정확한 `MoveFileExW`는 `ERROR_NOT_SAME_DEVICE`(17)로 실패하며, 직접 native 경로는 같은 파일 ID로 성공한다. 복사를 허용한 성공은 ID가 바뀌므로 원자적 rename의 근거가 아니다. 원인이 되는 정확한 WinFsp 내부 분기는 격리하지 못했다. 사용자 reparse resolver의 부재나 host 옵션이 해결책이라고 확인한 것도 아니다.

| 경계 | 가상 볼륨 하나와 provider 내부 native backing | 물리 NTFS 프로젝트 root와 소스 overlay |
| --- | --- | --- |
| Bee rename·교체 | 같은 가상 namespace에서 전달받아 원자적으로 이름을 갱신하고 교체된 열린 identity를 유지해야 함 | Bee가 NTFS에 남으며 직접 경로 진단에서 확인한 native rename이 유력한 장점 |
| 소스·root 파일 | 기존 가상 namespace를 유지하되 backing 변경이 소스 세대·결과 guard를 우회하면 안 됨 | root 파일, 새 소스 디렉터리, 임시 파일의 소스 교체에 새로운 경로·보호 모델 필요 |
| 기존 Library 재사용 | 불변 읽기와 범위 변경을 유지하며 상속 Bee 파일을 native 전체 파일로 만들면 안 됨 | Bee를 비어 있는 private 경로로 시작할 수 있지만 나머지 Library·소스 공유와 경계 간 동작은 여전히 필요 |
| I/O 비용 | 선택한 payload의 압축·저널 재작성·쓰기별 durable flush를 줄임. WinFsp callback·버퍼 복사·namespace·가상 cache 비용은 남음 | Bee callback을 없앨 수 있음. 소스 callback과 디렉터리 junction 경계 비용은 남음 |
| mapping·종료 | 가상 파일 context가 mapping·image를 소유하며 native backing 수명도 이를 따라야 함 | Bee mapping은 native이나 소스 overlay와 native backing 각각 소유권·종료 증명이 필요 |
| 구현 범위 | 새 backing 정책과 일관된 namespace·flush·복구·계수·회수 통합 | workspace 배치, 임의 소스·root 편집, Git/Plastic 경계, identity·결과·수명 재설계 |

graph-only 67.0% 감소는 이전 junction의 작은 graph 작업 근거다. 두 대안의 컴파일·Editor 시간을 예측하지 않는다. 전체 native 출력은 압축 범위보다 저장량이 클 수 있으며 SX graph 할당도 증가했다. 시간과 공간을 함께 측정해야 한다.

## 설계 A: 가상 namespace 하나

### Namespace와 메타데이터

`WinFspOverlayFileSystem`은 현재 일반 파일 연산을 `IWorkspaceDeltaStore`에 위임하고 안정된 논리 `DeltaEntry.Id`를 보고한다. 별도의 경로 기반 쓰기 우회를 추가하지 않고 이 경계를 유지한다. 클라이언트에 `Library/Bee` reparse point를 노출하지 않는다. 내부 구현은 논리 파일 identity별 opaque native payload를 보관하고, namespace 소유자 하나가 경로를 identity에 연결할 수 있다. rename은 논리 namespace 변경 하나를 게시하며 논리 이름이 바뀌었다는 이유만으로 payload를 복사하거나 물리적으로 옮길 필요가 없다.

교체에서는 목적지 이름이 원자적으로 바뀌고 source identity가 유지되며, 이전 목적지의 열린 핸들은 마지막 close까지 이전 내용을 읽을 수 있어야 한다. 디렉터리 이동, 대소문자 변경, 공유·삭제 의미, EOF·allocation size, append·constrained write, 속성·시각도 backing 종류와 관계없이 같아야 한다. 중단 복구 절차 없이 저널 namespace와 별도의 native 경로 트리를 함께 갱신하면 권위가 둘이 된다.

기존 [소스/cache 전달 구현](../../../src/Splinter.Runtime/SourceViewDeltaStore.cs)은 계약 참고 사례이며 바로 쓸 수 있는 native 구현은 아니다. identity를 보존하고 중단된 private 파일 전달을 복구한다. 시험에는 `Temp`의 sparse private 데이터를 `Assets`로 옮기고 열린 identity로 목적지를 교체하는 경우가 있다. Bee→source 이동을 일괄 거부하면 일반 저널 namespace 계약이 좁아진다.

### 복사 금지 유지

native backing은 비어 있는 상태로 시작한다. 신규 파일에는 애플리케이션이 쓴 바이트만 들어가며 상속 파일을 읽었다는 이유로 native payload를 만들지 않는다. 기존 Bee 파일 일부를 수정하면 불변 기준과 private 변경 범위를 유지해야 한다. 파일 전체 copy-up, 읽기 hydration, 원본 Library 복사는 허용하지 않는다.

보수적인 첫 설계는 상속 파일을 범위 방식으로 유지하고 신규 파일만 native backing을 사용할 수 있게 한다. 새 임시 출력은 기존 바이트 복사 없이 namespace 게시로 상속 이름을 교체할 수 있다. 0으로 truncate한 파일의 backing 전환은 identity·열린 핸들·중단 의미를 증명한 후 별도로 고려할 수 있으며 확장자로 추정하지 않는다. 명시적으로 Bee cache를 비워 시작하는 것은 재빌드 비용을 가진 다른 앱 정책이다. 기존 cache가 없거나 손상됐을 때의 자동 fallback이 아니다.

### 내구성과 생성 데이터·소스 경계

소스와 일반 프로젝트 파일은 durable ACK, dirty 검출과 독립 결과를 유지한다. buffered Bee callback은 명시한 재생성 출력 정책 안에서만 일반 NTFS buffering 수준으로 응답할 수 있다. `Flush`는 관련 backing 파일 모두에 도달해야 하고 실패 뒤 성공을 게시하면 안 된다. rename의 원자적 가시성과 crash·정전 내구성은 별개다.

Bee에서 만들어 소스로 옮기는 파일은 성공 게시 전에 소스 세대에 포함되고 소스 내구성을 갖춰야 한다. 기존 열린 핸들의 후속 쓰기도 새 분류를 따라야 한다. 소스를 Bee로 옮겼다고 이미 ACK한 소스 바이트를 조용히 잃어서는 안 된다. 안전한 전환을 구현하기 전의 제한 개발 시제품은 이 이동을 명시적으로 거부할 수 있지만 제품 파일시스템의 투명한 대체품으로 자격을 얻을 수 없다.

선택한 정책, workspace·basis identity, 소유 backing root와 native 파일 identity를 현재 버전 상태에 기록한다. 알 수 없는 파일·소유 불일치·중단 상태는 보존·거부하고 정의한 경계에서 같은 요청으로 복구한다. 생성 데이터 정책은 애플리케이션 어댑터 계약에 둔다. Core에 Unity 경로를 넣거나 모든 앱의 쓰기 정책을 바꾸지 않는다.

### 핸들·mapping·소유권

`FlushAndPurgeOnCleanup`과 핸들 cleanup·최종 file-context close의 구별을 유지한다. 열린 핸들 수 0만으로 data mapping·실행 image context가 비었다고 판단할 수 없다. native payload 핸들은 논리 identity에 속하며 읽기·쓰기·삭제/교체된 identity·잔존 가상 context가 필요로 하는 동안 유효해야 한다. backing dispose와 unlink 회수는 이 연산들을 drain해야 한다.

backing 트리의 직접 경로를 일반 클라이언트 경로로 쓰지 않는다. task 소유 root·조상을 고정하고 leaf identity·최종 경로를 검증하며, 예기치 않은 reparse·hardlink·미지원 항목을 거부하고 A/B·원본끼리 writable native 파일을 공유하지 않는다. 이는 소유 보호이며 OS sandbox가 아니다. 임의 외부 data mapping 부재를 증명하지도 않는다. 가상 cache page가 있는 상태에서 native buffering이 NTFS cache page를 추가할 수 있으므로 RAM 감소를 자동으로 주장할 수 없다.

## 제품 연결 지점

| 현재 코드 | 필요한 설계 |
| --- | --- |
| [Core/SnapshotOverlay.cs](../../../src/Splinter.Core/SnapshotOverlay.cs)의 `DeltaEntry`·`IWorkspaceDeltaStore` | 안정된 identity와 CRUD를 유지한다. native 바이트·내구성 관측을 정의하고 소스 저널만의 값을 전체 저장량으로 보고하지 않는다. native API는 Core 밖에 둔다. |
| [WinFspOverlayFileSystem.cs](../../../src/Splinter.Platform.Windows/WinFspOverlayFileSystem.cs)의 `Write`·`Cleanup`·`Close`·`Rename`·목록·`Flush` | 모든 연산을 일관되게 전달하며 교체·unlink identity와 cache flush를 유지한다. `Cleanup`에서 backing을 회수하지 않는다. 전역 mutation gate·버퍼 복사 비용은 별도 변경·검증 전까지 남는다. |
| [ApplicationProjectPolicy.cs](../../../src/Splinter.Core/ApplicationProjectPolicy.cs)·[UnityProjectPolicy.cs](../../../src/Splinter.Application.Unity/UnityProjectPolicy.cs) | 현재 generated descriptor는 `Library`이며 native Bee 정책이 아니다. 설계를 채택할 때만 공통 정책 경계와 Unity가 소유하는 정확한 subtree 선택을 추가한다. |
| [WinFspOverlayServer.cs](../../../src/Splinter.Platform.Windows/WinFspOverlayServer.cs)의 store 조합·소스 query | 현재 `SourceViewDeltaStore` 선택 또는 `JournalDeltaStore` cast가 있다. wrapper에서도 세대·내용 증명, stop·flush 실패 보존과 전체 자원 계수가 필요하다. |
| [NtfsSnapshotWorkspaceBackend.cs](../../../src/Splinter.Platform.Windows/NtfsSnapshotWorkspaceBackend.cs)의 `SealAsync`·`CompactAsync`·`RemoveDeltaAsync` | 현재 seal·계수·회수 inventory는 저널 전용이다. 별도 backing inventory의 소유권·복구를 연결하고 소스 seal만으로 native 파일을 삭제하지 않는다. 전후·unmount 검사를 유지한다. |
| [JournalDeltaCandidates.cs](../../../src/Splinter.Runtime/JournalDeltaCandidates.cs)·[JournalCandidatePublication.cs](../../../src/Splinter.Runtime/JournalCandidatePublication.cs) | 후보 게시는 저널 범위를 읽고 source seal을 검사한다. 소스로 분류한 모든 바이트가 독립 결과가 되어야 한다. native 경로 참조를 남기는 것은 독립 결과가 아니다. |
| [ManagedDirectoryBasis.cs](../../../src/Splinter.Platform.Windows/ManagedDirectoryBasis.cs) | 원본·peer 의존성과 복원 guard를 유지한다. 첫 검토는 task-private Bee로 한정한다. 원본 관리 Library의 표현 변경은 복원 계획도 바꾸므로 별도 범위다. |

## 물리 프로젝트 root를 우선하지 않는 이유

일반 NTFS root에 private Bee를 두고 가상 `Assets`·`Packages`·`ProjectSettings`와 다른 소스 트리로 junction을 연결할 수 있다. Bee의 가상 dispatch를 없앨 가능성은 있지만 세 디렉터리가 전체 editable namespace는 아니다. root 파일, 임의 새 디렉터리, 소스·VCS 관리 파일에도 정확한 private 저장과 보호가 필요하다. 기준본에 writable hardlink를 만들거나 mutable 원본으로 링크하는 것은 허용되지 않는다.

Editor와 코드 도구는 최종 목적지 디렉터리 밖에 임시 파일을 만든 뒤 소스 파일을 교체할 수 있다. 이 작업은 NTFS와 가상 소스 볼륨을 건넌다. 현재 graft 실패는 .NET 복사 fallback 시험이 아닌 직접 원자 rename 검증이 필요함을 보여준다. 역방향 구조의 모든 연산이 실패함을 증명한 것은 아니지만 투명한 호환성을 가정할 근거도 없다. 여러 소스 submount의 같은 볼륨 identity와 subtree 간 이동도 검증해야 한다.

root 파일마다 링크를 만드는 것으로 임의 생성·열거·디렉터리 이동·링크 교체가 해결되지 않는다. 완전한 해결에는 새로운 namespace 가로채기·배치 계약이 필요하다. 최초 전체 복사, ReFS, 외부 도구 패치와 도구가 항상 목적지 옆에 임시 파일을 만든다는 가정은 제외한다. 작은 단일 볼륨안이 유의미한 정확성·비용 기준을 통과하지 못하기 전에는 보류한다.

## 승인한 구현과 작은 검증 순서

고정 실험은 기존 Unity 6000.3.22f1의 assembly 세 개짜리 Bee 입력을 사용한다. Editor를 시작하거나 원본 Unity 프로젝트를 변경하지 않는다. `overlay`는 현재 기본 bounded SJR2 저널이다. `internal-native`는 같은 namespace·저널을 유지하면서 새로 생성되는 Bee 파일에만 빈 opaque NTFS payload를 추가한다. 양쪽 모두 cleanup purge를 유지한다. 과거 junction 모드는 진단 근거만 보존한다.

| ID | 종료 기준 |
| --- | --- |
| BN-01 | 안정된 namespace ID, client reparse point 없음, native 복사 없는 rename/replacement, 이전 대상의 열린 핸들, 기준본 범위 쓰기, 소스 내구성과 원본·peer 보호 |
| BN-02 | 정확한 owner/policy/inventory, 명시적 flush, 동일 owner 복구와 실패 보존; 전원 장애 자격 아님 |
| BN-03 | 실제 mapped file·image 수명, retained context 0으로 정상 종료, 파일명별 정리 우회 없음 |
| BN-04 | 실제 cold/unchanged/incremental Bee 빌드와 상수 `42 → 43`, 현재 bounded 저장과 직렬 ABBA 전체 소형 수명주기 비교, 시간·공간·RAM 경계 분리 |
| BN-05 | 필수 빌드, 구현 검토와 양쪽 문서, 실패 실행을 성공한 시간 표본으로 사용하지 않음 |

생성 파일을 소스로 승격할 때는 소스 내구성을 유지하고 기존 핸들의 후속 쓰기도 소스 저널로 보낸다. native 하위 파일을 포함하는 미지원 디렉터리 경계 이동은 namespace 변경 전에 거부한다. 이 제한과 제품 수명주기 미연결 때문에 이 후보를 투명한 제품 backend로 소개할 수 없다.

1. **계측 전 namespace 증명:** 빈 소유 Bee에서 copy fallback 없는 native `MoveFileExW`로 신규 이름·교체, 상대·절대 프로젝트 경로를 확인한다. 동일 파일 identity, 이전 목적지 핸들, 내용, 대소문자·디렉터리 이동, 실패 보존과 원본·peer sentinel hash를 검사한다. .NET move만으로 충분하지 않다.
2. **파일·mapping 동작:** 0/짧은/multi-chunk·offset 쓰기, hole, truncate, metadata, 동시 읽기·rename·delete, memory-mapped write와 executable-image 수명. `WinFspTransferTests`·`WinFspCleanupConcurrencyTests`의 계약을 유지하고 cache·mapping은 실제 mount로 증명한다. 특정 파일을 다시 여는 우회 없이 정상 provider 종료와 잔존 context 0을 요구한다.
3. **복사 금지·소스 안전:** 기존 sparse·큰 파일 읽기·작은 수정 시 전체 materialization 부재, 신규 파일 교체, 열린 핸들을 가진 소스 경계 전달, backing·basis 제거 뒤 독립 결과, dirty·target 거부, B·원본이 유지되는 A release. `ImmutableCandidateTests`·`CandidateSourceProjectionTests`·`CandidatePublicationTests`의 동작을 재사용한다.
4. **복구·flush:** namespace 게시·flush·소스 승격·회수 경계에서 소유 자식을 중단한다. buffered cache와 durable source의 명시적 기대, 정확한 inventory, 같은 요청 복구를 검사한다. 프로세스 중단 근거이며 정전 근거가 아니다.
5. **실제 작은 Bee:** 앞선 기준을 통과한 후 기존 3 assembly 입력을 재생한다. cold·unchanged·소스 한 번 변경 backend 빌드 성공, 컴파일 상수 `42 → 43`, state·digest-cache 원자 게시와 정상 종료를 요구한다. 그 후 제한 시간 내 직렬 ABBA로 준비·graph·compile·flush·보존·회수를 나눠 측정하고 실패 기록을 유지한다. Editor·ILPP·대형 프로젝트 자격으로 확대하지 않는다.

namespace metadata와 임시 교체 파일을 포함해 native·journal 할당을 나눠 잰다. 논리 바이트·할당 바이트·buffering/RAM 관측·시간 경계를 보고하고 최종 할당을 peak라고 부르지 않는다. 유효한 개선은 graph 구간만이 아니라 현재 작은 backend 전체 수명주기보다 나아야 한다. 이 검토는 수치 개선률을 예측하지 않는다.

## 첫 후보 측정 이후 결정

[BN-01–05](../verification/bee-internal-native.md)는 실측 비용을 근거로 제품 채택을 거부하며, 성공한 원자 게시와 안전성 근거는 유지한다. 다음 구조 후보는 native association을 동일한 namespace mutation/재생 기록에 포함하는 것이다. owner·소스 내구성·복구 증명을 제거하지 않고 두 저장소 사이의 파일별 intent를 줄이는 방향이다. 현재 추가 control 게시 비용은 개별 계측하지 않았고 이 대안은 미구현이므로 속도 개선을 예측하지 않는다. 변경된 native 파일만 flush하면 변경 없는 파일의 반복 처리를 줄일 가능성이 있지만, retained context가 종료된 뒤의 최종 flush를 생략하는 근거는 아니다.
