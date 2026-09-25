using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D), typeof(PlayerHealth))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float speed = 4f;
    [SerializeField] private PlayerMovementArea movementArea;
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
        if (keyboard == null || health.IsDead || !Application.isFocused) return;
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
        Vector2 next = body.position + input * speed * Time.fixedDeltaTime;
        Vector2 constrained;
        if (movementArea != null && movementArea.isActiveAndEnabled &&
            movementArea.TryConstrain(next, out constrained))
            next = constrained;
        else
        {
            next.x = Mathf.Clamp(next.x, minPosition.x, maxPosition.x);
            next.y = Mathf.Clamp(next.y, minPosition.y, maxPosition.y);
        }
        body.MovePosition(next);
    }
}
