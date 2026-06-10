using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Channel semantics of RT_GrassState (TDD §3.2), fixed as of F0.4:
    /// R = grass height (mm / 60, normalized), G = health (0-1),
    /// B = moisture (0-1), A = damage mask (0-1). This is the encoding used
    /// both for the initial CPU-grid seed blit and by any tool brush that
    /// writes grass state.
    /// </summary>
    public static class GrassStateEncoding
    {
        /// <summary>Logical grass height ceiling in mm (CellState range, TDD §3.1).</summary>
        public const float MaxGrassHeightMm = 60f;

        public static Color32 Encode(in CellState cell)
        {
            return new Color32(
                ToByte01((float)cell.GrassHeightMm / MaxGrassHeightMm),
                ToByte01(cell.Health / 100f),
                ToByte01(cell.Moisture / 100f),
                ToByte01(cell.Damage / 100f));
        }

        private static byte ToByte01(float normalized)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(normalized) * 255f);
        }
    }
}
