# VCS 독립 수명주기 검증

범위는 [확정 계획](../plans/implementation-plan.md#다시-확정한-다음-개발-단위-vcs-없는-기본-수명주기), 현재 구현은 [일반 폴더 계약](../vcs-independent-workspaces.md), 관문 상태는 [VCS 추적표](../plans/validation-matrix.md#vcs-없는-기본-수명주기)가 소유한다. 기존 Git 기반 M0의 완료를 이번 후보에 승계하지 않는다.

`FileWorkspaceTests`는 실제 파일 공급자와 **fake snapshot backend**를 사용한다. fixture 복사는 시험의 작은 합성 기준 준비이며 제품 NTFS의 복제/범위 쓰기 자격이 아니다. A/B·원본 이후 변경·같은 key 재개·checkpoint payload의 실제 내용과 source/snapshot/mount 없는 확인·dirty discard·기존 결과 유지·peer 보호·capture/connect/unmount/delta/snapshot 응답 유실·손상 payload 보존·seal 변화 차단·VCS 관리 정보 거부·Git 오류 fallback 부재·이전 상태 거부를 검증한다. 모든 일반 폴더 단계에서 Git adapter 호출 0을 확인한다.

`ContractAndCliTests`는 일반 폴더 입력/schema와 Git/Unity 없는 doctor를 확인한다. 기존 Git 수명주기·merge/dirty guard와 Windows 자원/저널 회귀는 필수 `scripts/build.ps1`에 포함한다. 새 `Splinter.SourceControl.Files.dll`이 runner 묶음과 서비스 패키지에 포함된다. runner는 현재 7개 파일이며 과거 6개 후보의 실제 메모리 수치를 현재 후보 수치로 재사용하지 않는다.

`pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, 794개 중 793개 통과·선택 ReFS 1개 Skipped다. 현재 file-source schema와 전체 회귀의 TRX는 `artifacts/vcs-independent/automated-final.trx`, 요약은 `verification.json`이다. 초기 schema/시험 fixture 실패 근거도 보존하며 최종 통과로 덮어쓰지 않는다. UTF-8 70개 문서와 선택한 123개 로컬 문서 링크를 확인했다.

현재 실제 일반 폴더 VSS/WinFsp·소유 Unity `6000.3.22f1` A/B·회수 후 payload 및 실제 공간/자원 관문은 **NotRun**이다. [고정 native 계획](../plans/vcs-independent-validation.md)의 새 fixture·서비스 패키지와 기존 3개를 보존하는 4개 grant의 읽기 전용 계획을 준비했다. 별도 관리자 Apply 승인 뒤 실행한다. 기존 fixture의 `.git`을 삭제/이동하지 않는다. 일반 폴더 original-apply·Plastic 전용 연동·대형/ARM/OS 장애는 제외한다.
