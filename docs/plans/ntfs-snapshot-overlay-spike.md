# NTFS 스냅샷과 변경분 작업공간 검증 계획

상태: **SO-01~06 fixture 증거 확보·회수 완료, 제품 workspace backend의 NI-01~08 내부 알파 검증·회수 완료**. 독립 [실험 계약](../snapshot-overlay.md)과 [SO 기록](../verification/snapshot-overlay.md), 제품 [NTFS 계약](../ntfs-workspaces.md)과 [NI 기록](../verification/ntfs-workspace-integration.md)을 구분한다. ReFS 증거는 [내부 알파 계약](../storage-and-resources.md)과 [SR 검증](../verification/storage-and-resources.md)에 유지한다. 최초 전체 기준본 준비를 정상 NTFS 경로에서 제거한다.

## 고정된 요구사항

- NTFS 원본의 위치와 파일시스템을 유지한다. 최초 등록·작업공간 생성·읽기에서 소스·리소스·Library의 전체 데이터 사본을 만들지 않는다. 별도 1TB 기준본, 필수 cold import, ReFS/VHDX 이관을 해결책으로 삼지 않는다.
- **원본은 작업 중에도 계속 수정할 수 있어야 한다. 각 작업은 시작 시점의 불변 스냅샷을 참조한다.** 작업 기간 내내 원본을 읽기 전용으로 묶거나 Editor를 종료하는 대체안은 금지한다. 캡처 순간의 짧은 저장·임포트 체크포인트가 필요하면 범위·시간·영향을 검증한다. 미저장 메모리 상태까지 캡처했다고 주장하지 않는다.
- 작업공간은 기준 스냅샷 읽기와 전용 변경 저장소 쓰기를 합성한 일반 파일시스템 경로다. Unity·Git·에이전트·자식 프로세스·mmap I/O 모두 이 경계를 사용해야 한다.
- 큰 파일의 일부 수정은 블록/범위 단위로 저장한다. 최초 쓰기에서 파일 전체를 복사하는 copy-up은 합격이 아니다. 삭제·rename·truncate·확장은 작업별 메타데이터와 변경 범위로 처리한다.
- 읽은 내용의 작업별 영구 복제는 금지한다. 캐시의 상한·소유권·사용량·회수를 명시하고 대량 hydration으로 우회하지 않는다.
- 추가 공간은 기준 상태 보존에 필요한 이전 블록, 작업별 변경/신규 데이터, 메타데이터, 제한된 캐시와 결과에 비례해야 한다. 공급자의 최소 할당도 측정한다. Unity의 실제 재작성과 도구의 불필요 복제를 구분한다.
- 결과를 보존한 뒤 소유 Editor/자식, 파일시스템 제공 프로세스, 핸들·마운트·변경 저장소를 회수한다. 변경분을 해석하는 데 필요한 기준 스냅샷은 마지막 의존성이 해제되기 전에 삭제하지 않는다.
- AI가 적은 호출로 상태·재개·회수를 판단하도록 JSON 계약과 명시적 실패 이유를 제공한다. 숨은 전체 복사나 원본 쓰기 차단 fallback은 없다.

## 후보와 공통 계약

Windows 우선 후보는 **VSS 시스템 공급자의 CoW 스냅샷 + WinFsp 파일시스템 + 범위별 변경 저장소**다. VSS는 읽기 전용 기준을 제공할 뿐 작업자 쓰기 overlay를 제공하지 않으며, WinFsp는 변경 저장 정책을 대신 구현하지 않는다. 후보를 이미 지원하는 기능으로 표시하지 않는다.

VSS의 OS/에디션·권한·공급자·볼륨 지원, 동일 snapshot ID 재연결, 유지 공간 증가, 외부 삭제·소실·공간 압박을 검증한다. 볼륨 단위이므로 프로젝트 밖 쓰기의 diff area 비용도 분리한다. 최초 full-copy 공급자는 거부한다. 실제 재부팅 시험 없이 재부팅 후 복구를 통과 처리하지 않는다.

Unity Library는 소스·패키지·설정과 일관된 캡처 시점을 검증한다. 기존 캐시를 작업별로 읽고 필요한 재작성만 허용한다. 실패할 때 원본이나 모든 작업의 Library를 삭제하지 않는다.

ProjFS 읽기 hydration, 일반 OverlayFS 파일 copy-up, writable hardlink/junction은 요구사항을 충족하는 구현이 아니다. ReFS는 원본이 이미 호환 볼륨에 있는 경우의 선택적 최적화다. Linux/macOS는 공통 변경 저장소와 FUSE 계열 OS 어댑터를 분리해 확장할 수 있게 하되 이번 실제 지원 범위에는 넣지 않는다.

Core 계약은 기준 확보/유지/해제, mount/reconnect/flush/unmount, 변경분·증거·자원 관측으로 나눈다. 현재 `IWorkspaceStorageProvider.CloneAsync`만으로 충분하다고 가정하지 않는다. 플랫폼 API는 어댑터에 둔다. CLI 응답·Job Object·Git no-checkout/index/결과 보존은 재사용한다. 구현 시 schema·fake·capabilities·문서를 함께 갱신한다.

## 작업 순서와 의도

1. **지원 가능성 판정:** 현재 머신의 NTFS/VSS/WinFsp 지원과 설치 상태를 확인한다. 원본 이동이나 전체 준비 사본 없이 안정적인 기준과 파일 I/O 경계를 만들 수 있는지 판정한다. 필요한 설치·권한은 구체적인 최소 변경을 준비한 후 처리한다. 기존 ReFS 설치 승인을 새 드라이버 설치 승인으로 해석하지 않는다.
2. **작은 저장소 실험:** 공통 계약/fake와 실제 NTFS의 비희소 데이터로 SO-01~04를 재현한다. 제품 전체를 먼저 재작성하지 않는다. 동일한 작은 쓰기에서 파일 크기를 늘려 데이터·메타데이터·정렬 비용을 구분한다.
3. **Git/Unity 연결:** 가상 경로에 실제 linked worktree와 독립 index를 연결하고 SO-05를 수행한다. `.git`을 복제하지 않는다. 원본/A/B가 각자 수정·임포트·컴파일해야 한다. 스크립트·패키지·에셋 변경 후 해당 Editor에 포커스를 맞춰 새로고침과 실제 완료를 확인한다.
4. **장애와 회수:** SO-06에서 mount/쓰기/flush/결과 보존/해제 중단, 제공 프로세스 종료, 디스크 부족과 스냅샷 소실을 주입한다. 유일한 결과와 원본을 보존하며 재조정한다. 기존 ReFS clone/seal 복구 확장은 이 관문보다 우선하지 않는다.
5. **통합 판단:** 코드·재현 명령·측정·원시 증거·한계를 납품한다. 실패한 후보는 원인과 대체 실험으로 이어가되 요구사항을 약화하지 않는다. 여섯 관문의 실제 증거가 있어야 완료다. 못한 항목은 NotRun/Unsupported와 해결 조건을 명시한다.

## 여섯 검증 관문

| ID | 검증과 의도 | 통과 조건 | 필수 증거 |
| --- | --- | --- | --- |
| SO-01 | NTFS 최초 생성: 초기 1TB 준비 비용 제거 | 원본 이동·전체 데이터 복사 없이 A/B 생성, 원본 쓰기 가능 | 모든 관련 볼륨의 전후 할당량, snapshot/provider ID, Git 등록·HEAD/index, 초기 payload 복사량 |
| SO-02 | 대형 파일 전체 읽기: 지연 복제 우회 방지 | 전체/반복 읽기에도 작업별 영구 사본이 생기지 않음 | 실제 데이터 크기/hash, 읽기 바이트, 할당량·캐시·상한 |
| SO-03 | 작은 범위 수정: 파일 copy-up 배제 | 추가 저장이 전체 파일 크기에 비례하지 않고 변경 범위로 설명됨 | 여러 파일 크기의 동일 4KiB 쓰기, 경계·mmap·truncate/확장·rename·교체, delta/메타데이터 할당 |
| SO-04 | 원본/A/B 동시 변경: 시작 시점 유지 | 원본은 수정 가능, 기존 A/B 기준 불변, 각 작업 변경 독립 | 원본 t0/t1, 각 snapshot ID, 수정·삭제·rename 전후 hash, 새 작업의 새 시점 |
| SO-05 | Unity 실행·컴파일: 앱 호환성 입증 | 메모리 매핑·잠금·변경 알림·자식 I/O·실제 컴파일 동작, 증가량 설명 | 원본/A/B 경로·PID·세대, refresh/compile/새 코드 실행, Library 재작성·RAM·시간·디스크 |
| SO-06 | 중단·재개·회수: 기준과 변경분 공동 수명 | 동일 기준과 변경분 재연결, 결과 보존 후 소유 프로세스·핸들·마운트·임시 공간 회수 | 장애별 상태/재시도, snapshot 유효성, 다른 작업 보존, 소유 메모리 0, 경로·Git·마운트 회수 |

현실적인 크기의 합성 데이터에서 단계적으로 늘린다. 논리 크기만 1TB인 sparse/압축/모두 0인 파일을 유일한 공간 절감 증거로 삼지 않는다. 실제 1TB 사용자 프로젝트를 임의 시험 대상으로 삼지 않는다. 볼륨 전체 잡음과 OS 파일 캐시를 분리한다.

## 측정과 완료 보고

최초 준비부터 최종 회수까지 관측한다. 지표 후보는 `initialPayloadCopiedBytes`, `snapshotPreservedBytes`, `deltaAllocatedBytes`, `metadataAllocatedBytes`, `readCacheBytes`, `resultStoreBytes`, `peakAdditionalBytes`, `remainingTemporaryBytes`다. 관측 가능성·단위를 명시하고 논리 크기와 물리 할당을 구분한다. Git/LFS 결과 저장·Unity 재작성·원본 이전 블록 보존도 숨기지 않는다.

전체 원본 해시를 매 호출 반복하지 않는다. 안정적인 snapshot 식별과 변경 기록을 활용하되 시험의 독립 hash 검증은 생략하지 않는다. 파일 감시 이벤트만으로 이전 내용 복구나 쓰기 격리를 보장하지 않는다.

납품물은 실험 코드·계약/fake, 실행 증거, 여섯 상태, 플랫폼/앱 제약, 회수 결과와 통합 판단이다. 기존 56개 시험과 약 297MiB 관측은 ReFS 반복 생성에 대한 별도 증거이며 SO 통과로 재사용하지 않는다.

## 제품 통합 전에 남은 조건

이 조건 중 다음 구현 단위는 [NTFS workspace 1차 통합 범위](ntfs-workspace-integration.md)에 확정했다. coordinator·결과·공간 보호와 제한된 복구를 NI-01~08로 검증하고, 재부팅·범용 파일/Unity 지원의 나머지 조건은 아래에 유지한다.

1. workspace coordinator의 durable operation·snapshot 의존성/lease·result manifest는 1차 코드에 연결했다. NI의 실제 생성·재개·범위 저장·Unity·실패 보호를 검증했다. 실제 dirty 원본 통합 차단과 최종 소유 VSS 회수까지 확인해 이번 NI 범위를 완료했다. 일반 지원을 확정하지 않으며 후속 조건은 아래와 NI 기록을 따른다.
2. snapshot 보존 공간의 admission/압박 정책, VSS 재부팅 후 동일 identity 검증과 임의 명령 경계 중단 시험을 보강한다. 물리 볼륨 고갈·전원 손실은 별도 통제 환경에서 수행한다.
3. Unity의 초기 대량 임포트·전체 metadata 계측 지연·누적 저널 쓰기 비용을 개선하고, 긴 Unity 패키지 경로의 초기 로딩 오류, 패키지/에셋 변경·dirty/LFS checkpoint·지원 파일 특성을 확대 검증한다. PID/port whiteout과 실제 loaded-code 관측을 유지한다.
4. 파일시스템을 일반 지원으로 배포하기 전에 driver 버전 정책, ACL/ADS/hardlink/reparse의 명시적 거부·지원 계약, 일반 프로젝트 전체 fingerprint/validation을 정한다. Library 전체 삭제나 복사로 우회하지 않는다.

## 근거

- [VSS 공급자와 CoW](https://learn.microsoft.com/en-us/windows/win32/vss/providers)
- [VSS 시점 일관성과 Writer 없는 앱](https://learn.microsoft.com/en-us/windows/win32/vss/shadow-copy-creation-for-providers)
- [WinFsp 기능](https://winfsp.dev/doc/NTFS-Compatibility/)
- [ProjFS hydration](https://learn.microsoft.com/en-us/windows/win32/projfs/cache-state)
- [OverlayFS copy-up](https://kernel.org/doc/html/latest/filesystems/overlayfs.html)
