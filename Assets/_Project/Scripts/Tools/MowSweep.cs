using Greenkeeper.Pitch;
using Unity.Mathematics;
using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>Parameters of one mow pass over a cell (from ToolDefinitionSO).</summary>
    public struct MowCutParams
    {
        public float CutHeightMm;
        public Vector2 MoveDir;              // normalized stroke direction
        public float ScalpingHeightThresholdMm;
        public float ScalpingCutThresholdMm;
        public byte ScalpingHealthDamage;
    }

    /// <summary>Result of applying a cut to one cell.</summary>
    public struct MowCutResult
    {
        public CellState Cell;
        public float CutMm;      // 0 when the grass was already at/below the setting
        public bool Scalped;
    }

    /// <summary>
    /// Pure mowing core (TDD §4.2): the per-cell cut rule. Cell selection is
    /// shared with the liner via CapsuleCells.Collect. Deterministic and
    /// scene-free for EditMode tests; MowerTool drives it and mirrors the same
    /// stroke to GPU stamps (dual-layer write).
    /// </summary>
    public static class MowSweep
    {
        /// <summary>
        /// One cell cut (TDD §4.2): height is written only when the grass is
        /// taller than the setting; the stroke direction is recorded as the
        /// pattern vector; scalping (setting &lt; threshold AND cut ≥ threshold
        /// in one pass) damages health (GDD §3.1.1).
        /// </summary>
        public static MowCutResult ApplyCut(in CellState cell, in MowCutParams p)
        {
            MowCutResult result;
            result.Cell = cell;
            result.CutMm = 0f;
            result.Scalped = false;

            float currentMm = cell.GrassHeightMm;
            if (currentMm <= p.CutHeightMm)
            {
                return result; // nothing to cut; pattern is only laid where blades are cut
            }

            float cutMm = currentMm - p.CutHeightMm;
            result.CutMm = cutMm;
            result.Cell.GrassHeightMm = (half)p.CutHeightMm;
            result.Cell.PatternDirX = (half)p.MoveDir.x;
            result.Cell.PatternDirY = (half)p.MoveDir.y;

            if (p.CutHeightMm < p.ScalpingHeightThresholdMm && cutMm >= p.ScalpingCutThresholdMm)
            {
                result.Scalped = true;
                result.Cell.Health = (byte)Mathf.Max(0, cell.Health - p.ScalpingHealthDamage);
            }
            return result;
        }
    }
}
