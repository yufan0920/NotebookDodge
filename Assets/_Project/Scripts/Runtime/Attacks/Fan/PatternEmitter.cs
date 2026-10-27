using UnityEngine;

public class PatternEmitter : MonoBehaviour, ITimelineEmitter, IEmitterOrientation
{
    [SerializeField] private PatternBullet bulletPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    [SerializeField] private Transform muzzle;
    public Vector2 LocalForwardDirection => localForwardDirection.sqrMagnitude > 0.0001f
        ? localForwardDirection.normalized : Vector2.down;
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
        Vector2 local = LocalForwardDirection;
        Vector2 center = ((Vector2)transform.TransformDirection(local)).normalized;
        Transform source = muzzle != null ? muzzle : transform;
        MuzzleSparkVfx flash = GetComponent<MuzzleSparkVfx>();
        for (int i = 0; i < count; i++)
        {
            float degrees = count == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, (float)i / (count - 1));
            Vector2 heading = Quaternion.Euler(0, 0, degrees) * center;
            PatternBullet bullet = Instantiate(bulletPrefab, source.position, Quaternion.identity);
            // One representative bullet triggers one flash for the entire fan volley.
            if (flash != null && i == count / 2) bullet.Launched += OnPatternLaunched;
            bullet.Initialize(heading, player);
        }
        readyAt = Time.time + bulletPrefab.PreparationDuration;
        return true;
    }

    private void OnPatternLaunched(PatternBullet shot)
    {
        shot.Launched -= OnPatternLaunched;
        if (this == null || !isActiveAndEnabled) return;
        MuzzleSparkVfx flash = GetComponent<MuzzleSparkVfx>();
        if (flash == null) return;
        Transform source = muzzle != null ? muzzle : transform;
        flash.Play(source, source.position, transform.TransformDirection(LocalForwardDirection),
            shot.GetComponent<SpriteRenderer>().color);
    }
}
