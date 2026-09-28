using UnityEngine;
using UnityEngine.Serialization;

public class LaserEmitter : MonoBehaviour
{
    public enum ForwardAxis { Right = 0, Left = 1, Down = 2, Up = 3 }

    [SerializeField] private LaserBeam beamPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField, Tooltip("出射位置；激光方向始终跟随发射器的旋转。")]
    private Transform muzzle;
    [FormerlySerializedAs("direction")]
    [SerializeField, Tooltip("发射器未旋转时，图片炮口朝向哪一端。旋转发射器的 Z 轴即可朝任意角度发射。")]
    private ForwardAxis localForwardAxis;
    [SerializeField, Min(0)] private float initialDelay = 1f;
    [SerializeField, Min(0)] private float cooldown = 3f;

    private LaserBeam activeBeam;
    private float remaining;
    private bool waitingForEnd;

    private void OnEnable() => remaining = Mathf.Max(0, initialDelay);

    private void Start()
    {
        if (beamPrefab == null || player == null || beamPrefab.gameObject.scene.IsValid())
        {
            Debug.LogError("Assign a LaserBeam prefab asset and scene Player to LaserEmitter.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (player == null || player.IsDead || beamPrefab == null || activeBeam != null) return;
        if (waitingForEnd)
        {
            waitingForEnd = false;
            remaining = Mathf.Max(0, cooldown);
        }
        remaining -= Time.deltaTime;
        if (remaining > 0) return;
        activeBeam = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        activeBeam.transform.localScale = Vector3.one;
        Transform source = muzzle != null ? muzzle : transform;
        Vector3 localDirection;
        switch (localForwardAxis)
        {
            case ForwardAxis.Left: localDirection = Vector3.left; break;
            case ForwardAxis.Down: localDirection = Vector3.down; break;
            case ForwardAxis.Up: localDirection = Vector3.up; break;
            default: localDirection = Vector3.right; break;
        }
        Vector2 heading = transform.TransformDirection(localDirection);
        if (!activeBeam.Initialize(player, heading, source.position))
        {
            enabled = false;
            return;
        }
        waitingForEnd = true;
    }
}
