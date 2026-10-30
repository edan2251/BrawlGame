using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 디버그 글자: 왼쪽 위에 틱과 경기 단계, 파이터 위에 행동 상태. 캡처에도 찍히도록 월드 공간 TextMesh로 그린다.
    /// F3 판정 오버레이(CP4)가 생기면 그쪽으로 옮긴다.
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
        ActionState shown0 = (ActionState)255;
        ActionState shown1 = (ActionState)255;
        string shownArena;

        /// <summary>값이 바뀔 때만 글자를 새로 만든다(디버그 글자라도 매 프레임 할당은 피한다).</summary>
        public void Render(in SimState state, Vector3 fighter0, Vector3 fighter1, string arena)
        {
            if (state.Tick != shownTick)
            {
                shownTick = state.Tick;
                header.text = $"tick {state.Tick}  {state.Match.Phase}";
            }
            if (!ReferenceEquals(arena, shownArena))
            {
                shownArena = arena;
                footer.text = $"{arena} / mode -";
            }
            if (state.Fighter0.Action != shown0)
            {
                shown0 = state.Fighter0.Action;
                label0.text = $"P1 {shown0}";
            }
            if (state.Fighter1.Action != shown1)
            {
                shown1 = state.Fighter1.Action;
                label1.text = $"P2 {shown1}";
            }
            Place(label0, fighter0);
            Place(label1, fighter1);
        }

        static void Place(TextMesh label, Vector3 position)
        {
            // 화면 가장자리에서 잘리지 않게 가로 위치만 안쪽으로 당긴다. 체력 바 위에 둔다.
            label.transform.localPosition = new Vector3(Mathf.Clamp(position.x, -14.5f, 14.5f), position.y + 1.3f, 0f);
        }
    }
}
