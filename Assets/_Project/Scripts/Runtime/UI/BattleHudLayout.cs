using UnityEngine;

// A full, uncropped 16:9 camera view below an independent overlay HUD.
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public class BattleHudLayout : MonoBehaviour
{
    [Tooltip("Header height at 1080p. Scales with screen height while the battle view keeps its 16:9 aspect ratio.")]
    [SerializeField, Range(0, 216)] private float headerHeight = 72;
    [SerializeField] private RectTransform header;
    [Tooltip("Renders bg1 across the screen underneath the battle camera and HUD.")]
    [SerializeField] private Camera deskCamera;
    [SerializeField] private SpriteRenderer deskBackground;
    private Camera battleCamera;
    private Rect previousRect;
    private float previousAspect;

    private void OnEnable()
    {
        battleCamera = GetComponent<Camera>();
        previousRect = battleCamera.rect;
        previousAspect = battleCamera.aspect;
        ApplyLayout();
    }

    private void LateUpdate() => ApplyLayout();
    private void OnPreCull() => ApplyLayout();

    public static Rect CalculateViewport(float width, float height, float headerPixels)
    {
        width = Mathf.Max(1, width); height = Mathf.Max(1, height);
        float availableHeight = height * (1 - Mathf.Clamp(headerPixels, 0, 216) / 1080f);
        float viewHeight = Mathf.Min(availableHeight, width * 9f / 16f);
        float viewWidth = viewHeight * 16f / 9f;
        return new Rect((width - viewWidth) / (2 * width), (availableHeight - viewHeight) / (2 * height),
            viewWidth / width, viewHeight / height);
    }

    private void ApplyLayout()
    {
        if (battleCamera == null) return;
        Rect view = CalculateViewport(Screen.width, Screen.height, headerHeight);
        battleCamera.rect = view;
        battleCamera.aspect = 16f / 9f;
        float headerBottom = 1 - Mathf.Clamp(headerHeight, 0, 216) / 1080f;
        Place(header, 0, headerBottom, 1, 1);
        if (deskCamera != null && deskBackground != null && deskBackground.sprite != null)
        {
            // Cover the screen with one continuous tabletop; only its decorative
            // edges may be cropped on other aspect ratios. Gameplay keeps its own view.
            Bounds bounds = deskBackground.bounds;
            float aspect = Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);
            deskCamera.rect = new Rect(0, 0, 1, 1);
            deskCamera.aspect = aspect;
            deskCamera.orthographicSize = Mathf.Max(0.001f,
                Mathf.Min(bounds.extents.y, bounds.extents.x / aspect));
            Vector3 position = deskCamera.transform.position;
            deskCamera.transform.position = new Vector3(bounds.center.x, bounds.center.y, position.z);
            deskCamera.depth = battleCamera.depth - 1;
        }
    }

    private static void Place(RectTransform rect, float left, float bottom, float right, float top)
    {
        if (rect == null) return;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDisable()
    {
        if (battleCamera == null) return;
        battleCamera.rect = previousRect;
        battleCamera.aspect = previousAspect;
    }
}
