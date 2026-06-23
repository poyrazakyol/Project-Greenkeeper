using System.Linq;
using Greenkeeper.Pitch;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// FIFA pitch line geometry (GDD §1.5): parametric, centered on origin,
    /// deterministic. The ideal the line score compares against.
    /// </summary>
    public class LineGeometryTests
    {
        private const float PitchLength = 105f;
        private const float PitchWidth = 68f;

        private static IdealLine[] Generate(float spacing = 0.25f)
        {
            return LineGeometry.Generate(PitchLength, PitchWidth, spacing);
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            IdealLine[] a = Generate();
            IdealLine[] b = Generate();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (int i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].Polyline.Length, Is.EqualTo(b[i].Polyline.Length));
                Assert.That(a[i].Polyline[0], Is.EqualTo(b[i].Polyline[0]));
            }
        }

        [Test]
        public void Generate_HasExpectedElementCounts()
        {
            IdealLine[] lines = Generate();
            Assert.That(lines.Count(l => l.Type == LineElementType.Touchline), Is.EqualTo(2));
            Assert.That(lines.Count(l => l.Type == LineElementType.GoalLine), Is.EqualTo(2));
            Assert.That(lines.Count(l => l.Type == LineElementType.HalfwayLine), Is.EqualTo(1));
            Assert.That(lines.Count(l => l.Type == LineElementType.CenterCircle), Is.EqualTo(1));
            Assert.That(lines.Count(l => l.Type == LineElementType.PenaltyBox), Is.EqualTo(2));
            Assert.That(lines.Count(l => l.Type == LineElementType.GoalBox), Is.EqualTo(2));
            Assert.That(lines.Count(l => l.Type == LineElementType.CornerArc), Is.EqualTo(4));
            Assert.That(lines.Count(l => l.Type == LineElementType.PenaltyArc), Is.EqualTo(2));
        }

        [Test]
        public void CenterCircle_HasRadius9_15()
        {
            IdealLine circle = Generate().First(l => l.Type == LineElementType.CenterCircle);
            foreach (Vector2 p in circle.Polyline)
            {
                Assert.That(p.magnitude, Is.EqualTo(9.15f).Within(0.001f));
            }
        }

        [Test]
        public void Touchlines_RunFullLengthAtPitchEdge()
        {
            IdealLine[] touch = Generate().Where(l => l.Type == LineElementType.Touchline).ToArray();
            foreach (IdealLine line in touch)
            {
                // Constant |Z| = 34, X spans [-52.5, 52.5].
                Assert.That(Mathf.Abs(line.Polyline[0].y), Is.EqualTo(34f).Within(0.001f));
                float minX = line.Polyline.Min(p => p.x);
                float maxX = line.Polyline.Max(p => p.x);
                Assert.That(minX, Is.EqualTo(-52.5f).Within(0.001f));
                Assert.That(maxX, Is.EqualTo(52.5f).Within(0.001f));
            }
        }

        [Test]
        public void PenaltyBox_ReachesCorrectDepthAndWidth()
        {
            // +X end penalty box: inner line at X = 52.5 - 16.5 = 36, half width 20.16.
            IdealLine box = Generate().First(l =>
                l.Type == LineElementType.PenaltyBox && l.Polyline.Any(p => p.x > 0f));
            float minX = box.Polyline.Min(p => p.x);
            float maxZ = box.Polyline.Max(p => p.y);
            Assert.That(minX, Is.EqualTo(36f).Within(0.001f));
            Assert.That(maxZ, Is.EqualTo(20.16f).Within(0.001f));
        }

        [Test]
        public void CornerArc_HasRadius1AtCorner()
        {
            IdealLine corner = Generate().First(l => l.Type == LineElementType.CornerArc);
            // Pick the nearest pitch corner and verify all points are r≈1 from it.
            var corners = new[]
            {
                new Vector2(-52.5f, -34f), new Vector2(-52.5f, 34f),
                new Vector2(52.5f, -34f), new Vector2(52.5f, 34f)
            };
            Vector2 nearest = corners.OrderBy(c => (c - corner.Polyline[0]).sqrMagnitude).First();
            foreach (Vector2 p in corner.Polyline)
            {
                Assert.That((p - nearest).magnitude, Is.EqualTo(1f).Within(0.001f));
            }
        }

        [Test]
        public void PenaltyArc_OnlyExistsOutsideTheBox()
        {
            // +X end arc: all points must be outside the box (x < 36).
            IdealLine arc = Generate().First(l =>
                l.Type == LineElementType.PenaltyArc && l.Polyline.Any(p => p.x > 0f));
            foreach (Vector2 p in arc.Polyline)
            {
                Assert.That(p.x, Is.LessThan(36f + 0.001f));
                // And on the circle of radius 9.15 around the spot at X=41.5.
                Assert.That((p - new Vector2(41.5f, 0f)).magnitude, Is.EqualTo(9.15f).Within(0.05f));
            }
        }

        [Test]
        public void Generate_FinerSpacing_ProducesMorePoints()
        {
            int coarse = Generate(0.5f).First(l => l.Type == LineElementType.CenterCircle).Polyline.Length;
            int fine = Generate(0.1f).First(l => l.Type == LineElementType.CenterCircle).Polyline.Length;
            Assert.That(fine, Is.GreaterThan(coarse));
        }
    }
}
