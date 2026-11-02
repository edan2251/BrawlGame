using System;
using UnityEngine;

namespace Knockbox.Game.Online
{
    /// <summary>
    /// 친구 시험용 방 화면(CR-004 온라인 3단계): 방 만들기(Relay 참가 코드 표시·복사) / 코드 입력 참가 / 혼자 연습.
    /// 연결 중·대기·실패 상태와 짧은 조작 안내를 보인다. 뒤에서는 로컬 연습 경기가 돈다. 연습 중 Esc로 다시 연다.
    /// 화면 글자는 영문 짧은 단어(D-016). 최종 UI가 아니다(IMGUI).
    /// </summary>
    [RequireComponent(typeof(OnlineMatch))]
    public sealed class OnlineLobby : MonoBehaviour
    {
        OnlineMatch online;
        bool open = true;
        bool busy;
        string code = "";
        string joinCode = "";
        string status = "";
        GUIStyle title, label, box, field;

        void Awake()
        {
            online = GetComponent<OnlineMatch>();
            // 개발 빌드의 디버그 글자(틱, 상태 이름, 아래쪽 규칙 줄)는 친구 시험 화면에서 숨긴다.
            var presenter = FindFirstObjectByType<MatchPresenter>();
            if (presenter != null)
                presenter.SetDebugLabels(false);
        }

        void Update()
        {
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame && !online.Active)
                open = !open;
        }

        async void CreateRoom()
        {
            busy = true;
            status = "SIGNING IN...";
            try
            {
                var (relay, newCode) = await RelayTransport.HostAsync();
                code = newCode;
                status = "";
                online.StartSession(relay, true, OnlineMatch.DefaultRelayDelay);
                Debug.Log($"[Knockbox][online] 방 만들기: 코드 {code}");
            }
            catch (Exception e)
            {
                status = "FAILED: " + Short(e);
                Debug.LogError($"[Knockbox][online] 방 만들기 실패: {e}");
            }
            busy = false;
        }

        async void Join()
        {
            if (string.IsNullOrWhiteSpace(joinCode))
                return;
            busy = true;
            status = "JOINING...";
            try
            {
                var relay = await RelayTransport.JoinAsync(joinCode);
                code = joinCode.Trim().ToUpperInvariant();
                status = "";
                online.StartSession(relay, false, 0);
                Debug.Log($"[Knockbox][online] 참가: 코드 {code}");
            }
            catch (Exception e)
            {
                status = "FAILED: " + Short(e);
                Debug.LogError($"[Knockbox][online] 참가 실패: {e}");
            }
            busy = false;
        }

        static string Short(Exception e)
        {
            string m = e.Message ?? e.GetType().Name;
            return m.Length > 90 ? m.Substring(0, 90) + "..." : m;
        }

        void Styles()
        {
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            box ??= new GUIStyle(GUI.skin.box) { fontSize = 14 };
            field ??= new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        }

        void OnGUI()
        {
            float scale = OnlineMatch.GuiScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            Styles();
            var session = online.Session;

            // 방장 대기·손님 연결 중.
            if (session != null && (session.State == SessionState.WaitingPeer || session.State == SessionState.Connecting))
            {
                var panel = new Rect(width / 2 - 200, 120, 400, 290);
                GUI.Box(panel, "", box);
                GUILayout.BeginArea(new Rect(panel.x + 16, panel.y + 12, panel.width - 32, panel.height - 24));
                if (session.State == SessionState.WaitingPeer)
                {
                    GUILayout.Label("ROOM CODE", label);
                    GUILayout.Label(code, title);
                    if (GUILayout.Button("COPY CODE", GUILayout.Height(28)))
                        GUIUtility.systemCopyBuffer = code;
                    GUILayout.Label("WAITING FOR OPPONENT...", label);
                }
                else
                {
                    GUILayout.Label("CONNECTING TO ROOM " + code + "...", label);
                }
                GUILayout.Space(8);
                if (GUILayout.Button("CANCEL", GUILayout.Height(28)))
                    online.LeaveToPractice();
                GUILayout.EndArea();
                return;
            }

            // 연결이 끝남(나감·끊김·어긋남): 메뉴로.
            if (session != null && session.State == SessionState.Ended)
            {
                if (GUI.Button(new Rect(width / 2 - 80, 290, 160, 32), "BACK TO MENU"))
                {
                    online.LeaveToPractice();
                    open = true;
                }
                return;
            }
            if (session != null || !open)
            {
                if (session == null)
                    GUI.Label(new Rect(0, 4, width, 24), "PRACTICE   ESC: MENU", label);
                return;
            }

            var menu = new Rect(width / 2 - 210, 60, 420, 420);
            GUI.Box(menu, "", box);
            GUILayout.BeginArea(new Rect(menu.x + 16, menu.y + 10, menu.width - 32, menu.height - 20));
            GUILayout.Label("KNOCKBOX  ONLINE TEST", title);
            GUILayout.Space(6);
            GUI.enabled = !busy;
            if (GUILayout.Button("CREATE ROOM", GUILayout.Height(34)))
                CreateRoom();
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("CODE", label, GUILayout.Width(50), GUILayout.Height(34));
            joinCode = GUILayout.TextField(joinCode, 12, field, GUILayout.Height(34));
            if (GUILayout.Button("JOIN", GUILayout.Width(80), GUILayout.Height(34)))
                Join();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            if (GUILayout.Button("PRACTICE (ESC: MENU)", GUILayout.Height(28)))
                open = false;
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(status))
                GUILayout.Label(status, label);
            GUILayout.EndArea();
            DrawControls(new Rect(menu.x + 20, menu.y + 230, menu.width - 40, 170));
        }

        /// <summary>짧은 조작 안내(실험 경기, 마우스 + 키보드).</summary>
        public static void DrawControls(Rect rect)
        {
            GUI.Box(rect, "CONTROLS");
            var style = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            string text =
                "A / D        MOVE\n" +
                "SPACE        JUMP\n" +
                "W at wall    CLIMB\n" +
                "MOUSE        AIM\n" +
                "LEFT CLICK   PUNCH (tap), HEADBUTT (hold)\n" +
                "RIGHT CLICK  GRAB, then LEFT CLICK to THROW\n" +
                "MATCH OVER   both HOLD LEFT CLICK to rematch";
            GUI.Label(new Rect(rect.x + 12, rect.y + 22, rect.width - 24, rect.height - 26), text, style);
        }
    }
}
