# Plastic 결과를 원본에 반영하기

[대규모 저장 후보](scalable-storage.md)는 task v4·관리형 basis v3를 사용한다. [반복 원본 반영](repeated-original-apply.md)이 실행 중 불변 수집·명시적 결과 선택·경로별 반영 이력·외부 도구의 실행 중 Editor 협력을 소유한다. 과거 [RI 자격](verification/repeated-original-apply.md)은 task v3·basis v2를 사용했으며 후보는 별도 [SCAL 검증](verification/scalable-storage.md)을 따른다. 이전 상태는 이관 없이 보존·거부한다.

[English](../plastic-original-apply.md)

`plastic-snapshot`의 현재 독립 결과를 정확한 regular/static 원본에 조건부로 게시한다. `ntfs-snapshot` Plastic, partial/dynamic 및 released 결과 재수입은 지원하지 않는다. 서버 add/checkout/update/check-in/잠금 변경은 AI와 Plastic 도구의 책임이다. 중간 수집은 task·원본 Editor를 유지하며 원본에는 명시적 외부 협력 영수증을 사용한다. 최종 처리는 해당 task만 정상 종료하고 같은 수집 엔진을 사용한다.

```powershell
pwsh scripts/splinter.ps1 schema workspace.integrate
pwsh scripts/splinter.ps1 workspace integrate --id <id> --target original --result <result-id> --plan --path Assets/Selected.txt --home <home>
pwsh scripts/splinter.ps1 workspace integrate --id <id> --target original --result <result-id> --intent-key <key> --expected-plan-digest <digest> --path Assets/Selected.txt --home <home>
```

`--path`는 반복할 수 있는 정확한 상대 파일/디렉터리 경로다. 생략하면 전체 변경이다. 필요한 새 부모와 Unity Assets의 파일·폴더 `.meta`를 함께 계획하며 변하지 않은 companion도 원본 GUID 보호를 위해 확인한다. managed 경로·VCS 관리 정보·패턴·중복·누락 선택은 거부한다. `sourceIdentity`·`changeCount`·`conflictCount`·`digest`를 검토하고 `workspace read --id <id> --plan-id <digest> --collection <collection> --page-size 128 --home <home>`으로 `plan`·`selection`·`conflicts`를 페이지 조회한다. 반환한 root 결합 cursor로 이어 읽는다. 현재 페이지 계획의 인라인 목록은 비어 있으므로 이 빈 목록을 변경이나 충돌이 없다는 근거로 쓰지 않는다. 경로 순서는 정규화한다.

계획의 경로/삭제 guard 검색은 [공통 원본 반영](plain-folder-original-apply.md)의 조상 집합을 사용한다. CLI `nextActions`에도 `target=original`·선택 `paths`를 유지하고 미완료 반영의 재개에는 정확한 intent/digest를 포함한다. 선택 계획에서 재개 인자를 생략해 전체 범위로 바꾸지 않는다.

경로별 반영 상태/결과/현재 원본의 내용과 관련 속성을 비교하며 최초 반영 상태는 고정 기준본이다. 그 반영 상태 또는 이미 같은 결과만 허용하며 제3의 변경·경로 대소문자 변경·특수 메타데이터·외부 자식이 남는 삭제는 거부한다. 선택하지 않은 변경은 독립 결과에 남고 원본에 게시하지 않는다. 같은 task에서 추가 편집하고 나중 반영에는 새 불변 결과 버전을 수집한다.

Plastic revision은 NTFS indexing 속성을 기록하지 않는다. 기존 원본의 `NotContentIndexed` 비트만 게시 버전에 보존하며 파일 내용·다른 속성·identity와 게시 직전 조건을 계속 검증한다. 일반 폴더 기준의 속성 비교는 변경하지 않는다.

반영 때 원본 등록 GUID·클라이언트 버전·관리 파일 hash·repository/loaded changeset/selector를 확인한다. `.plastic`의 workspace/selector/wktree 읽기 핸들을 게시 동안 유지하며 원본 정지 reservation 또는 정확한 실행 중 원본 외부 협력 허가를 적용한다. 잠금/retained 관측이 선택 경로나 조상/자손과 겹치거나 관측이 불명확하면 거부한다. 자기 소유 서버 잠금도 별도 처리 없이 무시하지 않는다. 서버 잠금 조회는 원격 예약이 아니며 조회 후 새 원격 잠금과 로컬 게시 사이의 분산 원자성을 제공하지 않는다.

현재 경로는 원본의 loaded changeset이 요청한 recorded changeset과 같아야 한다. 원본의 로컬 편집과 비선택 dirty 내용은 허용하지만 check-in/update로 loaded changeset을 바꾼 원본에는 이 결과를 자동 반영하지 않는다. 바뀐 기준과 실제 필요에 맞는 다음 작업으로 명시적으로 처리한다.

게시의 내용·NTFS identity·부모 핸들·stage/retired 소유 검증과 개별 durable 진행 영수증은 [공통 원본 게시](plain-folder-original-apply.md)를 재사용한다. 같은 intent/digest/선택만 중단 후 재개하며 미완료 intent는 release/discard/새 수집을 차단한다. 완료 중복 요청은 다시 게시하지 않는다. 원본 반영은 discard로 되돌리지 않는다. 작업 결과와 회수는 별도 결정이다.

Library와 관리 데이터는 반영 대상이 아니다. `--library-basis`의 마지막 dependent 회수/restore는 [공통 Library 수명주기](git-library-reference.md)로 따로 처리한다. 실제 자격은 [검증](verification/plastic-original-apply.md), 범위와 종료 기준은 [계획](plans/plastic-original-apply.md)을 따른다.
