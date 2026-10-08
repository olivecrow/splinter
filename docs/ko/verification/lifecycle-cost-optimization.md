# 종료와 준비 단계 비용 최적화

[English](../../verification/lifecycle-cost-optimization.md)

LC-01–03은 [US-01](unity-startup-remeasurement.md)의 남은 수명주기 비용을 다룬다. 소스 기준은 `76f8f7f`, 비교 대상은 `.local/sa8-runtime`에 고정한 일반 실행 파일이다. 작은 쓰기의 메타데이터·Library 초기화·seal/회수 비용을 대상으로 한다. 새 전체 Unity 시작·종료·checkpoint/release·Library 복원 시간은 **NotRun**이다. 과거 US 시간을 현재 후보의 결과로 읽지 않는다.

## 입력 근거

US의 정상 Editor 종료는 287.4339초였다. 소유 bridge는 각 종료 요청 뒤 약 1초 만에 `exiting`에 도달했다. 종료 전후 provider 표본에는 쓰기 10,311 / 10,306회, payload 1,172,606 / 1,165,926바이트, 논리 object-put 5,962,591,600 / 5,962,585,870바이트가 기록됐다. 쓰기 구간 합계는 275.7885 / 271.4713초이며 그 안에 durable flush 150.6798 / 147.4325초가 포함된다. A/B 구간은 겹치므로 더하지 않는다. 표본 구간은 종료 이후 몇 초까지 포함한다. 논리 put은 저장 계층 내부에서 옮긴 object를 제외하며 물리 디스크 I/O나 잔존 할당량이 아니다. 정확한 Unity 파일과 경로별 원인은 미확정이다.

기존 SJN2 구현은 앞부분 변경 뒤 live paging을 중단하고, 커지는 남은 extent/block 목록을 매 쓰기마다 직렬화했다. 이는 코드로 확인한 크기 증가 문제이며 관측된 put 크기 증가와 부합한다. 아래 한정 비교는 Unity를 실행하지 않고 저장 동작만 분리한다.

기존 최초 Library 준비는 capture·client 초기화·provider 허용에서 payload를 해시했다. A/B 생성도 각각 client와 provider에서 Library를 다시 해시했다. US Library 3,141,660,252바이트 기준 전체 입력 순회 7회다. client 순회 3회를 제거하면 해당 코드 경로의 논리 해시 입력 9,424,980,756바이트를 피한다. 이는 측정된 물리 I/O나 시간 절감이 아닌 계산값이다.

## 현재 경계

- **LC-01 — 작은 쓰기 메타데이터:** SJN3는 임의 덮어쓰기 뒤에도 같은 논리 페이지를 재사용하고 변경한 짧은 앞부분과 다음 불변 페이지를 32 extent 범위 안에서 합친다. SJP2/SJM2 metadata를 user payload 뒤에 두어 metadata 교체 시 앞의 private prefix를 제자리에서 유지한다. 남기는 prefix는 원래 header hash와 모든 완전 block을 검증하며 node/page checksum·정확한 inventory가 현재 소유권을 계속 증명한다. 전체 seal 내용·mutation별 durable flush 1회·동일 저널 복구를 유지한다. root/초기화에 NodeFormat=3이 필수이며 이전 NodeFormat=2는 이관 없이 보존·거부한다. page 참조 배열은 여전히 page 수에 비례한다. 현재 표현과 미지원 형식 경계는 [현재본 저널](../latest-only-journal.md)이 소유한다.
- **LC-02 — client 초기화:** 고정 manifest/read-proof/owner identity를 검증하고 reader lease/root namespace를 유지하며 저널의 root metadata만 초기화한다. 제한된 tree는 payload 읽기·목록·하위 조회를 거부한다. provider는 mount 게시 전에 모든 불변 파일을 열어 내용을 검증한다. client 초기화는 payload 합격이 아니다. 기존 초기화 intent와 before-effect/after-record 경계를 유지하며 불일치·손상 입력은 보존·거부한다.
- **LC-03 — seal/회수:** Windows seal과 회수 inventory 검증은 파일마다 모든 조상을 걷는 대신 연산 동안 고정한 object-directory 증명을 사용한다. 각 leaf·내용 checksum·현재 head·마지막 membership/pending 검사는 유지한다. 회수는 fence를 열기 전 저장 계획의 경로를 검증하고 모든 fence를 얻은 뒤 남은 내용을 검증하여 fence 전의 전체 해시 반복을 없앤다. delete sharing이 있으므로 기존 삭제 직전 해시는 유지한다. 일부 회수 뒤 `owner.lock`이 없으면 전체 경로 검사를 사용하며 owner를 재생성하지 않는다. 소유 디렉터리를 삭제하기 전에 모든 경로 pin을 닫는다.

서비스·UAC·Unity 패키지·전역 설정·외부 도구는 변경하지 않는다. 원본 Unity 프로젝트와 이전 evidence/result는 보존한다. 저널 payload cache·전체 파일 copy-up·응답 일괄 승인·mtime만의 내용 증명은 추가하지 않는다.

## 검증

[데이터셋](../../../reports/benchmarks/lifecycle-cost-optimization-20261008.json)에 소스·고정 실행 파일·원시 근거 hash, 제한과 아래 입력 범위 정정을 기록했다. 필수 `pwsh -NoProfile -File scripts/build.ps1`의 locked restore·Release 빌드와 **1,498개 성공 / 실패 0개 / 조건부 생략 20개**를 확인했다(합계 1,518개, 5분 20.306초). 빌드 경고·오류는 0개다. 관련 없는 미커밋 파일 27개의 hash와 clean인 옆 Unity 저장소는 그대로다.

임의·역순 쓰기와 재연결, metadata 때문에 앞의 private payload를 복사하지 않는 현재본, 독립 계산 seal, prefix 손상·부분/소실 block, 중단 경계 4개, 최종 회수 대상 변조, 이전 형식 거부, client root 전용 초기화와 provider 전체 내용 손상 거부, graft 허용 조건, 고정한 seal namespace, owner lock 유무별 부분 회수를 검증했다. 모든 쓰기의 개별 durable flush/commit을 유지한다. 앞선 집중 시험의 실패 5개는 시험 reader의 공유 모드 오류였으며 수정 후 113개를 통과했다. 이후 회수 대상 변조 시험을 추가하고 전체 시험을 통과했다. 실패 fixture/log도 보존한다.

## 한정 시간 비교

모든 probe는 새 일반 사용자 NTFS fixture와 각 실행 파일의 고유 형식을 사용한다. 준비와 내용·재연결 seal 검사는 타이머에서 제외한다. 작은 연산의 기준본 2회는 최초 전/후/후/전 탐색에서 재사용했고, 최종 후보 2회는 읽기 보완 뒤 수행했으므로 **새로운 균형 순서 측정은 아니다**. 최종 probe 중 빌드나 다른 벤치마크를 함께 실행하지 않았다. 작은 표본이며 일반적인 성능 연구가 아니다.

| 연산 | 이전, 초 | 이후, 초 | 관측 변화 |
| --- | ---: | ---: | ---: |
| Library client 초기화: 256파일 / 16MiB, 2회 평균 | 0.205016 | 0.072486 | 64.64% 단축 |
| 이후 provider의 전체 payload 허용 검증, 2회 평균 | 0.138438 | 0.148922 | 7.57% 증가 |
| 정지된 seal: 256파일 / 1MiB, 2회 평균 | 0.213477 | 0.119339 | 44.10% 단축 |
| 같은 정지 fixture의 소유 회수, 2회 평균 | 0.628538 | 0.328238 | 47.78% 단축 |
| 범위가 증가하는 64B 쓰기 2,048회, 한 쌍 | 17.210355 | 14.125682 | 17.92% 단축 |
| 완전히 private인 파일의 64B 덮어쓰기 2,048회, 2회 평균 | 13.608212 | 13.708671 | 0.74% 증가 |
| 완전히 private인 파일의 64B 덮어쓰기 8,192회, 한 쌍 | 53.800992 | 55.661433 | 3.46% 증가 |
| 대조군 이어 쓰기 2,048회, 2회 평균 | 11.293879 | 11.398550 | 0.93% 증가 |

provider의 전체 내용 검사는 필수로 유지하며 의도한 코드 감소가 없다. 관측된 10.48ms 증가도 표에 남겼다. client 초기화에는 provider 실행·mount·workspace 등록·Editor 준비가 포함되지 않는다. seal/회수도 해당 연산만의 값이며 전체 checkpoint/release 명령 시간이 아니다. 이어 쓰기 표본 구간은 겹친다. 이 표본은 이어 쓰기와 완전 private 덮어쓰기의 시간 개선을 입증하지 않는다.

### 범위 증가 입력 정정

처음 probe는 128개의 연속 private chunk로 파일 전체를 채웠다. 보존한 stopped journal을 검사하니 양쪽 실행 파일 모두 **2,048회와 8,192회 쓰기 후 extent 128개**였다. 부분 쓰기가 연속 private 범위를 다시 묶고 합쳤기 때문이다. 따라서 호출 횟수 증가가 extent metadata 증가를 재현하지 못했다. 이 결과와 회귀는 덮어쓰기 비용 측정으로 그대로 유지하며 원시 `scaling8192` key도 과거 이름을 보존한다.

추가 한정 비교는 hole로 초기화한 논리 256KiB 파일에 간격을 둔 64B seed 128개를 쓰고, 128B 간격의 위치에 섞인 순서로 64B 쓰기 2,048회를 수행한다. **초기 128 / 최종 2,048 / 재연결 2,048 extent**, 정확한 최종 내용, 개별 flush/commit 2,048회, 같은 재연결 seal을 확인했다. 논리 put은 **490,134,535 → 32,151,320바이트(93.44% 감소)**, 시간은 17.92% 감소했다. extent가 늘지 않는 덮어쓰기 probe는 별도로 2,048회에서 **64,419,073 → 21,065,407바이트(67.30%)**, 8,192회에서 **309,962,047 → 135,101,755바이트(56.41%)** 감소했지만 시간은 개선되지 않았다.

논리 put은 저장 계층 내부에서 옮긴 object를 제외하며 물리 쓰기나 잔존 디스크 할당량이 아니다. 첫 고정 후보의 덮어쓰기 지연을 보고 옮길 object의 중복 읽기를 제거하고 전체/prefix 회수 hash를 같은 스트림에서 계산했다. 삭제 직전 hash 검사는 유지한다. 이전 형식 probe, sandbox 게시 실패, probe의 삭제된 helper API 참조 실패는 ignored `artifacts/lifecycle-cost-followup-20261008/`에 보존한다. probe에서만 해당 helper를 같은 lease/tree 구성으로 대체했다.

원본 Unity Library/프로젝트·설치된 서비스·전역/외부 도구 설정은 그대로다. 종료 때 쓰기 패턴을 만든 실제 Unity 파일은 아직 미확정이다. 전체 Unity 종료/시작·checkpoint/release·복원·전체 RAM/물리 저장·OS/전원 장애 자격은 **NotRun**이다. 이 한정 절감률을 US의 287초 종료나 사용자가 제시한 과거 전체 단계 비중에 직접 적용할 수 없다.
