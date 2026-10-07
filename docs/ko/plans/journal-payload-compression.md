# 변경 범위 압축의 제품 연결 범위

[English](../../plans/journal-payload-compression.md)

질문은 **독립 codec을 실제 범위 저널에 연결했을 때 내용·승인 쓰기·재개·독립 결과·원본/peer 보호를 유지하면서 NTFS 저널 할당을 줄이는가**다. [저장공간 우선 계획](storage-priority-and-editor-modes.md#범위-payload-codec과-다음-제품-연결)의 다음 단위이며 사용자 요청으로 이 단위의 완료를 목표로 진행한다. 독립 codec 표본의 압축률을 제품 절감률로 승계하지 않는다.

JP-01~06을 [직접 실행/비용/제한 근거](../verification/journal-payload-compression.md)에 따라 scoped-complete-with-limitations로 마감했다. 아래는 이 단위의 고정 범위와 종료 기준이며 추가 A/B나 전체 SF/NR을 재개하는 지시가 아니다.

## 포함과 종료 조건

| ID | 포함 | 완료 근거 |
| --- | --- | --- |
| JP-01 | 공통 저장/복원 길이·논리 fragment 계약, raw 선택과 bounded codec 연결 | Core 계약·literal/fake 경계·Runtime의 부분 overlap/resize/rename/delete/동일 private 쓰기. 압축·raw 모두 callback당 한 frame/개별 durable flush/ACK 유지 |
| JP-02 | 현재 저널 write/read/replay, compaction/candidate와 예산 연결 | 현재 frame의 checksum/잘림/손상/복원 길이 거부, 안정된 내용/sequence/seal·같은 intent/후보 게시 복구·정리 겹침 예산. 압축 48B와 외부 checksum/metadata 비용을 포함한 물리 크기 비교 |
| JP-03 | 현재 단일 형식과 발견/schema | overlay v2·SJR2 frame·candidate v3. 이전 identity/저널/후보는 보존 후 거부하며 자동 변환/구버전 reader를 추가하지 않음. workspace 상태 v2·VSS/protocol은 유지 |
| JP-04 | 작은 실제 A/B 한 쌍 | 기존 T: Plastic 원본/home의 생성·같은 요청 재개·독립 소유 Editor·정상 stop·결과 보존·A release 중 B 지속·B 후속 변경과 release·마지막 소유 snapshot 회수. 원본/관리 정보/등록/기존 결과 보존 |
| JP-05 | 실제 공간과 메모리 비용 | 동일 metadata/논리 callback 입력의 현재 raw frame과 제품 압축 frame의 NTFS 파일 할당·payload/metadata·유효/대체 범위 비교. 추가 버퍼·관리 할당/소유 프로세스 관측·실패 비용을 분리하며 전체 peak/공유 VSS 귀속/native codec RAM은 미측정이면 unknown |
| JP-06 | 회귀와 문서 | 필수 locked restore·Release·자동 시험, 필요한 변경 경계의 기존 중단 시험, schema/UTF-8·diff와 현재 기능/검증표 동기화 |

실제 입력은 기존 `T:\plastic-fixture-20261002`, home `T:\pt1002`, `--source plastic --application unity --base snapshot --storage ntfs-snapshot`, Unity `6000.3.22f1`·기존 warm Library/Unity CLI/Pipeline이다. key는 `jp-t-a-20261003`·`jp-t-b-20261003`, 작업별 journal budget은 17,179,869,184B다. 실패를 fresh key로 우회하지 않고 정확한 요청·바이너리 hash·basis/digest/소유 PID를 실행 manifest에 보존한다. 반환 경로만 편집한다.

실행 전 read-only service/doctor·원본/관리 root hash·Plastic 등록·기존 결과/records·원본 Editor identity와 공간을 확인한다. 서비스는 이미 실행 중이고 기존 grant를 사용한다. 설치/서비스/grant/볼륨 변경이 필요하면 구체적 준비안을 분리한다. 정상 CLI/Git/Unity를 승격하거나 자동 helper로 우회하지 않는다.

각 worker의 서로 다른 작은 source marker와 `Library/SplinterPayloadQualification` 아래 bounded private 입력만 작성한다. 네 Bee 파일 표본의 원본은 읽기 전용이며 최초 전체 프로젝트 복제·읽기 hydration·파일 전체 copy-up과 구분한다. Editor의 실제 Library 쓰기와 고정 파일 client 쓰기의 비용은 각각 기록한다. 비교용 raw 저널은 동일 frame metadata/decoded payload의 한정 계측 artifact이며 작업공간·결과로 게시하지 않는다. 압축된 저널과 raw 대조의 명시적 범위를 기록하고 비용에 계측 artifact를 별도 포함한다.

새 전체 harness/장애 행렬·추가 A/B·새 source/home·원본 integrate/check-in·MCP/패키지 업데이트·Play/Test/build·대형/ARM/OS 장애·flush/group commit·새 backend·자동 모드/큐는 제외한다. C:의 기존 reclaim-pending/discard-reclaim-pending과 비소유 blocker는 그대로 유지한다. 결과가 작다는 이유로 회수 gate를 완화하거나 다른 snapshot을 삭제하지 않는다.

JP-01~06의 직접 근거가 모두 있을 때 이 연결 단위만 마감한다. 공간 증가·내용/복구/회수 실패는 실패 증거와 작업을 보존하고 해당 경계를 수정한다. 기존 native/M0/UH 실행을 현재 저널 형식의 통과로 승계하지 않는다.
