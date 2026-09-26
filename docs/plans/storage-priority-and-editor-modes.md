# 저장공간 우선 설계와 Editor 실행 방식 검토

검토일: 2026-09-24. **확정된 우선순위는 대형 Unity 프로젝트의 작업공간 용량 부담 해소가 1순위, Editor 동시 실행이 2순위다.** 단일 Editor 순차 검증은 유력한 후속 설계 후보이며, 기본 실행 방식으로의 채택과 구현은 아직 확정하지 않았다. SF-01의 파일 단위 소스 검증과 SF-02 계측 하위 범위는 구현했지만 [실행 단위](../verification/storage-foundation.md)는 기존 VSS 보존 실패로 마감하지 못했다. SF-03~06은 NotRun이다. 현재 제품은 작업별 독립 Editor 방식이며 새 큐·후보 전환·저널 압축은 미구현이다.

## 확정한 다음 실행 단위

2026-09-25의 [공통 저장소 개선 1차](storage-foundation-first-milestone.md)를 실행했다. 반복 대형 파일 재해시 0·저장 계측·작은 Unity A/B 결과를 확인했으나 기존 VSS 5개가 회수 중 소실됐다. [후속 재현·수정](../verification/storage-recovery.md)은 제어 파일 게시 충돌을 해결했고, 격리 VSS에서도 최신/중간 삭제 시 다른 오래된 복사본이 제거됨을 확인했다. 비강제 직접 API도 동일하며 제품 guard의 위험 요청 차단·오래된 순서 회수는 실제 통과했다. 다음 범위는 독립 결과 보존과 snapshot 삭제 가능 시점을 분리하는 지연 회수 상태·재시도·잔존 공간 비용의 계약이다. 최초 복제 없는 저장과 변경 기반 검증의 개발을 막는 결과는 아니지만 임의 완료 순서의 즉시 회수는 보장하지 못한다. SF-02 압축·flush 정책과 SF-03 단일 Editor 연결은 별도 후속 범위로 유지한다.

## 현재 근거와 개선 가능성

현재 [PV 결과](../../artifacts/parallel-workflow/pv-20260923/final-findings.md)는 제한된 관측이다. 과거 전체 팩 입력 동등성 unknown, S4 입력 실패와 복구 비용을 유지한다. 100GB 실사용 프로젝트의 성능은 측정하지 않았다. 사용자가 마감한 PV 범위를 재개하거나 C/S/Q 대용량 반복을 자동으로 추가하지 않는다.

| 항목 | 현재 근거 | 후속 판단 |
| --- | --- | --- |
| 소스 fingerprint 반복 | [파일 단위 검증](../source-verification.md)으로 변경 없는 raw/Git hash를 재사용. 제한된 실제 3회 검사에서 변경 없는 대형 payload 재해시 0 | 초기·감사·provider 재시작의 전체 읽기와 Git 객체·seal 비용은 남음. [실측](../verification/storage-foundation.md)은 일반 속도/용량 우위 보장이 아님 |
| 저널 쓰기·읽기 | [범위 저널](../../src/Splinter.Runtime/JournalDeltaStore.cs)은 저장소 내부 잠금, 기록마다 동기 flush, append-only 기록을 사용 | 경합·flush·재생·중복 쓰기를 분리 계측하고 개선한다. 완료를 응답한 쓰기의 내구성은 유지한다 |
| 필요한 범위만 저장 | [보조 합성 시험](../../artifacts/parallel-workflow/pv-20260923/synthetic-storage-final-summary.md)의 16/256/1,024MiB 파일에 각 4KiB 쓰기는 payload 합계 12,288B. 반복 읽기의 payload 복사와 read cache는 0 | 범위 저장의 기본 기능은 있다. 합성 시험의 보존 복구·정상 unmount 실패를 유지하며 일반 에셋 지원으로 확대하지 않는다 |
| Editor별 RAM·Library 쓰기 | C/S의 독립 Editor 메모리 차이는 입증되지 않음 | 파일 공유만으로 Unity 프로세스의 객체 메모리를 공유하지 못한다. 단일 검증 Editor는 이 중복을 줄일 수 있다 |
| 대형 바이너리 재저장·재임포트 | 작은 의미 변경도 도구가 파일 전체를 쓰거나 큰 Library 결과를 생성할 수 있음 | 실제 바이트 변경이 크면 저장 비용도 크다. 모든 FBX·텍스처 수정이 작은 delta가 된다고 보장하지 않는다 |
| VSS·Git·지원 범위 | VSS는 볼륨 변경을 보존하며 현재 NTFS 입력은 LFS와 사용자 filter 등을 거부 | 저널 밖 저장 비용과 복구를 포함한다. LFS가 필요한 프로젝트의 적용 관문을 별도로 통과해야 한다 |

미구현 최적화의 개선 가능성은 구조에 근거한 판단이다. 구현한 파일 단위 검증의 실측은 별도 기록하며 전체 저장량 우위로 확대하지 않는다. 사용자 공간 파일시스템의 비용, Unity 자체 임포트·domain reload, 단일 Editor 대기는 남을 수 있다.

## 공통 저장소를 중심으로 한 두 실행 방식

작업자는 Splinter가 제공하는 Git linked worktree와 snapshot/delta에서 병렬로 편집한다. 별도 Codex 일반 checkout을 중첩하지 않는다. 파일 관리 계층과 Unity 실행 정책을 분리해 다음을 비교한다.

| 방식 | 작업공간 | Unity 실행 |
| --- | --- | --- |
| 독립 Editor | 불변 기준 + 작업별 변경분 | 필요한 작업별 Editor·Library 쓰기 상태 |
| 단일 Editor 순차 검증 후보 | 같은 독립 파일 작업공간, Editor 없이 편집 가능 | 고정된 검증 경로와 하나의 Library 쓰기 상태를 가진 검증 Editor를 임대 |

두 번째 방식에서는 작업자별 Editor·임포트 캐시 생성 없이 코드 작업을 지속한다. Unity가 필요한 요청만 큐에 들어간다. Unity Agent Coordination의 임대/FIFO를 어댑터로 재사용하는 방안을 평가하며 Core에 해당 패키지 API를 넣지 않는다. 기존 PV의 C/S/Q 구분은 바꾸지 않는다. 현재 Q의 일반 worktree를 그대로 사용하면 대형 Assets checkout 문제가 남으므로 새 설계 후보와 Q는 구별한다.

원본의 지속 편집을 보호하려면 별도 검증 view를 우선 검토한다. 검증 view도 전체 복사 없이 기준 snapshot과 범위 변경분으로 구성해야 한다. 검증 Editor는 하나이며, 사용자가 원본 Editor를 별도로 열어 둔다면 전체 Editor 수는 둘이다. 기존 원본 Editor까지 공유해 전체 하나로 운영하려면 짧은 임대 동안 원본 후보 적용·원복·사용자 미저장 상태를 보호하는 별도 계약이 필요하다. 작업 기간 전체의 원본 쓰기를 막는 대안은 채택하지 않는다.

## 필요한 파일과 필요한 부분의 의미

파일 목록과 전체 프로젝트 구조는 Unity·Git에 계속 보인다. 변경하지 않은 파일의 데이터는 불변 기준에서 필요한 읽기 범위만 제공하고, 작업에서 실제로 쓴 범위와 메타데이터만 보존한다. 파일을 읽었다는 이유로 전체 파일이나 읽기 캐시를 새로 만들지 않는다. 파일 수에 따른 메타데이터 비용은 별도다.

이는 현재 씬이 참조하는 파일만 골라 나머지를 숨기는 설계와 다르다. Unity의 GUID/.meta, 패키지, importer와 동적 로딩 의존성을 불완전하게 추정하면 결과가 달라질 수 있다. 전체 논리 트리를 유지하면서 물리 중복을 줄이는 경로를 우선한다.

100GB 파일의 4KiB 범위만 실제로 썼다면 작은 범위 저장을 목표로 한다. 도구가 전체 파일을 다시 쓰는 경우에는 현재 구현이 전달된 쓰기를 모두 누적한다. 동일 블록 재사용이나 압축은 추가 읽기·비교·내구성 검증을 요구하는 별도 최적화이며 현재 capability가 아니다. snapshot 소실에도 남는 독립 결과를 만들 때의 Git 객체 또는 파일 materialization 비용도 반드시 포함한다.

## 공통 성능 개선의 조건

1. 검증된 불변 기준 manifest와 파일/범위 hash를 snapshot identity에 연결한다. 동일 Git revision이라는 이유만으로 raw 파일 동등성을 가정하지 않는다. 초기 전체 읽기가 필요하면 명시적으로 측정하고 작업마다 재실행하지 않도록 재사용 조건을 정한다.
2. 파일 추가·삭제·rename·truncate·.meta·새 파일을 포함한 변경 기록으로 검증 대상을 좁힌다. 같은 크기·mtime만으로 성공을 재사용하지 않는다. 정상 반복 편집에서 변경 없는 대형 소스의 payload 재해시를 없애는 것이 목표다. 파일 전체 또는 chunk 단위 중 어떤 경계가 필요한지 별도로 결정한다.
3. 후보의 불변 세대, 실제 컴파일/로드 세대와 검증 결과를 결합한다. 기록 누락·손상·쓰기 경쟁이 있으면 결과를 보존하고 명시적 재검증을 요구한다. 검증 생략이나 신뢰할 수 없는 cache hit로 속도를 얻지 않는다.
4. 저널의 잠금·flush·범위 탐색을 먼저 분리 계측한다. durable commit 단위의 묶음 처리, 대체된 범위 정리와 인덱스 개선은 acknowledged write 복구 및 stable-journal-v1과 동등 이상의 seal 계약을 갖춘 뒤 적용한다. 압축 중 임시 공간·실패 복구·살아 있는 후보가 참조하는 이전 세대를 계산한다.

단일 Editor 방식도 이 개선이 필요하다. 큐를 추가하는 것만으로 전체 해시·Git 읽기·범위 저널의 병목이 사라지지 않는다.

## 단일 Editor 후보 전환에서 해결할 일

1. 작업 A/B의 검증 요청은 편집 중 상태와 분리한 불변 후보 세대를 가리킨다. 현재 checkpoint는 최종 수집이므로 이 용도로 재사용하지 않는다. 검증 후보 보존과 최종 결과 수집을 구분하는 계약이 필요하다.
2. 큐에서 Editor 임대를 얻은 뒤 Play/import/compile·미저장 scene/prefab 상태를 확인하고 후보 전환을 시작한다. 열린 Editor 아래에서 mount root나 기준 snapshot을 단순 교체하지 않는다. 열린 핸들·파일 감시·domain 상태의 정합성을 검증한다.
3. 고정 검증 경로의 이전 후보 변경을 제거하고 다음 후보의 추가·수정·삭제·rename과 .meta를 일관되게 반영한다. 검증 Library는 해당 Editor만 쓰며 작업자끼리 쓰기 공유하지 않는다. Unity 버전·패키지·플랫폼 차이에 따른 캐시 무효화, 전환 실패와 필요한 재시작도 비용에 포함한다.
4. 변경 적용 중 import 중간 상태를 통제하고 이후 명시적으로 refresh/import/compile을 수행한다. Unity의 자동 refresh 억제나 import batching은 파일시스템 트랜잭션과 열린 핸들 일관성을 보장하지 않는다. 장애 후에는 완전한 이전/다음 후보 확인 또는 격리·복구가 필요하다.
5. .meta 등 Unity가 생성·변경한 소스를 해당 후보 결과로 수집하고 작업자가 그 사이 한 편집과 충돌 검사한다. Library/Temp는 검증 환경에 남긴다. loaded code·에셋·후보 세대가 일치한 증거를 남긴 뒤 임대를 반환한다.

소스 적용뿐 아니라 되돌림, 임포트 산출물, 사용자 원본 보호가 핵심 구현 범위다. Unity 공식 [refresh 절차](https://docs.unity3d.com/6000.3/Documentation/Manual/AssetDatabaseRefreshing.html), [StartAssetEditing](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.StartAssetEditing.html), [DisallowAutoRefresh](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AssetDatabase.DisallowAutoRefresh.html)의 제한을 적용한다.

## 후속 검증과 선택 기준

| ID | 검증 단위 | 완료 조건 | 상태 |
| --- | --- | --- | --- |
| SF-01 | 불변 기준·변경 기반 검증 | 코드 한 파일 수정 시 변경 없는 대형 소스 payload 재해시 0. 결과 identity·.meta·rename/delete·재시작·손상·동시 쓰기의 올바른 판정. 초기/감사 읽기와 일반 반복 비용 분리 | Passed: 정의한 소스 검증. 전체 보존 실패 별도 |
| SF-02 | 범위 저장 성능·누적량 | 작은 범위 쓰기·전체 재쓰기·동일 영역 반복 쓰기의 실제 할당과 flush 지연, 저널 정리의 임시 공간·중단 복구를 검증. 최초 전체 복제·읽기 hydration 0 유지 | Partial: 계측·저장 불변조건만 확인 |
| SF-03 | 단일 Editor A/B/A 후보 검증 | 작업자 Editor 0, 검증 Editor 1. 전체 Assets checkout 없이 후보 전환·원복, 코드/에셋/.meta 결과·캐시 정합성, 임대 중단 복구 및 원본 보존 | NotRun |
| SF-04 | 작업 재개와 독립 결과 | 후보 검증 뒤 작업 계속 가능, 최종 결과는 snapshot과 독립 보존. VSS 소실·새 CLI·해제 재시도·외부 원본 변경에서 기존 보존 계약 유지 | NotRun |
| SF-05 | 대형 실사용 입력 적합성 | 목표 프로젝트의 파일 수·에셋·LFS/filter·패키지 조건을 명시. 지원하지 않는 입력은 실행 전 구분. 필수 NR 장애·파일 지원 관문과 연결 | NotRun |
| SF-06 | 저장량 중심의 실행 방식 선택 | 같은 입력과 작업으로 신규/재개/누적 편집/정리의 용량·시간·메모리 비교. 기본 모드와 독립 Editor 선택 조건을 결과로 결정 | NotRun |

SF-01/02는 두 실행 방식이 공통으로 사용하는 저장소의 개선이다. SF-03은 작은 소스·이미 확보한 에셋으로 후보 전환 정합성부터 검증할 후속 단위다. 그 뒤 필요한 규모와 시간 예산을 정해 확장한다. 합성 범위 저장을 100GB Unity 지원 증거로 대체하지 않는다. 이번 실행은 기존 작은 fixture와 합성 범위로 한정했고 100GB 데이터·대용량 PV 비교를 시작하지 않았다.

가장 먼저 비교할 수치는 **원본을 제외한 총 추가 물리 공간과 그 최고치**다. 작업자 delta, 검증 view/Library, VSS diff area, Git 객체·LFS cache, 독립 결과, 임시 압축 공간·로그를 분리한 뒤 누락 없이 보고한다. VSS는 같은 볼륨의 다른 쓰기도 보존할 수 있고 변화가 크면 비용이 증가한다([Microsoft VSS](https://learn.microsoft.com/en-us/windows-server/storage/file-server/volume-shadow-copy-service)). unknown은 0으로 바꾸지 않는다. 읽기·논리 크기·할당 크기와 누적 쓰기를 혼합하지 않는다.

판단 순서는 원본·결과 보존과 입력 적합성 → 전체 복제 없는 공간 사용 → 반복 편집/후보 전환의 실용성 → 메모리 → Editor 동시성이다. 단일 Editor 후보가 이 조건을 충족하면 기본 모드로 채택할 수 있다. 독립 Editor는 병렬 실행의 이익이 추가 RAM·Library 쓰기·운영 비용을 감당하는 경우의 선택지로 유지한다.

[지연 회수 1차](deferred-snapshot-reclamation.md)는 결과 보존과 snapshot 삭제 시점을 분리해 `reclaim-pending`, 같은 작업의 명시적 release 재시도와 공유 VSS 공간 관측을 제공한다. [해당 검증](../verification/deferred-snapshot-reclamation.md)은 SF-02 압축/flush 또는 SF-03 단일 Editor 연결의 실행 증거를 대체하지 않는다. 임의 완료 순서의 즉시 물리 공간 회수는 계속 보장하지 않는다.
