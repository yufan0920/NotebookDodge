using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImpactTestSetup
{
    private const string PrefabFolder = "Assets/_Project/Prefabs/";

    [MenuItem("Tools/Notebook Dodge/Create Impact Test")]
    private static void Create()
    {
        if (Application.isPlaying) { Debug.LogWarning("Stop Play mode first."); return; }
        var scene = EditorSceneManager.GetActiveScene();
        PlayerHealth player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            ImpactEmitter existing = root.GetComponentInChildren<ImpactEmitter>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("Impact test already exists; selected it without overwriting settings.", existing);
                return;
            }
            foreach (PlayerHealth candidate in root.GetComponentsInChildren<PlayerHealth>(true))
            {
                if (player != null) { Debug.LogError("Multiple players found; configure the test manually."); return; }
                player = candidate;
            }
        }
        PlayerMovement movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        Vector2[] corners = new Vector2[4];
        if (movement == null || movement.MovementArea == null || !movement.MovementArea.TryCopyBoundary(corners))
        {
            Debug.LogError("Assign a valid Player Movement Area first.");
            return;
        }
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "Bullet.prefab");
        if (source == null || source.GetComponent<PatternBullet>() == null || source.GetComponent<SpriteRenderer>() == null)
        {
            Debug.LogError("Expected Bullet.prefab with PatternBullet and SpriteRenderer. Create prefabs manually if renamed.");
            return;
        }
        PatternBullet fragment = GetFragment(source);
        if (fragment == null) return;
        ImpactBullet bullet = GetImpact(fragment, source.GetComponent<SpriteRenderer>().sprite);
        if (bullet == null) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create Impact Test");
        GameObject rootObject = new GameObject("Impact_Test");
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Impact Test");
        GameObject emitterObject = new GameObject("Emitter_Impact");
        emitterObject.transform.SetParent(rootObject.transform, false);
        ImpactEmitter emitter = emitterObject.AddComponent<ImpactEmitter>();
        GameObject points = new GameObject("LandingPoints");
        points.transform.SetParent(rootObject.transform, false);
        SerializedObject settings = new SerializedObject(emitter);
        settings.FindProperty("bulletPrefab").objectReferenceValue = bullet;
        settings.FindProperty("player").objectReferenceValue = player;
        SerializedProperty list = settings.FindProperty("landingPoints");
        list.arraySize = 3;
        Vector2 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        Vector2 left = (corners[0] + corners[3]) * 0.5f;
        Vector2 right = (corners[1] + corners[2]) * 0.5f;
        Vector2[] positions = { Vector2.Lerp(center, left, 0.5f), center, Vector2.Lerp(center, right, 0.5f) };
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject point = new GameObject("Point_" + (i + 1).ToString("00"));
            point.transform.SetParent(points.transform, false);
            point.transform.position = positions[i];
            list.GetArrayElementAtIndex(i).objectReferenceValue = point.transform;
        }
        settings.ApplyModifiedPropertiesWithoutUndo();
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = emitterObject;
        Debug.Log("Impact test created. Move Point_01/02/03, save the scene, and solo Emitter_Impact in Emitter Test Panel.", emitter);
    }

    private static PatternBullet GetFragment(GameObject source)
    {
        string path = PrefabFolder + "Bullet_Fragment.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            GameObject template = Object.Instantiate(source);
            try
            {
                template.name = "Bullet_Fragment";
                template.transform.localScale = Vector3.one * 0.15f;
                SpriteRenderer visual = template.GetComponent<SpriteRenderer>();
                visual.color = Color.white;
                visual.sortingLayerName = "Bullet";
                SerializedObject settings = new SerializedObject(template.GetComponent<PatternBullet>());
                settings.FindProperty("useCharge").boolValue = false;
                settings.FindProperty("normalSpeed").floatValue = 3f;
                settings.FindProperty("damage").intValue = 1;
                settings.FindProperty("flightLifetime").floatValue = 6f;
                settings.ApplyModifiedPropertiesWithoutUndo();
                asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally { Object.DestroyImmediate(template); }
        }
        PatternBullet result = asset != null ? asset.GetComponent<PatternBullet>() : null;
        if (result == null) Debug.LogError(path + " must contain PatternBullet; existing asset was not changed.");
        return result;
    }

    private static ImpactBullet GetImpact(PatternBullet fragment, Sprite sprite)
    {
        string path = PrefabFolder + "Bullet_Impact.prefab";
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            GameObject template = new GameObject("Bullet_Impact");
            try
            {
                ImpactBullet bullet = template.AddComponent<ImpactBullet>();
                GameObject visualObject = new GameObject("Visual");
                visualObject.transform.SetParent(template.transform, false);
                visualObject.transform.localScale = new Vector3(0.5f, 0.8f, 1);
                SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
                visual.sprite = sprite;
                visual.color = Color.white;
                visual.sortingLayerName = "Bullet";
                visual.sortingOrder = 11;
                SerializedObject settings = new SerializedObject(bullet);
                settings.FindProperty("fallingVisual").objectReferenceValue = visual;
                settings.FindProperty("fragmentPrefab").objectReferenceValue = fragment;
                settings.ApplyModifiedPropertiesWithoutUndo();
                asset = PrefabUtility.SaveAsPrefabAsset(template, path);
            }
            finally { Object.DestroyImmediate(template); }
        }
        ImpactBullet result = asset != null ? asset.GetComponent<ImpactBullet>() : null;
        if (result == null) Debug.LogError(path + " must contain ImpactBullet; existing asset was not changed.");
        return result;
    }
}
