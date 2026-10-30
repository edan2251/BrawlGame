using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 효과음 묶음(05-presentation.md 7절). 파일은 Tools/sfx_synth.py가 만든 WAV(Assets/_Project/Audio/Sfx).
    /// CP8에서 나머지 효과음을 더한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Knockbox/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip jump;
        public AudioClip land;
        public AudioClip dash;

        public AudioClip Get(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Jump: return jump;
                case Sfx.Land: return land;
                case Sfx.Dash: return dash;
                default: return null;
            }
        }
    }
}
