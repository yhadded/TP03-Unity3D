using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private InputAction move;
    private InputAction jump;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    public bool CanMove { get; set; } = true;
    public bool IsGrounded { get; private set; }
    public float MoveSpeed => moveSpeed;
    public float HorizontalSpeed => new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
    public event Action Jumped;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        move = new InputAction("Move", InputActionType.Value);
        move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        move.AddBinding("<Gamepad>/leftStick");

        jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jump.AddBinding("<Gamepad>/buttonSouth");
    }

    private void OnEnable()
    {
        move.Enable();
        jump.Enable();
    }

    private void OnDisable()
    {
        move.Disable();
        jump.Disable();
    }

    private void OnDestroy()
    {
        move.Dispose();
        jump.Dispose();
    }

    private void Update()
    {
        IsGrounded = controller.isGrounded;
        if (IsGrounded && verticalVelocity < 0f) verticalVelocity = -2f;

        Vector2 input = CanMove ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        Vector3 target = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, target, 1f - Mathf.Exp(-acceleration * Time.deltaTime));

        if (CanMove && IsGrounded && jump.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Jumped?.Invoke();
        }

        verticalVelocity += gravity * Time.deltaTime;
        controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
    }
}
