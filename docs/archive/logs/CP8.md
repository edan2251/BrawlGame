# CP8. 경기 완성도

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | 2026-10-31 |
| 상태 | 완료 |
| 관련 명세 | spec/04-arena.md 3·5절, spec/05-presentation.md 4~7절, spec/06-match-and-tools.md 1절 |
| 태그 | cp-8 |

## 1. 계획

1. 가시(04 3절): 닿으면 데미지 15, vy = 20, 경직 20틱, 45틱 면역(면역 중에는 바닥처럼 섬), 공중 점프·대시 회복.
2. 맵 검증(04 5절): 이동만으로 모든 발판에 닿는지 매크로 입력 BFS로 확인(두 시작 위치), Towers 튕김 패드 → 가운데 방, Pit 가시 → 대지 복귀.
3. KO 파편: 진 쪽 몸이 히트스탑 20 뒤 4~6조각으로 깨져 연출 물리로 튕겨 다님. 연출 물리는 별도 물리 씬을 시뮬레이션 시간으로 직접 진행(프로젝트 물리 설정·시뮬레이션과 무관). 이긴 쪽은 통통 튐.
4. HUD: READY/GO 카운트다운, KO(무승부 DRAW), MatchEnd(승자, 아무 버튼), 점수 칸.
5. 남은 효과음 6종(잡기, 던지기, 가시, 튕김 패드, 카운트다운, KO).
6. 검증: CP8-1·2 EditMode, CP8-5 연출 물리 켜고 끈 체크섬, CP8-6 효과음·중복 제한, CP8-8 가장 바쁜 장면 프레임 시간, CP8-3·4·7 캡처.

## 2. 작업 기록

- 2026-10-31 가시: `CombatParams` 가시 값 4개, `SpikeImmunityTicks` 필드, `Spike` 이벤트, `FighterMovement.SpikeHit`. 붙잡기 중에는 가시에 닿지 않게(S-076). 기존 테스트 중 Pit 봇 대전 4개가 "충분히 싸웠는지" 확인에 걸림(봇이 구덩이에서 오래 튐, 미러·SyncTest는 통과) → 확인 기준 조정(S-078).
- 맵 테스트(`ArenaPlayTests`) 10개: 입력 덩어리 BFS로 세 맵 × 두 시작 위치 모든 발판 도달(첫 실행에서 Towers 튕김 패드만 "서서 멈춘" 상태로 안 잡힘 → 패드는 닿으면 도달로 셈), Towers 패드 → 가운데 방, Pit 가시 → 대지, 가시 규칙과 대칭.
- 효과음 6종 합성·연결, `SfxPlayer.Play`에 음높이 인자. `KoShatter`(별도 물리 씬), 진 쪽 숨기기·이긴 쪽 통통 튀기, HUD 가운데 문구, 카운트다운 소리, 가시·패드 연출.
- showcase 8개(KO 연속, MatchEnd, 3맵 × 2모드 전경). PlayMode `MatchCompletionTests` 3개(CP8-5, 6, 8).
- 캡처 확인 후 수정: HUD 글자가 바래 보임(캡처 픽셀로 잉크 #24283B가 #6B6B7A로 나옴) → TextMesh 색에 Linear 보정(디버그 글자도). 보정 뒤 READY가 정확히 #24283B로 찍힘.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 컴파일 후 `read_console` 오류·경고 | PASS | 오류 0, 경고 0 |
| ALL-2 | EditMode·PlayMode 전체 | PASS | EditMode 326/326, PlayMode 14/14 |
| ALL-3 | `Knockbox/Verify/Check Simulation Rules` + `SimulationRulesTests`·`AllocationTests` | PASS | "시뮬레이션 규칙 점검 통과", Step 할당 0 |
| ALL-4 | `Knockbox/Run Scenario Captures` 실행 중 콘솔 | PASS | 107장 저장, 오류·경고 0. showcase 34개 연출 켬·끔 모두 헤드리스 체크섬 일치 |
| CP8-1 | `ArenaTests`(CP2, 파서), `MovementTests.BoxPillar_ClearedByFullJumpOnly`(CP2), `ArenaPlayTests.EveryPlatform_ReachableFromSpawn`(6), `Towers_BouncePadLeadsIntoMiddleRoom`, `Pit_RecoverFromSpikesToPlateau` | PASS | 세 맵 파서 검증. Box 기둥: 최대 점프만 넘음. 도달: Box 5/5, Towers 11/11, Pit 6/6 발판(두 시작 위치 모두, 착지 상태 41~54개 탐색). Towers 패드로 가운데 방 바닥에 섬. Pit 가시에서 대지로 돌아옴 |
| CP8-2 | `Spike_DamagesBouncesStunsAndRestoresAirMoves`, `Spike_IsMirrored` | PASS | 데미지 15, vy = 20, 경직 20, 면역 45, 공중 점프·대시 회복. 면역 동안 다시 내려와도 무사, 정확히 45틱 뒤 다시 가시. 좌우 대칭 |
| CP8-3 | showcase 3맵 × 2모드 전경 캡처 | PASS | `captures/CP8/showcase_cp8_{box,towers,pit}_{charge,swing}_overview.png` |
| CP8-4 | KO 연속 캡처 6장 | PASS | `showcase_cp8_ko_ko_hit / ko_freeze / shatter1~4`: 명중 번쩍임 → KO 고리 → 조각 6개로 깨짐 → 튀어 다님 → 바닥에 멈춤 |
| CP8-5 | `MatchCompletionTests.PresentationPhysics_OnAndOff_SameChecksum` + CP3-6 재실행 | PASS | KO 장면 체크섬 켬 9994C2D6 = 끔 9994C2D6 = 헤드리스. 연출 물리를 끈 showcase 34개도 헤드리스와 같음. 켜면 파편이 실제로 생김 |
| CP8-6 | `EverySfx_HasClip_AndSameFrameDuplicatesAreDropped` + `SfxSynth_RegeneratesIdenticalFiles` | PASS | 15개(점프 … KO) 모두 Sandbox 효과음 묶음에 연결, 같은 프레임 같은 효과음 두 번째는 건너뜀, 다른 효과음·다음 프레임은 재생. 15개 WAV 다시 만들어도 같은 파일 |
| CP8-7 | HUD 캡처 | PASS | `showcase_cp8_ko_next_ready`(READY, ROUND 2, P1 점수 1칸), `next_go`(GO), `showcase_cp8_matchend_ko / matchend / matchend_prompt`(KO, P1 WINS, PRESS ANY BUTTON) |
| CP8-8 | `BusiestScene_KoShatterAndParticles_Holds60Fps` | PASS | KO 파편 구간 183프레임, 1080p 렌더 텍스처 평균 5.47 ms(183 fps), 최대 9.42 ms |

측정값: Step 평균 0.86 µs, 롤백 8틱 6.82 µs. 1080p 렌더 텍스처 평균 5.62 ms(178 fps), 연출 할당 0 B/프레임. 실시간 10.0035초에 600틱.

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| showcase_cp8_ko_* (8장) | 명중 순간 화면 번쩍임, KO 고리, 20틱 뒤 진 쪽이 보라색 조각 6개(잉크 외곽선)로 깨져 튀어 다니다 바닥에 멈춤, 이긴 쪽이 통통 튐, 다음 라운드 READY·GO | 맞음 |
| showcase_cp8_matchend_ko / matchend / matchend_prompt | KO → P1 WINS(P1 그늘색) → 60틱 뒤 PRESS ANY BUTTON, 점수 칸 1/1 | 맞음. 처음엔 글자 색이 바래 보여 보정 |
| showcase_cp8_{box,towers,pit}_{charge,swing}_overview | 세 맵 전경(Towers 패드, Pit 가시 구덩이), 모드 A 충전·모드 B 무기, 가드 | 맞음 |
| 이전 CP showcase 다시 찍음 | 회귀 | 글자 색 보정으로 디버그 글자가 있는 장면은 글자 픽셀이 달라짐(의도). 그 밖의 차이는 없음 |

## 5. 자체 결정

- S-076 가시 세부
- S-077 맵 도달 가능성 탐색 방법
- S-078 가시 도입에 따른 이전 테스트의 확인 기준 조정
- S-079 KO 파편(별도 물리 씬)
- S-080 HUD 문구와 TextMesh 색 보정
- S-081 효과음 6종
- S-082 CP8-6·8 측정 방법

## 6. 알려진 문제와 미룬 것

- 효과음 15개는 파일·재현성·연결·중복 제한까지 확인했고, 실제로 들어 보지는 못했다(사람 확인 필요).
- KO 조각은 다음 라운드 시작까지 바닥에 남는다(라운드가 바뀌면 지움).
- 붙잡은 동안 두 몸 그림 겹침, 모드 B의 벽꽝 드묾 등 이전 CP의 남은 항목은 CP9 비교에서 같이 본다.
- 맵별 배경 실루엣은 세 맵이 같다(CP3 임시 배치 그대로).

## 7. 인계 사항

- 현재 상태: 컴파일 오류 0, EditMode 326/326, PlayMode 14/14, 규칙 점검 통과. 경기가 처음부터 끝까지(카운트다운 → 싸움 → KO 파편 → 다음 라운드 → MatchEnd → 새 경기) 돈다.
- CP9: Windows 개발 빌드(`Builds/`, Git 제외), 처음 실행 시 `~` 단축키 도움말, `PLAYTEST.md`, 사용자 보고, Q-004 질문. 빌드에는 디버그 단축키가 포함된다(06 6절). F2 맵 순환, F9·F10 기록·재생, `~` 도움말은 아직 없다.
- 다음 첫 작업: CP9 로그, `CHECKPOINTS.md` CP9 절, 빌드 설정(씬 목록, 개발 빌드)부터.
