# 개발 CLI와 선택적 저장 경로

[English](../development-cli.md)

제품 진입점은 `scripts/splinter.ps1`이며 NTFS 작업공간 생성/재개·소스 보존/통합·회수/폐기와 소유 Unity Editor의 start/status/stop만 제공한다. 컴파일·refresh·Play·테스트·빌드·캡처와 합격 판단은 에이전트와 외부 도구가 수행한다. `checkpoint`는 안정된 데이터를 보존하며 작업 내용의 성공을 선언하지 않는다.

개발·선택 기능은 별도 `scripts/splinter-dev.ps1`과 `tools/Splinter.Development`에서 발견한다. 같은 필수 빌드로 준비하며 자동 설치·실행·정리는 없다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter-dev.ps1 capabilities
pwsh ./scripts/splinter-dev.ps1 schema view.execute
```

| 개발 경로 | 범위 |
| --- | --- |
| `workspace ensure --storage git-checkout` | 명시적 Git checkout 진단 경로. 제품 스냅샷 실패의 fallback으로 사용하지 않음 |
| `view --input` (`view.execute`) | [불변 소스·공통 Library](view-workflow.md)의 명시적 수명 관리. 자체 refresh/검증 수집·합격 판정·worker 결과 보존의 중복 경로는 없음 |
| `snapshot-spike --input` | [독립 overlay 실험](snapshot-overlay.md)과 후보 게시. 제품 작업공간 명령과 구별 |
| `preparation-probe --input` | [준비 중단 개발 시험](ntfs-workspaces.md#개발용-준비-중단-시험)의 한정 gate |

기존 replay/write·저널/kernel/제품 쓰기 복구 도구와 시험은 해당 문서에서 실행 조건을 확인한다. 제품 capabilities는 이 개발 목록을 광고하지 않으며 별도 개발 진입점만 알린다.

view와 열린 cache가 의존하는 snapshot은 제품 release/discard에서도 보호한다. 현재 계약의 workspace·후보·cache·영수증만 읽으며 지원하지 않는 필드·형식, 손상·미완료 요청은 보존하고 명시적으로 거부한다. 같은 요청의 중단 복구는 해당 intent와 receipt로 수행한다.

verification의 Unity·WF/LB 실행 기록은 명시한 후보·입력의 근거다. 현재 계약에서 변경된 외부 도구 연결이나 native 실행 자격으로 자동 승계하지 않는다.

[기준본 복귀의 한정 계측](verification/journal-base-reversion.md)은 `JournalBaseReversionEvidenceTests`·`NativeEvidenceTests`·`ProductEvidenceTests`의 명시적 환경 입력으로 수행한다. 일반 실행은 gate/실제 제품 시험을 자동 활성화하지 않는다. 고정 입력과 새 evidence 경로에서만 실행하며 제품 명령 발견에 별도 probe를 추가하지 않는다.

[다수 파일 시간 계측](verification/journal-base-reversion-optimization.md)은 `JournalBaseReversionManyFileEvidenceTests`의 `SPLINTER_BASE_REVERSION_MANY_FILES` JSON 입력으로만 활성화한다. input은 정확한 source/revision/owner/receipt/output/variant/files/profiles를 고정하며 `before`는 소유 Git 기준 ref를 보존하고 `after`가 전후 내용 검증 뒤 그 ref만 release한다. 입력이 없는 필수 build는 이 실제 계측을 실행하지 않는다. 사전에 [파일 수·쓰기·소유 기준·종료 조건](plans/journal-base-reversion-optimization.md)을 고정하며 기존 evidence를 덮어쓰거나 실패한 기준을 자동 회수하지 않는다.
