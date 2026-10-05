# Git 소스와 원본 Library 참조

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

공통 기준은 이동한 데이터이므로 새 전체 payload 비용으로 합산하지 않는다. manifest·파일 핸들·provider·각 저널은 별도 비용이다. 전체 읽기 검증에는 I/O가 들고, Unity가 재수입하거나 자체 cache를 다시 쓰면 그 private 범위도 저장된다. 파일 참조만으로 Unity heap·graphics·scene RAM 공유를 주장하지 않는다. RAM 계측은 `scripts/measure-process-memory.ps1`의 Private Bytes·상주 페이지·PSS 추정을 구분한다. 이 스크립트는 page metadata/residency만 조회하고 원본 payload를 읽거나 GC/working-set trim을 하지 않는다. ShareCount의 7 상한·표본 비원자성·각 Editor 간 공유 귀속 unknown을 유지한다.

파일 할당 계측은 기존 `scripts/measure-workspace-storage.ps1`에 정확한 `-WorkspaceIds`와 `-GeneratedDirectoryIds <basis-id> -SkipOriginalLibrary`를 전달한다. 선택한 state/project/owner/경로를 대조하고 공유 basis root를 한 번만 센다. Win32 조회에는 긴 NTFS 경로도 전달한다. `moved-original-Library-payload`는 원래 있던 데이터의 이동이며 추가 비용에서 제외한다. 원본 delta·basis metadata·각 작업 delta·공유 runner/Git·증거 비용은 구별한다. 전체 물리 peak/VSS 귀속은 이 파일 표본으로 확정하지 않는다.

일반 NTFS 파일/디렉터리만 대상으로 한다. reparse·hardlink·ADS·EFS·NTFS compression·sparse 입력과 별도 보안 descriptor/OS 장애는 추가 자격이 필요하다. 기존 VSS·개발용 single-view Library cache의 자격을 이 경로로 승계하지 않는다. 선택 범위와 현재 자격은 [실행 계획](plans/git-library-reference.md)과 [검증](verification/git-library-reference.md)을 따른다.
