using System;
using System.Collections.Generic;

namespace Knockbox.Simulation
{
    /// <summary>
    /// 맵 텍스트(04-arena.md 2절) → ArenaDef. 같은 종류의 인접 칸을 가로로 먼저 합치고,
    /// 같은 가로 구간을 가진 행끼리 세로로 합친다. 반 칸짜리 지형(=, B, ^)은 층이 생기지 않도록 가로로만 합친다.
    /// </summary>
    public static class ArenaParser
    {
        public const string Separator = "---";

        public static ArenaDef Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            string name = "";
            int gridStart = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line == Separator)
                {
                    gridStart = i + 1;
                    break;
                }
                if (line.StartsWith("name:", StringComparison.Ordinal))
                    name = line.Substring(5).Trim();
            }
            if (gridStart < 0)
                throw new FormatException($"'{Separator}' 구분선이 없음");
            if (name.Length == 0)
                throw new FormatException("'name:' 머리글이 없음");

            var grid = new List<string>();
            for (int i = gridStart; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length == 0)
                    continue;
                grid.Add(line);
            }
            return ParseGrid(name, grid);
        }

        static ArenaDef ParseGrid(string name, List<string> grid)
        {
            if (grid.Count != ArenaDef.Rows)
                throw new FormatException($"{name}: 행 수가 {grid.Count} (기대 {ArenaDef.Rows})");

            bool found1 = false, found2 = false;
            Vec2 spawn0 = default, spawn1 = default;
            for (int r = 0; r < ArenaDef.Rows; r++)
            {
                string row = grid[r];
                if (row.Length != ArenaDef.Columns)
                    throw new FormatException($"{name}: {r}행 길이가 {row.Length} (기대 {ArenaDef.Columns})");
                for (int c = 0; c < ArenaDef.Columns; c++)
                {
                    char ch = row[c];
                    bool border = r == 0 || r == ArenaDef.Rows - 1 || c == 0 || c == ArenaDef.Columns - 1;
                    if (border && ch != '#')
                        throw new FormatException($"{name}: 테두리 ({c}열, {r}행)가 '#'이 아님: '{ch}'");
                    switch (ch)
                    {
                        case '#':
                        case '=':
                        case 'B':
                        case '^':
                        case '.':
                            break;
                        case '1':
                            if (found1)
                                throw new FormatException($"{name}: 시작 위치 '1'이 둘 이상");
                            found1 = true;
                            spawn0 = CellFoot(c, r);
                            break;
                        case '2':
                            if (found2)
                                throw new FormatException($"{name}: 시작 위치 '2'가 둘 이상");
                            found2 = true;
                            spawn1 = CellFoot(c, r);
                            break;
                        default:
                            throw new FormatException($"{name}: 알 수 없는 문자 '{ch}' ({c}열, {r}행)");
                    }
                }
            }
            if (!found1 || !found2)
                throw new FormatException($"{name}: 시작 위치 '1'과 '2'가 각각 하나씩 있어야 함");

            var boxes = new List<ArenaBox>();
            MergeFullCells(grid, '#', TerrainKind.Solid, boxes);
            MergeHalfCells(grid, '=', TerrainKind.OneWay, top: true, boxes);
            MergeHalfCells(grid, 'B', TerrainKind.Bounce, top: false, boxes);
            MergeHalfCells(grid, '^', TerrainKind.Spike, top: false, boxes);
            return new ArenaDef(name, boxes.ToArray(), spawn0, spawn1);
        }

        static Vec2 CellFoot(int c, int r) => new Vec2(c - ArenaDef.HalfWidth + 0.5f, ArenaDef.HalfHeight - r - 1f);

        struct Run
        {
            public int Row;
            public int C0;
            public int C1;
        }

        static List<Run> HorizontalRuns(List<string> grid, char ch, int row)
        {
            var runs = new List<Run>();
            string line = grid[row];
            int c = 0;
            while (c < ArenaDef.Columns)
            {
                if (line[c] != ch)
                {
                    c++;
                    continue;
                }
                int start = c;
                while (c < ArenaDef.Columns && line[c] == ch)
                    c++;
                runs.Add(new Run { Row = row, C0 = start, C1 = c - 1 });
            }
            return runs;
        }

        /// <summary>가로로 합친 뒤, 바로 아래 행에 같은 구간이 있으면 세로로 이어 붙인다.</summary>
        static void MergeFullCells(List<string> grid, char ch, TerrainKind kind, List<ArenaBox> output)
        {
            var open = new List<(Run top, int bottomRow)>();
            for (int r = 0; r <= ArenaDef.Rows; r++)
            {
                var runs = r < ArenaDef.Rows ? HorizontalRuns(grid, ch, r) : new List<Run>();
                var next = new List<(Run top, int bottomRow)>();
                foreach (var run in runs)
                {
                    int match = open.FindIndex(o => o.top.C0 == run.C0 && o.top.C1 == run.C1 && o.bottomRow == r - 1);
                    if (match >= 0)
                    {
                        next.Add((open[match].top, r));
                        open.RemoveAt(match);
                    }
                    else
                    {
                        next.Add((run, r));
                    }
                }
                foreach (var closed in open)
                    output.Add(FullBox(kind, closed.top, closed.bottomRow));
                open = next;
            }
        }

        static ArenaBox FullBox(TerrainKind kind, Run top, int bottomRow) => new ArenaBox(
            kind,
            top.C0 - ArenaDef.HalfWidth,
            ArenaDef.HalfHeight - (bottomRow + 1),
            top.C1 + 1 - ArenaDef.HalfWidth,
            ArenaDef.HalfHeight - top.Row);

        static void MergeHalfCells(List<string> grid, char ch, TerrainKind kind, bool top, List<ArenaBox> output)
        {
            for (int r = 0; r < ArenaDef.Rows; r++)
            {
                foreach (var run in HorizontalRuns(grid, ch, r))
                {
                    float cellTop = ArenaDef.HalfHeight - r;
                    float cellBottom = cellTop - 1f;
                    output.Add(new ArenaBox(
                        kind,
                        run.C0 - ArenaDef.HalfWidth,
                        top ? cellTop - 0.5f : cellBottom,
                        run.C1 + 1 - ArenaDef.HalfWidth,
                        top ? cellTop : cellBottom + 0.5f));
                }
            }
        }
    }
}
