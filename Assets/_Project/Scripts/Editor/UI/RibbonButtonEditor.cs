using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(RibbonButton)), CanEditMultipleObjects]
public class RibbonButtonEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        serializedObject.Update();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Ribbon Feedback", EditorStyles.boldLabel);
        foreach (string name in new[] { "artwork", "label", "visualRoot", "normalSprite", "selectedSprite",
            "selectedTextColor", "hoverScale", "pressedScale", "transitionSeconds" })
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
        serializedObject.ApplyModifiedProperties();
    }
}
