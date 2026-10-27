using UnityEngine;
using UnityEngine.SceneManagement;

// Keep the set of hazards in one place when adding a new attack type.
public static class AttackCleanup
{
    public static bool IsAttack(MonoBehaviour component)
        => component is PatternBullet || component is BounceBullet || component is TraceBullet
            || component is LaserBeam || component is ImpactBullet || component is GroundArea;

    // Null scene means all loaded scenes (the independent emitter test panel).
    // Timeline playback supplies its own scene and disables hazards immediately.
    public static void Clear(Scene? scene = null, bool deactivate = true)
    {
        if (!Application.isPlaying) return;
        foreach (MonoBehaviour attack in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (attack == null || !IsAttack(attack)) continue;
            Scene owner = attack.gameObject.scene;
            if (!owner.IsValid() || !owner.isLoaded || (scene.HasValue && owner != scene.Value)) continue;
            if (deactivate) attack.gameObject.SetActive(false);
            // Each attack owns disposal of its warnings, meshes and materials.
            Object.Destroy(attack.gameObject);
        }
    }
}
