# 대표 AI A/B 사용과 비용 판단

Windows x64의 등록된 작은 Unity fixture에서 독립 Editor A/B의 반복 편집·오류 격리·같은 작업 재개·보존/반영/회수와 실패 작업 폐기를 한 묶음으로 실행했다. 이 단위는 **scoped-complete-with-limitations**로 마감한다. 제품 코드·계약·설치·grant는 변경하지 않았다. 첫 checkpoint와 refresh의 실패 및 같은 작업의 재시도 비용을 포함하며, 일반 프로젝트·대형 입력·OS 장애 지원으로 확대하지 않는다.

## 입력과 증거

- 제품 후보: `development`의 `4c7bd6c47f1001827a16f4884b9901108ce71c72`. 이전 `9e10b4a`와 실행 코드가 같고 최소 AI 스킬/안내 문서만 추가된 후보다.
- 원본: `T:/ua-source`, clean `codex/library-20260929`, HEAD `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`, 추적 파일 94개. 기존 warm Library의 관측 파일 할당은 1,871,929,368 bytes다.
- 기존 등록 home: `.local/wl29`. 기존 제한 VSS 서비스와 Unity `6000.3.22f1`, Coplay `10.2.0`을 사용했다. 새 통합용 fixture branch는 `codex/m0-20261001-195330-423`이며 기존 branch/ref를 대체하지 않았다.
- A: key `m0-20261001-195330-423-a`, ID `1f96f4d47b8cb65bfe9854d5`, snapshot `{16D3B086-973A-4FD5-AD79-576752633DA2}`. B: 같은 접두사의 `-b`, ID `79ad32c9178fca5938210399`, snapshot `{CCE9951F-5B2C-4DDF-902B-60A8D3A8EAB3}`.
- 원시 evidence: `artifacts/m0-workflow/m0-20261001-195330-423/`. `plan.json`, `before/after.json`, 반환 경로/identity를 저장한 A/B binding, CLI별 JSON·stderr·monitor, 실제 loaded code, 저널 overlap frame, 단계별 저장 계측, `result.json`, `build.log`·`automated.trx`를 보존한다. binding/HTTP 세션 파일은 로컬 시험 자료이며 새 제품 계약이 아니다.

## 실제 사용 흐름

| 단계 | 확인한 동작 |
| --- | --- |
| A/B 생성·Editor 사용 | 반환 절대 경로에서 작업. 원본 NTFS 위치를 유지하고 두 ensure의 `initialPayloadCopiedBytes=0`. 각 Git 포인터 payload는 61 bytes이며 읽기 cache는 0. 별도 Codex worktree/Coordination을 사용하지 않음 |
| 오류 격리 | A를 `M0MissingA`로 수정해 현재 소스의 `CS0103` 검증 실패를 확인. B는 별도 Editor에서 `Calculate(3)=213`을 현재 검증과 실제 loaded assembly로 확인 |
| A 수정·동일 작업 재개 | A를 수정해 113으로 검증. 새 ensure의 key/id/home·경로·digest·기준·snapshot·provider PID/시작 시각과 Editor가 같음. B는 이어서 214로 검증 |
| 독립 결과 보존 | A Editor 정상 stop 후 첫 checkpoint가 저널 변경을 거부. 같은 A 재시도로 `stable-journal-v1` 독립 manifest/ref를 수집. 결과 `5d54cbeededd50a29e547ee710041cd3963c4826` |
| 원본 통합 guard | 소유 marker 하나로 dirty 원본에 integrate를 요청해 `GitDirtyWorktree`. 원본 HEAD/index·marker hash·독립 result ref 불변 확인 후 그 marker만 제거. 깨끗한 새 시험 branch에 통합, revision `119d69ede8199cc23d07d001b82c1109da33ec7a` |
| A 종료 중 B 진행 | A의 보존형 release와 겹쳐 B를 215로 수정. 동일 ID·provider·Editor PID 30936에서 새 MVID `5c6c7338-5935-4dcd-976c-a0d21e39edfa`·실제 값 215와 현재 성공 검증 확인. A release는 Succeeded |
| 실패 작업 폐기 | B를 `M0MissingB`로 수정해 현재 `CS0103` 검증 실패·result revision 없음 확인. 정확한 intent/digest/id/home의 discard가 정상 Editor stop을 포함해 Succeeded·`discarded`/`complete` |
| 회수·원본 보존 | 두 소유 Editor/provider 종료, 실제 mount·Git 등록·delta 부재, 정확한 두 snapshot은 서비스 관측 `missing`/`released`, 소유 프로세스·메모리·핸들 0. 원본 branch 복귀 뒤 94개 hash·HEAD/clean·기존 68개 record hash·153개 refs 불변. A의 결과 branch/ref와 새 통합 branch만 추가; B의 변경은 원본에 없음 |

검증은 `sourceVerification=verified`·`validationCurrent=true`·validation 성공/실패와 실제 프로젝트/PID의 loaded assembly를 함께 읽었다. 오래 남은 최초 `snapshot-probe.json`이나 오류 이전 assembly 값으로 성공을 판단하지 않았다. 통합된 trial branch의 소스/ref 포함을 확인했으며 원본 Editor의 별도 통합 후 게임 동작 검증은 실행하지 않았다.

일반 CLI 38회 모두 Windows 관리자 권한이 아니고, 50 ms consent 프로세스 관측은 0이다. 사용자가 시작한 HTTP MCP는 다른 원본 `C:/Users/olive/Desktop/Development/splinter-unity`의 PID 76076·instance `splinter-unity@983ac2308e6c044d`였다. A/B 종료 후 새 읽기 전용 HTTP 세션에서 같은 프로젝트/instance·ready 상태를 확인했다. 이전 HTTP 세션은 `Session not found`여서 새 세션으로 조회했으며 연속 세션 수명을 보장하는 증거로 읽지 않는다. 원본 MCP에 편집·refresh·종료를 요청하거나 Unity CLI를 추가 설치하지 않았다.

## 실패와 재시도의 경계

첫 A checkpoint는 소유 Editor 종료와 자원 0 뒤에도 첫 seal 189,968,241 bytes에서 두 번째 seal 189,977,313 bytes로 바뀌어 `DeltaChangedAfterCollection`을 반환했다. 추가 9,072 bytes의 frame checksum을 확인했으며 sequence 41188/41189는 `Library/ArtifactDB-lock`·`Library/SourceAssetDB-lock`의 각 4,096-byte write였다. Unity 종료 뒤 지연된 Library lock 쓰기를 관측한 것이며 OS 내부 원인을 더 넓게 확정하지 않는다.

실패 때 독립 Git revision·delta·snapshot을 보존하고 manifest 게시/회수를 거부했다. Editor 부재·open handle 0·동일 저널의 안정된 sequence/길이를 확인한 뒤 **같은 ID/home**에서 checkpoint를 명시적으로 재시도해 성공했다. release는 관리 파일 제거 뒤 다시 수집하고 unmount 후 seal도 검증했다. 정상 종료 직후 첫 결과 수집이 항상 한 번에 성공한다는 자격은 없으며 seal/flush 보호를 약화하거나 자동 재시도를 추가하지 않았다.

B의 마지막 오류 refresh는 `EditorFocusRequired`로 한 번 거부됐고 같은 명령 재시도 뒤 현재 실패 응답을 얻었다. Editor 로그에는 bridge와 fixture 관측 파일의 `File.Replace` 공유 오류도 남았다. 이후 현재 검증·종료·회수는 성공했지만 이전 파일 게시 오류의 원인/완전한 제거를 주장하지 않는다. 강제 프로세스 종료·새 key 우회·전역 설정/서비스 권한 확대 없이 종료했다.

## 같은 실행의 비용

시간은 monitor의 CLI 시작/끝과 검증 `completedAt`에서 계산했다. Editor 준비는 첫 ready 조회까지의 **관측 상한**으로, polling·에이전트 대기/분석을 포함한다. 전체 실행 시간 36분 57초는 이러한 대기·계측·실패 조사와 재시도를 포함하며 제품 처리시간 합이나 SLA가 아니다.

| 항목 | 관측 |
| --- | --- |
| 새 workspace ensure | A 42.41초, B 40.94초 |
| Editor start 응답 / 첫 ready 관측 상한 | A 7.90초 / 약 223초, B 10.86초 / 약 176초 |
| 동일 A ensure 재개 | 11.19초, 기존 Editor/provider/snapshot 유지 |
| 성공 refresh 요청 시작 → 검증 완료 | A 수정 32.56초, B 213/214/215 각각 38.80/39.02/54.64초 |
| A checkpoint | 최초 거부 85.51초, 같은 작업 재시도 41.77초. 조사/안정 관측 대기는 별도 |
| A integrate / release | dirty 거부 13.01초, clean 통합 20.10초, release 95.84초 |
| B 오류 refresh / discard | focus 거부 8.86초, 재시도 시작 → 실패 검증 완료 14.25초, discard 37.80초 |

`measure-workspace-storage.ps1`의 완전한 파일 할당 관측에서 시작 baseline을 뺐다. control/delta·runner·Git common·기존 validation roots를 한 번씩 합하고 원본 Library와 시험 evidence는 제외했다. Library의 private 쓰기는 이미 delta 안에 있어 중복 합산하지 않았다.

| 단계 | 관측된 제품 roots의 추가 파일 할당 |
| --- | --- |
| 두 Editor와 첫 수정 | 336,118,296 bytes (약 320.55 MiB) |
| A/B 현재 검증 및 A 종료 시작 부근 | 351,355,544 bytes (약 335.08 MiB), 이 단계 표본 중 최대 |
| A release 뒤 B만 사용 | 173,370,184 bytes (약 165.34 MiB) |
| A release·B discard·원본 복귀 뒤 | 1,850,672 bytes (약 1.77 MiB), 보존 로그/기록·runner cache·Git 결과 객체 포함 |

A/B의 종료 직전 논리 payload는 각각 171,028,633/159,550,568 bytes, raw journal은 189,980,670/176,382,435 bytes다. 이는 실제 delta 논리량이며 위 파일 할당이나 VSS 물리 공간과 같은 값이 아니다. 첫 ready의 소유 프로세스 그룹 working set은 A 약 3.03 GiB/B 약 4.27 GiB, private bytes는 약 3.61/5.39 GiB다. 합을 시스템 전체 물리 메모리/peak로 해석하지 않는다.

원본 Library 할당은 시작/끝 동일했다. VSS 전역 CIM 계측은 초기화 실패로 **unknown**이며 공유 VSS의 workspace별 귀속·전체 물리 peak·총 절감률은 보고하지 않는다. 파일 할당 표본에는 MFT/디렉터리·OS cache가 없고 두 종료 사이 peak도 놓칠 수 있다. 로그·시험 evidence는 별도 비용이며 `storage-ended` 시점 evidence 파일 할당은 758,776 bytes였다. 이후 문서/자동 시험 증거 복사를 포함한 최종 evidence 크기와 구분한다.

현재 선택한 warm 작은 입력에서는 최초 전체 Library 복제 없이 두 독립 Editor와 명시적 종료가 동작한다. 다만 준비에 분 단위 시간, 두 Editor에 상당한 메모리, 결과 수집 재시도가 들어간다. **독립 실행이 필요한 지속 작업에 한정해 채택**하고 짧은 수정에는 direct 경로를 유지한다. 기본 Editor 모드 자동 선택·대형 공간 목표 달성·범용 성능 개선은 선언하지 않는다. 이 비용 판단을 위해 새 비교군/반복 벤치마크를 추가하지 않는다.

## 자동 회귀와 다음 경계

필수 `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, **총 745 / 성공 744 / 실패 0 / 선택적 ReFS 건너뜀 1**이다. 현재 코드 변경이 없어 지연 쓰기를 잡는 기존 seal/재수집 회귀를 유지했으며 새로운 harness·계약·모사 시험을 만들지 않았다.

M0의 대표 사용/비용 단위는 마감한다. 이 정상 사용 흐름을 native 강제 중단 통과로 승계하지 않는다. 현재 복구 회귀·기존 소스 대조/근거 재사용 경계와 한정 버전 마감은 별도 [납품 판정](m0-delivery.md), 진입법·지원 입력/서비스 전제와 다음 행동은 [시작 안내](../agent-start.md)를 따른다. 새 장애 행렬·일반 설치·자동 binding·VIVOBOOK·대형 PV를 자동 개시하지 않는다.
