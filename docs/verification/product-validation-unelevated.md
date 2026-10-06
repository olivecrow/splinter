# Library 수명주기와 일반 권한 Unity 검증

2026-09-29의 작은 Windows x64·NTFS·Unity `6000.3.22f1` 입력에서 **WF-09~12와 LB-01~05를 scoped-complete-with-limitations로 마감**한다. 기존 Library snapshot 직접 읽기, 단일 cache의 A/B/A 재사용, 정지 cache 정리와 재증가, 독립 결과 보존과 최종 회수를 실행했다. SF-02/03 전체, 대형 프로젝트의 용량 목표, 일반 cold 안정성·NR 전체·기본 모드 선택의 완료를 뜻하지 않는다.

## 재현 입력과 후보

- 제품은 `development @ 183118ba1f9071b19f8e725ba7703db07b53c1f6`에 이번 변경을 적용한 후보다. 원시 증거는 [`library-20260929`](../../artifacts/storage-foundation/library-20260929/)에 있다. `candidate-manifest.json`은 실행 파일 hash, `candidate-source.json`·`candidate-code.patch`·`candidate-source-files/`는 코드·계약·시험 변경을 고정한다. 실행 중 제품 바이너리를 교체하지 않았다.
- `T:\ua-source`, `.local\wl29`, 기존 NTFS VHD와 기존 VSS 예산을 사용했다. 기존 UA의 `development`와 독립 결과 refs를 보존하고 시험용 `codex/library-20260929 @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`를 구성했다. 작은 입력의 출처는 `58f81b5bc6ff8a940d8a90872bd3fed858a54cc1`, 계측은 형제 fixture의 `6a54974`다. 이 시험 입력 준비는 제품 workspace의 데이터 복제가 아니다. 형제 저장소의 `development`는 수정하지 않았다.
- cold에는 원본 Library가 없었다. 회수 후 원본 경로의 소유 Unity를 한 번 실행해 Library를 만들고 정상 종료했다. 준비 123.01초, Library 논리 1,837,363,213B·파일 할당 1,871,929,336B·31,904개 파일이다. 소스 변경은 없었다. warm은 이 Library를 재사용했다. A의 96개, B의 94개 입력 파일 hash가 cold/warm에서 각각 정확히 같다.
- 서비스는 사용자 승인으로 기존 등록 두 개를 유지하고 해당 project/home identity를 추가했다. 등록은 로컬 SID·경로·파일/볼륨 identity의 허용 목록이며 외부 공개가 아니다. 설치 패키지 manifest SHA-256은 `e6698e38ec2659689bdecb8a0ff745055e8b953689c5a36c4d37216e523aeafb`다. 관리자 구성 한 번과 이후 일반 사용자 실행을 구분한다.

## 실제 실행과 판정

| 관문 | 이번 후보에서 확인한 결과 |
| --- | --- |
| WF-09 | 일반 CLI 187회 모두 `callerElevated=false`. Git·Unity·provider는 이 일반 사용자 실행 경로를 사용했다. VSS만 설치된 SYSTEM 서비스에 요청했다. 50ms 대기를 둔 consent 관측 68,928회에서 추가 UAC 관측 0. 서비스 업데이트 UAC는 별도다. |
| WF-10 | cold A/B/A와 warm A/B/A/정리 후 A, 총 7개 source generation의 실제 코드·에셋·최종 refresh·독립 결과를 확인했다. 같은 trial의 view 경로와 cache는 동일하다. 준비/검증을 위해 각 세대를 정상 종료 후 재시작했다. 원본 Library 준비를 포함해 Unity 시작은 총 15회다. |
| WF-11 | 실제 `prepare-worker`와 같은 요청 재호출, 새 CLI의 같은 key/ID/snapshot 재연결을 확인했다. 게시 뒤 worker A를 24로 다시 편집해도 불변 A 후보는 23, B는 33이었다. worker 결과의 `validationClaimed=false`를 유지했다. |
| WF-12 | cold 7개·warm 8개 결과를 bundle과 별도 bare Git에 보존했다. 소유 snapshot 회수 후 `git fsck --full --strict`, 객체·코드·텍스처·세대 출력·원본 모델/PNG 객체를 재확인했다. alternates가 없으며 검사는 원본/snapshot을 읽지 않는다. |
| LB-01 | warm Editor 시작 전 private cache journal 0B. `mounted-cache-b.json`에서 snapshot 직접 읽기 736,942,029B·초기 복사 0B·overlay I/O 실패 0을 관측했다. 범위 쓰기와 전체 파일 copy-up 부재는 실제 경로 및 16MiB 기준 파일/4KiB 변경 자동 시험을 함께 근거로 삼는다. |
| LB-02 | 최초 A snapshot을 cache 기준으로 고정한 채 서로 다른 A/B snapshot 후보를 선택했다. snapshot 생성 후 원본 Library 시험 파일을 바꿔도 네 세대는 고정된 옛 내용만 읽었다. artifact를 회수한 뒤에도 cache가 남으면 worker release가 `SnapshotInUse`로 거부됐다. |
| LB-03 | 정지 자원·명시적 12GiB 예상 겹침 예산·내용 동등성 검사 후 cache 정리를 실행했다. 같은 요청 재호출은 동일 attempt의 완료 receipt를 반환했다. 실제 재실행과 재증가도 확인했다. 8개 중단 경계의 복구와 작은 예산 거부는 자동 시험이며 실제 전원 장애 시험으로 확대하지 않는다. |
| LB-04 | 동일 소스의 cold/기존 Library, 준비·재시작·검증·보존·회수 시간과 추가 파일 할당을 기록했다. 기존 Library가 새 경로에서 전체 payload로 복제되는 결과는 관측되지 않았다. 원본 Library 준비 비용·측정 오버헤드·공유 VSS·표본 최고치의 한계를 아래에 분리한다. |
| LB-05 | source/candidate 결과 보존·회수 → cache 닫기 → worker 회수 순서를 실행했다. 최종 소유 프로세스·메모리·handle 0, mount/provider/Git 등록/delta와 소유 VSS 부재를 확인했다. 기존 비소유 snapshot 세 개와 UA refs·시험 기준 소스/index hash는 보존됐다. |

각 A는 코드 값 23·1024 텍스처·A 전용 파일, B는 33·2048 텍스처·A 파일 부재를 확인했다. 모델 3 mesh와 씬 복구, PID·시작 시각·projectPath·domain/MVID·요청/sequence/seal을 연결한다. 이전 generation의 `Validation*.txt`는 새 세대에 유입되지 않는다. raw 입력/응답/후속 payload schema 검사 530건은 모두 유효했다.

640×480 PNG는 요청 경로·hash·pass별 픽셀을 검증했다. cold 첫 A는 `[0, 9430]`으로 두 번째 draw를 채택했고, 나머지 검증 세대는 첫 draw가 유효했다. 정리 전후 A의 PNG SHA-256은 `5138d38f72aaa13739b4fb0f302b262db6d4338ce4dde9d5854a591e699e72a2`로 같다. Unity 내부 첫 draw 원인은 확정하지 않는다.

## Library 저장량과 정리

아래 journal은 Library를 포함한 관리 cache 전체의 논리 길이다. 불변 원본 Library, 공유 VSS, source generation, Git 결과와 별개다.

| 정지 경계 | cold cache journal (B) | 기존 Library cache journal (B) |
| --- | ---: | ---: |
| Editor 시작 전 | 0 | 0 |
| 첫 A | 2,282,881,974 | 145,697,693 |
| B | 2,318,250,764 | 181,050,420 |
| 다시 A | 2,352,051,347 | 216,031,269 |
| 정리 직후 | 실행하지 않음 | 108,340,700 |
| 정리 후 A 재실행 | 실행하지 않음 | 142,327,043 |

같은 A/B/A 종료 경계의 warm journal은 cold보다 **90.82% 작았다**. 이는 이 입력의 cache 변경 저장량 비교다. 원본 Library의 약 1.87GB를 무료 공간이나 workspace별 절감량으로 중복 계산하지 않는다. 원본에 Library가 없을 때는 여전히 cold import가 필요하다.

| warm cache | 정리 전 | 정리 직후 | A 재실행 후 |
| --- | ---: | ---: | ---: |
| 현재 유효 private payload (B) | 96,787,797 | 96,787,797 | 96,818,730 |
| 대체된 payload (B) | 77,261,698 | 0 | 21,751,456 |
| frame/metadata (B) | 41,981,774 | 11,552,903 | 23,756,857 |
| 실제 journal 파일 할당 (B) | 216,035,328 | 108,343,296 | 142,327,808 |

정리로 journal 길이 49.85%를 줄였다. 정리 전후 Library 논리 1,840,074,382B·31,915개 파일과 유효 private bytes가 같았다. 정리 CLI는 11.10초, 내부 엔진은 5.17초, 같은 요청 replay는 1.70초였다. 동시에 존재한 이전/새 journal 할당 합계는 324,378,624B다. 마지막 A의 두 Editor 실행 후 journal은 33,986,343B 증가했다. 자동 정리는 없으며, 유효 Library를 삭제하거나 불변 기준 payload를 새 journal로 복사하지 않는다.

## 준비와 반복 비용

| trial/세대 | 첫 start 요청→준비 ready (초) | 검증 재시작 요청→ready (초) | 최종 refresh 요청→성공 (초) | 첫 start→source 결과 보존 (초) |
| --- | ---: | ---: | ---: | ---: |
| cold A | 1554.54 | 181.59 | 149.37 | 2100.80 |
| cold B | 194.07 | 164.58 | 60.39 | 521.20 |
| cold A 복귀 | 93.13 | 71.92 | 24.64 | 277.63 |
| warm A | 226.85 | 62.46 | 24.64 | 406.58 |
| warm B | 92.18 | 65.76 | 25.60 | 281.52 |
| warm A 복귀 | 87.04 | 77.66 | 33.33 | 289.56 |
| 정리 후 A | 85.20 | 64.93 | 29.06 | 272.68 |

cold 첫 refresh 시간은 최초 포커스 거부와 동일 요청 재시도 대기를 포함한다. trial baseline부터 최종 회수까지 cold 3285.79초, warm 1626.17초다. warm에는 추가 A와 정리도 포함하지만 원본 Library 준비 123.01초는 별도다. 소수의 순차 실행이며 OS/Unity cache·계측·포커스 조건을 통제한 성능 우위 시험이 아니다.

## 추가 파일 할당과 한계

| 정지 경계 | cold home (MiB) | warm home (MiB) | warm cache journal (MiB) |
| --- | ---: | ---: | ---: |
| 첫 A | 2188.346 | 151.002 | 138.949 |
| B | 2231.481 | 194.258 | 172.664 |
| A 복귀 | 2273.119 | 237.166 | 206.027 |
| 정리 직후 | 해당 없음 | 134.492 | 103.324 |
| 정리 후 A | 해당 없음 | 176.430 | 135.734 |
| 최종 회수 | 2.221 | 3.122 | 0 |

home에는 worker/candidate/source/cache journal·영수증·로그·TEMP/TMP가 포함된다. warm baseline에는 cold의 보존 메타데이터 2.221MiB가 남아 있다. 최종 남은 home 파일은 기록이며 active journal은 0이다. 각 trial의 최종 증거/독립 결과 추가 할당은 cold 20.738MiB, warm 20.233MiB, 원본 Git 추가 할당은 각각 45.273KiB·20.703KiB다. 상세 분해는 `evidence-summary.json`과 `sample-*.json`에 있다.

- C:의 home 증가·시험 artifacts 증가·VHD backing 증가를 중복 없이 합친 **완전한 표본의 관측 최고**는 cold 2,426,399,008B, warm 319,740,536B다. VHD 내부 Git/Library/VSS를 여기에 다시 더하지 않는다. 두 trial의 시작 baseline은 다르며 원본 Library 준비를 제외한 warm 증가량이다.
- 이 최고값은 순간 물리 peak가 아니다. 실제 정리의 두 journal 합계 324,378,624B만으로도 warm 표본 최고보다 크므로 정리 순간 전체 peak를 놓쳤음을 알 수 있다. 전체 물리 peak는 unknown으로 유지한다.
- 전체 891개 표본 중 trial 구간은 cold 630개·warm 206개다. 게시/회수 중 사라진 파일 때문에 각각 2개·1개가 불완전했고 최고치에서 제외했다. 최대 표본 간격은 6.93초·8.90초다. 계측 자체의 누적 벽시계는 cold 133.79초·warm 601.45초이며 실행 시간에 영향을 줄 수 있다. 원본 Library 준비의 바뀌는 PackageCache 임시 파일도 별도 불완전 표본으로 보존했다.
- capture/release 경계의 T: 공유 VSS 관측 최고는 used 1,802,240B·allocated 34,127,872B이고 마지막에는 둘 다 0이다. 같은 경계의 C: 최고는 used 7,490,551,808B·allocated 8,959,721,472B다. 볼륨 공유 관측이며 workspace 귀속과 경계 사이 최고치는 unknown이다. C:의 기존 snapshot을 삭제하지 않았다.
- 원본 NTFS/VHD와 준비한 원본 Library, 독립 결과, 원시 artifacts는 보존했다. VHD를 분리하거나 원본 Library를 삭제해서 공간 수치를 맞추지 않았다.

## 실패·복구와 자동 검증

- cold 첫 A의 `EditorFocusRequired` 1회를 보존했다. 정확한 PID·시작 시각·실행 경로의 Editor를 일반 권한으로 활성화하고 동일 요청을 재시도했다. 이후 refresh 전에도 소유 창 활성화를 기록했다. 이를 완전 무인 실행으로 표현하지 않는다.
- cold A의 최종 Editor 로그에 heartbeat `File.Replace` IOException 기록 2건이 있다. 후속 heartbeat와 최종 검증은 정상이며 독립 결과와 회수도 확인했다. 원인은 미확정이다. 나머지 최종 Editor 로그에는 같은 bridge 오류가 없었다. 준비 로그는 재시작 시 덮어쓸 수 있으므로 모든 실행 구간의 오류 0을 주장하지 않는다. 과거 IPC 경합의 정확한 원인 해결 증거로 승계하지 않는다.
- 개발용 원본 Library 준비 helper가 deps/runtimeconfig 파일 누락으로 Unity 시작 전에 한 번 실패했다. 실행 프로세스와 Library가 없음을 확인하고 실패 디렉터리를 보존한 뒤 helper 파일 구성을 보완했다. 제품 바이너리 변경이나 privileged fallback은 없었다.
- 두 `SnapshotInUse` 응답은 cache 수명 의존성의 예상된 거부다. artifact를 보존/회수한 뒤에도 cache가 기준 snapshot을 붙들고 있음을 확인했으며 `close-view` 후에만 worker 회수가 성공했다.
- `pwsh ./scripts/build.ps1`: locked restore·Release 빌드 성공, **588 Passed / 0 Failed / 1 Skipped**. 첫 전체 실행의 제어 파일 시험 시간 초과는 따로 보존했고 단독 9개 시험은 통과했다. 해당 시험 묶음을 다른 flush 부하와 병렬 실행하지 않도록 분리하되 4개 동시 reader·500회 게시·30초 deadline 및 제품의 1초 재시도 계약은 유지했다. 최종 전체 실행을 다시 통과했다.

실제 강제 종료·응답 유실의 모든 명령 경계, 재부팅/전원/공간 장애, 일반 파일·에셋과 ARM A/B는 별도 NR 관문이다. Library의 이번 구현·실측·회수는 마감하지만, SF-02/03 전체는 Partial, SF-04~06과 설치/binding/discard는 기존 관문을 유지한다.
