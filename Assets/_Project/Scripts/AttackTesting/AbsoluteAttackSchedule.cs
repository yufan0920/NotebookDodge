using System;
using System.Collections.Generic;

// Consumes each planned shot once. A blocked or missed shot never shifts later shots. 按指定秒数触发攻击，计算组内连射时间；错过的攻击不顺延
public sealed class AbsoluteAttackSchedule
{
    public struct Shot
    {
        public float time;
        public int index;
        public int burst;
    }
    private readonly List<Shot> shots;
    private int next;
    public const float MaxFrameLateness = 0.1f;
    public bool IsComplete => next >= shots.Count;
    public AbsoluteAttackSchedule(List<float> starts, int burstCount, float interval)
        : this(starts, burstCount, interval, float.NegativeInfinity, float.PositiveInfinity) { }

    // Cold range tests omit earlier preparations without replaying or reporting them as missed.
    // Filter AFTER expansion so surviving burst shots keep their original index (Impact targets).
    public AbsoluteAttackSchedule(List<float> starts, int burstCount, float interval, float from, float to)
        : this(Expand(starts, burstCount, interval), from, to) { }

    // A wave retains full-track shot indices, including when a range starts mid-burst.
    public AbsoluteAttackSchedule(List<Shot> plannedShots, float from, float to)
    {
        if (float.IsNaN(from) || float.IsNaN(to) || from >= to)
            throw new ArgumentException("Use an increasing range [from, to).");
        shots = new List<Shot>(plannedShots);
        shots.RemoveAll(shot => shot.time < from || shot.time >= to);
    }

    public static List<List<Shot>> ExpandBursts(List<float> starts, int burstCount, float interval)
    {
        var groups = new List<List<Shot>>();
        for (int i = 0; i < starts.Count; i++) groups.Add(new List<Shot>());
        foreach (var shot in Expand(starts, burstCount, interval)) groups[shot.burst].Add(shot);
        groups.Sort((a, b) => a[0].time.CompareTo(b[0].time));
        return groups;
    }
    public static List<Shot> Expand(List<float> starts, int burstCount, float interval)
    {
        var result = new List<Shot>();
        if (starts == null) return result;
        for (int burst = 0; burst < starts.Count; burst++)
            for (int i = 0; i < burstCount; i++)
                result.Add(new Shot { time = starts[burst] + i * interval, burst = burst });
        result.Sort((a, b) => a.time.CompareTo(b.time));
        for (int i = 0; i < result.Count; i++) { var shot = result[i]; shot.index = i; result[i] = shot; }
        return result;
    }
    public void Tick(float now, Func<int, string> fire, Action<float, float, string> report)
    {
        while (next < shots.Count && shots[next].time <= now + 0.0001f)
        {
            Shot shot = shots[next++];
            string reason = now - shot.time > MaxFrameLateness ? "Missed frame deadline" : fire(shot.index);
            report(shot.time, now, reason);
        }
    }
}

// Repeats attack starts in [from, end). Full bursts keep their original shot indices.
public static class EndlessAttackLoop
{
    public static string Validate(float from, float end, float phase3)
    {
        if (!Finite(from) || !Finite(end) || !Finite(phase3) || phase3 < 0 || from < phase3 || end - from < 1)
            return "Endless P3 needs Phase 3 Start <= Loop From < Duration, with at least 1 second to repeat.";
        return null;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    // 0 = original authored pass, 1 = first repeat after the original end.
    public static int RepeatNumber(float elapsed, float from, float end)
        => elapsed < end ? 0 : 1 + (int)Math.Floor(((double)elapsed - end) / (end - from));
    public static float PatternTime(float elapsed, float from, float end)
        => elapsed < end ? elapsed : from + (float)(((double)elapsed - end) % (end - from));
    public static List<List<AbsoluteAttackSchedule.Shot>> RepeatBursts(
        List<float> starts, int count, float interval, float from, float end, int repeat)
    {
        if (Validate(from, end, 0) != null || repeat < 1) throw new ArgumentException("Invalid repeat range or number.");
        var bursts = AbsoluteAttackSchedule.ExpandBursts(starts, count, interval);
        bursts.RemoveAll(burst => burst[0].time < from || burst[0].time >= end);
        float shift = repeat * (end - from);
        foreach (var burst in bursts)
            for (int i = 0; i < burst.Count; i++)
            {
                var shot = burst[i]; shot.time += shift; burst[i] = shot;
            }
        return bursts;
    }
}
