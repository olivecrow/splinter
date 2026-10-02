# 독립 범위 payload codec 검증

2026-10-03의 이 선행 단위는 [저장공간 계획](../plans/storage-priority-and-editor-modes.md#범위-payload-codec과-다음-제품-연결)의 **제품 연결 전 공통 codec**이다. `IWorkspacePayloadCodec`·Runtime의 raw/Brotli 표현·literal fake/적합성·개발용 발견 정보와 output schema를 추가했다. 당시 후보는 journal/workspace 미연결이며 이 자격은 독립 codec에 한정한다. 이후 제품 연결은 [별도 JP](journal-payload-compression.md)가 소유한다. 현재 block/byte schema와 버퍼 수명은 [기능 문서](../workspace-payload-codec.md)를 따른다.

## 자동 검증의 경계

`WorkspacePayloadCodecTests`는 별도의 literal fake와 Runtime에 같은 공통 계약을 적용한다. 빈/작은/4KiB/64KiB 범위·최대 1MiB 반복 입력·원본 불변·buffer prefix/부분/EOF 읽기·음수/상한/버퍼 겹침을 확인한다. 비압축 입력은 같은 codec의 raw 표현을 넘기지 않고 raw read는 scratch를 대여하지 않는다. 한 codec의 동시 8 caller도 자기 입력·encoded/output bytes를 유지한다.

checksum 손상과 잘림/추가 bytes, 유효 checksum을 가진 잘린 Brotli stream·stream 뒤의 bytes·너무 작거나 큰 선언 길이도 성공으로 읽지 않는다. 알려지지 않은 version/encoding·reserved/지나친 선언 길이는 빈/EOF 읽기에서도 거부한다. 출력은 실패 전 값과 같으며 decoded scratch는 성공/실패 뒤 반환한다. 256회 작은 반복 read의 누적 관리 할당은 이미 준비한 pool을 사용한 32KiB 이내의 경계다. native codec state·pool 잔류·시스템/Editor 메모리를 포함하지 않는다.

`ContractAndCliTests.PayloadCodecDiscoveryKeepsJournalIntegrationUnqualified`는 일반 CLI의 codec 비노출과 개발 CLI의 `standalone-not-journal-integrated`·실제 크기 상수·현재 output schema를 확인한다. schema는 integrated stage 주장도 거부한다. 새 codec command를 광고하지 않는다.

첫 전체 시험에서 개발 capabilities의 신규 property를 response schema에 빠뜨려 기존 발견 시험 5개가 실패했다. strict schema를 완화하지 않고 해당 property의 정확한 standalone 계약을 추가했다. 초기 sandbox restore가 구체적 오류 없이 종료된 로그와 일반 사용자 권한에서 실행한 초기 빌드/시험도 원시 증거에 남긴다. 최종 필수 빌드 결과는 이 수정 이후 후보로 판정한다.

## 같은 실제 Bee 입력의 codec 대조

입력은 [후보 선정](library-block-compression-feasibility.md)의 같은 네 파일/전체 SHA-256/길이·mtime, 합계 **51,859,898B**다. `artifacts/storage-foundation/library-payload-20261003/codec-probe/`는 이번 대조용 자료이며 새 제품 명령/일반 harness가 아니다. `codec-measurement.json`의 Runtime DLL SHA-256은 `cb00008f42eed073ecadb3ce1e5926ec9007b5fe86783f034b760f3af3b770f7`다. 실제 `BrotliWorkspacePayloadCodec`를 4KiB/64KiB/1MiB profile로 스트리밍해 **12개 파일/profile**의 전체 복원 hash·요청별 내용·작은 부분 읽기를 검증했다. source file을 read-only로 고정해 초기/최종 hash/길이·mtime가 같았고 encoded payload 파일은 저장하지 않았다.

아래는 metadata 가정이 아닌 **codec v1의 실제 48B overhead를 포함한 논리 byte 표현**이다. 비교 기준인 raw 표현에도 블록마다 같은 48B가 있다. 이 standalone 표현과 현재 product journal frame을 동일한 형식으로 읽지 않는다.

| 블록 | raw codec 표현 B | 선택한 codec 표현 B | raw codec 대비 감소 | encode ms | full decode ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| 4KiB | 52,467,722 | 8,448,264 | 83.90% | 555.28 | 399.96 |
| 64KiB | 51,898,010 | 5,025,381 | 90.32% | 336.47 | 195.33 |
| 1MiB | 51,862,346 | 2,229,642 | 95.70% | 236.53 | 248.81 |

4KiB 표현은 bare 입력 bytes와 비교하면 83.71% 작다. 그러나 실제 callback 경계·해당 private 데이터·frame/외부 journal metadata·파일 할당·VSS·전체 공간은 측정하지 않아 이 비율을 작업공간 절감률로 표시하지 않는다. profile마다 한 번 실행한 CPU 구간이며 읽기/hash·durable flush·Editor 지연의 전후 대조가 아니다.

전체 profile의 작은 부분 읽기는 55/10/8회였고 모두 정확했다. 최대 scratch 요청/실제 pool 배열은 각각 4,096/65,536/1,048,576B였다. 대여/반환 수는 25,381/1,598/110회로 각각 같았다. caller의 고정 byte 배열은 별도로 3,145,783B다. 작은 요청이 현재 profile의 블록 길이에 맞춰 scratch를 빌리며 파일 전체/누적 입력을 메모리에 유지하지 않는 근거다. 7B read도 그 압축 블록 전체를 복원하므로 read amplification은 남는다.

thread 누적 관리 할당은 profile 합계 425,344/91,288/1,575,024B이며 pool 준비 비용을 포함한 표본이다. native codec state·pool 잔류/상주/전체 RAM은 미측정이다. 최초 전체 복제·read hydration·snapshot 비교 읽기 없이 실제 파일을 한 범위씩 읽어 대조한 근거이며 Editor/provider 메모리 절감은 주장하지 않는다.

## 최종 판정

최종 `pwsh ./scripts/build.ps1`의 locked restore·Release는 경고/오류 0, **총 865개 / 성공 864 / 실패 0 / 선택적 ReFS 1 skip**이다. 최종 실행에서는 이 shell의 `DOTNET_PROCESSOR_COUNT=4`만 설정했다. `build-final.log`·`tests.trx`와 초기 실패의 `build-normal-user.log`·`initial-tests.trx`를 보존하며 18개 codec 시험과 2개 발견 시험 모두 통과했다. 실제 입력 대조의 Runtime DLL hash는 최종 빌드와 같다. 문서 UTF-8·로컬 링크·diff 검사를 통과했다.

**이 독립 codec 단위는 scoped-complete-with-limitations로 마감한다.** 이 단위에서 실제 product journal·replay/compaction/candidate/독립 결과·seal/budget·native 중단/회수·NTFS 절감은 NotRun이었다. 당시 workspace 수명주기·서비스/등록·원본/peer/보류 snapshot은 변경하지 않았다. 이후 한정 제품 자격은 [JP](journal-payload-compression.md)로 구분하며 SF-02/03 전체 Partial과 전체 peak/VSS 귀속 unknown을 유지한다.
