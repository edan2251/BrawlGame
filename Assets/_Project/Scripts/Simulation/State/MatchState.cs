using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 경기 흐름(06-match-and-tools.md 1절).
    /// Countdown → Fighting → KO → RoundEnd(1틱) → RoundTransition(1틱, 훅) → Countdown, 또는 RoundEnd → MatchEnd.
    /// </summary>
    public enum MatchPhase : byte
    {
        Countdown,
        Fighting,
        KO,
        RoundEnd,
        RoundTransition,
        MatchEnd,
    }

    [Serializable]
    public struct MatchState
    {
        public MatchPhase Phase;
        /// <summary>현재 단계에 들어온 뒤 지난 틱 수.</summary>
        public int PhaseTick;
        /// <summary>1부터 센다.</summary>
        public int Round;
        public int Score0;
        public int Score1;
        /// <summary>마지막 KO의 승자. 0, 1, 또는 무승부·미정이면 -1.</summary>
        public int RoundWinner;
        /// <summary>라운드마다 맵 교체(CP12). 롤백 대상이라 상태에 둔다.</summary>
        public ArenaRotation Rotation;

        public readonly int GetScore(int fighter) => fighter == 0 ? Score0 : Score1;

        public void SetPhase(MatchPhase phase)
        {
            Phase = phase;
            PhaseTick = 0;
        }

        public readonly void Visit<T>(ref T v) where T : struct, IStateVisitor
        {
            v.Value(nameof(Phase), (int)Phase);
            v.Value(nameof(PhaseTick), PhaseTick);
            v.Value(nameof(Round), Round);
            v.Value(nameof(Score0), Score0);
            v.Value(nameof(Score1), Score1);
            v.Value(nameof(RoundWinner), RoundWinner);
            v.Begin(nameof(Rotation));
            Rotation.Visit(ref v);
            v.End();
        }
    }
}
