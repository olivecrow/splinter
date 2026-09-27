# 검증 view의 첫 준비와 재연결 시간

2026-09-27 확정. `5c5d94f`의 [VC 실행](../verification/immutable-validation-view.md) 다음 범위다. 용량과 작업 가능 시점까지의 시간을 함께 판단한다. 기존 작은 Unity fixture와 격리 NTFS 볼륨만 사용한다. [VR 실행 기록](../verification/validation-view-readiness-and-replay.md)으로 제한을 포함해 마감했으며 첫 PNG가 빈 화면인 VR-03은 Partial이다. 이전 관측이나 아직 미해결인 첫 렌더 문제를 통과로 바꾸지 않는다.

| ID | 구현·실행 범위 | 완료 기준 |
| --- | --- | --- |
| VR-01 | 저널 replay의 읽기·검증·인덱스 구성 계측과 제한된 읽기 개선 | 프레임 수·실제 읽기 호출/바이트·단계 시간을 제공한다. 읽기 버퍼만 제한적으로 사용하며 원본 hydration·checksum 생략·저널/flush 정책 변경은 하지 않는다. 경계·손상·미완료 꼬리 회귀를 통과한다. |
| VR-02 | 긴 시작·같은 provider 재연결의 준비 응답 | launch와 replay를 mounted로 반환하지 않는다. 같은 PID/시작 시각을 기다려 실제 mount 및 최신 상태를 확인한다. 실패·취소·한도 초과는 보존하며 중복 provider를 만들지 않는다. |
| VR-03 | 빈 cache의 정상 첫 import·첫 렌더 | 수정된 현재 코드와 기존 모델/텍스처로 첫 import, 명시적 compile 완료, 실제 코드/에셋과 첫 미리보기를 확인한다. 빈 화면이면 원본 첫 관측을 보존하고 같은 Editor 재관측·관련 상태로 원인을 좁힌다. 운영 개입과 일반 성공을 구분한다. |
| VR-04 | 같은 cache의 반복 재연결과 보존/회수 | Editor를 정상 종료하고 닫힌 동일 저널의 전/후 구현 재생 시간을 각각 반복 측정한다. journal hash·논리 상태·읽기량을 맞추며 OS cache를 강제 비우지 않는다. source·Git·cache·공유 VSS·증거의 파일 할당과 소유 자원 회수를 기록한다. |

이번 실험은 단일 후보의 첫 준비·재연결이며 새로운 A/B 병렬 cohort가 아니다. 기존 VC의 A/B/A, 첫 실패와 빈 미리보기 증거를 덮어쓰지 않는다. 첫 렌더 문제를 조사하기 위해 fixture의 계측이 필요하면 소유 시험 view에만 변경하고 독립 결과로 보존한다. canonical Editor/MCP와 원본 fixture의 사용자 변경을 보호한다.

flush 정책·온라인 정리·자동 기본 모드·제품 큐/Coordination·source 반환 병합·대형 PV는 범위 밖이다. 단계 시간과 전체 CLI/Editor 준비 시간을 구분하고 개선 효과는 같은 닫힌 저널의 관측 범위에서만 판단한다. 실제 실행 전 상태는 NotRun이다.
