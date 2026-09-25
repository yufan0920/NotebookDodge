using UnityEngine;

// 子弹负责自身的展开、蓄力、速度、伤害和寿命；发射器只决定何时、朝哪里生成。
[RequireComponent(typeof(SpriteRenderer), typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class PatternBullet : MonoBehaviour
{
    [SerializeField, Min(1)] private int damage = 1;
    [Header("Prepare")]
    [SerializeField] private bool useCharge = true;
    [SerializeField, Min(0)] private float unfoldDistance = 0.5f;
    [SerializeField, Min(0)] private float unfoldDuration = 0.2f;
    [SerializeField, Min(0)] private float holdDuration = 1f;
    [Header("Movement")]
    [SerializeField, Min(0.01f)] private float normalSpeed = 3f;
    [SerializeField, Min(0)] private float accelerationDuration = 0.3f;
    [SerializeField] private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0, 0.1f, 1, 1);
    [Header("Lifetime")]
    [SerializeField, Min(0.1f)] private float flightLifetime = 6f;
    // 只读信息：让发射器维持“准备结束后再冷却”的原有节奏。
    public float PreparationDuration => useCharge ? Mathf.Max(0, unfoldDuration) + Mathf.Max(0, holdDuration) : 0f;
    private Rigidbody2D body;
    private CircleCollider2D hitbox;
    private SpriteRenderer visual;
    private Color originalColor;
    private PlayerHealth player;
    private Vector2 origin, direction, readyPosition;
    private float unfoldTime, holdTime, accelerateTime, speed;
    private AnimationCurve curve;
    private float age;
    private bool initialized, consumed;

    public void Initialize(Vector2 heading, PlayerHealth owner)
    {
        body = GetComponent<Rigidbody2D>();
        hitbox = GetComponent<CircleCollider2D>();
        visual = GetComponent<SpriteRenderer>();
        originalColor = visual.color;
        player = owner;
        origin = body.position;
        direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.down;
        readyPosition = origin + direction * (useCharge ? Mathf.Max(0f, unfoldDistance) : 0f);
        unfoldTime = useCharge ? Mathf.Max(0f, unfoldDuration) : 0f;
        holdTime = useCharge ? Mathf.Max(0f, holdDuration) : 0f;
        accelerateTime = useCharge ? Mathf.Max(0f, accelerationDuration) : 0f;
        speed = Mathf.Max(0.01f, normalSpeed);
        flightLifetime = Mathf.Max(0.1f, flightLifetime);
        curve = speedCurve == null ? AnimationCurve.Linear(0, 0.1f, 1, 1) : new AnimationCurve(speedCurve.keys);
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 0;
        body.drag = 0;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        hitbox.isTrigger = true;
        hitbox.enabled = false;
        Color warning = originalColor;
        warning.a *= 0.4f;
        visual.color = warning;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || consumed) return;
        if (player == null || player.IsDead) { Destroy(gameObject); return; }
        age += Time.fixedDeltaTime;
        // 展开位置与方向在创建时锁定，后续炮口旋转不会改变这一轮预告。
        if (age < unfoldTime)
        {
            body.MovePosition(Vector2.Lerp(origin, readyPosition, age / unfoldTime));
            return;
        }
        if (age < unfoldTime + holdTime)
        {
            body.MovePosition(readyPosition);
            return;
        }
        if (!hitbox.enabled)
        {
            body.position = readyPosition;
            hitbox.enabled = true;
            visual.color = originalColor;
        }
        float flightAge = age - unfoldTime - holdTime;
        if (flightAge >= flightLifetime) { Destroy(gameObject); return; }
        // 曲线只负责加速阶段；阶段结束后恒定使用正常速度。
        float multiplier = accelerateTime > 0f && flightAge < accelerateTime
            ? Mathf.Max(0f, curve.Evaluate(flightAge / accelerateTime)) : 1f;
        body.MovePosition(body.position + direction * speed * multiplier * Time.fixedDeltaTime);
    }

    private void OnTriggerEnter2D(Collider2D other) => Hit(other);
    private void OnTriggerStay2D(Collider2D other) => Hit(other);
    private void Hit(Collider2D other)
    {
        if (!initialized || consumed || !hitbox.enabled) return;
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null) return;
        consumed = true;
        health.TakeDamage(damage);
        Destroy(gameObject);
    }
}
