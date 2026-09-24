using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ControlPointDragger : MonoBehaviour
{
    [SerializeField] private BezierCurve editableCurve;
    [SerializeField] private GameObject pointPrefab;
    [SerializeField] private Color hoverColor = Color.white;

    private Camera cam;
    private Transform dragged;
    private Plane dragPlane;
    private Vector3 grabOffset;
    private Renderer hovered;
    private Color hoveredColor;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        if (mouse == null) return;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        UpdateHover(ray);

        if (mouse.leftButton.wasPressedThisFrame && Physics.Raycast(ray, out RaycastHit hit, 500f) && hit.collider.GetComponent<ControlPointHandle>())
        {
            dragged = hit.transform;
            dragPlane = new Plane(-cam.transform.forward, dragged.position);
            dragPlane.Raycast(ray, out float enter);
            grabOffset = dragged.position - ray.GetPoint(enter);
        }

        if (dragged && mouse.leftButton.isPressed && dragPlane.Raycast(ray, out float d))
            dragged.position = ray.GetPoint(d) + grabOffset;

        if (mouse.leftButton.wasReleasedThisFrame) dragged = null;

        if (kb == null || !editableCurve) return;

        if (kb.nKey.wasPressedThisFrame && pointPrefab)
        {
            var pts = editableCurve.ControlPoints;
            Vector3 pos = pts.Count > 0 && pts[pts.Count - 1] ? pts[pts.Count - 1].position + new Vector3(1.5f, 0f, 1f) : Vector3.zero;
            var p = Instantiate(pointPrefab, pos, Quaternion.identity, editableCurve.transform);
            p.name = "P" + pts.Count;
            editableCurve.AddControlPoint(p.transform);
        }

        if (kb.backspaceKey.wasPressedThisFrame)
        {
            var removed = editableCurve.RemoveLastControlPoint();
            if (removed) Destroy(removed.gameObject);
        }
    }

    private void UpdateHover(Ray ray)
    {
        Renderer r = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 500f) && hit.collider.GetComponent<ControlPointHandle>())
            r = hit.collider.GetComponent<Renderer>();
        if (dragged) r = dragged.GetComponent<Renderer>();

        if (r == hovered) return;
        if (hovered) hovered.material.color = hoveredColor;
        hovered = r;
        if (hovered)
        {
            hoveredColor = hovered.material.color;
            hovered.material.color = hoverColor;
        }
    }
}
