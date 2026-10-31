using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 전투 튜닝 데이터(03-combat.md). 값은 시뮬레이션의 CombatParams를 그대로 직렬화한다.
    /// 플레이 중에 바꾼 값은 라운드 리셋(F5)이나 다음 라운드(RoundTransition) 때 반영된다.
    /// </summary>
    [CreateAssetMenu(menuName = "Knockbox/Combat Tuning", fileName = "CombatTuning")]
    public sealed class CombatTuning : ScriptableObject
    {
        public CombatParams Combat = CombatParams.Default;

        void Reset()
        {
            Combat = CombatParams.Default;
        }
    }
}
