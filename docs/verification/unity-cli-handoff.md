# Unity CLI의 정확한 A/B 인계 검증

2026-10-03의 작은 Windows x64/NTFS·Plastic 입력에서 **반환 경로를 지정한 Unity CLI 작업·A 정상 종료/회수 뒤 B 후속 작업·두 독립 결과와 마지막 snapshot 회수**를 scoped-complete-with-limitations로 마감했다. 프로그램 수정·패키지 교체 없이 기존 연결 절차로 실행했다. `status`/`pipeline list`의 worker 누락과 첫 eval 타임아웃을 유지하며 자동 발견·다른 도구/입력의 일반 지원으로 확대하지 않는다. 실행 범위는 [인계 계획](../plans/session-workflow-and-installation.md#후속-범위-unity-cli의-ab-인계), 현재 사용 절차는 [Unity 작업공간](../alpha-workspaces.md#unity-cli의-정확한-대상-선택)을 따른다.

## 고정 입력과 실행 근거

| 항목 | 실행한 범위 |
| --- | --- |
| 제품 | `development @ 57d4439`, 프로그램 소스는 `4e262b7`과 같음. 이번 변경은 지침/검증 문서이며 실제 빌드 파일 hash는 `artifacts/unity-cli-handoff/product-binaries.json` |
| 도구 | Unity `6000.3.22f1`, 설치된 Unity CLI `1.0.0-beta.5`, 기존 Pipeline `0.8.0-exp.1`. 로컬 version/help와 반환 명령 schema를 확인. 새 설치/업데이트 없음 |
| 원본/home | 기존 `T:\plastic-fixture-20261002`·`T:\pt1002`, `source=plastic`, `application=unity`, `base=snapshot`, journal budget 17,179,869,184 bytes. 기존 regular/static 등록/서비스 사용; 새 grant·source 복제 없음 |
| A | key `cli-t-a-20261003`, id `78cc98798c232f2872f8a9dd`, digest `6eee03de35a50713f8d4416e`, 반환 root `T:\pt1002\workspaces\78cc98798c232f2872f8a9dd`, PID 90496 / start ticks `639265517648992204`, Pipeline 7801 |
| B | key `cli-t-b-20261003`, id `bbe46e538e9fe1e10a5d3d7c`, digest `5a20e9c46eee3aaf0af8fcc6`, 반환 root `T:\pt1002\workspaces\bbe46e538e9fe1e10a5d3d7c`, PID 53928 / start ticks `639265517999023455`, Pipeline 7802 → 7801 |
| 기준 | 두 작업의 baseline `sha256:8a58a18cac8d41149bafa58e886dcd25b78fd8162ea7cd5b19e9533fef0c9e0b`, 62개 파일 / 245,523 bytes. T: 원본의 이전 검증 marker를 포함하며 원본은 이번 작업에서 편집하지 않음 |
| 원본 Editor | C: Plastic 프로젝트의 PID 68624, 시작 `2026-10-02T21:34:17.425636+09:00`, Pipeline 7800 ready. 시작/종료/회수하지 않음 |
| 원시 근거 | `artifacts/unity-cli-handoff/`: 제품/도구 JSON·단계별 시간, schema, source/등록 전후 hash, 선택한 프로세스 식별자, 빌드/TRX·바이너리 hash, 독립 객체 검증과 읽기 전용 관리자 VSS/ledger/grant 대조 |

Splinter의 launch receipt와 heartbeat의 정확한 PID/시작 ticks 일치를 별도로 확인했다. Unity CLI eval에서 큰 정수 ticks를 반환하면 CLI JSON 숫자 처리를 거치며 정밀도가 달라질 수 있어 그 숫자를 정확한 lease 비교에 사용하지 않았다. 에이전트가 이 값을 조회할 때는 C# `Ticks.ToString()`처럼 문자열로 반환해야 한다. 인증 token·전체 명령행·license 값은 공개 근거에 싣지 않는다.

## 실제 인계와 실패의 구분

| 관문 | 판정과 실제 결과 |
| --- | --- |
| UH-01: 정확한 연결/발견 | Passed with limitations. A/B `command --project-path <반환 root>`의 catalog/target·eval 실제 project/PID를 확인. `status --project-path`는 둘 다 STATUS_NO_INSTANCES, 일반 권한 `pipeline list`는 원본만 반환. sandbox 밖에서도 같은 제한을 확인했고 누락을 Editor 부재로 판정하지 않음 |
| UH-02: 편집·loaded 결과·정상 종료 공존 | Passed with limitations. 각 `Assets/SplinterCliProbe.cs`의 `a-one`/`b-one`, A 수정 후 `a-two`, A stop/release 뒤 같은 B PID의 후속 `b-two`·외부 recompile completed/failed=false를 확인. A 수정 전후 B 값/MVID는 동일. 원본 소스/관리 정보·등록·Editor/Pipeline 연결 보존 |
| UH-03: 독립 결과/회수 | Passed. 두 checkpoint·release와 실제 객체 hash/크기/최종 probe bytes 검증, mount/provider/delta/소유 프로세스/핸들/메모리 0 또는 부재, 마지막 소유 VSS 두 개 회수와 T: snapshot 0. 기존 C: 보류를 회수 성공으로 승계하지 않음 |
| UH-04: 기존 계약/비용 | Passed with limitations. 필수 locked restore·Release 빌드 0 warning/error, 844개 자동 시험 통과/0 실패/당시 제외 시험 1 skip. 현재 embedded catalog로 제품 JSON 응답 28개 유효. 단계 시간/범위 저장과 독립 결과 비용을 기록; 전체 물리 peak·RAM peak/절감·VSS 작업별 귀속은 unknown |

`a-catalog-initial.json`은 descriptor 게시 전 COMMAND_FAILED다. 각 Editor의 첫 eval과 A recompile 후 첫 eval은 `Main thread operation timed out after 5000ms`였고, **읽기 전용인 동일 대상 eval을 한 번 재시도**해 loaded 값과 실제 대상을 확인했다. 실패를 삭제하거나 첫 시도 통과로 표시하지 않는다. 첫 타임아웃의 내부 원인은 미확정이며 일반 Unity 작업/쓰기 요청을 자동 재시도하는 자격으로 확대하지 않는다.

A의 최초 probe MVID는 `597a3b2d-ee72-4ff7-8617-7b1c4b67fec6`, 수정 후 `9c839321-1df7-4a6f-95b1-b1ad544484de`다. B는 A 편집/stop 동안 `59385ca1-487b-4fcd-9dc4-5c7755edf644`를 유지했고 후속 편집 뒤 `62d4f4b6-55dc-4c52-b9b3-50e56ab54f4c`로 바뀌었다. B의 reload 때 포트가 7802에서 이미 종료된 A의 7801로 바뀌어도 반환 root를 지정한 CLI가 같은 B PID에 연결했다. 포트 번호는 지속 작업의 식별자가 아니다.

## 독립 보존·보호와 비용

| 항목 | 최종 근거 |
| --- | --- |
| A result | `sha256:d1554d89caf3435549ebe8c85e6a6a6fa87ad850fb57c96a9582ad2162bca54a`, 64파일 / 245,661 bytes, 고유 객체 64개. `a-two` 소스와 meta 포함 |
| B result | `sha256:ea3034db302fc699605a80b7b7c222d9bb86ed4842d2f8711f2afaeebcdb4f13`, 64파일 / 245,661 bytes, 고유 객체 64개. `b-two` 소스와 meta 포함 |
| 회수 | A `{4E09CAEF-7771-4962-B99A-7CECC79F800C}`, B `{CBCF82ED-583F-4898-AE8B-B589F3AB4C60}` reclaimed. `18-a-final.json`/`19-b-final.json`에서 둘 다 released·snapshotAvailable/mounted/providerAlive/deltaPresent=false, openHandles/owned activeProcesses/workingSet/privateBytes=0 |
| 기존 보호 | C:/T: 선택 소스 및 `.plastic` 전후 내용 비교, 기존 Plastic 등록 5개 동일. 읽기 전용 UAC inventory에서 기존 C: snapshot 6개·보호 ledger 파일 1,418개의 hash와 정확한 grant 7개 유지. 선행 C: A reclaim-pending/B discard-reclaim-pending은 관측만 함 |
| 일반 권한 단계 시간 | ensure A 30,619ms/B 32,110ms, A stop 58,663ms·checkpoint 10,025ms·release 14,290ms, B checkpoint(정상 stop 포함) 49,842ms·release 20,879ms. 일반 workspace CLI callerElevated=false. 관리자 Apply/설치 없음; 마지막 보호 대조만 읽기 전용 UAC 1회 |
| 범위 저장 | 두 ensure의 initialPayloadCopiedBytes/journalBytes/payloadBytes/readCacheBytes=0. 회수 직전 누적 A journal/payload 136,298,076/123,511,979 bytes, B 136,120,355/123,349,610 bytes. Unity Library/관리 파일 쓰기를 포함하며 회수 후 현재 delta 할당이 아님 |
| 최종 파일 할당 | `storage-final.json`의 같은 home records 전체 1,747,696 allocated bytes/335 files. 이전 PC 결과/기록도 포함하며 이번 A/B만의 비용이 아님. 그 안의 독립 file-result payload 932,176 allocated/736,837 logical bytes는 이미 records에 포함됨. 공유 runner 7파일 1,139,032 allocated bytes, 원본 T: Library 1,852,496,112 allocated/1,819,103,541 logical bytes·30,748파일·complete=true는 별도 기준선 |
| 공유 VSS | 최종 T: used/allocated=0, limit 10,737,418,240 bytes. C: used 27,434,254,336/allocated 27,931,082,752/limit 39,985,348,608 bytes는 기존 공유 영역이며 이 작업에 귀속하지 않음 |

실제 결과 검증은 worker 경로와 snapshot이 회수된 뒤 `files/baseline.json`·`result.json`의 모든 객체를 직접 읽어 SHA-256/크기를 확인했다. 원본의 Library·다른 기록·공유 runner와 독립 결과/저널을 중복 합산하지 않는다. 일반 계측 shell의 VSS 관측 실패는 0으로 읽지 않고 위 관리자 inventory의 실제 0과 구분한다. 비용은 한 묶음의 관측이며 시간 SLA나 저장/메모리 절감률이 아니다.

## 유지하는 제한과 다음 판단

이번 정상 경로는 **현재 CLI/Pipeline의 정확한 반환 경로를 사용한 작은 T: Plastic 입력**이다. 자동 목록 발견의 수정/CLI 업데이트, Coplay/MCP 연결·공유 서버 공존, Git fixture의 별도 전체 인계, Play/Test/build/캡처, 원본 integrate/check-in, 대형/ARM/OS 장애·새 설치/binding·개발용 view는 실행하지 않았다. Splinter가 외부 컴파일 성공 gate를 제공한다는 뜻도 아니다.

선행 C: 두 소유 snapshot은 기존 비소유 VSS blocker로 보류돼 있다. 이번 T: 회수로 해결됐다고 표시하거나 다른 작업의 snapshot을 지워 우회하지 않는다. 새 도구/버전/입력에서 목록 발견이 필수라면 현재 실패와 필요한 최소 변경을 먼저 범위로 확정한다. 이 대표 A/B 단위와 마감한 M0를 자동 반복하지 않는다.
