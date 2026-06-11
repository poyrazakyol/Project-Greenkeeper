using System.Collections.Generic;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// MowerTool against a real PitchGrid and a fake stamp sink (no scene):
    /// dual-layer write, basket fill/block/empty and height step cycling.
    /// </summary>
    public class MowerToolTests
    {
        private StadiumDefinitionSO _stadium;
        private PitchGrid _grid;
        private ToolDefinitionSO _definition;
        private MowerTool _mower;
        private readonly List<StampRequest> _stamps = new List<StampRequest>();

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);
            FillGrass(45f);

            _definition = ScriptableObject.CreateInstance<ToolDefinitionSO>();
            _definition.mowWidthM = 0.5f;
            _definition.mowForwardOffsetM = 0.7f;
            _definition.cutHeightStepsMm = new[] { 10f, 15f, 20f, 25f, 30f };
            _definition.defaultStepIndex = 3; // 25 mm
            _definition.basketCapacityL = 50f;
            _definition.clippingsLPerM2PerMm = 0.35f;
            _definition.basketEmptySeconds = 2f;

            _mower = new MowerTool(_definition);
            _stamps.Clear();
            _mower.OnEquip(ContextAt(Vector3.zero));
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_stadium);
        }

        private void FillGrass(float heightMm)
        {
            var cells = _grid.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                CellState cell = cells[i];
                cell.GrassHeightMm = (half)heightMm;
                cell.Health = 70;
                cells[i] = cell;
            }
        }

        private ToolContext ContextAt(Vector3 playerPosition)
        {
            return new ToolContext(
                playerPosition, Vector3.forward,
                new Ray(playerPosition + Vector3.up, Vector3.forward),
                _grid, stamp => _stamps.Add(stamp));
        }

        private float HeightAtWorld(float x, float z)
        {
            Assert.That(_grid.TryWorldToCell(new Vector3(x, 0f, z), out int cx, out int cy), Is.True);
            return (float)_grid.Cells[_grid.IndexOf(cx, cy)].GrassHeightMm;
        }

        [Test]
        public void UseHeld_CutsCpuCellsAtDeckPosition()
        {
            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);

            // Deck sits 0.7 m in front of the player along +Z.
            Assert.That(HeightAtWorld(0f, 0.7f), Is.EqualTo(25f).Within(0.05f));
            Assert.That(HeightAtWorld(0f, 5f), Is.EqualTo(45f).Within(0.05f), "Far cell must stay uncut.");
        }

        [Test]
        public void UseHeld_EnqueuesMaskedDualStamps()
        {
            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);

            Assert.That(_stamps.Count, Is.GreaterThanOrEqualTo(2));

            StampRequest heightStamp = _stamps.Find(s => s.Target == PitchRenderTarget.GrassState);
            Assert.That(heightStamp.Mask, Is.EqualTo(ColorWriteMask.Red),
                "Mow height stamp must write R only (health/moisture untouched).");
            Assert.That(heightStamp.BlendOp, Is.EqualTo(BlendOp.Min),
                "Mow height stamp must only be able to shorten.");
            Assert.That(heightStamp.Value.r, Is.EqualTo(25f / 60f).Within(0.001f));

            StampRequest patternStamp = _stamps.Find(s => s.Target == PitchRenderTarget.Pattern);
            Assert.That(patternStamp.Mask, Is.EqualTo(ColorWriteMask.Red | ColorWriteMask.Green));
            Assert.That(patternStamp.BlendOp, Is.EqualTo(BlendOp.Add));
        }

        [Test]
        public void UseHeld_SweepBetweenFrames_CutsThePath()
        {
            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);
            _mower.OnUseHeld(ContextAt(new Vector3(0f, 0f, 2f)), 0.016f);

            // Midpoint of the swept segment (deck z: 0.7 -> 2.7).
            Assert.That(HeightAtWorld(0f, 1.7f), Is.EqualTo(25f).Within(0.05f));
        }

        [Test]
        public void Basket_FillsFromClippings_AndBlocksCuttingWhenFull()
        {
            _definition.basketCapacityL = 0.05f; // tiny: one pass fills it

            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);
            Assert.That(_mower.BasketFull, Is.True);

            _mower.OnUseHeld(ContextAt(new Vector3(5f, 0f, 5f)), 0.016f);
            Assert.That(HeightAtWorld(5f, 5.7f), Is.EqualTo(45f).Within(0.05f),
                "A full basket must stop cutting.");
        }

        [Test]
        public void Basket_EmptyHold_RequiresFullHoldThenResets()
        {
            _definition.basketCapacityL = 0.05f;
            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);
            Assert.That(_mower.BasketFull, Is.True);

            Assert.That(_mower.TickEmptyHold(1f), Is.False, "Half the hold must not empty yet.");
            _mower.ResetEmptyHold();
            Assert.That(_mower.TickEmptyHold(1f), Is.False, "Released hold must restart.");
            Assert.That(_mower.TickEmptyHold(1.1f), Is.True);
            Assert.That(_mower.BasketFill01, Is.EqualTo(0f));
        }

        [Test]
        public void UseReleased_BreaksTheStroke()
        {
            _mower.OnUseHeld(ContextAt(Vector3.zero), 0.016f);
            _mower.OnUseReleased(ContextAt(Vector3.zero));
            _mower.OnUseHeld(ContextAt(new Vector3(0f, 0f, 4f)), 0.016f);

            // The gap between the strokes must stay uncut.
            Assert.That(HeightAtWorld(0f, 2.5f), Is.EqualTo(45f).Within(0.05f));
            Assert.That(HeightAtWorld(0f, 4.7f), Is.EqualTo(25f).Within(0.05f));
        }

        [Test]
        public void CycleHeightStep_ClampsAtBothEnds()
        {
            Assert.That(_mower.CurrentCutHeightMm, Is.EqualTo(25f));
            _mower.CycleHeightStep(1);
            _mower.CycleHeightStep(1); // would pass the end
            Assert.That(_mower.CurrentCutHeightMm, Is.EqualTo(30f));
            _mower.CycleHeightStep(-10);
            Assert.That(_mower.CurrentCutHeightMm, Is.EqualTo(10f));
        }
    }
}
