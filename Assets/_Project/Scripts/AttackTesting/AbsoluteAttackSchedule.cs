using System;
using System.Collections.Generic;

// Consumes each planned shot once. A blocked or missed shot never shifts later shots. 按指定秒数触发攻击，计算组内连射时间；错过的攻击不顺延
public sealed class AbsoluteAttackSchedule
{
    public struct Shot
    {
        public float time;
        public int index;
    }
    private readonly List<Shot> shots;
    private int next;
    public const float MaxFrameLateness = 0.1f;
    public AbsoluteAttackSchedule(List<float> starts, int burstCount, float interval)
    {
        shots = Expand(starts, burstCount, interval);
    }
    public static List<Shot> Expand(List<float> starts, int burstCount, float interval)
    {
        var result = new List<Shot>();
        if (starts == null) return result;
        foreach (float start in starts)
            for (int i = 0; i < burstCount; i++) result.Add(new Shot { time = start + i * interval });
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
