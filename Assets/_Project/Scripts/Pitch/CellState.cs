using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Per-cell pitch state (TDD §3.1). The CPU grid built from these cells is
    /// the single authority for simulation, scoring and saving; GPU render
    /// textures are visual-only. Size target: 16 bytes (29.7k cells ≈ 475 KB).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct CellState
    {
        public half GrassHeightMm;   // 0–60 mm, logical height (visual detail lives in RTs)
        public byte Health;          // 0–100
        public byte Moisture;        // 0–100
        public byte Compaction;      // 0–100, passive/constant until Phase 3
        public byte Damage;          // 0–100
        public byte PaintCoverage;   // 0–100, logical (visual detail in RT_Paint)
        public byte ZoneId;          // zone tag (goal area, midfield...) for damage distribution
        public half PatternDirX;     // mowing direction vector (aesthetic scoring)
        public half PatternDirY;
        public byte _reserved;
    }
}
