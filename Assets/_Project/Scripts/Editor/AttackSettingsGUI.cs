using UnityEditor;
using UnityEngine;

public static class AttackSettingsGUI
{
    private static void Field(SerializedProperty settings, string name, string label)
        => EditorGUILayout.PropertyField(settings.FindPropertyRelative(name), new GUIContent(label), true);
    public static void Draw(SerializedProperty settings, Object template)
    {
        if (template == null) { EditorGUILayout.HelpBox("Assign an emitter template first.", MessageType.Info); return; }
        if (template is PatternEmitter || template is BounceEmitter || template is DirectPatternEmitter)
        {
            Field(settings, "bulletCount", "Bullet Count");
            Field(settings, "spreadAngle", "Spread Angle");
        }
        if (template is TraceEmitter || template is BounceEmitter) Field(settings, "aimAtPlayer", "Aim At Player");
        Field(settings, "burstCount", "Burst Count");
        if (settings.FindPropertyRelative("burstCount").intValue > 1)
            Field(settings, "shotInterval", "Shot Interval (s)");
        Field(settings, "cooldown", "Cooldown (s)");
        if (template is ImpactEmitter) Field(settings, "landingPoints", "Landing Points (World XY)");
        string hint = template is LaserEmitter ? "Cooldown starts after the last laser in a burst disappears. Shot Interval also waits for the previous laser to finish."
            : template is PatternEmitter ? "Cooldown starts after the volley finishes preparation."
            : template is TraceEmitter ? "Cooldown starts after the last shot in a burst. Tracking preparation pauses the next-shot timer."
            : "Cooldown is the interval between attacks.";
        EditorGUILayout.HelpBox(hint, MessageType.None);
    }
    public static void Reset(SerializedProperty settings, Object template)
    {
        settings.FindPropertyRelative("bulletCount").intValue = 1;
        settings.FindPropertyRelative("spreadAngle").floatValue = 40;
        settings.FindPropertyRelative("aimAtPlayer").boolValue = template is TraceEmitter || template is BounceEmitter;
        settings.FindPropertyRelative("burstCount").intValue = 1;
        settings.FindPropertyRelative("shotInterval").floatValue = 0.5f;
        settings.FindPropertyRelative("cooldown").floatValue = 2;
        settings.FindPropertyRelative("landingPoints").ClearArray();
    }
}

[CustomEditor(typeof(EmitterTestDriver))]
public class EmitterTestDriverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("Prototype test schedule only. Timeline clips own the settings in Level_test.", MessageType.Info);
        var emitter = serializedObject.FindProperty("emitter");
        EditorGUILayout.PropertyField(emitter);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("player"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialDelay"));
        AttackSettingsGUI.Draw(serializedObject.FindProperty("attack"), emitter.objectReferenceValue);
        serializedObject.ApplyModifiedProperties();
    }
}
