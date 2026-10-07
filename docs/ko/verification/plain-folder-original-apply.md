# 일반 폴더 원본 반영의 한정 검증

[English](../../verification/plain-folder-original-apply.md)

범위는 [확정 계획](../plans/plain-folder-original-apply.md), 현재 명령·게시·메타데이터 제한은 [기능 문서](../plain-folder-original-apply.md)가 소유한다. 프로그램 후보는 `development @ fa1efea`다. 일반 권한의 Core/fake·파일/Windows 시험과 아래 실제 제품 VSS·WinFsp 실행을 구분한다. 기존 VCS-01~06·M0·Unity 자격을 이 기능으로 승계하지 않는다.

## 현재 판정

**AP-01~06의 한정 범위를 scoped-complete-with-limitations로 마감했다.** 자동 계약/실제 파일 시험과 Windows x64 NTFS의 작은 Editor 없는 제품 A/B 한 묶음을 확인했다. 실제 반영은 일반 파일 수정·생성·삭제, 무관한 원본 수정 유지, B 지속 편집·충돌 거부와 양쪽 독립 결과/회수에 한정한다. 디렉터리·동시 원본 writer·중단 영수증·보안 경계는 앞선 자동 파일 시험의 자격이며 이 native 묶음의 통과로 넓히지 않는다.

| 행 | 현재 근거와 한도 |
| --- | --- |
| AP-01 | portable publisher fake·정확한 결과/seal·읽기 전용 계획·요청/응답 schema·잘못된 CLI 조합의 home 생성 전 거부. plan 성공은 apply 성공이 아님 |
| AP-02 | 실제 일반 권한 NTFS 파일의 수정/생성/삭제·디렉터리 생성/빈 삭제, 무관한 원본 쓰기와 peer 유지. 영향을 받는 파일의 쓰기/삭제·원본 조상 이동 거부, 기존 native writable mmap의 게시 전 거부와 게시 간격에 생긴 새 파일의 비덮어쓰기 |
| AP-03 | 원본 충돌/계획 변경·checkpoint 뒤 편집·ADS·무관한 삭제 디렉터리 자식·미지원/손상/unknown 영수증의 보존·거부. 미해결 게시 파일의 새 task 캡처 거부와 기존 peer 지속. 전체 원본 rollback/재귀 삭제 없음 |
| AP-04 | stage 생성/완성·퇴역/게시 전후 영수증·회수/완료의 유한 중단 주입과 같은 intent 재개. 모르는 stage identity·altered prefix·새 intent는 보존/거부. Runtime과 권한 서비스의 pending-apply 회수 차단, 외부 reader로 삭제가 pending인 기존 파일의 실제 부재 확인 전 완료 차단 |
| AP-05 | Passed (작은 Editor 없는 제품 A/B 한정). 같은 A 재개·변경 3경로 반영·A release 동안 B의 91회 쓰기·B 충돌의 원본 쓰기 전 거부·독립 결과·최종 mount/delta/snapshot/소유 자원 부재 |
| AP-06 | Passed (고정 계측/회귀 한정). 8MiB·32MiB publisher stage/관리 할당과 아래 제품 결과/journal/runner·프로세스 관측·회귀/schema/안내. 전체 물리 peak·workspace별 VSS 귀속·전체 CLI/Editor RAM 절감은 unknown/미자격 |

위 자동 시험의 workspace snapshot은 합성 backend로 준비한다. fixture setup의 파일 복사는 제품 clone/copy-up의 증거가 아니다. publisher는 실제 Windows 파일/핸들을 사용하며 sharing 경쟁 시도는 같은 시험 프로세스의 별도 파일 열기/이름 변경이다. observer가 주입한 중단은 실제 CLI/provider 강제 종료·재부팅·전원 장애의 자격이 아니다. owner/group·DACL 보호·ACE 내용의 동등성을 검사하며 감사 SACL·특수 보안 label·동시 권한 변경은 미자격이다.

## 공간과 메모리

AP-06 당시 증거는 `artifacts/plain-folder-apply/cost-*.json`이다. 변경 파일 하나의 stage가 8,388,608 / 33,554,432 bytes를 할당하고 정상 게시 뒤 임시 파일 0개를 확인했다. publisher 자체의 관리 누적 할당은 해당 선택 시험에서 524,488~525,312 bytes였다. 고정 64KiB 스트림과 metadata/hash 버퍼의 비용이며 Runtime state publication·VSS·Editor·전체 프로세스 working set 또는 물리 peak를 포함하지 않는다. 독립 결과의 기준/결과 payload와 workspace 저널을 합쳐 절감률을 계산하지 않는다.

현재 `PublicationStreamsPayloadAndSamplesOnlyOneChangedFileStage` 회귀는 같은 계측값을 xUnit 시험 출력에 기록한다. 필수 빌드의 `artifacts/test-results/Splinter.Tests_net10.0_x64.trx`에서 각 8MiB·32MiB 사례의 출력으로 확인하며 실행마다 별도 `cost-<크기>-<workspace ID>.json`을 누적하지 않는다. 위 JSON은 당시의 보존 근거이며 현재 시험의 입력은 아니다.

처음 순차 EOF 확장에서는 각각 9,371,648 / 34,537,472 bytes를 할당했다. 알려진 최종 stage 길이를 한 번 예약해 두 표본의 추가 983,040 bytes를 제거했다. 최초 실패/관측은 `artifacts/plain-folder-apply-preallocation-before.trx`와 `.log`에 보존한다. 다른 크기·볼륨·대형 성능의 고정 절감률이나 SLA를 주장하지 않는다.

## 실제 제품 A/B와 보호 경계

고정 입력과 승인 범위는 [실행 계획](../plans/plain-folder-original-apply-validation.md)을 따랐다. `T:\plain-apply-20261002`의 27바이트 seed와 `.local\ap1002`에 source/application `none`, snapshot 기준, 16MiB journal 예산으로 생성했다. A `42fdead74de65116b2669858` / B `78b313a0f90b0261a72742e9`의 key·request digest·basis·snapshot·반환 경로를 보존했다. 최초 payload 복제와 read cache는 모두 0이었다.

사용자가 복귀 후 UAC 포함 실행을 승인했다. 첫 UAC는 실수로 취소돼 helper가 시작되지 않았고, 명시적 재승인 뒤 패키지 Update·새 등록을 적용했다. 기존 등록 4개와 보호 ledger 파일 1,352개 hash를 유지하고 등록 1개만 추가했다. 설치 파일 14개가 검토한 manifest와 일치하며 서비스는 Running이다. 일반 CLI의 15개 권한 관측은 모두 `callerElevated=false`다.

첫 샌드박스 ensure는 `StateWriteIncomplete`·`publish-state`·게시 unknown·Win32 5를 반환했다. 같은 ID의 status는 `requested`·snapshot 미캡처를 관측했고 반환 nextAction의 동일 ensure를 샌드박스 밖 일반 사용자 권한으로 재개했다. 새 key/capture나 상태/권한 편집 없이 성공했으며 첫 응답과 임시 상태 파일을 보존한다. 이 사실만으로 접근 거부의 내부 원인이나 native 강제 종료 복구 자격을 선언하지 않는다.

A는 `data.txt` 수정·`added.txt` 생성·`obsolete.txt` 삭제를 checkpoint했다. 읽기 전용 plan의 상태 파일 hash와 원본 내용이 유지되고 충돌 0·3경로를 확인했다. `ap-a-apply-20261002`와 정확한 plan digest로 반영했다. 원본의 `marker-after`와 B의 `marker-before`·`b-result`·`obsolete.txt`를 유지했다. A release의 약 11.1초 동안 B 자식 writer의 91회 쓰기가 성공했고 회수 뒤 추가 B 편집도 성공했다.

B checkpoint의 `data.txt` 충돌 1개를 확인했다. 명시적 실행은 `OriginalApplyConflict`로 거부됐고 전후 원본의 내용/hash/mtime/속성 manifest가 같았다. 두 작업 모두 release 뒤 독립 result/baseline object의 길이/hash를 다시 검사했다. 실제 mount/provider/delta/snapshot 부재·소유 핸들/working set/private bytes 0을 status와 회수 영수증에서 확인했다. 회수 isolation의 `missingOtherIds=[]`와 기존 snapshot ID 4개 보존도 확인했다. 남은 원본 게시 파일은 0개다.

### 제품 비용과 증거

| 구분 | 이번 관측과 한도 |
| --- | --- |
| 일반 CLI 경과 시간 | A/B 생성 약 17.4/17.3초, A 적용 약 2.46초, A/B release 약 11.1/10.9초. 프로세스 시작·검사·IPC를 포함한 한 표본이며 SLA 아님 |
| provider 메모리 | 생성 완료 working set A 50,774,016 / B 50,843,648 bytes, private A 16,220,160 / B 15,962,112 bytes. release 뒤 0. 고정 시점 관측이며 peak·전체 CLI/Editor 메모리 아님 |
| 독립 payload | A 결과 30 / 기준+결과 object 44 bytes, B 결과 55 / 기준+결과 object 60 bytes. 총 object 논리 104 / 파일 할당 144 bytes. 이 작은 resident 파일의 값은 일반 큰 파일의 할당률 아님 |
| private journal | A 2,388 / B 112,932 bytes의 최종 seal. B의 반복 쓰기/metadata 비용 포함. A release 뒤 B journal 파일 할당 131,072 bytes, 최종 두 delta 부재 |
| 보존 기록과 runner | 최종 records 파일 할당 136,400 bytes(위 object 포함), 같은 home의 runner 7파일 1,110,360 bytes. 기록·측정 overhead·payload를 별도로 읽으며 중복 합산하지 않음 |
| 원본 게시 임시 공간 | 완료 뒤 게시 파일 0. 실제 제품 실행 중 stage 순간 할당은 미관측; 앞의 별도 publisher 8/32MiB 계측과 구분 |
| 공유 VSS | 해당 T: 볼륨 캡처 후 할당 33,554,432 bytes, 마지막 release 뒤 used/allocated 0. 서비스 영수증의 볼륨 관측이며 작업별 귀속·전체 물리 peak는 unknown |

원시 응답 `01~16-*.json`, 일반 권한/시간 `*-cost.json`, `response-schema-validation.json`의 응답 24개 유효, 단계별 `storage-*.json`, `native-qualification.json`, `.local\ap1002\records`의 독립 결과·소유 회수 영수증을 보존한다. 관리자 Update는 `service-update-plan.json`, `service-apply-outcome.json`, `ledger-before.json`과 configuration 전후 기록으로 구분한다. 새 native 장애 행렬·Unity/Plastic·대형/ARM/OS 시험은 실행하지 않았다.

최종 프로그램 후보의 `pwsh ./scripts/build.ps1` locked restore·Release 빌드는 경고/오류 0, 자동 시험은 **826 Passed / 0 Failed / 당시 제외 시험 1 Skipped (총 827)**다. `artifacts/plain-folder-apply/automated.trx`, `artifacts/plain-folder-apply-final-build.log`, `artifacts/plain-folder-apply/candidate.json`에 근거를 보존한다. 새 반영 경계 32개와 기존 Git/Windows/계약 회귀를 포함한다. 서비스 package의 명시 파일 14개 hash와 제품의 Core/Runtime/Files/Windows 바이너리 일치를 확인했으며 native 실행 뒤 제품 코드는 변경하지 않았다.
