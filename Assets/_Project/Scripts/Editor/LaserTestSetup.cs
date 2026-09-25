using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LaserTestSetup
{
    [MenuItem("Tools/Notebook Dodge/Create Laser Test")]
    private static void Create()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play mode before creating the laser test.");
            return;
        }
        PlayerHealth player = null;
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (PlayerHealth candidate in root.GetComponentsInChildren<PlayerHealth>(true))
            {
                if (player != null)
                {
                    Debug.LogError("Multiple players found. Create and assign the laser emitter manually.");
                    return;
                }
                player = candidate;
            }
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        Vector2[] corners = new Vector2[4];
        if (movement == null || movement.MovementArea == null || !movement.MovementArea.TryCopyBoundary(corners))
        {
            Debug.LogError("Assign a valid Movement Area on the Player first.");
            return;
        }
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (LaserEmitter existing in root.GetComponentsInChildren<LaserEmitter>(true))
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Laser emitter already exists; selected it instead of creating a duplicate.", existing);
                return;
            }
        const string path = "Assets/_Project/Prefabs/LaserBeam.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            GameObject template = new GameObject("LaserBeam");
            try
            {
                template.AddComponent<LaserBeam>();
                asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally { Object.DestroyImmediate(template); }
        }
        LaserBeam prefab = asset != null ? asset.GetComponent<LaserBeam>() : null;
        if (prefab == null)
        {
            Debug.LogError("LaserBeam.prefab must have a LaserBeam component. Existing asset was not overwritten.");
            return;
        }
        float left = corners[0].x, centerY = 0;
        foreach (Vector2 corner in corners) { left = Mathf.Min(left, corner.x); centerY += corner.y * 0.25f; }
        GameObject emitterObject = new GameObject("Emitter_Laser");
        Undo.RegisterCreatedObjectUndo(emitterObject, "Create Laser Test");
        emitterObject.transform.position = new Vector3(left - 0.5f, centerY, 0);
        LaserEmitter emitter = Undo.AddComponent<LaserEmitter>(emitterObject);
        SerializedObject settings = new SerializedObject(emitter);
        settings.FindProperty("beamPrefab").objectReferenceValue = prefab;
        settings.FindProperty("player").objectReferenceValue = player;
        settings.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = emitterObject;
        Debug.Log("Laser test created. Save the scene, then use Emitter Test Panel to solo Emitter_Laser.", emitter);
    }
}
