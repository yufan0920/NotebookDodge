using UnityEngine;

// 挂载本脚本时，要求同一物体具有二维刚体和圆形碰撞体。
// 仍需在 Inspector 设置子弹的 Gravity Scale = 0、碰撞体 Is Trigger = true。
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Bullet : MonoBehaviour
{
    // Min：限制 Inspector 中可填写的最小值，不是运行时的自动检查。
    // speed 的单位是 Unity 世界单位/秒；默认每秒移动 3 unit。
    [SerializeField, Min(0.01f)] private float speed = 3f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.1f)] private float lifetime = 6f;// 未命中也会在生成后约 6 秒销毁，避免场景中不断积累子弹。

    private Rigidbody2D body;
    
    private Vector2 direction = Vector2.down;// 固定的飞行方向。未调用 Initialize 时，默认向世界坐标下方 (0,-1) 飞行。

    private bool consumed;

    private void Awake() => body = GetComponent<Rigidbody2D>();
    
    private void Start() => Destroy(gameObject, lifetime);// 开始运行时预约销毁。这里不会暂停代码；延时使用游戏时间，受 timeScale 影响

    // 由发射器在生成子弹后调用，指定本颗子弹的飞行方向
    public void Initialize(Vector2 flightDirection)
    {
        // sqrMagnitude 是向量长度的平方，用来排除零向量或几乎为零的方向
        // normalized 将方向长度变成 1，保证斜向飞行不会比横向更快
        direction = flightDirection.sqrMagnitude > 0.0001f
            ? flightDirection.normalized : Vector2.down;
    }

    // 按固定物理时间步更新，而不是每个画面帧更新
    private void FixedUpdate()
    {
        // 匀速直线弹
        // 命中后停止移动。未命中时，通过刚体移动到计算出的下一个位置。
        // 新位置 = 当前位置 + 单位方向 × 速度 × 本次物理步长。
        if (!consumed)
            body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime); 
    }

    // 首次进入另一个二维碰撞体时，由 Unity 的 Trigger 检测调用
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 已命中过一次就忽略后续回调
        if (consumed) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();

        // 没有生命组件的对象不处理；本脚本不会因此销毁或反弹子弹
        if (health == null) return;

        // Destroy 不会在这行代码执行时立即移除对象!
        // 先标记 consumed，可避免销毁前的其他回调再次扣血。
        consumed = true;
        // 扣血由 PlayerHealth 负责，子弹只传入伤害数值。
        health.TakeDamage(damage);
        // 命中后提前销毁整颗子弹，不再等待 lifetime 到期。
        Destroy(gameObject);
    }
}
