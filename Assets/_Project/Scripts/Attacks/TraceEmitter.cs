using UnityEngine;

public class TraceEmitter : MonoBehaviour, ITimelineEmitter
{
    [SerializeField] private TraceBullet dropPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    [SerializeField] private Transform muzzle;
    private bool aimAtTarget;
    private TraceBullet preparingDrop, latestDrop;
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
        latestDrop = drop;
        if (drop.HasTrackingWarning)
        {
            preparingDrop = drop;
            drop.Initialize(lockedPosition, player, source, AimAt);
        }
        else drop.Initialize(lockedPosition, player);
        return true;
    }

    private void AimAt(Vector2 lockedPosition)
    {
        if (this != null && aimAtTarget)
        {
            Vector2 toTarget = lockedPosition - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 local = localForwardDirection.sqrMagnitude > 0.0001f
                    ? localForwardDirection.normalized : Vector2.down;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float localAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, targetAngle - localAngle);
            }
        }
    }
}
