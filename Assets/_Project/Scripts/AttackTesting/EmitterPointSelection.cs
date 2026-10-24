using System;
using System.Collections.Generic;
using UnityEngine;

// Shared by the placement preview and emitter orchestration.
// Positions and spacing are world XY units, including across different edges.
public static class EmitterPointSelection
{
    public sealed class Candidate
    {
        public readonly string Id;
        public readonly Vector2 Position;
        public Candidate(string id, Vector2 position) { Id = id; Position = position; }
    }

    public sealed class Result
    {
        public readonly IReadOnlyList<Candidate> Points;
        public readonly bool Success;
        public readonly bool SearchLimitReached;
        public readonly string Message;
        internal Result(List<Candidate> points, int requested, bool limited)
        {
            Points = points.AsReadOnly();
            Success = points.Count == requested;
            SearchLimitReached = limited;
            Message = Success ? $"Selected {requested} points."
                : limited ? $"Requested {requested}; found {points.Count} before the search limit. A larger set may exist. Reduce Count / Spacing, widen Region, or change Seed."
                : $"Requested {requested}; only {points.Count} fit this region with the current spacing and occupancy. Reduce Count / Spacing or widen Region.";
        }
    }

    public sealed class Reservation
    {
        public readonly IReadOnlyList<Candidate> Points;
        public readonly float Spacing;
        private readonly List<Candidate> activePoints;
        internal Reservation(IReadOnlyList<Candidate> points, float spacing)
        {
            activePoints = new List<Candidate>(points);
            Points = activePoints.AsReadOnly();
            Spacing = spacing;
        }
        internal bool RemovePoint(string id)
        {
            int index = activePoints.FindIndex(point => point.Id == id);
            if (index < 0) return false;
            activePoints.RemoveAt(index);
            return true;
        }
    }

    // Use one shared occupancy instance for all groups in a play session.
    // Reserve BEFORE entry. Keep through preparation, firing and exit.
    // Release only AFTER the emitter(s) have fully exited, or on explicit test reset.
    public sealed class Occupancy
    {
        private readonly List<Reservation> reservations = new List<Reservation>();
        public IReadOnlyList<Reservation> Reservations => reservations.AsReadOnly();

        public Result Preview(IReadOnlyList<Candidate> candidates, int count, float spacing, int seed)
            => Select(candidates, count, spacing, seed, reservations);

        public bool TryReserve(IReadOnlyList<Candidate> candidates, int count, float spacing, int seed,
            out Reservation reservation, out Result result, ISet<string> excludedIds = null)
        {
            // Reentry excludes the last successful wave without mutating the point library.
            if (excludedIds != null && excludedIds.Count > 0)
            {
                var filtered = new List<Candidate>();
                foreach (var point in candidates)
                    if (!excludedIds.Contains(point.Id)) filtered.Add(point);
                candidates = filtered;
            }
            result = Preview(candidates, count, spacing, seed);
            reservation = null;
            if (!result.Success) return false; // Never partially reserve a failed group.
            reservation = new Reservation(result.Points, spacing);
            reservations.Add(reservation);
            return true;
        }

        public bool Release(Reservation reservation) => reservations.Remove(reservation);
        // A group reserves atomically, but each emitter releases its own point after exit.
        public bool ReleasePoint(Reservation reservation, string id)
        {
            if (reservation == null || !reservations.Contains(reservation) || !reservation.RemovePoint(id)) return false;
            if (reservation.Points.Count == 0) reservations.Remove(reservation);
            return true;
        }
        public int PointCount
        {
            get { int count = 0; foreach (var reservation in reservations) count += reservation.Points.Count; return count; }
        }
        public void Clear() => reservations.Clear();
    }

    public static Result Select(IReadOnlyList<Candidate> candidates, int count, float spacing, int seed,
        IReadOnlyList<Reservation> occupied = null, int searchBudget = 20000)
    {
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (count < 1 || count > 120) throw new ArgumentOutOfRangeException(nameof(count), "Count must be between 1 and 120.");
        if (!Finite(spacing) || spacing < 0) throw new ArgumentOutOfRangeException(nameof(spacing), "Spacing must be finite and non-negative.");
        if (searchBudget < 1) throw new ArgumentOutOfRangeException(nameof(searchBudget));
        var available = new List<Candidate>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var point in candidates)
        {
            if (point == null || string.IsNullOrEmpty(point.Id) || !Finite(point.Position.x) || !Finite(point.Position.y))
                throw new ArgumentException("All candidate points need an ID and a finite world position.");
            if (!ids.Add(point.Id)) throw new ArgumentException("Duplicate point ID: " + point.Id);
            bool blocked = false;
            if (occupied != null)
                foreach (var reservation in occupied)
                {
                    foreach (var active in reservation.Points)
                        if (Conflicts(point, active, Math.Max(spacing, reservation.Spacing))) { blocked = true; break; }
                    if (blocked) break;
                }
            if (!blocked) available.Add(point);
        }
        // No UnityEngine.Random state is consumed. A fixed seed is repeatable.
        var random = new System.Random(seed);
        for (int i = available.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            var temp = available[i]; available[i] = available[j]; available[j] = temp;
        }
        var search = new Search(count, spacing, searchBudget);
        search.Visit(available, new List<Candidate>());
        return new Result(search.Best, count, search.Limited);
    }

    private static bool Conflicts(Candidate a, Candidate b, float spacing)
    {
        if (a.Id == b.Id) return true;
        double dx = (double)a.Position.x - b.Position.x;
        double dy = (double)a.Position.y - b.Position.y;
        // Even with zero spacing, coincident points cannot be occupied twice.
        return dx * dx + dy * dy < Math.Max((double)spacing * spacing, 1e-10);
    }

    private sealed class Search
    {
        private readonly int target, budget;
        private readonly float spacing;
        private int visited;
        public List<Candidate> Best = new List<Candidate>();
        public bool Limited;
        public Search(int target, float spacing, int budget) { this.target = target; this.spacing = spacing; this.budget = budget; }
        public bool Visit(List<Candidate> available, List<Candidate> chosen)
        {
            if (chosen.Count > Best.Count) Best = new List<Candidate>(chosen);
            if (Best.Count >= target) return true;
            if (chosen.Count + available.Count <= Best.Count) return false;
            if (++visited > budget) { Limited = true; return false; }
            // Backtrack instead of reporting failure after one unlucky greedy draw.
            for (int i = 0; i < available.Count; i++)
            {
                if (chosen.Count + available.Count - i <= Best.Count) break;
                var next = new List<Candidate>();
                for (int j = i + 1; j < available.Count; j++)
                    if (!Conflicts(available[i], available[j], spacing)) next.Add(available[j]);
                chosen.Add(available[i]);
                bool success = Visit(next, chosen);
                chosen.RemoveAt(chosen.Count - 1);
                if (success) return true;
                if (Limited) break;
            }
            return false;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
