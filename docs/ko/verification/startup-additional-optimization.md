# US 이후 추가 준비 최적화

[English](../../verification/startup-additional-optimization.md)

> **현재 시간 기준:** 아래는 해당 후보/단계에서 기록한 과거 근거이며, 수치와 당시 NotRun 경계는 그대로 보존합니다. 이후 최적화를 합친 `116fd44`의 [UF-01 실측](unity-final-lifecycle.md)은 전체 **18분 3.0초**, Editor 준비 **3분 36.1초**, A/B 보존·회수 **2분 52.0초**, Library 복원 **2분 6.0초**입니다. UF가 개별 최적화의 효과나 별도 미실행 항목을 모두 검증한 것은 아닙니다.

SA-01–05는 [US-01](unity-startup-remeasurement.md)의 남은 Library·Close·소스 증명·게시·상속 Bee 그래프 비용을 개선한다. 소스 기준은 `49d8679`, 비교 배포본은 US에서 고정한 `9c09f04` CLI 묶음이다. **새 전체 Unity A/B 준비 시간·ILPP 대기 감소·checkpoint/release·원본 Library 복원 시간은 NotRun이다.** US의 349.7933초는 과거 근거이며 이번 후보의 시간이 아니다.

## 변경과 보호 조건

| ID | 변경 | 유지한 증명과 범위 |
| --- | --- | --- |
| SA-01 | 소유 Library root와 모든 조상을 판독기 수명 동안 고정하여 반복 경로 조회를 핸들의 수명 증명으로 대체한다. | 일반 디렉터리 identity/reparse 입장 검사·불변 파일 핸들·내용 증명을 유지한다. 읽기는 수명 gate 밖에서 수행하고 모든 동시 close가 허용한 읽기를 기다린다. 읽기 오류는 가용성을 실패로 고정한다. 변경 없는 디렉터리 복원은 같은 볼륨/파일 identity의 이동 핸들로 root를 인계하며 중단/교체는 보존·거부한다. |
| SA-02 | linked/unknown Close는 로컬 상태를 검사한 뒤 회수가 불필요하면 반환한다. 게시한 SJL4 root 검증과 인덱스는 읽기 핸들 하나를 공유한다. | disposed/poison/저널 길이 검사와 외부 mutation 직렬화를 유지한다. 실제 unlinked 회수는 기준 가용성도 검사한다. Root checksum·owner·namespace·metadata·길이·범위·세대 검증과 mutation별 durable flush/ACK를 유지한다. |
| SA-03 | 소스 변경 추적기가 있는 `git-snapshot` 검사에서 Git clean 변환이 바이트를 유지한다고 증명되면 전체 스트림 한 번으로 raw SHA256과 Git blob hash를 계산한다. | 정확한 속성/config 해석·object format·전후 세대를 유지한다. 보수적인 text/EOL/내용 증명만 허용하며 filter·encoding·ident·safecrlf·미증명은 기존 Git 경로를 사용한다. payload cache·시각만의 내용 증명은 없다. |
| SA-04 | 새 Editor 없는 ensure의 응답에 같은 연산의 첫 소스 검사 결과를 재사용한다. | 1회성 연산 한정 capability다. 등록·workspace/snapshot identity·정책·HEAD·index·구성원·속성/config·provider epoch/sequence를 다시 확인한다. 영속 증명은 없다. 기존 작업·감사·capability 부재·Editor 시작 응답은 일반 검사를 유지한다. |
| SA-05 | 명시적으로 Library를 참조한 새 작업의 첫 Editor 시작 전에 직속 일반 파일 `Library/Bee/*.dag`만 무효화한다. | Library ID/project 일치·원본과 workspace 분리, 전체 경로/reparse 검사 뒤 삭제하고 각 파일을 재확인한다. 이전 receipt·불확정 시작·일반 checkout·Library가 아닌/전체 프로젝트 입력은 선택하지 않는다. JSON·payload·state·backend info·응답 파일·산출물·원본·peer를 유지한다. |

SA-01은 경로 결과 cache를 추가하지 않는다. 실제 native 핸들이 수명을 증명하고 payload 읽기는 장치/읽기 오류를 계속 반환한다. 강제 볼륨 제거와 OS 장애는 이번 자격 밖이다. 복원 인계가 실패한 reader는 재사용을 영구 거부하며 정상 같은 계획 복구가 새 reader를 연다. 형식·서비스·mount 계약은 바꾸지 않는다.

접근 권한과 공유 방식은 구분한다. 불변 Library root는 쓰기/삭제 공유를 거부한다. 조상은 삭제를 막되 정상 자식 게시를 허용하며 고정한 하위 경로 때문에 비워지지 않는다. 쓰기가 필요한 저널 디렉터리도 자식 게시를 허용하고, **기존** `owner.lock`을 추가 읽기 핸들로 삭제하지 못하게 하여 빈 디렉터리가 되지 않게 한다. Native junction 전환 시험으로 이 경계를 확인한다. 전체 사슬을 연 뒤 핸들의 최종 경로와 조상 속성을 검증한다. 복원 부모는 비워질 수 있으므로 이름 고정 **및** 파일/디렉터리 입장·이동 때 실제 경로/reparse 검사를 유지한다. 새 marker는 없다. 임의의 파일시스템 filter 동작 자격으로 확대하지 않는다.

SA-03의 NUL 검사는 **전체 스트림**에 대한 자동 binary 판정의 충분조건이며 native 계수 범위로 제한한다. Git diff의 prefix 휴리스틱이 아니다. 명시적 text는 더 엄격한 no-CR 조건이다. 설치된 Git의 SHA1/SHA256 결과와 CRLF·모호한 속성 값·첫 MiB 이후 NUL까지 비교한다. 대응하는 일차 구현은 [Git for Windows convert.c](https://github.com/git-for-windows/git/blob/v2.45.1.windows.1/convert.c)다.

SA-04는 최초 검사 계측을 반환한다. Guard 재검증을 별도 전체 검사로 기록하거나 최초 검사 시간에 포함하지 않는다. 세대·구성원·해석 변경/소실과 두 번째 사용은 증명을 거부하고 작업을 보존한다. Unity 관리 쓰기는 정당한 소스 metadata를 만들 수 있으므로 `ensure --editor`에는 이 clean 증명을 재사용하지 않는다.

## 한정 Bee 실험

Unity [6000.3 컴파일 코드](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.3/Editor/Mono/Scripting/ScriptCompilation/EditorCompilation.cs)는 그래프 검증을 지연한다. 설치된 Unity **6000.3.22f1**의 Bee backend로 action 하나인 작은 합성 그래프를 실행했다. Unity나 기존 프로젝트 그래프는 실행하지 않았다.

1. 합성 그래프 생성: exit 0, action 계수 1.
2. compiled graph와 deferred verification 재사용: 검증 신호 **이전**에 action 계수 2.
3. 시험 경로에서 compiled `.dag`만 이동: exit 4로 frontend 실행을 요청하고 action 계수는 2 유지.
4. 유지한 JSON으로 명시적 재생성: exit 0, action 계수 3.

그래프 부재 단계에서 state·그래프 JSON·payload hash는 같다. Bee 자체가 derived graph cache를 지우고 다시 만든다. 제품은 `.dag`만 무효화하고 그래프/응답 파일을 다시 쓰거나 process endpoint를 공유하거나 설치된 Unity/도구를 변경하지 않는다. 그래프 생성 비용은 남는다. 이는 backend의 한정 부재 동작 증명이며 **실제 Unity frontend 재실행이나 US의 약 60초 ILPP 대기 제거 증명이 아니다.**

## 근거와 제한

[데이터셋](../../../reports/benchmarks/startup-additional-optimization-20261008.json)에 최종 회귀 결과·고정 runtime hash·소형 NTFS 비교·Bee 실험을 기록한다. 원시 로그/fixture는 ignored `artifacts/startup-additional-optimization-20261008/`와 `artifacts/ilpp-dag-metadata-probe-20261008/`에 있다.

필수 `pwsh -NoProfile -File scripts/build.ps1`의 locked restore·Release 빌드와 **시험 1,453개 성공 / 실패 0개 / 조건부 생략 20개**를 통과했다. 빌드 경고/오류는 0개이며 이전 1,396개 묶음보다 77개 사례를 추가했다. 최종 전체 시험은 4분 55.231초였다. 무관한 dirty 파일 27개의 hash와 깨끗한 형제 Unity 저장소 상태도 유지했다.

시험은 빈 root rename/reparse 보호·겹친 읽기와 close·복원 중단/교체·Close 로컬 상태·게시 핸들 경쟁/손상·소스 해석/세대 변경·1회성 증명·시작 receipt·일부 그래프 삭제 후 재시도를 다룬다. 첫 native 시험은 속성 전용 디렉터리 핸들이 rename을 막지 못함을 확인했다. 수정 뒤에도 실제 rename 시도를 회귀 검사로 유지한다. 조건부 외부 시험은 조건부로 남는다.

첫 읽기 전용 공유 보정은 정상 자식 파일 이동도 막았다. 한정 access/share 조합과 실제 `FSCTL_SET_REPARSE_POINT` 실험으로 위의 좁은 설계를 선택했다. 최종 회귀는 살아 있는 저널 guard 아래 create/move/backup 포함 replace/delete와 복원 부모 pin 아래 게시/자식 이동을 포함한다. [Windows 공유 계약](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew)은 속성 접근과 data/delete 접근을 구분하므로 metadata 전용 open을 이름 고정의 증명으로 사용하지 않는다. 두 실패 시도도 원시 근거에 보존한다.

비용 비교는 일반 NTFS 파일 64개(4MiB), 가용성 호출 20,000회, linked Close 20,000회, 4KiB 개별 append 512회다. 준비와 내용 검사는 타이머 밖이며 각 append flush·전체 바이트·재연결 seal을 검사한다. 전/후/후/전 순서는 단순 warmup 편향을 줄이지만 무작위 성능 연구는 아니다. reader/저널 변경을 합친 연산 비용이며 개별 원인 귀속이나 물리 I/O 계측은 아니다.

| 측정 연산 | 변경 전 평균, 초 | 변경 후 평균, 초 | 관측 감소 |
| --- | ---: | ---: | ---: |
| Library 가용성 확인 20,000회 | 0.537752 | 0.016376 | 96.95% |
| linked Close 20,000회 | 0.648384 | 0.025096 | 96.13% |
| 4KiB append 512회, 매회 flush | 3.178398 | 3.090191 | 2.78% |

각 평균은 고정한 후보별 2회이며 실제 Windows 저널 guard를 사용한다. Append 범위는 전 3.0873–3.2695초, 후 3.0488–3.1316초로 겹치므로 **일관된 쓰기 시간 개선은 이 표본으로 확정하지 않는다.** 첫 탐색 묶음은 읽기 전용 UTF-8 검사와 일부 겹쳐 별도로 보존했다. 위 최종 묶음은 다른 에이전트 도구 작업 없이 실행했다.

원본 Unity 프로젝트/Library·설치 서비스·전역/외부 도구 설정은 변경하지 않는다. 무관한 dirty 파일 27개는 기록된 hash로 보호한다. 새 전체 Unity 실행·Play/Test/build·지원 행렬·OS 장애·전체 RAM/물리 저장소 자격으로 확대하지 않는다.
