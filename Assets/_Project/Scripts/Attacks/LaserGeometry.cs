using System.Collections.Generic;
using UnityEngine;

internal static class LaserGeometry
{
    public static void BuildStrip(Vector2[] corners, float left, float right,
        List<Vector2> polygon, List<Vector2> scratch)
    {
        BuildStroke(corners, left, right, float.NegativeInfinity, float.PositiveInfinity, polygon, scratch);
    }

    public static void BuildStroke(Vector2[] corners, float left, float right, float bottom, float top,
        List<Vector2> polygon, List<Vector2> scratch)
    {
        polygon.Clear();
        if (left >= right || bottom >= top) return;
        polygon.AddRange(corners);
        Clip(left, true, true, polygon, scratch);
        Clip(right, false, true, polygon, scratch);
        Clip(bottom, true, false, polygon, scratch);
        Clip(top, false, false, polygon, scratch);
        float twiceArea = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            twiceArea += a.x * b.y - a.y * b.x;
        }
        if (Mathf.Abs(twiceArea) < 0.000001f) polygon.Clear();
    }

    private static void Clip(float boundary, bool keepGreater, bool horizontal, List<Vector2> polygon, List<Vector2> scratch)
    {
        scratch.Clear();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            float av = horizontal ? a.x : a.y, bv = horizontal ? b.x : b.y;
            bool aIn = keepGreater ? av >= boundary : av <= boundary;
            bool bIn = keepGreater ? bv >= boundary : bv <= boundary;
            if (aIn) scratch.Add(a);
            if (aIn != bIn) scratch.Add(Vector2.Lerp(a, b, (boundary - av) / (bv - av)));
        }
        polygon.Clear();
        foreach (Vector2 point in scratch)
            if (polygon.Count == 0 || (point - polygon[polygon.Count - 1]).sqrMagnitude > 0.00000001f)
                polygon.Add(point);
        if (polygon.Count > 1 && (polygon[0] - polygon[polygon.Count - 1]).sqrMagnitude < 0.00000001f)
            polygon.RemoveAt(polygon.Count - 1);
    }
}
