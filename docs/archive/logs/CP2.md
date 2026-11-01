# CP2. 이동과 지형

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | CHECKPOINTS.md CP2, spec/02-movement.md, spec/04-arena.md 2~5절 |
| 태그 | cp-2 |

## 1. 계획

1. `ArenaDef`(지형 상자 목록, 시작 위치)와 `ArenaParser`(문자 격자 → 검증 → 같은 종류 칸 합치기), 맵 파일 Box·Towers·Pit.
2. `TerrainCollision`: X → Y 순서, 하위 단계 0.2 이하, 단방향 발판(직전 틱 발 위치 기준), 튕김 패드, 접지·벽 탐침 0.02, 경계 밖 보정.
3. `FighterMovement`: 02-movement.md 전체(가감속, 가변 점프, 정점 보정, 빠른 낙하, 공중 점프, 코요테, 버퍼, 벽 미끄러짐·벽 점프·입력 잠금·벽 코요테, 대시와 대시 점프 캔슬, 아래+점프 하강). 파이터끼리 밀어내기와 머리 위 서기는 두 파이터를 이전 상태에서 동시에 계산한다.
4. TempMovement·TempArena 제거, `SimWorld`가 `ArenaDef`를 받게 바꾸고 기록·시나리오·MatchRunner를 맞춘다.
5. 임시 그래픽(색 사각형, 지형 상자)과 시나리오 캡처(`Knockbox/Run Scenario Captures`), showcase 시나리오.
6. 검증 CP2-1~14와 CP1 테스트 회귀.

## 2. 작업 기록

- Simulation: `Data/ArenaDef`, `Data/ArenaParser`, `Systems/TerrainCollision`, `Systems/FighterMovement`(이동 두 단계 + `FighterPush`). `SimWorld.StepFighting` = 두 파이터 Integrate → 몸 겹침 해소 → 두 파이터 접촉 판정. `FighterState`에 이동 필드 19개 추가(모두 `Visit`에 포함, 체크섬 범위 테스트 통과). 이벤트 `AirJump`, `PadBounce`, `OutOfBounds` 추가. TempMovement·TempArena 삭제.
- 입력 기록 형식 v2: 맵 체크섬을 넣어 다른 맵으로 재생하면 거부.
- Game: `ArenaFiles`(디스크 읽기), MatchRunner에 맵 텍스트 목록과 `ResolveArena`, `MatchBegan` 이벤트, `RenderAlpha`(멈춤 = 1), OutOfBounds·이벤트 버퍼 넘침 경고 로그. `MatchPresenter`(상태 → 연출), `DebugStateLabels`(틱·단계·행동 글자, 캡처에 찍힘), `ScenarioPlayer.RunCaptures`(틱 직접 진행, 카메라를 1920×1080 sRGB 렌더 텍스처로 찍어 PNG).
- Presentation: `PaletteAsset`(색 토큰), `ShapeSprites`(실행 중 만든 흰 사각형), `ArenaView`, `FighterView`(임시: 색 사각형 + 바라보는 쪽 눈 하나).
- Editor: `ScenarioCaptureMenu`(`Knockbox/Run Scenario Captures`, 라벨은 PROGRESS.md의 현재 CP), `ProjectSetup`이 팔레트 에셋 생성, 맵 텍스트를 MatchRunner에 등록, MatchPresenter 추가까지 맞춘다(기존 오브젝트에는 빠진 것만 채움).
- CP1 시나리오 기대값 3개 갱신: 맵 시작 위치가 (∓12.5, -7.55)로 바뀜. `sample_walk_jump`의 P2 x 6 → 12.5, `showcase_cp1_mirror_run_jump`의 tick 41 x 범위 [0.9, 1.2] → [-5.6, -5.3] (계산: -12.5 + 41틱 동안 7.06 = -5.44, 실측 -5.4486).
- 테스트 설계 오류 2건 수정: 맵 검증 실패 케이스 중 "행 길이"는 길이가 그대로라 테두리 오류가 먼저 났고, "'1'이 둘 이상"은 바꾸는 문자열이 같았다.
- 기대 상자 수를 처음에 Towers 9, Pit 5로 잘못 셌다. 손으로 다시 세면 Towers 8(위·아래·양옆 벽, 탑 기둥 2, 탑 발 2), Pit 6 → 테스트 수정 후 실측과 일치.
- 캡처 1차 확인에서 문제 2개 발견 → 수정: (1) 벽 미끄러짐 후 오른쪽으로 대시하는데 눈이 왼쪽을 봄 → 대시를 시작하는 틱에는 가로 입력 쪽을 바라보도록(S-023), 테스트와 showcase expect 추가. (2) 화면 끝에서 디버그 글자가 잘림 → 가로 위치를 안쪽으로 제한.
- showcase 타이밍 조정: 처음 입력으로는 두 번째 점프가 벽에 닿기 전에 착지해 벽 미끄러짐이 없었다 → 헤드리스 실행으로 틱별 상태를 출력해 점프 시점을 80 → 102로 옮김.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | `read_console` | PASS | 오류 0, 프로젝트 경고 0 |
| ALL-2 | MCP `run_tests` | PASS | EditMode 119/119, PlayMode 3/3 |
| ALL-3 | `SimulationRulesTests` | PASS | 위반 0 |
| ALL-4 | `ShowcaseScenarios_RunWithoutErrors` + 캡처 실행 | PASS | showcase 2개 플레이 모드 재생, expect 실패 0, 콘솔 오류·경고 0, 플레이 모드 체크섬 = 헤드리스 |
| ALL-5 | 커밋·태그·푸시 | PASS | `cp-2` |
| ALL-6 | 기록 갱신 | PASS | 이 로그, PROGRESS.md, DECISIONS.md |
| CP2-1 | `ArenaTests` | PASS | 합친 상자 수: **Box 8**(단단 5, 단방향 3), **Towers 14**(단단 8, 단방향 5, 튕김 1), **Pit 11**(단단 6, 단방향 4, 가시 1). 검증 실패 6종(행 길이, 행 수, 시작 위치 중복·누락, 알 수 없는 문자, 테두리) 오류 문장 확인 |
| CP2-2 | `MovementTests.JumpApex_FullAndShort` | PASS | 최대 점프 정점 **3.885**(3.8~4.3), 짧은 점프 **1.648**(1.3~1.9) |
| CP2-3 | `Run_AccelerationAndDecelerationTicks` | PASS | 정지→최대 **6틱**(5~7), 최대→정지 **9틱**(7~10). 방향 전환 가속 배율 1.5 수식 일치 |
| CP2-4 | `Coyote_JumpAfterLeavingLedge` (공중 점프 0) | PASS | 벗어난 지 5틱 성공, 6틱 성공, 7틱 실패 |
| CP2-5 | `JumpBuffer_PressedSevenTicksBeforeLanding_JumpsOnLandingTick` | PASS | 착지 7틱 전 입력 → 착지 틱에 Land와 Jump 이벤트가 같은 틱. 10틱 전 입력은 실행 안 됨 |
| CP2-6 | `WallSlide_SpeedIsCapped`, `WallJump_IgnoresWallwardInputForSevenTicks`, `WallJumps_ClimbTwoWideShaft` | PASS | 미끄러짐 최저 vy -5.0. 벽 점프 후 1~7틱 벽 쪽 입력 무시(무입력과 vx 동일), 8틱째 반영. 폭 2 통로에서 벽 점프 11회로 **15.10** 상승 |
| CP2-7 | `GroundDash_DistanceAndNoVerticalMotion` 외 3개 | PASS | 지상 수평 대시 **3.467**(3.0~4.0), 대시 중 높이 변화 0, 종료 속도 14.3(26 × 0.55). 공중 대시 후 재대시 불가 → 착지로 회복, 공중 벽 접촉으로 회복. 대시 점프 캔슬 3틱 이후만, 수평 속도 26 유지 |
| CP2-8 | `TunnelingTests` | PASS | 이동기 단위: 속도 26·30·40 × 8방향 × 시작 50곳 = **1200가지**, 각 12틱. 종단: 대시 8방향 × 시작 125곳 = **1000가지**, 각 15틱. 두께 0.5 벽 통과 0 |
| CP2-9 | `OneWay_PassFromBelowLandFromAboveDropWithDownJump` | PASS | 아래에서 점프로 통과해 착지, 위에서 착지, 아래만 누르면 유지, 아래 + 점프는 점프 없이 하강해 바닥에 착지 |
| CP2-10 | `BouncePad_LaunchesAndRecovers`, `BoxPillar_ClearedByFullJumpOnly` | PASS | 패드 착지 시 vy = 30, 공중 점프·대시 회복. Box 기둥: 점프 시점 56가지 중 최대 점프 **56/56** 넘음(일부는 기둥 위에 올라섰다 넘어감), 짧은 점프 **0/56** |
| CP2-11 | `SymmetryTests.MirroredMatch_ProducesMirroredResult` | PASS | 시드 3개 × 1800틱, 매 틱 위치·속도·방향·행동·접지·벽·충전 반전 일치(1e-4). 정면 충돌과 머리 위 서기도 확인 |
| CP2-12 | `DeterminismTests`, `SyncTestTests`, `EachMap_DeterministicAndSyncTestClean` | PASS | 이동이 들어간 Box에서 CP1 결정론·SyncTest 전부 통과. 세 맵 각각 1800틱 두 번 실행 체크섬 동일, SyncTest(7틱) 불일치 0, 경기장 밖 0. Step 할당 0 유지 |
| CP2-13 | `Knockbox/Run Scenario Captures` → 열어서 확인 | PASS | 4절 표 |
| CP2-14 | 수치와 캡처로 자체 평가 | PASS | 아래 |

### CP2-14 손맛 자체 평가 (사용자 직접 평가는 CP9)

측정은 기본 튜닝, Box 맵, 헤드리스 실행.

| 질문 | 근거 수치 | 판단 |
|---|---|---|
| 달리다 멈출 때와 방향을 틀 때 미끄러운 느낌이 없는가 | 최대 속도에서 정지 9틱(0.15초), 미끄러지는 거리 0.67(몸 0.75개). 반대 방향 최대 속도까지 10틱(0.17초) | 미끄럽지 않다. 0.67은 관성이 보일 정도의 짧은 거리라 축 1(탄력)에 맞는다. 정밀한 발판 끝 정지가 어렵다면 CP9 피드백으로 감속 80 → 100을 검토 |
| 짧은 점프와 긴 점프가 의도대로 구분되는가 | 짧게 1.65, 끝까지 3.89(2.4배). 체공 0.7초(42틱). 높이 3 기둥을 최대 점프로는 56/56, 짧은 점프로는 0/56 | 뚜렷이 구분된다. 기둥이 두 점프의 차이를 지형으로 확인시켜 준다 |
| 벽 점프를 연속으로 해서 좁은 수직 통로를 오를 수 있는가 | 폭 2 통로에서 벽 점프 11회로 15.1 상승. 입력 잠금 7틱 동안에도 반대쪽 입력은 그대로 먹힘 | 오를 수 있다. 다만 사람 손으로는 잠금 7틱과 벽 코요테 5틱의 박자를 익혀야 하므로 CP9에서 확인 |
| 대시 후 남는 관성이 "탄력"으로 느껴지는가, 조작을 방해하는가 | 지상 대시 3.47 + 무입력 미끄러짐 1.16 = 총 4.63(19틱). 같은 방향 입력이면 3틱 만에 최대 속도로 돌아옴. 공중 대시는 공중 감속 25 때문에 착지 때까지 vx 10.1이 남음 | 지상은 탄력 쪽(짧게 미끄러지고 금방 정상 속도). 공중은 관성이 크게 남아 정밀 조작을 방해할 수 있다. 공중 대시 후 반대 입력은 방향 전환 가속(70 × 1.5)으로 0.1초 안에 꺾이므로 막히지는 않는다. CP9 플레이 질문 목록에 넣는다 |

## 4. 캡처 확인

`docs/progress/captures/CP2/`. 모든 장면에서 P1(노랑)과 P2(보라)가 좌우 대칭 위치에 있고, 위치를 픽셀로 환산하면 헤드리스 상태와 맞는다(1유닛 = 60px).

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| `showcase_cp2_movement_idle.png` | tick 8, 두 파이터 시작 위치(x = ∓12.5)에서 서로 바라봄, Box 지형(가운데 기둥, 단방향 발판 3개, 벽) | 맞음 |
| `showcase_cp2_movement_run.png` | tick 20, P1 x -11.13(292px), 진행 방향을 바라봄, Run | 맞음 |
| `showcase_cp2_movement_apex.png` | tick 48, P1 (-6.24, -3.66) → (585px, 760px), 정점 부근 Airborne | 맞음 |
| `showcase_cp2_movement_wallslide.png` | tick 135, 두 파이터가 양쪽 벽에 붙어 WallSlide, 벽 쪽을 바라봄 | 맞음(1차 캡처에서 글자 잘림 → 수정 후 재캡처) |
| `showcase_cp2_movement_dash.png` | tick 194, P1 x -12.82(191px) 오른쪽 대시, 오른쪽을 바라봄 | 맞음(1차 캡처에서 반대쪽을 봄 → 수정 후 재캡처) |
| `showcase_cp1_mirror_run_jump_*.png` | CP1 시나리오의 회귀 캡처(start, apex, end) | 맞음 |

공통 관찰: 몸 그림은 1.0, 충돌 상자는 0.9라 바닥·벽에 0.05(3px)씩 겹쳐 보인다. 명세가 의도한 크기 차이이며 CP3 그림에서 다룬다.

## 5. 자체 결정

S-016 ~ S-029 (DECISIONS.md). 시작 위치 기준, 반 칸 지형 합치기와 가시 임시 처리, 빠른 낙하 배율 해석, 조기 해제 적용 범위, 최대 속도 초과 관성, 점프 우선순위와 착지 틱 버퍼, 코요테 계산, 대시 세부, 몸 충돌, 벽 판정 대상, 경계 밖 처리, 임시 그래픽과 팔레트 선도입, 캡처 방식, 기록 형식 v2.

## 6. 알려진 문제와 미룬 것

- 맵 도달 가능성 검증(04-arena.md 5절 "모든 발판에 도달")은 CP8-1 범위라 미뤘다. Box는 기둥 위 → 아래쪽 발판 → 위쪽 발판 경로를 계산으로만 확인했다(점프 3.9 + 공중 점프 약 2.8).
- 가시(`^`)는 지금 단단한 지형처럼 막기만 한다. 데미지·튕김·면역은 CP8.
- 키보드·게임패드 입력이 아직 없어 사람이 직접 조작할 수 없다. 입력 배치(06 2절)는 CP3 HUD·연출 작업과 함께 붙이거나, 늦어도 CP4 디버그 단축키와 같이 넣는다.
- 몸 그림(1.0)과 충돌 상자(0.9)의 차이로 지형에 살짝 겹쳐 보인다(CP3).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 119/119, PlayMode 3/3. Step 평균 0.66µs, 롤백 8틱 4.9µs.
- 이동 코드는 `FighterMovement.Integrate`(입력·속도·지형 이동)와 `ResolveContacts`(접지·벽·착지·행동)로 나뉜다. 전투(CP4)는 그 사이(`FighterPush` 자리)의 상호작용 단계에 판정을 넣으면 된다.
- 테스트용 맵은 `MovementTests.Build`(테두리 + 칸 함수) 또는 `TestWorlds.Grid`(문자열)로 만든다. 시작 위치는 `TestWorlds.CreateAt`으로 직접 준다.
- 캡처: `Tools/focus-window.ps1 -Unity` → 메뉴 `Knockbox/Run Scenario Captures` → 플레이 모드가 끝날 때까지 대기 → `-Restore`. 모든 `showcase_*`를 다시 찍는다.
- 헤드리스로 틱별 상태를 보려면 MCP `execute_code`(C# 6 컴파일러라 `in` 인수를 `ref`로 넘겨야 함)로 `ScenarioInputs`·`SimWorld`를 직접 돌린다.
- 다음 첫 작업: CP3 로그, `05-presentation.md` 전체 읽기, `SpriteFactory`(에디터)와 정렬 레이어부터.
