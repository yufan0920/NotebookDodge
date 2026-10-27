using UnityEngine;

// A one-shot muzzle accent. Triggered by actual launch, never by the warning timer.
public sealed class MuzzleSparkVfx : MonoBehaviour
{
    [SerializeField] private Texture2D sheet;
    [SerializeField] private Shader tintShader;
    [SerializeField, Min(1)] private int frameCount = 16;
    [SerializeField, Min(1)] private int columns = 8;
    [SerializeField, Min(1)] private int cellSize = 64;
    [SerializeField, Min(0.01f)] private float duration = 0.2f;
    [SerializeField, Min(0.01f), Tooltip("Width of the full frame in world units; the artwork occupies part of it.")]
    private float size = 0.55f;
    [SerializeField] private bool matchBulletColor = true;
    [SerializeField] private Color tint = new Color(0.16f, 0.32f, 0.22f, 1);
    [SerializeField, Range(0, 1)] private float opacity = 0.9f;
    [SerializeField] private bool blendFrames = true;
    [SerializeField] private string sortingLayer = "Emitter";
    [SerializeField] private int sortingOrder = 10;

    private Mesh mesh;
    private Material material;
    private MeshRenderer flash;
    private Transform visual;
    private Color activeColor;
    private float startedAt;
    private bool playing;
    private readonly Vector2[] uv = new Vector2[4];

    public void Play(Transform muzzle, Vector2 position, Vector2 heading, Color bulletColor)
    {
        if (!isActiveAndEnabled || muzzle == null) return;
        if (sheet == null || tintShader == null || !tintShader.isSupported || columns < 1 ||
            frameCount < 1 || cellSize < 1 || sheet.width != columns * cellSize ||
            sheet.height % cellSize != 0 || frameCount > columns * (sheet.height / cellSize))
        {
            Debug.LogWarning("Muzzle burst sheet layout or shader is invalid.", this);
            return;
        }
        if (flash == null)
        {
            GameObject child = new GameObject("Muzzle Burst Flash");
            child.layer = gameObject.layer;
            visual = child.transform;
            mesh = new Mesh { name = "Muzzle burst frame" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh.MarkDynamic();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(tintShader);
            flash = child.AddComponent<MeshRenderer>();
            flash.sharedMaterial = material;
            flash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flash.receiveShadows = false;
        }
        // Reuse one visual per emitter; overlapping shots restart this short accent.
        visual.SetParent(muzzle, false);
        visual.position = new Vector3(position.x, position.y, 0);
        visual.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
        Vector3 parentScale = muzzle.lossyScale;
        visual.localScale = new Vector3(size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            size / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1);
        flash.sortingLayerName = sortingLayer;
        flash.sortingOrder = sortingOrder;
        material.mainTexture = sheet;
        activeColor = matchBulletColor ? bulletColor : tint;
        startedAt = Time.time;
        playing = true;
        flash.enabled = true;
        RenderFrame(0);
    }

    private void LateUpdate()
    {
        if (!playing) return;
        float progress = (Time.time - startedAt) / Mathf.Max(0.01f, duration);
        if (progress >= 1) { playing = false; if (flash != null) flash.enabled = false; return; }
        if (flash == null) { playing = false; return; }
        RenderFrame(progress);
    }

    private void RenderFrame(float progress)
    {
        float cursor = Mathf.Clamp01(progress) * frameCount;
        int frame = Mathf.Min(Mathf.FloorToInt(cursor), frameCount - 1);
        Rect rect = SpriteSheetUv.GridFrame(frame, columns, cellSize, sheet.height);
        SpriteSheetUv.WriteQuad(uv, rect, sheet.width, sheet.height);
        mesh.uv = uv;
        int next = Mathf.Min(frame + 1, frameCount - 1);
        material.SetVector("_NextFrameOffset", SpriteSheetUv.BlendOffset(rect,
            SpriteSheetUv.GridFrame(next, columns, cellSize, sheet.height), sheet.width, sheet.height));
        material.SetFloat("_FrameBlend", blendFrames ? cursor - frame : 0);
        Color color = activeColor;
        color.a *= opacity * Mathf.Clamp01((1 - progress) / 0.2f);
        material.color = color;
    }

    private void OnDisable()
    {
        playing = false;
        if (flash != null) flash.enabled = false;
    }
    private void OnDestroy()
    {
        if (visual != null) Destroy(visual.gameObject);
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
