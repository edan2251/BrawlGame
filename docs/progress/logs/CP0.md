# CP0. 환경과 골격

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | CHECKPOINTS.md CP0, spec/01-architecture.md, SETUP.md 8절 |
| 태그 | cp-0 |

## 1. 계획

1. 환경 확인: Unity 버전, URP 2D Renderer 활성, 사전 승인 패키지, MCP `read_console`.
2. Git 초기화 후 템플릿 상태를 첫 커밋으로 남기고, 사용자가 알려준 원격(`edan2251/BrawlGame`)을 연결한다.
3. Player Settings의 Run In Background를 켠다.
4. `01-architecture.md` 2절의 폴더와 어셈블리 정의 6개(Simulation, Presentation, Game, Editor, Tests.EditMode, Tests.PlayMode)를 만든다. Simulation은 `noEngineReferences: true`.
5. 씬 `Main`, `Sandbox`를 에디터 스크립트(`Knockbox/Setup/...` 메뉴)로 생성해 재현 가능하게 하고 빌드 설정에 등록한다.
6. EditMode 스모크 테스트와 시뮬레이션 어셈블리 참조 검사 테스트를 만들고 MCP `run_tests`로 통과를 확인한다.
7. Main 씬 스크린샷, `git status` 무시 규칙 확인, 패키지 목록 기록 후 태그 `cp-0` 푸시.

## 2. 작업 기록

- 이 세션에는 MCP for Unity 도구가 로드되지 않았다(`claude mcp list`에서는 `UnityMCP: http://127.0.0.1:8080/mcp (HTTP) - Connected`). 같은 로컬 엔드포인트에 JSON-RPC(`tools/call`, `resources/read`)를 직접 보내는 방식으로 진행했다. Claude Code를 다시 시작하면 기본 도구로 보일 것으로 예상.
- MCP 확인: `mcpforunity://instances` → `Project_Brawl@d67741550f30280e`, Unity 6000.3.23f1. `read_console`(error, warning) → 0건.
- 렌더 파이프라인 조회(`execute_code`): 품질 단계 6개 모두 `Assets/Settings/UniversalRP.asset`, 렌더러 0번 `Renderer2DData`. 다만 Graphics Settings의 기본 파이프라인은 비어 있었다 → 기본값도 URP로 지정(S-003).
- 입력 처리 설정 `activeInputHandler: 1`(Input System 전용) 확인.
- 어셈블리 6개 생성. 첫 컴파일에서 `Light2D`를 못 찾는 오류 2건 → `Light2D`와 `Renderer2DData`는 `Unity.RenderPipelines.Universal.2D.Runtime` 어셈블리에 있어 Presentation, Editor, Tests.EditMode에 참조를 추가해 해결.
- 씬 생성 1차: 추가 모드로 만들었더니 열려 있던 SampleScene의 전역 조명과 겹쳐 "More than one global light" 메시지가 났고, 빌드 씬 목록이 디스크에 저장되지 않았다. 단일 모드 생성 + `AssetDatabase.SaveAssets()`로 고치고 두 씬을 지운 뒤 다시 생성 → 메시지 0건, 빌드 목록 저장 확인.
- 멱등성: `Knockbox/Setup/Run All`을 한 번 더 실행해 두 씬과 `EditorBuildSettings.asset`의 md5가 그대로임을 확인.
- 참조 검사 테스트 1차 FAIL: `CompilationPipeline_PassesNoUnityReferences`가 `Unity.Collections.LowLevel.ILSupport.dll`, `Unity.VisualScripting.Antlr3.Runtime.dll`을 잡아냈다. `overrideReferences: false`라서 '자동 참조'가 켜진 패키지 DLL이 들어오고 있었다 → Simulation asmdef를 `overrideReferences: true`로 바꾸고(S-002), asmdef 검사 테스트에도 이 조건을 추가. 2차 PASS.
- 규칙 점검(수동 grep, 점검 도구는 CP1에서 만든다): 주석 속 `UnityEngine` 문자열 1건 → 문자열 기반 점검기와 충돌하지 않게 문구 수정. 재검사 0건.
- **PlayMode 문제**: Unity 창이 포커스를 잃은 상태(터미널이 앞)에서는 플레이 모드가 1프레임에서 멈춘다. `Time.frameCount=1`, `Time.time=0`이 수 초 동안 고정. Run In Background 켜짐, 창 최소화 아님, vSync 끔, MCP의 No Throttling 적용 상태에서도 같다. MCP `run_tests`(PlayMode)는 "failed to initialize within 15000ms"로 자동 실패 처리됐다. `EditorApplication.isPaused = true; EditorApplication.Step();`은 백그라운드에서도 1프레임씩 진행된다 → 테스트 실행 중 MCP로 `Step()`을 반복 호출하는 방식으로 PlayMode 스모크 테스트 통과 확인(13프레임). 실시간 측정(CP1-9 틱 수, CP3-5 fps)은 이 방식으로 할 수 없어 Q-006으로 질문.
- MCP for Unity가 도메인 리로드마다 `[WebSocket] Unexpected receive error: WebSocket is not initialised` 경고를 남긴다. 패키지 자체 메시지라 프로젝트 경고로 세지 않는다.
- MCP의 PlayMode 테스트 기능이 에디터 환경설정 `InteractionMode`를 1(No Throttling)로 바꾼 채 남겨 두었다(사용자 에디터 설정, 프로젝트 밖). 동작에 해가 없어 그대로 둔다.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | `read_console` error/warning | PASS | 마지막 컴파일 후 오류 0. 프로젝트 경고 0(MCP 패키지의 WebSocket 경고 1건은 외부, 2절 참고) |
| ALL-2 | MCP `run_tests` | PASS | EditMode 7/7, PlayMode 1/1(백그라운드라 `Step()` 수동 진행, 2절) |
| ALL-3 | 금지 문자열 grep(`01-architecture.md` 8절 목록) | PASS | `Assets/_Project/Scripts/Simulation` 위반 0. 자동 점검 도구는 CP1 범위 |
| ALL-4 | showcase 실행 중 콘솔 | 해당 없음 | CP0에는 showcase 시나리오가 없다(시나리오 러너는 CP1). 플레이 모드 테스트 실행 중 콘솔 오류 0 |
| ALL-5 | 커밋·태그·푸시 | PASS | `cp-0` 태그, `origin/main` 푸시 |
| ALL-6 | 기록 갱신 | PASS | 이 로그, PROGRESS.md, DECISIONS.md |
| CP0-1 | `ProjectSettings/ProjectVersion.txt` | PASS | `6000.3.23f1 (09d2ecc7fb28)` |
| CP0-2 | `execute_code` 조회 + 테스트 | PASS | 현재 파이프라인 `UniversalRenderPipelineAsset`(UniversalRP.asset), 기본 렌더러 `Renderer2DData`(Renderer2D.asset). `SmokeTests.ActivePipeline_IsUrpWith2DRenderer` |
| CP0-3 | MCP `run_tests` EditMode | PASS | `SmokeTests` 4개: `RuntimeAssemblies_AreCompiledWithExpectedNames`, `BuildScenes_AreMainThenSandbox`, `ActivePipeline_IsUrpWith2DRenderer`, `PlayerSettings_RunInBackgroundIsOn` |
| CP0-4 | 참조 검사 테스트 | PASS (2회차) | `SimulationAssemblyReferenceTests`: asmdef 설정, 컴파일 참조 목록, 실제 DLL 참조 목록 3단계. 1회차에 자동 참조 DLL 2개를 실제로 잡아냄 |
| CP0-5 | MCP `manage_camera` screenshot | PASS | `captures/CP0/main_scene_editmode.png` 1920×1080, 샘플 픽셀 전부 `#C9DCEB`(bgBase) |
| CP0-6 | `git status --ignored` | PASS | Unity 실행 후에도 `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`은 무시 목록에만 있음 |
| CP0-7 | `Packages/packages-lock.json` | PASS | 아래 표 |

### 설치된 패키지 (직접 의존, `com.unity.modules.*` 34개 제외)

| 패키지 | 버전 | 출처 |
|---|---|---|
| `com.coplaydev.unity-mcp` | v10.3.0 (aa5fc638d6) | git |
| `com.unity.2d.animation` | 13.0.6 | registry |
| `com.unity.2d.aseprite` | 3.0.2 | registry |
| `com.unity.2d.psdimporter` | 12.0.2 | registry |
| `com.unity.2d.sprite` | 1.0.0 | builtin |
| `com.unity.2d.spriteshape` | 13.0.0 | registry |
| `com.unity.2d.tilemap` | 1.0.0 | builtin |
| `com.unity.2d.tilemap.extras` | 6.0.3 | registry |
| `com.unity.2d.tooling` | 1.0.4 | registry |
| `com.unity.collab-proxy` | 2.13.6 | registry |
| `com.unity.ide.rider` | 3.0.40 | registry |
| `com.unity.ide.visualstudio` | 2.0.26 | registry |
| `com.unity.inputsystem` | 1.20.0 | registry |
| `com.unity.multiplayer.center` | 1.0.1 | builtin |
| `com.unity.render-pipelines.universal` | 17.3.0 | builtin |
| `com.unity.test-framework` | 1.6.0 | builtin |
| `com.unity.timeline` | 1.8.13 | registry |
| `com.unity.ugui` | 2.0.0 | builtin |
| `com.unity.visualscripting` | 1.9.12 | registry |

간접 의존: 2d.common 12.0.4, burst 1.8.30, collections 2.6.8, ext.nunit 2.0.5, mathematics 1.3.3, nuget.mono-cecil 1.11.6, nuget.newtonsoft-json 3.2.2, render-pipelines.core 17.3.0, render-pipelines.universal-config 17.0.3, searcher 4.9.5, shadergraph 17.3.0, test-framework.performance 3.5.0.

사전 승인 패키지(Input System, 2D Animation, 2D Sprite, Test Framework)는 모두 템플릿에 이미 들어 있어 새로 설치한 것은 없다.

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| `captures/CP0/main_scene_editmode.png` | Main 씬의 `Main Camera`(직교 크기 9) 렌더 결과. 해상도 1920×1080, 픽셀 색 분포 | 의도대로 bgBase 단색. 경기장·파이터는 아직 없음(CP2·CP3 범위) |

## 5. 자체 결정

- S-001 네임스페이스는 어셈블리 단위로만 두고, Editor 어셈블리 네임스페이스는 `Knockbox.EditorTools`
- S-002 Simulation asmdef `overrideReferences: true`
- S-003 Graphics Settings 기본 렌더 파이프라인도 URP로 지정
- S-004 씬은 `Assets/_Project/Scenes/`에 메뉴로 생성, SampleScene은 파일만 두고 빌드 목록에서 뺌
- S-005 빈 폴더는 `.gitkeep`으로 추적
- S-006 백그라운드 PlayMode 테스트는 `Step()` 수동 진행으로 실행(임시, Q-006 답 전까지)

## 6. 알려진 문제와 미룬 것

- **백그라운드 플레이 모드 정지**(Q-006 → D-015): 사용자 답에 따라 실시간 검증 동안 `Tools/focus-window.ps1 -Unity`로 Unity를 앞으로 가져오고 `-Restore <핸들>`로 되돌린다. 확인: 앞에 있을 때 5초 동안 약 1460프레임 진행, 포커스 복원 성공.
- 씬 생성기(`ProjectSetup.cs`)에 bgBase 색 `#C9DCEB`가 직접 들어 있다. CP3에서 `PaletteAsset`이 생기면 그 값을 읽도록 바꾼다(CP3-7 규칙 점검 대상은 연출 코드지만 일관성을 위해).
- 규칙 자동 점검 도구는 CP1 범위. CP0은 수동 grep으로 대신했다.
- 템플릿의 `Assets/Scenes/SampleScene.unity`는 그대로 두었다(빌드 목록에서만 뺌). 지우려면 사용자 확인 후.

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 7/7, PlayMode 1/1. Main 씬이 열려 있다.
- 재현: `Knockbox/Setup/Run All` 메뉴가 Run In Background, 기본 파이프라인, 없는 씬 생성, 빌드 목록을 맞춘다(여러 번 실행해도 같은 결과).
- 네임스페이스 주의: `Knockbox.Game` 아래에 `Debug`, `Input` 같은 하위 네임스페이스를 만들지 않는다(`UnityEngine.Debug`, `UnityEngine.Input`을 가린다). 커스텀 인스펙터는 `UnityEditor.Editor`를 완전한 이름으로 쓴다.
- Simulation asmdef는 `references`와 `precompiledReferences`가 비어 있어야 한다. 테스트가 이를 강제한다.
- MCP 도구가 기본 도구로 안 보이면 로컬 `http://127.0.0.1:8080/mcp`에 JSON-RPC로 직접 호출할 수 있다(세션 초기화 → `notifications/initialized` → `tools/call`).
- 다음 첫 작업: CP1 로그 생성, `Vec2`/`SimMath`/`Hasher`/`SimRng`/`InputFrame`부터.
