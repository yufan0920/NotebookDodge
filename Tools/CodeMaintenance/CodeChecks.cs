using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Offline regression checks. No GameObjects, native rendering or Play Mode are started.
internal static class CodeChecks
{
    private static int assertions;

    private static void Require(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private static void Near(float actual, float expected, string message)
        => Require(Math.Abs(actual - expected) < 0.000001f, message + ": " + actual + " != " + expected);

    private static object Call(Type type, string method, params object[] args)
        => type.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);

    private static void SheetCoordinates()
    {
        Type uvMath = typeof(LandingExplosionVfx).Assembly.GetType("SpriteSheetUv", true);
        foreach (int cell in new[] { 64, 192 })
        foreach (int rows in new[] { 2, 3 })
        {
            int width = 8 * cell, height = rows * cell;
            for (int frame = 0; frame < rows * 8; frame++)
            {
                int column = frame % 8, row = frame / 8;
                Rect rect = (Rect)Call(uvMath, "GridFrame", frame, 8, cell, height);
                Near(rect.x, column * cell, "frame x");
                Near(rect.y, height - (row + 1) * cell, "frame y");
                Vector2[] uv = new Vector2[4];
                Call(uvMath, "WriteQuad", uv, rect, width, height);
                Near(uv[0].x, (column * cell + 0.5f) / width, "left inset");
                Near(uv[1].x, ((column + 1) * cell - 0.5f) / width, "right inset");
                Near(uv[0].y, (height - (row + 1) * cell + 0.5f) / height, "bottom inset");
                Near(uv[2].y, (height - row * cell - 0.5f) / height, "top inset");
                int next = Math.Min(frame + 1, rows * 8 - 1);
                Rect nextRect = (Rect)Call(uvMath, "GridFrame", next, 8, cell, height);
                Vector4 offset = (Vector4)Call(uvMath, "BlendOffset", rect, nextRect, width, height);
                Near(offset.x, (next % 8 - column) * cell / (float)width, "next column");
                Near(offset.y, (row - next / 8) * cell / (float)height, "next row");
            }
        }
        Rect ink = new Rect(2, 534, 264, 264);
        Rect inkNext = new Rect(268, 534, 264, 264);
        Vector4 inkOffset = (Vector4)Call(uvMath, "BlendOffset", ink, inkNext, 800, 800);
        Near(inkOffset.x, 266f / 800f, "Ink2 unequal grid spacing");
        Near(inkOffset.y, 0f, "Ink2 same row");
        Vector4 rowOffset = (Vector4)Call(uvMath, "BlendOffset", new Rect(534, 534, 264, 264),
            new Rect(2, 268, 264, 264), 800, 800);
        Near(rowOffset.x, -532f / 800f, "Ink2 row transition x");
        Near(rowOffset.y, -266f / 800f, "Ink2 row transition y");
    }

    private static void Playback()
    {
        Type landing = typeof(LandingExplosionVfx);
        Require((int)Call(landing, "FrameAtProgress", 0f, 9) == 0, "first landing frame");
        Require((int)Call(landing, "FrameAtProgress", 1f, 9) == 8, "last landing frame");
        Near((float)Call(landing, "FadeAtProgress", 0.9f, 0.5f), 0.2f, "landing fade");
        Near((float)Call(landing, "FadeAtProgress", 1f, 0.7f), 0f, "no hard end alpha");
        int[] expected = { 3, 4, 5, 4, 3, 4, 5, 4 };
        for (int i = 0; i < expected.Length; i++)
            Require((int)Call(typeof(LaserLineBlastVfx), "GetSustainFrame", 3, 5, i) == expected[i],
                "laser sustain ping-pong");
    }

    private static void Scheduling()
    {
        var starts = new List<float> { 1f, 3f };
        var fired = new List<int>();
        var reasons = new List<string>();
        var schedule = new AbsoluteAttackSchedule(starts, 2, 0.25f, 1.1f, 3.1f);
        schedule.Tick(1.25f, i => { fired.Add(i); return null; }, (planned, actual, reason) => reasons.Add(reason));
        schedule.Tick(3.2f, i => { fired.Add(i); return null; }, (planned, actual, reason) => reasons.Add(reason));
        Require(fired.Count == 1 && fired[0] == 1, "range retains full-track shot indices");
        Require(reasons.Count == 2 && reasons[1] == "Missed frame deadline", "late shot skipped");
        Require(schedule.IsComplete, "missed shot consumed, not postponed");
        fired.Clear();
        var busy = new AbsoluteAttackSchedule(new List<float> { 1f }, 2, 0.25f);
        busy.Tick(1f, i => "busy", (planned, actual, reason) => { });
        busy.Tick(1.25f, i => { fired.Add(i); return null; }, (planned, actual, reason) => { });
        Require(fired.Count == 1 && fired[0] == 1, "busy first shot does not shift second shot");
        Require(EndlessAttackLoop.RepeatNumber(533f, 382f, 533f) == 1, "first endless boundary");
        Near(EndlessAttackLoop.PatternTime(533f, 382f, 533f), 382f, "first repeat time");
        Require(EndlessAttackLoop.RepeatNumber(684f, 382f, 533f) == 2, "second endless boundary");
        Near(EndlessAttackLoop.PatternTime(684f, 382f, 533f), 382f, "second repeat time");
        var waves = EndlessAttackLoop.RepeatBursts(new List<float> { 380f, 382f, 532f }, 2, 2f, 382f, 533f, 1);
        Require(waves.Count == 2, "repeat excludes starts before loop range");
        Near(waves[0][0].time, 533f, "shifted first burst");
        Near(waves[1][1].time, 685f, "burst may finish across loop boundary");
        Require(waves[0][0].index == 2 && waves[1][1].index == 5, "repeat keeps target indices");
    }

    private static bool Contains(List<Vector2> polygon, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    private static void LaserDamageWidth()
    {
        Type geometry = typeof(LaserBeam).Assembly.GetType("LaserGeometry", true);
        foreach (int degrees in new[] { 0, 45, 90, 135 })
        {
            float cosine = (float)Math.Cos(degrees * Math.PI / 180);
            float sine = (float)Math.Sin(degrees * Math.PI / 180);
            var corners = new[] { new Vector2(-4, -3), new Vector2(4, -3), new Vector2(4, 3), new Vector2(-4, 3) };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 world = corners[i];
                corners[i] = new Vector2(world.x * cosine + world.y * sine, -world.x * sine + world.y * cosine);
            }
            var polygon = new List<Vector2>();
            var scratch = new List<Vector2>();
            Call(geometry, "BuildStroke", corners, 0f, 12f, -0.05f, 0.05f, polygon, scratch);
            Require(Contains(polygon, new Vector2(1, 0)), "laser core is damaging at " + degrees);
            Require(Contains(polygon, new Vector2(1, 0.049f)), "laser inner edge is damaging");
            Require(!Contains(polygon, new Vector2(1, 0.051f)), "just outside damage width is safe");
            Require(!Contains(polygon, new Vector2(1, 0.2f)), "visual glow is safe");
            foreach (Vector2 local in polygon)
            {
                float x = local.x * cosine - local.y * sine;
                float y = local.x * sine + local.y * cosine;
                Require(x >= -4.00001f && x <= 4.00001f && y >= -3.00001f && y <= 3.00001f,
                    "damage strip remains clipped to movement area");
            }
            Call(geometry, "BuildStroke", corners, 0f, 2f, -0.05f, 0.05f, polygon, scratch);
            Require(Contains(polygon, new Vector2(1.99f, 0)), "drawn part can damage");
            Require(!Contains(polygon, new Vector2(2.01f, 0)), "undrawn part cannot damage");
        }
    }

    public static void Main()
    {
        SheetCoordinates();
        Playback();
        Scheduling();
        LaserDamageWidth();
        Console.WriteLine(assertions + " regression assertions passed (UVs, fades, laser sustain/width, scheduling and P3).");
    }
}
