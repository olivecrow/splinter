# 일반 폴더 원본 반영의 제품 A/B 준비

[확정 범위](plain-folder-original-apply.md)의 AP-05 한 묶음에 사용한 고정 입력·승인·중단 조건이다. 사용자 복귀 후 UAC 포함 실행을 승인받아 서비스 Update와 새 등록·실제 제품 A/B를 수행했다. **한정 단위를 마감했으며 자동으로 재실행하지 않는다.** 후보별 실행/비용·첫 UAC 취소와 상태 게시 실패/동일 요청 재개는 [검증 기록](../verification/plain-folder-original-apply.md)이 소유한다.

| 항목 | 고정 후보와 준비 상태 |
| --- | --- |
| 제품 후보 | `development @ fa1efea`의 원본 반영 코드·입출력 계약·pending-apply 서비스 guard. `artifacts/plain-folder-apply/candidate.json`에 최종 코드/바이너리 hash와 회귀 근거 보존 |
| 원본 | `T:\plain-apply-20261002`, 작은 VCS 없는 editorless 일반 폴더. 등록 identity `22B349E4:000E000000008A2D`. 검증 후 원본을 보존 |
| home | `C:\Users\olive\Desktop\Development\splinter\.local\ap1002`, 등록 identity `B0C924D3:00BD00000008911E`. 양쪽 독립 결과·소유 영수증 보존 |
| seed | `artifacts/plain-folder-apply/native-seed`와 `native-seed-manifest.json`: `data.txt=basis`, `obsolete.txt=delete-me`, `marker.txt=marker-before`. VCS/Unity/Library 없음. 실제 source 전송 전 후 seed hash 대조 |
| 요청 | A `ap-editorless-a-20261002`, B `ap-editorless-b-20261002`; `--source none --application none --base snapshot --storage ntfs-snapshot`; journal budget 16MiB |
| 서비스 변경 | `artifacts/plain-folder-apply/package-20261002-01`, 명시 파일 14개, manifest SHA-256 `850e65db3dc59e16ea015cd2afa51ff65dbb25a5bc92d70891f29d92584e5877`. 기존 보호 구성의 등록 4개/ledger를 대조·유지한 Update에 새 source/home 1개 추가. 설치 hash 확인 |
| 읽기 전용 상태/계획 | `service-update-plan.json`: Update·Apply=false·등록 5개, `grants.json`에 정확한 경로 identity. 초기 4개 등록의 package-only 계획과 구분 |
| 승인 | 이전 fixture의 승인으로 확대하지 않음. 사용자 복귀 후 UAC 포함 실행 승인 및 첫 취소 뒤 동일 Update 재승인. `service-apply-outcome.json`과 configuration/ledger 근거 보존 |
| 제외 | Unity/원본 Editor·MCP·추가 전체 장애 harness·볼륨/VSS budget 변경·기존 source/workspace/snapshot 삭제·원격/ARM 시험 |

## 준비와 승인 경계

일반 권한으로 제품 빌드와 새 package/manifest·seed를 evidence 안에 작성하고 새 source/home만 준비한다. `storage-grant`로 경로 identity를 읽고 기존 4개를 유지한 최종 grant·기본 read-only Update 계획을 작성한다. 준비 단계에서는 `setup-storage-service.ps1 -Apply`·SCM 재시작·elevation을 실행하지 않는다.

관리자 Apply는 사용자 승인 후 검토한 setup script·package manifest·grant hash에 고정한다. 보호 구성과 계획이 다르면 서비스 정지/복사 전에 거부한다. Update 전후 기존 ledger 파일 hash와 설치된 package를 대조하며 등록 삭제·ledger/snapshot 정리는 하지 않는다. 이후 CLI는 계속 일반 사용자 권한으로 실행한다. 서비스·grant 거부를 elevated CLI나 자동 fallback으로 우회하지 않는다.

## 한 묶음과 종료 조건

1. 최종 후보·설치 package hash와 새 grant·일반 권한 CLI를 확인한다. 기존 소유하지 않은 process/snapshot과 여유 공간·VSS headroom을 기록한다. unknown과 다른 작업은 보존한다.
2. A/B를 만들고 새 CLI에서 같은 A 요청을 재개한다. 정확한 ID/home/request digest·snapshot·basis와 두 반환 경로를 유지한다. 원본의 `marker.txt`를 `marker-after`로 수정해 불변 기준과 무관한 원본 지속 편집을 확인한다.
3. A에서 `data.txt=a-result`, `added.txt=a-created`, `obsolete.txt` 삭제를 수행한다. B의 `data.txt=b-result`와 `b-only.txt=b-private`는 별도 경로에 계속 편집한다. A checkpoint 뒤 `--target original --plan`의 변경 집합/충돌 0/digest를 확인하고 명시적 intent `ap-a-apply-20261002`로 반영한다.
4. 원본에서 A의 정확한 내용만 확인한다. `marker-after`와 B의 기준/편집을 유지한다. A의 명시적 release 중 B를 계속 읽고 수정하며 A 독립 결과와 실제 소유 자원 부재를 확인한다. reclaim-pending은 완료가 아니며 정확한 같은 요청만 재시도한다.
5. B checkpoint 뒤 같은 원본 경로의 충돌을 읽기 전용 계획에서 확인한다. 명시적 실행이 `OriginalApplyConflict`로 원본 쓰기 전에 거부되고 A 원본과 B 독립 결과가 유지돼야 한다. 명시적 B release로 독립 결과를 보존하고 마지막 소유 snapshot·mount·delta·process/handle/메모리 회수를 확인한다.
6. raw CLI 응답/schema·파일 hash·independent result·소유 자원 관측과 journal/결과/stage 할당을 구분해 AP-05/06을 판정한다. VSS의 workspace 귀속·전체 물리 peak는 unknown을 유지한다. 이 한 묶음 뒤 새 입력/전체 행렬을 열지 않는다.

지원 경로의 원본/peer 훼손·승인 쓰기 손실은 우선 경로를 차단하고 수정한다. 그 외 sharing·권한·소유권·손상·회수 격리 실패는 원시 증거와 작업을 보존하고 같은 요청의 필요한 복구만 한다. 비소유 snapshot 삭제·fresh key recapture·자동 rollback/sweep으로 성공을 만들지 않는다.
