using System.Collections.Generic;
using System.Linq;
using Greenkeeper.Pitch;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Line smoothness + completeness scoring (TDD §4.3/§6.2, T4 risk): pure
    /// vector math, deterministic, edge cases pinned (backwards/crossing/
    /// partial/stray strokes). No pixel readback.
    /// </summary>
    public class LineSmoothnessScorerTests
    {
        private static LineScoreParams DefaultParams() => new LineScoreParams
        {
            MaxAssociationM = 1.0f,
            CoverageThresholdM = 0.15f,
            CoverageSampleSpacingM = 0.25f
        };

        // A single horizontal ideal segment from (0,0) to (10,0), tessellated.
        private static IdealLine[] HorizontalSegment()
        {
            var pts = new List<Vector2>();
            for (float x = 0f; x <= 10f + 1e-4f; x += 0.25f)
            {
                pts.Add(new Vector2(x, 0f));
            }
            return new[] { new IdealLine(LineElementType.HalfwayLine, pts.ToArray()) };
        }

        private static LineStroke Stroke(params Vector2[] points)
        {
            var s = new LineStroke();
            s.Points.AddRange(points);
            return s;
        }

        private static LineStroke DenseStroke(float fromX, float toX, float z, float step = 0.1f)
        {
            var s = new LineStroke();
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toX - fromX) / step));
            for (int i = 0; i <= steps; i++)
            {
                float x = Mathf.Lerp(fromX, toX, i / (float)steps);
                s.Points.Add(new Vector2(x, z));
            }
            return s;
        }

        [Test]
        public void StraightStrokeOnSegment_NearZeroDeviation_FullCoverage()
        {
            IdealLine[] ideal = HorizontalSegment();
            var strokes = new[] { DenseStroke(0f, 10f, 0f) };

            LineScoreReport report = LineSmoothnessScorer.Score(strokes, ideal, DefaultParams());

            Assert.That(report.Elements[0].AvgDeviationCm, Is.LessThan(0.5f));
            Assert.That(report.Elements[0].CoverageRatio, Is.EqualTo(1f).Within(0.001f));
            Assert.That(report.StrayPointCount, Is.EqualTo(0));
        }

        [Test]
        public void WavyStroke_HasHigherDeviationThanStraight()
        {
            IdealLine[] ideal = HorizontalSegment();

            var straight = new[] { DenseStroke(0f, 10f, 0f) };
            var wavy = new LineStroke();
            for (int i = 0; i <= 100; i++)
            {
                float x = i / 10f;
                wavy.Points.Add(new Vector2(x, 0.2f * Mathf.Sin(x * 3f))); // ±20 cm wobble
            }

            float straightDev = LineSmoothnessScorer.Score(straight, ideal, DefaultParams()).Elements[0].AvgDeviationCm;
            float wavyDev = LineSmoothnessScorer.Score(new[] { wavy }, ideal, DefaultParams()).Elements[0].AvgDeviationCm;

            Assert.That(wavyDev, Is.GreaterThan(straightDev + 5f));
            Assert.That(LineSmoothnessScorer.Score(new[] { wavy }, ideal, DefaultParams()).Elements[0].MaxDeviationCm,
                Is.GreaterThan(15f));
        }

        [Test]
        public void BackwardsStroke_ScoresSameAsForwards()
        {
            IdealLine[] ideal = HorizontalSegment();
            var forwards = new[] { DenseStroke(0f, 10f, 0f) };
            var backwards = new[] { DenseStroke(10f, 0f, 0f) };

            LineScoreReport f = LineSmoothnessScorer.Score(forwards, ideal, DefaultParams());
            LineScoreReport b = LineSmoothnessScorer.Score(backwards, ideal, DefaultParams());

            Assert.That(b.Elements[0].CoverageRatio, Is.EqualTo(f.Elements[0].CoverageRatio).Within(0.001f));
            Assert.That(b.Elements[0].AvgDeviationCm, Is.EqualTo(f.Elements[0].AvgDeviationCm).Within(0.001f));
        }

        [Test]
        public void CrossingStrokes_EachPointAssignedToNearestElement()
        {
            // Two perpendicular ideal segments forming a plus through origin.
            var horizontal = new List<Vector2>();
            var vertical = new List<Vector2>();
            for (float t = -5f; t <= 5f + 1e-4f; t += 0.25f)
            {
                horizontal.Add(new Vector2(t, 0f));
                vertical.Add(new Vector2(0f, t));
            }
            var ideal = new[]
            {
                new IdealLine(LineElementType.HalfwayLine, horizontal.ToArray()),
                new IdealLine(LineElementType.HalfwayLine, vertical.ToArray())
            };

            // One stroke runs along horizontal, another along vertical.
            var strokes = new[] { DenseStroke(-5f, 5f, 0f), VerticalStroke(-5f, 5f, 0f) };

            LineScoreReport report = LineSmoothnessScorer.Score(strokes, ideal, DefaultParams());

            Assert.That(report.Elements[0].CoverageRatio, Is.EqualTo(1f).Within(0.02f));
            Assert.That(report.Elements[1].CoverageRatio, Is.EqualTo(1f).Within(0.02f));
            Assert.That(report.StrayPointCount, Is.EqualTo(0));
        }

        [Test]
        public void PartialStroke_LowersCoverage_NotDeviation()
        {
            IdealLine[] ideal = HorizontalSegment(); // 0..10
            var halfPainted = new[] { DenseStroke(0f, 5f, 0f) }; // only first half

            LineScoreReport report = LineSmoothnessScorer.Score(halfPainted, ideal, DefaultParams());

            Assert.That(report.Elements[0].CoverageRatio, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(report.Elements[0].AvgDeviationCm, Is.LessThan(0.5f));
        }

        [Test]
        public void StrayPaint_CountedSeparately_DoesNotPolluteElement()
        {
            IdealLine[] ideal = HorizontalSegment();
            // A stroke 5 m away from the only element (> maxAssociation 1 m).
            var stray = new[] { DenseStroke(0f, 10f, 5f) };

            LineScoreReport report = LineSmoothnessScorer.Score(stray, ideal, DefaultParams());

            Assert.That(report.StrayPointCount, Is.GreaterThan(0));
            Assert.That(report.Elements[0].AssignedPointCount, Is.EqualTo(0));
            Assert.That(report.Elements[0].AvgDeviationCm, Is.EqualTo(0f));
            Assert.That(report.Elements[0].CoverageRatio, Is.EqualTo(0f));
        }

        [Test]
        public void ArcElement_StraightChordStroke_DeviatesFromArc()
        {
            // Quarter arc radius 5 centered at origin, 0..90°.
            var arcPts = new List<Vector2>();
            for (int i = 0; i <= 40; i++)
            {
                float deg = i / 40f * 90f;
                float rad = deg * Mathf.Deg2Rad;
                arcPts.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 5f);
            }
            var ideal = new[] { new IdealLine(LineElementType.CenterCircle, arcPts.ToArray()) };

            // A stroke that follows the arc -> low deviation.
            var onArc = new LineStroke();
            for (int i = 0; i <= 90; i++)
            {
                float rad = i * Mathf.Deg2Rad;
                onArc.Points.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * 5f);
            }
            float onArcDev = LineSmoothnessScorer.Score(new[] { onArc }, ideal, DefaultParams()).Elements[0].AvgDeviationCm;
            Assert.That(onArcDev, Is.LessThan(2f));
        }

        [Test]
        public void EmptyStrokes_DoNotThrow_ZeroCoverage()
        {
            IdealLine[] ideal = HorizontalSegment();
            LineScoreReport report = LineSmoothnessScorer.Score(new LineStroke[0], ideal, DefaultParams());

            Assert.That(report.OverallCoverage, Is.EqualTo(0f));
            Assert.That(report.StrayPointCount, Is.EqualTo(0));
            Assert.That(report.Elements[0].CoverageRatio, Is.EqualTo(0f));
        }

        [Test]
        public void EquidistantPoint_BreaksTieToLowestElementIndex()
        {
            // Two identical-distance segments; a point exactly between them.
            var a = new[] { new Vector2(-1f, 1f), new Vector2(1f, 1f) };   // z = +1
            var b = new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f) }; // z = -1
            var ideal = new[]
            {
                new IdealLine(LineElementType.Touchline, a),
                new IdealLine(LineElementType.Touchline, b)
            };
            var strokes = new[] { Stroke(new Vector2(0f, 0f)) }; // equidistant (1 m each)

            LineScoreReport report = LineSmoothnessScorer.Score(strokes, ideal, DefaultParams());

            Assert.That(report.Elements[0].AssignedPointCount, Is.EqualTo(1));
            Assert.That(report.Elements[1].AssignedPointCount, Is.EqualTo(0));
        }

        private static LineStroke VerticalStroke(float fromZ, float toZ, float x, float step = 0.1f)
        {
            var s = new LineStroke();
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(toZ - fromZ) / step));
            for (int i = 0; i <= steps; i++)
            {
                s.Points.Add(new Vector2(x, Mathf.Lerp(fromZ, toZ, i / (float)steps)));
            }
            return s;
        }
    }
}
