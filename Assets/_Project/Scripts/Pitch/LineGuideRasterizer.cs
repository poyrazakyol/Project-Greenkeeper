using System.Collections.Generic;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Bakes the ideal pitch lines (LineGeometry) into an offset field for the
    /// ghost guide overlay (M.3.5). Pure and deterministic: the same polylines
    /// the line score compares against become the pixels the player sees, so
    /// the guide can never drift from the scoring reference. Visual only — no
    /// game logic reads the field (CPU/GPU authority split, TDD §3.1).
    ///
    /// Why an offset field and not a coverage mask: the guide is a thin
    /// "taut string" (~3 cm), thinner than a texel of any affordable mask.
    /// Each texel instead stores the vector to the nearest ideal-line point.
    /// That vector passes linearly through zero across a line, so bilinear
    /// filtering reconstructs the true distance between texel centers and the
    /// shader can draw a crisp line of any width from a coarse texture.
    /// </summary>
    public static class LineGuideRasterizer
    {
        /// <summary>
        /// Returns width*height*2 bytes (R = offset X, G = offset Z), row-major
        /// with row 0 at v = 0 — the PitchBounds.WorldToUv convention, so the
        /// buffer uploads straight into an RG16 Texture2D sampled with the
        /// shared pitch UV. Offsets are clamped to ±<paramref name="rangeM"/>
        /// per axis; texels with no line within range store the far corner.
        /// </summary>
        public static byte[] RasterizeOffsetField(
            IReadOnlyList<IdealLine> lines, PitchBounds bounds, int width, int height, float rangeM)
        {
            if (width <= 0 || height <= 0)
            {
                return new byte[0];
            }

            float range = Mathf.Max(rangeM, 1e-3f);
            int texelCount = width * height;
            var bestSq = new float[texelCount];
            var field = new byte[texelCount * 2];
            for (int i = 0; i < texelCount; i++)
            {
                bestSq[i] = float.MaxValue;
                field[i * 2] = 255;       // far: decodes to (+range, +range)
                field[i * 2 + 1] = 255;
            }

            float texelX = bounds.Size.x / width;
            float texelY = bounds.Size.y / height;

            for (int l = 0; l < lines.Count; l++)
            {
                Vector2[] polyline = lines[l].Polyline;
                for (int i = 0; i < polyline.Length - 1; i++)
                {
                    Vector2 a = polyline[i];
                    Vector2 b = polyline[i + 1];

                    // Texel AABB of the segment grown by the field range.
                    int minX = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - range - bounds.Origin.x) / texelX));
                    int maxX = Mathf.Min(width - 1, Mathf.FloorToInt((Mathf.Max(a.x, b.x) + range - bounds.Origin.x) / texelX));
                    int minY = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - range - bounds.Origin.y) / texelY));
                    int maxY = Mathf.Min(height - 1, Mathf.FloorToInt((Mathf.Max(a.y, b.y) + range - bounds.Origin.y) / texelY));

                    for (int y = minY; y <= maxY; y++)
                    {
                        float worldZ = bounds.Origin.y + (y + 0.5f) * texelY;
                        int row = y * width;
                        for (int x = minX; x <= maxX; x++)
                        {
                            var texel = new Vector2(bounds.Origin.x + (x + 0.5f) * texelX, worldZ);
                            Vector2 offset = ClosestPointOnSegment(texel, a, b) - texel;
                            float sq = offset.sqrMagnitude;
                            int index = row + x;
                            if (sq < bestSq[index])
                            {
                                bestSq[index] = sq;
                                field[index * 2] = EncodeAxis(offset.x, range);
                                field[index * 2 + 1] = EncodeAxis(offset.y, range);
                            }
                        }
                    }
                }
            }
            return field;
        }

        /// <summary>Inverse of the byte encoding; mirrored in the shaders.</summary>
        public static Vector2 DecodeOffset(byte r, byte g, float rangeM)
        {
            return new Vector2(
                (r / 255f * 2f - 1f) * rangeM,
                (g / 255f * 2f - 1f) * rangeM);
        }

        /// <summary>
        /// Guide opacity profile across the line, mirrored in the shaders:
        /// 1 at the center, 0 at half the width. The soft edge lies INSIDE
        /// <paramref name="widthM"/>, so nothing is drawn beyond it and a
        /// correctly painted (wider) line covers the guide completely.
        /// </summary>
        public static float Coverage(float distanceM, float widthM, float edgeSoftnessM)
        {
            float half = Mathf.Max(widthM, 0f) * 0.5f;
            float softness = Mathf.Clamp(edgeSoftnessM, 1e-4f, Mathf.Max(half, 1e-4f));
            return Mathf.Clamp01((half - distanceM) / softness);
        }

        private static byte EncodeAxis(float offsetM, float rangeM)
        {
            float normalized = Mathf.Clamp(offsetM / rangeM, -1f, 1f) * 0.5f + 0.5f;
            return (byte)Mathf.RoundToInt(normalized * 255f);
        }

        private static Vector2 ClosestPointOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 1e-8f)
            {
                return a;
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / abLenSq);
            return a + ab * t;
        }
    }
}
