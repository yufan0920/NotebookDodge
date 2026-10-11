public interface ITimelineEmitter
{
    bool IsBusy { get; }
    bool CanFire { get; }
    float PreparationDuration { get; }
    bool CooldownAfterCompletion { get; }
    bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex);
}
