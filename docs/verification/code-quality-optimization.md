# 코드 최적화의 자동 회귀

`development @ 8873ba6`의 기존 안전 수정 커밋 위에서 검토한 여섯 항목만 수정했다. 종료 조건은 반복 전체 검색/블록 복원 제거, 정확한 다음 행동의 인자 보존, 기존 seal/소유권/내용·실패 미게시 계약과 필수 자동 회귀다. 새 의존성·schema/상태 버전·명령·지속 cache를 추가하지 않는다. 실제 Unity/VSS·서비스 설치/업데이트·새 native A/B나 닫힌 검증 행렬은 실행하지 않는다.

## 확인한 경계

| 항목 | 확인 방법 | 한계 |
| --- | --- | --- |
| 다음 행동의 공통 소유 | `WorkspaceActionTests`의 None/Plastic 전체·선택 계획/재개 8개와 Git/Plastic Library·None project basis ensure 3개. 직렬화 인자를 기존 입력 schema로 검증 | 명령을 실제 제품 자원에 재실행하지 않음 |
| 파일 rename 검색 | 파일은 해당 node만 이동. 기존 replacement/lazy/whiteout/Unicode/대소문자 회귀와 새 깊은 하위 파일의 부모 미기록 재생 2개 | 디렉터리는 부모 없는 재생 node 때문에 경계 접두사 검색 유지. 대형 입력 시간 전후 미측정 |
| 복원 extent 탐색 | sparse 512 extent의 처음/중간/마지막 4B 복원은 검색 probe 최대 10·방문 extent 1. hole·다른 seal 거부 | private 범위/fake 기준의 자동 시험이며 실제 프로젝트 최종 restore를 다시 실행하지 않음 |
| 고유 객체 검사 | 32개 같은 파일과 기준/결과의 같은 객체는 한 검증 호출에서 객체 open 1회. 이후 손상은 새 호출에서 거부. 다른 선언 길이는 payload 읽기 전에 거부 | 게시 전/후 및 이후 검사 단계는 각각 다시 읽음. 파일 수집 전체 시간 절감률 미측정 |
| 원본 계획 검색 | 중첩 선택·경계 형제 `dir1`/`dir10`·외부 자식 삭제 차단·없는 선택 거부와 기존 전체/선택·Unity companion 회귀 | 현재 원본 메타데이터/내용 관측·물리 identity/게시 guard 유지. native 게시 수명주기 자격은 기존 소유 문서의 당시 후보에 한정 |
| 압축 순회 | 최대 1MiB private 같은 쓰기는 저장 payload만큼 journal read. 최대 블록·부분 덮어쓰기 fragment의 compaction bytes/sequence와 `before-state-hash`·`candidate-scan` 읽기 수 확인 | extent/단계 간 영구 dedup 없음. public 작은/빈 read staging·손상 tail 거부·pool 반환은 기존 codec 회귀. native codec/Editor/전체 RAM·실제 Unity 시간 미측정 |

상태 hash/비교는 extent별 한 번 복원하고 정리의 같은 블록 slice는 모아 한 번 복원한다. 기존 header/checksum·tail/선언 길이 검사를 생략하지 않는다. 임시 digest/비교/후보는 검증 성공 뒤에만 사용·게시하며 공개 read destination은 전체 검증 전 수정하지 않는다. 소유권·seal·승인 쓰기의 개별 flush와 회수 guard는 그대로다.

## 실행 근거

`artifacts/code-quality-optimization/`에 집중 시험의 최초/최종 TRX와 필수 빌드 로그·전체 TRX를 보존한다. 최초 집중 시험은 294개 중 293 통과·1 실패다. 실패는 Plastic fixture가 recorded base schema 대신 임의 문자열을 사용한 입력 오류였으며 실제 recorded 형식으로 수정했다. 기존 실패 결과도 보존한다.

최종 집중 시험은 **299 통과·실패/skip 0**이다. 추가한 회귀는 다음 행동 11개·저널 검색/블록 순회 4개·부모 미기록 rename 재생 2개·객체/원본 계획 3개로 총 20개다. 필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, **1,025개 중 1,024 통과·실패 0·선택적 ReFS 1 skip**이며 전체 시험은 약 4분 35초다. `build-final.log`·`full-final.trx`와 변경 C#의 `source-manifest.json`을 보존했다. 문서 119개는 UTF-8 검사에 통과했다. 이 문서는 자동 회귀의 증거이며 과거 RI/JP/ME/SP·원본 반영의 실제 입력 자격을 현재 후보의 native 통과로 승계하지 않는다.
