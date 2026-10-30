using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 색 토큰(05-presentation.md 1절). 연출 코드는 색을 직접 쓰지 않고 여기서만 읽는다.
    /// 값은 대비와 조화를 위해 조정할 수 있지만, 두 플레이어가 흑백에서 구분되어야 한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Knockbox/Palette", fileName = "Palette")]
    public sealed class PaletteAsset : ScriptableObject
    {
        public Color ink = Hex(0x24283B);
        public Color terrainLip = Hex(0x3B4466);
        public Color bgBase = Hex(0xC9DCEB);
        public Color bgShape = Hex(0xB3CBDF);
        public Color shadow = Hex(0x9FB8CF);
        public Color p1Body = Hex(0xFFB627);
        public Color p1Shade = Hex(0xE08E0B);
        public Color p2Body = Hex(0x7C5CFF);
        public Color p2Shade = Hex(0x5A3BDB);
        public Color oneWay = Hex(0x6B7AA6);
        public Color bouncy = Hex(0x22C3A6);
        public Color hazard = Hex(0xF0475C);
        public Color flash = Hex(0xFFFFFF);
        public Color spark = Hex(0xFFF3B0);

        public Color FighterBody(int fighter) => fighter == 0 ? p1Body : p2Body;
        public Color FighterShade(int fighter) => fighter == 0 ? p1Shade : p2Shade;

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
