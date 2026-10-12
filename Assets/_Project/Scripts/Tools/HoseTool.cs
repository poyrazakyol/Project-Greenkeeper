using System.Collections.Generic;
using Greenkeeper.Pitch;
using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Hand hose (TDD §4.4). While held it sprays the ground disc the player
    /// looks at and writes CPU Moisture — the authority the dose mechanic and
    /// inspector read — then marks the grid's moisture dirty. The visual
    /// moisture channel (RT_GrassState.B) is refreshed from the CPU grid by
    /// PitchRenderBridge, so the screen can never show more water than the
    /// simulation holds (no independent GPU stamp). Pressure (scroll) sets
    /// the flow rate.
    /// </summary>
    public sealed class HoseTool : ITool
    {
        public ToolDefinitionSO Definition => null; // hose uses HoseToolDefinitionSO
        public HoseToolDefinitionSO HoseDefinition { get; }

        public float CurrentFlowLPerSec => HoseDefinition.pressureStepsLPerSec[_stepIndex];
        public bool IsSpraying { get; private set; }
        public float SmoothedSprayRate01 { get; private set; }

        private readonly List<Vector2Int> _cellBuffer = new List<Vector2Int>(128);
        private int _stepIndex;
        private float _moistureDebt;

        public HoseTool(HoseToolDefinitionSO definition)
        {
            HoseDefinition = definition;
            _stepIndex = Mathf.Clamp(definition.defaultStepIndex, 0, definition.pressureStepsLPerSec.Length - 1);
        }

        public void OnEquip(ToolContext ctx) { }

        public void OnUseHeld(ToolContext ctx, float dt)
        {
            IsSpraying = false;
            if (!TryGroundHit(ctx.LookRay, out Vector3 hit))
            {
                SmoothedSprayRate01 = Mathf.Lerp(SmoothedSprayRate01, 0f, Mathf.Clamp01(dt / 0.2f));
                return;
            }

            IsSpraying = true;
            SprayDisc(ctx, hit, dt);

            float blend = Mathf.Clamp01(dt / 0.2f);
            SmoothedSprayRate01 = Mathf.Lerp(SmoothedSprayRate01, 1f, blend);
        }

        public void OnUseReleased(ToolContext ctx)
        {
            IsSpraying = false;
            SmoothedSprayRate01 = 0f;
            _moistureDebt = 0f;
        }

        public void OnUnequip()
        {
            IsSpraying = false;
            SmoothedSprayRate01 = 0f;
            _moistureDebt = 0f;
        }

        /// <summary>Scroll pressure step cycling, clamped at the ends.</summary>
        public void CyclePressure(int delta)
        {
            _stepIndex = Mathf.Clamp(_stepIndex + delta, 0, HoseDefinition.pressureStepsLPerSec.Length - 1);
        }

        /// <summary>
        /// Projects the aim ray onto the ground plane (Y = 0). Returns false
        /// when looking up or beyond range — no spray into the sky/distance.
        /// </summary>
        private bool TryGroundHit(Ray ray, out Vector3 hit)
        {
            hit = default;
            if (ray.direction.y >= -1e-4f)
            {
                return false;
            }
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f || t > HoseDefinition.maxRangeM)
            {
                return false;
            }
            hit = ray.origin + ray.direction * t;
            return true;
        }

        private void SprayDisc(in ToolContext ctx, Vector3 center, float dt)
        {
            // CPU layer: the authority the dose mechanic / inspector reads.
            // Moisture is byte-quantized; accumulate the fractional add so a
            // small per-frame amount (flow*dt at 60 fps) is not floored to 0.
            _moistureDebt += CurrentFlowLPerSec * HoseDefinition.moisturePerLiter * dt;
            int add = (int)_moistureDebt;
            _moistureDebt -= add;
            if (add > 0)
            {
                CapsuleCells.Collect(ctx.Grid, center, center, HoseDefinition.coneRadiusM, _cellBuffer);
                var cells = ctx.Grid.Cells;
                for (int i = 0; i < _cellBuffer.Count; i++)
                {
                    int index = ctx.Grid.IndexOf(_cellBuffer[i].x, _cellBuffer[i].y);
                    CellState cell = cells[index];
                    cell.Moisture = (byte)Mathf.Min(100, cell.Moisture + add);
                    cells[index] = cell;
                }
                ctx.Grid.MarkMoistureDirty();
            }
        }
    }
}
