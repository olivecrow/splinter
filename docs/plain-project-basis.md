# VCS 없는 프로젝트 기준본

`project-snapshot`은 VCS 없는 프로젝트와 Library를 하나의 불변 기준으로 읽고 원본·각 task의 변경 범위만 SJR2에 저장한다. VSS, Git init, 최초 checkout, 읽기 disk cache와 전체 파일 copy-up fallback을 사용하지 않는다. [실제 자격](verification/plain-project-basis.md)은 현재 후보의 파일 시험과 작은 Unity 입력에 한정한다.

사용자가 승인한 관리형 원본 접근 계약이다. 원본 Editor를 저장하고 정상 종료한 뒤 기존 트리를 **같은 NTFS 볼륨에서 복사 없이 이동**하며 기존 프로젝트 경로에 writable WinFsp mount를 제공한다. 활성 원본 경로의 파일시스템은 `SplinterSO`이고 실제 기준 데이터는 같은 NTFS 볼륨에 남는다. 소스·Library의 원본 쓰기도 원본 전용 범위 저널에 저장한다. 모든 작업 회수 후 명시적 `project restore`가 원본의 현재 내용과 metadata를 일반 NTFS 폴더로 복원한다. 직접 NTFS 원본 접근을 유지하는 kernel 쓰기 선행 보존 계층은 제공하지 않는다.

## 입력과 준비

원본과 home은 서로 분리된 같은 NTFS 볼륨에 있어야 한다. 원본·조상·하위의 `.git`, `.plastic`, `.hg`, `.svn`과 미완료 게시 파일은 거부한다. VCS 있는 입력은 해당 Git/Plastic 경로를 사용하며 오류를 None으로 fallback하지 않는다. reparse·ADS·hardlink·특수 metadata 등 기존 불변 directory 입력 제한을 유지한다. 최초 검증/hash 읽기는 필요하며 이를 I/O 0이라고 표시하지 않는다.

```powershell
pwsh ./scripts/splinter.ps1 schema project.prepare
pwsh ./scripts/splinter.ps1 project prepare --project <원본> --key <고정 기준 key> --application unity --home <같은 NTFS home>
pwsh ./scripts/splinter.ps1 schema workspace.ensure
pwsh ./scripts/splinter.ps1 workspace ensure --project <원본> --key <task key> --source none --application unity --storage project-snapshot --project-basis <반환 basis.id> --home <같은 home>
```

일반 데이터는 prepare와 ensure 모두 `--application none`을 사용한다. `unity`는 Unity 버전·package 호환 key를 고정하며 Library도 전체 프로젝트 기준에 포함한다. `--library-basis`와 `--project-basis`는 함께 사용하지 않는다. `base=snapshot`, purpose task만 허용한다. 현재 상태는 v2이고 기준 state는 version 1이다. 이전 상태의 자동 이관은 없다.

같은 project/key/home/application의 prepare와 같은 task ensure는 기존 identity를 유지한다. 응답의 `data.basis.basis.id`, `data.workspace.path`, task id/digest와 basis manifest hash를 인계에 남긴다. prepare 실패는 새 key로 재캡처하지 않고 같은 요청의 준비 상태를 복구한다. `project status --id <basis id>`는 저장된 identity/phase를 읽으며 현재 mount 건강을 인증하지 않는다. task attach·원본 계획/게시는 정확한 provider·원본 volume identity와 기준 reader lease를 별도로 확인한다. 소실·손상·owner 불일치는 보존 후 거부한다.

## 작업·결과·반영

원본·A·B는 같은 불변 tree를 읽지만 소스·Library·Temp 쓰기는 각각 private 저널이다. 반환 작업 경로에서 파일 도구·MCP·Unity CLI를 사용한다. Splinter는 도구의 씬·컴파일·테스트 성공을 판정하지 않는다. MCP는 매 요청의 정확한 project/PID/start를 대조하고 reload 뒤 고정 port나 세션 기본 대상을 신뢰하지 않는다.

checkpoint는 기존 파일 공급자의 기준·결과 manifest와 독립 payload를 보존한다. Unity의 Library/Temp 등 생성 경로와 소유 bridge는 소스 결과에서 제외한다. 일반 application none은 모든 파일을 데이터로 취급한다. 명시적 결과 보존의 payload 복사 비용은 최초 기준/읽기/작업 변경 비용과 별도다.

```powershell
pwsh ./scripts/splinter.ps1 workspace checkpoint --id <task id> --message <설명> --home <home>
pwsh ./scripts/splinter.ps1 workspace integrate --id <task id> --target original --plan --home <home>
# 선택 반영은 계획과 실행 모두 같은 --path <경로> 집합을 유지한다.
pwsh ./scripts/splinter.ps1 workspace integrate --id <task id> --target original --intent-key <고정 intent> --expected-plan-digest <검토 digest> --home <home>
```

`project-snapshot/source=none`은 application none/unity의 전체·선택 원본 게시를 허용한다. 기준/결과/현재 원본의 3자 비교·정확한 mount lease·원본 Editor 종료 lock·고정 intent/digest·조건부 파일 게시를 사용한다. 전체 결과 파일의 게시 stage는 명시적 원본 반영 비용이며 파일 copy-up fallback이 아니다. managed mount는 초기 ADS 거부와 NamedStreams=false, 고정 security descriptor를 전제로 하며 하위 reparse/관리 경로 guard를 유지한다. legacy `ntfs-snapshot/source=none/application=unity` 원본 반영은 여전히 미지원이다. 자세한 파일 게시/미완료 intent 보호는 [일반 폴더 원본 반영](plain-folder-original-apply.md)을 따른다.

## 회수와 원본 복원

release는 결과·seal·소유 자원 0·mount/delta 부재를 확인한 뒤 **해당 task의 공통 기준 참조만** 회수한다. peer의 기준/Editor/저널을 제거하지 않는다. discard는 기존 명시적 intent/digest를 요구한다. 다른 작업의 validation 의존성과 미완료 apply 보호는 유지한다. 공통 기준 payload는 `project restore`가 소유하므로 task의 snapshot reference 해제와 공통 데이터 삭제를 구분한다.

```powershell
pwsh ./scripts/splinter.ps1 workspace release --id <task id> --home <home>
pwsh ./scripts/splinter.ps1 schema project.restore
pwsh ./scripts/splinter.ps1 project restore --id <basis id> --home <home>
```

최종 restore 전에 모든 task를 명시적으로 회수하고 원본 Editor를 저장·정상 종료한다. active dependent/reader·미완료 intent와 예상 밖 원본 점유는 복원을 차단한다. 고정 restore plan/seal/cursor로 기존 파일을 이동하고 바뀐 범위만 적용한다. 같은 id/home의 중단 재개를 허용하며 성공한 restore 재요청은 완료 영수증을 반환한다. 활성 mount/provider/delta와 이동 기준 payload는 회수하지만 상태·manifest·plan·로그·독립 결과는 보존한다.

현재 restore plan은 v2다. 기존 파일의 NTFS volume/file identity를 계획에 고정하고 새 파일은 생성 뒤 파일별 영수증을 durable 게시한 다음 범위를 쓴다. 이동·쓰기 동안 파일과 부모 경로의 핸들을 유지하며, 재개·완료 확인에도 같은 파일 identity와 내용을 대조한다. 같은 이름/같은 바이트의 교체 파일도 소유 파일로 취급하지 않는다. 생성과 영수증 게시 사이에 중단되면 소유권을 추측하지 않고 대상·기준·저널을 보존해 거부한다. 이전 restore plan은 자동 이관하지 않는다. workspace v2·overlay v2/SJR2 형식은 유지한다.

계측은 `scripts/measure-workspace-storage.ps1 -SourceKind none -SkipOriginalLibrary -ProjectBasisIds <id> -WorkspaceIds <A>,<B>`로 공통 기준 root를 한 번만 센다. 이동한 기존 payload를 추가 저장 비용에서 제외하고 공통 manifest/원본 저널·task 저널·runner·독립 결과를 구분한다. 전체 순간 peak/OS metadata와 RAM 절감은 이 계약의 보장이 아니다.
