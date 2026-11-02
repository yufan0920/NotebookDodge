using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerHealth))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 4f;
    [SerializeField] private PlayerMovementArea movementArea;
    [Tooltip("World-space clearance for the player center from each boundary edge. Does not change Bounce or Laser boundaries.")]
    [SerializeField, Min(0f)] private float boundaryPadding = 0.3f;
    private bool warnedPadding;
    // Bounds describe the allowed position of the player's CENTER.
    [SerializeField] private Vector2 minPosition = new Vector2(-7.5f, -4f);
    [SerializeField] private Vector2 maxPosition = new Vector2(7.5f, 4f);
    private Rigidbody2D body;
    private PlayerHealth health;
    private Vector2 input;
    public PlayerMovementArea MovementArea => movementArea;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
    }

    private void Update()
    {
        input = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || health.IsDead || Time.timeScale <= 0 || !Application.isFocused) return;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1;
        // Keep diagonals from being sqrt(2) times faster.
        input = Vector2.ClampMagnitude(input, 1f);
    }

    private void FixedUpdate()
    {
        if (health.IsDead) return;
        float areaMultiplier = GroundArea.GetSpeedMultiplier(health, body.position);
        Vector2 next = body.position + input * speed * areaMultiplier * Time.fixedDeltaTime;
        Vector2 constrained;
        if (movementArea != null && movementArea.isActiveAndEnabled)
        {
            if (!movementArea.TryConstrain(next, boundaryPadding, out constrained))
            {
                if (!warnedPadding) Debug.LogWarning("Player cannot move: check Movement Area and reduce Boundary Padding if the inset area is empty.", this);
                warnedPadding = true;
                return;
            }
            warnedPadding = false;
            next = constrained;
        }
        else
        {
            float padding = float.IsNaN(boundaryPadding) || float.IsInfinity(boundaryPadding) ? 0 : Mathf.Max(0, boundaryPadding);
            Vector2 low = minPosition + Vector2.one * padding;
            Vector2 high = maxPosition - Vector2.one * padding;
            next.x = low.x <= high.x ? Mathf.Clamp(next.x, low.x, high.x) : (minPosition.x + maxPosition.x) / 2;
            next.y = low.y <= high.y ? Mathf.Clamp(next.y, low.y, high.y) : (minPosition.y + maxPosition.y) / 2;
        }
        body.MovePosition(next);
    }
}
