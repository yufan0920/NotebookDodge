using UnityEngine;

// Pure pixel/UV math shared by muzzle, landing and laser sheet players.
// Frame rectangles use Unity's bottom-left pixel coordinates; grid order is top-to-bottom.
internal static class SpriteSheetUv
{
    public static Rect GridFrame(int frame, int columns, int cellSize, int textureHeight)
        => new Rect(frame % columns * cellSize,
            textureHeight - (frame / columns + 1) * cellSize, cellSize, cellSize);

    public static Rect Bounds(Rect frame, int textureWidth, int textureHeight)
        => Rect.MinMaxRect((frame.xMin + 0.5f) / textureWidth, (frame.yMin + 0.5f) / textureHeight,
            (frame.xMax - 0.5f) / textureWidth, (frame.yMax - 0.5f) / textureHeight);

    public static void WriteQuad(Vector2[] uv, Rect frame, int textureWidth, int textureHeight)
    {
        Rect bounds = Bounds(frame, textureWidth, textureHeight);
        uv[0] = new Vector2(bounds.xMin, bounds.yMin);
        uv[1] = new Vector2(bounds.xMax, bounds.yMin);
        uv[2] = new Vector2(bounds.xMax, bounds.yMax);
        uv[3] = new Vector2(bounds.xMin, bounds.yMax);
    }

    public static Vector4 BlendOffset(Rect frame, Rect next, int textureWidth, int textureHeight)
        => new Vector4((next.x - frame.x) / textureWidth,
            (next.y - frame.y) / textureHeight, 0f, 0f);
}
