# Unity L/A/B 실제 저장공간·메모리 비교

[English](../../verification/unity-live-comparison.md)

[확정 범위 UC-01~05](../plans/unity-live-comparison.md)의 작은 실제 Unity 비교와 [HTML 보고서](../../../reports/unity-live-comparison-20261003.html)를 작성했다. **측정·코드/에셋·격리/재개·B 지속은 확인했고, UC-04의 마지막 snapshot 회수는 Partial**이다. 전체 물리 공간 절감·최소 RAM·대형/OS 지원의 통과로 확대하지 않는다.

위 Partial은 비교 실행 당시의 판정이다. 이후 사용자가 종료된 구형 기록 73개의 보존 이동과 같은 A/B의 release 재시도를 명시적으로 승인했다. [병렬 편집 후속 검증](unity-parallel-editor-validation.md)에서 2,130개 파일의 hash·72개 독립 result refs를 유지한 채 이동하고, 앞선 두 snapshot의 `missing`과 `released`를 확인했다. 자동 이관이나 최초 실패를 없던 일로 해석하지 않으며, 비교 수치와 C worktree 회수 제한은 그대로다.

## 후보·입력·독립 작업

2026-10-03 Windows 11 Home x64, Ryzen 5 5600X, visible RAM 67,020,072 KiB에서 실행했다. 제품 `development` 후보는 `52d9099b2df3bdd786defbabe8e0b04d3ea0cdf3`, Runtime SHA-256은 `b769a1fe899c982416d62f84325021c282313bf7930bad9c1db96b831c9a13ea`다. 제품 코드·schema·서비스/등록·볼륨을 변경하지 않았다.

원본은 기존 등록된 `T:\ua-source`, 기준 `f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`의 94 tracked files/56 Assets files다. 실제 `Target.fbx` 105,120 B와 `Target.png` 5,821,546 B, 재질/씬을 포함한다. Unity `6000.3.22f1`, URP `17.3.0`, 설치된 Unity CLI `1.0.0-beta.5`를 사용했다. 사용자가 승인한 Pipeline `0.8.0-exp.1` trial 기준은 두 Packages 파일만 바꾼 `23896dfacba2d90d0c49f826444b178fdbe82d2b`다.

| 방식 | A | B |
| --- | --- | --- |
| C: 실제 Codex 앱 Git worktree 대화 | `01a10118-d986-7e60-93f4-2a59f0b2cb42`, `.codex/worktrees/1a8c/splinter-unity` | `01a10119-11d9-7960-b631-2c3f2eecc4a1`, `.codex/worktrees/90dd/splinter-unity` |
| S: 제품 NTFS workspace·병렬 AI 작성자 | key `uc-s-a-20261003`, id `40cd86f88ef543d7a8418a6e`, digest `9696acb894812daf3a5e0d65` | key `uc-s-b-20261003`, id `fd4c3247d6b0d38e6c7b5fba`, digest `0c97742488cb4c6f2a6b7671` |

S home은 기존 `.local/wl29`, budget은 각각 17,179,869,184 B다. C는 생성·편집·결과·정상 종료·보관에 Splinter를 전혀 사용하지 않았다. OS 파일/프로세스 계측도 Splinter assembly/API와 독립적이다. C/S 모두 L 원본 + A/B의 세 독립 Editor를 동시에 사용하며 cohort는 순차 실행했다. Coordination/Q는 제외했다. C의 사용자 소유 대화와 S의 현재 대화 subagent 처리 시간을 제품 성능으로 비교하지 않는다.

C 최초 `baseline.txt`의 LF/CRLF 1 B 차이를 원본 bytes로 맞춰 보존했다. 0.8 편집 전 C/S A/B의 94개 파일 bytes/hash는 모두 원본과 같았다. 각 exact-path Editor에서 mesh vertices 1855/815/331, texture 2048×2048을 실제 load했다. C는 Library 없는 기본 worktree, S는 원본 warm Library snapshot이며 S 전에 C 준비·0.8 변경·재컴파일 이력이 있다. 두 cold import의 속도 비교로 해석하지 않는다.

## UC-02~03: 실제 수정·재개·파일과 메모리

`Increment(3)/Double(3)/Calculate(3)`의 기준은 4/6/13이다. L 수정 뒤 4/6/23, A는 5/6/13, B는 4/9/13으로 각각 실행됐다. 원본의 이후 수정은 A/B에 전파되지 않았다. 두 방식의 A를 정상 종료·같은 작업으로 재개한 뒤 A 값과 B의 PID/MVID를 유지했다. C A 종료 뒤 B가 같은 Editor에서 b12로 추가 변경했다. S A 소유 mount/delta 회수 뒤에도 B PID64016·시작 ticks `639266201197725855`는 유지됐고 b9/MVID 확인 뒤 b12와 새 MVID `c5fe00c4-7a14-49ee-bb9a-97f2a316785e`를 실행했다. compile/update/failed=false와 코드 hash를 대조했다. Play/Test/build는 실행하지 않았다.

| 0.8 수정·A 재개 뒤 안정 구간 | C 실제 할당 B | S 실제 할당 B |
| --- | ---: | ---: |
| A 작업 소유 파일 | 2,002,743,336 | 43,085,992 |
| B 작업 소유 파일 | 1,887,255,864 | 36,901,240 |
| A/B 합계 | 3,889,999,200 | 79,987,232 |
| S 공통 runner 증가 | 해당 없음 | 1,155,416 |

C는 source/Library·제어/로컬 증거, S는 private journal/records/logs를 포함한다. C A/B Library는 각각 1,981,165,568/1,868,238,120 B, S 저널 할당은 42,926,080/36,634,624 B다. 추가 runner를 한 번 포함한 S 81,142,648 B와 비교하면 **지정 소유 파일 할당은 97.91% 감소**했다. source common Library 2,068,443,280 B, 기존 C Git 2,391,743,272 B, 공통 evidence와 기존 runners/records는 별도다. VSS는 작업별 귀속 unknown이며 전체 물리 절감률도 unknown이다. 디렉터리/MFT·VHD container 증가·미관측 전체 peak는 이 파일 합계 밖이다.

S 생성 응답의 initialPayloadCopiedBytes/readCacheBytes는 0, 최초 저널/payload는 755/61 B였다. 마지막 A 관측은 약 2.62 GB 읽기와 readCacheBytes=0이며 Library 읽기를 디스크에 hydration하지 않았다. 정상 종료·재컴파일·메타데이터 쓰기는 저널을 추가로 키운다. 절대 최소 자원이나 변경한 스크립트 한 줄 크기만의 저장을 입증한 것은 아니다. `s-created-allocation`은 Editor start 요청 뒤 계측이므로 순수 ensure 비용으로 사용하지 않는다.

| 작업 group 중앙값 | C Private / Working set GiB | S Private / Working set GiB |
| --- | --- | --- |
| L Editor·소유 자식 | 3.86 / 3.25 | 3.74 / 2.07 |
| A Editor·소유 자식 | 3.25 / 2.72 | 3.21 / 2.71 |
| B Editor·소유 자식 | 3.87 / 3.44 | 3.66 / 2.98 |
| S A/B provider 별도 | 해당 없음 | Private 합계 213.20 MiB |
| 같은 시점 전체 작업 합계 | 10.98 / 9.41 | 10.82 / 8.06 |

C 54표본/150.94초, S 24표본/67.76초를 선택했다. 2초 대기 + 수집 overhead의 실제 중앙 간격은 2.85/2.94초다. PID와 정확한 시작 ticks·생성 이후 descendant를 대조하고 다른 등록 root를 parent group에서 제외했다. 선택 표본의 PID 중복은 0이다. root 미관측은 null/unknown으로 유지한다.

독립 Editor 세 개의 Private 차이는 작으며 큰 메모리 절감을 증명하지 않았다. 동일 L PID의 working set도 3.25→2.07 GiB로 줄었다. 시간·cache/Editor 이력·OS 페이지 관리가 다르고 working set 합계에는 공유 페이지 중복이 가능하므로 RAM 절감률로 환산하지 않는다. 전체 시스템 available/committed 중앙값은 C 31.80/59.08 GiB, S 31.73/61.01 GiB이며 다른 앱/기존 Editor·서비스·cache를 포함한다. 최소 RAM과 미관측 전체 peak는 unknown이다.

## UC-04: 결과와 회수 차단

| 작업 | 독립 Git 결과 |
| --- | --- |
| C A | `codex/uc-c-a-20261003` → `40816fb5968da4e79f87b36267055fc272b4b441` |
| C B | `codex/uc-c-b-20261003` → `cb1e869bd642963ae035180d63ce5535efd05160` |
| S A | `refs/splinter/snapshot-results/40cd86f88ef543d7a8418a6e` → `106d51e155e28b3cbd11ddb84fb9607d233455f8` |
| S B | `refs/splinter/snapshot-results/fd4c3247d6b0d38e6c7b5fba` → `8374d1b5dea8dd1e853b64bb9a2c63802dbca9ac` |
| L C / L S | trial 브랜치의 `99b75b399aa08839482453bc9efcc4ca8be2253e` / `551c1418da5065831cf8179accc210db7045a2da` |

C A/B의 앱 archive는 primary checkout 제거 제한으로 거부됐다. source/결과/증거를 유지하고 Editor 부재·정확한 경로·dry-run 확인 뒤 공식 Unity CLI `projects clean --yes`로 Library/Logs만 정리했다. 전후 94 tracked hash는 같다. 잔여 root 할당 21,596,032/19,085,352 B는 source와 작업별 로컬 증거를 포함하며 전체 checkout 회수 성공으로 표시하지 않는다.

S A/B checkpoint는 각각 94.191/97.297초에 성공했고 `stable-journal-v1`의 독립 result/seal을 남겼다. release는 각각 44.095/44.047초 뒤 `StateCorrupt`였다. 소유 Editor/provider/mount/Git 등록/delta는 부재, 제품 process/handles/private/working set 관측은 0이다. records 잔여 할당은 156,104/238,032 B이며 두 snapshot은 **available로 보존**됐다. durable phase는 `releasing`, recovery는 `preserve/StateCorrupt`다. 전체 release 성공이나 VSS 0을 주장하지 않는다.

새 A/B JSON은 현재 Core로 정상 역직렬화됐다. `SnapshotWorkspaces`의 마지막 의존성 gate가 `store.List()`로 같은 home의 기록을 읽을 때 과거 73개 기록에 현재 필수 `sourceKind`/`applicationKind`가 없어 거부됐다. 정확한 JsonException·각 SHA를 읽기 전용으로 남겼고 그 73개 기록은 최종에도 변하지 않았다. 구버전 자동 이관·기존 기록 삭제·외부 snapshot 회수·새 home/grant로 우회하지 않았다. 설치된 서비스와 현재 Runtime binary는 별도 후보이며, 이번 차단은 서비스 삭제 요청 전 Runtime의 전체 기록 조회에서 확인했다. 설치 변경도 하지 않았다.

원본 `codex/library-20260929 @f71e4e2`와 초기 94 raw bytes/hash·clean 상태를 복원했다. canonical fixture `development @6a54974`는 clean이다. 기존 Editor PID68624·자식85184/52688의 CIM 시작 ticks와 사용자 branch/tag/기존 결과 refs는 보존됐다. Codex 내부 `refs/codex/turn-diffs` 4개는 최종 목록에서 없어져 별도 차이로 기록했으며 원인을 확정하지 않았다. 기존 C: 보류 snapshot 요청은 0, 전역 live 검증은 unknown이다.

## 실패와 해석 한계

0.7 L의 `0.0.0.0:7800`과 기존 보호 Editor의 `127.0.0.1:7800` 겹침 및 exact-path catalog 실패를 보존했다. C A/B의 0.7 코드/에셋은 성공했다. 사용자 승인 뒤 시험 대상만 0.8로 바꾸었으며 manifest 변경·resolve/reload·정상 재시작을 구분했다. port 겹침으로 내부 실패 원인을 확정하거나 모든 0.7 동작을 실패로 일반화하지 않는다.

C B의 stale descriptor 401과 S B reload 뒤 401은 새 exact-path catalog로 재연결했다. S A/B 최초/read·reload 뒤/read·A 재개 첫 read의 서버 5초 timeout은 같은 읽기 한 번 재시도로 성공했다. 쓰기/compile은 timeout만으로 반복하지 않았다. S A stop의 후속 Git 관측에는 계측자가 실행별 XDG trust를 빠뜨렸고, Editor가 이미 정상 종료된 상태에서 실패했다. 기존 process-local 설정으로 같은 작업을 재조정한 비용 49.349+8.675초와 실제 재개·첫 read 실패를 모두 보존했다. 전역 safe.directory나 관리자 CLI로 우회하지 않았다.

처음부터 모든 process 준비 순간을 수집하지 못해 true peak는 unknown이다. 전체 Medieval 입력/이전 PV S4 실패·전체 raw unknown, 대형/ARM/원격, Play/Test/build/capture, 전원/재부팅/native 전체 장애, 일반 설치/binding은 NotRun을 유지한다. 새 A/B나 전체 행렬을 자동 재실행하지 않는다.

## UC-05: 보고서와 증거

`artifacts/unity-live-comparison-20261003/`에 응답·cost·94 파일 hash·OS allocation/JSONL·독립 결과·실패 진단을 보존했다. HTML은 `reviewed.json`의 검토한 표와 문장을 포함하고, 공개 범위에 맞는 28개 JSON 근거를 연결한다. 인증 인자가 들어갈 수 있는 원시 Editor 로그나 전체 명령행은 보고서에 싣지 않는다.

필수 build의 현재 후보 결과는 기존 locked restore/Release·906 passed/0 failed/1 skip이다. 이번 실행은 제품 코드를 바꾸지 않은 실제 Unity 검증이며 Unity Play/Test/build 통과로 해석하지 않는다. UTF-8·HTML source links/계산·실제 browser의 표/그래프와 disclosure 확인 결과는 `report-validation.json`에 별도로 남긴다. 작은 화면의 CSS breakpoint는 source 검사이며 별도 device viewport 실행 자격은 NotRun이다.
