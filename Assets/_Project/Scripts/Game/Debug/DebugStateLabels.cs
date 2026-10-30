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
        TextMesh label0;
        TextMesh label1;

        public void Setup(Color color)
        {
            header = Create("Header", color, TextAnchor.UpperLeft);
            header.transform.localPosition = new Vector3(-15.8f, 8.85f, 0f);
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
            go.GetComponent<MeshRenderer>().sortingOrder = SortingOrder;
            text.fontSize = FontSize;
            text.characterSize = CharacterSize;
            text.anchor = anchor;
            text.color = color;
            return text;
        }

        public void Render(in SimState state, Vector3 fighter0, Vector3 fighter1)
        {
            header.text = $"tick {state.Tick}  {state.Match.Phase}";
            Place(label0, state.Fighter0, fighter0, "P1");
            Place(label1, state.Fighter1, fighter1, "P2");
        }

        static void Place(TextMesh label, in FighterState f, Vector3 position, string name)
        {
            label.text = $"{name} {f.Action}";
            label.transform.localPosition = position + new Vector3(0f, 0.65f, 0f);
        }
    }
}
