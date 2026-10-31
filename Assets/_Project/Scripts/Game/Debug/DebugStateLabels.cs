using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 디버그 글자: 왼쪽 위에 틱과 경기 단계, 왼쪽 아래에 맵·모드·P2 입력원, 파이터 위에 행동 상태와 남은 경직·히트스탑.
    /// 캡처에도 찍히도록 월드 공간 TextMesh로 그린다. 판정 원과 몸 상자는 F3 HitboxOverlay가 그린다.
    /// </summary>
    public sealed class DebugStateLabels : MonoBehaviour
    {
        const int SortingOrder = 100;
        const float CharacterSize = 0.12f;
        const int FontSize = 48;

        TextMesh header;
        TextMesh footer;
        TextMesh label0;
        TextMesh label1;

        public void Setup(Color color)
        {
            header = Create("Header", color, TextAnchor.UpperLeft);
            header.transform.localPosition = new Vector3(-15.8f, 8.85f, 0f);
            footer = Create("Footer", color, TextAnchor.LowerLeft);
            footer.transform.localPosition = new Vector3(-15.8f, -8.95f, 0f);
            label0 = Create("P1", color, TextAnchor.LowerCenter);
            label1 = Create("P2", color, TextAnchor.LowerCenter);
        }

        TextMesh Create(string objectName, Color color, TextAnchor anchor)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            go.GetComponent<MeshRenderer>().sortingLayerName = Knockbox.Presentation.SortingLayers.UI;
            go.GetComponent<MeshRenderer>().sortingOrder = SortingOrder;
            text.fontSize = FontSize;
            text.characterSize = CharacterSize;
            text.anchor = anchor;
            text.color = color;
            return text;
        }

        int shownTick = -1;
        int shownKey0 = -1;
        int shownKey1 = -1;
        string shownFooter;

        /// <summary>값이 바뀔 때만 글자를 새로 만든다(디버그 글자라도 매 프레임 할당은 피한다). footer는 참조가 바뀔 때만.</summary>
        public void Render(in SimState state, Vector3 fighter0, Vector3 fighter1, string footerText)
        {
            if (state.Tick != shownTick)
            {
                shownTick = state.Tick;
                header.text = $"tick {state.Tick}  {state.Match.Phase}  round {state.Match.Round}";
            }
            if (!ReferenceEquals(footerText, shownFooter))
            {
                shownFooter = footerText;
                footer.text = footerText;
            }
            UpdateLabel(label0, "P1", state.Fighter0, ref shownKey0);
            UpdateLabel(label1, "P2", state.Fighter1, ref shownKey1);
            Place(label0, fighter0);
            Place(label1, fighter1);
        }

        static void UpdateLabel(TextMesh label, string name, in FighterState f, ref int shownKey)
        {
            int key = (int)f.Action | (f.StunTicks << 8) | (f.HitstopTicks << 20);
            if (key == shownKey)
                return;
            shownKey = key;
            string text = $"{name} {f.Action}";
            if (f.StunTicks > 0)
                text += $" {f.StunTicks}";
            if (f.HitstopTicks > 0)
                text += $" hs{f.HitstopTicks}";
            label.text = text;
        }

        static void Place(TextMesh label, Vector3 position)
        {
            // 화면 가장자리에서 잘리지 않게 가로 위치만 안쪽으로 당긴다. 체력 바 위에 둔다.
            label.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -14.5f, 14.5f), position.y + 1.3f, 0f);
        }
    }
}
