using UnityEngine;

namespace GravityPrototype
{
    /// <summary>BIRP-only presentation pass. Camera projection and all physics remain unchanged.</summary>
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public sealed class PixelViewport : MonoBehaviour
    {
        [SerializeField, Range(120, 540)] private int verticalPixels = 216;
        private Camera sceneCamera;
        private void Awake() => sceneCamera = GetComponent<Camera>();

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (sceneCamera == null) sceneCamera = GetComponent<Camera>();
            int desiredHeight = sceneCamera.orthographic
                ? Mathf.RoundToInt(sceneCamera.orthographicSize * 2f * PixelMetrics.PixelsPerUnit)
                : verticalPixels;
            int height = Mathf.Min(desiredHeight, source.height);
            int width = Mathf.Max(1, Mathf.RoundToInt(height * (float)source.width / source.height));
            RenderTexture pixel = RenderTexture.GetTemporary(width, height, 0, source.format);
            pixel.filterMode = FilterMode.Point;
            pixel.wrapMode = TextureWrapMode.Clamp;
            try
            {
                Graphics.Blit(source, pixel);
                Graphics.Blit(pixel, destination);
            }
            finally { RenderTexture.ReleaseTemporary(pixel); }
        }
    }
}
