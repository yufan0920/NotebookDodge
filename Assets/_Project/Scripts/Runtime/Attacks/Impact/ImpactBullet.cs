using UnityEngine;

public class ImpactBullet : MonoBehaviour
{
    [Header("Warning")]
    [SerializeField, Min(0)] private float warningDuration = 1f;
    [SerializeField] private Color warningColor = new Color(1, 0.65f, 0.15f, 0.8f);
    [SerializeField, Min(0.001f)] private float warningWidth = 0.035f;
    [Header("Fall")]
    [SerializeField] private SpriteRenderer fallingVisual;
    [SerializeField, Min(0)] private float fallHeight = 3f;
    [SerializeField, Min(0.01f)] private float fallDuration = 0.3f;
    [SerializeField] private AnimationCurve fallCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
    [Header("Explosion")]
    [SerializeField, Min(0.01f)] private float explosionRadius = 1f;
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float explosionVisualDuration = 0.25f;
    [Header("Fragments")]
    [SerializeField] private PatternBullet fragmentPrefab;
    [SerializeField, Min(0)] private int fragmentCount = 8;
    [SerializeField] private float startAngle;
    [Header("Visual")]
    [SerializeField] private int sortingOrder = 10;
    [Header("Temporary Crater")]
    [SerializeField] private bool showCrater;
    [SerializeField, Min(0.01f)] private float craterDuration = 0.5f;
    [SerializeField] private Color craterColor = new Color(0.25f, 0.26f, 0.28f, 0.8f);
    [SerializeField, Tooltip("坑印宽高相对于预警圈直径的倍率；(1,1)为圆形。仅改变落地痕迹，不改变预警或伤害范围。")]
    private Vector2 craterScale = Vector2.one;

    private enum Phase { Warning, Falling, Explosion }
    private Phase phase;
    private PlayerHealth player;
    private Vector2 landingPosition;
    private Vector3 visualPosition;
    private LineRenderer ring;
    private Material material;
    private float elapsed, warnTime, fallTime, blastTime, radius, height;
    private bool initialized;
    private LandingExplosionVfx landingVfx;
    private Mesh craterMesh;
    private float explosionStartTime;
    public float ExplosionProgress => phase == Phase.Explosion
        ? Mathf.Clamp01((Time.time - explosionStartTime) / Mathf.Max(0.01f, blastTime)) : 0f;

    public bool Initialize(Vector2 position, PlayerHealth target)
    {
        if (fragmentCount > 0 && (fragmentPrefab == null || fragmentPrefab.gameObject.scene.IsValid()))
        {
            Debug.LogError("Assign a Project PatternBullet fragment prefab, or set Fragment Count to zero.", this);
            Destroy(gameObject);
            return false;
        }
        if (fallingVisual == null) fallingVisual = GetComponentInChildren<SpriteRenderer>(true);
        Shader shader = Shader.Find("Sprites/Default");
        if (fallingVisual == null || shader == null || target == null)
        {
            Debug.LogError("ImpactBullet needs a falling SpriteRenderer and a Player.", this);
            Destroy(gameObject);
            return false;
        }
        player = target;
        landingVfx = GetComponent<LandingExplosionVfx>();
        landingPosition = position;
        warnTime = Mathf.Max(0, warningDuration);
        fallTime = Mathf.Max(0.01f, fallDuration);
        blastTime = Mathf.Max(0.01f, explosionVisualDuration);
        radius = Mathf.Max(0.01f, explosionRadius);
        height = Mathf.Max(0, fallHeight);
        visualPosition = new Vector3(position.x, position.y, 0);
        fallingVisual.transform.position = visualPosition + Vector3.up * height;
        fallingVisual.enabled = false;
        GameObject marker = new GameObject("Impact Warning");
        marker.layer = gameObject.layer;
        marker.transform.SetParent(transform, false);
        ring = marker.AddComponent<LineRenderer>();
        material = new Material(shader);
        ring.sharedMaterial = material;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = Mathf.Max(0.001f, warningWidth);
        ring.sortingLayerName = "Bullet";
        ring.sortingOrder = sortingOrder;
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = 2 * Mathf.PI * i / ring.positionCount;
            ring.SetPosition(i, visualPosition + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius);
        }
        ring.startColor = ring.endColor = warningColor;
        phase = Phase.Warning;
        elapsed = 0;
        initialized = true;
        return true;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        if (player == null || player.IsDead) { Destroy(gameObject); return; }
        elapsed += Time.fixedDeltaTime;
        switch (phase)
        {
            case Phase.Warning:
                if (elapsed < warnTime) return;
                phase = Phase.Falling;
                elapsed = 0;
                fallingVisual.enabled = true;
                break;
            case Phase.Falling:
                float progress = Mathf.Clamp01(elapsed / fallTime);
                float distance = fallCurve != null && fallCurve.length > 0
                    ? Mathf.Clamp01(fallCurve.Evaluate(progress)) : progress;
                fallingVisual.transform.position = visualPosition + Vector3.up * height * (1 - distance);
                if (progress >= 1) Explode();
                break;
            case Phase.Explosion:
                Color color = Color.white;
                color.a = 1 - Mathf.Clamp01(elapsed / blastTime);
                ring.startColor = ring.endColor = color;
                if (elapsed >= Mathf.Max(blastTime, showCrater ? craterDuration : 0f)) Destroy(gameObject);
                break;
        }
    }

    private void Explode()
    {
        phase = Phase.Explosion;
        explosionStartTime = Time.time;
        elapsed = 0;
        fallingVisual.transform.position = visualPosition;
        fallingVisual.enabled = false;
        ring.widthMultiplier = Mathf.Max(0.001f, warningWidth) * 2f;
        ring.startColor = ring.endColor = Color.white;
        if (landingVfx != null && landingVfx.TryPlay(landingPosition, radius, fallingVisual))
            ring.enabled = false;
        if (showCrater) CreateCrater();
        DamagePlayer();
        if (player.IsDead) return;
        int count = Mathf.Max(0, fragmentCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (startAngle + 360f * i / count) * Mathf.Deg2Rad;
            Vector2 heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            PatternBullet fragment = Instantiate(fragmentPrefab, visualPosition, Quaternion.identity);
            ChalkFragmentVfx artwork = fragment.GetComponent<ChalkFragmentVfx>();
            if (artwork != null && artwork.isActiveAndEnabled) artwork.Configure(i);
            fragment.Initialize(heading, player);
        }
    }

    private void CreateCrater()
    {
        GameObject mark = new GameObject("Impact Crater Placeholder");
        mark.layer = gameObject.layer;
        // Parent to the attack root, not the falling/scaled Visual child.
        mark.transform.position = visualPosition;
        mark.transform.SetParent(transform, true);
        // Decorative mark only; the warning and damage continue to use the circular radius.
        const int segments = 64;
        Vector3[] vertices = new Vector3[segments + 1];
        Color[] colors = new Color[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 3];
        for (int i = 0; i < vertices.Length; i++)
        {
            colors[i] = craterColor;
            uvs[i] = new Vector2(0.5f, 0.5f);
        }
        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * Mathf.PI * i / segments;
            vertices[i + 1] = new Vector3(
                Mathf.Cos(angle) * radius * Mathf.Max(0.01f, craterScale.x),
                Mathf.Sin(angle) * radius * Mathf.Max(0.01f, craterScale.y), 0f);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }
        craterMesh = new Mesh { name = "Impact crater mark", vertices = vertices,
            colors = colors, uv = uvs, triangles = triangles };
        craterMesh.RecalculateBounds();
        mark.AddComponent<MeshFilter>().sharedMesh = craterMesh;
        MeshRenderer renderer = mark.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerID = fallingVisual.sortingLayerID;
        renderer.sortingOrder = fallingVisual.sortingOrder - 1;
        Destroy(mark, Mathf.Max(0.01f, craterDuration));
    }

    private void DamagePlayer()
    {
        if (damage <= 0) return;
        foreach (Collider2D collider in player.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            if ((collider.ClosestPoint(landingPosition) - landingPosition).sqrMagnitude > radius * radius) continue;
            player.TakeDamage(damage);
            return;
        }
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (craterMesh != null) Destroy(craterMesh);
    }
}
