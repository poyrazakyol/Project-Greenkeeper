using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// One grass tile: an XZ rectangle inside the pitch bounds plus the AABB
    /// used for per-tile frustum culling (TDD §3.3).
    /// </summary>
    public readonly struct GrassTile
    {
        /// <summary>World XZ of the tile's min corner.</summary>
        public Vector2 OriginXz { get; }

        /// <summary>Tile extent in meters (edge tiles are clipped to the bounds).</summary>
        public Vector2 SizeXz { get; }

        /// <summary>Culling AABB: tile rectangle extruded up to the max blade height.</summary>
        public Bounds Aabb { get; }

        public GrassTile(Vector2 originXz, Vector2 sizeXz, float maxHeightM)
        {
            OriginXz = originXz;
            SizeXz = sizeXz;
            Aabb = new Bounds(
                new Vector3(originXz.x + sizeXz.x * 0.5f, maxHeightM * 0.5f, originXz.y + sizeXz.y * 0.5f),
                new Vector3(sizeXz.x, maxHeightM, sizeXz.y));
        }

        public float AreaM2 => SizeXz.x * SizeXz.y;

        public Vector2 CenterXz => OriginXz + SizeXz * 0.5f;
    }

    /// <summary>
    /// Splits the pitch bounds into grass tiles (TDD §3.3: ~8x8 m, one
    /// instanced batch each). Pure and deterministic; the tile list is built
    /// once and culled per frame.
    /// </summary>
    public static class GrassTileLayout
    {
        public static GrassTile[] Build(PitchBounds bounds, float tileSizeM, float maxBladeHeightM)
        {
            int countX = Mathf.CeilToInt(bounds.Size.x / tileSizeM);
            int countZ = Mathf.CeilToInt(bounds.Size.y / tileSizeM);

            var tiles = new GrassTile[countX * countZ];
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    var origin = new Vector2(
                        bounds.Origin.x + x * tileSizeM,
                        bounds.Origin.y + z * tileSizeM);
                    var size = new Vector2(
                        Mathf.Min(tileSizeM, bounds.Origin.x + bounds.Size.x - origin.x),
                        Mathf.Min(tileSizeM, bounds.Origin.y + bounds.Size.y - origin.y));
                    tiles[z * countX + x] = new GrassTile(origin, size, maxBladeHeightM);
                }
            }
            return tiles;
        }

        /// <summary>
        /// Distance from a world XZ point to the tile rectangle (0 inside).
        /// Used as the tile's LOD distance so a tile the camera stands on is
        /// always full density.
        /// </summary>
        public static float DistanceToTileXz(Vector2 pointXz, Vector2 tileOriginXz, Vector2 tileSizeXz)
        {
            float dx = Mathf.Max(tileOriginXz.x - pointXz.x, 0f, pointXz.x - (tileOriginXz.x + tileSizeXz.x));
            float dz = Mathf.Max(tileOriginXz.y - pointXz.y, 0f, pointXz.y - (tileOriginXz.y + tileSizeXz.y));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
