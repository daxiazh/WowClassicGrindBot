using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharedLib.Extensions;

public static class VectorExt
{
    public static Vector3[] FromList(List<List<float>> points)
    {
        Vector3[] output = new Vector3[points.Count];
        for (int i = 0; i < output.Length; i++)
        {
            output[i] = new(points[i][0], points[i][1], 0);
        }
        return output;
    }

    public static float MapDistanceXYTo(this Vector3 l1, in Vector3 l2)
    {
        return MapDistanceXY(l1, l2);
    }

    public static float MapDistanceXY(Vector3 l1, Vector3 l2)
    {
        return Vector2.Distance(l1.AsVector2() * 100, l2.AsVector2() * 100); // would be nice to remove that 100 multiplier :sweat:
    }

    public static float WorldDistanceXYTo(this Vector3 l1, in Vector3 l2)
    {
        return WorldDistanceXY(l1, l2);
    }

    public static float WorldDistanceXY(Vector3 l1, Vector3 l2)
    {
        return Vector2.Distance(l1.AsVector2(), l2.AsVector2());
    }

    public static List<Vector3> ShortenRouteFromLocation(Vector3 location, List<Vector3> pointsList)
    {
        var result = new List<Vector3>();

        var closestDistance = pointsList.Select(p => (point: p, distance: MapDistanceXYTo(location, p)))
            .OrderBy(s => s.distance);

        var closestPoint = closestDistance.First();

        var startPoint = 0;
        for (int i = 0; i < pointsList.Count; i++)
        {
            if (pointsList[i] == closestPoint.point)
            {
                startPoint = i;
                break;
            }
        }

        for (int i = startPoint; i < pointsList.Count; i++)
        {
            result.Add(pointsList[i]);
        }

        return result;
    }

    public static Vector2 GetClosestPointOnLineSegment(in Vector2 A, in Vector2 B, in Vector2 P)
    {
        Vector2 AP = P - A;       //Vector from A to P
        Vector2 AB = B - A;       //Vector from A to B

        float magnitudeAB = AB.LengthSquared();     //Magnitude of AB vector (it's length squared)
        float ABAPproduct = Vector2.Dot(AP, AB);    //The DOT product of a_to_p and a_to_b
        float distance = ABAPproduct / magnitudeAB; //The normalized "distance" from a to your closest point

        return distance < 0
            ? A
            : distance > 1
            ? B
            : A + (AB * distance);
    }

    public static float TotalDistance<T>(ReadOnlySpan<T> points, Func<T, T, float> accumulator)
    {
        if (points.Length <= 1)
            return 0f;

        float totalDistance = 0f;
        for (int i = 1; i < points.Length; i++)
        {
            totalDistance += accumulator(points[i - 1], points[i]);
        }
        return totalDistance;
    }

    public static void Deconstruct(this Vector3 v3, out float x, out float y, out float z)
    {
        x = v3.X;
        y = v3.Y;
        z = v3.Z;
    }

    public static void Deconstruct(this Vector3 v2, out float x, out float y)
    {
        x = v2.X;
        y = v2.Y;
    }

    public static string ToStringF(this Vector3 v)
    {
        return $"({v.X} {v.Y} {v.Z})";
    }

    /// <summary>
    /// 在两个路径点之间生成自适应的之字形(Zigzag)路径
    /// 根据两点之间的距离动态调整之字形的密度
    /// </summary>
    /// <param name="start">起点(地图坐标)</param>
    /// <param name="end">终点(地图坐标)</param>
    /// <param name="amplitude">摆动幅度(地图坐标单位,默认 0.04 约等于 4 码)</param>
    /// <returns>包含起点、之字形中间点和终点的路径数组</returns>
    public static Vector3[] GenerateZigzagPath(Vector3 start, Vector3 end, float amplitude = 0.04f)
    {
        float distance = MapDistanceXY(start, end);

        // 距离小于 10 码,不生成之字形,直接返回起点和终点
        if (distance < 10f)
            return new[] { start, end };

        // 计算之字形摆动点的数量
        // 规则: 每 18 码生成一个摆动点
        const float zigzagInterval = 18f;
        int zigzagCount = (int)(distance / zigzagInterval);

        // 限制摆动点数量在 1-10 之间
        zigzagCount = Math.Max(1, Math.Min(zigzagCount, 10));

        // 计算方向向量和垂直向量
        Vector2 start2D = start.AsVector2();
        Vector2 end2D = end.AsVector2();
        Vector2 direction = Vector2.Normalize(end2D - start2D);
        Vector2 perpendicular = new(-direction.Y, direction.X); // 逆时针旋转 90 度

        // 生成之字形路径点
        List<Vector3> zigzagPoints = new(zigzagCount + 2)
        {
            start // 添加起点
        };

        // 生成中间摆动点
        for (int i = 1; i <= zigzagCount; i++)
        {
            // 计算当前点在起点到终点线段上的位置 (0-1)
            float t = i / (float)(zigzagCount + 1);

            // 线性插值得到基准点
            Vector2 basePoint = Vector2.Lerp(start2D, end2D, t);

            // 左右交替偏移
            // i 为奇数时向左,偶数时向右
            float offset = (i % 2 == 1) ? amplitude : -amplitude;
            Vector2 zigzagPoint = basePoint + perpendicular * offset;

            // Z 坐标也进行插值
            float z = start.Z + (end.Z - start.Z) * t;

            zigzagPoints.Add(new Vector3(zigzagPoint.X, zigzagPoint.Y, z));
        }

        zigzagPoints.Add(end); // 添加终点

        return zigzagPoints.ToArray();
    }

    /// <summary>
    /// 对整个路径应用之字形生成,在每两个相邻路径点之间插入之字形中间点
    /// </summary>
    /// <param name="originalPath">原始路径点数组</param>
    /// <param name="amplitude">摆动幅度(地图坐标单位)</param>
    /// <returns>包含之字形中间点的完整路径</returns>
    public static Vector3[] ApplyZigzagToPath(ReadOnlySpan<Vector3> originalPath, float amplitude = 0.04f)
    {
        if (originalPath.Length <= 1)
            return originalPath.ToArray();

        List<Vector3> fullPath = new();

        for (int i = 0; i < originalPath.Length - 1; i++)
        {
            Vector3[] segmentPath = GenerateZigzagPath(originalPath[i], originalPath[i + 1], amplitude);

            // 添加当前线段的所有点(除了最后一个,因为它会是下一个线段的起点)
            for (int j = 0; j < segmentPath.Length - 1; j++)
            {
                fullPath.Add(segmentPath[j]);
            }
        }

        // 添加最后一个终点
        fullPath.Add(originalPath[^1]);

        return fullPath.ToArray();
    }
}
