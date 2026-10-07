# Live journal 쓰기 계측

[English](../write-profiling.md)

`tools/Splinter.WriteProbe`는 Runtime의 고정 작은 합성 쓰기 비용을 비교하는 개발용 도구다. 제품 workspace·VSS·Unity를 만들지 않으며 범위는 [JW 계획](write-profiling.md)을 따른다.

## 고정 입력과 사용법

표준 `pwsh ./scripts/build.ps1`으로 빌드한 뒤, 아직 없는 출력 디렉터리를 지정한다.

```powershell
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run serial-1024 artifacts/write-probe/new-serial
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run parallel-4x256 artifacts/write-probe/new-parallel
dotnet run --project tools/Splinter.WriteProbe --configuration Release --no-build -- run metadata-1024 artifacts/write-probe/new-metadata
```

| profile | 실제 Runtime 호출 |
| --- | --- |
| serial-1024 | 한 파일에 4KiB씩 1,024회 순차 쓰기 |
| parallel-4x256 | 4개 전용 thread가 자기 파일에 4KiB씩 256회 쓰기 |
| metadata-1024 | 한 파일의 마지막 쓰기 시각을 1,024회 변경 |

원본이 없는 synthetic snapshot identity만 사용하고 journal 상한은 실행당 16MiB다. 기존 경로는 거부하며 출력 journal·identity를 자동 삭제하지 않는다. 내용의 독립 기대 hash·metadata를 live 상태와 읽기 전용 replay에서 확인하고, replay 전후 journal hash가 같은지 검증한다. 원본 payload를 읽으려 하면 실패한다.

타이머는 fixture 파일 생성 뒤 호출 loop와 thread 준비/시작을 포함하며 마지막 명시적 Flush·내용 검증·replay는 제외한다. `storage.metrics`는 파일 생성과 마지막 명시적 Flush도 포함하는 provider 누적값이다. `allocatedManagedBytes`는 호출 loop 구간의 누적 관리 할당이고 `peakWorkingSetBytes`는 replay 검증 뒤까지의 프로세스 최고 관측값이다. 서로 같은 구간이나 디스크 비용으로 취급하지 않는다.

같은 probe DLL과 기준/계측 Runtime·Core의 hash를 남기고 새 출력 디렉터리에서 순서를 바꿔 반복한다. 새 journal은 ID·시각이 달라 hash가 달라질 수 있으므로 구현 사이의 내용 동등성은 `contentHash`로, 각 실행의 입력 보존은 자기 `journalHash`로 판단한다. 파일 할당은 별도 Windows 계측으로 잰다. 작은 합성의 차이를 실제 Unity 시간·물리 공간 절감으로 환산하지 않는다.

## 제품 관측의 해석

`OverlayIoMetrics.liveJournalWrites`의 계약은 [소스 검증·계측](source-verification.md)을 따른다. live commit의 고정 6개 연산만 집계하며 replay는 포함하지 않는다. 연산별 단계 시간은 gate를 얻은 뒤의 비용이고 gate 대기는 호출별 누적값이다. 여러 대기자의 시간이 겹칠 수 있으므로 Editor 벽시계와 더하지 않는다.

flush 전후 다른 mutation 호출 수는 Runtime gate 앞에서 기다리는 호출의 표본이다. 읽기 요청, WinFsp나 OS 안의 대기, 표본 사이에 발생한 최대 동시성은 포함하지 않는다. 대기 호출은 나중에 변경 없음이나 입력 거부로 끝날 수 있어 묶을 수 있는 durable commit 수를 보장하지 않는다. 현재 모든 acknowledged commit은 개별 durable flush를 완료한 뒤 응답한다.

Windows의 `WinFspOverlayFileSystem`은 변경 callback 전체에 별도의 `mutationGate`를 적용한다. 따라서 이 어댑터의 쓰기 요청은 Runtime에 들어오기 전에 직렬화되며, Runtime 대기 표본이 0이어도 어댑터/OS 대기가 없다는 뜻은 아니다. 직접 Runtime을 호출하는 `parallel-4x256`과 실제 Unity의 동시성 조건은 다르다. append 위치·constrained I/O·파일 길이 등의 복합 검사를 지키는 이 경계를 검토하지 않고 Runtime 내부에 flush 묶음만 추가할 수 있다고 판단하지 않는다.
