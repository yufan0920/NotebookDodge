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

    // Keep actionable warnings visible when the detailed timing preview is collapsed.
    public static void DrawWarnings(SerializedProperty clip, Object template)
    {
        if (template == null) return;
        using (var source = new SerializedObject(template))
        {
            Object projectile = source.FindProperty("bulletPrefab")?.objectReferenceValue
                ?? source.FindProperty("dropPrefab")?.objectReferenceValue
                ?? source.FindProperty("beamPrefab")?.objectReferenceValue;
            if (projectile == null)
            {
                EditorGUILayout.HelpBox("Assign the projectile prefab on the emitter.", MessageType.Warning);
                return;
            }
            var starts = new List<float>();
            foreach (SerializedProperty time in clip.FindPropertyRelative("attackTimes")) starts.Add(time.floatValue);
            var attack = clip.FindPropertyRelative("attack");
            int count = attack.FindPropertyRelative("burstCount").intValue;
            if (count < 1 || (long)count * starts.Count > 10000) return;
            using (var data = new SerializedObject(projectile))
            {
                GetTiming(template, data, out _, out float busy, out _, out _);
                DrawReentryWarning(clip, starts, count, attack.FindPropertyRelative("shotInterval").floatValue,
                    template is TraceEmitter ? Mathf.Max(busy, Number(data, "preLaunchDuration")) : busy);
                var shots = AbsoluteAttackSchedule.Expand(starts, count, attack.FindPropertyRelative("shotInterval").floatValue);
                for (int i = 1; i < shots.Count; i++)
                    if (shots[i].time - shots[i - 1].time < busy - 0.0001f)
                    {
                        EditorGUILayout.HelpBox("Possible timing conflict: an earlier attack may still be active. Increase attack spacing or use another track. Busy shots are skipped.", MessageType.Warning);
                        return;
                    }
            }
        }
    }
    private static void DrawReentryWarning(SerializedProperty clip, List<float> starts, int count, float interval, float busy)
    {
        if (!clip.FindPropertyRelative("reenterPerAttack").boolValue) return;
        starts = new List<float>(starts);
        starts.Sort();
        float enter = clip.FindPropertyRelative("enterDuration").floatValue;
        float exit = clip.FindPropertyRelative("exitDuration").floatValue;
        for (int i = 1; i < starts.Count; i++)
        {
            float finishes = starts[i - 1] + (count - 1) * interval + busy + exit;
            float nextEntry = starts[i] - enter;
            if (finishes > nextEntry - 0.0001f)
            {
                EditorGUILayout.HelpBox($"Reentry conflict: attack at {starts[i - 1]:0.##}s may not finish exiting until ~{finishes:0.##}s; next entry starts at {nextEntry:0.##}s. Increase spacing and allow a small frame margin. A blocked wave is skipped, never delayed.", MessageType.Warning);
                break;
            }
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
        DrawReentryWarning(clip, starts, count, attack.FindPropertyRelative("shotInterval").floatValue,
                    template is TraceEmitter ? Mathf.Max(busy, Number(data, "preLaunchDuration")) : busy);
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
                if (area != null)
                {
                    using (var ground = new SerializedObject(area))
                    {
                        float lifetime = GroundArea.ResolveLifetime(Number(ground, "lifetime"),
                            attack.FindPropertyRelative("groundLifetime").floatValue);
                        phases += $"; ground area lasts {lifetime:F2}s after impact";
                    }
                }
            }
            else if (template is BounceEmitter)
                phases = $"Flight starts {at:F2}s; keeps bouncing until game / test end; player hits do not remove it";
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
        EditorGUILayout.HelpBox("Stage estimates have frame / physics-step tolerance. Hits and tracking can end hazards early or change impact time. Game / range-test end clears remaining attacks; endless repeat boundaries do not.", MessageType.None);
        if (GUILayout.Button("Copy Timing Preview (Excel)")) EditorGUIUtility.systemCopyBuffer = text.ToString();
    }
}
