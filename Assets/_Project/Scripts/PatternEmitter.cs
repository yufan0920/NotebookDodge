using UnityEngine;

// 正式排列发射器：支持单发、扇形，以及可选的展开蓄力。
public class PatternEmitter : MonoBehaviour
{
    [SerializeField] private PatternBullet bulletPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Vector2 direction = Vector2.down;
    [SerializeField, Min(1)] private int bulletCount = 3;
    [SerializeField, Range(0, 180)] private float spreadAngle = 40f;
    [SerializeField, Min(0)] private float initialDelay = 1f;
    // 仅为旧配置迁移保留，运行时不再读取这些字段。
    [HideInInspector, SerializeField] private bool useCharge = true;
    [HideInInspector, SerializeField, Min(0)] private float unfoldDistance = 0.5f;
    [HideInInspector, SerializeField, Min(0)] private float unfoldDuration = 0.2f;
    [HideInInspector, SerializeField, Min(0)] private float holdDuration = 1f;
    [HideInInspector, SerializeField, Min(0.01f)] private float normalSpeed = 3f;
    [HideInInspector, SerializeField, Min(0)] private float accelerationDuration = 0.3f;
    [HideInInspector, SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0.1f, 1, 1);
    [HideInInspector, SerializeField, Min(0.1f)] private float flightLifetime = 6f;
    [Tooltip("上一轮开始正式飞行后，等待多久开始下一轮展开。不等待子弹消失。")]
    [SerializeField, Min(0.05f)] private float cooldown = 1f;
    private float remaining;
    private void OnEnable() => remaining = Mathf.Max(0, initialDelay);
    private void Start()
    {
        if (bulletPrefab == null || player == null || bulletPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a Project PatternBullet prefab and scene Player.", this);
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
        for (int i = 0; i < count; i++)
        {
            float degrees = count == 1 ? 0 : Mathf.Lerp(-spreadAngle / 2, spreadAngle / 2, (float)i / (count - 1));
            Vector2 heading = Quaternion.Euler(0, 0, degrees) * center;
            PatternBullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(heading, player);
        }
        remaining = bulletPrefab.PreparationDuration + Mathf.Max(0.05f, cooldown);
    }
}
