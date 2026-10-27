using UnityEngine;

// Cosmetic only: one reusable, world-space particle system per Bounce bullet.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class BounceWallVfx : MonoBehaviour
{
    [SerializeField, Range(1, 12)] private int minPieces = 4;
    [SerializeField, Range(1, 12)] private int maxPieces = 6;
    [SerializeField, Min(0.01f)] private float duration = 0.25f;
    [SerializeField] private Vector2 pieceSize = new Vector2(0.045f, 0.085f);
    [SerializeField] private Vector2 launchSpeed = new Vector2(1.2f, 2.4f);
    [SerializeField, Range(0, 180), Tooltip("Maximum deviation from the inward normal, in degrees. Directions outside the arena are rejected.")]
    private float spreadAngle = 65f;
    [SerializeField] private bool matchBulletColor = true;
    [SerializeField] private Color tint = new Color(0.2f, 0.4f, 0.6f, 1f);
    [SerializeField, Range(0f, 1f)] private float opacity = 0.9f;
    private ParticleSystem particles;
    private Material material;
    private SpriteRenderer source;
    // Keep decorative randomness separate from gameplay's Unity random state.
    private readonly System.Random random = new System.Random();

    private bool Prepare()
    {
        if (particles != null) return true;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null || !shader.isSupported) return false;
        source = GetComponent<SpriteRenderer>();
        GameObject child = new GameObject("Bounce Wall Squares");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = 128;
        main.gravityModifier = 0;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        // Keep readable square silhouettes; only contract slightly at the very end.
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(0.7f, 1), new Keyframe(1, 0.65f)));
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.6f));
        var rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.35f),
                new GradientAlphaKey(0.55f, 0.65f), new GradientAlphaKey(0.12f, 0.9f),
                new GradientAlphaKey(0, 1) });
        color.color = fade;
        material = new Material(shader) { mainTexture = Texture2D.whiteTexture };
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder - 1;
        return true;
    }

    public void Play(Vector2 position, Vector2 inward, Vector2[] corners, Vector2[] normals)
    {
        if (!isActiveAndEnabled || inward.sqrMagnitude < 0.0001f || !Prepare()) return;
        inward.Normalize();
        Color color = matchBulletColor ? source.color : tint;
        color.a *= opacity;
        int minimum = Mathf.Clamp(minPieces, 1, 12);
        int count = random.Next(minimum, Mathf.Clamp(maxPieces, minimum, 12) + 1);
        particles.Play(false);
        for (int i = 0; i < count; i++)
        {
            Vector2 heading = inward;
            // At corners reject directions that point through either adjacent wall.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                // Give each piece its own sector so the burst cannot bunch into a plume.
                float fraction = (i + Range(0.15f, 0.85f)) / count;
                float angle = (attempt == 0 ? Mathf.Lerp(-spreadAngle, spreadAngle, fraction)
                    : Range(-spreadAngle, spreadAngle)) * Mathf.Deg2Rad;
                Vector2 candidate = new Vector2(inward.x * Mathf.Cos(angle) - inward.y * Mathf.Sin(angle),
                    inward.x * Mathf.Sin(angle) + inward.y * Mathf.Cos(angle));
                bool valid = true;
                for (int wall = 0; wall < corners.Length; wall++)
                    if (Vector2.Dot(position - corners[wall], normals[wall]) < 0.001f &&
                        Vector2.Dot(candidate, normals[wall]) < 0f) valid = false;
                if (valid) { heading = candidate; break; }
            }
            var piece = new ParticleSystem.EmitParams {
                position = new Vector3(position.x, position.y, 0),
                velocity = heading * Mathf.Max(0, Range(launchSpeed.x, launchSpeed.y)),
                startLifetime = Mathf.Max(0.01f, duration),
                startSize = Mathf.Max(0.001f, Range(pieceSize.x, pieceSize.y)),
                startColor = color,
                rotation = Range(0, 90)
            };
            particles.Emit(piece, 1);
        }
    }

    private float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
    private void OnDisable()
    {
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
