using System;

// Pure display conversion. Never writes to tracks, phase boundaries, or runtime clocks.
public static class AttackTimelineTimeView
{
    public static float Offset(int view, float p2, float p3) => view == 2 ? p2 : view == 3 ? p3 : 0;
    public static float ToLocal(float global, float offset) => global - offset;
    public static float ToGlobal(float local, float offset) => local + offset;
    public static int PhaseAt(float global, float p2, float p3) => global >= p3 ? 3 : global >= p2 ? 2 : 1;
    public static bool Overlaps(float appear, float stop, float from, float to) => appear < to && stop > from;
    public static string BoundaryError(float p2, float p3)
        => float.IsNaN(p2) || float.IsInfinity(p2) || float.IsNaN(p3) || float.IsInfinity(p3) || p2 <= 0 || p3 <= p2
            ? "Phase starts must be finite and ordered: 0 < P2 < P3." : null;
    public static string Clock(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--";
        double rounded = Math.Round(Math.Abs((double)seconds), 2, MidpointRounding.AwayFromZero);
        double minutes = Math.Floor(rounded / 60);
        double remainder = rounded - minutes * 60;
        return (seconds < 0 && rounded > 0 ? "-" : "") + minutes.ToString("0", System.Globalization.CultureInfo.InvariantCulture)
            + ":" + remainder.ToString("00.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
