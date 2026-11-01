# CP1. 결정론 시뮬레이션 코어

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
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

- 만든 것(Simulation): `Core/`(Vec2, SimMath, Hasher, SimRng, IStateVisitor), `Data/`(InputFrame, SimConfig·MovementParams·MatchParams, SimEvent·SimEventBuffer), `State/`(SimState, FighterState, MatchState), `Systems/`(SimWorld, TempArena, TempMovement), `Replay/`(SyncTest, StateDiff, InputRecording, RandomInputGenerator), `Scenario/`(ScenarioDef, ScenarioInputs, ExpectEvaluator, ScenarioRun).
- 만든 것(Game): `Bootstrap/`(MatchRunner, FixedTickClock, InputDelayQueue), `Input/`(IInputSource, Idle·Random·FrameArray 입력원), `Tuning/`(MovementTuning, SimConfigFactory), `Debug/`(MiniJson, ScenarioJson, ScenarioPlayer, ReplayFiles).
- 만든 것(Editor): `SimRulesChecker`(메뉴 `Knockbox/Verify/Check Simulation Rules`), `ProjectSetup`에 튜닝 에셋 생성과 씬 오브젝트 배치 추가(Main: `Match`/MatchRunner 자동 시작, Sandbox: `Match`/MatchRunner 수동 시작 + `Scenario`/ScenarioPlayer). `Run All`로 재현.
- 데이터: `Data/Tuning/MovementTuning.asset`(명세 초기값), `Data/Scenarios/sample_walk_jump.json`, `showcase_cp1_mirror_run_jump.json`.
- 컴파일 오류 1회: `Hasher.Value` 속성이 방문자 메서드 `Value(...)`와 이름이 겹침 → 속성 이름을 `Hash`로.
- 컴파일 오류 1회: Unity 할당 제약 `Is.Not.AllocatingGCMemory()`는 확장 메서드라 `using UnityEngine.TestTools.Constraints;`가 별칭과 함께 필요.
- 규칙 점검기 보완: 처음 판정식은 `(`가 있으면 무조건 메서드로 봐서 `static readonly List<int> X = new List<int>();`를 놓쳤다. 대입 `=`와 `(`·`=>`의 위치를 비교해 메서드와 필드 초기화를 구분하도록 고치고 테스트 케이스 2개 추가.
- 시나리오 기대값 계산 실수: showcase의 tick 41 `pos.x` 범위를 처음에 [-3, -1.5]로 적었으나 계산하면 P1은 41틱 동안 7.06 유닛을 가서 x ≈ 1.06 → [0.9, 1.2]로 고침(실행 전 수정).
- 이 단계의 두 파이터는 서로 통과한다(몸 충돌은 CP2 범위).

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | `read_console` error/warning | PASS | 최종 컴파일 후 오류 0, 프로젝트 경고 0(MCP 패키지 WebSocket 경고는 외부, CP0 로그 참고) |
| ALL-2 | MCP `run_tests` | PASS | EditMode 80/80, PlayMode 3/3(CP0 테스트 포함) |
| ALL-3 | `SimulationRulesTests.SimulationSources_FollowRules` + 메뉴 | PASS | 위반 0 |
| ALL-4 | `MatchRunnerTests.ShowcaseScenarios_RunWithoutErrors`(플레이 모드) | PASS | `showcase_cp1_mirror_run_jump` 150틱, expect 9개 통과, 콘솔 오류 0, 플레이 모드 최종 체크섬 = 헤드리스 실행 체크섬 |
| ALL-5 | 커밋·태그·푸시 | PASS | `cp-1` |
| ALL-6 | 기록 갱신 | PASS | 이 로그, PROGRESS.md, DECISIONS.md |
| CP1-1 | `DeterminismTests.SameSeedRandomInputs_ChecksumsMatchEveryTick` | PASS | 카운트다운부터 3600틱, 매 틱 두 월드 체크섬 동일. 서로 다른 체크섬 3600개, P1 x 이동 폭 > 5 |
| CP1-2 | `DeterminismTests.SaveAdvanceRestoreReplay_ChecksumMatches` | PASS | 200틱 후 저장 → 100틱 → 복원(체크섬 = 저장 시점) → 재진행 체크섬 동일 |
| CP1-3 | `SyncTestTests.Rollback7_3600Ticks_NoMismatch` | PASS | 3600프레임 불일치 0, SyncTest를 거친 최종 체크섬 = 그냥 진행한 체크섬. 검출력: `NonDeterministicSim_IsDetectedWithFieldName`이 1프레임에서 `Fighter0.Position.X` 불일치를 잡음 |
| CP1-4 | `AllocationTests.Step_DoesNotAllocate` 외 2개 | PASS (2회차) | 600틱 Step, 체크섬 1000회, SyncTest.Advance 590회 모두 0바이트. 1회차 FAIL: 측정 구간 안에서 이동·점프·착지 경로가 처음 실행되며 JIT 할당이 잡힘 → 같은 입력으로 전체 경로를 예열한 뒤 새 월드로 측정 |
| CP1-5 | 임시 파일 `Simulation/RuleViolationProbe.cs` | PASS | 넣은 위반 9종(LINQ, UnityEngine, static 가변 필드, 삼각함수, SimMath 밖 Math, System.Random, DateTime, Dictionary, HashSet)을 모두 잡아 FAIL → 파일 삭제 후 PASS. 상시 테스트 `Checker_DetectsForbiddenPattern` 23케이스, `Checker_AllowsLegalCode` 8케이스 |
| CP1-6 | `ScenarioTests` | PASS | `sample_walk_jump` 로드, expect 9개 평가·통과. `WrongExpect_IsReportedAsFailure`: 틀린 expect 2개를 실패로 보고(맞는 1개는 통과). 공용 러너 `Scenario_ExpectsPass`가 expect 있는 시나리오 2개 실행 |
| CP1-7 | `DeterminismTests.RecordSerializeReplay_FinalChecksumMatches` | PASS | 1800틱 기록 → 바이트 직렬화 → 역직렬화(입력 전부 동일) → 재생 최종 체크섬 = 기록 시점. 설정이 다르면 재생 거부(`ReplayWithDifferentConfig_IsRejected`) |
| CP1-8 | `PerformanceTests.StepAndRollback_StayWithinBudget` | PASS | Step 평균 **0.15 µs**(예산 250 µs), 롤백 8틱 **1.44 µs**(예산 2000 µs). 에디터 EditMode, 파이터 2명, 지형 없음(CP2에서 재측정) |
| CP1-9 | `MatchRunnerTests.TenSecondsOfRealTime_Runs600Ticks`(Unity 앞) | PASS | **10.0025초에 600틱**(기대 600.2), 버린 틱 0. 빈 씬 기준 약 400fps |

## 4. 캡처 확인

CP1에는 [S] 기준이 없다. showcase 시나리오의 `captures` 항목은 정의만 해 두었고, 캡처 저장은 임시 그래픽이 생기는 CP2에서 붙인다.

## 5. 자체 결정

- S-007 상태 필드 목록은 `IStateVisitor` 하나로 관리, 반사 기반 범위 테스트
- S-008 Simulation에 `Replay/`, `Scenario/` 폴더 추가, JSON 읽기는 Game 계층
- S-009 시나리오 해석 규칙(hold 양 끝 포함, 입력 인덱스와 expect 틱의 의미 등)
- S-010 CP1 임시 이동과 임시 경기장
- S-011 규칙 점검 방식과 추가 금지 항목
- S-012 카운트다운 중 버튼 기록 갱신, ActionTick 증가 시점
- S-013 MatchRunner 시간원은 실제 경과 시간, 입력 지연 최대 15틱
- S-014 입력 기록 파일 형식 `.kbrp`
- S-015 이벤트 버퍼 기본 용량 64

## 6. 알려진 문제와 미룬 것

- 디버그 단축키(F8 SyncTest, F9/F10 입력 기록·재생)는 키 연결 없이 API만 있다(`MatchRunner.SetSyncTest`, `StartRecording`/`StopRecording`, `ReplayFiles`). 키보드 입력을 붙이는 CP2 이후, 명세상 CP4에서 연결한다.
- `ScenarioPlayer`는 아직 캡처를 저장하지 않는다. 그릴 것이 생기는 CP2에서 `Knockbox/Run Scenario Captures` 메뉴와 함께 만든다.
- 시나리오의 `arena`, `mode`, `grab`은 읽기만 하고 아직 쓰지 않는다(CP2 맵, CP4~CP7 모드).
- 입력 기록은 라운드 시작 상태에서만 시작할 수 있다(시작 상태를 생성 값으로만 저장하므로). 라운드 중간부터 기록하려면 상태 직렬화가 필요한데, 지금은 필요가 없어 미룬다.
- 두 파이터가 서로 통과한다(CP2 밀어내기).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 80/80, PlayMode 3/3. Main 씬이 열려 있다.
- **CP2에서 교체할 것**: `Systems/TempMovement.cs`, `Systems/TempArena.cs`. `SimState.CreateRound`와 `ScenarioInputs.CreateInitialState`, `MatchRunner.StartNewMatch`가 TempArena를 쓰고 있으니 ArenaDef로 바꿀 때 함께 고친다. `MovementParams`에는 02-movement.md의 모든 초기값이 이미 들어 있다.
- 상태에 필드를 추가하면 그 구조체의 `Visit`에도 추가한다. 빠뜨리면 `StateChecksumTests`가 실패한다. 상태에 참조 형식 필드를 넣으면 같은 테스트가 실패한다.
- 시뮬레이션 소스의 주석에도 금지 문자열(`UnityEngine`, `DateTime` 등)을 쓰지 않는다(문자열 규칙은 주석까지 검사).
- 할당 테스트는 같은 입력으로 예열한 뒤 측정한다. 새 경로를 추가하면 예열 입력이 그 경로를 지나가는지 확인한다.
- PlayMode 테스트와 실시간 측정은 `Tools/focus-window.ps1 -Unity`로 Unity를 앞으로 가져와 실행하고 `-Restore <핸들>`로 되돌린다.
- 다음 첫 작업: CP2 로그 생성, `04-arena.md` 맵 형식대로 `ArenaParser`와 맵 파일 3종.
