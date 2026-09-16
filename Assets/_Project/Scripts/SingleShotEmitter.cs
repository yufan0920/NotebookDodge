using UnityEngine;

public class SingleShotEmitter : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField, Min(0.1f)] private float interval = 1f;
    [SerializeField] private Vector2 direction = Vector2.down;
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
        Bullet bullet = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        bullet.Initialize(direction);
    }
}
