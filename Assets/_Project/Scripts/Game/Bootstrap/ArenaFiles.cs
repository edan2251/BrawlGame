using System;
using System.IO;
using Knockbox.Simulation;
using UnityEngine;

namespace Knockbox.Game
{
    /// <summary>맵 텍스트 파일(Assets/_Project/Data/Arenas/*.txt)을 디스크에서 읽는다. 에디터와 테스트용.</summary>
    public static class ArenaFiles
    {
        public static string Folder => Path.Combine(Application.dataPath, "_Project", "Data", "Arenas");

        public static ArenaDef LoadByName(string name)
        {
            string path = Path.Combine(Folder, name + ".txt");
            if (!File.Exists(path))
                throw new FileNotFoundException($"맵 파일이 없음: {name}", path);
            return Parse(File.ReadAllText(path), name);
        }

        public static ArenaDef Parse(string text, string sourceName)
        {
            try
            {
                return ArenaParser.Parse(text);
            }
            catch (FormatException e)
            {
                throw new FormatException($"{sourceName}: {e.Message}", e);
            }
        }

        public static string[] ListNames()
        {
            if (!Directory.Exists(Folder))
                return new string[0];
            string[] files = Directory.GetFiles(Folder, "*.txt", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
                files[i] = Path.GetFileNameWithoutExtension(files[i]);
            Array.Sort(files, StringComparer.Ordinal);
            return files;
        }
    }
}
