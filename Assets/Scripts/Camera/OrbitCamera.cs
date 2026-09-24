using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Zoom")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 12f;
    [SerializeField] private float zoomStep = 1f;
    [SerializeField] private float zoomSmoothing = 10f;

    [Header("Rotation")]
    [SerializeField] private float sensitivity = 0.15f;
    [SerializeField] private float minPitch = 5f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private float startPitch = 20f;

    [Header("Collision (bonus)")]
    [SerializeField] private bool avoidObstacles = true;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float cameraRadius = 0.25f;
    [SerializeField] private float obstaclePadding = 0.2f;
    [SerializeField] private float returnSmoothing = 4f;

    private float yaw;
    private float pitch;
    private float desiredDistance;
    private float zoomDistance;
    private float currentDistance;

    private void Start()
    {
        pitch = startPitch;
        desiredDistance = zoomDistance = currentDistance = distance;
        if (target) yaw = target.eulerAngles.y;
    }

    private void LateUpdate()
    {
        if (!target) return;

        var mouse = Mouse.current;
        bool rightHeld = mouse != null && mouse.rightButton.isPressed;
        bool leftHeld = mouse != null && mouse.leftButton.isPressed;

        if (rightHeld || leftHeld)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (rightHeld) target.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                desiredDistance = Mathf.Clamp(desiredDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }
        zoomDistance = Mathf.Lerp(zoomDistance, desiredDistance, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot = target.position + pivotOffset;
        Vector3 back = rotation * Vector3.back;

        float allowed = zoomDistance;
        if (avoidObstacles && Physics.SphereCast(pivot, cameraRadius, back, out RaycastHit hit, zoomDistance, obstacleMask, QueryTriggerInteraction.Ignore))
            allowed = Mathf.Max(0.3f, hit.distance - obstaclePadding);

        currentDistance = allowed < currentDistance
            ? allowed
            : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-returnSmoothing * Time.deltaTime));

        transform.position = pivot + back * currentDistance;
        transform.rotation = rotation;
    }
}
