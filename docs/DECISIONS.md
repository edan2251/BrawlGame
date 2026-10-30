# 결정 기록

세 종류를 구분한다.

- **D (확정/기본값)**: 사용자가 정한 것, 또는 기획 대화에서 제안해 기본값으로 둔 것. 기본값을 바꾸는 것은 기획 결정이므로 사용자에게 묻는다.
- **S (자체 결정)**: Claude Code가 명세 범위 안에서 스스로 정한 것. 묻지 않고 기록만 한다.
- **Q (대기 질문)**: 사용자 답이 필요한 것. 답을 받으면 D로 옮기고 Q에는 "→ D-번호"라고 남긴다.

---

## D: 확정과 기본값

| ID | 결정 | 출처 | 근거 |
|---|---|---|---|
| D-001 | 엔진은 Unity 6000.3 LTS(최신 패치). 6000.5·6000.6은 LTS 표시가 확인되지 않았고 2D 저수준 물리 API 네임스페이스도 바뀌어 쓰지 않는다. Unity Hub에 6000.3보다 높은 LTS가 생기면 사용자에게 알리고 판단한다 | 사용자 | 사용자가 익숙한 버전, LTS 조건 |
| D-002 | Windows, MCP for Unity v10.3.0, Git + GitHub 원격 저장소 | 사용자 | 환경 질문 답변 |
| D-003 | 게임 결과를 정하는 시뮬레이션은 Unity 물리 없이 순수 C#로 직접 구현. Unity 물리는 연출 전용 | 기본값 | 온라인 롤백에 필요한 상태 저장·복원과 결정론 |
| D-004 | 프로토타입은 `float` 사용, 수학 연산은 `SimMath`로만. 고정소수점 전환은 CP10 착수 전에 측정 후 판단 | 기본값 | 개발 속도와 이후 전환 비용의 균형 |
| D-005 | 체력 100의 체력 바 방식. 넉백 누적 방식은 채택하지 않음 | 기본값 | 닫힌 경기장 구조와 단순함 |
| D-006 | 대시와 기상에 무적 시간 없음 | 기본값 | 회피는 움직임과 패링으로 |
| D-007 | 전투 후보: 주 모드 A 돌진 충돌, 주 모드 B 무기 휘두르기, 두 모드 공통 보조 스킬 잡기·던지기 | 사용자 | 세 후보 모두 넣고 세 번째는 보조 스킬로 |
| D-008 | 로컬 2인 먼저 완성, 온라인은 CP10에서 질문 후 착수 | 기본값 | 손맛 확인을 온라인보다 먼저 |
| D-009 | 업그레이드 카드 시스템은 이 프로토타입 범위 밖. RoundTransition 훅만 둔다 | 기본값 | 전투 방향 확정이 먼저 |
| D-010 | 두 레퍼런스 게임은 스타일 방향만 참고하고 캐릭터·UI·카드·로고·대표 색 조합은 따라 하지 않는다 | 기본값 | 고유한 정체성, 출시 위험 회피 |
| D-011 | 버튼 구성: 점프, 공격, 대시, 가드, 잡기 + 8방향 이동 | 기본값 | 세 후보를 같은 조작 위에서 비교하기 위해 |
| D-012 | 아트 방향 "잉크 외곽선 장난감 블록", 팔레트는 `05-presentation.md` | 기본값 | 플랫한 화면, 색각 이상 고려(노랑·보라) |
| D-013 | 가제 KNOCKBOX, 네임스페이스 `Knockbox` | 기본값 | 바꾸려면 CP1 이전에 질문 |
| D-014 | GitHub 원격 저장소는 `https://github.com/edan2251/BrawlGame` (사용자가 직접 만듦, **공개** 저장소). 로컬 프로젝트 폴더 이름은 `Project_Brawl` | 사용자 | Q-003 답변(2026-10-31 첫 지시에서 주소 제공) |
| D-015 | 실시간 플레이 모드 검증(틱 수, fps, 플레이 모드 캡처, PlayMode 테스트) 동안은 Claude Code가 `Tools/focus-window.ps1`로 Unity 창을 앞으로 가져오고, 끝나면 이전 창으로 포커스를 돌려놓는다 | 사용자 | Q-006 답변. 백그라운드에서는 플레이 모드가 멈춘다 |
| D-016 | UI 글자는 영문 짧은 단어와 숫자만 쓴다. 한글 폰트가 필요하면 사용자가 나중에 추가한다 | 사용자 | Q-002 답변 |

---

## S: 자체 결정

형식: `| S-번호 | 날짜 | CP | 결정 | 이유 | 영향받는 문서·코드 |`

| ID | 날짜 | CP | 결정 | 이유 | 영향 |
|---|---|---|---|---|---|
| S-001 | 2026-10-31 | CP0 | 네임스페이스는 어셈블리 단위(`Knockbox.Simulation`, `Knockbox.Presentation`, `Knockbox.Game`)로만 두고 하위 폴더 이름을 네임스페이스로 쓰지 않는다. Editor 어셈블리(`Knockbox.Editor`)의 네임스페이스는 `Knockbox.EditorTools` | 폴더 `Game/Debug`, `Game/Input`, `Presentation/Camera`, `Presentation/Audio`를 네임스페이스로 만들면 `UnityEngine.Debug`·`Input`·`Camera`를 가리고, `Knockbox.Editor` 네임스페이스는 `UnityEditor.Editor`를 가려 컴파일 오류가 난다 | 모든 asmdef의 rootNamespace |
| S-002 | 2026-10-31 | CP0 | `Knockbox.Simulation.asmdef`의 `overrideReferences`를 true로, `precompiledReferences`는 비워 둔다 | false면 '자동 참조'가 켜진 패키지 DLL(`Unity.Collections.LowLevel.ILSupport`, `Unity.VisualScripting.Antlr3.Runtime`)이 시뮬레이션 컴파일에 들어온다. CP0-4 테스트가 실제로 잡아냄 | Simulation asmdef, `SimulationAssemblyReferenceTests` |
| S-003 | 2026-10-31 | CP0 | Graphics Settings의 기본 렌더 파이프라인도 `UniversalRP.asset`으로 지정 | 템플릿은 품질 단계별 지정만 있고 기본값이 비어 있었다. 품질 단계가 추가되거나 지정이 빠져도 내장 파이프라인으로 떨어지지 않게 | `GraphicsSettings.asset`, `ProjectSetup.ApplyProjectSettings` |
| S-004 | 2026-10-31 | CP0 | 씬은 `Assets/_Project/Scenes/{Main,Sandbox}.unity`에 `Knockbox/Setup/Run All` 메뉴로 만든다(없을 때만 생성). 템플릿 `Assets/Scenes/SampleScene.unity`는 파일을 두고 빌드 목록에서만 뺀다 | 재현 가능한 생성, 명세 2절 폴더 구조, 템플릿 산출물은 그대로 둔다는 규칙 | `ProjectSetup.cs`, `EditorBuildSettings.asset` |
| S-005 | 2026-10-31 | CP0 | 빈 폴더는 `.gitkeep`으로 추적한다 | Unity는 점으로 시작하는 파일을 가져오지 않으므로 무해하고, 폴더 `.meta`만 커밋돼 다른 PC에서 고아 meta 경고가 나는 것을 막는다 | `Assets/_Project/**`, `Tools/` |
| S-006 | 2026-10-31 | CP0 | 에디터가 백그라운드일 때 PlayMode 테스트는 `EditorApplication.isPaused = true; Step()`을 MCP로 반복 호출해 프레임을 진행시켜 실행한다(임시) | 백그라운드에서는 Run In Background를 켜도 플레이 모드가 1프레임에서 멈춘다(CP0 로그 2절). 프레임 수 기반 테스트는 이 방식으로 검증 가능. 실시간 측정은 Q-006 답을 받아 정한다. **D-015로 대체**: 이후에는 Unity를 앞으로 가져와 실행한다 | PlayMode 테스트 실행 절차 |
| S-007 | 2026-10-31 | CP1 | 상태 필드는 각 구조체의 `Visit<T>(ref T) where T : struct, IStateVisitor` 한 곳에 명시적 순서로 나열하고, 체크섬(`Hasher`)과 상태 비교(`StateDiff`)가 이를 공유한다. 테스트가 반사로 모든 말단 필드를 바꿔 보며 체크섬이 변하는지, 참조 형식 필드가 없는지 검사한다 | 체크섬과 불일치 보고가 서로 다른 필드 목록을 갖는 실수를 막는다. 구조체 제네릭이라 할당이 없다. 반사는 테스트에서만 쓴다 | `IStateVisitor`, 상태 구조체, `StateChecksumTests` |
| S-008 | 2026-10-31 | CP1 | Simulation에 `Replay/`(SyncTest, 입력 기록, 상태 비교, 무작위 입력)와 `Scenario/`(시나리오 모델, 입력 펼치기, expect 평가, 헤드리스 실행) 폴더를 추가. JSON 읽기(`MiniJson`, `ScenarioJson`)는 Game 계층에 둔다 | 테스트와 플레이 모드가 같은 순수 코드를 쓰게 하려고. 시뮬레이션은 `Dictionary`를 쓸 수 없고 Unity JSON도 쓸 수 없으며, 외부 JSON 패키지는 사전 승인 목록에 없다 | 01-architecture.md 2절 폴더 구조의 확장 |
| S-009 | 2026-10-31 | CP1 | 시나리오 해석: hold의 `from`~`to`는 양 끝 포함, 입력 배열 인덱스 t는 틱 t→t+1 Step에 쓰임, expect의 `tick` T는 T번 Step한 직후 상태, 같은 틱의 좌우(상하) 동시 입력은 상쇄, `equals`는 숫자(오차 1e-4)·참거짓·이름(`action`, `phase`), 선택 필드 `seed`(기본 1). 지원 필드: tick, phase, pos.x/y, vel.x/y, facing, action, actionTick, grounded(hp는 CP4에서 추가) | 06-match-and-tools.md 5절이 열어 둔 세부 | `ScenarioInputs`, `ExpectEvaluator`, `ScenarioJson` |
| S-010 | 2026-10-31 | CP1 | CP1 임시 이동: 02-movement.md 수치 중 달리기 가감속·방향 전환 배율·점프 초속 22.9·상승 중력 65·하강 배율 1.6·최대 낙하 30만 사용. 임시 경기장은 Box 맵 안쪽과 같은 바닥 y=-8, 벽 x=±15, 천장 y=8. 기본 시작 위치 (∓6, -7.55) | 결정론 도구 검증에 의미 있는 상태 변화가 필요(CP1 범위). CP2에서 맵과 이동 명세 전체로 교체 | `TempMovement`, `TempArena` |
| S-011 | 2026-10-31 | CP1 | 규칙 점검은 `Tools/` 스크립트 대신 EditMode 테스트(`SimulationRulesTests`)와 에디터 메뉴로 한다. 문자열 규칙은 주석까지 검사한다. 명세 목록에 더해 `Stopwatch`, `Environment.`, `System.Threading`·`Thread`·`Task`·`async`·`await`, `MathF` 삼각함수를 금지하고, `Math.`/`MathF.` 호출은 `SimMath.cs`에서만 허용한다. static 필드는 `const`와 배열·List가 아닌 `static readonly`만 허용 | 회귀 테스트로 매번 돌게 하려고. 01-architecture.md 4절 2·4항의 금지 항목까지 덮는다 | `SimRulesChecker` |
| S-012 | 2026-10-31 | CP1 | 카운트다운 중에도 직전 버튼 기록을 갱신해, GO 순간 누르고 있던 버튼이 새 눌림으로 잡히지 않게 한다. `ActionTick`은 Step 시작에 1 올리고 행동이 바뀌면 0으로 되돌린다(바뀐 틱에 0) | 카운트다운 동안 점프를 누르고 있다가 GO에 의도치 않게 뛰는 일을 막는다 | `SimWorld.Step` |
| S-013 | 2026-10-31 | CP1 | `MatchRunner`의 시간원은 `Time.realtimeSinceStartupAsDouble`의 프레임 간 차이. 입력 지연은 0~15틱 | `deltaTime`은 `maximumDeltaTime`과 `timeScale`의 영향을 받는다. 실제 시간 기준이어야 CP1-9(10초에 600틱)가 맞는다 | `MatchRunner`, `FixedTickClock`, `InputDelayQueue` |
| S-014 | 2026-10-31 | CP1 | 입력 기록 파일은 바이너리 `.kbrp`(머리글 `KBRP`, 버전 1): 설정 체크섬, 시드, 시작 위치 2개, 시작 단계, 맵, 모드, 시작·최종 체크섬, 틱마다 플레이어당 3바이트. 재생 시 설정이나 시작 상태 체크섬이 다르면 거부 | 작고 빠르며 재생 결과를 검증할 수 있다. 라운드 중간 시작은 아직 필요 없어 상태 직렬화는 미룸 | `InputRecording`, `ReplayFiles` |
| S-015 | 2026-10-31 | CP1 | 이벤트 버퍼 기본 용량 64(최소 32), 넘치면 버리고 `Dropped`로 센다 | 명세의 최소 32보다 여유를 둔다. 한 틱에 64개를 넘는 일은 없어야 하므로 넘치면 버그 신호 | `SimEventBuffer` |

---

## Q: 대기 질문

| ID | 질문 | 선택지와 추천 | 막는 작업 | 상태 |
|---|---|---|---|---|
| Q-001 | 레퍼런스 영상(https://www.youtube.com/watch?v=tgr2NQsMFTE)에서 가져오고 싶은 점은? | 사용자가 자유롭게 적음. Claude Code는 영상을 볼 수 없으므로 추측하지 않는다 | 없음(답을 받으면 해당 명세에 반영) | 대기 |
| Q-002 | 한글 UI 폰트를 도입할까? | 추천: OFL 라이선스의 굵은 한글 디스플레이 폰트(예: Black Han Sans). 기본값은 영문 짧은 단어와 숫자만 | 없음(CP8 전에 답이 있으면 반영) | → D-016 |
| Q-003 | GitHub 저장소 이름과 공개 범위는? | 추천: `Knockbox`, 비공개 | CP0의 푸시만 | → D-014 |
| Q-004 | 비교용 빌드를 플레이해본 뒤, 어떤 전투 방향으로 갈까? | 모드 A / 모드 B / 섞기 / 둘 다 버리고 다른 방식. 잡기 유지 여부 | CP9 이후 전체 | CP9에서 질문 |
| Q-005 | 온라인 방식(플랫폼, 연결, 기술, 숫자 표현, 비용)은? | `07-online-draft.md`의 질문 목록 | CP10 | CP10 전에 질문 |
| Q-006 | Unity 창이 백그라운드면 플레이 모드가 멈춘다. 실시간 플레이 모드 검증(CP1-9 틱 수, CP3-5 fps, 시나리오 캡처)을 어떻게 할까? | (1) **추천**: 검증하는 동안 Claude Code가 Unity 창을 앞으로 가져왔다가 끝나면 터미널로 돌려놓는다(수 초~수십 초 포커스 이동). (2) 포커스를 건드리지 않는다: 프레임 수 기반 검증은 `Step()` 진행, 실시간 측정은 Windows 개발 빌드를 백그라운드로 실행해 측정. (3) 필요할 때 사용자에게 Unity 창 클릭을 요청 | CP1-9, CP3-5, 플레이 모드 캡처 | → D-015 |
