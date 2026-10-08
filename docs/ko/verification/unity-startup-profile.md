# 실제 Unity 초기 준비 프로파일

[English](../../verification/unity-startup-profile.md)

**UP-01**은 [JS](journal-startup-optimization.md)·[ST](startup-time-optimization.md) 최적화를 커밋한 `8709ba27574a2de8d0ecaee1a1413f42358d53ec` 후보의 실제 A/B 초기 준비 1회다. 준비 완료는 **766.9024초(12분 46.9초)**로, [UC-01](unity-checkpoint-release-remeasurement.md)의 848.3379초보다 **81.4355초 / 9.60%** 짧았다. 남은 시간의 큰 부분은 C# 컴파일러 계산보다 스캔이다. 정확한 계수·추적 범위·수명주기 근거는 [데이터셋](../../../reports/benchmarks/unity-startup-profile-20261008.json)에 보존한다.

## 입력과 측정 범위

Unity **6000.3.22f1**, 소스 `b09fa58e9d40a560d4fe1af7a213f8ac23e8e3b1`, 추적 파일 1,602개 / 2,804,772,822바이트다. 독립 Editor 두 개가 `git-snapshot`과 원본 Library 참조를 사용한다. **Library 항목 44,860개**의 내용 해시·경로·크기·속성·시각은 UC와 모두 같다. manifest 해시의 차이는 snapshot 소유권·경로·생성 시각 필드뿐이다. 일반 Release Runtime SHA256은 `3661d876c091b774965ecaddcbd9be080ec432378f561ee843647d5519b50537`이며 실행 전에 바이너리를 고정했다.

변경하지 않은 외부 fixture가 반환 경로·소유 PID/시작 시각·최신 noncompiling/nonupdating 상태와 예상 코드 값을 확인한다. Splinter heartbeat를 컴파일 증명으로 사용하지 않는다. 이번에는 초기 준비만 실행했고 소스 편집·별도 refresh 실험·Play·테스트·게임 빌드는 수행하지 않았다. 원본 소스와 설치된 패키지는 그대로다.

Provider 상태는 10초마다, 일반 사용자 Win32 CPU/I/O/private bytes는 2초마다 기록했다. A provider에 초기 스캔·후기 스캔·컴파일 준비의 겹치지 않는 60초 EventPipe 창 세 개를 적용했다. 각 버퍼는 16MiB다. 명령행 메타데이터는 관측한 소유 프로세스의 역할만 구분한다. 관리자/UAC·서비스 변경은 없다. 원시 근거는 ignored `artifacts/unity-startup-profile-20261008/`와 `.local/up8/`에 보존한다.

UC에는 외부 EventPipe 측정이 없었다. 캐시 상태·스케줄링·추적 오버헤드를 무작위 반복으로 통제하지 않았으므로 **한 실행 묶음의 관측 차이**이며, 고립된 인과적 개선율이나 일반 Unity 보장이 아니다. A/B는 독립 작업 두 개이지 벤치마크 반복 표본 두 개가 아니다.

## 초기 준비 시간

| 측정 | UC A / B, 초 | UP A / B, 초 |
| --- | ---: | ---: |
| 두 Editor 최초 준비 완료 | 묶음 848.3379 | 묶음 766.9024 |
| Scan | 448.3302 / 448.1171 | 446.9486 / 447.5970 |
| CompileScripts | 250.8740 / 251.5398 | 189.7586 / 192.4327 |
| ImportInProcess | 17.544 / 16.869 | 11.8075 / 11.7899 |

Unity의 단계들은 중첩되거나 겹치므로 **표의 시간을 합산하지 않는다**. A의 최초 AssetDatabase refresh는 732.187초다. Bee 로그는 최초 확인 **14.58초**, 그래프 생성 **68초**(로그의 반올림 값), backend 실행 **97.26초**로 구분한다. 이전 A의 그래프 생성/backend 실행은 90초/131초였다. CompileScripts에는 그래프 생성·I/O·후처리·대기가 포함되며 순수 C# 컴파일 CPU 시간이 아니다.

## 1. Git 읽기 직렬화가 스캔을 막는다

각 스캔 추적에는 동시 스레드의 파일시스템 callback 체류가 약 480초 누적된다. Monitor 진입 대기는 **419.399초 / 419.811초**, callback 체류의 **87.35% / 87.44%**다. 이 대기의 **98.54% / 99.93%**에서 가장 가까운 제품 프레임이 `GitObjectSnapshotTree.Read`였다. 코드와 대조하면 Git tree 자체의 `readGate`에 해당한다. ST의 독립 Library 읽기는 이 잠금을 제거하지 않는다.

이는 겹치는 **스레드 체류**이며 CPU 사용률이나 제거 가능한 실행시간 87%가 아니다. 대응하는 60초 창에서 provider CPU는 약 12초, 표본에 잡힌 Git 자식 CPU는 약 5초였다. 대응하는 내부 provider 구간 두 개 모두 **쓰기·flush·저널 append가 0**이다. 이 스캔 창의 원인은 저널 직렬화가 아니다.

실제 진행하는 Git 경로의 표본에는 `ReadByte`/헤더 약 30–32초, buffered read 18–21초, 프로세스 시작 3–4초, hash와 reader 회수 각각 2–3초가 포함된다. 헤더 체류에는 Git 응답 대기가 포함될 수 있으므로 헤더 묶음 읽기로 그 시간을 모두 회수한다고 볼 수 없다.

준비 중 provider 읽기 전송은 **69.39GiB / 71.52GiB**였고, 한정 provider 구간의 논리 기준 읽기는 **4.416GiB / 4.349GiB**였다. 전송에는 pipe·device·캐시된 파일 읽기가 포함된다. 물리 디스크 바이트나 노출되지 않은 `DecodedBlobBytes` 계수가 아니다. 코드의 역방향 앞부분 재디코딩과 두 세션의 퇴출 정책은 구체적인 증폭 후보다. 표본의 Git identity 839/832개는 Windows launcher/자식 쌍과 짧은 프로세스 누락을 포함하므로 **decoder 시작 횟수가 아니다**.

## 2. 빌드 준비도 저장 작업을 기다린다

Bee의 세부 A 추적은 **BuildProgram 67.550초** 중 **Backend.Write 66.500초**를 기록한다. 그래프 생성 자식의 CPU는 약 **1초**다. `JsonToString` 3.94초는 내부에 중첩돼 별도 합산하지 않는다. 준비 중 provider CPU 관측 하한은 307.1초/299.4초이며 kernel CPU 217.0초/213.5초를 포함한다. 그래프 계산만 비싼 것이 아니라 저장·시스템 호출 비용이 크다. 컴파일러 서버 하나가 두 작업을 처리할 수 있어 컴파일러 자식 CPU를 Editor별 전체 컴파일 비용으로 읽지 않는다.

컴파일 준비 추적의 callback 체류 192.341초 중 Monitor 진입은 105.190초다. 쓰기가 진행되는 동안 보안·열기·닫기·정리·메타데이터 callback이 대기한다. Monitor를 제외한 leaf 체류에는 **flush 21.874초**, **EnsurePlainPath 16.701초**, **Publish 12.606초**, **ReadLatest 6.133초**, **Brotli 2.913초**가 포함된다. 이는 합산 가능한 wall-clock 단계가 아니다. 반복되는 `PathOf` → `EnsurePlainPath` 조상 확인은 다음 개선의 구체적인 대상이고 Publish에도 여러 내부 연산이 포함된다.

최초 준비의 논리 저널 put은 약 **3.854GiB → 1.596GiB**, A의 쓰기 작업 시간은 **251.1초 → 182.7초**로 줄었다. 이는 한정 live 계수이며 물리 디스크 할당량이 아니다. 남은 flush·소유권 확인·게시 비용 때문에 메타데이터 바이트 감소율만큼 실행시간이 줄지는 않는다.

## 3. 필요한 무효화와 피할 수 있는 작업 구분

과거 재가져온 소스 에셋 27개는 terrain layer 17개·animation 5개·controller 5개이며 CRLF 차이만 있었다. 개별 import 로그 합계는 약 2초다. 원본 C# 7개는 이미 raw Git blob과 같다. 이 27개로 447초 스캔이나 광범위한 스크립트 준비를 설명할 수 없다.

원본/현재 Bee 입력의 차이는 assembly 경로 74개, Editor 소스 `WorkspaceBridge.cs` 하나, private IL 후처리 endpoint다. B의 실제 DAG payload 비교에서 **216개**가 달랐다. 147개는 원본→작업 root 치환만, 68개는 소유 ILPP pipe만, 하나는 Editor 응답 파일에 bridge를 추가한 차이다. 실제 실행 action은 양쪽 모두 WriteText 147개·WriteResponseFile 68개·ILPostProcess 67개·Csc 2개·CopyFiles 4개다. 바뀐 payload 수와 실행 action 수는 서로 다른 계측이다.

원본 Editor의 endpoint를 다른 작업에 재사용해서는 안 된다. 실제 cache 입력이 바뀐 것이지 패키지 소스 assembly 67개가 바뀌었다는 근거는 아니다. action별 Bee "why" 기록은 수집하지 않아 runtime Csc 원인을 Editor bridge 하나로 귀속하지 않는다. Bee 진단은 로컬에 보존하며 외부 패키지·컴파일러·캐시 내부를 수정하지 않는다.

## 후속 우선순위

1. **Git decoder 순서와 재사용.** 정확한 offset·재시작·디코딩 바이트를 먼저 계측하고, 짧은 proof/LRU 보호와 reader별 연산을 분리해 기존 두 reader가 독립 진행하도록 검토한다. 진행 중 proof/payload 예산·같은 object의 직렬화·손상 거부·close/reset 대기를 유지한다. 같은 object 재시작에도 검증된 요청 청크를 유지하면 헤더/역방향 탐색에 도움이 될 수 있지만 최초 전체 검증은 남고 다른 접근 패턴은 불리해질 수 있다. 한정된 길이의 헤더 묶음 비교는 작은 후보이며 효과는 미측정이다.
2. **내용으로 입증한 소스 메타데이터.** raw Git blob·적합한 원본 시각·Library basis를 durable하게 묶으면 일치하는 파일의 불필요한 재확인을 줄일 가능성이 있다. 현재 Git entry 시각은 snapshot 생성 시각이다. 변경 가능한 원본의 시각을 무조건 복사하거나 CRLF가 다른 바이트에 적용하지 않는다. provenance 계약과 오래된/변경 소스 guard가 필요하며 전체 스캔 제거를 약속하지 않는다.
3. **반복 경로 검사와 아무 변경 없는 Cleanup 대기.** 정확한 root/조상 핸들과 해당 generation이 고정된 동안만 소유권·조상 증명을 재사용한다. 현재 `WinFspOverlayFileSystem.Cleanup`은 `CleanupDelete`가 없어 interlocked 핸들 정리만 할 때에도 mutation gate를 잡는다. 실제 삭제일 때만 잡는 방향은 핸들·close·stop 순서 시험을 수반하는 작은 후보다. 관측된 대기 18.556 callback-thread초는 다른 대기와 겹친다. 교체/reparse/identity 거부·실제 삭제/context 직렬화·durable 게시/ACK는 유지한다. 광범위한 잠금·flush 생략은 근거가 없다.
4. **생성 Editor bridge의 assembly 분리.** 자동 참조를 끈 전용 Editor assembly는 bridge 변경으로 인한 predefined assembly 재컴파일을 줄일 가능성이 있다. Unity의 [assembly 참조 문서](https://docs.unity3d.com/6000.3/Documentation/Manual/assembly-definitions-referencing.html)가 해당 동작을 설명한다. 독립 Editor endpoint·경로 변경·Git 스캔·모든 패키지 후처리를 없애지는 않는다. 같은 workspace의 warm 재시작도 별도의 미측정 조건이다.

이 목록은 분석 결과이며 추가 구현이나 절감률 검증이 아니다. 첫 세 방향은 사용자 모드 작업으로 UAC·서비스 변경 없이 개발할 수 있다.

## 종료 확인

| 수명주기 단계 | 초 |
| --- | ---: |
| 원본 Library 준비 | 155.6209 |
| A / B workspace ensure, 순차 | 237.1078 / 233.1627 |
| 두 Editor 동시 초기 준비 | 766.9024 |
| 같은 정상 Editor 종료 | 226.6012 |
| A checkpoint / release | 59.9193 / 103.8614 |
| B checkpoint / release | 58.7054 / 100.1621 |
| 원본 Library 복원 | 113.9665 |
| 준비 시작부터 복원 완료 | 2,057.5467 |

즉 새 Library/작업공간 준비는 Editor 타이머 전에 **625.8914초(10분 25.9초)**를 추가한다. 초기 준비만 수행한 전체 수명주기는 **34분 17.5초**다. UC의 이후 소스 편집·refresh·에셋 검사는 제외하므로 전체 시간을 동일 작업량 비교로 읽지 않는다. 준비 완료 뒤 분석과 진단 복사가 회수와 겹치므로 회수 시간도 고립된 최적화 벤치마크가 아닌 맥락 정보다.

두 작업은 `released`이며 독립 결과 tree가 변경하지 않은 소스 tree와 같다. 소유 Editor/provider·mount·task delta·Git 등록이 없다. 원본 1,602개 파일의 바이트와 HEAD/branch/기존 refs를 보존했고 Library는 원래 볼륨의 일반 NTFS다. 임시 fixture-local Git 설정을 복원했으며 전역 설정은 그대로다.

두 stop 명령은 약 120초에 **EditorStopTimeout**을 반환했고 같은 정상 종료가 226.6012초에 완료됐다. 실패 응답은 실패로 유지하며 종료 요청 반복·강제 종료는 없다. **실제 CLI 응답 12개** 모두 기존 현재-schema 검사를 통과했다(집중 검사 1개 통과). 실행 전 커밋 후보의 필수 build 결과는 **1,315 passed, 0 failed, 19 conditional skips**다. 관련 없는 dirty 파일 27개는 보존했다. 새 Play/Test/build·물리 디스크 peak·전체 RAM peak·OS 장애 자격은 **NotRun**이다.

현재 구현 후속은 [SU-01–05](startup-profile-optimization.md)가 소유한다. 이 문서의 당시 입력·후보·시간 자격을 새 구현으로 승계하지 않는다.

이후 [US-01 측정](unity-startup-remeasurement.md)에 최초 준비 349.7933초, 시작 판독 수정과 남은 비용을 기록한다. 위 UP 근거는 당시 후보에 한정한다.
