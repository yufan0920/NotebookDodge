using UnityEngine;


[RequireComponent(typeof(SpriteRenderer))]
public class BounceBullet : MonoBehaviour
{
    // Cosmetic listeners receive the wall normal and final outgoing direction.
    public event System.Action<Vector2, Vector2> WallBounced;

    [SerializeField, Min(0.01f)] private float speed = 3f;
    [SerializeField, Min(1)] private int damage = 1;
    // Persistent for this session: no lifetime or bounce-count limit.
    [Tooltip("世界单位命中半径；反弹使用子弹中心，与玩家活动边界对齐。")]
    [SerializeField, Min(0.01f)] private float hitRadius = 0.1f;
    private PlayerMovement movement;
    private readonly Vector2[] corners = new Vector2[4];
    private Vector2 direction;
    private readonly Vector2[] normals = new Vector2[4];
    private PlayerHealth player;
    private bool warnedStepLimit;
    private bool initialized;
    private bool enteredArena;
    private BounceWallVfx wallVfx;
    private float radius;
    private readonly System.Collections.Generic.List<RaycastHit2D> hits = new System.Collections.Generic.List<RaycastHit2D>();

    public void Initialize(Vector2 heading, PlayerHealth targetPlayer)
    {
        player = targetPlayer;
        wallVfx = GetComponent<BounceWallVfx>();
        direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.down;
        radius = Mathf.Max(0.01f, hitRadius);
        movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        if (!RefreshBoundary())
        {
            Debug.LogError("Assign a valid Movement Area on the Player before spawning bounce bullets.", this);
            Destroy(gameObject);
            return;
        }
        warnedStepLimit = false;
        enteredArena = Contains(transform.position);
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        if (player == null || player.IsDead) { Destroy(gameObject); return; }
        if (!RefreshBoundary())
        {
            Debug.LogError("Bounce bullet lost its valid Player Movement Area.", this);
            Destroy(gameObject);
            return;
        }
        Vector2 position = transform.position;
        float travel = Mathf.Max(0.01f, speed) * Time.fixedDeltaTime;
        // 按碰边位置分段走完这一步，保留反弹后剩余距离，避免掉速或越界。
        int safety = 0;
        while (travel > 0 && safety++ < 64)
        {
            if (!enteredArena)
            {
                float entry;
                if (!TryGetEntryDistance(position, travel, out entry))
                {
                    MoveStraight(ref position, travel);
                    travel = 0;
                    break;
                }
                MoveStraight(ref position, entry);
                travel = Mathf.Max(0, travel - entry);
                enteredArena = true; // 首次飞入不算反弹，接下来碰到出口边界才反弹。
                continue;
            }
            float wallDistance = float.PositiveInfinity;
            Vector2 normal = Vector2.zero;
            for (int i = 0; i < 4; i++)
            {
                float approach = Vector2.Dot(direction, normals[i]);
                if (approach >= -0.000001f) continue;
                float distance = Mathf.Max(0, -Vector2.Dot(position - corners[i], normals[i]) / approach);
                if (distance < wallDistance - 0.00001f)
                {
                    wallDistance = distance;
                    normal = normals[i];
                }
                else if (Mathf.Abs(distance - wallDistance) <= 0.00001f)
                    normal += normals[i];
            }
            float segment = Mathf.Min(travel, wallDistance);
            // 扫过整段路径，而不是只检查终点，减少高速穿过玩家的问题。
            if (HitsPlayer(position, segment))
            {
                player.TakeDamage(damage);
            }
            position += direction * segment;
            travel -= segment;
            if (wallDistance > segment) break;
            Vector2 incomingDirection = direction;
            direction = Vector2.Reflect(direction, normal.normalized).normalized;
            if (wallVfx != null) wallVfx.Play(position, normal, corners, normals);
            for (int i = 0; i < 4; i++)
                if (Vector2.Dot(position - corners[i], normals[i]) <= 0.00001f &&
                    Vector2.Dot(direction, normals[i]) < -0.000001f)
                {
                    direction = -incomingDirection;
                    break;
                }
            WallBounced?.Invoke(normal.normalized, direction);
        }
        if (safety > 64 && !warnedStepLimit)
        {
            // Keep the last safe position and resume next step instead of deleting the bullet.
            warnedStepLimit = true;
            Debug.LogWarning("Bounce step reached its safety limit; remaining movement was skipped. Reduce speed or enlarge arena.", this);
        }
        transform.position = new Vector3(position.x, position.y, 0);
    }

    private void MoveStraight(ref Vector2 position, float distance)
    {
        if (HitsPlayer(position, distance))
        {
            player.TakeDamage(damage);
        }
        position += direction * distance;
    }

    private bool RefreshBoundary()
    {
        PlayerMovementArea area = movement != null ? movement.MovementArea : null;
        if (area == null || !area.isActiveAndEnabled || !area.TryCopyBoundary(corners)) return false;
        Vector2 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 edge = corners[(i + 1) % 4] - corners[i];
            Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
            normals[i] = Vector2.Dot(center - corners[i], normal) >= 0 ? normal : -normal;
        }
        return true;
    }

    private bool Contains(Vector2 position)
    {
        for (int i = 0; i < 4; i++)
            if (Vector2.Dot(position - corners[i], normals[i]) < -0.000001f) return false;
        return true;
    }

    private bool TryGetEntryDistance(Vector2 origin, float distance, out float entry)
    {
        entry = 0;
        float exit = distance;
        for (int i = 0; i < 4; i++)
        {
            float offset = Vector2.Dot(origin - corners[i], normals[i]);
            float approach = Vector2.Dot(direction, normals[i]);
            if (Mathf.Abs(approach) < 0.000001f)
            {
                if (offset < 0) return false;
                continue;
            }
            float crossing = -offset / approach;
            if (approach > 0) entry = Mathf.Max(entry, crossing);
            else exit = Mathf.Min(exit, crossing);
            if (entry > exit) return false;
        }
        return true;
    }
    private bool HitsPlayer(Vector2 origin, float distance)
    {
        // Include triggers explicitly so project-wide query settings do not alter damage.
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
        hits.Clear();
        Physics2D.CircleCast(origin, radius, direction, filter, hits, distance);
        foreach (RaycastHit2D hit in hits)
            if (hit.collider != null && hit.collider.GetComponentInParent<PlayerHealth>() == player) return true;
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.01f, hitRadius));
    }
}
