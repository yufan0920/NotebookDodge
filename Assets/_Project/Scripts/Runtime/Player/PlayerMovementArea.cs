using UnityEngine;
using System.Collections.Generic;

public class PlayerMovementArea : MonoBehaviour
{
    [SerializeField] private Vector2 bottomLeft = new Vector2(-6f, -3.5f);
    [SerializeField] private Vector2 bottomRight = new Vector2(6f, -3.5f);
    [SerializeField] private Vector2 topRight = new Vector2(5.4f, 3.3f);
    [SerializeField] private Vector2 topLeft = new Vector2(-5.4f, 3.3f);
    [SerializeField] private bool showBoundary = true;
    [SerializeField] private Color boundaryColor = Color.green;
    [SerializeField, Min(0.001f)] private float boundaryWidth = 0.035f;
    [SerializeField] private int boundarySortingOrder = 100;
    private readonly Vector2[] corners = new Vector2[4];
    private bool warned;
    private LineRenderer boundary;
    private Material boundaryMaterial;
    private readonly List<Vector2> inset = new List<Vector2>(8);
    private readonly List<Vector2> scratch = new List<Vector2>(8);

    private void LateUpdate()
    {
        if (!showBoundary)
        {
            if (boundary != null) boundary.enabled = false;
            return;
        }
        if (boundary == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            GameObject visual = new GameObject("Movement Area Boundary");
            visual.layer = gameObject.layer;
            visual.transform.SetParent(transform, false);
            boundary = visual.AddComponent<LineRenderer>();
            boundaryMaterial = new Material(shader);
            boundary.sharedMaterial = boundaryMaterial;
            boundary.useWorldSpace = true;
            boundary.loop = true;
            boundary.positionCount = 4;
        }
        GetCorners();
        for (int i = 0; i < 4; i++)
            boundary.SetPosition(i, new Vector3(corners[i].x, corners[i].y, 0));
        boundary.startColor = boundary.endColor = boundaryColor;
        boundary.widthMultiplier = Mathf.Max(0.001f, boundaryWidth);
        boundary.sortingOrder = boundarySortingOrder;
        boundary.enabled = true;
    }

    private void OnDisable()
    {
        if (boundary != null) boundary.enabled = false;
    }

    private void OnDestroy()
    {
        if (boundary != null) Destroy(boundary.gameObject);
        if (boundaryMaterial != null) Destroy(boundaryMaterial);
    }

    public bool TryConstrain(Vector2 position, out Vector2 constrained)
        => TryConstrain(position, 0, out constrained);

    public bool TryConstrain(Vector2 position, float padding, out Vector2 constrained)
    {
        constrained = position;
        GetCorners();
        float winding = 0;
        for (int i = 0; i < 4; i++)
        {
            float turn = Cross(corners[(i + 1) % 4] - corners[i],
                corners[(i + 2) % 4] - corners[(i + 1) % 4]);
            if (Mathf.Abs(turn) < 0.00001f || (i > 0 && turn * winding <= 0))
            {
                if (!warned) Debug.LogWarning("Player movement area must be a convex quadrilateral. Check corner order and Transform scale.", this);
                warned = true;
                return false;
            }
            winding = turn;
        }
        warned = false;
        if (!BuildInset(corners, padding, inset, scratch)) return false;
        bool inside = true;
        float bestDistance = float.PositiveInfinity;
        Vector2 closest = position;
        for (int i = 0; i < inset.Count; i++)
        {
            Vector2 a = inset[i];
            Vector2 edge = inset[(i + 1) % inset.Count] - a;
            if (Cross(edge, position - a) * winding < 0) inside = false;
            if (edge.sqrMagnitude < 1e-12f) continue;
            float t = Mathf.Clamp01(Vector2.Dot(position - a, edge) / edge.sqrMagnitude);
            Vector2 candidate = a + edge * t;
            float distance = (candidate - position).sqrMagnitude;
            if (distance < bestDistance) { bestDistance = distance; closest = candidate; }
        }
        constrained = inside ? position : closest;
        return true;
    }

    // Clip against inward-offset edges in world units. Unlike scaling, this
    // keeps the same clearance at slanted sides and their intersections.
    internal static bool BuildInset(Vector2[] boundary, float padding,
        List<Vector2> result, List<Vector2> buffer)
    {
        result.Clear();
        if (float.IsNaN(padding) || float.IsInfinity(padding)) return false;
        padding = Mathf.Max(0, padding);
        result.AddRange(boundary);
        float winding = Cross(boundary[1] - boundary[0], boundary[2] - boundary[1]);
        for (int i = 0; i < boundary.Length && result.Count > 0; i++)
        {
            Vector2 origin = boundary[i];
            Vector2 edge = boundary[(i + 1) % boundary.Length] - origin;
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized * Mathf.Sign(winding);
            buffer.Clear();
            for (int j = 0; j < result.Count; j++)
            {
                Vector2 a = result[j], b = result[(j + 1) % result.Count];
                float da = Vector2.Dot(a - origin, normal) - padding;
                float db = Vector2.Dot(b - origin, normal) - padding;
                if (da >= 0) buffer.Add(a);
                if ((da >= 0) != (db >= 0)) buffer.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            result.Clear();
            result.AddRange(buffer);
        }
        float area = 0;
        for (int i = 0; i < result.Count; i++) area += Cross(result[i], result[(i + 1) % result.Count]);
        return result.Count >= 3 && Mathf.Abs(area) > 0.000001f;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    public bool TryCopyBoundary(Vector2[] destination)
    {
        Vector2 unused;
        if (destination == null || destination.Length < 4 ||
            !TryConstrain(transform.position, out unused)) return false;
        for (int i = 0; i < 4; i++) destination[i] = corners[i];
        return true;
    }

    private void GetCorners()
    {
        corners[0] = transform.TransformPoint(bottomLeft);
        corners[1] = transform.TransformPoint(bottomRight);
        corners[2] = transform.TransformPoint(topRight);
        corners[3] = transform.TransformPoint(topLeft);
    }

    private void OnDrawGizmos()
    {
        GetCorners();
        Gizmos.color = Color.green;
        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            Gizmos.DrawSphere(corners[i], 0.06f);
        }
    }
}
