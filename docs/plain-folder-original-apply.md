# 일반 폴더의 명시적 원본 반영

`source=none`·`application=none`의 NTFS task에서 정확한 독립 checkpoint를 고정 원본 `ProjectPath`에 반영한다. 현재 구현과 일반 권한 파일 시험은 아래 계약을 제공한다. 실제 제품 VSS·WinFsp A/B 자격은 [검증 기록](verification/plain-folder-original-apply.md)과 [AP 추적표](plans/validation-matrix.md#vcs-없는-원본-반영)를 따른다. Unity application, Plastic 사용 환경, release한 결과의 가져오기와 다른 대상 export는 제공하지 않는다.

## 계획과 실행

```powershell
pwsh ./scripts/splinter.ps1 schema workspace.integrate
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --plan --home <home>
pwsh ./scripts/splinter.ps1 workspace integrate --id <id> --target original --intent-key <고정 요청 key> --expected-plan-digest <계획 digest> --home <home>
```

`--plan`은 원본·workspace 상태를 쓰지 않는다. 짧은 작업/원본 잠금 아래 현재 checkpoint·독립 payload·현재 원본을 읽고 `data.originalApplyPlan`에 owner/request digest, basis/result, 고정 대상의 물리 identity, 변경 경로, 내용/유형/속성 guard와 충돌을 반환한다. 변경 경로 밖의 원본 수정·새 파일은 유지하며 폴더 전체의 clean 상태를 요구하지 않는다. 계획의 성공 응답은 반영 성공이 아니다.

실행은 명시적 `original`·intent key·정확한 검토 digest를 요구한다. 계획 이후 원본이 바뀌면 새 intent 게시 전에 `OriginalApplyPlanChanged`로 거부한다. 시작 전 충돌/미지원은 원본에 쓰지 않는다. 작업공간이 checkpoint 뒤 바뀌면 `ResultNotReady`다. Git의 기존 branch 대상과 기본 `development`는 유지하며 plain-folder 옵션을 Git에 적용하지 않는다.

수정·생성·삭제할 일반 파일과 필요한 디렉터리 생성/빈 디렉터리 삭제를 경로별로 처리한다. 기준과 결과가 같은 경로는 건드리지 않는다. 원본이 기준과 같으면 반영 후보, 결과와 같으면 추가 쓰기 없이 확인, 서로 다른 변경이면 충돌이다. 경로 rename은 삭제/생성으로 비교한다. case-only rename·유형 교체·지원하지 않는 속성 변경·read-only·reparse·hard link·compressed/encrypted/sparse·대체 데이터 스트림은 보존하고 거부한다. 원본 삭제 디렉터리의 무관한 자식은 제거하지 않는다.

## 게시와 중단

Core의 `IOriginalApplyProvider`·`IOriginalFilePublisher`는 대상/계획/intent와 게시 port를 정의한다. Runtime은 소유 workspace 잠금, 정확한 결과/seal, 원본별 Splinter 잠금과 회수 guard를 제공한다. Files는 불변 기준·독립 결과·현재 원본을 비교한다. `WindowsOriginalFilePublisher`가 NTFS의 실제 물리 identity·부모 핸들·내용 읽기와 게시를 담당한다. Host는 계속 읽기 전용이다.

게시 전 변경 집합 전체의 기존 파일과 조상을 핸들로 고정하고 내용·유형·identity를 검사한다. 영향을 받는 기존 파일의 외부 쓰기/삭제와 조상 교체를 제외하며, 무관한 경로 쓰기는 허용한다. 새 이름의 점유자는 항상 덮어쓰기 없이 거부한다. 원본에 미해결 `.splinter-apply-` 게시 파일이 있으면 새로운 task의 캡처를 거부해 해당 파일을 새 기준으로 가져가지 않는다. 이미 생성한 peer의 고정 기준/편집은 계속 유지한다. 기존 파일은 같은 디렉터리의 정확한 `.old` 이름으로 옮겨 보존하고, 검증된 `.new`를 원래 이름으로 게시한 뒤 기존 파일을 회수한다. 이 두 이름 변경 사이에 원래 경로가 잠시 없을 수 있다. 중단하면 기존 내용과 새 payload를 해당 이름에 보존하며, 외부에서 원래 이름에 만든 파일을 자동 제거하지 않는다. 경로 집합 전체의 원자성은 제공하지 않는다.

기존 파일의 owner/group/DACL·DACL 보호와 creation time, 결과의 지원 속성/수정 시각을 게시 파일에 적용한다. 감사 SACL·보안 label·EFS 등 특수 보안 메타데이터의 보존과 동시 권한 변경은 현재 검증 범위가 아니다. 보안 메타데이터를 관리하는 입력에는 이 파일 시험을 일반 지원으로 승계하지 않는다. 접근/공유 오류를 elevation이나 속성 우회로 해결하지 않는다.

원본 쓰기 전에 v2 workspace의 `originalApply`에 version 1 계획·고정 intent·경로별 영수증을 게시한다. 같은 id/home·intent·digest·basis/result만 재개한다. stage 생성 identity, payload 완성, 기존 파일 이동, 게시와 회수를 구분하며 완료된 경로는 재확인한다. 미완성 소유 stage는 identity와 실제 payload prefix가 맞을 때만 이어 쓴다. receipt 없는 stage·변경된 임시 파일·중단 뒤 새 원본 충돌은 보존하고 중지한다. 자동 rollback/reset·다른 intent·임시 파일 sweep은 없다. 손상/누락/unknown 필드는 보존하고 거부한다.

미완료 intent는 checkpoint·ensure·Editor 수명 변경·compact·release/discard를 차단한다. 제한 서비스의 별도 Release authorization도 이를 검사한다. 정상 완료 후 명시적으로 회수/폐기할 수 있으며 discard가 이미 반영한 원본을 되돌리지 않는다. 완료 영수증은 정확한 결과에 고정된다. 완료 요청의 중복은 원본의 이후 수정을 다시 덮어쓰지 않는다. 이후 편집을 다른 checkpoint/apply 세대로 바꾸는 기능은 제공하지 않으며 기존 결과와 이후 편집을 보존하고 새 task를 사용한다.

## 비용과 자격

명시적 반영은 변경 파일 하나의 stage를 만들고 게시/회수 후 다음 경로로 진행한다. 기존 내용은 rename으로 보존하므로 별도 전체 원본 backup/clone을 만들지 않는다. stage는 알려진 payload 길이만 예약하고 64KiB 버퍼로 hash·읽기·쓰기한다. 독립 결과 객체·workspace 범위 저널·명시적 게시 파일은 각각 다른 비용이다. 이 동작은 workspace 생성/hydration/copy-up이 아니다.

원본 반영에는 [일반 폴더 수명주기](vcs-independent-workspaces.md)의 전제와 **이번 pending-apply guard를 포함한 현재 제한 서비스 패키지**가 필요하다. 설치/업데이트와 grant는 별도 명시적 관리자 Apply다. 일반 CLI는 이를 실행하거나 자동 elevation하지 않는다. 실제 제품 A/B가 끝나기 전에는 이 구현을 전체 수명주기의 native 통과로 표시하지 않는다.
