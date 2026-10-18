using UnityEngine;

public class LaserEmitter : MonoBehaviour, ITimelineEmitter, IEmitterOrientation
{
    [SerializeField] private LaserBeam beamPrefab;
    [SerializeField, Tooltip("Fixed forward direction of the artwork before Rotation Z is applied.")]
    private Vector2 localForwardDirection = Vector2.right;
    public Vector2 LocalForwardDirection => localForwardDirection.sqrMagnitude > 0.0001f
        ? localForwardDirection.normalized : Vector2.right;
    [SerializeField] private Transform muzzle;
    private LaserBeam activeBeam;
    public bool IsBusy => activeBeam != null;
    public bool CanFire => !IsBusy;
    public float PreparationDuration => 0;
    public bool CooldownAfterCompletion => true;
    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || beamPrefab == null || beamPrefab.gameObject.scene.IsValid() || IsBusy) return false;
        Vector2 local = LocalForwardDirection;
        Vector2 heading = transform.TransformDirection(local);
        Transform source = muzzle != null ? muzzle : transform;
        activeBeam = Instantiate(beamPrefab, Vector3.zero, Quaternion.identity);
        activeBeam.transform.localScale = Vector3.one;
        return activeBeam.Initialize(player, heading, source.position);
    }
}
