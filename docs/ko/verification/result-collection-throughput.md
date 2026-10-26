# 파일 결과 수집 처리량 검증

[English](../../verification/result-collection-throughput.md) · [범위](../plans/result-collection-throughput.md) · [기계 판독 결과](../../../reports/benchmarks/result-collection-throughput-20261026.json)

**RT-01–04, 2026-10-26.** 기준은 `9b68a82cd89912e404d8acd6dcf7e48ce513a353`이며 채택 소스 hash는 데이터와 수명주기 색인에 기록한다. 실제 Windows 제품의 **None / project-snapshot / application none** 비교다. Unity Editor 전체 수명주기나 실제 Plastic 서버의 자격은 아니다.

## RT-01–02 — 반영한 동작과 보호

`FileWorkspaceProvider`와 Plastic wrapper를 기존 `ISnapshotCheckpointSourceProvider`에 연결했다. 기존 checkpoint·inspection·preservation의 소스 scan 세 번을 coordinator의 전체 전후 seal 구간 안에서 하나의 소스 manifest 수집으로 합쳤다. Runtime은 seal 일치 후에만 결과 revision을 확정한다. 기존 task 잠금·앱 정상 종료·독립 결과 요구·unmount 뒤 seal은 유지한다.

로컬 전체 payload 보존과 독립 object 검증은 최대 8 worker를 사용한다. 서로 다른 hash는 별도 pending/output 경로를 가지며 새 object마다 영속 flush·내용 증명·게시를 유지한다. 기존/중단 object와 이후 호출은 다시 검증한다. 실패·취소 시 모든 진행 중 작업을 drain한 뒤 기존 제품 오류로 반환한다. recorded Plastic revision 획득은 직렬이고 주입한 검증 reader의 직렬 수명 계약도 유지한다. 저널 형식·CLI/schema·소스 manifest 형식·쓰기/ACK·원본 반영 계약은 바꾸지 않았다.

집중 시험 **68/68 통과**. 새 시험은 None/Plastic의 seal 변경·revision 미게시·같은 task 재시도, 손상된 pending object 보존과 결과 게시 거부, 실패/취소 시 worker drain을 확인했다. 기존 독립 결과 손상·중복 제거·소유/peer·원본 반영·checkpoint/release 시험을 유지했다. 합성 Plastic 시험은 계약만 증명한다.

## RT-03 — 같은 입력의 실제 제품 비교

별도 로컬 NTFS 원본 **2,281,701,376바이트(2.125GiB)·2,112개 파일**을 사용했다. 서로 다른 raw 파일 64 × 32MiB와 2,048 × 64KiB이며 고정 seed와 전체 SHA-256/길이/mtime manifest를 기록했다. 제품 prepare는 이 원본을 하나의 공통 관리형 project basis로 이동한다. 매번 새 작업공간에서 같은 큰 파일의 8바이트를 덮어쓰고 같은 64KiB 파일을 추가하며 수정 시각을 고정한 뒤 checkpoint·release한다. Editor는 시작하지 않았다.

전후 실행 파일을 SDK **10.0.114**로 고정했다. 실행 순서는 **전 → 후 → 후 → 전**이며 한 번에 작업공간 하나만 측정하고 build/test를 병행하지 않았다. 매 작업 전후 입력의 내용·수정 시각을 확인했다. 네 기준 identity와 네 결과 identity가 각각 일치했다. release 뒤에도 독립 결과의 필요한 모든 payload를 직접 읽어 hash를 확인했다. 결과마다 **고유 object 2,114개·2,315,321,344바이트**다. 네 task의 mount/delta/provider를 회수했고 공통 project basis를 일반 NTFS로 복원한 뒤 원본 내용/mtime 일치를 확인했다. 독립 결과와 영수증은 보존했다.

| 측정 단계 | 변경 전 평균(초) | 변경 후 평균(초) | 절감(초) | 절감률 |
| --- | ---: | ---: | ---: | ---: |
| 작업공간 ensure | 65.051 | 65.599 | -0.548 | -0.8% |
| 고정 private 편집 | 0.037 | 0.037 | 약 0 | 의미 있는 변화 없음 |
| checkpoint | **98.429** | **58.944** | **39.485** | **40.1%** |
| 별도 release | 39.788 | 35.326 | 4.463 | 11.2% |
| 위 작업공간 단계 합계 | **203.305** | **159.906** | **43.399** | **21.3%** |

실행 순서의 개별 checkpoint는 **87.991, 47.082, 70.807, 108.867초**다. 절대 시간 편차가 컸지만 인접한 전후 두 쌍 모두 변경 후가 40.910초·38.060초 짧았다. 평균은 사전에 정한 checkpoint 20%·10초 이상 기준을 충족했다. release의 작은 차이는 별도의 큰 최적화 성과로 주장하지 않는다. 두 변경을 묶어 측정했으므로 scan 제거와 payload 병렬 처리의 개별 인과 효과는 분리하지 않았다.

공통 준비는 **9.031초**, 마지막 공통 복원은 **12.018초**였다. 각각 ABBA 밖에서 한 번 수행했으므로 위 합계에 넣거나 전후 비교로 주장하지 않는다. 시험 입력 준비·입력/독립 결과 증명·사람/에이전트 대기도 합계 밖이다. 한 장치에서 버전별 두 번이며 OS 캐시를 강제로 비우지 않았다. 일반 절감률·전체 peak RAM·전원/장치 장애의 자격은 아니다.

이 입력에서 남은 큰 비용은 생성 **65.599초**, 전체 결과 수집 **58.944초**, 별도 회수 **35.326초**다. 기준/결과 전체 내용 구체화와 이후 호출의 새 증명 비용이 남는다. Git 생성/admission 겹치기, 새 증분 내부 저널 checkpoint, 변경 없는 원본 subtree 복원은 이번에 구현하지 않았다. 원본 반영/폐기 성능, Unity 시작·컴파일·Play/Test/build, 실제 Plastic 서버 시간은 측정하지 않았다.

## RT-04 — 저장소 검증과 근거

필수 `pwsh -NoProfile -File scripts/build.ps1` 통과: locked restore/build, **전체 1,631 / 통과 1,603 / 실패 0 / 건너뜀 28**, 명령 전체 **538.906초**. 현재 제품 assembly와 실측 후보가 일치한다. 문서 검증은 색인의 소스 hash **36개**·symbol 행 위치 **115개**·새 문서 링크 **12개**를 확인했다. 집중 빌드는 시험 취소 토큰 분석기 경고를 수정한 뒤 경고/오류 없이 성공했다. 기준 빌드 전에 샌드박스 MSBuild가 실패해 두 측정 버전 모두 같은 일반 로컬 환경에서 정상 빌드했다. 진단 실패 기록은 보존하며 측정 실행으로 세지 않는다.

후속 흐름 문서 갱신에서는 소스 hash **37개**·symbol 행 위치 **119개**·로컬 Markdown 링크 **512개**와 **언어별 도식 6개**의 동일한 연결 구조를 확인했다. JSON 색인은 같은 C20 단계 순서와 명령별 실측 평균을 담는다. 제품 소스/assembly hash와 기록된 gate log/TRX hash·집계가 여전히 일치하며, 문서만 갱신했으므로 새 실행 측정이나 시험은 반복하지 않았다.

원시 자료 `artifacts/result-throughput-20261026/`에 고정 실행 파일/hash·schema·`measure.py`·protocol/입력 manifest·네 command/closure 기록·공통 prepare/restore·집중/전체 시험 로그·최종 closure를 보존한다. 데이터에는 command 근거 hash와 소스/assembly identity를 기록했다. 현재 계약·양 언어 수명주기 지도·JSON 소스 색인을 갱신했다.
