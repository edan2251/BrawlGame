# CP1. 결정론 시뮬레이션 코어

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | |
| 상태 | 진행 중 |
| 관련 명세 | CHECKPOINTS.md CP1, spec/01-architecture.md 3~8절, spec/06-match-and-tools.md 1·5절 |
| 태그 | cp-1 |

## 1. 계획

1. Simulation 기본형: `Vec2`, `SimMath`(수학 관문), `Hasher`(FNV-1a), `SimRng`(xorshift32), `InputFrame`과 눌림/뗌 계산.
2. 상태: `FighterState`, `MatchState`, `SimState`(값 타입). 체크섬·상태 비교·(나중의) 직렬화가 같은 필드 목록을 쓰도록 **상태 방문자(`IStateVisitor`) 하나**로 필드를 나열한다. 반사(reflection)로 "모든 필드가 체크섬에 들어가는지" 검사하는 테스트를 둔다.
3. `SimWorld.Step`: Countdown → Fighting 뼈대와 CP1 임시 이동(중력, 좌우 가감속, 단순 점프, 임시 바닥·벽). CP2에서 지형과 이동 명세로 교체.
4. `SimEventBuffer`(고정 용량), `SimConfig`(+ `MovementParams`), `MovementTuning` ScriptableObject 변환.
5. 도구: SyncTest(되감기 N틱, 필드 단위 불일치 보고), 입력 기록·재생(바이너리), 시나리오 JSON → `ScenarioDef` → 입력 프레임, 공용 `expect` 러너, 시뮬레이션 규칙 점검(EditMode 테스트 + 메뉴).
6. Game: `MatchRunner`(고정 60틱, 프레임당 최대 4틱, 보간 비율, 입력 지연), `IInputSource` 구현들, 시나리오 재생 컴포넌트.
7. 검증: CP1-1~9. CP1-9와 PlayMode 테스트는 D-015에 따라 Unity를 앞으로 가져와 실행.

## 2. 작업 기록

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 콘솔 확인 | | |
| ALL-2 | 전체 테스트 | | |
| ALL-3 | 규칙 점검 | | |
| ALL-4 | showcase 실행 중 콘솔 | | |
| ALL-5 | 커밋·태그·푸시 | | |
| ALL-6 | 기록 갱신 | | |
| CP1-1 | | | |
| CP1-2 | | | |
| CP1-3 | | | |
| CP1-4 | | | |
| CP1-5 | | | |
| CP1-6 | | | |
| CP1-7 | | | |
| CP1-8 | | | |
| CP1-9 | | | |

## 4. 캡처 확인

CP1에는 [S] 기준이 없다.

## 5. 자체 결정

## 6. 알려진 문제와 미룬 것

## 7. 인계 사항
