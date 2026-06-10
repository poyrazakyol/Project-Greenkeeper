using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Guards the TDD §3.1 memory contract: 16 bytes per cell max
    /// (29.7k cells ≈ 475 KB) and a stable sequential layout for the
    /// pitch.bin save blob (TDD §9).
    /// </summary>
    public class CellStateTests
    {
        [Test]
        public void Size_IsAtMost16Bytes()
        {
            Assert.That(UnsafeUtility.SizeOf<CellState>(), Is.LessThanOrEqualTo(16),
                "CellState exceeds the 16-byte budget (TDD §3.1).");
        }

        [Test]
        public void Layout_IsSequential()
        {
            Assert.That(typeof(CellState).IsLayoutSequential, Is.True,
                "CellState must keep LayoutKind.Sequential for the save blob (TDD §9).");
        }
    }
}
