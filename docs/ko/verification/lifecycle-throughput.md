# 단일 작업공간 수명주기 처리량 검증

[English](../../verification/lifecycle-throughput.md)

[LT-01–04 범위](../plans/lifecycle-throughput.md)는 A/B 동시 실행이 아니라 작업공간 하나의 준비·사용·완료다. 기준 커밋은 `be761bb43032f72fbbd5ca9a3165bd937bd45a6f`, 후보는 현재 작업 트리 변경이다. [Dataset](../../../reports/benchmarks/lifecycle-throughput-20261024.json)에 개별 실행·hash를, `artifacts/lifecycle-optimization-20261023/`에 원시 근거를 보존한다. 아래 수치는 구성 요소 근거이며 **새 Unity 시작·컴파일·전체 수명주기 실측이 아니다**. ABBA는 두 실험 후보를 함께 측정했고 LT-01 철회 뒤 최종 Library만의 확인은 별도다.

## 구현과 보호

| ID | 최종 판단 | 검증 경계 |
| --- | --- | --- |
| LT-01 | **검토 후 제외.** SJR2 개별 durable flush 중 이전 확정 상태 읽기를 허용하는 후보였다. | 동시성·보호 시험은 통과했지만 추가 저장 지연 없는 부하의 절감이 7.659초·13.0%에 그쳤다. 큰 절감을 우선하라는 요청에 따라 Runtime 변경·전용 시험을 제거했고 제품 저널은 기존 동작을 유지한다. |
| LT-02 | Capture 해시·admission 파일 열기/해시·변경 없는 복원의 파일 열기/해시/metadata를 `min(max(논리 CPU 수, 2), 8)`개 worker로 처리한다. | 조상 map 전체를 먼저 고정하고 모든 worker가 끝난 뒤 index를 직렬 게시한다. 실패 시 worker drain 후 부분 핸들을 닫는다. identity/type/hardlink/reparse/final-path·길이/내용·이동 뒤 증명·디렉터리 metadata 후처리를 유지한다. 복사·hydration·영속 payload cache는 추가하지 않는다. |
| LT-03 | 동일 harness와 기준/후보 assembly를 별도 프로세스에서 ABBA 순서로 직렬 측정한다. | 합성 내용은 같으며 측정 중 다른 빌드/측정은 실행하지 않았다. OS cache를 비우지 않았고 2회 반복으로 cold storage·다른 하드웨어·전체 프로젝트 속도를 확정하지 않는다. |
| LT-04 | 집중 시험과 필수 전체 자동 회귀를 실행한다. | 최종 결과는 아래에 기록한다. 원본 프로젝트·실행 중 Editor·외부 도구·이전 결과·서비스는 변경하지 않았다. |

제외 후보는 flush 전후 barrier, 이전 compressed/private/base 내용, lazy namespace identity, 쓰기/seal/flush/변경 관측/dispose 대기, 실패 poison, WinFsp read/GetFileInfo와 Close를 포함한 전용 동시성 15개 사례를 통과했다. 소스·바이너리는 원시 근거에만 보존한다. 채택한 Library 변경은 실패·취소 worker drain을 8개 worker로도 시험하고 기존 디렉터리 교체·인계·metadata·저널 복구·원본 반영·회수 시험을 유지한다.

공통 owned-directory capture/reader는 관리형 `project-snapshot` 기준본에서도 사용한다. 해당 호출자도 worker 변경을 받으며 기존 admission guard를 유지한다. 아래 비용 입력은 Library 구성 요소만의 자격이며 새 실제 project-snapshot 수명주기·다른 OS의 자격이 아니다.

## Library 구성 요소

각 호출은 일반 NTFS의 전용 Library에 동일 내용의 65,536바이트 파일 20,000개, 총 1,310,720,000바이트(약 1.22GiB)를 만들며 첫 파일은 읽기 전용이다. 입력 생성·복원 뒤 전체 내용 대조는 타이머 밖이다. Capture·admission은 여전히 모든 파일 내용을 검사한다. 복원에는 계획·root 인계/이동·새 identity/content 증명·metadata·정확한 bounded 저널 회수를 포함한다. **원본 Library가 변경 없는 경로**이며 원본 Library 수정 뒤 일반 파일별 복원은 아니다.

| 단계 | 기준 평균(초) | 후보 평균(초) | 감소 |
| --- | ---: | ---: | ---: |
| Capture | 4.825 | 2.192 | 54.6% |
| Admission | 2.515 | 0.875 | 65.2% |
| 변경 없는 복원 | 10.088 | 3.711 | 63.2% |
| 위 구성 요소 합계 | 17.428 | 6.778 | 61.1% |

원시 수치는 `before-native-{1,2}-library.json`·`after-native-{1,2}-library.json`이다. 해시 scratch는 64KiB buffer 8개의 최대 512KiB다. 이전 admission은 2개·128KiB였고 capture/restore는 직렬이었다. Admission/restore 핸들은 이전에도 보유했으며 파일별 배열·capture record의 관리 할당이 추가된다. 측정 allocation은 누적 관리 할당이며 전체 프로세스 peak RAM이 아니다. 최종 후보는 기존 파일별 정규화 오류 분류와 비-Windows의 열기 직전 경로 검사를 추가로 유지하며 별도 확인 측정으로 기록한다.

## 읽기와 durable 쓰기 혼합 부하

초기 2,048회 쓰기는 한 바이트 범위를 차례로 늘리고 private/base 읽기와 회당 최소 5ms SHA256 작업을 병행한다. 추가 저장 지연이 없을 때 평균 25.329초/20.794초로 4.535초 절감에 그쳤다. Flush 전에 **5ms sleep을 요청한** 별도 모델은 50.337초/31.072초였다. Windows timer scheduling으로 실제 지연은 5ms보다 길며 BC-01의 평균 flush 지연 재현이나 Editor 속도 근거가 아니다. 이것만으로 일반 부하의 큰 절감을 확정하지 않는다.

후속은 **추가 저장 지연 없이 같은 바이트를 8,192번 덮어쓰기**하고 private/base 읽기·SHA256 작업을 병행했다. 기준은 59.216초/58.514초, 후보는 48.564초/53.849초다. 평균은 **58.865초/51.207초**, 절감은 **7.659초·13.0%**다. 각 실행은 bounded checkpoint를 포함해 commit 8,194개·flush 8,200개를 기록했다. Writer 시간도 17.440–23.869초로 변동했다. Lock-wait 누계 감소를 전체 시간의 큰 절감으로 읽지 않으며 **이 저널 후보는 최종 제품 변경에서 제외했다**. 두 부하 모두 파일시스템 mount·Unity 실행이 없고 callback 정확성 시험은 Runtime 비용과 별도다.

## 검증과 재현

실험 후보 둘을 함께 둔 집중 native 시험은 **513 통과·실패 0·조건부 skip 16개**(전체 529개)다. Library만 남긴 최종 후보의 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·Release 빌드의 경고/오류 0개, **1,558 통과·실패 0·조건부 skip 25개**(전체 1,583개, 531.522초)로 통과했다. 최종 비용 시험도 통과했다. Capture/admission/변경 없는 복원은 각각 1.935초/0.729초/3.001초(합계 5.665초)였고 복원된 모든 바이트를 대조했다. 이 별도 확인은 VCS 밖 일반 사용자 TEMP에서 실행했으므로 추가 동일 조건 ABBA 쌍으로 보거나 위 평균에 합치지 않는다. Dataset에 최종 소스·assembly hash를 기록했다.

기준 커밋 archive를 빌드하고 동일 `LifecycleThroughputCostTests.LargeLibraryCaptureAdmissionAndUnchangedRestore`를 기준/현재 제품 DLL로 실행한다. `SPLINTER_LIFECYCLE_LIBRARY_COST`에 새 JSON 출력 경로를 지정하고 직렬 실행하며 이전 출력은 보존한다. Assembly hash는 dataset에 남긴다. 제거한 저널 harness/소스는 원시 근거의 `rejected-journal-candidate/`에만 있으며 현재 진입점이 아니다. 초기 sandbox의 temp rename/final-path 거부와 잘못된 runner filter 기록도 보존한다. 첫 전체 시험은 Git checkout 안의 temp root 때문에 None/Plastic의 VCS 상속 보호가 거부했다. 최종 검증은 VCS 밖 일반 temp에서 실행한다. NTFS 성공 시험은 제한 sandbox 밖의 전용 fixture만 사용한다. 기준 archive는 네트워크 거부 뒤 온라인 취약점 조회를 끄고 lock된 로컬 package로 복원했으며 최종 필수 gate는 저장소의 일반 restore 정책을 유지한다.

이 구성 요소 gate에서는 Unity 전체 수명주기를 측정하지 않았다. 후속 [LU-01](unity-single-lifecycle.md)이 동일 입력의 시작·코드 변경/컴파일·에셋 사용·폐기·변경 없는 Library 복원을 별도로 측정하며 양쪽 공통 bounded 폐기 경로 수정을 포함한다. 원본 반영·일반 파일별 Library 복원·전체 프로세스 peak RAM·OS crash 자격은 **NotRun**으로 유지한다. 원본 게시·seal·stop·독립 결과·회수 알고리즘은 증명을 생략하거나 timeout을 완료로 취급하지 않는다. 중단했던 과거 비교·디버거 회수도 재개하거나 재분류하지 않는다.
