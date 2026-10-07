# 저널 기본값 메타데이터 저장 검증

[English](../../verification/journal-metadata-storage.md)

2026-10-03 Windows x64/C: NTFS에서 [JM 범위](../plans/journal-metadata-storage.md)를 실행했다. 원본 전체 복제 없이 범위를 저장하는 제품 목표에서, 현재 SJR2의 생략 가능한 기본값을 개선 대상으로 골랐다. JP의 실제 Unity 저널을 새 소유 복사본에서 정리하는 오프라인 비교다. 새 후보의 실제 VSS/WinFsp/Unity 수명주기·전체 workspace 공간/RAM 자격을 주장하지 않는다.

## 고정 입력과 현재 계약

근거 root는 `artifacts/journal-metadata-storage-20261003/`다. [계측 소스](../../../artifacts/journal-metadata-storage-20261003/probe/Program.cs)는 기준 payload 읽기를 거부하는 fake snapshot으로 private state/sequence를 대조하고 `FILE_STANDARD_INFO.AllocationSize`를 측정한다. 두 입력은 JP의 [실제 실행과 checkpoint seal](journal-payload-compression.md)에 묶인 저장 저널이며 원본 evidence를 읽기 전용·write/delete 비공유 핸들로 고정했다.

| 입력 | SHA-256 | sequence / frame 수 |
| --- | --- | ---: |
| A, `a-after-compact/encoded` | `97ec3db788345d2a558b9d5a16f386cc6afcdfa3b9e7e217af603f88c89c389c` | 27,970 / 23,718 |
| B, `b-collected-journal/encoded` | `9aa877ef0d640cae6122bccb418ef31483605a29746dab577451d7052226f39e` | 61,687 / 61,687 |

선행 Runtime은 `development @ 7f07cb7`의 SHA-256 `4f841493c324cbca64fc88595dc14e607e6a3498962a299ffde49bf06886bb43`, 후보는 `d100d2331fa059dc13ecadbd18f574e3b49b7371a5d1009cefe47ddfb8e69d17`다. Core는 양쪽 동일하다. 각 입력/Runtime 조합을 한 번 정리했고 같은 원본/identity hash를 전후 확인했다. 실패한 샌드박스 A 비교는 `baseline-a`에 보존하고 채택 표는 일반 사용자 권한의 `*-qualified`만 사용한다.

변경은 private Mutation의 `Offset=0`·`Destination=null`·`Replace=false` 생략, 명시적 정리/후보의 더 작은 현재 JSON 선택이다. 기본값을 이미 허용하는 reader 의미·overlay v2/SJR2/candidate v3/workspace v2·Core/CLI/schema·raw/Brotli 저장 길이·callback당 승인/개별 durable flush는 그대로다. 저장된 저널은 read/ensure에서 재작성하지 않는다. 같은 버전의 더 작은 표현이며 구버전 migration/fallback은 없다.

## 같은 입력의 실제 파일 할당

| NTFS journal allocation B | 원 입력 | 선행 Runtime 정리 후 | 후보 정리 후 | 이번 추가 감소 |
| --- | ---: | ---: | ---: | ---: |
| A, 이미 정리됨 | 38,043,648 | 38,043,648 | 37,191,680 | 851,968 (2.24%) |
| B, checkpoint seal | 116,260,864 | 54,697,984 | 53,215,232 | 1,482,752 (2.71%) |

A의 decoded/stored payload는 전후 동일하다. 저널 논리 길이 38,042,178→37,188,067B, frame 비용 11,406,654→10,552,543B다. B의 선행/후보 정리 후 stored payload는 모두 35,242,050B, live 논리 범위는 172,504,063B, 완전히 미참조 stored payload는 24B다. 선행/후보 frame 비용 19,453,670→17,970,281B, 논리 journal 54,695,720→53,212,331B다. 차이는 메타데이터이며 기존 payload 제거 효과와 중복 합산하지 않는다.

이번 추가 journal 할당 감소는 A/B 합계 **2,334,720B**다. B에서 기존 정리가 이미 줄이는 61,562,880B는 이 변경의 효과가 아니다. 원 입력 기본값 2,194,498B와 B 최종 1,483,389B의 논리 감소가 다른 이유는 기존 정리가 skip frame을 병합하기 때문이다. 이 표본으로 일반 Library의 절감률을 약속하지 않는다.

게시 직후 이전/현재 journal의 파일 할당 합은 A 76,087,296→75,235,328B, B 170,958,848→169,476,096B다. 검증한 소유 이전 세대와 intent를 `CompleteCompaction`으로 회수한 뒤 잔여 journal 할당은 위 후보 정리 후와 같다. 예상 겹침량은 A 75,230,245B, B 169,470,542B이며 각각 **두 저널의 논리 길이 합**과 일치한다. NTFS allocation·제어 파일·디렉터리/MFT·VSS·시스템 cache·관측 사이 순간 최고치까지 포함하는 예산이 아니다.

[선행 A](../../../artifacts/journal-metadata-storage-20261003/baseline-a-qualified/measurement.json), [후보 A](../../../artifacts/journal-metadata-storage-20261003/candidate-a-qualified/measurement.json), [선행 B](../../../artifacts/journal-metadata-storage-20261003/baseline-b-qualified/measurement.json), [후보 B](../../../artifacts/journal-metadata-storage-20261003/candidate-b-qualified/measurement.json)에 전체 seal·private state·payload/frame 비용·단계별 비용을 보존한다. 비교 복사본·실패 세대·바이너리/로그는 별도 계측 artifact이며 제품 workspace 비용에 더하지 않는다. 원본 Library/Git/독립 결과와 공유 VSS는 이번 비교 대상이 아니고 전체 물리 peak/VSS 작업별 귀속은 unknown이다.

## 정확성·비용·실패

A의 private state hash는 `2e28ef2352868301e4fa2ae033404077ca93bbc11ebe003bc39f6c9380e3f043`, B는 `690e9fd91225c876a610ab54f3202cd9ed32a32a6d041debee0bec45b2d053b0`로 양쪽 Runtime·정리 전후 동일하다. 내용/metadata·논리 extent·path changes·sequence를 포함하는 Runtime state hash다. payload 기준 읽기 0, 원본 저널/identity hash 불변, 이전 세대의 원본 seal 일치와 회수 완료를 확인했다. 실제 mount나 독립 결과의 새 실행 증거를 대신하지 않는다.

| 정리 비용 | 선행 A | 후보 A | 선행 B | 후보 B |
| --- | ---: | ---: | ---: | ---: |
| elapsed ms | 5,570 | 5,027 | 7,905 | 15,827 |
| 관리 누적 할당 B | 270,208,800 | 303,181,296 | 669,452,680 | 751,697,112 |

추가 직렬화로 관리 누적 할당이 증가했다. 이것은 관리 heap peak·전체 RAM/native codec memory가 아니다. 후보 B는 필수 전체 빌드와 실행 시간이 겹쳐 시간 대조의 조건이 같지 않다. 소수 단일 표본이며 시간 개선·정체 원인·SLA·메모리 절감을 주장하지 않는다. 공간 우선의 한정 개선으로 채택하고 추가 타이밍 반복을 열지 않는다.

- 샌드박스의 최초 A `File.Replace` 접근 거부로 후보/intent를 보존했다. 일반 사용자 권한의 새 소유 비교 폴더에서 완료했으며 삭제/속성 변경이나 관리자 설치로 우회하지 않았다.
- 시험 프로젝트의 기본 MSBuild 실행은 자식 reference target에서 진단 없는 실패를 남겼다. `baseline-test-build.log`를 보존하고 `-m:1`의 일반 빌드로 focused fixture를 준비했다. 필수 build는 격리 밖 일반 사용자 권한으로 원 스크립트를 실행했다.
- [수정 전 시험](../../../artifacts/journal-metadata-storage-20261003/baseline-tests.trx)은 3건 중 2건 실패, 이미 생략된 literal 현재 frame은 통과했다. [수정 후 focused](../../../artifacts/journal-metadata-storage-20261003/after-focused-tests.trx)는 23건 통과했다. 독립 literal SJR2·기본값 명시/생략·비기본값 offset/replace rename·raw/encoded 내용·개별 flush·read 중 불변·정리 크기/예산과 이전 세대 보존을 확인한다.

## 판정

필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 경고/오류 0, 자동 시험 **884 passed·0 failed·1 skipped**다. [build 로그](../../../artifacts/journal-metadata-storage-20261003/build.log)와 [같은 실행 TRX](../../../artifacts/journal-metadata-storage-20261003/automated.trx)를 보존했다. 기존 candidate/compaction 게시 중단·동일 intent 재개·손상 보존·개별 ACK/flush와 checksum/tail·원본/peer/회수 guard 회귀가 포함된다. 현재 private JSON의 표현만 바꾸므로 공개 schema/Core/fake 계약의 형식 변경은 없다. UTF-8 88파일·변경 문서의 로컬 링크와 diff 검사를 통과했다. [최종 감사](../../../artifacts/journal-metadata-storage-20261003/final-audit.json)는 입력/바이너리/현재 source hash·전후 내용/sequence·소유 이전 세대 회수·파일 할당과 전체 시험을 대조한다.

JM-01~04를 **scoped-complete-with-limitations**로 마감한다. 채택 근거는 이미 정리된 A를 포함한 두 실제 저널의 더 작은 NTFS 파일 할당과 내용/복구 회귀다. 추가 관리 누적 할당·단일 시간 표본의 한계를 공개하며 메모리/시간 개선으로 읽지 않는다. 새 Unity/VSS A/B·설치/grant/볼륨·원본 반영·대형/ARM/원격/OS 장애·전체 peak/VSS 귀속/전체 RAM은 NotRun 또는 unknown을 유지한다. 기존 JP/M0·SF-02/03 Partial·C: 보류의 상태를 변경하지 않는다.

위 NotRun은 JM의 오프라인 단위 실행 범위다. 이후 같은 JM Runtime의 실제 T: A/B·현재 승인 쓰기/정리·독립 결과/회수는 별도 [PS-03~05 직접 실행](physical-storage-completion.md)으로 확인했다. JM의 전후 파일 비교와 관리 할당 값을 PS의 전체 공간/RAM 개선으로 환산하지 않는다.
