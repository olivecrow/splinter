# Windows 제어 파일 게시/읽기 경합

[English](../../verification/control-file-read-races.md)

IPC-01~03은 **scoped-complete-with-limitations**다. 아래 기록은 실제 Windows 파일 교체/읽기와 합성 overlay client 상태의 검증이며 현재 처리 계약은 [NTFS 작업공간](../ntfs-workspaces.md)을 따른다. 이후 고정 후보의 실제 WinFsp/VSS `prepare-worker`·Unity 실행은 [WF-09~12](product-validation-unelevated.md)에서 별도로 확인했다. 그 실행의 heartbeat 게시 오류 기록과 과거 t2의 미확정 원인을 유지하며 이 합성 시험의 성공으로 승계하지 않는다.

## 수정 전 재현

기준은 `62a84c4`와 Windows x64 10.0.26200, .NET 10.0.12다. 제품 `WindowsProcessWorkspaceProvider.WriteAsync/ReadAsync`로 완성된 8 KiB body를 3,000회 게시하고 reader 8개가 동시에 읽었다. writer는 같은 디렉터리의 flush된 임시 파일과 `File.Replace`를 그대로 사용했다. 목적 파일 삭제·내용 덮어쓰기·강제 장애 주입은 없다.

[수정 전 raw 결과](../../../artifacts/storage-foundation/ipc-read-20260928/before-native/result.json): 게시 3,000회, 정상 반환 80,173회 중 null 9,333회, `FileNotFoundException` 2,278회, 공유 오류 32인 `IOException` 2,419회, 읽힌 내용 손상 0회, 최종 sequence 3,000, 남은 임시 파일 0개, 경과 23.27초다. 정상 반환 횟수에는 null이 포함되며 예외는 별도다. null은 파일을 실제로 제거했다는 뜻이 아니다.

stack에서 `CheckPath`의 `File.GetAttributes`와 `FileStream` open 양쪽의 파일 부재, open의 공유 오류를 확인했다. 일부 예외 직후 같은 파일의 존재도 재확인됐다. `File.Exists`의 false는 오류와 부재를 구분하지 않으므로 관측 경로의 판정으로 사용할 수 없다.

[WF t2](product-validation-cold-cost.md)의 정상 unmount 직후 파일 없음 메시지와 같은 실패 종류가 재현됐다. 과거 응답에는 reader 내부 stack/HRESULT가 없어 그 한 건의 정확한 발생 지점·외부 요인까지 확정하지 않는다. 최초 sandbox 실행은 writer의 접근 거부로 중단됐고 완결 결과가 없어 위 집계에서 제외한다. 원시 파일은 보존했다. 이후 실환경 재현과 검증은 비관리자 Windows 실행이다.

## 현재 처리와 보존 경계

현재 계약은 [NTFS 작업공간](../ntfs-workspaces.md)을 따른다. 관측 reader의 guard/open을 유한 시간 재시도하고 이미 열린 stream에서 JSON을 해석한다. JSON 손상은 다시 읽어 숨기지 않는다. 선택적 stop/flush/source 요청 polling만 즉시 missing을 허용한다. reader는 파일·속성·저널을 변경하지 않는다.

추가 회귀 시험은 실제 동시 교체의 완전성/sequence, 첫 게시 대기, 짧은 배타 handle, 지속 공유 실패 뒤 같은 읽기 재시도, 취소, 실제 부재, 선택적 polling, JSON 손상, 접근 거부, reparse parent를 포함한다. 별도 client 시험은 합성 unmounted 상태의 읽기 실패 뒤 같은 stop 재호출과 저널/상태 보존, 다른 owner 거부, 미해결 launch의 unknown을 확인한다. 합성 process/snapshot 영수증은 실제 WinFsp mount·VSS·Unity 지원의 근거가 아니다.

전체 시험의 다른 flush 부하와 겹치면 500회 교체 시험이 30초 제한에 도달한 기록이 있다. Library 구현 후보의 [전체 실행](../../../artifacts/storage-foundation/library-20260929/automatic-first.trx)에서도 같은 제한 실패가 있었고, [해당 9개 시험만 실행](../../../artifacts/storage-foundation/library-20260929/control-file-read-recheck.trx)하면 5.10초에 통과했다. 이 시간 경계 시험 collection은 다른 collection과 직렬 실행한다. 시험 내부 4개 동시 reader·500회 게시·30초 제한·제품의 1초 복구 계약은 유지하며, 이를 과거 Unity 실패 원인의 확정으로 해석하지 않는다.

## 수정 후 판정

[수정 후 raw 결과](../../../artifacts/storage-foundation/ipc-read-20260928/after-native/result.json)는 같은 3,000회 게시·8개 reader·8 KiB body에서 읽기 22,831회, null 0회, 예외 0회, 손상 0회, 최종 sequence 3,000, 임시 파일 0개다. 경과 17.29초이며 전후 한 번씩의 경합 재현이므로 처리량 개선이나 최대 부하 한계로 해석하지 않는다. 수정 전후 제품 DLL과 probe를 별도 보존했다.

`pwsh -NoProfile -File scripts/build.ps1`의 locked restore·Release 빌드·전체 자동 시험은 **527 Passed, 0 Failed, 당시 제외 시험 1 Skipped**다. 추가 시험 12개가 통과했고 실제 동시 교체 회귀는 별도로 500회 게시와 reader 4개를 사용했다. 지속 공유 오류는 약 1초 뒤 예외로 남았고 같은 stop 요청 재호출에서 상태/저널 bytes와 unknown snapshot 관측을 유지했다. JSON 손상·접근 거부·reparse·다른 owner·미해결 launch의 보존도 확인했다.

[빌드 로그](../../../artifacts/storage-foundation/ipc-read-20260928/build.log), [최종 TRX](../../../artifacts/storage-foundation/ipc-read-20260928/final-candidate.trx), [교차 검증 요약](../../../artifacts/storage-foundation/ipc-read-20260928/verification-summary.json), [소스·바이너리·증거 manifest](../../../artifacts/storage-foundation/ipc-read-20260928/evidence-manifest.json)를 보존한다. 문서 UTF-8과 변경 diff 검사를 함께 통과했다. 외부 schema·현재 버전·Core fake 계약·게시 writer·저널 flush/seal은 바뀌지 않았다.

실제 파일 부재를 확정하기까지 관측 읽기 한 번당 최대 약 1초의 재시도 대기가 추가된다. 지속적으로 없는 선택적 stop/flush/source 요청은 이 대기를 생략한다. 파일 열기 자체의 OS 지연을 강제로 중단하는 기능은 아니며 첫 오류 뒤 자의적으로 owner를 인수하거나 데이터를 회수하지 않는다.

SF-02/03은 Partial, SF-04~06은 NotRun을 유지한다. 다음 실제 제품 준비 시험에서 새 후보의 정상 unmount/동일 요청 경로를 별도로 확인해야 하며, 이번 합성 client 성공을 그 시험의 통과로 바꾸지 않는다.
