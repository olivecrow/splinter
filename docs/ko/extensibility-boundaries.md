# OS와 앱의 확장 경계

[English](../extensibility-boundaries.md)

현재 실행 어댑터와 CLI의 지원 선택은 Windows/NTFS, `unity|none`이다. macOS/Linux의 mount·프로세스·게시 어댑터와 Blender/Unreal/Godot 어댑터는 구현하지 않았다. 공통 계약의 합성 시험은 이 환경의 실제 지원을 의미하지 않는다. 검증 범위는 [확장 계약 시험](verification/extensibility-boundaries.md)을 따른다.

## 경로와 파일 metadata

`SnapshotReference.PathPolicy`는 불변 snapshot identity의 일부다. `WorkspacePathPolicy`가 경로 정규화·비교·부모/자식 판정을 제공하며, 저널의 색인·이동·숨김·재생·정리·후보와 graft는 같은 정책을 사용한다. 생략된 값은 현재 Windows 계약의 의미를 유지한다. 대소문자를 구분하는 정책은 별도 identity로 고정하며, 같은 delta를 다른 정책으로 재연결하거나 서로 다른 정책의 tree를 graft하지 않는다. 알 수 없는 정책은 거부한다.

`SnapshotEntry`와 `DeltaEntry`의 선택적 `UnixMode`는 Unix permission 비트를 전달한다. 알 수 없는 권한은 null이며 Windows attributes로 추측하지 않는다. Git의 일반 파일과 실행 파일 모드도 구분한다. 저널 metadata 갱신과 후보 저장은 이 정보를 보존한다. Windows attributes/timestamps, Unix 권한과 실제 OS의 ACL·소유자·특수 파일 지원은 별도이며, 정보 전달을 실제 Unix 파일시스템 구현으로 읽지 않는다.

## 앱별 프로젝트 정책

Core의 `IApplicationProjectPolicy`는 앱 identity, 호환 키, 생성 디렉터리 descriptor, 원본 접근 reservation, 선택한 소스의 동반 파일 범위를 제공한다. `ApplicationProjectPolicies`는 조합 계층에서 정확한 정책을 선택한다. 미등록 앱을 Unity 또는 none으로 대체하지 않는다.

`UnityProjectPolicy`가 `Temp/UnityLockfile`, `Assets`의 부모와 `.meta` 쌍, Unity/bridge 호환 키와 `Library` descriptor를 소유한다. Files 공급자는 공통 부모 처리·3자 비교·고정 intent·조건부 게시를 맡고 앱 정책이 반환한 동반 파일을 변경되지 않은 guard로도 보호한다. Plastic 등록/selector/서버 잠금 검사는 기존 VCS 경계에 남는다. 원본 reservation은 scope가 끝나거나 검사가 실패하면 해제하며, 다른 프로세스의 잠금이나 Temp 내용을 삭제하지 않는다.

관리형 원본은 Windows의 정확한 mount identity lease를 먼저 획득한 뒤 그 root만 앱 정책에 전달한다. 이 허용은 다른 reparse 경로나 임의 mount에 적용하지 않는다. 앱 정책은 정상 종료를 확인/예약할 뿐 컴파일·Play/Test·빌드의 성공을 판정하지 않는다.

## 생성 디렉터리

`ManagedDirectoryBasis`는 주입한 정책의 `GeneratedDirectoryDescriptor(Kind, RelativePath)`와 호환 키를 사용한다. 현재 공통 graft는 단일 최상위 하위 디렉터리만 제공하며 중첩·복수 생성 디렉터리는 미지원이다. Unity의 공개 명령은 기존 `library prepare/status/restore`와 `--library-basis`다. 다른 앱을 위한 공개 선택지나 명령을 추가하지 않았다.

whole-project 기준은 별도 `Project` descriptor를 사용한다. 관리형 basis 상태와 준비 intent의 현재 버전은 **v2**이며 앱·디렉터리 종류·경로·호환 키를 고정한다. v1 기록은 보존하고 명시적으로 거부하며 자동 변환하지 않는다. workspace v2, overlay v2/SJR2, candidate v3와 복원 계획 v2는 유지한다. 같은 볼륨 이동·원본/peer 보호·reader 의존성·고정 복원 계획·명시적 최종 복원은 기존 [Library](git-library-reference.md)와 [전체 프로젝트](plain-project-basis.md) 계약을 따른다.
