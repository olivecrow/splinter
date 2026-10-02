# Unity VCS 사용 환경의 Splinter 병렬 작업

2026-10-02 사용자가 다음 개발 단위의 실행을 요청했다. **외부 VCS가 있건 없건 Splinter 자체가 병렬 작업을 제공하며, Unity VCS/Plastic SCM의 관리 정보·등록·잠금이 원본/peer 보호와 저장 공간·메모리·병렬 작업을 방해하지 않게 하는 것**이 목적이다. VCS 기능의 확장이나 check-in 성공을 완료 기준으로 삼지 않는다.

## 이번 범위

Windows x64·로컬 NTFS의 작은 Unity 프로젝트와 설치된 Plastic CLI `11.0.16.9915`, Unity `6000.3.22f1`을 첫 입력으로 삼는다. `splinter-unity`는 기존 Git fixture로 보존한다. 사용자가 `C:\Users\olive\Desktop\Development\splinter-unity-plastic`을 준비했고 정확한 root/GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`·regular/static 등록을 읽기 전용으로 확인했다. 기존 등록된 다른 프로젝트는 검증 입력으로 사용하지 않는다.

- 명시적인 Plastic 사용 환경 선택을 요청·상태·digest에 고정한다. 일반 폴더로 fallback하거나 내부에서 Git을 초기화하지 않는다. 현재 상태 v2와 기존 Git/none 경로의 guard를 유지한다.
- 공통 계약/fake부터 생성·같은 작업 재개·독립 편집/소유 Editor·안정된 checkpoint·독립 결과·release·명시적 discard를 연결한다. Core에는 Plastic 전용 API를 넣지 않는다.
- 원본의 `.plastic` 관리 정보와 등록·잠금을 A/B가 공유하지 않도록 처리한다. 첫 후보는 원본의 VCS 등록을 읽기 전용으로 확인하고, 관리 영역을 제외한 불변 소스와 private delta를 사용하는 등록하지 않는 파일 작업공간을 연결한다. 관리 root 읽기/열거·생성/rename을 차단하고 home의 VCS 상속/Plastic 등록 겹침을 거부한다. 실제 client/Unity의 프로젝트 발견 동작으로 이 경계를 검증한다. 원본/전역 설정·서버·인증·기존 등록을 변경하는 우회는 하지 않는다.
- 최초 전체 checkout/복제·읽기 hydration·파일 전체 copy-up을 추가하지 않는다. 생성/읽기와 명시적 독립 결과 보존의 payload 비용을 구분하고, 추가 관리 정보/인덱스·delta·결과·임시 할당·소유 working set을 관측한다. 공유 VSS의 작업별 귀속은 unknown을 유지한다.
- 입력/출력 schema·provider/fake 적합성·CLI/Host/Windows 권한 경계·capabilities·AI 사용 지침·기능 문서·검증표를 함께 갱신한다.

원본 반영·Plastic update/checkout/check-in/branch/merge 자동화·Gluon/partial/dynamic workspace·혼합/중첩 VCS·대형/ARM/OS 장애 전체·개발용 view 확장은 이번 단위에서 제외한다. 원본 반영은 부작용 전에 명시적으로 거부하며 기존 일반 폴더 AP 자격을 Unity/Plastic에 승계하지 않는다. Editor의 컴파일/Play/Test/빌드·도구 인계의 전체 자격은 AI와 외부 도구의 별도 검증이다.

## 실제 실행의 질문과 종료 기준

질문은 **Plastic으로 관리하는 작은 프로젝트에서 Splinter A/B가 원본 전체 복제 없이 독립적으로 작업하고, 원본을 계속 편집하면서 A 결과/회수와 B 지속 작업/폐기를 안전하게 끝낼 수 있는가**다. 새 장애 harness나 전체 행렬을 만들지 않는다.

1. client 버전·원본 workspace 경로/GUID/유형·Unity 패치·원본 내용/관리 영역·기존 등록과 실행 중 Editor·project/home·기존 서비스 grant/ledger/snapshot·여유 공간을 읽기 전용으로 확인한다. Plastic 계정/저장소 생성과 check-in은 사용자의 준비 단계다.
2. 현재 후보의 변경 계약을 자동 적합성으로 확인한 뒤 실제 A/B 한 묶음을 실행한다. A/B의 반환 경로·서로 다른 key/ID·불변 기준·private delta·정확한 소유 Editor와 같은 요청 재개를 확인한다. 원본 이후 변경과 peer는 보존한다.
3. 원본의 Plastic 관리 영역을 작업공간에서 읽거나 변경할 수 없고, 작업공간에서 기존 원본/다른 Plastic workspace를 선택하지 않는지 확인한다. 새 client 등록/잠금·공유 cache/전체 payload 복제를 만들지 않는 후보라면 그 부재를 확인한다. 추측이나 fixture-only 성공은 실제 자격으로 승격하지 않는다.
4. A의 정상 stop/checkpoint/release 후 독립 결과의 실제 bytes를 검증하고 B의 편집/Editor가 지속하는지 확인한다. 실패 또는 dirty B는 정확한 명시적 intent로 discard한다. 결과 seal·mount/provider/delta·소유 snapshot/자원 회수와 원본/peer/기존 VCS 등록 보호를 확인한다.
5. 기존 Git/none/AP 자동 회귀와 필수 빌드를 통과시키고, 첫 후보의 비용·성공/실패·재시도·제한을 기록한다. 입력/서비스 변경이 필요하면 정확한 패키지·경로·유지할 등록/보호 기록과 쓰기를 계획한 뒤 관리자 Apply를 실행한다. 지원 경로의 원본/peer 훼손·승인 쓰기 손실은 수정하고 나머지 미자격은 명시한다.

제품 CLI와 자동 적합성을 연결하고 고정한 [C: 실제 실행 계획](plastic-validation.md)의 A/B를 실행했다. C: 기존 비소유 VSS 보호로 A release·B discard의 snapshot 회수는 Partial이며 작업 기록과 snapshot을 보존 중이다. 사용자가 다음 작업 실행을 요청한 뒤 [별도 T: 계획](plastic-reclamation-validation.md)의 한 묶음에서 생성·관리 영역·같은 요청·Editor·독립 결과·B 지속·A release/B discard와 마지막 snapshot 회수를 확인해 이번 단위를 **scoped-complete-with-limitations**로 마감했다. [검증 기록](../verification/plastic-compatibility.md)이 각 입력의 판정과 비용을 구분한다. C: 보류를 해소했다고 주장하지 않으며 외부 Unity CLI의 A/B 발견 실패도 미자격으로 남긴다. MCP/CLI 전체 인계 검증·원본 반영은 별도 후속이다.
