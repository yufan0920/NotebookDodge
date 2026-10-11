using UnityEngine;

public class DirectPatternEmitter : MonoBehaviour, ITimelineEmitter
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    public bool IsBusy => false;
    public bool CanFire => true;
    public float PreparationDuration => 0;
    public bool CooldownAfterCompletion => false;
    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || bulletPrefab == null || bulletPrefab.gameObject.scene.IsValid()) return false;
        Vector2 local = localForwardDirection.sqrMagnitude > 0.0001f ? localForwardDirection.normalized : Vector2.down;
        Vector2 center = transform.TransformDirection(local);
        int count = Mathf.Max(1, settings.bulletCount);
        float spread = Mathf.Clamp(settings.spreadAngle, 0, 180);
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, (float)i / (count - 1));
            Bullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(Quaternion.Euler(0, 0, angle) * center);
        }
        return true;
    }
}
