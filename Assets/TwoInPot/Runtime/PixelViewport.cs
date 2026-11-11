using UnityEngine;
using UnityEngine.UI;

namespace TwoInPot
{
    [RequireComponent(typeof(Camera))]
    public sealed class PixelViewport : MonoBehaviour
    {
        Camera view;
        RenderTexture target;
        GameObject output;
        RectTransform screenImage;
        int lastWidth, lastHeight;
#if UNITY_EDITOR || DEBUG
        bool forceProbeRender;
#endif
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            view = GetComponent<Camera>();
            target = new RenderTexture(640, 360, 16, RenderTextureFormat.ARGB32)
            { name = "TwoInPot Pixel View", filterMode = FilterMode.Point, antiAliasing = 1, useMipMap = false };
            target.Create(); view.targetTexture = target;
            output = new GameObject("Pixel Output", typeof(RectTransform), typeof(Canvas));
            var canvas = output.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -1000; canvas.pixelPerfect = true;
            var bars = new GameObject("Letterbox", typeof(RectTransform), typeof(RawImage)); bars.transform.SetParent(output.transform, false);
            var black = bars.GetComponent<RawImage>(); black.color = Color.black; black.raycastTarget = false;
            var full = bars.GetComponent<RectTransform>(); full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one; full.offsetMin = full.offsetMax = Vector2.zero;
            var image = new GameObject("World", typeof(RectTransform), typeof(RawImage)); image.transform.SetParent(output.transform, false);
            var raw = image.GetComponent<RawImage>(); raw.texture = target; raw.raycastTarget = false;
            screenImage = image.GetComponent<RectTransform>(); screenImage.anchorMin = screenImage.anchorMax = new Vector2(.5f, .5f);
            Resize();
#if UNITY_EDITOR || DEBUG
            forceProbeRender = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-potFestivalTests") >= 0;
#endif
        }
        void Resize()
        {
            lastWidth = Screen.width; lastHeight = Screen.height;
            float available = Mathf.Min(Screen.width / 640f, Screen.height / 360f);
            float scale = available < 1 ? available : Mathf.Floor(available);
            screenImage.sizeDelta = new Vector2(640 * scale, 360 * scale);
        }
        void LateUpdate()
        {
            if (target == null) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight) Resize();
#if UNITY_EDITOR || DEBUG
            // Hidden regression windows may skip automatic rendering. Force the world
            // camera so GPU measurements and captures exercise the actual scene.
            if (forceProbeRender) view.Render();
#endif
        }
        void OnDisable()
        {
            if (view != null) view.targetTexture = null;
            if (output != null) Destroy(output);
            if (target != null) { target.Release(); Destroy(target); target = null; }
        }
#if UNITY_EDITOR || DEBUG
        public void CaptureWorld(string path)
        {
            if (target == null) return;
            var previous = RenderTexture.active;
            var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
            try
            {
                view.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Destroy(image); }
        }
#endif
    }
}
