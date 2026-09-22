# Live VSS 식별·재연결 검증

기준일: 2026-09-22. [LV 계획](../plans/ntfs-live-vss-identity.md)의 실행 기록이다. NR 전체, 실제 재부팅·전원 종료·공간 고갈·ARM A/B의 통과를 의미하지 않는다.

## 후보와 자동 검증

제품 `development`의 `591d53fe5dc66e632eb92cdb8e108df7497b0fb6`에 작업 트리 변경을 적용했다. [source manifest](../../artifacts/ntfs-recovery/lv-20260922/source-manifest.json)의 124개 파일 aggregate는 `73f590dfc7d9676fc897daeefe00d1dd81da52f08d5c3fe5873905e54774b11a`다. 실행 증거는 이 커밋 전 작업 트리 후보의 파일 hash에 연결되며, 제품 push·배포는 검증 범위에 포함하지 않는다. fixture의 독립 checkpoint 커밋은 아래 결과에 포함된다.

`pwsh ./scripts/build.ps1`의 locked restore와 Release build는 경고·오류 0, [최종 TRX](../../artifacts/ntfs-recovery/lv-20260922/final-build.trx)는 **181개 중 180 통과·0 실패·선택적 ReFS 1 skip**이다. 새로운 dependency나 lock 변경은 없다. 초기 시험의 기존 기대값·capabilities schema 누락·시험의 열린 저널 읽기 공유 문제는 수정 후 전체 재실행했다. 이전 실패 결과는 별도 artifact에 보존했다.

| ID | 확인 범위 | 상태 |
| --- | --- | --- |
| LV-01 | 캡처/live identity, boot·owner·요청 세대 대조와 접근 DTO/schema; 실제 helper 값 | Passed |
| LV-02 | 다른 ID/owner/provider/volume/시각/flags/device와 상대 경로 탈출 거부 | 자동 Passed |
| LV-03 | 검증된 새 읽기 root를 선택해 실제 JournalDeltaStore를 재개, 부분 쓰기 hash·sequence·identity/저널 바이트 보존 | fake/실제 저널 Passed |
| LV-04 | 이전 boot·오래된 요청·실패/불완전 조회·경로 접근 불가·정확한 ID 부재·손상 기록 구분 | 자동 Passed |
| LV-05 | legacy 별도 재검증·입증 불가 보존, version 2 필수 정보, 동일 key 동시 ensure, `.new`/요청 `.tmp` 보존 | 자동 Passed |
| LV-06 | 실제 제품 workspace의 범위 쓰기·flush·provider 종료·관리자 Observe·비관리자 동일 key 재연결 | Passed |
| LV-07 | binary 독립 결과 보존·제품 release·최종 소유 VSS 부재; Unity checkpoint/회수 | Passed |
| LV-08 | 동일 후보의 Windows x64 / Unity 6000.3.22f1 단일 workspace, 검증 3건·재연결·결과·회수 | Passed |

LV-03은 Windows가 실제 device 번호를 재배정하거나 OS가 재부팅된 시험이 아니다. LV-02/04/05의 장애는 결정적 입력·파일 I/O 시험이며 실제 전원·볼륨 장애로 집계하지 않는다. 새 후보의 ARM Windows 실행은 NotRun이다.

## 실제 Git·VSS 재연결

[실행 증거](../../artifacts/ntfs-recovery/lv-20260922/evidence/)와 재현 스크립트는 `artifacts/ntfs-recovery/lv-20260922/`에 있다. 짧은 home은 `C:\slv260922`다. Windows x64, 기존 NTFS/WinFsp를 사용하고 볼륨·VSS 설정이나 설치를 변경하지 않았다.

작은 독립 Git fixture의 workspace는 `934420476b7f584ac4266967`, snapshot은 `{FB351A2F-B273-45FC-9DAD-BF7E4AA7C770}`이다. 비관리자 ensure → 정확한 Capture helper → 동일 ensure → 8MiB 파일 반복 읽기 → 원본 `source.txt` 변경 → 4KiB 쓰기/flush → 소유 provider 종료 → 동일 ensure의 Observe 요청 → 정확한 helper → 동일 ensure 순서로 실행했다. provider 종료는 별도 개발용 `snapshot-spike unmount`가 지정 어댑터에 위임한 시험 경계이며 workspace 제품 기능으로 새로 집계하지 않는다.

측정은 최초 payload 복사 0, 읽기 cache 0, 반복 읽기의 저널/NTFS file-data 할당 증가 0, 4KiB 쓰기의 payload 증가 4096바이트·저널 증가 4508바이트다. 초기 Git pointer의 저널 payload 143바이트는 원본 복제량이 아니다. 재연결 후 같은 snapshot/delta identity, 전체 파일 SHA-256, durable sequence 3을 확인했다. 원본은 새 내용을 유지하고 작업공간은 캡처 당시 `captured`를 읽었다. Observe 전후 VSS ID 목록은 동일했다.

승인 쓰기 결과는 외부 `binary-acknowledged-output.bin`과 hash로 보존했다. 소유 시험 쓰기만 원상 복원한 뒤 미변경 기준의 독립 Git 결과를 남기고 제품 release를 수행했다. 이 cleanup에 Unity validation을 부여하지 않았다. 최종 응답은 released, 소유 프로세스·핸들·메모리 0, mount·Git 등록·delta·정확한 소유 snapshot 부재다. 별도의 원본 변경은 보존했다.

## 단일 Unity 회귀

fixture는 원래 clean `development`, revision `46e7f4dbdb219e4afa602f3598a3de9ff4070fae`의 77개 추적 파일이다. workspace `bc49f980a40cc853e5ad6638`과 snapshot `{7DD62742-5422-477B-9745-15574A74AF72}`를 사용한다. 원본의 saved/idle 상태와 coordination을 확인한 후 캡처했다.

준비 중 사용자가 원본 Editor를 종료 후 다시 열었다. 사용자 요청 동안 추가 Unity 조작을 보류했고, 새 MCP 세션과 canonical PID 67176을 확인한 뒤 같은 캡처/작업을 계속 사용했다. 이를 OS 재부팅이나 제품의 canonical Editor 재시작 시험으로 집계하지 않는다. 이전 coordination 세션은 새 Editor에서 비활성으로 확인했고 새 작업 세션을 등록했다.

기본 상태는 주석만 추가해 실제 컴파일을 유도한 뒤 값 13과 현재 fingerprint의 validation을 확인했다. 같은 파일의 `+10`을 `+17`로 바꾼 뒤 값 20과 두 번째 validation을 확인했다. 제품 WorkspaceBridge는 기존 기능으로 실제 로드된 assemblyMvid도 보고한다. fixture 자체의 별도 신호와 구분하며 계측 코드는 변경하지 않았다.

첫 `unity stop`은 `EditorStopTimeout`을 반환했지만 이후 status에서 정상 종료와 소유 프로세스·메모리 0을 확인했다. 강제 종료하지 않았으며 지정 어댑터로 provider를 종료한 뒤 저널 identity·전체 hash·sequence를 보존했다. provider에는 기존 Unity의 rename 시도 18건이 `Rename destination exists`로 기록됐고 컴파일 검증 오류는 0이다. 이를 일반 Unity 파일 API 전체 지원으로 확대하지 않는다. checkpoint의 앱 종료도 한 차례 같은 timeout을 반환했지만 이후 소유 자원 0을 확인하고 동일 checkpoint 재시도로 완료했다. 두 timeout과 후속 종료/재개 증거를 보존하며 종료 지연의 일반 SLA 통과를 주장하지 않는다.


재연결 전후 동일 snapshot/delta identity·전체 저널 SHA-256·source fingerprint를 확인했다. 새 Observe는 요청 세대만 갱신하고 VSS ID 목록을 유지했다. 새 작업 Editor PID 62216에서 기존 PID 62456과 같은 값 20·assemblyMvid, 다른 domain generation을 확인했다. 재연결 검증용 주석을 추가해 세 번째 실제 컴파일과 현재 fingerprint의 validation도 통과했다.

| 검증 | 값 | PID | fingerprint 앞 12자리 |
| --- | --- | --- | --- |
| 기본 컴파일 | 13 | 62456 | `1220d654cd77` |
| 수정 컴파일 | 20 | 62456 | `25f145d77937` |
| 재연결 후 컴파일 | 20 | 62216 | `bcff3a1f5a5d` |

최종 독립 결과 revision은 `1d7e848fc986aaeb964f97da14b64617fc4b3ca1`, ref는 `refs/splinter/snapshot-results/bc49f980a40cc853e5ad6638`이다. 변경은 workspace의 `Assets/SplinterSnapshotProbe.cs` 한 파일뿐이며 원본 브랜치에 통합하지 않았다. 제품 release는 브리지 제거 후 최종 seal을 sequence 59211·213,808,189바이트·SHA-256 `257a4355561878910890baca58028bdc745f733d08aa5e3a73b12985bb1695e6`로 확인하고 회수했다. 이 누적 저널 수치는 회수 후 남은 공간이나 최초 복제량이 아니다.

[최종 result.json](../../artifacts/ntfs-recovery/lv-20260922/result.json)은 결과 ref/revision/tree·manifest·seal, 두 workspace의 released와 소유 앱/provider·핸들·메모리 0, mount·Git 등록·delta·소유 VSS 부재를 검증한다. 원본 HEAD·clean 상태·77개 추적 파일 hash와 기존 VSS snapshot 3개는 유지됐다. 사용자 재시작 후 canonical PID 67176과 MCP는 최종 Ready/idle, Console 오류·경고 0으로 확인했고 coordination 작업 세션을 종료했다. 제어 영수증·독립 결과·원시 증거·짧은 home의 재현 기록은 보존한다.

**LV 단위는 정의한 시험 수준에서 완료다.** NR 전체, 실제 OS 재부팅/device 경로 재배정·전원/물리 공간 장애·ARM A/B·일반 에셋/Play/Test Framework의 자격은 포함하지 않는다. 다음 작업은 상위 NR 계획의 나머지 중단·파일 경계부터 진행한다.
