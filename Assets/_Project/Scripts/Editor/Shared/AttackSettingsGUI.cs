using UnityEditor;
using UnityEngine;

//绘制数量、夹角、连射等参数输入框
public static class AttackSettingsGUI
{
    private static void Field(SerializedProperty settings, string name, string label)
        => EditorGUILayout.PropertyField(settings.FindPropertyRelative(name), new GUIContent(label), true);
    public static void Draw(SerializedProperty settings, Object template, bool absoluteTimes = false, bool showHint = true)
    {
        if (template == null) { EditorGUILayout.HelpBox("Assign an emitter template first.", MessageType.Info); return; }
        if (template is PatternEmitter || template is BounceEmitter)
        {
            Field(settings, "bulletCount", "Bullet Count");
            Field(settings, "spreadAngle", "Spread Angle");
        }
        if (template is TraceEmitter || template is BounceEmitter) Field(settings, "aimAtPlayer", "Aim At Player");
        if (absoluteTimes && template is TraceEmitter ink && ink.DefaultGroundLifetime > 0)
        {
            var lifetime = settings.FindPropertyRelative("groundLifetime");
            float displayed = GroundArea.ResolveLifetime(ink.DefaultGroundLifetime, lifetime.floatValue);
            EditorGUI.BeginChangeCheck();
            float value = EditorGUILayout.FloatField(new GUIContent("Ground Lifetime (s)",
                "How long the ground area lasts after impact. Applies only to this track. Until edited, uses the ground prefab lifetime."), displayed);
            if (EditorGUI.EndChangeCheck()) lifetime.floatValue = Mathf.Max(0.1f, value);
        }
        Field(settings, "burstCount", "Burst Count");
        if (settings.FindPropertyRelative("burstCount").intValue > 1)
            Field(settings, "shotInterval", "Shot Interval (s)");
        if (!absoluteTimes) Field(settings, "cooldown", "Cooldown (s)");
        if (template is ImpactEmitter) DrawLandingPoints(settings);
        string hint = absoluteTimes ? "Each Attack Time starts one burst. Burst shots use exact offsets; busy shots are skipped with a warning, never delayed." : template is LaserEmitter ? "Cooldown starts after the last laser in a burst disappears. Shot Interval also waits for the previous laser to finish."
            : template is PatternEmitter ? "Cooldown starts after the volley finishes preparation."
            : template is TraceEmitter ? "Cooldown starts after the last shot in a burst. Tracking preparation pauses the next-shot timer."
            : "Cooldown is the interval between attacks.";
        if (showHint) EditorGUILayout.HelpBox(hint, MessageType.None);
    }
    private static void DrawLandingPoints(SerializedProperty settings)
    {
        Field(settings, "landingPointSource", "Landing Point Source");
        if (settings.FindPropertyRelative("landingPointSource").enumValueIndex == 0)
        {
            Field(settings, "landingPoints", "Landing Points (World XY)");
            return;
        }
        var points = settings.FindPropertyRelative("landingPointObjects");
        EditorGUILayout.LabelField("Landing Points", EditorStyles.boldLabel);
        for (int i = 0; i < points.arraySize; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var point = points.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(point, new GUIContent("Point " + (i + 1),
                    "Drag a scene point here, or use the object picker. Its world XY position is read when the attack starts."));
                if (GUILayout.Button("-", GUILayout.Width(24)))
                {
                    point.objectReferenceValue = null;
                    points.DeleteArrayElementAtIndex(i);
                    break;
                }
            }
        }
        if (GUILayout.Button("+ Landing Point"))
        {
            int index = points.arraySize++;
            points.GetArrayElementAtIndex(index).objectReferenceValue = null;
        }
        EditorGUILayout.HelpBox("Drag points from this scene (for example Impact_Test / LandingPoints). Attacks use them in list order, then repeat. Positions are read at attack start.", MessageType.None);
    }

    public static void Reset(SerializedProperty settings, Object template)
    {
        settings.FindPropertyRelative("bulletCount").intValue = 1;
        settings.FindPropertyRelative("spreadAngle").floatValue = 40;
        settings.FindPropertyRelative("aimAtPlayer").boolValue = template is TraceEmitter || template is BounceEmitter;
        settings.FindPropertyRelative("burstCount").intValue = 1;
        settings.FindPropertyRelative("shotInterval").floatValue = 0.5f;
        settings.FindPropertyRelative("cooldown").floatValue = 2;
        settings.FindPropertyRelative("groundLifetime").floatValue = 0;
        settings.FindPropertyRelative("landingPointSource").enumValueIndex = 0;
        settings.FindPropertyRelative("landingPoints").ClearArray();
        settings.FindPropertyRelative("landingPointObjects").ClearArray();
    }
}
