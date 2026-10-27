using System;
using System.Collections.Generic;

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
