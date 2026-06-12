using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §6.4: the visual share card — a 1080×1350 composition (wordmark +
    /// the share string VERBATIM, so the image can never contain anything the
    /// text doesn't) rendered through an offscreen camera, then handed to the
    /// platform share path: Android ACTION_SEND text intent (the PNG is cached
    /// beside it until the FileProvider manifest lands with the SDK pass —
    /// DECISIONS.md), clipboard in the editor.
    /// </summary>
    public static class ShareCardRenderer
    {
        public const int Width = 1080;
        public const int Height = 1350;

        public static Texture2D Render(string shareText)
        {
            var rigGo = new GameObject("ShareCardRig");
            rigGo.transform.position = new Vector3(9000f, 9000f, 0f);
            try
            {
                var cam = rigGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = ThemeRuntime.Color("bg.abyss");
                cam.cullingMask = ~0;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 50f;

                var canvasGo = new GameObject("ShareCanvas");
                canvasGo.transform.SetParent(rigGo.transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(Width, Height);

                var canvasRoot = canvas.GetComponent<RectTransform>();
                RectTransform bg = UiComponents.Rect(canvasRoot, "bg", Vector2.zero);
                UiComponents.Stretch(bg);
                var bgImage = bg.gameObject.AddComponent<Image>();
                bgImage.sprite = SpriteFactory.Solid();
                bgImage.color = ThemeRuntime.Color("bg.deep");

                TextMeshProUGUI wordmark = UiText.Create(canvasRoot, "wordmark", "RIPTIDE",
                    "display", "accent.primary");
                UiComponents.Place(wordmark.rectTransform, new Vector2(0.5f, 0.88f), new Vector2(900f, 140f));

                RectTransform card = UiComponents.Card(canvasRoot, "card", new Vector2(940f, 760f));
                UiComponents.Place(card, new Vector2(0.5f, 0.47f), new Vector2(940f, 760f));
                TextMeshProUGUI body = UiText.Create(card, "body", shareText, "body", "text.primary");
                UiComponents.Stretch(body.rectTransform);

                var rt = RenderTexture.GetTemporary(Width, Height, 24);
                cam.targetTexture = rt;
                Canvas.ForceUpdateCanvases();
                cam.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);
                return tex;
            }
            finally
            {
                Object.Destroy(rigGo);
            }
        }

        /// <summary>Renders, caches the PNG, and fires the platform share path.</summary>
        public static string Share(string shareText)
        {
            Texture2D tex = Render(shareText);
            string path = Path.Combine(Application.persistentDataPath, "riptide_share.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);

#if UNITY_ANDROID && !UNITY_EDITOR
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
                intent.Call<AndroidJavaObject>("setType", "text/plain");
                intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), shareText);
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Share"))
                {
                    activity.Call("startActivity", chooser);
                }
            }
#else
            GUIUtility.systemCopyBuffer = shareText;
#endif
            return path;
        }
    }
}
