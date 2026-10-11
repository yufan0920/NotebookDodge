using System;
using UnityEngine;

[Serializable]
public class AttackSettings
{
    [Min(1)] public int bulletCount = 1;
    [Range(0, 180)] public float spreadAngle = 40;
    public bool aimAtPlayer;
    [Min(1)] public int burstCount = 1;
    [Min(0.05f)] public float shotInterval = 0.5f;
    [Min(0.05f)] public float cooldown = 2;
    public Vector2[] landingPoints = new Vector2[0];
    public AttackSettings Copy()
    {
        var copy = (AttackSettings)MemberwiseClone();
        copy.landingPoints = landingPoints == null ? new Vector2[0] : (Vector2[])landingPoints.Clone();
        return copy;
    }
}

// Shared scheduling for timeline clips and the independent test driver.
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
