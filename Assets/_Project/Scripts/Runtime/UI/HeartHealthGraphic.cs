using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// One icon per health point. Dots work without art; a sprite can replace them later.
[AddComponentMenu("")]
public class HeartHealthGraphic : MaskableGraphic
{
    private const int Segments = 64;
    private static readonly Vector2[] Contour = CreateContour();
    private int current, maximum, columns = 10;
    private float size = 32f, gap = 8f;
    private Color fill, outline, empty;
    private Sprite icon;
    private Vector2[] iconVertices, iconUV;
    private ushort[] iconTriangles;
    private Sprite emptyIcon;
    private readonly List<Image> spriteImages = new List<Image>();
    public override Texture mainTexture => icon != null ? icon.texture : base.mainTexture;

    public void Configure(int health, int capacity, float heartSize, float spacing,
        int heartsPerRow, Color filledColor, Color outlineColor, Color emptyColor, Sprite iconSprite, Sprite emptySprite = null)
    {
        capacity = Mathf.Max(0, capacity);
        health = Mathf.Clamp(health, 0, capacity);
        heartSize = Mathf.Max(12f, heartSize);
        spacing = Mathf.Max(0f, spacing);
        heartsPerRow = Mathf.Clamp(heartsPerRow, 1, 20);
        raycastTarget = false;
        if (current == health && maximum == capacity && size == heartSize && gap == spacing &&
            icon == iconSprite && emptyIcon == emptySprite && columns == heartsPerRow && fill == filledColor && outline == outlineColor && empty == emptyColor)
            return;

        if (icon != iconSprite)
        {
            icon = iconSprite;
            iconVertices = icon != null ? icon.vertices : null;
            iconUV = icon != null ? icon.uv : null;
            iconTriangles = icon != null ? icon.triangles : null;
            SetMaterialDirty();
        }
        current = health;
        emptyIcon = emptySprite;
        maximum = capacity;
        size = heartSize;
        gap = spacing;
        columns = heartsPerRow;
        fill = filledColor;
        outline = outlineColor;
        empty = emptyColor;

        int rows = maximum == 0 ? 0 : (maximum - 1) / columns + 1;
        int usedColumns = Mathf.Min(maximum, columns);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            usedColumns * size + Mathf.Max(0, usedColumns - 1) * gap);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            rows * size + Mathf.Max(0, rows - 1) * gap);
        RefreshSpriteImages();
        SetVerticesDirty();
    }

    public void Clear()
    {
        current = maximum = 0;
        RefreshSpriteImages();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (icon != null && emptyIcon != null) return;
        Rect rect = rectTransform.rect;
        for (int heart = 0; heart < maximum; heart++)
        {
            Vector2 center = new Vector2(rect.xMin + size * 0.5f + (heart % columns) * (size + gap),
                rect.yMax - size * 0.5f - (heart / columns) * (size + gap));
            if (icon != null) AddIcon(mesh, center, heart < current);
            else AddDot(mesh, center, heart < current);
        }
    }

    // Separate Images allow full/empty art to use different textures without
    // building an atlas or requiring readable source textures.
    private void RefreshSpriteImages()
    {
        bool useSprites = icon != null && emptyIcon != null;
        while (useSprites && spriteImages.Count < maximum)
        {
            var item = new GameObject("Health Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.hideFlags = HideFlags.HideAndDontSave;
            item.layer = gameObject.layer;
            item.transform.SetParent(transform, false);
            var image = item.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            spriteImages.Add(image);
        }
        for (int i = 0; i < spriteImages.Count; i++)
        {
            Image image = spriteImages[i];
            if (image == null) continue;
            bool visible = useSprites && i < maximum;
            image.gameObject.SetActive(visible);
            if (!visible) continue;
            image.sprite = i < current ? icon : emptyIcon;
            image.color = (i < current ? fill : empty) * color;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = new Vector2(i % columns * (size + gap), -(i / columns) * (size + gap));
        }
    }

    protected override void OnDisable()
    {
        foreach (Image image in spriteImages)
            if (image != null)
            {
                if (Application.isPlaying) Destroy(image.gameObject);
                else DestroyImmediate(image.gameObject);
            }
        spriteImages.Clear();
        // Rebuild after enabling or an editor assembly reload, even if HP did not change.
        maximum = -1;
        base.OnDisable();
    }

    private void AddDot(VertexHelper mesh, Vector2 center, bool isFilled)
    {
        // An actual hollow outline for lost health, with no background rectangle.
        int start = mesh.currentVertCount;
        Color32 edgeColor = (isFilled ? outline : empty) * color;
        for (int i = 0; i < Segments; i++)
        {
            mesh.AddVert(center + Contour[i] * size, edgeColor, Vector2.zero);
            mesh.AddVert(center + Contour[i] * (size * 0.80f), edgeColor, Vector2.zero);
        }
        for (int i = 0; i < Segments; i++)
        {
            int a = start + i * 2;
            int b = start + ((i + 1) % Segments) * 2;
            mesh.AddTriangle(a, b, a + 1);
            mesh.AddTriangle(a + 1, b, b + 1);
        }
        if (!isFilled) return;

        start = mesh.currentVertCount;
        Color32 fillColor = fill * color;
        mesh.AddVert(center, fillColor, Vector2.zero);
        for (int i = 0; i < Segments; i++)
            mesh.AddVert(center + Contour[i] * (size * 0.80f), fillColor, Vector2.zero);
        for (int i = 0; i < Segments; i++)
            mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % Segments);
    }

    private void AddIcon(VertexHelper mesh, Vector2 center, bool isFilled)
    {
        int start = mesh.currentVertCount;
        Color32 tint = (isFilled ? fill : empty) * color;
        // Sprite mesh/UVs also support tightly packed or rotated atlas artwork.
        float scale = size / Mathf.Max(icon.rect.width, icon.rect.height);
        Vector2 offset = icon.pivot - icon.rect.size * 0.5f;
        for (int i = 0; i < iconVertices.Length; i++)
            mesh.AddVert(center + (iconVertices[i] * icon.pixelsPerUnit + offset) * scale,
                tint, iconUV[i]);
        for (int i = 0; i < iconTriangles.Length; i += 3)
            mesh.AddTriangle(start + iconTriangles[i], start + iconTriangles[i + 1],
                start + iconTriangles[i + 2]);
    }

    private static Vector2[] CreateContour()
    {
        var points = new Vector2[Segments];
        for (int i = 0; i < Segments; i++)
        {
            float t = i * Mathf.PI * 2f / Segments;
            points[i] = new Vector2(Mathf.Sin(t), Mathf.Cos(t)) * 0.5f;
        }
        return points;
    }
}
