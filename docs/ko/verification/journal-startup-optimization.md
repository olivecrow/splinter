# 시작 단계 저널 메모리·조회 최적화

[English](../../verification/journal-startup-optimization.md)

> **현재 시간 기준:** 아래는 해당 후보/단계에서 기록한 과거 근거이며, 수치와 당시 NotRun 경계는 그대로 보존합니다. 이후 최적화를 합친 `116fd44`의 [UF-01 실측](unity-final-lifecycle.md)은 전체 **18분 3.0초**, Editor 준비 **3분 36.1초**, A/B 보존·회수 **2분 52.0초**, Library 복원 **2분 6.0초**입니다. UF가 개별 최적화의 효과나 별도 미실행 항목을 모두 검증한 것은 아닙니다.

**JS-01–04**는 [UC-01 Unity 시작 단계 검토](unity-checkpoint-release-remeasurement.md)에서 확인한 저장 경로 문제를 다룬다. 이전 Unity 준비 시간 **848.3379초**는 당시 후보의 기록이다. 이번 변경의 자격은 작은 Editor 없는 시험이며 새 Unity 준비 시간을 뜻하지 않는다.

## 회수한 metadata의 수명 — JS-01

기존 payload 위치는 capsule의 전체 `Frame`을 참조했다. metadata 회수 뒤 물리 footer와 대표 frame의 metadata는 제거해도 payload 위치가 이전 frame·직렬화 footer·역직렬화 item 배열을 계속 참조했다. block을 이어 쓰면 디스크에서 회수한 과거의 커지는 node metadata가 메모리에 누적될 수 있었다.

`LatestJournalStorage.Location`은 이제 물리 경로·offset·길이와 선택적인 현재 metadata만 갖는다. metadata 읽기는 경로별 현재 frame을 조회하고 payload 위치는 과거 frame을 유지하지 않는다. 현재 metadata 검증을 유지하며 payload cache나 저장 형식 변경을 추가하지 않는다. 약한 참조 회귀 시험으로 writer와 원래 payload가 살아 있어도 과거 footer/item 배열은 수거됨을 확인하며, 바이트·개별 mutation flush·재연결 seal을 유지한다.

## 변경 파일 조회와 임시 할당 — JS-02

파일별 object 인덱스로 capsule 회수 계획과 이전 root 이동의 전체 object 검색을 대체했다. seed·순서대로 재생·교체·삭제·게시에서 두 인덱스를 함께 갱신한다. 재연결에서는 검증한 저장소에서 다시 구성하며, 인덱스를 영속화하거나 소유·내용 검증을 대신하지 않는다.

footer 비교는 최대 64KiB의 재사용 임시 버퍼로 모든 바이트를 검사하고 전후 파일 길이를 확인한다. metadata hash는 검증한 현재 배열을 복제하지 않고 직접 사용한다. 전체 hash/checksum·개별 mutation의 한 durable flush·게시/회수 순서·poison·복구 경계를 유지한다. SJL4/root v4·overlay v2·candidate v3 형식은 같으며 명령/schema 변경이 없다.

## 작은 일반 배포본 측정 — JS-03

[데이터](../../../reports/benchmarks/journal-startup-optimization-20261008.json)에 소스·바이너리 hash와 6개 실행을 기록했다. 일반 Release probe가 로컬 NTFS에서 별도 private 파일 128개를 만든 뒤 한 파일에 4KiB block을 256회 또는 512회 이어 쓴다. 준비와 최종 참조/내용/seal/재연결 검사는 이어 쓰기 타이머 밖이다. 모든 실행에서 정확한 payload·다른 파일 유지·이어 쓰기마다 한 flush를 검증했다. 각 입력의 이전/현재 논리 put 계수와 현재 저널 크기도 같았다.

기준은 UC-01의 고정한 `168081c` runtime이다. 후보는 `4989f27`에서 시작했으며 소스/runtime hash로 식별한다. 순서는 이전256 → 이전512 → 현재512 → 현재256 → 현재512 반복 → 이전512 반복이며 cache를 비우지 않았다. 표는 후보별 512회 실행 두 번을 사용한다.

| 항목 | 이전 | 현재 | 관측 감소 |
| --- | ---: | ---: | ---: |
| 도달 가능한 footer/item byte 배열 | 66.956 MiB | 0.523 MiB | 99.22% |
| 호출 스레드 누적 할당 | 554.501 MiB | 383.912 MiB | 30.76% |
| 512회 이어 쓰기 평균 | 3.880 s | 3.740 s | 3.61% |

남아 있는 배열의 정확한 합계는 **70,208,305 → 548,599바이트**다. 저장소 frame/location을 통해 도달 가능한 서로 다른 footer/item byte 배열만 세며 객체 부가 비용·전체 provider/시스템 RAM은 제외한다. 호출 스레드 할당은 동기 쓰기 중 누적 할당이며 현재 heap 크기가 아니다. 256회 비교의 남은 배열은 **17,981,848 → 413,218바이트**, 시간은 **1.7956 → 1.7651초**다.

이 이어 쓰기 패턴에서 큰 참조 유지 감소를 확인했다. 작은 시간 감소는 순서가 고정된 warm 관측이며 일반적인 속도 추정이 아니다. 전체 현재 node metadata 직렬화는 계속 수행하며 512회 논리 put 합계는 **31,906,237바이트**로 같다. 논리 put은 물리 디스크 I/O가 아니다. metadata 표현의 추가 변경과 원래 전체 작업의 기록 증폭은 별도 후속이다.

## 검증과 남은 경계 — JS-04

`pwsh -NoProfile -File ./scripts/build.ps1`의 locked restore·Release 빌드가 **경고/오류 0개**로 통과했고, 시험은 **1,277개 통과·실패 0개·조건부 건너뜀 19개**다. 새 4개 사례는 살아 있는 writer의 과거 참조 수거, 이어 쓰기/metadata/축소/부분 덮어쓰기/이름 변경/unlinked 회수와 쓰기 재연결, 첫 64KiB 뒤 또는 마지막 바이트의 footer 손상을 다룬다. 기존 게시·회수 중단/손상/candidate/seal 회귀도 함께 실행했다.

최초 sandbox 실행은 `File.Replace`를 `UnauthorizedAccessException`으로 거부했다. 불완전 fixture는 보존했다. 실패한 정확한 소유 probe만 PID·시작 시각·명령행을 확인해 종료했고 성공 측정은 일반 사용자 권한의 새 경로에서 수행했다. UAC·관리자 승격·서비스 변경·Unity 조작은 없었다. 원시 실행·probe·빌드 결과는 ignored `artifacts/journal-startup-optimization-20261008/`에 유지하며 기존 별도 미커밋 파일은 변경하지 않았다.

새 Unity 준비/컴파일 시간·전체 provider/시스템 메모리·최대 디스크·OS 장애 자격은 **NotRun**이다. Unity 스캔 448초와 Bee 그래프 재생성은 별도 검토 대상이다. 소스 timestamp와 raw-Git 바이트 계약은 바꾸지 않았다. 재임포트한 원본 에셋 27개는 원본 CRLF와 committed LF로 달라 원본 timestamp의 무조건 복사는 부적절하다. 이 작은 fixture로 현재 Editor 시간 개선을 주장하지 않는다.
