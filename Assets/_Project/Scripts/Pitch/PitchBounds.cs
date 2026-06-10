using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Fixed world↔UV mapping of the pitch grid area (TDD §3.2). The CPU grid
    /// and every render texture share these bounds, so RT texels and grid
    /// cells live in one space and tools can write both layers from a single
    /// stroke position.
    /// </summary>
    public readonly struct PitchBounds
    {
        /// <summary>World XZ position of the min corner.</summary>
        public Vector2 Origin { get; }

        /// <summary>Extent in meters along world X and Z.</summary>
        public Vector2 Size { get; }

        public PitchBounds(Vector2 origin, Vector2 size)
        {
            Origin = origin;
            Size = size;
        }

        /// <summary>Bounds covering the grid area, centered on the world origin.</summary>
        public static PitchBounds FromStadium(StadiumDefinitionSO stadium)
        {
            var size = new Vector2(
                stadium.gridWidth * stadium.cellSizeM,
                stadium.gridHeight * stadium.cellSizeM);
            return new PitchBounds(-size * 0.5f, size);
        }

        public Vector2 WorldToUv(Vector3 worldPosition)
        {
            return new Vector2(
                (worldPosition.x - Origin.x) / Size.x,
                (worldPosition.z - Origin.y) / Size.y);
        }

        public Vector3 UvToWorld(Vector2 uv)
        {
            return new Vector3(
                Origin.x + uv.x * Size.x,
                0f,
                Origin.y + uv.y * Size.y);
        }

        /// <summary>
        /// Shader-friendly transform: uv = worldXZ * zw + xy.
        /// </summary>
        public Vector4 UvTransform => new Vector4(
            -Origin.x / Size.x,
            -Origin.y / Size.y,
            1f / Size.x,
            1f / Size.y);
    }
}
