using System.Collections.Generic;
using UnityEngine;

public static class Bezier
{
    public static Vector3 Quadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    public static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    public static Vector3 Recursive(IReadOnlyList<Vector3> points, float t)
    {
        if (points == null || points.Count == 0) return Vector3.zero;
        if (points.Count == 1) return points[0];

        var reduced = new Vector3[points.Count - 1];
        for (int i = 0; i < reduced.Length; i++)
            reduced[i] = Vector3.Lerp(points[i], points[i + 1], t);
        return Recursive(reduced, t);
    }
}
