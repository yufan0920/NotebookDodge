using UnityEngine;

// Deforms only a display copy; the root renderer remains the Animator's target.
[RequireComponent(typeof(BounceBullet), typeof(SpriteRenderer))]
public sealed class BounceSquashVfx : MonoBehaviour
{
    [SerializeField, Range(0.4f, 1f), Tooltip("碰壁方向压缩后的比例；越小越扁。")]
    private float squashScale = 0.7f;
    [SerializeField, Range(1f, 1.6f), Tooltip("沿反弹方向拉长的比例。")]
    private float stretchScale = 1.18f;
    [SerializeField, Min(0.01f), Tooltip("从当前形状压扁所需的秒数。")]
    private float squashDuration = 0.04f;
    [SerializeField, Min(0.01f), Tooltip("从压扁经过原形再拉长所需的秒数。")]
    private float reboundDuration = 0.08f;
    [SerializeField, Min(0.01f), Tooltip("拉长后恢复原形所需的秒数。")]
    private float settleDuration = 0.12f;

    private BounceBullet bullet;
    private SpriteRenderer source;
    private SpriteRenderer display;
    private Transform deformation;
    private Transform art;
    private bool previousForceRenderingOff;
    private bool playing;
    private float startedAt;
    private float initialFactor = 1f;
    private float initialAngle;
    private float wallAngle;
    private float reboundAngle;
    private float currentFactor = 1f;
    private float currentAngle;

    private void Awake()
    {
        bullet = GetComponent<BounceBullet>();
        source = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (deformation == null)
        {
            GameObject pivot = new GameObject("Bounce Deformation");
            pivot.layer = gameObject.layer;
            deformation = pivot.transform;
            deformation.SetParent(transform, false);
            GameObject child = new GameObject("Art");
            child.layer = gameObject.layer;
            art = child.transform;
            art.SetParent(deformation, false);
            display = child.AddComponent<SpriteRenderer>();
        }
        previousForceRenderingOff = source.forceRenderingOff;
        source.forceRenderingOff = true;
        playing = false;
        ApplyShape(1f, 0f);
        CopyAppearance();
        deformation.gameObject.SetActive(true);
        bullet.WallBounced += Play;
    }

    private void Play(Vector2 inward, Vector2 outgoing)
    {
        initialFactor = currentFactor;
        initialAngle = currentAngle;
        wallAngle = LocalAngle(inward);
        reboundAngle = LocalAngle(outgoing);
        startedAt = Time.time;
        playing = true;
    }

    private float LocalAngle(Vector2 direction)
    {
        Vector3 local = transform.InverseTransformDirection(direction);
        return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
    }

    private void LateUpdate()
    {
        // Animator has already selected this frame. No new material or sprite is made.
        CopyAppearance();
        if (!playing) return;
        float elapsed = Mathf.Max(0f, Time.time - startedAt);
        float squash = Mathf.Max(0.01f, squashDuration);
        float rebound = Mathf.Max(0.01f, reboundDuration);
        float settle = Mathf.Max(0.01f, settleDuration);
        float compressed = Mathf.Clamp(squashScale, 0.4f, 1f);
        float stretched = Mathf.Clamp(stretchScale, 1f, 1.6f);
        if (elapsed < squash)
        {
            float t = Ease(elapsed / squash);
            // A half-turn describes the same deformation axis; take the short path.
            float delta = Mathf.Repeat(wallAngle - initialAngle + 90f, 180f) - 90f;
            ApplyShape(Mathf.Lerp(initialFactor, compressed, t), initialAngle + delta * t);
        }
        else if (elapsed < squash + rebound)
        {
            float t = (elapsed - squash) / rebound;
            // Change axes at the neutral shape, avoiding a sideways snap or spin.
            if (t < 0.5f)
                ApplyShape(Mathf.Lerp(compressed, 1f, Ease(t * 2f)), wallAngle);
            else
                ApplyShape(Mathf.Lerp(1f, stretched, Ease((t - 0.5f) * 2f)), reboundAngle);
        }
        else if (elapsed < squash + rebound + settle)
        {
            float t = Ease((elapsed - squash - rebound) / settle);
            ApplyShape(Mathf.Lerp(stretched, 1f, t), reboundAngle);
        }
        else
        {
            ApplyShape(1f, 0f);
            playing = false;
        }
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void ApplyShape(float factor, float angle)
    {
        currentFactor = factor;
        currentAngle = angle;
        deformation.localRotation = Quaternion.Euler(0f, 0f, angle);
        // Reciprocal axes preserve area instead of making the ink vanish or inflate.
        deformation.localScale = new Vector3(factor, 1f / factor, 1f);
        art.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private void CopyAppearance()
    {
        SpriteAppearance.Copy(source, display);
        display.enabled = source.enabled && !previousForceRenderingOff;
    }

    private void OnDisable()
    {
        if (bullet != null) bullet.WallBounced -= Play;
        if (source != null) source.forceRenderingOff = previousForceRenderingOff;
        playing = false;
        if (deformation != null)
        {
            ApplyShape(1f, 0f);
            deformation.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (deformation != null) Destroy(deformation.gameObject);
    }
}
