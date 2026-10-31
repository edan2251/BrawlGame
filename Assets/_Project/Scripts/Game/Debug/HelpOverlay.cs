using Knockbox.Presentation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// `~` 단축키 도움말(06-match-and-tools.md 4·6절). 빌드에서는 처음 실행할 때 열려 있다.
    /// 화면 가운데의 잉크색 판과 밝은 영문 글자(D-016). 월드 공간이라 캡처에도 찍힌다.
    /// </summary>
    public sealed class HelpOverlay : MonoBehaviour
    {
        public const string Text =
            "KNOCKBOX  -  prototype controls\n" +
            "\n" +
            "P1  WASD move   SPACE jump   F attack   LSHIFT dash   E guard   Q grab\n" +
            "P2  ARROWS move   NUM0 jump   NUM1 attack   NUM2 dash   NUM3 guard   NUM4 grab\n" +
            "PAD  stick/dpad   A jump   X attack   B/RT dash   LB/LT guard   Y grab\n" +
            "\n" +
            "F1 combat mode (A charge / B swing, frozen)    F2 next arena\n" +
            "F3 hitbox overlay    F4 grab on/off    F5 reset round\n" +
            "F6 P2 input (keyboard / pad / dummy / bot)    [ ] dummy behavior\n" +
            "F7 reduce shake    F8 sync test    F11 tuning preset (default / fast)\n" +
            "F9 start/stop input recording    F10 replay last recording\n" +
            "P pause    O step one tick    F12 debug text    ~ close this help";

        TextMesh text;
        SpriteRenderer panel;

        public void Setup(PaletteAsset palette, ShapeLibrary shapes)
        {
            // 경기 화면(밝은 배경) 위에 확실히 떠 보이도록 불투명 잉크색 판에 밝은 글자.
            // 반투명이면 Linear 색 공간 혼합 때문에 뒤의 발판 선이 글자 사이로 비친다.
            panel = ShapeLibrary.CreateSliced(transform, "Panel", shapes.RoundedBody, SortingLayers.UI, 200, palette.ink);
            ShapeLibrary.SetSize(panel, 25f, 9.6f);
            var go = new GameObject("Text");
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = text.font.material;
            mr.sortingLayerName = SortingLayers.UI;
            mr.sortingOrder = 201;
            text.fontSize = 56;
            text.characterSize = 0.09f;
            text.lineSpacing = 1.15f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Left;
            text.color = PaletteAsset.ForText(palette.flash);
            text.text = Text;
            transform.localPosition = new Vector3(0f, 0.5f, 0f);
        }

        public bool Visible
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }
    }
}
