using System.Collections.Generic;
using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Push line marker (TDD §4.3). While held it sweeps a thin capsule under
    /// the cart and writes both layers from that one stroke: CPU PaintCoverage
    /// (the authority the inspector scores against, via the recorded polyline)
    /// and a GPU RT_Paint stamp (visual only). The smoothness/completeness
    /// score is computed from Strokes by LineSmoothnessScorer — never from the
    /// RT (CPU/GPU authority split, TDD §3.1).
    /// </summary>
    public sealed class LinerTool : ITool
    {
        public ToolDefinitionSO Definition => null; // liner uses LinerToolDefinitionSO
        public LinerToolDefinitionSO LinerDefinition { get; }

        /// <summary>Recorded line passes — scorer + save (M.8) input.</summary>
        public IReadOnlyList<LineStroke> Strokes => _strokes;

        private readonly List<LineStroke> _strokes = new List<LineStroke>();
        private readonly List<Vector2Int> _cellBuffer = new List<Vector2Int>(64);
        private LineStroke _currentStroke;
        private bool _hasPreviousPosition;
        private Vector3 _previousPosition;

        public LinerTool(LinerToolDefinitionSO definition)
        {
            LinerDefinition = definition;
        }

        public void OnEquip(ToolContext ctx)
        {
            _hasPreviousPosition = false;
            _currentStroke = null;
        }

        public void OnUseHeld(ToolContext ctx, float dt)
        {
            Vector3 cart = CartPosition(ctx);
            if (!_hasPreviousPosition)
            {
                _previousPosition = cart;
                _hasPreviousPosition = true;
            }
            if (_currentStroke == null)
            {
                _currentStroke = new LineStroke();
                _strokes.Add(_currentStroke);
                RecordPoint(cart);
            }

            PaintCells(ctx, _previousPosition, cart);
            EmitStamp(ctx, cart);
            RecordPoint(cart);

            _previousPosition = cart;
        }

        public void OnUseReleased(ToolContext ctx)
        {
            // A short tap still leaves a one-point stroke; drop empty ones.
            if (_currentStroke != null && _currentStroke.Points.Count == 0)
            {
                _strokes.Remove(_currentStroke);
            }
            _currentStroke = null;
            _hasPreviousPosition = false;
        }

        public void OnUnequip()
        {
            OnUseReleased(default);
        }

        public void ClearStrokes()
        {
            _strokes.Clear();
            _currentStroke = null;
            _hasPreviousPosition = false;
        }

        private Vector3 CartPosition(in ToolContext ctx)
        {
            var forwardXz = new Vector3(ctx.PlayerForward.x, 0f, ctx.PlayerForward.z);
            forwardXz = forwardXz.sqrMagnitude < 1e-6f ? Vector3.forward : forwardXz.normalized;
            return ctx.PlayerPosition + forwardXz * LinerDefinition.forwardOffsetM;
        }

        private void PaintCells(in ToolContext ctx, Vector3 from, Vector3 to)
        {
            float radius = LinerDefinition.lineWidthM * 0.5f;
            CapsuleCells.Collect(ctx.Grid, from, to, radius, _cellBuffer);

            var cells = ctx.Grid.Cells;
            var strength = (byte)LinerDefinition.paintStrength;
            for (int i = 0; i < _cellBuffer.Count; i++)
            {
                int index = ctx.Grid.IndexOf(_cellBuffer[i].x, _cellBuffer[i].y);
                CellState cell = cells[index];
                if (strength > cell.PaintCoverage)
                {
                    cell.PaintCoverage = strength;
                    cells[index] = cell;
                }
            }
        }

        private void EmitStamp(in ToolContext ctx, Vector3 position)
        {
            // RT_Paint is R8; default alpha blend converges to full opacity on
            // repeated passes, so no special blend op is needed.
            ctx.EnqueueStamp(new StampRequest
            {
                Target = PitchRenderTarget.Paint,
                WorldPosition = position,
                WorldRadius = LinerDefinition.lineWidthM * 0.5f,
                Value = Color.white,
                Hardness = 0.95f,
                Mask = ColorWriteMask.Red
            });
        }

        private void RecordPoint(Vector3 worldPosition)
        {
            var point = new Vector2(worldPosition.x, worldPosition.z);
            List<Vector2> points = _currentStroke.Points;
            if (points.Count > 0)
            {
                float spacing = LinerDefinition.minStrokePointSpacingM;
                if ((points[points.Count - 1] - point).sqrMagnitude < spacing * spacing)
                {
                    return;
                }
            }
            points.Add(point);
        }
    }
}
