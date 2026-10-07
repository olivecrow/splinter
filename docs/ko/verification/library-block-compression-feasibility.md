# Library 범위 payload 압축의 후보 선정

[English](../../verification/library-block-compression-feasibility.md)

2026-10-03에 Unity CLI A/B 비용과 Plastic fixture의 Bee 파일 4개를 읽기 전용으로 분석해 **한정된 범위 payload의 독립 블록 압축**을 선정했다. 이 문서는 선행 후보 모델의 근거를 소유하며 당시 제품 journal/NTFS 할당은 미측정이었다. 이후 독립 codec은 [별도 검증](workspace-payload-codec.md), 실제 제품 연결은 [JP](journal-payload-compression.md)를 따른다. 후보 모델을 제품 전체 절감/지원 자격으로 승계하지 않는다.

## 기존 비용에서 고른 대상

제품 후보는 `development @ 6edc3de`이며 프로그램 소스는 선행 UH의 `4e262b7`에서 바꾸지 않았다. [UH 실행](unity-cli-handoff.md)의 보존된 JSON에서 아래 값을 다시 계산했다. final은 회수 직전까지의 누적 저장량이며 현재 파일 할당이 아니다.

| 표본 | 누적 journal B | 누적 payload B | frame/metadata B | frame/metadata 비중 | write당 평균 저장 payload B |
| --- | ---: | ---: | ---: | ---: | ---: |
| A ready | 123,356,406 | 116,220,215 | 7,136,191 | 5.79% | 8,405.9 |
| B ready | 123,151,262 | 116,035,959 | 7,115,303 | 5.78% | 8,420.6 |
| A final | 136,298,076 | 123,511,979 | 12,786,097 | 9.38% | 4,897.8 |
| B final | 136,120,355 | 123,349,610 | 12,770,745 | 9.38% | 4,898.5 |

누적 payload가 대부분이므로 frame 메타데이터 축소보다 payload를 먼저 선택했다. write당 평균은 원래 callback 크기 분포가 아니다. 현재 private 앞뒤 재사용 뒤 저장한 bytes의 평균이며, 이 값만으로 전체 쓰기가 4KiB라고 판정할 수 없다. A/B final의 write 내부 시간은 각각 124.92/128.22초, 그 안의 flush 비중은 99.08/98.86%다. 이 중첩 계측을 Editor 벽시계와 더하거나 공간 작업의 이유로 개별 durable flush를 바꾸지 않는다.

ready 때 provider working set은 A/B 121,761,792/120,512,512B, private bytes는 80,338,944/79,257,600B였다. Editor 소유 프로세스 그룹의 working set은 3,290,206,208/3,072,770,048B, private bytes는 3,736,121,344/3,588,870,144B였다. 서로 다른 시점의 그룹 표본이며 동시 peak·시스템 물리 RAM 또는 절감률이 아니다. 저장 우선순위를 유지하고 메모리·Editor 동시성은 이번 구현 대상으로 선택하지 않았다.

회수 후 records·독립 결과와 공유 runner 비용은 UH의 기존 장부를 따른다. records에 이미 포함한 result payload를 다시 더하지 않는다. UH 저널은 회수됐으므로 현재 worker의 Library 유효량/대체 기록 비중을 계산할 수 없다. [선행 실제 Library 분류](resource-efficiency-followup.md)의 106,388,937B 유효 Library와 큰 Bee 경로는 후보를 찾는 근거이며 UH의 같은 값으로 승계하지 않는다.

## 고정한 읽기 전용 입력

원본은 `T:/plastic-fixture-20261002`의 현재 Library다. 다음 4개만 읽었으며 이전 worker의 callback trace·저널·독립 결과 payload와 구분한다. 전체 소스나 Library를 복제하지 않았다.

| `Library/Bee/` 아래 경로 | 논리 bytes | 입력 SHA-256 앞 12자리 |
| --- | ---: | --- |
| `1900b0aE.dag` | 23,432,080 | `a9cd54908001` |
| `1900b0aE.dag.json` | 17,762,603 | `1a32020ca67b` |
| `1900b0aE.dag.payloads` | 7,154,032 | `565f29aa13f5` |
| `1900b0aE-inputdata.json` | 3,511,183 | `3b483045762b` |
| 합계 | 51,859,898 | 전체 hash는 원시 measurement |

새 로컬 증거 디렉터리 `artifacts/storage-foundation/library-payload-20261003/`의 `plan.json`에 질문·입력·종료 조건을 먼저 고정했다. `Program.cs`/`Probe.csproj`는 이번 입력용 분석 자료이며 새 제품 명령이나 일반 harness가 아니다. .NET `10.0.12`의 BCL `BrotliEncoder`로 4KiB/64KiB/1MiB 독립 블록·quality 1/4·window 22의 **24개 파일/profile 조합**을 실행했다. 블록 사이 dictionary/cache를 유지하지 않는다.

4개 파일의 read-only `FileShare.Read` 핸들을 모든 profile 동안 유지해 다른 쓰기/교체와 공존하면 열기를 거부했다. 파일·profile마다 전체 SHA-256, 길이·mtime 일치와 모든 압축 블록의 정확한 복원을 확인했다. reparse file은 거부했다. 원본 payload 복사와 압축 payload 파일 저장은 0B이며 workspace·Editor·VSS·서비스/grant·등록을 변경하지 않았다. 이 분석의 잠깐 유지한 파일 핸들을 제품의 원본 쓰기 잠금 정책으로 읽지 않는다.

## 모델 크기와 후보 선택

각 압축 블록에 **추가 metadata 64B를 가정**하고, 그 합이 raw보다 작은 경우만 압축을 선택한 모델이다. 기존 공통 frame overhead는 비교 양쪽에서 제외했다. 64B는 새 제품 형식의 실제 크기가 아니다. 아래는 네 입력의 합계이며 논리 payload 크기다.

| 독립 블록 | quality | 모델 저장 B | 모델 payload 감소 | encode ms | decode ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4KiB | 1 | 8,650,872 | 83.32% | 332.93 | 213.84 |
| 4KiB | 4 | 7,839,787 | 84.88% | 2,636.34 | 236.62 |
| 64KiB | 1 | 5,038,085 | 90.29% | 113.12 | 95.86 |
| 64KiB | 4 | 4,154,551 | 91.99% | 756.84 | 114.31 |
| 1MiB | 1 | 2,230,458 | 95.70% | 60.37 | 106.27 |
| 1MiB | 4 | 1,587,525 | 96.94% | 505.76 | 81.37 |

각 행은 한 번 실행한 CPU 구간의 합이다. 읽기/hash·Editor 대기·durable flush를 제외하며 profile 순서/cache와 native codec 비용을 통제한 성능 대조가 아니다. 큰 블록의 높은 비율을 실제 작은 callback에 적용할 수 없다. 제품 read는 작은 범위에도 블록 전체 복원이 필요할 수 있어 read amplification은 별도 관문이다.

질문 단계의 선정 기준인 **4KiB 모델 aggregate 25% 이상 감소**를 quality 1이 충족했다. quality 4의 이득은 이 모델에서 1.56 percentage points에 그치므로 다음 단위는 quality 1 후보부터 다룬다. 이 선정 기준은 제품 합격 기준/시간 SLA가 아니다. raw fallback은 압축이 더 커지는 블록의 저장 선택이며 파일 전체 copy-up이나 snapshot hydration과 무관하다.

명시적 재사용 byte 배열은 총 3,145,990B였다. profile loop의 thread 누적 관리 할당은 첫 4KiB/q1 합계 16,640B, 뒤 조합은 0B였다. 배열의 최초 할당과 native codec state·pool/상주/전체 RAM을 포함하지 않으므로 메모리 절감 근거가 아니다. 다음 단위는 제품이 요청 크기에 맞춘 버퍼를 쓰는 경계를 따로 확인해야 한다.

## 판정과 재사용 한도

**읽기 전용 후보 선정 단위는 마감했다.** `measurement.json`은 전체 입력 hash/시각과 24개 roundtrip·크기/CPU 표본, `summary.json`은 UH 4개 원시 JSON의 hash·재계산과 probe/바이너리 hash를 보존한다. `summarize.py`는 profile 수·모든 복원·선정 기준을 확인했다. 제품 코드/schema/상태·저널 형식은 바꾸지 않았다.

후속 [독립 codec](workspace-payload-codec.md)과 [제품 JP 연결](journal-payload-compression.md)을 구분한다. 실제 저널 연결은 이 후보 분석에서 NotRun이었으며 새 형식의 부분 읽기·replay/compaction/candidate/seal·예산·중단·원본/peer 보호를 이 모델로 통과 처리하지 않는다. 별도 JP의 직접 근거만 한정 자격으로 사용한다. SF-02/03 Partial·대형 비용/전체 peak/VSS 귀속 unknown과 기존 C: 보류를 유지한다.
