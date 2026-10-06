# 작은 Unity 반복 작업의 동일 쓰기 공간 비교

실행일: 2026-10-02. 기존 warm fixture의 편집·같은 작업 재개·정상 Editor 재시작·독립 결과/release 한 쌍을 실행했다. **이 입력의 종료 직전 저널 할당 차이는 256KiB, 약 0.16%로 작다.** [합성 반복 입력의 96.1%](duplicate-private-writes.md)를 실제 Library 절감률로 승계하지 않는다. 이 단위는 scoped-complete-with-limitations이며 SF-02 전체 Partial과 M0의 한정 완료 경계는 유지한다.

## 고정 입력과 실행 경계

- 원본은 등록된 `T:/ua-source`, clean `codex/library-20260929 @ f71e4e244ba9b085aa0fc439336e4c60ee3d5d5d`다. 기존 Library의 파일 할당은 1,871,929,368B이며 처음과 끝이 같다. home은 기존 `.local/wl29`, Unity는 `6000.3.22f1`이다.
- `93080be`의 Runtime과 `05f946a`의 Runtime을 고정했다. CLI 묶음의 실행 파일 차이는 `Splinter.Runtime.dll` 하나다. 실제 provider의 묶음도 각각 `7AAC910D9542CF632B1397C6A4E8FF6F83D7A5B1790044E6D13E9D6CD9F7CD78` / `58FF65F386965EC329101C2081AB3334D4515E389B50762CEF12A7FC8446B1BC`로 대조했다.
- 각 작업의 반환 경로에서 `SplinterSnapshotProbe.Calculate` offset 10→110→111을 적용했다. 현재 소스 검증·성공 validation과 실제 loaded 코드 값 13→113→114, 프로젝트/PID·MVID를 확인했다. 같은 ensure는 ID·digest·snapshot·provider·실행 중 Editor를 유지했다. 정상 stop/start로 Editor PID는 바뀌고 provider는 유지됐다.
- 두 Runtime을 **순차** 실행하고 각 결과를 보존한 뒤 release했다. 새 서비스/설치/grant/볼륨 구성·원본 통합·강제 종료·VIVOBOOK·대형/cold·새 장애 행렬을 포함하지 않는다. 제품 코드·계약은 이 비교에서 변경하지 않았다.

원시 증거는 [plan](../../artifacts/storage-foundation/unity-space-20261002-004005-467/plan.json), [고정 파일 hash](../../artifacts/storage-foundation/unity-space-20261002-004005-467/binaries.json), CLI JSON·monitor·loaded code·단계별 장부와 [최종 판정](../../artifacts/storage-foundation/unity-space-20261002-004005-467/result.json)이다. 이 디렉터리의 실행/계측 어댑터는 이번 입력의 시험 자료이며 새 제품 명령이나 일반 harness가 아니다.

native의 고정 DLL은 해당 변경을 커밋하기 전 Release 빌드이며 informational version은 각각 `+b526646` / `+93080be`다. 이 작업의 필수 재빌드는 `+05f946a`와 Runtime hash `7107163364D74A17C87F192900C4D24E7B69B2AB3F4AA16571354F596A08F42D`다. 동일 쓰기 실행 소스 hash `C63AD2F4B8BCA736D1872F11C54C55CD5B668D1725D576929831997B0DC04A90`는 같지만 바이너리는 다르므로, [빌드 identity](../../artifacts/storage-foundation/unity-space-20261002-004005-467/runtime-build-identity.json)와 [검증 manifest](../../artifacts/storage-foundation/unity-space-20261002-004005-467/qualification.json)에 native와 재빌드 자동 회귀의 자격을 구분한다. 재빌드의 native 통과로 자동 승계하지 않는다.

## 실제 파일 할당과 Library 분류

`FILE_STANDARD_INFO.AllocationSize`로 소유 저널 파일을 관측했다. 기존 Library를 추가 물리 복사로 합산하지 않는다. Library의 private 쓰기는 이미 저널 안에 있다.

| 관측 B | 기존 Runtime | 동일 쓰기 재사용 Runtime |
| --- | ---: | ---: |
| 첫 ready 저널 파일 할당 | 129,892,352 | 128,909,312 |
| 첫 편집 뒤 저널 파일 할당 | 139,984,896 | 143,392,768 |
| 재시작 뒤 저널 파일 할당 | 149,291,008 | 149,225,472 |
| 종료 직전 저널 논리 길이 | 161,642,716 | 161,414,505 |
| 종료 직전 저널 파일 할당 | 161,742,848 | 161,480,704 |
| 기록된 Library payload, 논리 | 132,522,134 | 132,308,082 |
| 유효 Library private 범위, 논리 | 96,298,264 | 96,189,510 |
| 전체 무효 payload, 논리 | 37,623,220 | 37,508,986 |

[기존 저널 분류](../../artifacts/storage-foundation/unity-space-20261002-004005-467/before-journal-accounting.json)와 [현재 분류](../../artifacts/storage-foundation/unity-space-20261002-004005-467/after-journal-accounting.json)는 frame checksum·sequence·읽기 전후 크기/mtime와 **제품이 보존한 정확한 stable-journal-v1 seal**을 대조했다. 소유 provider의 쓰기 핸들을 허용하는 읽기 전용 어댑터를 사용했으며 seal/hash가 다르면 거부한다. snapshot payload를 복사하거나 읽기 hydration하지 않았다. 두 ensure의 최초 payload 복사와 read cache는 0이다.

종료 직전 파일 할당은 262,144B 줄었지만 첫 편집 뒤에는 현재 실행의 할당이 더 컸다. 단계 전체가 일관되게 줄었다고 판정하지 않는다. 같은 코드 입력이어도 snapshot·작업 경로·시각·Editor/cache 상태·생성 파일과 실제 callback 열은 다르다. 유효 private Library의 크기도 약간 달라 **0.16%는 이 한 쌍의 관측 차이**이며 최적화 하나의 정확한 인과적 절감률·일반 프로젝트 수치가 아니다. 관리 frame의 새 timestamp/sequence와 개별 flush 계약은 기존 자동/작은 WinFsp 검증을 따른다.

현재 유효 private payload 104,799,904B 중 Library는 96,189,510B(약 91.8%)다. 큰 유효 경로는 `Library/Bee/1900b0aE.dag`, `.dag.json`, `.dag.payloads`, `-inputdata.json`과 post-processed DLL이다. 이 입력에서는 동일 private 쓰기 재사용만으로 Library 부담이 크게 줄지 않았다. 다음 공간 개선은 남은 새/변경 Library 기록을 대상으로 판단해야 하며, 동일 쓰기 비중·부분 변경 절감 잠재량은 아직 측정하지 않았다. 무효 payload의 논리량을 실제 정리 절감량으로 사용하지 않는다.

## 보존·회수와 실패 비용

기존 작업 `80b9144554356e112e354869`의 결과는 `cb3a2c0cabf468dedbff5e26575bc33c41ec7db7`, 현재 작업 `7c3c8e2114a7639208f3de8d`의 결과는 `4e512796b57d3e0750844427e691e3b1a2f3f603`이다. 회수 후 두 결과의 스크립트 blob은 `42fdea2ed8529f4352d918c9ddc0824206ab0147`로 같고 offset 111을 유지한다. 각각의 결과 branch/ref를 보존했다. 실제 mount·Git 등록·delta는 부재, 정확한 snapshot은 missing/released, 소유 process/working set/private bytes·핸들은 0이다. 원본 94개 추적 파일·HEAD/clean, 기존 workspace 기록·refs와 원본 Editor identity를 보존했다.

기존 Runtime의 최초 start와 두 Runtime의 restart는 `SourceChanged`를 반환했다. 이미 게시한 실행 영수증·소유 Editor를 확인해 같은 ID로 관측/재시도했고 새 작업이나 강제 종료로 우회하지 않았다. 기존 최초 start의 추가 현재 소스 확인에서 제한된 도구 실행 환경의 접근 거부 `InternalError`도 보존하고 일반 Windows 사용자 실행으로 재확인했다. source fingerprint는 같았으나 진행 중 저널 변경을 더 넓은 OS 원인으로 확정하지 않는다. 첫 start/restart가 항상 한 번에 성공한다는 자격은 없다.

첫 편집의 소유 파일 계측에는 경로 부재 오류가 한 번 있었다. 편집/검증은 반복하지 않고 실패 증거와 새 계측을 남겼다. 현재 재시작의 전체 control 장부도 `state.json`/임시 경로 부재로 allocatedBytes=unknown·관측 subtotal을 남겼다. 해당 소유 저널 표본과 마지막 장부는 완전하며 unknown을 0으로 읽지 않았다. 모든 67개 CLI monitor는 비승격이고 consent 프로세스의 50ms 관측은 0이다.

회수 뒤 전체 제품 roots의 기준선 대비 추가 할당은 기록 335,008B·두 Runtime의 재사용 runner 묶음 2,024,112B·Git 공통 결과 9,904B다. 기존 validation roots와 원본 Library는 같다. 시험 증거는 별도 비용이다. [최종 장부](../../artifacts/storage-foundation/unity-space-20261002-004005-467/storage-ended.json)의 공유 VSS는 unknown이며 MFT/디렉터리·OS cache·순간 전체 peak와 총 물리 공간 절감률을 주장하지 않는다. 단계 시간/실패·재시도는 monitor에 보존하며 준비 시간 SLA나 게임/시스템 메모리 절감으로 읽지 않는다.

필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 경고/오류 0, **765개 중 764 통과·실패 0·당시 제외 시험 1 skip**이다. [build 로그](../../artifacts/storage-foundation/unity-space-20261002-004005-467/build.log)와 [TRX](../../artifacts/storage-foundation/unity-space-20261002-004005-467/automated.trx)를 보존했다. 이번 비교를 마감하고 추가 반복/지원 확대를 자동 개시하지 않는다.
