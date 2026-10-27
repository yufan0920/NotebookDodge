using UnityEngine;

// Only explosion presentation. TraceBullet or ImpactBullet owns gameplay and lifetime.
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: null, sourceClassName: "TraceExplosionVfx")]
public sealed class LandingExplosionVfx : MonoBehaviour
{
    [SerializeField, Tooltip("Keep the source artwork colors instead of replacing them with Tint or the bullet color.")]
    private bool useSheetColor;
    [SerializeField] private bool matchBulletColor = true;
    [SerializeField, Tooltip("Used when Match Bullet Color is off.")]
    private Color tint = new Color(0.77204883f, 0.58272517f, 0.9150943f, 1f);
    [SerializeField, Range(0f, 1f)] private float opacity = 1f;
    [SerializeField, Range(0.01f, 1f), Tooltip("Fraction of Explosion Visual Duration used for the final fade.")]
    private float fadeOutFraction = 0.25f;
    [SerializeField, Range(0f, 1f), Tooltip("0 plays evenly; 1 advances quickly at impact and slows toward the end. Does not change lifetime or damage.")]
    private float playbackEaseOut;
    [SerializeField, Range(0f, 1f), Tooltip("Slow start, faster finish. Applied after Ease Out; leave Ease Out at 0 for this mode.")]
    private float playbackEaseIn;
    [SerializeField, Tooltip("Crossfade adjacent sheet frames when using Tint. Softens slow playback without adding new drawings.")]
    private bool blendFrames;
    [Header("Sprite Sheet")]
    [UnityEngine.Serialization.FormerlySerializedAs("glowPulseSheet")]
    [SerializeField] private Texture2D sheet;
    [SerializeField, Tooltip("Optional sliced frames, in playback order. Overrides Sheet/Frame Count/Columns/Cell Size. Use equal square rectangles, centered pivots, one unpacked texture.")]
    private Sprite[] frameSprites;
    [SerializeField, Min(1)] private int frameCount = 24;
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int cellSize = 192;
    [SerializeField, Min(1f), Tooltip("Main ring radius in source pixels, used to align it with Explosion Radius.")]
    private float sourceRadius = 64f;
    [SerializeField, Min(0.01f), Tooltip("Visual size multiplier only; does not change the damage radius.")]
    private float visualScale = 1f;
    [SerializeField, Tooltip("Landing point within each cell, measured from bottom-left (0..1).")]
    private Vector2 sheetPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Shader tintShader;

    private TraceBullet bullet;
    private ImpactBullet impact;
    private Texture2D activeSheet;
    private Mesh mesh;
    private Material material;
    private MeshRenderer effectRenderer;
    private Color activeTint;
    private bool playing;
    private int activeFrameCount;
    private int activeCellSize;
    private readonly Vector2[] uvs = new Vector2[4];

    private void Awake()
    {
        bullet = GetComponent<TraceBullet>();
        impact = GetComponent<ImpactBullet>();
    }

    public bool TryPlay(Vector2 position, float radius, SpriteRenderer source)
    {
        if (!isActiveAndEnabled || (bullet == null && impact == null)) return false;
        if (playing) return true;
        bool slicedFrames = frameSprites != null && frameSprites.Length > 0;
        activeSheet = slicedFrames && frameSprites[0] != null ? frameSprites[0].texture : sheet;
        activeFrameCount = slicedFrames ? frameSprites.Length : frameCount;
        activeCellSize = slicedFrames && frameSprites[0] != null ? Mathf.RoundToInt(frameSprites[0].rect.width) : cellSize;
        Shader shader = useSheetColor ? Shader.Find("Sprites/Default") : tintShader;
        bool validLayout = slicedFrames ? ValidateSlicedFrames() :
            frameCount > 0 && columns > 0 && cellSize > 0 && activeSheet != null &&
            activeSheet.width == columns * cellSize && activeSheet.height % cellSize == 0 &&
            frameCount <= columns * (activeSheet.height / cellSize);
        if (!validLayout || shader == null || !shader.isSupported)
        {
            Debug.LogWarning("Explosion sheet layout or shader is invalid. Using the original ring.", this);
            return false;
        }

        GameObject effect = new GameObject("Crisp Juice Landing Effect");
        effect.layer = gameObject.layer;
        effect.transform.SetParent(transform, false);
        effect.transform.position = new Vector3(position.x, position.y, 0f);
        effect.transform.rotation = Quaternion.identity;
        Vector3 scale = transform.lossyScale;
        // Projectile scale varies by prefab; explosionRadius is in world units.
        effect.transform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.0001f, Mathf.Abs(scale.y)), 1f);

        // Align the brightest ring's maximum extent to the damage radius.
        // Glow's soft outer halo is decorative and can extend beyond it.
        float halfSize = radius * (activeCellSize * 0.5f) / Mathf.Max(1f, sourceRadius) * Mathf.Max(0.01f, visualScale);
        mesh = new Mesh { name = "Trace explosion sheet frame" };
        mesh.MarkDynamic();
        mesh.vertices = new[] {
            new Vector3(-halfSize, -halfSize, 0f), new Vector3(halfSize, -halfSize, 0f),
            new Vector3(halfSize, halfSize, 0f), new Vector3(-halfSize, halfSize, 0f)
        };
        Vector3[] vertices = mesh.vertices;
        Vector3 pivotOffset = new Vector3(0.5f - sheetPivot.x, 0.5f - sheetPivot.y, 0f) * (2f * halfSize);
        for (int i = 0; i < vertices.Length; i++) vertices[i] += pivotOffset;
        mesh.vertices = vertices;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.RecalculateBounds();
        effect.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(shader) { mainTexture = activeSheet };
        effectRenderer = effect.AddComponent<MeshRenderer>();
        effectRenderer.sharedMaterial = material;
        effectRenderer.sortingLayerID = source.sortingLayerID;
        effectRenderer.sortingOrder = source.sortingOrder + 1;
        activeTint = useSheetColor ? Color.white : (matchBulletColor ? source.color : tint);
        playing = true;
        RenderFrame(0f);
        return true;
    }

    private void LateUpdate()
    {
        if (playing) RenderFrame(bullet != null ? bullet.ExplosionProgress : impact.ExplosionProgress);
    }

    private void RenderFrame(float progress)
    {
        // Play all frames once inside the existing gameplay visual duration.
        // A looping source asset must not create another explosion or extend it.
        float time = Mathf.Clamp01(progress);
        // Remap only the sheet playback. Fade still follows real elapsed time.
        float sheetProgress = Mathf.Lerp(time, 1f - (1f - time) * (1f - time), playbackEaseOut);
        sheetProgress = Mathf.Lerp(sheetProgress, sheetProgress * sheetProgress, playbackEaseIn);
        int frame = FrameAtProgress(sheetProgress, activeFrameCount);
        Rect rect = FrameRect(frame);
        SpriteSheetUv.WriteQuad(uvs, rect, activeSheet.width, activeSheet.height);
        mesh.uv = uvs;
        if (!useSheetColor && material.HasProperty("_FrameBlend"))
        {
            Rect nextRect = FrameRect(Mathf.Min(frame + 1, activeFrameCount - 1));
            material.SetVector("_NextFrameOffset",
                SpriteSheetUv.BlendOffset(rect, nextRect, activeSheet.width, activeSheet.height));
            material.SetFloat("_FrameBlend", blendFrames
                ? Mathf.Clamp01(sheetProgress * activeFrameCount - frame) : 0f);
        }
        Color color = activeTint;
        color.a *= opacity * FadeAtProgress(progress, fadeOutFraction);
        material.color = color;
    }

    private bool ValidateSlicedFrames()
    {
        if (activeSheet == null || activeCellSize < 1) return false;
        foreach (Sprite sprite in frameSprites)
        {
            if (sprite == null || sprite.packed || sprite.texture != activeSheet) return false;
            Rect rect = sprite.rect;
            if (Mathf.Abs(rect.width - activeCellSize) > 0.01f ||
                Mathf.Abs(rect.height - activeCellSize) > 0.01f ||
                (sprite.pivot - rect.size * 0.5f).sqrMagnitude > 0.01f) return false;
        }
        return true;
    }

    private Rect FrameRect(int frame)
    {
        if (frameSprites != null && frameSprites.Length > 0) return frameSprites[frame].rect;
        return SpriteSheetUv.GridFrame(frame, columns, cellSize, activeSheet.height);
    }

    internal static int FrameAtProgress(float progress, int count)
        => Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * count), 0, count - 1);

    internal static float FadeAtProgress(float progress, float fraction)
        => Mathf.Clamp01((1f - progress) / Mathf.Clamp(fraction, 0.01f, 1f));

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
