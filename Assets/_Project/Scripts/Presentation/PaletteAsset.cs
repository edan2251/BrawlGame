using System;
using UnityEngine;

namespace Knockbox.Presentation
{
    /// <summary>
    /// 맵 테마 하나(08-visual-direction.md 2절). 빛 색은 배경만 물들이고, 고정 지형은 밝게, 움직이는 구조물은 같은 계열의 어둡고 탁한 색.
    /// </summary>
    [Serializable]
    public struct ArenaTheme
    {
        public string name;
        [Tooltip("광원이 배경을 비추는 색(지형과 반대쪽 계열)")] public Color light;
        [Tooltip("고정 지형(밟아도 안전)")] public Color terrain;
        [Tooltip("움직이는 구조물과 줄(CP13부터)")] public Color prop;

        public ArenaTheme(string name, Color light, Color terrain, Color prop)
        {
            this.name = name;
            this.light = light;
            this.terrain = terrain;
            this.prop = prop;
        }
    }

    /// <summary>
    /// 색 토큰(08-visual-direction.md 2절, 사용자 팔레트에서 추출, D-021). 연출 코드는 색을 직접 쓰지 않고 여기서만 읽는다.
    /// 값은 08 6절의 검사 기준(대비, 흑백 구분)을 지키는 범위에서 조정할 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "Knockbox/Palette", fileName = "Palette")]
    public sealed class PaletteAsset : ScriptableObject
    {
        /// <summary>토큰 구성이 바뀌면 올린다. ProjectSetup이 낮은 버전의 에셋을 새 기본값으로 바꾼다.</summary>
        public const int CurrentVersion = 2;
        /// <summary>
        /// 에셋의 토큰 버전. 초기값을 두지 않는다: 이 필드가 없던 예전 에셋은 0으로 읽혀야 업그레이드 대상으로 잡힌다
        /// (필드 초기값이 있으면 저장 안 된 필드에 그 값이 들어간다).
        /// </summary>
        public int version;

        [Header("공통")]
        [Tooltip("배경 바탕, 그림자 영역")] public Color bgBase = Hex(0x030512);
        [Tooltip("캐릭터 외곽선, 팔다리, 눈")] public Color ink = Hex(0x020304);
        public Color p1Body = Hex(0xFFD466);
        public Color p1Shade = Hex(0xD1AE54);
        public Color p2Body = Hex(0xCB49FF);
        public Color p2Shade = Hex(0xAD1FFF);
        [Tooltip("섬광, 글자")] public Color flash = Hex(0xFFFFFF);
        [Tooltip("피격 파편, 체력 감소분, 가시, 돌진 위험 외곽선. 다른 용도로 쓰지 않는다")] public Color hit = Hex(0xFF0A14);
        [Tooltip("메인 색: 충전 단계, 패링 고리, 클래시 빛, 승리 표시")] public Color energy = Hex(0x14F0DB);
        [Tooltip("충전 1단계, UI 강조")] public Color energySoft = Hex(0x3DE0CF);
        [Tooltip("빛줄기 속 먼지 입자")] public Color mote = Hex(0x5CC2D6);
        [Tooltip("보조 글자, 먼지")] public Color uiSub = Hex(0x8C949E);
        [Tooltip("빈 점수 점, 체력 바 바탕")] public Color uiEmpty = Hex(0x667885);

        [Header("맵 테마 (D-022)")]
        public ArenaTheme[] themes =
        {
            new ArenaTheme("A mint", Hex(0x330947), Hex(0x11C1B3), Hex(0x225857)),
            new ArenaTheme("B cyan", Hex(0x330947), Hex(0x0AB2E6), Hex(0x1E4859)),
            new ArenaTheme("C slate", Hex(0x033642), Hex(0x8C949E), Hex(0x574F3E)),
        };

        public int ThemeCount => themes != null ? themes.Length : 0;

        public ArenaTheme Theme(int index) => themes[Mathf.Clamp(index, 0, themes.Length - 1)];

        public Color FighterBody(int fighter) => fighter == 0 ? p1Body : p2Body;
        public Color FighterShade(int fighter) => fighter == 0 ? p1Shade : p2Shade;

        /// <summary>색에서 알파만 바꾼 것(투명한 색도 토큰에서 만든다).</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>
        /// TextMesh(내장 글꼴 셰이더)는 정점 색을 색 공간에 맞게 바꾸지 않아 Linear 프로젝트에서 밝게 바랜다.
        /// 글자 색은 이 함수를 거쳐 스프라이트와 같은 색으로 보이게 한다.
        /// </summary>
        public static Color ForText(Color color) => QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;

        /// <summary>메시 정점 색도 TextMesh와 같이 색 공간 변환 없이 셰이더로 간다. 직접 만든 메시는 이 함수를 거친다.</summary>
        public static Color ForVertex(Color color) => ForText(color);

        static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
