using Greenkeeper.Pitch;
using NUnit.Framework;
using Unity.Mathematics;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// RT_GrassState channel encoding (fixed in F0.4, TDD §3.2): R height,
    /// G health, B moisture, A damage. Brushes and the seed blit must agree
    /// on this mapping, so it is pinned by tests.
    /// </summary>
    public class GrassStateEncodingTests
    {
        [Test]
        public void Encode_MapsChannelsToExpectedBytes()
        {
            var cell = new CellState
            {
                GrassHeightMm = (half)30f, // half of 60 mm ceiling
                Health = 100,
                Moisture = 0,
                Damage = 50
            };

            UnityEngine.Color32 encoded = GrassStateEncoding.Encode(cell);

            Assert.That(encoded.r, Is.EqualTo(128).Within(1), "R must be height / 60 mm.");
            Assert.That(encoded.g, Is.EqualTo(255), "G must be health / 100.");
            Assert.That(encoded.b, Is.EqualTo(0), "B must be moisture / 100.");
            Assert.That(encoded.a, Is.EqualTo(128).Within(1), "A must be damage / 100.");
        }

        [Test]
        public void Encode_ClampsHeightAboveCeiling()
        {
            var cell = new CellState { GrassHeightMm = (half)90f };
            Assert.That(GrassStateEncoding.Encode(cell).r, Is.EqualTo(255));
        }

        [Test]
        public void Encode_ZeroCell_IsAllZero()
        {
            UnityEngine.Color32 encoded = GrassStateEncoding.Encode(default);
            Assert.That(encoded.r, Is.EqualTo(0));
            Assert.That(encoded.g, Is.EqualTo(0));
            Assert.That(encoded.b, Is.EqualTo(0));
            Assert.That(encoded.a, Is.EqualTo(0));
        }
    }
}
