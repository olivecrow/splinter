# Offline replay 계측

`tools/Splinter.ReplayProbe`는 합성 저널의 Runtime 재생을 비교하는 개발 도구다. 제품 workspace·VSS·WinFsp mount·Unity Editor를 만들거나 기존 원본을 읽지 않는다. 사용 가능 여부와 경계는 `capabilities.developmentReplayProbe`에도 표시한다. 실제 Unity의 시간·저장공간 증거를 대신하지 않는다.

## 실행

먼저 `pwsh ./scripts/build.ps1`로 빌드한다. 새 입력 경로를 명시한다.

```powershell
dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build -- generate append artifacts/replay-example/append
dotnet run --project tools/Splinter.ReplayProbe --configuration Release --no-build -- run artifacts/replay-example/append
```

`generate <append|overlap|file-renames|write-append-4096|write-append-16384|write-overwrite-16384|write-distributed-16384> <새 디렉터리>`는 존재하는 경로를 거부한다. `append`는 8,192회 순차 4KiB 쓰기, `overlap`은 초기 2,048회 4KiB 쓰기와 6,144회 겹치는 1KiB 쓰기·끝부분 축소/확장, `file-renames`는 파일 35,000개와 rename 4,032회다. 네 `write-*` 입력은 1KiB payload로 한 파일 순차 4,096/16,384회, 한 범위 반복 덮어쓰기 16,384회, 128개 파일에 총 16,384회를 분산한 경우다. 각 추가 입력의 journal 상한은 32MiB, 기존 입력의 상한은 128MiB다. payload·frame checksum·identity·독립 기대 파일 hash와 완료 영수증을 생성한다. fixture 생성의 마지막 flush는 제품의 acknowledged write flush 정책이나 쓰기 성능을 재현하지 않는다.

`run <봉인된 합성 디렉터리>`는 이 도구의 고정 synthetic identity와 journal hash를 검사한다. 같은 서명의 비공개 read-only Runtime 생성자를 reflection으로 호출하므로 기존 비교 바이너리를 그대로 쓸 수 있다. 해당 생성자가 없는 바이너리는 명시적으로 거부한다. 이는 안정적인 제품 API가 아니다. 읽기 전용 handle을 사용해 입력의 미완료 tail도 고치지 않으며, 기존 입력을 덮어쓰거나 자동 정리하지 않는다.

재생 뒤 모든 기대 파일의 길이/내용 hash, 경로 목록과 삭제된 경로, 내부 state hash, durable sequence와 replay frame, write commit/flush/base payload read 0을 확인한다. 마지막에 원본 journal hash가 그대로인지 다시 확인한다. 범용 손상 저널 복구·임의 workspace 진단에는 사용하지 않는다.

## 해석

`elapsedTicks / stopwatchFrequency`는 store 생성에 걸린 시간이다. 전체 입력 hash·출력 내용 검증·state hash 계산·프로세스 시작 시간은 별도다. `storage.metrics`는 실제 Runtime의 replay 계수이며 연산별 의미는 [소스 검증·계측](source-verification.md)을 따른다.

`allocatedManagedBytes`는 생성 구간의 누적 관리 메모리 할당량이다. 동시에 필요한 메모리나 디스크 공간이 아니다. `managedHeapBytes`는 재생 직후 GC의 회수 전 객체도 포함할 수 있는 추정 heap 크기, `peakWorkingSetBytes`는 그때까지 프로세스 전체의 관측 working-set 최고치다. receipt/hash 준비와 Runtime 초기화 비용도 프로세스 메모리에 포함된다. `replayWriteRangeIndex`의 범위별 비용·extent 분포는 [소스 검증·계측](source-verification.md)을 따른다.

매 run 직전 전체 hash를 읽으므로 OS cache가 데워진다. cache를 강제로 비우지 않으며 별도 프로세스·순서·반복 수·같은 input hash와 state hash를 기록해야 한다. 파일 할당은 FILE_STANDARD_INFO 등 별도 계측으로 확인한다. 이 도구의 실행 범위와 바이너리 비교는 [RI 검증](verification/replay-index-cost.md)과 [WI 검증](verification/cumulative-write-range-index.md), 실제 Unity 관측은 [VR 검증](verification/validation-view-readiness-and-replay.md)과 [RU 작은 Unity 비교](verification/unity-replay-comparison.md)를 따른다. 실제 Editor의 재시작은 journal에 새 기록을 쓸 수 있으므로 같은 입력의 offline 개선률로 전체 재개 시간이나 디스크 절감량을 추정하지 않는다.
