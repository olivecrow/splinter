[ST-79–82](verification/scalable-storage-throughput-followup.md#st-7982)는 Core 선택 `IWorkspaceMutationBatch`·제한된 혼합 native callback 진입·변경 없는 extent 저장 생략·private shadow 재생 batch를 연결한다. 영속 ACK·root/native/소유 증명·형식은 유지한다. 국소 106개 통과/선택 skip 3개이며 1k 교대 쓰기 구간은 개선했으나 일반 전체 블록 시간은 변동/악화했다. 보존 100k·전체 수명주기 자격은 추가하지 않는다.

# 대규모 소스·결과·metadata 저장

[ST-65–68](verification/scalable-storage-throughput-followup.md)는 요청 범위의 native catalog/하위 트리 증명·바이트 총량 결합, retained root 증분 갱신, 실제 과거 참조 포함 증명 뒤 중복 집계 생략, 게시 전후 독립 재검증하는 구성/의존성 증명을 추가한다. 형식·GC·ACK는 유지하며 작은 시험 26개는 대형·전체 수명주기 성능 자격이 아니다.

[ST-56–60](verification/scalable-storage-throughput-followup.md)은 참조 작업공간에 맞게 수집을 바꾼다. 파일 manifest v4는 변경 전후 의존성과 기존 보존 내용만 보관하며, 평탄한 작업 인덱스를 다시 만들지 않고 기존 디렉터리 root를 비교한다. 결과 descriptor는 최대 64개씩 게시하고 수집 중 별칭 정리는 release의 보존 결과 유지관리로 옮긴다. 고정 결과 읽기는 덮이지 않은 기준 범위만 읽고 목록은 검증한 하위 metadata를 재사용한다. 전체 수집/게시 검사와 영속 ACK는 유지한다. 이전 파일 manifest v3는 보존·거부하며 task/basis/overlay·SPM2·명령 필드는 유지한다. 작은 None 근거를 대형/Unity/Plastic 자격으로 승계하지 않는다.

[처리량 후속](verification/scalable-storage-throughput-followup.md)은 작업 범위의 정렬 spool·payload index 재사용·제한된 영속 게시를 추가한다. Adoption은 최대 64개 frame, packing은 relocation 63개와 해당 progress frame을 묶는다. Raw range 객체/참조는 최대 64개를 정확한 native source fence 아래 게시하며 backing pin intent는 link·결과 root보다 먼저 영속화한다. 전체 독립 hash와 게시 경계는 유지한다. Payload hash는 제한된 worker마다 취소 가능한 1MiB 버퍼를 사용하며 metadata cache와 별도다. 파일 수·총 바이트·이력 상한이 아니다.

[English](../scalable-storage.md) · [계획](plans/scalable-storage.md) · [SCAL-01–08 검증](verification/scalable-storage.md)

[ST-36–40 후속](verification/scalable-storage-throughput-followup.md#st-3640-남은-직렬-처리와-순회-비용)은 저널 배치를 component 이름·계층 namespace 삽입·WinFsp Create까지 확대한다. 최대 64개 변경이 조상 갱신을 공유하며 깊은 경로는 기존 반복 경로를 유지한다. Root 공개 전 pending 페이지를 flush하고 성공한 Create 핸들은 영속 완료 뒤 mutation gate 안에서 보존한다. 원본 진행 기록은 최대 64개 대기 항목을 페이지 flush·제어 저장 각 한 번으로 묶되 둘 다 성공하기 전에 ACK하지 않으며 실패하면 대기/후속 접수를 차단한다. 조밀한 반영 이력과 보존 부모 집합은 정렬 병합하고 희소 이력은 개별 갱신을 유지하며 원본 전체 scan은 남는다. Metadata graph 방문/relocation은 공통 scratch와 sort-run 상한 아래 항목당 512/256바이트를 계상한 정확한 작업 범위 캐시를 사용한다. 부족하면 최초 디스크 frontier/map fallback을 유지한다. 전체 page/root/native 증명·같은 요청 복구·소유 확인 후 삭제를 유지하며 명령·capability·영속/wire 버전은 바꾸지 않는다.

**후보 구현이며 필수 자격 검증은 진행 중이다.** 현재 후보 형식은 task v4·관리형 basis v3·제품 overlay v3·metadata 저장소 형식 2(`SPM2`)다. 직전 task-v4 후보의 `SPM1` 저장소를 포함한 이전 상태는 보존·거부하며 일치하는 바이너리로 마무리한다. 과거 RI/MX/GCO 근거는 당시 후보·입력의 자격이다. 필수 gate와 최종 build 뒤 검증 소유 문서에서 완료를 판단한다.

[ST-44–48](verification/scalable-storage-throughput-followup.md#st-4448-metadata-레코드-중복-검토)은 조밀한 페이지를 검토하고 작은 값 codec을 구현한다. 디렉터리 record v2는 flag를 묶고 0인 생성/접근 시각을 생략하며 유효한 시각·Unix mode·source reference·경로 정책을 모두 보존한다. 게시 변경 tag 4/5는 Before 전체와 Before/After에 대한 필드 차이를 저장하며 속성·nullable Unix mode도 정확히 복원한다. native identity는 별도다. 저장소 identity/container reader·회수/중단 audit·읽기 전용 replay·Plastic admission 모두 `SPM2`를 요구한다. treap node/page 배치·`SPN1` 논리 hash 알고리즘·root schema v1은 유지하고 변경한 값 바이트는 새로운 정확한 논리 root/digest를 만든다. 혼합 형식 append·이관·이전 저장소 회수는 없다. 작은 제어/CLI JSON과 task/basis/overlay schema는 유지한다.

파일 비교/반영 이력 spool은 checksum으로 검증하는 바이너리 metadata를 사용하며 경로는 spool key 한 곳에만 둔다. 디렉터리 목록 정렬 key는 원래 대소문자를 유지하고 모든 run/merge에서 불변 경로 정책의 comparer를 사용한다. 값에는 고정 metadata만 저장하여 이름/전체 경로를 중복하지 않는다. 가용성·overlay 우선순위·숨김/제외 경로·고정 root 검사를 유지한다. 여러 항목을 한 페이지에 넣는 변경은 보류했으며 현재도 키 하나당 node다. 트리 깊이나 대형 전체 시간 개선을 주장하지 않는다.

2026-11-01 사용자가 남은 검증을 축소했다. 대형 검증은 상태를 보존하고 종료했으며 실제17GiB·10만 전체 게시/회수·정상 ABBA·최신 전체 build gate는 미자격이다. [느린 구간 기록](verification/scalable-storage-throughput-diagnosis.md)에 작은 파일 입력의 수 시간 실행을 남겼으며 현재 대형 프로젝트 사용성과 작은 입력 성능 유지는 입증하지 않았다.

후보는 프로젝트 전체 파일/디렉터리 수·경로 합계·평면 결과/계획 JSON·고정 저널 예산을 디스크 인덱스·순차 처리로 대체한다. 제품의 `--max-journal-bytes` 옵션은 없다. 파일시스템 경로/파일/용량 제약·미지원 metadata·checksum/identity 검사·실제 I/O 오류는 유지한다. 필요한 고유 데이터와 불변 이력은 증가할 수 있으며 상한 제거가 일정한 디스크 사용량·처리 시간을 뜻하지는 않는다.

## 표현과 처리 단위

현재 소스 순회는 열거한 leaf를 매번 한 번 새로 관측하여 종류·길이·시각·leaf 경로 검사를 결합한다. 상위 경로는 여전히 구성요소마다 현재 속성을 한 번씩 검사하며, 내용 hash 뒤에도 새 metadata 관측을 검사한다. 관리형 None 준비는 원본 이동 전에 ordinary native root/상위/디렉터리 핀 아래 중첩 VCS·미완료 게시 이름 검사를 전체 NTFS admission 순회에 결합한다.

활성 paged journal writer는 복원한 node를 최대 512개·object/path 추정 계상 1MiB로 유지한다. Metadata page cache와 별도이며 어느 쪽도 RSS 상한이 아니다. Root 교체·게시하지 않고 버린 admission·native container fence 세대 변경 시 무효화하고, reader 퇴출·append writer 닫힘도 세대를 바꾼다. 읽기 전용·derived·write-shared reader는 기존 검증 조회를 사용한다. 고정 cursor는 자신의 정확한 record를 유지하며 원본 가용성·poison·page/native 검사·내구 ACK 경계는 유지한다. 원본/기준본/작업공간 전체 순회는 남으며, [준비 검증](verification/scalable-storage-throughput-followup.md#st-7578-준비-경로와-node-조회-비용)은 작은 비교·재개·첫 작업의 대형 관측을 구분한다.

디렉터리 항목에는 이름·종류·필수 metadata·내용/하위 참조를 기록한다. 논리 바이너리 페이지는 최대 64KiB이며 목표 64MiB append 컨테이너에 묶는다. 전체 프로젝트 상한이 아니라 처리 단위다. 논리 hash는 물리 주소·재배치를 제외한다. 디스크 정렬·경로 interning·cursor로 전체 catalog를 메모리에 합치지 않는다. Git/Plastic 목록은 순차 읽으며 Git 대형 blob의 chunk 검증 hash metadata는 전용 디스크에 저장할 수 있다.

검증된 Plastic reader는 하나의 직렬 private 자식에서 최대 64개 exact 작은 revision·decoded 합계 4MiB를 묶어 읽는다. 각 record의 ID·segment·길이·MD5와 tree의 영속 SHA-256 chunk proof를 유지하고 더 큰 파일은 일반 segment로 순차 처리한다. 이 bounded payload 묶음·vendor packet/decoder buffer·자식 전체 private memory는 metadata cache 계상과 별도다. 같은 hash로 다른 revision이나 수정 가능한 원본 파일을 대체하지 않는다.

선택적 Core `ISnapshotAvailabilityScope`는 이미 열린 불변 reader의 정확한 snapshot/read access에 관측을 결합한다. Windows Plastic reader는 전체 접수 증명 전에 manifest·native basis/catalog 디렉터리·모든 물리 catalog 파일의 쓰기와 삭제를 차단한다. 이후 heartbeat는 전체 항목을 반복 순회하는 대신 해당 native 경로·identity·길이·물리 namespace를 재검증한다. 읽기/게시 경계의 전체 manifest 증명과 일반 관측·미자격 Git/None reader의 기존 전체 검사는 유지한다. 소유 영수증을 만들거나 catalog를 채택하지 않는다. 연결한 native 핸들 결합 자료는 물리 metadata 파일 수에 비례하며 페이지 캐시 예산과 별도로 계상한다.

Git 데이터 record 명령은 변경 없는 전체 tree 확인이 끝날 때까지 출력이 없을 수 있다. 이 명령은 blob protocol의 30초 출력 읽기 제한 없이 순차 읽으며, 사용자 취소는 정확한 소유 명령을 종료·drain하고 종료 코드·잘림·개별 record 크기를 검사한다. 고정 object reader의 기존 30초 blocking-read 보호는 유지한다. R19 결합 focused 실행은 187행 중 186개 통과이며 skip 1개는 통과한 필수 부모가 실행한 subprocess actor 진입점이다. 실제 대형 입력의 후속과 최종 전체 gate는 별도다.

2026-11-01 사용자가 승인한 자원 정책은 이전 32MiB 캐시 상한을 대체한다. `StorageResources`는 가용 메모리의 1/8을 128MiB–4GiB로 제한해 프로세스별 작업 목표를 정한다(가용량 미상은 512MiB). 사전 확보 없이 수요에 따라 metadata 캐시에 목표의 5/8, 공통 정렬/spool scratch에 1/8을 계상한다. 목표 4GiB에서는 각각 2.5GiB·512MiB이며 개별 sort run/spool은 최대 128MiB/64MiB이고 개별·공통 예산 부족 시 디스크로 넘긴다. Merge 버퍼와 64KiB fallback sort run(또는 더 큰 유효 record 1개)은 공통 scratch 밖의 제한된 작업 메모리로, 중첩 cursor의 대기 교착과 record마다 spill 파일을 만드는 증폭을 피한다. 사전 예약·OS working-set·여러 프로세스 합산 상한은 아니다. Payload 버퍼·GC·native 결합 자료에는 별도 여유가 필요하다. 입력 전체 tree fence와 원본 전체 대상 잠금은 고정한 대상 수에 비례할 수 있으며 실제 대형 입력의 private memory는 미자격이다.

각 metadata 저장소는 자원 정책에 따라 container reader 핸들을 16–64개(4GiB 목표에서는 48개) 명시적 접근 순서로 유지한다. 핸들 사용·캐시된 페이지 hit도 해당 핸들의 최근 접근을 갱신하며 가장 오래 사용하지 않은 핸들을 축출한다. append writer와 공유하는 핸들은 페이지 캐시 없이 재사용하며 매번 현재 container prefix·길이·페이지 checksum·논리 hash·개수·이전 참조를 검사한다. 쓰기를 배제하는 핸들만 페이지 캐시를 허용한다. 캐시 무효화는 프로세스의 무관한 캐시 전체 대신 선택한 scope/container partition만 직접 방문하고 partition/list 색인 비용도 캐시 계상에 포함한다. Eviction·Dispose가 정확한 핸들을 해제한다. 결합한 source-change view와 inspection proof는 해제 가능한 reader 참조를 소유한다. Record 복사는 독립 참조를 유지하고 cursor는 접수한 reader를 유지하며 완료·실패 시 같은 프로세스의 회수 전에 닫는다.

`BoundedParallel`은 일반 파일 열거·취소 가능한 hash·정렬 catalog 소비를 겹쳐 실행해 hash 묶음 전체의 완료를 기다리지 않는다. Hash worker는 논리 CPU 수에 따라 1–32개, 독립 payload I/O는 CPU 수의 두 배를 8–32개로 제한한다. 고정 Plastic revision stream은 직렬을 유지한다. 조기 Dispose와 실패는 접수한 작업을 취소하고 모두 join한다. 디렉터리 목록은 반환된 불변 metadata를 재사용하되 가용성·overlay 검사를 유지한다. 저널 묶음은 node와 변경 경로 색인의 삽입/갱신 경로를 영속 ACK 전에 결합하며 일반 파일 변경 검사는 정렬 catalog 병합과 변경 집합 일괄 생성을 사용한다. 영속 형식과 전체 게시 증명은 유지한다.

오프라인 저널 재생은 원래 인덱스에 쓰지 않는다. 파생 페이지는 같은 논리 저장소 identity와 겹치지 않는 container 범위를 가진 별도 scratch 저장소에 기록한다. 소스 페이지를 제자리에서 검증해 읽으며, 파생 저장소를 만들 때 소스 catalog나 파일 payload를 복사하지 않는다. 파생 페이지 쓰기 전에 native 디렉터리·제어 파일 identity와 영속 소유 기록을 확정한다. 접수한 cursor lease는 마지막 reader가 끝날 때까지 파생 저장소·소스 reader·소유 기준본을 유지한다. 복구는 활성 reader를 배제하며 검증한 소유 scratch만 회수하고, 미확인 생성 간격은 delta 밖에 보존한다.

WinFsp 디렉터리 읽기는 열린 핸들마다 고정 cursor 하나를 유지하고 native 출력 packet 전체를 직렬화한다. 버퍼에 담기지 않은 한 항목은 다음 packet에서 재전달한 뒤 진행한다. 재시작/seek·명시적 pattern 변경은 새 cursor를 고정하고, continuation의 pattern 생략은 기존 cursor를 유지한다. EOF·Cleanup·Close·오류에서 reader를 정확히 해제하며 전체 디렉터리 native buffer·프로젝트 크기 배열은 추가하지 않는다. 설치된 WinFsp.Net 2.2.26215 구성 요소 시험은 실제 256바이트 native buffer로 생성 항목 10,000개의 선형 방문·overflow·동시 변경·재시작·구두점/dot-marker·reader 회수를 확인했다. 이는 별도 100,000항목 실제 mount 자격의 완료가 아니다.

작은 제어 JSON에는 수명·진행 중 요청·root를 둔다. 결과/요청/반영 이력·마지막 반영 경로·계획/선택/진행 상태·대량 Library 준비/복원 목록은 페이지 인덱스를 사용한다. CLI는 미리보기/개수 또는 root에 결합한 페이지를 반환하며 다른 root의 cursor는 거부한다.

## 접수된 쓰기와 불변 결과

제품 overlay v3는 SJS1 제어 아래 segment로 나눈 raw SJR2 frame을 사용한다. 16MiB/4,096-frame append 처리 단위가 차면 다음 segment와 고정 metadata root로 넘어간다. 수집 경계에서는 접수한 쓰기·root·보호를 영속화하고 payload 읽기/hash·독립 보존은 경계 밖에서 수행한다. 같은 요청은 최초 고정 결과를 반환하며 이후 편집은 실행 중 workspace에 남는다.

같은 순번의 checksum `capture-freeze` frame은 `capture.json`·pin보다 먼저 미완료 수집 인덱스에 조각낸 제어 root를 기록한다. 복구는 이 인덱스를 먼저 재생하고 정확한 누락/잘린 수집 제어 prefix를 복구한 뒤 GC 전에 pin을 복원한다. 외부 header가 없어도 다른 저장소의 고정 인덱스 참조를 보호한다. 수집 완료는 materialized marker/alias 정리 전에 배타적 native reader fence를 요구하며 불변 과거 결과는 결과 저장소로 읽는다. live 인덱스 전환·닫힌 segment 회수도 이 보호와 경계 밖 제한 복사/tail 재생을 유지한다.

접수된 소스 seal은 실제 checksum 소스 frame·접수한 소스 frame 바이트·순번을 연결한다. 같은 순번의 CAS 채택·journal forget·segment/index 재배치·회수는 이 논리 seal을 유지한다. 물리 backing identity·hash·페이지·정확한 inventory는 별도 증명이며 논리 identity가 같다고 변경/비소유 자료를 삭제하지 않는다. `physicalJournalBytes`와 접수된 `sealJournalBytes`는 다른 양이다.

Library/관리형 원본 저널도 CAS reader 없이 부분 생존 closed segment를 재배치한다. 하나의 고정 외부 디스크 계획에서 물리 live 구간을 정렬/병합하고 최대 16MiB target을 순차 처리한다. 음수 data-only ID는 정상 SJR2 재생에서 제외한다. 원래 block/offset에 결합한 조건부 재배치는 동시 편집을 보존하며 target native/hash fence 뒤만 이전 alias를 정확히 삭제한다. Capture/reader가 보호한 backing은 삭제 성공까지 보존·계상한다. 접수된 packing 요청 동안 임시 COW metadata가 늘 수 있고 drain 뒤 회수하므로 임시 디스크 사용량이 일정하다고 주장하지 않는다.

Live metadata 회수는 접수된 index cursor 때문에 게시할 수 없으면 shadow 준비·재생 전에 유예한다. 강제 호출에서도 기존 회수 intent를 같은 복구 대상으로 고정하며 유예를 회수 완료로 판단하지 않는다. Reader가 drain된 뒤 접수된 쓰기 전체와 최종 caught-up root/reader 검사를 유지하며 재개한다. 긴 디렉터리 순회 중 게시할 수 없는 shadow의 반복 재구성을 피하되 reader 보호는 약화하지 않는다. Focused 자격은 아래에 기록하며 보존 중인 100,000항목 provider의 업그레이드·측정은 남아 있고 실제 속도 개선을 주장하지 않는다.

제품 overlay v3의 offline 재생은 고정 소스 인덱스를 보존한 읽기 전용 저장소로 참조하고 파생 COW 페이지만 별도 소유 scratch에 쓴다. Scratch는 같은 논리 store identity와 겹치지 않는 container 주소 범위를 사용하며 소스 인덱스에 append하거나 metadata graph 전체를 복사하지 않는다. Windows 복원 호출자는 native file pin을 전달한다. Scratch 디렉터리는 고정한 부모에 대해 배타적으로 생성하고 정확한 생성 native identity를 기록하며, 자식 작업은 허용하되 디렉터리 교체를 배제하는 read/list fence를 유지한다. Native 경로 열기는 절대 extended Windows/UNC 경로를 사용하고 기존 내용·소유권·조상 경로 검사는 유지한다.

접수된 restore cursor는 원래 journal owner가 Dispose된 뒤에도 마지막 reader가 닫힐 때까지 소스 인덱스·소유 기준본·journal backing·파생 scratch를 보존한다. 그 뒤에만 정확한 소유 scratch 회수를 수행한다. 소유권 게시 중단이나 충돌 파일은 보존하고 조회할 수 있다. Offline overlay v3의 `ObserveChanges`는 `SourceObservationUnavailable`을 반환하며 다시 결합할 변경 root는 정확한 소유 live provider에서 관측한다. 별도 overlay v2 개발용 candidate 경로는 제품 fallback이 아니다.

같은 home에서 불변 전체 내용·private 범위·metadata를 공유한다. 결과는 고정 기준본과 private chunk/slice를 조합할 수 있으며 수정 가능한 원본으로 보존 내용을 대체하지 않는다. 채택은 고정 파일/block/범위를 검사한 뒤 live 참조를 바꾸므로 이후 편집을 보존한다. 회수 전 고정 root·reader·미완료 수집·채택 진행 상태로 metadata와 backing segment를 보호한다.

전체 객체와 raw 범위 pool은 각각 정확히 같은 불변 내용을 중복 제거한다. 새 private chunk는 전체 SHA-256·길이가 같은 기존 소유 whole 객체를 참조할 수 있다. 최초 generation-basis header 게시 전의 non-Git 기준 파일은 파일 전체를 덮는 정확한 소유 raw chunk에 peer 참조를 추가해 사용할 수 있다. 조합 파일 descriptor codec v3는 이 raw 기준 선택과 보호된 외부 Git 기준 선택을 별도·상호 배타 flag로 기록한다. 이미 고정한 header/root는 바꾸지 않으며 임의의 부분 hash/하위 범위 일치로 대체하지 않는다. 따라서 이전 whole/raw 복사본은 공존할 수 있다. 저장 관측은 pool 종류를 구분하고 실제 물리 공유는 native 파일 identity 합집합으로 계측한다.

관리형 None 입력 기준본의 수명은 영구 결과 저장소와 다르다. 파일의 단일 링크 입력 보호를 유지하며 나중에는 일반 NTFS의 수정 가능한 원본으로 돌아간다. 준비는 payload 복사 없이 이동과 색인으로 수행한다. 첫 독립 checkpoint에서는 정확한 기존 소유 객체가 없는 고유 기준 파일 버전을 whole-content pool로 복사할 수 있다. A/B와 이후 결과는 그 canonical 객체를 공유한다. 입력 기준본이 활성인 동안 basis/CAS 바이트가 공존할 수 있으며, 이는 수명 분리에 따른 추가 비용이지 제거한 중복이나 전부 불가피한 데이터가 아니다. 할당 보고서는 기존 basis identity와 추가 CAS identity를 분리한다. 고정한 10만 항목 입력은 32바이트 내용 16종, 고유 논리 payload 512바이트뿐이므로 대부분의 에셋이 고유한 프로젝트의 보존 비용을 대표하지 않는다.

Git 원본 파일 게시는 HEAD/index/branch를 유지한다. 일반 수집은 파일/범위 결과와 보호한 불변 Git 기준을 보존하며 commit/tree/ref 구체화는 명시적 branch 반영에 필요할 때 수행한다. 필요한 보호 객체를 유지하고 저장소 GC·비소유 객체 삭제는 하지 않는다. Plastic도 고정 revision·등록·관리 정보·잠금 보호를 유지하며 자동 check-in은 하지 않는다.

응답 없이 진행하는 index refresh·순차 객체 생성 등 Git 데이터 작업은 고정 총시간 상한 없이 호출자의 취소를 따른다. 작은 제어 조회의 제한 시간은 유지한다. 취소·producer 실패에는 소유 subprocess만 종료하고 입출력 작업을 모두 drain한 뒤 반환한다. 타임아웃은 남은 Git 잠금 삭제나 새 task 우회의 승인이 아니다.

## 원본 게시와 유지보수

변경 전 전체 대상과 필요한 조상을 검사·고정한다. 자원 정책이 선택한 8–32개 worker가 독립 파일을 준비/게시하며 topology/동반 파일 순서·직렬화한 영속 진행 배치·실패/취소 drain을 유지한다. 기존 복구를 위해 최대 payload 1개는 staging할 수 있다. 추가 병렬 staging은 worker 수·작업 메모리 목표의 1/4·관측한 디스크 여유의 1/4로 제한하며 파일별 여유 공간 검사를 유지한다. 이는 디스크 허용량이며 사전 할당이나 RAM 사용량이 아니다. 전체 선택은 인덱스로 보존하며 미리보기는 최대 100항목/4,096 UTF-16문자로 긴 경로에서 비어 있을 수 있다. 같은 intent/digest 재시도는 경로를 생략해 고정 선택을 복구할 수 있고 새 intent의 경로 생략은 전체 범위다.

`NotContentIndexed`는 원본 경로의 로컬 색인 정책이다. 앱의 원자 저장으로 이 비트가 빠져도 Git·None·Plastic 게시에서 관측한 대상 비트를 보존한다. 검토 digest와 조건부 게시는 여전히 관측한 대상의 정확한 metadata·identity에 결합한다. Hidden/System/ReadOnly·지원하지 않는 저장 속성·내용 보호는 유지한다.

유지보수는 저장소의 배타적 회수 fence 전에 권위 root를 고정한다. workspace 저장소는 작업 잠금, 공통 metadata는 게시 잠금 아래 anchor를 먼저 읽고 보존 header 전체를 읽으며, 내용/범위 catalog는 각 연산 잠금을 유지한다. 회수 intent/영수증은 checksum 내부 protocol v2를 사용하며 이전/손상 상태를 거부한다. 위치 게시·이전 컨테이너 삭제 전에 복사 대상 페이지·native identity를 검사한다. graph 복사·checksum을 가진 단일 단계 위치 인덱스로 게시한 참조/논리 hash를 유지하면서 무참조 소유 페이지를 회수한다. 컨테이너 payload 전에 native 생성 영수증을 기록하고 삭제 때 해당 native identity를 검증한다.

명령은 `workspace maintain --id <id> --request-key <key> --scope workspace|shared|content|ranges|all`이다. 수집/반영 뒤 새 metadata가 충분할 때 예약하며 4MiB 실행 판단값은 입력 거부 상한이 아니다. 최종 release는 delta 제거·보존 결과 유지보수 뒤 공유 기준 회수 전에 강제로 수행한다. 진행 중 요청은 다른 키/범위·새 변경 작업을 차단하며 Editor 정상 종료는 허용한다. status는 회수 없이 고정 단계·다음 동작을 관측하고 완료한 과거 유지보수 재시도는 영수증만 반환한다.

미확인 무참조 scratch/소유권 생성 간격은 영속 페이지 inventory·요약 개수로 보호하며 live root/컨테이너의 미확인 교체는 계속 거부한다. `workspace read --collection maintenance-residuals --id <id> --request-key <key> --store-scope <store>`와 해당 root의 cursor로 조회한다. 저장소 범위는 `workspace-history`·`workspace-original`·`workspace-applied`·`workspace-results`·`shared`·`content`·`ranges`다. 공간/I/O 실패에는 접수한 요청/root를 유지하며 용량·접근을 복구한 뒤 같은 identity로 재개한다.

backing 회수 보류·보존 참조 요약은 `workspace read --collection deferred-reclamation --store-scope content|ranges|content-references|journal-packing|journal-indices|read-only-replay`로 조회한다. 원본 게시 scratch는 `--collection preserved-scratch --store-scope workspace-original`이며 deferred-reclamation 범위가 아니다. 저널 조회는 소유권을 정확한 overlay identity에 결합하고 디스크 기반 페이지를 사용하며 필수 수명주기 자격은 진행 중이다. `read-only-replay`는 활성·미완료·보존한 offline scratch 범위를 회수 없이 열거한다. `journal-indices`가 반환한 요청 키를 지정하면 보존한 native inventory를 페이지로 조회한다. 미확인 컨테이너 header·control 생성 간격은 회수할 delta 밖에 보존한다. 별도 중지된 복원 복구는 기존 segment 회수 영수증·고정 seal·전체 frame/control/root 증명·배타적 reader/native inventory fence에 결합한 뒤 전체 인덱스를 그대로 보존할 수 있다. `stopped-source-index-retained-intact` inventory는 소유·비소유 파일을 모두 유지하며 payload 정리나 소유권 채택을 허용하지 않는다. 활성 root·외래 교체·지원하지 않는 미완료/재배치 domain은 계속 복구를 거부한다. 선택은 `--collection selection --plan-id <digest>`다. schema의 정확한 owner/범위/root-bound cursor를 보존한다. 참조 중/회수 보류 항목은 backing 강제 삭제의 승인이 아니다.

선택적 `SPLINTER_RESULT_GENERATION_DIAGNOSTICS=1`은 task의 `diagnostics` 디렉터리에 단계별 시간을 기록한다. 기본값은 OFF이며 복구 권위를 제공하지 않고 진단 기록 실패가 성공 판정을 바꾸지 않는다. 전체 수집 요청 시간과 실제 접수 쓰기 대기 시간은 구분한다.

## 구현 소유와 자격

Runtime은 페이지/catalog/정렬/경로/범위 인덱스·segment 저널/수집/이력·불변 내용/범위 저장소·metadata 회수를 소유한다. Files는 manifest/결과·채택·조건부 게시·유지보수 root 수집, Git은 고정 객체 증명·순차 해석·필요 시 branch 결과를 담당한다. Windows는 native fence·WinFsp·디렉터리 수집·페이지 Library 복원, Core는 이식 가능한 port/root, Protocol/CLI는 schema·cursor를 제공한다. DB·daemon·새 직렬화 도구·Unity 연동 패키지는 추가하지 않는다.

진행 중 live-index 복사는 독립 segment 정리보다 먼저 복구한다. Reader 때문에 미룬 복사는 고정 요청을 유지하고 해당 catalog 변경을 막는다. 이전 순서에서 이미 확정된 변경의 복구는 모든 non-segment root·계상 값·접수 쓰기 seal이 일치한 뒤 shadow에서만 현재 없는 일반 header-only descriptor를 제거할 수 있다. 나머지 descriptor는 전체 native/길이/hash 증명과 최종 같은 시점의 root 검사를 유지한다. 원본 파일을 unlink하거나 intent를 초기화하지 않으며 비어 있지 않은 segment 회수는 이 좁은 경로로 허용하지 않는다. 실제 killed-child 시험으로 정상 순서와 이미 비어 있는 header를 회수한 경우를 검증했다.

R14 focused native 실행은 34개를 선택해 33개 통과·실패 0·부모 목록에서 subprocess 전용 actor 진입점 1개 skip이었다. 해당 부모 시험은 실제 reader 강제 종료·복구를 실행했다. Extended 경로의 원자 생성·접수된 reader 수명·소스 인덱스 native identity/길이/SHA-256 무변경과 실제 96MiB 재생 tail이 64MiB scratch 컨테이너 경계를 넘어 관리형 복원·회수되는 것을 확인했다. R17의 결합 focused 실행은 107개 선택·106개 통과·실패 0·subprocess 전용 actor 진입점 1개 skip이었다. 중지된 전체 인덱스의 그대로 보존·복구와 캐시/LRU/회수 보호를 포함하며 두 실행 모두 최종 SCAL gate가 아니다.

그 뒤 별도 실제 Git Unity 실험 정리는 R17로 기존 정확한 plan/cursor를 한 번 재개해 일반 Library로 복원했다. 계획한 파일 1,477개의 검증 상태를 유지하고 어셈블리 MVID가 앞서 Editor에서 로드한 증명과 일치했다. 원본 Git 관리 상태는 같았으며 A/B 과거 결과 6개 모두 독립 payload/Git 경계 검증을 통과했다. 이전 재생의 세 컨테이너 인덱스는 기존 소유 컨테이너와 여전히 소유 영수증이 없는 두 컨테이너를 포함해 논리 합계 156,775,860바이트를 그대로 보존했다. 인덱스 파일 7개의 실제 할당은 156,778,744바이트이고 보존 제어 자료의 추가 할당은 20,632바이트다. 전체 basis-control 디렉터리의 잔여 할당 169,801,528바이트는 다른 보존 metadata도 포함하는 별도 범위다. 보존 전후 native identity·SHA-256이 일치하며 payload 정리·소유권 채택이나 잔여량 0을 주장하지 않는다. 원본 Editor가 작업공간 회수까지 유지되었음을 먼저 증명한 뒤 수행한 별도 실험 정리다.

SCAL-01–08은 손상/구형 상태 거부·정한 백만 항목 구성 요소 입력·이전 경계·동시 수집·불변/공유 결과·중단/공간 복구·실제 100,000항목 수명주기와 작은 Unity A/B·작은 동일 입력 비용/최종 gate를 소유한다. 이 문서는 미실행 대형 입력·다른 Unity 버전·OS/앱 지원·일반 속도/RAM 절감을 주장하지 않는다. 정확한 근거·미검증·비용은 검증 기록을 따른다.
