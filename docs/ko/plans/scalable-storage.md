# 대규모 소스 저장과 반복 반영

[English](../../plans/scalable-storage.md) · [구현](../scalable-storage.md) · [검증](../verification/scalable-storage.md)

상태: 사용자의 구현 요청에 따른 **진행 중** 프로그램이다. SCAL-01–08이 소유하며 이전에 마감한 성능 프로그램을 자동 재개하지 않는다. 기준 소스는 development `b68eaaa9e7e78930ff7b4b0b368f137bc3e528c6`이다.

2026-11-01 사용자가 검증을 축소했다. 대형 검증 입력은 상태를 보존하고 안전하게 중단했으며 남은 10만/17GiB/ABBA/전체 gate 의무는 통과가 아닌 미완료다. [지연 기록](../verification/scalable-storage-throughput-diagnosis.md)은 관측한 단계 시간·확인된 수정·미측정 원인을 구분한다. 이 대형 실행을 자동 재개하지 않는다. 구현 활성화와 focused 자격이 아래 전체 완료 조건의 충족을 뜻하지 않는다.

남은 직렬/순회 후보는 [ST-36–40](../verification/scalable-storage-throughput-followup.md#st-3640-남은-직렬-처리와-순회-비용)으로 구현했다. 영속 진행 배치·최초 namespace/Create 배치·제한된 정확한 graph 캐시·계획 병합이며 작은 64개 검증은 대형/전체 gate 마감이나 키 하나당 node 형식 교체가 아니다.

## 확정 계약

Git/None/Plastic의 준비·편집·수집·원본 반영·회수 전반에서 프로젝트 전체 항목·경로·manifest·저널 총량 상한을 없앤다. 처리 페이지·지연 캐시·동시성은 제한하며 실제 파일시스템 용량과 metadata 제약은 유지한다. 불변 독립 결과·같은 요청 복구·원본/peer 보호·전체 게시 대상 native 잠금을 유지한다. 필요한 보존 내용을 수정 가능한 원본 경로로 대체하지 않는다.

대량 metadata는 디렉터리/이름 항목·최대 64KiB의 불변 바이너리 페이지·공유 append 컨테이너로 저장한다. 변경 없는 페이지와 내용을 재사용한다. 2026-11-01 자원 사용 승인은 초기 32MiB 캐시를 사전 확보 없는 프로세스별 최대 4GiB 작업 목표로 대체한다. 목표의 5/8은 metadata 캐시, 1/8은 공통 정렬/spool scratch에 배분한다. OS RSS나 여러 프로세스 합산 강제 상한은 아니다. 정렬/인덱스는 제한 없는 전체 배열·딕셔너리 대신 제한된 메모리와 디스크 fallback을 사용하며 정확한 정책은 [구현 문서](../scalable-storage.md)가 소유한다. 작은 요청·영수증·CLI는 JSON을 유지한다. DB·daemon·직렬화 의존성을 추가하지 않는다.

원본 전체 대상을 검사·고정한 뒤 자원 정책의 8–32개 worker로 독립 파일을 처리한다. topology/동반 파일 순서·단일 영속 진행 확정·실패/취소 drain을 유지한다. 복구용 최대 payload 1개 외 추가 병렬 staging은 worker 수·작업 목표의 1/4·관측한 디스크 여유의 1/4로 제한하며 파일별 여유 공간 검사를 유지한다. native 핸들 비용은 고정한 대상 수에 비례할 수 있다.

## 구현 순서

1. 공통 계약/fake·불변 페이지 저장소·디렉터리 metadata·범위 인덱스·손상/중단 시험.
2. Git/Plastic/입력/Library metadata와 실행 중 node/extent/hidden/change 인덱스의 페이지화·고정 수집 root·영속 reader 보호.
3. 결과/요청/반영 이력 인덱스·내용 재사용·독립 결과·정확한 참조 기반 회수.
4. 전체 잠금 아래 제한 병렬 게시·cursor CLI/schema·실제 backing 용량 표시. 대체 경로가 준비된 뒤 `--max-journal-bytes`와 연결 상한 제거.
5. native/Unity 자격·작은 동일 입력 비용 비교·양 언어 문서·최종 전체 gate.

후보 코드는 task v4·관리형 basis v3·제품 overlay identity v3를 기록한다. 이전 task/basis/overlay는 보존·거부하고 일치하는 바이너리를 사용하며 이관·fallback·회수를 추가하지 않는다. 후보 코드의 활성화가 SCAL 완료는 아니며 실제 수명주기·장애·비용·문서·최종 gate 근거를 모두 확보해야 한다. 명시적 개발 view/candidate는 각기 기록한 별도 형식을 유지한다.

## 후보 연결과 남은 자격

현재 코드는 이름/페이지 provider catalog·segment live/수집 인덱스·독립 공통 내용/범위·페이지 이력/계획/선택·전체 잠금 제한 worker를 연결한다. root-bound `workspace read`·같은 키/범위 `workspace maintain`을 제공하며 metadata 회수는 권위 root/reader fence·native 소유 영수증을 사용한다. 수집/반영 뒤 유지보수 예약은 처리 판단이고 최종 release는 강제로 회수한다. 명시적 개발 overlay2/candidate3 진단은 제품 overlay3와 구분한다.

현재 live-index/backing 회수 복구·정한 native/Unity 입력·비용/회귀 계측·최종 소스 지도 hash·build/문서 gate를 마무리한다. 각 gate는 새 후보 근거로 보고하며 필수 항목을 skip·이전 통과로 대체하지 않는다. 미확인 보호 잔여는 명시한 자료이며 자동 삭제 대상이 아니다. 현재 결과·남은 행은 검증 소유 문서를 따른다.

## 필수 gate

| ID | 필수 결과 |
| --- | --- |
| SCAL-01 | 논리 identity 재현·손상/잘림/잘못된 참조/순환 거부·이전 상태 보존/거부. |
| SCAL-02 | 계층형/단일 대형 폴더/긴 경로의 1,024 / 32,769 / 262,145 / 1,048,576항목을 전체 상주 없이 열거·조회·비교·재연결. |
| SCAL-03 | 기존 node/extent/hidden/path/JSON/journal 한도를 다른 총량 상한 없이 초과. 큰 offset 모델과 실제 대형 파일을 구분. |
| SCAL-04 | 동시 write/rename/delete/truncate/교체 중 고정 수집·고정 재시도·이후 편집 분리·선택 반영. |
| SCAL-05 | 반영 후 원본 수정에도 workspace/과거 결과 유지·중복 공유·무참조 소유 자료만 회수. |
| SCAL-06 | root/payload/게시/이력/회수 경계 중단과 공간 부족 주입·같은 요청 복구·결과 혼합/유실/이중 게시 방지. |
| SCAL-07 | 실제 Editor 없는 Git/None/Plastic 각 100,000항목 수명주기·별도 작은 Unity 원본/A/B에서 A 중간 2회/최종 1회 반영과 원본/B Editor 보존. |
| SCAL-08 | 작은 동일 입력 전후후전 비용·실제 할당/잔여·metadata/private memory/핸들·지속적 의미 있는 회귀 수정·최종 `pwsh ./scripts/build.ps1`와 문서 검사. |

베타 게시·Unity 패키지·다른 OS/앱 지원·일반 성능 주장은 포함하지 않는다. 연결된 상한이나 필수 자격이 남아 있으면 전체 완료가 아니다.
