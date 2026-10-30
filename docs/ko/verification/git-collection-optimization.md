# Git 수집 최적화 검증

[English](../../verification/git-collection-optimization.md)

[범위·종료 기준](../plans/git-collection-optimization.md), 2026-10-30, `f91e470` 위 소스 `0432721a1ce1f7042994d5f75146a5e8004a1c5b`다(reader 구현 `1e20f49af96e6f64c73b98a3021e525a87e7c486`). 커밋 준비에서 소스/시험 15파일이 전체 gate 당시 입력과 byte 단위로 같음을 확인했다. **GCO-01–04를 한정 완료하고 채택했다.** [구조화된 근거·소스/원시 hash](../../../reports/benchmarks/git-collection-optimization-20261030.json)에 고정 비교·최종 gate를 기록한다. 과거 [GCD-01](git-result-collection-diagnosis.md)은 이전 reader의 수치를 유지한다. 새 Unity 자격을 주장하지 않는다.

## 구현과 보호

`ISnapshotTree.VerifyReadBoundary`의 기본 동작은 기존 가용성 검사다. Git 수집 조합은 정확한 빌린 reader lease 아래 `GitSnapshotReadScope`를 제공한다. 소유 loose basis ref·basis receipt만 fence하고 읽는 동안 현재 ref/receipt 내용을 다시 읽으며 게시 경계에서 Git observation을 전체 수행한다. 접수 시 commit도 검증하고 tree/blob/chunk proof를 유지한다. packed payload는 지원하며 packed-only basis ref는 기존 전체 observation 경로다. HEAD·index·branch·공유 packed ref는 fence하지 않는다. 예외 경로도 scope를 먼저 닫은 뒤 빌린 lease를 닫으며 검증 실패는 해당 scope를 poison한다.

전체 경계 검사는 결과 manifest 게시·Git 결과 ref 게시·generation receipt 게시·Runtime 결과 게시 전에 수행한다. 읽어 증명한 payload가 reader에 남아 있어도 게시 시 commit 객체가 없으면 거부한다. 전체 프로젝트 복사·hydration·변경 payload buffering 정책·저널/내구성 변경은 없다. wire schema·task v3·관리형 basis v2 변경이 필요하지 않았으며 기본/fake reader는 전체 가용성 동작을 유지한다.

새 `checkpoint --release`는 정상 종료·infrastructure 제거 뒤 한 번의 수집을 전체 seal로 감싼다. 같은 호출의 seal한 결과를 과거 결과 검사·unmount 뒤 seal과 함께 회수로 바로 넘긴다. 이전에 고정/게시된 요청이나 진행 중 capture는 새 호출의 seal을 빌리지 않는다. 요청의 고정 결과를 반환하고 마감용 현재 소스를 수집한다. 독립 release도 현재 소스를 수집하며 호출 사이의 소스 동등성 shortcut을 추가하지 않았다.

## 집중 검증

- 첫 집중 묶음: **총 145개 / 144 통과 / 0 실패 / subprocess 전용 skip 1개**. 새 lease reader와 기존 Git/소스/application/게시/복구 동작을 포함한다. subprocess 부모 시험은 실행하며 자식 진입점은 독립 자격 행이 아니다.
- 추가 최종화 묶음: **5 통과 / 0 실패 / 0 skip**이며 기존 fresh 최종화 사례 하나가 앞 묶음과 겹친다. capture 고정·generation 게시 뒤 응답 유실, 전체 전후 seal 변경, unmount 뒤 감지한 변경을 주입했다. 과거 고정 결과를 유지하며 이후 마감 소스를 보존한다.
- 새 reader 시험은 loose-ref/receipt fence와 해제, 원본·peer ref 쓰기, packed payload, packed-only ref 삭제, 잘못된/유실 lease, 게시 시 객체 유실, poison된 scope, 결과 manifest/receipt 게시 전 거부를 검증한다.

원시 시험 보고서는 `artifacts/git-collection-optimization-20261030/focused.trx`·`combined-recovery.trx`다. 시험 빌드의 경고/오류는 없으며 전체 gate는 아래 마감에 추가한다.

## 고정한 작은 실제 CLI 비교

같은 Git 원본에서 순차 전후 각 한 번이며 **기준본 27파일 / 75,034 B**, seed `2d701823dc6fee4b9a47d5d9a734e1b4a93b749b`, application `none`, `git-snapshot`, Windows/NTFS/WinFsp다. Library·Editor를 시작하지 않았다. 전후 별도 home마다 A/B 한 묶음을 만들고 project/key 값과 새 task record를 사용했다. 소유 회수 완료 뒤 알려진 실험 생성 파일 두 개만 명시적으로 제거해 원본을 되돌렸다. reset·clean·재귀 삭제는 하지 않았다.

| 정상 CLI 작업 | 이전, 초 | 변경, 초 | 절감, 초 | 감소율 |
| --- | ---: | ---: | ---: | ---: |
| A 첫 수집 | 37.408 | 4.937 | 32.471 | 86.8% |
| A 두 번째 수집 | 23.694 | 4.780 | 18.914 | 79.8% |
| A 독립 release | 29.114 | 10.070 | 19.043 | 65.4% |
| B `checkpoint --release` | 64.023 | 10.042 | 53.981 | 84.3% |

네 독립 결과 tree가 전후 모두 일치한다. A는 고정 결과 두 개를 원본 파일에 게시하고 독립 release에서 이후 `last.txt`를 보존하며 전후 모두 generation 세 개를 유지한다. B는 A 작업 중과 A 회수 뒤에도 쓴다. B의 결합 처리는 같은 요청 최종 tree를 **generation 2 → 1개**로 보존한다. 두 묶음은 모두 `released`이며 소유 provider·mount·delta가 없고 과거 결과 graph가 유효하다. 회수 뒤 과거 수집 요청은 sequence 1을 반환한다. 원본 HEAD·index·무관한 원본 편집을 보존했고 별도 실험 reset 뒤 현재 소스가 clean으로 돌아왔다.

사전 채택 기준인 **10초 또는 30%** 절감을 충족했다. 범위 보호 아래 읽기마다 Git 프로세스를 없애고 같은 새 결합 호출의 수집을 공유했다. 독립 release는 현재 소스를 여전히 수집한다.

순차 한 쌍이며 무작위/cold-cache 실험이 아니다. 이전 Git 객체·보존 ref를 남겼고 같은 project/key 값으로 별도 home의 task/result ID도 같다. 일부 변경 결과는 기존 불변 ref를 재사용하여 `update-ref`를 생략하며 그 기여는 따로 분리하지 않았다. OS cache·프로세스 시작·백그라운드 provider polling을 포함한다. 수십 초 절감 전체를 미세 변경 하나의 효과로 귀속하거나 Unity·대형 입력으로 일반화하지 않는다. 측정 뒤 capabilities 설명만 바꿨으며 수집·최종화 코드는 이후 변경하지 않았다.

## 최종 gate와 한계

최종 `pwsh ./scripts/build.ps1`의 locked restore·Release build는 **경고/오류 0개**이며 **총 2,065개 / 2,029 통과 / 0 실패 / 조건부 skip 36개**, **7m 10s 511ms**로 통과했다. skip은 선택 비용/native fixture·subprocess 전용 자식이며 필수 범위 보호/복구 부모 시험을 대체하지 않는다. **실제 응답 31개**를 현재 catalog로 검증해 **무효 0개 / 제외 0개**다. 양언어/source-index·공백 검사도 통과했다. 기준 binary·CLI 응답·시간 이벤트·반환 identity·결과 tree hash·실험 원본 복원 영수증·이벤트 로그는 ignored `.local/git-collection-optimization-20261030/`·`artifacts/git-collection-optimization-20261030/`에 보존한다.

최초 driver 준비 시도는 PowerShell 함수/실행 파일 이름 충돌로 task 생성 전에 중단했고 수정 뒤 측정에서 제외했다. 제품 guard는 유지했다. 기존 RI Unity/Plastic/None 자격은 당시 소스에 고정한다. 새 실제 Unity refresh/compile/Play/build·대형 입력·Plastic/None native 확대·OS/전원 장애·RAM/공간 절감·일반 속도 비율은 측정하지 않았다.
