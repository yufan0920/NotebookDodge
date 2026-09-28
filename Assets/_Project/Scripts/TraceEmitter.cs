using UnityEngine;

// 追踪弹发射器：每颗子弹发射时重新取样玩家位置，一组结束后休息。
public class TraceEmitter : MonoBehaviour
{
    [SerializeField] private TraceBullet dropPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform muzzle;
    [SerializeField] private bool aimAtTarget = true;
    [SerializeField] private Vector2 localAimDirection = Vector2.down;
    [Tooltip("启用后等待多少秒，开始第一颗子弹的预警。")]
    [SerializeField, Min(0f)] private float initialDelay = 1f;
    [Tooltip("每组实际子弹数量。3 就是三颗，不再嵌套连发。")]
    [SerializeField, Min(1)] private int burstCount = 3;
    [Tooltip("组内相邻两颗开始预警的间隔，单位秒。预警时长相同时，起飞间隔也相同。")]
    [SerializeField, Min(0.05f)] private float shotInterval = 0.6f;
    [Tooltip("本组最后一颗开始预警，到下一组第一颗开始预警的间隔。不会等待爆炸。")]
    [SerializeField, Min(0.05f)] private float burstCooldown = 2f;
    private float remaining;
    private int shotsFired;
    private TraceBullet preparingDrop;

    private void OnEnable()
    {
        remaining = Mathf.Max(0f, initialDelay);
        shotsFired = 0;
    }

    private void Start()
    {
        if (dropPrefab == null || player == null || dropPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a Project asset to Drop Prefab and a scene Player to TraceEmitter.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (player == null || player.IsDead || dropPrefab == null) return;
        if (preparingDrop != null && preparingDrop.IsPreparing) return;
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;
        // 保存坐标值，不把目标 Transform 作为持续追踪的位置来源。
        Vector2 lockedPosition = player.transform.position;
        AimAt(lockedPosition);
        Transform source = muzzle != null ? muzzle : transform;
        TraceBullet drop = Instantiate(dropPrefab, source.position, Quaternion.identity);
        if (drop.HasTrackingWarning)
        {
            preparingDrop = drop;
            drop.Initialize(lockedPosition, player, source, AimAt);
        }
        else drop.Initialize(lockedPosition, player);
        shotsFired++;
        if (shotsFired >= Mathf.Max(1, burstCount))
        {
            shotsFired = 0;
            remaining = Mathf.Max(0.05f, burstCooldown);
        }
        else remaining = Mathf.Max(0.05f, shotInterval);
    }

    private void AimAt(Vector2 lockedPosition)
    {
        if (this != null && aimAtTarget)
        {
            Vector2 toTarget = lockedPosition - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 local = localAimDirection.sqrMagnitude > 0.0001f
                    ? localAimDirection.normalized : Vector2.down;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                float localAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, targetAngle - localAngle);
            }
        }
    }
}
