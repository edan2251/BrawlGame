using System;

namespace Knockbox.Simulation
{
    /// <summary>경기 흐름(06-match-and-tools.md 1절). CP1은 Countdown → Fighting만 동작한다.</summary>
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
        }
    }
}
