# Unity 작업 처리 시간

[English](../../plans/unity-throughput-optimization.md)

이 후속은 [MW-01](../verification/unity-maze-workflow.md)에서 측정한 느린 최초 준비와 최종 회수를 다룬다. 판단 기준은 준비·통합·정상 종료·최종 복원을 모두 포함한 단일 공유 Editor와의 정직한 비교다. 저장 공간 절감만으로 유용한 시간상 이점을 입증하지 않는다.

## 구현과 적용 조건

1. Git 설정과 경로별 속성 해석이 같은 미변경 파일의 변환 hash를 재사용한다. 전체 연산 proof, HEAD/index, 구성원과 저널 세대 장벽을 유지한다. 새 manifest는 version 3이며 감사 요청에서도 이전 버전을 보존·거부한다.
2. 보호된 한 번의 commit 준비 안에서 capsule metadata 열기·검증 중복을 줄인다. 현재 물리 바이트 검증, 최종 회수 검사와 승인한 mutation마다 한 durable flush를 유지한다.
3. 크기가 제한된 진행 중 encoded 데이터와 기존 decoded cache로 private 읽기 경합을 검토한다. 읽기는 한 세대를 유지하고 close 전에 배출하며 동시에 writer가 poison되면 거부해야 한다.
4. 제품 변경 전에 작은 mount에서 Windows cache 유지를 시험한다. buffered/mapped 쓰기, 명시적 최종 flush, rename/unlink, seal 일치와 완전한 회수가 적용 조건이다. 읽기 반복만 빨라진 것으로 충분하지 않다.
5. 최초 Git 허용과 제한된 capsule container 설계는 계약을 바꾸기 전에 검토한다. 전체 읽기를 마감 단계로 옮기거나 현재 내용 증명을 시각으로 대체하지 않는다. 제한 없는 과거 payload, RAM만으로 쓰기 승인, 파일 전체 copy-up은 계속 제외한다.

독립 구현은 병렬 진행하되 빌드와 native probe를 조정한다. 기존 문서 작업과 과거 evidence를 보존한다. 자동 Editor pool, 원본 Editor 공유, 패키지 변경, 서비스 Update와 전역 설정 변경은 포함하지 않는다.

## 검증과 비교

대상 정확성·작은 비용 검증 뒤 `pwsh -NoProfile -File ./scripts/build.ps1`을 실행한다. 전체 Unity 비교에는 고정한 후보, 정확한 원본/source/Library inventory와 MW-01과 같은 고정 A/B 미로 payload가 필요하다. 수정된 공유 Editor 장면 인계를 양쪽에 사용하고 소유 정상 종료 receipt를 기다린다. A/B의 명시적 통합 checkpoint/release 명령은 동시에 실행한다. 독립 결과를 보존하고 원본 복원 및 소유 활성 자원 0을 확인한다.

최초 시작부터 끝까지의 비용과 단계를 보고한다. 겹치는 branch/counter 시간을 합산하거나 prewarming을 무료로 보거나 작은 fixture 절감을 확대하거나 과거 공유 Editor modal 지연을 유효 작업으로 제시하지 않는다. 작은 검증의 이득이 비싼 전체 재실행을 정당화하지 못하면 시간상 승리를 주장하지 말고 그 한계와 남은 병목을 기록한다.

## 결과

TP-01–04는 유지한다. native cache 후보는 작은 mapped-file 검증을 통과했지만 실제 미로 작업에서 정상 회수에 실패했다. 두 Editor가 정상 종료한 뒤에도 작업공간마다 연결된 Burst DLL 실행 이미지 문맥11개가 남았다. 정확한 기존 파일을 쓰지 않고 열어 사용이 끝난 이미지 문맥을 해제했으며, 소스·DLL metadata·전체 delta 바이트 inventory가 그대로인 상태에서 같은 workspace ID를 회수했다. kernel cache 유지는 철회하고 cleanup purge를 유지한다. 복구를 성공한 전체 시간 측정으로 취급하지 않는다. 공유 Editor 비교와 남은 성능 한계는 TP-06에 기록하며 일부 단계 개선만으로 추가 대형 Splinter 재실행을 정당화하지 않는다.
