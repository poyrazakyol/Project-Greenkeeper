using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>LOD band a tile falls into; drives the debug tint (F5).</summary>
    public enum GrassLodBand
    {
        Near = 0,
        Mid = 1,
        Far = 2
    }

    /// <summary>
    /// Distance-based density LOD (TDD §3.3): near band full density, mid band
    /// reduced density with widened blades, far band no blades (the ground
    /// shader takes over by sampling the same RTs). Pure functions; piecewise
    /// linear so density is continuous and monotonically non-increasing.
    /// </summary>
    public static class GrassLod
    {
        public static float DensityAt(
            float distanceM,
            float nearEndM, float midEndM, float farEndM,
            float nearDensityPerM2, float midDensityPerM2)
        {
            if (distanceM <= nearEndM)
            {
                return nearDensityPerM2;
            }
            if (distanceM <= midEndM)
            {
                float t = (distanceM - nearEndM) / Mathf.Max(midEndM - nearEndM, 0.0001f);
                return Mathf.Lerp(nearDensityPerM2, midDensityPerM2, t);
            }
            if (distanceM <= farEndM)
            {
                float t = (distanceM - midEndM) / Mathf.Max(farEndM - midEndM, 0.0001f);
                return Mathf.Lerp(midDensityPerM2, 0f, t);
            }
            return 0f;
        }

        public static GrassLodBand BandAt(float distanceM, float nearEndM, float midEndM, float farEndM)
        {
            if (distanceM <= nearEndM)
            {
                return GrassLodBand.Near;
            }
            return distanceM <= farEndM ? GrassLodBand.Mid : GrassLodBand.Far;
        }

        /// <summary>
        /// Blade width multiplier: 1 in the near band, widening toward
        /// <paramref name="midWidthScale"/> across the mid band so fewer
        /// blades still read as full cover (TDD §3.3 "geniş blade").
        /// </summary>
        public static float WidthScaleAt(float distanceM, float nearEndM, float midEndM, float midWidthScale)
        {
            float t = Mathf.InverseLerp(nearEndM, midEndM, distanceM);
            return Mathf.Lerp(1f, midWidthScale, t);
        }

        public static int InstanceCount(float densityPerM2, float tileAreaM2, int maxPerTile)
        {
            int count = Mathf.RoundToInt(densityPerM2 * tileAreaM2);
            return Mathf.Clamp(count, 0, maxPerTile);
        }
    }
}
