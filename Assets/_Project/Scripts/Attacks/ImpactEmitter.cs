using UnityEngine;

public class ImpactEmitter : MonoBehaviour, ITimelineEmitter
{
    [SerializeField] private ImpactBullet bulletPrefab;
    public bool IsBusy => false;
    public bool CanFire => true;
    public float PreparationDuration => 0;
    public bool CooldownAfterCompletion => false;
    public bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex)
    {
        if (player == null || player.IsDead || bulletPrefab == null || bulletPrefab.gameObject.scene.IsValid()) return false;
        if (settings.ValidateLandingPoints(gameObject.scene) != null ||
            !settings.TryGetLandingPoint(shotIndex, out Vector2 position)) return false;
        ImpactBullet bullet = Instantiate(bulletPrefab, new Vector3(position.x, position.y, 0), Quaternion.identity);
        return bullet.Initialize(position, player);
    }
}
