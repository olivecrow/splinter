# Git 수집 최적화

[English](../../plans/git-collection-optimization.md)

2026-10-30 사용자 승인으로 [GCD-01](../verification/git-result-collection-diagnosis.md)의 후속을 진행한다. 기준은 `f91e470`이며 기존 진단 문서·근거를 보존한다. MX·GR·실제 Unity 자격을 재개하는 대신 새로 한정한 최적화 단위다.

- **GCO-01:** 정확한 기준본 reader lease 안에서 수집 범위의 Git reader를 추가한다. 소유 loose basis ref와 receipt를 fence하고 읽는 동안 현재 identity/내용을 로컬 검사하며 독립 결과 게시 전 Git을 전체 재검증한다. packed-only basis ref는 기존 전체 observation 경로를 유지한다. raw blob/tree proof와 ref 유실·변경, receipt 손상, lease 유실, 게시 시 객체 유실의 거부를 유지한다.
- **GCO-02:** `checkpoint --release`에서 같은 호출이 전체 seal한 generation을 재사용한다. 정상 종료·infrastructure 제거·전체 전후 seal·unmount 뒤 seal·과거 결과 검사·같은 key 복구를 유지한다. 별도 release는 현재 소스를 다시 수집하며 상태 버전·CLI flag를 추가하지 않는다.
- **GCO-03:** 계약/fake/Git·중단/소유 보호 집중 시험 뒤 고정 입력의 정상 CLI 수집·최종화 전후 비용을 잰다. 보존 RI의 작은 Git object 입력을 쓰며 Library·Unity 프로세스 없이 제한된 Editor 없는 A/B 흐름을 검증한다. HEAD·index·다른 ref·peer 편집·독립 결과를 보존하고 소유 task·진단 lease만 회수한다. 입력·결과 일치 검사와 전후 최소 1회씩을 요구한다. 측정한 수집/최종화에서 10초 이상 또는 30% 이상 절감하고 보호 시험이 유지되어야 채택한다. 구성 요소 수치를 Unity 절감으로 환산하지 않는다.
- **GCO-04:** `pwsh ./scripts/build.ps1`과 양언어 문서/source 탐색 검사를 수행하고 정확한 한계로 검증 기록을 마감한다. read-boundary API 변경 시 계약/fake를 갱신한다. 새 의존성·외부 서비스/도구·실제 Unity 실행·Plastic/None native 확대·대형 입력·OS 장애 행렬·자동 commit/check-in·게시를 추가하지 않는다.

완료에는 구현된 범위의 보호/복구 시험, 동일 입력 비용 근거, 소유 회수, 최종 전체 gate와 영문/한국어 소유·탐색 문서 동기화가 필요하다. 후보 폐기 시 원시 근거를 남기고 구현을 제거한다. **마감:** [고정 입력 근거·최종 gate](../verification/git-collection-optimization.md)로 GCO-01–04를 한정 완료하고 채택했다. 마감한 Unity/MX/GR 프로그램은 재개하지 않았다.
