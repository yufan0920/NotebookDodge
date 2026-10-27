// Optional orientation contract for placement; projectile behavior stays in the emitter.
public interface IEmitterOrientation
{
    UnityEngine.Vector2 LocalForwardDirection { get; }
}
