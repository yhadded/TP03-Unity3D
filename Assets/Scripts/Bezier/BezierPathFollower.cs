using UnityEngine;
using UnityEngine.InputSystem;

public class BezierPathFollower : MonoBehaviour
{
    [SerializeField] private BezierCurve path;
    [SerializeField] private float speed = 12f;
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 40f;
    [SerializeField] private float lookAhead = 6f;
    [SerializeField] private float rotationSmoothing = 4f;
    [SerializeField] private bool loop = true;
    [SerializeField, Range(100, 5000)] private int samples = 1500;

    private float[] cumulative;
    private Vector3[] points;
    private float totalLength;
    private float distance;

    public float Speed => speed;
    public float Progress => totalLength > 0f ? distance / totalLength : 0f;

    private void Start()
    {
        BuildTable();
        if (points == null) return;
        transform.position = PointAt(0f);
        transform.rotation = Quaternion.LookRotation(PointAt(lookAhead) - transform.position);
    }

    public void BuildTable()
    {
        if (!path || !path.IsValid) return;
        points = new Vector3[samples + 1];
        cumulative = new float[samples + 1];
        points[0] = path.Evaluate(0f);
        for (int i = 1; i <= samples; i++)
        {
            points[i] = path.Evaluate((float)i / samples);
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
        }
        totalLength = cumulative[samples];
    }

    private void Update()
    {
        if (points == null || totalLength <= 0f) return;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.upArrowKey.isPressed || kb.numpadPlusKey.isPressed) speed = Mathf.Min(maxSpeed, speed + 10f * Time.deltaTime);
            if (kb.downArrowKey.isPressed || kb.numpadMinusKey.isPressed) speed = Mathf.Max(minSpeed, speed - 10f * Time.deltaTime);
            if (kb.rKey.wasPressedThisFrame) distance = 0f;
        }

        distance += speed * Time.deltaTime;
        if (distance > totalLength) distance = loop ? distance - totalLength : totalLength;

        transform.position = PointAt(distance);
        Vector3 ahead = PointAt(distance + lookAhead) - transform.position;
        if (ahead.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(ahead), 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime));
    }

    private Vector3 PointAt(float d)
    {
        if (loop) d = Mathf.Repeat(d, totalLength);
        else d = Mathf.Clamp(d, 0f, totalLength);

        int lo = 0, hi = cumulative.Length - 1;
        while (lo < hi - 1)
        {
            int mid = (lo + hi) / 2;
            if (cumulative[mid] < d) lo = mid;
            else hi = mid;
        }
        float segment = cumulative[hi] - cumulative[lo];
        float t = segment > 0f ? (d - cumulative[lo]) / segment : 0f;
        return Vector3.Lerp(points[lo], points[hi], t);
    }
}
