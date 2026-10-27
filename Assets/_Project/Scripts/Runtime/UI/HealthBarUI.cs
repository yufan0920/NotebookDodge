using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(HeartHealthGraphic))]
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    [Header("Health Icons")]
    [Tooltip("Optional heart artwork. Leave empty to display dots.")]
    [SerializeField] private Sprite iconSprite;
    [SerializeField, Min(12f)] private float heartSize = 32f;
    [SerializeField, Min(0f)] private float spacing = 8f;
    [SerializeField, Range(1, 20)] private int heartsPerRow = 10;
    [SerializeField] private Color filledColor = new Color(0.95f, 0.22f, 0.32f, 1f);
    [SerializeField] private Color outlineColor = new Color(0.22f, 0.12f, 0.17f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.48f, 0.48f, 0.53f, 1f);
    private HeartHealthGraphic hearts;

    private void Awake() => hearts = GetComponent<HeartHealthGraphic>();

    private void OnEnable()
    {
        if (player != null)
        {
            if (Application.isPlaying) player.HealthChanged += Refresh;
            Refresh(Application.isPlaying ? player.CurrentHealth : player.MaxHealth, player.MaxHealth);
        }
    }

    private void Start()
    {
        if (player == null)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Assign Player on HealthBarUI.", this);
                enabled = false;
            }
            return;
        }
        // All Awake calls have completed, so health is initialized now.
        Refresh(Application.isPlaying ? player.CurrentHealth : player.MaxHealth, player.MaxHealth);
    }

    private void OnDisable()
    {
        if (player != null) player.HealthChanged -= Refresh;
        if (hearts != null) hearts.Clear();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // Preview maximum health in Edit mode; never change the player's health.
        if (!Application.isPlaying)
            Refresh(player != null ? player.MaxHealth : 0, player != null ? player.MaxHealth : 0);
    }
#endif

    private void Refresh(int current, int maximum)
    {
        if (!EnsureHealthGraphic()) return;
        hearts.Configure(current, maximum, heartSize, spacing, heartsPerRow,
            filledColor, outlineColor, emptyColor, iconSprite);
    }

    private bool EnsureHealthGraphic()
    {
        // RequireComponent does not retrofit objects already loaded before the
        // script changed. Upgrade those objects without reloading the scene.
        if (hearts == null)
        {
            hearts = GetComponent<HeartHealthGraphic>();
            if (hearts == null) hearts = gameObject.AddComponent<HeartHealthGraphic>();
        }
        if (hearts == null) return false;

        // An open scene can still contain the old Slider and its visible bar.
        var oldBar = GetComponent<Slider>();
        if (oldBar != null)
        {
            oldBar.enabled = false;
            if (oldBar.fillRect != null)
                oldBar.fillRect.gameObject.SetActive(false);
            if (oldBar.targetGraphic != null && oldBar.targetGraphic != hearts)
                oldBar.targetGraphic.enabled = false;
            var background = transform.Find("Background");
            if (background != null && background.gameObject.activeSelf)
                background.gameObject.SetActive(false);
        }
        return true;
    }
}
