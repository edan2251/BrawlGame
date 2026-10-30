# 작업 환경 설정 (Windows)

이 문서의 1~7단계는 **사용자가 직접** 한다. 인증, 설치 승인, 계정 로그인이 들어 있기 때문이다. 8단계부터는 Claude Code가 CP0에서 확인하고 이어서 진행한다.

## 1. Unity 프로젝트 만들기

1. Unity Hub에서 **Unity 6000.3 LTS의 최신 패치**를 설치한다. 설치 모듈은 기본값이면 충분하고, 나중에 빌드하려면 Windows Build Support를 함께 고른다. Hub의 설치 목록에 6000.3보다 높은 **LTS** 표시 버전이 있으면 Claude Code에게 알려준다. 6000.5와 6000.6은 LTS 표시가 확인되지 않았고 2D 저수준 물리 API가 바뀐 버전이라, LTS로 표시되지 않는 한 이 프로젝트에서는 쓰지 않는다.
2. 새 프로젝트를 **Universal 2D** 템플릿으로 만든다. 프로젝트 이름은 `Knockbox`를 추천한다(가제, 나중에 바꿔도 된다).
3. 프로젝트 경로에는 **한글과 공백을 넣지 않는다**. 예: `D:\Dev\Knockbox`. Python, uv, 일부 Unity 도구가 비ASCII 경로에서 문제를 일으킬 수 있다.

## 2. Git과 GitHub CLI

1. Git for Windows를 설치한다. Unity Package Manager의 git URL 설치와 Claude Code 모두 이것이 필요하다.
2. GitHub CLI(`gh`)를 설치하고 터미널에서 `gh auth login`으로 **직접 로그인**한다. Claude Code는 인증 정보를 다루지 않는다.
3. 원격 저장소는 CP0에서 Claude Code가 이름과 공개 범위(기본 추천: 비공개)를 물어본 뒤 만든다. 직접 만들고 싶다면 빈 저장소를 만든 뒤 주소를 알려주면 된다.

## 3. Python과 uv (MCP for Unity 서버용)

MCP for Unity는 Python 3.10 이상과 `uv`가 필요하다. 패키지를 설치하면 뜨는 설정 마법사가 없는 항목을 안내해준다. 미리 설치하려면 PowerShell에서 `winget install --id=astral-sh.uv -e`를 실행한다.

## 4. Claude Code

Claude Code를 설치하고 로그인한다. 설치 방법과 Windows 요구 사항은 공식 문서를 따른다: https://docs.claude.com/en/docs/claude-code/overview

Claude Code는 항상 **Unity 프로젝트 루트 폴더**(이 `CLAUDE.md`가 있는 곳)에서 실행한다.

## 5. MCP for Unity 설치

1. Unity에서 `Window → Package Manager → + → Add package from git URL...`을 열고 아래 주소를 넣는다. 버전을 고정해 두어야 도중에 도구 동작이 바뀌지 않는다.
   ```
   https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.3.0
   ```
2. 설치 후 뜨는 설정 마법사에서 Python과 uv가 초록색이 될 때까지 진행한다.
3. 감지된 클라이언트 목록에서 **Claude Code**를 골라 설정한다. 언제든 `Window → MCP for Unity`에서 상태를 다시 볼 수 있고, `Connected`로 표시되면 된다.
4. 자동 설정이 실패하면 MCP for Unity 문서의 수동 설정 절을 따른다: https://coplaydev.github.io/unity-mcp/
5. 이후 작업 중에는 **Unity 에디터를 계속 열어둔다**. Claude Code는 열린 에디터를 통해 컴파일, 테스트, 스크린샷을 처리한다.

## 6. 이 문서 키트 넣기

받은 압축 파일의 내용(`CLAUDE.md`, `docs/`, `.gitignore`, `.gitattributes`, `.editorconfig`)을 Unity 프로젝트 루트에 그대로 풀어 넣는다. `Assets` 폴더와 같은 위치에 `CLAUDE.md`가 있어야 한다.

## 7. 첫 지시

프로젝트 루트에서 Claude Code를 실행하고 다음과 같이 시작한다.

> CLAUDE.md와 docs/progress/PROGRESS.md를 읽고 CP0부터 진행해줘.

레퍼런스 영상에서 가져오고 싶은 포인트가 있다면 이때 함께 적어준다. Claude Code가 `docs/REFERENCES.md`에 기록한다.

## 8. Claude Code가 CP0에서 확인하는 것

- Unity 버전이 6000.3.x인지, URP 2D Renderer가 활성인지
- 사전 승인 패키지가 설치돼 있는지(없으면 설치)
- MCP 연결: `read_console` 호출이 성공하는지
- Git 저장소 초기화, `.gitignore` 적용, 첫 커밋, 원격 저장소 연결
- Player Settings의 **Run In Background** 켜기(에디터가 포커스를 잃어도 플레이 모드 검증이 멈추지 않게)

## 사전 승인된 패키지

아래 표에 있는 것은 Claude Code가 묻지 않고 설치하거나 활성화할 수 있다. 버전은 Unity 6000.3이 Package Manager에서 권장하는 버전을 쓴다. 표에 없는 패키지, 에셋 스토어 에셋, 폰트, 외부 라이브러리는 설치 전에 사용자에게 묻는다.

| 패키지 | 용도 |
|---|---|
| Universal 2D 템플릿에 포함된 패키지 전부 | URP 2D 렌더러, 2D 기본 기능 |
| `com.unity.inputsystem` | 키보드 2인 분할과 게임패드 입력 |
| `com.unity.2d.animation` | 2D IK(팔다리) |
| `com.unity.2d.sprite` | 스프라이트 편집 |
| `com.unity.test-framework` | EditMode/PlayMode 테스트 |
| UI Toolkit, uGUI (내장) | HUD와 디버그 UI |
| `com.coplaydev.unity-mcp` (v10.3.0 고정) | 에디터 제어 (사용자가 설치) |

## 에디터 사용 팁

- IDE는 익숙한 것을 쓰면 된다(Rider 또는 Visual Studio). Claude Code는 IDE와 무관하게 동작한다.
- Claude Code가 작업하는 동안 같은 파일을 에디터에서 동시에 수정하면 충돌이 날 수 있다. 직접 손볼 때는 Claude Code에게 먼저 알려준다.
