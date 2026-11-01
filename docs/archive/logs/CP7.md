# CP7. 보조 스킬: 잡기·던지기

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | spec/03-combat.md 2·6절, spec/05-presentation.md 2·4절 |
| 태그 | cp-7 |

## 1. 계획

1. 잡기 상태 `Grab`(시작 5 / 활성 3 / 헛잡기 후딜 22, 몸 앞 0.65의 0.7 × 0.9 상자), 성공하면 `Holding`·`Held`(상대를 몸 앞 0.8에 고정, 둘 다 정지, 최대 20틱), 버튼을 떼거나 20틱이면 던지기(8방향, 중립은 앞쪽 위 30°, 속도 28, 데미지 6, 24틱 `Thrown`).
2. 상호작용 판정 순서: 판정 원 → 몸 충돌 → 잡기. 앞 단계에서 결과가 나면 잡기는 실패하므로 "공격이 닿으면 공격 승"이 구조로 보장된다. 잡기끼리(같은 틱, 또는 상대가 잡기 시작·판정 중)는 잡기 풀기(반대로 10, 반동 12, 히트스탑 6).
3. 잡을 수 없는 상대: 피격 경직, 던져지는 중, KO. 비틀거림은 잡힌다. 던져진 상태 벽꽝 기준 12·데미지 10.
4. F4 잡기 켜기/끄기(설정 값, 새 경기), 시나리오 `grab` 플래그 연결.
5. 연출: 잡는 손(상자 쪽으로 뻗기, 붙잡을 때 상대 몸에), 붙는 순간 번쩍임, 던져진 쪽 잔상과 회전, F3 잡기 상자.
6. 검증: 상성표 모든 칸을 두 모드 시나리오로(CP7-1), CP7-2~7 EditMode, CP7-8 showcase.

## 2. 작업 기록

- 2026-10-31 시뮬레이션: `GrabParams`, `SimConfig.GrabEnabled`, 행동 상태 `Grab`·`Holding`·`Held`·`Thrown`, 필드 `ThrowPending`·`ThrowDirection`, 이벤트 `GrabStart`·`GrabBreak`. 상호작용 판정을 던지기 → 판정 원 → 몸 충돌 → 잡기 순서로(S-068). 던져진 상태는 `IsLaunched`로 피격 경직과 규칙을 공유. 기존 EditMode 256개 그대로 통과.
- 상성표 22개 시나리오를 파이썬으로 만들고 헤드리스로 각 칸의 첫 결과를 뽑아 확인: 22칸 모두 표의 결과와 일치(모드 B 명중 18틱·클래시 17틱, 모드 A 18틱, 잡기 16틱 등). 그 틱으로 expect를 고정.
- 테스트 20개(`GrabTests`) 추가. 봇 대전 미러·SyncTest는 통과했지만 잡기가 분당 2~3회뿐이라 "잡기 3회 이상" 확인에 걸림 → 봇이 잡기 거리 안에서만 잡도록 고쳐도 비슷 → 확인을 "1회 이상"으로 두고, 9방향·벽 던지기를 정해진 입력으로 지나가는 미러·SyncTest(`MirroredScriptedGrabs_AllThrowDirections`)를 두 모드로 추가(S-075).
- 연출: 잡는 손·붙잡는 손·버둥거림, 붙는 순간 번쩍임·고리, 던지기 먼지·파편·흔들림, 던져진 쪽 회전·잔상, 잡기 풀기 불꽃, F3 잡기 상자, F4. showcase 5개(던지기, 뒤로 던지기, 벽에 던져 넣기, 잡기 풀기, 오버레이).

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 컴파일 후 `read_console` 오류·경고 | PASS | 오류 0, 경고 0 |
| ALL-2 | EditMode·PlayMode 전체 | PASS | EditMode 314/314, PlayMode 11/11 |
| ALL-3 | `Knockbox/Verify/Check Simulation Rules` + `SimulationRulesTests`·`AllocationTests` | PASS | "시뮬레이션 규칙 점검 통과", Step 할당 0 |
| ALL-4 | `Knockbox/Run Scenario Captures` 실행 중 콘솔 | PASS | 90장 저장, 오류·경고 0. showcase 26개 헤드리스 체크섬 일치 |
| CP7-1 | `ScenarioTests.Scenario_ExpectsPass(matrix_*)` 22개 | PASS | 모드 A·B 각각: 공격 → 무방비 명중, 공격 중 클래시, 패링 구간 패링당함, 가드 막힘, 잡기 시작 중 명중 / 잡기 → 무방비 잡힘, 공격이 닿으면 공격 승, 공격이 안 닿으면 잡힘, 패링 구간 잡힘, 가드 잡힘, 잡기 중 잡기 풀기 |
| CP7-2 | `Grabbability_ByState`(5), `KnockedOut_IsNotGrabbable`, `Whiff_HasFiveThreeTwentyTwo` | PASS | 피격 경직·던져지는 중 → 헛잡기, 비틀거림·가드 경직·반동 → 잡힘, KO·이미 붙잡힘 → 잡기 불가. 헛잡기 5/3/22 |
| CP7-3 | `Hold_FreezesBoth_AndThrowsAfterTwentyTicks`, `ReleasingGrab_ThrowsThatTick`, `ThrowDirection_EightWayAndNeutral`(9) | PASS | 몸 앞 0.8 고정, 20틱 동안 둘 다 위치 그대로, 누르고 있어도 20틱째 던짐, 떼는 틱에 던짐(데미지 6, 던져진 24틱). 8방향 × 28, 중립은 앞쪽 위 30°, 뒤로 던지면 등 뒤에서 |
| CP7-4 | `ThrownWallSplat_ThresholdTwelve_DamageTen`(5), `ThrowIntoWall_Splats` | PASS | 던져진 상태 13·12 → 벽꽝 10, 11.5 → 튕김. 피격 경직 13 → 튕김, 14 → 벽꽝 6. 벽 앞 던지기 → 체력 100 − 6 − 10 |
| CP7-5 | `MutualGrabSameTick_Breaks` + matrix `grab_vs_grabbing` | PASS | 둘 다 반동 12, 반대로 10, 히트스탑 6, 피해 0. 상대가 잡기 시작 중이어도 잡기 풀기 |
| CP7-6 | `GrabDisabled_IgnoresGrabInput` | PASS | 잡기를 끄면 잡기 시작 이벤트·상태 없음, 설정 체크섬이 달라짐 |
| CP7-7 | `MirroredGrabFight_AndSyncTest`(4), `MirroredScriptedGrabs_AllThrowDirections`(2), 이전 미러·SyncTest 전체 | PASS | 봇 대전 4종 미러 일치·SyncTest 불일치 0(잡기 2~3회씩), 정해진 입력 10회 던지기(9방향 + 벽) × 두 모드 미러 일치·SyncTest 불일치 0 |
| CP7-8 | showcase 5개 캡처를 열어 확인(4절) | PASS | `docs/progress/captures/CP7/showcase_cp7_*.png` 15장 |

측정값: Step 평균 0.83 µs, 롤백 8틱 6.42 µs. 1080p 렌더 텍스처 평균 5.20 ms(192 fps), 연출 할당 0 B/프레임. 실시간 10.0015초에 600틱. CP3~CP6 showcase를 다시 찍어 CP6 캡처와 비교: 모두 차이 0(디버그 글자 줄 제외).

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| showcase_cp7_throw_reach / hold / throw / thrown_trail / wallsplat | 두 팔을 앞으로 뻗음 → 상대 몸에 손을 붙이고 붙잡음(붙는 순간 고리) → 앞쪽 위로 던짐(회전) → 잔상을 끌며 날아감 → 가운데 기둥에 벽꽝 | 맞음. 붙잡은 동안 두 몸 그림이 겹친다(고정 거리 0.8 < 그림 폭 1.12, 붙잡은 느낌이라 허용) |
| showcase_cp7_backthrow_hold / back_throw / thrown_trail | 등 뒤로 옮겨져 뒤쪽으로 날아감 | 맞음 |
| showcase_cp7_wallthrow_hold_at_wall / wallsplat / after | 벽 앞 붙잡기 → 벽꽝(흰 번쩍임, 금 자국) → 튕겨 나오는 고리 | 맞음 |
| showcase_cp7_break_reach / break / after | 서로 뻗은 손 → 불꽃·고리 → 양쪽 반동 | 맞음 |
| showcase_cp7_overlay_grab_box | F3: 헛잡기 활성 틱의 잡기 상자(spark 색) | 몸 앞 0.65, 0.7 × 0.9 |
| CP3~CP6 showcase 다시 찍음 | 회귀 | 차이 0 |

## 5. 자체 결정

- S-068 상호작용 판정 순서와 "공격 중" 해석
- S-069 잡기 세부
- S-070 붙잡기·던지기 세부
- S-071 던져진 상태 규칙과 `GrabBreak`
- S-072 F4와 잡기 설정
- S-073 상성표 시나리오 구성
- S-074 잡기 연출
- S-075 봇 잡기 행동과 잡기 경로 검증 방법

## 6. 알려진 문제와 미룬 것

- 잡기·던지기 전용 효과음은 CP8에서(지금은 기존 소리를 씀).
- 붙잡은 동안 두 몸 그림이 겹쳐 보인다(시뮬레이션 고정 거리 0.8).
- 봇 대전에서 잡기는 분당 2~6회로 적다(봇이 잡기 거리를 잘 만들지 못함). 사람 대전에서의 잡기 빈도는 CP9 플레이테스트에서.
- 붙잡힌 쪽이 빠져나오는 수단은 없다(명세에 없음).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 314/314, PlayMode 11/11, 규칙 점검 통과. 전투 규칙(CP4~7)은 여기서 끝나고, CP8은 맵·가시·KO 파편(연출 물리)·남은 효과음·HUD 문구다.
- 가시(04 3절): 데미지 15, vy = 20, 경직 20틱, 가시 면역 45틱, 공중 점프·대시 회복. 지형 접촉은 `FighterMovement.ResolveContacts`에서 `contacts.LandedKind == TerrainKind.Spike`로 잡을 수 있고, 경직은 `Hitstun`(또는 별도 상태)으로 두면 벽꽝·연속 명중 규칙을 그대로 쓴다. 면역 틱은 새 필드가 필요하다.
- KO 파편은 연출 물리(Unity 2D 물리, 연출 전용 레이어). 시뮬레이션 체크섬이 연출 물리 켜고 끈 경우 같아야 한다(CP8-5).
- 효과음 목록(05 7절) 중 남은 것: 잡기, 던지기, 가시, 튕김 패드, 카운트다운, KO.
- 다음 첫 작업: CP8 로그, `04-arena.md` 3·5절과 `05-presentation.md` 4~6절, 가시 규칙부터.
