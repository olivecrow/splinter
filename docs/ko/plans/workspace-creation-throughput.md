# 작업공간 생성 처리량

[English](../../plans/workspace-creation-throughput.md)

이번 후속 요청은 작업공간 하나의 생성이 대상이다. [LU-01](../verification/unity-single-lifecycle.md)은 Library 병렬화 뒤 74.3초, 그 안의 연결·최초 검사 구간은 48.3–48.6초로 거의 그대로임을 측정했다. 기존 변경과 근거는 보존한다.

WC-01은 Git blob 전체 증명에서 이미 읽은 앞으로의 chunk를 최대 128MiB 보관한다. 전체 RAM LRU는 256MiB/4,096-entry다. WC-05는 큰 파일의 첫 바이트를 읽는 작업 하나를 필수 초기 Git index refresh와 겹친다. 실패·취소를 포함해 양쪽이 끝나야 attach가 반환한다. 새 index이며 소스 변경을 허용하지 않는 경우에만 사용한다. 기존 index도 계속 refresh하고 변경한 candidate에는 선행 읽기를 하지 않는다. 두 decoder·4MiB proof 한도·전체 object/chunk hash·실패한 reader의 게시 차단·health 검사·close/drain을 유지한다. 디스크 cache·최초 복제·Git stat cache 조작·index refresh 생략·durable ACK 지연은 추가하지 않는다. 요청 chunk와 선행 보관 chunk는 같은 예산을 쓰며 퇴출은 속도에만 영향을 준다.

먼저 고정한 실제 Unity 소스 1,602개 / 2,804,769,932바이트를 동일한 순차 읽기로 측정한다. cache 단독 ABBA의 8.6초 절감만으로는 부족했다. 큰 pipe 구성 요소 시제품의 약 4초 절감도 제외한다. 선행 읽기를 합한 pilot은 앞선 기준 74.0초에 비해 51.8초를 기록했다. 따라서 WC-03-final은 최종 결합 후보를 동일한 Library 기준본에서 전→후→후→전 순서로 비교한다. 두 실행 파일을 고정하고 전체 소스·내용 증명을 남기며 각 측정 task만 명시적 discard로 회수한다. 생성 측정으로 Editor 시작이나 Unity 전체 수명주기 개선을 주장하지 않는다. 유의미한 생성 시간 절감이 관측된 후보만 채택하고 실패 후보는 제품 복잡성 대신 근거로 남긴다.

WC-02는 전체 proof 실패·alias/병렬 가시성·손상·proof/LRU 퇴출·예산보다 큰 blob·close/drain을 다룬다. WC-03은 실제 생성·입력·회수 근거를, WC-04는 `pwsh -NoProfile -File scripts/build.ps1`과 양쪽 언어 문서를 소유한다. 원본 반영·Editor/컴파일/Play/Test/build·변경된 원본 Library 복원·OS crash 자격은 이번 범위 밖이다.

WC-01–05는 이 한정 입력에서 완료했다. 최종 생성 ABBA 평균 **73.953 → 51.915초(29.8%)**, 정확한 입력/회수 검사, 전체 gate **1,580 통과 / 실패 0 / 건너뜀 26**을 확인했다. Window/선행 읽기의 결합 변경을 채택한다. Pipe 시제품은 제외하며 앞선 관측치를 최종 평균과 분리한다. 마감한 Unity 비교를 자동 재개하거나 다른 입력/backend·Editor 작업으로 자격을 확대하지 않는다.
