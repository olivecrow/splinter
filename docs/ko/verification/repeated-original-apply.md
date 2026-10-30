# 반복 원본 반영 검증

[English](../../verification/repeated-original-apply.md)

[GCD-01](git-result-collection-diagnosis.md)은 보존된 Git 입력의 계속 작업 수집·회수 비용을 별도로 진단한다. 기준본 가용성 프로세스 반복과 최종 재수집을 확인했으며 RI 자격 변경이나 구버전 대비 속도 비교를 주장하지 않는다.

[승인 범위](../plans/repeated-original-apply.md), 기준 `aff285f0da769eeaba0300d448f027ceeaa4a5bb` 위 development 작업 트리, 2026-10-30. **아래 한계를 유지하며 RI-01–08을 한정 완료했다. 세 입력의 고정 실제 Unity 순서와 최종 자격 전체 gate가 통과했다.** 과거 Unity 통과나 선택 시험 skip으로 필수 RI 행을 대체하지 않는다.

[구조화 근거·원시 파일 hash](../../../reports/benchmarks/repeated-original-apply-20261030.json)에 정확한 결과·process identity·시간·할당 범위를 기록한다. 원시 로컬 근거는 `artifacts/repeated-original-apply-20261030/`, 시험 보고서는 `artifacts/ri-tests/`다.

| ID | 현재 근거 | 상태 |
| --- | --- | --- |
| RI-01 | 현재 v3 상태/CLI/schema/fake, 같은 요청·최신 결과·과거 영수증·잘못된 Editor/path/result/만료·손상/이전 상태 거부. 현재 native 응답/permit **99개 유효·0개 무효**. 과거 discovery 3개(이전 descriptor 2개·이전 reader 버전 1개)는 보존하고 현재 대체 응답을 검사하며 operation 근거는 제외하지 않는다. | 통과 |
| RI-02 | 합성 불변 트리와 실제 NTFS alias로 append/bounded/latest 교체·overwrite/truncate/rename/delete·기준 디렉터리 rename 확인. 접수된 동시 batch의 영속 완료까지 수집이 기다리고 이후 batch 중에도 고정 reader가 이전 바이트를 유지한다. 경계 안 basis payload 읽기 없음. | 집중 통과 |
| RI-03 | 반복/선택·잔여 선택·최초 내용 복귀·생성/삭제·미수집 후속 편집·충돌·과거 요청 재게시 방지·Unity 동반 파일·원본/peer 보존. 실제 Git index 바이트/HEAD/branch 유지. 실제 Plastic 등록·selector·loaded changeset·관리 파일 hash와 관측 lock 목록도 유지됐다. | 자동·Git/None/Plastic 통과 |
| RI-04 | alias 보호 전후 저널 writer 프로세스 강제 종료 2개, 수집/payload/결과/게시/반영 상태 경계의 coordinator 강제 종료 6개, 두 번째 반영의 응답 유실 8경계. 실제 None CLI를 영속 고정 뒤 종료하고 같은 키로 복구했다. | 지정 process/계약 경계 통과 |
| RI-05 | 작은 실제 Git Unity 원본/A/B 한 묶음. 같은 A의 실행 중 2회·해당 Editor 정상 종료 뒤 최종 1회, 원본/B 생존·실제 코드/씬/에셋·외부 컴파일·종료 중 마지막 쓰기 확인. | 같은 identity 복구를 포함한 한정 통과 |
| RI-06 | 작은 VCS 없는 관리형 Unity 원본/A/B 한 묶음에서 같은 흐름, 실제 수집 CLI 중단·복구, 원본 실행 중 두 작업 회수 뒤 별도 원본 정상 종료/project restore. | 같은 identity 복구를 포함한 한정 통과 |
| RI-07 | 사용자 승인한 별도 작은 regular/static Plastic Unity 저장소의 고정 changeset 1. 같은 A에서 실행 중 중간 반영 2회·정상 종료 뒤 최종 반영. 원본/B identity·dirty 저장·충돌 거부·실제 loaded 값·무관한 미커밋 내용·독립 결과·양쪽 회수 확인. 현재 private reader **11.0.16.10484**의 segmented binary/BOM 바이트 확인. | 같은 identity 도구 복구 포함 한정 통과 |
| RI-08 | 세 입력의 mutation 대기/정지·명령 단계·고유 NTFS 할당·보존 결과, 양언어 소유 문서/skill/지도/capabilities 갱신. 현재 reader의 최종 전체 build와 문서 검사는 아래와 같이 통과했다. | 한정 통과 |

## 자동 시험과 근거 한계

`feature-and-readers-final.trx`: **총 227개·225 통과·0 실패·subprocess 전용 진입점 2개**. 실제 부모 시험이 해당 자식을 실행·종료하며 이 skip으로 필수 중단 시험을 대신하지 않는다. `finalization-regressions-current.trx`: **6 통과·0 실패·0 skip**. 이전 집중 보고서는 보존하고 겹치는 선택의 총계를 더하지 않는다.

첫 전체 gate의 14개 실패는 v3 fixture/응답 기대값(이력 누락 1개·더 구체적인 회수 오류 4개·이전 반영 인자 fixture 8개)와 실제 Windows retained-prefix 교체 오류였다. source 입력 핸들을 닫고 정확한 identity를 재검증한 뒤 교체하도록 수정했으며 별도로 열린 capture alias는 이전 바이트를 계속 읽는다. 새 prefix 복구 시험은 중단된 소유 prefix 정리·alias 보존·unknown copy 거부를 확인한다. 영향받은 14개는 집중 재검증을 통과했다. 최종 prefix 소유 영수증은 payload 쓰기 전에 실제 NTFS copy ID를 결합하며 부분/게시 완료/동일 바이트 외부 교체 4개 사례가 unknown 파일을 보존한다. `prefix-ownership-current.trx`는 17 통과와 부모가 호출하는 자식 진입점 1개다. 기존 공간/시작 관측기는 task v3·관리형 basis v2를 구분하고 이전 task를 거부하며 None/Plastic adapter의 지속 결과 수집도 통과했다. 첫 실패 보고서 `full-build-initial.trx`/`full-build.log`를 최종 gate와 구분해 남겼다.

provider 강제 종료 시험은 합성 불변 트리·실제 NTFS alias를 사용하는 저널 writer다. coordinator 중단 시험은 파일 소스/backend double과 native publisher를 쓴다. 실제 None은 진짜 WinFsp provider·모든 Editor를 유지하며 CLI만 종료한다. 근거 층을 구분하며 모든 mounted provider/OS/재부팅/전원 장애 경계의 통과를 주장하지 않는다.

## 실제 고정 입력과 결과

Unity **6000.3.22f1**, Windows x64/NTFS/WinFsp, 기존 Unity CLI **1.0.0-beta.13**과 기존 캐시 Pipeline 도구를 사용했다. 제품 내부 도구 호출·패키지 설치/패치·새 원본 연동 패키지·Play/Player build는 없다. 사용자 실제 Temp의 짧은 fixture 경로를 사용했다. 준비 Editor를 종료하고 기준본을 한 번 준비한 뒤 아래 원본 Editor를 시작했다.

| 입력 | 원본 PID / 정확한 UTC 시작 ticks | A PID / 정확한 UTC 시작 ticks | B PID / 정확한 UTC 시작 ticks |
| --- | --- | --- | --- |
| Git | 6576 / `639288980328504821` | 32184 / `639288980363824731` | 41200 / `639288980365447807` |
| None | 42988 / `639289002455754606` | 50884 / `639289002470440772` | 50500 / `639289002468863197` |
| Plastic | 22540 / `639289313599324497` | 15904 / `639289313613984766` | 62412 / `639289313630511049` |

Git 원본 `.../Temp/sri30g/p`, home `.../Temp/sri30g/h`, A `d0430a81cc5731d835c9fbe2`, B `2dce419f339124f6621e1e37`, 고정 HEAD `2d701823dc6fee4b9a47d5d9a734e1b4a93b749b`. None 원본 `.../Temp/sri30n/p`, home `.../Temp/sri30n/h`, A `5d1b5a1aacfabf254c3ee28b`, B `45879d0ed429a0056e888dd5`, 관리형 project basis `fb94bb0a900b9fb08d19664c`. 각 순서에서 반환 경로·request digest·불변 기준본·Library 참조는 고정돼 있다.

| 회차 | Git 결과 ID | None 결과 ID | Plastic 결과 ID | 원본 로드 코드 / 씬 / 에셋 |
| --- | --- | --- | --- | --- |
| 중간 1 | `00b6567d49bf8a3fc5d5dc1c` | `2cbf35db0e1e7682906a2817` | `f4cd117791f5bae2f218ce09` | 101 / 101 / 101 |
| 중간 2 | `4b3fd99f41dd68e75b0fbea3` | `901d2ef0c5ef6ed42c7e8697` | `30d4ba7e20fd110b48ac79ae` | 102 / 102 / 102 |
| 최종 | `8b3799b21aca862df4093f5e` | `d73d6cc3646d36e22f72ffec` | `ff2ae252c39dcf28f6a45988` | **104** / 103 / 103 |

각 회차는 task의 이름 있는 dirty 씬/에셋에서 시작해 외부 저장을 수행했으며 원본 dirty 씬/에셋도 저장했다. 무관한 원본 빨간 material은 게시 뒤에도 실제 빨간 값으로 로드했다. 두 번째 회차에 원본 에셋 999를 의도적으로 저장하자 계획·실제 반영이 충돌로 거부하고 999를 보존했다. fixture가 의도한 이전 내용·지원 metadata를 명시적으로 복원한 뒤 새 계획을 검토했다. 덮어쓰기·자동 충돌 해결 기능을 추가한 것이 아니다.

최종 A를 103으로 저장하고 fixture 전용 정상 `quitting` hook이 코드 104를 썼다. A만 종료한 뒤 같은 수집 엔진이 104를 포함했다. 원본/B는 정확히 같은 프로세스를 유지하며 B의 추가 편집·도구 요청도 성공했다. 원본을 종료하기 전에 각 task를 회수했고 `original-survives-both-reclamations.json`으로 최종 내용과 원본 생존을 증명했다. 이후 `cleanup-*`에서 원본 정상 종료·Library/project 복원을 별도 기록했다. 최종 관측은 두 task 경로 부재·fixture Editor 0·일반 NTFS 원본을 확인한다. 독립 결과 버전/영수증은 남으며 각 A의 최종 종료 소스 identity는 최종 게시 결과와 같다. 정리 뒤 현재 바이너리로 같은 최종 수집 요청을 재시도해 원본 Editor 재획득/재수집 없이 최초 결과를 반환했다.

Git 원본 HEAD/branch·index SHA-256은 반영 및 A 회수 전후 완전히 같다. index는 `C61F5548CE2B70E81EB48A4C2D2801BBC0326140CDD169C57D982E2E77920C6E`다. release 뒤 과거 Git 수집/반영도 최종 코드 104를 덮어쓰지 않는다. None은 첫 수집 고정 직후 정확한 CLI 자식을 종료하고 나중 파일/meta를 추가했다. 같은 키 복구 결과 1에는 그 파일이 없으며 결과 2에 포함했다.

## 현재 Plastic 입력과 reader 자격

사용자는 현재 계정에 작은 검증 저장소 생성을 명시적으로 승인했다. `nicks-defense/splinter-ri-20261030@20066124403170@cloud`는 기존 Unity project 아래 별도 저장소이며 기존 production 저장소/작업공간은 변경하지 않았다. fixture seed만 check-in했다. **`1@nicks-defense/splinter-ri-20261030@20066124403170@cloud`**, **35개 entry / 4,334,458 logical B**다. 원본 `.../Temp/sri30p/p`, home `.../Temp/sri30p/h`, A `df3c3336b4c7f40a60d98dc2`, B `8bcceefbf2a220f285036556`, Library 기준본 `e4166f29bf641f01c4ef444d`다. worker는 미등록 상태를 유지했다. 원본의 `.plastic` 관리 파일 3개 hash·loaded changeset 1·selector·등록 목록·빈 lock 목록은 정리 전후 동일하다. 반복 게시에는 check-in이 없고 소스 편집은 미커밋으로 남는다. 실제 서버 잠금 경합 주입은 NotRun이며 자동 시험이 locked/unknown 경로를 확인한다.

설치 cm **11.0.16.10484**로 단일 지원 reader 버전을 교체했으며 버전 우회·호환 fallback은 없다. ABI/라이브러리 hash와 실제 읽기는 **4,259,841 B** segmented binary·**48 B** UTF-16 BOM 파일·**2,354 B** manifest의 길이/MD5와 원본 SHA-256 일치를 확인했다. vendor `Uncompress`가 `None=0`에 0을 반환하는 raw decoder 결함을 발견했다. Raw는 그대로 읽고 `ZLib=2`는 크기를 제한하며 미지원 형식은 거부한다. 수정 전 실패도 별도 보존했다. `plastic-reader-and-apply-current.trx`는 새 raw/전체 segment·손상 형식 시험을 포함해 **41 통과·0 실패·0 skip**다. [reader 보안 자격](security-boundaries.md)을 함께 따른다. 회수 뒤 A의 결과 4개와 B의 결과 2개는 각 task 안에서 검증된 binary 객체 하나를 공유하며 task 간 중복 제거를 주장하지 않는다.

기존 CLI는 descriptor와 listener PID가 정확해도 제품 저장소에서 실행하면 A/B를 발견하지 못했다. 사용자 지시로 양쪽 Window/Pipeline의 Stop Server → Start Server를 실행했고 각 task의 working directory와 명시적 project 경로로 정확한 PID를 찾았다. Editor는 재시작하지 않았다. 컴파일 직후 편집의 network 오류는 후속 쓰기 전에 실제 상태를 관측했으며 컴파일 타임아웃 한 번 뒤에도 코드 102는 실제 로드됐다. 기록된 새 dirty 검사 후 clean 씬 재로드 대화상자는 UI로 처리했다. 최종 파일은 코드 104였지만 처음에는 코드 102가 로드됐고 CLI recompile도 `up_to_date`를 반환했다. 외부 도구의 명시적 script import/compile 뒤 새 MVID의 코드 104를 확인했으며 파일 게시만으로 동기화 성공을 판정하지 않았다. 회수 후 과거 capture/apply 재시도도 최초 결과/영수증을 반환하고 최종 코드 104를 덮어쓰지 않았다. 원본이 살아 있는 동안 양쪽 task 경로가 사라졌음을 확인한 뒤 별도 정리로 원본을 정상 종료하고 일반 NTFS Library를 복원했다. 검증 저장소·원본 fixture·독립 결과는 보존한다.

## 보존한 실패와 같은 identity 복구

Git A가 이미 정상 종료한 뒤 최초 source 검사에서 관리 root `.git`을 거부했다. 관리 경로를 ordinary 검사 전에 제외하도록 수정하고 같은 ID의 stop을 재개했다. Git B 최종 종료에서 한 번 전체 seal 변경을 감지해 결과/저널을 보존하고 같은 요청을 재개했다. source sequence가 2 증가했지만 정확한 지연 쓰기 원인은 확정하지 않았다.

None A의 기존 도구 listener가 처음 포트 선택 충돌을 일으켰다. Editor를 유지하고 명시적 Editor Assets/Refresh로 패키지/설정 변경이나 process 교체 없이 도구 연결을 복구했다. 도구 timeout/기본 eval 제한과 refresh 뒤 씬 reload 대화상자는 제품 성공과 별도 기록했다. 검증된 clean 씬의 대화상자가 eval을 막을 때 UI로 reload했으며 이름 있는 dirty 작업은 먼저 저장했다. tool 목록 개수·고정 포트가 아닌 native process/시작/경로로 identity를 판단한다. 초기 숫자 ticks는 반올림 가능성이 있어 이후 문자열/native 정수를 근거로 사용한다. 잘못된 cleanup CLI 구문은 효과 없이 거부됐고 올바른 정상 정리를 별도 기록했다.

## 비용과 최종 gate

보존 수집들의 store mutation pause는 Git **8.5237–48.3030 ms**(7회), None **7.3187–27.9088 ms**(6회)다. callback drain/대기/pause와 store gate 대기를 각각 남겼다. 명령 시간에는 payload/proof/CLI가 포함되며 중첩 구간을 인과 절감량으로 더하지 않는다. 작은 소스의 관측이며 일반 지연 상한이 아니다.

None 고유 소유 일반 파일 할당(delta/capture/files, NTFS identity로 alias 중복 제거)은 수집 전 **57,901,056 B**, 결과 1 뒤 **72,224,448 B**, 결과 2 뒤 **72,267,056 B**, A 회수 뒤 **37,023,384 B**, 두 작업 회수/정리 뒤 **523,944 B**다. 마지막 표본의 결과 파일 할당은 **399,096 B**, 나머지는 보존한 capture manifest/cost/materialization metadata이며 표본의 live capture alias는 **0개**다. 관측 증가분은 물리 peak나 전체 프로젝트 footprint가 아니다.

Git 회수 후 같은 소유 제어/결과 파일 표본은 **264,440 B**다. 별도로 보존 결과 ref에서 도달하고 최초 HEAD에서 도달하지 않는 loose Git 객체 **39개**의 실제 NTFS 파일 할당은 **22,568 B**였고 packed 객체 귀속은 필요하지 않았다. Git 수집 전/전환 할당은 표본이 없으며 source/공유 basis·디렉터리/MFT·전체 시스템 peak·RAM은 unknown이다. 공개 v3 읽기 전용 공간 장부는 선택 task의 전체 제어/로그와 공유 Git root까지 별도 측정해 Git 3,353,736 B, None 5,501,176 B와 모든 root complete를 확인했다. 이 넓은 범위에는 앞선 delta/capture/files 표본이 제외한 로그/제어 자료가 포함되므로 같은 범주나 추가 payload 비용으로 해석하지 않는다. 속도/RAM 절감률은 주장하지 않는다.

Plastic store mutation 정지는 **6회 수집에서 10.8598–41.2505 ms**였다. 고유 delta/capture/files 할당은 수집 전 **54,165,504 B**, 결과 1 뒤 **59,060,328 B**, 결과 2 뒤 **62,055,960 B**, 결과 3 뒤 **59,813,432 B**, A 회수 뒤 **31,604,608 B**, 양쪽 회수/정리 뒤 **9,077,048 B**다. 최종 결과 파일 할당은 **8,944,008 B**이고 완료 표본의 live capture alias는 **0개**다. 두 ID를 모두 포함한 수정 장부는 **12,067,896 B**·root 5개 모두 complete다. 앞선 한 ID 호출은 좁은 측정으로 보존하며 양쪽 합계로 읽지 않는다. 디렉터리/MFT/system peak는 제외되며 속도/RAM 절감 주장이 아니다.

현재 reader 재검증 뒤 최종 `pwsh ./scripts/build.ps1`: **총 2,054개·2,018 통과·0 실패·조건부 skip 36개**, `artifacts/ri-tests/full-build-plastic-qualified.log`와 `.trx`. Locked restore·Release build의 경고/오류는 0개다. 필수 RI 수집/중단 부모 시험은 실행됐으며 skip은 선택 비용/native 입력·subprocess 전용 자식 진입점으로 RI 행을 대체하지 않는다. 이전 2,052개 gate는 `full-build-complete-source.*`로 보존한다. 양언어 UTF-8/링크/source-index 검사와 `git diff --check`도 통과했고 sibling Unity 저장소는 clean이다. 실제 MCP·Play/Player build·대형 입력·다른 Unity/client/OS 버전·실제 서버 잠금 경합 주입·OS/전원 장애 행렬은 미검증으로 유지하며 속도/RAM 절감 주장은 추가하지 않는다.

커밋 준비: 기능 소스 `f5f7b9b5d8c1fb1a00c0aa8be4726b27a8941759`, 현재 reader/소스 `b51f18fb6d4515a149975bbe6b3bf334804e1217`. 전체 gate 당시 소스 hash는 `fullGateSourceManifest`에 보존하고 `currentSourceManifest`는 커밋된 LF blob과 일치한다. gate 뒤 변경은 저장소 규칙의 LF 정규화와 capabilities의 자격 설명뿐이다. 현재 Release build와 **계약 시험 62개(실패/skip 0개)**가 통과했고 응답/허가서 **99개 유효·0개 무효**다. 이 설명/줄바꿈 변경 때문에 Unity/native 자격을 재실행하지 않았다.
