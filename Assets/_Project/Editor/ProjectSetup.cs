using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Knockbox.EditorTools
{
    /// <summary>
    /// CP0 골격 설정을 코드로 재현한다. 메뉴는 여러 번 실행해도 결과가 같다.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string MainScenePath = ScenesFolder + "/Main.unity";
        public const string SandboxScenePath = ScenesFolder + "/Sandbox.unity";
        const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";

        // 05-presentation.md: 직교 크기 9(세로 18 유닛), 배경 bgBase #C9DCEB.
        // PaletteAsset은 CP3에서 생긴다. 그 전까지 씬 생성기에만 이 값을 둔다.
        const float CameraOrthoSize = 9f;
        static readonly Color BgBase = new Color32(0xC9, 0xDC, 0xEB, 0xFF);

        [MenuItem("Knockbox/Setup/Run All")]
        public static void RunAll()
        {
            ApplyProjectSettings();
            CreateMissingScenes();
        }

        [MenuItem("Knockbox/Setup/Apply Project Settings")]
        public static void ApplyProjectSettings()
        {
            // 에디터가 포커스를 잃어도 플레이 모드 검증이 멈추지 않게 한다.
            PlayerSettings.runInBackground = true;

            // 품질 단계별 지정이 비어도 URP로 떨어지게 기본 파이프라인도 지정한다.
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
            {
                Debug.LogError($"[Knockbox] URP 에셋을 찾을 수 없음: {UrpAssetPath}");
                return;
            }
            if (GraphicsSettings.defaultRenderPipeline != urp)
                GraphicsSettings.defaultRenderPipeline = urp;

            AssetDatabase.SaveAssets();
            Debug.Log("[Knockbox] 프로젝트 설정 적용: runInBackground=true, defaultRenderPipeline=UniversalRP");
        }

        /// <summary>
        /// 없는 씬만 만든다. 이미 있는 씬은 건드리지 않는다(사람이 편집했을 수 있으므로).
        /// 다른 씬의 조명과 섞이지 않게 단일 모드로 만들기 때문에, 저장 안 된 씬이 열려 있으면 멈춘다.
        /// 끝나면 Main 씬을 연다.
        /// </summary>
        [MenuItem("Knockbox/Setup/Create Missing Scenes")]
        public static void CreateMissingScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.isDirty)
                {
                    Debug.LogError($"[Knockbox] 저장하지 않은 씬이 열려 있어 씬 생성을 멈춤: {open.name}");
                    return;
                }
            }

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");

            CreateSceneIfMissing(MainScenePath);
            CreateSceneIfMissing(SandboxScenePath);
            RegisterBuildScenes();
            AssetDatabase.SaveAssets();

            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        }

        static void CreateSceneIfMissing(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateGlobalLight();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[Knockbox] 씬 생성: {path}");
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = CameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgBase;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;

            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<AudioListener>();
        }

        static void CreateGlobalLight()
        {
            var go = new GameObject("Global Light 2D");
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.color = Color.white;
            light.intensity = 1f;
        }

        /// <summary>빌드 씬 목록을 Main, Sandbox 순서로 맞춘다. 다른 씬은 목록에서만 뺀다(파일은 그대로).</summary>
        static void RegisterBuildScenes()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(MainScenePath, true),
                new EditorBuildSettingsScene(SandboxScenePath, true),
            };
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
