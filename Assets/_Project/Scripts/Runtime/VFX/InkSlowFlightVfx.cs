using UnityEngine;

// Single-sprite ink animation. TraceBullet continues to own all attack timing.
[DisallowMultipleComponent]
[RequireComponent(typeof(TraceBullet), typeof(SpriteRenderer))]
public sealed class InkSlowFlightVfx : MonoBehaviour
{
    [SerializeField, Range(0f, 0.25f), Tooltip("飞行时伸缩幅度；0.02 表示约 2%，0 为关闭伸缩。")]
    private float wobbleAmount = 0.02f;
    [SerializeField, Min(0.1f), Tooltip("完成一次晃动的秒数；越大越慢。")]
    private float wobblePeriod = 1.1f;
    [SerializeField, Range(0f, 15f), Tooltip("在缓慢旋转上叠加的左右摇摆角度。")]
    private float tiltAngle = 8f;
    [SerializeField, Range(-90f, 90f), Tooltip("基础旋转速度（度/秒），再乘 Rotation Speed Curve；负数反向。")]
    private float rotationSpeed = 18f;
    [SerializeField, Tooltip("X=飞行距离进度（0发射、1落地），Y=旋转速度倍率。只改变转速，不直接设置角度。")]
    private AnimationCurve rotationSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 0.15f, 0f, 0f),
        new Keyframe(0.45f, 2.5f, 0f, 0f),
        new Keyframe(1f, 0.2f, 0f, 0f));
    [SerializeField, Range(0f, 0.06f), Tooltip("垂直飞行方向的小幅漂晃，单位为世界单位；只移动外观。")]
    private float swayAmount = 0.018f;

    private TraceBullet bullet;
    private SpriteRenderer source;
    private SpriteRenderer display;
    private Transform art;
    private bool previousForceRenderingOff;
    private bool wasFlying;
    private float flightAge;
    private float rotationAngle;
    private float previousRotationRate;

    private void Awake()
    {
        bullet = GetComponent<TraceBullet>();
        source = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (art == null)
        {
            GameObject child = new GameObject("InkSlow Blob Art");
            child.layer = gameObject.layer;
            art = child.transform;
            art.SetParent(transform, false);
            display = child.AddComponent<SpriteRenderer>();
        }
        previousForceRenderingOff = source.forceRenderingOff;
        source.forceRenderingOff = true;
        wasFlying = false;
        flightAge = 0f;
        rotationAngle = 0f;
        previousRotationRate = 0f;
        ResetShape();
        display.enabled = false;
        art.gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        bool flying = bullet.IsFlying;
        // Read the original renderer so warning/landing visibility stays authoritative.
        SpriteAppearance.Copy(source, display);
        display.enabled = flying && source.enabled && !previousForceRenderingOff;
        if (!flying)
        {
            if (wasFlying) ResetShape();
            wasFlying = false;
            flightAge = 0f;
            rotationAngle = 0f;
            previousRotationRate = 0f;
            return;
        }

        // Start neutral, then ease into the motion instead of popping at launch.
        float multiplier = rotationSpeedCurve != null && rotationSpeedCurve.length > 0
            ? Mathf.Max(0f, rotationSpeedCurve.Evaluate(bullet.FlightProgress)) : 1f;
        float rotationRate = Mathf.Clamp(rotationSpeed, -90f, 90f) * multiplier;
        if (wasFlying)
        {
            flightAge += Time.deltaTime;
            // Integrate velocity. Multiplying total age by the current rate would
            // rewind the artwork when the curve slows down near the landing point.
            rotationAngle += (previousRotationRate + rotationRate) * 0.5f * Time.deltaTime;
        }
        previousRotationRate = rotationRate;
        wasFlying = true;
        float phase = flightAge * (2f * Mathf.PI) / Mathf.Max(0.1f, wobblePeriod);
        float entry = Mathf.SmoothStep(0f, 1f, flightAge / 0.15f);
        float width = 1f + Mathf.Sin(phase) * Mathf.Clamp(wobbleAmount, 0f, 0.25f) * entry;
        art.localScale = new Vector3(width, 1f / width, 1f);
        art.localRotation = Quaternion.Euler(0f, 0f,
            rotationAngle +
            Mathf.Sin(phase + 0.6f) * Mathf.Clamp(tiltAngle, 0f, 15f) * entry);
        Vector2 heading = bullet.FlightDirection;
        Vector2 side = heading.sqrMagnitude > 0.0001f
            ? new Vector2(-heading.y, heading.x).normalized : Vector2.right;
        Vector2 offset = side * (Mathf.Sin(phase) * Mathf.Clamp(swayAmount, 0f, 0.06f) * entry);
        art.localPosition = transform.InverseTransformVector(new Vector3(offset.x, offset.y, 0f));
    }

    private void ResetShape()
    {
        art.localPosition = Vector3.zero;
        art.localScale = Vector3.one;
        art.localRotation = Quaternion.identity;
    }

    private void OnDisable()
    {
        if (source != null) source.forceRenderingOff = previousForceRenderingOff;
        wasFlying = false;
        flightAge = 0f;
        rotationAngle = 0f;
        previousRotationRate = 0f;
        if (art != null)
        {
            ResetShape();
            art.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (art != null) Destroy(art.gameObject);
    }
}
