using System.Collections.Generic;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// LinerTool against a real PitchGrid and a fake stamp sink (no scene):
    /// dual-layer write (CPU PaintCoverage + Paint stamp), stroke recording
    /// with spacing thinning, and clear.
    /// </summary>
    public class LinerToolTests
    {
        private StadiumDefinitionSO _stadium;
        private PitchGrid _grid;
        private LinerToolDefinitionSO _definition;
        private LinerTool _liner;
        private readonly List<StampRequest> _stamps = new List<StampRequest>();

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);

            _definition = ScriptableObject.CreateInstance<LinerToolDefinitionSO>();
            _definition.lineWidthM = 0.12f;
            _definition.forwardOffsetM = 0.6f;
            _definition.paintStrength = 100;
            _definition.minStrokePointSpacingM = 0.1f;

            _liner = new LinerTool(_definition);
            _stamps.Clear();
            _liner.OnEquip(ContextAt(Vector3.zero));
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_stadium);
        }

        private ToolContext ContextAt(Vector3 playerPosition)
        {
            return new ToolContext(
                playerPosition, Vector3.forward,
                new Ray(playerPosition + Vector3.up, Vector3.forward),
                _grid, stamp => _stamps.Add(stamp));
        }

        private byte PaintAtWorld(float x, float z)
        {
            Assert.That(_grid.TryWorldToCell(new Vector3(x, 0f, z), out int cx, out int cy), Is.True);
            return _grid.Cells[_grid.IndexOf(cx, cy)].PaintCoverage;
        }

        [Test]
        public void UseHeld_PaintsCpuCellsUnderCart()
        {
            _liner.OnUseHeld(ContextAt(Vector3.zero), 0.016f);

            // Cart sits 0.6 m in front along +Z.
            Assert.That(PaintAtWorld(0f, 0.6f), Is.EqualTo(100));
            Assert.That(PaintAtWorld(0f, 5f), Is.EqualTo(0), "Far cell stays unpainted.");
        }

        [Test]
        public void UseHeld_EnqueuesPaintStamp()
        {
            _liner.OnUseHeld(ContextAt(Vector3.zero), 0.016f);

            Assert.That(_stamps.Count, Is.GreaterThanOrEqualTo(1));
            StampRequest stamp = _stamps[0];
            Assert.That(stamp.Target, Is.EqualTo(PitchRenderTarget.Paint));
            Assert.That(stamp.Mask, Is.EqualTo(ColorWriteMask.Red));
            Assert.That(stamp.Value.r, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void UseHeld_RecordsStrokePoints_WithSpacingThinning()
        {
            // Move the cart in 0.05 m steps (< 0.1 m spacing): consecutive
            // sub-spacing points must be dropped.
            for (int i = 0; i < 10; i++)
            {
                _liner.OnUseHeld(ContextAt(new Vector3(0f, 0f, i * 0.05f)), 0.016f);
            }

            Assert.That(_liner.Strokes.Count, Is.EqualTo(1));
            int pointCount = _liner.Strokes[0].Points.Count;
            // ~0.45 m of travel at 0.1 m spacing -> ~5-6 points, well under 10.
            Assert.That(pointCount, Is.LessThan(10));
            Assert.That(pointCount, Is.GreaterThan(2));
        }

        [Test]
        public void Release_StartsNewStrokeOnNextHold()
        {
            _liner.OnUseHeld(ContextAt(new Vector3(0f, 0f, 0f)), 0.016f);
            _liner.OnUseHeld(ContextAt(new Vector3(0f, 0f, 1f)), 0.016f);
            _liner.OnUseReleased(ContextAt(new Vector3(0f, 0f, 1f)));

            _liner.OnUseHeld(ContextAt(new Vector3(3f, 0f, 0f)), 0.016f);

            Assert.That(_liner.Strokes.Count, Is.EqualTo(2));
        }

        [Test]
        public void ClearStrokes_EmptiesTheRecord()
        {
            _liner.OnUseHeld(ContextAt(Vector3.zero), 0.016f);
            Assert.That(_liner.Strokes.Count, Is.EqualTo(1));

            _liner.ClearStrokes();
            Assert.That(_liner.Strokes.Count, Is.EqualTo(0));
        }

        [Test]
        public void Paint_DoesNotLowerExistingCoverage()
        {
            _definition.paintStrength = 40;
            // Pre-set a cell to 80.
            Assert.That(_grid.TryWorldToCell(new Vector3(0f, 0f, 0.6f), out int cx, out int cy), Is.True);
            int index = _grid.IndexOf(cx, cy);
            CellState cell = _grid.Cells[index];
            cell.PaintCoverage = 80;
            _grid.Cells[index] = cell;

            _liner.OnUseHeld(ContextAt(Vector3.zero), 0.016f);

            Assert.That(_grid.Cells[index].PaintCoverage, Is.EqualTo(80), "Weaker paint must not reduce coverage.");
        }
    }
}
