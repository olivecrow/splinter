# Raw 쓰기와 묶음 flush 범위

[English](../../plans/raw-write-batching.md)

사용자는 2026-10-25 쓰기 압축 폐기와 묶음 flush 구현을 명시적으로 요청했다. 이전의 변경별 물리 flush·압축 요구를 대체한다. 응답별 내구성은 유지하며 조기 ACK나 주기적 지연 저장은 추가하지 않는다. 현재 계약은 [raw 쓰기와 공유 flush](../raw-write-batching.md)다.

이 범위의 RW-01–05는 완료했다. 최종 회귀 gate와 구성 요소/native 측정의 한계는 [검증](../verification/raw-write-batching.md)에 기록한다.

- RW-01: 제품의 모든 압축 writer·선택 경로 제거. 현재 압축 데이터 reader와 손상/복구 검증 유지.
- RW-02: 크기가 제한된 순서 보장 SJR2 batch API, 공유 내구성, checkpoint drain, 실패 poison/재연결. 단일 쓰기와 SJL4 게시 보호 유지.
- RW-03: 직렬화 전 실제 WinFsp 동시 callback 수집. append/constrained 의미·native 버퍼 수명·staging 한도 유지.
- RW-04: 동일 raw 입력의 순차/batch 구성 요소 측정과 새 작은 native WinFsp fixture 하나. 내용·정상 unmount·재연결 확인. 과거 Unity/폐기 행렬은 반복하지 않음.
- RW-05: 영어/한국어 계약·capability schema·시험 갱신과 필수 locked Release 빌드/시험.

원본 반영·Unity 수명주기 실측·장치 설정·새 저장 backend·CLI batch 선택·이전 형식 자동 이관은 제외한다. 한 요청씩 완료를 기다리는 호출자는 요청마다 flush가 유지될 수 있으므로 묶음의 측정 이득을 일반 배수로 확대하지 않는다.
