using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Knockbox.Game;
using Knockbox.Simulation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Knockbox.EditorTools
{
    /// <summary>
    /// Knockbox/Run Scenario Captures: Sandbox 씬을 플레이 모드로 열고 모든 showcase_ 시나리오를 재생해
    /// docs/progress/captures/&lt;라벨&gt;/에 캡처를 저장한 뒤 플레이 모드를 끝낸다. 라벨은 PROGRESS.md의 현재 CP.
    /// 에디터가 백그라운드면 플레이 모드가 멈추므로 Unity 창을 앞으로 가져와 실행한다(D-015).
    /// </summary>
    [InitializeOnLoad]
    public static class ScenarioCaptureMenu
    {
        const string PendingKey = "Knockbox.ScenarioCapture.Pending";
        const string LabelKey = "Knockbox.ScenarioCapture.Label";
        const string ProgressPath = "docs/progress/PROGRESS.md";

        static ScenarioCaptureMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Knockbox/Run Scenario Captures")]
        public static void Run() => Run(CurrentCheckpointLabel());

        public static void Run(string label)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Knockbox] 플레이 모드 중에는 캡처를 시작할 수 없음");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ProjectSetup.SandboxScenePath, OpenSceneMode.Single);
            SessionState.SetString(LabelKey, label);
            SessionState.SetBool(PendingKey, true);
            EditorApplication.isPlaying = true;
        }

        /// <summary>PROGRESS.md의 "| 현재 CP | CPn |" 줄에서 라벨을 읽는다. 없으면 "adhoc".</summary>
        public static string CurrentCheckpointLabel()
        {
            if (!File.Exists(ProgressPath))
                return "adhoc";
            var match = Regex.Match(File.ReadAllText(ProgressPath), @"\|\s*현재 CP\s*\|\s*(CP\d+)\s*\|");
            return match.Success ? match.Groups[1].Value : "adhoc";
        }

        public static List<ScenarioDef> LoadShowcases()
        {
            var list = new List<ScenarioDef>();
            var files = ScenarioJson.ListFiles();
            System.Array.Sort(files, System.StringComparer.Ordinal);
            foreach (string path in files)
            {
                if (Path.GetFileName(path).StartsWith("showcase_", System.StringComparison.Ordinal))
                    list.Add(ScenarioJson.LoadFile(path));
            }
            return list;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false))
                return;
            SessionState.SetBool(PendingKey, false);
            string label = SessionState.GetString(LabelKey, "adhoc");

            var player = Object.FindFirstObjectByType<ScenarioPlayer>();
            var camera = Camera.main;
            if (player == null || camera == null)
            {
                Debug.LogError("[Knockbox] Sandbox 씬에 ScenarioPlayer 또는 Main Camera가 없음");
                EditorApplication.isPlaying = false;
                return;
            }

            var scenarios = LoadShowcases();
            player.StartCoroutine(player.RunCaptures(scenarios, label, camera, saved =>
            {
                Debug.Log($"[Knockbox] 시나리오 캡처 {saved.Count}장 저장: {ScenarioPlayer.CaptureFolder(label)}\n" + string.Join("\n", saved));
                EditorApplication.isPlaying = false;
            }));
        }
    }
}
