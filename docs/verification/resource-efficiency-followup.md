# 부분 private 쓰기·작은 버퍼·Editor 시작

실행일: 2026-10-02. 범위는 **private 덮어쓰기의 일치 앞뒤 재사용, 요청 크기 버퍼, 시작과 현재 소스 검증의 분리**다. 현재 저널 형식·schema·공통 port·버전·설치 서비스는 유지한다. 전체 Library/Editor RAM·대형·cold·원격·OS 장애 지원으로 확대하지 않는다. 소유 계약은 [NTFS](../ntfs-workspaces.md#동일한-private-범위의-반복-쓰기)와 [소스 검증](../source-verification.md)이다.

원시 evidence는 `artifacts/storage-foundation/resource-followup-20261002-020617-014/`다. `plan.json`, 고정 전후 CLI/probe 묶음·소스, `before-fixed/result.json`·`after-fixed/result.json`·`comparison.json`, 빌드 로그/TRX와 별도 `unity-plan.json`·CLI 응답/monitor를 보존한다. probe와 이번 실행 어댑터는 고정 개발 입력이며 새 제품 명령/일반 harness가 아니다.

## 고정 입력의 공간과 대여 버퍼

동일한 불변 fake 기준에 `Library/Bee/cache` 64KiB를 최초 기록하고 중앙 8바이트를 바꾸는 전체 요청을 16번 썼다. 기존/현재 Runtime을 별도 프로세스·새 소유 디렉터리에서 실행했다. 초기 신규 쓰기는 전체 payload이며 이후 전체 private 범위의 일치 앞뒤만 재사용한다. 기존 `write` frame 하나·수정 시각·경로 sequence·개별 durable flush를 유지한다. snapshot payload를 읽으면 probe가 실패한다.

| 관측 B | 변경 전 | 변경 후 |
| --- | ---: | ---: |
| 기록한 payload | 1,114,112 | 65,664 |
| 저널 논리 길이 | 1,122,136 | 73,752 |
| NTFS 저널 파일 할당 | 1,122,304 | 77,824 |
| durable sequence / flush 수 | 20 / 20 | 20 / 20 |
| 4KiB Read callback의 실제 대여 버퍼 | 1,048,576 | 4,096 |
| 같은 callback의 cold thread 관리 할당 | 1,050,048 | 5,568 |

파일 할당은 `FILE_STANDARD_INFO.AllocationSize`다. 이 입력의 감소는 1,044,480B, 약 93.1%이며 두 실행의 최종 내용 hash·재생 결과는 같다. 처음 생성/길이 확장/모든 바이트 변경·base/hole은 최적화하지 않는다. Library의 동일 앞뒤 비중·전체 절감률은 미측정이다. 전체 물리 peak·MFT/디렉터리·VSS/Git/증거 비용은 표에 포함하지 않는다.

메모리 표본은 1MiB 최대 전송 설정의 같은 WinFsp Read callback을 새 관리 thread에서 호출한 결과다. 실제 ArrayPool 대여 이벤트와 현재 thread 누적 관리 할당을 관측했다. callback 요청이 작을 때 대여 크기를 줄인 근거이며 pool 잔류·provider 상주 메모리·Editor 또는 전체 시스템 RAM 절감률이 아니다. 큰 요청의 chunk/승인 경계와 성공/실패 뒤 반환은 유지한다.

두 실행은 별도로 실제 로컬 WinFsp mount에서 64KiB 파일 생성·1바이트 변경의 전체 덮어쓰기·flush·읽기·정상 unmount 뒤 재생을 확인했다. 불변 기준은 fake이며 VSS/Git/Unity/강제 종료/전원 검증을 대신하지 않는다. 자동 회귀는 private extent/64KiB 경계·첫/끝 변경·중단 frame/flush 뒤 재생과 0/1/4KiB/16KiB/128KiB callback·constrained EOF를 다룬다.

## 시작 응답과 현재 검증

`unity start`와 `ensure --editor`의 시작 후 View는 일반 status의 실행 관측을 사용한다. 새 pending validation 응답이 없다면 소스를 검사하지 않으며 `not-checked`다. 새 검증 결과 조정·Editor 없는 ensure·refresh·명시적 현재/감사 검사·checkpoint/integrate·수집 전후/unmount 뒤 seal은 유지한다. import 중의 소스 검사 실패를 일반적인 자동 retry로 숨기거나 검증 성공으로 바꾸지 않는다.

공통 fake 회귀는 시작 후 소스 검사가 `SourceChanged`를 반환할 상황의 실행 접수·새 coordinator의 같은 작업/Editor 재사용, refresh의 기존 거부, 새 pending 검증의 바뀐 소스 거부와 checkpoint 차단을 확인했다. 이전 [Unity 비교](unity-duplicate-private-writes.md)의 정확한 OS/파일 변경 원인을 확정하는 근거는 아니다.

현재 Unity 실행은 등록된 `T:/ua-source`, `.local/wl29`, `6000.3.22f1`의 새 소유 작업 `3cb811fcc192df22638cabbb` 하나다. 최초 start와 정상 stop 뒤 restart는 각각 약 2.05초/2.88초의 `Accepted`·`not-checked`였고, 준비는 별도로 기다렸다. 실제 loaded 값 13→113→114·각 Editor PID/프로젝트/MVID·현재 소스 검증/컴파일 성공을 확인했다. 같은 ensure는 ID/digest/snapshot/provider를 유지했고 정상 재시작은 Editor PID만 바뀌었다.

첫 checkpoint와 release는 성공했다. 결과 `0fad9415052bc4233af2a1abb3cfaf5c6bc535c6`과 스크립트 blob `42fdea2ed8529f4352d918c9ddc0824206ab0147`은 회수 뒤에도 독립적으로 남는다. 실제 mount/Git 등록/delta·정확한 snapshot은 부재/회수, 소유 프로세스/메모리/핸들은 0이다. 원본 94개 추적 파일·HEAD/clean·기존 workspace 기록 72개·기존 refs·원본 Editor/process identity 3개를 보존했다. CLI 33회는 실패 0·비승격이고 추가 consent 표본은 0이다. 원본 통합·설치/grant/볼륨·원격·새 장애 행렬을 포함하지 않았다.

종료 직전 소유 저널 파일 할당은 201,719,808B다. 제품 checkpoint의 정확한 `stable-journal-v1` seal과 checksum을 대조한 장부에서 유효 private 데이터 115,933,762B 중 Library는 106,388,937B다. 이는 위 합성 입력의 93.1%를 실제 Library 전체 절감률로 승계하지 못하는 근거다. 이전 Unity 비교와는 작업 경로·snapshot·생성 내용/callback 열이 다르며 이번 단독 실행은 전체 저장량 개선의 전후 대조가 아니다. 공유 VSS 귀속·전체 물리 peak·Editor/시스템 RAM 절감은 unknown/미판정이다. 실제 시작/재시작의 불필요한 검사 실패는 이 실행에서 관측하지 않았지만 모든 재시도 제거·OS 원인 확정을 주장하지 않는다.

## 자동 회귀와 적용 한계

최종 필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 경고/오류 0, **777개 중 776 통과·실패 0·당시 제외 시험 1 skip**이다. `build-final.log`와 `automated.trx`를 보존한다. 최초 전체 시험의 정리 비율 실패도 `build.log`에 남긴다. 해당 시험은 전체 덮어쓰기를 의도했지만 첫 바이트만 바꾸고 있어 새 재사용이 정리 전 비용을 줄였다. 전체 바이트 변경 입력으로 수정했으며 기존 정리 비율·내용/회수 판정을 약화하지 않았다.

현재 고정 probe와 실제 제품 provider의 Runtime/Windows hash는 최종 필수 빌드와 일치한다. `source-identity.json`은 `development @ e7b1c49` 위 미커밋 구현 3개의 hash를 고정한다. 재빌드/새 후보의 native 전체 통과를 주장하지 않으며 입력·바이너리·실행 경계별로 판정한다. **이 세 개선 단위는 scoped-complete-with-limitations로 마감한다.** 남은 새/전체 변경 Library 부담, 실제 provider/Editor RAM, SourceChanged/늦은 수집 쓰기의 다른 경계는 별도 후속 판단이다.
