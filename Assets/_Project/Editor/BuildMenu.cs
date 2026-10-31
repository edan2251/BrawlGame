using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Knockbox.EditorTools
{
    /// <summary>
    /// 비교용 Windows 개발 빌드(CHECKPOINTS.md CP9, 06-match-and-tools.md 6절).
    /// Main 씬 하나, 디버그 단축키 포함(개발 빌드), 출력은 프로젝트 루트의 Builds/(Git 제외).
    /// </summary>
    public static class BuildMenu
    {
        public const string OutputPath = "Builds/Windows/Knockbox.exe";

        [MenuItem("Knockbox/Build/Windows Development")]
        public static void BuildWindowsDevelopment()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.MainScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[Knockbox] 빌드 성공: {Path.GetFullPath(OutputPath)} ({summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0}초, 경고 {summary.totalWarnings})");
            else
                Debug.LogError($"[Knockbox] 빌드 실패: {summary.result}, 오류 {summary.totalErrors}");
        }
    }
}
