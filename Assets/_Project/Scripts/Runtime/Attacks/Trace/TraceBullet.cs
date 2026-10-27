using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class TraceBullet : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float speed = 6f;
    // X = 飞行进度（0 = 发射，1 = 到达）
    // Y = 速度倍率
    [SerializeField]
    private AnimationCurve speedCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 0.5f);
    // 最低速度倍率
    [SerializeField, Min(0.01f)]
    private float minimumSpeedMultiplier = 0.5f;
    [Header("Pre-launch Warning")]
    [SerializeField]
    private bool trackDuringWarning = true;
    [UnityEngine.Serialization.FormerlySerializedAs("trackingDuration")]
    [SerializeField, Min(0f)]
    private float preLaunchDuration;
    [SerializeField, Min(0f)]
    private float trackingSmoothTime;
    [SerializeField]
    private Color warningColor = new Color(1f, 0.65f, 0.15f, 0.8f);
    [SerializeField, Min(0.001f)]
    private float warningWidth = 0.035f;
    [Header("Flight Tracking")]
    [SerializeField, Min(0f)]
    private float flightTrackingDuration;
    [SerializeField, Min(1f)]
    private float turnSpeed = 360f;
    [Header("Explosion")]
    [SerializeField]
    private bool explodeOnPlayerContact = true;
    [SerializeField, Min(0.01f)]
    private float explosionRadius = 0.6f;
    [SerializeField, Min(0)]
    private int damage = 1;
    [SerializeField, Min(0.01f)]
    private float explosionVisualDuration = 0.25f;
    [Header("Ground Area (Optional)")]
    [SerializeField]
    private GroundArea groundAreaPrefab;
    [Header("Lifetime")]
    [SerializeField, Min(0.1f)]
    private float maxLifetime = 30f;
    public float DefaultGroundLifetime => groundAreaPrefab != null ? groundAreaPrefab.DefaultLifetime : 0;
    private float groundLifetime;
    public bool IsPreparing => initialized && !launched && !exploded;
    public bool IsFlying => initialized && launched && !exploded;
    public Vector2 FlightDirection => flightDirection;
    // Same distance-based progress used by speedCurve; visuals can read it safely.
    public float FlightProgress => exploded ? 1f : (!IsFlying ? 0f :
        (startDistance <= 0.001f ? 1f : Mathf.Clamp01(1f -
            Vector2.Distance(transform.position, target) / startDistance)));
    public event System.Action<TraceBullet> Launched;
    public bool HasTrackingWarning => trackDuringWarning && preLaunchDuration > 0f;
    public float ExplosionProgress => Mathf.Clamp01(explosionAge / Mathf.Max(0.01f, explosionVisualDuration));
    private LandingExplosionVfx explosionVfx;
    private TraceTrailVfx trailVfx;
    private Transform launchOrigin;
    private System.Action<Vector2> aimAtPosition;
    // Runtime
    private Vector2 target;
    private PlayerHealth player;
    private SpriteRenderer visual;
    private Collider2D projectileCollider;
    private LineRenderer ring;
    private Material ringMaterial;
    private bool initialized;
    private bool exploded;
    private bool launched;
    private float explosionAge;
    private float radius;
    private float startDistance;
    private float flightAge;
    private Vector2 flightDirection;
    private Vector2 flightTrackingVelocity;
    // 只初始化当前这一颗子弹；连发时序由编排或单发射器测试调度器控制。
    public void Initialize(Vector2 lockedPosition, PlayerHealth targetPlayer, Transform origin = null, System.Action<Vector2> aim = null, float groundLifetime = 0)
    {
        StopAllCoroutines();
        launchOrigin = origin;
        aimAtPosition = aim;
        // Capture this shot's setting; later track edits and emitter exit cannot change it.
        this.groundLifetime = groundLifetime;
        target = lockedPosition;
        player = targetPlayer;
        visual = GetComponent<SpriteRenderer>();
        explosionVfx = GetComponent<LandingExplosionVfx>();
        trailVfx = GetComponent<TraceTrailVfx>();
        projectileCollider = GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            initialized = false;
            Debug.LogError("TraceBullet needs a Collider2D, such as a circle or polygon.", this);
            Destroy(gameObject);
            return;
        }
        radius = Mathf.Max(0.01f, explosionRadius);
        initialized = true;
        exploded = false;
        launched = false;
        explosionAge = 0f;
        // 设置 Projectile Hitbox
        projectileCollider.isTrigger = true;
        // 预警阶段暂时关闭碰撞
        projectileCollider.enabled = false;
        // 预警期间隐藏子弹
        visual.enabled = false;
        // 创建这一颗自己的黄圈
        CreateWarning();
        // 完成预警后起飞。
        StartCoroutine(LaunchAfterWarning());
        Destroy(gameObject, Mathf.Max(0.1f, maxLifetime));
    }

    // 预警结束后发射
    private IEnumerator LaunchAfterWarning()
    {
        float trackingTime = Mathf.Max(0f, preLaunchDuration);
        bool continuousTracking = flightTrackingDuration > 0f;
        flightTrackingVelocity = Vector2.zero;
        for (float elapsed = 0f; elapsed < trackingTime; elapsed += Time.deltaTime)
        {
            if (player == null || player.IsDead)
            {
                Destroy(gameObject);
                yield break;
            }

            if (trackDuringWarning)
                target = trackingSmoothTime > 0f ? Vector2.SmoothDamp(target, player.transform.position, ref flightTrackingVelocity, trackingSmoothTime, Mathf.Infinity, Time.deltaTime) : (Vector2)player.transform.position;
            UpdateWarningPosition();
            aimAtPosition?.Invoke(target);
            yield return null;
        }

        if (trackingTime > 0f && player != null && !player.IsDead)
        {
            UpdateWarningPosition();
            aimAtPosition?.Invoke(target);
            if (!continuousTracking)
                ring.startColor = ring.endColor = Color.Lerp(warningColor, Color.white, 0.5f);
        }

        if (player == null || player.IsDead)
        {
            Destroy(gameObject);
            yield break;
        }

        aimAtPosition?.Invoke(target);
        if (launchOrigin != null)
            transform.position = launchOrigin.position;
        aimAtPosition = null;
        launchOrigin = null;
        // 真正发射这一刻记录距离
        startDistance = Vector2.Distance(transform.position, target);
        // 子弹出现
        visual.enabled = true;
        // 开启飞行碰撞
        projectileCollider.enabled = explodeOnPlayerContact;
        launched = true;
        flightAge = 0f;
        flightDirection = (target - (Vector2)transform.position).normalized;
        if (trailVfx != null) trailVfx.BeginFlight();
        Launched?.Invoke(this);
    }

    // 创建黄色 Warning
    private void CreateWarning() => CreateRing("TraceWarning", warningWidth, warningColor);
    private void UpdateWarningPosition()
    {
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, new Vector3(target.x + Mathf.Cos(angle) * radius, target.y + Mathf.Sin(angle) * radius, 0f));
        }
    }

    // Projectile 飞行
    private void FixedUpdate()
    {
        if (!initialized || !launched || exploded)
        {
            return;
        }

        if (player == null || player.IsDead)
        {
            Destroy(gameObject);
            return;
        }

        if (flightAge < flightTrackingDuration)
        {
            float trackingStep = Mathf.Min(Time.fixedDeltaTime, flightTrackingDuration - flightAge);
            target = trackingSmoothTime > 0f ? Vector2.SmoothDamp(target, player.transform.position, ref flightTrackingVelocity, trackingSmoothTime, Mathf.Infinity, trackingStep) : (Vector2)player.transform.position;
            UpdateWarningPosition();
            if (flightAge + Time.fixedDeltaTime >= flightTrackingDuration)
                ring.startColor = ring.endColor = Color.Lerp(warningColor, Color.white, 0.5f);
        }

        flightAge += Time.fixedDeltaTime;
        // 当前距离目标还有多远
        float remainingDistance = Vector2.Distance(transform.position, target);
        // 飞行进度
        float progress = startDistance <= 0.001f ? 1f : 1f - remainingDistance / startDistance;
        progress = Mathf.Clamp01(progress);
        // Curve 控制速度
        float curveSpeedMultiplier = speedCurve.Evaluate(progress);
        float speedMultiplier = Mathf.Max(minimumSpeedMultiplier, curveSpeedMultiplier);
        float currentSpeed = speed * speedMultiplier;
        // 飞向锁定位置
        Vector2 next = Vector2.MoveTowards(transform.position, target, currentSpeed * Time.fixedDeltaTime);
        if (flightTrackingDuration > 0f && remainingDistance > currentSpeed * Time.fixedDeltaTime)
        {
            Vector2 desiredDirection = (target - (Vector2)transform.position).normalized;
            if (flightDirection.sqrMagnitude < 0.0001f)
                flightDirection = desiredDirection;
            float angle = Vector2.SignedAngle(flightDirection, desiredDirection);
            float rotation = Mathf.Clamp(angle, -turnSpeed * Time.fixedDeltaTime, turnSpeed * Time.fixedDeltaTime);
            flightDirection = Quaternion.Euler(0f, 0f, rotation) * flightDirection;
            next = (Vector2)transform.position + flightDirection * currentSpeed * Time.fixedDeltaTime;
        }

        transform.position = new Vector3(next.x, next.y, 0f);
        // 还没到落点
        if ((next - target).sqrMagnitude > 0.000001f)
        {
            return;
        }

        // 正常抵达落点
        ExplodeAt(target);
    }

    // Projectile 飞行途中碰到玩家
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!explodeOnPlayerContact)
            return;
        if (!launched || exploded)
        {
            return;
        }

        if (player == null || player.IsDead)
        {
            return;
        }

        // 判断碰到的 Collider
        // 是否属于当前 Player。
        PlayerHealth hitPlayer = other.GetComponentInParent<PlayerHealth>();
        if (hitPlayer != player)
        {
            return;
        }

        // 飞行途中命中玩家
        //
        // 不再继续飞向原 Target。
        // 直接在当前 Projectile 位置爆炸。
        ExplodeAt(transform.position);
    }

    // 在指定位置爆炸
    private void ExplodeAt(Vector2 explosionPosition)
    {
        if (exploded)
            return;
        exploded = true;
        launched = false;
        if (trailVfx != null) trailVfx.EndFlight(explosionVisualDuration);
        // 子弹停止参与碰撞
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        // 子弹本体消失
        visual.enabled = false;
        // 非常重要：
        //
        // 把 target 改成真正的爆炸位置。
        //
        // 正常落地：
        // explosionPosition = 原 target
        //
        // 半路撞玩家：
        // explosionPosition = 子弹当前位置
        target = explosionPosition;
        // 原来的黄色落点圈
        //
        // 如果是提前碰撞，
        // 原黄圈已经不再是实际爆炸位置，
        // 所以先删除。
        DestroyWarning();
        // 在真正爆炸位置
        // 创建白色爆炸圈
        if (explosionVfx == null || !explosionVfx.isActiveAndEnabled ||
            !explosionVfx.TryPlay(explosionPosition, radius, visual))
            CreateExplosionRing();
        if (groundAreaPrefab != null && player != null && !player.IsDead)
        {
            if (groundAreaPrefab.gameObject.scene.IsValid())
                Debug.LogError("Ground Area Prefab must be a Project asset.", this);
            else
            {
                GroundArea area = Instantiate(groundAreaPrefab, new Vector3(explosionPosition.x, explosionPosition.y, 0), Quaternion.identity);
                area.Initialize(player, groundLifetime);
            }
        }

        // 范围伤害
        DamagePlayerAt(explosionPosition);
    }

    // 爆炸范围伤害
    private void DamagePlayerAt(Vector2 explosionPosition)
    {
        if (player == null || player.IsDead)
        {
            return;
        }

        foreach (Collider2D hitbox in player.GetComponentsInChildren<Collider2D>())
        {
            if (!hitbox.enabled || !hitbox.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector2 closest = hitbox.ClosestPoint(explosionPosition);
            if ((closest - explosionPosition).sqrMagnitude <= radius * radius)
            {
                player.TakeDamage(damage);
                break;
            }
        }
    }

    // 删除黄色 Warning
    private void DestroyWarning()
    {
        if (ring != null)
        {
            Destroy(ring.gameObject);
            ring = null;
        }

        if (ringMaterial != null)
        {
            Destroy(ringMaterial);
            ringMaterial = null;
        }
    }

    // 创建白色爆炸圈
    private void CreateExplosionRing() => CreateRing("TraceExplosion", 0.09f, Color.white);
    // Warning and explosion share geometry; only their color and width differ.
    private void CreateRing(string label, float width, Color color)
    {
        GameObject marker = new GameObject(label);
        ring = marker.AddComponent<LineRenderer>();
        ringMaterial = new Material(Shader.Find("Sprites/Default"));
        ring.sharedMaterial = ringMaterial;
        ring.useWorldSpace = true;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = width;
        ring.sortingLayerID = visual.sortingLayerID;
        ring.sortingOrder = visual.sortingOrder + 1;
        ring.startColor = ring.endColor = color;
        UpdateWarningPosition();
    }

    // 爆炸视觉淡出
    private void Update()
    {
        if (!exploded)
        {
            return;
        }

        explosionAge += Time.deltaTime;
        float duration = Mathf.Max(0.01f, explosionVisualDuration);
        Color color = Color.white;
        color.a = 1f - Mathf.Clamp01(explosionAge / duration);
        if (ring != null)
        {
            ring.startColor = ring.endColor = color;
        }

        if (explosionAge >= duration)
        {
            Destroy(gameObject);
        }
    }

    // Cleanup
    private void OnDestroy() => DestroyWarning();
}
