using Greenkeeper.Pitch;
using NUnit.Framework;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Grass tile layout (TDD §3.3): the tile set must exactly cover the
    /// pitch bounds with clipped edge tiles, deterministically.
    /// </summary>
    public class GrassTileLayoutTests
    {
        // Matches PitchBounds.FromStadium of the default 212x140 grid.
        private static readonly PitchBounds Bounds =
            new PitchBounds(new Vector2(-53f, -35f), new Vector2(106f, 70f));

        [Test]
        public void Build_DefaultTileSize_Produces14x9Tiles()
        {
            GrassTile[] tiles = GrassTileLayout.Build(Bounds, 8f, 0.1f);
            Assert.That(tiles.Length, Is.EqualTo(14 * 9));
        }

        [Test]
        public void Build_TileAreasSumToBoundsArea()
        {
            GrassTile[] tiles = GrassTileLayout.Build(Bounds, 8f, 0.1f);
            float totalArea = 0f;
            foreach (GrassTile tile in tiles)
            {
                totalArea += tile.AreaM2;
            }
            Assert.That(totalArea, Is.EqualTo(106f * 70f).Within(0.01f));
        }

        [Test]
        public void Build_EdgeTilesAreClippedToBounds()
        {
            GrassTile[] tiles = GrassTileLayout.Build(Bounds, 8f, 0.1f);
            foreach (GrassTile tile in tiles)
            {
                Assert.That(tile.OriginXz.x + tile.SizeXz.x, Is.LessThanOrEqualTo(53f + 0.001f));
                Assert.That(tile.OriginXz.y + tile.SizeXz.y, Is.LessThanOrEqualTo(35f + 0.001f));
                Assert.That(tile.SizeXz.x, Is.GreaterThan(0f));
                Assert.That(tile.SizeXz.y, Is.GreaterThan(0f));
            }

            // 106 = 13*8 + 2 and 70 = 8*8 + 6: last column is 2 m, last row 6 m.
            GrassTile lastTile = tiles[tiles.Length - 1];
            Assert.That(lastTile.SizeXz.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(lastTile.SizeXz.y, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void Build_AabbCoversBladeHeight()
        {
            GrassTile[] tiles = GrassTileLayout.Build(Bounds, 8f, 0.12f);
            Assert.That(tiles[0].Aabb.min.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tiles[0].Aabb.max.y, Is.EqualTo(0.12f).Within(0.001f));
        }

        [Test]
        public void DistanceToTileXz_InsideIsZero_OutsideMatchesAxisAndDiagonal()
        {
            var origin = new Vector2(0f, 0f);
            var size = new Vector2(8f, 8f);

            Assert.That(GrassTileLayout.DistanceToTileXz(new Vector2(4f, 4f), origin, size), Is.EqualTo(0f));
            Assert.That(GrassTileLayout.DistanceToTileXz(new Vector2(-3f, 4f), origin, size),
                Is.EqualTo(3f).Within(0.001f));
            Assert.That(GrassTileLayout.DistanceToTileXz(new Vector2(11f, 12f), origin, size),
                Is.EqualTo(5f).Within(0.001f), "Diagonal distance must be Euclidean (3-4-5).");
        }
    }
}
