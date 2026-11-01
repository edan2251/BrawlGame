# CP6. 모드 B: 무기 휘두르기

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | spec/03-combat.md 5절, spec/05-presentation.md 2절 |
| 태그 | cp-6 |

## 1. 계획

1. `CombatMode.Swing`과 방향별 `AttackDef` 3종(앞 5/4/14·12·14@30°, 위 5/4/16·10·16@80°, 내려찍기 6/5/16·10·12@-70°). 판정 원 3개가 어깨 둘레의 호를 따라 움직이도록 원마다 Start → Mid → End 키프레임(활성 틱 사이는 직선 보간).
2. 입력: 위 + 공격 → 위 휘두르기, 공중에서 아래 + 공격 → 내려찍기, 그 외 → 앞 휘두르기. 내려찍기 명중 시 공격자 vy = 18, 공중 점프·대시 회복.
3. 대시 시작 4틱 이후 공격으로 캔슬(설정으로 끔), 공중에서 시작한 공격 도중 착지하면 4틱 착지 경직. 몸끼리 접촉은 밀어내기만.
4. 연출: 앞손이 쥔 무기(연출 전용). 활성 틱에는 무기 끝을 바깥쪽 판정 원 중심에 맞추고, 시작·후딜은 휴식 자세와 첫·끝 키프레임 사이를 보간.
5. 검증: CP6-1~6 EditMode, 무기 끝과 판정 원 일치를 PlayMode 수치로도 확인, CP6-7 오버레이 캡처, CP6-8 허수아비 1분 대전(모드 A와 비교).

## 2. 작업 기록

- 2026-10-31 시뮬레이션: `HitCircle`에 Mid 키프레임과 `At(t)`, `AttackDef.PogoSpeed`, `SwingModeParams`(기술 3종, 대시 캔슬, 착지 경직), 공격 번호 `AttackIds`, 행동 상태 `LandingLag`, 필드 `AttackFromAir`. 키프레임은 회전축 둘레 세 각도로 미리 계산한 상수(S-062). 기존 EditMode 225개 중 설정 체크섬 테스트 1개 실패: 판정 원 개수를 설정 단계에서 3으로 잘라 4로 바꿔도 체크섬이 같았음 → 값은 그대로 두고 쓸 때 자르게 고침.
- 테스트 31개(`SwingModeTests`) 추가. 첫 실행 실패 1건은 테스트 오류(대시가 끝난 뒤에도 공격을 눌러 정상 공격이 나감) → 대시 중에만 누르게 고침. 가드에 막힌 내려찍기 테스트는 조건부 단언을 없애고 같은 배치에서 가드를 확정.
- 연출: 무기(잉크 외곽선 + 그늘색), 활성 틱에 손잡이·끝을 판정 원에 고정, 시작·후딜 보간. PlayMode `WeaponTests`로 세 기술 활성 13틱의 무기 끝과 마지막 판정 원 중심 거리를 잼(최대 0.0000). 이 과정에서 손 자세 진행도가 한 틱 앞서던 것을 고침(테스트 공격 포함).
- showcase 8개(오버레이 3기술 × 활성 틱 전부, 앞·위 자세, 포고, 클래시, 대시 캔슬). 캡처 확인 후 수정: 앞 시나리오(CP5 오버레이)의 위력 숫자가 다음 오버레이 시나리오에 남음 → 경기 시작 때 지움. 휴식 무기가 아래로 늘어져 바닥을 뚫음 → 앞 위로 세워 듦. 기대값 1건(공격 종료 틱)은 히트스탑 7틱만큼 늦게 끝나는 것을 반영해 고침.
- CP6-8: 1분 무작위 허수아비 대전은 오류 없이 진행, 라운드는 끝나지 않음(체력 90/100). 10분 헤드리스로 모드 A와 비교.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 컴파일 후 `read_console` 오류·경고 | PASS | 오류 0, 경고 0 |
| ALL-2 | EditMode·PlayMode 전체 | PASS | EditMode 256/256, PlayMode 11/11 |
| ALL-3 | `Knockbox/Verify/Check Simulation Rules` + `SimulationRulesTests`·`AllocationTests` | PASS | "시뮬레이션 규칙 점검 통과", Step 할당 0 |
| ALL-4 | `Knockbox/Run Scenario Captures` 실행 중 콘솔 | PASS | 75장 저장, 오류·경고 0. showcase 21개 expect·헤드리스 체크섬 일치 |
| CP6-1 | `SwingModeTests.FrameData_MatchesSpec`(7) | PASS | 앞 5/4/14(중립·오른쪽·지상 아래·공중 중립), 위 5/4/16(지상·공중), 내려찍기 6/5/16(공중 아래) |
| CP6-2 | `HitCircles_FollowKeyframesEachActiveTick`(4), `OneSwing_HitsSameOpponentOnce` | PASS | 활성 틱마다 원 3개가 꺾은선 위치(바라보는 쪽 반전 포함), 첫 틱 Start·끝 틱 End. 활성 내내 겹쳐도 한 번만 명중 |
| CP6-3 | `DownSlamHit_PogosAndRestoresAirMoves`, `GuardedDownSlam_DoesNotPogo` | PASS | 명중 시 vy = 18, 공중 점프 1·대시 1 회복, 넉백 12 @ -70°, 히트스탑 뒤 실제로 올라감. 가드에 막히면 포고 없음 |
| CP6-4 | `ActiveSwingsOverlap_Clash`, `BodyContact_OnlyPushesInSwingMode` | PASS | 동시 앞 휘두르기 → 클래시 1회, 피해 0, 양쪽 반동. 모드 B의 대시 몸통 접촉은 피해 없음 |
| CP6-5 | `DashAttackCancel_OnlyAfterFourTicks`(5), `DashAttackCancel_CanBeDisabled`, `LandingDuringAirAttack_FourTickLag`, `GroundAttack_NoLandingLag_AndTestModeUnaffected` | PASS | 대시 1~3틱째 공격 무시, 4·5틱째 캔슬(속도 > 11 유지). 설정을 끄면 캔슬 없음. 공중 공격 중 착지 → 4틱 착지 경직(점프 불가) 뒤 조작 가능 |
| CP6-6 | `MirroredSwingFight_AndSyncTest`(Box, Towers, Pit) | PASS | 3600틱 봇 대전 미러 일치, SyncTest 불일치 0. Box: 휘두르기 99, 명중 21, 가드 10, 패링 1, 클래시 13, KO 3 |
| CP6-7 | 오버레이 캡처(활성 13틱) + `WeaponTests.WeaponTip_MatchesOuterHitCircle_EveryActiveTick` + 자세 캡처 | PASS | 무기 끝과 마지막 판정 원 중심 최대 거리 0.0000(13틱). 캡처에서 원 3개가 무기를 따라 놓임. `captures/CP6/showcase_cp6_*.png` 30장 |
| CP6-8 | `DummyMatchTests.RandomDummies_OneMinuteMatch(Swing)` + 10분 헤드리스 비교 | PASS | 1분: 오류 0, 공격 90, 명중 1, 끝난 라운드 0(체력 90/100; 모드 A는 68/75). 10분 무작위 허수아비: B 라운드 1개(305.4초), A 2개(215.8초). 10분 CombatBot: B 20라운드 평균 26.6초·클래시 분당 9.8, A 32라운드 평균 15.7초·클래시 분당 6.3 |

측정값: Step 평균 0.88 µs, 롤백 8틱 7.31 µs. 1080p 렌더 텍스처 평균 5.08 ms(197 fps), 연출 할당 0 B/프레임. 실시간 10.0014초에 600틱. CP3~CP5 showcase를 다시 찍어 비교: CP4 테스트 공격 장면 11장만 주먹 주변 약 20 × 25픽셀이 달라짐(손 자세 진행도 수정, 의도한 변화), 나머지는 차이 0.

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| showcase_cp6_overlay_forward_active0~3 | 머리 위 앞 → 정면 → 앞 아래로 내려오는 무기, 원 3개가 무기 위에, 무기 끝 = 바깥 원 | 일치 |
| showcase_cp6_overlay_up_active0~3 | 앞 → 머리 위 → 뒤로 넘어가는 호 | 일치 |
| showcase_cp6_overlay_down_active0~4 | 공중에서 앞 위 → 앞 아래 → 바로 아래 | 일치. 처음엔 이전 시나리오의 위력 숫자가 남아 있어 고침 |
| showcase_cp6_forward_windup / hit / follow / flight | 무기를 들어 올림 → 수평으로 맞힘(번쩍임) → 앞 아래로 따라감 → 상대가 구르며 날아감 | 맞음 |
| showcase_cp6_up_windup / overhead / follow | 앞 위로 듦 → 머리 위 → 뒤에서 돌아옴 | 맞음. 후딜에 팔·무기가 몸 앞을 가로지름(짧은 순간이라 허용) |
| showcase_cp6_pogo_slam / hit / pogo_rise | 내려찍기 → 명중 → 위로 튀어 오름 | 맞음 |
| showcase_cp6_clash_swings / clash / clash_sparks / recoil | 두 무기 끝이 위에서 부딪혀 별 폭발, 양쪽 반동 | 맞음 |
| showcase_cp6_dashcancel_dash / cancel_windup / hit | 대시 잔상을 끌고 들어 올림 → 명중 | 맞음 |
| 휴식 자세(여러 장) | 무기를 앞 위로 세워 듦 | 처음엔 아래로 늘어져 바닥을 뚫어 고침 |

## 5. 자체 결정

- S-062 휘두르기 판정 원 키프레임과 개수 처리
- S-063 기술 선택과 대시 캔슬 세부
- S-064 착지 경직 적용 범위
- S-065 포고는 명중일 때만
- S-066 무기 연출과 손 자세 진행도
- S-067 CP6-8 측정 방식

## 6. 알려진 문제와 미룬 것

- 봇 대전 기준 모드 B 라운드가 모드 A보다 길다(26.6초 대 15.7초). 무작위 허수아비끼리는 두 모드 모두 1분 안에 라운드가 끝나지 않는다. 사람 대전 비교는 CP9 플레이테스트에서.
- 기본 수치의 휘두르기로는 벽꽝이 거의 나오지 않는다(봇 대전 3맵 모두 0회; 앞 휘두르기 수평 넉백 12.1 < 14). 위 휘두르기의 천장 벽꽝만 가능. CP9 비교 때 볼 점으로 남긴다.
- 위 휘두르기 후딜에 팔과 무기가 몸 앞을 가로지른다. 무기가 가늘어 작은 화면에서는 잘 안 보일 수 있다.
- 무기 휘두르는 소리는 없다(05 7절 목록에 없음).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 256/256, PlayMode 11/11, 규칙 점검 통과. F1로 Test → Charge → Swing 순환.
- 상호작용 판정은 `FighterCombat.ResolveInteractions` 한 곳: 판정 원(`ResolveHitboxes`) → 몸 충돌(`ResolveBodies`). 잡기(CP7)는 여기에 같은 스냅숏 기반으로 더하고, 03 2절 상성 행렬(공격 > 잡기 > 가드 > 공격, 같은 행동끼리 클래시·잡기 풀기)을 지킬 것.
- 잡기 불가 상태(피격 경직, 던져지는 중, KO)와 "비틀거리는 상대는 잡을 수 있다"는 `CanBeHit`와 비슷한 술어를 따로 둘 것.
- 던져진 상태의 벽꽝 기준 12·데미지 10은 `FighterMovement.Rebound`의 기준값을 상태별로 고르면 된다.
- 다음 첫 작업: CP7 로그, `03-combat.md` 2·6절, 잡기 상태(시작 5 / 활성 3 / 헛잡기 후딜 22)와 잡기 상자부터.
