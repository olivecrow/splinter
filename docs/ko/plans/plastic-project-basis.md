# Plastic 프로젝트 기준본·Library 참조의 다음 작업 범위

[English](../../plans/plastic-project-basis.md)

2026-10-04 확정 범위는 **Plastic에 기록된 프로젝트 버전 + 원본 Library의 불변 기준 + 작업별 범위 저널**을 연결하고 작은 실제 Unity A/B에서 검증하는 것이다. 상위 [VCS 기준본 전환](project-scoped-basis-and-reclamation-review.md)의 VP-03과 Plastic의 VP-04/05를 소유한다. 사용자 요청의 구현/고정 A/B를 2026-10-05에 scoped-complete-with-limitations로 한정 마감했으며 현재 실행 판정/비용/제한은 [검증](../verification/plastic-project-basis.md)이 소유한다. RAM 절감은 합격 조건에서 제외하고 실제 작업량에 따른 비용만 기록한다.

## 현재 완료 경계

| 경로 | 현재 구현과 자격 | 이번 작업과의 관계 |
| --- | --- | --- |
| 공통·VCS 없음 | 생성/재개·격리·결과 보존·release/discard와 작은 Editor 없는 원본 반영을 한정 마감. 기준본은 기존 VSS 경로 | 기본 수명주기를 다시 구현하지 않는다. VCS 없는 프로젝트 기준본 전환은 사용자 결정대로 후순위 |
| Git | fixed commit reader·범위 저장·원본 Library 참조·실제 MCP/리소스/씬/current-state tests·결과/회수·Library 복원을 한정 마감 | 공통 계약과 Library 구현을 재사용한다. [GL 검증](../verification/git-library-reference.md)의 RAM 절감 미확인·MCP 실패 상세 소실·준비/복원 비용과 전체 peak unknown은 유지 |
| Plastic | regular/static 등록·관리 root 격리·VSS 기반 A/B·독립 파일 결과·release/discard를 한정 마감 | changeset reader와 Library 연결의 자격은 PPB를 따른다. 원본 전체/선택 반영은 별도 POA 계약/자격이며 기준본 전환과 구분 |

이 Plastic 경로를 한정 마감했다. 이 단위의 마감을 VCS 없는 새 기준본·Plastic 원본 반영·실제 Unity RAM 절감까지 완성한 것으로 표시하지 않는다. 기존 C: VSS pending도 별도 [회수 근거](../verification/plastic-compatibility.md#과거-c-작업의-승인된-동일-요청-회수-재시도-2026-10-04)를 유지한다.

## 포함하는 구현

1. **고정 Plastic 기준본 reader**: 정확한 서버/저장소 identity·changeset·project subtree·파일 revision/크기/내용 식별을 receipt와 요청 digest에 고정한다. branch 최신값이나 살아 있는 원본 파일을 fallback으로 읽지 않는다. 서버 내용 접근은 일반 사용자 권한이며 Core에는 공통 opaque identity와 불변 tree 계약만 둔다.
2. **프로젝트 범위의 overlay 연결**: 별도 `plastic-snapshot` 선택을 CLI/schema·Runtime·Windows provider에 연결한다. 생성/읽기/재개/정리/결과 보존/회수에 새 VSS 요청·전체 checkout·payload disk hydration·파일 전체 copy-up을 사용하지 않는다. 기존 `ntfs-snapshot` 상태·결과·pending 영수증은 유지하며 자동 이관하지 않는다.
3. **원본 Library 참조**: 기존 `library prepare/status/restore`와 graft/dependent/read-proof를 Plastic 경로에 연결한다. 사용자 승인한 Library 전용 관리형 mount 방식을 재사용하고 소스는 원본 NTFS 위치에 남긴다. 원본/A/B가 같은 불변 Library를 읽고 각각 범위 저널에 쓰며, 마지막 작업 회수 뒤 원본 Library를 일반 NTFS로 복원한다.
4. **기존 격리·결과·회수 유지**: `.plastic` 등 관리 root를 worker에서 차단하고 새 Plastic workspace 등록·서버 잠금·설정/인증 변경을 만들지 않는다. home은 모든 VCS 관리/Plastic 등록을 상속하거나 겹치지 않는 같은 NTFS 볼륨의 짧은 경로를 사용한다. 독립 파일 결과·seal·같은 요청/intent 복구·원본/peer 보호·명시적 release/discard와 자원 0 관측을 유지한다.
5. **계약과 사용 절차**: 입력/출력 schema·fake/적합성·capabilities·현재 기능 문서·AI 스킬·AGENTS·검증 기록을 함께 갱신한다. 현재 버전 하나만 지원하고 손상/unknown/미지원은 보존 후 거부한다. Git과 VCS 없음의 기존 회귀를 유지한다.

## 먼저 해결할 읽기 비용과 입력 조건

설치된 CLI `11.0.16.9915`의 `cm help ls`는 고정 changeset의 tree/revision 목록을, `cm help cat`은 revision의 raw stdout 읽기를 제공한다. [공식 list](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/list)·[getfile](https://docs.unity.com/en-us/unity-version-control/uvcs-cli/getfile)도 이 접근을 문서화한다. 현재 도움말의 `cat`에는 offset/length 선택이 명시되어 있지 않으므로, 파일 내용에 접근할 수 있다는 사실만으로 효율적인 range reader가 검증됐다고 읽지 않는다.

첫 구현 전에 bounded stream의 순차·비연속 읽기, 재연결, hash/길이·binary/CRLF 의미와 실패 buffer 미게시를 확인한다. 같은 파일의 작은 읽기가 전체 revision 전송을 반복하는지, client/server cache·임시 파일·프로세스 메모리를 얼마나 쓰는지 관측한다. Splinter의 payload 저장 0과 네트워크 전송·외부 client cache 비용을 구분한다. 관측한 병목이 작은 실제 Unity 작업을 막으면 그 reader만 수정한다. 전체 파일 메모리/disk cache·VSS·mutable 원본 참조로 우회하지 않으며 접근 권한/서버 장애를 VCS 없음으로 fallback하지 않는다.

기존 [사전 probe](project-scoped-basis-and-reclamation-review.md#고정-vcs-내용의-사전-확인)의 Plastic changeset 1은 26개 파일/70,004B이고 **Assets가 기록되지 않았다**. 실제 구현 실행 시 먼저 읽기 전용으로 현재 등록·changeset·필수 소스 포함 여부를 새로 확인한다. 현재 원본의 Assets·Packages·ProjectSettings와 필요한 `.meta`/도구 패키지가 기록된 명시적 기준을 선택한다. Library와 dirty/private 소스의 동등성을 추측하지 않는다.

필요한 파일이 기록된 기준이 없으면 그 정확한 입력 준비가 Unity 검증의 선행 조건이다. 목록/차이와 필요한 check-in 내용을 제시한 뒤 별도 승인된 준비로 다룬다. 임의 check-in/update·원본 파일 생략·전체 복제로 기준을 맞추지 않는다. 입력이 준비되지 않아도 common fake/reader 적합성까지는 진행할 수 있고 실제 Unity는 NotRun으로 남긴다. 계획 확정 당시에는 새 저장소·changeset·home을 생성하지 않았다. 이후 구현 요청과 사용자가 승인한 정확한 37개 입력 check-in으로 기존 시험 저장소의 changeset 2를 고정했다. 실제 한 A/B는 순수한 같은 C: NTFS의 소유 `C:\splinter-ppb1004` home과 `ppb1004-library` key를 사용하며 새 저장소·원본 내용 변경·추가 서버 이력 쓰기를 포함하지 않는다. A/B의 MCP 도구 패키지는 각 private 작업공간에만 기존 검증 버전 v10.2.0/revision 30d22075093d1d35dfb0091c1c7550e9ad948577를 추가한다.

## 실행 순서와 종료 기준

| ID | 선택한 단위 | 종료 기준 | 현재 상태 |
| --- | --- | --- | --- |
| PPB-01 | 기준과 reader의 사전 확인 | 정확한 project/repository/changeset/revision·필수 소스 manifest 고정, raw 내용 의미·범위 읽기의 전송/cache/메모리 비용 확인. 권한/소실/손상/응답 유실에서 내용 미게시 | Passed with limitations. cs 2 전체 길이/hash 확인, 외부 cache/network unknown |
| PPB-02 | 공통 계약·Plastic overlay 수명주기 | schema/fake·소유 receipt·같은 요청 재개·원본 변화 뒤 기준 유지·관리 root 차단·결과/회수 연결. 새 VSS/checkout/hydration/copy-up 0, Git/none 회귀 통과 | Passed. 최종 필수 회귀 961 Passed/1 선택적 skip |
| PPB-03 | Library 연결과 작은 native 확인 | Git과 같은 불변 Library/개별 저널·의존성·A 회수 중 B 지속·마지막 NTFS 복원. 바뀐 부작용 경계의 중단/같은 요청 복구와 원본/peer 보호 확인 | Passed, 한정 실제 Unity. checkpoint 중 B 쓰기·release 뒤 B tests·마지막 NTFS 복원. 새 native 강제 중단 행렬은 미자격 |
| PPB-04 | 실제 Unity A/B 한 묶음 | 원본·A·B의 정확한 경로/PID/start, 각 MCP 연결/명시적 reload 재발견, 독립 texture/importer·scene 생성/편집·현재 상태 tests와 test job 겹침, A 종료 중 B 후속 작업. 실패·timeout·결과 유실을 기록 | Passed with limitations. A/B 각 3/3, job 겹침 9.84초, B peer 3/3. 긴 MCP 쓰기 응답/초기 준비 비용 보존 |
| PPB-05 | 독립 결과·폐기·최종 정리 | A checkpoint/release와 B checkpoint 뒤 명시적 discard로 필요한 독립 결과를 보존. 소유 Editor/자식/provider/handle/mount/delta/basis 의존성 부재를 확인하고 원본 Library 복원·원본 재열기, 등록/원본 변경/다른 작업/공유 서버 보존 | Passed, 한정 실제 Unity. 독립 결과·소유 회수·일반 NTFS 복원/재열기·원본/등록/기존 Editor·서버 보존 |
| PPB-06 | 비용과 최종 판정 | 최초·재개·편집·수집·회수·Library 복원 시간과 작업 delta/shared 기준/기존 데이터/결과/외부 cache·전송을 분리. 동일 구간의 원본/A/B Editor·provider Private Bytes/resident/PSS 추정. 필수 build·UTF-8·판정 기록 | 계측/기록 완료. RAM 절감 요구 없음, 전체 peak·외부 cache/전송·공유 VSS 귀속 unknown 유지 |

PPB-01/02부터 연결하고 Library의 변경된 경계를 작은 native로 확인한 뒤, 입력을 고정한 새 실제 Unity A/B **한 묶음**으로 PPB-04~06을 마감한다. 기존 Git/VSS/Codex 비교군이나 전체 장애 행렬을 다시 실행하지 않는다. 테스트는 외부 MCP/Unity CLI가 현재 A/B 파일을 대상으로 수행하며 Splinter는 컴파일/게임 검증 gate를 추가하지 않는다. 원본 Editor의 최초/마지막 정상 종료·Library만 전환하는 예외는 이미 승인한 계약을 유지하고 dirty 변경을 자동 폐기하지 않는다. 기존 공유 MCP 서버는 대상별 연결을 확인하며 수명을 자동 변경하지 않는다.

완료는 위 범위의 구현/회귀와 고정 실제 A/B의 결과·정리·비용 근거가 있을 때 `scoped-complete-with-limitations`로 판정한다. RAM 절감률을 보장하거나 미절감/unknown을 0으로 바꾸지 않는다. 중요한 증거가 NotRun/실패이면 해당 상태와 이유를 남기고 실제 검증 완료를 선언하지 않는다. 실패에서는 같은 id/home/기준/intent와 증거를 보존하며 필요한 결함만 수정하고, 새 입력·반복 확대에는 별도 질문/종료 기준을 둔다.

고정한 한 묶음의 판정은 위 한정 마감이다. 초기 준비와 Library 복원 비용, 긴 MCP 쓰기 실패 응답/실제 완료, A release와 B test job 실행 겹침 미확인·요청 지연 원인 미확정은 검증 문서의 제한으로 유지한다. 같은 시험을 반복해 제한을 지우거나 추가 자격으로 확대하지 않는다.

## 이번 단위 뒤에 남는 작업

- **Plastic 원본 반영**: [후속 고정 범위](plastic-original-apply.md)가 전체/선택 로컬 게시·3자 비교·관리 identity/잠금·원본 Editor 보호·부분 게시 복구를 소유한다. PPB의 독립 결과 자격으로 반영 성공을 승계하지 않는다. VCS check-in/merge는 AI와 기존 도구의 책임이다.
- **VCS 없는 프로젝트의 새 기준본**: 사용자 결정대로 후순위다. 기존 VSS 수명주기와 일반 폴더 원본 반영은 유지한다. Library 기능이 공통 계약이라는 사실을 `source=none`의 현재 연결/자격으로 승계하지 않는다.
- **과거 VSS의 회수 제약**: 기존 C: pending과 비소유 snapshot 보호는 별도 문제다. 새 Plastic 기준본의 VSS 사용 0으로 과거 snapshot이 회수됐다고 표시하지 않는다.
- **추가 지원·성능**: Gluon/partial/dynamic·혼합/중첩 VCS·Plastic 서버 이력 변경·전용 installer/service/grant·OS/ARM/대형 전체·Play/build·일반 RAM 절감 연구·새 HTML 보고서는 포함하지 않는다. 실제 사용의 안전 결함/차단이나 명시적 요구가 생기면 필요한 단위를 다시 선택한다.
