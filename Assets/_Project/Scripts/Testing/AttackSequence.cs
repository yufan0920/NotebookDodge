using UnityEngine;

// Repeating bursts for EmitterTestDriver. Level tracks use AbsoluteAttackSchedule.
public sealed class AttackSequence
{
    private float remaining;
    private int shotsInBurst, shotIndex;
    private bool waitingForCompletion;
    public bool Failed { get; private set; }
    public AttackSequence(float delay = 0) { remaining = Mathf.Max(0, delay); }
    public void Tick(float deltaTime, ITimelineEmitter emitter, AttackSettings settings, PlayerHealth player)
    {
        if (Failed || !emitter.CanFire) return;
        if (waitingForCompletion)
        {
            if (emitter.IsBusy) return;
            waitingForCompletion = false;
            remaining = Mathf.Max(0.05f, settings.cooldown);
        }
        remaining -= deltaTime;
        if (remaining > 0) return;
        if (!emitter.Fire(settings, player, shotIndex)) { Failed = true; return; }
        shotIndex++;
        shotsInBurst++;
        bool burstFinished = shotsInBurst >= Mathf.Max(1, settings.burstCount);
        if (burstFinished) shotsInBurst = 0;
        waitingForCompletion = burstFinished && emitter.CooldownAfterCompletion;
        remaining = emitter.PreparationDuration + Mathf.Max(0.05f,
            burstFinished ? settings.cooldown : settings.shotInterval);
    }
}
