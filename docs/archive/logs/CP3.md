# CP3. 연출 기반

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | CHECKPOINTS.md CP3, spec/05-presentation.md |
| 태그 | cp-3 |

## 1. 계획

1. 정렬 레이어 9개, 에디터 `SpriteFactory`로 도형 스프라이트(둥근 사각형 9-슬라이스, 원, 캡슐, 삼각형, 고리) PNG 생성, `ShapeLibrary` 에셋.
2. 파이터: 둥근 몸 + 잉크 외곽선 + 평면 그림자, 눈(P1 원, P2 캡슐, 깜박임), 스프링 몸 변형과 기울기, 대시 잔상. 팔다리는 **해석적 2마디 IK**(명세가 허용한 대안, 자체 결정)로 다리 걷기·공중·벽, 팔 대기·달리기. 발 위치는 지형 상자로 직접 계산.
3. 지형(몸체·윗면 띠·그림자·발판·패드·가시 무늬)과 배경 실루엣, 카메라 프레이밍(16:9가 아니면 bgBase 레터박스), 먼지, 화면 흔들림(트라우마, 흔들림 줄이기), HUD 뼈대(머리 위 체력 바, 점수 칸, 월드 공간).
4. 연출 시간은 시뮬레이션 시간(틱 + 보간 비율)으로 진행 → 캡처 재현 가능. 캡처는 한 틱씩 그린다.
5. `Tools/sfx_synth.py`(표준 라이브러리, 고정 시드)로 점프·착지·대시 WAV, 이벤트로 재생(±5% 음높이, 같은 프레임 중복 제거).
6. 검증 CP3-1~8. 키보드 입력원(06 2절)도 붙여 사람이 Main 씬에서 움직여 볼 수 있게 한다.

## 2. 작업 기록

- Presentation: `ShapeLibrary`(+ `SortingLayers`), `TwoBoneIK`·`GroundQuery`·`FxRandom`, `LimbRig`, `FighterView`(새로 작성), `ArenaView`(새로 작성), `BackgroundView`, `DustPool`, `CameraRig`(프레이밍 + 흔들림), `SfxLibrary`, `SfxPlayer`.
- Game: `MatchPresenter`(시뮬레이션 시간 기반 연출 시계, 틱 이벤트 큐 → 몸 변형·먼지·잔상·흔들림·효과음), `HudView`(UI), `KeyboardInputSource`·`GamepadInputSource`(Input), `DebugHotkeys`(F5 리셋, F7 흔들림 줄이기, F8 SyncTest, P 일시정지, O 한 틱), MatchRunner `useLocalDevices`(Main 씬에서 켬). 캡처는 한 틱 진행마다 한 프레임을 그린다. 해상도별 캡처 메뉴 추가.
- Editor: `SpriteFactory`(`Knockbox/Art/Generate Sprites`, 정렬 레이어), `ProjectSetup`이 팔레트·도형·효과음 참조, 디버그 단축키, 카메라 리그까지 채운다.
- 효과음: `Tools/sfx_synth.py` → `Audio/Sfx/jump.wav`, `land.wav`, `dash.wav`. 두 번 생성해 해시 동일 확인.
- 문제와 해결(시도 순서대로):
  1. 삼각형 스프라이트가 거의 비어 있었다(변의 부호 거리 부호가 반대) → 부호를 고쳐 다시 생성, ASCII로 알파 확인.
  2. `SfxLibrary`(ScriptableObject)를 `SfxPlayer.cs` 안에 두었더니 에셋이 스크립트에 연결되지 않았다("No script asset for SfxLibrary") → 파일 분리, 잘못 만들어진 에셋 삭제 후 재생성. `BackgroundView`도 같은 이유로 분리.
  3. 첫 캡처에서 모든 스프라이트가 검게 나왔다. 씬의 Global Light 2D가 만들어질 때 있던 `Default` 정렬 레이어만 비추고, 스프라이트는 조명 재질이었다 → 모든 연출 스프라이트를 URP `Sprite-Unlit-Default`로(S-033).
  4. 캡처 확인: 앞팔 팔꿈치가 몸 안쪽으로 굽어 얼굴을 가로지름 → 팔꿈치는 바깥으로, 손 목표를 몸 밖으로.
  5. 캡처 확인: 착지 찌그러짐이 캡처 시점에 거의 복원돼 보이지 않음(스프링이 2프레임 만에 1로 돌아옴, 사건 프레임에도 바로 적분) → 강성 2000 → 900, 감쇠 22 → 21, 사건 프레임에는 찌그러진 모양을 그대로 보여 줌. 착지 캡처를 착지 틱(32)으로.
  6. 캡처 확인: 화면 맨 위(천장 바깥면)에 윗면 띠가 그려짐 → 경기장 바깥 테두리 윗면은 제외.
  7. CP3-3 1차 FAIL(디딘 발 오차 0.059): 빨리 달릴 때 디딘 발이 다리 길이를 넘어 IK가 발을 띄움 → 움직이는 중에는 뒤처진 발을 0.12에서 들고, 다리 길이의 96%를 넘으면 다른 발이 내딛는 중이어도 든다. 2차 0.0000.
  8. CP3-6 1차 FAIL: 내 요약 로그가 `LogAssert.NoUnexpectedReceived`에 걸림 → 검사 뒤에 로그.
  9. 캡처 확인: 벽 미끄러짐 때 몸 그림이 벽에 0.11 겹쳐 외곽선이 벽에 묻힘 → 벽에 닿으면 몸 그림만 0.11 떼어 그림. 대시 잔상이 뒷팔 위에 그려져 팔이 물듦 → 잔상을 LimbBack 레이어 가장 뒤로.
  10. 캡처 확인: 대시 중 발이 엉덩이보다 한참 뒤에 있을 때 앞으로 굽힌 무릎이 바닥 아래로 꺾임 → 무릎이 지면 아래로 가면 반대로 굽힘, 대시·벽 미끄러짐 중 다리는 몸 뒤에 그림. 무릎·발 깊이를 재는 PlayMode 테스트 추가(0.0000).

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 |
|---|---|---|---|
| ALL-1 | `read_console` | PASS | 오류 0, 프로젝트 경고 0 |
| ALL-2 | MCP `run_tests` | PASS | EditMode 123/123, PlayMode 8/8 |
| ALL-3 | `SimulationRulesTests` | PASS | 위반 0(이번 CP에 시뮬레이션 코드 변경 없음) |
| ALL-4 | `Showcases_WithPresentation_MatchHeadlessChecksum`, 캡처 실행 | PASS | 연출이 연결된 Sandbox에서 showcase 3개, expect 실패 0, 콘솔 오류 0 |
| ALL-5 | 커밋·태그·푸시 | PASS | `cp-3` |
| ALL-6 | 기록 갱신 | PASS | 이 로그, PROGRESS.md, DECISIONS.md |
| CP3-1 | showcase 캡처 + 05 8절 체크리스트 | PASS | 4절 |
| CP3-2 | `PresentationTests.TwoBodies_LuminanceDiffersByAtLeastQuarter` | PASS | 몸 중심 상대 휘도 P1 **0.549**, P2 **0.192**, 차이 **0.357**(≥ 0.25) |
| CP3-3 | `Running120Frames_FeetStayOnGround` | PASS (2회차) | 120프레임 달리기(왕복), 디딘 발 표본 232, 발과 지면 최대 오차 **0.0000**, 지면 아래 0. 1회차 0.059 FAIL → 걸음 수정 |
| CP3-4 | `Knockbox/Run Scenario Captures (Resolutions)` | PASS | `*_720p.png`, `*_1440p.png` 각 6장: 경기장과 HUD 위치·비율 유지. 추가로 4:3(`*_4x3.png`)에서 위아래 bgBase 레터박스, 경기장 전체 보임 |
| CP3-5 | `FrameTimeAndPresentationAllocations` | PASS | 1920×1080 렌더 텍스처에 300프레임: 평균 **4.89 ms(205 fps)**, 최대 8.96 ms. 연출 계층 할당(StepOnce + 연출 한 프레임 × 240) **0 B**. 에디터 전체 GC 프레임당 평균 약 16.7 KB(테스트 러너·MCP·에디터 포함, 연출 아님). 개발 PC: Windows 11, Unity 6000.3.23f1 에디터, DX12 |
| CP3-6 | `Showcases_WithPresentation_MatchHeadlessChecksum` | PASS | 연출을 켠 실행의 최종 체크섬 = 시뮬레이션만의 실행(showcase 3개) |
| CP3-7 | `PresentationRulesTests.PresentationCode_HasNoHardcodedColors` | PASS | Presentation, Game/UI, Game/Bootstrap에 색 리터럴 0(PaletteAsset, 흰 마스크 텍스처 ShapeSprites 제외). 검사 정규식의 자체 검사가 1회 실패(`Color32?`가 `new Color(`를 놓침) → 수정 |
| CP3-8 | `SfxSynth_RegeneratesIdenticalFiles` | PASS | 스크립트를 두 번 실행한 결과와 커밋된 WAV가 바이트 단위로 동일 |

## 4. 캡처 확인

`docs/progress/captures/CP3/`. 체크리스트(05 8절)로 확인했다. 확대가 필요한 곳은 잘라 키워 보았다.

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| `showcase_cp3_presentation_land.png` | 높은 곳(y 4)에서 떨어진 착지 틱: 몸 1.3 × 0.7 찌그러짐, 눈도 함께 눌림, 발밑 먼지 원 | 맞음 |
| `..._idle.png` | 두 발 디딤, 팔은 몸 옆, 평면 그림자, P1 원 눈 / P2 캡슐 눈 | 맞음 |
| `..._run.png` | 진행 방향 기울기(최대 8°), 다리 번갈아 내딛기, 팔은 몸 밖에서 흔듦 | 맞음 |
| `..._apex.png` | 정점: 팔 벌림, 다리 접힘 | 맞음 |
| `..._wallslide.png` | 몸이 벽에서 떨어져 외곽선이 보임, 다리는 몸 뒤에서 벽에 닿음 | 맞음(수정 후) |
| `..._dash.png` | 잔상 3개(투명도 40%에서 사라짐), 가로로 늘어난 몸, 뒷팔이 끌림, 다리가 얼굴을 가리지 않음 | 맞음(수정 후) |
| `*_720p`, `*_1440p`, `*_4x3` | 해상도별 배치·HUD·레터박스 | 맞음 |
| `showcase_cp1_*`, `showcase_cp2_*` | 이전 CP 장면 재캡처: 위치·상태는 이전 캡처와 같고, 그림만 임시 사각형에서 새 그림으로 바뀜 | 의도한 변화만 있음 |

체크리스트: 흑백 구분(휘도 차 0.357), 외곽선 끊김·겹침 없음, 팔다리가 몸에 붙어 있고 발이 지면을 뚫거나 뜨지 않음(테스트 0.0000), 지형(진한 잉크)·캐릭터(밝은 노랑·보라)·배경(옅은 파랑)이 서로 묻히지 않음, 1280×720과 2560×1440에서 깨지지 않음, 이전 캡처와 비교해 의도한 변화만 있음.

## 5. 자체 결정

S-030 ~ S-040 (DECISIONS.md): 해석적 IK, 연출 시간과 캡처 방식, 몸 그림 위치와 다리·팔 규칙, 무광 재질, 스프라이트 생성 규격, 몸 변형 스프링 값, 월드 공간 HUD, 카메라·흔들림·먼지 값, 키보드·게임패드 입력원과 디버그 단축키, 효과음 합성 값, CP3-5 측정 방식.

## 6. 알려진 문제와 미룬 것

- 체력이 아직 상태에 없어 체력 바는 늘 가득 차 있다(CP4).
- 효과음은 점프·착지·대시 3개만(나머지는 CP8). 실제로 소리를 들어 보지는 못했다(파일 존재·재현성·재생 호출만 확인).
- 디버그 글자는 4:3처럼 좁은 화면에서 가장자리가 잘릴 수 있다(디버그 전용).
- 게임패드는 연결된 장치가 없어 실제로 확인하지 못했다. 키보드 입력도 자동 테스트가 없다(장치 입력은 사람이 확인해야 함). Main 씬에서 WASD + Space(점프) + Left Shift(대시), P2는 방향키 + Numpad로 움직일 수 있다.
- 배경 실루엣 배치는 고정 임시값이다. 맵별 배경은 CP8에서 다시 본다.
- 씬에 남은 Global Light 2D는 무광 재질이라 영향이 없다. 지우지 않았다.

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 123/123, PlayMode 8/8.
- 연출은 `MatchPresenter.RenderFrame`에서 시뮬레이션 시간 dt로 진행한다. 새 연출을 붙일 때 `Time.deltaTime` 대신 이 dt를 쓰고, 난수는 `FxRandom`을 쓴다(캡처 재현성).
- 스프라이트는 `ShapeLibrary.Create/CreateSliced`로 만들면 무광 재질과 정렬 레이어가 맞춰진다. 색은 `PaletteAsset`에서만(CP3-7 테스트가 막는다).
- 타격 연출(CP4)은 `MatchPresenter.Consume`에 이벤트별로 붙이고, 흔들림은 `CameraRig.AddTrauma`, 번쩍임·색 반전은 `CameraRig.ReduceMotion`을 확인한다.
- 캡처: `capture.sh` 흐름(Unity 앞으로 → 메뉴 → 플레이 모드 종료 대기 → 되돌림). 해상도 확인은 `Knockbox/Run Scenario Captures (Resolutions)`.
- 다음 첫 작업: CP4 로그, `03-combat.md` 읽기, 체력·행동 상태·판정 원과 상호작용 판정 단계부터.
