using System.Collections.Generic;
using Greenkeeper.Pitch;
using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Capsule-vs-grid cell collection (extracted from MowSweep at M.2, now
    /// shared by the mower and the liner). Touch is capsule-vs-cell-RECT, not
    /// cell center: a narrow swath can have a radius smaller than the cell half
    /// diagonal (≈0.354 m at 0.5 m cells), so a center test would miss cells the
    /// capsule clearly crosses. Pure and deterministic.
    /// </summary>
    public static class CapsuleCells
    {
        public static void Collect(
            PitchGrid grid, Vector3 fromWorld, Vector3 toWorld, float radiusM, List<Vector2Int> result)
        {
            result.Clear();

            var from = new Vector2(fromWorld.x, fromWorld.z);
            var to = new Vector2(toWorld.x, toWorld.z);

            float minX = Mathf.Min(from.x, to.x) - radiusM;
            float maxX = Mathf.Max(from.x, to.x) + radiusM;
            float minZ = Mathf.Min(from.y, to.y) - radiusM;
            float maxZ = Mathf.Max(from.y, to.y) + radiusM;

            int cellMinX = Mathf.Max(0, Mathf.FloorToInt((minX - grid.WorldOrigin.x) / grid.CellSize));
            int cellMaxX = Mathf.Min(grid.Width - 1, Mathf.FloorToInt((maxX - grid.WorldOrigin.x) / grid.CellSize));
            int cellMinY = Mathf.Max(0, Mathf.FloorToInt((minZ - grid.WorldOrigin.y) / grid.CellSize));
            int cellMaxY = Mathf.Min(grid.Height - 1, Mathf.FloorToInt((maxZ - grid.WorldOrigin.y) / grid.CellSize));

            float radiusSq = radiusM * radiusM;
            float halfCell = grid.CellSize * 0.5f;
            for (int y = cellMinY; y <= cellMaxY; y++)
            {
                for (int x = cellMinX; x <= cellMaxX; x++)
                {
                    Vector3 center = grid.CellToWorldCenter(x, y);
                    var rectMin = new Vector2(center.x - halfCell, center.z - halfCell);
                    var rectMax = new Vector2(center.x + halfCell, center.z + halfCell);
                    if (DistanceSqSegmentToRect(from, to, rectMin, rectMax) <= radiusSq)
                    {
                        result.Add(new Vector2Int(x, y));
                    }
                }
            }
        }

        private static float DistanceSqToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-8f)
            {
                return (point - a).sqrMagnitude;
            }
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / abLenSq);
            return (point - (a + ab * t)).sqrMagnitude;
        }

        /// <summary>Exact 2D distance² between segment ab and an axis-aligned rect.</summary>
        private static float DistanceSqSegmentToRect(Vector2 a, Vector2 b, Vector2 rectMin, Vector2 rectMax)
        {
            if (PointInRect(a, rectMin, rectMax) || PointInRect(b, rectMin, rectMax))
            {
                return 0f;
            }

            var c0 = rectMin;
            var c1 = new Vector2(rectMax.x, rectMin.y);
            var c2 = rectMax;
            var c3 = new Vector2(rectMin.x, rectMax.y);
            if (SegmentsIntersect(a, b, c0, c1) || SegmentsIntersect(a, b, c1, c2) ||
                SegmentsIntersect(a, b, c2, c3) || SegmentsIntersect(a, b, c3, c0))
            {
                return 0f;
            }

            float best = Mathf.Min(
                Mathf.Min(DistanceSqToSegment(c0, a, b), DistanceSqToSegment(c1, a, b)),
                Mathf.Min(DistanceSqToSegment(c2, a, b), DistanceSqToSegment(c3, a, b)));
            best = Mathf.Min(best, PointToRectDistanceSq(a, rectMin, rectMax));
            best = Mathf.Min(best, PointToRectDistanceSq(b, rectMin, rectMax));
            return best;
        }

        private static bool PointInRect(Vector2 p, Vector2 rectMin, Vector2 rectMax)
        {
            return p.x >= rectMin.x && p.x <= rectMax.x && p.y >= rectMin.y && p.y <= rectMax.y;
        }

        private static float PointToRectDistanceSq(Vector2 p, Vector2 rectMin, Vector2 rectMax)
        {
            float dx = Mathf.Max(rectMin.x - p.x, 0f, p.x - rectMax.x);
            float dy = Mathf.Max(rectMin.y - p.y, 0f, p.y - rectMax.y);
            return dx * dx + dy * dy;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c);
            float d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a);
            float d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) &&
                   ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }
    }
}
