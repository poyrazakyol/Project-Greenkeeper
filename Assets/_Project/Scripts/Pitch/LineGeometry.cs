using System.Collections.Generic;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    public enum LineElementType
    {
        Touchline,
        GoalLine,
        HalfwayLine,
        CenterCircle,
        PenaltyBox,
        GoalBox,
        CornerArc,
        PenaltyArc
    }

    /// <summary>
    /// One ideal pitch line as a polyline. Straight lines and arcs share this
    /// representation (arcs are tessellated by the generator) so the smoothness
    /// scorer needs only point→polyline math, no arc-specific cases.
    /// </summary>
    public readonly struct IdealLine
    {
        public LineElementType Type { get; }
        public Vector2[] Polyline { get; }

        public IdealLine(LineElementType type, Vector2[] polyline)
        {
            Type = type;
            Polyline = polyline;
        }
    }

    /// <summary>
    /// Generates the FIFA pitch line geometry (GDD §1.5) parametrically from
    /// the pitch dimensions. Pure and deterministic — the ideal that the line
    /// smoothness/completeness score compares against (TDD §4.3, §6.2). The
    /// pitch is centered on the world origin (X = length, Z = width).
    /// Penalty/center marks are points, scored as a separate "present" item by
    /// the inspector (M.4), so they are not emitted here.
    /// </summary>
    public static class LineGeometry
    {
        // FIFA reference measures (GDD §1.5), in meters.
        private const float PenaltyBoxDepthM = 16.5f;
        private const float PenaltyBoxHalfWidthM = 20.16f;  // 40.32 / 2
        private const float GoalBoxDepthM = 5.5f;
        private const float GoalBoxHalfWidthM = 9.16f;      // 18.32 / 2
        private const float CenterCircleRadiusM = 9.15f;
        private const float PenaltySpotDistanceM = 11f;
        private const float PenaltyArcRadiusM = 9.15f;
        private const float CornerArcRadiusM = 1f;

        public static IdealLine[] Generate(float pitchLengthM, float pitchWidthM, float tessellationSpacingM)
        {
            float halfL = pitchLengthM * 0.5f;
            float halfW = pitchWidthM * 0.5f;
            float spacing = Mathf.Max(tessellationSpacingM, 0.05f);

            var lines = new List<IdealLine>();

            // Boundary: two touchlines (along X at Z = ±halfW), two goal lines.
            lines.Add(new IdealLine(LineElementType.Touchline, Segment(new Vector2(-halfL, halfW), new Vector2(halfL, halfW), spacing)));
            lines.Add(new IdealLine(LineElementType.Touchline, Segment(new Vector2(-halfL, -halfW), new Vector2(halfL, -halfW), spacing)));
            lines.Add(new IdealLine(LineElementType.GoalLine, Segment(new Vector2(-halfL, -halfW), new Vector2(-halfL, halfW), spacing)));
            lines.Add(new IdealLine(LineElementType.GoalLine, Segment(new Vector2(halfL, -halfW), new Vector2(halfL, halfW), spacing)));

            // Halfway line + center circle.
            lines.Add(new IdealLine(LineElementType.HalfwayLine, Segment(new Vector2(0f, -halfW), new Vector2(0f, halfW), spacing)));
            lines.Add(new IdealLine(LineElementType.CenterCircle, Arc(Vector2.zero, CenterCircleRadiusM, 0f, 360f, spacing)));

            // Both ends: penalty box, goal box, penalty arc.
            AddEnd(lines, +1f, halfL, spacing);
            AddEnd(lines, -1f, halfL, spacing);

            // Corner arcs (quarter circles), one per corner.
            AddCornerArc(lines, new Vector2(-halfL, -halfW), 0f, spacing);
            AddCornerArc(lines, new Vector2(-halfL, halfW), 270f, spacing);
            AddCornerArc(lines, new Vector2(halfL, -halfW), 90f, spacing);
            AddCornerArc(lines, new Vector2(halfL, halfW), 180f, spacing);

            return lines.ToArray();
        }

        private static void AddEnd(List<IdealLine> lines, float sign, float halfL, float spacing)
        {
            float goalLineX = sign * halfL;
            float boxInnerX = sign * (halfL - PenaltyBoxDepthM);
            float goalInnerX = sign * (halfL - GoalBoxDepthM);

            // Penalty box: three segments (the goal line closes the fourth side).
            lines.Add(new IdealLine(LineElementType.PenaltyBox, BoxThreeSides(
                goalLineX, boxInnerX, PenaltyBoxHalfWidthM, spacing)));
            // Goal box.
            lines.Add(new IdealLine(LineElementType.GoalBox, BoxThreeSides(
                goalLineX, goalInnerX, GoalBoxHalfWidthM, spacing)));

            // Penalty arc (the "D"): full circle around the spot, kept only
            // outside the penalty box (|x| < |boxInnerX|).
            float spotX = sign * (halfL - PenaltySpotDistanceM);
            Vector2[] full = Arc(new Vector2(spotX, 0f), PenaltyArcRadiusM, 0f, 360f, spacing);
            var outside = new List<Vector2>();
            foreach (Vector2 p in full)
            {
                if ((sign > 0f && p.x < boxInnerX) || (sign < 0f && p.x > boxInnerX))
                {
                    outside.Add(p);
                }
            }
            if (outside.Count >= 2)
            {
                lines.Add(new IdealLine(LineElementType.PenaltyArc, outside.ToArray()));
            }
        }

        private static void AddCornerArc(List<IdealLine> lines, Vector2 corner, float startAngleDeg, float spacing)
        {
            lines.Add(new IdealLine(LineElementType.CornerArc, Arc(corner, CornerArcRadiusM, startAngleDeg, startAngleDeg + 90f, spacing)));
        }

        /// <summary>Three sides of an end box; the goal-line side is omitted.</summary>
        private static Vector2[] BoxThreeSides(float goalLineX, float innerX, float halfWidth, float spacing)
        {
            var a = new Vector2(goalLineX, -halfWidth);
            var b = new Vector2(innerX, -halfWidth);
            var c = new Vector2(innerX, halfWidth);
            var d = new Vector2(goalLineX, halfWidth);

            var points = new List<Vector2>();
            AppendSegment(points, a, b, spacing, includeStart: true);
            AppendSegment(points, b, c, spacing, includeStart: false);
            AppendSegment(points, c, d, spacing, includeStart: false);
            return points.ToArray();
        }

        private static Vector2[] Segment(Vector2 a, Vector2 b, float spacing)
        {
            var points = new List<Vector2>();
            AppendSegment(points, a, b, spacing, includeStart: true);
            return points.ToArray();
        }

        private static void AppendSegment(List<Vector2> points, Vector2 a, Vector2 b, float spacing, bool includeStart)
        {
            float length = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / spacing));
            for (int i = includeStart ? 0 : 1; i <= steps; i++)
            {
                points.Add(Vector2.Lerp(a, b, i / (float)steps));
            }
        }

        private static Vector2[] Arc(Vector2 center, float radius, float startDeg, float endDeg, float spacing)
        {
            float arcLength = Mathf.Abs(endDeg - startDeg) * Mathf.Deg2Rad * radius;
            int steps = Mathf.Max(1, Mathf.CeilToInt(arcLength / spacing));
            var points = new Vector2[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float deg = Mathf.Lerp(startDeg, endDeg, i / (float)steps);
                float rad = deg * Mathf.Deg2Rad;
                points[i] = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            }
            return points;
        }
    }
}
