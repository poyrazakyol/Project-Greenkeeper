using System.Collections.Generic;
using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Push mower (TDD §4.2). Each held frame sweeps a capsule from the
    /// previous to the current deck position and writes both layers from that
    /// one stroke: CPU cells via MowSweep (height, pattern dir, scalping) and
    /// GPU stamps (height to RT_GrassState.R with Min blend so the stamp can
    /// only shorten; direction to RT_Pattern.RG). Clippings fill the basket;
    /// a full basket stops cutting until emptied (hold-interact micro task —
    /// Phase 0 empties in place, a bin object is considered at MVP).
    /// </summary>
    public sealed class MowerTool : ITool
    {
        public ToolDefinitionSO Definition { get; }

        public float CurrentCutHeightMm => Definition.cutHeightStepsMm[_stepIndex];
        public float BasketFill01 => Definition.basketCapacityL <= 0f
            ? 0f
            : Mathf.Clamp01(_basketFillL / Definition.basketCapacityL);
        public bool BasketFull => _basketFillL >= Definition.basketCapacityL;
        public float EmptyHoldProgress01 => Definition.basketEmptySeconds <= 0f
            ? 1f
            : Mathf.Clamp01(_emptyHoldSeconds / Definition.basketEmptySeconds);

        private readonly List<Vector2Int> _cellBuffer = new List<Vector2Int>(256);
        private int _stepIndex;
        private float _basketFillL;
        private float _emptyHoldSeconds;
        private bool _hasPreviousPosition;
        private Vector3 _previousPosition;

        public MowerTool(ToolDefinitionSO definition)
        {
            Definition = definition;
            _stepIndex = Mathf.Clamp(definition.defaultStepIndex, 0, definition.cutHeightStepsMm.Length - 1);
        }

        public void OnEquip(ToolContext ctx)
        {
            _hasPreviousPosition = false;
        }

        public void OnUseHeld(ToolContext ctx, float dt)
        {
            Vector3 deckPosition = DeckPosition(ctx);
            if (!_hasPreviousPosition)
            {
                _previousPosition = deckPosition;
                _hasPreviousPosition = true;
            }

            if (!BasketFull)
            {
                Sweep(ctx, _previousPosition, deckPosition);
            }
            _previousPosition = deckPosition;
        }

        public void OnUseReleased(ToolContext ctx)
        {
            // Next press starts a fresh stroke instead of sweeping the gap.
            _hasPreviousPosition = false;
        }

        public void OnUnequip()
        {
            _hasPreviousPosition = false;
            _emptyHoldSeconds = 0f;
        }

        /// <summary>Scroll/Q-E height step cycling, clamped at the ends.</summary>
        public void CycleHeightStep(int delta)
        {
            _stepIndex = Mathf.Clamp(_stepIndex + delta, 0, Definition.cutHeightStepsMm.Length - 1);
        }

        /// <summary>
        /// Advances the hold-to-empty micro task; call with the held time delta
        /// and reset via <see cref="ResetEmptyHold"/> when the key is released.
        /// Returns true the moment the basket empties.
        /// </summary>
        public bool TickEmptyHold(float dt)
        {
            if (_basketFillL <= 0f)
            {
                return false;
            }
            _emptyHoldSeconds += dt;
            if (_emptyHoldSeconds >= Definition.basketEmptySeconds)
            {
                _basketFillL = 0f;
                _emptyHoldSeconds = 0f;
                return true;
            }
            return false;
        }

        public void ResetEmptyHold()
        {
            _emptyHoldSeconds = 0f;
        }

        private Vector3 DeckPosition(in ToolContext ctx)
        {
            var forwardXz = new Vector3(ctx.PlayerForward.x, 0f, ctx.PlayerForward.z);
            forwardXz = forwardXz.sqrMagnitude < 1e-6f ? Vector3.forward : forwardXz.normalized;
            return ctx.PlayerPosition + forwardXz * Definition.mowForwardOffsetM;
        }

        private void Sweep(in ToolContext ctx, Vector3 from, Vector3 to)
        {
            float radius = Definition.mowWidthM * 0.5f;
            var moveXz = new Vector2(to.x - from.x, to.z - from.z);
            Vector2 moveDir = moveXz.sqrMagnitude < 1e-8f
                ? new Vector2(ctx.PlayerForward.x, ctx.PlayerForward.z).normalized
                : moveXz.normalized;

            var cutParams = new MowCutParams
            {
                CutHeightMm = CurrentCutHeightMm,
                MoveDir = moveDir,
                ScalpingHeightThresholdMm = Definition.scalpingHeightThresholdMm,
                ScalpingCutThresholdMm = Definition.scalpingCutThresholdMm,
                ScalpingHealthDamage = (byte)Definition.scalpingHealthDamage
            };

            // CPU layer: the simulation/scoring authority.
            MowSweep.CollectCells(ctx.Grid, from, to, radius, _cellBuffer);
            var cells = ctx.Grid.Cells;
            float totalCutMm = 0f;
            for (int i = 0; i < _cellBuffer.Count; i++)
            {
                int index = ctx.Grid.IndexOf(_cellBuffer[i].x, _cellBuffer[i].y);
                MowCutResult result = MowSweep.ApplyCut(cells[index], cutParams);
                cells[index] = result.Cell;
                totalCutMm += result.CutMm;
            }

            float cellAreaM2 = ctx.Grid.CellSize * ctx.Grid.CellSize;
            _basketFillL = Mathf.Min(
                Definition.basketCapacityL,
                _basketFillL + totalCutMm * cellAreaM2 * Definition.clippingsLPerM2PerMm);

            // GPU layer, same stroke (TDD §3.1 dual write, no readback).
            EmitStamps(ctx, from, to, radius, moveDir);
        }

        private void EmitStamps(in ToolContext ctx, Vector3 from, Vector3 to, float radius, Vector2 moveDir)
        {
            float heightNorm = CurrentCutHeightMm / GrassStateEncoding.MaxGrassHeightMm;
            float segmentLength = Vector3.Distance(from, to);
            float spacing = Mathf.Max(radius * 0.7f, 0.01f);
            int stampCount = Mathf.Max(1, Mathf.CeilToInt(segmentLength / spacing) + 1);

            for (int i = 0; i < stampCount; i++)
            {
                Vector3 position = stampCount == 1
                    ? to
                    : Vector3.Lerp(from, to, i / (float)(stampCount - 1));

                // Height: Min blend can only shorten — the GPU mirror of the
                // "write only if current > setting" rule.
                ctx.EnqueueStamp(new StampRequest
                {
                    Target = PitchRenderTarget.GrassState,
                    WorldPosition = position,
                    WorldRadius = radius,
                    Value = new Color(heightNorm, 1f, 1f, 1f),
                    Hardness = 0.9f,
                    Mask = ColorWriteMask.Red,
                    BlendOp = BlendOp.Min
                });

                // Pattern: the roller lays direction wherever it passes (the
                // CPU records it only on cut cells — scoring vs. visuals).
                ctx.EnqueueStamp(new StampRequest
                {
                    Target = PitchRenderTarget.Pattern,
                    WorldPosition = position,
                    WorldRadius = radius,
                    Value = new Color(moveDir.x, moveDir.y, 0f, 1f),
                    Hardness = 0.8f,
                    Mask = ColorWriteMask.Red | ColorWriteMask.Green,
                    BlendOp = BlendOp.Add
                });
            }
        }
    }
}
