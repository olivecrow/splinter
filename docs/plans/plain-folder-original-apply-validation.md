# 일반 폴더 원본 반영의 제품 A/B 준비

[확정 범위](plain-folder-original-apply.md)의 AP-05 한 묶음이다. 현재는 **준비 계획이며 실행하지 않는다**. 사용자 요청으로 UAC/관리자 Apply와 새 등록은 보류한다. 일반 권한 구현·파일 시험은 [별도 기록](../verification/plain-folder-original-apply.md)을 따른다.

| 항목 | 고정 후보와 준비 상태 |
| --- | --- |
| 제품 후보 | 현재 development의 원본 반영 코드·입출력 계약·pending-apply 서비스 guard. `artifacts/plain-folder-apply/candidate.json`에 최종 코드/바이너리 hash와 회귀 근거 보존 |
| 원본 후보 | `T:\plain-apply-20261002`, 작은 VCS 없는 editorless 일반 폴더. 아직 생성/identity 등록하지 않음 |
| home 후보 | `C:\Users\olive\Desktop\Development\splinter\.local\ap1002`. 아직 source/home grant를 설치하지 않음 |
| seed | `artifacts/plain-folder-apply/native-seed`와 `native-seed-manifest.json`: `data.txt=basis`, `obsolete.txt=delete-me`, `marker.txt=marker-before`. VCS/Unity/Library 없음. 실제 source 전송 전 후 seed hash 대조 |
| 요청 | A `ap-editorless-a-20261002`, B `ap-editorless-b-20261002`; `--source none --application none --base snapshot --storage ntfs-snapshot`; journal budget 16MiB |
| 서비스 변경 | `artifacts/plain-folder-apply/package-20261002-01`, 명시 파일 14개, manifest SHA-256 `850e65db3dc59e16ea015cd2afa51ff65dbb25a5bc92d70891f29d92584e5877`. 기존 grant 근거 4개와 실제 보호 구성/ledger를 대조·보존한 Update에 새 source/home 1개 추가 예정 |
| 읽기 전용 상태/계획 | `plain-folder-apply-service-status.json`: running·callerElevated=false. `artifacts/plain-folder-apply/service-package-update-plan.json`: Update·Apply=false·기존 grant 근거 4개. 새 source/home이 없는 현재는 새 등록을 포함한 설치 가능한 최종 grant가 아님 |
| 현재 승인 | 이전 fixture의 Apply 승인을 사용하지 않음. 설치 가능한 최종 grant/읽기 전용 Update 계획을 갖춘 뒤 해당 변경의 명시적 관리자 Apply 승인 필요 |
| 제외 | Unity/원본 Editor·MCP·추가 전체 장애 harness·볼륨/VSS budget 변경·기존 source/workspace/snapshot 삭제·원격/ARM 시험 |

## 준비와 승인 경계

일반 권한으로 제품 빌드와 새 package/manifest·seed를 evidence 안에 작성했다. 기존 서비스 status, 네 등록의 기존 grant 근거와 보호 대상을 읽는다. 이때 `setup-storage-service.ps1 -Apply`·SCM 재시작·elevation을 실행하지 않는다.

실제 실행 준비 때 source/home이 정확한 일반 NTFS 경로인지, 비어 있는 새 대상인지 확인하고 seed를 고정한다. 원본 metadata/hash·source/home의 경로/볼륨 identity와 기존 보호 구성을 대조해 최종 grant와 기본 read-only Update 계획을 작성한다. 새 source가 없어 identity를 확인하지 못한 지금은 기존 grant를 재사용한 read-only package Update 계획과 새 등록 설계를 구분한다. 이후 검토한 변경에만 관리자 Apply를 승인받는다. 서비스·grant 거부를 elevated CLI나 자동 fallback으로 우회하지 않는다.

## 한 묶음과 종료 조건

1. 최종 후보·설치 package hash와 새 grant·일반 권한 CLI를 확인한다. 기존 소유하지 않은 process/snapshot과 여유 공간·VSS headroom을 기록한다. unknown과 다른 작업은 보존한다.
2. A/B를 만들고 새 CLI에서 같은 A 요청을 재개한다. 정확한 ID/home/request digest·snapshot·basis와 두 반환 경로를 유지한다. 원본의 `marker.txt`를 `marker-after`로 수정해 불변 기준과 무관한 원본 지속 편집을 확인한다.
3. A에서 `data.txt=a-result`, `added.txt=a-created`, `obsolete.txt` 삭제를 수행한다. B의 `data.txt=b-result`와 `b-only.txt=b-private`는 별도 경로에 계속 편집한다. A checkpoint 뒤 `--target original --plan`의 변경 집합/충돌 0/digest를 확인하고 명시적 intent `ap-a-apply-20261002`로 반영한다.
4. 원본에서 A의 정확한 내용만 확인한다. `marker-after`와 B의 기준/편집을 유지한다. A의 명시적 release 중 B를 계속 읽고 수정하며 A 독립 결과와 실제 소유 자원 부재를 확인한다. reclaim-pending은 완료가 아니며 정확한 같은 요청만 재시도한다.
5. B checkpoint 뒤 같은 원본 경로의 충돌을 읽기 전용 계획에서 확인한다. 명시적 실행이 `OriginalApplyConflict`로 원본 쓰기 전에 거부되고 A 원본과 B 독립 결과가 유지돼야 한다. 명시적 B release로 독립 결과를 보존하고 마지막 소유 snapshot·mount·delta·process/handle/메모리 회수를 확인한다.
6. raw CLI 응답/schema·파일 hash·independent result·소유 자원 관측과 journal/결과/stage 할당을 구분해 AP-05/06을 판정한다. VSS의 workspace 귀속·전체 물리 peak는 unknown을 유지한다. 이 한 묶음 뒤 새 입력/전체 행렬을 열지 않는다.

지원 경로의 원본/peer 훼손·승인 쓰기 손실은 우선 경로를 차단하고 수정한다. 그 외 sharing·권한·소유권·손상·회수 격리 실패는 원시 증거와 작업을 보존하고 같은 요청의 필요한 복구만 한다. 비소유 snapshot 삭제·fresh key recapture·자동 rollback/sweep으로 성공을 만들지 않는다.
