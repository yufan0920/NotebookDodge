using UnityEngine;

public class BounceEmitter : MonoBehaviour, ITimelineEmitter
{
    [SerializeField] private BounceBullet bulletPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.down;
    public bool IsBusy => false;
    public float PreparationDuration => 0;
    public bool CanFire => true;
    public bool CooldownAfterCompletion => false;

    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || bulletPrefab == null || bulletPrefab.gameObject.scene.IsValid()) return false;
        int count = Mathf.Max(1, settings.bulletCount);
        Vector2 local = localForwardDirection.sqrMagnitude > 0.0001f ? localForwardDirection.normalized : Vector2.down;
        Vector2 center = ((Vector2)transform.TransformDirection(local)).normalized;
        if (settings.aimAtPlayer)
        {
            // 玩家位置减去炮口位置，得到这一轮子弹的瞄准向量。
            Vector2 toPlayer = (Vector2)(player.transform.position - transform.position);
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                center = toPlayer.normalized;
                // 让本地 Direction 指向玩家，发射器的子物体外观也随之旋转。
                float targetAngle = Mathf.Atan2(center.y, center.x) * Mathf.Rad2Deg;
                float localAngle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, targetAngle - localAngle);
            }
            // 与玩家重合时沿用当前朝向，避免零向量导致无有效发射方向。
        }
        float spread = Mathf.Clamp(settings.spreadAngle, 0, 180);
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, (float)i / (count - 1));
            BounceBullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(Quaternion.Euler(0, 0, angle) * center, player);
        }
        return true;
    }
}
