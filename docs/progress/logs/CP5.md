# CP5. 모드 A: 돌진 충돌

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | spec/03-combat.md 4절, spec/05-presentation.md 3·4절 |
| 태그 | cp-5 |

## 1. 계획

1. `CombatMode.Charge` 추가. 공격 버튼: 누르면 충전(`Charge` 상태, 지상 최대 속도 3, 공중 낙하 최대 4), 떼면 8방향 돌진(`Lunge`, 18→34 선형, 4틱 미만은 최소, 최대 충전 24틱 뒤 자동 발사).
2. 돌진: 중력 × 0.3, 속도 12 미만이나 30틱이면 종료, 지형에 부딪히면 반사 × 0.6(최대 2회).
3. 몸 충돌 판정: 이동 직후(밀어내기 전) 스냅숏에서 접촉과 법선을 구하고, 위력 = max(0, 속도·법선) × 가중치(돌진 1.0, 대시 0.6). 둘 다 6 미만 밀어내기, 둘 다 6 이상·차이 4 미만 클래시, 그 외 높은 쪽 승리. 같은 두 파이터 판정은 10틱에 한 번.
4. 승리 결과는 CP4의 패링·가드·명중 적용을 재사용(데미지 clamp(round(위력 × 0.6), 4, 24), 넉백 방향 혼합, 세기 max(10, 위력 × 0.9), 승자 속도 × -0.25). 패링이면 승자 속도를 법선 반사 × 0.8.
5. 연출: 충전 3단계(납작·떨림·외곽선 밝기·가는 눈), 돌진 잔상 길이 ∝ 위력, 위력 6 이상 hazard 외곽선, 팔(충전 때 뒤로, 돌진 때 앞으로), F3 위력 숫자. F1 모드 전환(테스트 ↔ 돌진).
6. 검증: CP5-1~7 EditMode, CP5-8 showcase 캡처, CP5-9 무작위 허수아비 1분 대전 기록, 이전 테스트 전체.

## 2. 작업 기록

- 2026-10-31 시뮬레이션: `ChargeModeParams`(명세 수치 24개 + 클래시 반동 비율), 행동 상태 `Charge`·`Lunge`, 파이터 필드 `LungeBounces`·`ContactCooldown`, 이벤트 `LungeStart`·`PowerCheck`. 판정 원 공격과 몸 충돌이 같은 적용(`ApplyStrike`: 패링·가드·명중)을 쓰도록 `FighterCombat`을 정리. 몸 충돌 판정·타격 값은 순수 함수(`JudgeBodies`, `BodyStrikeValues`)로 분리해 표로 시험. 기존 EditMode 179개 그대로 통과.
- 테스트 43개 추가(`ChargeModeTests`, 봇 대칭·SyncTest 3맵). 첫 실행에서 "스치는 돌진" 테스트의 배치가 법선 반대로 움직여 위력 계산 자체가 일어나지 않음 → 두 파이터가 함께 위로 날며 P1만 옆으로 다가가게 고침(테스트 배치 수정).
- 연출·도구: 충전 단계 외곽선·납작함·떨림·가는 눈, 돌진 잔상(위력 비례)·hazard 외곽선·팔 뻗기, 발사 먼지·소리, F3 위력 숫자, F1 모드 전환, Setup이 Main 씬을 모드 A로. showcase 4개(charge, clash, overlay, wallbounce), 각자 expect로 발사·명중·클래시·튕김 틱 고정.
- 캡처 확인 후 수정: 충전 중 앞팔이 얼굴을 가로질러 체크 표시처럼 보임 → 충전 때 두 팔을 몸 뒤에 그림. 돌진 중 뒷팔이 몸 앞을 가로지름 → 뒷팔은 몸 뒤로. F3 위력 숫자 두 개가 겹침 → 각자 상대 반대쪽 바깥에. 튕김이 먼지뿐이라 약함 → 작은 고리와 세기 비례 흔들림(CP4 벽꽝 캡처 2장이 이 때문에 달라짐, 의도한 변화).
- CP5-9: 1분 무작위 허수아비 대전은 오류 없이 진행됐지만 라운드가 끝나지 않음(체력 68/75). 10분 헤드리스로 보충 측정(S-061).

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 컴파일 후 `read_console` 오류·경고 | PASS | 오류 0, 경고 0 |
| ALL-2 | EditMode·PlayMode 전체 | PASS | EditMode 225/225, PlayMode 9/9 |
| ALL-3 | `Knockbox/Verify/Check Simulation Rules` + `SimulationRulesTests`·`AllocationTests` | PASS | "시뮬레이션 규칙 점검 통과", Step 할당 0 |
| ALL-4 | `Knockbox/Run Scenario Captures` 실행 중 콘솔 | PASS | 45장 저장, 오류·경고 0. `ShowcaseScenarios_RunWithoutErrors`·`Showcases_WithPresentation_MatchHeadlessChecksum`(showcase 13개) 통과 |
| CP5-1 | `LaunchSpeed_IsLinearInChargeRatio`(8), `FullCharge_AutoLaunchesAfter24MoreTicks`, `Launch_UsesEightWayInput_NeutralUsesFacing`, `Charging_LimitsSpeedAndFloatsInAir` | PASS | 1·3틱 → 18, 4/10/18/27/36틱 → 18 + 16 × 틱/36, 50틱 → 34. 60틱 누르고 있으면 61번째 틱에 34로 자동 발사. 대각선 정규화, 중립은 바라보는 쪽 |
| CP5-2 | `PowerJudgement`(10), `HeadOnLunges_SimilarPower_Clash`, `HeadOnLunges_StrongerWins`, `GlancingLunge_BelowThreshold_OnlyPushes` | PASS | 6 미만 둘 → 밀어내기, 20 대 16.1 → 클래시, 20 대 16 → 승리, 같은 위력 → 클래시(번호 우선 없음). 실제 충돌 20 대 18 클래시, 26 대 18 승리, 위력 5 스침은 밀어내기만 |
| CP5-3 | `Damage_IsClampedFourToTwentyFour`(5), `KnockbackDirection_MixesVelocityAndNormal`, `Winner_ReboundsAndStopsLunging`, `SamePair_JudgedOncePerTenTicks` | PASS | 위력 6.5 → 4, 30 → 18, 40·60 → 24. 방향 혼합 공식 일치, 세기 max(10, 위력 × 0.9). 위력 30 정면: 넉백 27, 이긴 쪽 -7.5, 돌진 종료, 10틱 동안 재판정 없음 |
| CP5-4 | `TerrainBounce_AtMostTwice_ThenEnds`, `Lunge_EndsBelowSpeed12`, `Lunge_EndsAfter30Ticks` | PASS | 폭 3 통로: 34 → 20.4 → 12.24로 두 번 튕기고 세 번째 접촉에서 종료. 수직 돌진은 30틱 전에 속도 12 미만으로 종료. 수평 지상 돌진은 정확히 30틱 |
| CP5-5 | `LungeIntoParry_ReflectsAndStaggers`, `LungeIntoGuard_FollowsGuardRules` | PASS | 패링: -16(반사 × 0.8), 비틀거림, 히트스탑 10, 피해 0. 가드: 데미지 2, 수평 넉백 6.3, 가드 경직, 히트스탑 4, 이긴 쪽 -5 |
| CP5-6 | `DashIntoStandingOpponent_Damages`, `OrdinaryMovement_NeverDamages`, `DashInTestMode_DoesNoDamage` | PASS | 대시(위력 15.6) → 데미지 9. 달리기·점프로 180틱 부딪혀도 피해 0. 테스트 모드의 대시는 피해 없음 |
| CP5-7 | `MirroredChargeFight_AndSyncTest`(Box, Towers, Pit), `SymmetryTests`(돌진 필드 추가) | PASS | 3600틱 봇 대전을 뒤집어도 매 틱 일치, SyncTest 불일치 0. Box: 돌진 62, 명중 24, 가드 24, 클래시 2, 벽꽝 3, KO 5 |
| CP5-8 | showcase 4개 캡처를 열어 확인(4절) | PASS | `docs/progress/captures/CP5/showcase_cp5_*.png` 14장 |
| CP5-9 | `DummyMatchTests.RandomDummies_OneMinuteChargeMatch`(연출 켬) + 10분 헤드리스 보충 | PASS | 1분: 콘솔 오류 0, 3600틱 모두 진행, 돌진 71, 명중 5, 클래시 0, 끝난 라운드 0(체력 68/75). 10분 무작위 허수아비: 라운드 2개(평균 215.8초), 클래시 분당 0.4. 10분 CombatBot: 라운드 32개(평균 15.7초), 클래시 분당 6.3, 경기 4번 끝남 |

측정값: Step 평균 0.84 µs, 롤백 8틱 7.31 µs(예산 0.25 ms / 2 ms). 1080p 렌더 텍스처 평균 4.72 ms(212 fps), 연출 할당 0 B/프레임. 실시간 10.0039초에 600틱. CP3·CP4 showcase를 다시 찍어 이전 캡처와 픽셀 비교: CP4 벽꽝 2장(튕김 연출 추가)만 다르고 나머지는 디버그 글자 줄을 빼면 차이 0.

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| showcase_cp5_charge_charge_stage1~3 | 외곽선 oneWay → spark → 흰색, 점점 납작해짐, 가는 눈, 뒤로 기울어짐 | 단계가 한눈에 구분됨. 앞팔이 얼굴을 가로질러 고친 뒤 다시 찍음 |
| showcase_cp5_charge_lunge_trail | 위력 34 돌진: 긴 잔상, hazard 외곽선, 앞팔을 진행 방향으로 뻗음 | 맞음 |
| showcase_cp5_charge_lunge_hit / wallsplat | 정면 명중(흰 번쩍임, 파편, 고리), 맞은 쪽이 가운데 기둥에 벽꽝 | 맞음 |
| showcase_cp5_clash_lunges / clash / clash_sparks / recoil | 같은 충전으로 마주 돌진, 별 폭발·흰 외곽선·불꽃, 양쪽 반동 | 맞음 |
| showcase_cp5_overlay_power_numbers | F3: 양쪽 위력 26.9가 각자 바깥쪽에 위험색으로, 몸 상자·속도 | 처음엔 숫자가 겹침 → 바깥쪽 배치로 고침 |
| showcase_cp5_wallbounce_lunge_up / wall_bounce / after_bounce | 왼쪽 위 대각선 돌진 → 벽에 튕겨 오른쪽 위로, 잔상이 V자 경로를 그림 | 맞음 |
| CP3·CP4 showcase 다시 찍음 | 회귀 비교 | CP4 벽꽝 2장만 의도한 차이(튕김 고리·흔들림) |

## 5. 자체 결정

- S-055 몸 충돌 접촉 측정 시점과 10틱 대기
- S-056 충전·돌진 세부
- S-057 클래시 반동 속도 max(8, 위력 × 0.35)
- S-058 몸 충돌 승리의 패링·가드·명중 적용
- S-059 모드 A 연출 수치
- S-060 F1 모드 순환, Main 씬 모드 A, 허수아비 시드
- S-061 CP5-9 측정 방법

## 6. 알려진 문제와 미룬 것

- 무작위 허수아비끼리는 거의 만나지 않아 1분 안에 라운드가 끝나지 않는다(평균 215.8초). 사람 대전의 라운드 길이는 CP9 플레이테스트에서 본다. 다가가 싸우는 봇은 평균 15.7초.
- 최대 충전 돌진은 30틱 동안 약 17유닛(경기장 폭의 절반)을 간다. 명세 수치 그대로이며 손맛은 CP9에서 확인.
- 봇 대전에서 패링이 거의 나오지 않는다(봇이 타이밍을 노리지 않음). 돌진 패링은 `LungeIntoParry` 테스트로 확인했다.
- 충전 효과음은 없다(05 7절 목록에 없음). 돌진 발사는 대시 소리를 쓴다.
- F3 위력 숫자는 몸과 일부 겹친다(디버그 전용).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 225/225, PlayMode 9/9, 규칙 점검 통과. Main 씬은 모드 A로 시작하고 F1로 테스트 모드와 오간다.
- 공격 버튼의 의미는 `FighterCombat.BeginTick`의 `config.Mode` 분기에 있다. 모드 B는 `CombatMode.Swing`을 더하고 방향별 `AttackDef` 3개를 `SimConfig.GetAttack`에 연결한 뒤, 같은 분기에서 입력 방향으로 공격 번호를 고른다. 판정 원·클래시·패링은 이미 `ResolveHitboxes`가 처리한다.
- 모드 B에서는 몸 충돌 가중치가 0이어야 한다(`FighterCombat.Aggression`이 모드 A에서만 0보다 큼).
- 대시 공격 캔슬(03 5절)은 `BeginTick`의 "대시 중이면 return" 앞에서 처리할 것.
- 다음 첫 작업: CP6 로그, `03-combat.md` 5절(모드 B), 방향별 휘두르기 `AttackDef` 3종과 판정 원 궤적부터.
