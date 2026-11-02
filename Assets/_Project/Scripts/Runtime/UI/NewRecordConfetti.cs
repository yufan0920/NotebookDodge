using UnityEngine;
using UnityEngine.UI;

// UI sheet animation uses unscaled time so celebrations also work after death pauses gameplay.
[AddComponentMenu("UI/New Record Confetti")]
public class NewRecordConfetti : MaskableGraphic
{
    [SerializeField] private Texture2D sheet;
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int cellSize = 256;
    [SerializeField, Min(1)] private int frameCount = 48;
    [SerializeField, Min(1)] private float framesPerSecond = 30;
    [SerializeField, Min(0.1f)] private float duration = 4;
    [SerializeField, Min(0)] private float fadeOutDuration = 0.6f;
    [SerializeField, Min(64)] private float tileSize = 420;
    private float elapsed;
    private bool playing;

    public override Texture mainTexture => sheet != null ? sheet : base.mainTexture;

    public void Play()
    {
        if (sheet == null) return;
        elapsed = 0;
        playing = true;
        raycastTarget = false;
        SetVerticesDirty();
    }

    public void Stop()
    {
        playing = false;
        elapsed = 0;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!playing) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration) Stop();
        else SetVerticesDirty();
    }

    protected override void OnDisable()
    {
        Stop();
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (!playing || sheet == null) return;
        int cols = Mathf.Max(1, columns);
        int cell = Mathf.Max(1, cellSize);
        if (sheet.width < cols * cell || sheet.height < cell) return;
        int count = Mathf.Clamp(frameCount, 1, cols * (sheet.height / cell));
        int frame = Mathf.FloorToInt(elapsed * Mathf.Max(1, framesPerSecond)) % count;
        Color tint = color;
        float fade = Mathf.Min(Mathf.Max(0, fadeOutDuration), Mathf.Max(0.1f, duration));
        if (fade > 0) tint.a *= Mathf.Clamp01((duration - elapsed) / fade);
        Rect rect = rectTransform.rect;
        // Bound the tile count even if the Canvas is unusually large.
        float size = Mathf.Max(64, tileSize, rect.width / 32f, rect.height / 32f);
        int rows = Mathf.CeilToInt(rect.height / size);
        int tiles = Mathf.CeilToInt(rect.width / size);
        for (int x = 0; x < tiles; x++)
        {
            // Different column phases avoid an obvious repeated pattern.
            Rect pixels = SpriteSheetUv.GridFrame((frame + x * 11) % count, cols, cell, sheet.height);
            Rect uv = SpriteSheetUv.Bounds(pixels, sheet.width, sheet.height);
            float left = rect.xMin + x * size;
            float right = Mathf.Min(left + size, rect.xMax);
            float uRight = Mathf.Lerp(uv.xMin, uv.xMax, (right - left) / size);
            for (int y = 0; y < rows; y++)
            {
                float top = rect.yMax - y * size;
                float bottom = Mathf.Max(top - size, rect.yMin);
                float vBottom = Mathf.Lerp(uv.yMax, uv.yMin, (top - bottom) / size);
                int start = mesh.currentVertCount;
                mesh.AddVert(new Vector3(left, bottom), tint, new Vector2(uv.xMin, vBottom));
                mesh.AddVert(new Vector3(left, top), tint, new Vector2(uv.xMin, uv.yMax));
                mesh.AddVert(new Vector3(right, top), tint, new Vector2(uRight, uv.yMax));
                mesh.AddVert(new Vector3(right, bottom), tint, new Vector2(uRight, vBottom));
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start + 2, start + 3, start);
            }
        }
    }
}
