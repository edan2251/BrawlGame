using System;
using System.IO;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game.Online
{
    /// <summary>
    /// 온라인 락스텝 경기(CR-004 온라인 2단계, 개발 빌드·에디터 전용, 패키지 없음). AutoPlayDebug가 명령줄 인수를 보고 붙인다.
    ///   -host 포트              방장(P1). 손님을 기다렸다가 시드·입력 지연을 정해 보낸다
    ///   -join 주소:포트         손님(P2)
    ///   -delay N                입력 지연 틱(방장이 정함, 기본 3)
    ///   -onlinebot              내 자리를 봇이 친다(방장 추격 봇, 손님 BrawlBot). 경기 끝에는 잠깐 뒤 공격을 눌러 재대결 준비
    ///   -onlinematches N        경기 N개가 끝나면 나간다(종료 코드 0 = 동기화 어긋남·오류 없음)
    ///   -netsim 편도ms,손실%    보내는 쪽 지연·잃음 흉내(왕복은 두 끝 합)
    /// 끝나면 입력 기록과 요약을 Replays/online_P슬롯_시각.*에 남긴다(어긋나면 -replaycheck로 그 틱 상태를 다시 뽑는다).
    /// 디버그 단축키는 끈다(설정을 바꾸면 두 PC가 어긋남).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(MatchRunner))]
    public sealed class OnlineMatch : MonoBehaviour
    {
        public const int DefaultPort = 7777;
        public const int DefaultDelay = 3;

        MatchRunner runner;
        LockstepSession session;
        SimulatedLink link;
        bool started;
        bool finished;
        bool bot;
        int targetMatches;
        int matches;
        double quitAt = -1;
        GUIStyle style;

        public LockstepSession Session => session;
        public int MatchesFinished => matches;

        public static bool Requested => AutoPlayDebug.HasArg("-host") || AutoPlayDebug.HasArg("-join");

        void Awake()
        {
            runner = GetComponent<MatchRunner>();
            Application.runInBackground = true;
            bot = AutoPlayDebug.HasArg("-onlinebot");
            targetMatches = int.TryParse(ReplayCheck.StringArg("-onlinematches"), out int n) ? n : 0;
        }

        void Start()
        {
            runner.Running = false;
            runner.OnlineRematch = true;
            runner.RandomizeSeed = false;
            var hotkeys = FindFirstObjectByType<DebugHotkeys>();
            if (hotkeys != null)
                hotkeys.enabled = false;

            var config = runner.CreateConfig();
            var arenas = runner.ResolveArenas(runner.ArenaName);
            uint configChecksum = config.ComputeChecksum();
            uint arenaChecksum = InputRecording.ComputeArenaChecksum(arenas);
            int delay = int.TryParse(ReplayCheck.StringArg("-delay"), out int d) ? d : DefaultDelay;

            ITransport transport;
            bool host = AutoPlayDebug.HasArg("-host");
            try
            {
                if (host)
                {
                    int port = int.TryParse(ReplayCheck.StringArg("-host"), out int p) ? p : DefaultPort;
                    transport = UdpTransport.Host(port);
                }
                else
                {
                    string target = ReplayCheck.StringArg("-join") ?? "127.0.0.1";
                    int colon = target.LastIndexOf(':');
                    string address = colon > 0 ? target.Substring(0, colon) : target;
                    int port = colon > 0 && int.TryParse(target.Substring(colon + 1), out int p) ? p : DefaultPort;
                    transport = UdpTransport.Join(address, port);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Knockbox][online] 연결을 열지 못함: {e.Message}");
                finished = true;
                Quit(2);
                return;
            }
            string netsim = ReplayCheck.StringArg("-netsim");
            if (!string.IsNullOrEmpty(netsim))
            {
                var parts = netsim.Split(',');
                double oneWay = parts.Length > 0 && double.TryParse(parts[0], out double ms) ? ms / 1000.0 : 0;
                double loss = parts.Length > 1 && double.TryParse(parts[1], out double pct) ? pct / 100.0 : 0;
                link = new SimulatedLink(transport, () => Time.realtimeSinceStartupAsDouble, oneWay, oneWay * 0.25, loss, 0.01, host ? 11 : 22);
                transport = link;
                Debug.Log($"[Knockbox][online] 망 흉내: 편도 {oneWay * 1000:0} ms(흔들림 25%), 잃음 {loss * 100:0.#}%, 중복 1%");
            }
            uint seed = (uint)Environment.TickCount | 1u;
            session = new LockstepSession(transport, host, configChecksum, arenaChecksum, seed, delay);
            Debug.Log($"[Knockbox][online] {(host ? "방장" : "손님")} 시작: 설정 {configChecksum:X8}, 맵 {arenaChecksum:X8}, 봇 {bot}");
        }

        void Update()
        {
            if (session == null)
                return;
            double now = Time.realtimeSinceStartupAsDouble;
            session.Poll(now);
            if (!started && session.State == SessionState.Running)
                BeginMatch();
            if (session.State == SessionState.Ended && !finished)
                Finish();
            if (quitAt > 0 && now >= quitAt)
                Quit(ExitCode());
        }

        void BeginMatch()
        {
            started = true;
            runner.Seed = session.Seed;
            runner.StartNewMatch();
            int slot = session.LocalSlot;
            IInputSource local;
            if (bot)
            {
                IInputSource inner = slot == 0
                    ? new BrawlChaserBot(() => runner.World, slot, 10)
                    : new BrawlBot(() => runner.World, slot, 0xB22u * 10);
                local = new RematchHoldInput(() => runner.World, inner, slot == 0 ? 40 : 95);
            }
            else
            {
                local = new MouseAimInputSource(() => runner.World, slot);
            }
            session.Begin(runner.World.State.Tick, local);
            runner.Lockstep = session;
            runner.StartRecording(runner.World.Config.Rules.Format());
            runner.Ticked += OnTicked;
            runner.Running = true;
            Debug.Log($"[Knockbox][online] 경기 시작: 내 자리 P{slot + 1}, 시드 {session.Seed:X8}, 입력 지연 {session.DelayTicks}틱");
        }

        void OnTicked(MatchRunner r)
        {
            var w = r.World;
            int tick = w.State.Tick;
            if (LockstepSession.NeedsChecksum(tick))
                session.ReportChecksum(tick, w.ComputeChecksum());
            for (int i = 0; i < w.Events.Count; i++)
            {
                if (w.Events[i].Type != SimEventType.MatchEnd)
                    continue;
                matches++;
                Debug.Log($"[Knockbox][online] t={tick} 경기 {matches} 끝: 승자 P{w.Events[i].Fighter + 1}, 점수 {w.State.Match.Score0}:{w.State.Match.Score1}, 체크섬 {w.ComputeChecksum():X8}, {Stats()}");
            }
            // 목표 경기 수를 채우면 재대결이 시작된 뒤(양쪽이 같은 틱까지 계산) 잠깐 있다가 나간다.
            if (targetMatches > 0 && matches >= targetMatches && quitAt < 0 && w.State.Match.Phase != MatchPhase.MatchEnd)
            {
                quitAt = Time.realtimeSinceStartupAsDouble + 1.0;
                Debug.Log($"[Knockbox][online] 목표 경기 {targetMatches}개 끝, 곧 나감");
            }
        }

        string Stats() =>
            $"왕복 {(session.RttSeconds < 0 ? "-" : (session.RttSeconds * 1000).ToString("0"))} ms(최대 {session.MaxRttSeconds * 1000:0}), " +
            $"대기 {session.StallCount}회, 비교한 체크섬 {session.ChecksumsCompared}개, 중복 무시 {session.DuplicatesIgnored}, " +
            $"보냄 {session.PacketsSent}·받음 {session.PacketsReceived}" + (link != null ? $", 흉내 잃음 {link.Dropped}·중복 {link.Duplicated}" : "");

        void Finish()
        {
            finished = true;
            runner.Running = false;
            string detail = $"{session.Reason}: {session.EndDetail}";
            if (session.Reason == EndReason.Desync)
                Debug.LogError($"[Knockbox][online] 동기화 어긋남, 경기를 멈춤. {detail}");
            else
                Debug.Log($"[Knockbox][online] 연결 끝: {detail}");
            SaveRecords(detail);
            if (targetMatches > 0 && quitAt < 0)
                quitAt = Time.realtimeSinceStartupAsDouble + 0.5;
        }

        void SaveRecords(string detail)
        {
            var recording = runner.StopRecording();
            if (recording == null)
                return;
            string name = $"online_P{session.LocalSlot + 1}_{DateTime.Now:yyyyMMdd_HHmmss}";
            string path = ReplayFiles.Save(recording, name);
            File.WriteAllText(Path.ChangeExtension(path, ".txt"),
                $"끝: {detail}\n틱 {runner.World.State.Tick}, 경기 {matches}개, 최종 체크섬 {recording.FinalChecksum:X8}\n어긋난 틱 {session.DesyncTick}\n{Stats()}\n");
            Debug.Log($"[Knockbox][online] 입력 기록 저장: {path} ({recording.Count}틱, 최종 {recording.FinalChecksum:X8})");
        }

        int ExitCode()
        {
            if (session == null)
                return 2;
            bool bad = session.Reason == EndReason.Desync || session.Reason == EndReason.ProtocolError || session.Reason == EndReason.Rejected
                       || session.Reason == EndReason.Timeout;
            return !bad && matches >= targetMatches ? 0 : 1;
        }

        void Quit(int code)
        {
            if (session != null && session.State != SessionState.Ended)
            {
                session.Leave();
                if (!finished)
                    Finish();
            }
            Debug.Log($"[Knockbox][online] 종료 코드 {code}");
            if (!Application.isEditor)
                Application.Quit(code);
            quitAt = -1;
            targetMatches = 0;
        }

        void OnDestroy()
        {
            if (session == null)
                return;
            if (session.State != SessionState.Ended)
            {
                session.Leave();
                if (!finished)
                    Finish();
            }
            session.Dispose();
            session = null;
        }

        void OnGUI()
        {
            if (session == null)
                return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperCenter };
            string text;
            switch (session.State)
            {
                case SessionState.WaitingPeer:
                    text = "ONLINE  상대를 기다리는 중…";
                    break;
                case SessionState.Connecting:
                    text = "ONLINE  방장에게 연결 중…";
                    break;
                case SessionState.Ended:
                    text = session.Reason == EndReason.Desync ? $"동기화 오류(틱 {session.DesyncTick}) — 경기를 멈춤"
                        : session.Reason == EndReason.PeerLeft ? "상대가 나갔습니다"
                        : session.Reason == EndReason.Timeout ? "연결 끊김"
                        : session.Reason == EndReason.Rejected ? "연결 거절: 빌드가 다름"
                        : $"연결 끝({session.Reason})";
                    break;
                default:
                    string rtt = session.RttSeconds < 0 ? "-" : (session.RttSeconds * 1000).ToString("0");
                    text = $"ONLINE  나 = P{session.LocalSlot + 1}   왕복 {rtt} ms   입력 지연 {session.DelayTicks}틱";
                    if (session.Waiting && session.SecondsSinceReceive > 0.25)
                        text += $"\n상대 기다리는 중… {session.SecondsSinceReceive:0.0}초";
                    if (runner.World != null && runner.World.State.Match.Phase == MatchPhase.MatchEnd)
                    {
                        ref readonly var s = ref runner.World.State;
                        bool me = (s.GetFighter(session.LocalSlot).PrevButtons & InputButtons.Attack) != 0;
                        bool other = (s.GetFighter(1 - session.LocalSlot).PrevButtons & InputButtons.Attack) != 0;
                        text += $"\n재대결: 둘 다 공격을 누르고 있으면 시작   나 {(me ? "준비" : "-")} / 상대 {(other ? "준비" : "-")}";
                    }
                    break;
            }
            GUI.Label(new Rect(0, 8, Screen.width, 80), text, style);
        }
    }
}
