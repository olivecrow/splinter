# 같은 작업공간에서 반복 원본 반영

[English](../repeated-original-apply.md)

새 `checkpoint --release` 요청은 정상 종료·infrastructure 제거 뒤 한 번의 수집을 전체 전후 seal로 감싼다. 같은 호출이 seal한 generation으로 두 번째 수집 없이 회수하며 unmount 뒤 seal은 필수다. 이미 고정/게시된 요청의 재시도는 새 호출의 seal로 과거 결과를 재사용하지 않는다. 요청의 고정 결과를 반환하고 마감용 현재 소스를 별도로 수집한다. 독립 release도 현재 소스를 수집한다. [GCO 범위](plans/git-collection-optimization.md)를 따른다.

이 문서가 작업을 계속하는 결과 버전과 원본 파일 게시 계약을 소유한다. [범위](plans/repeated-original-apply.md)와 [RI-01–08 검증](verification/repeated-original-apply.md)은 구현·자동 시험·실제 Unity 입력을 구분한다. 작은 고정 Git·일반 폴더·Plastic Unity 원본/A/B 순서와 최종 전체 gate가 통과했으며 RI-01–08은 입력/도구/OS·비용 한계를 유지해 한정 완료했다.

## 수명과 identity

현재 task record는 **v3**다. 관리형 project/Library 기준본 상태는 v2를 유지한다. 이전 작업은 보존·거부하며 일치하는 이전 바이너리로 마무리한다. CLI 갱신은 자동 이관·회수·새 기준본·새 작업을 의미하지 않는다. 이전 record가 남은 home에는 새 버전을 섞지 않고 새 v3 task용 home을 별도로 명시한다. 이전 record를 이동·수정·자동 sweep하지 않는다.

작업공간은 반복 수집·반영 중 경로·ID·요청 digest·고정 기준본·private 저널·Library를 유지한다. 결과는 별도의 불변 ID·순번·요청 키·메시지·소스 순번·수집 digest·내용 identity·독립 참조를 가진다. 수집은 Editor를 종료하거나 이후 편집·재시작을 금지하지 않는다.

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.checkpoint
pwsh -File <product-root>/scripts/splinter.ps1 workspace checkpoint --id <task> --request-key <capture-key> --message <description> --home <home>
```

반환 `data.resultGeneration.id`를 사용한다. 같은 키·메시지는 이후 수집이나 작업 회수 뒤에도 최초 결과를 반환한다. 같은 키의 다른 메시지는 거부한다. 미완료 수집은 키·메시지를 고정하며 새 결과·compact·회수보다 먼저 완료한다.

provider는 접수된 쓰기를 drain하고 callback/mutation 경계에서 ACK한 데이터를 영속화한 뒤 namespace·metadata·범위 참조·backing 파일 identity를 영속 기록한다. 내부 NTFS backing alias가 범위를 보호한다. payload 읽기·해시·독립 보존은 이 경계 밖에서 수행하며 이후 편집은 다음 결과에 속한다. bounded 교체·capsule prefix 단축은 alias를 보호하고 필요한 private prefix만 소유 영수증에 결합해 복사한다. 원본 파일 copy-up·전체 clone·hydration은 제공하지 않는다. 재시작은 writer replay·회수보다 먼저 수집 보호를 읽는다.

파일 입력은 결과 버전 사이에서 내용 주소 payload를 재사용한다. Git은 독립 result ref/object를 보존하고 공통 게시기로 blob을 stream한다. 성공한 결과와 반영 영수증은 작업 회수 뒤에도 남는다. 보존 payload의 논리 수치와 실제 NTFS 할당·최대 RAM은 구분한다.

## 고정 결과 반영

```powershell
pwsh -File <product-root>/scripts/splinter.ps1 schema workspace.integrate
pwsh -File <product-root>/scripts/splinter.ps1 workspace integrate --id <task> --target original --result <result-id> --plan --home <home>
# 충돌과 전체/선택 write set을 검토한 뒤:
pwsh -File <product-root>/scripts/splinter.ps1 workspace integrate --id <task> --target original --result <result-id> --intent-key <apply-key> --expected-plan-digest <digest> --home <home>
```

선택 반영은 두 호출에 같은 `--path <relative-path>`를 반복한다. 새 반영은 최신 수집 결과를 명시하며 worker의 미수집 편집은 계속될 수 있다. 비교 기준은 최초 고정 기준본에서 시작한 **경로별 마지막 반영 상태**다. 선택·게시한 경로만 이를 갱신한다. 미선택 편집·삭제·이미 게시한 파일의 최초 내용 복귀도 변경으로 남는다. Unity는 필요한 `.meta` 동반 경로를 닫는다.

계획은 task·요청·기준·결과·직전 반영 digest·대상 root identity·VCS identity·선택·관측한 원본 내용/metadata/identity·실행 중 Editor 협력 identity를 결합한다. native 조건부 게시는 전체 write set 검사·소유 staging/복구 영수증을 유지한다. 충돌·미지원 metadata 변경·자원 교체·소유 불명은 보존·거부하며 강제 덮어쓰기 옵션은 없다. 미완료 intent는 같은 결과·키·digest·선택·협력 identity로 재개하고 새 수집·반영·회수로 우회하지 않는다.

완료한 과거 요청은 `data.originalApplyReceipt`와 해당 불변 결과 검증만 반환한다. 회수 뒤에도 원본 게시나 새 Editor admission을 수행하지 않고 이후 원본 편집을 덮어쓰지 않는다. status는 작업 수명·`results`·`captureIntent`·`originalApplies`·삭제 표시를 포함한 유효 `originalAppliedPaths`·진행 중 intent·복구 동작을 구분한다. 실행 중 원본의 복구에는 **같은** 협력 identity를 새로 관측한 영수증이 필요하며 status 안내 자체가 외부 도구 counter를 재획득하지 않는다.

Git `--target original`은 HEAD·index·branch를 바꾸지 않는 working-tree 파일 게시다. 관리 영역 읽기/lease·진행 중 lock 검사는 유지한다. 기존 명시적 branch merge는 별도의 clean-target 기능이다. Plastic은 고정 revision·미등록 worker·관리 root/selector/lock 보호와 서버 읽기 전용 동작을 유지한다. 원본 변경의 worker 유입·자동 commit/check-in·기준본 교체·자동 충돌 해결은 제외한다.

## 실행 중 Unity 원본: 에이전트 협력 절차

Splinter는 MCP/Unity CLI 호출·도구 패키지 설치/관리·저장·컴파일·게임 합격 판단을 수행하지 않는다. 에이전트가 기존 도구를 사용하고 **매 응답의 정확한 project/PID**와 native PID/시작 시각을 대조한다. 불완전한 목록에서 Editor 부재를 추정하지 않고 reload 뒤 재발견하며 포트를 identity로 고정하지 않는다.

1. 저장 전 Play/busy와 모든 dirty 씬·에셋을 확인한다. 경로 없는 dirty 씬·Play·저장 실패/불명은 편집을 보존하고 중단한다. 이름 있는 dirty 씬과 `AssetDatabase.SaveAssets`를 명시적으로 저장하고 dirty 해소를 확인한다. 도구의 `save_all`은 씬만 저장할 수 있다. 그 뒤 task를 수집한다.
2. 정확한 원본도 저장한다. 외부 도구로 `AssetDatabase.DisallowAutoRefresh`와 필요한 경우 `EditorApplication.LockReloadAssemblies`를 획득한다. 반환 전에 Editor에 요청 키별 소유 상태를 남긴다. 타임아웃은 완료부터 관측하며 counter를 중복 증가하거나 부분 게시 원본을 무조건 다시 저장하지 않는다.
3. `live-original-editor-permit.schema.json`에 맞게 version 1·정확한 project/PID/native start ticks·결과 ID·안정된 협력 키·저장/관측/만료 시각·`importsDeferred: true` 영수증을 쓴다. 만료는 관측 뒤 최대 5분이다. plan/apply에 `--live-original <receipt.json>`을 전달한다. 제품은 실제 Unity 실행 파일·시작 시각·`-projectPath`를 검사하고 lockfile 전제와 계획 결합을 유지한다. 필요하면 같은 소유 유예를 다시 관측해 동일 identity의 유효 시간을 갱신한다.
4. 게시 뒤 영향 씬·에셋의 **새** dirty 상태를 외부에서 확인한다. 해당 요청이 얻은 counter만 기록에 따라 정확히 한 번 해제한다. 새 편집이 있으면 동기화 미완료로 보고하고 강제 재로드하지 않는다. 안전하면 refresh·컴파일·필요한 깨끗한 씬/에셋 재로드와 실제 로드 값을 확인한다. process가 응답 중이어도 씬 외부 변경 확인창이 뜰 수 있으므로 검증한 clean 상태에 따라 처리하고 다시 연결해 완료를 확인한다.

자동 refresh 유예는 명시적 `Refresh`·씬 감시·외부 쓰기를 차단하지 않는다. 실제 내용 보호는 조건부 게시가 맡는다. [Unity 유예 계약](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/assetdatabase/disallowautorefresh)을 따른다. 타임아웃 뒤 저장·게시·유예 해제·재로드를 무조건 반복하지 않는다. CLI/JavaScript의 숫자 직렬화는 64비트 ticks를 반올림할 수 있으므로 도구에서는 문자열로 반환하고 허가 파일은 native 정수와 정확히 대조한다.

## 최종 처리

task를 저장하고 **해당 소유 Editor만** 정상 종료한 뒤 새 요청 키로 같은 결과 엔진을 수집하고 위 절차로 게시한다. 정상 종료 중 마지막 소스 쓰기도 포함한다. 원본·peer Editor는 유지한다. 이후 명시적 `workspace release --id <task> --home <home>`가 최종 소스 보존·seal·소유 회수를 수행한다. `checkpoint --release`는 정상 종료·같은 수집·독립 보존/회수의 결합이며 원본 반영은 하지 않는다. release는 통합 승인이 아니다. 검증용 원본 종료와 project/Library 최종 restore는 모든 dependent 회수 뒤 별도로 승인한 정리 단계다.

기존 제한을 유지한다. bounded 일반 파일 manifest·지원 metadata, 기록한 RI 입력의 Windows/NTFS/WinFsp 자격만 대상이다. Play/Player build·대형 입력 일반화·다른 Unity 버전/OS·전원 장애는 미검증이다. 필수 RI와 최종 전체 build gate는 검증 문서에 기록하기 전까지 남은 의무다.

## 구현 소유 경계

| 경계 | 코드 |
| --- | --- |
| 결과·실행 중 원본 계약 | [Core 결과](../../src/Splinter.Core/WorkspaceGenerations.cs), [원본 계획/상태](../../src/Splinter.Core/OriginalApply.cs) |
| task 요청·이력·최종 종료 | [Runtime 결과](../../src/Splinter.Runtime/WorkspaceGenerations.cs), [원본 반영](../../src/Splinter.Runtime/WorkspaceOriginalApply.cs), [상태 저장소](../../src/Splinter.Runtime/WorkspaceStore.cs) |
| 영속 범위 수집·회수 보호 | [저널 수집](../../src/Splinter.Runtime/JournalSourceCapture.cs), [NTFS 연결](../../src/Splinter.Platform.Windows/NtfsSourceGenerations.cs), [backing alias](../../src/Splinter.Platform.Windows/WindowsJournalFilePins.cs) |
| 독립 payload·경로별 게시 | [파일 결과](../../src/Splinter.SourceControl.Files/FileSourceGenerations.cs), [파일 게시](../../src/Splinter.SourceControl.Files/FileOriginalApply.cs), [Git guard](../../src/Splinter.SourceControl.Git/GitOriginalPublicationGuard.cs) |
| 외부 협력 접수 | [Unity 정책](../../src/Splinter.Application.Unity/UnityProjectPolicy.cs), [native Editor identity](../../src/Splinter.Platform.Windows/WindowsOriginalEditorIdentityVerifier.cs) |

[수명주기 지도](lifecycle-optimization-map.md)·[구조 색인](../lifecycle-optimization-map.json)의 C20/A20/A30·K32–K35가 이를 연결하며 이전 성능 근거는 별도로 유지한다.
