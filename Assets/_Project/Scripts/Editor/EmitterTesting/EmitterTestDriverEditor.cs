using UnityEditor;

//在 Inspector 中显示测试驱动的参数设置
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
