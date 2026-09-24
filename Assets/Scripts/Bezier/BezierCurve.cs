using System.Collections.Generic;
using UnityEngine;

public enum BezierMode { Quadratic, Cubic, Recursive, CubicChain }

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class BezierCurve : MonoBehaviour
{
    [SerializeField] private BezierMode mode = BezierMode.Cubic;
    [SerializeField] private List<Transform> controlPoints = new List<Transform>();
    [SerializeField, Range(2, 500)] private int resolution = 60;
    [SerializeField] private LineRenderer polygonLine;

    private LineRenderer line;
    private readonly List<Vector3> buffer = new List<Vector3>();

    public BezierMode Mode => mode;
    public List<Transform> ControlPoints => controlPoints;

    public int RequiredPoints => mode switch
    {
        BezierMode.Quadratic => 3,
        BezierMode.Cubic => 4,
        _ => 2
    };

    public bool IsValid
    {
        get
        {
            int count = CountValidPoints();
            if (mode == BezierMode.CubicChain) return count >= 4;
            return count >= RequiredPoints;
        }
    }

    private void OnEnable()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Update()
    {
        Redraw();
    }

    public void Redraw()
    {
        if (!line) line = GetComponent<LineRenderer>();
        if (!IsValid)
        {
            line.positionCount = 0;
            if (polygonLine) polygonLine.positionCount = 0;
            return;
        }

        line.useWorldSpace = true;
        line.positionCount = resolution + 1;
        for (int i = 0; i <= resolution; i++)
            line.SetPosition(i, Evaluate((float)i / resolution));

        if (polygonLine)
        {
            polygonLine.useWorldSpace = true;
            var pts = Points();
            polygonLine.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) polygonLine.SetPosition(i, pts[i]);
        }
    }

    public Vector3 Evaluate(float t)
    {
        t = Mathf.Clamp01(t);
        var p = Points();
        switch (mode)
        {
            case BezierMode.Quadratic:
                return Bezier.Quadratic(p[0], p[1], p[2], t);
            case BezierMode.Cubic:
                return Bezier.Cubic(p[0], p[1], p[2], p[3], t);
            case BezierMode.Recursive:
                return Bezier.Recursive(p, t);
            default:
                int segments = (p.Count - 1) / 3;
                float scaled = t * segments;
                int s = Mathf.Min(Mathf.FloorToInt(scaled), segments - 1);
                int i0 = s * 3;
                return Bezier.Cubic(p[i0], p[i0 + 1], p[i0 + 2], p[i0 + 3], scaled - s);
        }
    }

    public void AddControlPoint(Transform point)
    {
        controlPoints.Add(point);
    }

    public Transform RemoveLastControlPoint()
    {
        if (controlPoints.Count <= RequiredPoints) return null;
        var last = controlPoints[controlPoints.Count - 1];
        controlPoints.RemoveAt(controlPoints.Count - 1);
        return last;
    }

    private int CountValidPoints()
    {
        int count = 0;
        foreach (var t in controlPoints) if (t) count++;
        return count;
    }

    private List<Vector3> Points()
    {
        buffer.Clear();
        foreach (var t in controlPoints) if (t) buffer.Add(t.position);
        return buffer;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        foreach (var t in controlPoints) if (t) Gizmos.DrawWireSphere(t.position, 0.3f);
    }
}
