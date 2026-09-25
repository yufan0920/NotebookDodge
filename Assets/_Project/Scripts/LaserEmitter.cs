using UnityEngine;

public class LaserEmitter : MonoBehaviour
{
    public enum DrawDirection { LeftToRight = 0, RightToLeft = 1, TopToBottom = 2, BottomToTop = 3 }

    [SerializeField] private LaserBeam beamPrefab;
    [SerializeField] private PlayerHealth player;
    [SerializeField] private Transform muzzle;
    [SerializeField] private DrawDirection direction;
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
        switch (direction)
        {
            case DrawDirection.RightToLeft: localDirection = Vector3.left; break;
            case DrawDirection.TopToBottom: localDirection = Vector3.down; break;
            case DrawDirection.BottomToTop: localDirection = Vector3.up; break;
            default: localDirection = Vector3.right; break;
        }
        Vector2 heading = source.TransformDirection(localDirection);
        if (!activeBeam.Initialize(player, heading, source.position))
        {
            enabled = false;
            return;
        }
        waitingForEnd = true;
    }
}
