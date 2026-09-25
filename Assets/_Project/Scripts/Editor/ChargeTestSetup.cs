using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChargeTestSetup
{
    [MenuItem("CONTEXT/PatternEmitter/Assign Test References")]
    private static void Assign(MenuCommand command)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play mode before assigning persistent test references.");
            return;
        }
        PatternEmitter emitter = (PatternEmitter)command.context;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Bullet_ChargeTest.prefab");
        PatternBullet bullet = prefab != null ? prefab.GetComponent<PatternBullet>() : null;
        PlayerHealth player = null;
        foreach (PlayerHealth candidate in Resources.FindObjectsOfTypeAll<PlayerHealth>())
        {
            if (EditorUtility.IsPersistent(candidate) || candidate.gameObject.scene != emitter.gameObject.scene) continue;
            if (player != null)
            {
                Debug.LogError("Multiple players in this scene. Assign Player manually.", emitter);
                return;
            }
            player = candidate;
        }
        if (bullet == null || player == null)
        {
            Debug.LogError("Cannot find Bullet_ChargeTest prefab with PatternBullet, or scene PlayerHealth.", emitter);
            return;
        }
        SerializedObject settings = new SerializedObject(emitter);
        settings.Update();
        settings.FindProperty("bulletPrefab").objectReferenceValue = bullet;
        settings.FindProperty("player").objectReferenceValue = player;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(emitter.gameObject.scene);
        Debug.Log("Test references assigned. Press Ctrl+S to save the scene before Play.", emitter);
    }
}
