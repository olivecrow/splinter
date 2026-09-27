# Replay index 비용 분해와 제한된 개선

2026-09-27 확정. `d48b3b0`의 [VR 실행](../verification/validation-view-readiness-and-replay.md)에서 새 replay 중앙값 14.90초 중 index 적용은 11.75초였다. 이번 작업은 그 내부 비용을 구분하고 재현 가능한 병목 하나를 개선하는 범위다. 용량과 생성·재개 시간을 함께 기록한다.

| ID | 범위 | 완료 기준 |
| --- | --- | --- |
| RI-01 | 연산별 replay index 횟수·시간 | 검증된 9개 연산만 계측하고 합계가 기존 frame/index 계수와 맞는다. 과거 누락은 unknown이다. schema·현재 문서·자동 시험을 함께 갱신한다. |
| RI-02 | 제한된 offline 재현 | 기존 VR의 연산 수·경로 규모를 참고한 range 쓰기와 rename 합성 입력을 만든다. 원시 Unity 저널은 회수했으므로 동일 실제 입력으로 광고하지 않는다. 전체 입력은 256MiB 안에 두고 생성 시간·할당·hash·메모리를 기록한다. |
| RI-03 | 확인된 병목의 최소 개선 | 계측 결과가 뒷받침하는 index 알고리즘 하나만 바꾼다. checksum·sequence·저널 형식·flush·snapshot 읽기 계약을 유지한다. 범위 겹침·truncate·hole·재개·손상·후보/정리 회귀를 검증한다. |
| RI-04 | 동일 입력 비교와 마감 | 같은 닫힌 입력으로 기존/계측/개선 구현을 구분하고 반복 시간·출력 상태/hash를 확인한다. 필수 build와 문서 검사를 마친다. 실제 Unity 재연결의 개선률은 NotRun으로 남긴다. |

새 Unity import·VSS·mount·전체 Library 복사본은 만들지 않는다. 범용 profiler나 새로운 제품 workspace 명령은 추가하지 않는다. 개발용 offline probe는 합성 identity만 허용하며 기존 입력을 덮어쓰거나 자동 정리하지 않는다. 첫 렌더, flush 묶음 변경, 기본 모드·Coordination 연결과 대형 PV는 별도 범위다. 실제 판정은 [RI 검증](../verification/replay-index-cost.md)에 기록한다.

정의한 범위는 `scoped-complete-with-limitations`로 마감했다. 300 자동 통과·선택적 ReFS 1 skip과 공통 probe 바이너리의 합성 27회 비교를 완료했다. 실제 Unity 재연결 개선률·첫 렌더는 후속 범위이며 기존 VR 판정을 바꾸지 않는다.
