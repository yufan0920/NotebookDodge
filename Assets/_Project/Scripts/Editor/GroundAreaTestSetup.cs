using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GroundAreaTestSetup
{
    [MenuItem("Tools/Notebook Dodge/Create Ink Test")]
    private static void Create()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode before creating the ink test."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        PlayerHealth player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (PlayerHealth candidate in root.GetComponentsInChildren<PlayerHealth>(true))
            {
                if (player != null) { Debug.LogError("Multiple players found; assign the ink emitter manually."); return; }
                player = candidate;
            }
        if (player == null) { Debug.LogError("No PlayerHealth found in the active scene."); return; }
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bullet_Trace.prefab");
        if (original == null || original.GetComponent<TraceBullet>() == null)
        {
            Debug.LogError("Expected Bullet_Trace.prefab with TraceBullet; create the ink prefab manually if it was renamed.");
            return;
        }
        GroundArea burn = GetArea("GroundArea_Burn", true);
        GroundArea slow = GetArea("GroundArea_Slow", false);
        if (burn == null || slow == null) return;
        TraceBullet burnBullet = GetBullet("Bullet_InkBurn", original, burn);
        TraceBullet slowBullet = GetBullet("Bullet_InkSlow", original, slow);
        if (burnBullet == null || slowBullet == null) return;
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (TraceEmitter existing in root.GetComponentsInChildren<TraceEmitter>(true))
                if (existing.name == "Emitter_Ink")
                {
                    Selection.activeGameObject = existing.gameObject;
                    Debug.Log("Ink test already exists. Existing settings were preserved.", existing);
                    return;
                }
        GameObject obj = new GameObject("Emitter_Ink");
        Undo.RegisterCreatedObjectUndo(obj, "Create Ink Test");
        obj.transform.position = new Vector3(0, 4, 0);
        TraceEmitter emitter = Undo.AddComponent<TraceEmitter>(obj);
        SerializedObject settings = new SerializedObject(emitter);
        settings.FindProperty("dropPrefab").objectReferenceValue = burnBullet;
        settings.FindProperty("player").objectReferenceValue = player;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = obj;
        Debug.Log("Ink test created. Swap Drop Prefab between Bullet_InkBurn and Bullet_InkSlow. Save the scene and solo Emitter_Ink in Emitter Test Panel.", emitter);
    }

    private static GroundArea GetArea(string name, bool burn)
    {
        string path = "Assets/_Project/Prefabs/" + name + ".prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            GameObject template = new GameObject(name);
            try
            {
                GroundArea area = template.AddComponent<GroundArea>();
                SerializedObject settings = new SerializedObject(area);
                settings.FindProperty("burnEnabled").boolValue = burn;
                settings.FindProperty("slowEnabled").boolValue = !burn;
                settings.FindProperty("areaColor").colorValue = burn
                    ? new Color(0.7f, 0.12f, 0.3f, 0.35f) : new Color(0.2f, 0.3f, 0.8f, 0.35f);
                settings.ApplyModifiedPropertiesWithoutUndo();
                asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally { Object.DestroyImmediate(template); }
        }
        GroundArea result = asset != null ? asset.GetComponent<GroundArea>() : null;
        if (result == null) Debug.LogError(path + " must contain GroundArea; existing asset was not overwritten.");
        return result;
    }

    private static TraceBullet GetBullet(string name, GameObject original, GroundArea area)
    {
        string path = "Assets/_Project/Prefabs/" + name + ".prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            GameObject template = Object.Instantiate(original);
            template.name = name;
            try
            {
                SerializedObject settings = new SerializedObject(template.GetComponent<TraceBullet>());
                settings.FindProperty("groundAreaPrefab").objectReferenceValue = area;
                settings.FindProperty("damage").intValue = 0;
                settings.FindProperty("explosionRadius").floatValue = 1f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally { Object.DestroyImmediate(template); }
        }
        TraceBullet result = asset != null ? asset.GetComponent<TraceBullet>() : null;
        if (result == null) Debug.LogError(path + " must contain TraceBullet; existing asset was not overwritten.");
        return result;
    }
}
