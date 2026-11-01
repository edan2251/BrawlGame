# CP4. 전투 공통 기반

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | spec/03-combat.md 3절, spec/05-presentation.md 4절, spec/06-match-and-tools.md 1·3·4절 |
| 태그 | cp-4 |

## 1. 계획

1. 시뮬레이션: `CombatParams`·`AttackDef` 설정, 파이터 전투 필드(체력, 히트스탑, 경직, 연속 명중, 패링 구간 등)와 행동 상태(Attack, Guard, Hitstun, GuardStun, Stagger, Recoil, KO).
2. 한 틱 순서: 히트스탑 중이면 멈춤(입력 버퍼만 기록) → 행동 전환 → 이동·지형(경직 중 벽꽝·튕김) → 몸 밀어내기 → **상호작용 판정**(이동 후 스냅숏 하나로 양쪽 결과를 계산해 동시에 적용) → KO 확인 → 경기 흐름.
3. 경기 흐름 전체: Countdown → Fighting → KO(60) → RoundEnd → RoundTransition(훅, 설정 교체 지점) → Countdown, 또는 MatchEnd(아무 버튼으로 새 경기).
4. 모드별 기술 대신 테스트용 공격 하나(`AttackDef` 데이터)를 공격 버튼에 붙인다. 모드 A/B는 CP5/CP6에서 같은 틀에 얹는다.
5. Game: `CombatTuning`, 허수아비 입력원, F3 판정 오버레이, F6·`[` `]` 단축키.
6. 연출: 명중·가드·패링·클래시·벽꽝 연출(번쩍임, 떨림, 파편, 고리, 흔들림, 확대, 금 자국)과 효과음, 히트스탑 중 보간 금지, 체력 바 잔상.
7. 검증: CP4-1~7 EditMode 테스트, CP4-8 showcase 캡처, 이전 테스트 전체 재실행.

## 2. 작업 기록

- 2026-10-31 시뮬레이션 전투 규칙 1차: `CombatParams`·`AttackDef`(테스트 공격 TestJab: 5/4/14, 데미지 12, 넉백 14 앞쪽 위 30°, 판정 원 2개), 파이터 전투 필드 9개, 행동 상태 7개 추가, `FighterCombat`(행동 전환, 히트스탑 멈춤, 상호작용 판정), 경직 중 벽꽝·튕김, KO와 경기 흐름 전체. 기존 EditMode 123개는 그대로 통과(무작위 입력에 공격·가드가 섞여도 결정론·대칭 유지).
- CP4 테스트 43개 추가(`CombatTests`, `MatchFlowTests`, `CombatSymmetryTests`, 봇 `CombatBot`). 첫 실행에서 봇이 Box 가운데 기둥 앞에서 짧은 점프만 해서 맞붙지 못함 → 봇이 점프를 길게 누르게 고침(테스트 도구 수정, 규칙 변경 없음).
- 연속 명중 측정: 테스트 공격은 같은 공격 반복으로 연속 명중이 나오지 않음(최대 1). 넉백 24 수평 변형은 벽 앞에서 상한 3에 걸림, 상한 없이 감쇠만 두면 5까지 이어짐 → 연속 명중 상한 3을 규칙으로 둠(S-041).
- Game·연출: `CombatTuning` 에셋(Setup 메뉴가 생성·연결), 허수아비 6종, F3 판정 오버레이(몸 상자·판정 원·속도·패링 구간), F6·`[` `]`, 디버그 글자에 경직·히트스탑 숫자. `ImpactFx`(파편·불꽃·고리·별·금 자국·화면 번쩍임), 카메라 확대, 파이터 번쩍임·떨림·회전·찡그린 눈·주먹·가드 팔, 체력 바, 효과음 6종.
- 시나리오 `"tuning"` 덮어쓰기와 `"overlay"` 추가(S-049). showcase 6개(hit, overlay, parry, clash, wallsplat, ko), 각자 expect로 판정 틱을 고정.
- PlayMode 첫 실행 실패 3건과 조치: ① 라운드 사이 튜닝 재적용이 시나리오의 덮어쓰기를 지움(KO 시나리오 다음 라운드 체력 100) → 튜닝 에셋으로 시작한 경기만 재적용. ② 헤드리스 비교가 덮어쓰기 없이 설정을 만듦 → `ScenarioConfig.Create(def)` 사용. ③ 실시간 600틱 테스트가 579틱 — Unity를 앞으로 가져오지 못함(데스크톱 앱 안의 포그라운드 잠금) → `focus-window.ps1` 대체 경로(S-054).
- 캡처 확인 후 수정 3건: 패링 화면 반전의 첫 프레임(잉크)이 캡처에 흰색으로 찍힘(시간이 흐르지 않은 프레임에도 번쩍임이 진행) → dt가 0이면 진행하지 않게. 라운드가 바뀐 직후 발이 이전 위치에서 끌려옴 → 라운드 번호가 바뀌면 보간 없이 자세 초기화. 붙어 있을 때 디버그 글자가 겹침 → 오른쪽 파이터 글자를 한 줄 위로.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 컴파일 후 `read_console` 오류·경고 | PASS | 오류 0, 경고 0 (MCP 연결 로그 제외) |
| ALL-2 | EditMode·PlayMode 전체 | PASS | EditMode 179/179, PlayMode 8/8 (이전 CP 테스트 포함) |
| ALL-3 | `Knockbox/Verify/Check Simulation Rules` + `SimulationRulesTests` | PASS | "시뮬레이션 규칙 점검 통과", 할당 테스트(`AllocationTests`) 통과 |
| ALL-4 | `Knockbox/Run Scenario Captures` 실행 중 콘솔 | PASS | 31장 저장, 오류·경고 0. PlayMode `ShowcaseScenarios_RunWithoutErrors`(expect 포함) 통과 |
| CP4-1 | `CombatTests.Hitstop_FreezesBothFighters_AndKeepsInputBuffer`, `PressDuringParryHitstop_ActsOnFirstTickAfter` | PASS | 히트스탑 7틱 동안 양쪽 위치·속도·행동·행동 틱·경직 그대로, 점프 버퍼 7 유지, 히트스탑 중 누른 버튼 보관 → 끝난 첫 틱에 패링한 쪽이 바로 공격 |
| CP4-2 | `Knockback_ReplacesVelocity`, `HitstunFormula`(5), `ComboDecay_InWorld`(3), `ComboCap_FourthHitDoesNotLand`, `SameAttackRepeated_NeverHitsFourTimesInARow` | PASS | 달려오던 속도와 무관하게 (12.124, 7.0). 경직 15 / 8 / 45 / 10 / 7. 같은 공격 반복 150가지 × 2종: 최대 연속 1, 3 (상한 없이 감쇠만이면 1, 5 → S-041) |
| CP4-3 | `WallSplatOrBounce_Wall`(4), `_Floor`(2), `_Ceiling`(2), `WallSplat_OnlyWhileInHitstun` | PASS | 벽 14.0 → 벽꽝(데미지 6, × 0.45, 경직 +18, 히트스탑 6), 13.9 → 튕김(× 0.35). 바닥 16.2/12.7, 천장 14.9/11.9 분기 맞음, 양쪽 벽 대칭 |
| CP4-4 | `ParryWindow_IsExactlySixTicks`, `ParryCooldown_24TicksSincePreviousGuardPress`(3), `GuardHit_ReducesDamageAndKnockback`, `Parry_StaggersAttacker`, `Guard_LimitsSpeedAndBlocksJumpDashAttack` | PASS | 누른 틱 포함 1~6번째 틱 패링, 7~9번째 가드. 간격 23 → 가드, 24·40 → 패링. 가드: 데미지 2, 수평 넉백 4.24, 가드 경직 10, 히트스탑 4. 패링: 비틀거림 28, -8, 히트스탑 10 |
| CP4-5 | `MatchFlowTests` 5개 | PASS | 동시 벽꽝 KO → 무승부(점수 0:0), RoundEnd·RoundTransition 각 1틱 뒤 2라운드 Countdown. 5점 → MatchEnd(RoundTransition 없음), 60틱 전 버튼 무시, 뒤에는 새 경기. `ReplaceConfig`는 RoundTransition에서만. 봇 경기 5라운드 3:2로 MatchEnd |
| CP4-6 | `CombatSymmetryTests.MirroredFight_ProducesMirroredResult`(Box ×2, Towers, Pit), `MirroredWallSplat`, `CombatTests.SimultaneousAttacks_Clash`, `SymmetryTests`(전투 필드 비교 추가) | PASS | 3600틱 봇 대전을 뒤집어도 매 틱 일치(체력·히트스탑·경직·연속·패링·공격 포함). 예: Box 시드 1 명중 13, 가드 4, 패링 1, 클래시 3, KO 2 |
| CP4-7 | `CombatSymmetryTests.SyncTest_WithCombat_NoMismatch`(3맵), 기존 `SyncTestTests`·`EachMap_...` | PASS | 3600틱 되감기 7, 불일치 0. Box 명중 16·가드 7·패링 3·클래시 5·KO 3·라운드 전환 2 |
| CP4-8 | showcase 6개 캡처를 열어 확인(4절) | PASS | `docs/progress/captures/CP4/showcase_cp4_*.png` 17장 |

측정값: Step 평균 0.69 µs, 롤백 8틱 5.89 µs(예산 0.25 ms / 2 ms). 1080p 렌더 텍스처 평균 4.63 ms(216 fps), 연출 할당 0 B/프레임. 실시간 10.0007초에 600틱. CP3 showcase 6장을 다시 찍어 CP3 캡처와 픽셀 비교: 디버그 글자 줄을 빼면 차이 0.

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| showcase_cp4_hit_windup | 시작 동안 앞손을 젖힘, 상대 Idle | 문제 없음. 붙어 있어 두 팔이 가운데서 겹쳐 보인다(허용) |
| showcase_cp4_hit_hit_hitstop | 히트스탑 중 양쪽 흰색 번쩍임, 공격자 색 삼각 파편, 흰 고리, 맞은 쪽 찡그린 눈, 체력 바 흰 잔상 | 맞음. 글자 겹침은 고쳐서 다시 찍음 |
| showcase_cp4_hit_hitstun_spin | 날아가며 구르듯 회전, 파편이 흩어짐, 공격자 후딜 | 맞음 |
| showcase_cp4_overlay_hit_circles | F3: 몸 상자(잉크), 판정 원 2개(빨강)가 상대 몸 상자와 겹침, 속도 벡터 | 판정 원과 주먹 위치 일치 |
| showcase_cp4_parry_parry_invert | 화면 잉크 반전 프레임, 패링한 쪽 spark 외곽선 | 처음엔 흰색으로 찍힘 → 수정 후 잉크 반전 확인 |
| showcase_cp4_parry_parry_ring | 보라 고리 + 흰 고리, 공격자 비틀거림(찡그린 눈, 기울기), 가드 팔 | 맞음 |
| showcase_cp4_parry_stagger / punish | 비틀거리는 P1을 P2가 반격해 명중 | 맞음 |
| showcase_cp4_clash_clash / clash_sparks / recoil | spark 빛살 별, 흰 외곽선, 불꽃 줄기, 양쪽 반동 | 맞음. 불꽃 줄기가 몸 위를 지나가지만 2~3프레임이라 허용 |
| showcase_cp4_wallsplat_hit / wallsplat / crack | 벽에 납작하게 붙은 흰 몸, 파편, 확대(왼쪽 벽 가장자리 60→45px), 벽의 금 자국, 바닥 먼지 | 맞음 |
| showcase_cp4_ko_ko_hit / ko_after | 큰 흰 고리, 두 색 파편, 진 쪽 그늘색·찡그린 눈, 체력 바 비움 | 맞음(조각나는 연출은 CP8) |
| showcase_cp4_ko_next_round | 2라운드 Countdown, P1 점수 칸 1개, 시작 위치·체력 복구 | 처음엔 발이 끌려옴 → 수정 후 정상 |
| showcase_cp1/cp2/cp3 다시 찍음 | 이전 CP 장면 회귀 | CP3 캡처와 픽셀 차이 0(디버그 글자 제외) |

## 5. 자체 결정

- S-041 연속 명중 상한 3
- S-042 테스트 공격 TestJab 수치와 판정 원
- S-043 동시 명중은 클래시, 한쪽만 닿으면 명중
- S-044 반올림·정수 규칙
- S-045 히트스탑 중 입력 보관과 밀어내기
- S-046 가드·패링 세부
- S-047 벽꽝·튕김 세부(`BounceMinSpeed` 2)
- S-048 경기 흐름 단계와 설정 교체 지점
- S-049 시나리오 `tuning`·`overlay`
- S-050 타격 연출 수치
- S-051 금 자국 색과 지형 파편 방식
- S-052 효과음 6종
- S-053 허수아비 행동 주기와 F6 순환
- S-054 포커스 도구 대체 경로

## 6. 알려진 문제와 미룬 것

- 기본 테스트 공격(넉백 14, 30°)의 수평 속도는 12.1이라 지상 명중만으로는 벽꽝(14)이 나오지 않는다. 모드 B 앞 휘두르기도 같은 수치라 CP6에서 확인할 것. 벽꽝 showcase는 시나리오 덮어쓰기(넉백 22)로 찍었다.
- KO 조각 연출(연출 물리), 카운트다운·KO 문구, KO 효과음, 잡기·던지기 효과음은 CP8. 지금 KO는 강한 명중음과 큰 고리로 대신한다.
- F3 오버레이의 잡기 상자(CP7)와 돌진 위력(CP5) 표시는 해당 CP에서. F1·F2·F4·F9·F10·`~` 단축키도 해당 CP에서.
- 효과음 6종은 파일 생성·재현성·재생 호출만 확인했고 직접 들어 보지 못했다. F3·F6·`[` `]`는 키 입력 자동 테스트가 없다(허수아비 동작은 `DummyInputTests`로 확인).
- 붙어 있는 두 파이터의 머리 위 체력 바는 서로 맞닿는다(폭 1.2, 간격 1.2). 겹치지는 않는다.

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 179/179, PlayMode 8/8, 규칙 점검 통과.
- 공격은 `AttackDef` 데이터 + `FighterState.AttackId`로 돈다. `SimConfig.GetAttack(id)`는 지금 0번(테스트 공격)만 돌려준다. 모드 B는 방향별 `AttackDef` 3개를 여기에 더하고, `FighterCombat.BeginTick`의 공격 시작에서 `config.Mode`로 고른다.
- 모드 A(돌진)는 공격 버튼을 떼는 시점이 중요하다. 히트스탑 동안의 버튼 뗌은 `PendingPresses`처럼 보관되지 않으므로 "누르고 있지 않음"으로 판정할 것.
- 몸 충돌 공격(모드 A)은 `FighterCombat.ResolveInteractions`에 위력 비교를 더하고, 결과 적용은 `Connect`(패링·가드·명중)를 재사용한다.
- 시나리오는 `"tuning"`으로 체력·넉백을 바꿔 장면을 짧게 만들 수 있다. 헤드리스 실행은 `ScenarioConfig.Create(def)`.
- 실시간 검증은 `Tools/focus-window.ps1 -Unity` / `-Restore`(S-054 이후 데스크톱 앱에서도 동작).
- 다음 첫 작업: CP5 로그, `03-combat.md` 4절(모드 A), 충전·돌진 상태와 위력 판정부터.
