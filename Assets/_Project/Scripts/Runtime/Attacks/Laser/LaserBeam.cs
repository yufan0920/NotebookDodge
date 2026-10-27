using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LaserBeam : MonoBehaviour
{
    [Header("Warning")]
    [SerializeField, Min(0.05f)] private float warningDuration = 1.2f;
    [SerializeField, Min(0.001f)] private float warningWidth = 0.035f;
    [SerializeField] private Color warningColor = new Color(1f, 0.85f, 0.15f, 0.85f);
    [Header("Charge And Acceleration")]
    [SerializeField, Min(0)] private float chargeDuration = 1.5f;
    [SerializeField, Min(0)] private float chargeAdvanceDistance = 0.3f;
    [SerializeField, Min(0)] private float accelerationDuration = 0.12f;
    [SerializeField] private AnimationCurve speedCurve = new AnimationCurve(
        new Keyframe(0, 0.02f), new Keyframe(0.65f, 0.05f), new Keyframe(1, 1));
    [Header("Drawing")]
    [SerializeField, Min(0.01f)] private float beamLength = 16f;
    [FormerlySerializedAs("sweepSpeed")]
    [SerializeField, Min(0.01f)] private float drawSpeed = 100f;
    [SerializeField, Min(0.01f)] private float initialLength = 0.6f;
    [SerializeField, Min(0.01f), Tooltip("激光外观的宽度基准，不包含独立的伤害宽度设置。")]
    private float beamWidth = 0.5f;
    [SerializeField] private Color beamColor = new Color(0.8f, 1f, 0.1f, 0.65f);
    [Header("Damage")]
    [SerializeField, Min(0.01f), Tooltip("激光中心的伤害总宽度（世界单位），以中心线对称分布；不超过Beam Width。只覆盖白色核心时请调小此值，外侧光晕不参与判定。")]
    private float damageWidth = 0.5f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.02f)] private float damageInterval = 0.5f;
    [Header("End")]
    [SerializeField, Min(0)] private float holdDuration = 4f;
    [SerializeField, Min(0)] private float fadeDuration = 0.25f;
    [Header("Visual")]
    [SerializeField] private int sortingOrder = 10;
    [SerializeField, Tooltip("Hide only the built-in beam strip; warning line and hit detection remain active.")]
    private bool hideBuiltInBeamVisual;

    private readonly Vector2[] corners = new Vector2[4];
    private readonly List<Vector2> polygon = new List<Vector2>(8);
    private readonly List<Vector2> scratch = new List<Vector2>(8);
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly List<Vector3> vertices = new List<Vector3>(8);
    private readonly List<int> triangles = new List<int>(18);
    private readonly List<Color> colors = new List<Color>(8);
    private PlayerHealth player;
    private Mesh mesh;
    private Material material;
    private PolygonCollider2D hitbox;
    private LineRenderer warning;
    private float startX, endX, currentX, strokeY;
    private float elapsed, nextDamageTime, activeWidth, activeDamageWidth, activeSpeed, warnTime, fadeTime, holdTime;
    private enum Phase { Warning, Charge, Drawing, Hold, Fade }
    private Phase phase;
    private float chargeTime, accelerationTime;
    private float chargeStartX, chargeEndX;
    private AnimationCurve activeSpeedCurve;
    private bool initialized;

    // Read-only drawing state for optional visual effects. Collision and timing stay here.
    public bool HasStartedDrawing { get; private set; }
    public float DrawStartTime { get; private set; }
    public float VisibleLength => Mathf.Max(0f, currentX - startX);
    public float VisibleWidth => activeWidth;
    public float VisibleAlpha { get; private set; }
    public int VisualSortingOrder => sortingOrder;

    public bool Initialize(PlayerHealth target, Vector2 heading, Vector3 emitterPosition)
    {
        player = target;
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        PlayerMovementArea area = movement != null ? movement.MovementArea : null;
        Shader shader = Shader.Find("Sprites/Default");
        if (area == null || !area.isActiveAndEnabled || !area.TryCopyBoundary(corners) || shader == null)
        {
            Debug.LogError("LaserBeam requires a valid Player Movement Area and Sprites/Default shader.", this);
            Destroy(gameObject);
            return false;
        }
        heading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.right;
        transform.position = new Vector3(emitterPosition.x, emitterPosition.y, 0);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
        transform.localScale = Vector3.one;
        for (int i = 0; i < corners.Length; i++) corners[i] = transform.InverseTransformPoint(corners[i]);
        activeWidth = Mathf.Max(0.01f, beamWidth);
        activeDamageWidth = Mathf.Clamp(damageWidth, 0.01f, activeWidth);
        activeSpeed = Mathf.Max(0.01f, drawSpeed);
        warnTime = Mathf.Max(0.05f, warningDuration);
        fadeTime = Mathf.Max(0, fadeDuration);
        holdTime = Mathf.Max(0, holdDuration);
        chargeTime = Mathf.Max(0, chargeDuration);
        accelerationTime = Mathf.Max(0, accelerationDuration);
        activeSpeedCurve = speedCurve != null && speedCurve.length > 0 ? new AnimationCurve(speedCurve.keys) : AnimationCurve.Linear(0, 1, 1, 1);
        strokeY = 0;
        startX = 0;
        endX = Mathf.Max(0.01f, beamLength);
        currentX = startX;
        HasStartedDrawing = false;
        VisibleAlpha = 0f;
        material = new Material(shader);
        mesh = new Mesh { name = "Laser strip" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Bullet";
        renderer.sortingOrder = sortingOrder;
        renderer.enabled = !hideBuiltInBeamVisual;
        hitbox = gameObject.AddComponent<PolygonCollider2D>();
        hitbox.isTrigger = true;
        hitbox.enabled = false;
        warning = CreateLine("Warning", warningWidth);
        warning.SetPosition(0, new Vector3(startX, strokeY, 0));
        warning.SetPosition(1, new Vector3(endX, strokeY, 0));
        initialized = true;
        DrawWarning();
        return true;
    }

    private LineRenderer CreateLine(string label, float width)
    {
        GameObject visual = new GameObject(label);
        visual.layer = gameObject.layer;
        visual.transform.SetParent(transform, false);
        LineRenderer line = visual.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.useWorldSpace = false;
        line.widthMultiplier = Mathf.Max(0.001f, width);
        line.sortingLayerName = "Bullet";
        line.sortingOrder = sortingOrder + 1;
        return line;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        if (player == null || player.IsDead) { Destroy(gameObject); return; }
        elapsed += Time.fixedDeltaTime;
        if (phase == Phase.Warning)
        {
            DrawWarning();
            if (elapsed < warnTime) return;
            phase = Phase.Charge;
            elapsed = 0;
            currentX = Mathf.MoveTowards(startX, endX, Mathf.Max(0.01f, initialLength));
            chargeStartX = currentX;
            chargeEndX = Mathf.Min(endX, chargeStartX + Mathf.Max(0, chargeAdvanceDistance));
            UpdateStroke(false, 0.85f);
            if (chargeTime <= 0) BeginDrawing();
            return;
        }
        if (phase == Phase.Charge)
        {
            DrawWarning();
            float progress = chargeTime > 0 ? Mathf.Clamp01(elapsed / chargeTime) : 1f;
            currentX = Mathf.Lerp(chargeStartX, chargeEndX, Mathf.SmoothStep(0, 1, progress));
            UpdateStroke(false, 0.85f);
            if (elapsed >= chargeTime) BeginDrawing();
            return;
        }
        if (phase == Phase.Drawing)
        {
            float multiplier = accelerationTime > 0 && elapsed < accelerationTime
                ? Mathf.Max(0, activeSpeedCurve.Evaluate(Mathf.Clamp01(elapsed / accelerationTime))) : 1f;
            currentX = Mathf.MoveTowards(currentX, endX, activeSpeed * multiplier * Time.fixedDeltaTime);
            UpdateStroke();
            if (currentX == endX)
            {
                phase = Phase.Hold;
                elapsed = 0;
                hitbox.enabled = false;
            }
            return;
        }
        if (phase == Phase.Hold)
        {
            if (elapsed < holdTime) { CheckDamage(); return; }
            phase = Phase.Fade;
            elapsed = 0;
        }
        float alpha = fadeTime > 0 ? 1 - Mathf.Clamp01(elapsed / fadeTime) : 0;
        DrawStrip(alpha);
        if (alpha <= 0) Destroy(gameObject);
    }

    private void BeginDrawing()
    {
        phase = currentX >= endX ? Phase.Hold : Phase.Drawing;
        elapsed = 0;
        HasStartedDrawing = true;
        DrawStartTime = Time.time;
        warning.enabled = false;
        UpdateStroke();
    }

    private void DrawWarning()
    {
        Color color = warningColor;
        color.a *= 0.65f + 0.35f * Mathf.Sin(elapsed * 8f) * Mathf.Sin(elapsed * 8f);
        warning.startColor = warning.endColor = color;
    }

    private void UpdateStroke(bool canDamage = true, float alpha = 1f)
    {
        LaserGeometry.BuildStroke(corners, Mathf.Min(startX, currentX), Mathf.Max(startX, currentX),
            strokeY - activeDamageWidth * 0.5f, strokeY + activeDamageWidth * 0.5f, polygon, scratch);
        DrawStrip(alpha);
        if (canDamage) CheckDamage();
    }

    private void DrawStrip(float alpha)
    {
        VisibleAlpha = alpha;
        mesh.Clear();
        if (currentX <= startX) return;
        vertices.Clear(); triangles.Clear(); colors.Clear();
        Color color = beamColor;
        color.a *= alpha;
        vertices.Add(new Vector3(startX, -activeWidth * 0.5f, 0));
        vertices.Add(new Vector3(currentX, -activeWidth * 0.5f, 0));
        vertices.Add(new Vector3(currentX, activeWidth * 0.5f, 0));
        vertices.Add(new Vector3(startX, activeWidth * 0.5f, 0));
        for (int i = 0; i < 4; i++) colors.Add(color);
        triangles.Add(0); triangles.Add(1); triangles.Add(2);
        triangles.Add(0); triangles.Add(2); triangles.Add(3);
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void CheckDamage()
    {
        if (phase != Phase.Drawing && phase != Phase.Hold) return;
        if (polygon.Count < 3 || Time.time < nextDamageTime || player.IsInvincible) return;
        hitbox.SetPath(0, polygon.ToArray());
        hitbox.enabled = true;
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
        overlaps.Clear();
        hitbox.OverlapCollider(filter, overlaps);
        hitbox.enabled = false;
        foreach (Collider2D other in overlaps)
            if (other != null && other.GetComponentInParent<PlayerHealth>() == player)
            {
                player.TakeDamage(Mathf.Max(1, damage));
                nextDamageTime = Time.time + Mathf.Max(0.02f, damageInterval);
                break;
            }
    }

    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
