using UnityEngine;

public class ImpactEmitter : MonoBehaviour
{
    [SerializeField] private ImpactBullet bulletPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform[] landingPoints = new Transform[0];
    [SerializeField] private bool autoFire = true;
    [SerializeField, Min(0)] private float initialDelay = 1f;
    [SerializeField, Min(0.05f)] private float interval = 4f;
    private float remaining;
    private int nextPoint;

    private void OnEnable()
    {
        remaining = Mathf.Max(0, initialDelay);
        nextPoint = 0;
    }

    private void Update()
    {
        if (!autoFire || player == null || player.IsDead) return;
        remaining -= Time.deltaTime;
        if (remaining > 0) return;
        Vector2 position;
        if (!TryGetNextPoint(out position))
        {
            Debug.LogError("ImpactEmitter needs at least one scene Landing Point.", this);
            enabled = false;
            return;
        }
        if (!FireAt(position)) { enabled = false; return; }
        remaining = Mathf.Max(0.05f, interval);
    }

    private bool TryGetNextPoint(out Vector2 position)
    {
        position = default;
        if (landingPoints == null || landingPoints.Length == 0) return false;
        for (int i = 0; i < landingPoints.Length; i++)
        {
            nextPoint %= landingPoints.Length;
            Transform point = landingPoints[nextPoint];
            nextPoint = (nextPoint + 1) % landingPoints.Length;
            if (point == null || !point.gameObject.scene.IsValid()) continue;
            position = point.position;
            return true;
        }
        return false;
    }

    public bool FireAt(Vector2 position)
    {
        if (player == null || player.IsDead) return false;
        if (bulletPrefab == null || bulletPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a Project ImpactBullet prefab and scene Player.", this);
            return false;
        }
        ImpactBullet bullet = Instantiate(bulletPrefab, new Vector3(position.x, position.y, 0), Quaternion.identity);
        return bullet.Initialize(position, player);
    }

    private void OnDrawGizmosSelected()
    {
        if (landingPoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (Transform point in landingPoints)
            if (point != null) Gizmos.DrawWireSphere(point.position, 0.15f);
    }
}
