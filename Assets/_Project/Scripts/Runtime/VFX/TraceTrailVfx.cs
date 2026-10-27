using System.Collections.Generic;
using UnityEngine;

// Sparse triangular graphite chips; cosmetic only, with independent world-space motion.
[RequireComponent(typeof(TraceBullet), typeof(SpriteRenderer))]
public sealed class TraceTrailVfx : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float pieceLifetime = 0.2f;
    [SerializeField, Range(1, 8)] private int maxVisiblePieces = 3;
    [SerializeField, Tooltip("World-space triangle size range.")]
    private Vector2 pieceSize = new Vector2(0.045f, 0.075f);
    [SerializeField, Tooltip("Random seconds between individual chips; no bursts.")]
    private Vector2 emissionInterval = new Vector2(0.065f, 0.09f);
    [SerializeField, Min(0.01f), Tooltip("Minimum distance travelled before another chip may appear.")]
    private float minSpacing = 0.12f;
    [SerializeField, Min(0f), Tooltip("Minimum distance behind the projectile center, in world units.")]
    private float attachmentOffset = 0.17f;
    [SerializeField, Tooltip("Sideways offset at birth, in world units.")]
    private Vector2 sideOffset = new Vector2(0.025f, 0.06f);
    [SerializeField, Range(0f, 1f)] private float opacity = 0.65f;
    [SerializeField] private bool matchBulletColor = true;
    [SerializeField, Tooltip("Used when Match Bullet Color is off.")]
    private Color tint = new Color(0.28f, 0.4f, 0.31f, 1f);
    [SerializeField, Range(0f, 1f), Tooltip("Blend toward light grey so chips are less prominent than the bullet.")]
    private float colorLightening = 0.18f;

    private ParticleSystem particles;
    private ParticleSystemRenderer particleRenderer;
    private Mesh triangle;
    private Material material;
    private SpriteRenderer source;
    private TraceBullet bullet;
    private bool flying;
    private float nextEmission, distanceSinceEmission;
    private Vector2 previousPosition;
    private int sideSign;
    private Color activeColor;
    // Keep decorative randomness separate from gameplay's Unity random state.
    private readonly System.Random random = new System.Random();
    private readonly ParticleSystem.Particle[] remaining = new ParticleSystem.Particle[8];

    private void Awake()
    {
        source = GetComponent<SpriteRenderer>();
        bullet = GetComponent<TraceBullet>();
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null || !shader.isSupported) { enabled = false; return; }
        GameObject child = new GameObject("Trace Graphite Fragments");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        particles = child.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = Mathf.Clamp(maxVisiblePieces, 1, 8);
        main.gravityModifier = 0;
        main.startSpeed = 0;
        main.startRotation3D = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(0.3f, 0.9f), new Keyframe(1, 0.25f)));
        var spin = particles.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
        var color = particles.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.85f, 0.25f),
                new GradientAlphaKey(0.3f, 0.7f), new GradientAlphaKey(0, 1) });
        color.color = fade;

        // Mesh vertices are in local units; this does not use Sprite.OverrideGeometry.
        triangle = new Mesh { name = "Graphite chip triangle" };
        triangle.vertices = new[] { new Vector3(-0.5f, -0.3f, 0),
            new Vector3(0.45f, -0.35f, 0), new Vector3(0.1f, 0.55f, 0) };
        triangle.triangles = new[] { 0, 1, 2 };
        triangle.uv = new[] { Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f };
        // Leave mesh colors absent: the mesh-particle Color stream supplies startColor.
        // Adding a mesh COLOR channel here corrupts this Sprites/Default particle path.
        triangle.RecalculateBounds();
        material = new Material(shader) { mainTexture = Texture2D.whiteTexture, color = Color.white };
        particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = material;
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = triangle;
        particleRenderer.enableGPUInstancing = false;
        particleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream> {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
        });
        particleRenderer.alignment = ParticleSystemRenderSpace.World;
        particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }

    public void BeginFlight()
    {
        if (!isActiveAndEnabled || particles == null) return;
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleRenderer.sortingLayerID = source.sortingLayerID;
        particleRenderer.sortingOrder = source.sortingOrder - 1;
        var main = particles.main;
        main.maxParticles = Mathf.Clamp(maxVisiblePieces, 1, 8);
        previousPosition = transform.position;
        distanceSinceEmission = 0;
        nextEmission = Time.time + NextInterval();
        sideSign = random.Next(2) == 0 ? -1 : 1;
        flying = true;
        UpdateParticleColor();
        particles.Play(false);
    }

    private void LateUpdate()
    {
        if (particles == null) return;
        UpdateParticleColor();
        if (!flying || !bullet.IsFlying) return;
        Vector2 position = transform.position;
        distanceSinceEmission += Vector2.Distance(previousPosition, position);
        previousPosition = position;
        if (Time.time < nextEmission || distanceSinceEmission < Mathf.Max(0.01f, minSpacing)) return;
        // Never catch up with a burst after a slow frame.
        nextEmission = Time.time + NextInterval();
        if (particles.particleCount >= Mathf.Clamp(maxVisiblePieces, 1, 8)) return;
        Vector2 heading = bullet.FlightDirection.normalized;
        if (heading.sqrMagnitude < 0.0001f) return;
        distanceSinceEmission = 0;
        Vector2 side = new Vector2(-heading.y, heading.x) * sideSign;
        sideSign = -sideSign;
        float size = Mathf.Max(0.001f, Range(pieceSize));
        // Keep visible separation from the bullet, including after size edits.
        float gap = Mathf.Max(attachmentOffset, source.bounds.extents.magnitude + size * 0.6f);
        Vector2 origin = position - heading * gap + side * Range(sideOffset);
        var chip = new ParticleSystem.EmitParams {
            position = new Vector3(origin.x, origin.y, 0),
            velocity = side * Range(new Vector2(0.12f, 0.3f)) - heading * 0.04f,
            startLifetime = Mathf.Max(0.01f, pieceLifetime),
            startSize = size,
            startColor = activeColor,
            rotation3D = new Vector3(0, 0, Range(new Vector2(0, 360)))
        };
        particles.Emit(chip, 1);
    }

    private void UpdateParticleColor()
    {
        Color baseColor = matchBulletColor ? source.color : tint;
        Color color = Color.Lerp(baseColor, new Color(0.65f, 0.65f, 0.65f, baseColor.a), colorLightening);
        color.a = baseColor.a * opacity;
        if (color == activeColor) return;
        activeColor = color;
        // Retint existing chips too, so editing a running instance is immediately visible.
        int count = particles.GetParticles(remaining);
        for (int i = 0; i < count; i++) remaining[i].startColor = color;
        particles.SetParticles(remaining, count);
    }

    public void EndFlight(float remainingVisualTime)
    {
        flying = false;
        if (particles == null) return;
        particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        int count = particles.GetParticles(remaining);
        for (int i = 0; i < count; i++)
            remaining[i].remainingLifetime = Mathf.Min(remaining[i].remainingLifetime, Mathf.Max(0, remainingVisualTime));
        particles.SetParticles(remaining, count);
    }

    private float NextInterval() => Mathf.Max(0.01f, Range(emissionInterval));
    private float Range(Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
    private void OnDisable()
    {
        flying = false;
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (triangle != null) Destroy(triangle);
    }
}
