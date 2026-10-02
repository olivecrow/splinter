# Plastic의 별도 볼륨 회수 검증

2026-10-02 사용자가 현재 변경의 커밋과 다음 작업 실행을 요청했다. 앞서 질문한 T: 별도 fixture의 한 A/B 회수 검증을 다음 범위로 진행한다. [기존 C: 실행](../verification/plastic-compatibility.md)의 보류 작업과 비소유 snapshot을 보존한다.

질문은 **이전 snapshot이 없는 T:의 작은 Plastic 프로젝트에서 A의 독립 결과/release 동안 B가 계속 작업하고, 이어서 명시적 B discard와 마지막 소유 snapshot 회수를 끝낼 수 있는가**다. 전체 장애 행렬·새 harness·MCP/CLI 발견 수정·원본 반영·check-in은 포함하지 않는다. 현재 후보 `4e262b7`의 기존 제품 CLI를 사용한다.

## 고정 입력과 쓰기

- 새 fixture `T:\plastic-fixture-20261002`, 별도 home `T:\pt1002`. 모두 기존 경로가 없음을 확인했다. 사용자 원본을 이동하지 않는다.
- 사용자 C: 프로젝트의 기존 소스 61개/245,487 bytes를 내용 hash로 확인해 테스트 입력으로만 복사한다. `.plastic`·Library·생성 솔루션/프로젝트·원본 이후 marker를 복사하지 않는다. 이 작은 fixture 준비는 제품 workspace 생성/읽기의 복제 fallback이 아니다. 실제 A/B는 동일 NTFS/VSS/WinFsp 범위 저장 경로를 쓴다.
- 실제 읽은 selector의 repository `splinter-unity-plastic/splinter-unity-plastic@20066124403170@cloud`를 명시해 새 regular/static Plastic workspace `splinter-plastic-reclamation-20261002`를 등록한다. 새 서버 repository/계정·checkout/update/check-in·기존 등록 이동/삭제는 하지 않는다. 새 fixture 등록은 source 준비이며 A/B는 등록하지 않는다.
- Unity `6000.3.22f1`, Plastic `11.0.16.9915`, collab-proxy `2.12.4`, Pipeline `0.8.0-exp.1`. 새 fixture의 소유 batch Editor로 Library를 준비하고 정상 quit한 뒤 capture한다. C: 사용자 Editor는 종료하지 않는다.
- 관리자 읽기 전용 사전 inventory에서 T: snapshot 0, diff-area used/allocated 0·limit 10,737,418,240 bytes, T: free 15,123,443,712 bytes를 확인했다. VSS 상한/볼륨을 변경하지 않으며 capture 직전의 현재 공간 guard를 유지한다. C: snapshot 6개는 보존한다.
- 새 source Plastic GUID는 `fc4fce46-cb20-4ad8-b474-cdb0000f9866`, source/home identity는 `22B349E4:0003000000009637` / `22B349E4:00030000000096A1`이다. 현재 후보 서비스 package `artifacts/plastic-reclamation/package-20261002-01`의 14개 파일·manifest SHA-256 `c4976b440884bc2feaae31883e693632f0c15fb20756da7f93d0304688cb6179`, grant `grants.json` SHA-256 `37c49c8ae3d9fce605f7793aa8f1f5527459b1446bdc417545bc0806f07400bb`, setup SHA-256 `b7b3fcc7946f1e57aa098fdcb483edcef0d3c06faa4dec4b31bf5218f35e9bc6`를 고정했다. 기존 grant 6개 + 새 등록 1개를 읽기 전용 `service-update-plan.json`에서 확인한 뒤 기존 제한 서비스를 Update한다. 보호 ledger·기존 등록·비소유 snapshot을 전후 확인한다. 일반 cm/CLI/Unity는 비승격이며 설치 helper만 UAC를 사용한다.
- A key `plastic-t-a-20261002`, B key `plastic-t-b-20261002`, source plastic/application unity/base snapshot/ntfs-snapshot/기본 16 GiB journal budget. 반환 경로의 새 작은 probe와 원본 새 marker만 편집한다.

## 종료 기준

1. 기존 등록/원본/C: 작업과 snapshot·서비스 grant/보호 ledger를 보존한다. 등록 GUID·source/home identity·seed·작업별 basis/digest/snapshot을 기록한다.
2. A/B 생성 초기 payload/readCache 0·격리·같은 요청·정확한 소유 Editor를 관측한다. 생성·Library·delta·독립 결과·파일 할당·메모리 비용과 전체 peak/VSS 귀속 unknown을 구분한다.
3. A 정상 stop/checkpoint/release, B의 동일 Editor/파일 쓰기 지속, A의 independent bytes/hash와 mount/provider/delta/snapshot/소유 자원 0을 확인한다.
4. dirty B는 정확한 intent/digest로 명시적 discard한다. B 결과를 보존한 것으로 표시하지 않고 마지막 소유 자원과 snapshot 회수 및 원본/peer/비소유 보호를 확인한다.
5. 실패/보류는 같은 id/home/intent에서 관측하고 보존한다. 새 key·비소유 snapshot 삭제·서비스/권한/볼륨 우회·강제 Editor 종료로 통과시키지 않는다. 다른 입력의 통과로 C: 보류를 해소했다고 주장하지 않는다.

원시 증거는 `artifacts/plastic-reclamation/`, 판정은 [Plastic 검증](../verification/plastic-compatibility.md#별도-t-입력의-회수-자격)에 별도 후보/입력으로 기록했다. 이 한 묶음은 A released·B discarded·소유 snapshot/자원 회수와 기존 C: 6개 snapshot/1,418개 보호 기록 보존을 확인해 **scoped-complete-with-limitations**로 마감했다. C: 보류 상태와 외부 CLI/MCP 전체 인계 미자격은 유지한다.
