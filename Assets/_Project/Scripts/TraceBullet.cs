using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TraceBullet : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float speed = 6f;
    [SerializeField, Min(0.01f)] private float explosionRadius = 0.6f;
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(0.01f)] private float explosionVisualDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float maxLifetime = 30f;
    [SerializeField] private Color warningColor = new Color(1f, 0.65f, 0.15f, 0.8f);
    private Vector2 target;
    private PlayerHealth player;
    private SpriteRenderer visual;
    private LineRenderer ring;
    private Material ringMaterial;
    private bool initialized;
    private bool exploded;
    private float explosionAge;
    private float radius;

    public void Initialize(Vector2 lockedPosition, PlayerHealth targetPlayer)
    {
        target = lockedPosition;
        player = targetPlayer;
        visual = GetComponent<SpriteRenderer>();
        radius = Mathf.Max(0.01f, explosionRadius);
        initialized = true;
        CreateWarning();
        Destroy(gameObject, Mathf.Max(0.1f, maxLifetime));
    }

    private void CreateWarning()
    {
        // 预警是独立世界坐标圆，不跟随子弹或玩家移动。
        GameObject marker = new GameObject("TraceWarning");
        ring = marker.AddComponent<LineRenderer>();
        ringMaterial = new Material(Shader.Find("Sprites/Default"));
        ring.sharedMaterial = ringMaterial;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = 0.035f;
        ring.sortingLayerID = visual.sortingLayerID;
        ring.sortingOrder = visual.sortingOrder + 1;
        ring.startColor = ring.endColor = warningColor;
        // 半径与实际范围判定共用同一个数值。
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(target.x + Mathf.Cos(angle) * radius,
                target.y + Mathf.Sin(angle) * radius, 0f));
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || exploded) return;
        if (player == null || player.IsDead) { Destroy(gameObject); return; }
        // MoveTowards 不会越过目标；方向只指向发射瞬间记录的位置。
        Vector2 next = Vector2.MoveTowards(transform.position, target,
            Mathf.Max(0.1f, speed) * Time.fixedDeltaTime);
        transform.position = new Vector3(next.x, next.y, 0f);
        if ((next - target).sqrMagnitude > 0.000001f) return;
        Explode();
    }

    private void Explode()
    {
        exploded = true; // 一次性爆炸，不在特效持续期间反复判伤。
        visual.enabled = false;
        ring.startColor = ring.endColor = Color.white;
        ring.widthMultiplier = 0.09f;
        // 检查玩家实际受击碰撞体，而不是美术图片的大小。
        // 即使玩家有多个碰撞体，也只造成一次伤害。
        foreach (Collider2D hitbox in player.GetComponentsInChildren<Collider2D>())
        {
            if (!hitbox.enabled || !hitbox.gameObject.activeInHierarchy) continue;
            Vector2 closest = hitbox.ClosestPoint(target);
            if ((closest - target).sqrMagnitude <= radius * radius)
            {
                player.TakeDamage(damage); // 沿用已有无敌时间、闪红和音效。
                break;
            }
        }
    }

    private void Update()
    {
        if (!exploded) return;
        explosionAge += Time.deltaTime;
        float duration = Mathf.Max(0.01f, explosionVisualDuration);
        Color color = Color.white;
        color.a = 1f - Mathf.Clamp01(explosionAge / duration);
        if (ring != null) ring.startColor = ring.endColor = color;
        if (explosionAge >= duration) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // 子弹命中、超时或场景卸载时，同时清理预警与临时材质。
        if (ring != null) Destroy(ring.gameObject);
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
