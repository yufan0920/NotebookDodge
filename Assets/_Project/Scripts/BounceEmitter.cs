using UnityEngine;

// 只负责发射时间、初始方向和排列；运动参数全部由 BounceBullet 管理。
public class BounceEmitter : MonoBehaviour
{
    [SerializeField] private BounceBullet bulletPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Vector2 direction = Vector2.down;
    [Tooltip("每次发射时瞄准玩家当前位置；已发射的子弹不会继续追踪。")]
    [SerializeField] private bool aimAtPlayer = true;
    [SerializeField, Min(1)] private int bulletCount = 1;
    [SerializeField, Range(0, 180)] private float spreadAngle = 40f;
    [SerializeField, Min(0)] private float initialDelay = 1f;
    [SerializeField, Min(0.05f)] private float interval = 1.5f;
    private float remaining;

    private void OnEnable() => remaining = Mathf.Max(0, initialDelay);
    private void Start()
    {
        if (bulletPrefab == null || player == null || bulletPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a BounceBullet prefab asset and scene Player.", this);
            enabled = false;
        }
    }
    private void Update()
    {
        if (player == null || player.IsDead || bulletPrefab == null) return;
        remaining -= Time.deltaTime;
        if (remaining > 0) return;
        int count = Mathf.Max(1, bulletCount);
        Vector2 local = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
        Vector2 center = ((Vector2)transform.TransformDirection(local)).normalized;
        if (aimAtPlayer)
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
        float spread = Mathf.Clamp(spreadAngle, 0, 180);
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0 : Mathf.Lerp(-spread / 2, spread / 2, (float)i / (count - 1));
            BounceBullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(Quaternion.Euler(0, 0, angle) * center, player);
        }
        remaining = Mathf.Max(0.05f, interval);
    }
}
