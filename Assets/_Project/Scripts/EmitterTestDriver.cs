using UnityEngine;

// Prototype-only orchestration. Timeline instances disable this component.
public class EmitterTestDriver : MonoBehaviour
{
    [SerializeField] private MonoBehaviour emitter;
    [SerializeField] private PlayerHealth player;
    [SerializeField, Min(0)] private float initialDelay = 1;
    [SerializeField] private AttackSettings attack = new AttackSettings();
    private AttackSequence sequence;
    private void OnEnable() { sequence = null; }
    private void Update()
    {
        if (emitter == null || !emitter.enabled) { sequence = null; return; }
        if (player == null || player.IsDead || !(emitter is ITimelineEmitter source)) return;
        if (sequence == null) sequence = new AttackSequence(initialDelay);
        sequence.Tick(Time.deltaTime, source, attack, player);
        if (sequence.Failed) { Debug.LogError("Test emission failed. Check the projectile prefab and attack settings.", this); enabled = false; }
    }
}
