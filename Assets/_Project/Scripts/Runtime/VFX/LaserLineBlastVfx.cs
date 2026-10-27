using System.Collections.Generic;
using UnityEngine;

// Crisp Juice beam visual. LaserBeam still owns warning, timing, and damage.
[RequireComponent(typeof(LaserBeam))]
public sealed class LaserLineBlastVfx : MonoBehaviour
{
    [SerializeField] private Texture2D sheet;
    [SerializeField, Min(1)] private int frameCount = 16;
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int cellSize = 192;
    [SerializeField, Min(1f)] private float framesPerSecond = 30f;
    [SerializeField, Min(1), Tooltip("First opaque frame used while the beam exists. Frame numbers start at 1.")]
    private int sustainFirstFrame = 4;
    [SerializeField, Min(1), Tooltip("Last opaque frame of the ping-pong loop. Excludes the sheet's disappearing tail.")]
    private int sustainLastFrame = 6;
    [SerializeField, Range(0.1f, 1f), Tooltip("How much of each source cell contains the visible line. Changes visual thickness only.")]
    private float lineFillOfCell = 0.44f;
    [SerializeField, Range(0f, 1f), Tooltip("Beam visual opacity; does not change laser damage.")]
    private float opacity = 0.8f;

    private readonly List<Vector3> vertices = new List<Vector3>(128);
    private readonly List<Vector2> uvs = new List<Vector2>(128);
    private readonly List<Color32> colors = new List<Color32>(128);
    private readonly List<int> triangles = new List<int>(192);
    private LaserBeam beam;
    private Mesh mesh;
    private Material material;
    private MeshRenderer effectRenderer;

    private void Awake()
    {
        beam = GetComponent<LaserBeam>();
        if (sheet == null || sheet.width < columns * cellSize || sheet.height < cellSize ||
            frameCount > columns * (sheet.height / cellSize))
        {
            Debug.LogWarning("Laser line blast needs its 192px Crisp Juice sprite sheet.", this);
            enabled = false;
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            enabled = false;
            return;
        }

        GameObject effect = new GameObject("Crisp Juice Line Blast");
        effect.layer = gameObject.layer;
        effect.transform.SetParent(transform, false);
        mesh = new Mesh { name = "Crisp Juice laser tiles" };
        mesh.MarkDynamic();
        effect.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(shader) { mainTexture = sheet };
        effectRenderer = effect.AddComponent<MeshRenderer>();
        effectRenderer.sharedMaterial = material;
        effectRenderer.sortingLayerName = "Bullet";
        effectRenderer.enabled = false;
    }

    private void LateUpdate()
    {
        if (effectRenderer == null) return;
        float length = beam.VisibleLength;
        if (length <= 0f || beam.VisibleAlpha <= 0f)
        {
            effectRenderer.enabled = false;
            return;
        }

        // The source's final frame is almost transparent. Its one-shot duration
        // must never end our beam: drawing/hold/fade are owned by LaserBeam.
        int first = Mathf.Clamp(sustainFirstFrame - 1, 0, frameCount - 1);
        int last = Mathf.Clamp(sustainLastFrame - 1, first, frameCount - 1);
        int step = beam.HasStartedDrawing
            ? Mathf.Max(0, Mathf.FloorToInt((Time.time - beam.DrawStartTime) * framesPerSecond)) : 0;
        int frame = GetSustainFrame(first, last, step);

        float tileSize = beam.VisibleWidth / Mathf.Max(0.1f, lineFillOfCell);
        Rect uvBounds = SpriteSheetUv.Bounds(
            SpriteSheetUv.GridFrame(frame, columns, cellSize, sheet.height), sheet.width, sheet.height);
        float u0 = uvBounds.xMin, u1 = uvBounds.xMax;
        float v0 = uvBounds.yMin, v1 = uvBounds.yMax;

        vertices.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();
        int count = Mathf.CeilToInt(length / tileSize);
        for (int i = 0; i < count; i++)
        {
            float left = i * tileSize;
            float right = Mathf.Min(length, left + tileSize);
            float tileFraction = (right - left) / tileSize;
            int index = vertices.Count;
            vertices.Add(new Vector3(left, -tileSize * 0.5f, 0f));
            vertices.Add(new Vector3(right, -tileSize * 0.5f, 0f));
            vertices.Add(new Vector3(right, tileSize * 0.5f, 0f));
            vertices.Add(new Vector3(left, tileSize * 0.5f, 0f));
            uvs.Add(new Vector2(u0, v0));
            uvs.Add(new Vector2(Mathf.Lerp(u0, u1, tileFraction), v0));
            uvs.Add(new Vector2(Mathf.Lerp(u0, u1, tileFraction), v1));
            uvs.Add(new Vector2(u0, v1));
            for (int vertex = 0; vertex < 4; vertex++) colors.Add(new Color32(255, 255, 255, 255));
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        material.color = new Color(1f, 1f, 1f, opacity * beam.VisibleAlpha);
        effectRenderer.sortingOrder = beam.VisualSortingOrder + 2;
        effectRenderer.enabled = true;
    }

    // Zero-based inclusive bounds. Ping-pong avoids a jump from last to first.
    internal static int GetSustainFrame(int first, int last, int step)
    {
        int span = last - first;
        if (span <= 0) return first;
        int offset = step % (span * 2);
        return first + (offset <= span ? offset : span * 2 - offset);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
