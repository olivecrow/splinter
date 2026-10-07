# Plastic 호환 검증

[English](../../verification/plastic-compatibility.md)

아래 과거 C: pending은 당시 증거다. 현재 동일 A/B의 승인된 released/discarded 종결·독립 결과와 최신 DirectX 보존은 [VR-01](vss-and-mcp-remediation.md)이 소유한다.

이번 후보는 [현재 계약](../plastic-compatibility.md)과 [확정 범위](../plans/plastic-compatibility.md)를 따른다. 2026-10-02 작은 Windows x64/NTFS Plastic/Unity A/B에서 생성·관리 영역 격리·원본 지속 편집·같은 작업 재개·소유 Editor·독립 결과와 peer 지속 작업을 확인했다. **C: 입력의 물리 snapshot 회수는 Partial**이며 기존 비소유 snapshot을 보호해 A `reclaim-pending`·B `discard-reclaim-pending`으로 남겼다. 아래 별도 T: 입력에서는 A release와 B discard의 마지막 snapshot 회수까지 **scoped-complete-with-limitations**로 마감했다. T: 통과를 C: 보류 해소·기존 Git/none/AP/M0 자격·완전한 외부 도구 인계로 승계하지 않는다.

## 고정 입력

- 사용자 준비 프로젝트: `C:\Users\olive\Desktop\Development\splinter-unity-plastic`. 원본 Git fixture `splinter-unity`는 보존한다.
- Unity `6000.3.22f1`, `com.unity.collab-proxy 2.12.4`, Pipeline `0.8.0-exp.1`; Coplay/MCP 설치는 필요하지 않아 추가하지 않았다.
- Plastic CLI `11.0.16.9915`, workspace GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`, `regular/static`. 다른 기존 등록 3개는 검증 입력이 아니다.
- 원본 Editor는 사용자 소유이며 stop하지 않는다. source capture 직전 저장/import 상태를 확인하고 원본/peer를 보존한다.

## 자동 적합성

`PlasticWorkspaceTests`는 실제 파일 결과 engine과 synthetic snapshot/backend·읽기 전용 client fake로 A/B·같은 요청·원본 이후 수정·관리 영역 제외·독립 payload·release/discard·응답 유실 재개·손상 거부를 확인한다. synthetic backend의 테스트용 복제는 제품의 VSS/WinFsp/무복제 증거가 아니다.

`OverlayAdministrationTests`는 불변 fake tree의 숨긴 관리 영역·대소문자·생성/rename 거부·저널 미변경·재연결 정책 고정과 Git private pointer 생성 회귀를 확인한다. 실제 WinFsp callback·Unity plugin의 행동은 별도 실제 관문이다.

필수 locked restore/Release/전체 시험의 첫 native 후보는 **843 Passed / 0 Failed / 1 Skipped**였고, `artifacts/plastic-compatibility/build.log`에 보존했다. 실제 B의 deferred discard 응답을 대조하다 response schema의 retry 분기가 release에만 적용되는 기존 불일치를 발견했다. 동일 intent의 discard 재시도 계약을 schema에 반영하고 CLI 응답/관측/잘못된 terminal·retry 거부 회귀를 추가했다. 최종 필수 빌드는 **844 Passed / 0 Failed / 1 Skipped**, Release warning/error 0이었다. 근거는 `build-final-03.log`·`response-schema-validation.json`이며 UTF-8 문서 검사도 통과했다. 앞선 sandbox restore 실패와 소유 진단 helper의 DLL 핸들로 실패한 빌드는 별도 로그로 보존하고, 해당 helper의 정확한 PID/시작 시각/스크립트 identity를 확인해 종료했다. 이 schema 수정은 설치된 Windows 회수 동작을 바꾸지 않는다.

## 실제 실행과 보호

고정 입력/쓰기/패키지는 [실행 계획](../plans/plastic-validation.md)이 소유하며 원시 증거는 `artifacts/plastic-compatibility/`에 있다. 제한 서비스 업데이트는 기존 grant 5개에 새 source/`T:\pc1002` 등록 1개를 더했다. 설치 파일 14개 hash·보호 ledger 기존 1,384개 파일 hash가 일치했고 서비스는 Running이었다. 일반 cm/CLI/Unity는 비승격으로 실행했다. 원본 Plastic 등록 1개와 다른 기존 등록 3개는 전후 동일하다.

| 관문 | 실제 관측과 근거 |
| --- | --- |
| 생성/격리 | A `c050da8fd660e350f0ef2756`, B `1ac08fe356c9603d0f13bf83`, 서로 다른 반환 경로·VSS·private delta. `01-a-ensure.json`·`02-b-ensure.json` 및 동일 요청 `09-a-same-request.json` |
| 원본 지속 편집 | 캡처 뒤 원본의 새 `SplinterPlasticOriginalAfterCapture.txt`는 A/B에 나타나지 않음. A/B의 서로 다른 `SplinterPlasticProbe.txt` 내용은 원본/peer에 반영되지 않음. `source-before.json`·`source-final.json` 비교는 원본 marker 1개 추가, 기존 source/`.plastic` 내용 변경·삭제 0 |
| 관리 영역 | A의 `.plastic`·`.git`·`.hg`·`.svn` 생성과 `.plastic` rename 5회 모두 AccessDenied. 전후 durable sequence 2·journal bytes 불변. A/B의 `cm gwp`는 등록 없음으로 실패. `denied-administration.json`·`04/05-a-status-*.json`·`plastic-lookup-a/b.json` |
| 소유 Editor | A PID 72140·B PID 47768이 반환한 정확한 A/B 프로젝트에서 ready. 원본 PID 68624는 사용자 소유로 계속 ready. `08-a-editor-observe.json`·`10-b-editor-ready.json`·`original-editor-final.json` |
| A 결과 | 정상 stop 후 안정된 checkpoint. 결과 `sha256:709298955c2c47c5016f63f8d08effbae11ae7f45e5fa0bcbb83e41c9cc17ffb`, `files:c050da8fd660e350f0ef2756:result`. unmount/delta 회수 뒤 독립 객체의 실제 bytes/hash·크기와 A probe 내용을 검증. 기준 61개/245,487 bytes, 결과 62개/245,515 bytes. `12-a-checkpoint.json`·`independent-result-verification.json` |
| peer 지속/폐기 | A release 중 B의 같은 PID/Editor 유지, bounded 파일 쓰기 43회 성공. 이후 intent `plastic-b-discard-20261002`로 dirty B 명시적 discard; 결과 보존으로 위장하지 않음. `b-writer-outcome.json`·`15-b-after-a-release.json`·`16-b-discard.json` |
| 마지막 소유 자원 | `17-a-final.json`·`18-b-final.json`: 양쪽 mount/provider false, handles·provider working set/private bytes·소유 application process/working set/private bytes 0, deltaPresent false. snapshotAvailable true는 보류된 회수를 뜻함 |

### 보류된 snapshot 회수

A 소유 `{15F429DA-9D49-4D2E-A7A7-29759C68E8DE}`, B 소유 `{1C744E83-7E77-46E8-B51F-B9AC2A7B2589}`는 보존 중이다. C:에 먼저 존재한 비소유 snapshot `{78AD7813-DF73-4845-8513-F14CC6E87331}`·`{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`·`{A16A6070-1AAE-445F-8561-08C86B00A115}`·`{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}`가 A 회수의 isolation guard를 막았으며, B는 이 4개와 A의 보류 snapshot도 보호한다. `14-a-release.json`·`16-b-discard.json`의 durable reclamation에 정확한 blocker와 공유 비용이 있다.

두 명령의 응답은 Accepted/nonterminal이다. A release를 즉시 Succeeded/released로 기대한 실행 측 assertion은 실패했으며, 상태를 다시 읽어 정상적인 보류를 확인하고 B의 보호/폐기를 계속 관측했다. 실패를 완전 회수의 통과로 바꾸지 않는다. blocker가 같은 동안 자동 반복·다른 작업 release·비소유 snapshot 삭제는 하지 않는다. 후속 회수는 같은 id/home와 A release 또는 B의 같은 discard intent만 사용한다. 별도 볼륨 fixture의 추가 A/B는 질문·입력·종료 기준을 따로 고정해야 하며 이 기록의 통과가 아니다.

## 비용과 외부 도구 제한

| 관측 | 값과 해석 |
| --- | --- |
| ensure | A 57,908 ms·B 57,334 ms. 양쪽 생성 초기 payloadCopied/journal/payload/readCache 0, Plastic checkout/wktree/index 복제 없음 |
| 초기 파일 할당 | 두 control/delta 합계 118,368 bytes·40 files, 공유 runner 1,139,032 bytes·7 files를 한 번 계상. `storage-created-editorless.json` |
| Editor 이후 delta | 마지막 누적 저널 A 164,730,160 bytes·payload 150,376,988; B 412,004,227·payload 386,105,920. Library 등의 실제 쓰기 비용이며 최초 source 복제와 구분. 최종 delta 파일은 양쪽 회수됨 |
| 독립 결과/최종 파일 할당 | A 독립 payload logical 245,515/allocated 310,624 bytes. 마지막 control/result/evidence 1,066,288 bytes·138 files, 공유 runner 별도 1,139,032 bytes. `storage-final-pending.json` |
| 소유 메모리 표본 | A Editor 트리 working set 3,442,298,880/private 3,996,483,584 bytes·11 processes; B 표본 6,594,654,208/7,422,308,352·24 processes. provider 표본 A 124,489,728/81,526,784·B 132,812,800/89,894,912 bytes. 같은 시각의 peak나 메모리 절감 비교가 아님 |
| 공유 VSS | 마지막 C: source-volume used 26,004,471,808/allocated 27,461,320,704/limit 39,985,348,608 bytes. 기존 비소유 snapshot과 함께 공유하며 workspaceAttribution unknown. A/B에 귀속하거나 중복 합산하지 않음 |
| 비교 불확실성 | 실행 중 원본 Library의 locked 파일로 physical baseline complete=false/allocatedBytes=null. 전체 물리 peak·VSS 작업별 귀속·전체 RAM/공간 절감은 unknown |

기존 Pipeline으로 Splinter의 정확한 A/B Editor launch/readiness/normal stop은 확인했다. 설치된 Unity CLI `1.0.0-beta.5`의 read-only `status --project-path <A/B>`는 **STATUS_NO_INSTANCES**였고 원본만 발견했다. `unity-cli-a-status.json`·`unity-cli-b-status.json`에 실패를 보존했다. Coplay/MCP 추가·Pipeline 교체·광범위한 연결 수정을 자동으로 포함하지 않는다. 외부 도구의 A/B 전체 인계·refresh/컴파일/Play/Test/빌드·원본 반영·check-in·대형/ARM/OS 장애는 미자격이다.

## 별도 T: 입력의 회수 자격

이 PC 단위 뒤 [Unity CLI A/B 인계](unity-cli-handoff.md)를 별도 새 T: A/B 한 묶음에서 한정 확인했다. 목록의 STATUS_NO_INSTANCES는 유지되지만 정확한 반환 경로의 command가 실제 작업 Editor에 연결됨을 확인했다. 아래 PC의 과거 실패를 첫 시도 통과로 바꾸거나 C: 회수 보류를 해결했다고 읽지 않는다.

사용자의 다음 작업 실행 요청에 따라 [고정한 추가 계획](../plans/plastic-reclamation-validation.md)의 한 A/B만 실행했다. 제품 후보는 `4e262b7`이며 이후 프로그램 변경은 없었다. 위 C: 실행과 구분해 원시 증거를 `artifacts/plastic-reclamation/`에 보존한다.

입력은 새 `T:\plastic-fixture-20261002`, home `T:\pt1002`, Plastic regular/static GUID `fc4fce46-cb20-4ad8-b474-cdb0000f9866`다. 사용자 원본의 작은 소스 61개/245,487 bytes만 hash를 확인해 fixture로 준비하고 `.plastic`·Library는 복사하지 않았다. 명시한 기존 repository에 source 등록 1개를 추가했고 checkout/update/check-in은 하지 않았다. 소유 batch Unity `6000.3.22f1`은 새 Library를 준비하고 정상 quit했다. 준비 helper가 `T:`/`t:`를 대소문자 구분으로 비교해 등록 뒤 assertion을 실패했지만, 같은 GUID와 61개 실제 hash를 읽기 전용으로 확인해 이어갔다. 새 중복 등록이나 제품 fallback은 만들지 않았다.

서비스 package 14개·grant/setup hash와 설치 쓰기는 추가 계획에 고정했다. Update 뒤 기존 grant 6개 + 새 등록 1개, 보호 ledger 기존 1,418개 파일 hash·설치 14개 hash·기존 C: snapshot 6개를 확인했다. 일반 workspace/cm/Unity 명령은 비승격이다.

| 관문 | 실제 T: 판정과 근거 |
| --- | --- |
| 생성/재개/관리 보호 | A `f3282c3a99d1131747f82ae9`, B `7cdc0fba90c3029daa166356`, 별도 기준 snapshot/private delta. 초기 payload/journal/readCache 0, 동일 A 요청의 id/basis/snapshot 유지, 관리 root 생성/rename 5회 거부·sequence 2→2와 journal 불변. `01/02-*-ensure.json`·`09-a-same-request.json`·`denied-administration.json` |
| 원본/Editor/peer | 캡처 뒤 source marker는 A/B에 들어오지 않음. 정확한 A PID 34716·B PID 77552 ready, A 정상 stop/checkpoint/release 중 B 같은 Editor ready와 bounded 쓰기 41회 성공. `10-*-editor-ready.json`·`11/12-*.json`·`b-writer-outcome.json`·`15-b-after-a-release.json` |
| A 독립 결과와 회수 | 결과 `sha256:d3504d1ed423f0dea3181423263fd448dbade1a486664a8c495e10f6ac77fc40`. release Succeeded/terminal, phase released. A snapshot `{08E9AA89-C328-4342-BD38-3B822ABC49E4}` 해제 뒤 기준 61개/245,487 bytes·결과 62개/245,515 bytes·62개 독립 객체 hash/크기와 A probe 실제 내용 재검증. `14-a-release.json`·`independent-result-verification.json` |
| B 명시적 폐기와 회수 | intent `plastic-t-b-discard-20261002`·반환 digest로 dirty B를 discard. Succeeded/terminal, phase discarded, B snapshot `{F66DC85E-B51A-43A6-B6E1-B7DFF2440096}` 해제. B result manifest/result revision 없음. `16-b-discard.json` |
| 마지막 소유 자원 | `17-a-final.json`·`18-b-final.json`: snapshotAvailable/mounted/providerAlive/deltaPresent false, handles/provider working set/private bytes 0, releasedResources ownershipVerified true·rootRunning false·activeProcesses/working set/private bytes 0 |
| 기존 자원 보호 | 관리자 read-only 최종 대조에서 T: snapshot 0·기존 C: 6개 보존·기존 ledger 1,418개 hash 보존·grant 7개 exact 일치. C: source/`.plastic` 파일 변경/삭제/추가 0, 기존 원본 Editor PID 68624와 시작 시각 유지. Plastic 등록 5개는 전후 byte 일치. C: 두 작업은 여전히 reclaim-pending/discard-reclaim-pending. `protection-outcome.json`·`snapshots-final.json`·`C-original-source-comparison.json`·`19/20-c-*-observe.json` |

| T: 비용 표본 | 값과 한계 |
| --- | --- |
| 단계 시간 | ensure A 30,482 ms/B 30,357 ms, A 정상 stop 28,359 ms·checkpoint 8,388 ms·release 13,613 ms, B discard 37,285 ms. 다른 C: 입력과 전체 성능 비교로 사용하지 않음 |
| 준비된 원본 Library | logical 1,819,103,541/allocated 1,852,496,112 bytes·30,748 files, complete=true. 새 fixture의 원본 준비 비용이며 작업별 복제 비용이 아님 |
| 초기/최종 파일 할당 | 초기 control/delta 111,232 bytes·40 files·payload 0. 최종 control/result/evidence 699,704 bytes·135 files, 독립 payload logical 245,515/allocated 310,624 bytes. 공유 runner 1,139,032 bytes·7 files 별도 1회 계상 |
| Editor 쓰기 delta | 마지막 누적 journal A 129,774,265/payload 117,550,197 bytes, B 129,256,319/117,040,994 bytes. 최종 delta 파일은 둘 다 없음 |
| 소유 메모리 | ready 표본 A working set 3,005,116,416/private 3,449,417,728 bytes·11 processes; B 3,167,453,184/3,731,984,384·13 processes. 최종 소유 자원 0, 전체 peak/메모리 절감 비교 미자격 |
| VSS | 마지막 T: source-volume used/allocated 0·limit 10,737,418,240 bytes. 공유 비용의 workspace 귀속은 unknown을 유지하며 원본 Library/저널/독립 결과와 중복 합산하지 않음. C: shared used 27,067,400,192 bytes는 기존 6개 snapshot의 다른 볼륨 비용 |

현재 프로그램 후보의 필수 자동 회귀는 위 844 Passed / 0 Failed / 1 Skipped를 유지한다. 추가 T: 응답 22개는 현재 response schema를 통과했고 문서 UTF-8도 확인했다. 지원 판정은 위 고정 입력의 정상 수명주기·회수·원본/peer 보호에 한정한다. C: 공유 snapshot의 보류·외부 CLI worker 발견 실패·MCP 전체 인계·원본 반영·Plastic 서버 기능·대형/ARM/OS 장애의 미자격은 유지한다.

## 과거 C: 작업의 승인된 동일 요청 회수 재시도 (2026-10-04)

사용자가 과거 C: Plastic 정리를 명시해 기존 A `c050da8fd660e350f0ef2756`/B `1ac08fe356c9603d0f13bf83`·home `T:\pc1002`·원본 `C:\Users\olive\Desktop\Development\splinter-unity-plastic`만 재시도했다. A는 같은 release, B는 기존 intent `plastic-b-discard-20261002`·digest `51a72495ab9ea5bde42a2ab2`를 유지했다. 새 workspace/설치/grant/서비스 교체·다른 snapshot 삭제는 수행하지 않았다.

현재 CLI의 [A](../../../artifacts/unity-followup-20261003/plastic-a-release-retry.json)·[B](../../../artifacts/unity-followup-20261003/plastic-b-discard-retry.json)는 `OverlayIdentityMismatch`로 거부했다. 저장된 overlay identity v1과 현재 backend의 v2 계약 차이를 확인했으며 자동 이관/호환 fallback을 추가하지 않았다. 원래 실행 계약 `4e262b7`의 소스를 별도 evidence 폴더로 archive하고 기존 패키지 cache의 locked restore/Release(경고/오류 0)로 역사적 CLI를 재구성했다. 이 별도 도구로 정확한 기존 요청만 실행해 실제 회수 blocker를 확인했다. 제품의 구버전 지원이나 설치된 서비스 rollback으로 해석하지 않는다.

| 실제 같은 요청 재시도 | 판정 |
| --- | --- |
| [원래 계약 A release](../../../artifacts/unity-followup-20261003/plastic-original-a-release-retry-corrected.json) | Accepted/nonterminal, reclaim-pending, deferred-reclamation / `SnapshotReleaseIsolationRequired` |
| [원래 계약 B discard](../../../artifacts/unity-followup-20261003/plastic-original-b-discard-retry-corrected.json) | Accepted/nonterminal, discard-reclaim-pending, deferred-discard-reclamation / 같은 격리 guard. 같은 intent/digest 보존 |

A snapshot `{15F429DA-9D49-4D2E-A7A7-29759C68E8DE}`를 막는 비소유 snapshot은 `{78AD7813-DF73-4845-8513-F14CC6E87331}`, `{15182498-4A5B-4E95-B85F-FBE2EC57AE15}`, `{A16A6070-1AAE-445F-8561-08C86B00A115}`, `{0A89C286-0E9C-43EB-87AA-4CD068E98E7E}` 네 개다. B snapshot `{1C744E83-7E77-46E8-B51F-B9AC2A7B2589}`에는 같은 네 개와 A의 pending snapshot이 blocker로 남는다. 소유 Editor/provider/mount/delta/handles는 이미 0이고 독립 A 결과와 B discard intent는 보존됐다.

[최종 관리자 read-only snapshot 관측](../../../artifacts/unity-followup-20261003/final-native-protection.json)은 C:의 원래 6개가 모두 유지됨을 확인한다. 이번 별도 Git S A/B의 마지막 T: snapshot 회수로 이 C: 보류가 해소됐다고 읽지 않는다. 사용자 정리 승인은 유효하며, 보류 사유는 비소유 snapshot에 영향을 줄 수 있는 실제 VSS 격리 제약이다. 같은 blocker를 자동 반복하거나 다른 작업을 지워 우회하지 않는다.
