# Plastic 원본 반영 검증

[English](../../verification/plastic-original-apply.md)

[고정 범위](../plans/plastic-original-apply.md)와 [현재 계약](../plastic-original-apply.md)을 따른다. Windows x64/NTFS·cm 11.0.16.9915·Unity 6000.3.22f1의 작은 recorded Plastic A/B 한 묶음을 **scoped-complete-with-limitations**로 마감한다. 작업 Editor는 실행하지 않았고, 원본 Editor의 종료·반영 리소스 import·시험 변경 복원·재열기는 실제 Unity CLI로 확인했다. PPB의 병렬 Editor/MCP 자격과 구별한다.

| ID | 질문 | 상태 |
| --- | --- | --- |
| POA-01 | 전체/선택 계획·부모/Unity meta·schema·기존 digest 보존 | Passed, 계약/파일 시험과 실제 계획 |
| POA-02 | 등록/selector/잠금 unknown·원본 Editor·충돌 거부 | Passed, 등록/잠금의 fake 관측 거부·실제 관리 핸들·원본 Editor/파일 충돌. 실제 외부 서버 잠금 생성은 NotRun |
| POA-03 | 동일 intent 복구·관리 핸들 배제·미완료 회수 차단 | Passed, 실제 NTFS 게시에 주입한 응답 유실과 같은 선택 intent 복구. 전체 WinFsp/OS 장애 주입은 NotRun |
| POA-04 | 기존 cs2의 새 실제 A/B 선택/전체 반영·peer 격리 | Passed, 실제 게시 후보의 고정 A/B. 마지막 불완전 asset/meta 삭제 거부 추가는 자동 파일 시험; 최종 후보의 새 실제 재게시 묶음은 NotRun |
| POA-05 | A release/B discard·시험 변경만 원본 복원·공존 | Passed, 실제 제품·현재 상태/결과·원본 Unity 확인 |

cm과 mount가 fake인 Windows 파일 시험은 실제 Plastic 서버나 WinFsp A/B의 자격이 아니다. 원격 잠금과 로컬 게시의 분산 원자성, partial/dynamic·대형·OS/전원 장애·ARM·Play/build·released 결과 수입은 미자격이다. 과거 C: VSS 보류와 MCP 실행 시간 제한을 유지한다.

## 고정 입력과 실제 결과

원본은 `C:\Users\olive\Desktop\Development\splinter-unity-plastic`, base는 `2@splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud`, GUID는 `f3e87fff-e66c-4820-aec7-ebe2a633962f`다. home은 `C:\splinter-poa1005`이며 새 등록/서버 쓰기/VSS는 사용하지 않았다. 원시 근거는 `artifacts/plastic-original-apply-20261005/`와 PPB 폴더의 `original-poa1005-*`/`poa1005-*source.json`에 보존한다.

| 작업 | id / digest | 독립 결과 | 최종 상태 |
| --- | --- | --- | --- |
| A | `572060a3e4de722d02289c90` / `ecd0b666472170eae068b378` | `sha256:5bb52b9e293b6168929c4215e02ea457667895a500b6988449660b0b77b5f48d` | released, selected apply completed |
| B | `57c8eb493bde0a6defd690aa` / `c2420bef2608d104a3bb9099` | `sha256:220de59757d3a32c5493f77e8e98c2af76a401625d6e9fbdc3c381c724df3cf6` | discarded, full apply completed |

A의 전체 계획은 8개, `--path Assets/Readme.asset --path Assets/SplinterApplyQualification1005/Selected.txt` 계획은 6개 변경/guard를 포함했다. 선택 계획의 digest는 `sha256:15aba30665919dc63ef3abfe11db81e0f2a9cfade4ed83b346995cab5d60cea7`, intent는 `poa1005-A-selected`다. Selected/asset·folder meta와 Readme 주석만 게시했고 Withheld와 meta는 독립 objects에만 남았다. B는 원본에 A 주석이 생긴 뒤에도 이전 Readme와 자기 리소스를 읽었다.

B의 소유 시험 충돌은 `OriginalApplyConflict`로 intent/원본 쓰기 전에 거부됐다. 내용이 일치하는 그 파일만 제거한 뒤 4개 항목의 전체 계획 `sha256:b25645c2e4eff7b7bfe5d0a571a57fe7661aa7644592da3fa98e6e8efda79e95`와 `poa1005-B-full`로 게시했다. A의 선택 결과는 유지됐다. 실제 서버 잠금 관측은 빈 inventory였고, Locked/Retained/unknown의 긍정 거부는 parser/fake 시험이다.

원본 Editor가 열린 첫 계획은 `PlasticOriginalUnavailable`로 거부했고 intent는 없었다. 최초 지연 종료 콜백은 Editor를 종료하지 않았다. 다시 상태를 확인한 직접 정상 Exit는 응답의 `Invalid response format`과 실제 PID 종료를 함께 기록했으며 강제 종료하지 않았다. 동일 쓰기를 timeout으로 반복한 사례가 아니다.

실제 원본의 Readme/metadata는 Archive+NotContentIndexed(8224), recorded 기준은 Archive(32)여서 첫 계획이 내용 일치에도 충돌했다. 해당 indexing 비트를 원본 값으로 보존하도록 수정한 같은 A 계획은 충돌 0, 실제 게시도 성공했다. 마지막 후보에는 asset/meta 한쪽만 삭제하는 결과의 거부를 추가하고 자동 파일 시험으로 검증했다. 새 A/B로 실제 게시를 자동 반복해 이 추가 거부의 native 자격으로 표시하지 않는다.

## 정리·Unity·비용

`final-cleanup-proof-verified.json`은 mount/provider/delta 부재, 로컬 basis released, 두 apply completed, 모든 baseline/result object SHA-256 일치와 provider 34352/55572 및 직접 자식 부재를 확인한다. 과거 mount 영수증은 유지되는 소유 기록이며 실제 mount 존재로 해석하지 않는다. 첫 계측의 잘못된 필드/이벤트 이름은 `final-cleanup-proof.json`에 실패 근거로 보존했고 현재 관측으로 다시 확인했다.

A 회수의 실제 releasing 이벤트는 UTC 16:45:59.768~16:46:02.160이며, B의 25회 읽기 중 **2회**가 그 구간에 실제 실행돼 모두 일치했다. 전체 release 명령의 21.16초와 물리 회수 이벤트 구간을 구분한다. 이것은 파일 읽기의 중첩이며 기존 PPB의 MCP test/release 실행 중첩 미확인을 해소하지 않는다.

원본 재열기 89888/639267293612476820에서 Selected/B TextAsset 내용, GUID `27e34c93796a40d3a99cb11e68001005`/`533281f1093d469ba6b28301bc001005`와 folder GUID `953efc528fb74d71bf6a4ed958001005`가 일치했다. Withheld는 없고 compile/updating/failed=false였다. 정상 종료 뒤 해시가 일치하는 소유 시험 파일만 제거하고 Readme bytes를 복원했다. 기존 37개 SHA-256·Packages 2파일·등록 GUID/hash는 모두 일치했다. 마지막 원본 77676/639267294741020073은 clean/nonplaying·compile/updating/failed=false·시험 리소스 부재·일반 NTFS Library다. 최초 최종 조회의 .NET 해시 API는 Unity eval 컴파일에서 거부돼 프로젝트 파일을 바꾸지 않고 지원 API로 조회를 수정했다.

기존 Git Editor 34200/639267143482280358과 공유 8080 서버 73512/639267092568061834는 동일 PID/시작 시각을 유지한다. 초기 payload 복제/read cache는 A/B 각각 0이다. 준비 A/B 41.10/39.14초, checkpoint 65.61/48.88초, A 선택 apply 33.33초, B 전체 apply 25.60초, A release 21.16초, B discard 8.13초다. 수집 당시 delta는 A 6690B(할당 8192B)/B 3039B(할당 4096B); 최종 delta 0이고 control 할당은 422936/427056B, 보존 objects는 314656/310376B다. 공유 runner/evidence와 원본 Library를 별도 분리했다. 전체 순간 peak·공유 VSS 귀속은 unknown이며 RAM 절감률은 이 범위의 요구가 아니다.

최종 `pwsh scripts/build.ps1`: locked restore/Release 경고·오류 0, **978 Passed + 1 Skip / 979**, 실패 0. `final-build.trx`와 `final-candidate.json`에 현재 회귀와 소스 hashes를 보존한다. UTF-8과 diff 검사는 별도로 확인한다.
