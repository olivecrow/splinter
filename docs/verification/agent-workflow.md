# 최소 AI 작업 연결 검증

[스킬 원본](../../skills/splinter/SKILL.md)의 CLI 발견·반환 경로 사용·동일 작업 재개·명시적 종료를 로컬 등록 fixture 한 작업으로 확인했다. 이 Editor 없는 개발 단위는 **scoped-complete-with-limitations**로 마감한다. Editor의 현재 별도 자격은 [대표 A/B 사용](representative-agent-workflow.md)과 M0 추적표를 따른다. 자동 설치·Codex 스킬 발견·대화 binding의 자격이 아니다. 현재 지침을 다시 시험하기 위해 전체 Unity/VSS 장애 행렬을 열지 않는다.

## 고정한 입력과 증거

- 제품 실행 코드: `development`의 `9e10b4a92e2c18c38d14de0fccf2979e26be315c`. 이 단위는 소스 스킬과 안내/계획 문서만 추가·수정하며 제품 API·상태/schema를 변경하지 않았다.
- 지침: `skills/splinter/SKILL.md`, SHA-256 `8CA432EC0AB2F133519ED4B851FE33A51846E436ABA6ACBA170FFF395E69B3AF`. 현재 대화의 에이전트가 원본을 읽어 적용했다. 전역/프로젝트 설치나 자동 선택은 수행하지 않았다.
- 로컬 원본: `T:/ua-source`, clean branch `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, 추적 파일 94개. 기존 warm Library와 등록 home·제한 서비스를 사용했다.
- home: `artifacts/storage-authority/ua-20260928/home`. 새 작업 key `agent-20261001-190502-766`, ID `25bb092e3f55d5dda58aaccb`, request digest `a1d2e87aad35e16b0d548f19`, VSS ID `{D9EC72CE-81A0-49FD-96E0-E1DCA4A42013}`.
- 원시 evidence: `artifacts/agent-workflow/agent-20261001-190502-766/`. `plan.json`, `before.json`, `decision.json`, 발견/schema 응답, `binding.json`, 편집·재개·폐기 응답, CLI별 monitor와 `result.json`을 보존한다. binding은 시험 인계 기록이며 새 제품 상태 계약이 아니다.

## 관측한 사용 흐름

| 단계 | 실제 실행과 결과 |
| --- | --- |
| direct 판단 | 스킬/문서 작성에는 독립 실행이 필요 없어 기존 제품 checkout에서 직접 작업. fixture의 기존 작업 ID 목록 불변, 새 workspace/Editor와 Codex worktree 0 |
| 명령 발견 | 기존 `capabilities`, `schema workspace.ensure`, `schema workspace.discard`, 읽기 전용 서비스 status 모두 Succeeded. 필요한 CLI usage·입력·retry를 응답에서 찾음 |
| 작업 생성·첫 편집 | ensure가 반환한 `data.workspace.path`를 shell working directory와 절대 파일 대상으로 사용. 루트 시험 파일에 44 bytes 기록·flush, 원본에는 파일 없음 |
| 새 CLI의 동일 요청 재개 | 저장한 project/key/base/storage/home으로 새 프로세스의 ensure 실행. ID·key·home·root/control·digest·base revision·snapshot·provider PID/시작 시각 동일. 첫 편집 hash 유지, source 변경 경로도 해당 파일로 관측 |
| 재개 후 계속 편집 | 같은 반환 경로에서 34 bytes 추가·flush, 최종 78 bytes. 기존 코드 파일 읽기 hash도 원본과 일치. Editor/validation/checkpoint는 생성하지 않음 |
| 명시적 종료 | 저장한 ID/intent/digest/home의 discard가 첫 요청에 Succeeded·`discarded`/`complete`. 일반 release나 성공 검증으로 위장하지 않음 |
| 회수·보존 | 실제 root·delta 부재, 해당 provider 종료, Git 등록/refs 원상태. 서비스의 정확한 snapshot 관측은 missing, reclamation released. 소유 프로세스·메모리·핸들 0; 원본 94개 hash·HEAD/branch/clean·기존 record 파일 117개와 이전 작업 ID 불변 |

일반 CLI 7회 모두 Windows 관리자 권한이 아니며 추가 consent 프로세스 관측은 0이다. 50 ms 샘플링과 자동 elevation 없는 경로를 확인한 범위다. 서비스 설치/업데이트·grant 변경·원본 편집·Unity·VIVOBOOK은 이 실행에 포함하지 않았다.

## 제한과 남은 확인

explicit/direct 사례의 실제 에이전트 사용과 새 CLI 재개를 확인했다. busy/unknown 판단은 스킬 규칙이며 실제 다른 작성자 점유·새 대화/압축·동시 대화 접속을 이번 실행으로 검증하지 않았다. workspace CLI의 짧은 잠금이 임의 파일 편집을 대화별로 배타 제어한다고 선언하지 않는다. 자동 스킬 발견·설치와 자동 경로 binding은 후속이며 SW 전체로 승계하지 않는다.

전체 `Win32_ShadowCopy` 목록은 sandbox에서 접근 거부, 일반 권한의 sandbox 밖 조회에서 초기화 실패로 관측할 수 없었다. 전역 snapshot inventory·비소유 snapshot 전체 비교는 **unknown**이다. 이를 위해 새 관리자 작업이나 서비스 권한 확대를 하지 않았다. 해당 task의 기존 서비스 관측과 Git/원본/기존 기록 보존만 위 표에 자격으로 남긴다.

Unity Editor·컴파일 오류/수정·보존형 종료/통합·dirty guard·A 종료 뒤 B 지속과 공간/시간 채택 판단은 별도 [대표 A/B 실행](representative-agent-workflow.md)에서 확인했다. 이 Editor 없는 기록의 자격으로 승계하지 않는다. 네이티브 강제 중단·실제 재부팅/전원/공간 고갈·ARM·대형 입력은 별도 지원 확대다.

## 자동 검증

스킬의 frontmatter/이름/description과 미완성 placeholder를 `skill-creator/scripts/quick_validate.py`로 검사했다. 이는 구조 검사이며 실제 사용 자격은 위 원시 응답/파일 관측에서 얻는다. 필수 `scripts/build.ps1`의 locked restore·Release는 경고/오류 0, 자동 시험은 **총 745 / 성공 744 / 실패 0 / 선택적 ReFS 건너뜀 1**이다. sandbox 안의 첫 restore 실패 로그를 보존하고 일반 권한의 sandbox 밖에서 같은 표준 빌드를 완료했다. `build.log`, `build-escaped.log`, TRX와 `automated.json`을 해당 evidence에 보존한다.

전체 문서 58개와 변경 파일 10개를 UTF-8로 확인했고, 변경 파일의 로컬 문서 링크와 Git diff 공백 검사를 통과했다. 제품 코드/계약 변경이나 별도 지침 모사 시험은 추가하지 않았다.
