using System;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 파이터의 폼(10-forms-and-growth.md, D-035). 폼이 곧 기본공격이다.
    /// 가위 = 충전 돌진(구 모드 A), 바위 = 투사체(CP19), 보 = 근접 휘두르기(구 모드 B, CP20).
    /// Test는 CP4의 시험 공격을 쓰는 기존 테스트 전용이며 게임에서 고를 수 없다.
    /// </summary>
    public enum FighterForm : byte
    {
        Scissors,
        Rock,
        Paper,
        Test,
    }

    public static class Forms
    {
        /// <summary>게임에서 고를 수 있는 폼 수(가위, 바위, 보).</summary>
        public const int PlayableCount = 3;

        public static byte Bit(FighterForm form) => (byte)(1 << (int)form);

        public static bool Owns(byte mask, FighterForm form) => (mask & Bit(form)) != 0;

        /// <summary>CR-003 전의 전역 모드 → 시작 폼.</summary>
        public static FighterForm FromMode(CombatMode mode)
        {
            switch (mode)
            {
                case CombatMode.Charge: return FighterForm.Scissors;
                case CombatMode.Swing: return FighterForm.Paper;
                default: return FighterForm.Test;
            }
        }
    }

    /// <summary>
    /// 기능 스위치(10 문서 5.1). 가드·잡기는 한 스위치(D-043)지만, 기존 F4(잡기만 끔) 테스트를 위해 잡기를 따로 둔다.
    /// </summary>
    [Serializable]
    public struct FeatureSwitches
    {
        public bool GuardEnabled;
        public bool GrabEnabled;
        /// <summary>공격 에너지(10 문서 4.4). 끄면 비용 없이 공격한다.</summary>
        public bool EnergyEnabled;

        /// <summary>CR-003 기본: 가드·잡기 끔, 에너지 켬.</summary>
        public static FeatureSwitches Default => new FeatureSwitches { GuardEnabled = false, GrabEnabled = false, EnergyEnabled = true };

        /// <summary>CP16까지의 규칙(기존 행동 회귀 검사): 가드 켬, 잡기는 인자, 에너지 끔.</summary>
        public static FeatureSwitches Legacy(bool grabEnabled) => new FeatureSwitches { GuardEnabled = true, GrabEnabled = grabEnabled, EnergyEnabled = false };
    }

    /// <summary>시작 폼과 기능 스위치. 입력 기록에는 Format() 문자열로 남긴다.</summary>
    [Serializable]
    public struct RuleSet
    {
        public FighterForm StartForm;
        public FeatureSwitches Switches;

        public static RuleSet Default => new RuleSet { StartForm = FighterForm.Scissors, Switches = FeatureSwitches.Default };

        public static RuleSet Legacy(CombatMode mode, bool grabEnabled) =>
            new RuleSet { StartForm = Forms.FromMode(mode), Switches = FeatureSwitches.Legacy(grabEnabled) };

        /// <summary>예: "Scissors g0 r0 e1"(가드, 잡기, 에너지).</summary>
        public readonly string Format() =>
            $"{StartForm} g{(Switches.GuardEnabled ? 1 : 0)} r{(Switches.GrabEnabled ? 1 : 0)} e{(Switches.EnergyEnabled ? 1 : 0)}";

        public static bool TryParse(string text, out RuleSet rules)
        {
            rules = Default;
            if (string.IsNullOrEmpty(text))
                return false;
            string[] parts = text.Split(' ');
            if (parts.Length != 4 || !Enum.TryParse(parts[0], false, out FighterForm form) || !Enum.IsDefined(typeof(FighterForm), form))
                return false;
            if (!Flag(parts[1], 'g', out bool guard) || !Flag(parts[2], 'r', out bool grab) || !Flag(parts[3], 'e', out bool energy))
                return false;
            rules = new RuleSet
            {
                StartForm = form,
                Switches = new FeatureSwitches { GuardEnabled = guard, GrabEnabled = grab, EnergyEnabled = energy },
            };
            return true;
        }

        static bool Flag(string part, char key, out bool value)
        {
            value = part.Length == 2 && part[1] == '1';
            return part.Length == 2 && part[0] == key && (part[1] == '0' || part[1] == '1');
        }
    }
}
