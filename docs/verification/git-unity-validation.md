# Git 기준의 실제 Unity 검증

이번 입력·종료 기준은 [실행 계획](../plans/git-unity-validation.md)이 소유한다. 이전 VSS/Unity 자격을 승계하지 않는다. 제품 구현 commit `d401677`에서 원본 `splinter-unity` development `6a54974a9c655031aeb2be17fb7622fc5d641c52`의 1,602 tracked files/2,804,769,314바이트와 Unity 6000.3.22f1·Coplay 10.2.0·원본 Pipeline 0.7을 고정했다. 실제 원본 Editor PID 81892/start ticks 639266922811331760, HTTP `splinter-unity@983ac2308e6c044d`를 확인했다.

새 home은 `.local/gitu1004/home`이며 A `4a559ff02453d25916bf9a4b`, B `1132a8831629b7e773a16a27`다. 원본 Library는 캡처하지 않았다. 원본 repo의 local `core.autocrlf=input`만 임시 사용한 뒤 원래 config를 복원했고 global 설정·checkout 파일은 보존했다. 독립 원시 자료는 `artifacts/git-unity-validation-20261004/`다. 기존 stdio connector의 instance 0과 사용자 HTTP 서버의 실제 original 연결을 구별한다.

## 확인한 준비 결함과 복구

수정 전 A/B의 mount 연결은 완료됐으나 `git update-index --refresh`가 각각 기존 10분 작업 기한을 넘었다. A는 소유 provider PID 55556에서 385MB/1,021 read, 570MB/1,251 read, 1.11GB/2,451 read 등으로 진행하다 기한에 도달했다. 요청분 수치를 전체 blob 해독량으로 해석하지 않는다. 최초 payload 복제/read disk cache는 0, 당시 private journal은 `.git` pointer 98바이트 payload/792바이트 전체였다. 실패 명령 JSON과 상태·저널을 보존한다.

관측한 두 경계를 수정했다. 첫 full blob hash로 64KiB SHA-256 proof를 고정하고 한 blob의 proof(최대 4MiB/8GiB blob), 검증 chunk 하나와 bounded 요청 staging만 유지한다. 연속 decoder를 이어 쓰며 뒤로 읽기·다른 blob은 필요한 stream 재시작을 한다. 새 요청분이 처음 검증한 고정 내용과 일치함을 확인하고 전체 요청분 검증이 실패하면 destination을 전혀 게시하지 않는다. subtree는 하나의 bounded batch stream에서 모두 hash 검증한다. 매 요청의 전체 blob 손상 감사·무제한 native Git RAM을 주장하지 않는다.

각 실패 명령이 종료한 뒤 정확한 owner/request/token과 client handle 0을 대조해 기존 private stop protocol로 provider를 정상 unmount했다. 동일 등록 `.git/worktrees/<id>`의 빈 `index.lock`은 exclusive open·내용/생성 UTC 시각으로 확인해 `preparation-recovery-A/B/owned-stale-index.lock`에 보존 이동했다. A 복구 스크립트의 PowerShell using 문법 오류와 local/UTC 비교·최종 phase 명칭 오류는 실행/가드 실패로 구별하며, 정상 provider stop 뒤 추가 자원을 삭제하지 않았다. 새 A/B 또는 원본/peer/VSS 정리로 우회하지 않았다.

첫 수정 build는 아직 실행 중이던 B CLI PID 43932의 출력 DLL 잠금 때문에 실패했고 로그를 보존했다. B의 기한 종료·동일 소유 stop 뒤 필수 locked restore/build/회귀는 922 Passed/1 선택적 ReFS NotRun, Git reader의 선택 검사는 14/14 Passed였다.

수정 reader로 동일 A/B의 준비를 재개해 등록·mount·source 검사와 Editor 시작을 완료했다. 준비 중 약 8.54GB의 누적 source 읽기와 약 704,000 callback을 관측했다. 초기 index 읽기·raw SHA-256·Git 해시 입력이 포함된 누적 값이며 최초 데이터 복제량이 아니다. 별도로 range source의 full SHA-256 입력을 기존 4KiB 대신 상한 1MiB pooled buffer로 읽도록 수정했다. 내용 hash·취소·journal barrier·manifest 게시 의미는 유지한다. A 정상 Editor 종료 뒤 전체 감사는 1,620파일/2,804,781,822바이트 SHA-256·동일량 Git hash 입력을 131.494초에 확인했다. 이 명령 전후 provider read callback은 17,418회, baseRead는 6,094,751,628바이트 증가했고 I/O failure는 0이었다. Git 검사와 index 관측을 포함한 값이므로 SHA-256 단독 callback/전체 성능 개선율로 환산하지 않는다.

A Editor PID 69124/start ticks 639266955196373287, B PID 81688/start ticks 639266955572189536으로 시작했다. 두 `unity start`는 `Accepted`/running=true/ready=false였으며 컴파일 통과를 의미하지 않는다. 약 30분의 cold PackageCache 준비 뒤 두 Editor의 package 등록 완료 로그를 확인했다. 이 구간의 저널은 A 약 795MB/B 약 838MB까지 증가했고 I/O failure는 0이었다. 개별 durable flush의 비용을 관측했으며 ACK/flush를 합치거나 생략하지 않았다.

같은 프로그램 소스의 build는 경고·오류 없이 완료됐으나 cold import와 겹친 전체 회귀는 920 Passed/2 Failed/1 선택적 ReFS NotRun이었다. 실패는 기존 preparation 자식이 30초 안에 경계에 도달하지 못한 경우와 storage setup 자식의 30초 종료 대기 취소다. `full-under-cold-import.trx`와 로그를 보존한다. A/B 회수 뒤 필수 `pwsh ./scripts/build.ps1`의 locked restore·Release 빌드·전체 923검사는 922 Passed/0 Failed/1 선택적 ReFS NotRun으로 통과했다. 테스트 시간은 4분 42.447초이며 앞선 두 시간 제한 검사도 통과했다. 제품/시험의 기한을 늘리거나 실패 검사를 제외하지 않았다. `build-quiet-final.log`·`full-quiet-final.trx`와 UTF-8/whitespace 검사 결과를 최종 후보의 근거로 사용한다.

GUI Editor의 cold InitialRefreshV2는 A 5,297.249초(88.3분), B 5,263.066초(87.7분)에 완료됐다. 실제 Medieval village 텍스처·모델·URP shader include·material/terrain/prefab 입력을 import했고 첫 스크립트 컴파일 시간은 A 124.635초/B 197.441초, Editor 어셈블리 초기화는 약 73초였다. 일부 원본 텍스처의 Crunch는 약 38초로 기록됐다. FBX 중복 이름/타입 및 terrain shader 의존성/지원 경고를 보존한다. 완료 후 정확한 project/PID에서 compiling/updating/compilationFailed/playing=false를 확인했다. 빠른 준비나 warm Library 자격은 아니다.

`texture-import-memory.json`의 당시 Unity Private Bytes는 A 약 3.10GB/B 약 3.05GB, 원본 약 3.04GB였다. process group private 합계는 A 약 3.67GB/B 약 3.66GB이며 원본 group은 shared HTTP/licensing 자원도 포함한다. 표본의 Editor RAM 절감은 확인하지 못했다. resident working set 변화·group 합계를 전체 실제 RAM 귀속이나 순간 peak로 해석하지 않는다. Git 기준본의 initialPayloadCopiedBytes/readCacheBytes 0과 별개의 결과다.

## 실제 MCP 편집·현재 상태 테스트

사용자 HTTP 서버에서 original+A+B를 발견했고 private bootstrap은 각 Editor의 HTTP 연결만 시작했다. 원본 server/global preferences를 수정하지 않았다. 매 호출에 exact instance를 지정하고 C#에서 실제 project full path와 PID/start ticks를 대조했다. 첫 identity 코드의 변수명 `process.StartTime`이 MCP의 `Process.Start` 문자열 검사에 걸려 거부됐고, 변수명만 변경한 읽기 요청으로 확인했다. safety checks는 유지했다.

A/B의 `execute_code` 요청을 병렬 실행해 동일 경로 `Assets/GitQualification/parallel.png`에 각각 빨강/파랑 16×16 texture를 만들고 Unity importer로 확인했다. 동일 경로 `Parallel.unity`는 A `Qualification_A`/x=1, B `Qualification_B`/x=2로 Unity API를 통해 생성·저장했다. 원본에는 qualification 경로가 없다. Scene 테스트의 additive 로드를 위해 각 Editor에서 원래 SampleScene을 열고 dirty=false를 확인했다.

동일 `GitQualification.EditMode` assembly의 marker·scene·texture 세 테스트를 A/B에 작성해 각각 3/3 성공을 반환받았다. 초기 A job `1d5d002d332d439ca5a835d5df0e708f`와 B `978d150c172f4dd4924370fdb0449fda`의 job 시작/종료 구간은 775ms 겹쳤다. 개별 test method의 실행 구간은 측정하지 않았다. B marker만 `wrong-B`로 바꾼 뒤 A는 다시 3/3 성공했고 B의 명시적 negative job `1f961c4c622447bfa6bd565319c0ef1e`는 completed=3/total=3, marker 실패 하나를 기록했다. B marker를 복원한 마지막 job `7598902cd11e489d88565e5065b0d2f4`는 다시 3/3 성공했다.

B의 negative/복원/후속 파일 작성 뒤 첫 `run_tests` 요청은 각각 domain reload 중 연결이 끊겼다. 실제 reload·같은 project/PID의 HTTP 재연결 뒤 읽기 전용 SessionState 검사로 current job이 없고 해당 요청의 새 job도 없음을 확인한 후 명시적으로 실행했다. 타임아웃만으로 쓰기를 자동 반복하지 않았다. negative job의 실패 목록은 남았으나 `result` 상세는 두 조회 모두 null이었다. 복원 후 성공 상세는 반환됐다. 따라서 대상 격리·재연결은 확인했지만 reload 동안 요청/실패 상세 보존의 완전한 안정성을 주장하지 않는다. Mono Process RAM API의 0은 외부 process 관측으로 대체했고 메모리 비용 0으로 읽지 않았다.

## 독립 결과·peer 지속과 회수

A checkpoint는 `72b19165b4cd5dcf133d8afdd9608d9e7993254a`를 `refs/splinter/snapshot-results/4a559ff02453d25916bf9a4b`에 보존했다. 18개 qualification source/meta 경로만 추가됐고 Library는 포함하지 않았다. 명령 경과 시간은 checkpoint 295.304초, release 117.684초였다. A의 소유 application process/handle/mount/provider/delta와 basis ref·linked worktree 등록 부재를 확인했고, 독립 결과는 유지됐다.

A 회수 중 B의 같은 PID/start ticks·idle/clean scene을 조회했고, 회수 뒤 `after-A.txt` 작성 및 파랑 texture 읽기가 성공했다. 첫 후속 테스트 요청의 reload disconnect와 job 부재를 확인한 뒤 새 job `98c175445ef04515abb1eb7bb30633a3`은 세 테스트 모두 성공했다. B 정상 Editor 종료 후 원본 PID/start ticks·SampleScene dirty=false·compiling/updating/failed/playing=false·HTTP 연결을 다시 확인했다. B 전체 감사는 1,622파일/2,804,782,008바이트 SHA-256·동일량 Git hash 입력을 135.053초에 확인했다.

B checkpoint는 `d152ef87b611a46577aec6425654b415ef2fbc42`를 `refs/splinter/snapshot-results/1132a8831629b7e773a16a27`에 보존했고, 같은 `gu1004-discard-B` intent/request digest로 명시적 discard를 완료했다. `cleanup-verification.json`은 A released/B discarded와 mount/provider/handle/delta/basis ref·Git 등록 부재, 결과의 marker·씬·서로 다른 PNG hash·B 후속 파일, Library/Temp/Logs/obj의 결과 제외를 확인한다. 원본 development HEAD/clean 상태·기존 refs 16개·worktree 목록을 최초 관측과 대조했고 local config hash와 global autocrlf도 정확히 복원/유지했다. `process-cleanup-verification.json`에서 이전 자원 표본의 A/B 소유 78개 PID/start ticks가 모두 부재였고 callerElevated=false였다. 최종 HTTP 목록에는 원본만 남고 원본의 동일 PID/start ticks·clean SampleScene·idle 상태가 유지됐다. 독립 결과·control/evidence·내용 키 runner cache는 보존하며 제품 소유 작업 자원 잔존으로 읽지 않는다.

## 비용 표본

`storage-after-authoring.json`의 모든 7개 root 할당 관측은 complete였다. A/B control은 각각 1,952,709,928/1,955,405,712바이트, 그중 delta는 1,950,875,648/1,953,628,160바이트였다. delta에는 새 Library·PackageCache와 private source·metadata가 함께 있으므로 전체를 최종 Library 논리량으로 읽지 않는다. shared runner는 세 내용 키/24파일 4,051,976바이트다. 기존 shared Git 2,392,262,208바이트와 원본 Library 3,211,753,520바이트는 작업별 초기 복제량이 아니며 중복 합산하지 않는다. 초기 각 journal은 8,192바이트 파일 할당으로 시작했고 initialPayloadCopiedBytes/readCacheBytes는 0이었다. 저널은 cold 생성 데이터/반복 변경의 범위 저장·압축을 포함하지만 warm 기준 재사용은 하지 않았다.

`storage-final.json`의 모든 7개 root 할당 관측도 complete였다. A/B control 할당은 1,765,536/1,835,576바이트이며 delta는 없다. source manifest·소유 영수증·Editor 로그와 복구/회수 metadata가 남는다. shared runner 4,051,976바이트, 독립 결과를 포함한 기존 shared Git 2,391,857,504바이트, 관측 시점 evidence 5,130,992바이트를 구별한다. `final-memory.json`의 A/B process group은 각각 0이고 원본 group은 유지됐다.

전체 순간 peak와 공유 VSS·시스템 RAM의 workspace 귀속은 unknown이다. Git 경로는 새 VSS capture/reclaim을 호출하지 않았으며 기존 비소유 snapshot을 삭제하지 않았다. 현재 표본의 RAM 절감 미확인과 cold 준비/결과 수집 비용을 no-payload-copy의 실패나 비용 0으로 바꾸지 않는다.

## 실행 상태

| 질문 | 상태 |
| --- | --- |
| GU-01 실제 import/compile·동일 작업 재개 | Passed, cold import 약 88분과 준비 실패/수정/동일 요청 재개 포함 |
| GU-02 exact HTTP A/B 인계 | Passed with limitations, reload 연결 복구·요청 disconnect와 실패 상세 소실 보존 |
| GU-03 texture/scene/current-state tests | Passed, 병렬 authoring·A/B 3/3·B 의도 실패/복원 확인, test method 동시 실행 시각 미측정 |
| GU-04 독립 결과·A release 중 B 지속·B discard | Passed, 독립 결과·peer 후속 작업·두 작업 소유 자원 0·원본/config/기존 refs 보호 |
| GU-05 공간/RAM·준비 비용 | Passed with limitations, 단계별 할당/정리·RAM 표본·cold 비용 확인. RAM 절감 미확인·전체 peak unknown |

warm Library 조합·Play/build·원본 통합·새 VCS/ARM/OS 행렬은 이번 자격이 아니다. 사용자의 Library 조합 요구와 프로젝트 단위 불변 기준 필요는 실행 계획에 보존한다.

GU-01~05의 고정한 한 A/B 실행은 scoped-complete-with-limitations로 마감한다. 새로운 Git reader의 실제 소스 감사·Unity 편집/시험·소유 회수를 확인했으며, cold 준비 88분·MCP reload 요청/실패 상세 보존 제한과 RAM 절감 미확인은 후속 판단에 유지한다. 이 실행을 빠른 warm 준비나 일반 지원으로 확대하지 않는다.
