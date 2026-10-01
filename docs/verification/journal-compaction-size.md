# 저널 정리의 크기 증가 회귀

실행일: 2026-10-01, Windows x64/C: NTFS. 범위는 `JournalDeltaStore`의 오프라인 정리에서 메타데이터 증가가 제거한 payload보다 커지는 조건이다. 기존 `workspace compact`·Library `compact-cache`가 사용하는 frame 선택만 변경한다. 입력·응답·저널 형식·내구성·소유권·중단 복구 계약은 유지한다. SF-02 전체는 Partial이며 대형 Unity 공간 목표의 완료로 확대하지 않는다.

## 재현과 선택 규칙

- 8KiB 쓰기에 1바이트 덮어쓰기가 흩어지면 살아 있는 작은 범위의 목록이 payload 절감보다 커졌다. 이미 `write-compacted`인 frame에 같은 편집을 더한 경우도 재현했다. 기존 코드는 후보 길이가 커져 직전 길이의 두 배로 정한 중첩 예산을 거부했다.
- 두 파일을 번갈아 1바이트씩 덮어쓰면 무효 쓰기를 묶을 수 없고, 개별 `skip-write`의 메타데이터 증가가 제거한 1바이트보다 컸다.
- 범위로 나눈 frame이 더 크거나 같으면 기존 frame을 유지한다. 개별 skip이 더 크면 기존 frame을 보관하되 같은 경로의 인접 skip과 묶을 기회는 유지한다. 묶음은 두 frame의 더 작은 표현 합계를 넘지 않을 때 채택한다. 사전 scan과 실제 후보 쓰기가 같은 규칙을 사용한다.

기존 frame의 payload는 이미 소유한 저널에서 읽는다. 기준 파일을 읽어 채우거나 전체 파일을 copy-up하지 않는다. 일부 obsolete payload가 남아도 전체 저널이 커지지 않는 쪽을 선택하며 obsolete=0을 보장하지 않는다. 후보 checksum·전체 재생 상태 hash·sequence·전후 seal 확인과 이전 세대 보존은 계속 적용한다.

## 같은 입력의 실제 파일 할당

기준 커밋 `b526646`의 Runtime 바이너리와 수정한 바이너리를 고정했다. 합성 불변 트리에서 파일 4개에 각각 8KiB를 쓰고 두 구간에 1바이트씩 128회 덮어쓴 저널을 한 번 작성했다. **바이트가 같은 저널**을 새 소유 디렉터리에 각각 복사해 정리했다. [기존 코드 응답](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/before-qualified.json)과 [수정 코드 응답](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/after-qualified.json)을 비교한다.

| 관측 B | 정리 직전 | 기존 코드 정리 후 | 수정 코드 정리 후 |
| --- | ---: | ---: | ---: |
| 저널 논리 길이 | 238,404 | 252,296 | 238,404 |
| NTFS 저널 파일 할당 | 241,664 | 253,952 | 241,664 |
| 게시 뒤 두 저널 파일 할당 합계 | 해당 없음 | 495,616 | 483,328 |

수정은 이 입력에서 기존 정리가 일으킨 **12,288B의 파일 할당 증가를 방지**했다. 정리 직전보다 저장량을 줄인 결과는 아니다. 입력 journal SHA-256은 두 실행 모두 `fd6a49b371015dc89c0cc18dbfdabee89a79971cfeac7febe7fa4dbd4d3b4a7f`, durable sequence는 520이다. 네 파일의 현재 내용 hash·변경 경로·sequence가 같고 기준 payload 읽기는 0이었다. 사전 예상 겹침량은 실제 두 저널의 논리 길이 합계와 같았다. 검증 후 이전 journal과 intent를 회수했다.

할당은 `FILE_STANDARD_INFO.AllocationSize`다. 디렉터리/MFT·VSS·Git·Unity·OS 캐시·전체 작업공간 순간 peak를 포함하지 않는다. 첫 샌드박스 실행의 `File.Replace` 접근 거부 자료는 `before-journal`에 보존했다. 비교 표는 새 소유 `before-qualified-journal`/`after-qualified-journal`에서 일반 사용자 권한으로 완료한 실행만 사용한다. 관리자 설치·grant·VSS·WinFsp mount·Unity·VIVOBOOK은 실행하지 않았다.

## 자동 회귀와 완료 경계

`JournalCompactionSizeTests`의 세 사례는 수정 전 모두 실패했고 수정 후 모두 통과했다. 기존/이미 정리한 frame의 재정리·중첩 예산·사전 길이/실제 길이 일치·파일 내용과 메타데이터·변경 경로/sequence·기준 읽기 0을 대조한다. [수정 전 TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/before-tests.trx)와 [수정 후 TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/after-tests.trx)를 보존한다.

필수 `pwsh ./scripts/build.ps1`은 locked restore·Release 빌드 경고/오류 0, 자동 시험 **757건 중 756 통과·0 실패·선택적 ReFS 1 skip**이다. [build 로그](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/build.log), [같은 실행의 TRX](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/automated.trx), [판정과 현재 소스 hash](../../artifacts/storage-foundation/compaction-size-20261001-232212-446/result.json)를 보존했다. 기존 큰 payload 제거·동일 경로 skip 묶음·네 게시 경계의 재개·손상 후보 보존·workspace/cache 적합성 시험도 통과했다. 문서 63개는 UTF-8 검사를 통과하고 변경 문서의 로컬 링크 222개는 모두 존재한다.

이 단위는 **scoped-complete-with-limitations**다. 완료는 합성 불변 기준·현재 자동 회귀·실제 로컬 저널 파일 할당 비교에 한정한다. 실제 Unity/Library 프로젝트 전체의 절감률이나 VSS/WinFsp·전원/재부팅 자격을 새 후보의 통과로 승계하지 않는다.
