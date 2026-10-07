# ReFS 기준본 경로 폐기

[English](../../plans/refs-retirement.md)

상태는 **폐기**다. 선택적 개발 경로·지원 확대 후보·NTFS 실패의 fallback으로 유지하지 않는다.

제거한 계약과 기존 경로의 회귀는 [제거 검증](../verification/refs-retirement.md)을 따른다.

ReFS 블록 복제 방식은 작업공간을 만들기 전에 소스와 Library의 전체 기준본을 별도로 준비해야 했다. 대형 프로젝트에서는 이 최초 전체 기준본의 저장공간·준비 시간 자체가 큰 부담이므로 제품의 저장공간 우선 요구에 맞지 않는다. 현재 스냅샷 기준 경로는 원본 전체를 복제하지 않고 불변 기준과 작업별 변경 범위를 조합하므로 이 준비 단계가 필요 없다.

폐기 범위는 `refs-cow` 저장 공급자와 공통 복제/기준본 계약, Runtime의 기준본 준비·봉인·삭제, Git의 기준본 복제/영수증, 개발 CLI의 `baseline prepare/seal/release`·`storage probe`·`--baseline`, 해당 schema/fake/실제 ReFS 시험과 Dev Drive 준비 스크립트다. 기본 CLI와 개발 CLI 모두 이 기능을 제공하지 않는다.

현재 workspace 상태는 v2를 유지한다. 기존 스냅샷 작업의 request digest와 재개·보존·회수 계약을 유지하며 폐기된 저장 모드나 기준본 준비 상태는 보존 후 거부한다. 과거 ReFS 상태·데이터를 스냅샷으로 자동 이관하거나 삭제하지 않는다.

[당시 제한된 ReFS 실행 기록](../verification/storage-and-resources.md)은 과거 입력의 근거로만 보존한다. 기존 TRX·원시 artifacts·독립 결과·snapshot의 삭제 승인을 뜻하지 않으며 현재 기능이나 다음 개발 과제가 아니다. 현재 저장 경로는 [NTFS 작업공간](../ntfs-workspaces.md), [Git 기준본](../git-project-basis.md), [Plastic 기준본](../plastic-project-basis.md), [VCS 없는 관리형 기준본](../plain-project-basis.md)을 따른다.
