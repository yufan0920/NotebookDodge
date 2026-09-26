using System.Collections.Generic;
using UnityEngine;

internal static class GroundAreaGeometry
{
    private static List<Vector2> Circle(Vector2 center, float radius)
    {
        var result = new List<Vector2>(64);
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            result.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
        return result;
    }

    public static void BuildVisible(float radius, List<Vector3> blockers, List<Vector3> vertices, List<int> triangles)
    {
        vertices.Clear(); triangles.Clear();
        var pieces = new List<List<Vector2>> { Circle(Vector2.zero, radius) };
        foreach (Vector3 blocker in blockers)
        {
            var clip = Circle(new Vector2(blocker.x, blocker.y), blocker.z);
            var next = new List<List<Vector2>>();
            foreach (var piece in pieces)
            {
                var remaining = piece;
                for (int i = 0; i < clip.Count && remaining.Count >= 3; i++)
                {
                    Vector2 a = clip[i], b = clip[(i + 1) % clip.Count];
                    var outside = HalfPlane(remaining, a, b, false);
                    if (HasArea(outside)) next.Add(outside);
                    remaining = HalfPlane(remaining, a, b, true);
                }
            }
            pieces = next;
            if (pieces.Count == 0) break;
        }
        foreach (var piece in pieces)
        {
            if (!HasArea(piece)) continue;
            int offset = vertices.Count;
            foreach (Vector2 point in piece) vertices.Add(point);
            for (int i = 2; i < piece.Count; i++)
            {
                triangles.Add(offset); triangles.Add(offset + i - 1); triangles.Add(offset + i);
            }
        }
    }

    private static List<Vector2> HalfPlane(List<Vector2> input, Vector2 a, Vector2 b, bool inside)
    {
        var result = new List<Vector2>();
        Vector2 edge = b - a;
        for (int i = 0; i < input.Count; i++)
        {
            Vector2 p = input[i], q = input[(i + 1) % input.Count];
            float pd = Cross(edge, p - a), qd = Cross(edge, q - a);
            bool pIn = inside ? pd >= 0 : pd <= 0;
            bool qIn = inside ? qd >= 0 : qd <= 0;
            if (pIn) result.Add(p);
            if (pIn != qIn) result.Add(Vector2.Lerp(p, q, pd / (pd - qd)));
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static bool HasArea(List<Vector2> polygon)
    {
        float sum = 0;
        for (int i = 0; i < polygon.Count; i++) sum += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        return Mathf.Abs(sum) > 0.000001f;
    }
}
