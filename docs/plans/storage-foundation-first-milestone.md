# 공통 저장소 개선 1차 범위와 남은 마감 조건

확정일·실행일: 2026-09-25. 상태: **구현·제한된 소스 검증 완료, 전체 FailedPreservation**. SF-01의 정의한 소스 검증은 Passed, SF-02는 계측 하위 범위만 Partial이다. [실행 기록](../verification/storage-foundation.md)의 기존 VSS 5개 소실로 해당 실행의 마감 조건은 충족하지 못했다. [후속 재현·보호 시험](../verification/storage-recovery.md)에서 제어 JSON 게시 충돌을 수정하고 VSS 삭제 guard의 위험 요청 차단·오래된 순서 회수를 실제 확인했다. 일반 비소유 백업 공존과 임의 순서의 즉시 회수는 지원으로 확대하지 않는다.

확정한 작업은 **추가 공간과 저널 비용의 기준선 계측 → 변경 기반 소스 검증 → 작은 fixture 회귀**로 한정한다. SF-01을 완료하고 SF-02의 계측·저장 불변조건 기준선을 확보하는 것이 종료점이다. 저널 압축·flush 정책 변경과 단일 Editor 제품 연결은 별도 후속 단위다. 이 단계 완료만으로 SF-02 전체나 SF-03~06을 Passed로 올리지 않는다.

## 제품 목표와 현재 도달점

Splinter는 각 대화의 AI가 필요할 때 독립 작업공간을 확보하고, 여러 차례 편집·검증·재개한 뒤 사용자의 명시적 요청에 반영·보존·폐기하도록 만드는 로컬 CLI 도구다. 첫 지원 조합은 Windows/NTFS + Git + Unity이며, 대형 프로젝트의 작업공간 용량 부담 해소가 1순위, Editor 동시 실행이 2순위다.

원본 위치와 NTFS를 유지하고 원본은 작업 중에도 수정할 수 있어야 한다. 작업마다 시작 시점의 불변 기준과 실제 변경 범위를 사용한다. 최초 전체 복제, 읽기 hydration, 파일 전체 copy-up fallback, 작업 기간 전체의 원본 쓰기 차단은 허용하지 않는다. 전체 논리 파일 트리와 실제 Git linked worktree를 유지하며 Core에는 Git·Unity·Win32·Codex 전용 API를 넣지 않는다.

| 구분 | 현재 확인한 범위 | 완료로 확대하지 않는 범위 |
| --- | --- | --- |
| 기반·내부 알파 | CLI JSON/schema, atomic JSON 상태·잠금, 작업별 Git 등록·Unity start/refresh/stop·검증·결과 수집·통합·회수 | 상주 daemon/DB, 설치·자동 대화 binding·명시적 discard |
| NI-01~08 | 제품 NTFS snapshot/delta, 원본/A/B 격리, 같은 작업 재개, 독립 결과, dirty 원본 통합 차단, 소유 자원 회수 | 일반 에셋·파일·장애 지원 전체 |
| LV-01~08 | 정의된 자동/실환경 수준의 live VSS 식별·재연결, 단일 Unity 검증과 결과·회수 | 실제 재부팅·장치 번호 재배정·전원 손실·물리 공간 고갈 |
| NR | 복구 분류·unknown 보존·상태/저널 손상 보존·일부 파일 API 거부, ARM Windows/x64 Unity 단일 회귀 | 나머지 중단 경계·파일 특성·ARM A/B 및 NR 전체 |
| PV-01 | 대표 fixture의 독립 A/B 반복 편집·재개·A 선행 회수 후 B 지속·최종 결과와 회수, 복구·운영 개입 포함 | 무개입 성공·설치/binding·일반 지원 |
| PV-02 | 사용자 선택 범위 `scoped-complete-with-limitations`로 마감 | 전체 Passed·성능 우위. S4 `FailedInputQualification`, 소수 표본, 과거 전체 팩 raw 동등성 unknown 유지 |
| 공개 목표 | 제품 요구사항과 사용 모델·검증 계획 정의 | 공개 48개, SW-01~10, SF-02 전체·SF-03~06의 실행 완료 |

구현 전 검토 기준은 `development`의 `30ce3075bb377e968931a6bc53dde77c4065673f`이며 PV 후보 125개 hash 일치와 저장 TRX의 206 통과·1 skip을 확인했다. 이후 이번 기준 재실행도 206 통과·1 skip이었다. 1차 마감 후보는 [저장소 기록](../verification/storage-foundation.md)의 226 통과·0 실패·1 skip, 후속 현재 후보는 [복구 기록](../verification/storage-recovery.md)의 231 통과·0 실패·1 skip이다. 과거 PV 후보와 동일한 파일 집합이 아니다.

실환경 완료 판단은 [NI](../verification/ntfs-workspace-integration.md), [LV](../verification/ntfs-live-vss-identity.md), [NR](../verification/ntfs-recovery.md), [PV 기록](../verification/parallel-workflow.md)과 [최종 마감 영수증](../../artifacts/parallel-workflow/pv-20260923/final-closeout.json)을 따른다. 영수증의 자원 회수는 당시 관측이며 현재 OS의 mount·VSS·Editor 상태를 새로 확인한 것은 아니다.

## 이 범위를 먼저 하는 이유

[PV 결과](../../artifacts/parallel-workflow/pv-20260923/final-findings.md)는 최초 payload 복제·읽기 hydration 없이 작업별 delta를 사용하는 효과를 확인했지만, 방식 전체의 추가 물리 공간과 절감률은 입증하지 못했다. 서로 다른 단계의 Assets·Library·delta 표본과 VSS 볼륨 관측을 같은 총량으로 비교할 수 없다. 따라서 공간 계측을 먼저 고정한다.

구현 전 `GitWorkspaceProvider.InspectAsync`는 관리 대상 소스를 매번 전체 해시했다. 일반 status poll 읽기는 이미 제거됐지만 명시적 검증·수집에는 남아 있었다. 현재 [변경 기반 소스 검증](../source-verification.md)은 변경 없는 파일의 raw/Git hash를 재사용하며 초기·감사·provider 재연결에는 전체 읽기가 남는다. 이번 16/256/1,024MiB fixture의 실제 값은 [실행 기록](../verification/storage-foundation.md)을 따른다.

[JournalDeltaStore](../../src/Splinter.Runtime/JournalDeltaStore.cs)는 잠금 안에서 append하고 각 기록을 `Flush(true)`한 뒤 성공을 반환한다. 같은 영역 반복 쓰기의 누적량·잠금·flush·재생 비용을 분리한 새 기준선 없이 압축이나 내구성 정책부터 바꾸지 않는다. 공간 절감과 처리시간 개선은 별도로 판정한다.

## 포함 범위와 구현 순서

| 순서 | 산출물 | 담당 경계 | 완료 기준 |
| --- | --- | --- | --- |
| 1 | 추가 공간·읽기·저널 비용 계측과 변경 전 기준선 | Runtime 계측, Windows 물리 할당/VSS 관측, Git 비용 구분 | 생성·읽기·편집·재개·결과 보존·회수의 같은 종료점에 시간과 바이트 기록. 소유·공유·미확인 비용을 구분 |
| 2 | 불변 기준 manifest와 신뢰할 수 있는 변경 이력의 계약·fake | Core의 공급자 중립 계약, Runtime의 영속 상태 | snapshot identity·경로/제외 정책·변경 세대 연결. 누락·손상·동시 쓰기에서 과거 성공을 재사용하지 않음 |
| 3 | 파일 단위 증분 fingerprint | Git의 소스 집합/해시, Runtime의 저널 변경 집합, Windows의 연결 검증 | 변경 없는 파일은 검증된 raw hash 재사용, 변경된 파일만 재해시. 기존 전체 해시 oracle과 결과 일치 |
| 4 | 재개·검증·결과 보존 회귀와 제한된 실제 실행 | Runtime/Protocol/fake·Git/Windows/Unity 어댑터 | 새 CLI 재개, 정확한 소스와 loaded-code 연결, 수집 경쟁 보호·독립 결과·소유 자원 회수 유지 |
| 5 | 변경 전후 보고·현재 계약 문서와 추적표 | 관련 계약/schema·capabilities·문서 | 성능과 저장량을 별도 보고. SF-01 판정과 SF-02의 완료/미실행 하위 조건 명시 |

### SF-01의 고정 경계

- 첫 구현의 해시 재사용 단위는 **파일**이다. 작은 코드 편집에서 변경 없는 대형 파일을 다시 읽지 않는다. 변경된 대형 파일 자체의 전체 해시는 허용하고 그 비용을 기록한다. 범위별 hash tree는 후속 판단으로 남긴다.
- 처음 확인한 raw 파일 hash와 경로 집합은 정확한 snapshot identity 및 소스 포함/제외 정책에 묶는다. 같은 Git revision·크기·mtime만으로 재사용하지 않는다. 다른 snapshot의 manifest를 검증 없이 공유하지 않는다.
- 가능하면 기존 fingerprint의 경로 정렬·값을 유지한다. 형식을 바꿔야 하면 버전과 이전 validation/manifest의 무효화·복구 계약을 함께 정의하고 이전 성공을 새 형식으로 묵시 승계하지 않는다.
- 추가·삭제·rename·truncate·원자적 교체·`.meta`·새 untracked 파일을 다룬다. `.gitignore`·`.gitattributes`·관리 경로 정책처럼 소스 집합/해석에 영향을 주는 변경은 재열거와 재검증 조건에 넣는다. 미지원 filter/LFS를 지원으로 바꾸지 않는다.
- provider 재시작·저널 누락/손상·쓰기 경쟁·관측 unknown에서는 보존하고 명시적 재검증을 요구한다. 검증의 앞뒤 세대가 다르면 현재 성공을 발행하지 않는다. Library 등 관리 캐시의 쓰기와 검증 대상 소스 쓰기를 구분한다.
- `workspace status`의 `sourceVerification`·`validationCurrent`와 실제 validation 성공을 함께 보는 계약을 유지한다. `checkpoint`는 최종 수집이며 중간 검증 후보 생성으로 바꾸지 않는다.
- 결과 수집의 `stable-journal-v1` 전후 seal, unmount 후 확인, Git 결과 독립성은 유지한다. seal의 전체 저널 읽기와 Git의 객체 생성·필수 읽기 비용은 별도 계측하며 소스 fingerprint 개선으로 없어졌다고 보고하지 않는다.

### SF-02에서 이번에 완료할 하위 범위

공간 장부에는 작업별 delta/메타데이터, 새 검증 캐시, Library 쓰기, VSS diff area, 공유 Git 객체, 독립 결과, 임시 파일·로그를 포함한다. Library 쓰기가 delta 안에 들어 있으면 분류만 하고 다시 합산하지 않는다. 공유 자원도 한 번만 센다. 기존 원본의 기초 할당은 제외하되 snapshot 이후 원본 쓰기로 늘어난 VSS 비용은 관측에 포함한다.

물리 할당·논리 길이·누적 쓰기·읽기 바이트는 각각 기록한다. 각 표본의 동시 합계와 **관측 최고치**를 계산하고, 서로 다른 시점의 최대값을 더하지 않는다. 표본 간 놓칠 수 있는 최고치와 MFT 등 미계측 항목을 명시한다. VSS를 작업별로 귀속할 수 없으면 볼륨 변화와 unknown을 그대로 남긴다. 총량이 불완전하면 총 절감률이나 전체 비용 우위를 결론내리지 않는다.

작은 범위 쓰기, 같은 영역 반복 쓰기, 파일 전체 재쓰기, 삭제/truncate 후의 현재 파일 크기와 남은 저널 할당을 비교한다. 잠금 대기·flush 횟수/시간·재생·seal 읽기 및 초기 manifest 비용을 구분한다. **압축/대체 영역 회수, flush 묶음 처리, 압축 중단 복구는 이번 구현에서 제외**하며 SF-02 전체는 미완료로 남긴다. 변경 이력 제공에 필요한 최소 인덱스·메타데이터만 추가한다.

## 검증 자원과 완료 조건

현재 Windows x64/NTFS와 기존 도구 버전을 사용한다. 먼저 fake 불변 트리·실제 Git 임시 fixture에서 검증하고, 제품 경로의 실제 VSS/WinFsp와 기존 작은 Unity 스크립트·확인용 에셋으로 연결한다. Unity는 기존 `6000.3.22f1`을 유지한다. 제품 Splinter 작업에는 Coordination을 설치하지 않는다. 원본과 무관한 Editor·작업을 보존한다.

합성 파일은 기존 SO/NI 크기인 16/256/1,024MiB를 상한으로 하고 각 4KiB 쓰기·반복 읽기를 사용한다. 같은 영역 4KiB 덮어쓰기 100회, 16MiB 파일 전체 재쓰기, rename/delete/truncate를 추가한다. 이는 저장 계약의 제한된 시험이며 대용량 Unity C/S/Q 재측정이 아니다. 실제 원본 해시 확인과 시험용 oracle의 전체 읽기는 제품의 정상 반복 읽기와 분리한다.

새 후보에서 다음을 모두 확인해야 이 실행 단위를 완료한다.

1. `pwsh ./scripts/build.ps1`의 locked restore·빌드·관련 자동 시험을 통과하고 후보 manifest를 남긴다. 선택적 ReFS skip과 실제 실행하지 않은 환경은 분리한다.
2. 동일 불변 기준에서 코드 한 파일을 바꾼 뒤 반복 소스 검증 3회에서 **변경 없는 대형 소스 payload 재해시 0바이트**를 계수로 입증한다. 해시 값은 독립 전체 읽기 oracle과 일치해야 한다. 초기 기준 작성·명시적 감사 읽기를 반복 비용에서 숨기지 않는다.
3. 동일 크기/mtime의 내용 변경, `.meta`, 추가/삭제/rename/truncate/원자적 교체와 소스 집합 변경을 검출한다. 이전 validation과 현재 내용이 다르면 성공을 재사용하지 않는다.
4. 새 CLI/provider 재연결, 기록 손상·미완료 쓰기·동시 쓰기에서 같은 기준을 검증하거나 보존/차단한다. acknowledged write의 복구와 기존 수집 경쟁 회귀가 유지돼야 한다.
5. 제품 backend의 최초 전체 payload 복제·읽기 hydration 0, 큰 파일의 4KiB 범위 저장을 다시 확인한다. 캐시/계측 추가가 작업별 전체 복사나 메모리 payload 사본을 만들지 않아야 한다.
6. 작은 Unity A/B 작업에서 기본 값→수정 값→재개 후 값의 fingerprint·domain/MVID·loaded-code를 연결하고 A 회수 뒤 B의 편집을 확인한다. B를 미리 checkpoint하지 않는다. 최종 독립 결과, 원본 보존/요청한 통합, dirty 원본 차단과 소유 자원 회수를 확인한다. 실패/복구가 있으면 그대로 기록한다.
7. 같은 입력·종료점의 변경 전후 시간·읽기·추가 할당 장부를 남긴다. 느린 구간이나 추가 manifest 공간도 포함한다. unknown이 남으면 제한으로 보고하고 추정 합계로 대체하지 않는다.

증거는 `artifacts/storage-foundation/<runId>/`에 후보/입력 manifest, 명령·시각·계수, 공간 표본, oracle 비교, 복구·독립 결과·회수 기록으로 보존한다. 실환경 기록은 [공통 저장소 검증](../verification/storage-foundation.md)에 있다. 새 소스 검증 결과와 기존 VSS 보존 실패를 분리하며 과거 증거를 새 후보 전체 성공으로 승계하지 않는다.

## 이번 범위 밖과 다음 판단

| 항목 | 이후 진행 조건 |
| --- | --- |
| VSS 보존·회수 | 격리 재현·실제 차단은 정의한 조건에서 완료. 다음은 결과 보존과 snapshot 삭제 시점을 분리하는 상태·재시도·잔존 공간 비용. 기존 snapshot 삭제로 우회하지 않음 |
| SF-02의 저널 압축·내구성 정책 최적화 | 이번 누적량·지연 기준선으로 대상 선정. 임시 공간·이전 세대 참조·중단 복구의 별도 범위 확정 |
| SF-03 단일 Editor A/B/A 후보 전환 | 공통 저장소 1차 결과 뒤 작은 후보 실험. 불변 후보·고정 검증 view·원복·Library 정합성·소스 결과 반환 계약부터 정의 |
| SF-04~06·100GB 실사용·기본 실행 방식 선택 | 앞선 결과와 입력 적합성을 바탕으로 별도 자원/시간 범위 확정. 단일 Editor 채택은 아직 미결정 |
| SW-01~10 설치·자동 binding·명시적 discard | 저장소/실행 계약 판단 후 별도 구현. 보존형 release와 구별 |
| NR 남은 장애·파일 지원·ARM A/B | 지원 확대의 필수 관문. 이번 변경에 직접 관련된 보존/복구 회귀는 즉시 포함하고 나머지는 별도 계획 유지 |
| 일반 에셋 저작·Play·Unity Test Framework·dirty/LFS·공개 배포 | 각각의 공개 지원 시나리오를 실제 실행한 뒤 판정 |

마감한 PV 대용량 비교, 전체 Medieval 팩 수리·lighting rebake, 새 에셋 구매·100GB 데이터 준비, ReFS 신규 확장, daemon/DB·Runner/DAG 도입은 이 실행 단위에 포함하지 않는다. 기존 결과·원본·타 작업을 보존할 수 없는 문제가 발견되면 성능 목표보다 해당 보존 결함을 먼저 해결한다.

SF-01의 소스 검증 통과와 SF-02의 계측 성과를 전체 실행 성공으로 확대하지 않는다. 해당 실행은 **FailedPreservation**, SF-02 전체는 Partial, SF-03~06은 NotRun이다. 제어 파일 충돌 수정과 VSS 삭제 연쇄 재현·실제 보호 검증의 결과는 [복구 기록](../verification/storage-recovery.md)을 따른다. 다음에는 더 오래된 활성/비소유 snapshot 때문에 회수 대기 중인 공간을 명시하는 계약을 정한다. 자동 지연 회수는 아직 구현하지 않았다.
