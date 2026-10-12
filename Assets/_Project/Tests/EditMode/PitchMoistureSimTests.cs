using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Collections;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// In-day moisture diffusion (TDD §3.4): a wet spot spreads to its
    /// neighbours, a flat field is unchanged, total moisture is roughly
    /// conserved, and it is deterministic. Double-buffered (no directional
    /// smear), edge-replicating (no leak to phantom zero). ApplyMoisture
    /// writes back only changed cells.
    /// </summary>
    public class PitchMoistureSimTests
    {
        private static byte[] Field(int w, int h, byte fill)
        {
            var field = new byte[w * h];
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = fill;
            }
            return field;
        }

        [Test]
        public void Diffuse_SpreadsAWetSpotToNeighbours()
        {
            const int w = 5, h = 5;
            byte[] source = Field(w, h, 0);
            byte[] dest = new byte[w * h];
            int center = 2 * w + 2;
            source[center] = 100;

            PitchMoistureSim.Diffuse(source, dest, w, h, 0.5f);

            Assert.That((int)dest[center], Is.LessThan(100), "Center must give moisture up.");
            Assert.That((int)dest[2 * w + 1], Is.GreaterThan(0), "Left neighbour gains moisture.");
            Assert.That((int)dest[1 * w + 2], Is.GreaterThan(0), "Lower neighbour gains moisture.");
            Assert.That((int)dest[0], Is.EqualTo(0), "Far corner stays dry after one pass.");
            Assert.That((int)source[center], Is.EqualTo(100), "Source buffer must stay untouched.");
        }

        [Test]
        public void Diffuse_FlatField_IsUnchanged()
        {
            const int w = 4, h = 4;
            byte[] source = Field(w, h, 55);
            byte[] dest = new byte[w * h];

            PitchMoistureSim.Diffuse(source, dest, w, h, 0.5f);

            for (int i = 0; i < dest.Length; i++)
            {
                Assert.That((int)dest[i], Is.EqualTo(55), "A uniform field must not change.");
            }
        }

        [Test]
        public void Diffuse_ApproximatelyConservesTotalMoisture()
        {
            const int w = 6, h = 6;
            byte[] source = Field(w, h, 0);
            byte[] dest = new byte[w * h];
            source[2 * w + 3] = 80;
            source[4 * w + 1] = 40;

            PitchMoistureSim.Diffuse(source, dest, w, h, 0.3f);

            // Byte rounding allows small drift; edge replication conserves.
            Assert.That(Sum(dest), Is.EqualTo(Sum(source)).Within(w * h));
        }

        [Test]
        public void Diffuse_ZeroRate_CopiesThrough()
        {
            const int w = 3, h = 3;
            byte[] source = Field(w, h, 0);
            byte[] dest = Field(w, h, 77); // garbage that must be overwritten
            source[4] = 99;

            PitchMoistureSim.Diffuse(source, dest, w, h, 0f);

            Assert.That((int)dest[4], Is.EqualTo(99));
            Assert.That((int)dest[0], Is.EqualTo(0));
        }

        [Test]
        public void Diffuse_IsDeterministic()
        {
            const int w = 5, h = 5;
            byte[] source = Field(w, h, 0);
            source[12] = 100;
            byte[] a = new byte[w * h];
            byte[] b = new byte[w * h];

            PitchMoistureSim.Diffuse(source, a, w, h, 0.4f);
            PitchMoistureSim.Diffuse(source, b, w, h, 0.4f);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void ApplyMoisture_WritesOnlyChangedCells_AndCountsThem()
        {
            var cells = new NativeArray<CellState>(3, Allocator.Temp);
            try
            {
                cells[0] = new CellState { Moisture = 10, Health = 70 };
                cells[1] = new CellState { Moisture = 20, Health = 71 };
                cells[2] = new CellState { Moisture = 30, Health = 72 };
                byte[] before = { 10, 20, 30 };
                byte[] after = { 10, 25, 30 };

                int changed = PitchMoistureSim.ApplyMoisture(cells, before, after);

                Assert.That(changed, Is.EqualTo(1));
                Assert.That((int)cells[1].Moisture, Is.EqualTo(25));
                Assert.That((int)cells[1].Health, Is.EqualTo(71), "Other channels must survive the write-back.");
                Assert.That((int)cells[0].Moisture, Is.EqualTo(10));
                Assert.That((int)cells[2].Moisture, Is.EqualTo(30));
            }
            finally
            {
                cells.Dispose();
            }
        }

        private static int Sum(byte[] field)
        {
            int total = 0;
            for (int i = 0; i < field.Length; i++)
            {
                total += field[i];
            }
            return total;
        }
    }
}
