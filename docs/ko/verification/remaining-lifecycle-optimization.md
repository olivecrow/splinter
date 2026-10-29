# 남은 수명주기 최적화 검증

[English](../../verification/remaining-lifecycle-optimization.md) · [진행 범위](../plans/remaining-lifecycle-optimization.md) · [기계 판독 요약](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)

앞선 행의 문단은 해당 근거 당시 상태를 보존하며 현재 행 표와 마지막 MX10 마감 절이 최종 상태를 소유한다. MX-01–10 프로그램을 마감했다. MX-01은 진단 귀속으로 마감했고 MX-02–04·MX-06/07·MX-08B·MX-09는 미채택했다. MX-05 A·guard 적용 B2와 MX-08A를 유지한다. 최종 전체 gate와 동일 입력 Git/Unity 반영·폐기 4회의 정확성은 통과했지만 실제 전체 시간의 큰 개선이나 개별 변경의 인과 절감은 입증하지 못했다.

**초기 MX-01 기록과 당시 진행 상태(역사적 요약):** **MX-01, 2026-10-28: 진단 원인 분석 완료.** 수집한 초기화 구간은 Rider와 Burst가 대부분을 차지하며, 구체적인 제품 검토 경로를 MX-02/04/08에 연결했다. 성능 수정 완료나 전후 개선을 뜻하지 않는다. **MX-02·MX-03의 두 후보·MX-04는 측정 후 폐기·제거했다. MX-05는 최종 일반 NTFS 복원 비교·전체 gate 뒤 A와 게시 수 제한 guard를 적용한 B2를 채택해 마감했다. B1 무조건 선택과 앞선 실패 기록은 그대로 보존한다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08A는 잠정 유지하고 MX-08B는 폐기·제거했다. MX-09는 실제 Git/Unity 최종화 ABBA가 사전 큰 절감 기준에 미달하여 폐기·제거로 마감했다. MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.** 전체 프로그램은 계속 진행한다.

## 입력·수집·해석

프로그램 시작 커밋은 `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`, 고정한 측정 제품 소스는 `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`다. Windows/NTFS의 GUI 작업공간 하나에서 Unity **6000.3.22f1**, 전용 fixture 기준 `f121a5adc9c4248994774dea03843504c949f09a`를 사용했다. 외부 launch decorator가 지원되는 profiler 옵션을 전달했으며 제품 실행 계약과 설치 패키지는 변경하지 않았다. 추가 fixture profiler 코드는 수집 전 일반 NTFS에서 warmup했다. 계측과 fixture 작업은 시간에 영향을 줄 수 있다.

원시 기록은 **567,910,230바이트**, SHA256 `8e6ca48b7d382f3f3abaf52a616f90fb5168580dba1f04b41855bb2e0a260a49`다. 큰 원시 데이터는 ignored `artifacts/remaining-optimization-20261028/profile-01/`에 유지한다. 추적 JSON에는 원시 sample·로컬 계정 경로·실행 인수·자격증명 대신 정제한 hash·범위별 결과·한계를 기록한다.

최초 후위 순서 export는 **512MiB** 상한에 도달해 확인한 15,628,788개 중 1,356,547개 sample만 기록했고 큰 초기화 조상을 출력하지 못했다. 출력된 작은 메서드는 누락 비용의 상한이 아니었다. 첫 별도 분석은 frame 0 Main Thread를 완료한 뒤 다른 스레드의 이름 없는 marker에서 중단했다. 해당 근거는 보존한다. 수정한 `summary-export-02`는 **로드된 15,628,788개 전체**, 두 frame·216 thread view를 순회하고 raw 불변을 검증했다. 정상 종료 코드 2는 완전한 capture가 아닌 partial summary를 뜻한다.

| 수정 요약의 범위 | 결과 |
| --- | ---: |
| 순회 / 확인 sample | 15,628,788 / 15,628,788 |
| 보존한 개별 행 | 4,019 |
| 개별 metadata 없이 집계한 GC.Alloc sample | 15,076,698 |
| 임계값 미만으로 세부 정보 생략 | 536,742 |
| 제한된 reservoir에서 제외/퇴출한 큰 후보 행 | 11,329 |
| marker identity가 unknown인 sample / bucket | 434 / 245 |
| 초기화 context의 unknown marker sample | 0 |
| hierarchy 오류 / unknown self 계산 / marker 이름 충돌 | 0 / 0 / 0 |

**요약은 partial이며 원시 capture 손실 여부는 unknown이다.** 전체 순회가 수집 이전·buffer·domain reload·frame 경계에서 기록 손실이 없었음을 증명하지 않는다. 요청한 조상 13개, 초기화 marker 14개, 초기화 직접 자식 1,944개는 이전 메인 스레드 분석의 모든 기존 필드와 일치한다. **대상 행 1,971개가 동일**하다. 이전에 `OverlayCanvas.Initialize`로 표시된 무관한 invalid-ID 행 두 개는 unknown으로 바로잡았고 클래스 원인 분석에 사용하지 않는다.

Self 시간은 완전한 직접 자식을 제외하지만 대기와 계측되지 않은 작업을 포함하므로 CPU 사용 시간은 아니다. 수정 export의 이름 있는 marker 합계는 여러 스레드/frame을 합하므로 시작 경과 시간으로 읽지 않는다. 아래 메인 스레드 합계는 다른 view를 읽기 전 frame 0 Main Thread의 5,844,396개를 정확히 완료한 이전 분석을 사용하고, 동일 raw·수정한 marker identity와 대조했다. 중첩·여러 스레드 시간을 더하지 않는다. 과거 BD-01의 49.364초와 MX-01 초기화 시간은 **최적화 전후 한 쌍이 아니다**.

## 초기화·I/O 결과

| 주 ProcessInitializeOnLoadAttributes의 겹치지 않는 부분 | 포함 시간(초) |
| --- | ---: |
| RiderScriptEditor 정적 생성자 | **12.503079936** |
| BurstLoader 정적 생성자 | **9.06021888** |
| 나머지 자식과 wrapper/외부 자체 비용 | **1.163828224** |
| **전체 외부 구간** | **22.72712704** |

Rider와 Burst는 직접 형제 호출이며 **21.563298816초 / 94.87912298834934%**를 차지한다. 그 자식/self 시간을 다시 더하지 않는다. 보존한 non-GC 직접 자식 288개의 합은 22.722389288초로 wrapper의 전체 직접 자식보다 **0.0000367초**만 작다. 소유 WorkspaceBridge 생성자는 **0.080975696초**(self 0.066170396초)이므로 생성자 자체 최적화만으로 큰 시간을 줄일 수 없다.

Rider 소스는 솔루션 부재 시 초기화를 `ProjectGeneration.Sync`, 프로젝트 파일별 `FileIOProvider.WriteAllText`, `LastWriteTracker.UpdateLastWriteIfNeeded`, 저장 상태 `LastWrite` setter의 `Save(true)`로 연결한다. 수정 profile metadata는 Rider 아래에서 `Library/com.unity.ide.rider/PersistedState.asset` 반복 연산 **135개 / 포함 3.4662601초**를 확인한다. 보존한 메인 스레드 전용 상세에는 136개 / 3.4721689초와 중첩 쓰기·Close가 있으며, 수정 reservoir에서는 다른 스레드 대기가 일부 작은 항목을 밀어냈다. 서로 겹치는 구간이므로 추가 시간으로 합하지 않는다. Rider의 **self 8.842435832초** 전부를 솔루션 생성·I/O·flush로 귀속하지 않는다.

Burst의 큰 직접 `JitCompilerService.CompileInternal` 호출 두 개는 **5.785662976초**, **2.420623872초**다. 컴파일 서비스 관리 명령도 진입점을 공유하므로 이름만으로 전부 사용자 Burst 함수 컴파일 시간이라고 판단하지 않는다. 설치 패키지나 private callback을 변경·비활성화하지 않았다.

Frame 0 Main Thread는 **File.Open 2,640개 / self 12.951948684초**, **File.Close 1,993개 / self 0.8690874초**를 기록했다. 앞부분의 정확한 경로 근거는 workspace ScriptAssemblies 열기에 **5.048252412초**, PackageCache 열기에 **4.740115272초**를 대응하며 이를 나머지 구간으로 확대하지 않는다. 수정한 전체 스레드 File.Open 합은 **16.856342584 thread-seconds**이며 메인 스레드나 수명주기 시간이 아니다. Open 비용은 provider 접수·stat/proof·kernel/cache/antivirus·loader 동작으로 아직 분해하지 않았고 Close도 flush와 같은 의미가 아니다.

별도로 보존한 BD-01 Bee 입력 비교는 **실행 연산 292개**를 모두 설명한다. 기존 ILPP 응답 67개와 configuration은 세션 pipe만 달라지고 68번째 ILPP는 새 bridge다. 기존 Csc main 응답 74개는 동일하며 기존 Csc 연산은 재사용한다. bridge의 새 source/asmdef가 compilation을 요청하지만 모든 기존 ILPP를 직렬로 막는 것은 아니다. 기존 asset-import 개별 로그 시간은 작고 개별 무효화 원인은 unknown이다. 같은 실행의 의존성 원인 분석이며 속도 개선 시험이나 이전 프로세스에 묶인 graph 재사용 승인이 아니다.

## 제품 후속 연결과 마감

| 후속 | 근거가 있는 작업 범위 | 유지하는 경계 |
| --- | --- | --- |
| MX-02 | 소유 Editor 전용 precompiled bridge가 source/asmdef 삽입에 따른 불필요한 compilation/reload를 피하는지 시험한다. | DLL import도 reload를 요구할 수 있다. 컴파일 구간이나 22.72712704초 초기화가 사라진다는 보장은 없다. 아래 측정에서 폐기했으며 큰 이득·채택은 없다. |
| MX-04 | 큰 virtual-Library Open을 provider 연산과 대응한 뒤 확인한 제품 비용을 줄인다. | namespace/내용/identity 증명과 reader/context 수명을 유지한다. 미검증 gate 제거나 폐기한 mapped-cache/native Bee 경로를 되살리지 않는다. 안전한 비마지막 Close 후보는 아래에서 측정 후 폐기했으며 Open 최적화는 유지하지 않았다. |
| MX-08 | 생성 프로젝트 파일·반복 상태 저장을 실제 부하로 삼아 제품 쓰기/metadata/replacement 처리를 측정한다. | 순서 보장 mutation·영속 ACK·제한 drain·정확한 복구를 유지한다. 패키지 patch·전역 IDE 설정 변경·가짜 생성 상태·내구성 완화를 하지 않는다. |

동일 진단 작업공간은 코드 0으로 정상 종료하고 명시적으로 폐기했으며 소스/Library 내용·수정 시각·속성을 완전히 복원했다. 외부 관측기가 처음에 root Editor와 asset worker 상태를 혼동해 같은 task를 복구했고 **103.72708079998847초** 복구를 별도 기록했다. 원본 외부 서버/설정은 복원하고 health를 확인했다. Package-free 분석 프로세스도 raw와 외부 상태를 보존했다. 에이전트가 수행한 진단·복구이며 제품 서버 관리 기능이나 중단 없는 수명주기 시간은 아니다.

MX-01은 진단 원인 분석만 마감한다. 일반 전후 개선·보편적 시작 배수·최종 반영/폐기 비교·새 Play/Test/build 자격·peak memory 절감·전원 장애 동작을 입증하지 않는다. MX-02/03/04의 별도 실측 폐기는 아래에 기록하며, 남은 후보 작업은 MX-05–09가 담당하고 MX-10이 유지한 조합의 요청된 전체 작업 과정을 검증해야 한다.

<a id="mx-02-precompiled-bridge-rejected"></a>

## MX-02: 사전 컴파일 bridge 폐기

**판정: 완료, 폐기·제거.** 시제품은 같은 소유 bridge를 Unity 6000.3.22f1 공개 reference assembly로 컴파일하고 Editor 전용 importer·암묵적 참조 없음·시제품 v3 binary/importer 소유 marker를 사용했다. 최초 Csc와 ILPP를 각각 한 번 줄였지만 나머지 컴파일 pipeline과 reload는 피하지 못했다. 새 제품 모드·소유 형식·binary 배포·Unity 버전 지원은 유지하지 않는다.

동일 입력 순서는 **전 / 후 / 후 / 전**, variant별 두 번이다. 두 빌드의 기록된 커밋은 `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`, 기준 제품 소스는 `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`다. 후보는 Unity application assembly와 그에 의존하는 workflow/CLI assembly가 달라졌고, 다른 runtime/storage binary는 동일했다. 입력은 fixture `f121a5adc9c4248994774dea03843504c949f09a`, **소스 파일 1,607개 / Library 파일 42,101개**이며 매 실행 전 전체 내용·수정 시각·속성을 확인했다. 초기 loaded runtime MVID는 네 번 모두 `a827452c-9e24-4558-855e-22035ade4dd1`로 동일했다.

단일 연속 스톱워치는 Library 준비·작업공간 생성·GUI 준비 완료·소스 편집 1회/명시적 refresh/loaded-code 확인·asset 확인·정상 종료·명시적 폐기·소유 회수·Library 복원까지 포함했다. 초기 준비 완료는 단계 구간이며 `unity-start` 명령 반환과 다르다. 단계 counter는 겹치거나 제어자 사이 시간이 빠질 수 있으므로 전체 시간을 합산해 만들지 않는다. 측정 실행에는 **Unity profiler와 제품 쓰기 진단을 사용하지 않았다**. 서버 관측과 정상 종료 후 제한된 Bee 수집도 연속 전체 시간에 포함하고 빼지 않았다.

| ABBA 실행 | 초기 준비 완료(초) | 편집·컴파일 1회(초) | 연속 전체 시간(초) |
| --- | ---: | ---: | ---: |
| 01-before | 102.91423130 | 26.19117240 | 307.97620330 |
| 02-after | 103.63832160 | 27.38300990 | 305.78099690 |
| 03-after | 102.69305180 | 30.39486990 | 298.89506030 |
| 04-before | 101.29306420 | 26.18818050 | 292.96616340 |
| **변경 전 평균** | **102.10364775** | **26.18967645** | **300.47118335** |
| **변경 후 평균** | **103.16568670** | **28.88893990** | **302.33802860** |
| 변경 후 − 전 | +1.06203895 | +2.69926345 | **+1.86684525** |

평균 전체 시간은 **0.62130592%** 늘었다. 네 번의 작은 관측에서 큰 절감은 없었으며, 보편적인 성능 악화를 증명한 것은 아니다. 사전 채택 기준은 초기 준비 완료 평균이 **15초 이상이면서 10% 이상** 감소하고 전체 수명주기도 줄며 편집/종료/메모리/호환성 비용이 이득을 크게 상쇄하지 않는 것이었다. 이를 충족하지 못했다. 전체에 포함된 서버 관측 평균은 **1.87500995→1.81646510초**, 종료 후 Bee 수집 평균은 **1.51918565→1.47541795초**다. 해당 비용을 공개하며 임의로 빼서 개선율을 만들지 않는다.

최초 로그는 일관되게 **Csc 1→0 / ILPP 69→68**을 보인다. 나머지 pipeline/reload와 약 22초 InitializeOnLoad 구간은 계속 발생했다. 이 연산 수는 해당 fixture의 결과이며 MX-01이나 과거 BD-01 수치/시간과 합쳐 하나의 인과 비교로 사용하지 않는다.

후보는 **집중 시험 37개 통과, 실패 0개, skip 0개**를 기록했다. 별도의 패키지 없는 일반 NTFS 최초/재시작 smoke에서 정확한 identity·잘못된 종료 token 거부·Editor 전용/암묵적 참조 없음·정상 종료 코드 0·DLL/importer 불변을 확인했다. 준비 시간은 **11.604772499995306초 / 4.023874499951489초**, 정상 종료 시간은 **0.8016131999902427초 / 0.6014807999599725초**다. 이는 정확성 검사이며 GUI benchmark 성능 결과가 아니다.

GUI 네 번 모두 코드 0 정상 종료·명시적 폐기·source/Library 내용/수정 시각/속성 및 source metadata의 정확한 복원·소유 자원 회수를 완료했다. 시험 Editor는 남지 않았다. 원본 외부 서버와 보관한 설정도 정확히 복원하고 health를 확인했으며 외부 재시작 영수증은 코드 0이었다. 에이전트의 실험 정리이며 제품의 서버 관리가 아니다.

변경한 제품/source/test 파일은 바이트 단위로 복원했다. 폐기 patch와 컴파일 binary/importer는 ignored 로컬 근거로만 보존하며 hash·절차/runtime/분석 영수증은 추적 JSON에 기록한다. 큰 실측 이득이 없는 후보를 미측정 배포·추가 Unity 버전 자격 부담까지 안고 유지할 이유는 없다. MX-02는 폐기로 마감한다. 남은 프로그램·최종 반영/폐기 조합·Play/Test/build·다른 Unity 버전·peak memory·전원 장애 자격의 완료가 아니다.

<a id="mx-03a-first-consume-retention-rejected"></a>

## MX-03A: 최초 소비 보존 정책 폐기

**판정: 측정 후 폐기·제거.** 선행 읽기 항목을 처음 성공적으로 사용할 때 후보는 MRU로 올리는 대신 삽입 순서를 유지했다. 진단 on/off 모두 성공한 게시에서 소비를 표시하고 이후 hit는 일반 MRU를 유지했다. 전후 모두 **reader 2개·warm worker 1개·256MiB/4,096-entry payload cache·128MiB 최초 proof window**와 전체 증명·메모리 상한·실패/취소 drain을 유지했다. 용량만 늘렸다가 폐기한 GR 재실행이 아닌 정책 시험이다. 고정 runtime 비교에서는 `Splinter.SourceControl.Git.dll`과 PDB만 달랐다.

같은 fixture 기준 `f121a5adc9c4248994774dea03843504c949f09a`의 **소스 1,607개 / Library 파일 42,101개**를 사용했다. 새 editorless `workspace.ensure`마다 독립 monotonic stopwatch를 사용하고, 공통 Library 준비/복원·각 private 편집/격리 확인·명시적 폐기는 별도로 측정했다. 실행 전후 전체 source/Library 내용·수정 시각·속성·Git metadata 검사는 측정 밖이다. 양쪽 모두 G1과 제한된 **0.5초** sampler를 사용했으며 메모리는 정확한 provider 프로세스 수명의 peak로 전체 시스템 peak RAM이 아니다. 최초 reader 2개 기준본은 build commit `24956f4`의 제품 `bea4acdb`다.

최초 진단 관측기는 물리 CRLF 보조 파일과 정규화된 LF Git blob을 비교했다. **Git blob 1,607개 전체**를 독립 검증하고 줄 끝만 다른 파일 네 개의 근거를 보존했다. 제품 내용 실패가 아닌 관측 불일치였다. 동일 진단 task를 정상 폐기하고 Library를 일반 NTFS로 복원했으며 **43.48075650003739초** 복구는 성능 결과에서 제외한다. 복구 후 새 고정 cohort `mx03a-measured`가 같은 runtime/source/Library로 전후후전을 실행했다. 최초 실패 검사·차이 기록·절차 보완·종료 증명은 보존한다.

| 새 editorless 실행 | Ensure(초) | 명시적 폐기(초) | Provider peak working set(bytes) |
| --- | ---: | ---: | ---: |
| 01-before | 51.02398000 | 9.27977430 | 737,333,248 |
| 02-after | 50.74656150 | 8.77316820 | 741,326,848 |
| 03-after | 50.31551940 | 8.31865440 | 745,742,336 |
| 04-before | 51.55086200 | 8.26941030 | 741,146,624 |
| **변경 전 평균** | **51.28742100** | **8.77459230** | **739,239,936** |
| **변경 후 평균** | **50.53104045** | **8.54591130** | **743,534,592** |

평균 ensure 절감은 **0.75638055초 / 1.47478765%**로, 정확성·자원 상한·상쇄할 수명주기/유지보수 비용이 없으면서 **10초 이상이자 15% 이상** 줄어야 한다는 사전 기준에 미달했다. private 격리 검사 평균은 **0.26199120→0.26230280초**였다. 공통 Library 준비는 **39.84370919992216초**, 최종 복원은 **22.63769159989897초**다. 공통 연산 한 번씩이며 variant별 전체 수명주기 측정으로 읽거나 ensure 절감에 합하지 않는다. provider 수명 peak private commit 평균은 **753,795,072→757,583,872 bytes**이며 역시 전체 시스템 peak가 아니다.

proved-read 재해독 bytes는 **305,237,082→11,628,972(96.19018373%)**, 사용하지 않은 선행 읽기 퇴출은 **227,704,832→0 bytes**로 감소했다. 전체 proof 해독 bytes는 variant별 평균 **2,787,059,626**으로 같았다. 큰 counter 감소가 큰 경과 시간 이득을 입증하지 못했으므로 96% 속도 개선 주장·중첩 counter 절감 합산·선택 정책 유지의 근거로 사용하지 않는다.

집중 시험은 **55개 통과**, 실패 **0개**, **조건부 비용 시험 1개**(`RepeatedPackedBackwardReadCost`) 미실행이다. 폐기한 범위의 전체 build gate는 실행하지 않았다. 측정 task 네 개 모두 명시적으로 폐기했고 소유 provider가 종료됐으며 task 경로/private delta가 없었다. 전후 증명 모두 전체 source/Library 내용·수정 시각·속성·Git metadata가 일치했고 최종 Library 복원도 입력과 같았다. 진단 task의 종료 증명과 이 네 번의 정상 종료를 구분한다. editorless 비교에서 Unity·MCP/서버·패키지·전역 설정 작업은 하지 않았다.

MX-03A 마감 시 제품 소스와 전용 시험 파일을 바이트 단위로 복원했으며 patch·원시 근거는 ignored로 보존한다. MX-03B는 A를 채택해 확대한 것이 아닌 아래 별도 측정 후보다. OS cache를 비우지 않은 Windows/NTFS 한 대의 variant별 두 관측은 GUI 시작/컴파일·반영·Play/Test/build·전체 수명주기·전원 장애 자격이 아니다.

<a id="mx-03b-reader-warm-concurrency-rejected"></a>

## MX-03B: reader/warm 동시성 후보 폐기

**판정: 측정 후 폐기·제거. A와 B 모두 폐기하여 MX-03은 완료했다.** B는 제한된 decoder session 4개와 최초 큰 파일 warm worker 최대 3개를 필수 index refresh와 함께 사용했다. 256MiB/4,096-entry payload·4MiB proof 제한은 유지하고 reader별 proof 선행 읽기 window를 **128MiB에서 64MiB**로 줄였다. 가장 큰 blob 두 개는 새 window보다 크다. A의 보존 정책은 포함하지 않았다. 전체 hash/alias 증명·자원 상한·실패/취소 drain은 유지했다. reader 수만의 독립 인과 효과가 아닌 이 후보 조합을 측정했다.

최초 setup은 `Platform.Windows`가 바이트 단위로 같아야 한다고 잘못 요구하여 native 실행이나 home 생성 전에 중단했다. 후보의 Git 내부 생성자/reference metadata 변경 뒤 의존 assembly가 재빌드됐다. Git·Platform.Windows·Workflows.Windows·CLI의 **DLL/PDB 8파일**이 달라졌고, **45파일** runtime 목록의 나머지는 같았으며 추가/삭제는 없었다. 원래 고정 출력과 setup 실패를 보존한다. 후속 비교에서 세 의존 DLL의 **1,703 / 374 / 380개 메서드**에 대해 이름/raw signature/raw IL/MaxStack/LocalVariablesInitialized 행이 같았다. 해석한 token 대상·exception region·local-variable signature·전체 metadata/resource/PDB/native code는 비교하지 않았으므로 **전체 PE나 동작 동등성을 증명하지 않는다**. 의존 재빌드는 소스/reference 변경과 부합하는 해석이며 모든 변경 byte의 원인을 증명하는 compiler trace가 아니다. 이 자격 확인은 읽기 전용으로 완료한 비교를 재사용했고 대상 DLL이나 새 native 시험을 실행하지 않았다.

새 `mx03b-measured` cohort는 같은 **소스 1,607개 / Library 파일 42,101개**, 입력 증명·editorless 측정 경계와 A의 **평균 ensure 10초 이상이면서 15% 이상 절감** 기준을 사용했다. 양쪽 모두 G1과 0.5초 관측기를 사용했고 Unity·원본 서버·패키지·전역 설정은 조작하지 않았다. A와 B는 각자의 before 실행이 있는 별도 비교이며 평균을 합쳐 하나의 이득으로 읽지 않는다.

| 새 editorless 실행 | Ensure(초) | 유효 reader 표본 | 표본의 최대 active reader | reader ≥3 표본 |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 60.68203910 | 33 | 2 | 0 |
| 02-after | 60.98883890 | 35 | 4 | 6 |
| 03-after | 61.23225740 | 35 | 4 | 7 |
| 04-before | 52.97522150 | 28 | 2 | 0 |

평균 ensure는 **56.82863030→61.11054815초**, **4.28191785초 / 7.53478982% 증가**하여 채택 기준에 미달했다. reader 4개 동시 사용은 관측했지만 최대치는 표본 값이지 절대 peak가 아니다. 약 **1초** provider 게시와 **0.5초** 관측 사이 구간은 놓칠 수 있다. 프로세스 시작 합계는 동시성 수가 아니며 ≥3 표본이 없다는 사실만으로 해당 동시성 부재를 증명하지 않는다.

Warm 평균은 **22.69030445→9.29738000초**로 줄었지만 index refresh는 **26.72628210→32.84063510초**로 늘었다. 겹친 구간 평균은 **22.67312135→9.28021300초**다. 동시에 실행하므로 warm 절감을 ensure에서 빼거나 구간 시간을 더하면 중복 계산이다. proved-read 재해독 평균은 **1,266,507,990.5→3,396,886,562**, 미사용 선행 읽기 퇴출은 **988,282,880→2,523,725,824 bytes**로 늘었고 전체 proof 해독은 **2,787,059,626**으로 같았다. 추가 작업의 근거이며 경과 시간 악화를 항목별로 나눠 측정한 결과는 아니다.

Provider 수명 peak working set 평균은 **736,632,832→748,523,520 bytes**, private commit은 **750,837,760→762,107,904 bytes**이며 전체 시스템 peak RAM이 아니다. 별도 폐기 평균은 **9.43170930→8.92076205초**다. 공통 Library 준비 **42.31323570001405초**와 복원 **22.951573599944822초**도 별도이며 variant별 전체 수명주기 결과가 아니다.

후보 빌드는 **경고 0개**, 집중 시험은 **80개 통과**, 실패 **0개**, **조건부 비용 skip 1개**를 기록했다. 폐기한 범위의 전체 build gate는 실행하지 않았다. 네 번 모두 전후 입력 증명에서 source/Library 내용·수정 시각·속성·Git metadata가 정확했다. 모든 task를 명시적으로 폐기하고 provider 종료·task/private-delta 경로 부재·최종 Library 복원 일치를 확인했다. B 마감 시 **source/test 7파일**을 바이트 단위로 복원했고 폐기 patch·근거는 ignored로 보존한다. reader 모드나 후보 정책은 유지하지 않았다. OS cache를 비우지 않은 variant별 두 관측을 실제 Unity/전체 수명주기/반영/Play/Test/build/전원 장애 자격으로 확대하지 않는다.

<a id="mx-04-nonfinal-close-rejected"></a>

## MX-04: 비마지막 Close 분리 후보 폐기

**판정: 완료, 폐기·제거.** 후보는 이미 비마지막인 context의 참조 수를 작은 context 잠금 안에서 줄이며 전역 mutation gate를 거치지 않았다. 마지막일 수 있는 Close는 mutation gate를 획득하고 context를 다시 확인한 뒤 최종 release를 했다. Open/stat/retain의 원자성·rename/delete/replace·영속 forget·cleanup purge·reader 수명 보호는 유지했다. 원자적 lookup/retain 계약 없이 Open의 바깥 gate만 제거하면 삭제/교체 경쟁이 생긴다. Runtime Stat은 여전히 저널 gate와 전체 검사를 사용하며 현재 Library 검사는 고정한 핸들을 확인하고 파일 내용을 hash하지 않는다. 추가로 제거 가능한 전체 파일 증명이나 MX-01 File.Open 전체 구간의 절감을 입증한 결과가 아니다.

기준본은 모든 Close가 기존 mutation gate를 사용하도록 유지하고 후보와 같은 opt-in Close/Stat 관측을 추가했다. 보존한 공통 계측 manifest는 wrapper를 제거하면 원본 소스가 재현됨을 증명한다. 그 manifest의 “not built”는 부모가 이후 빌드하기 전인 자료 생성 시점의 상태다. variant마다 **동일한 test DLL**을 사용했으며 SHA256은 `b44eaf844f40c69a3c6047937c4b9b0b2f395b3742da5daf3104f3117ba4c536`이다. Runtime 목록 **129파일** 중 Platform.Windows와 의존 Workflows.Windows/CLI의 **DLL/PDB 6파일**이 다르고 추가/삭제는 없다. 후보와 공통 진단의 제품 소스 변경은 Platform에만 있으며 다른 제품 소스는 바뀌지 않았다. 소스/runtime 출처 증명이며 **전체 PE나 동작 동등성 증명은 아니다**. 정확한 hash와 근거 경로는 추적 JSON에 기록한다.

Native 사례는 **dispatcher thread 8개의 WinFsp MountEx**, 결정적인 불변 memory basis 파일 8개, 실제 **16MiB** bounded 저널, **4KiB** 파일과 **reader마다 open/read/close 1,000회**를 사용했다. 같은 ID의 anchor/transient 쌍 외에는 reader별 파일을 분리했다. 혼합 사례는 인위적인 지연 없이 **512바이트 WriteThrough 동기 쓰기 512회**와 마지막 `Flush(true)` 1회를 추가했다. anchor/writer open과 준비 barrier는 준비 구간이며 worker 작업·정상 close·context drain은 측정했다. 부하 후 flush·정상 unmount·replay·setup은 별도 기록했다. 완전한 부하마다 스톱워치 하나를 사용하며 표의 합계는 **순차 실행**한 부하 5개의 스톱워치 합이다. 병렬 callback의 누적 시간이나 전체 제품 수명주기가 아니다.

| Native ABBA 실행 | 부하 5개 합(초) | 외부 process 경과 시간(초) |
| --- | ---: | ---: |
| 01-before | 5.87151450 | 6.89947690 |
| 02-after | 5.56908080 | 6.63975800 |
| 03-after | 5.42757730 | 6.41605430 |
| 04-before | 5.37714620 | 6.37993600 |

| Native 부하 | 변경 전 평균(초) | 변경 후 평균(초) | 절감(초) |
| --- | ---: | ---: | ---: |
| 순차 단일 handle | 0.86534285 | 0.87995220 | -0.01460935 |
| 같은 ID의 anchor와 transient | 0.23160400 | 0.20495600 | 0.02664800 |
| reader 2개 동시 실행 | 0.89727235 | 0.88197580 | 0.01529655 |
| reader 8개 동시 실행 | 1.28414960 | 1.23754220 | 0.04660740 |
| reader 8개와 영속 writer | 2.34596155 | 2.29390285 | 0.05205870 |
| **순차 합계** | **5.62433035** | **5.49832905** | **0.12600130** |

평균 절감은 **2.24028982%**로, 이후 동일 입력의 실제 Unity 시험에 진입하기 위한 사전 component 기준인 **10초 이상이면서 15% 이상**에 미달했다. 따라서 실제 Unity 시험은 하지 않았다. 혼합 부하 절감은 **0.05205870초 / 2.21907729%**였다. anchor 사례는 **매 실행에서 실제 비마지막 Close 1,000회**가 있었으므로 적용 가능성은 확인했지만 절감은 **26.648ms**에 그쳤다. 후보의 혼합 실행은 각각 **Close 8,001회**를 분류했으며 빠른 비마지막 경로는 **4회**뿐이었다. **7,961 / 7,980회**는 마지막 Close였고 **36 / 17회**는 기다린 뒤 재확인해야 비마지막이었다. native handle이 여러 개라는 이유만으로 적용 비중이 크다고 가정할 수 없다.

별도 직접 callback ABBA는 사례당 **reader 반복 합계 4,096회**, writer가 있으면 실제 저널에 **1KiB 동기 peer 쓰기 256회**를 사용했다. 별도 구성 요소 관측이며 native 결과에 더하는 시간이 아니다.

| Callback reader / anchor / writer | 변경 전 평균(초) | 변경 후 평균(초) | 절감(초) |
| --- | ---: | ---: | ---: |
| 1 / 없음 / 없음 | 0.05387970 | 0.05785250 | -0.00397280 |
| 1 / 있음 / 없음 | 0.02708575 | 0.02770980 | -0.00062405 |
| 2 / 있음 / 없음 | 0.03092805 | 0.03042445 | 0.00050360 |
| 8 / 있음 / 없음 | 0.02922030 | 0.03208575 | -0.00286545 |
| 1 / 없음 / 있음 | 0.59291610 | 0.58359345 | 0.00932265 |
| 1 / 있음 / 있음 | 0.56843345 | 0.56915665 | -0.00072320 |
| 2 / 있음 / 있음 | 0.57074180 | 0.56483050 | 0.00591130 |
| 8 / 있음 / 있음 | 0.57562165 | 0.57099385 | 0.00462780 |
| **순차 합계** | **2.44882680** | **2.43664695** | **0.01217985** |

Callback 합계 절감은 **0.49737491%**였다. Close 분류 경과 시간·Open/Security Stat 자식 시간·gate 대기는 구간과 스레드 사이에서 겹치므로 제거 가능한 시간으로 더하지 않는다. 기준본의 `close-rechecked-nonfinal`은 원래 gate 안에서 처리하던 비마지막 분기이며 기준본에 재확인을 추가했다는 뜻이 아니다. 이를 후보의 두 비마지막 분류 합과 비교한다. `close` mutation bucket은 gate를 획득한 Close 횟수이며 후보의 전체 Close 횟수가 아니다. 전체 횟수는 분류 합에서 얻는다. native 혼합 관측에는 마지막 Close의 gate 대기와 상당한 Security Stat 경과 시간이 남았다. Stat 경과 시간은 내부 저널 잠금 대기·proof 실행·kernel·scheduler 효과를 분리하지 않으므로 그것만으로 gate 제거나 원인별 시간 절감을 정당화하지 않는다.

두 빌드 모두 **경고 0개 / 오류 0개**였다. 집중 시험은 **84개 통과**, 실패 **0개**, **조건부 비용 skip 3개**, 시간 **15.552초**다. skip은 직접 Close 비용·native Close 비용·기존 telemetry 비용 진입점이며 앞의 두 개는 이후 별도 ABBA에서 명시적으로 실행했다. **native 사례 20개 전부** worker drain·정상 close/unmount·**열린 handle/보존 context 0개**와 같은 사례의 variant 사이 **예상 파일 9개 전체 hash replay 일치**를 확인했다. private 저널 identity와 시각은 당연히 다르며, 내용/수명 검증이지 저널 파일 자체의 바이트 일치나 전원 장애 시험이 아니다.

변경한 기존 source/test 3파일은 바이트 단위로 복원하고 후보 전용 시험 4파일을 제거했다. 후보 잠금·진단 hook·선택 모드는 남기지 않았다. 폐기 source/patch·시험 정의·원시 실행은 ignored 근거로 보존한다. 이 행 마감 시 기존 `bin/Release`에는 폐기 후보가 남아 있어 사용 전 재빌드가 필요했다. 소스 복원만으로 binary가 갱신되지는 않는다. MX-04에서는 원본 프로젝트·Unity Editor·외부 서버·패키지·전역 설정을 조작하지 않았다. OS cache를 비우지 않은 한 대의 variant별 두 관측은 일반적인 Unity 절감을 입증하지 않는다. **MX-04는 폐기로 마감한다.**

<a id="mx-05a-fixed-topology-restore-provisional"></a>

## MX-05A: 이전 고정 구조 root 복원 후보

**이전 후보 기록: 큰 구성 요소 절감으로 당시 잠정 유지했고 최종 후보는 아래 별도 비교 뒤 채택했다.** A는 Library root를 한 번 이동하고 안정된 파일을 그대로 유지하며 정확한 삭제 leaf를 stage하고 같은 경로의 private 범위·truncate·확장·새 leaf를 반영한다. 디렉터리 rename/삭제/생성은 B가 담당하며 bulk와 leaf 관측은 아래에 별도로 기록한다. 확인했던 제어 영수증 게시 중단 문제는 수정했다. 정확한 canonical 제어 파일 stem의 일반 `.GUID.tmp`만 무시하고 보존하며 권한 근거로 채택하거나 삭제하지 않는다. unknown stem·잘못된 이름·디렉터리·reparse는 계속 거부한다. 이후 소스에서 취소·재개 집중 시험을 통과했다. 새 leaf의 identity 영수증 전 생성은 별도의 보존·거부 경계다. 최종 후보 비교·전체 gate의 별도 완료 결과는 아래에 기록한다. 안정된 proof 재사용은 구현하지 않은 제안이며 추가 실측 이득이 아니다.

순차 일반 NTFS 비교는 고정 metadata의 **10,000 × 1,024-byte 파일**(**입력 10,240,000 bytes**)과 **1KiB 덮어쓰기 네 번**(**합계 4KiB**), **17 bytes**로 truncate, 마지막 세 바이트 쓰기를 포함한 **2,049 bytes** 확장, 새로운 sparse **2,049-byte** leaf 하나와 삭제 leaf 하나를 사용했다. 결과 mutable/new 파일 일곱 개에 영속 범위 완료 처리를 하며 이 입력에는 디렉터리 구조 변경이 없다. fixture는 실제 bounded 저널을 사용한다. 전체 `ManagedDirectoryRestore` 호출의 단일 스톱워치에는 계획 게시·내용/identity 증명·metadata·범위 flush·최종 증명·회수가 포함된다. fixture 생성과 독립 최종 검증은 그 밖이며 외부 프로세스 경과 시간은 같은 구간이 아니다.

| 일반 NTFS ABBA 실행 | 전체 복원 (초) | 누적 관리형 할당 (bytes) | Root 이동 | 살아남은 파일 이동 |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 60.5269537 | 312,879,848 | 0 | 9,999 |
| 02-after | 21.1528534 | 340,946,856 | 1 | 0 |
| 03-after | 39.3249137 | 358,742,640 | 1 | 0 |
| 04-before | 99.1948066 | 312,902,408 | 0 | 9,999 |

전체 복원 평균은 **79.86088015→30.23888355초**, 절감은 **49.62199660초 / 62.13554935%**였다. 정확성·복구·자원 검토를 전제로 사전 시간 기준인 **10초 이상이면서 15% 이상**을 통과한다. 두 variant 모두 실행별 편차가 크며 각각 두 관측이고 OS cache를 비우지 않았다. 평균 누적 관리형 할당은 **312,891,128→349,844,748 bytes**로 늘었다. 이는 복원 구간에 할당한 누적량이며 현재 메모리·프로세스/시스템 peak RAM이나 CPU 측정이 아니다. 일반 속도 배수나 Unity 수명주기 절감으로 해석하지 않는다.

네 번 모두 동일한 test DLL을 사용했으며 SHA256은 `47f5321c6738bb3b0c3e9760b2493962d3dbb90b975d354dd613cedf6be79e5f`다. 고정 runtime 목록은 각각 **129파일**이며 Platform.Windows와 의존 Workflows.Windows/CLI의 **DLL/PDB 6파일**이 다르고 추가/삭제는 없다. Runtime·Git·Unity adapter DLL은 일치한다. 보존한 소스 snapshot과 runtime hash는 게시 중단 수정과 B 구현 전의 실측 A 버전을 나타낸다. 이후 변경의 자격이나 전체 PE 동등성을 입증하지 않는다.

전체 논리 입력 hash는 `56e86c20804ff26d4439596813b380eeacbde19712e23f93c2169f7235bc23fc`, 기대 결과 hash는 `c78f0fdf65744a8f3d5db8d15e988c7449477c85fc7f1cb7de799b53d58bc66d`다. 완료한 각 실행은 모든 최종 파일의 내용/길이·속성·생성/접근/수정 시각, 정확한 트리 구성 목록, 살아남은 파일 NTFS identity, 이전 basis·private delta·삭제 leaf stage의 부재를 독립적으로 비교했다. 제품의 최초 payload 복사량은 0이었다. 후보 두 번은 원래 root/디렉터리 identity 보존도 관측했으며 기준본은 새 디렉터리를 생성한다. 따라서 디렉터리 identity 일치는 기준본의 요구가 아니다. 비용 시험 네 번 모두 통과해 소유 fixture 정리를 허용했다.

수정 전 집중 시험은 **228개 통과**, **실패 0개**, **조건부 비용 skip 2개**였다. 최초의 잘못된 filter는 시험을 하나도 선택하지 않아 검증 근거가 아니며 기록된 집중 실행 전에 바로잡았다. skip은 sparse 복원과 root-delta 복원의 opt-in 비용 시험이고 이 ABBA에서는 후자만 추가 실행했다. 기록된 집중 시험은 새로 확인한 게시 중단 수정 전의 결과다. 게시 중단 수정과 집중 안전 검증 뒤 A를 잠정 유지했으며 최종 후보 채택은 아래 별도 결과를 따른다. 이 구성 요소 시험에서는 사용자 원본 프로젝트·mounted provider·Unity Editor·외부 서버·패키지·전역 설정을 조작하지 않았다. 전원 장애·최종 조합 성능·전체 반영/폐기 과정은 미자격이다.

원시 protocol·runtime/source 목록·네 번의 result/TRX·closure·analysis는 ignored `artifacts/remaining-optimization-20261028/mx05a-cost/`와 상위 폴더에 유지한다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)에 정확한 hash·이전 잠정 판단·별도 최종 채택을 기록하며 기존 원시 관측은 보존한다.

<a id="mx-05-safety-progress"></a>

## MX-05 최종 비교 전의 안전성 진행 기록

A의 게시 임시 파일 수정은 구현했고 완료된 집중 재실행에 포함했다. 게시되지 않은 일반 `.GUID.tmp`는 정확한 canonical 제어 파일 stem일 때만 무시하고 보존한다. 내용은 복원 권한의 근거가 되지 않으며 이름으로 회수하지 않는다. completion 쓰기 후 취소 시 임시 파일을 보존하고 시험한 저널 모드에서 새 취소 토큰으로 같은 intent 재개에 성공했다. unknown stem·잘못된 GUID·디렉터리·reparse 항목은 계속 보존·거부한다. 앞선 A ABBA는 보관한 수정 전 후보의 결과이며 안전성 결과로 그 시간을 다시 측정한 것으로 읽지 않는다.

| 별도 진단·검증 실행 | 소스 단계 | 통과 | 실패 | 조건부 비용 skip | runner 경과 시간(초) |
| --- | --- | ---: | ---: | ---: | ---: |
| 최초 집중 | B 공유·fixture 수정 전 | 277 | 68 | 3 | 211.296 |
| Narrow2 | B root 공유·부모 기준 native 보강 | 21 | 0 | 0 | 21.399 |
| Focused2 | A 임시 파일 수정·B 보강; 새 leaf helper 전 | 358 | 0 | 3 | 277.072 |
| Newleaf3 | 이후 A/B 정확한 부모 기준 새 파일 helper | 18 | 0 | 0 | 24.175 |

선택 범위가 겹치고 소스도 다르므로 고유 통과 시험 수로 합산하지 않는다. focused2의 **361개** 선택은 358개 통과와 opt-in 비용 skip 3개다. 보강 Release 빌드는 **51.39초**, 새 leaf Release 빌드는 **45.42초**였으며 각각 **경고 0개 / 오류 0개**였다. 빌드 시간은 성능 절감이나 대기 중인 필수 전체 gate의 근거가 아니다. 최초 실패 runtime(`runtime-mx05b-failed1`)·TRX/log·소스 snapshot은 보존한다. 68개 실패 메시지는 stopped-reader 계약 오류 **14개**, 소유 파일 공유 오류 **53개**, 오래된 fresh-route 기대 **1개**로 분류된다. 68개 각각의 원인을 독립 재현했다는 뜻은 아니다. 기존 general 경로 시험은 정지된 읽기 전용 저널에서 현재 v2 plan을 명시적으로 고정해 새 B 경로가 general 복구·cursor 검증을 우회하지 않게 했다. 이 수정과 실패 진단은 정상 비용 측정 코호트에서 제외한다.

Narrow2 native 메시지는 공유 설정이 모든 reparse 변환을 막는다고 가정하는 대신 다음을 입증한다.

| 투입 경계와 접근 권한 | 사례 수 | 실제 결과 | 확인한 보호 |
| --- | ---: | --- | --- |
| 최종 빈 root; GENERIC_WRITE `0x40000000` | 3 | Win32 **32**, 공유 위반 | 투입 차단, 일반 복원 완료 |
| 최종 빈 root; FILE_WRITE_ATTRIBUTES `0x100` | 5 | **0**, junction 전환 성공 | 제품 거부, 외부 tree·원본 파일/stage/저널 보존 |
| 부모 기준 topology install/create 직전; 위 두 접근 권한 | 4 | **0**, junction 전환 성공 | 제품 거부, 외부 tree와 정확한 plan/intent/cursor 보존 |
| Newleaf3: A 기존 빈 부모·B 영수증이 있는 빈 부모; FILE_WRITE_ATTRIBUTES `0x100` | 2 | **0**, junction 전환 성공 | 정확한 부모 기준 생성이 외부 tree로 향하지 않으며 file/write/verified 영수증 게시 없음 |

`PinDirectoryAgainstDataWrites`는 이제 data-write/GENERIC_WRITE 배제를 설명하며 속성 쓰기만의 전환을 차단한다고 주장하지 않는다. topology rename과 디렉터리 생성은 정확히 유지한 부모 핸들을 사용한다. A/B 새 파일은 이제 `CreateOwnedFile`을 사용한다. 부모에 상대적인 leaf 하나, 대체 없는 `FILE_CREATE`, share **0**, 타입 제한과 `FILE_CREATED` 확인을 적용한다. native 호출 전후 부모의 타입·identity·경로를 검사하고 반환된 동일 파일 핸들을 stream으로 쓴다. 성공한 생성은 여전히 identity 영수증 전에 `Flush(true)`를 수행한다. 실패는 핸들만 닫고 영수증 없는 새 항목을 삭제하지 않으며 재시도도 이를 채택하지 않는다. occupied-target 시험은 외부 파일의 bytes와 identity를 보존한다. 새 hook은 부모 확인 후 native 생성 직전에 투입해 이전 절대 경로 `RequireAbsent`→`CreateNew`의 특정 틈을 검증한다.

외부 tree 비교는 정확한 구성 목록·타입·속성·생성/수정 시각·NTFS identity·파일 hash를 검사하며 마지막 접근 시각은 제외한다. native topology 투입은 정확한 plan/intent/cursor bytes를 보존한다. 최종 root 사례는 원본 파일 identity/길이/내용과 stage/저널 존재를 입증하며 모든 제어 bytes나 전체 저널 hash를 주장하지 않는다. 시험한 고정 경계의 근거이며 가능한 모든 악의적 실행 교차를 증명하지 않는다. 영수증 없는 항목은 보존·거부하고 unknown 항목 생성 후 자동 복구 성공을 약속하지 않는다. 새 전원 장애·Unity 자격은 없다.

ignored `mx05b-tested2-source.json`은 focused2의 보관 파일 **18개**를 고정한다. 이후 `mx05b-cost/sources.json`은 새 leaf helper/시험과 leaf 비용 fixture를 포함한 보관 파일 **19개**를 고정한다. 최초 시도에서 동결한 두 비용 runtime manifest는 각각 **129파일**이며 동일 test DLL SHA256 `622a6d35145912c88d51c9eabc34dc6d621d94ddee8aa9cc634ef81eff75e05f`를 사용한다. Platform.Windows/Workflows.Windows/CLI의 DLL/PDB 6파일이 다르다. 정확한 manifest/source hash와 선택 runtime hash는 [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)에 기록했다. 진행 중인 대형 비용 payload를 다시 scan하지 않고 보관 source hash와 동결 test/Platform DLL을 확인했다. 전체 PE 동등성이나 이전 집중 시험 소스와 이후 후보의 동일성을 뜻하지 않는다.

최초 bulk 비용 시도(`mx05b-cost/01-before`)는 fixture 생성 중 `MissingMethodException`으로 실패했다. 시험 identity helper가 새 인자 5개 `DirectoryHandle`을 참조했지만 동결한 이전 baseline에는 해당 signature가 없었다. **복원에 진입하지 않았고 후보도 실행하지 않았으며 비교 시간은 없다.** stdout/stderr·TRX·외부 process-cost 기록은 준비 진단으로 보존한다. 두 binary에서 같은 native identity를 측정하도록 공통 fixture를 바로잡았으며 아래 별도 동결 cost2 코호트는 정상 완료했다. 제품 복원 실패로 분류하거나 프로세스 시간을 복원 시간으로 쓰거나 정상 ABBA에 섞지 않는다. 위 runtime hash는 최초 시도 코호트의 근거이며 덮어쓰지 않고 보존한다.

이 안전성 단계만으로 최종 지원을 선언하지 않는다. 후속 최종 채택에 맞춰 AGENTS·구현 계약·수명주기 지도를 함께 갱신했다.

<a id="mx-05b1-bulk-topology-screen"></a>

## MX-05B1: 정상 bulk 비교에서 시간 screen 통과

별도 `mx05b-cost2` 코호트는 바로잡은 공통 native identity fixture와 새 공통 test assembly를 사용하고, 제품 DLL은 실패한 준비 시도와 동일하게 동결했다. 제품 복원 네 번을 모두 완료했다. 이전 `mx05b-cost/01-before` 준비 실패는 보존·제외하며 프로세스 시간을 복원 관측으로 쓰지 않는다. 공통 fixture Release 빌드는 **21.78초**, **경고 0개 / 오류 0개**였으며 전체 gate 결과가 아니다.

고정 일반 NTFS 입력은 **10,000 × 1,024-byte 파일**, **10,240,000 bytes**이며 최초 **9,993파일 subtree**(직속 9,991개와 중첩 leaf 2개)를 포함한다. 중첩·개별 예외를 먼저 분리하며 남은 주 이동 단위에는 **9,989파일**이 있다. 디렉터리 reparent, 개별 leaf/subtree 이동·삭제, 중첩 디렉터리/파일 생성, 고정 metadata와 overwrite/truncate/extend를 함께 수행한다. 한 스톱워치가 plan/증명·영속 범위 쓰기·최종 검증·회수를 포함한 전체 제품 복원을 측정한다. fixture 준비와 독립 최종 assertion은 제외하며 외부 프로세스 시간은 별도다.

| 정상 B1 ABBA 실행 | 전체 복원(초) | 누적 관리형 할당(bytes) | Root 이동 | Unit stage/install 이동 | 살아남은 파일 이동 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 01-before | 82.4396773 | 312,928,224 | 0 | 0 / 0 | 9,997 |
| 02-after | 73.0181388 | 496,938,616 | 1 | 9 / 6 | 0 |
| 03-after | 68.5058357 | 496,930,456 | 1 | 9 / 6 | 0 |
| 04-before | 88.6892374 | 312,917,440 | 0 | 0 / 0 | 9,997 |

전체 복원 평균은 **85.56445735→70.76198725초**, 절감은 **14.80247010초 / 17.29978844%**로 사전 시간 screen인 **10초 이상이면서 15% 이상**을 넘었다. 평균 누적 할당은 **312,922,832→496,934,536 bytes**, 즉 **184,011,704 bytes 증가**했다. 복원 구간의 누적 관리형 할당이며 peak RAM·살아 있는 유지 메모리·CPU 사용량이 아니다. stage 횟수는 실행 경로를 설명하며 시간이나 더할 수 있는 절감·독립 인과 측정이 아니다. B1은 실행마다 새 디렉터리 identity 영수증 2개를 게시했으며 최종 증명·정확한 회수는 측정 호출에 포함한다.

네 번 모두 복원 후 최종 파일 bytes/길이·속성·생성/접근/수정 시각·정확한 구성 목록/대소문자·살아남은 파일 identity를 확인했다. basis·delta·stage는 없어졌고 초기 payload-copy 수치는 0이었다. 후보는 살아남은 디렉터리 identity도 보존했으며 general baseline은 새 디렉터리를 만들었다. 공통 논리 입력 hash는 `d20337772adb801b36dbdd52956ca24caaf341587a72f05d3bdf14beb4fd6b5c`, 최종 기대 결과 hash는 `3d3c71d0404bc5d25ce35eb4c8d61cf3759a67ce25611e2797d8ab0707b7f631`이다. 각 result/TRX가 통과한 뒤 fixture 정리를 허용했다.

동결 runtime 목록은 각각 **129파일**이며 공통 test DLL SHA256 `5f48c958907790828c211ded8fe85cda491a6f3a3aa4db4e6b086dbb3eeaa948`를 사용한다. Platform.Windows/Workflows.Windows/CLI DLL/PDB 6파일이 다르다. 후보 source archive **19파일**과 네 번의 result/runtime identity는 앞선 집중 시험의 18파일 소스와 별도로 고정한다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)는 canonical 분석·protocol·runtime/source manifest·네 result/TRX hash·closure를 보존한다. 전체 PE 동등성이나 앞선 A 후보의 재측정이라고 주장하지 않는다.

**B1의 큰 구성 요소 시간 절감 screen이며 최종 채택은 아니다.** variant별 두 관측·한 대·초기화하지 않은 OS cache의 한계를 유지한다. 큰 공유 subtree와 예외를 섞은 입력으로 개별 rename leaf 비용을 확정하지 않으며 아래 완료된 별도 ABBA는 동일 동결 B1 runtime의 무조건 topology 선택을 폐기했다. 이 역사적 B1 단계에서 B2는 inventory 반복 조상 검사와 최대 8개 전체 hash worker의 공유 forest proof를 개선하려는 시제품 빌드·좁은 안전 시험을 통과했다. 강화 guard·최종 성능은 이후 아래의 별도 결과로 검증했다. B2도 정확한 구성 목록/identity/내용 증명·제한 worker·실패 drain을 유지해야 한다. 이 비용 코호트에서 사용자 원본 프로젝트·Editor·외부 서버·패키지·전역 설정을 조작하지 않았으며 mounted Unity·peak memory/CPU·모든 구조·전원 장애·최종 조합 개선을 주장하지 않는다.

<a id="mx-05b1-leaf-regression-and-guard"></a>

## MX-05B1 leaf 회귀: topology 무조건 선택 폐기

별도 고정 **1,024 × 1,024-byte** 일반 NTFS 입력(**1,048,576 bytes**)에서 살아남는 모든 파일을 같은 부모 안에서 개별 rename했다. B1 bulk 코호트와 정확히 같은 동결 test/product runtime을 쓰고 동시 빌드·시험은 없었다. 한 스톱워치가 최종 증명·회수까지 전체 복원을 측정하며 준비·독립 assertion은 제외한다. bulk의 추가 표본이나 Unity 시간이 아니다.

| 개별 leaf ABBA | 전체 복원(초) | 누적 관리형 할당(bytes) | Unit stage/install 이동 | General 파일 이동 |
| --- | ---: | ---: | ---: | ---: |
| 01-before | 8.2841113 | 37,304,136 | 0 / 0 | 1,024 |
| 02-after | 47.7502525 | 145,578,168 | 1,024 / 1,024 | 0 |
| 03-after | 40.1972167 | 145,639,496 | 1,024 / 1,024 | 0 |
| 04-before | 8.8615848 | 37,296,312 | 0 / 0 | 1,024 |

평균 복원은 **8.57284805→43.97373460초**, 즉 **35.40088655초 / 412.94195749% 증가**했다. 평균 누적 관리형 할당은 **37,300,224→145,608,832 bytes**(**108,308,608 bytes 증가**, peak memory 아님)였다. 후보는 root 한 번과 stage 1,024회·install 1,024회를 수행하고 각각 topology cursor를 게시했다. baseline은 파일 1,024개를 이동하고 묶음 general cursor 경계 8회를 기록했다. 횟수는 구조적 설명의 근거이며 개별 인과 기여를 따로 측정한 시간이 아니므로 경과 시간에 더하지 않는다.

네 result/TRX는 최종 내용·metadata·구성 목록·살아남은 파일 identity를 정확히 검증했으며 정상 fixture 정리 전에 basis/delta/stage를 회수했다. 논리 입력 hash는 `62fe2c29b5945703d52c0f08609bc2755db6d3b9fa6f0327d0e9ab77e2568521`, 기대 결과 hash는 `347d6edbd2d53e380c1fb500e3f4611fe26662f9b11ca756bbaa618add236e52`다. runtime manifest는 cost2와 같고 SHA256은 `560389d6ca8cbc02c58410eb0b5a18518ff6f022ccec4f81a2c62997eaa869da`다. 정상 회귀 결과는 앞선 fixture binding 실패·성공한 bulk 코호트와 구분한다. variant별 두 관측·warm cache·누적 할당과 peak 구분·Unity/전원 장애 미자격을 유지한다.

**B1 topology 무조건 선택은 폐기한다.** 첫 B2 후속 소스는 최초 경로 선택에만 `PreferFresh`를 추가했으며 이동 수만의 규칙은 아래의 검증한 게시 한도로 강화했다. `1 + 2 × 살아남는 non-root unit + 삭제 non-root unit < 살아남는 source-backed 파일`로 topology의 보존 이동 수가 general보다 적어야 한다. 같거나 많으면 general을 선택한다. 이 하한에서 leaf 입력은 제외되고(**2,049 대 1,024**) bulk 입력은 남는다(**16 대 9,997**). 소스에서 계산한 경로 예상이며 guard 적용 실측이나 전체 비용 보장이 아니다. 이미 게시한 topology plan은 이 선호 조건 전에 `ResumeAsync`로 가며 `Eligible`·plan 검증·같은 intent 복구에는 새 비용 조건을 부과하지 않는다.

한정 읽기 B2 리뷰에서 검토한 변경의 확정 차단 결함은 발견하지 못했다. inventory 문법/경로 계산 뒤 실제 타입/경로/identity/전체 내용 증명을 유지한다. 원본·영수증 forest는 별도 proof root를 유지한다. receipt-root의 share-write는 제어 파일 게시를 허용하며 고정 제어 파일이 root가 비지 않게 한다. 삭제 디렉터리의 DELETE 접근은 유지하고 원본 root는 빌려 쓰며 닫지 않는다. directory map을 고정한 뒤 최대 8개 전체 hash worker를 실행하고 각 index 핸들은 모두 join한 뒤 실패 정리하며 최종 membership/TreePins는 유지한다. 이는 소스 리뷰이며 실행한 시험이 아니다. 속성 쓰기만의 reparse 허용과 정확한 부모/현재 타입 보호는 계속 유효한 경계다. 8개 64KiB hash buffer가 전체 plan/proof/handle 메모리 한도는 아니다.

앞선 B2 빌드와 좁은 안전 시험은 아래에 기록한다. 강화 guard는 집중 시험·전체 gate·아래의 동일 조건 fixed/bulk/leaf 비교를 완료했다. 기존 B1 결과는 보존하고 최종 결과로 덮어쓰지 않는다. A와 guard 포함 B2는 채택했으며 실제 동일 입력 Unity 반영/폐기 비교는 MX-10에 남아 있다. MX-05는 완료했다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)는 네 원시 result hash·closure·B1 구성 요소 한계 16개·B2 리뷰 소스 hash를 기록한다.


<a id="mx-05b2-validation-progress"></a>

## MX-05B2: 앞선 안전 시험과 강화 게시 guard 검증

최초 B2 빌드는 **28.36초에 xUnit1051 analyzer 오류 2개**, **경고 0개**로 실패했다. 시험은 실행하지 않았으며 `mx05b2-build.log`에 별도 보존한다. 시험의 취소 token을 수정한 뒤 `mx05b2-build2.log`는 **19.64초에 경고/오류 0개**인 Release 빌드 성공을 기록한다. `mx05b2-narrow.trx`와 로그는 **5.034초에 14개 통과, 0개 실패, 0개 skip**을 기록한다. 앞선 이동 수만의 신규 선택·기존 intent 재개·거부 입력의 general 경로·worker 실패/취소 join을 다룬다. 소스별 결과이며 다른 suite와 겹치므로 고유 시험 수로 합산하거나 이후 guard 수정의 자격으로 쓰지 않는다.

첫 필수 전체 gate는 **이전 이동 수만의 guard runtime**에서 **전체 1,958개, 통과 1,924개, 실패 1개, skip 33개**로 종료했다. `mx05-full-gate1.trx`와 `mx05-final-build-gate.log`의 시험 실행 시간은 **25m 31.649초**이며 독립 복원 벤치마크나 전체 build 명령 시간은 아니다. 유일한 실패는 `SparseDirectoryRestoreTests.ContentAndTopologyChangesSelectTheirOwnRootRestorePlans(change: "remove-directory")`로, **F=0, T=2**에서 general을 선택하는 것과 과거 기대 경로가 다르다. 실패 실행과 `runtime-mx05b2-gate1/` 및 hash manifest를 보존한다. 이 필수 gate는 **통과하지 못했으며** 별도의 최종 후보 재실행 성공은 아래에 기록한다.

이후 게시 guard/시험 빌드는 **48.38초에 경고/오류 0개**로 성공했고 `mx05b2-publication-build.log`에 기록했다. RootTopology/selection/forest/Sparse/Unchanged 집중 시험은 **107.228초에 234개 통과, 실패 0개, skip 0개**로 완료했고 `mx05b2-publication-focused.trx`와 로그에 기록했다. 소스별 시험은 앞선 suite와 겹치므로 고유 합계로 더하지 않는다. 두 번째 전체 gate는 **전체 1,974개, 통과 1,941개, 실패 0개, skip 33개**로 성공했고 `mx05-full-gate2.trx`와 `mx05-final-build-gate2.log`의 시험 실행 시간은 **10m 34.813초**다. 정확성 근거이며 복원 성능이 아니고 앞선 suite와 겹치는 선택을 합산하지 않는다. 앞선 14개 시험은 별도 수정의 근거로 유지한다.

추가 소스 검토에서 최초 이동 수만의 guard에 미실측 약점을 발견했다. 생존 source 파일 **1,000개** 중 독립 leaf rename **499개**는 **T=999 < F=1000**을 만족하지만 topology 연산 cursor **998회**와 general 완료 묶음 **8회**의 차이가 있다. 이는 구조적 비용 위험이며 해당 입력에서 측정한 감속은 아니다. 강화하려는 신규 요청 선택은 다음과 같다.

`T < F && O + N <= G`, 단 `G = ceil(F / 128)`.

`F`는 생존 source-backed 파일 수, `T`는 root를 포함한 보존 이동 수, `O`는 topology 연산 수, `N`은 새 디렉터리 영수증 수다. Bulk 입력은 **F=9997, T=16, O=17, N=2, G=79**로 식을 통과하지만 이 산술만으로 경과시간 이득을 입증하지 않는다. 첫 guard의 앞선 통과 기록은 역사적 근거이며 강화 규칙의 검증이 아니다. 기존 topology intent는 원래 복구 프로토콜을 유지하고 수정은 신규 선택에만 적용한다. 강화 규칙은 빌드·집중 시험·두 번째 전체 gate와 아래 별도 동일 조건 fixed/bulk/leaf 비교를 통과했다. A와 guard 포함 B2를 채택해 MX-05는 완료했다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다.


첫 전체 gate 뒤 최종 시간 측정 전의 읽기 전용 host 표본에서 관계없는 배경 작업은 **5.0606903초** 동안 **CPU 33.109375초**, **logical processor 12개**의 전체 용량 대비 약 **54.5%**를 사용했다. free RAM은 **35,249,604KiB**, C: 여유 공간은 **161,543,368,704 bytes**였다. 이 관측은 `mx05-host-load.json`에 기록했으며, 시험 시간의 인과 설명·성능 비교·peak 자원 측정이 아니다. 사용자는 해당 배경 작업 종료 뒤 시간 측정을 명시했다. 이후 제어자가 해당 배경 작업 부재를 확인하고 동결한 fixed/bulk/leaf ABBA를 완료했으며 이 host 표본으로 결과를 추론하지 않는다. 채택에는 사전의 큰 경과시간 이득 기준·정확한 안전/회수·허용할 자원 비용·최종 후보 전체 gate 성공이 필요하다. 과거 A/B1 시간은 그대로이며 host 표본으로 B2의 이득이나 Unity 절감을 추론하지 않는다. 채택은 별도의 최종 복원 결과를 따른다.


최종 후보 시간 비교는 아래에서 별도로 마감한다. 첫 gate와 두 번째 gate의 시간을 제품 속도 개선으로 비교하지 않으며, 중간 안전성·자원 표본은 최종 복원 stopwatch와 합산하지 않는다.


<a id="mx-05-final-adoption"></a>

## MX-05 최종: A와 guard 적용 B2 채택

**MX-05를 마감한다.** 최종 A root-delta 경로와 게시 수 제한 신규 선택 guard를 적용한 B2 topology 경로를 유지한다. Fixed·bulk는 정확성·전체 gate 성공과 이 입력에서 합당한 자원 비용을 확인하고 사전 기준 **평균 10초 이상이면서 15% 이상 절감**을 충족했다. Leaf는 general 복원을 사용하며 유의미한 경과시간 변화가 없다. B1 무조건 선택은 계속 폐기한다. **MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.**

최종 소스는 기준 `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3` 위의 미커밋 제품 변경 10개이며 정확한 hash는 [지도 JSON](../../lifecycle-optimization-map.json)에 고정했다. `mx05-final-cost/`는 **source/test 22파일**과 **variant별 runtime 129파일**, 서로 다른 **Platform/Workflows/CLI DLL/PDB 6파일**을 고정한다. 기준본은 동결한 `24956f4 / bea4acd` 제품이다. 두 variant의 시험 DLL SHA256은 `87d6e6dac8405066ee5492ea7543a29cf38f430f595dc3bf2a61358395d64b4b`이고 Platform SHA256은 before `e28ee738556b59b8ad1a61e91d8f2e7753c9fd61100ac9fe0b258ebdbc9c4990`, after `5acf5c4a2e5c30c56e7a7cb607d5052aee8fee467e221ff3f748e0b27f9f6315`다. 비교 후보를 고정하는 inventory이며 전체 PE 동등성이나 이전 A/B1 소스와의 동등성을 뜻하지 않는다.

단일 외부 제어자가 해당 배경 작업 부재를 확인하고 경쟁하는 빌드/시험 없이 고정 입력마다 전후후전을 직렬 실행했다. Stopwatch는 proof·쓰기/flush·최종 검증·소유 회수를 포함한 **일반 NTFS 제품 restore 호출 전체**를 측정한다. Fixture 준비와 별도의 최종 검증은 밖에 있다. Fixed·bulk는 각각 **10,000파일 × 1,024 bytes(10,240,000 bytes)**이고 leaf는 **1,024 × 1,024 bytes(1,048,576 bytes)**다. 정확한 논리 입력/결과 hash는 위 fixed·bulk·leaf의 별도 기록과 같다.

| 최종 입력 | Before 1 (초) | After 1 (초) | After 2 (초) | Before 2 (초) |
| --- | ---: | ---: | ---: | ---: |
| 고정 구조 | 45.5749939 | 14.0881750 | 15.3804282 | 46.5311454 |
| Bulk 구조 | 46.0219583 | 14.8832109 | 14.5989551 | 44.7094776 |
| 개별 leaf | 4.9901118 | 4.9923640 | 5.0194015 | 5.0507841 |

| 최종 입력 | Before 평균 (초) | After 평균 (초) | 절감 (초) | 절감 (%) | 판단 |
| --- | ---: | ---: | ---: | ---: | --- |
| 고정 구조 | 46.05306965 | 14.73430160 | 31.31876805 | 68.00582087 | A 채택 |
| Bulk 구조 | 45.36571795 | 14.74108300 | 30.62463495 | 67.50611769 | Guard 적용 B2 채택 |
| 개별 leaf | 5.02044795 | 5.00588275 | 0.01456520 | 0.29011754 | General 유지; 사실상 동일 |

**12회 모두** 정상 종료했다. 각 실행 TRX는 **시험 1개 통과, 실패/skip 0개**이고 `verified=true`·입력/결과 hash 일치와 fixture의 독립 최종 내용/metadata/구성원/생존 파일 identity 검사를 확인했다. Basis/delta/stage를 정상 회수했다. Fixed·bulk 후보는 디렉터리 identity를 보존하고 general 기준본은 새 디렉터리를 만든다. Leaf는 두 variant 모두 **general 파일 이동 1,024회·완료 묶음 8회**, **root/unit 이동 0회**다. 별도 두 번째 전체 gate는 시험 실행 **10m 34.813초**에 **통과 1,941개 / 실패 0개 / skip 33개, 전체 1,974개**를 기록했다. 앞선 suite와 겹치므로 집중 234개나 비용 fixture와 합산하지 않는다.

| 최종 입력 | 누적 관리형 할당 before → after (bytes) | 증가 (bytes) |
| --- | ---: | ---: |
| 고정 구조 | 312,864,028 → 358,422,964 | 45,558,936 |
| Bulk 구조 | 312,954,588 → 453,336,828 | 140,382,240 |
| 개별 leaf | 37,297,300 → 39,405,908 | 2,108,608 |

| 최종 입력 | 관측한 OS peak working set 평균 before → after (bytes) | 표본 peak Private 평균 before → after (bytes) | 전체 process CPU before → after (초) |
| --- | ---: | ---: | ---: |
| 고정 구조 | 163,299,328 → 190,105,600 | 123,709,440 → 151,457,792 | 75.7500000 → 51.9609375 |
| Bulk 구조 | 163,004,416 → 203,534,336 | 122,279,936 → 164,251,648 | 74.5546875 → 54.5546875 |
| 개별 leaf | 104,947,712 → 103,886,848 | 56,219,648 → 54,740,992 | 10.7500000 → 10.5625000 |

누적 할당은 **peak RAM이 아니다**. OS working-set peak와 Private 표본은 전체 시험 process를 약 **250ms** 간격으로 관측하며 실제 peak를 놓칠 수 있다. Process CPU/메모리에는 fixture 준비·독립 검증도 포함되므로 restore만의 값이 아니다. Fixed·bulk 평균 working set 증가 **26,806,272 bytes(약 26.8MB)**·**40,529,920 bytes(약 40.5MB)**와 낮아진 관측 process CPU 평균을 함께 고려해, 이 입력의 복원 **31.31876805초 / 30.62463495초** 절감에 비해 합당한 비용으로 판단했다. 한정된 공학적 판단이며 보편적 자원 상한이나 CPU 인과 기여를 분리해 입증한 결과는 아니다.

유지한 신규 규칙 `T < F && O + N <= ceil(F / ManagedDirectoryRestore.CursorBatchSize)`는 일반 실행 상수 **128**을 공유하고 이동/게시 증가를 제한한다. 모든 적격 입력의 개선을 보장하지 않는다. 기존 intent 프로토콜·전체 identity/내용/구성원 증명·durable 승인·실패 보존·정확한 회수는 유지한다. 전체 프로젝트·append SJR2는 general이고 새 경로는 원본 Unity Library의 SJL4/bounded SJR2만 지원한다. 상세 경계는 [Library 계약](../git-library-reference.md)·[F10 지도](../lifecycle-optimization-map.md#f10)를 따른다.

[추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)는 `mx05-final-summary.json`과 script·protocol/source/runtime inventory·12개 result/TRX/process-cost·분석 3개의 hash, 최종 실행별 수치와 자원 평균을 기록한다. 과거 A/B1·첫 gate/analyzer/setup 실패·안전 보강은 역사적 근거로 보존하며 덮어쓰거나 B2만의 인과 개선으로 비교하지 않는다. Variant별 두 관측·한 대·초기화하지 않은 OS cache의 한계를 유지한다. **실제 mounted Unity 전체 수명주기/반영/폐기·전원 장애 자격은 추가하지 않으며 README RR-01 시간은 그대로다.** 이 구성 요소 절감을 더해 Unity 전체 개선율을 예측하지 않는다.


<a id="mx-06-current-product-cost"></a>

## MX-06: 현재 제품 checkpoint 비용 분해

**MX-06은 미채택으로 완료했다.** 현재 제품 비용 8회와 별도 시제품 cohort를 아래에 보존하며 [최종 판단](#mx-06-final-disposition)에서 이번 범위의 제품 연결을 채택하지 않았다. MX-06은 미채택으로 완료했고 MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다. 이번 실행은 **동일하고 변경 없는 checkpoint 구현의 관측기 활성/비활성 조건**이며 최적화 전후 비교가 아니다. 유지한 MX05 복원 변경을 이 checkpoint 비용의 원인으로 읽지 않는다.

동결한 runtime 목록은 **129파일**이다. Runtime SHA256은 `285b379b21040344109c2c795ff0b2278b6867f6bed121f7b410b683ae47f812`, 시험 assembly는 `6915ad5762aa0f44e2f5286f2ec43cc5b1e0534b3a07a9db4dbbebabcb9a3dc7`다. checkpoint 소스 5개의 hash·protocol/test/controller hash·모든 원시 result/TRX/자원 근거는 [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-06.currentProductCost`에 기록했다. raw는 ignored `artifacts/remaining-optimization-20261028/mx06-cost/`에 보존한다.

닫힌 입력 두 개를 고정하고 시간 측정 밖에서 복사했다. **DF tail**은 과거와 정확히 같은 3파일 fixture로, cold private payload 20MiB·hot 내용 1,792,000B와 순서대로 개별 영속 완료하는 **10,305 × 112B** 쓰기를 사용한다. **Private-128m**은 **cold private payload 128MiB·hot 내용 1MiB·metadata-only leaf 4,096개**와 **8,192 × 112B** 쓰기다. 별도 fixture 준비·전체 논리 증명은 **27.967163초**였으며 비용 표본이 아니다. 입력 journal 길이/hash는 각각 **28,788,115B / `4778c7994eab77502f88f893697f305f5e2705ee380fb960f2a076963ed87899`**, **138,073,358B / `7fd16c8d5393c7f37d6daa6e90ea79809a9c5663285cabaf6182c4bc7d15012f`**다. 매 실행은 입력 전체의 hash·수정 시각·속성을 검증한다.

입력별 순서는 **observer → plain → plain → observer**로 조건마다 2회다. 기존 observer는 제한된 메모리 timestamp만 보관하지만 non-null hook은 크기가 1보다 큰 payload append를 두 번의 쓰기로 나눈다. 양쪽 모두 같은 저비용 checkpoint counter를 조회한다. 쓰기 stopwatch 이전의 전체 입력 검증이 data/cache를 데우며 OS cache는 비우지 않았다. 경쟁하는 빌드·성능 시험은 없었다.

| 입력 / 실행 | 조건 | 쓰기 구간 (초) | 누적 flush (초) | 자동 checkpoint 전체 (초) | Checkpoint 수 | Prefix bytes |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| df-tail / 01-observer | observer | 24.3909387 | 21.8218068 | 2.4603300 | 3 | 86,386,653 |
| df-tail / 02-plain | plain | 24.2689690 | 21.6967858 | 2.3071578 | 3 | 86,386,653 |
| df-tail / 03-plain | plain | 42.7237793 | 40.0566258 | 3.1790125 | 3 | 86,386,653 |
| df-tail / 04-observer | observer | 32.6202367 | 30.0467886 | 2.4888998 | 3 | 86,386,653 |
| private-128m / 01-observer | observer | 61.4305174 | 57.5486221 | 4.2482368 | 2 | 280,739,076 |
| private-128m / 02-plain | plain | 36.8716730 | 33.0436321 | 3.6960229 | 2 | 280,739,076 |
| private-128m / 03-plain | plain | 21.1357607 | 17.3218044 | 3.6948417 | 2 | 280,739,076 |
| private-128m / 04-observer | observer | 21.2407267 | 17.3650472 | 3.7626646 | 2 | 280,739,076 |

| 입력 | Checkpoint 평균 observer / plain (초) | 상태 출력 + candidate flush 평균, observer (초) |
| --- | ---: | ---: |
| DF tail | 2.47461490 / 2.74308515 | 0.54136015 |
| Private-128m | 4.00545070 / 3.69543230 | 0.73128705 |

관측한 checkpoint 하위 구간의 평균은 아래와 같다. 이들은 전체 checkpoint/쓰기 시간에 포함되므로 전체 시간에 더하지 않는다. 잔여 구간은 sizing·source/state hash·첫 intent flush·작은 tail/finally 비용을 포함하며 순수 직렬화 시간이 아니다. proof 구간은 여러 연산을 묶었고 출력 구간은 영속 flush를 포함한다.

| 관측 checkpoint 구간 | DF tail 평균 (초) | Private-128m 평균 (초) |
| --- | ---: | ---: |
| Intent 이전 + 잔여 | 0.44089855 | 0.67597835 |
| 상태 출력 + candidate flush | 0.54136015 | 0.73128705 |
| Candidate replay/proof + old-source 재hash + prepared intent | 0.77775435 | 1.33206885 |
| Close + 원자 게시 | 0.00336505 | 0.00231785 |
| 게시본 replay/proof + extent 재연결 | 0.59759625 | 0.91313320 |
| Previous hash + 삭제 | 0.11304530 | 0.35023745 |
| Intent 삭제 + 완료 | 0.00059525 | 0.00042795 |

**8회 모두** 각각 비용 시험 1개를 **실패/skip 0개**로 통과했다. 재연결 뒤 전체 내용·metadata·sequence 증명, 정확한 bounded-store 회수, 임시 root 제거, 고정 입력 불변과 **basis-read 시도 0회**를 확인했다. 누락된 checkpoint/hook 표본은 없다. 실행마다 DF는 **commit 10,305회 / flush 10,314회**, Private-128m은 **8,192회 / 8,198회**다. 별도 준비 증명은 아홉 번째 성능 표본이나 전체 regression gate가 아니다.

쓰기 stopwatch에는 모든 성공 ACK와 자동 checkpoint가 들어간다. 복사/open/입력 검증·최종 live 검증·재연결 검증·seal·정확한 회수는 별도 기록하고 강제 마지막 checkpoint를 추가하지 않았다. 회수 범위는 DF **0.1352157–0.1437103초**, Private-128m **0.5218845–0.5627663초**다. 누적 allocation은 peak RAM이 아니다. OS peak working set·약 **250ms**의 Private 표본·process CPU는 준비와 독립 검증을 포함하며 쓰기/checkpoint만의 자원이 아니다. 표본은 실제 peak를 놓칠 수 있다.

같은 변경 없는 제품에서도 쓰기/flush 편차가 크다. Plain/observer 평균 차이는 최적화 이득이나 관측기 오버헤드의 독립 인과 추정이 아니다. 이 입력들에서 checkpoint *전체*를 없애도 약 2.5–4.0초이고, 반복 상태 출력/candidate flush만의 측정 범위는 더 작은 0.54–0.73초다. 이는 낙관적인 기회 상한이며 예상 절감이 아니다. 단독으로 사전 기준인 **10초 이상이면서 15% 이상**을 충족하지 않는다. 별도 fixed-object full/incremental 시제품은 측정 후 미채택했으며 현재 제품·전체 상태/복구·WinFsp·Unity 자격을 승계하지 않는다. 제품 checkpoint 형식은 변경하지 않았고 채택 후보가 없으며 실제 Unity·전원 장애 근거를 추가하지 않는다. 과거 DF/SX/BL과 실패·폐기 후보는 기존 한계를 유지한다.

<a id="mx-06-standalone-prototype"></a>

## MX-06: 최초 별도 시제품 관측

**이 최초 관측은 보존하며 아래 별도 window 측정 뒤 제품 연결을 채택하지 않았다.** 별도 BCL 전용 고정 object 모델에서 전체 재작성과 불변 segment 재사용을 비교했다. raw writer·영속 ACK·전체 metadata/graph 증명·intent 게시·복구·정확한 회수는 공유한다. 두 번째 빌드는 **warning 0개 / error 0개**, 작은 중단·보존 self-test는 **44개를 8.6984688초에 통과**했다. 앞선 **43개 시험** 소스와 primer 추가 전·poison 수정 전 소스는 보존하며, 겹치는 실행을 고유 시험 합계로 더하지 않는다. 최종 catch-poison 회귀는 상태 증명이 실패한 뒤 ACK를 금지한다. 제품 전체 gate나 OS 전원 장애 자격이 아니다.

실제 측정에 사용한 동결 DLL SHA256은 `ec8834956f564fb040b18f3c6b6e872981ef0782b727fed95fd32f004f7aa58d`다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-06.standalonePrototypeEvidence`에 정확한 소스 8개·runtime 5개·protocol·controller·원시 result/closure hash·두 과거 소스 manifest·읽기 리뷰를 기록했다. 실제 바이너리와 원시 근거는 ignored `artifacts/remaining-optimization-20261028/mx06-prototype-cost/`에 남긴다.

각 입력을 **full → incremental → incremental → full** 순서로 실행했으며 mode당 관측은 두 번이다. Cold/hot은 처음부터 private인 cold 데이터 **256 MiB**, hot **4 KiB**, **256 bytes 쓰기 8,192회**다. Fragmentation은 별도 **32 MiB** control로 **1 MiB 쓰기 18회** 각각 뒤에 checkpoint를 수행해 obsolete-byte consolidation 한도를 넘긴다. 양쪽은 같은 full seed·강제 통합한 초기 상태·값이 같은 metadata primer 한 개를 만든 뒤 선택 mode로 다시 연다. GUID 파일명은 다르지만 기대하는 논리 바이트·metadata·sequence는 같다. 준비와 그 과정의 cache warming은 timer 밖이며 cache를 초기화하지 않았다.

| 입력 / 실행 순서별 mode | 전체 측정 초 | 쓰기 loop 초 | 마지막 강제 consolidation 초 |
| --- | ---: | ---: | ---: |
| Cold/hot 01 full | 48.8059669 | 42.7703663 | 3.5270722 |
| Cold/hot 02 incremental | 26.9824026 | 20.9857475 | 3.4524478 |
| Cold/hot 03 incremental | 27.1943104 | 20.8839859 | 3.6459297 |
| Cold/hot 04 full | 56.3562115 | 50.2641567 | 3.5344133 |
| Fragmentation 01 full | 13.1008051 | 12.1239038 | 0.6052183 |
| Fragmentation 02 incremental | 8.1645396 | 7.1113079 | 0.6336120 |
| Fragmentation 03 incremental | 8.2633262 | 7.1155321 | 0.7499371 |
| Fragmentation 04 full | 10.9940462 | 10.0775182 | 0.5348609 |

| 입력 | Full 평균 초 | Incremental 평균 초 | 관측한 절감 | 별도 시제품 screen |
| --- | ---: | ---: | --- | --- |
| Cold/hot | 52.58108920 | 27.08835650 | 25.49273270초 / 48.48270184% | 10초 이상이면서 15% 이상 충족 |
| Fragmentation | 12.04742565 | 8.21393290 | 3.83349275초 / 31.82001584% | 절대 10초 미달; consolidation/회귀 control |

전체 측정 구간에는 쓰기 loop와 자동 checkpoint, 마지막 강제 consolidation, 독립 기대 내용 생성, 종료·재열기 증명과 정확한 회수가 포함된다. Cold/hot은 **자동 checkpoint 2회와 마지막 강제 checkpoint 1회**이며 full은 모두 consolidation하고 incremental은 앞의 두 번에 segment를 유지한다. Fragmentation은 **loop checkpoint 18회와 마지막 강제 checkpoint 1회**이며 incremental은 loop 중 한 번과 마지막에 한 번 consolidation한다. 여덟 실행 모두 성공 종료하고 기대 논리 hash·재연결·회수를 증명했으며 자기 store만 정확히 제거했다. 회수 영수증은 남긴다. Unity나 WinFsp는 실행하지 않았다.

**전체 관측 절감이 checkpoint 알고리즘만의 인과 효과라는 뜻은 아니다.** 마지막 consolidation을 포함한 cold/hot checkpoint 평균은 **12.16008485→7.02195740초**, 감소는 **5.13812745초**다. 변경하지 않은 append-flush 경로는 **37.74354865→17.33445330초**로 **20.40909535초** 차이가 났다. mode당 두 관측으로 checkpoint I/O 상호작용·cache/순서·host 편차를 분리하지 못했다. 단계 timer는 전체 구간 안에 중첩되므로 별도 벽시계 시간처럼 더하지 않는다. 위의 다른 입력에서 현재 제품 checkpoint는 약 **2.5–4.0초**, 관측한 상태 출력/flush는 **0.54–0.73초**였다. 모델 초·비율을 제품이나 Unity 이득으로 대체하지 않는다.

append payload를 제외한 측정 checkpoint 복사량은 cold/hot **805,318,656→268,439,552 bytes**, fragmentation **637,612,032→67,117,056 bytes**였다. 마지막 consolidation을 포함한 cold/hot 물리 파일 길이 최고 관측은 **538,604,472→538,605,552 bytes**로 거의 같았고 fragmentation은 **68,192,641→83,934,459 bytes**로 늘었다. 선택 mode의 새 open 이후 누적 접수/관측 길이이며 별도 초기화한 단계 peak·할당 cluster·RAM이 아니다. 앞선 full seed 생성 중 overlap은 setup metrics에만 속한다. 관측 proof 바이트도 일반 frame/control parse를 제외하며 실제 디스크 I/O가 아니다.

| 전체 process 자원 평균 | Cold/hot full → incremental | Fragmentation full → incremental |
| --- | --- | --- |
| OS가 보고한 peak working set, bytes | 69,201,920 → 70,780,928 | 74,115,072 → 78,077,952 |
| 표본 Private 최고값, bytes | 35,702,784 → 36,861,952 | 40,372,224 → 44,109,824 |
| Process CPU, 초 | 56.453125 → 53.484375 | 13.2109375 → 10.7421875 |

이 자원 관측은 공통 준비와 assertion을 포함한다. Private는 약 250ms 표본으로 실제 peak를 놓칠 수 있으며 알고리즘만의 자원 비용이 아니다. 실험은 제한 suffix·segment/obsolete 한도·최종 consolidation 비용을 유지하지만 제품 namespace/basis/provider/candidate/seal/schema 연결이나 NTFS identity/부모 핸들 기준 containment를 제공하지 않는다. 관리형 예외는 실제 process 종료·재부팅·전원 장애 자격이 아니다. 최초 cold/hot 결과를 바탕으로 아래 독립 window 진단을 수행했으며 원시 수치는 그대로 보존한다. **제품 연결은 채택하지 않았다.** 선택적 제품 단계 비용 시험은 보관 후 제거했고 lifecycle map의 제품 hash는 바뀌지 않았다.

<a id="mx-06-final-disposition"></a>

## MX-06 최종: window 진단과 미채택

**MX-06은 미채택으로 완료했다.** 아래 독립 cold/hot ABBA는 모델 양쪽에 같은 scalar window 관측을 추가했다. 최초 모델 8회나 변경 없는 제품 비용 8회를 대체하지 않는다. 모델 시험 44개는 앞선 protocol 소스의 근거이며, window만 추가한 빌드는 **warning 0개 / error 0개, 3.12초**로 통과했다. 새 측정 4회의 정상 회수는 별도 근거다. 제품 형식·checkpoint 코드는 바꾸지 않았다.

같은 **cold 256MiB / hot 4KiB / 256B 쓰기 8,192회 / seed 41**과 공통 consolidated seed·metadata primer 한 개를 사용했다. 완료 쓰기 **0, 4,095, 4,096, 8,191, 8,192**에서 scalar를 기록하며 누적 자동 checkpoint는 **0, 0, 1, 1, 2**다. 따라서 구간별 쓰기 수는 **4,095 / 1 / 4,095 / 1**이다. 원소 다섯 개의 고정 value 배열을 timer 전에 할당하며 관측은 scalar counter·단조 clock만 읽고 측정 loop의 파일 I/O·상태 복사·ACK를 추가하거나 바꾸지 않는다. 첫 checkpoint 전 양쪽은 같은 append 경로를 따른다. 독립 정적·원시 결과 검토로 이 경계를 확인했다.

| 실행 / mode | 전체 측정 초 | 쓰기 loop 초 | Loop checkpoint 초 | Loop append flush 초 | 마지막 강제 consolidation 초 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 01 full | 31.1421934 | 24.9395249 | 7.2867287 | 17.5161976 | 3.6215352 |
| 02 incremental | 27.0137796 | 20.9623484 | 3.5368401 | 17.2975515 | 3.5105223 |
| 03 incremental | 27.0652545 | 21.0771747 | 3.4243205 | 17.5119701 | 3.3756428 |
| 04 full | 36.6497808 | 27.5493863 | 10.0130104 | 17.3971427 | 6.5293515 |

| 평균 구간 | Full 초 | Incremental 초 |
| --- | ---: | ---: |
| 전체 측정 | 33.89598710 | 27.03951705 |
| 쓰기 loop | 26.24445560 | 21.01976155 |
| Loop checkpoint | 8.64986955 | 3.48058030 |
| Loop append flush | 17.45667015 | 17.40476080 |
| 마지막 강제 consolidation | 5.07544335 | 3.44308255 |
| 종료/재열기 증명 | 1.53615035 | 1.56518295 |
| 정확한 회수 | 1.03979000 | 1.01137705 |

| Window와 구간 내 완료 쓰기 수 | Full 경과 / flush / checkpoint 초 | Incremental 경과 / flush / checkpoint 초 |
| --- | --- | --- |
| 첫 checkpoint 이전, 4,095 | 8.80167760 / 8.73069335 / 0 | 8.80248340 / 8.73456015 / 0 |
| 첫 checkpoint와 ACK 한 개, 1 | 3.53649170 / 0.00300050 / 3.53345925 | 1.72720770 / 0.00304115 / 1.72413745 |
| Checkpoint 사이, 4,095 | 8.78288360 / 8.71611630 / 0 | 8.73048330 / 8.66412085 / 0 |
| 둘째 checkpoint와 ACK 한 개, 1 | 5.12330160 / 0.00686000 / 5.11641030 | 1.75951245 / 0.00303865 / 1.75644285 |

전체 절감은 **6.85647005초 / 20.22796985%**로 상대 **15%**는 통과하지만 절대 **10초** 기준에 미달했다. Loop checkpoint 절감은 **5.16928925초**, append-flush 차이는 **0.05190935초**뿐이다. 첫 ACK 4,095개의 flush 시간은 사실상 같다. 앞선 전체 절감 **25.49273270초**와 공통 append-flush 차이 **20.40909535초**는 유효한 관측 기록으로 남기지만 큰 flush 차이는 재현되지 않았고 원인은 미확정이다. 마지막 강제 consolidation의 **1.63236080초** 차이는 큰 full-mode 내부 편차를 포함하므로 모두 증분 게시의 효과로 돌리지 않는다. 중첩 window/단계는 전체 구간에 더할 추가 시간이 아니다.

네 실행 모두 같은 초기/최종 기대 논리 hash에 일치하고 **sequence 8,453**으로 끝났다. **ACK / append flush 8,192회**, 자동 checkpoint **두 번**과 마지막 강제 consolidation을 완료했다. Full은 consolidation **세 번**, incremental은 **한 번**이다. 매 재연결/증명·정확한 회수가 통과했고 자기 store 디렉터리는 모두 없으며 외부 회수 영수증은 남는다. 원시 result/config/closure hash, source/protocol/controller hash와 독립 재계산한 평균은 일치했다. 측정 DLL SHA256은 `0c70e0201e2351f79cc9c792358ace21ab0703d68d84b548f426fb541a00d03a`다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-06.windowDiagnosticEvidence`는 별도 source/runtime manifest·네 원시 행·자원 관측·독립 검토를 고정한다. 원시 근거는 ignored `artifacts/remaining-optimization-20261028/mx06-window-cost/`에 보존한다.

Mode당 관측은 두 번이다. 공통 full-seed 준비/증명이 timer 밖에서 cache를 데우며 물리 GUID 배치와 device pressure는 통제하지 않았다. Process CPU/working set은 준비/검증을 포함하고 250ms Private 표본은 실제 peak를 보장하지 않는다. 모델은 제품 namespace/basis/provider/candidate/seal 연결과 NTFS identity/부모 핸들 기준 경합 보호를 제공하지 않는다. 제품·Unity·process 강제 종료·전원 장애 자격은 없으며 이 독립 window cohort가 이전 cohort의 flush 차이 원인을 확정하지 않는다.

현재 제품 checkpoint 기회(plain 평균 **2.74308515초 / 3.69543230초**, 관측 출력+candidate flush **0.54136015초 / 0.73128705초**)와 모델의 재현되지 않은 큰 이득은 이번 범위에서 새 다중 파일 저장 형식·graph 복구·Library 복원·정확 회수 계약을 추가할 근거가 부족하다. 제품 연결은 구현하지 않았다. `mx06-removal.json`에 선택적 비용 시험의 byte-exact 보관/제거와 변경 없는 제품 소스 5개 hash를 기록했으며 보관 시험 SHA256은 `7a487f43e31bfee9188505e3b286ceb85bcad415cc0a5a9ea1769812038eb58c`다. 시제품 소스/바이너리·제품 연결 설계·모든 원시 cohort는 ignored 과거 근거이며 선택적 제품 backend나 자동 재개할 작업이 아니다. README RR-01과 lifecycle map 제품 hash는 바꾸지 않았다. **MX-07A와 MX-07B는 실측 후 폐기·제거하여 MX-07을 마감했다. MX-08은 진행 중이고 MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다. 전체 프로그램은 진행 중이다.**


<a id="mx-07a-source-cache-rejected"></a>
## MX-07A: 호출 간 소스 cache 폐기

**MX-07A는 실측 후 폐기·제거했다.** MX-07B와 행의 최종 판단은 아래에 기록한다. 소스 cache 후보는 고정된 Editor 없는 입력에서 **8.45773340초 / 15.47910650%**를 절감했지만 사전 선언한 작업공간 전체 평균 **최소 10초 AND 15%** 기준에는 미달했다. Unity 전체 개선을 뜻하지 않으며 B의 국소 근거는 아래에 별도로 기록한다.

후보는 None/recorded Plastic의 소스 내용 hash만 제한된 checksum·현재 버전 cache로 재사용했다. 호출마다 전체 namespace/metadata를 scan하고 완전한 변경 세대 이력·owner/basis/provider/mount identity를 작업 전후에 확인했다. Cache를 사용하는 읽기는 고정한 native volume 경로를 사용했고 독립 보존 payload는 새 전체 증명을 유지했다. 이력 backfill·손상/pending의 명시적 audit 복구·유효한 외부 영수증·identity 변경은 보존·거부했다. 이 계약 설명은 보관한 후보 실험에 해당하며 제거 후 현재 제품 지원이 아니다.

실제 측정은 NTFS/WinFsp의 **source none / application none / project-snapshot**이며 **2,112파일 / 2,281,701,376바이트(2.125GiB)**, 64 × 32MiB와 2,048 × 64KiB, 고정 seed 261026·mtime·attributes를 사용했다. 큰 파일 하나의 8바이트를 덮어쓰고 고정 metadata의 64KiB 파일을 추가했다. ABBA는 작업공간 하나씩 직렬 실행했고 추가 status 반복은 넣지 않았다. 하나의 monotonic timer가 ensure 호출·편집·checkpoint·별도 release·응답 검증·controller 근거 쓰기를 포함한다. 아래 명령별 시간은 전체에 중첩되어 있으므로 합산하지 않는다. Setup·전체 입력 증명·release 뒤 독립 증명·공통 기준본 준비/복원은 별도다. 사전 전체 증명은 cache를 예열하며 OS cache는 지우지 않았고 공통 관측 비용을 0으로 주장하지 않는다.

| 순서 / 후보 | 작업공간 전체(초) | Ensure(초) | Checkpoint(초) | Release(초) |
| --- | ---: | ---: | ---: | ---: |
| 1 before | 61.99037280 | 20.90413850 | 31.17722710 | 9.82596920 |
| 2 after | 33.69321480 | 17.00396150 | 10.83320760 | 5.77979260 |
| 3 after | 58.67066730 | 17.59344670 | 35.51189250 | 5.48544230 |
| 4 before | 47.28897610 | 21.10313050 | 16.01507530 | 10.03636720 |
| before 평균 | 54.63967445 | 21.00363450 | 23.59615120 | 9.93116820 |
| after 평균 | 46.18194105 | 17.29870410 | 23.17255005 | 5.63261745 |

공통 준비 **3.69286070초**와 마지막 복원 **12.16613020초**는 primary 비교 밖이다. 양쪽 모두 checkpoint 내부 편차가 컸으며 원인은 계측하지 않았다. 이 편차를 flush·cache·후보 알고리즘에 귀속하지 않는다. 상대 기준은 통과했지만 절대 기준은 실패해 후보를 유지하지 않았다. 한 장치/호스트의 후보별 두 관측은 일반 속도·peak RAM·실제 Plastic 서버·원본 반영·전원 장애·Unity 전체 이득 자격이 아니다. 과거 PR/RT 입력은 fixture 설계 근거이며 당시 시간은 이번 기준 표본으로 사용하지 않는다.

검증 기록은 소스별로 다르고 서로 중복되므로 **고유 시험 수로 합산하지 않는다**.

| 근거 / 소스 단계 | 결과와 범위 |
| --- | --- |
| Build 1 / build 2 | WorkspaceStore의 Runtime import 누락, 이후 시험의 JournalDeltaStore import 누락으로 compile 실패를 보존한다. 두 소스의 검증 통과를 주장하지 않는다. |
| Build 3–6 | 각각 warning 0 / error 0으로 통과했고 마지막 build 6은 9.12초다. 빌드이며 전체 시험 gate가 아니다. |
| Focused 1 | 총 169: 168 통과 / fixture 1 실패. CopyHashFixture가 디렉터리에 파일 mtime API를 적용했으며 디렉터리 API로 고친 근거를 별도로 보존했다. |
| Native probe 1 / focused 2 | 최초 실제 device-root 열거는 .NET가 끝 구분자를 제거해 실패했다. 수정한 namespace probe와 fixture의 선택 시험 2개가 통과했다. GLOBALROOT 이중 trailing separator 표기만 조사한 모든 native 연산을 통과했다. |
| Native guard 2 | 해당 소스에서 11 통과 / 0 실패 / 0 skip. |
| Cache focused 3 | 총 36: 35 통과 / 신규 fixture 1 실패. 일반 NTFS extended 경로가 중간 이중 구분자를 오류 123으로 거부했으며 실제 WinFsp GLOBALROOT 동작과 구분한다. |
| Focused 4 / 마지막 build 6 | 2개 통과: 수정한 일반 Win32 fake-root 시험과 실제 WinFsp Files 전체/metadata scan 시험 1개. Fake 시험만으로 GLOBALROOT 자격을 주장하지 않는다. Native scan은 private 덮어쓰기/생성 전후 public·pinned 전체 scan과 metadata scan을 대조한다. |

후보 소스 목록은 **327파일**, CLI runtime은 **45파일**이며 기준본은 **소스/시험 323개 / runtime 45파일**다. 시작 HEAD `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3`와 별도 소스 목록으로 미커밋 상태의 채택 MX-05 기준본과 폐기 MX-07A 후보를 고정했다. 의존성 재빌드로 DLL/PDB 22개가 달라졌으며 전체 PE 동등성을 주장하지 않는다. 원시 실패·수정 전 파일·source/runtime 목록·측정 근거는 ignored `artifacts/remaining-optimization-20261028/` 아래에 보존한다. [추적 요약](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-07.sourceCacheCandidate`에 정제한 정확한 hash와 시험별 범위를 기록한다. 성능 기준 실패 뒤 채택용 전체 gate는 실행하지 않았다.

네 실행 모두 소스 bytes/mtime/attributes·기준/결과 oracle 일치·release 뒤 독립 payload 전체 hash·정확한 provider 종료·task mount/delta 부재를 증명했다. 각 독립 결과 검증은 **고유 object 2,114개 / 2,315,321,344바이트**를 확인했다. 공통 기준본은 전체 입력이 정확한 일반 NTFS로 복원됐고 basis payload/delta/provider는 없으며 독립 결과는 보존했다. 기존 6파일은 byte-exact 복원하고 후보 전용 4파일은 보관 뒤 제거했다. MX-07A 제거 시점의 소스/시험 **323개** hash는 모두 MX-07 기준본과 일치했다. 제품 소스에 source-cache mode·guard API·CLI 변경·후보 시험은 남지 않았다. README RR-01과 lifecycle 지도는 변경하지 않았다. 별도 결과 proof 후보와 MX-07 최종 판단은 아래에 기록한다.

<a id="mx-07b-result-proof-rejected"></a>

## MX-07B: 결과 proof 유지 후보 폐기·MX-07 마감

후보는 한 명령 동안 최대 **4,096개**의 읽기 전용 독립 object 핸들을 유지하고 기준/결과의 정확한 합집합을 한 번 hash한 뒤, proof를 다시 쓸 때 제어 기록과 workspace/revision/control identity를 새로 확인했다. P0는 결과 게시 뒤 두 번째 합집합 hash를 줄이고 P1은 첫 proof를 unmount부터 source unregister까지 유지했다. capability가 없거나 상한을 넘으면 기존 전체 검증을 유지했다. 새 디스크 형식이나 공개 옵션은 추가하지 않았다.

Files·coordinator·native lease 국소 정확성은 **50개 통과, 실패 0개/skip 0개, 6.051초**였다. fresh controls·foreign/stale/disposed binding·fallback·실패/취소 해제·drain·unmount/회수 순서·native identity/reparse 보호를 확인했다. 최초 빌드는 시험의 취소 token analyzer 오류 **4개**로 실패했고 수정 build2는 **경고/오류 0개, 9.56초**로 통과했다. 첫 focused filter는 잘못돼 실행 시험이 **0개**였으며 focused2가 통과 기록이다. 이 준비 실패를 제품 실행 실패로 합치지 않는다. 별도 opt-in 국소 비용 시험은 **1개, 6.698초**로 통과했으며 전체 수명주기 시험이 아니다.

읽기 전용 비용 입력은 MX-07A에서 이미 released 상태로 보존한 독립 결과 **object 2,114개 / 2,315,321,344바이트**다. 같은 빌드의 기존 `VerifyResultAsync` + `UnregisterAsync`와 선택적 proof 획득 + proof 포함 unregister 경로를 비교했다. 공통 전체 proof로 측정 전에 입력을 warm했고, 모든 lease 해제 뒤 새 전체 proof와 변경 없는 제어 파일 hash를 확인했다.

| 실행 | 경로 | 초 |
| --- | --- | ---: |
| Before 1 | 반복 전체 proof | 1.25475270 |
| After 1 | 유지 proof | 1.49346260 |
| After 2 | 유지 proof | 1.40144320 |
| Before 2 | 반복 전체 proof | 1.10463180 |
| 평균 before → after | | 1.17969225 → 1.44745290 |

유지 경로는 이 warm 국소 관측에서 **0.26776065초 / 22.69750013% 더 느렸다**. 큰 이득이 없어 추가 전체 gate나 작업공간 전체 시험 없이 폐기했다. 한 호스트에서 경로별 두 관측뿐이다. 소스 scan·unmount·준비·Editor·전체 수명주기는 이 timer 밖이며 전체 소스/Unity나 실제 Plastic 성능으로 확대하지 않는다.

`mx07b-removal.json`은 기존 6파일의 byte-exact 복원과 후보 전용 8파일의 보관 후 제거를 기록한다. 보관본은 **소스/시험 14파일과 runtime 45파일**이다. 채택한 MX-05는 유지했고 동시에 시작한 MX-08 변경은 건드리지 않았다. 한정된 제거 증명이며 전체 소스가 MX-08 이전 기준본과 같다는 주장은 아니다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-07.resultProofCandidate`에 원시 hash를 추가했고 이전 근거는 모두 보존한다. **MX-07A/B를 폐기하며 MX-07을 마감한다. MX-08은 진행 중, MX-09는 대기, MX-10은 마지막 조합 검증으로 남는다.** README RR-01과 lifecycle-map 성능 주장은 바꾸지 않는다.

<a id="mx-08a-combined-resize-local"></a>

## MX-08A: resize/metadata 결합 국소 근거

제품 4파일 변경으로 `SetLengthAndMetadata`를 추가하고 SourceView를 통해 전달하며 WinFsp Overwrite에서 사용한다. SJR2는 최종 entry를 resize commit 1회로 게시하고 길이가 같으면 metadata commit 0/1회를 수행한다. 기존 clipping/replay·영속 ACK 경계는 유지한다. SJL4는 별도 resize와 metadata capsule 게시를 유지한다. 새 CLI 옵션이나 저널 형식은 추가하지 않았다. 후보에는 집중/비용 시험 2파일이 있으며 후속 조합 자격을 기다리는 **잠정 유지** 상태다.

Build1은 **경고/오류 0개, 33.29초**로 통과했다. 국소 정확성은 **30개 통과, 실패 0개/skip 0개, 1.428초**였다. 별도 opt-in 비용 시험은 **1개 통과, 실패 0개/skip 0개, 263.674초**다. 국소 검증이며 뒤로 미룬 전체 저장소 gate가 아니다.

같은 빌드에서 별도 resize + metadata와 결합 연산을 실제 파일 기반 bounded SJR2 저널로 비교했다. **64파일 × 128라운드 = 저장 8,192회**, refill은 **4,096바이트**다. 측정에는 mutation/refill loop와 마지막 flush를 포함한다. 준비·seal 수집·재연결 후 전체 내용 증명은 측정 밖이며 네 실행 모두 재연결한 결과가 정확히 일치했다. 불변 기준본은 작은 합성 tree이고 작업공간 mount나 Unity process는 실행하지 않았다.

| 실행 | 경로 | 초 | 영속 mutation |
| --- | --- | ---: | ---: |
| Before 1 | 별도 | 123.00958680 | 24,576 |
| After 1 | 결합 | 51.99791950 | 16,384 |
| After 2 | 결합 | 35.19008530 | 16,384 |
| Before 2 | 별도 | 52.79853000 | 24,576 |
| 평균 before → after | | 87.90405840 → 43.59400240 | 24,576 → 16,384 |

관측 평균 절감은 **44.31005600초 / 50.40729269%**지만 기존 경로 편차가 커 단독 인과 개선율로 주장하지 않는다. 과거 Unity Overwrite **260회**에 비해 저장 **8,192회(31.50769231배)**를 반복하며, 해당 연산을 의도적으로 증폭한 시험이지 대표적인 Unity 절감을 입증한 것이 아니다. 영속 mutation 감소는 확인한 동작 원리이며 이를 전체 수명주기 시간으로 환산하지 않는다.

[추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-08.A`에 원시 행과 hash를 추가한다. 부모 제어자가 `mx08a-candidate/manifest.json`에 **소스/시험 6파일과 runtime 45파일**을 동결했으며 정확한 소스 hash·manifest hash를 보고서에 연결했다. 후보 identity를 고정하는 근거이며 자격 범위를 확대하지 않는다. **A는 잠정 유지이며 MX-08B 혼합 mutation batching은 폐기·제거 완료, MX-09는 대기, MX-10은 마지막 동일 입력 조합 비교다.** 이 국소 관측으로 전체 수명주기 채택이나 README RR-01 성능 수치를 바꾸지 않는다.

<a id="mx-08b-mixed-batching-rejected"></a>

## MX-08B: 혼합 batching 폐기·제거

후보는 Write/nullable metadata/resize를 순서가 있는 typed batch와 기존 WinFsp callback 큐에 연결했다. accepted-ticket Flush drain·요청 실행 시점 반환 entry·명확한 요청 오류 격리·불확실 실패 후 중단·제한된 복사·write-only/큰 write 경로를 유지했다. 이 후보 API·혼합 계측·후보 전용 시험은 국소 screen 뒤 제거했으며 현재 제품 기능이 아니다.

Build1은 시험 property 문법 오류 **8개**로 실패했다. 시험만 수정한 build2는 **경고/오류 0개, 7.97초**로 통과했다. Focused1은 **총 45개 / 43개 통과 / 1개 실패 / 조건부 telemetry 비용 skip 1개, 3.548초**였다. partial-frame replay 시험이 lazy source id를 다시 쓰던 가정만 path로 재연결하도록 수정했다. Build3는 **경고/오류 0개, 8.22초**로 통과했고 해당 메서드 **3 variants만 통과, 실패 0개/skip 0개, 0.263초**였다. 고유 결과는 **44개 통과·조건부 skip 1개**이며 중복 실행을 합산하지 않는다. 최초 실패를 보존했고 집중 시험 사이 제품 소스는 같으며 제거한 후보의 결과를 현재 제품 시험 수로 쓰지 않는다.

ABBA는 **runtime별 129파일**과 같은 test DLL을 사용했다. baseline의 A runtime **45파일**은 동결 A manifest에 일치했다. 측정 전 경로 구분자 inventory 수정은 hash 차이가 없었고 trial을 시작하지 않았다. 직접 callback 부하는 실제 영속 파일 기반 bounded SJR2와 합성 불변 소스를 사용하며 worker별 private 파일에서 매 라운드 Write64·SetBasicInfo·Resize32를 수행했다. 준비/ready barrier는 측정 전, 모든 요청과 명시적 Flush는 측정 안, 재연결·내용/소스 증명은 측정 밖이다. 인위적인 batching 대기·mount·kernel scheduling·Unity process는 없었다.

| ABBA 실행 | 직렬: 1 worker × 64라운드(초) | 동시: 8 worker × 256라운드(초) |
| --- | ---: | ---: |
| Before 1 | 0.42679080 | 12.33052060 |
| After 1 | 0.43725330 | 3.17787470 |
| After 2 | 0.43728460 | 3.19732090 |
| Before 2 | 0.43332210 | 12.30007430 |
| 평균 before → after | 0.43005645 → 0.43726895 | 12.31529745 → 3.18759780 |

동시 **6,144 callback**은 **9.12769965초 / 74.11676159%**를 절감했고 직렬 **192 callback**은 **0.00721250초** 증가했다. 동시는 **15%**를 통과하지만 필수 절대 **10초**에 미달해 증폭 재시험 없이 폐기했다. 네 trial 시험과 두 부하의 replay 증명은 모두 통과했다. Variant별 두 관측·한 호스트·초기화하지 않은 OS cache 한계를 유지하며 이 stress 구성은 과거 Unity callback 빈도나 전체 수명주기 절감이 아니다.

`mx08b-removal.json`은 기존 **4파일** byte-exact 복원과 후보 전용 **6파일** 보관 후 제거를 기록하며 소스 보관본은 **10파일**이다. A 소스/시험 **6개** hash는 모두 같다. [추적 보고서](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 `MX-08.B`에 원시 결과·hash·시험 이력·제거를 추가했다. 제거 시점 `bin/Release`에는 폐기한 B가 남아 현재 소스 재빌드가 필요했다. 아래의 후속 MX09 Release 재빌드로 교체했으며 동결 A runtime은 유효하다. **MX-08 구현과 국소 판단은 완료했다. A는 잠정 유지, B는 거부로 마감했고 MX-09는 남아 있으며 MX-10은 마지막 조합 비교다.** B 마감 자체에는 제거 뒤 빌드·전체 gate·mount·Unity 성능 자격이 없었으며 후속 MX09 빌드는 아래에 별도로 기록한다.

<a id="mx-09-unpublished-preparation"></a>

## MX-09: 제거한 후보의 구현·국소 검증 기록

**아래는 제거 전 후보의 역사적 계약·국소 검증 기록이다. 당시 미확정 상태를 보존하며 현재 제품 경로나 최종 상태를 뜻하지 않는다. 마지막 실제 측정·폐기 절이 현재 판정을 소유한다.**

새 range-journal task의 `ResultRevision`이 null이고 소유 Editor가 실행 중이며 공급자가 capability를 지원할 때만 정상 `StopAsync`와 `PrepareCheckpointAsync`를 겹친다. 기존 source proof cache와 검증한 미참조 content object만 준비하며 collection/result manifest, index, commit 또는 ref를 게시하지 않는다. Files는 전체 source generation과 owner/basis를 확인하고 변경 payload를 hash·metadata로 검증한 RAM에서만 게시한다. 서로 다른 변경 payload가 **256MiB** 상한을 넘으면 준비를 생략한다. 이 상한은 전체 process RAM 제한이 아니다. Git은 기존 owner/source/해석 증명과 경로별 clean filter를 유지하며 변경 파일의 content object만 쓴다.

양쪽 작업은 실패·취소에도 모두 drain한다. 둘 다 성공한 뒤 `RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal`과 기존 게시·회수 guard를 따른다. 늦은 쓰기는 최종 수집이 새로 판단한다. 기존 결과, release 재개, Editor 없는 작업과 미지원 공급자는 기존 경로다. MX-08A는 잠정 유지하고 MX-08B는 폐기·제거했다. MX-09 구현과 국소 정확성 검증은 완료했으며 성능·채택·전체 검증은 미확정이다. MX-10은 마지막 조합 검증으로 남는다. 아직 수행하지 않은 시험이나 시간 측정을 통과로 기록하지 않는다.

동결된 제품 소스와 시험의 첫 Release 빌드는 **경고 0개·오류 0개, 27.40초**로 통과했다(`mx09-build1.log`). 이는 MX08B 제거 뒤 현재 소스로 binary를 교체한 빌드이며 전체 `build.ps1` gate는 아니다. 완료된 국소 시험 결과는 아래에 따로 기록하며 시간 이득은 아직 확정하지 않는다.


최종 **focused4 한 번의 완료 실행**은 **55개 통과 / 0개 실패 / 0개 skip**, xUnit **281.172초**(TRX run wall **281.17478초**)다. 신규 **45개**와 기존 보호 **10개**를 포함한다. 이는 정확성 시험 시간이며 종료 성능 측정값이 아니다.

- Build1은 **27.40초**, Build2는 **7.94초**, Build3는 **7.95초**이며 각각 **경고 0개·오류 0개**다. Build2는 Git 시험 observer의 동기 `GetResult`를 async로 바꿨고, Build3는 Runtime 시험 조건식의 target-typed `new`를 명시적 `PreparationAtomicSource` 생성으로 수정했다. 제품 **6개**는 Build1 이후 hash가 같다.
- Focused1/2는 Starting 상태에서 중단되어 완료 count가 없다. Verbose focused3는 Files **17개** 완료 뒤 no-capability fixture가 별도 `PreparingSource`를 만들어 해제하지 않은 준비 barrier에서 기다리는 것을 확인하고 중단했다. 원본 source/runtime과 실패 기록은 보존한다. Git observer 수정은 별도의 시험 위험 보강이며 앞선 대기의 원인으로 확정하지 않는다. 중단 실행의 일부 사례를 최종 통과 수에 더하지 않는다.
- `mx09-local-final/manifest.json`은 **9개 source/test 파일과 129개 runtime 파일**을 고정한다. manifest SHA256은 `b03df13197a7bdd7e284509fa81806f042c5731e5f02c4176abf1f935d0c4260`, 최종 TRX SHA256은 `e25b17cc7db33561af63c95b524619ff6e557608288c66460f2789d18411fe50`이다. [추적 요약](../../../reports/benchmarks/remaining-lifecycle-optimization-20261028.json)의 MX09 블록이 이 근거를 소유한다.

**구현·국소 정확성 검증 완료이며 성능·채택·전체 검증은 미확정이다.** 프로그램과 MX09 행은 미완료로 유지한다. MX08A는 잠정 유지, MX08B는 제거, MX10은 마지막이다. 저장소 전체 `build.ps1` gate나 실제 Unity 종료/전체 수명주기 비교를 실행한 것으로 읽지 않는다.

<a id="mx-09-final-disposition"></a>

## MX-09 실제 Git/Unity 최종화: 폐기·제거

동일 소스/Library 파일 bytes·mtime·attributes와 최초 loaded MVID의 ABBA 각 2회에서, 실행 중인 소유 Editor에 `workspace checkpoint --release`를 호출하여 정상 종료·독립 결과·소유 회수까지 잰 평균은 **69.76183845→70.28309595초**다. 전−후 평균 절감은 **-0.52125750초 / -0.74719576%**로 사전 **10초 이상 AND 15% 이상** 기준에 미달했다. MX09 후보는 미채택으로 마감하고 제품·시험을 보관 후 제거/복원했다. MX08A는 잠정 유지, MX05는 유지하며 전체 프로그램과 MX10은 미완료다.

별도 `unity stop`을 먼저 호출하지 않았다. Library 복원은 별도 시간이며 연속 전체 관찰에는 준비·시작·편집/컴파일·최종화·복원과 관측 비용이 모두 포함된다. 초기 ready는 MX09 준비가 시작되기 전이고 편차가 크므로 전체 관찰시간 감소를 MX09 효과로 주장하지 않는다. 아래 구간은 중첩되어 합산하지 않는다.

| 순서 | checkpoint/release 초 | 초기 ready 초 | Library 복원 초 | 연속 전체 관찰 초 |
|---|---:|---:|---:|---:|
| 01-before | 70.59876170 | 175.74334200 | 22.89105400 | 394.47825940 |
| 02-after | 69.99108030 | 114.17107240 | 22.79306420 | 322.22756980 |
| 03-after | 70.57511160 | 100.91895940 | 22.88731850 | 311.21626180 |
| 04-before | 68.92491520 | 119.48113130 | 22.69723040 | 325.83617140 |

네 번 모두 정상 종료·독립 Git 결과·소유 회수 및 원본 source/Library/index/HEAD/config/등록 보존을 확인했다. 이전 ref를 모두 보존하고 각 작업의 정확한 task branch/result ref만 추가했다. 공유 Git object DB와 보존 결과는 회차 사이 증가하므로 저장소 전체 바이트가 동일한 입력이라고 주장하지 않는다. None/Plastic 속도, 일반 Unity 개선, 원본 반영/폐기 전체 수명주기, peak RAM 또는 전원 장애 자격은 없다. 전체 `build.ps1` gate와 MX10 반영/폐기 비교는 여전히 마지막에 남는다. 현재 제품 흐름은 `Stop → RequireStopped → RemoveInfrastructure → full pre-seal → fresh checkpoint/preserve → full post-seal → publish/reclaim`이다.

`mx09-performance-analysis.json` SHA256 `10f7a13a995b61680b353d9e4bea58cdb9fbef26c2c4cb610b70b905dbb3b84b`; `mx09-removal.json` SHA256 `96621f7841db9792cbf69e8dc3e8a5222a333afc84edb295108b5c539f656e06`. 제거 영수증: 기존 4파일 복원, 후보 전용 5파일 제거; `retainedMx08AExact=true`, `verified=true`. 원시 run/후보/55-case 및 build·중단 기록은 보존한다.

<a id="mx-10-final-closeout"></a>

## MX-10 최종 조합 검증과 프로그램 마감

**프로그램 완료, 큰 전체 시간 개선은 미입증.** MX-01은 진단, MX-02–04·MX-06/07·MX-08B·MX-09는 미채택, MX-05 A·게시 수 제한 B2와 MX-08A는 채택으로 마감한다. 새로운 후속 시험은 이 기록에서 승인하거나 시작하지 않는다.

| 경로 | 이전 전체 초 | 최종 전체 초 | 전−후 절감 초 | 전−후 절감 % |
|---|---:|---:|---:|---:|
| apply | 338.51645460 | 326.31757210 | 12.19888250 | 3.60363059 |
| discard | 281.90245510 | 286.53377360 | -4.63131850 | -1.64287980 |

각 경로는 이전/최종 **1회씩**, 고정 순서 `01-apply-before → 02-apply-final → 03-discard-before → 04-discard-final`이다. 반영은 **12.19888250초 / 3.60363059%** 짧고 폐기는 **4.63131850초 / 1.64287980%** 길었다. 서로 다른 종료 경로를 표본으로 합치거나 ABBA·일반 배수·개별 기능의 인과 효과로 읽지 않는다.

**최종 검증과 채택.** `scripts/build.ps1` 한 번의 locked restore·Release build는 **경고 0 / 오류 0**, 시험은 **총 2,000 / 통과 1,966 / 실패 0 / 조건부 NotExecuted 34**, log의 시험 실행 시간은 **9m46.195s**다. 선택적 cost/native/evidence 및 Child/ClientActor 진입점의 34개는 미실행이며 `CombinedOverwriteCost`, root-delta/topology/leaf cost의 과거 성능 측정을 이번 전체 gate가 다시 실행한 것이 아니다. 국소 30개·후보 55개·과거 전체 시험 수를 이 결과에 더하지 않는다.

최종 source/test **313개**와 runtime **45개**를 고정했다. 최종 runtime은 MX08A 동결본 45개와 byte-exact 일치한다. MX08A는 국소 영속 저장 비용 **87.90405840→43.59400240초**, **44.31005600초** 관측 절감과 영속 mutation **24,576→16,384**, 국소 **30개**·최종 **1,966개** 시험·실제 **4회**의 정확성에 근거해 최종 채택한다. 새 형식·큐·worker는 없다. 국소 입력은 **8,192회 저장**, 기존 Unity 기록의 **260회 Overwrite**보다 **31.5077배** 증폭됐고 baseline 편차가 컸다. 이 국소 절감을 Unity 전체 절감이나 순수 인과 비율로 승계하지 않는다.

**입력과 타이머.** 이전 runtime은 커밋 `24956f4975a8c9ae57a21f4c52a656b9f8b51ea3` 메타데이터와 실제 제품 소스 `bea4acdb95f3ea2a914587c7f5e5293b57940e7d`에 고정되며 기존 BO 최적화를 이미 포함한다. 최종 후보는 그 위의 유지한 MX05·MX08A 조합이다. Unity **6000.3.22f1**, fixture base `f121a5adc9c4248994774dea03843504c949f09a`, 동일 전체 source/Library 파일 hash·길이·mtime·attributes와 최초 loaded MVID를 매회 검증했다. 한 줄 `value + 1 → value + 2` 변경, 명시적 refresh 성공, 새 generation/MVID와 실제 로드 값 **4/6/13→5/6/13**을 확인했다.

전체 시간은 `library.prepare` 시작부터 `library.restore` 성공 반환까지 한 개의 멈추지 않는 monotonic stopwatch다. 아래 초 단위 단계는 그 안에 포함되며 합산하지 않는다. initial ready는 `unity-start` 호출을 포함하고 최종화는 checkpoint/integrate/release 또는 discard 및 정상 Stop을 포함한다. Stop 단독 시간은 별도 계측하지 않았다. 입력/runtime/schema 증명·반영 대상 branch 준비는 시작 전, 독립 결과/원본 전체 증명·로그 복사·guard가 있는 fixture 복귀는 끝난 뒤다. 외부 서버 수명 관리도 별도이며 내부 관측 비용은 전체에 포함한다.

| 실행 | Library 준비 | Ensure | 초기 ready | 편집·컴파일 | 정상 종료 포함 최종화 | Library 복원 | 전체 |
|---|---:|---:|---:|---:|---:|---:|---:|
| 01-apply-before | 40.08292510 | 49.70547910 | 100.62175830 | 25.11523190 | 97.61364890 | 22.69338090 | 338.51645460 |
| 02-apply-final | 40.44669190 | 49.21571400 | 98.49277190 | 22.66951840 | 90.57529460 | 22.88032810 | 326.31757210 |
| 03-discard-before | 39.91864980 | 50.36644790 | 98.90863560 | 23.87092720 | 44.12045770 | 22.68171050 | 281.90245510 |
| 04-discard-final | 39.67230330 | 52.07913550 | 100.92310290 | 22.97087820 | 44.63291410 | 24.29955000 | 286.53377360 |

| 반영 실행 | Checkpoint | Integrate | Release |
|---|---:|---:|---:|
| 01-apply-before | 63.98219020 | 17.76494740 | 15.86388280 |
| 02-apply-final | 56.55279770 | 18.46311250 | 15.55726580 |

**결과·원본·회수.** 반영은 실행 중 Editor의 checkpoint가 정상 종료를 소유하고, 독립 결과를 실제 원본의 새 고정 대상 branch에 integrate한 뒤 release했다. 결과 전체 tree/mode와 한 줄 변경의 정확한 bytes, merge 부모, 실제 원본 내용과 나머지 파일 metadata를 검증했다. 독립 결과·task·대상 refs는 보존한다. 원본 Editor는 시작하거나 결과를 다시 컴파일하지 않았다. 원본 HEAD/index는 승인된 branch 전환·통합·detached 복귀 과정에서 정당하게 변경되므로 index bytes/mtime가 처음과 같다는 주장은 없다. index tree/HEAD/branch/등록 의미와 config/exclude를 증명했고, 측정 밖 복귀 뒤 source/Library bytes·mtime·attributes를 다시 확인했다.

폐기는 실행 중 Editor에서 정확한 intent/digest로 수행하며 checkpoint/result를 만들지 않았다. 기존 refs·원본 Git metadata와 source/Library를 보존했다. 네 번 모두 정상 exit **0**, 소유 resource **0**, mount/delta 회수를 확인했다. 기존 모든 ref는 유지하고 각 반영의 정확한 새 task/result/target ref만 허용했다. 공유 Git object DB와 보존 결과는 회차마다 늘어 전체 저장소 bytes가 동일한 입력이라는 주장은 없다. 외부 원본 서버는 별도 승인 절차로 재시작하여 health·설정 복원을 확인했다.

**복원 경로와 남은 시간.** 네 번 모두 원본 Library journal durable sequence **0**, `unchanged-root`였다. 작업공간의 private Library 변경은 원본 Library 변경이 아니며 MX05의 변경 Library A/B2 경로는 선택되지 않았다. 따라서 이 실제 비교는 MX05 전체 수명주기 절감을 입증하지 않는다. 현재 관측의 큰 구간은 Library 준비 약 **40초**, ensure **49–52초**, 최초 ready **98–101초**, 최종 반영 최종화 **90.57529460초**/폐기 **44.63291410초**, 복원 **22–24초**다. 이는 다음 시험 제안이나 승인 없이 남기는 관측 한계다. 원본 입력 증명은 cache를 데우지만 초기화/flush·고정 순서·OS cache를 같게 만들지 않는다. None/Plastic·동시 다중 Editor·Play/Test/player build·peak RAM·전원 장애·일반 Unity 속도 자격은 없다. README RR-01 수치는 바꾸지 않는다.

정확한 근거는 ignored `artifacts/remaining-optimization-20261028/`와 추적 report에 보존한다. Source/runtime manifest의 pending 값은 동결 당시 상태이며 이 마감 절이 후속 결과를 소유한다.

- `mx10/analysis.json`: `42cc1a5d85e2f9a534417f2576da9652c8e8370d4dc8694b9b5f3703492c0293`
- `mx10-final-gate1.json`: `1ac7e98bd11807b9163bca3480bf0e2c0992dc9ae675a9a2222d7f65789c7761`
- `mx10-final-gate1.trx`: `31fb67c0968431d4977522c146147436fd51a09405de1ffb0128f8dd1bda59f4`
- `mx10-final-runtime.json`: `5866b47485b9ff3cbdb1d32f7f55326e860bef404f661fbd3fcca911ec87906d`
- `mx10-server-restoration.json`: `7a617e0c6223207c1256caaf1b60745af057b1649616b4bed3a4f2757b442134`
