#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Render an isolated preview without opening or saving over the user's working scene.
[InitializeOnLoad]
public static class LoginScenePreview
{
    private const string Request = "Temp/LoginScenePreview.request";

    static LoginScenePreview()
    {
        EditorApplication.update += CheckRequest;
    }

    private static void CheckRequest()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += () =>
        {
            Render();
        };
    }

    [MenuItem("Tools/Flooded/Preview Login Scene")]
    public static void Render()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/01.Scenes/LoginScene.unity");
        RenderTexture target = null;
        Texture2D capture = null;
        Camera camera = null;
        var previous = RenderTexture.active;
        try
        {
            var roots = scene.GetRootGameObjects();
            camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).First();
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            var canvas = roots.SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).First();
            target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            // Do not let preview animations hide the panels on their first frame.
            foreach (var group in roots.SelectMany(r => r.GetComponentsInChildren<CanvasGroup>(true)))
                group.alpha = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            capture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            capture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("Temp/LoginReview");
            File.WriteAllBytes("Temp/LoginReview/LoginScene.png", capture.EncodeToPNG());
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/04.Images/Login/QuarantineBackground.shader");
            var messages = ShaderUtil.GetShaderMessages(shader);
            File.WriteAllText("Temp/LoginReview/Validation.txt", "Preview rendered at 1920x1080.\n" +
                string.Join("\n", messages.Select(m => m.severity + ": " + m.message)));
            Debug.Log("Login preview: Temp/LoginReview/LoginScene.png");
        }
        finally
        {
            RenderTexture.active = previous;
            if (camera != null) camera.targetTexture = null;
            if (capture != null) Object.DestroyImmediate(capture);
            if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
#endif
