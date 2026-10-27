using UnityEngine;

// Cosmetic variation only. PatternBullet still owns fragment motion and hits.
[DisallowMultipleComponent]
[RequireComponent(typeof(PatternBullet), typeof(SpriteRenderer))]
public sealed class ChalkFragmentVfx : MonoBehaviour
{
    [SerializeField, Tooltip("依次使用的碎屑图片；数量不足时循环使用。")]
    private Sprite[] variants;
    [SerializeField, Min(0f), Tooltip("碎屑旋转速度，单位度/秒；0 为不旋转。")]
    private float rotationSpeed = 80f;
    [SerializeField, Tooltip("相邻碎屑交替顺、逆时针旋转。")]
    private bool alternateSpin = true;

    private SpriteRenderer source;
    private SpriteRenderer display;
    private Transform art;
    private bool previousForceRenderingOff;
    private float angle;
    private float spinSign = 1f;

    private void Awake() => source = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        if (art == null)
        {
            GameObject child = new GameObject("Chalk Fragment Art");
            child.layer = gameObject.layer;
            art = child.transform;
            art.SetParent(transform, false);
            display = child.AddComponent<SpriteRenderer>();
        }
        previousForceRenderingOff = source.forceRenderingOff;
        source.forceRenderingOff = true;
        art.gameObject.SetActive(true);
        Configure(0);
    }

    // Called before PatternBullet.Initialize so its original-color capture is intact.
    public void Configure(int index)
    {
        if (source == null || art == null) return;
        index = Mathf.Max(0, index);
        if (variants != null && variants.Length > 0)
        {
            for (int offset = 0; offset < variants.Length; offset++)
            {
                Sprite candidate = variants[(index + offset) % variants.Length];
                if (candidate == null) continue;
                source.sprite = candidate;
                break;
            }
        }
        // Decorative variation does not consume gameplay's Unity random sequence.
        angle = Mathf.Repeat(index * 137.5f, 360f);
        spinSign = alternateSpin && index % 2 != 0 ? -1f : 1f;
        art.localRotation = Quaternion.Euler(0f, 0f, angle);
        CopyAppearance();
    }

    private void LateUpdate()
    {
        CopyAppearance();
        angle = Mathf.Repeat(angle + Mathf.Max(0f, rotationSpeed) * spinSign * Time.deltaTime, 360f);
        art.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CopyAppearance()
    {
        SpriteAppearance.Copy(source, display);
        display.enabled = source.enabled && !previousForceRenderingOff;
    }

    private void OnDisable()
    {
        if (source != null) source.forceRenderingOff = previousForceRenderingOff;
        if (art != null) art.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (art != null) Destroy(art.gameObject);
    }
}
