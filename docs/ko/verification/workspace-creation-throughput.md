# 작업공간 생성 처리량 검증

[English](../../verification/workspace-creation-throughput.md)

[고정 범위](../plans/workspace-creation-throughput.md)의 WC-01–05 기록이다. 기준 버전은 Library 병렬화·bounded 폐기 수정을 포함한 LU-01 후보이며 기존 작업 트리 변경은 양쪽에 유지한다. 새 제품 변경은 Git blob 최초 전체 증명의 한정 선행 보관과 초기 Git index refresh 중 큰 파일을 선행 읽는 작업 하나의 결합이다. 원시 근거는 `artifacts/workspace-creation-20261024/`에 있다.

## Reader 근거

첫 구성 요소 비교는 실제 Unity 소스 커밋 `fd611140aa0c22d66782c19b007f1a68c00af2a6`의 1,602개 파일 / 2,804,769,932바이트를 모두 읽고 각 반환 파일의 SHA-256을 독립 manifest와 비교했다. 이 측정기는 입력 저장소를 읽기만 하며 refs·설정·파일을 바꾸지 않는다. 변경 전 **27.844초**, **5,570,158,047바이트 디코딩**에서 후보 **19.659초**, **2,792,956,972바이트 디코딩**으로 줄었다. 내용이 같은 blob은 proof를 공유하므로 고유 디코딩량이 파일 크기 합계보다 작을 수 있다. 양쪽 모두 전체 blob proof 1,563개·reader 프로세스 2개다.

보관한 chunk payload peak는 **16,777,216 → 267,815,217바이트**로 각각 16MiB·256MiB 한도 안이다. 전체 순회 중 관리 할당은 약 2.913 → 2.912GB이며 보유 메모리 측정이 아니다. 후보의 각 전체 proof는 앞으로의 최대 128MiB를 보관하고 미검증·요청·선행 chunk 모두 256MiB/4,096-entry LRU를 공유한다. 전체 object hash/framing 이후에만 게시하며 cache hit도 현재 보관 bytes를 chunk proof와 대조한다. decoder·proof 한도·30초 무진행 timeout·디스크 hydration 금지·개별 journal 내구성은 그대로다.

이 구성 요소 수치는 고정 health 관측기를 사용하며 WinFsp·Git index refresh·Library·Editor 비용을 제외한다. 이 한 쌍을 전체 개선으로 확대하지 않고 아래 실제 생성 근거로 채택을 판단한다.

## 보호 검증

최종 reader 집중 시험은 **47 통과 / 실패 0 / opt-in 건너뜀 1**이다. loose/packed 순차·alias 읽기, 제한된 퇴출·선행 범위 밖 읽기, 전체 proof 실패, 선행 RAM 손상, proof 퇴출, 선행 보관 상태의 실패 peer 게시 차단·close/drain을 확인했다. 이전 요청 범위 cache·session 시험은 선행 보관을 명시적으로 끄고 캐시에 없는 decoder 경로를 계속 검사한다.

앞선 넓은 Git 검사에서는 fixture 전제 불일치 1건이 있었다. 이전에는 캐시에 없던 작은 손상 대상 suffix가 확대된 불변 cache에 들어가 원래 검증 bytes를 반환했다. 해당 저장소 손상 경로는 선행 보관을 명시적으로 끄도록 수정했으며 cache bytes 손상과 object store 변경 뒤 고정 cache 읽기는 별도 시험으로 확인한다. 시험 통과를 위해 제품 guard를 완화하지 않았다. 최초 CLI 필터 오류는 시험을 실행하지 않았으며 통과 근거에 포함하지 않는다.

## 후보 선정

Cache 단독 실제 ABBA는 **74.080 / 65.376 / 65.305 / 73.863초**다. 평균 **73.972 → 65.340초**, **8.631초(11.7%)** 절감이다. mount 연결은 17.8–17.9초로 유지됐고 attach/검사는 48.1–48.4초에서 39.5–39.6초로 줄었다. 이것만으로는 요청한 우선순위에 부족했다. 네 task 모두 내용/mtime·독립 초기 source fingerprint·수정 감지/원본 격리·명시적 폐기·provider/delta/등록/ref 회수 검사를 통과했다. 공통 Library 준비/복원은 43.833/21.134초로 생성 시간 밖이다. 최종 Library bytes/시각은 같고 소유 provider는 모두 종료했다.

별도 큰 pipe 시제품은 같은 고유 blob 1,563개 / 2,787,030,306바이트를 검증하며 15.381 → 11.409초를 기록했다. 구성 요소 약 4초 절감으로 Windows 프로세스 전송 경로를 바꿀 이유는 부족했다. 제품 코드에서 제외했으며 전체 시간 결과가 아니다.

WC-05는 1MiB 이상 regular file마다 첫 바이트를 읽는 작업 하나를 필수 초기 index refresh와 겹친다. 기존 불변 reader가 전체 blob proof를 수행하고 제한 window를 보관하면 뒤따르는 Git 내용 읽기가 재사용한다. `Task.WhenAll`은 성공·실패·취소에서 양쪽 종료를 기다린다. 최초 index이며 소스 변경을 허용하지 않는 attach만 사용하고 기존 index 복구·변경한 candidate는 선행 읽기 없이 필수 refresh를 유지한다. 미완료 index lock도 보존한다. 같은 입력 pilot은 **51.778초**, 그중 attach/검사 **25.995초**, mount 연결 **18.014초**다. 수정 격리·폐기·Library 복원·최종 입력 일치/provider 종료를 확인했다. 이 pilot 한 번은 최종 대조 측정의 근거이며 최종 평균에 섞지 않는다.

## 실제 생성과 최종 gate

WC-03은 공통 고정 Library 기준본(42,038개 파일 / 3,105,800,655바이트)에 작업공간 하나씩을 전→후→후→전 순서로 생성한다. 전체 소스/Library 내용·수정 시각 검사는 `workspace ensure` 시간 밖이다. 초기 source fingerprint를 독립 대조하고, 측정 뒤 코드 변경이 Git에 보이면서 원본은 유지되는지 검사한 다음 정확한 task를 폐기한다. 공통 Library 준비·복원은 별도 기록한다. Editor는 시작하지 않고 과거 LU/SD/BS 흐름을 재개하지 않는다.

필수 `pwsh -NoProfile -File scripts/build.ps1`은 locked restore·빌드(경고/오류 0)와 **1,580 통과 / 실패 0 / opt-in 건너뜀 26 / 전체 1,606**을 완료했다. 최초 선행 읽기 신규 5개 사례는 실제 Git refresh 성공/실패·이미 시작한 읽기의 종료 대기·읽기 실패 주입과 기존 index 재시도·변경 candidate·취소를 다룬다. 첫 gate는 시험의 취소 token analyzer 오류 7개에서 멈췄고 수정 후 위 gate를 통과했다. 원시 로그/TRX를 보존한다.

WC-03-final은 최종 검증한 소스·실행 파일로 완료했다. 공통 Library 기준본을 이미 준비한 상태의 결과다.

| 순서 | 버전 | 생성 (`workspace ensure`) | Mount 연결 | Git attach + 최초 검사 |
| --- | --- | ---: | ---: | ---: |
| 1 | 변경 전 | 74.162초 | 17.868초 | 48.374초 |
| 2 | 변경 후 | 51.893초 | 18.000초 | 25.919초 |
| 3 | 변경 후 | 51.937초 | 18.052초 | 25.942초 |
| 4 | 변경 전 | 73.745초 | 17.847초 | 48.066초 |
| 평균 | 전 → 후 | **73.953 → 51.915초** | 17.857 → 18.026초 | **48.220 → 25.931초** |

같은 입력·버전별 두 번의 이 묶음에서 생성은 **22.038초(29.8%)** 줄었다. 절감은 Git attach/검사에 집중된다. Library admission을 포함한 mount 연결은 그대로이며 남은 Git 검사와 함께 가장 큰 생성 비용이다. 결합 후보를 채택한다. 앞선 cache 단독·pilot 수치는 이 평균에 섞지 않았다.

공통 Library 준비/복원은 **43.672 / 21.157초**로 생성 시간 밖이다. 수정·폐기도 별도이며 폐기 평균은 **7.788 → 7.560초**지만 회수 속도 개선을 주장하지 않는다. 네 task 모두 독립 초기 source fingerprint·수정 감지/원본 격리·명시적 폐기·mount/delta 부재·정확한 소유 provider 종료·Git 등록/ref/설정 복원을 확인했다. 각 실행 전후와 마지막 Library 복원 뒤 전체 소스/Library bytes와 파일 수정 시각이 같았다. 측정한 task와 Library provider는 모두 종료했다. 이후 임시 실험 checkout을 제거했으며 receipts·원시 근거·clone의 hard-link Git 메타데이터는 공유 object 속성을 바꾸지 않고 보존한다.

[기계 판독 보고서](../../../reports/benchmarks/workspace-creation-throughput-20261024.json)에 정확한 시간·입력 identity·실행 파일 hash·소스 hash·검증·회수를 기록했다. 제품 소스 변경은 `GitObjectSnapshotTree.cs`와 `GitOverlayRegistration.cs`이며 의존 실행 파일도 기록대로 다시 빌드했다. WC-01–05는 이 범위에서 완료했다. 새 명령·schema·저장 형식·별도 서비스는 없다.

한계는 Windows 한 대·논리 processor 12개·버전별 두 번·OS cache 미초기화·고정한 실제 Unity 소스/Library 한 묶음이다. 측정 중 빌드/시험을 함께 실행하지 않았다. **Library 준비 후 Git 기반 작업공간을 Editor 없이 생성한 결과**이며 새 Unity 전체 수명주기나 모든 프로젝트/backend의 배수가 아니다. 원본 반영·Editor 시작/컴파일/Play/Test/build·변경된 원본 Library 복원·다른 VCS/backend 성능·전체 프로세스 peak 메모리·OS crash 자격은 이번 변경에서 **NotRun**이다.
