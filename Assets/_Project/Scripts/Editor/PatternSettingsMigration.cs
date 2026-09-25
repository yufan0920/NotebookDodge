using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PatternSettingsMigration
{
    [MenuItem("CONTEXT/PatternEmitter/Migrate Old Bullet Settings")]
    private static void Migrate(MenuCommand command)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        PatternEmitter emitter = (PatternEmitter)command.context;
        SerializedObject source = new SerializedObject(emitter);
        PatternBullet original = source.FindProperty("bulletPrefab").objectReferenceValue as PatternBullet;
        if (original == null || !EditorUtility.IsPersistent(original))
        {
            Debug.LogError("Assign a PatternBullet prefab asset first.", emitter);
            return;
        }
        // 单独复制资源，避免多个发射器不同的旧参数互相覆盖。
        string oldPath = AssetDatabase.GetAssetPath(original);
        string path = AssetDatabase.GenerateUniqueAssetPath(
            System.IO.Path.GetDirectoryName(oldPath).Replace('\\', '/') + "/Bullet_PatternConfigured.prefab");
        if (!AssetDatabase.CopyAsset(oldPath, path)) return;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            PatternBullet target = root.GetComponentInChildren<PatternBullet>(true);
            SerializedObject destination = new SerializedObject(target);
            foreach (string name in new[] { "useCharge", "unfoldDistance", "unfoldDuration", "holdDuration",
                "normalSpeed", "accelerationDuration", "speedCurve", "flightLifetime" })
                destination.CopyFromSerializedProperty(source.FindProperty(name));
            destination.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        source.FindProperty("bulletPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<PatternBullet>(true);
        source.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(emitter.gameObject.scene);
        Debug.Log("Copied old bullet settings to " + path + ". Save your scene. Configure the bullet prefab from now on.", emitter);
    }
}
