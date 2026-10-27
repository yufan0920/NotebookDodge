using UnityEngine;

// Keeps the assigned artwork facing along flight. Artwork should point upward.
[RequireComponent(typeof(TraceBullet), typeof(SpriteRenderer))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: null, sourceClassName: "TraceTriangleVisual")]
public sealed class TraceBulletOrientation : MonoBehaviour
{
    private TraceBullet bullet;

    private void Awake()
    {
        bullet = GetComponent<TraceBullet>();
    }

    private void LateUpdate()
    {
        if (!bullet.IsFlying || bullet.FlightDirection.sqrMagnitude < 0.0001f) return;
        Vector2 heading = bullet.FlightDirection;
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg - 90f);
    }
}
