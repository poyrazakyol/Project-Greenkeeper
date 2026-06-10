using Greenkeeper.Pitch;
using NUnit.Framework;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Distance density LOD (TDD §3.3): three bands, continuous and
    /// monotonically non-increasing, far band empty (ground shader takes
    /// over), per-tile instance cap respected.
    /// </summary>
    public class GrassLodTests
    {
        private const float NearEnd = 18f;
        private const float MidEnd = 42f;
        private const float FarEnd = 70f;
        private const float NearDensity = 250f;
        private const float MidDensity = 60f;

        private static float Density(float distance)
        {
            return GrassLod.DensityAt(distance, NearEnd, MidEnd, FarEnd, NearDensity, MidDensity);
        }

        [Test]
        public void DensityAt_NearBand_IsFullDensity()
        {
            Assert.That(Density(0f), Is.EqualTo(NearDensity));
            Assert.That(Density(NearEnd), Is.EqualTo(NearDensity));
        }

        [Test]
        public void DensityAt_BandEdges_AreContinuous()
        {
            Assert.That(Density(NearEnd + 0.001f), Is.EqualTo(NearDensity).Within(0.1f));
            Assert.That(Density(MidEnd), Is.EqualTo(MidDensity).Within(0.001f));
            Assert.That(Density(MidEnd + 0.001f), Is.EqualTo(MidDensity).Within(0.1f));
            Assert.That(Density(FarEnd), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DensityAt_IsMonotonicallyNonIncreasing()
        {
            float previous = float.MaxValue;
            for (float d = 0f; d <= 100f; d += 0.5f)
            {
                float density = Density(d);
                Assert.That(density, Is.LessThanOrEqualTo(previous + 0.0001f),
                    $"Density rose between {d - 0.5f} m and {d} m.");
                previous = density;
            }
        }

        [Test]
        public void DensityAt_BeyondFar_IsZero()
        {
            Assert.That(Density(FarEnd + 0.1f), Is.EqualTo(0f));
            Assert.That(Density(1000f), Is.EqualTo(0f));
        }

        [Test]
        public void BandAt_MapsDistancesToBands()
        {
            Assert.That(GrassLod.BandAt(5f, NearEnd, MidEnd, FarEnd), Is.EqualTo(GrassLodBand.Near));
            Assert.That(GrassLod.BandAt(30f, NearEnd, MidEnd, FarEnd), Is.EqualTo(GrassLodBand.Mid));
            Assert.That(GrassLod.BandAt(80f, NearEnd, MidEnd, FarEnd), Is.EqualTo(GrassLodBand.Far));
        }

        [Test]
        public void WidthScaleAt_WidensAcrossMidBand()
        {
            Assert.That(GrassLod.WidthScaleAt(0f, NearEnd, MidEnd, 2.2f), Is.EqualTo(1f));
            Assert.That(GrassLod.WidthScaleAt(NearEnd, NearEnd, MidEnd, 2.2f), Is.EqualTo(1f));
            Assert.That(GrassLod.WidthScaleAt(MidEnd, NearEnd, MidEnd, 2.2f), Is.EqualTo(2.2f).Within(0.001f));
            Assert.That(GrassLod.WidthScaleAt(MidEnd + 50f, NearEnd, MidEnd, 2.2f), Is.EqualTo(2.2f).Within(0.001f));
        }

        [Test]
        public void InstanceCount_ScalesWithAreaAndRespectsCap()
        {
            Assert.That(GrassLod.InstanceCount(250f, 64f, 24000), Is.EqualTo(16000));
            Assert.That(GrassLod.InstanceCount(250f, 64f, 10000), Is.EqualTo(10000), "Per-tile cap must clamp.");
            Assert.That(GrassLod.InstanceCount(0f, 64f, 24000), Is.EqualTo(0));
        }
    }
}
