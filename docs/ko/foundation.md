# 기술 구조 설명서

[English](../foundation.md) · [한국어 README](../../README.ko.md) · [영문 문서 지도](../README.md)

Splinter는 불변 프로젝트 기준본과 작업별 변경 byte range를 결합하여 writable NTFS/WinFsp 프로젝트 경로를 제공합니다. 원본은 계속 편집할 수 있습니다. 최초 전체 복제·읽기 hydration·파일 전체 copy-up은 하지 않습니다. 저장소 격리는 악성 코드에 대한 OS 보안 sandbox가 아닙니다.

## 구성요소

| 프로젝트 | 책임 |
| --- | --- |
| `Splinter.Core` | 저장소·원본·앱·프로세스 port, 결과·소유권·실패 계약. Win32/Unity/Git/Codex 전용 API 제외 |
| `Splinter.Protocol` | 제품/개발 명령 catalog, DTO, JSON Schema |
| `Splinter.Host` | 읽기 전용 발견·진단 |
| `Splinter.Runtime` | 상태·잠금·수명주기·journal·paged metadata·독립 결과·자원 및 복구 정책 |
| `Splinter.SourceControl.Git` | 고정 commit/tree/blob, 소유 ref/fence, Git 결과·명시적 branch 구체화 |
| `Splinter.SourceControl.Files` | 일반 폴더·Plastic 기준본, 독립 파일/범위 결과, 보호된 원본 게시 |
| `Splinter.Application.Unity` | 정확한 프로젝트/버전 Editor 실행·준비 관측·정상 종료·소유 bridge |
| `Splinter.Platform.Windows` | WinFsp·native 경로/identity 보호·프로젝트/Library 기준본·게시·프로세스 소유·복원 |
| `Splinter.Workflows.Windows` | 개발용 불변 view·공유 cache·자원 수명 |
| `Splinter.Cli`, `tools/` | 명령 해석·구성·JSON/exit code와 별도 개발 CLI·재사용 probe |

제품 진입점은 `scripts/splinter.ps1`, 진단은 `scripts/splinter-dev.ps1`입니다. [에이전트 skill](../../skills/splinter/SKILL.md)이 schema·정확한 경로·같은 작업 인계를 소유합니다. 전용 설치기·자동 대화 binding·스케줄러·AI Runner/DAG·daemon/DB는 없습니다.

## 기준본과 상태

작업은 source(`git|plastic|none`), application(`unity|none`), project/key/id/home와 불변 기준 identity를 고정합니다. 증명 실패 뒤 다른 source로 fallback하지 않습니다. Git은 고정 객체, Plastic은 기록된 changeset/revision과 자격화된 client reader, None은 명시적 관리형 프로젝트 기준본을 사용합니다. Plastic 등록·check-in·서버 pin은 하지 않습니다.

관리형 프로젝트/Library 준비는 같은 NTFS 볼륨에서 기존 데이터를 이동하고 원본 경로를 writable view로 바꿉니다. 최초 준비와 최종 복원에는 정확한 원본 Editor의 저장·정상 종료가 필요합니다. 작업 회수는 자기 참조만 해제하며 최종 복원은 모든 dependent/reader 종료를 기다립니다.

현재 task/basis/제품 overlay는 **v4/v3/v3**, 파일 결과 manifest는 **v4**, metadata는 **SPM2**입니다. Unity의 일회성 `Library` 기준본은 SHA-256 체크섬이 포함된 평면 이진 인덱스(`fast-index.bin`), 하드링크 거부 및 동일 핸들 길이/시각 검증, 지연 핸들 풀(마운트당 최대 512개), 미변경 시(인덱스 및 시퀀스 0 확인 후) 같은 볼륨 O(1) NTFS 디렉터리 이동 복원을 사용하여 캐시 재해싱을 생략합니다. 원본 `Library`에 변경이 있으면 비공개 기준 폴더에 해당 항목을 설치하고 검증한 뒤 원래 위치에 공개합니다. VCS 없는 `project-snapshot` 기준본은 전체 내용 해시 검증을 유지합니다. 손상됐거나 지원하지 않는 상태는 보존하고 처리를 중단합니다.

## Metadata와 변경 범위

[대규모 저장 계약](../scalable-storage.md)은 최대 64KiB 이진 페이지·경로 interning·디스크 sort/spool·이력/범위 index·cursor 조회·metadata 회수를 소유합니다. 논리 node hash/count·accepted-source seal은 전체 native identity/hash/page/inventory 증명을 대체하지 않습니다.

제품 overlay v3는 소스 경로(`Assets`, `Packages`, `ProjectSettings` 등)에 SJS1 제어와 raw SJR2 segment를 사용합니다. Git/Plastic 작업공간에서 소스 스냅샷이 추적하지 않는 생성/일회성 경로(Unity `Library`, `Temp`, `Logs`, `UserSettings`, `obj`, `.vs`, `.idea`, `*.csproj`, `*.sln`, `*.slnx`)는 `PartitionedWorkspaceDeltaStore`가 `EphemeralRangeStore`(copy-up 없는 기준 읽기, 동시 읽기가 없을 때 기존 범위를 제자리 덮어쓰는 `ranges.dat` 부분 쓰기, 핸들 풀링 scratch 파일, 언마운트 시 인덱스 저장)로 분리하여 쓰기당 fsync와 `SPM2` 부하를 제거합니다. 파티션 간 이름 변경은 스테이징 경로와 파일 ID를 기록하고 호출자의 `replace` 설정으로 목적지에 공개합니다. 연속 이동 시에도 열린 핸들은 최종 파일을 가리킵니다. 재연결 시에는 이미 공개한 목적지의 후속 편집을 보존하고 기록된 출발지 정리만 마칩니다. WinFsp 마운트는 커널 메타데이터/파일 캐싱(`FileInfoTimeout`/`DirInfoTimeout`/`SecurityTimeout` 10초, `VolumeInfoTimeout` 5초, `FlushAndPurgeOnCleanup = false`, 워커 16스레드)을 활성화하고, `capture-source`·`source-query`·`flush`·`stop` 직전에 수정된 소스 경로만 `FlushFileBuffers`로 영속화합니다(flush 오류는 호출자에게 전파하고 동시 쓰기·열린 핸들의 dirty 표시는 유지하며, 메모리 매핑 뷰는 작성자가 뷰와 파일을 명시적으로 flush해야 합니다).

각 raw frame에는 헤더, 변경 메타데이터, 페이로드와 SHA-256이 들어갑니다. 16 MiB 또는 4,096 frame에 도달하면 다음 segment로 넘어갑니다. 저장 길이와 디코딩 길이는 일치해야 합니다. 개발용 append, bounded, latest-state 저널에는 각각 별도 프로토콜이 적용됩니다.

쓰기 결합은 FIFO·buffer/handle lease·callback/chunk/staging 예산·실패 poison/drain을 유지합니다. 여러 변경이 flush를 공유할 수 있지만 각 성공 응답은 데이터와 metadata의 영속화 뒤에만 나갑니다. metadata header/page는 root ACK 전에 flush합니다. flush 재사용은 정확한 열린 writer·동일 바이트의 증명 안에 한정하며 reopen을 넘지 않습니다.

요청 내 metadata/catalog reader와 경로 증명은 native fence·현재 소유/leaf 검사·용량 제한·무효화를 유지합니다. 선택 metadata/mutation batch·원본 관측 port의 미지원 store는 전체 검증 fallback입니다. 정확한 writer의 append 추적은 실패 시 무효화합니다. 적응형 callback 수집·비용 기반 packing도 ACK·reader·복구 보호를 유지합니다.

`StorageResources`의 프로세스별 수요 기반 작업 목표는 최대 4GiB이며 metadata cache 최대 2.5GiB·공통 sort/spool scratch 최대 512MiB·worker 최대 32개를 사용합니다. 사전 할당·OS RSS·여러 provider 합산 상한은 아닙니다. Plastic revision 읽기는 직렬입니다.

## 수집·결과·게시

중간 checkpoint 뒤에도 같은 task/path/ID/basis/Library를 사용합니다. 수집은 변경 경계 안에서 accepted root·보호를 고정하고 내용 읽기/hash·독립 보존을 경계 밖에서 수행합니다. 같은 request는 최초 고정 결과를 반환하며 이후 편집은 live 작업에 남습니다.

[작업공간 간 변경 반영](../workspace-import.md)에서는 받는 쪽 작업공간이 반영 계획, 조건부 파일 쓰기, 보낸 작업공간별 경로 이력을 관리합니다. 받는 쪽에서 체크포인트를 만들면 자체 편집과 가져온 변경이 함께 보존됩니다.

미완료 capture/control/pin·live-repack은 별도 GC보다 먼저 같은 요청으로 복구합니다. 읽기 전용 metadata replay는 별도 소유 scratch 주소 공간을 사용하며 원본에 append하거나 전체 graph를 복사하지 않습니다. reader lease는 마지막 종료까지 source/basis/journal/scratch를 유지합니다. offline v3는 live root를 꾸미지 않고 변경 관측을 거부합니다.

`OwnedContentStore`·`OwnedRangeContentStore`·`ResultFileContentStore`는 불변 whole/chunk/slice와 전체 의존성을 보존합니다. 수정 가능한 원본은 기준본 대체가 될 수 없습니다. 전체 수집/게시 검증·독립 내용 증명·과거 참조·같은 요청 adoption을 유지합니다.

원본 반영에는 명시적 결과·plan·intent/digest·전체 대상 잠금·현재 내용/native 재검증이 필요합니다. Git 원본 파일 반영은 HEAD/index/branch를 보존하며 branch 구체화는 별도 명시적 통합입니다. 실행 중 원본은 정확한 외부 Editor 협력 증거가 필요합니다. 자동 commit/check-in·강제 충돌 해결은 없습니다.

## 복구·회수와 도구 경계

현재 버전의 중단·응답 유실·재시작은 같은 identity/request/intent로 복구합니다. 미지원·손상·unknown·비소유 자료는 보존·거부합니다. 게시·삭제 경계에서 전체 native handle/path/parent/content와 reader 보호를 유지합니다.

`workspace maintain`은 workspace/shared/content/range metadata를 소유 범위에서 회수하며 진행 중 요청을 먼저 재개합니다. 완료 재시도는 receipt만 반환합니다. `workspace read`는 root-bound 페이지와 보호/지연 잔여를 제공합니다. 회수 임계값은 입력 상한이 아닙니다.

Library 복원은 기존 intent를 먼저 재개합니다. 빠른 인덱스 기반 Unity `Library` 기준본은 미변경 시(인덱스·시퀀스 0 확인 후) 같은 볼륨 O(1) NTFS 디렉터리 이동으로 복원하고, 변경 시에는 작업 계획에 기록된 항목을 검증한 뒤 디렉터리를 공개합니다. catalog 기반 기준본은 unchanged·sparse·root-delta·적격 topology·일반 파일별 경로를 선택합니다.

Splinter는 반환 프로젝트 identity와 소유 Editor/provider/resource 수명을 관리합니다. Windows 프로세스 그룹은 루트 종료 시 작업공간 하위 프로세스를 정리하되 `KILL_ON_JOB_CLOSE`를 설정하지 않고(동일 핸들에서 job 소속과 이미지 이름을 확인해) 머신 공용 `Unity.Licensing.Client`를 종료 대상에서 제외하여 여러 Unity Editor가 병렬 실행 및 임의 순서로 독립 종료할 수 있게 합니다. Unity 작업 마운트의 명시적 `release`·`discard`에 한해 루트 디렉터리 CWD 핸들만 남은 경우 750ms 유예 후 정상 언마운트를 허용합니다(`compact`·`library restore`·view는 열린 핸들 0개를 유지합니다). 에이전트와 기존 도구는 편집·대상 선택·연결·refresh·컴파일·Play/Test/build·캡처·합격 판단을 담당합니다. 매 응답의 project/PID를 작업공간과 대조하며 도구 목록 누락을 Editor 부재로 판단하지 않습니다. timeout만으로 쓰기를 반복하거나 reload 뒤 port를 고정하지 않습니다.

생성/재개 → 작업 → checkpoint → 선택적 명시 반영 → release/discard가 기본 흐름입니다. release는 독립 결과를 보존하고 discard는 별도 명시 요청입니다. 대화/Editor 종료·임대 만료·시간 초과·디스크 압박은 통합/삭제 승인이 아닙니다. 대형 입력·전체 수명주기·RAM·OS 장애 자격은 [영문 검증 안내](../verification/README.md)의 미완료 경계를 따릅니다.
