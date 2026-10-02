# Plastic 실제 A/B 실행 계획

[호환 개발 범위](plastic-compatibility.md)의 한 묶음만 실행한다. 사용자 제공 source는 `C:\Users\olive\Desktop\Development\splinter-unity-plastic`, Unity `6000.3.22f1`·Plastic `11.0.16.9915`·regular/static GUID `f3e87fff-e66c-4820-aec7-ebe2a633962f`다. 현재 원본 Editor는 사용자 소유이며 종료하지 않는다. Pipeline `0.8.0-exp.1`을 사용하며 Coplay/MCP를 추가하지 않는다.

| 항목 | 검토 가능한 실행 값/쓰기 |
| --- | --- |
| home/요청 | 새 빈 `T:\pc1002`, A `plastic-a-20261002` / B `plastic-b-20261002`; source plastic/application unity/base snapshot/storage ntfs-snapshot/기본 16 GiB budget |
| 새 패키지 | `artifacts/plastic-compatibility/package-20261002-01`, 명시 파일 14개, manifest SHA-256 `f82dbe455d3667f7b89cd446ef458985a383693b1272d94163cd95b543feffbe` |
| grant | `artifacts/plastic-compatibility/grants.json`, SHA-256 `ef7e14d401e880ee333a483b4c2e9f4a173c5571634b09173ac4f43cc2eedd1f`. 기존 5개 경로/볼륨 identity 유지, 새 source/home 등록 1개 추가 |
| source/home identity | `B0C924D3:011900000010DC5F` / `22B349E4:000A000000008A2F`, source volume `\\?\Volume{dcb1e12b-4d05-4c46-8279-c89e02953fc3}\` |
| 관리자 쓰기 | 기존 제한 서비스 Update/정상 재시작 1회. 읽기 전용 `service-update-plan.json`·정확한 package/grant/setup hash·기존 protected configuration/grants/ledger hash와 snapshot을 전후 대조. 입력이 다르면 중단/보존 |
| 보존 | 원본 Editor·기존 source/관리 정보·다른 Plastic 등록 3개와 원본 등록·Git fixture·5개 기존 grant·ledger·비소유 snapshot/refs/results |
| 파일 쓰기 | 반환 A/B의 새 최상위 `SplinterPlasticProbe.txt` 등 작은 probe. 캡처 뒤 원본의 별도 새 marker로 지속 편집/격리 확인. 기존 Assets/설정·Plastic 관리 영역을 직접 편집하지 않음 |
| 비용 | 생성/읽기 초기 payload·관리 metadata/index 복제 0과 delta·명시적 file-result·소유 working set·단계 시간 구분. 순간 물리 peak/공유 VSS workspace 귀속 unknown |
| 종료 | A 안정된 독립 checkpoint/release와 B의 bounded 파일 쓰기/소유 Editor 지속. dirty B는 이 검증이 명시적으로 소유한 intent로 discard. 정확한 결과 bytes/hash·mount/provider/delta/소유 snapshot·자원 회수와 기존 자원 보존 확인 |

사용자가 UAC를 포함한 다음 작업을 허용한 요청에 따라 위 계획을 준비하고 진행한다. 일반 CLI/Unity/cm는 비승격이며 관리자 helper만 설치 Update를 수행한다. VSS 상한·볼륨·기존 등록/서버·원본 Editor를 바꾸지 않는다. 준비 시 C: 여유 282,410,065,920 bytes·T: 여유 15,125,950,464 bytes를 읽었으며 capture 직전 현재 공간 guard를 유지한다.

캡처 직전에 원본 저장/import 상태를 확인한다. filesystem crash consistency이며 미저장 Editor 메모리는 기준에 포함되지 않는다. 실패/응답 유실은 같은 key/id/home의 관측으로 확인하고 허용된 다음 행동만 수행한다. 새 key·파일 삭제·권한/설정 변경으로 우회하지 않는다.

이 고정 계획은 2026-10-02 실행했다. 기존 grant/보호 ledger·원본/등록을 보존하며 A/B 생성·독립 Editor·같은 요청·독립 결과·B 지속/명시적 폐기를 관측했다. **A/B의 mount/provider/delta/소유 프로세스는 회수했지만 snapshot 회수는 C: 비소유 snapshot 보호로 보류**됐다. 정확한 작업/snapshot ID·blocker·비용·외부 CLI 발견 실패는 [검증 기록](../verification/plastic-compatibility.md)이 소유한다. 이후 사용자 요청으로 [별도 T: 고정 계획](plastic-reclamation-validation.md)의 마지막 회수까지 한정 마감했으며, 이 C: 작업/snapshot 보류는 그대로 보존한다.
