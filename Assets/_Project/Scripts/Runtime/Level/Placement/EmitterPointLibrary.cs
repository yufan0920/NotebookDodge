using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Reusable placement data. This component does not spawn or fire emitters.
[DisallowMultipleComponent]
public class EmitterPointLibrary : MonoBehaviour
{
    public enum Side { Top, Bottom, Left, Right }
    public enum Region { All, Top, Bottom, Left, Right, UpperLeft, UpperRight, LowerLeft, LowerRight }

    [Serializable]
    public sealed class Point
    {
        [SerializeField] private string id;
        [SerializeField] private Side side;
        [SerializeField] private Vector2 position;
        [SerializeField] private Vector2 direction;

        public string Id => id;
        public Side EntrySide => side;
        public Vector2 LocalPosition => position;
        public Vector2 LocalDirection => direction;

        public Point(string id, Side side, Vector2 position, Vector2 direction)
        {
            this.id = id;
            this.side = side;
            this.position = position;
            this.direction = direction;
        }
    }

    [FormerlySerializedAs("pointsPerSide")]
    [SerializeField, Range(1, 30)] private int horizontalPointsPerSide = 10;
    [SerializeField, Range(1, 30)] private int verticalPointsPerSide = 10;
    [SerializeField] private Vector2 layoutCenter = Vector2.zero;
    [SerializeField] private Vector2 layoutSize = new Vector2(16, 8);
    [SerializeField, Min(0.05f)] private float cornerInset = 0.5f;
    [SerializeField] private List<Point> points = new List<Point>();
    // Keep the applied center separate: changing generation settings must not
    // silently change the region membership of an already edited library.
    [SerializeField, HideInInspector] private Vector2 regionCenter = Vector2.zero;
    [SerializeField] private Region previewRegion = Region.All;
    [SerializeField] private bool showLabels = true;
    [SerializeField, Range(0.2f, 2)] private float arrowLength = 0.6f;

    public IReadOnlyList<Point> Points => points;
    public Vector2 RegionCenter => regionCenter;
    public Region PreviewRegion => previewRegion;
    public bool ShowLabels => showLabels;
    public float ArrowLength => arrowLength;

    public Vector3 GetWorldPosition(Point point) => transform.TransformPoint(point.LocalPosition);

    public Vector2 GetWorldDirection(Point point)
    {
        Vector2 local = point.LocalDirection.sqrMagnitude > 0.0001f
            ? point.LocalDirection.normalized : DefaultDirection(point.EntrySide);
        return ((Vector2)transform.TransformDirection(local)).normalized;
    }

    public bool IsInRegion(Point point, Region region) => MatchesRegion(point, regionCenter, region);

    // Read the saved generation settings directly; the editor still edits them
    // through SerializedProperty so Undo and prefab overrides remain supported.
    public List<Point> CreateConfiguredLayout()
        => CreateLayout(horizontalPointsPerSide, verticalPointsPerSide, layoutCenter, layoutSize, cornerInset);

    public List<EmitterPointSelection.Candidate> GetCandidates(Region region)
    {
        var candidates = new List<EmitterPointSelection.Candidate>();
        foreach (var point in points)
            if (IsInRegion(point, region))
                candidates.Add(new EmitterPointSelection.Candidate(point.Id, GetWorldPosition(point)));
        return candidates;
    }

    public static Vector2 DefaultDirection(Side side)
    {
        switch (side)
        {
            case Side.Top: return Vector2.down;
            case Side.Bottom: return Vector2.up;
            case Side.Left: return Vector2.right;
            default: return Vector2.left;
        }
    }

    public static bool MatchesRegion(Point point, Vector2 center, Region region)
    {
        if (point == null) return false;
        bool right = point.LocalPosition.x >= center.x;
        bool upper = point.LocalPosition.y >= center.y;
        switch (region)
        {
            case Region.All: return true;
            case Region.Top: return point.EntrySide == Side.Top;
            case Region.Bottom: return point.EntrySide == Side.Bottom;
            case Region.Left: return point.EntrySide == Side.Left;
            case Region.Right: return point.EntrySide == Side.Right;
            case Region.UpperLeft: return upper && !right;
            case Region.UpperRight: return upper && right;
            case Region.LowerLeft: return !upper && !right;
            case Region.LowerRight: return !upper && right;
            default: return false;
        }
    }

    // Horizontal sides are numbered left to right; vertical sides bottom to top.
    public static List<Point> CreateLayout(int count, Vector2 center, Vector2 size, float inset)
        => CreateLayout(count, count, center, size, inset);

    public static List<Point> CreateLayout(int horizontalCount, int verticalCount, Vector2 center, Vector2 size, float inset)
    {
        if (horizontalCount < 1 || horizontalCount > 30) throw new ArgumentOutOfRangeException(nameof(horizontalCount));
        if (verticalCount < 1 || verticalCount > 30) throw new ArgumentOutOfRangeException(nameof(verticalCount));
        if (!Finite(center.x) || !Finite(center.y) || !Finite(size.x) || !Finite(size.y) ||
            size.x < 0.2f || size.y < 0.2f || !Finite(inset) || inset < 0.05f ||
            inset * 2 >= Mathf.Min(size.x, size.y))
            throw new ArgumentException("Use a finite size of at least 0.2, and a Corner Inset between 0.05 and half the shorter side.");

        var result = new List<Point>((horizontalCount + verticalCount) * 2);
        Vector2 half = size * 0.5f;
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            int count = side == Side.Top || side == Side.Bottom ? horizontalCount : verticalCount;
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : (float)i / (count - 1);
                float x = Mathf.Lerp(-half.x + inset, half.x - inset, t);
                float y = Mathf.Lerp(-half.y + inset, half.y - inset, t);
                Vector2 position;
                switch (side)
                {
                    case Side.Top: position = new Vector2(x, half.y); break;
                    case Side.Bottom: position = new Vector2(x, -half.y); break;
                    case Side.Left: position = new Vector2(-half.x, y); break;
                    default: position = new Vector2(half.x, y); break;
                }
                result.Add(new Point(side + "_" + (i + 1).ToString("D2"), side,
                    center + position, DefaultDirection(side)));
            }
        }
        return result;
    }

    public static int RecommendHorizontalCount(int verticalCount, Vector2 size, float inset)
    {
        // Validate the same limits as generation before calculating spacing.
        CreateLayout(1, verticalCount, Vector2.zero, size, inset);
        if (verticalCount < 2) throw new ArgumentException("At least two left/right points are needed to measure spacing.");
        double spacing = (size.y - 2.0 * inset) / (verticalCount - 1);
        double count = Math.Round((size.x - 2.0 * inset) / spacing, MidpointRounding.AwayFromZero) + 1;
        if (count > 30) throw new ArgumentException("Matching spacing needs more than 30 top/bottom points. Reduce the left/right count or adjust the layout size.");
        return Math.Max(2, (int)count);
    }

    // Inserts points without redistributing or resetting the user's edited points.
    // Top/bottom IDs are renumbered in spatial order; left/right points are untouched.
    public static List<Point> AddHorizontalMidpoints(IReadOnlyList<Point> source)
    {
        var result = new List<Point>();
        foreach (Side side in Enum.GetValues(typeof(Side)))
        {
            var edge = new List<Point>();
            foreach (Point point in source)
                if (point != null && point.EntrySide == side) edge.Add(point);
            if (side == Side.Left || side == Side.Right) { result.AddRange(edge); continue; }
            if (edge.Count < 2 || edge.Count * 2 - 1 > 30)
                throw new ArgumentException("Adding horizontal midpoints requires 2 to 15 existing points on each horizontal edge.");
            edge.Sort((a, b) => a.LocalPosition.x.CompareTo(b.LocalPosition.x));
            for (int i = 0; i < edge.Count; i++)
            {
                result.Add(new Point(side + "_" + (i * 2 + 1).ToString("D2"), side,
                    edge[i].LocalPosition, edge[i].LocalDirection));
                if (i == edge.Count - 1) continue;
                Vector2 a = edge[i].LocalDirection;
                Vector2 b = edge[i + 1].LocalDirection;
                double start = Math.Atan2(a.y, a.x);
                double delta = Math.Atan2(b.y, b.x) - start;
                if (delta > Math.PI) delta -= Math.PI * 2;
                if (delta < -Math.PI) delta += Math.PI * 2;
                double angle = start + delta * 0.5;
                var direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                result.Add(new Point(side + "_" + (i * 2 + 2).ToString("D2"), side,
                    (edge[i].LocalPosition + edge[i + 1].LocalPosition) * 0.5f, direction));
            }
        }
        return result;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
