using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 효과음 묶음(05-presentation.md 7절). 파일은 Tools/sfx_synth.py가 만든 WAV(Assets/_Project/Audio/Sfx).
    /// 필드 이름이 WAV 파일 이름이다(ProjectSetup.EnsureSfxLibrary가 이름으로 연결). CP8에서 나머지 효과음을 더한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Knockbox/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public AudioClip jump;
        public AudioClip land;
        public AudioClip dash;
        public AudioClip hitWeak;
        public AudioClip hitStrong;
        public AudioClip clash;
        public AudioClip parry;
        public AudioClip guard;
        public AudioClip wallSplat;

        public AudioClip Get(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Jump: return jump;
                case Sfx.Land: return land;
                case Sfx.Dash: return dash;
                case Sfx.HitWeak: return hitWeak;
                case Sfx.HitStrong: return hitStrong;
                case Sfx.Clash: return clash;
                case Sfx.Parry: return parry;
                case Sfx.Guard: return guard;
                case Sfx.WallSplat: return wallSplat;
                default: return null;
            }
        }
    }
}
