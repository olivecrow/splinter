# Git 소스와 원본 Library 참조

[English](../git-library-reference.md)

`git-snapshot`의 Git commit/tree/blob 기준과 원본 Library의 불변 기준을 함께 읽을 수 있다. Git이 추적하지 않는 Library도 별도 기준의 정확한 id·owner·manifest·read proof를 mount와 저널에 고정한다. 초기 전체 복사·읽기 hydration·파일 전체 copy-up·VSS는 사용하지 않는다. 원본·A/B는 같은 Library 기준을 읽고 각각의 범위 저널에 쓴다.

## 명시적 수명주기

앱별 Library descriptor·원본 reservation·호환 키는 Unity 정책을 명시적으로 주입해 선택한다. 관리형 기준 state와 준비 intent는 앱·디렉터리 종류를 고정한 v2만 지원하며 v1은 보존·거부한다. 공통 확장 계약은 [확장 경계](extensibility-boundaries.md)를 따른다.

사용자가 허용한 예외는 **Library만 관리형 mount로 전환하는 것**이다. 프로젝트 root와 소스는 기존 NTFS 위치에 남는다. 최초 연결과 마지막 복원에는 원본 Editor를 저장하고 정상 종료한다. Splinter 명령은 원본 Editor를 자동 종료하거나 다시 열지 않는다.

1. `library prepare --project <original> --key <stable-key> --home <same-volume-home>`는 기존 Library를 같은 NTFS 볼륨의 소유 `library-bases/<id>/base`로 rename하고 기존 `<original>/Library`에 원본 전용 writable WinFsp overlay를 연결한다.
2. 반환 `data.basis.basis.id`를 `workspace ensure --storage git-snapshot --library-basis <id>`에 고정한다. 원본과 작업 home/프로젝트/id를 보존한다. 기존 cold Git 경로의 생략된 Library 기준을 자동 선택·변경하지 않는다.
3. `library status --id <id> --home <home>`는 durable 상태를 읽는다. Editor·컴파일·MCP 준비 완료 증거가 아니다. MCP/CLI는 항상 반환 workspace 경로와 실제 Editor PID/시작 시각을 대조한다.
4. 각 작업을 명시적으로 checkpoint/release 또는 discard한다. 소유 mount/provider/delta가 실제로 없어야 생성 데이터 의존성을 제거한다. A 회수는 B의 기준과 쓰기를 제거하지 않는다.
5. 원본 Editor를 정상 종료한 뒤 `library restore --id <id> --home <home>`를 실행한다. 작업 의존성과 모든 basis reader가 없어야 한다. 최종 파일 구조의 metadata 계획과 journal seal을 고정한 뒤 기존 파일은 같은 볼륨에서 이동하고 private 범위만 덮어쓴다. 원본 Library가 다시 일반 NTFS 폴더가 된 뒤 소유 base와 원본 delta를 회수한다.

같은 key/id/home/호환 키·manifest·요청을 사용한다. 준비 중 원본과 base가 둘 다 있거나, 복원 대상에 예상 밖 파일/변경된 보존 영역/다른 seal이 있으면 보존하고 거부한다. 초기 file hash와 고정 metadata를 확인하고 reader가 기존 basis 파일의 직접 쓰기·삭제를 차단한다. 읽기는 manifest와 열린 basis 핸들에서만 수행한다. 생성 데이터의 탈락·손상은 mutable 원본이나 빈 Library로 fallback하지 않는다.

준비 metadata보다 먼저 현재 버전의 `prepare-intent.json`에 project/key/경로/호환 키를 고정한다. 원본을 아직 이동하지 않은 내용 읽기 실패는 정확한 intent와 분류된 metadata만 남았을 때 같은 요청으로 재개한다. intent 없는 root·다른 요청·provider 실행 흔적·불명 데이터는 보존하고 거부한다. 공개되지 않은 `preparing` 상태의 명시적 restore는 원본 또는 이동한 base를 그대로 돌려놓을 수 있으며, 불명 실행/비어 있지 않은 delta는 취소하지 않는다.

Library 안에 외부 서버의 계속 쓰는 로그가 있으면 기준본을 고정할 수 없다. 외부 서버의 수명/로그 위치는 호출자의 별도 승인 절차로 먼저 정리한다. Splinter는 그 서버를 자동 종료하거나 입력 파일을 생략하지 않는다.

## 복원과 비용 경계

복원은 durable 계획·완료 cursor로 재개한다. 시작된 파일 이동·범위 쓰기는 같은 계획으로 검증/redo하고, 이미 완료한 파일의 변경은 거부한다. 잘린 basis tail은 다시 나타나지 않도록 retained length와 private 범위를 적용한다. 새 파일은 zero hole과 새 payload를 쓰며 기존 파일의 나머지 데이터는 복사하지 않는다. 원본의 최종 Library 내용이 독립적으로 검증된 뒤만 소유 payload를 삭제한다. 작은 파일 시험의 중단 주입은 실제 OS 전원 장애 자격이 아니다.

현재 복원 계획은 v2이며 기존 파일의 NTFS volume/file identity와 새 파일의 생성 영수증을 확인한다. 파일과 부모 경로를 핸들로 보호한 상태에서 이동·범위 쓰기·메타데이터 적용을 수행한다. 완료 cursor 앞뒤와 최종 회수 전 재개에서도 교체 파일은 내용이 같아도 거부한다. 새 파일 생성 뒤 영수증 게시 전 중단은 보존 필요 상태이며 경로 존재만으로 같은 파일이라고 추측하지 않는다. 대상·기준·저널·영수증을 보존하고 이전 복원 계획을 자동 이관하지 않는다.

변경 없는 파일은 검증 후 핸들로 보호 중인 basis manifest의 hash를 계획의 최종·보존 hash로 재사용한다. 실행은 저장된 파일 identity와 전체 내용을 새로 검증한 뒤 쓰기·삭제를 막는 같은 핸들로 이동·메타데이터 적용까지 수행한다. rename·메타데이터만 바뀐 파일도 같은 경로다. 변경 파일의 private 범위 재생과 개별 durable data flush는 유지한다. 이미 최종 hash와 일치하는 재시도는 같은 배타 output 핸들 안의 중복 hash만 생략한다. 부모 디렉터리는 절대 경로로 정규화하고 root부터 핸들로 고정해 각 부모에서 전체 조상을 다시 순회하지 않는다. 완료 cursor는 128개마다와 마지막 파일에서 게시하며 초기·최종 기록, 새 파일 소유 영수증과 범위 data flush는 개별 내구성을 유지한다. 묶음 게시 실패 시 완료 효과 최대 128개와 진행 중 효과를 재개 때 검증할 수 있다. 파일 identity·내용, 외부 대상, 계획·seal과 회수 guard는 그대로다. 이 파일별 경로는 기존 v2 계획·cursor 형식을 유지한다. [검증과 한정 비용](verification/lifecycle-read-optimization.md)을 따른다.

신규 복원에서 원본 Unity Library가 완전히 변경되지 않았고 SJL4 저널이면 대상 덮어쓰기 없는 디렉터리 이동 한 번을 사용한다. sequence 0과 논리 manifest 전체 일치로 선택하며 변경 원본·SJR2·전체 프로젝트 기준본·시작한 v2 파일 계획은 일반 복원을 유지한다. 별도 v1 디렉터리 계획은 root/하위 NTFS identity, 원본/delta 경로, overlay identity, manifest hash와 seal을 고정한다. 하위 핸들을 닫아야 하는 간격 뒤 identity·전체 내용·구성원을 검증한 다음에만 메타데이터 복원과 정확한 SJL4 회수를 진행한다. root·조상 핸들은 유지하며 예상 밖 항목이나 교체 항목은 보존·거부한다. 재시도는 내용 재검증·메타데이터 재적용과 계획에 결합한 완료 영수증·재개 가능한 회수 inventory를 사용한다. 직렬 복원 임대 안의 선택적 계획/cursor 부재는 기다리지 않는다. 복구 경계와 작은 입력 비용은 [LO-09–11](verification/lifecycle-read-optimization.md)을 따르며 새 대형 Unity 결과를 주장하지 않는다.

공통 기준은 이동한 데이터이므로 새 전체 payload 비용으로 합산하지 않는다. manifest·파일 핸들·provider·각 저널은 별도 비용이다. 전체 읽기 검증에는 I/O가 들고, Unity가 재수입하거나 자체 cache를 다시 쓰면 그 private 범위도 저장된다. 파일 참조만으로 Unity heap·graphics·scene RAM 공유를 주장하지 않는다. RAM 계측은 `scripts/measure-process-memory.ps1`의 Private Bytes·상주 페이지·PSS 추정을 구분한다. 이 스크립트는 page metadata/residency만 조회하고 원본 payload를 읽거나 GC/working-set trim을 하지 않는다. ShareCount의 7 상한·표본 비원자성·각 Editor 간 공유 귀속 unknown을 유지한다.

파일 할당 계측은 기존 `scripts/measure-workspace-storage.ps1`에 정확한 `-WorkspaceIds`와 `-GeneratedDirectoryIds <basis-id> -SkipOriginalLibrary`를 전달한다. 선택한 state/project/owner/경로를 대조하고 공유 basis root를 한 번만 센다. Win32 조회에는 긴 NTFS 경로도 전달한다. `moved-original-Library-payload`는 원래 있던 데이터의 이동이며 추가 비용에서 제외한다. 원본 delta·basis metadata·각 작업 delta·공유 runner/Git·증거 비용은 구별한다. 전체 물리 peak/VSS 귀속은 이 파일 표본으로 확정하지 않는다.

일반 NTFS 파일/디렉터리만 대상으로 한다. reparse·hardlink·ADS·EFS·NTFS compression·sparse 입력과 별도 보안 descriptor/OS 장애는 추가 자격이 필요하다. 기존 VSS·개발용 single-view Library cache의 자격을 이 경로로 승계하지 않는다. 선택 범위와 현재 자격은 [실행 계획](plans/git-library-reference.md)과 [검증](verification/git-library-reference.md)을 따른다.

## 내용 검증 소스 시각

새 Unity Library를 일반 Git repository root에서 준비할 때 고정 commit/tree를 기록하고, 원본 파일을 write/delete 공유 없는 같은 핸들로 읽어 raw Git blob hash와 일치한 파일의 시각만 수집한다. 작업 디렉터리의 CRLF 변환·내용 변경·누락·점유 파일은 재사용하지 않는다. 원본 핸들은 준비 뒤 닫으므로 이후 원본 편집은 가능하다. 별도 payload 복사나 cache 없이 소스 내용 전체 읽기 1회를 추가한다. Git이 아닌 입력과 `.git` 파일인 linked root는 이 최적화를 선택하지 않으며 기존 입력 판정은 그대로다. 손상된 일반 Git root·partial clone·shallow·alternates는 보존·거부한다.

원자 게시하는 `source-metadata.json` v1은 현재 canonical 내용의 `contentHash`를 포함한다. Library 전체 tuple·manifest hash와 전체 map의 `sourceMetadataHash`를 기준 참조와 Git receipt/snapshot identity에 고정한다. 상태 게시 전 재시도도 같은 intent·Library manifest/proof·map 내부 내용 증명을 확인하고 기존 bytes를 재사용한다. 원본 재조회·자동 손상 복구는 없다. 같은 경로/blob/길이만 시각을 적용하며 다른 항목/디렉터리는 snapshot capture 시각을 유지한다. Map 없는 기존 기준은 자동 보충하지 않는다. Schema·회귀·추가 읽기 비용과 새 전체 A/B 미실행 경계는 [SU-05](verification/startup-profile-optimization.md)를 따른다.
