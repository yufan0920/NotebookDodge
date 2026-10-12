//发射器统一接口，让测试控制器能调用不同类型的发射器
public interface ITimelineEmitter
{
    bool IsBusy { get; }
    bool CanFire { get; }
    float PreparationDuration { get; }
    bool CooldownAfterCompletion { get; }
    bool Fire(AttackSettings settings, PlayerHealth player, int shotIndex);
}
