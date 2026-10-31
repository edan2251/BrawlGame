using System;
using System.Collections.Generic;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>
    /// 맵 짝 파일 `Data/Arenas/<이름>.json`(맵 형식 v2, 04-arena.md 6절). 연출과 경기 순환만 쓴다.
    /// 시뮬레이션은 격자(`<이름>.txt`)와 구조물만 쓴다. 구조물 목록은 CP13부터 채운다.
    /// <code>{ "theme": "A", "light": [0, 25], "rotation": true, "props": [] }</code>
    /// </summary>
    public sealed class ArenaMeta
    {
        /// <summary>0 A 민트, 1 B 사이언, 2 C 슬레이트(D-022). 짝 파일이 없으면 0.</summary>
        public int Theme;
        /// <summary>주 광원 위치. 없으면 연출 기본값(경기장 위쪽 끝에서 경기장 높이의 3/4 위).</summary>
        public bool HasLight;
        public Vector2 Light;
        /// <summary>경기 순환(라운드마다 맵 교체)에 들어가는 맵인지. v1 맵(테스트 고정 자료)은 false.</summary>
        public bool Rotation;

        public static readonly ArenaMeta Default = new ArenaMeta();

        public static ArenaMeta Parse(string json, string sourceName)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                throw new FormatException($"{sourceName}: 맵 짝 파일은 JSON 객체여야 함");
            var meta = new ArenaMeta();
            if (root.TryGetValue("theme", out object theme) && theme != null)
            {
                int index = ScenarioJson.ParseTheme(theme as string ?? throw new FormatException($"{sourceName}: \"theme\"은 문자열"));
                meta.Theme = Mathf.Max(0, index);
            }
            if (root.TryGetValue("light", out object light) && light != null)
            {
                if (!(light is List<object> xy) || xy.Count != 2 || !(xy[0] is double x) || !(xy[1] is double y))
                    throw new FormatException($"{sourceName}: \"light\"는 [x, y] 두 숫자");
                meta.HasLight = true;
                meta.Light = new Vector2((float)x, (float)y);
            }
            if (root.TryGetValue("rotation", out object rotation) && rotation != null)
                meta.Rotation = rotation is bool b ? b : throw new FormatException($"{sourceName}: \"rotation\"은 참/거짓");
            if (root.TryGetValue("props", out object props) && props != null && !(props is List<object>))
                throw new FormatException($"{sourceName}: \"props\"는 배열");
            return meta;
        }
    }
}
