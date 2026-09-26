using System.Collections.Generic;
using UnityEngine;

public class GroundArea : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float radius = 1f;
    [SerializeField, Min(0.1f)] private float lifetime = 6f;
    [Header("Burn")]
    [SerializeField] private bool burnEnabled = true;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.05f)] private float damageInterval = 0.5f;
    [Header("Slow")]
    [SerializeField] private bool slowEnabled;
    [SerializeField, Range(0.05f, 1f)] private float speedMultiplier = 0.5f;
    [Header("Visual")]
    [SerializeField] private Color areaColor = new Color(0.5f, 0.15f, 0.65f, 0.35f);
    [SerializeField] private int sortingOrder = -10;

    private static readonly HashSet<GroundArea> activeAreas = new HashSet<GroundArea>();
    private static int visualRevision;
    private static long nextVisualId;
    private int builtRevision = -1;
    private long visualId;
    private PlayerHealth player;
    private Vector2 center;
    private float effectiveRadius, expiresAt;
    private bool initialized;
    private Mesh mesh;
    private Material material;

    public void Initialize(PlayerHealth target)
    {
        player = target;
        center = transform.position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        effectiveRadius = Mathf.Max(0.01f, radius);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        visualId = ++nextVisualId;
        initialized = true;
        CreateVisual();
        if (isActiveAndEnabled) Register();
    }

    public static float GetSpeedMultiplier(PlayerHealth target, Vector2 position)
    {
        float multiplier = 1f;
        foreach (GroundArea area in activeAreas)
            if (area != null && area.isActiveAndEnabled && area.initialized &&
                area.player == target && area.slowEnabled && Time.time < area.expiresAt && area.Contains(position))
                multiplier = Mathf.Min(multiplier, Mathf.Clamp(area.speedMultiplier, 0.05f, 1f));
        return multiplier;
    }

    private bool Contains(Vector2 position) => (position - center).sqrMagnitude <= effectiveRadius * effectiveRadius;

    public static bool TryGetBurn(PlayerHealth target, Vector2 position, out int amount, out float interval)
    {
        amount = 0;
        interval = float.PositiveInfinity;
        foreach (GroundArea area in activeAreas)
        {
            if (area == null || !area.isActiveAndEnabled || !area.initialized || area.player != target ||
                !area.burnEnabled || Time.time >= area.expiresAt || !area.Contains(position)) continue;
            int candidateDamage = Mathf.Max(1, area.damage);
            float candidateInterval = Mathf.Max(0.05f, area.damageInterval);
            if (candidateDamage > amount || (candidateDamage == amount && candidateInterval < interval))
            {
                amount = candidateDamage;
                interval = candidateInterval;
            }
        }
        return amount > 0;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        if (player == null || player.IsDead || Time.time >= expiresAt)
        {
            Unregister();
            Destroy(gameObject);
            return;
        }
    }

    private void CreateVisual()
    {
        mesh = new Mesh { name = "Ground Area", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        material = new Material(Shader.Find("Sprites/Default"));
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = "Bullet";
        renderer.sortingOrder = sortingOrder;
    }

    private void LateUpdate()
    {
        if (!initialized || mesh == null || builtRevision == visualRevision) return;
        var blockers = new List<Vector3>();
        foreach (GroundArea other in activeAreas)
            if (other != null && other != this && other.visualId < visualId &&
                other.burnEnabled == burnEnabled && other.slowEnabled == slowEnabled &&
                (other.center - center).sqrMagnitude < (other.effectiveRadius + effectiveRadius) * (other.effectiveRadius + effectiveRadius))
                blockers.Add(new Vector3(other.center.x - center.x, other.center.y - center.y, other.effectiveRadius));
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        GroundAreaGeometry.BuildVisible(effectiveRadius, blockers, vertices, triangles);
        var colors = new List<Color>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++) colors.Add(areaColor);
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        builtRevision = visualRevision;
    }

    private void Register() { if (activeAreas.Add(this)) visualRevision++; }
    private void Unregister() { if (activeAreas.Remove(this)) visualRevision++; }
    private void OnEnable() { if (initialized) Register(); }
    private void OnDisable() { Unregister(); }
    private void OnDestroy()
    {
        Unregister();
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = areaColor;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, radius));
    }
}
