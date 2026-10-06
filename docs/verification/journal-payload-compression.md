# 압축 저널 제품 연결 검증

2026-10-03의 [확정 범위](../plans/journal-payload-compression.md)에 따라 현재 SJR2/overlay v2/candidate v3의 제품 연결·실제 T: Plastic A/B·저널 할당을 **scoped-complete-with-limitations**로 마감했다. [독립 codec](workspace-payload-codec.md)의 논리 표본과 이전 M0/PC/UH를 이번 형식의 native 통과로 승계하지 않는다.

| 관문 | 상태 | 근거 |
| --- | --- | --- |
| JP-01 공통 frame/fragment와 단일 승인 | Passed | Core 저장/복원 길이·논리 fragment/literal 경계, raw/압축 callback당 한 frame·한 durable flush·한 sequence. 새 `JournalPayloadTests`와 기존 codec fake/적합성 |
| JP-02 재생/손상/정리/후보·예산 | Passed with limitations | overlap/resize/rename/delete·부분 읽기·손상·동일 intent/후보·정리 예산 회귀, 기존 실제 자식 중단 시험의 압축 입력/ACK. 실제 A 정리/재연결 뒤 source fingerprint/네 mounted 표본 동일. 새 native 강제 종료·OS/전원 행렬 미실행 |
| JP-03 단일 형식/schema/발견 | Passed | SJR2·overlay v2·candidate v3, 이전 요청/저장 identity와 완전한 미지원 frame 보존/거부. workspace v2·서비스 protocol 유지. 최종 발견과 실제 응답 40개를 현재 embedded schema로 검증 |
| JP-04 실제 T: A/B/독립 결과/회수 | Passed with limitations | 동일 ensure·정확한 project/PID/ticks·loaded A/B·A 정리/회수 중 같은 B 지속·후속 b-two·두 독립 결과/마지막 소유 snapshot 회수. 원본/등록/기존 records 보존 |
| JP-05 NTFS 파일 할당·메모리 비용 | Passed with limitations | 동일 metadata/decoded 입력의 실제 journal raw 대조·파일 할당·논리/저장/유효/대체 payload, 관리 누적 할당/소유 메모리 표본 공개. 전체 peak/VSS 귀속/native codec RAM unknown |
| JP-06 전체 회귀·문서 | Passed | 최종 locked restore·Release 경고/오류 0, 881 passed·0 failed·1 skip. 현재 embedded schema의 실제 응답/발견 40개 유효. 현재 계약/스킬/계획/검증표·UTF-8/로컬 링크/diff 대조 |

첫 전체 빌드는 성공했으나 이전 버전·raw 물리 길이를 전제한 회귀 59개가 실패했다. 이전 형식의 지원을 재개하지 않고 시험 입력/현재 schema/개발 도구를 갱신했다. 집중 회귀의 첫 229개 중 3개는 바뀐 입력 oracle/압축 이전 크기·예산 전제였다. 원시 실패를 `artifacts/payload-integration-initial.trx`·`payload-integration-focused.log`에 보존한다.

수정한 Runtime 후보의 필수 빌드 881 passed·0 failed·1 optional skip와 집중 64개 통과는 `artifacts/payload-integration-build-final.log`·`artifacts/payload-integration-focused-final.xml`에 남긴다. 이후 발견 정보의 문서 경로 상수를 schema에 빠뜨린 6개 실패를 `artifacts/payload-integration-qualified-build.log`·`artifacts/payload-integration-qualified-failed.trx`에 보존하고 정확한 상수를 수정했다. strict schema·내구성·회수 gate를 완화하지 않는다.

최종 빌드/TRX는 `artifacts/payload-integration-qualified-final-build.log`·`artifacts/payload-integration-qualified-final.trx`다. `response-schema-validation-final.json`은 현재 embedded catalog로 실제 JSON 40개를 검증했다. 최종 source/CLI의 native 모듈 hash는 실행 manifest와 동일하다. Codec의 작은 부분 읽기에는 전체 bounded block 검증/복원 비용이 남으며 일반 지원·전체 공간/RAM 절감으로 확대하지 않는다.

## 고정 후보와 실제 수명주기

근거 root는 `artifacts/journal-payload-compression-20261003/`다. `manifest.json`이 요청/예산/바이너리 hash를 고정한다. Native 구현 8개 모듈은 이후 Host/Protocol의 발견 정보 수정 뒤에도 hash가 일치하며 `final-audit.json`으로 대조했다. 다른 Runtime의 실행 자격으로 승계하지 않는다.

| 항목 | 실행 범위 |
| --- | --- |
| 환경 | Windows x64/NTFS·기존 서비스, Unity `6000.3.22f1`, CLI `1.0.0-beta.5`, Pipeline `0.8.0-exp.1`. 설치/grant/볼륨/UAC 변경 없음; 일반 workspace 명령 callerElevated=false |
| 원본/home | `T:\plastic-fixture-20261002` / `T:\pt1002`, plastic/unity/snapshot/ntfs-snapshot, 각 journal budget 17,179,869,184B |
| A | `jp-t-a-20261003`, id `fea2876f791abc7be2653c30`, digest `49bb16148b82b7582e6ef09e`, Editor 73380 / ticks `639265613835498043`, snapshot `{25C177CB-3A94-4A31-A8BD-56D09EC3ACBD}` |
| B | `jp-t-b-20261003`, id `45f908d5d62a2508a8320569`, digest `b0afbff5a7013c7063b86b0a`, Editor 89680 / ticks `639265614221381303`, snapshot `{EFA84F28-1AC2-4B9F-9300-9B26F4EA79AB}` |
| 기준 | 양쪽 `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`, 62파일 / 245,523B. 초기 payload/journal/read cache 0, 같은 ensure의 id/root/digest/기준/snapshot 유지 |

반환 경로의 `Assets/SplinterJournalProbe.cs`를 a-one/b-one으로 작성하고 외부 CLI의 실제 project/PID/정확한 시작 ticks·loaded 값/MVID를 확인했다. A a-two의 재컴파일 동안 B 값/PID/MVID는 유지됐다. A 정상 stop·1GiB 겹침 예산의 정리·같은 snapshot/provider 재연결 뒤 mounted 표본과 source fingerprint를 확인했다. A checkpoint/release 뒤 같은 B가 b-two로 편집·재컴파일했다. B marker는 2,388B 반복 주석을 포함해 압축된 source payload의 독립 결과 수집도 확인했다. 이 작은 표본을 실제 Library 입력의 개선 근거로 대신하지 않는다.

| 결과와 보호 | 근거 |
| --- | --- |
| A 결과 | `sha256:75e008cbcd7b9d002e76a1c8cb25583d760d2705cb257978ec5e74dd90b43d8e`, 64파일 / 245,665B, a-two bytes 동일 |
| B 결과 | `sha256:af8b9127c0e5f8dd9c9341fd66681ad0713fe43c911a235cf6591d413e8e782b`, 64파일 / 247,970B, b-two marker hash `5895036a025c924db4219176c3840eddeb335672128871d7d27962784f6b5aad` |
| 회수 | `21-a-final.json` / `22-b-final.json`: released·두 정확한 snapshot missing·mount/provider/delta false·handles/provider working/private·소유 application activeProcesses/working/private 모두 0. 회수 뒤 baseline/result 모든 객체 SHA-256/길이·marker를 `independent-results.json`으로 확인 |
| 기존 보호 | T: 원본 65개/C: 원본 66개 선택 파일·관리 root bytes/hash 동일, Plastic 등록 5개 동일, 기존 T: records 335파일 bytes/hash 동일. 원본 Editor 68624 / ticks `639265412574256363` 유지. records의 절대/상대 경로를 정규화해 대조하고 원시 전후 근거를 유지 |

기존 C: 보류/비소유 blocker는 회수 대상으로 호출하지 않았다. 이번 ordinary 관측은 UAC 없는 범위이며 전체 보호 ledger/C: VSS inventory와 볼륨별 used/allocated를 다시 측정하지 않았다. 두 소유 T: snapshot의 missing을 전체 볼륨 snapshot 0이나 C: 보류 해소로 표현하지 않는다.

## 같은 raw 표현의 실제 공간

각 worker는 기존 Bee 네 파일 합계 51,859,898B를 `Library/SplinterPayloadQualification`에 64KiB 스트림으로 명시적으로 작성했다. 신규 고정 쓰기 입력이며 source clone·읽기 hydration·copy-up fallback이 아니다. 원본은 읽기 전용이고 네 actual mount SHA-256/길이가 모두 원본과 일치했다. A 정리/재생 뒤에도 같다. 저널에는 Unity 자신의 Library/관리 파일 쓰기도 포함된다.

도구는 T:의 실제 안정된 journal을 C:의 한정 계측 artifact로 읽어 **동일 frame metadata와 decoded callback 입력**의 SJR2 raw 표현을 만들었다. outer checksum·codec 48B 비용을 포함한다. encoded/raw 대조는 같은 C: NTFS에서, 실제 journal 할당은 T:에서 별도 측정했다. private replay/state hash/sequence는 같고 fake base는 payload 읽기를 금지한다. Actual mount와 독립 결과는 별도 native 근거다. 이전 바이너리의 전체 성능 비교가 아니다.

| journal | T: 실제 할당 B | C: encoded 대조 B | 같은 raw 대조 B | encoded 대조 감소 |
| --- | ---: | ---: | ---: | ---: |
| A 정리 전 | 43,778,048 | 43,741,184 | 188,538,880 | 76.80% |
| A 정리 후 | 38,043,648 | 38,043,648 | 163,287,040 | 76.70% |
| B 최종 checkpoint seal | 116,260,864 | 116,260,864 | 297,238,528 | 60.89% |

A 정리의 실제 할당 감소는 5,734,400B다. raw 대조와 정리 효과를 더해 전체 workspace 절감률을 만들지 않는다. 현재 raw header는 이전 형식보다 frame당 8B 크며 과거 83% 모델/이전 journal 형식의 실측 대조로 표현하지 않는다.

| 비용 B | A 정리 후 | B 최종 |
| --- | ---: | ---: |
| 누적 decoded payload | 151,877,141 | 268,742,908 |
| stored payload | 26,635,524 | 87,765,774 |
| live 논리 범위 | 151,856,365 | 172,504,063 |
| 참조되는 stored block | 26,635,516 | 35,283,117 |
| 완전히 미참조 stored payload | 8 | 52,482,657 |
| header/metadata/outer checksum | 11,406,654 | 28,492,437 |

부분적으로 유효한 압축 block은 저장 비용 전체를 유지하며 stored에서 논리 live를 빼지 않는다. B의 대체 payload는 공개하고 범위 밖의 두 번째 정리를 추가하지 않았다. 최종 records 전체 3,193,528 allocated B 안의 독립 payload 1,557,752B를 중복 합산하지 않는다. 이전 결과도 포함한다. 공유 runner는 14파일/2,294,448B로 이전 내용 키 7파일을 보존했다. 원본 Library 1,852,496,112 allocated B·30,748파일은 별도 기준선이다. Evidence 약 1.263GB에는 실패/성공 raw 대조 복제 8개도 포함되며 제품 workspace 비용과 분리한다. MFT/디렉터리·kernel cache·순간 최고치를 파일 할당으로 주장하지 않는다.

## 메모리·시간·실패

표본 writer buffer는 65,536B, 관리 누적 할당 A/B 3,866,144/3,955,112B다. Elapsed 57,944/7,285ms에는 서로 다른 import·cache·durable flush가 포함돼 성능 우위 대조가 아니다. Raw 생성·두 private replay/state-hash 비교의 관리 누적 할당은 A 전/후 194,709,360/169,363,424B·B 393,757,088B다. 전체 파일 buffer나 peak가 아니다.

Provider working/private 최대 관측 표본은 A 140,529,664/107,995,136B·B 148,160,512/115,240,960B다. Application 자식 합계 working/private 최대 표본은 A 3,390,918,656/3,883,597,824B·B 6,494,679,040/7,402,373,120B다. Working set의 공유 페이지가 중복될 수 있으며 전체 RAM/codec 비용으로 귀속하지 않는다. Native Brotli memory·전체 peak/RAM 절감·VSS 작업별 귀속은 unknown이다. Codec buffer 상한/pool 반환의 적합성과 메모리 절감을 구분한다.

일반 권한 elapsed ms는 ensure A/B 59,092/30,583, A stop/compact/checkpoint/release 35,639/17,907/10,458/13,597, B stop/checkpoint/release 30,965/10,226/13,311이다. 준비·관측·외부 도구·실패·계측 비용도 raw cost에 남기며 한 묶음을 시간 SLA로 표현하지 않는다.

- 최초 eval/reload 뒤 최초 eval의 5초 main-thread timeout은 각 동일 대상의 읽기 전용 query만 한 번 재시도했다. 쓰기/recompile을 timeout으로 반복하지 않았다.
- B reload 중 recompile_status의 기존 7802 연결 실패를 보존했다. 같은 소유 PID/heartbeat와 exact-path status로 completed를 확인했으며 새 7801을 고정하지 않았다.
- B 최초 비교는 stop 뒤 입력 변경을 검출했다. 추가된 첫 frame은 `Library/ArtifactDB-lock`의 payload 0·448B metadata였고 이후 sequence도 증가했다. 원인은 미확정이다. 실패 artifact `b-final-journal`을 보존하고 정상 checkpoint sequence 61,687 / 116,258,211B / hash `9aa877ef0d640cae6122bccb418ef31483605a29746dab577451d7052226f39e`에 묶인 비교만 채택했다. Release의 unmount 후 seal guard도 통과했다. Editor 종료를 저널 불변으로 읽지 않는다.

새 A/B·원본 integrate/check-in·MCP·Play/Test/build·대형/ARM/OS/전원 장애·flush/group commit·새 backend·자동 모드/큐·전체 메모리 최적화는 실행하지 않았다. SF-02/03 전체 Partial과 기존 C: 보류/unknown을 유지한다.
