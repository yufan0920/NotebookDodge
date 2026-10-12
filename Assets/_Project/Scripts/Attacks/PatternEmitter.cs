using UnityEngine;

public class PatternEmitter : MonoBehaviour, ITimelineEmitter
{
    [SerializeField] private PatternBullet bulletPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    private float readyAt;
    public bool IsBusy => Time.time < readyAt;
    public float PreparationDuration => bulletPrefab != null ? bulletPrefab.PreparationDuration : 0;
    public bool CanFire => true;
    public bool CooldownAfterCompletion => false;

    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || bulletPrefab == null || bulletPrefab.gameObject.scene.IsValid() || IsBusy) return false;
        float spread = Mathf.Clamp(settings.spreadAngle, 0, 180);
        int count = Mathf.Max(1, settings.bulletCount);
        Vector2 local = localForwardDirection.sqrMagnitude > 0.0001f ? localForwardDirection.normalized : Vector2.down;
        Vector2 center = ((Vector2)transform.TransformDirection(local)).normalized;
        for (int i = 0; i < count; i++)
        {
            float degrees = count == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, (float)i / (count - 1));
            Vector2 heading = Quaternion.Euler(0, 0, degrees) * center;
            PatternBullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(heading, player);
        }
        readyAt = Time.time + bulletPrefab.PreparationDuration;
        return true;
    }
}
