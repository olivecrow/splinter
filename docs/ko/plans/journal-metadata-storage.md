# 저널 기본값 메타데이터의 물리 저장 개선

[English](../../plans/journal-metadata-storage.md)

질문은 **현재 SJR2의 의미가 같은 기본값을 생략해 새 쓰기와 명시적 정리의 실제 NTFS 저널 할당을 줄일 수 있는가**다. 제품 목표는 원본 NTFS 위치·계속 편집 가능한 원본·최초 전체 복제/읽기 hydration/전체 파일 copy-up 금지·작업별 범위 저장이다. 물리 공간 → 메모리 → 병렬 안정성 순서를 유지한다. 이 문서는 사용자 요청으로 확정한 구현/검증 단위이며 마감한 JP/M0·새 Unity A/B를 재개하지 않는다.

JM-01~04는 [직접 검증과 비용/제한](../verification/journal-metadata-storage.md)에 따라 scoped-complete-with-limitations로 마감했다. 아래는 고정 범위와 종료 기준이며 추가 실행 지시가 아니다.

## 선정 근거

JP의 보존된 실제 입력 `a-after-compact/encoded`와 checkpoint seal에 묶인 `b-collected-journal/encoded`를 읽기 전용으로 검사했다. 모든 외부 checksum과 전체 SHA-256이 기존 기록과 일치한다. A 23,718 frame의 JSON 10,268,190B 중 기본값 854,111B, B 61,687 frame의 JSON 25,531,461B 중 기본값 2,194,498B가 생략 가능하다. A는 이미 정리된 입력이므로 대체 payload 제거와 구분해 판단할 수 있다. B의 완전히 대체된 payload 52,482,657B는 기존 명시적 정리가 담당한다. 이번 개선량에 그 효과를 포함하지 않는다.

## 포함과 완료 조건

| ID | 범위 | 완료 조건 |
| --- | --- | --- |
| JM-01 | private Mutation의 Offset=0·Destination=null·Replace=false 직렬화 생략 | 기본값 생략과 명시적 기본값의 동일한 현재 reader 의미를 literal frame으로 확인. 비기본값 offset/rename destination/replace=true 보존·raw/Brotli 내용·개별 ACK/flush 유지 |
| JM-02 | 명시적 compaction/candidate의 유지 frame 메타데이터 축소 | decoded 현재 mutation을 다시 직렬화한 표현이 원본보다 작을 때 선택. payload 블록은 재사용하며 fragment/skip 선택·sequence/path changes·state hash·예상/실제 길이·겹침 예산·같은 intent 재개 유지 |
| JM-03 | 같은 실제 저널 전후 파일 할당 | 위 A/B 두 고정 저널의 새 소유 복사본에서 고정 선행 Runtime과 후보 Runtime으로 각 한 번 정리. FILE_STANDARD_INFO.AllocationSize·이전 세대/겹침·정리 완료 후 잔여량·payload/frame 비용과 시간/관리 할당을 별도 기록. 원본 evidence hash 불변, private state hash/sequence 동일·기준 payload 읽기 0 |
| JM-04 | 회귀와 문서 | 기존 정리/후보 게시 중단·checksum/부분 tail/ACK·원본/peer/회수 guard의 필수 build 회귀, UTF-8·diff·현재 계약/지도/계획/검증 동기화 |

계약은 overlay v2·SJR2·candidate v3·workspace v2 그대로다. 생략할 세 필드는 현재 reader가 이미 default로 읽는 private JSON 필드다. 새 버전/구버전 reader·자동 migration을 추가하지 않는다. 저장된 저널은 ensure/read에서 재작성하지 않으며, 승인 쓰기와 명시적 정리/후보 게시에서만 더 작은 현재 표현을 사용한다. 입력/응답 schema·Core payload 계약·서비스/권한·CLI 의미를 변경하지 않는다.

## 실행 자원과 제외

`development @ 7f07cb7`을 선행 Runtime으로 고정하고 `artifacts/journal-metadata-storage-20261003/`에 새 비교 복사본·바이너리 hash·raw 결과를 보존한다. 기존 JP artifact는 읽기 전용이다. fake snapshot은 private replay만 제공하며 기준 payload 읽기는 거부한다. 실제 Unity 입력에서 나온 저널의 오프라인 비교이며 새 후보의 실제 WinFsp/VSS/Unity 수명주기 자격으로 승계하지 않는다.

추가 전체 데이터 복제는 계측용 저널 복사본의 비용이며 제품 생성/읽기 hydration이 아니다. 소유 복사본의 이전 journal/intent만 검증된 CompleteCompaction으로 회수하고 원본·기존 결과·snapshot·타 작업은 수정하지 않는다. 원본 Library/Git/독립 결과/임시 evidence·공유 VSS·전체 workspace 물리 peak는 저널 파일 할당과 분리한다. 전체 workspace 절감률·VSS 작업별 귀속·전체 RAM/native codec memory는 unknown이다.

새 harness 행렬·추가 A/B/Editor·원본 반영/check-in·설치/grant/볼륨/UAC·대형/ARM/원격/OS 장애·flush 정책·새 codec/backend·자동 정리/큐·전체 메모리 최적화는 제외한다. 증가/내용/복구 실패가 있으면 증거를 보존하고 이 경계만 수정한다. 네 조건의 직접 근거가 있으면 이 단위만 scoped-complete-with-limitations로 마감한다.
