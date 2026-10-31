# 01. 아키텍처

## 1. 계층 구조

```
 입력 장치 ──▶ [Game] InputProvider ──▶ InputFrame(P1, P2)
                                         │
                                         ▼
                           [Simulation] SimWorld.Step()   ← 고정 60틱, 순수 C#
                                         │
                         SimState(복제·복원·체크섬)  +  SimEvent 버퍼
                                         │
                                         ▼
                 [Presentation] 보간 렌더링, IK 팔다리, 타격 연출, 사운드, 연출 물리
```

| 계층 | 어셈블리 | 책임 | 참조 가능 |
|---|---|---|---|
| Simulation | `Knockbox.Simulation` | 게임 결과를 결정하는 모든 것 | 없음 (`noEngineReferences: true`) |
| Presentation | `Knockbox.Presentation` | 화면, 소리, 연출 전용 물리 | Simulation, Unity |
| Game | `Knockbox.Game` | 부트스트랩, 틱 실행기, 입력, 튜닝 데이터, HUD, 디버그 도구 | Simulation, Presentation, Unity |
| Editor | `Knockbox.Editor` | 스프라이트 생성기, 검증 메뉴, 시나리오 도구 | 전부 (에디터 전용) |
| Tests | `Knockbox.Tests.EditMode`, `Knockbox.Tests.PlayMode` | 자동 검증 | Simulation, Game, Presentation |

연출 계층은 시뮬레이션 상태를 **읽기만** 한다. 연출에서 생긴 값(파티클 위치, 래그돌 결과)이 시뮬레이션으로 돌아가는 경로는 없어야 한다.

## 2. 폴더 구조

```
<프로젝트 루트>/
├─ CLAUDE.md
├─ docs/                         명세, 체크포인트, 진행 기록
├─ Tools/                        Unity 밖에서 쓰는 스크립트 (효과음 합성, 규칙 점검)
└─ Assets/_Project/
   ├─ Scripts/
   │  ├─ Simulation/             Knockbox.Simulation.asmdef
   │  │  ├─ Core/                Vec2, SimMath, Hasher, SimRng
   │  │  ├─ Data/                InputFrame, SimConfig, ArenaDef, SimEvent
   │  │  ├─ State/               SimState, FighterState, MatchState
   │  │  └─ Systems/             Movement, TerrainCollision, Combat/(Ram, Swing, Grab, Guard), Match
   │  ├─ Presentation/           Knockbox.Presentation.asmdef
   │  │  ├─ Fighter/             FighterView, LimbRig, SquashStretch, Face
   │  │  ├─ Arena/               ArenaView, Background
   │  │  ├─ Fx/                  Hitstop 연출, ScreenShake, Flash, Trails, 파티클 풀, KO 파편
   │  │  ├─ Audio/
   │  │  └─ Camera/
   │  └─ Game/                   Knockbox.Game.asmdef
   │     ├─ Bootstrap/           MatchRunner (틱 실행기), 씬 시작
   │     ├─ Input/               InputProvider, 장치 배정, 8방향 양자화
   │     ├─ Tuning/              ScriptableObject → SimConfig 변환
   │     ├─ UI/                  HUD
   │     └─ Debug/               오버레이, 단축키, SyncTest 토글, ScenarioRunner, 캡처
   ├─ Editor/                    Knockbox.Editor.asmdef
   ├─ Tests/EditMode/  Tests/PlayMode/
   ├─ Art/  Audio/  Prefabs/  Scenes/
   └─ Data/
      ├─ Tuning/                 MovementTuning, CombatTuning (ScriptableObject)
      ├─ Arenas/                 맵 데이터
      └─ Scenarios/              시나리오 JSON (테스트와 캡처에 공용)
```

템플릿이 만든 `Assets/Settings` 등은 그대로 둔다.

## 3. 시뮬레이션 API

```csharp
namespace Knockbox.Simulation
{
    public struct InputFrame            // 한 틱의 한 플레이어 입력
    {
        public sbyte MoveX;             // -1, 0, 1
        public sbyte MoveY;             // -1(아래), 0, 1(위)
        public InputButtons Buttons;    // 비트 플래그: Jump, Attack, Dash, Guard, Grab
    }

    public sealed class SimWorld
    {
        public SimState State;          // 값 타입. 대입 한 번으로 스냅숏이 된다
        public readonly SimConfig Config;
        public readonly ArenaDef Arena;
        public SimEventBuffer Events { get; }   // 매 Step 시작 시 비워짐

        public void Step(in InputFrame p1, in InputFrame p2);
        public uint ComputeChecksum();
    }
}
```

- `SimState`는 **값 타입의 구조체**로, 배열이나 참조 필드 없이 `Fighter0`, `Fighter1`, `Match`, `Rng`, `Tick` 같은 필드로 구성한다. 그래서 `var snapshot = world.State;`가 곧 완전한 저장이다.
- **구조물 상태도 시뮬레이션 상태다**(D-025, `09-props-physics.md`). 강체, 줄, 경첩의 위치·회전·속도·끊어짐 여부와 마지막 접촉자가 저장·복원·체크섬 대상이다. 용량은 고정(예: 강체 48, 연결 48)으로 미리 할당한다. 구조물 때문에 "대입이 곧 스냅숏"인 성질이 깨지면 명시적 복사(`CopyTo`)와 미리 할당한 스냅숏 버퍼로 대신하며, 어느 쪽이든 `Visit`(체크섬)와 `StateDiff`에 모든 필드가 들어가고 `Step`과 스냅숏 복사에 GC 할당이 없어야 한다.
- 맵 상태(라운드마다 바뀌는 맵 순서와 현재 맵, D-024)도 `MatchState`에 들어간다.
- "이번 틱에 눌렀다/뗐다"는 시뮬레이션이 직접 계산한다. 이를 위해 각 파이터 상태에 직전 틱의 버튼 값을 저장한다.
- 입력 장치의 아날로그 값은 Game 계층에서 **8방향으로 양자화**한 뒤 시뮬레이션에 넘긴다.
- `SimEventBuffer`는 고정 용량(최소 32) 배열이며 상태에 포함하지 않는다. 이벤트 종류: `Jump`, `Land`, `WallSlideStart`, `WallJump`, `Dash`, `Hit`, `Clash`, `Parry`, `GuardHit`, `Grab`, `Throw`, `WallSplat`, `BounceRebound`, `KO`, `RoundStart`, `RoundEnd`. 각 이벤트는 틱, 파이터 번호, 위치, 방향, 세기를 가진다.
- 나중에 롤백을 위해 이벤트는 `(틱, 종류, 파이터)`로 식별 가능해야 한다. 지금은 연출이 즉시 소비하면 된다.

## 4. 결정론 규칙

시뮬레이션 어셈블리 안에서는 다음을 지킨다.

1. UnityEngine을 참조하지 않는다(어셈블리 설정으로 강제).
2. `System.Random`, `DateTime`, `Stopwatch`, `Environment`, 스레드, `Task`, `async`를 쓰지 않는다. 난수가 필요하면 상태에 포함된 `SimRng`(xorshift 계열)를 쓴다.
3. `Dictionary`, `HashSet`, LINQ를 쓰지 않는다. 고정 크기 구조와 명시적 순서의 순회만 쓴다.
4. 삼각함수(`Math.Sin`, `Cos`, `Atan2` 등)를 쓰지 않는다. 각도는 설정 단계에서 단위 벡터로 미리 바꿔 둔다. 제곱근 같은 필요한 연산은 `SimMath`를 통해서만 호출한다. 나중에 고정소수점으로 바꿀 때 이 한 곳만 고치면 되도록 하기 위해서다.
   - **회전하는 구조물**(09 3절): 회전은 각도가 아니라 **(cos, sin) 쌍**으로 저장하고, 각속도만큼 돌린 뒤 정규화하는 방식으로 적분한다(Box2D v3와 같은 방식). 맵 데이터의 초기 각도는 단위 벡터로 적거나, `SimMath`에 둔 결정론적 다항식 근사로 바꾼다(Box2D v3의 근사를 옮기면 MIT 출처를 파일 머리에 적는다). 이 경우에도 `Math.Sin`류는 쓰지 않는다.
5. 정적 가변 상태(static 필드에 값 저장)를 두지 않는다.
6. `Step` 안에서 GC 할당을 하지 않는다(테스트로 강제).
7. **대칭성**: 두 파이터의 판정은 같은 이전 상태에서 동시에 계산하고 동시에 적용한다. 동률일 때 P1을 우선하는 식의 인덱스 기반 판정을 하지 않는다. 동률은 규칙으로 해소한다(예: 클래시).
8. 체크섬은 `Hasher`(FNV-1a)로 모든 상태 필드를 명시적으로 순서대로 넣어 계산한다. float는 비트 패턴(`BitConverter.SingleToInt32Bits`)으로 넣는다. 리플렉션을 쓰지 않는다.

숫자 표현은 프로토타입 동안 `float`를 쓴다. 같은 빌드와 같은 CPU 계열에서는 위 규칙만 지키면 대체로 일치하지만, 서로 다른 플랫폼이나 빌드 백엔드 사이에서는 보장되지 않는다. 고정소수점 전환 여부는 온라인 CP(CP18) 착수 전에 다시 판단한다(`DECISIONS.md`의 D-004).

## 5. 틱 실행기와 렌더링

- `MatchRunner`(Game 계층)가 `Update`에서 경과 시간을 누적해 1/60초마다 `Step`을 호출한다. 한 프레임에 최대 4틱까지만 따라잡고 나머지는 버린다.
- 각 틱의 입력은 `IInputSource`에서 가져온다. 구현체는 로컬 장치 입력, 시나리오 재생, 허수아비 행동, (나중에) 네트워크 입력이다.
- 로컬 **입력 지연 설정**(기본 0틱)을 둔다. 온라인 체감을 미리 시험하는 용도다.
- 연출은 직전 상태와 현재 상태를 누적 비율로 보간해서 그린다. 히트스탑 중에는 보간하지 않는다.
- 튜닝 ScriptableObject를 `SimConfig`(순수 C#)로 변환하는 일은 라운드 시작 시점에만 한다. 플레이 중 인스펙터에서 값을 바꾸면 리셋(F5) 시 반영된다.

## 6. SyncTest와 입력 기록

- **SyncTest**: 매 틱 상태를 저장하고 진행한 뒤, N틱(기본 7) 전으로 되감아 저장된 입력으로 다시 시뮬레이션해서 체크섬이 같은지 비교한다. 롤백 넷코드의 사전 검증 도구이며, EditMode 테스트와 플레이 중 토글(F8) 두 가지로 쓸 수 있다. 불일치가 나면 해당 틱과 필드를 로그에 남긴다.
- **입력 기록**: 매치 동안의 양쪽 입력, 맵, 모드, 설정 체크섬을 기록해 파일로 저장하고 다시 재생할 수 있게 한다. 재생 결과의 최종 체크섬이 기록 시점과 같아야 한다.
- **시나리오**: 사람이 쓰는 짧은 입력 스크립트(JSON). 형식은 `06-match-and-tools.md`. 자동 테스트와 화면 캡처가 같은 시나리오를 공유한다.

## 7. 성능 예산

| 항목 | 기준 |
|---|---|
| `Step` 1회 | 에디터에서 평균 0.25ms 이하 (파이터 2명, 맵 최대 64개 상자 기준) |
| `Step`의 GC 할당 | 0바이트 |
| 롤백 8틱 재시뮬레이션 | 에디터에서 2ms 이하 |
| 플레이 모드 | 1920×1080, 에디터에서 60fps 유지. 개발 PC 사양과 실측값을 로그에 기록 |

## 8. 규칙 자동 점검

`Tools/check-sim-rules` 스크립트(또는 동등한 EditMode 테스트)로 시뮬레이션 폴더의 소스에서 다음 문자열이 없는지 검사한다: `UnityEngine`, `System.Random`, `DateTime`, `Dictionary<`, `HashSet<`, `System.Linq`, `Math.Sin`, `Math.Cos`, `Math.Atan`, `Math.Tan`, `static` 가변 필드. `SimMath.cs`는 허용된 연산의 유일한 관문이므로 예외 목록을 명시한다.
