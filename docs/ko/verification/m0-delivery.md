# 한정 M0 납품 판정

[English](../../verification/m0-delivery.md)

**등록된 작은 Windows x64 NTFS/Git/Unity 환경의 개발 CLI·소스 스킬 버전을 scoped-complete-with-limitations로 납품 가능하다고 판정한다.** 시작점은 [AI 시작 안내](../agent-start.md), 실행 상태는 [M0 추적표](../plans/validation-matrix.md)다. 일반 설치·자동 binding·대형 효율·ARM A/B·전체 OS 장애 지원은 이 판정에 포함하지 않는다.

후보는 `development @ 95b48b4`이며 `plan.json`이 전체 Git SHA를 고정한다. 이 단위의 변경은 시작 안내·근거/범위 정리 문서뿐이며 제품 코드·schema·의존성·설치된 서비스는 변경하지 않았다. [대표 사용](representative-agent-workflow.md)의 `4c7bd6c` 뒤에도 실행 코드는 같다. 과거 바이너리의 native 시험 전체를 새로운 후보의 통과로 표시하지 않는다.

원시 evidence는 `artifacts/m0-delivery/delivery-20261001-205002-403/`에 있다. `plan.json`, `recovery-source-audit.json`, `current-recovery-tests-audited.json`, `automated-audited.json`·`automated.trx`·`build.log`, 읽기 전용 CLI 응답/monitor와 문서 검사를 보존한다. 소스 hash 대조는 근거의 적용 경계를 판단하는 자료이며 실행/내구성 시험을 대신하지 않는다.

## 현재 후보의 M0-06 근거

필수 `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, **총 745 / 성공 744 / 실패 0 / 당시 제외 시험 건너뜀 1**이다. TRX에서 아래 고정 조건의 170개 성공을 대조했다. 새로운 시험이나 harness를 추가하지 않았으며 아래 합성/실제 파일·자식 종료를 native VSS 전체 장애 자격으로 읽지 않는다.

| 현재 자동 회귀 | 선택한 성공 수 | 확인한 경계 |
| --- | --- | --- |
| `PreparationInterruptionTests.KilledClient…` | 19 | 실제 자식 CLI 종료와 durable fake 준비 receipt·같은 요청 재개, 중복 identity/부작용 방지 |
| `JournalInterruptionTests`·`JournalReplayTests` | 69 | 합성 불변 트리의 실제 자식 종료·승인 prefix/불완전 tail·재생/손상 거부. VSS/WinFsp callback/전원 시험은 아님 |
| `RecoveryContractTests`·`StateWriteDiagnosticTests` | 27 | 손상/잠긴 기록·경로 불일치의 보존/unknown, 실제 Windows 교체/append 오류와 게시 상태, 같은 소유 diagnostic Git CLI의 재개 |
| snapshot coordinator 응답 유실·unknown·seal 회귀 | 10 | capture/release 유실 뒤 같은 identity 재조정·unknown 보존·결과 수집 전후 쓰기 거부. backend는 fake |
| storage authority 응답/intent 회귀 | 13 | 새 processor의 같은 요청 재사용·pending 게시·부분/위조 요청 거부·지연 회수. native operation은 fake |
| discard·Git/delta·별도 authority gate 회귀 | 32 | stop/unmount/source/delta/snapshot 응답 유실의 같은 intent 재개, unknown/변경된 요청 거부·durable 자원 부재 게시, 실제 임시 Git/파일 삭제의 중단 재개·helper authorization 검사 |

추가로 [현재 대표 A/B](representative-agent-workflow.md)는 늦은 Library 쓰기로 첫 수집을 거부한 뒤 **같은 A**의 안정된 재수집/독립 결과/release와 미커밋 컴파일 실패 B discard를 확인했다. [서비스 적용 후 폐기](workspace-discard.md)는 최초 권한 거부 후 같은 intent 재개·완료 요청 중복·peer 보존·실제 VSS 회수다. 지원 경로에서 허위 완료/승인 쓰기 손실·원본/peer 훼손을 새로 관측하지 않았다. 미확정 상태 게시 오류는 해당 기록의 제한으로 남긴다.

M0-06은 **현재 계약의 보존·동일 요청 재시도·손상/unknown 거부와 위 한정 실행 조건**으로 마감한다. 현재 바이너리의 모든 native 강제 중단·VSS 내부 호출/OS flush·재부팅/전원/물리 공간 고갈을 완료 조건에 추가하거나 보장하지 않는다. NR 전체 InProgress·미실행 경계는 유지한다.

## 과거 native 근거의 재사용 경계

기록된 raw 소스 hash와 현재 파일을 대조했다. 숫자는 각 manifest의 전체/일치/변경/제거 파일 수이며 새 파일이나 전체 환경의 동등성을 의미하지 않는다. 제거 4개는 제외한 DAG/plan API의 파일이며 손실된 실행 증거가 아니다.

| 기존 근거 | hash 대조 | 현재 판단 |
| --- | --- | --- |
| [준비 CLI 26개](preparation-recovery.md), `nr02-20260930d/code-and-environment.json`의 scoped 22개 | 22 / 14 / 8 / 0 | mount/client/server/backend·기존 준비 시험 파일 일치. 조정/진단/명령 계약은 바뀌었으므로 현재 native 26개 통과로 승계하지 않음 |
| [공통 저널 58개](ntfs-recovery.md#nr-03-공통-저널-경계의-실제-프로세스-종료) | 229 / 198 / 27 / 4 | `JournalDeltaStore`·`JournalReplayReader`·atomic 게시 구현 일치. 해당 합성 저널 근거만 재사용; 당시 WinFsp와 현재 코드는 달라 현재 kernel 자격으로 확대하지 않음 |
| [로컬 kernel 19개](ntfs-recovery.md#nr-03-로컬-winfsp-callbackmmap새-provider-연결) | 238 / 208 / 26 / 4 | 저널·실제 WinFsp filesystem/client/server/mount 구현 일치. 바뀐 coordinator/authority를 포함한 제품 전체 native 중단 보장은 아님 |
| [제품 client 13개](ntfs-recovery.md#nr-03-제품-vsscli의-클라이언트-경계) | 244 / 214 / 26 / 4 | native 읽기/저널·mount 구현 일치. discard·authority/조정/schema 변경과 미확정 게시 실패 때문에 현재 end-to-end 13개 통과로 승계하지 않음 |
| [상태 진단](state-write-diagnostics.md) | 247 / 218 / 25 / 4 | atomic 게시·진단 시험 일치. `WorkspaceStore` 변경은 discard intent 읽기 검사, `9ec88e6..HEAD` 대조에서 기존 게시/진단 정책 불변. 현재 전체 회귀로 실제 파일/CLI 조건 확인 |
| [수정 뒤 native 폐기](workspace-discard.md), `source-after-observation-fix.json`의 scoped 24개 | 24 / 24 / 0 / 0 | 기록한 폐기 코드/계약 그대로. 같은 intent 재개·중복과 정상 회수의 근거로 유지하며 실제 discard 도중 강제 종료 자격으로 확대하지 않음 |

현재 변경이 문서뿐이고, 변경된 discard 경계는 기존 실제 서비스 적용/재개와 대표 Unity 실패 작업 폐기 및 현재 자동 회귀로 확인했으므로 전체 native 행렬을 반복하지 않았다. 소스가 같다는 이유만으로 provider/볼륨/부팅/환경 자격을 현재 전체 통과로 승계하지 않는다.

## 시작 안내와 납품의 실제 경계

기존 등록 `T:/ua-source`에서 `capabilities`·`schema doctor`·서비스 status·doctor를 읽기 전용으로 확인했다. 서비스는 running, caller는 비승격이다. doctor는 Git/Unity `6000.3.22f1` 메타데이터를 읽지만 `readyForExecution=false`·실행 `checksNotRun`을 유지한다. 이를 초기 준비/Editor/grant 인증으로 안내하지 않는다.

첫 doctor는 installer 소유 fixture의 process-scoped Git trust가 새 evidence 환경에 없어 `GitInspectionFailed`였다. 원래 실패를 보존하고 이 자식 환경의 `XDG_CONFIG_HOME`에 정확한 `T:/ua-source` 하나만 설정해 재조회했다. 원본/전역 Git 설정·소유권·service grant를 변경하지 않았다. CLI 관측 5회는 Windows 비관리자이며 추가 consent 프로세스 표본은 0이다. 등록 fixture의 제품 NTFS workspace/Unity Editor를 새로 만들거나 원본 MCP·VIVOBOOK을 사용하지 않았다. 위 자동 회귀의 소유 임시 Git/fake 작업은 이 관측과 구분한다.

납품물은 기존 개발 checkout의 빌드/CLI와 [소스 스킬](../../../skills/splinter/SKILL.md), [시작 안내](../agent-start.md)다. 일반 배포 installer·전역/프로젝트 스킬 설치·자동 대화 binding은 아직 제공하지 않는다. 기계의 service/driver 등록을 새로 수행하거나 외부 공개/업로드·tag/release를 만들지 않았다.

## 유지할 제한과 후속 판단

첫 결과 수집의 지연 Library 쓰기·Editor 포커스 재시도, bridge/fixture 관측 파일 게시 오류의 미확정 원인, 분 단위 준비/소유 메모리 비용은 [대표 사용](representative-agent-workflow.md)이 소유한다. 공유 VSS 귀속·전체 물리 peak는 unknown이다. 일반 파일/에셋 조합·대형 용량·ARM A/B·재부팅/전원/물리 공간 장애는 NR/SF의 미완료 지원 확대다.

한정 버전은 M0를 마감하며 남은 NR/SF/SW 전체를 다시 개발 진척 분모에 넣지 않는다. 후속 작업은 실제 사용할 프로젝트/입력 또는 설치 필요가 정해졌을 때 그 변경 경계만 고른다. 새 입력에 대한 지원 확인·일반 설치/자동 binding·기본 Editor 모드·대형 최적화를 이번 판정에 자동 포함하지 않는다.
