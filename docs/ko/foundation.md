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

현재 task/basis/제품 overlay는 **v4/v3/v3**, 파일 결과 manifest는 **v4**, metadata는 **SPM2**입니다. 미지원 과거 상태는 자동 이관·호환 fallback 없이 보존·거부합니다. VSS/StorageAuthority와 ReFS 기준본은 폐기했습니다.

## Metadata와 변경 범위

[대규모 저장 계약](../scalable-storage.md)은 최대 64KiB 이진 페이지·경로 interning·디스크 sort/spool·이력/범위 index·cursor 조회·metadata 회수를 소유합니다. 논리 node hash/count·accepted-source seal은 전체 native identity/hash/page/inventory 증명을 대체하지 않습니다.

제품 overlay v3는 SJS1 제어와 raw SJR2 segment를 사용합니다. 16MiB/4,096-frame rollover는 처리 단위이며 전체 입력 상한이 아닙니다. frame의 header·mutation metadata·raw payload·SHA-256을 유지하고 stored/decoded 길이가 같아야 합니다. 과거 압축 payload는 바이트를 고치지 않고 거부하며 Brotli 호환 reader·decoded cache는 제거했습니다. 개발용 append/bounded/latest-only 경로의 현재 프로토콜은 각각 유지합니다.

쓰기 결합은 FIFO·buffer/handle lease·callback/chunk/staging 예산·실패 poison/drain을 유지합니다. 여러 변경이 flush를 공유할 수 있지만 각 성공 응답은 데이터와 metadata의 영속화 뒤에만 나갑니다. metadata header/page는 root ACK 전에 flush합니다. flush 재사용은 정확한 열린 writer·동일 바이트의 증명 안에 한정하며 reopen을 넘지 않습니다.

요청 내 metadata/catalog reader와 경로 증명은 native fence·현재 소유/leaf 검사·용량 제한·무효화를 유지합니다. 선택 metadata/mutation batch·원본 관측 port의 미지원 store는 전체 검증 fallback입니다. 정확한 writer의 append 추적은 실패 시 무효화합니다. 적응형 callback 수집·비용 기반 packing도 ACK·reader·복구 보호를 유지합니다.

`StorageResources`의 프로세스별 수요 기반 작업 목표는 최대 4GiB이며 metadata cache 최대 2.5GiB·공통 sort/spool scratch 최대 512MiB·worker 최대 32개를 사용합니다. 사전 할당·OS RSS·여러 provider 합산 상한은 아닙니다. Plastic revision 읽기는 직렬입니다.

## 수집·결과·게시

중간 checkpoint 뒤에도 같은 task/path/ID/basis/Library를 사용합니다. 수집은 변경 경계 안에서 accepted root·보호를 고정하고 내용 읽기/hash·독립 보존을 경계 밖에서 수행합니다. 같은 request는 최초 고정 결과를 반환하며 이후 편집은 live 작업에 남습니다.

미완료 capture/control/pin·live-repack은 별도 GC보다 먼저 같은 요청으로 복구합니다. 읽기 전용 metadata replay는 별도 소유 scratch 주소 공간을 사용하며 원본에 append하거나 전체 graph를 복사하지 않습니다. reader lease는 마지막 종료까지 source/basis/journal/scratch를 유지합니다. offline v3는 live root를 꾸미지 않고 변경 관측을 거부합니다.

`OwnedContentStore`·`OwnedRangeContentStore`·`ResultFileContentStore`는 불변 whole/chunk/slice와 전체 의존성을 보존합니다. 수정 가능한 원본은 기준본 대체가 될 수 없습니다. 전체 수집/게시 검증·독립 내용 증명·과거 참조·같은 요청 adoption을 유지합니다.

원본 반영에는 명시적 결과·plan·intent/digest·전체 대상 잠금·현재 내용/native 재검증이 필요합니다. Git 원본 파일 반영은 HEAD/index/branch를 보존하며 branch 구체화는 별도 명시적 통합입니다. 실행 중 원본은 정확한 외부 Editor 협력 증거가 필요합니다. 자동 commit/check-in·강제 충돌 해결은 없습니다.

## 복구·회수와 도구 경계

현재 버전의 중단·응답 유실·재시작은 같은 identity/request/intent로 복구합니다. 미지원·손상·unknown·비소유 자료는 보존·거부합니다. 게시·삭제 경계에서 전체 native handle/path/parent/content와 reader 보호를 유지합니다.

`workspace maintain`은 workspace/shared/content/range metadata를 소유 범위에서 회수하며 진행 중 요청을 먼저 재개합니다. 완료 재시도는 receipt만 반환합니다. `workspace read`는 root-bound 페이지와 보호/지연 잔여를 제공합니다. 회수 임계값은 입력 상한이 아닙니다.

Library 복원은 기존 intent를 먼저 재개하고 unchanged·sparse·root-delta·적격 topology·일반 파일별 경로를 선택합니다. 최적화도 전체 identity/content/membership·정확한 소유권을 요구하며 미지원 변경은 일반 경로를 사용합니다.

Splinter는 반환 프로젝트 identity와 소유 Editor/provider/resource 수명을 관리합니다. 에이전트와 기존 도구는 편집·대상 선택·연결·refresh·컴파일·Play/Test/build·캡처·합격 판단을 담당합니다. 매 응답의 project/PID를 작업공간과 대조하며 도구 목록 누락을 Editor 부재로 판단하지 않습니다. timeout만으로 쓰기를 반복하거나 reload 뒤 port를 고정하지 않습니다.

생성/재개 → 작업 → checkpoint → 선택적 명시 반영 → release/discard가 기본 흐름입니다. release는 독립 결과를 보존하고 discard는 별도 명시 요청입니다. 대화/Editor 종료·임대 만료·시간 초과·디스크 압박은 통합/삭제 승인이 아닙니다. 대형 입력·전체 수명주기·RAM·OS 장애 자격은 [영문 검증 안내](../verification/README.md)의 미완료 경계를 따릅니다.
