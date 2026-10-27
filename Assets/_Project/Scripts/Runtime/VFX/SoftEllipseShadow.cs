using UnityEngine;

// Decorative ground shadow: concentric vertex-alpha rings, no drawn texture.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SoftEllipseShadow : MonoBehaviour
{
    [SerializeField, Tooltip("阴影宽、高，使用世界单位，不随父物体缩放变化。")]
    private Vector2 shadowSize = new Vector2(0.28f, 0.12f);
    [SerializeField, Tooltip("相对子弹中心的世界坐标偏移；Y负值放在碎屑下方。")]
    private Vector2 shadowOffset = new Vector2(0f, -0.055f);
    [SerializeField] private Color shadowColor = new Color(0.18f, 0.17f, 0.16f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("阴影中心的不透明度；外缘自动渐变为透明。")]
    private float opacity = 0.32f;

    private SpriteRenderer source;
    private Transform shadow;
    private MeshRenderer shadowRenderer;
    private Mesh mesh;
    private Material material;

    private void Awake() => source = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        if (shadow == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported) return;
            GameObject child = new GameObject("Fragment Soft Shadow");
            child.layer = gameObject.layer;
            shadow = child.transform;
            shadow.SetParent(transform, false);
            mesh = CreateGradientMesh();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            shadowRenderer = child.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = material;
        }
        shadow.gameObject.SetActive(true);
        RefreshShadow();
    }

    private static Mesh CreateGradientMesh()
    {
        const int segments = 32, rings = 8;
        Vector3[] vertices = new Vector3[1 + segments * rings];
        Color[] colors = new Color[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * (1 + 2 * (rings - 1)) * 3];
        colors[0] = Color.white;
        for (int r = 1; r <= rings; r++)
        {
            float radius = r / (float)rings;
            // Smooth both at the center and at the transparent outer edge.
            float falloff = 1f - radius * radius;
            float alpha = falloff * falloff;
            for (int j = 0; j < segments; j++)
            {
                int index = 1 + (r - 1) * segments + j;
                float angle = j * 2f * Mathf.PI / segments;
                vertices[index] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radius * 0.5f);
                colors[index] = new Color(1f, 1f, 1f, alpha);
            }
        }
        for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(0.5f, 0.5f);
        int nextTriangle = 0;
        for (int j = 0; j < segments; j++)
        {
            triangles[nextTriangle++] = 0;
            triangles[nextTriangle++] = 1 + j;
            triangles[nextTriangle++] = 1 + (j + 1) % segments;
        }
        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * segments, outer = 1 + r * segments;
            for (int j = 0; j < segments; j++)
            {
                int next = (j + 1) % segments;
                triangles[nextTriangle++] = inner + j;
                triangles[nextTriangle++] = outer + j;
                triangles[nextTriangle++] = outer + next;
                triangles[nextTriangle++] = inner + j;
                triangles[nextTriangle++] = outer + next;
                triangles[nextTriangle++] = inner + next;
            }
        }
        Mesh result = new Mesh { name = "Soft ellipse shadow", vertices = vertices,
            colors = colors, uv = uvs, triangles = triangles };
        result.RecalculateBounds();
        return result;
    }

    private void LateUpdate() => RefreshShadow();

    private void RefreshShadow()
    {
        if (shadow == null) return;
        shadow.position = transform.position + new Vector3(shadowOffset.x, shadowOffset.y, 0f);
        shadow.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        shadow.localScale = new Vector3(
            Mathf.Max(0.001f, shadowSize.x) / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            Mathf.Max(0.001f, shadowSize.y) / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 1f);
        shadowRenderer.sortingLayerID = source.sortingLayerID;
        shadowRenderer.sortingOrder = source.sortingOrder - 1;
        shadowRenderer.enabled = source.enabled;
        Color tint = shadowColor;
        tint.a *= Mathf.Clamp01(opacity) * source.color.a;
        material.color = tint;
    }

    private void OnDisable()
    {
        if (shadow != null) shadow.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        if (shadow != null) Destroy(shadow.gameObject);
    }
}
