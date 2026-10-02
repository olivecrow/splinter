# 일반 폴더 원본 반영의 한정 검증

범위는 [확정 계획](../plans/plain-folder-original-apply.md), 현재 명령·게시·메타데이터 제한은 [기능 문서](../plain-folder-original-apply.md)가 소유한다. 이 후보는 `development @ 9e09258` 뒤 원본 반영 변경을 포함한다. 일반 권한의 Core/fake·파일/Windows 시험과 실제 제품 VSS·WinFsp 실행을 구분한다. 기존 VCS-01~06·M0·Unity 자격을 이 기능으로 승계하지 않는다.

## 현재 판정

프로그램 구현과 일반 권한 파일 시험은 진행했고 **AP-05 실제 제품 A/B는 NotRun**이다. 사용자가 부재 동안 UAC/관리자 작업을 미루도록 요청해 서비스 업데이트·등록·외부 source 준비를 적용하지 않았다. 그 작업과 한 A/B 묶음이 끝나기 전에는 전체 단위를 `scoped-complete-with-limitations`로 마감하지 않는다. 승인된 이전 `vcs-none-20261002` Apply를 새 입력/패키지의 승인으로 확대하지 않는다.

| 행 | 현재 근거와 한도 |
| --- | --- |
| AP-01 | portable publisher fake·정확한 결과/seal·읽기 전용 계획·요청/응답 schema·잘못된 CLI 조합의 home 생성 전 거부. plan 성공은 apply 성공이 아님 |
| AP-02 | 실제 일반 권한 NTFS 파일의 수정/생성/삭제·디렉터리 생성/빈 삭제, 무관한 원본 쓰기와 peer 유지. 영향을 받는 파일의 쓰기/삭제·원본 조상 이동 거부, 기존 native writable mmap의 게시 전 거부와 게시 간격에 생긴 새 파일의 비덮어쓰기 |
| AP-03 | 원본 충돌/계획 변경·checkpoint 뒤 편집·ADS·무관한 삭제 디렉터리 자식·미지원/손상/unknown 영수증의 보존·거부. 미해결 게시 파일의 새 task 캡처 거부와 기존 peer 지속. 전체 원본 rollback/재귀 삭제 없음 |
| AP-04 | stage 생성/완성·퇴역/게시 전후 영수증·회수/완료의 유한 중단 주입과 같은 intent 재개. 모르는 stage identity·altered prefix·새 intent는 보존/거부. Runtime과 권한 서비스의 pending-apply 회수 차단, 외부 reader로 삭제가 pending인 기존 파일의 실제 부재 확인 전 완료 차단 |
| AP-05 | NotRun. 실제 제품 CLI·VSS·WinFsp의 새 A/B, 일반 권한 적용·A release 중 B 지속·B 충돌/독립 결과·최종 mount/delta/snapshot 부재는 아래 계획으로 남음 |
| AP-06 | 8MiB·32MiB publisher의 실제 stage allocation과 관리 할당, 기존 회귀와 현재 안내. 실제 제품 추가 공간·VSS/전체 물리 peak·Editor/프로세스 RAM은 별도 미관측 |

시험의 workspace snapshot은 합성 backend로 준비한다. fixture setup의 파일 복사는 제품 clone/copy-up의 증거가 아니다. publisher는 실제 Windows 파일/핸들을 사용하며 sharing 경쟁 시도는 같은 시험 프로세스의 별도 파일 열기/이름 변경이다. observer가 주입한 중단은 실제 CLI/provider 강제 종료·재부팅·전원 장애의 자격이 아니다. owner/group·DACL 보호·ACE 내용의 동등성을 검사하며 감사 SACL·특수 보안 label·동시 권한 변경은 미자격이다.

## 공간과 메모리

증거는 `artifacts/plain-folder-apply/cost-*.json`이다. 변경 파일 하나의 stage가 8,388,608 / 33,554,432 bytes를 할당하고 정상 게시 뒤 임시 파일 0개를 확인했다. publisher 자체의 관리 누적 할당은 해당 선택 시험에서 524,488~525,312 bytes였다. 고정 64KiB 스트림과 metadata/hash 버퍼의 비용이며 Runtime state publication·VSS·Editor·전체 프로세스 working set 또는 물리 peak를 포함하지 않는다. 독립 결과의 기준/결과 payload와 workspace 저널을 합쳐 절감률을 계산하지 않는다.

처음 순차 EOF 확장에서는 각각 9,371,648 / 34,537,472 bytes를 할당했다. 알려진 최종 stage 길이를 한 번 예약해 두 표본의 추가 983,040 bytes를 제거했다. 최초 실패/관측은 `artifacts/plain-folder-apply-preallocation-before.trx`와 `.log`에 보존한다. 다른 크기·볼륨·대형 성능의 고정 절감률이나 SLA를 주장하지 않는다.

## 남은 제품 실행

실제 한 묶음의 고정 입력·서비스 후보·승인/중단 조건은 [준비 계획](../plans/plain-folder-original-apply-validation.md)을 따른다. 관리자 Apply 없이 package 작성·읽기 전용 상태/계획까지 준비하고, 원본·peer·기존 보호 ledger와 snapshot을 유지한다. 새 grant의 경로 identity가 준비되지 않았다면 설치 가능한 grant라고 표시하지 않는다.

최종 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드는 경고/오류 0, 자동 시험은 **826 Passed / 0 Failed / 선택적 실제 ReFS 1 Skipped (총 827)**다. `artifacts/plain-folder-apply/automated.trx`, `artifacts/plain-folder-apply-final-build.log`, `artifacts/plain-folder-apply/candidate.json`에 최종 근거를 보존한다. 새 반영 경계 32개와 기존 Git/Windows/계약 회귀를 포함하며 실제 제품 AP-05의 통과로 확대하지 않는다. 서비스 package의 명시 파일 14개 hash와 제품의 Core/Runtime/Files/Windows 바이너리 일치를 확인했다. 설치는 실행하지 않았다.
