# Git 원본 Library 참조 검증

범위는 [계획](../plans/git-library-reference.md)의 GL-01~05, 현재 계약은 [원본 Library 참조](../git-library-reference.md)다. 사용자 승인 경로는 Library만 같은 볼륨에서 이동하고 원본 경로에 관리형 mount를 제공한 뒤 마지막에 복원하는 것이다. 이전 cold Git A/B와 VSS/view 결과를 새 warm 경로의 통과로 읽지 않는다.

현재 v2 restore plan의 파일 identity/생성 영수증 보호는 프로젝트 복원과 공통 `ManagedDirectoryRestoreTests`에서 확인한다. 중단 뒤 다른 파일이 같은 경로를 차지하면 동일 바이트여도 대상·기준·저널을 보존하며 거부한다. 정상 중단의 같은 요청 재개와 readonly·핸들 보호도 시험한다. 아래 GL 실행은 당시 후보의 자격이며 이 보호 변경의 실제 Unity/WinFsp 복원·OS 장애는 NotRun이다.

| ID | 현재 판정 | 근거 |
| --- | --- | --- |
| GL-01 | Passed, 한정 실제 Unity | 원본 3,148,601,607B를 같은 볼륨에서 이동·초기 복사/read cache 0. base 145, 원본 146, A 147, B 148과 base hash 불변 |
| GL-02 | Passed, 한정 실제 Unity | 고정 복원 중단/동일 계획 재개·준비 취소/보존 시험과 작은 native A/B. 실제 Unity A 회수 중 B 지속·마지막 일반 NTFS 복원 통과 |
| GL-03 | Passed with limitations | 원본/A/B exact project·PID/start 확인, A/B 명시적 reload 후 새 초기화 값/HTTP 재연결, 텍스처·씬·동시 test job. B 실패의 상세 result 소실 제한 유지 |
| GL-04 | 계측 완료, 절감 미확인 | A/B Private Bytes 약 3.08/3.06GB로 cold 표본과 비슷함. 각 provider 비용과 resident/PSS 추정은 별도이며 공유/전체 peak unknown |
| GL-05 | Passed, 한정 실제 Unity | A/B 독립 결과·소유 회수·원본 일반 NTFS Library 복원/재열기·MCP idle/clean·기존 refs/config 보존·필수 build/UTF-8 |

원시 근거 위치는 `artifacts/git-library-reference-20261004/`다. 초기 시험에서 Windows 디렉터리 시각 설정에 File API를 사용한 오류와 capabilities의 새 enum 누락을 발견하고 수정했다. 관련 72개 시험 재실행은 모두 통과했다. 성공한 재시험과 최초 실패는 구분한다. HTML은 생성하지 않았다.

현재 후보의 필수 locked restore·Release·전체 회귀는 942개 중 941 통과, 실패 0, 선택적 ReFS 1 skip이다. 중복 prestart reader 재사용 수정 후 관련 79개 시험도 일반 사용자 권한에서 통과했다. 샌드박스의 게시/Git 거부 10건은 별도 로그로 보존한다. 새 schema를 포함한 최종 필수 빌드도 일반 사용자 권한에서 종료 코드 0으로 완료했다.

원본 Library 안의 공유 MCP 서버 로그가 쓰기 핸들을 유지해 최초 capture는 InputUnavailable로 실패했으며 원본은 이동하지 않았다. 사용자가 8080 서버 재시작/외부 로그를 승인한 뒤 같은 준비 요청으로 이어갔다. 첫 실패가 만든 정확한 빈 root만 비재귀 제거했고 새 preparing intent로 같은 요청 복구를 연결했다. 새 서버는 같은 10.2.0과 8080이며 전체 명령줄/인증 토큰은 기록하지 않았다.

Library 준비 응답은 536.8466초였고 초기 실패 96.5645초/서버 재시작 비용은 별도다. A ensure 491.1709초, B ensure 518.2890초이며 B의 첫 WorkspaceBusy 5.9601초 뒤 같은 id/key/home을 재시도했다. 이는 Editor 사용 가능 시간과 다르다. 원본 CLI 목록 STATUS_NO_INSTANCES 및 eval의 5초 timeout 뒤 실제 HTTP 연결을 관측했으며 연결 요청을 무조건 반복하지 않았다.

A/B의 start부터 최초 HTTP 연결 시각까지는 각각 약 860/796초였다. 새 qualification source/bridge로 인한 컴파일·Unity의 경로별 scan을 포함하며 Library를 참조해도 즉시 준비되는 경로는 아니다. 이전 cold import 약 88분의 별도 실행과 입력 조건·관측 경계가 같지 않으므로 일반 속도 배율로 환산하지 않는다. 원본 Library의 전체 읽기 검증과 43,131개 파일의 보호 핸들·metadata 비용은 남는다.

## 실제 MCP와 현재 상태 검사

Unity 6000.3.22f1·Coplay 10.2.0·원본 development `6a54974a9c655031aeb2be17fb7622fc5d641c52`를 사용했다. A `68ee8723a88580460666be3c`의 Editor PID/start는 `61356/639267103839383275`, B `121b2f293ead60d7e18c3de2`는 `39716/639267109491871584`다. 매 쓰기와 상태 관측에서 반환 경로·PID/시작 시각을 대조했다. qualification 코드는 새 파일이며 공유 서버를 시작하지 않고 기존 HTTP에 연결만 한다.

A는 빨강 16×16 PNG·`Qualification_A` 위치 x=1, B는 파랑 PNG·`Qualification_B` 위치 x=2를 각각 import/저장/재열기했다. 원본 소스에는 작성하지 않았다. 명시적 reload 후 같은 경로·PID/start와 새 `LoadedAt` 값을 확인했고 두 Editor의 MCP session도 바뀌었다. test job A `d8380cfb5b2d42c4aab0bd95c3e187fa`, B `8b932ea9952249f2a8b70b6ac6caaa03`은 각각 marker/texture/scene 3개 모두 통과했다. job 구간 겹침은 59ms이며 개별 test method의 CPU 동시 실행 시각은 측정하지 않았다. 각 테스트는 자기 `Application.dataPath`와 고정 `project.txt`도 대조한다.

원본/A/B의 범위 쓰기 이후 base probe byte 145와 초기 SHA-256이 유지됐다. `basis-direct-write-fence.json`은 정확한 소유 base probe에 쓰기 핸들만 요청했을 때 Win32 sharing violation 32로 차단됨을 기록한다. payload 쓰기는 0이며 immutable 기준에 직접 쓰는 우회를 허용하지 않았다.

B marker만 `WRONG`으로 바꾸자 B job `d59bd996c550456aae9ae125da952369`에서 기대 B/실제 WRONG의 실패를 검출했고 A는 3/3 통과했다. B는 reload 후 `failed`, 완료 3개와 failure 메시지를 보존했지만 전체 `result`는 null이었다. MCP `TestJobManager`는 SessionState 복원 시 전체 Result를 의도적으로 보존하지 않는다. 이를 2/3 상세 결과 통과로 추측하지 않는다. marker를 복원한 B job `eaa36df4591a4b3f8c91dcb4356ba1f7`은 전체 상세를 포함해 3/3 통과했다. 요청 timeout/연결 끊김을 근거로 쓰기를 무조건 반복하지 않았다.

## 저장량과 RAM

`storage-post-task-long-paths.json`은 8개 root 모두 complete다. 최초 표본은 Win32 장경로 조회 206개 실패를 unknown으로 보존했고 `\\?\` 절대 경로 전달을 수정한 뒤 같은 읽기 전용 계측을 재실행했다. A/B control 할당은 42,097,824/41,962,912B, 그중 delta journal은 41,549,824/41,418,752B다. 이전 cold 표본의 1,950,875,648/1,953,628,160B journal 대비 약 97.9% 작지만 작업 구간·생성 데이터 상태가 완전히 동일한 벤치마크는 아니다. 공통 기준 3,211,863,192B 할당은 기존 원본 데이터의 이동이며 새 복사 비용이 아니다. basis metadata 15,037,368B·원본 delta 2,949,120B·shared runner 2,867,888B와 기존 Git 2,392,301,232B·evidence는 별도다. 새 VSS는 만들지 않았고 기존 비소유 snapshot을 건드리지 않았다. 공유 VSS 조회는 권한 거부로 unknown, 전체 물리 peak도 unknown이다.

`all-idle-memory.json`의 정확한 6개 PID/start에 대한 세 표본은 모두 complete다. 중앙 Private Bytes는 원본 3,137,077,248B, A 3,078,062,080B, B 3,058,962,432B, 원본/A/B provider 195,493,888/217,513,984/214,241,280B다. A/B non-shareable resident는 2,470,907,904/2,458,296,320B, PSS 추정은 2,501,681,152/2,487,575,698B다. 이전 cold Editor Private 약 3.10/3.05GB와 비슷하며 **RAM 절감은 확인되지 않았다**. 파일 기준 공유가 Unity heap/scene/graphics를 공유하게 하지는 않는다. ShareCount 7 상한·시점 비원자성·kernel cache와 각 Editor 간 공유 귀속은 미확정이다. 오래 idle한 최초 원본과 재시작 원본의 residency 차이를 절감/회귀의 인과 증거로 사용하지 않는다. lifetime peak를 이번 구간의 순간 peak로 읽지 않는다.

A가 종료된 뒤 별도 `B-owned-group-memory.json`은 B의 정확한 소유 Job을 QUERY로 열어 Editor·자식·supervisor 9개 PID/start를 세 번 관측했고 모두 complete였다. 첫 표본의 Private 합계는 3,731,165,184B, PSS 추정 합계는 2,991,255,552B다. Library provider는 이 Job 밖의 별도 비용이다. A/B 모두 살아 있던 앞선 Editor/provider 표본과 시간·범위가 다르며 A의 동시 전체 자식 비용이나 전체 시스템 peak를 이 값으로 대체하지 않는다.

마지막 원본 복원의 CLI만 정확한 명령으로 선택한 `restore-cli-memory.json`의 계획 작성 구간 세 표본은 Private 183,074,816B, working set 171,159,552B, PSS 추정 약 132,426,021B였다. 전체 복원 구간/peak나 Editor RAM의 대체 값은 아니다. 명령줄은 출력/저장하지 않았다.

파일 이동 구간의 `restore-cli-moving-memory.json`도 세 표본 모두 complete로 Private 205,967,360B, working set 196,886,528B, PSS 추정 약 155,632,201B였다. 고정 복원 계획은 43,129개 파일·2,497개 디렉터리·최종 논리 3,150,139,635B이며 새 파일 14개, private 범위 합계 6,483,757B다. 범위 합계는 원본 최종 payload 크기나 전체 명령의 누적 I/O와 다르다.

## 결과 보존과 회수

A 정상 stop 159.9423초, checkpoint 346.3494초, release 125.3945초였다. 독립 결과 `27bb138f76207dd4448ef2ab303dfbd0f2b479ca`와 `releasedResources`의 verified process/working set/private 0을 보존했다. A mount/provider/handle/delta·기준 ref가 없고 공통 Library에는 B 의존성만 남았다. A checkpoint/release 중 B는 같은 PID/start에서 idle/clean 상태였으며, A 회수 뒤 MCP로 `after-A.txt`를 쓰고 파랑 PNG·probe 148·marker B를 확인했다. B 정상 stop을 포함한 checkpoint는 402.7276초, release는 124.7934초였으며 독립 결과 `59917b317b250f609c506e875b27eabc9fa4099a`를 보존했다. 원본 probe는 관리형 원본 delta에서 제거한 뒤 원본 Editor를 정상 종료했고 공유 8080 서버는 같은 PID/start로 유지됐다.

마지막 `library restore`는 888.9812초에 완료됐다. cursor는 43,129/43,129·filesVerified=true, 상태는 restored이며 소유 base/원본 delta/의존성은 없다. 원본 Library는 일반 NTFS 폴더이고 probe는 없다. `cleanup-verification.json`은 A/B의 독립 ref에서 marker·PNG·씬과 B 후속 파일을 읽고 서로 다른 hash를 확인하며 Library/Temp/Logs/obj는 결과에 포함하지 않는다. 원본 development HEAD·clean 상태·기존 refs 84개·worktree 목록과 config SHA-256 `c46a6b92587ba0498859c4fd8e953417edff90f9ae4095398ef1e1d139ce3a61`을 최초 관측과 대조했다. 재열기 helper의 예약 변수명 오류는 실행 전 실패했고 변수명을 수정한 같은 검토 절차가 성공했다. 원본/결과를 덮어써 오류를 우회하지 않았다.

`process-cleanup-verification.json`은 표본에 있던 종료 대상 14개 PID/start가 모두 부재이고 callerElevated=false를 기록한다. A/B 각 Job의 durable releasedResources도 verified process/working set/private 0이다. 공유 서버 `73512/639267092568061834`는 유지되며 원본 Editor를 같은 Unity/경로의 `34200/639267143482280358`로 다시 열었다. 초기 CLI 조회는 Pipeline 준비 전 COMMAND_FAILED였고 initial refresh 완료 후 읽기 전용 catalog/실제 project·PID/start 대조와 한 번의 connect-only 요청이 성공했다. 최종 HTTP에는 원본만 있고, MCP 조회는 compile/updating/failed/playing=false·clean SampleScene·Library reparse=false·probe/원본 qualification source 부재를 확인했다.

`storage-final.json`의 8개 root도 모두 complete다. A/B control 할당은 569,504/582,048B, Library 기준의 manifest·복원 계획·영수증은 42,534,024B, reusable runner는 2,867,888B다. 작업 delta·기준 payload는 없으며 이 metadata·독립 Git 결과·증거는 보존한다. 기존 Git 포함 할당 2,391,897,040B와 관측 시점 증거 1,569,832B는 새 작업 payload와 구별한다. 원본의 복원된 Library 데이터는 원본 위치에 남으므로 회수된 데이터나 새 복사로 합산하지 않는다.

GL-01~05의 고정한 한 Git/Unity A/B는 **scoped-complete-with-limitations**로 마감한다. RAM 절감 미확인, 준비/수집/복원 비용, 실패 상세의 SessionState 소실과 전체 물리/RAM peak·공유 귀속 unknown은 유지한다. Play/build·Plastic/non-Git·일반 보안 descriptor·ARM·OS 장애·대형 추가 입력의 자격을 승계하거나 자동 반복하지 않는다.
