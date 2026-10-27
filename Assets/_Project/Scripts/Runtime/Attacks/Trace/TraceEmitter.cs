using UnityEngine;

public class TraceEmitter : MonoBehaviour, ITimelineEmitter, IEmitterOrientation
{
    [SerializeField] private TraceBullet dropPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    public Vector2 LocalForwardDirection => localForwardDirection.sqrMagnitude > 0.0001f
        ? localForwardDirection.normalized : Vector2.down;
    [SerializeField] private Transform muzzle;
    private bool aimAtTarget;
    private TraceBullet preparingDrop, latestDrop;
    public float DefaultGroundLifetime => dropPrefab != null ? dropPrefab.DefaultGroundLifetime : 0;
    public bool IsBusy => latestDrop != null && latestDrop.IsPreparing;
    public bool CanFire => preparingDrop == null || !preparingDrop.IsPreparing;
    public float PreparationDuration => 0;
    public bool CooldownAfterCompletion => false;

    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || dropPrefab == null || !CanFire || dropPrefab.gameObject.scene.IsValid()) return false;
        aimAtTarget = settings.aimAtPlayer;
        Vector2 lockedPosition = player.transform.position;
        AimAt(lockedPosition);
        Transform source = muzzle != null ? muzzle : transform;
        TraceBullet drop = Instantiate(dropPrefab, source.position, Quaternion.identity);
        if (GetComponent<MuzzleSparkVfx>() != null) drop.Launched += OnBulletLaunched;
        latestDrop = drop;
        if (drop.HasTrackingWarning)
        {
            preparingDrop = drop;
            drop.Initialize(lockedPosition, player, source, AimAt, settings.groundLifetime);
        }
        else drop.Initialize(lockedPosition, player, groundLifetime: settings.groundLifetime);
        return true;
    }

    private void OnBulletLaunched(TraceBullet shot)
    {
        shot.Launched -= OnBulletLaunched;
        if (this == null || !isActiveAndEnabled) return;
        MuzzleSparkVfx flash = GetComponent<MuzzleSparkVfx>();
        if (flash != null)
            flash.Play(muzzle != null ? muzzle : transform, shot.transform.position,
                shot.FlightDirection, shot.GetComponent<SpriteRenderer>().color);
    }

    private void AimAt(Vector2 lockedPosition)
    {
        if (this != null && aimAtTarget)
        {
            Vector2 toTarget = lockedPosition - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 local = LocalForwardDirection;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float localAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, targetAngle - localAngle);
            }
        }
    }
}
