# Plastic 프로젝트 기준본 검증

이 실행 당시의 과거 C: pending과 MCP 긴 응답 제한은 보존한다. 현재 C: 동일 A/B 회수와 외부 Coplay 긴 요청/reload 상세 결과의 한정 해결은 [VR/MR](vss-and-mcp-remediation.md)을 따른다.

범위는 [PPB-01~06](../plans/plastic-project-basis.md), 현재 계약은 [Plastic 프로젝트 기준본](../plastic-project-basis.md)이다. RAM 절감은 사용자가 합격 조건에서 제외했으며 실제 비용만 관측한다. 원시 증거는 `artifacts/plastic-project-basis-20261004/`에 보존한다.

아래 PPB의 native/Unity·비용 자격은 실행 당시의 cm API reader에 한정한다. 현재 private stdio reader로 이관한 내용·자동 시험의 근거는 [SB-01](security-boundaries.md)이며, 새 reader의 실제 Unity·대형 segmented revision은 NotRun이다. 과거 전체 순회와 A/B를 새 reader의 통과로 승계하지 않는다.

| ID | 현재 상태 | 근거/남은 조건 |
| --- | --- | --- |
| PPB-01 | Passed with limitations | 승인된 cs 2의 60개 파일/244,715B 전체 길이·내용 hash 확인. binary/BOM reader 결함을 수정. 외부 cache/네트워크 비용은 unknown |
| PPB-02 | Passed, 한정 native/자동 | fixed basis·실제 범위 overlay·동일 A/B 재개·관리 격리·독립 결과/release/discard 연결. 필수 회귀 961 Passed/1 선택적 skip |
| PPB-03 | Passed, 한정 실제 Unity | 원본/A/B 개별 Library probe와 저널·A checkpoint 중 B 쓰기·release 뒤 B 테스트·마지막 dependent 부재·일반 NTFS 복원 |
| PPB-04 | Passed with limitations | 정확한 A/B MCP/CLI·reload 재발견·텍스처/importer·씬 생성/편집·현재 상태 3/3 tests와 job 겹침. 긴 쓰기의 실패 응답/실제 완료 제한 보존 |
| PPB-05 | Passed, 한정 실제 Unity | A release/B discard·Library 회수 뒤 독립 결과 검증·소유 자원 0·일반 NTFS 원본 재열기와 소스/등록/기존 Editor·공유 서버 보존 |
| PPB-06 | 계측·기록 완료 | 작업 delta/공통 Library/보존 결과/runner와 실제 Editor/provider/API helper RAM·준비/수집/복원 시간을 구분. 전체 peak/공유 VSS 귀속은 unknown |

## Reader·입력 확인

제한 실행의 `cm`은 인증/관리 경로 접근 거부를 반환했다. 일반 사용자 권한으로 동일한 읽기만 수행해 현재 CLI 11.0.16.9915·원본 root/GUID/regular/static과 changeset 0·1을 확인했다. 관리자 설치/서비스/grant·원본/서버 이력 변경으로 우회하지 않았다.

실제 `ReaderProbe`는 changeset 1의 가장 큰 ProjectSettings/ProjectSettings.asset 25,496B를 읽었다. fixed listing 905ms, 첫 4KiB 요청은 전체 25,496B 검증/한 stream으로 807ms였다. 같은 64KiB chunk 안의 tail와 이전 위치 요청은 각각 1/0ms, 추가 decoded bytes와 stream 0이었다. payload 저장 0이며 이 작은 한 chunk 파일의 결과를 대형/비연속 파일의 전송 효율로 확대하지 않는다. 실제 network bytes와 native client cache writes는 unknown이다. probe는 제품 A/B 실행 자격이 아니다.

changeset 1에는 Assets가 없고 현재 원본과 비교한 추가/변경 파일은 37개·201,141B·삭제 0이다. 정확한 path/size/SHA-256은 `input-review.json`이다. 사용자가 이 목록과 필요한 상위 디렉터리만 승인해 changeset 2에 기록했다. 60개 기록 파일 중 승인한 37개 내용의 Plastic MD5가 모두 원본 검토 바이트와 일치한다. Packages·기존 private `SplinterPlasticOriginalAfterCapture.txt`/ignore.conf는 추가/변경하지 않았다. 첫 check-in은 changed-but-not-checked-out 파일에 변경 없음 오류를 반환해 서버 이력 없음(cs 0/1)을 확인했다. 동일 해시/경로에만 `--all --applychanged`를 적용한 후 cs 2가 생성됐다. 두 실행과 원본 상태, 37개 revision/hash proof를 보존한다.

## 자동/실제 결과

`PlasticRevisionTests`는 bounded binary range proof/순차 재사용·손상/길이 불일치와 buffer 미게시·원본 이후 변경/다른 owner/고정 tree 변화 거부·reader fence/같은 release intent·unsupported paths·source/storage/schema를 확인한다. 파일 mount double의 독립 결과 시험은 서버/원본 없는 검증과 discard의 dependent 제거 실패 뒤 같은 기준 회수 재개를 다룬다. 그 시험의 파일 materialization은 제품 WinFsp/no-copy native 증거가 아니다.

최초 focused 실행은 반환 배열의 참조 동일성 비교와 capabilities schema의 빌드 시점 차이로 실패했다. 동일 내용 비교와 현재 schema를 포함한 재빌드로 수정했다. 다음 실행은 새 storageMode를 반영하지 않은 기존 discovery 기대값이 실패해 현재 계약으로 갱신했다. 후속 새 시험의 PascalCase fixture JSON과 recovery assertion의 잘못된 속성명도 수정했다. 최초/후속 로그와 TRX를 보존하며 제품 내용 손실이나 첫 실행 통과로 표시하지 않는다. 최종 후보의 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, 전체 962개 중 961 Passed·실패 0·선택적 ReFS 1 skip이다. 원시 로그/결과는 `build-verified-candidate.log`와 `build-verified-candidate.trx`다.

## 실제 입력에서 발견한 수정

첫 A 생성은 cs 2의 PNG가 XML type `bin`인데 parser가 이를 제외해 PlasticBasisInvalid로 거부됐다. regular binary를 fixed revision 계약으로 연결하고 XML 재현 회귀를 추가했다. 같은 A 재개는 `cm ls / --selector`가 설치된 Windows cm 11에서 `/ is not in a workspace`를 반환해 원본 repository 대조에 실패했다. 명시적 원본 local root의 controlled-directory XML만 대조하도록 수정하고 private child 무시·다른 root/repository 거부 회귀를 추가했다. 고정 서버 tree 내용 읽기에는 기존 / + --tree를 유지한다. 두 실패는 payload 게시나 새 VSS/등록 없이 보존됐으며 같은 key/home/cs 2/Library로 재개한다.

원본 Library 준비는 522.15초, initialPayloadCopiedBytes 0이었다. 이동한 데이터는 31,646개 파일/1,903,620,373B이며 초기 metadata/해시/reader fencing 비용과 실제 복사를 구분한다. `storage-prepared.json`의 6개 root 계측은 모두 complete이고 VSS 전체 귀속은 unknown이다.

cs 2의 raw stdout 순회는 60개 중 PNG 한 개·BOM C# 두 개에서 실패했다. 실제 PNG는 24,069B인데 stdout 44,536B/다른 hash가 됐고 C# 두 개는 각 3B BOM이 사라졌다. named pipe 출력 probe도 client 오류로 0B/exit 1이었다. payload disk cache로 우회하지 않았다. 공식 local cm API blob probe는 같은 세 revision의 길이·BOM/hash를 유지했고 자기 프로세스 종료를 확인했다. 새 tree-owned API reader의 cs 2 전체 순회는 60개/244,715B/60 streams 오류 0, payload disk 저장 0이다. 외부 client 내부 cache/서버 전송·native 전체 buffering은 unknown이다.

개발 unmount 검증에서 기존 overlay schema의 VSS consistency else와 별도 Plastic 조건이 충돌하는 것을 확인했다. provider별 consistency chain에 Plastic을 넣고 실제 request schema 회귀를 추가했다. 정상 unmount 뒤 동일 A를 새 reader로 재연결하며 이전 실패/응답과 delta/기준/Library 의존성을 보존한다.

초기 import 중 metadata callback마다 manifest 전체 검증/재구성이 반복되는 병목을 확인해 tree open의 전체 검증과 manifest 읽기 핸들 pin으로 바꿨다. 1,000개 Stat 동안 write/replace 거부와 reader 종료 뒤 회수를 자동 확인한다. Unity의 없는 nested `.git`/`.plastic` 조회는 ordinary not-found로 처리하고 recorded entry의 관리 경로 거부는 유지한다. 초기 provider의 Unsupported source path 6건은 보존하며 최종 A/B provider epoch의 I/O failure는 각각 0이었다. 같은 id/home/base/Library로 정상 stop/unmount·재연결했으며 추가 cohort를 만들지 않았다.

## 실제 Unity·MCP·현재 상태 검사

원본은 `splinter-unity-plastic`의 regular/static 등록 GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`, cm `11.0.16.9915`, 고정 changeset `2@splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud`다. root revision 196·repository ID 3301056을 고정했다. Unity `6000.3.22f1`·Pipeline `0.8.0-exp.1`을 사용하고 Coplay MCP `10.2.0`/revision `30d22075093d1d35dfb0091c1c7550e9ad948577`는 A/B private Packages에만 넣었다. 원본은 MCP 패키지가 없어서 원본 MCP는 NotRun이며 CLI로 상태를 확인했다. 기존 Git Editor와 공유 MCP 8080 서버는 유지했다.

| 대상 | 작업 id | 최종 Editor PID / UTC start ticks |
| --- | --- | --- |
| 원본 관리형 Library | basis `2257c2d6c05d07f59589b19f` | `59500 / 639267199358336563` |
| A | `bc32507b0419d443add291a6` | `58548 / 639267217111827072` |
| B | `245ed2e8b3b6de691bfe5cf5` | `31864 / 639267220495578750` |

첫 import의 start→최초 MCP 연결은 A 약 25분 5초·B 약 25분 38초였고 C# compile은 각각 487.056/485.459초였다. 정상 stop 요청 두 건은 120초 timeout 뒤 실제 종료를 관측하고 확인 요청으로 마감했다. 강제 Editor kill이나 timeout에 따른 쓰기 자동 반복은 없었다. 같은 A/B의 최종 reader 재연결 ensure는 92.16/119.04초, warm start→MCP는 약 4분 14초/3분 1초, compile 32.023/30.356초였다. 원본 Library 참조가 즉시 준비를 보장하지 않으며 cache/import/동시 부하 조건이 달라 단일 원인의 속도 배율로 환산하지 않는다.

매 MCP 쓰기에 정확한 `unity_instance`를 지정하고 코드의 반환 project/PID/start와 소유 Editor를 대조했다. CLI의 최초 원본/A/B eval은 main-thread 5초 timeout이었고 명시적 읽기 전용 관측으로 정확한 대상/idle/clean을 확인했다. reload 뒤 포트를 고정하지 않았다. private test source를 편집하고 각 Editor에 scripts refresh를 한 번 요청한 뒤 MCP instance를 재발견했다. B HTTP session 변경과 두 exact identity가 관측됐으며 수동 connect 반복은 필요하지 않았다. 각 `Library/ScriptAssemblies/PlasticQualification.EditMode.dll`의 서로 다른 MVID와 실제 IL의 `get_localScale`/`get_filterMode`를 확인해 편집된 시험이 로드됐음을 입증했다.

A는 빨강/B는 파랑의 읽기 가능한 16×16 PNG와 서로 다른 `.meta`를 import했다. 각 `Parallel.unity`는 root 하나 `Qualification_A/B`, x=1/2다. 후속 편집은 filter Point와 scale 2/3을 저장했다. 두 생성 요청은 실패/null 응답 후 실제 Editor history에서 각 58.26초의 성공을, 두 편집은 실패/null 응답 후 각 약 31.6초 성공을 확인했다. 파일 hash·history·후속 테스트가 실제 반영을 증명하며 같은 쓰기를 재전송하지 않았다. 직접 `manage_scene` load/hierarchy/return은 모두 성공(약 1.4~2.1초)했고 root 이름으로 대상 격리를 확인했다. 긴 CodeDOM 쓰기의 응답 timeout 문제를 해결한 것으로 표시하지 않는다.

최종 동시 test job A `2e6d700b96ef47fc976c4d1453cd0ca7`와 B `b27d7a37e6344e69982e32bb9a4056c6`는 각각 marker/정확한 project·Library probe/texture·importer/scene·scale **3/3 Passed**다. job lifecycle 구간 겹침은 **9.84초**이며 개별 test method의 CPU 동시 시각은 미측정이다. B에서 A job을 조회한 `Unknown job_id`는 예상한 namespace 분리 증거다. A checkpoint 중 B는 `B-after-A.txt`를 쓰고 import했다. 이 요청도 null 실패 응답이지만 history의 성공·현재 파일·최종 독립 결과로 확인했고 재전송하지 않았다. A release와 함께 보낸 B 테스트 요청은 응답에 62.07초가 걸렸으며 job `4ac077cc82a5428cba95c90c0db2bc23`은 상세 3/3 Passed였다. 실제 job 시작은 15:05:09.638Z로 A release의 durable 완료 15:04:54.179Z 이후다. 따라서 checkpoint 중 실제 B 쓰기와 release 이후 B 현재 상태 통과는 확인했지만 release와 test job 실행의 겹침은 입증하지 못했다. 요청 지연 원인도 미확정이며 자동 반복하지 않았다.

## 저장량과 RAM

`storage-work.json`의 7개 root 모두 complete다. 작업 control 할당은 A **71.90MiB**, B **72.21MiB**이며 Library 쓰기를 포함한 private 범위 저널이 대부분이다. 공통 Library 할당 **1865.82MiB** 중 **1849.86MiB**는 이동한 기존 원본 payload, 약 **11.02MiB**는 보호 metadata, 약 **4.94MiB**는 원본 delta다. runner **5.63MiB**, 같은 관측의 evidence **11.17MiB**는 별도다. 작업 하나마다 1.9GB Library를 전체 복제한 비용이 아니다. 이 표본은 전체 순간 peak나 동등한 Codex 비교 재측정이 아니며 일반 절감률로 환산하지 않는다. 새 소스/Library VSS 요청은 없고 공유 VSS 전체 비용은 조회 권한 제한으로 unknown, 과거 C: pending은 그대로다.

`memory-work.json`은 정확한 PID/start의 세 표본 8개 대상 모두 complete다. Editor root Private Bytes는 A **2995.5MiB**, B **3057.1MiB**, 원본 **3207.2~3209.6MiB**, PSS 추정은 각각 **2374.9/2439.7/2011.7~2012.0MiB**였다. A/B provider Private는 약 **180MiB**, 원본 Library provider **164.4MiB**, tree-owned cm API helper는 **54.8/55.4MiB**였다. Editor 자식 전체 합계나 시스템 peak를 root 표본으로 대체하지 않는다. ShareCount 상한·시점 비원자성·kernel cache/네트워크/외부 client native buffering의 귀속은 unknown이다. RAM 절감은 이번 합격 조건이 아니다.

## 결과 보존과 회수

A checkpoint는 정상 stop을 포함해 **103.07초**, release **30.00초**였다. B checkpoint **105.65초**, 같은 id/digest/intent의 명시적 discard **5.92초**였다. `releasedResources`는 두 소유 Editor/자식 group의 verified process/working-set/private 0을 기록하고 mount/provider/handle/delta 부재·local basis released를 확인한다. 마지막 Library dependents도 없다. 자동 sweep이나 원본/peer/비소유 snapshot 삭제를 사용하지 않았다.

독립 결과 A는 `sha256:23e1c347e3749f490dc33354a2ecf1469fb59ae8003e71f9f095ffd9f89d16e0`, B는 `sha256:132eee8fcb4af5b305b6d9c2851982bbe36e89611434ab0825f365ad8d661b34`다. 두 mount가 없는 상태에서 제품 `VerifyResultAsync`로 owner·baseline/result/collection·모든 payload를 검증했다. 원본·Library basis·Plastic 서버 내용은 읽지 않았다. A/B unique payload는 **278,291/278,493B**이며 생성/읽기 0과 명시적 결과 보존 비용을 구분한다. 수정된 tests·PNG/scene/importer와 B 후속 파일도 result에 있다. 보조 verifier 최초 출력은 disposed JsonDocument의 deferred enumeration 오류로 실패했으며 배열을 materialize한 최종 출력 `independent-results-final-verified.json`이 성공했다. 제품 verifier 실패나 첫 실행 통과로 표시하지 않는다.

`storage-after-tasks.json`의 7개 root 모두 complete이며 A/B control 할당은 **552,696/594,080B**다. 작업 delta는 없고 이 metadata·독립 결과·evidence는 보존한다.

원본의 정확한 경로·PID/start·compile/updating/dirty를 확인한 정상 Exit 요청은 CLI의 Invalid response format을 반환했다. 종료 중 연결이 끊긴 응답으로 보존하고 재전송하지 않았으며 같은 PID/start의 부재를 관측했다. 검증용 원본 Library probe만 내용/정확한 경로를 대조해 제거했다. `original-final-source-case-aware-proof.json`은 승인된 37개 파일 SHA-256·Packages 두 파일·`.plastic/plastic.workspace` admission hash·원본 GUID/regular/static과 home 등록 없음이 일치함을 기록한다. 첫 보조 관측의 경로 문자열 대소문자 비교 실패는 Windows 경로 의미에 맞게 고쳤고 원본/등록을 변경하지 않았다. `owned-processes-after-tasks.json`의 초기/최종 Editor/provider/API helper/supervisor 14개 PID는 모두 부재다. MCP에는 기존 Git Editor 연결만 남았다.

복원 계획은 31,007개 파일·2,411개 디렉터리·최종 논리 1,869,236,248B, 신규 15파일·private 최종 범위 합계 22,377,109B다. 이 범위 합계는 전체 복원 I/O나 journal 저장량과 다르다. `memory-restore-moving.json`의 정확한 복원 CLI 세 표본은 모두 complete이며 Private 186,331,136B·working set 173,518,848B·PSS 추정 약 132,163,291B다. 전체 복원 peak로 읽지 않는다.

최종 `library restore`는 **883.94초**에 성공했다. cursor는 **31,007/31,007·filesVerified=true**, 상태는 restored이며 mount/base/delta·dependents와 원본 Library provider가 없다. 원본 Library는 일반 NTFS 폴더이고 probe도 없다. `storage-final.json`의 7개 root 모두 complete다. A/B control은 위 값과 같고 Library의 manifest·복원 계획·영수증 할당 **32,396,920B**, reusable runner **5,907,808B**, 해당 관측 evidence **13,613,384B**를 보존한다. 복원된 1.87GB 논리 Library는 원본 위치의 현재 데이터이며 제거한 payload나 새 복사로 합산하지 않는다.

원본을 같은 Unity/경로의 **6440/639267243826229521**로 재열었다. 최초 refresh는 8.775초였고 exact-path CLI identity의 성공은 compile/updating/failed/playing=false·clean SampleScene·Library reparse=false·probe/qualification source 부재를 확인했다. 재열기 이후에도 37개 SHA-256·Packages·Plastic 등록 identity/hash와 home 등록 없음이 일치했다. `final-cleanup-proof.json`은 기존 Git Editor **34200/639267143482280358**와 8080 서버 **73512/639267092568061834**가 유지되고 callerElevated=false임을 기록한다. `independent-results-after-library-restore.json`은 Library basis가 없는 상태에서도 두 독립 결과의 제품 검증이 성공했음을 남긴다.

PPB-01~06의 고정 한 Plastic/Unity A/B는 **scoped-complete-with-limitations**로 마감한다. 초기 준비·수집/복원 비용, 긴 MCP 쓰기의 실패 응답/실제 완료, release와 B test job 실행 겹침 미확인·요청 지연 원인 미확정, 원본 MCP 패키지 부재와 외부 cache/전송·전체 물리/RAM peak unknown을 유지한다. Plastic 원본 반영·VCS 없는 새 기준본·partial/dynamic·서버 이력 변경·Play/build·ARM/OS/대형 추가 입력은 이번 자격이 아니다. 과거 C: VSS pending은 그대로 보존하며 새 질문 없이 같은 A/B나 전체 행렬을 자동 반복하지 않는다.
