# CP0. 환경과 골격

| 항목 | 값 |
|---|---|
| 시작 | 2026-10-31 |
| 완료 | |
| 상태 | 진행 중 |
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

- 2026-10-31 세션 시작. 이 세션에는 MCP for Unity 도구가 로드되지 않았다(`claude mcp list`에서는 `UnityMCP: http://127.0.0.1:8080/mcp - Connected`). 같은 로컬 HTTP 엔드포인트에 JSON-RPC(`tools/call`)로 직접 호출하는 방식으로 진행한다. 다음 세션부터 Claude Code를 다시 시작하면 기본 도구로 보일 것으로 예상.
- MCP 확인: `mcpforunity://instances` → `Project_Brawl@d67741550f30280e`, Unity 6000.3.23f1. `read_console`(error, warning) → 0건.

## 3. 검증 결과

| 기준 | 방법 | 결과 | 증거 (테스트 이름, 수치, 캡처 경로) |
|---|---|---|---|
| ALL-1 | 콘솔 확인 | | |
| ALL-2 | 전체 테스트 | | |
| ALL-3 | 규칙 점검 | | |
| ALL-4 | showcase 실행 중 콘솔 | | |
| ALL-5 | 커밋·태그·푸시 | | |
| ALL-6 | 기록 갱신 | | |
| CP0-1 | ProjectVersion.txt | | |
| CP0-2 | 렌더 파이프라인 조회 | | |
| CP0-3 | MCP run_tests | | |
| CP0-4 | 어셈블리 참조 검사 테스트 | | |
| CP0-5 | Main 씬 스크린샷 | | |
| CP0-6 | git status | | |
| CP0-7 | 패키지 목록 | | |

## 4. 캡처 확인

| 파일 | 확인한 것 | 판단 |
|---|---|---|
| | | |

## 5. 자체 결정

## 6. 알려진 문제와 미룬 것

## 7. 인계 사항
