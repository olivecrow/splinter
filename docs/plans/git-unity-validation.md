# Git 기준본의 실제 Unity 검증

사용자 승인: 기존 Git 구현을 커밋한 뒤, 현재 열어둔 `splinter-unity`와 `http://127.0.0.1:8080/mcp`를 이용해 새 `git-snapshot`의 실제 Unity 검증을 수행한다. 기존 Unity/VSS 자격을 승계하지 않고 이번 입력과 완료 조건을 고정한다. HTML은 추가하지 않는다.

## 입력과 보호 경계

- 제품 `development`의 Git 구현 커밋 `d401677`과 현재 빌드를 사용한다. 필수 자동 검사는 919 Passed/1 선택적 ReFS NotRun이다.
- 원본은 `C:/Users/olive/Desktop/Development/splinter-unity`, development commit `6a54974a9c655031aeb2be17fb7622fc5d641c52`다. 기록된 1,602파일/약 2.8GB를 그대로 기준으로 사용하며 별도 전체 checkout/복제를 만들지 않는다. Medieval 전체 팩의 기존 입력 제약은 보존한다.
- 실제 원본은 Unity 6000.3.22f1, Coplay 10.2.0, Pipeline 0.7.0-exp.1이다. 원본 PID 81892/start ticks 639266922811331760, HTTP instance `splinter-unity@983ac2308e6c044d`를 확인했다. 원본 Editor/씬/서버·원본 branch/파일/기존 결과 refs를 유지한다.
- raw Git 기준 계약에 맞추기 위해 원본 repository에만 임시 `core.autocrlf=input`을 지정한다. global true는 변경하지 않는다. 현재 checkout 파일을 다시 쓰지 않고 clean 상태를 확인한다. 최초/임시 config 내용과 원본 파일 hash를 기록하며 종료 때 같은 내용 guard로 local 설정을 복원한다. 다른 변경을 덮어쓰지 않는다.
- 새 소유 home은 `.local/gitu1004/home`, 키는 `git-unity-A`/`git-unity-B`다. source=git/application=unity/storage=git-snapshot/base=development를 최초 요청에 고정한다. 기존 작업·VSS pending·다른 snapshot은 사용/회수하지 않는다.
- 원본 Library를 캡처/복사하지 않는다. 각 작업의 cold 생성 Library·journal/공유 Git/runner·Editor/provider/native Git 및 원본 자원 비용을 분리한다. 초기 payload 복제/read disk cache 0과 전체 공간/RAM 절감을 같은 주장으로 쓰지 않는다.

## 한 묶음의 실행과 종료

| ID | 질문 | 완료 증거 |
| --- | --- | --- |
| GU-01 | Git 기준으로 실제 Unity A/B를 열고 같은 작업을 이어갈 수 있는가 | 정확한 workspace/id/base/digest·owned Editor PID/start·실제 import/compile 완료. 응답/목록만으로 준비 성공을 판단하지 않음 |
| GU-02 | 도구가 A/B 현재 Editor에 정확히 연결되는가 | 현재 HTTP discovery·호출별 exact instance와 실제 project/PID 일치. CLI는 필요할 때 exact-path로 사용. 원본/공유 서버 보호 |
| GU-03 | 같은 상대 경로의 texture·scene 작업과 현재 상태 테스트가 격리되는가 | A/B의 다른 픽셀/marker/transform·실제 import/저장/reload, 같은 test class의 각 현재 파일 판정. B marker만 의도적으로 변경한 실패와 원복 후 통과·A 불변 |
| GU-04 | A 결과 보존/release 중 B가 지속되고 마지막 소유 자원을 회수하는가 | A 독립 결과 후 release, B 후속 resource/test와 독립 결과 후 명시적 discard. owned Editor/provider/Git reader/mount/등록/delta/basis ref 부재·독립 결과 유지·원본/config/서버 보호 |
| GU-05 | 실제 비용과 제약을 구별할 수 있는가 | 파일 할당·준비 시간·생성 Library/journal·프로세스 표본과 실패/복구 증거. 전체 순간 peak/공유 귀속 unknown은 유지 |

새 원본 통합·원본 branch 전환·Play/build·다른 VCS/ARM/OS 장애 행렬은 이번 종료 기준에 포함하지 않는다. 기존 승인된 Pipeline 0.8은 도구 연결에 필요할 때 작업 A/B의 private Packages에서만 사용하고 원본 0.7을 보존한다. 새 패키지/Editor 버전을 임의로 추가하지 않는다. 준비가 수십 분 정체되면 관측한 병목만 조사/수정하고 같은 id/home으로 이어간다. timeout만으로 쓰기·씬·test를 반복하거나 새 A/B로 실패를 숨기지 않는다.

원시 실행과 identity·원본/설정/자원 비용은 `artifacts/git-unity-validation-20261004/`에 남긴다. 명시한 종료 회수는 이 A/B의 보존/release/discard를 승인하며 정리 지시를 승인 부재로 재해석하지 않는다. 실패 작업/결과/evidence와 shared cache는 보존한다.

고정한 GU-01~05 실행과 최종 필수 회귀를 [검증 기록](../verification/git-unity-validation.md)의 scoped-complete-with-limitations로 마감했다. 같은 최종 A/B나 새 행렬을 자동 반복하지 않는다. 아래 Library 기준 재사용 요구는 별도 후속 설계·구현이며 이번 완료에 포함하지 않는다.

## 관측한 준비 병목의 수정

수정 전 A/B는 Editor 시작 전 `git update-index --refresh`에서 기존 10분 기한을 넘었다. 같은 blob의 전체 재해독이 부분 읽기마다 반복되고, subtree 검증도 객체별 Git 프로세스를 시작했다. 이 입력에서 확인한 경계만 수정한다. 첫 전체 blob hash로 고정한 64KiB chunk proof·한 blob에 제한한 메모리와 연속 decoder 재사용, bounded subtree batch 검증을 사용한다. 원본 내용 fallback·disk hydration·검사/기한 생략은 추가하지 않는다. 소유 provider를 정상 unmount한 뒤 정확한 등록에 속한 빈 index lock을 증거로 보존 이동하고, 같은 id/key/home/base/digest로 재개한다. 회귀에는 작은 연속/뒤로 읽기와 재읽기 손상의 buffer 미게시·중첩 tree 손상 거부를 포함한다.

사용자의 Library 조합 질문은 별도 후속 설계 요구다. 기존 VCS 없는 `ntfs-snapshot`은 VSS에 고정한 원본 Library와 private 범위를 사용한다. Git 경로도 새 Library의 private 변경에는 같은 범위 저널·압축을 이미 사용한다. 빠진 경계는 기존 Library 기준본의 재사용이다. Git source와 Library 기준을 조합할 수 있지만, VSS 기준을 그대로 붙이면 볼륨 snapshot을 다시 사용한다. 개발용 공통 view/cache는 단일 writer이므로 병렬 A/B가 같은 mutable cache를 쓰게 연결하지 않는다. 프로젝트 단위의 불변 Library 세대를 공통 읽기 기준으로 고정하고 각 A/B의 Library 쓰기는 별도 private 범위로 저장하는 조합을 후속으로 검토한다. Unity/package/source 호환 키·owner/peer 회수 보호와 최초 세대 준비 비용을 정한 뒤 연결해야 한다. 현재 Git 검증은 cold Library 생성 비용을 유지하며 mutable 원본 Library를 공유하거나 재사용을 구현한 것으로 표시하지 않는다.
