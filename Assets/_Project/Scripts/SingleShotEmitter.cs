using UnityEngine;

public class SingleShotEmitter : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField, Min(0.1f)] private float interval = 1f;
    [Tooltip("相对于 Emitter 的本地方向。(0,-1) 从底部发射；旋转 Emitter 的 Z 轴会转动整个弹幕。")]
    [SerializeField] private Vector2 direction = Vector2.down;
    [Header("Fan Pattern")]
    [Tooltip("每轮同时发射的子弹数量。设为 1 恢复单发。")]
    [SerializeField, Min(1)] private int bulletCount = 3;
    [Tooltip("最外侧两颗子弹之间的总夹角，单位为度。")]
    [SerializeField, Range(0f, 180f)] private float spreadAngle = 40f;
    private float timer;

    private void Start()
    {
        if (bulletPrefab == null || player == null)
        {
            Debug.LogError("Assign Bullet Prefab and Player on the emitter.", this);
            enabled = false;
            return;
        }

        // 发射模板必须来自 Project 中的 Prefab，不能引用场景里会被销毁的子弹。
        if (bulletPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Bullet Prefab must reference the prefab asset in the Project window, not a bullet in the Hierarchy.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        // 防止运行中引用被移除后继续访问已经销毁的对象。
        if (bulletPrefab == null || player == null)
        {
            Debug.LogError("Emitter reference was lost. Check Bullet Prefab and Player.", this);
            enabled = false;
            return;
        }
        if (player.IsDead) return;
        timer += Time.deltaTime;
        if (timer < Mathf.Max(0.1f, interval)) return;
        timer = 0f;
        FireVolley();
    }

    private void FireVolley()
    {
        int count = Mathf.Max(1, bulletCount);
        float spread = Mathf.Clamp(spreadAngle, 0f, 180f);
        // 先得到本地方向，再转换到世界坐标：Emitter 旋转时，弹幕随之转向。
        // TransformDirection 不受位置或缩放影响。2D 发射器只旋转 Z 轴。
        Vector2 localDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized : Vector2.down;
        Vector2 center = ((Vector2)transform.TransformDirection(localDirection)).normalized;

        for (int i = 0; i < count; i++)
        {
            // 把总角度均分：三发、总角度 40 度时，偏转角为 -20、0、+20 度。
            // 单发单独处理，避免除以零，同时保证沿中央方向发射。
            float degrees = count == 1 ? 0f : -spread * 0.5f + spread * i / (count - 1);
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            // 二维旋转公式：只改变方向，不改变速度。
            Vector2 flightDirection = new Vector2(
                center.x * cos - center.y * sin,
                center.x * sin + center.y * cos);

            // 同一轮在同一帧、同一位置生成；之后各自按直线飞行。
            // 子弹不是 Emitter 的子对象，已发出的子弹不会跟着后续旋转转弯。
            Bullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
            bullet.Initialize(flightDirection);
        }
    }
}
