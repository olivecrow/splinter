# 개발 CLI와 선택적 저장 경로

제품 진입점은 `scripts/splinter.ps1`이며 NTFS 작업공간 생성/재개·소스 보존/통합·회수/폐기와 소유 Unity Editor의 start/status/stop만 제공한다. 컴파일·refresh·Play·테스트·빌드·캡처와 합격 판단은 에이전트와 외부 도구가 수행한다. `checkpoint`는 안정된 데이터를 보존하며 작업 내용의 성공을 선언하지 않는다.

개발·선택 기능은 별도 `scripts/splinter-dev.ps1`과 `tools/Splinter.Development`에서 발견한다. 같은 필수 빌드로 준비하며 자동 설치·실행·정리는 없다.

```powershell
pwsh ./scripts/build.ps1
pwsh ./scripts/splinter-dev.ps1 capabilities
pwsh ./scripts/splinter-dev.ps1 schema view.execute
```

| 개발 경로 | 범위 |
| --- | --- |
| `workspace ensure --storage refs-cow` / `git-checkout`, `storage probe`, `baseline prepare/seal/release` | [선택적 ReFS 기준본](storage-and-resources.md)과 진단 경로. 제품 NTFS 실패의 fallback으로 사용하지 않음 |
| `view --input` (`view.execute`) | [불변 소스·공통 Library](validation-workflow.md)의 명시적 수명 관리. 자체 refresh/검증 수집·합격 판정·worker 결과 보존의 중복 경로는 없음 |
| `snapshot-spike --input` | [독립 overlay 실험](snapshot-overlay.md)과 후보 게시. 제품 작업공간 명령과 구별 |
| `preparation-probe --input` | [준비 중단 개발 시험](ntfs-workspaces.md#개발용-준비-중단-시험)의 한정 gate |

기존 replay/write·저널/kernel/제품 쓰기 복구 도구와 시험은 해당 문서에서 실행 조건을 확인한다. 제품 capabilities는 이 개발 목록을 광고하지 않으며 별도 개발 진입점만 알린다.

현재 형식의 workspace·후보·cache·영수증·기존 검증 증거는 자동 이관하거나 삭제하지 않는다. 저장된 검증 증거는 과거 기록으로만 남고 새 명령의 합격 조건이나 현재 코드의 성공 판정에 쓰지 않는다. view와 열린 cache가 의존하는 snapshot은 제품 release/discard에서도 계속 보호한다. 읽을 수 없는 형식·손상·미완료 요청은 보존하고 명시적으로 거부한다.

기존 verification의 Unity 컴파일·ReFS·WF/LB 실행 기록은 당시 후보의 근거다. 이번 축소 이후의 외부 도구 연결이나 native 실행 자격으로 승계하지 않는다.
