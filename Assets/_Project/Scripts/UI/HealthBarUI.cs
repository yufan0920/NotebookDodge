using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth player;
    private Slider slider;

    private void Awake() => slider = GetComponent<Slider>();

    private void OnEnable()
    {
        if (player != null)
        {
            player.HealthChanged += Refresh;
            Refresh(player.CurrentHealth, player.MaxHealth);
        }
    }

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("Assign Player on HealthBarUI.", this);
            enabled = false;
            return;
        }
        // All Awake calls have completed, so health is initialized now.
        Refresh(player.CurrentHealth, player.MaxHealth);
    }

    private void OnDisable()
    {
        if (player != null) player.HealthChanged -= Refresh;
    }

    private void Refresh(int current, int maximum)
    {
        slider.minValue = 0;
        slider.maxValue = maximum;
        slider.value = current;
    }
}
