# 선택적 기준본 복귀 정리 검증

2026-10-06, Windows/NTFS, `development @ d97bd3c` 이후의 이번 미커밋 후보. [고정 범위와 판정 기준](../plans/journal-base-reversion.md), [현재 계약](../journal-base-reversion.md)을 따른다. **기본값은 OFF**다. ON은 정리 후 공간을 줄였지만 실제 입력의 전체 측정 시간과 관리 할당을 늘렸다. 자동 정리·append 비용 감소·Unity 준비 개선을 주장하지 않는다.

증거 root는 `artifacts/journal-base-reversion-20261006/`다. ignored 로컬 증거를 보존했으며 다른 후보의 통과를 승계하지 않았다.

## 입력과 비교 방법

- 실제 원본: 형제 `splinter-unity`, clean `development @ b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`. tracked 1,602개 / 2,804,769,928B, Assets 1,567개 / 2,804,669,568B, 기존 Library 42,420개 / 3,141,660,252B (`input.json`). Medieval 실제 PNG 3개(1,155,235 / 1,177,669 / 1,226,681B)의 4KiB·1MiB·1MiB 범위를 선택했다. 마지막 범위의 절반은 다른 bytes로 남겼다.
- Git fixed raw blob reader·현재 SJR2·동일 codec/한도, 각 32회 B→C→base. 합성은 seed 61006의 random/repeated bytes 및 분산 8범위다. OFF/ON 각각 독립 디렉터리, 3회씩 OFF/ON→ON/OFF→OFF/ON 순서. 전체 파일 bytes·entry·변경 sequence와 정리 재생을 확인했다.
- 실제 반복 비교는 소유 제품의 recorded Git 기준 reader와 **실제 NTFS 저널 파일**을 사용한 offline 정리/재생이다. 정리와 재생마다 새 reader를 열어 seed 단계의 blob proof를 재사용하지 않았다. 제품과 같이 snapshot health를 한 번 확인하고 그 관측을 사용한다. WinFsp callback/재시작의 반복 성능 비교와 Unity cold import는 NotRun이다. 별도 제품 A/B는 실제 WinFsp 수명주기의 보존·공존·회수를 확인한다.
- OS cache를 비우지 않았다. 최초/후속 cache 차이와 다른 증거 수집의 영향이 남는다. 표의 시간은 각 독립 실행의 쓰기+정리+새 reader 재생 합계의 중앙값이며 source 준비·전체 CLI 연결/checkpoint/release·Unity 준비 시간은 포함하지 않는다. 빠른 재생만으로 전체 작업 개선이라고 읽지 않는다.
- 초기 reader가 매 read마다 live health를 재확인한 실제 trial은 방법 오류로 제외했다. 정확한 소유 검증 자식만 종료했고 원래 디렉터리·`excluded-native-trial.json`을 보존했다. `native-offline-qualified`만 최종 수치에 사용한다. 초기 64KiB 개선 전 합성 `benchmark`도 보존하되 최종 수치는 `benchmark-final`이다.

## OFF/ON 결과

최종 저널 NTFS 할당은 각 3회 모두 같았다. 시간은 ms, 쓰기+정리+재생 합계다. 아래 절감은 **선택 저널**의 정리 후 할당이며 전체 2.8GB 프로젝트 또는 VSS 절감률이 아니다 (`comparison.json`).

| 입력 | OFF 할당 B | ON 할당 B | 할당 감소 | OFF 전체 ms | ON 전체 ms | 전체 변화 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| random 4KiB 복귀 | 8,192 | 4,096 | 50.00% | 246.56 | 233.47 | -5.31% |
| random 1MiB 복귀 | 1,052,672 | 4,096 | 99.61% | 931.30 | 906.09 | -2.71% |
| repeated 1MiB 복귀 | 4,096 | 4,096 | 0% | 445.42 | 441.65 | -0.85% |
| mixed 분산 8×4KiB | 131,072 | 110,592 | 15.63% | 1,747.15 | 1,749.51 | +0.14% |
| 실제 PNG 3개 | 2,109,440 | 532,480 | 74.76% | 4,712.48 | 7,040.90 | **+49.41%** |

실제 입력 OFF 전체 4,651.46–4,820.60ms, ON 7,036.59–7,250.65ms다. 정리 중앙값 2,266.65→4,647.39ms, 재생 1,146.07→1,185.62ms, 쓰기 1,322.89→1,310.05ms. 실제 입력의 비용 증가가 반복 변동보다 크다. 기준 비교가 새로운 fixed blob 읽기/검증을 필요로 하며 순수 payload 절감만으로 이 비용을 상쇄하지 못했다.

실제 입력의 정리 관리 누적 할당 중앙값은 11,342,824→20,441,432B였다. 이는 누적 managed allocation이며 resident RAM/전체 순간 peak가 아니다. 정리 전 저널 할당은 두 선택 모두 202,403,840B다. old+new 할당 표본은 OFF 204,513,280B / ON 202,936,320B로, 정리 전 누적의 대부분은 남아 있고 ON이 정리 전 peak를 없애지 않는다.

최종 payload/frame 구분 (`native-off-frame-ledger.json`, `native-on-frame-ledger.json`):

| 항목 | OFF B | ON B |
| --- | ---: | ---: |
| 최종 저널 논리 | 2,106,633 | 529,092 |
| live private / stored payload | 2,101,248 | 524,288 |
| metadata·frame·checksum | 5,385 | 4,804 |
| obsolete stored payload | 0 | 0 |
| 기준 reader 반환 비교 bytes | 0 | 2,101,248 |
| 제거한 논리 private bytes | 0 | 1,576,960 |

반복 bytes 1MiB는 OFF에서도 238B payload로 압축돼 NTFS 할당 개선이 없다. 사전 조건인 반복 입력 개선과 실제 전체 시간 10% 초과 회귀 없음에 미달하므로 ON을 기본으로 채택하지 않는다. 공간이 필요한 작업에서 명시적 옵션으로 사용한다. 기준 반환 bytes는 Git 내부 해독·OS 디스크 I/O 총량이 아니다.

## 보존과 중단 복구

| 항목 | 자격 | 증거 |
| --- | --- | --- |
| 원본/기대 hash·같은 bytes·entry/ID/rename/시간/속성·truncate/extend·삭제/재생성·기준 부재 | Passed, 자동/fake 한정 | `JournalBaseReversionTests`, coordinator/schema/fake 시험 |
| 실제 자식 강제 종료: append-flushed, intent, candidate-flushed, prepared, published, before-previous-delete, previous-deleted | 7/7 Passed, 합성 불변 기준·실제 NTFS | `process/result.json` |
| 같은 7개 경계, 실제 large recorded Git 기준 | 7/7 Passed, offline 정리·실제 NTFS | `native-process/result.json` |
| 같은 옵션/identity·외부 ACK·seal/bytes/metadata·이전 세대 보존/회수 | Passed, 위 고정 경계 | stage별 PID/시작 시각/종료·ACK·receipt |
| 실제 WinFsp 제품 A/B·독립 결과·소유 회수 | Passed, 최초 A 실패·명시적 재개 이력 포함, editorless 한정 | `product/result.json`, 아래 이력 |

각 강제 종료는 정확한 자기 자식 nonce/PID/시작 시각을 검증해 수행했다. ACK 전에 완전히 flush된 쓰기가 재생에서 살아 있는 기존 계약을 확인했으며 ACK 전 쓰기를 반드시 버린다고 주장하지 않는다. 일반 provider에는 개발 observer를 연결하지 않았다. live WinFsp provider kill·전원/OS 장애·전체 NR 행렬로 승계하지 않는다.

## 제품 A/B 이력

소유 home `.local/jr1006/home`, A `461062084c0a7b6c1b482b58` / key `jr1006-native-a`, B `3566d48668b4feecdfc239b9` / key `jr1006-native-b`. `--source git --application none --storage git-snapshot`, 동일 고정 base다. 원본 Library 전환/Unity Editor 실행·import를 하지 않았다. 기록 기준 준비 시 raw bytes 요건 때문에 로컬 `core.autocrlf=input`을 두 ensure 동안만 적용했고 try/finally로 원래 local unset을 복원했다. global 설정은 유지했다. 초기 거부 기록도 보존했다. 준비의 returned base reads 약 8.65/8.76GB는 전체 복제·disk hydration 양이 아니다.

A ON 첫 명령은 새 저널을 검증·게시한 뒤 runner 임시 디렉터리의 `Directory.Move` 접근 거부로 `InputUnavailable`, `compact-connect`에서 멈췄다. 새/이전 저널과 runner 8개 파일을 보존했고 실패 조사 시점의 빌드와 hash가 일치했다. ACL/속성/서비스/설치 변경이나 임시 디렉터리 수동 이동으로 우회하지 않았다. 자동 승인 심사는 첫 재시도를 거절했으며 사용자에게 근거를 알리고 **같은 소유 작업의 재시도·마무리**를 명시적으로 승인받았다. 이후 옵션을 생략한 공식 `workspace compact`가 저장된 ON과 같은 ID/home으로 성공했다 (`a-reconnect-approved.json`). 최초 실패를 통과로 바꾸거나 접근 거부 원인이 해결됐다고 단정하지 않는다. 첫 helper의 in-process 호출과 공식 standalone CLI 재개를 구별하며 후속 개발용 helper는 standalone CLI를 호출하도록 수정했다. 이 helper 수정의 새 native 실행을 주장하지 않는다.

B의 64×128KiB 새 파일은 모두 durable flush 후 읽기/hash를 확인했고 보존된 marker 64개가 일치한다. A 명령 중 동시 완료는 전체 compact 명령의 stop/준비 구간을 포함하는 자격이며 payload 비교 단계와의 겹침을 별도 주장하지 않는다. B OFF 정리에는 이 추가 8MiB가 있으므로 이 한 제품 A/B의 최종 할당 차이를 matched 성능 비교로 쓰지 않는다.

최종 `product/result.json`은 A 최초 compact 명령 중 marker 완료 63개, 전체 64개 보존을 기록한다. UTC와 monotonic 시간의 anchor·명령 시작/끝을 보존했다. A 재개와 B OFF 정리 뒤 세 에셋의 전체 bytes/수정 시각이 일치했고 같은 B ensure가 동일 ID를 반환했다. A checkpoint/release 뒤에도 B는 mount/provider 소유권·핸들 0을 유지해 에셋/64개 파일을 다시 읽었다. B checkpoint/release까지 성공했으며 **두 phase 모두 released**다. 두 결과의 독립 Git 리비전에서 에셋 bytes와 B marker 64개를 재검증했다. 결과는 A `80ae31c2bede6d7b4e308d3fd765583967c28ef6`, B `3f11fc4dde1d1e4af21c32f3f67689b16f19ad04`이며 각각 `refs/splinter/snapshot-results/<id>`와 소유 결과 branch에 보존한다. 원본에 통합하지 않았다.

`original-and-ownership-after.json`: 원본 development/HEAD·clean 상태와 실제 원본 PNG bytes 유지, local autocrlf unset 복원, 기존 **비소유 refs 88개**와 원본 worktree 등록 유지. 두 소유 Git basis ref·mount·delta·provider·핸들·프로세스/메모리는 0/부재로 확인했고 반환 snapshot reclamation은 released다. 원본 Unity Editor는 전후 0개였다. 원본 파일·다른 작업·설치/서비스/ACL을 변경하지 않았다.

checkpoint의 실제 전체 명령 시간 A 272.49s / B 272.92s, release A 91.41s / B 89.58s는 단일 관측이다. 추가 파일·동작 순서가 다른 한 쌍이므로 OFF/ON 인과 비교가 아니다. 이 소스 수집/소유 회수 비용을 앞의 offline 쓰기+정리+재생 수치에 포함한 것으로 읽지 않는다.

`storage-before-after.json`의 실제 회수 후 표본: 소유 기록 A 391,720B / B 395,816B, delta 세대는 부재다. 공유 runner 할당은 1,544,536→4,633,608B로 늘었고 첫 실패의 임시 bundle도 보존했다. shared Git(common/results) 할당은 2,392,353,848→2,393,277,504B다. 기록·runner·독립 결과의 보존 비용을 최종 저널 절감에서 누락해 전체 공간 개선이라고 주장하지 않는다. 이 공유 root의 전체 값을 A/B에 나누거나 원본 baseline과 함께 중복 합산하지 않는다. 시스템 전후 표본은 별도 `system-before-peer.json` / `system-after-peer.json`이며 shared OS 변동을 옵션의 RAM 효과로 읽지 않는다.

## 한계와 확인

전체 물리 순간 peak·VSS workspace 귀속·전체 RAM 순간 peak·native codec heap은 unknown이다. Git 기준 경로는 VSS를 사용하지 않는다. provider/시스템의 몇 시점 관측은 상주/공유 값이며 peak·workspace 귀속 또는 ON/OFF 인과 효과가 아니다. Unity 실제 compile/Play/Test/build·cold import, large 원본 Editor 공존·다른 OS/ARM·새 VSS 장애는 NotRun이다. checkpoint는 안정된 소스를 독립 보존하고 게임 검증 통과를 뜻하지 않는다.

필수 `pwsh ./scripts/build.ps1`: locked restore, build 오류/경고 0, 총 1,082 / Passed 1,073 / Failed 0 / Skipped 9 (`build-final.log`, `full-final.trx`). 이후 개발용 helper·구형 intent 거부와 응답 schema 시험을 추가/수정하고 test 프로젝트 재컴파일 오류/경고 0, 마지막 선택 시험 11/11 Passed를 확인했다 (`test-final-build.log`, `final-selected.log`). native 종료 뒤 최종 CLI 사용법 메타데이터를 포함한 솔루션 빌드 오류/경고 0 (`build-metadata-final.log`), 두 release 응답까지 현재 schema와 CLI 옵션 발견 2/2 Passed (`final-responses.log`)다. native 쓰기 환경 변수를 제거하고 저장된 응답만 읽었으므로 native A/B를 재실행한 것이 아니다. 명시적으로 실행한 비교/강제 종료 시험의 수치는 별도 증거로 분리한다. 컴파일 중 소유 CLI가 DLL을 사용해 실패한 앞선 build도 보존하고 종료 후의 성공만 최종으로 채택했다.

이번 고정 단위는 **scoped-complete-with-limitations**로 마감한다. 기본 OFF와 최초 실패/공식 재개 이력·미실행/unknown 범위를 유지한다. 새로운 실사용 부족/보존 결함 또는 명시적 입력/지원 확대 요청 없이 실제 A/B·Unity import·전체 장애 행렬을 자동 반복하지 않는다.

최종 솔루션 빌드 뒤 native 조사 시점의 runner 8개 파일과 hash가 모두 일치했다 (`qualified-final-binary-comparison.json`). Runtime/Core/Windows/Git reader를 이전 바이너리의 통과로 승계한 것이 아니다. 변경 파일 29개의 UTF-8, 계약 JSON 65개 parse, 새 소유 문서 3개의 상대 링크와 `git diff --check`도 확인했다. 구현 저장소의 변경은 미커밋 상태이며 push/merge/deploy를 하지 않았다.
