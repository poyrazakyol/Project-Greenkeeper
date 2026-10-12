using System.Collections.Generic;
using Greenkeeper.Pitch;
using Greenkeeper.Tools;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// HoseTool against a real PitchGrid and a fake stamp sink (no scene):
    /// ray→ground disc moisture add (clamped), no spray when aiming up/far,
    /// the Blue-mask moisture-tint stamp, and pressure cycling.
    /// </summary>
    public class HoseToolTests
    {
        private StadiumDefinitionSO _stadium;
        private PitchGrid _grid;
        private HoseToolDefinitionSO _definition;
        private HoseTool _hose;
        private readonly List<StampRequest> _stamps = new List<StampRequest>();

        [SetUp]
        public void SetUp()
        {
            _stadium = ScriptableObject.CreateInstance<StadiumDefinitionSO>();
            _grid = PitchGrid.CreateCentered(_stadium, Allocator.Persistent);
            // Start the field dry so additions are visible.
            for (int i = 0; i < _grid.Cells.Length; i++)
            {
                _grid.Cells[i] = new CellState { Moisture = 0 };
            }

            _definition = ScriptableObject.CreateInstance<HoseToolDefinitionSO>();
            _definition.pressureStepsLPerSec = new[] { 2f, 5f, 9f };
            _definition.defaultStepIndex = 1;
            _definition.coneRadiusM = 1.2f;
            _definition.maxRangeM = 8f;
            _definition.moisturePerLiter = 1.4f;

            _hose = new HoseTool(_definition);
            _stamps.Clear();
            _hose.OnEquip(default);
        }

        [TearDown]
        public void TearDown()
        {
            _grid.Dispose();
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_stadium);
        }

        // A look ray from 1.6 m up, aimed down at world XZ (targetX, targetZ).
        private ToolContext ContextAimingAt(float targetX, float targetZ)
        {
            var origin = new Vector3(targetX, 1.6f, targetZ);
            var ray = new Ray(origin, Vector3.down);
            return new ToolContext(origin, Vector3.forward, ray, _grid, stamp => _stamps.Add(stamp));
        }

        private byte MoistureAt(float x, float z)
        {
            Assert.That(_grid.TryWorldToCell(new Vector3(x, 0f, z), out int cx, out int cy), Is.True);
            return _grid.Cells[_grid.IndexOf(cx, cy)].Moisture;
        }

        [Test]
        public void UseHeld_AddsMoistureToTheAimedDisc()
        {
            // 0.5 s of spray at 5 L/s * 1.4 = 3.5 moisture/cell.
            _hose.OnUseHeld(ContextAimingAt(0f, 0f), 0.5f);

            Assert.That((int)MoistureAt(0f, 0f), Is.GreaterThan(0));
            Assert.That((int)MoistureAt(0f, 0f), Is.EqualTo(4).Within(1));
            Assert.That((int)MoistureAt(20f, 20f), Is.EqualTo(0), "Far cell stays dry.");
        }

        [Test]
        public void UseHeld_ClampsMoistureAt100()
        {
            for (int i = 0; i < 200; i++)
            {
                _hose.OnUseHeld(ContextAimingAt(0f, 0f), 0.5f);
            }
            Assert.That((int)MoistureAt(0f, 0f), Is.EqualTo(100));
        }

        [Test]
        public void UseHeld_MarksMoistureDirty_AndWritesNoGpuStamp()
        {
            // The visual moisture channel is driven from the CPU grid by the
            // render bridge; an independent GPU stamp let the screen show
            // more water than the simulation held (M.3 sync bug).
            _grid.ClearMoistureDirty();

            _hose.OnUseHeld(ContextAimingAt(0f, 0f), 0.5f);

            Assert.That(_grid.MoistureDirty, Is.True);
            Assert.That(_stamps, Is.Empty);
        }

        [Test]
        public void UseHeld_SubUnitFrames_AccumulateInsteadOfFlooringToZero()
        {
            // 60 fps frames at 5 L/s * 1.4 = 0.117 moisture/frame each; the
            // fractional debt must carry so sustained spray still wets.
            for (int i = 0; i < 60; i++)
            {
                _hose.OnUseHeld(ContextAimingAt(0f, 0f), 1f / 60f);
            }
            Assert.That((int)MoistureAt(0f, 0f), Is.EqualTo(7).Within(1));
        }

        [Test]
        public void UseHeld_AimingUp_DoesNotSpray()
        {
            var origin = new Vector3(0f, 1.6f, 0f);
            var ray = new Ray(origin, Vector3.up); // looking at the sky
            var ctx = new ToolContext(origin, Vector3.forward, ray, _grid, stamp => _stamps.Add(stamp));

            _hose.OnUseHeld(ctx, 0.5f);

            Assert.That(_hose.IsSpraying, Is.False);
            Assert.That(_stamps, Is.Empty);
        }

        [Test]
        public void UseHeld_BeyondRange_DoesNotSpray()
        {
            // Shallow ray from high up so the ground hit is > maxRange away.
            var origin = new Vector3(0f, 1.6f, 0f);
            var ray = new Ray(origin, new Vector3(1f, -0.1f, 0f).normalized);
            var ctx = new ToolContext(origin, Vector3.forward, ray, _grid, stamp => _stamps.Add(stamp));

            _hose.OnUseHeld(ctx, 0.5f);

            Assert.That(_hose.IsSpraying, Is.False);
            Assert.That(_stamps, Is.Empty);
        }

        [Test]
        public void CyclePressure_ClampsAtBothEnds()
        {
            Assert.That(_hose.CurrentFlowLPerSec, Is.EqualTo(5f));
            _hose.CyclePressure(1);
            _hose.CyclePressure(1);
            Assert.That(_hose.CurrentFlowLPerSec, Is.EqualTo(9f));
            _hose.CyclePressure(-5);
            Assert.That(_hose.CurrentFlowLPerSec, Is.EqualTo(2f));
        }
    }
}
