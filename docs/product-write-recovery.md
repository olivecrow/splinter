# 제품 VSS·CLI 쓰기 중단 개발 시험

`scripts/test-product-write-recovery.ps1`은 등록된 clean source/home과 설치된 제한 권한 서비스를 사용한다. 일반 제품 `workspace ensure`의 VSS·WinFsp·Git workspace에서 외부 client를 멈추고 정확한 소유 provider만 종료한다. 일반 provider에는 개발 observer를 연결하지 않는다. CLI·Git·client는 일반 사용자 권한이며 서비스 설치/grant·볼륨/예산·Unity·VIVOBOOK을 변경하지 않는다.

기본값은 읽기 전용 계획이고 source/home/evidence를 만들지 않는다. `-Execute`는 새 evidence만 허용하며 기존 workspace/home 기록을 보존하고 case마다 새 key를 쓴다. Release 바이너리는 `pwsh ./scripts/build.ps1`로 준비한다.

```powershell
pwsh ./scripts/test-product-write-recovery.ps1 -Project <registered-source> -HomePath <registered-home> -OutputPath artifacts/ntfs-recovery/<new-id> -BaseReference <recorded-base>
# 위 입력과 실행 조건 확인 후 -Execute. 부분 실행은 -Cases '<operation>:<boundary>'.
```

고정 fixture 경로는 `Assets/SplinterQualification/Target.fbx`와 `Assets/TutorialInfo/Icons/URP.png`다. beta는 repository의 `*.png binary`로 text 정규화 없이 보존한다. 두 tracked 파일의 작은 바이트 oracle만 evidence에 보존하며 이는 제품의 최초 복제·hydration이 아니다. 실제 snapshot 읽기와 원본 hash 일치를 확인하고, alpha 앞 4KiB를 write/flush한 prefix를 외부에 승인한 뒤 아래 경계로 간다. gamma는 같은 Qualification 폴더의 `Recovery-target.fbx`이며 기존 파일이 있으면 oracle이 실패한다. source의 파일과 `.meta`는 변경하지 않고 Unity import/asset 지원을 판정하지 않는다. Git의 text/eol 선언에 따른 정규화는 파일 원문 hash와 구분하며 이 시험은 그 정책을 변경하지 않는다.

| 연산 | client 경계 | 수 |
| --- | --- | --- |
| write/rename/replace/truncate/명시적 file flush | 연산 전, 완료한 oracle와 외부 ACK 후 | 각각 2, 합계 10 |
| write | 실제 Write 반환 뒤 명시적 file Flush 전 | 1 |
| mmap | view Flush 전, view/file Flush 완료와 외부 ACK 후 | 2 |

write는 8192 위치의 4KiB, truncate는 4KiB 길이, replace는 beta를 alpha로 바꾼다. mmap ACK는 view와 file Flush가 모두 끝난 뒤 게시한다. 명시적 Flush 전의 미승인 mmap page는 부재 또는 완전 생존을 허용한다. 정상 Write 반환은 제품의 개별 durable journal 계약을 따르므로 명시적 file Flush 전에도 새 내용 보존을 요구한다. callback 내부·OS flush 내부·전원 손실의 oracle이 아니다.

request/signal/result는 `product-write-recovery-probe-{request,signal,result}.schema.json`이며 정확한 workspace/snapshot/token/generation/PID/시작 시각을 고정한다. prefix/ACK/signal은 새 임시 파일을 flush하고 overwrite 없는 move로 게시한다. 실패는 client만 종료하고 제품 provider/workspace/snapshot과 delta를 보존한다. 종료 직후 journal 공유 오류 32/33만 최대 5초 재관측하며 손상·접근 거부·unknown을 숨기지 않는다.

정확한 provider 종료와 mount 부재 뒤 동일 ensure는 같은 snapshot/delta/Git 등록을 이어야 한다. 새 현재 VSS read verification·새 provider generation을 확인하고 두 번째 ensure는 같은 새 provider와 journal을 유지해야 한다. 실제 kernel 읽기·beta 4KiB 새 쓰기 후 `validation preserve-worker`로 독립 Git 결과를 명시적으로 보존하며 컴파일 성공은 부여하지 않는다. ref/blob의 바이트를 검증하고 일반 release의 source seal·mount/Git 등록/delta/마지막 snapshot 회수와 프로세스/핸들/메모리 0을 확인한다. `reclaim-pending`은 완료가 아니다.

원본 HEAD/index/tracked hash·기존 refs/worktree와 서비스의 capture/release-isolation snapshot ID를 대조한다. 일반 사용자의 전역 VSS 조회 실패는 unknown이며 0이 아니다. 제한 서비스의 guard와 영수증 근거만 사용하고 비소유 snapshot을 직접 삭제하지 않는다. 실행·실패·현재 후보·지원 한계는 [NR 검증](verification/ntfs-recovery.md)이 소유한다. 이전 fake/kernel 후보·Unity·ARM·재부팅/전원/공간 장애의 자격을 승계하지 않는다. VIVOBOOK 시험은 실행 전에 사용자에게 알린다.
