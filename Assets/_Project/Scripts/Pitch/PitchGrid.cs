using System;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// CPU-side pitch state grid (TDD §3.1) — the single authority for
    /// simulation, scoring and saving. Plain class (no scene dependency) so
    /// EditMode tests construct it directly. Cell (0,0) sits at the world-min
    /// corner; X spans the pitch length, Y spans the pitch width (world Z).
    /// </summary>
    public sealed class PitchGrid : IDisposable
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        /// <summary>World XZ position of the grid's min corner.</summary>
        public Vector2 WorldOrigin { get; }

        public NativeArray<CellState> Cells;

        public int CellCount => Width * Height;

        /// <summary>
        /// Set by anything that writes cell moisture (hose, in-day tick, day
        /// end) so the render bridge can refresh the visual moisture channel
        /// from this CPU authority (TDD §3.4 "görsel tint güncelleme ihtiyacı
        /// işaretleme").
        /// </summary>
        public bool MoistureDirty { get; private set; }

        public void MarkMoistureDirty() => MoistureDirty = true;

        public void ClearMoistureDirty() => MoistureDirty = false;

        public PitchGrid(int width, int height, float cellSize, Vector2 worldOrigin,
            Allocator allocator = Allocator.Persistent)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Grid dimensions must be positive.");
            }
            if (cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be positive.");
            }

            Width = width;
            Height = height;
            CellSize = cellSize;
            WorldOrigin = worldOrigin;
            Cells = new NativeArray<CellState>(width * height, allocator);
        }

        /// <summary>
        /// Grid centered on the world origin, sized per the stadium definition.
        /// Shares its bounds with the render textures via PitchBounds (TDD §3.2).
        /// </summary>
        public static PitchGrid CreateCentered(StadiumDefinitionSO stadium,
            Allocator allocator = Allocator.Persistent)
        {
            PitchBounds bounds = PitchBounds.FromStadium(stadium);
            return new PitchGrid(stadium.gridWidth, stadium.gridHeight, stadium.cellSizeM, bounds.Origin, allocator);
        }

        public int IndexOf(int x, int y) => y * Width + x;

        public bool TryWorldToCell(Vector3 worldPosition, out int x, out int y)
        {
            x = Mathf.FloorToInt((worldPosition.x - WorldOrigin.x) / CellSize);
            y = Mathf.FloorToInt((worldPosition.z - WorldOrigin.y) / CellSize);
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public Vector3 CellToWorldCenter(int x, int y)
        {
            return new Vector3(
                WorldOrigin.x + (x + 0.5f) * CellSize,
                0f,
                WorldOrigin.y + (y + 0.5f) * CellSize);
        }

        public void Dispose()
        {
            if (Cells.IsCreated)
            {
                Cells.Dispose();
            }
        }
    }
}
