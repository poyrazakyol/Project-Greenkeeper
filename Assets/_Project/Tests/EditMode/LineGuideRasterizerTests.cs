using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Ghost guide bake (M.3.5): the ideal line polylines rasterized into an
    /// offset field (vector to the nearest line point). Pure — no GPU. Guards
    /// the distance accuracy, the thin-line reconstruction between texels,
    /// the pitch-edge margin, symmetry, the row/column orientation (row 0 =
    /// v 0) and the "painted line covers the guide" width contract.
    /// </summary>
    public class LineGuideRasterizerTests
    {
        // Default stadium: 105x68 m pitch inside a 106x70 m grid area.
        private const float PitchLength = 105f;
        private const float PitchWidth = 68f;
        private const int Width = 1060;   // 0.1 m texels — coarser than the 3 cm guide on purpose
        private const int Height = 700;
        private const float Range = 0.25f;
        // 8-bit offsets over ±Range: one step ≈ 2 mm per axis.
        private const float QuantizationM = 0.003f;

        private static readonly PitchBounds Bounds =
            new PitchBounds(new Vector2(-53f, -35f), new Vector2(106f, 70f));

        private static byte[] BakePitch()
        {
            IdealLine[] lines = LineGeometry.Generate(PitchLength, PitchWidth, 0.25f);
            return LineGuideRasterizer.RasterizeOffsetField(lines, Bounds, Width, Height, Range);
        }

        private static Vector2 Offset(byte[] field, int x, int y, int width = Width)
        {
            int index = (y * width + x) * 2;
            return LineGuideRasterizer.DecodeOffset(field[index], field[index + 1], Range);
        }

        private static Vector2Int Texel(float worldX, float worldZ)
        {
            Vector2 uv = Bounds.WorldToUv(new Vector3(worldX, 0f, worldZ));
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(uv.x * Width), 0, Width - 1),
                Mathf.Clamp(Mathf.FloorToInt(uv.y * Height), 0, Height - 1));
        }

        private static Vector2 TexelCenter(Vector2Int texel)
        {
            return new Vector2(
                Bounds.Origin.x + (texel.x + 0.5f) * Bounds.Size.x / Width,
                Bounds.Origin.y + (texel.y + 0.5f) * Bounds.Size.y / Height);
        }

        [Test]
        public void Rasterize_ReturnsTwoBytesPerTexel()
        {
            Assert.That(BakePitch().Length, Is.EqualTo(Width * Height * 2));
        }

        [Test]
        public void HalfwayLine_OffsetPointsAtTheLineWithTrueDistance()
        {
            // Halfway line runs along Z at X = 0; z = 20 is clear of the
            // center circle (r = 9.15).
            byte[] field = BakePitch();
            Vector2Int texel = Texel(0.12f, 20f);
            Vector2 center = TexelCenter(texel);
            Vector2 offset = Offset(field, texel.x, texel.y);

            Assert.That(offset.x, Is.EqualTo(-center.x).Within(QuantizationM), "offset must reach X = 0");
            Assert.That(offset.y, Is.EqualTo(0f).Within(QuantizationM), "no offset along the line");
        }

        [Test]
        public void OpenGrass_FarFromEveryLine_IsBeyondRange()
        {
            byte[] field = BakePitch();
            foreach (Vector2 p in new[] { new Vector2(25f, 28f), new Vector2(-30f, -30f), new Vector2(4f, 0f) })
            {
                Vector2Int texel = Texel(p.x, p.y);
                Assert.That(Offset(field, texel.x, texel.y).magnitude, Is.GreaterThanOrEqualTo(Range), $"at {p}");
            }
        }

        [Test]
        public void BoundaryLines_FitInsideTheTextureMargin()
        {
            byte[] field = BakePitch();
            // Touchlines at Z = ±34, goal lines at X = ±52.5 — inside the
            // grid margin, so the nearest texel must sit within a texel of
            // the line.
            foreach (Vector2 p in new[]
            {
                new Vector2(10f, 34f), new Vector2(10f, -34f),
                new Vector2(52.5f, 25f), new Vector2(-52.5f, 25f)
            })
            {
                Vector2Int texel = Texel(p.x, p.y);
                Assert.That(Offset(field, texel.x, texel.y).magnitude, Is.LessThan(0.1f), $"at {p}");
            }
        }

        [Test]
        public void ThinLine_IsResolvedBetweenTexelCenters()
        {
            // The reason for an offset field: a line that falls between two
            // texel centers (here 5 cm from each, texels 10 cm apart) must
            // still reconstruct to ~zero distance under bilinear filtering,
            // so a 3 cm guide is drawn at full strength there.
            var bounds = new PitchBounds(new Vector2(-1f, -1f), new Vector2(2f, 2f));
            const int size = 20; // 10 cm texels; centers at ±0.05, ±0.15, ...
            var lines = new[]
            {
                new IdealLine(LineElementType.HalfwayLine, new[] { new Vector2(0f, -1f), new Vector2(0f, 1f) })
            };
            byte[] field = LineGuideRasterizer.RasterizeOffsetField(lines, bounds, size, size, Range);

            int row = size / 2;
            Vector2 left = Offset(field, size / 2 - 1, row, size);   // texel center x = -0.05
            Vector2 right = Offset(field, size / 2, row, size);      // texel center x = +0.05
            Assert.That(left.x, Is.EqualTo(0.05f).Within(QuantizationM));
            Assert.That(right.x, Is.EqualTo(-0.05f).Within(QuantizationM));

            // Bilinear sample exactly on the line (halfway between them).
            float onLine = Vector2.Lerp(left, right, 0.5f).magnitude;
            Assert.That(onLine, Is.LessThan(QuantizationM));
            Assert.That(LineGuideRasterizer.Coverage(onLine, 0.03f, 0.01f), Is.EqualTo(1f).Within(0.01f));

            // 2 cm off the line: outside a 3 cm guide.
            float offLine = Vector2.Lerp(left, right, 0.7f).magnitude;
            Assert.That(offLine, Is.EqualTo(0.02f).Within(QuantizationM));
            Assert.That(LineGuideRasterizer.Coverage(offLine, 0.03f, 0.01f), Is.EqualTo(0f));
        }

        [Test]
        public void Coverage_SoftEdgeLiesInsideTheWidth()
        {
            const float width = 0.03f;
            const float softness = 0.01f;
            Assert.That(LineGuideRasterizer.Coverage(0f, width, softness), Is.EqualTo(1f));
            Assert.That(LineGuideRasterizer.Coverage(0.005f, width, softness), Is.EqualTo(1f).Within(1e-4f), "core");
            Assert.That(LineGuideRasterizer.Coverage(0.010f, width, softness), Is.EqualTo(0.5f).Within(1e-3f), "soft band");
            Assert.That(LineGuideRasterizer.Coverage(0.015f, width, softness), Is.EqualTo(0f).Within(1e-4f), "edge of the width");
            Assert.That(LineGuideRasterizer.Coverage(0.02f, width, softness), Is.EqualTo(0f), "nothing beyond the width");

            float previous = 1f;
            for (float d = 0f; d <= 0.03f; d += 0.001f)
            {
                float current = LineGuideRasterizer.Coverage(d, width, softness);
                Assert.That(current, Is.LessThanOrEqualTo(previous + 1e-6f), $"coverage rose at {d}");
                previous = current;
            }
        }

        [Test]
        public void DefaultGuide_IsFullyCoveredByADefaultPaintedLine()
        {
            // Contract (M.3.5 editor tour): a correctly centered painted line
            // must hide the guide completely — on the ground and on blades.
            var layout = ScriptableObject.CreateInstance<LineLayoutSO>();
            var liner = ScriptableObject.CreateInstance<LinerToolDefinitionSO>();
            try
            {
                Assert.That(layout.guideWidthM, Is.LessThanOrEqualTo(0.04f), "guide is a thin center line");
                Assert.That(layout.guideEdgeSoftnessM, Is.LessThanOrEqualTo(layout.guideWidthM * 0.5f));
                Assert.That(layout.guideWidthM, Is.LessThan(liner.lineWidthM));
                Assert.That(layout.guideBladeWidthM, Is.LessThan(liner.lineWidthM));
                Assert.That(layout.guideFieldRangeM, Is.GreaterThan(layout.guideBladeWidthM * 0.5f));
                // Nothing of either band is drawn at the painted line's edge.
                float paintHalf = liner.lineWidthM * 0.5f;
                Assert.That(LineGuideRasterizer.Coverage(paintHalf, layout.guideWidthM, layout.guideEdgeSoftnessM), Is.EqualTo(0f));
                Assert.That(LineGuideRasterizer.Coverage(paintHalf, layout.guideBladeWidthM, layout.guideEdgeSoftnessM), Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(liner);
            }
        }

        [Test]
        public void Field_IsMirrorSymmetricOnBothAxes()
        {
            // Every texel: a stray segment (e.g. a false chord from a split
            // arc) must not slip between sample points.
            byte[] field = BakePitch();
            int mismatches = 0;
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float distance = Mathf.Min(Offset(field, x, y).magnitude, Range);
                    float mirrorX = Mathf.Min(Offset(field, Width - 1 - x, y).magnitude, Range);
                    float mirrorY = Mathf.Min(Offset(field, x, Height - 1 - y).magnitude, Range);
                    if (Mathf.Abs(distance - mirrorX) > 0.01f || Mathf.Abs(distance - mirrorY) > 0.01f)
                    {
                        mismatches++;
                    }
                }
            }
            Assert.That(mismatches, Is.EqualTo(0));
        }

        [Test]
        public void Orientation_RowZeroIsMinZ_ColumnZeroIsMinX()
        {
            // One short off-center line in the +X/+Z quadrant; a flipped or
            // transposed bake would put it in another quadrant.
            var bounds = new PitchBounds(new Vector2(-10f, -10f), new Vector2(20f, 20f));
            const int size = 200; // 10 cm texels
            var lines = new[]
            {
                new IdealLine(LineElementType.HalfwayLine, new[] { new Vector2(4f, 6f), new Vector2(6f, 6f) })
            };
            byte[] field = LineGuideRasterizer.RasterizeOffsetField(lines, bounds, size, size, Range);

            int column = Mathf.FloorToInt((5f + 10f) / 20f * size);   // x = 5 -> column 150
            int row = Mathf.FloorToInt((6f + 10f) / 20f * size);      // z = 6 -> row 160
            Assert.That(Offset(field, column, row, size).magnitude, Is.LessThan(0.1f), "expected quadrant");
            Assert.That(Offset(field, column, size - 1 - row, size).magnitude, Is.GreaterThanOrEqualTo(Range), "Z-flipped");
            Assert.That(Offset(field, size - 1 - column, row, size).magnitude, Is.GreaterThanOrEqualTo(Range), "X-flipped");
            Assert.That(Offset(field, row, column, size).magnitude, Is.GreaterThanOrEqualTo(Range), "transposed");

            // The offset points up (+Z) from the texel just below the line.
            Vector2 below = Offset(field, column, row - 1, size);     // texel center z = 5.95
            Assert.That(below.y, Is.EqualTo(0.05f).Within(QuantizationM));
        }

        [Test]
        public void EmptyInput_IsBeyondRangeEverywhere()
        {
            byte[] field = LineGuideRasterizer.RasterizeOffsetField(new IdealLine[0], Bounds, 16, 16, Range);
            Assert.That(field.Length, Is.EqualTo(16 * 16 * 2));
            for (int i = 0; i < 16 * 16; i++)
            {
                Assert.That(LineGuideRasterizer.DecodeOffset(field[i * 2], field[i * 2 + 1], Range).magnitude,
                    Is.GreaterThanOrEqualTo(Range));
            }
        }
    }

    /// <summary>
    /// Guide strength rule (M.3.5 editor tour fix): the debug override is a
    /// liner preview — with any other tool the guide must be 0 no matter what
    /// override is selected.
    /// </summary>
    public class LineGuideStrengthTests
    {
        [Test]
        public void LinerEquipped_NoOverride_FollowsTheTool()
        {
            Assert.That(LineGuideStrength.Resolve(0.35f, null, true), Is.EqualTo(0.35f));
        }

        [TestCase(0f)]
        [TestCase(0.35f)]
        [TestCase(1f)]
        public void LinerEquipped_OverrideReplacesTheToolStrength(float overrideStrength)
        {
            Assert.That(LineGuideStrength.Resolve(0.35f, overrideStrength, true), Is.EqualTo(overrideStrength));
        }

        [TestCase(0f)]
        [TestCase(0.35f)]
        [TestCase(1f)]
        public void OtherTool_OverrideIsIgnored_GuideStaysOff(float overrideStrength)
        {
            // Mower/hose announce strength 0; "strong" selected with [H]
            // must not leak onto them (the bug found in the editor tour).
            Assert.That(LineGuideStrength.Resolve(0f, overrideStrength, false), Is.EqualTo(0f));
        }

        [Test]
        public void Result_IsClampedTo01()
        {
            Assert.That(LineGuideStrength.Resolve(2f, null, true), Is.EqualTo(1f));
            Assert.That(LineGuideStrength.Resolve(0.35f, -1f, true), Is.EqualTo(0f));
            Assert.That(LineGuideStrength.Resolve(0.35f, 5f, true), Is.EqualTo(1f));
        }
    }
}
