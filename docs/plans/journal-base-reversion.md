# 명시적 저널 정리의 기준본 복귀

아래는 `25f4fa1` 당시 append/v2 compaction intent 구현의 마감 범위다. 현재 사용자 의도에 따른 [현재본 저장](latest-only-journal.md)은 별도 범위이며 v3 intent·SJL2를 사용한다. 이전 수치를 현재 후보의 자격으로 승계하지 않는다.

사용자가 승인한 구현·실제 비교 범위다. `development @ d97bd3c`에서 시작한다. 명시적 `workspace compact --revert-base-ranges true`만 고정 불변 기준과 같은 private 범위를 제거한다. append/개별 flush/ACK·정리 전 누적·자동 정리 없음은 유지한다. 기본은 OFF로 시작하며 아래 조건을 모두 충족한 경우에만 변경한다.

## 고정 비교와 판정

- OFF/ON 각각 독립 journal/workspace. 같은 compression·한도·기준·쓰기 순서. 3회, OFF/ON→ON/OFF→OFF/ON 순서. OS cache는 비우지 않으며 첫 실행/후속 warm 가능성과 변동을 보고한다.
- 합성: seed 61006, 4KiB/1MiB 범위, 각 32회 B→C→base 순환. 분산 8범위·부분 복귀·실제 잔여 변경·raw/random 및 반복 bytes. 정상 정리/재생 후 전체 바이트·entry·경로/변경 sequence를 비교.
- 실제 제품: 작은 등록 fixture와 기존 `splinter-unity`의 실제 Medieval 입력을 조사해 source/Library/파일 수를 고정한다. 큰 프로젝트는 최소 2GiB 실제 에셋의 소유 workspace를 사용하며 큰 합성 파일로 대체하지 않는다. 실제 에셋의 고정 4KiB/1MiB 범위에 동일 순환을 적용한다. Unity cold import를 불필요하게 반복하지 않고 준비 시간의 미측정은 NotRun으로 둔다.
- 변경 경계: 실제 자기 자식의 append flush 뒤 ACK 전·candidate flush·prepared intent·generation publication·previous cleanup 전/후에서 강제 종료. 외부 ACK와 같은 identity/요청 재개·bytes/metadata/seal을 확인. A 정리/복구 중 B 조회·64회 쓰기와 독립 결과를 한 묶음으로 확인한다.
- 기본값 ON 조건: 반복 입력과 큰 실제 입력 모두 최종 journal NTFS 할당 20% 이상 감소 또는 전체 정리/재개 시간 15% 이상 감소, 개선 폭이 반복 변동의 2배 초과. 전체 쓰기+정리+재개 시간 10% 초과 회귀 없음. 선택 복구/peer 보호 전부 통과. 유리한 하나의 표본/합성 결과 또는 누락한 실제 검증으로 ON을 선택하지 않는다.
- 논리/할당·payload/metadata·최종 live·old/new 겹침·base 비교 bytes/time·관리 할당/heap·provider/시스템 메모리·쓰기/정리/재생·전체 시간을 분리. volume free/shared VSS/전체 순간 peak는 절감량으로 환산하지 않는다.

## 안전성과 종료

source 구조 hash와 제거 후 기대 구조 hash를 구별한다. 제거는 현재 원본이 아닌 정확한 snapshot의 BasePath/BaseLength 안에서 읽고 검증한 부분만 허용한다. resize 후 잘린 tail·신규/삭제 재생성 파일·이름/ID/시간/속성/hidden/변경 sequence를 유지한다. 후보 전체 flush/checksum/replay·기대 hash·전후 seal·원자 게시 뒤에만 이전 세대를 회수한다. 선택은 durable intent에 고정하며 다른 옵션으로 재시도를 거부한다. old intent 형식은 보존·거부한다.

기준 부재/손상·부분 읽기 실패·내용 불일치는 삭제로 우회하지 않는다. 지원하지 않는 입력은 NotRun/unknown으로 보존하고 필요한 실제 입력/설치 준비를 구체화한다. 필수 build·schema/fake/계약·문서 검증을 끝내고 OFF/ON 근거·기본값 결정을 검증 문서에 기록한다. 설치/서비스/grant/볼륨 변경·전원/재부팅·비소유 종료·push/merge/deploy는 포함하지 않는다.

## 이번 단위 마감

2026-10-06 **scoped-complete-with-limitations**. 합성/실제 2.8GB recorded Unity 프로젝트 입력의 각 3회 OFF/ON, 합성/실제 Git 기준 강제 자식 종료 각 7경계, 실제 WinFsp editorless A/B·독립 결과·두 release를 마쳤다. 최초 A runner 접근 거부는 보존했으며 명시적 사용자 승인 뒤 같은 ID/home·저장된 ON의 공식 재개로 마무리했다. 실제 PNG 최종 저널 할당 -74.76%지만 쓰기+정리+재생 +49.41%, 반복 압축 입력은 할당 개선 0%로 기본 ON 조건에 미달한다. **기본 OFF 유지**. [검증 수치·이력·한계](../verification/journal-base-reversion.md)가 원본이다. Unity cold import·WinFsp kill·전원/OS/전체 행렬, 전체 순간 공간/RAM·공유 귀속은 NotRun/unknown으로 남긴다. 이 입력의 추가 A/B나 기본값 변경을 자동 재개하지 않는다.

이 구현은 `25f4fa1`로 커밋했다. 이후 사용자 요청의 [다수 파일 시간 최적화](journal-base-reversion-optimization.md)는 별도 범위이며 이때의 수치를 현재 최적화 바이너리의 측정으로 승계하지 않는다.
