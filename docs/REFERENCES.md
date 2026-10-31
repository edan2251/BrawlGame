# 레퍼런스

외부 코드는 **개념과 수치를 참고**하는 것이 기본이다. 코드를 옮겨올 때는 라이선스를 확인하고, 옮긴 파일 상단에 출처와 라이선스를 주석으로 남기며, 로그에 기록한다. 아래 내용은 2026년 10월 말 기준으로 저장소를 직접 확인한 것이다.

## giorgosn/wallbrawl

- https://github.com/giorgosn/wallbrawl — MIT
- HTML5 캔버스와 순수 자바스크립트로 만든 2인 로컬 막대인간 격투 게임(약 1,800줄). Stick Fight에서 영감을 받았고 총기 상자가 핵심이다.
- **참고할 것**: 작은 프로토타입이 갖출 범위의 기준점(이동, 화면 흔들림, 합성 효과음, 게임패드 지원이 한 덩어리로 작게 들어 있다), 사인파 위상으로 팔다리를 그리는 절차적 애니메이션, 피격 시 흔들림 누적 방식, WebAudio 합성 효과음의 구성.
- **쓰지 않을 것**: 원 충돌체 기반 물리와 총기 시스템. 자바스크립트 코드는 옮기지 않는다.

## Matthew-J-Spencer/Ultimate-2D-Controller (Tarodev)

- https://github.com/Matthew-J-Spencer/Ultimate-2D-Controller — MIT
- **참고할 것**: 손맛 요소의 목록과 구조. 코요테 타임, 점프 버퍼, 조기 해제 중력, 정점 보정, 지상/공중 가감속 분리, 수치를 ScriptableObject로 분리한 방식.
- **바꿔서 쓸 것**: 원본은 `Rigidbody2D`와 `CapsuleCast`, 초 단위 시간(`Time.fixedDeltaTime`)을 쓴다. 우리는 순수 C# 시뮬레이션, 틱 단위, 상자 충돌로 다시 구현한다. 원본 코드는 Unity 6에서 이름이 바뀐 `Rigidbody2D.velocity`를 쓰므로 그대로 붙이지 않는다.

## Unity-Technologies/PhysicsExamples2D

- https://github.com/Unity-Technologies/PhysicsExamples2D — Unity Companion License(Unity 프로젝트에서 사용 가능)
- **6000.3 브랜치를 기준으로 본다.** 이 브랜치는 Box2D v3 기반의 저수준 2D 물리 API(`UnityEngine.LowLevelPhysics2D`) 예제를 담고 있다. 6000.5 이후 브랜치는 네임스페이스가 `Unity.U2D.Physics` 패키지로 바뀌었으므로 참고 시 혼동하지 않는다.
- **참고할 것**: Sandbox의 `CharacterMover`(캐스트 기반 이동기의 충돌 처리 방식, 우리 지형 충돌 설계의 참고), 래그돌과 소프트바디(KO 연출), 지형 자르기·파편화(`PhysicsDestructor*`, 벽꽝 파편 연출), `OldPhysics2D` 프로젝트의 래그돌 프리팹.
- **원칙**: 이 저장소의 물리는 **연출에만** 쓴다(D-003).

## Unity 2D Animation: 2D IK

- 사용자가 준 링크는 2D Animation 10.0(Unity 6.0용) 문서다: https://docs.unity3d.com/Packages/com.unity.2d.animation@10.0/manual/2DIK.html
- Unity 6000.3에서는 Package Manager가 설치하는 2D Animation 버전의 문서를 기준으로 한다. API 차이가 있으면 설치된 버전을 따른다. MCP for Unity의 Unity 문서 조회 도구로 확인할 수 있다.
- **쓸 것**: `IKManager2D`, `LimbSolver2D`(2마디 팔다리). 목표점은 코드로 움직인다.

## 레퍼런스 영상

- https://www.youtube.com/watch?v=tgr2NQsMFTE
- Claude Code와 기획 대화의 Claude 모두 영상을 볼 수 없다. **내용을 추측해서 반영하지 않는다.** 사용자가 가져오고 싶은 점을 적어주면 여기에 기록하고 해당 명세에 반영한다(Q-001).
- 사용자 메모: 화면 방향은 Rounds 캡처로 대체(CR-001, 아래 "Rounds 캡처").

## 스타일 참고 게임

- **Rounds**: 화면의 구성, 분위기, 표현 방식(어두운 배경, 위에서 오는 빛과 긴 그림자, 촘촘한 구조물, 연출 강도, 라운드마다 바뀌는 맵)을 **적극적으로 따라간다**(D-020). 캐릭터 디자인, 카드 그림, UI 디자인, 로고, 이름 같은 구체적인 에셋은 만들지 않는다.
- Square Brawl: 플랫한 도형과 짧은 라운드 구조를 방향으로만 참고한다(이전 D-010의 범위).

## Rounds 캡처 (로컬 전용)

- 위치: 프로젝트 루트의 `References/rounds/`(12장과 목록·시간표). **Git에 올리지 않는다.** 저장소가 공개(D-014)이고 다른 사람의 게임 영상 캡처이기 때문이다. 폴더 안 `.gitignore`와 루트 `.gitignore`의 `/References/`가 막는다.
- 출처: YouTube "Rounds - ROGUE-LIKE STICKFIGHT?! (4-Player Gameplay)", Stumpt 채널.
- 쓰는 법: CP11 이후 화면 캡처를 같은 종류 장면(지도, 그림자, 명중, 물리)과 나란히 놓고 `08-visual-direction.md` 7절 체크리스트로 판단한다. 카드 캡처(`card_*`)는 나중 단계의 참고로만 둔다.

## 사용자 팔레트

- 출처: 사용자의 다른 게임 CursorDash의 핵심 팔레트. `08-visual-direction.md` 2절의 토큰으로 정리했다(D-021).
- 견본 장면: `docs/references/palette-preview.png`(저장소에 포함).

## Box2D v3 (erincatto/box2d)

- https://github.com/erincatto/box2d — MIT. v3.2(2026-10-18)는 플랫폼 간 결정론과 롤백용 스냅숏 API(실험적)를 제공한다.
- **알고리즘 참고서로만 쓴다**(D-026): (cos, sin) 회전 적분, 직사각형 충돌의 접촉점 계산, 서브스텝과 부드러운 제약. 코드를 옮긴 부분은 파일 머리에 출처와 MIT 라이선스를 적는다.
- Box2D.NET(C# 이식, MIT)은 3.1 기반이라 스냅숏이 없어 지금은 롤백에 쓸 수 없다. 재평가 조건은 `09-props-physics.md` 7절.

## Backdash

- C# 롤백 넷코드 라이브러리, MIT, 2026-10 기준 활발히 관리됨. 온라인 단계(CP16)의 첫 검토 후보(D-027). 착수 전에 상태와 라이선스를 다시 확인하고 Q-005로 정한다.

## 도구

- MCP for Unity: https://github.com/CoplayDev/unity-mcp — MIT, v10.3.0 고정. 문서: https://coplaydev.github.io/unity-mcp/
