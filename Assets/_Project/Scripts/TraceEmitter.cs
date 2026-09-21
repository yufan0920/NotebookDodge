using UnityEngine;

// 追踪弹发射器：每颗子弹发射时重新取样玩家位置，一组结束后休息。
public class TraceEmitter : MonoBehaviour
{
    [SerializeField] private TraceBullet dropPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0f)] private float initialDelay = 1f;
    [SerializeField, Min(1)] private int burstCount = 3;
    [SerializeField, Min(0.05f)] private float shotInterval = 0.6f;
    [Tooltip("从本组最后一颗子弹发射，到下一组第一颗子弹发射的时间。")]
    [SerializeField, Min(0.05f)] private float burstCooldown = 2f;
    private float remaining;
    private int shotsFired;

    private void OnEnable()
    {
        remaining = Mathf.Max(0f, initialDelay);
        shotsFired = 0;
    }

    private void Start()
    {
        if (dropPrefab == null || player == null || dropPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a Project asset to Drop Prefab and a scene Player to TraceEmitter.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (player == null || player.IsDead || dropPrefab == null) return;
        remaining -= Time.deltaTime;
        if (remaining > 0f) return;
        // 保存坐标值，不把目标 Transform 作为持续追踪的位置来源。
        Vector2 lockedPosition = player.transform.position;
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        TraceBullet drop = Instantiate(dropPrefab, origin, Quaternion.identity);
        drop.Initialize(lockedPosition, player);
        shotsFired++;
        if (shotsFired >= Mathf.Max(1, burstCount))
        {
            shotsFired = 0;
            remaining = Mathf.Max(0.05f, burstCooldown);
        }
        else remaining = Mathf.Max(0.05f, shotInterval);
    }
}
