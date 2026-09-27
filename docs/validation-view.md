# 불변 검증 후보와 고정 view 실험

`snapshot-spike`의 개발용 확장이다. 제품 `workspace ensure`의 실행 모드나 작업 큐에 연결되어 있지 않다. 범위는 [VC-01~04 계획](plans/immutable-validation-view-first-milestone.md), 실제 판정은 [검증 기록](verification/immutable-validation-view.md)을 따른다.

## 저장 구조

Core의 `ValidationCandidate`는 원본 `OverlayIdentity`, 원래 저널 seal, 별도 후보 저널 seal과 시각을 묶는다. `ValidationViewSelection`은 후보 디렉터리·캐시 디렉터리·캐시 최상위 이름을 전달한다. Core는 Unity·Git·Win32에 의존하지 않는다.

Runtime의 `JournalDeltaStore.ExportCandidate`는 정지한 작업자 저널을 읽기 전용으로 열어 동시 writer를 거부한다. 기존 정리 알고리즘으로 유효 변경 범위와 메타데이터만 새 디렉터리에 기록하고 replay/state hash/seal을 확인한 뒤 `candidate.json`을 마지막에 atomic 게시한다. 원본 파일 트리 전체를 열거하거나 원본 payload를 복제하지 않는다. 저장 중 대체된 저널 payload는 후보에 필요하지 않지만 메타데이터와 일부 표현 한계에 따른 기록은 남을 수 있다. 소스 저널은 변경하지 않는다. 게시 중 임시 공간은 작업자와 후보의 합계다.

`CandidateSnapshotTree`는 게시된 identity와 seal을 검사하고 사용하는 동안 읽기 전용 저널 핸들로 쓰기·교체를 막는다. 열린 핸들이 없는 동안 외부에서 변경한 후보는 다음 연결의 seal 검사에서 거부한다. snapshot과 후보를 조합한 identity는 원본 identity와 후보 seal을 모두 포함한다. snapshot이 소실되면 읽기를 중단한다. 후보 자체는 **snapshot에서 독립된 결과가 아니다**.

`ValidationViewDeltaStore`는 두 개의 독점 writer 저널을 사용한다.

| 저장 위치 | 수명과 역할 |
| --- | --- |
| 작업자 delta | Editor 없이 A/B를 편집한다. 후보 게시 뒤 다시 편집해도 기존 후보는 변하지 않는다. |
| 불변 후보 | 작업자와 별도 디렉터리의 범위 payload·메타데이터. snapshot에 의존한다. |
| 세대별 소스 delta | 후보를 기준으로 Unity가 쓴 소스·.meta·관리 bridge를 보존한다. 다음 후보에는 섞지 않는다. |
| 공통 cache delta | 빈 캐시에서 시작해 `Library`/`Temp`/`Logs`/`UserSettings`/`obj`의 쓰기를 보존한다. 후보 간 같은 저널을 재개한다. |

소스·후보·캐시는 서로 겹치지 않는 경로여야 한다. partition 설정·view ID·원본 snapshot·예산이 달라지면 기존 캐시를 재사용하지 않는다. 다른 writer가 캐시를 열고 있으면 다음 view를 열 수 없다. 저널 형식과 각 acknowledged write의 flush 정책은 기존과 같다. `state.json.validationView`는 source/cache 각각의 I/O 계수와 후보 seal을 보인다. 최상위 `storage` 합계는 두 저널의 관측이며 제품 `stable-journal-v1` 결과 seal로 사용하지 않는다.

Unity가 새 임시 파일을 다른 partition으로 rename할 때는 원본에서 읽을 범위가 0인 파일만 전달한다. `JournalPrivateFiles`는 유효 private extent만 복제하고 구멍을 채우지 않는다. 최대 유효 payload 64MiB·8,192개 extent이며 디렉터리와 원본 범위가 남은 파일의 경계 이동은 거부한다. `partition-transfer.json`은 정확한 source 세대와 양쪽 경로·file ID·range hash를 기록한다. 목적지 저널의 임시 파일에 범위를 기록하고 같은 저널 안의 atomic rename으로 게시한 뒤 원래 경로를 지운다. 원래 file ID와 열린 핸들은 유지한다. 중단 시 view I/O를 막고 같은 source 세대 재연결만으로 전송을 완료한다. 후보를 바꾸거나 intent/저널을 삭제해 복구를 우회하지 않는다. 공급자 epoch의 `transferCount`, `transferredPrivateBytes`, 마지막 경로는 관측 값이며 파일 전체 copy-up이나 새 저널 형식이 아니다.

## 개발용 진입점

준비된 작업자 overlay를 정상 unmount한 뒤 실행한다.

```json
{
  "action": "candidate-export",
  "overlayRequestPath": "C:/trial/worker-overlay.json",
  "candidatePath": "C:/trial/candidates/a"
}
```

`scripts/splinter.ps1 snapshot-spike --input <request.json>`으로 호출한다. 입력 schema는 `snapshot-spike-request`, 결과 후보 schema는 `validation-candidate`다. 이 실험의 export는 `.git`·관리 캐시를 쓴 작업자를 거부한다. Git pointer를 이미 쓴 일반 제품 workspace를 이 명령에 직접 넣는 흐름은 지원하지 않는다. 독립 결과 수집 단계의 Git 등록은 `GitOverlayRegistration`을 사용하며, 실험의 attach는 baseline과 다른 소스가 있는 index refresh의 정상 exit 1을 허용한다. lock·기타 오류를 무시하지 않고 제품 attach의 기본 계약은 유지한다.

검증용 `overlay-serve-request`에 다음 선택을 더한다.

```json
{
  "validationView": {
    "candidatePath": "C:/trial/candidates/a",
    "cacheDeltaPath": "C:/trial/cache",
    "cacheTopLevel": ["Library", "Temp", "Logs", "UserSettings", "obj"]
  }
}
```

전체 요청의 `identity`는 원본 snapshot을 가리킨다. 각 전환에는 새 `deltaPath`와 `controlPath`를 쓰고 동일한 `mountPath`, view ID, cache 경로를 유지한다. 이전 후보의 source delta·제어 기록은 보존한다. 재개는 같은 후보·같은 source delta·같은 identity를 쓴다. 오류 뒤 폴더나 저널을 삭제해서 identity 검사를 우회하지 않는다.

전환 순서는 `unity-stop` → 소유 application 자원 0 → `unmount` → provider 종료·mount 부재 → 다음 후보 `mount` → `unity-start` → 명시적 `unity-refresh`와 실제 code/asset 확인이다. `checkpoint`를 검증 후보로 재사용하지 않는다. 열린 Editor 아래 root 교체, 자동 임대/FIFO, 작업자에 생성 소스를 병합하는 명령은 없다. source delta의 출력은 독립 Git 결과로 수집한 후에 회수한다. release·TTL·디스크 압박에 의한 자동 후보 삭제도 없다.

provider는 저널을 여는 동안 `replaying` 상태를 게시한다. mount/reconnect는 같은 launch의 PID·시작 시각을 유지하며 최대 2분 동안 실제 mount와 5초 이내의 `mounted` 관측을 기다린다. 프로세스 시작이나 replay 상태만으로 준비 완료를 반환하지 않는다. 한도 초과·취소에는 저널과 실행 중인 provider를 보존하며 같은 요청의 다음 호출이 그 provider를 기다린다. 기록과 launch 소유권이 맞지 않으면 불확실 상태를 유지하고 중복 실행하지 않는다. `state.preparation.storeReadyTicks`는 후보 확인과 source/cache 열기를 합친 내부 시간이며 전체 CLI 준비 시간과 구별한다.

## 시간과 공간의 판정

후보의 `creationTicks / stopwatchFrequency`는 저널 준비·검증을 마친 뒤 최종 receipt 게시 직전까지다. 전체 후보 게시 시간은 CLI 반환까지의 별도 wall-clock으로 잰다. snapshot 캡처, Git 준비, 최초 mount, 기존 worker 재개, 후보 게시, 첫 Unity import/ready, 반복 stop/remount/replay/restart/ready, 검증·결과 수집·회수를 각각 기록한다. 사용자 작업 가능 시점까지 걸리는 시간을 작은 내부 함수 수치로 대체하지 않는다.

디렉터리별 논리 길이와 `FILE_STANDARD_INFO.AllocationSize`를 구분하고 공유 VSS는 볼륨 관측으로 둔다. 측정 증거·로그도 별도 비용이며 첫 import와 반복 전환을 분리한다. 작은 Editor 없는 delta의 준비 시간을 전체 제품 `workspace ensure`나 대형 프로젝트 준비 시간으로 일반화하지 않는다. 최초 source 검증 비용, 긴 저널 replay, Unity 버전·package·platform 변경, 일반 임대 중단 복구와 source 반환 병합은 별도 검증이 필요하다.

재생 읽기 버퍼와 단계별 계수는 [소스 검증·계측](source-verification.md), 실제 지원 범위와 시간은 [VR 실행](verification/validation-view-readiness-and-replay.md)과 [기존/개선 Runtime의 작은 Unity 비교](verification/unity-replay-comparison.md)를 따른다. 같은 닫힌 journal의 mount 재연결과 Editor가 새 기록을 쓰는 재시작은 입력 조건이 다르다. 최초 ready·명시적 compile·실제 코드/에셋 검증과 정상 stop의 재시도 비용을 함께 확인한다. mount 준비·compile 성공·에셋 로드 성공만으로 최초 렌더 성공을 보장하지 않는다. 첫 미리보기 자체를 확인해야 하며 첫 렌더의 초기화 문제는 미해결이다. 제품 기본 모드와 일반 Unity 지원은 별도 검증 대상이다.
