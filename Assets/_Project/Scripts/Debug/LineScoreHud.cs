using System.Collections.Generic;
using System.Text;
using Greenkeeper.Gameplay;
using Greenkeeper.Pitch;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Greenkeeper.Debugging
{
    /// <summary>
    /// Line score debug view (M.2): L runs the pure smoothness/completeness
    /// scorer (TDD §4.3/§6.2) over the liner's recorded strokes and shows the
    /// result per element type — the editor-side way to validate scoring until
    /// the inspection report card lands at M.4. C clears the recorded strokes.
    /// </summary>
    public sealed class LineScoreHud : MonoBehaviour
    {
        [SerializeField] private ToolController _toolController;
        [SerializeField] private LineLayoutSO _lineLayout;
        [SerializeField] private PitchLineGuide _lineGuide;

        // [H] guide override steps (M.3.5): follow the tool, then forced
        // off / weak / strong to preview higher-tier guidance.
        private static readonly float?[] GuideOverrides = { null, 0f, 0.35f, 1f };
        private static readonly string[] GuideOverrideNames = { "alet", "kapalı", "zayıf", "güçlü" };
        private int _guideOverrideIndex;
        private bool _linerEquippedSent;

        private string _report = "[L] çizgi skoru  [C] temizle";

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _toolController == null || _toolController.Liner == null)
            {
                return;
            }

            if (keyboard.lKey.wasPressedThisFrame)
            {
                RunScore();
            }
            if (keyboard.cKey.wasPressedThisFrame)
            {
                _toolController.Liner.ClearStrokes();
                _report = "Stroke kaydı temizlendi.";
            }
            if (_lineGuide != null)
            {
                // The override is a liner preview: re-sent whenever the step
                // or the equipped tool changes, so leaving the liner drops it.
                bool linerEquipped = _toolController.CurrentTool == _toolController.Liner;
                bool stepChanged = keyboard.hKey.wasPressedThisFrame;
                if (stepChanged)
                {
                    _guideOverrideIndex = (_guideOverrideIndex + 1) % GuideOverrides.Length;
                }
                if (stepChanged || linerEquipped != _linerEquippedSent)
                {
                    _linerEquippedSent = linerEquipped;
                    _lineGuide.SetDebugOverride(GuideOverrides[_guideOverrideIndex], linerEquipped);
                }
            }
        }

        private void RunScore()
        {
            IReadOnlyList<LineStroke> strokes = _toolController.Liner.Strokes;
            IdealLine[] ideal = _lineLayout.BuildIdeal();
            LineScoreReport report = LineSmoothnessScorer.Score(strokes, ideal, _lineLayout.ToScoreParams());

            // Aggregate per element type for a compact readout.
            var avgDev = new Dictionary<LineElementType, float>();
            var maxDev = new Dictionary<LineElementType, float>();
            var cov = new Dictionary<LineElementType, float>();
            var count = new Dictionary<LineElementType, int>();
            foreach (LineElementScore e in report.Elements)
            {
                avgDev.TryGetValue(e.Type, out float a);
                maxDev.TryGetValue(e.Type, out float m);
                cov.TryGetValue(e.Type, out float c);
                count.TryGetValue(e.Type, out int n);
                avgDev[e.Type] = a + e.AvgDeviationCm;
                maxDev[e.Type] = Mathf.Max(m, e.MaxDeviationCm);
                cov[e.Type] = c + e.CoverageRatio;
                count[e.Type] = n + 1;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"GENEL: kapsama %{report.OverallCoverage * 100f:F0}  ort.sapma {report.OverallAvgDeviationCm:F1} cm  başıboş {report.StrayPointCount}");
            foreach (KeyValuePair<LineElementType, int> kv in count)
            {
                LineElementType t = kv.Key;
                int n = kv.Value;
                sb.AppendLine($"{t}: kapsama %{cov[t] / n * 100f:F0}  ort {avgDev[t] / n:F1} cm  maks {maxDev[t]:F1} cm");
            }
            _report = sb.ToString();
        }

        private void OnGUI()
        {
            string tool = _toolController != null ? _toolController.CurrentToolName : "-";
            int strokeCount = _toolController != null && _toolController.Liner != null
                ? _toolController.Liner.Strokes.Count
                : 0;

            bool linerActive = _toolController != null && _toolController.CurrentTool == _toolController.Liner;
            string guideMode = linerActive
                ? GuideOverrideNames[_guideOverrideIndex]
                : $"liner değil; [H] seçimi: {GuideOverrideNames[_guideOverrideIndex]}";
            string guide = _lineGuide != null
                ? $"Rehber: %{_lineGuide.EffectiveStrength * 100f:F0} ({guideMode})  [H]   pişirme {_lineGuide.LastBakeMs:F1} ms"
                : "Rehber: -";

            var box = new Rect(8f, Screen.height - 222f, 460f, 182f);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x + 8f, box.y + 6f, box.width - 16f, 20f),
                $"Alet: {tool}  ([Q] değiştir)   Stroke: {strokeCount}");
            GUI.Label(new Rect(box.x + 8f, box.y + 28f, box.width - 16f, 20f), guide);
            GUI.Label(new Rect(box.x + 8f, box.y + 50f, box.width - 16f, 128f), _report);
        }
    }
}
