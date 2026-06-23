using System.Collections.Generic;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>One painted line pass: the recorded cart polyline (world XZ).</summary>
    public sealed class LineStroke
    {
        public List<Vector2> Points { get; } = new List<Vector2>();
    }

    public struct LineScoreParams
    {
        /// <summary>A stroke point farther than this from every element is "stray".</summary>
        public float MaxAssociationM;
        /// <summary>An ideal sample is "covered" if a stroke point is within this.</summary>
        public float CoverageThresholdM;
        /// <summary>Spacing for resampling ideal polylines into coverage samples.</summary>
        public float CoverageSampleSpacingM;
    }

    public struct LineElementScore
    {
        public LineElementType Type;
        public int ElementIndex;
        public float AvgDeviationCm;
        public float MaxDeviationCm;
        public float CoverageRatio;
        public int AssignedPointCount;
    }

    public struct LineScoreReport
    {
        public LineElementScore[] Elements;
        public int StrayPointCount;
        public float OverallCoverage;
        public float OverallAvgDeviationCm;
    }

    /// <summary>
    /// Pure line smoothness + completeness scoring (TDD §4.3, §6.2 — the
    /// inspector's strictest item, T4 risk). Vector math only, no pixel
    /// readback (RT_Paint stays visual-only). Each stroke POINT is assigned to
    /// its nearest ideal element (robust to backwards/crossing strokes); points
    /// beyond MaxAssociationM are stray and never pollute an element's score.
    /// Deterministic: ties break to the lowest element index.
    /// </summary>
    public static class LineSmoothnessScorer
    {
        public static LineScoreReport Score(
            IReadOnlyList<LineStroke> strokes, IReadOnlyList<IdealLine> ideal, in LineScoreParams p)
        {
            int elementCount = ideal.Count;
            var sumDeviationCm = new double[elementCount];
            var maxDeviationCm = new float[elementCount];
            var assignedCount = new int[elementCount];
            int strayCount = 0;

            // --- Deviation: assign each stroke point to its nearest element ---
            for (int s = 0; s < strokes.Count; s++)
            {
                List<Vector2> pts = strokes[s].Points;
                for (int i = 0; i < pts.Count; i++)
                {
                    int nearest = NearestElement(pts[i], ideal, out float distM);
                    if (nearest < 0 || distM > p.MaxAssociationM)
                    {
                        strayCount++;
                        continue;
                    }
                    float distCm = distM * 100f;
                    sumDeviationCm[nearest] += distCm;
                    if (distCm > maxDeviationCm[nearest])
                    {
                        maxDeviationCm[nearest] = distCm;
                    }
                    assignedCount[nearest]++;
                }
            }

            // --- Coverage: fraction of each element's samples near a stroke ---
            var elements = new LineElementScore[elementCount];
            int totalSamples = 0;
            int totalCovered = 0;
            double overallDeviationSum = 0.0;
            int overallAssigned = 0;

            float thresholdSq = p.CoverageThresholdM * p.CoverageThresholdM;
            for (int e = 0; e < elementCount; e++)
            {
                Vector2[] samples = Resample(ideal[e].Polyline, p.CoverageSampleSpacingM);
                int covered = 0;
                for (int k = 0; k < samples.Length; k++)
                {
                    if (AnyStrokePointWithin(samples[k], strokes, thresholdSq))
                    {
                        covered++;
                    }
                }

                elements[e] = new LineElementScore
                {
                    Type = ideal[e].Type,
                    ElementIndex = e,
                    AvgDeviationCm = assignedCount[e] > 0 ? (float)(sumDeviationCm[e] / assignedCount[e]) : 0f,
                    MaxDeviationCm = maxDeviationCm[e],
                    CoverageRatio = samples.Length > 0 ? covered / (float)samples.Length : 0f,
                    AssignedPointCount = assignedCount[e]
                };

                totalSamples += samples.Length;
                totalCovered += covered;
                overallDeviationSum += sumDeviationCm[e];
                overallAssigned += assignedCount[e];
            }

            return new LineScoreReport
            {
                Elements = elements,
                StrayPointCount = strayCount,
                OverallCoverage = totalSamples > 0 ? totalCovered / (float)totalSamples : 0f,
                OverallAvgDeviationCm = overallAssigned > 0 ? (float)(overallDeviationSum / overallAssigned) : 0f
            };
        }

        private static int NearestElement(Vector2 point, IReadOnlyList<IdealLine> ideal, out float distM)
        {
            int best = -1;
            float bestSq = float.MaxValue;
            for (int e = 0; e < ideal.Count; e++)
            {
                float sq = DistanceSqToPolyline(point, ideal[e].Polyline);
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }
            distM = best < 0 ? float.MaxValue : Mathf.Sqrt(bestSq);
            return best;
        }

        private static bool AnyStrokePointWithin(Vector2 sample, IReadOnlyList<LineStroke> strokes, float thresholdSq)
        {
            for (int s = 0; s < strokes.Count; s++)
            {
                List<Vector2> pts = strokes[s].Points;
                for (int i = 0; i < pts.Count; i++)
                {
                    if ((pts[i] - sample).sqrMagnitude <= thresholdSq)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float DistanceSqToPolyline(Vector2 point, Vector2[] polyline)
        {
            if (polyline.Length == 0)
            {
                return float.MaxValue;
            }
            if (polyline.Length == 1)
            {
                return (point - polyline[0]).sqrMagnitude;
            }
            float best = float.MaxValue;
            for (int i = 0; i < polyline.Length - 1; i++)
            {
                float sq = DistanceSqToSegment(point, polyline[i], polyline[i + 1]);
                if (sq < best)
                {
                    best = sq;
                }
            }
            return best;
        }

        private static float DistanceSqToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-8f)
            {
                return (p - a).sqrMagnitude;
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
            return (p - (a + ab * t)).sqrMagnitude;
        }

        /// <summary>Resamples a polyline to ~uniform spacing for coverage testing.</summary>
        private static Vector2[] Resample(Vector2[] polyline, float spacingM)
        {
            if (polyline.Length <= 1)
            {
                return polyline;
            }
            float spacing = Mathf.Max(spacingM, 0.05f);
            var samples = new List<Vector2> { polyline[0] };
            float carry = 0f;
            for (int i = 0; i < polyline.Length - 1; i++)
            {
                Vector2 a = polyline[i];
                Vector2 b = polyline[i + 1];
                float segLen = Vector2.Distance(a, b);
                float pos = spacing - carry;
                while (pos < segLen)
                {
                    samples.Add(Vector2.Lerp(a, b, pos / segLen));
                    pos += spacing;
                }
                carry = segLen - (pos - spacing);
            }
            return samples.ToArray();
        }
    }
}
