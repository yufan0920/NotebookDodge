using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class TraceBullet : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0.1f)]
    private float speed = 6f;

    // X = 飞行进度（0 = 发射，1 = 到达）
    // Y = 速度倍率
    [SerializeField]
    private AnimationCurve speedCurve =
        AnimationCurve.EaseInOut(
            0f, 2f,
            1f, 0.5f
        );

    // 最低速度倍率
    [SerializeField, Min(0.01f)]
    private float minimumSpeedMultiplier = 0.5f;


    [Header("Pre-launch Warning")]

    // 每颗锁定后多久发射
    [SerializeField, Min(0f)]
    private float warningDuration = 0.5f;



    [Header("Projectile Hitbox")]
    // 子弹飞行过程中本身的碰撞半径
    [SerializeField, Min(0.01f)]
    private float projectileHitRadius = 0.15f;


    [Header("Explosion")]
    [SerializeField, Min(0.01f)]
    private float explosionRadius = 0.6f;

    [SerializeField, Min(1)]
    private int damage = 1;

    [SerializeField, Min(0.01f)]
    private float explosionVisualDuration = 0.25f;


    [Header("Lifetime")]
    [SerializeField, Min(0.1f)]
    private float maxLifetime = 30f;


    [Header("Warning")]
    [SerializeField]
    private Color warningColor =
        new Color(
            1f,
            0.65f,
            0.15f,
            0.8f
        );

    [SerializeField, Min(0.001f)]
    private float warningWidth = 0.035f;


    // ========================================
    // Runtime
    // ========================================

    private Vector2 target;
    private PlayerHealth player;

    private SpriteRenderer visual;
    private CircleCollider2D projectileCollider;

    private LineRenderer ring;
    private Material ringMaterial;

    private bool initialized;
    private bool exploded;

    private bool launched;

    private float explosionAge;
    private float radius;
    private float startDistance;

    // 每次调用只初始化当前这一颗子弹。连发数量与间隔统一由 TraceEmitter 控制。
    public void Initialize(
        Vector2 lockedPosition,
        PlayerHealth targetPlayer)
    {
        StopAllCoroutines();


        target =
            lockedPosition;

        player =
            targetPlayer;


        visual =
            GetComponent<SpriteRenderer>();

        projectileCollider =
            GetComponent<CircleCollider2D>();


        radius =
            Mathf.Max(
                0.01f,
                explosionRadius
            );


        initialized = true;

        exploded = false;
        launched = false;

        explosionAge = 0f;


        // --------------------------------
        // 设置 Projectile Hitbox
        // --------------------------------

        projectileCollider.isTrigger = true;

        projectileCollider.radius =
            projectileHitRadius;

        // 预警阶段暂时关闭碰撞
        projectileCollider.enabled = false;


        // --------------------------------
        // 预警期间隐藏子弹
        // --------------------------------

        visual.enabled = false;


        // --------------------------------
        // 创建这一颗自己的黄圈
        // --------------------------------

        CreateWarning();


        // --------------------------------
        // 等待 warningDuration 后起飞，期间落点不再改变
        // --------------------------------

        StartCoroutine(
            LaunchAfterWarning()
        );


        Destroy(
            gameObject,
            Mathf.Max(
                0.1f,
                maxLifetime
            )
        );
    }


    // ========================================
    // 预警结束后发射
    // ========================================

    private IEnumerator LaunchAfterWarning()
    {
        yield return
            new WaitForSeconds(
                warningDuration
            );


        if (
            player == null ||
            player.IsDead
        )
        {
            Destroy(gameObject);
            yield break;
        }


        // 真正发射这一刻记录距离
        startDistance =
            Vector2.Distance(
                transform.position,
                target
            );


        // 子弹出现
        visual.enabled = true;

        // 开启飞行碰撞
        projectileCollider.enabled = true;

        launched = true;
    }


    // ========================================
    // 创建黄色 Warning
    // ========================================

    private void CreateWarning()
    {
        GameObject marker =
            new GameObject(
                "TraceWarning"
            );


        ring =
            marker.AddComponent<LineRenderer>();


        ringMaterial =
            new Material(
                Shader.Find(
                    "Sprites/Default"
                )
            );


        ring.sharedMaterial =
            ringMaterial;


        ring.useWorldSpace = true;
        ring.loop = true;

        ring.positionCount = 64;

        ring.widthMultiplier =
            warningWidth;


        ring.sortingLayerID =
            visual.sortingLayerID;

        ring.sortingOrder =
            visual.sortingOrder + 1;


        ring.startColor =
            ring.endColor =
            warningColor;


        for (
            int i = 0;
            i < ring.positionCount;
            i++
        )
        {
            float angle =
                i *
                Mathf.PI *
                2f /
                ring.positionCount;


            ring.SetPosition(
                i,
                new Vector3(
                    target.x +
                    Mathf.Cos(angle) *
                    radius,

                    target.y +
                    Mathf.Sin(angle) *
                    radius,

                    0f
                )
            );
        }
    }


    // ========================================
    // Projectile 飞行
    // ========================================

    private void FixedUpdate()
    {
        if (
            !initialized ||

            !launched ||
            exploded
        )
        {
            return;
        }


        if (
            player == null ||
            player.IsDead
        )
        {
            Destroy(gameObject);
            return;
        }


        // 当前距离目标还有多远
        float remainingDistance =
            Vector2.Distance(
                transform.position,
                target
            );


        // 飞行进度
        float progress =
            startDistance <= 0.001f
            ? 1f
            : 1f -
              remainingDistance /
              startDistance;


        progress =
            Mathf.Clamp01(
                progress
            );


        // Curve 控制速度
        float curveSpeedMultiplier =
            speedCurve.Evaluate(
                progress
            );


        float speedMultiplier =
            Mathf.Max(
                minimumSpeedMultiplier,
                curveSpeedMultiplier
            );


        float currentSpeed =
            speed *
            speedMultiplier;


        // 飞向锁定位置
        Vector2 next =
            Vector2.MoveTowards(
                transform.position,
                target,
                currentSpeed *
                Time.fixedDeltaTime
            );


        transform.position =
            new Vector3(
                next.x,
                next.y,
                0f
            );


        // 还没到落点
        if (
            (next - target).sqrMagnitude >
            0.000001f
        )
        {
            return;
        }


        // 正常抵达落点
        ExplodeAt(target);
    }


    // ========================================
    // Projectile 飞行途中碰到玩家
    // ========================================

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (

            !launched ||
            exploded
        )
        {
            return;
        }


        if (
            player == null ||
            player.IsDead
        )
        {
            return;
        }


        // 判断碰到的 Collider
        // 是否属于当前 Player。
        PlayerHealth hitPlayer =
            other.GetComponentInParent
            <PlayerHealth>();


        if (hitPlayer != player)
        {
            return;
        }


        // --------------------------------
        // 飞行途中命中玩家
        //
        // 不再继续飞向原 Target。
        // 直接在当前 Projectile 位置爆炸。
        // --------------------------------

        ExplodeAt(
            transform.position
        );
    }


    // ========================================
    // 在指定位置爆炸
    // ========================================

    private void ExplodeAt(
        Vector2 explosionPosition)
    {
        if (exploded)
            return;


        exploded = true;
        launched = false;


        // 子弹停止参与碰撞
        if (projectileCollider != null)
        {
            projectileCollider.enabled =
                false;
        }


        // 子弹本体消失
        visual.enabled = false;


        // ====================================
        // 非常重要：
        //
        // 把 target 改成真正的爆炸位置。
        //
        // 正常落地：
        // explosionPosition = 原 target
        //
        // 半路撞玩家：
        // explosionPosition = 子弹当前位置
        // ====================================

        target =
            explosionPosition;


        // --------------------------------
        // 原来的黄色落点圈
        //
        // 如果是提前碰撞，
        // 原黄圈已经不再是实际爆炸位置，
        // 所以先删除。
        // --------------------------------

        DestroyWarning();


        // --------------------------------
        // 在真正爆炸位置
        // 创建白色爆炸圈
        // --------------------------------

        CreateExplosionRing();


        // --------------------------------
        // 范围伤害
        // --------------------------------

        DamagePlayerAt(
            explosionPosition
        );
    }


    // ========================================
    // 爆炸范围伤害
    // ========================================

    private void DamagePlayerAt(
        Vector2 explosionPosition)
    {
        if (
            player == null ||
            player.IsDead
        )
        {
            return;
        }


        foreach (
            Collider2D hitbox
            in player
            .GetComponentsInChildren
            <Collider2D>()
        )
        {
            if (
                !hitbox.enabled ||
                !hitbox.gameObject
                .activeInHierarchy
            )
            {
                continue;
            }


            Vector2 closest =
                hitbox.ClosestPoint(
                    explosionPosition
                );


            if (
                (closest -
                 explosionPosition)
                .sqrMagnitude
                <=
                radius * radius
            )
            {
                player.TakeDamage(
                    damage
                );

                break;
            }
        }
    }


    // ========================================
    // 删除黄色 Warning
    // ========================================

    private void DestroyWarning()
    {
        if (ring != null)
        {
            Destroy(
                ring.gameObject
            );

            ring = null;
        }


        if (ringMaterial != null)
        {
            Destroy(
                ringMaterial
            );

            ringMaterial = null;
        }
    }


    // ========================================
    // 创建白色爆炸圈
    // ========================================

    private void CreateExplosionRing()
    {
        GameObject marker =
            new GameObject(
                "TraceExplosion"
            );


        ring =
            marker.AddComponent<LineRenderer>();


        ringMaterial =
            new Material(
                Shader.Find(
                    "Sprites/Default"
                )
            );


        ring.sharedMaterial =
            ringMaterial;


        ring.useWorldSpace = true;
        ring.loop = true;

        ring.positionCount = 64;

        ring.widthMultiplier =
            0.09f;


        ring.sortingLayerID =
            visual.sortingLayerID;

        ring.sortingOrder =
            visual.sortingOrder + 1;


        ring.startColor =
            ring.endColor =
            Color.white;


        for (
            int i = 0;
            i < ring.positionCount;
            i++
        )
        {
            float angle =
                i *
                Mathf.PI *
                2f /
                ring.positionCount;


            ring.SetPosition(
                i,
                new Vector3(
                    target.x +
                    Mathf.Cos(angle) *
                    radius,

                    target.y +
                    Mathf.Sin(angle) *
                    radius,

                    0f
                )
            );
        }
    }


    // ========================================
    // 爆炸视觉淡出
    // ========================================

    private void Update()
    {
        if (

            !exploded
        )
        {
            return;
        }


        explosionAge +=
            Time.deltaTime;


        float duration =
            Mathf.Max(
                0.01f,
                explosionVisualDuration
            );


        Color color =
            Color.white;


        color.a =
            1f -
            Mathf.Clamp01(
                explosionAge /
                duration
            );


        if (ring != null)
        {
            ring.startColor =
                ring.endColor =
                color;
        }


        if (
            explosionAge >= duration
        )
        {
            Destroy(gameObject);
        }
    }


    // ========================================
    // Cleanup
    // ========================================

    private void OnDestroy()
    {
        if (ring != null)
        {
            Destroy(
                ring.gameObject
            );
        }


        if (ringMaterial != null)
        {
            Destroy(
                ringMaterial
            );
        }
    }
}