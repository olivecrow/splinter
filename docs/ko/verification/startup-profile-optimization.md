# 준비 프로파일 후속 최적화

[English](../../verification/startup-profile-optimization.md)

> **현재 시간 기준:** 아래는 해당 후보/단계에서 기록한 과거 근거이며, 수치와 당시 NotRun 경계는 그대로 보존합니다. 이후 최적화를 합친 `116fd44`의 [UF-01 실측](unity-final-lifecycle.md)은 전체 **18분 3.0초**, Editor 준비 **3분 36.1초**, A/B 보존·회수 **2분 52.0초**, Library 복원 **2분 6.0초**입니다. UF가 개별 최적화의 효과나 별도 미실행 항목을 모두 검증한 것은 아닙니다.

현재 디렉터리 핸들 보정: [SA-01–02](startup-additional-optimization.md)는 속성 전용 핸들의 고정 가정을 실제 native access/sharing·소유 파일 수명 보호로 바로잡는다. 아래 SU 비용과 후보 자격은 과거 기록으로 유지한다.

**SU-01–05**는 [UP-01](unity-startup-profile.md)에서 확인한 사용자 모드 병목을 개선한다. UP의 최초 준비 766.9024초·약 447초 스캔은 이전 후보의 근거다. 이후 실제 시간은 시작 판독 수정과 서로 다른 runtime 재개를 포함한 [US-01](unity-startup-remeasurement.md)이 소유한다. UAC·설치 서비스 업데이트·외부 패키지 변경·원본 프로젝트 수정 없이 적용한다.

## 독립 Git decoder 진행 — SU-01

Git reader는 최대 두 decoder 세션에 독립 임대를 부여한다. 공통 gate는 proof 입장·LRU 소유·최종 게시를 보호하며 decoding·hash는 gate 밖에서 수행한다. 같은 blob의 별칭은 직렬화한다. 진행 중 proof를 고정하고 할당 전 새 proof 예약도 합산하여 4MiB/4,096개 proof 예산과 payload 창 합계 256KiB를 유지한다. 두 세션이 사용 중이면 세 번째 blob은 기다린다. 전체 blob·요청 chunk hash, 고정 identity, 무진행 시간 제한과 검증 뒤 출력 게시를 유지한다.

한 reader의 실패는 다른 진행 중 reader의 후속 게시를 막고 두 세션 종료를 기다린 뒤 proof와 프로세스를 정리한다. 동시 close는 새 읽기를 거부하고 진행 중 작업을 기다린다. 입장한 요청 각각의 staging은 최대 1MiB이며 다음 임대를 허용하기 전에 반환하고, 대기 요청은 할당하지 않는다. 정확히 알려진 blob header는 byte마다 timer/pipe를 호출하는 대신 한정 span으로 읽으며, 짧거나 missing·손상된 header는 존재하지 않는 나머지를 기다리지 않고 거부한다. 두 chunk 유지와 역방향 읽기의 재decoding은 그대로다. 디스크 payload cache·hydration은 없다.

첫 통합 시험에서 새 두 reader·두 Dispose 회귀의 종료 대기를 발견했다. 짧은 추적과 별도 일반 thread 재현에서 종료 예외 뒤에도 실패 reader가 관리 monitor를 가진 것을 확인했다. 예외 분류를 바깥 filter 대신 내부 잠금을 푼 뒤의 catch 본문에서 수행하도록 바꿨다. 집중 시험은 종료 예외와 예상 밖 완료 예외 뒤의 monitor 소유도 확인한다. 중단한 전체 실행과 진단은 실패 근거로 보존하며 통과로 바꾸지 않는다.

## 고정된 저널 조상 경로 재사용 — SU-02

제품 WinFsp 저널은 공통 `IJournalDirectoryGuard`를 주입한다. Windows 구현은 볼륨 root부터 정확한 SJL4 object 디렉터리까지 일반 디렉터리 핸들을 유지하며 write/delete sharing을 거부해 조상 교체·reparse 전환을 막는다. 이후 object 접근은 모든 조상을 다시 걷는 대신 leaf와 소유 namespace를 검사한다. 일반 하위 파일 쓰기는 가능하다.

증명은 object 디렉터리 세대 하나에 고정된다. 생성 실패·close는 진행 중 읽기/쓰기가 끝난 뒤 핸들을 반환한다. 다른 경로·기존 junction·주입된 pending-file junction·손상 상태는 보존·거부한다. guard 없는 경로·오프라인·seal은 전체 조상 검사를 유지한다. 형식·개별 durable flush/ACK·게시·복구·composite seal은 같다.

## 효과 없는 Cleanup의 직렬화 제거 — SU-03

삭제 없는 WinFsp Cleanup은 handle 수를 한 번만 감소시키며 mutation gate에 들어가지 않는다. Retained file context는 Close까지 살아 있다. 삭제 Cleanup과 Close는 여전히 mutation과 직렬화하며 마지막 context 종료까지 unlink 회수를 기다린다. 회귀 시험은 쓰기를 durable 효과 직전에 막아 non-delete의 독립 진행, delete/Close의 대기, unlink 뒤 두 번째 context 읽기를 확인한다.

## 소유 bridge assembly 분리 — SU-04

`Assets/__Splinter__/Editor/Splinter.Workspace.Editor.asmdef`는 bridge를 `autoReferenced: false`인 Editor 전용 assembly로 분리한다. 현재 infrastructure marker는 **version 2**이며 C#·assembly definition hash와 workspace 소유자를 묶는다. 전체 검증 뒤 동일 resource와 Unity의 `.meta`·시각을 재사용하고, 내용이 달라진 검증된 소유 resource만 다시 쓴다. 누락/이전 marker·변경된 definition·예상 밖 하위 항목·reparse는 자동 이관 없이 보존·거부한다.

작은 일반 NTFS 프로젝트를 실제 **Unity 6000.3.22f1**에서 열고 정상 종료한 뒤 같은 경로에 bridge를 추가해 다시 열었다. Runtime·기본 Editor assembly hash는 동일했다. 두 번째 로그의 유일한 Csc 작업은 `Splinter.Workspace.Editor.dll`이며, bridge는 소스 하나인 해당 assembly에서 로드되고 두 기본 assembly가 참조하지 않았다. Fixture는 정확한 ready heartbeat를 관측한 뒤 정상 종료를 요청했다. 두 번째 로그의 CompileScripts는 **1.181846초**였다. Cold 최초 준비와 warm 두 번째 시작은 입력이 다르므로 전체 시간 차이를 준비 시간 절감률로 읽지 않는다. 작은 assembly/수명 경계의 증명이며 대형 A/B·Play/Test/build·전체 패키지 후처리 자격은 아니다.

## 내용으로 증명한 소스 시각 — SU-05

일반 Git root의 새 Unity Library 준비에서 고정 commit/tree 목록을 캡처한다. 적합한 파일은 write/delete sharing을 거부하고 일반 조상을 고정한 뒤, 시각을 읽는 것과 같은 native 핸들로 내용을 읽고 hash한다. Raw blob hash가 정확히 같아야 metadata를 허용한다. CRLF 변환·내용 변경·누락·점유 파일에는 시각을 재사용하지 않는다. 캡처 뒤 소스 핸들을 모두 닫으므로 이후 원본은 수정 가능하다. Linked `.git` 파일 root는 최적화를 선택하지 않으며, 불완전/비로컬 일반 object store는 lazy fetch가 가능해지기 전에 거부한다.

원자 게시하는 크기 제한 `source-metadata.json` v1은 필수 canonical `contentHash`를 포함한다. 전체 파일 digest·완전한 Library identity를 generated-directory 참조와 Git receipt/snapshot identity에 고정한다. 상태 게시 전 중단 뒤 재시도도 기존 intent·Library manifest/proof·map 무결성을 검증한 뒤 고정 bytes를 재사용한다. 손상/다른 소유/누락된 쌍은 보존·거부한다. 이는 손상·소유 검증이며 악의적인 파일시스템 writer에 대한 인증은 아니다.

Reader를 열 때 고정 map을 한 번 검증하고 mutable 원본을 조회하지 않으며, 빈번한 읽기 health callback에서도 map을 다시 읽지 않는다. 같은 경로/blob/길이만 캡처 시각을 사용하며 이후 commit의 미변경 파일도 해당한다. 다른 항목·디렉터리는 일반 capture 시각을 유지한다. Map 없는 기존 기준은 자동 보충하지 않는다. Payload는 계속 raw Git object에서 읽는다. 최초 Library 준비에 소스 전체 내용 읽기 1회의 비용을 추가하며, 데이터 복사·payload cache·바이트 검증 약화·Bee cache/이전 ILPP endpoint/외부 도구 상태 대체는 없다.

후속 실제 A/B 측정에서 별도 CLI의 영속 작업공간 읽기 누락을 발견했다. `ensure`는 metadata를 포함한 올바른 snapshot ID를 저장했지만 다음 CLI의 `WorkspaceStore.Find`는 metadata 접미사가 없는 ID만 기대했다. 첫 `unity start` 두 개는 Editor 실행 전에 실패했다. 정확한 소문자 64자리 map hash를 기대 ID에 묶도록 수정하고 원본/provider/consistency/revision·mount guard는 유지했다. 실제 provider 재열기 3개와 잘못되거나 바뀐 identity 11개를 시험에 추가했다. 상태 파일 수정 없이 같은 task record와 mount를 이어 쓰며, 실패한 start 응답은 실패 근거로 보존한다. 이 수정은 record 허용 검증이며 decoder/저널 동작 변경이 아니다.

## 검증과 비용

구현의 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·Release build **경고/오류 0개**, 시험 **1,362개 통과 / 실패 0개 / 조건부 skip 20개**, 시험 실행 시간 258.697초로 완료했다. 영속 record 읽기 수정 뒤 같은 필수 build는 **1,376 / 0 / 20**, 256.381초로 통과했다. Git 동시성 집중 시험 13개와 선택적 저널 비용 시험 1개도 별도로 통과했다. 조건부 skip은 해당 외부/native 입력의 자격이 아니다. 아래 한정 비용은 record 허용 검증 수정 전의 수치이며 decoder·저널 알고리즘은 그대로다.

비용은 전체 시험이 끝난 뒤 단독 측정했다. 각 비교는 모드별 표본 두 개를 before/after/after/before 순서로 실행했다. 작은 warm 표본의 한정 비용 근거이며 통계적·Unity 전체 시간 절감률이 아니다.

| 측정 연산 | 기준 평균 | 최적화 평균 | 단축률 |
| --- | ---: | ---: | ---: |
| Packed Git blob 8MiB 두 개, 겹쳐 진행하는 4KiB 요청 | 0.268366초 | 0.149933초 | 44.13% |
| Peer 파일 32개를 둔 저널의 512 × 4KiB 이어 쓰기 | 3.847348초 | 2.953998초 | 23.22% |

Git 비교는 고정한 UP runtime과 최종 runtime으로 같은 packed tree를 읽는다. Timer는 reader barrier·decode·범위 게시를 포함하고 fixture 준비·최종 hash 검사·종료는 제외한다. 모든 실행에서 복원한 bytes가 같고, 16MiB 요청에 32MiB를 decode했으며 reader 두 개를 시작했다. 보유 payload는 최대 256KiB, proof는 8KiB였고 종료 뒤 소유 reader는 0개였다.

저널 비교는 같은 최종 binary에서 전체 조상 순회와 조상 핸들 고정을 각각 새 깊은 NTFS fixture에 적용하며 이어 쓰기 loop만 잰다. 네 실행 모두 **durable flush 정확히 512회**, 같은 논리 appended/journal byte 수, 증가 파일·peer 32개의 내용 일치, 각 fixture의 reconnect 전후 seal 일치를 확인했다. 이전 release 비교·물리 I/O 절감·flush 생략의 근거는 아니다.

실제 원본 Unity 소스의 읽기 전용 시각 캡처는 **목록 1,602개 / 2,804,673,377 bytes**를 검사했고 map 검증을 포함해 **5.489146초**, 목록/내용 읽기는 5.438930초였다. **C# 일곱 개 전체를 포함한 1,568개 파일**이 raw Git blob과 일치했다. 합성 Library tuple을 쓴 계측 map은 **397,543 bytes**였다. Warm 표본 한 번으로 추가되는 최초 준비 작업을 잰 것이다. Library 준비·이동·mount·상태 게시·Editor 실행은 없었으며 이후 scan 절감량을 측정한 것은 아니다.

작은 실제 Unity bridge 실행은 SU-04만 한정 검증한다. 모든 SU 변경을 적용한 전체 Unity A/B 준비·checkpoint/release·최종 복원은 **이 한정 측정 당시 NotRun**이었고 이후 [US-01](unity-startup-remeasurement.md)에 실제 cohort를 별도 기록한다. UP-01의 766.9024초를 현재 결과로 승계하지 않는다. 원본 Unity tree는 clean을 유지했고, 관련 없는 dirty 파일 27개는 작업 전 hash와 같았다. 한정 측정 중 Git staging·UAC·서비스·외부 패키지 변경은 없었다.

[기계 판독 결과](../../../reports/benchmarks/startup-profile-optimization-20261008.json)에 입력/runtime hash·개별 표본·검증 경계를 기록했다. 원시 로그·계측 도구·실패 실행 진단은 ignored `artifacts/startup-read-followup-20261008/`에 보존한다.
