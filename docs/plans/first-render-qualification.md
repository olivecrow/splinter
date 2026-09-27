# 첫 미리보기 검사 신뢰성

2026-09-27 확정. JW 완료 커밋 `a1c3f91` 다음의 FR-01~04 범위다. VC/VR/RU/WI/JW에서 반복된 첫 PNG 빈 화면과, PNG 저장만으로 검사 성공을 반환하는 fixture의 계약을 닫는다. 이후 전체 코드 품질 검토·리팩토링·최적화에 사용할 작은 Unity 검증을 신뢰할 수 있게 하는 한 단위다.

| ID | 확정 범위 | 완료 기준 |
| --- | --- | --- |
| FR-01 | 재현과 원인 분리 | 기존 작은 Unity와 격리 NTFS 시험 디스크를 재사용한다. 새 소유 작업의 기존 검사에서 첫/후속 렌더, pipeline·scene·mesh·카메라 상태와 실제 PNG를 기록한다. 관리 코드의 API 호출/준비 경계와 Unity 내부 원인 추정을 구별한다. |
| FR-02 | 공통 fixture 검사 수정 | `splinter-unity`의 명시적 qualification Editor에서만 동작하는 검사 코드를 수정한다. 한 요청이 유효한 실제 화면 또는 명확한 실패를 반환하고, 요청별 PNG·결과 식별자·내용 검사를 연결한다. 빈 화면·누락 에셋·저장 실패를 성공으로 처리하지 않으며 scene/카메라/임시 GPU 자원을 정리한다. 필요한 준비는 한정하고 무한 재시도하지 않는다. |
| FR-03 | 실제 첫 요청과 실패 경로 | 수정본의 별도 새 private Library로 cold import 후 첫 검사, 정상 종료/재시작 후 첫 검사, 코드 변경/domain reload 후 첫 검사를 실행한다. 기존 Library 전체 복사나 저널 되감기를 하지 않는다. 빈 화면과 누락 참조, 결과 게시 실패를 실제 fixture에서 주입해 이전 PNG가 성공으로 재사용되지 않고 후속 정상 검사로 회복하는지 확인한다. |
| FR-04 | 보존·회수·후속 검토 준비 | 새 시작의 원본 파일/ref/타 snapshot을 목록화한다. 독립 결과와 PNG/로그를 확보한 뒤 소유 Editor/provider/mount/Git 등록/delta/snapshot을 회수하고 VHD 분리 후 결과를 다시 검증한다. 재사용 fixture 코드·현재 문서·변경 기록을 갱신하고 로컬 커밋으로 마감한다. |

기존 작은 원본은 변경하지 않는다. 같은 새 snapshot에 기준/수정 작업의 독립 delta 두 개를 두고 Editor는 순차 실행한다. 기준 검사의 실패는 보존하고 수정본의 새 결과와 구별한다. 필요 시 기준 작업의 살아 있는 Editor에서 제한된 진단 변형을 먼저 확인하되, 수정본 cold 실행은 진단과 분리된 새 delta에서 시작한다. 원본/기존 결과 refs/비소유 snapshot을 보호하며 스냅샷은 두 작업의 독립 결과와 등록 해제 이후 회수한다.

판정은 고정 모델·텍스처·Unity 6000.3.22f1/URP fixture에 한정한다. 범용 이미지 의미 판별, 모든 렌더 pipeline 지원, 제품 workspace validation API 변경, Runtime flush/잠금 변경, 전체 코드 품질 검토·리팩토링, Library 물리 절감·대형 PV·기본 모드 전환은 별도 범위다. 준비/재시작 시간·작업별 파일 할당·공유 VSS·증거 저장량은 분리하며 성능 우위로 환산하지 않는다. 과거 VR-03 Partial와 첫 PNG 실패 기록은 유지한다.

## 실행 상태

FR-01~04를 `scoped-complete-with-limitations`로 마감했다. [실행 기록](../verification/first-render-qualification.md)은 새 cold·reload·정상 재시작의 첫 요청 3건, 실패 주입/복구 3종과 독립 결과 2개·원본/ref/타 snapshot 보존·소유 자원 회수·VHD 분리 뒤 결과 검증을 확인한다. 새 cold의 첫 draw 0은 보존하고 같은 요청의 두 번째 유효 PNG를 채택했다. 중간 진단 PNG의 육안 판독 정정과 stop timeout 뒤 자연 종료 재확인을 유지한다. Unity 내부 원인은 미확정이며 과거 실패와 VR-03 Partial는 바꾸지 않는다. 현재 fixture 계약은 [미리보기 검사](../unity-render-qualification.md)에 있다. 전체 코드 품질 검토·리팩토링·최적화는 이 단위 다음의 별도 작업이다.
