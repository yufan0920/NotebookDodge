using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

//预计攻击阶段时间、提示冲突、等等时间信息
public static class AttackTimingPreview
{
    private static float Number(SerializedObject obj, string name, float fallback = 0)
        => obj.FindProperty(name)?.floatValue ?? fallback;
    private static bool Flag(SerializedObject obj, string name)
        => obj.FindProperty(name)?.boolValue ?? false;
    private static string Range(float a, float b) => $"{a:F2}-{b:F2}s";
    private static void GetTiming(Object template, SerializedObject data, out float prepare, out float busy, out float hazard, out float tail)
    {
        prepare = busy = hazard = tail = 0;
        bool dynamicImpact = template is TraceEmitter;
        if (template is PatternEmitter)
        {
            prepare = Flag(data, "useCharge") ? Mathf.Max(0, Number(data, "unfoldDuration")) + Mathf.Max(0, Number(data, "holdDuration")) : 0;
            busy = prepare;
            hazard = Mathf.Max(0.1f, Number(data, "flightLifetime"));
        }
        else if (template is LaserEmitter)
        {
            prepare = Mathf.Max(0.05f, Number(data, "warningDuration")) + Mathf.Max(0, Number(data, "chargeDuration"));
            float remaining = Mathf.Max(0, Number(data, "beamLength") - Mathf.Max(0.01f, Number(data, "initialLength")) - Mathf.Max(0, Number(data, "chargeAdvanceDistance")));
            float acceleration = Mathf.Max(0, Number(data, "accelerationDuration"));
            float speed = Mathf.Max(0.01f, Number(data, "drawSpeed"));
            var curve = data.FindProperty("speedCurve")?.animationCurveValue;
            float draw = 0, step = Mathf.Max(0.001f, Time.fixedDeltaTime);
            // Integrate the configured curve; after acceleration the beam advances at full speed.
            for (int i = 0; remaining > 0 && draw < acceleration && i < 100000; i++)
            {
                draw += step;
                float multiplier = draw < acceleration && curve != null && curve.length > 0 ? Mathf.Max(0, curve.Evaluate(draw / acceleration)) : 1;
                remaining -= speed * multiplier * step;
            }
            draw += Mathf.Ceil(Mathf.Max(0, remaining) / speed / step) * step;
            hazard = draw + Mathf.Max(0, Number(data, "holdDuration"));
            tail = Mathf.Max(0, Number(data, "fadeDuration"));
            busy = prepare + hazard + tail + 3 * step;
        }
        else if (dynamicImpact)
        {
            prepare = Mathf.Max(0, Number(data, "preLaunchDuration"));
            busy = Flag(data, "trackDuringWarning") ? prepare : 0;
        }
        else if (template is ImpactEmitter)
        {
            prepare = Mathf.Max(0, Number(data, "warningDuration")) + Mathf.Max(0.01f, Number(data, "fallDuration"));
        }
        else hazard = Number(data, "lifetime", 0);
    }

    public static void DrawTrackConflicts(SerializedProperty clips, Object template)
    {
        if (template == null) return;
        var source = new SerializedObject(template);
        var projectile = source.FindProperty("bulletPrefab")?.objectReferenceValue
            ?? source.FindProperty("dropPrefab")?.objectReferenceValue
            ?? source.FindProperty("beamPrefab")?.objectReferenceValue;
        if (projectile == null) return;
        float prepare, busy, hazard, tail;
        var data = new SerializedObject(projectile);
        GetTiming(template, data, out prepare, out busy, out hazard, out tail);
        // Non-tracking Trace warnings still need the emitter to stay until launch.
        float exitBusy = template is TraceEmitter ? Mathf.Max(0, Number(data, "preLaunchDuration")) : busy;
        var ordered = new List<SerializedProperty>();
        for (int i = 0; i < clips.arraySize; i++)
        {
            var clip = clips.GetArrayElementAtIndex(i);
            if (clip.FindPropertyRelative("enabled").boolValue) ordered.Add(clip);
        }
        ordered.Sort((a, b) => a.FindPropertyRelative("appearAt").floatValue.CompareTo(b.FindPropertyRelative("appearAt").floatValue));
        float previousRelease = 0, previousExit = 0;
        for (int c = 0; c < ordered.Count; c++)
        {
            var clip = ordered[c];
            float at = clip.FindPropertyRelative("appearAt").floatValue;
            bool move = clip.FindPropertyRelative("transition").enumValueIndex == 1;
            float ready = Mathf.Max(at, previousRelease + (move ? 0 : previousExit)) + clip.FindPropertyRelative("enterDuration").floatValue;
            var times = clip.FindPropertyRelative("attackTimes");
            var settings = clip.FindPropertyRelative("attack");
            int count = settings.FindPropertyRelative("burstCount").intValue;
            if (count < 1 || (long)count * times.arraySize > 10000) continue;
            float first = float.PositiveInfinity, last = float.NegativeInfinity;
            for (int i = 0; i < times.arraySize; i++)
            {
                float start = times.GetArrayElementAtIndex(i).floatValue;
                first = Mathf.Min(first, start);
                last = Mathf.Max(last, start + (count - 1) * settings.FindPropertyRelative("shotInterval").floatValue);
            }
            if (c > 0 && first < ready - 0.0001f)
                EditorGUILayout.HelpBox(clip.FindPropertyRelative("label").stringValue + $": first attack at {first:F2}s is before estimated arrival {ready:F2}s. Allow time for the previous attack and exit, use Move, or use a separate track.", MessageType.Warning);
            previousRelease = Mathf.Max(clip.FindPropertyRelative("stopAt").floatValue, last + exitBusy);
            previousExit = clip.FindPropertyRelative("exitDuration").floatValue;
        }
    }
    public static void Draw(SerializedProperty clip, Object template)
    {
        if (template == null) return;
        var source = new SerializedObject(template);
        Object projectile = source.FindProperty("bulletPrefab")?.objectReferenceValue
            ?? source.FindProperty("dropPrefab")?.objectReferenceValue
            ?? source.FindProperty("beamPrefab")?.objectReferenceValue;
        if (projectile == null) { EditorGUILayout.HelpBox("Assign the projectile prefab on the emitter.", MessageType.Warning); return; }
        var data = new SerializedObject(projectile);
        float prepare, busy, hazard, tail;
        GetTiming(template, data, out prepare, out busy, out hazard, out tail);
        bool dynamicImpact = template is TraceEmitter;
        var starts = new List<float>();
        var times = clip.FindPropertyRelative("attackTimes");
        for (int i = 0; i < times.arraySize; i++) starts.Add(times.GetArrayElementAtIndex(i).floatValue);
        var attack = clip.FindPropertyRelative("attack");
        int count = attack.FindPropertyRelative("burstCount").intValue;
        if (count < 1 || (long)count * starts.Count > 10000) return;
        var shots = AbsoluteAttackSchedule.Expand(starts, count, attack.FindPropertyRelative("shotInterval").floatValue);
        var text = new StringBuilder("Attack Start (s)\tPhase Preview\n");
        float previous = float.NegativeInfinity;
        bool collision = false;
        EditorGUILayout.LabelField("Timing Preview (Estimated)", EditorStyles.boldLabel);
        for (int i = 0; i < shots.Count; i++)
        {
            float at = shots[i].time;
            if (at - previous < busy - 0.0001f) collision = true;
            previous = at;
            string phases;
            if (dynamicImpact)
            {
                phases = $"Prelaunch {Range(at, at + prepare)}; flight starts {at + prepare:F2}s; impact time varies";
                var area = data.FindProperty("groundAreaPrefab")?.objectReferenceValue;
                if (area != null) phases += $"; ground area lasts {Number(new SerializedObject(area), "lifetime"):F2}s after impact";
            }
            else if (template is ImpactEmitter)
                phases = $"Warning + fall {Range(at, at + prepare)}; explosion / fragments start ~{at + prepare:F2}s; fragment hits vary";
            else
                phases = (prepare > 0 ? $"Preparation {Range(at, at + prepare)}; " : "")
                    + (hazard > 0 ? $"potential hazard {Range(at + prepare, at + prepare + hazard)}" : $"flight starts {at:F2}s; lifetime depends on projectile")
                    + (tail > 0 ? $"; fade ends ~{at + prepare + hazard + tail:F2}s" : "");
            text.Append(at.ToString("F2")).Append('\t').Append(phases).Append('\n');
            if (i < 12) EditorGUILayout.LabelField($"{at:F2}s: {phases}", EditorStyles.wordWrappedMiniLabel);
        }
        if (shots.Count > 12) EditorGUILayout.LabelField($"... {shots.Count} shots total. Copy for the full list.");
        if (collision) EditorGUILayout.HelpBox("Possible timing conflict: an earlier attack may still be preparing or active. Increase spacing or use a separate emitter track. Runtime skips busy shots and logs them.", MessageType.Warning);
        EditorGUILayout.HelpBox("Stage estimates have frame / physics-step tolerance. Hits and tracking can end hazards early or change impact time. Loop end clears all remaining attacks.", MessageType.None);
        if (GUILayout.Button("Copy Timing Preview (Excel)")) EditorGUIUtility.systemCopyBuffer = text.ToString();
    }
}
