using System.Diagnostics;
using Greenkeeper.Core;
using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Scene host of the CPU pitch grid: owns allocation/disposal and runs the
    /// 4 Hz in-day tick (TDD §3.4). Tick cost is measured every tick and
    /// exposed read-only for the perf HUD (budget: 0.5 ms, TDD §1.3).
    /// </summary>
    public sealed class PitchController : MonoBehaviour
    {
        [SerializeField] private StadiumDefinitionSO _stadium;
        [Tooltip("Core time service: the tick follows GAME time (accelerable), not real time. Optional — falls back to real time when absent.")]
        [SerializeField] private TimeSystem _timeSystem;
        [SerializeField] private float _tickIntervalSeconds = 0.25f;

        [Tooltip("Moisture units (0-100 scale) evaporated per game-time second. Placeholder rate; becomes weather-driven data in Phase 2. Runs on ScaledDeltaTime, so it stops at the evening hold (one day's worth max).")]
        [SerializeField] private float _moistureEvaporationPerSecond = 0.1f;

        [Tooltip("In-day moisture diffusion strength per tick (TDD §3.4 irrigation spread). 0 disables it.")]
        [Range(0f, 1f)]
        [SerializeField] private float _moistureDiffusionRate = 0.12f;

        public PitchGrid Grid { get; private set; }
        public StadiumDefinitionSO Stadium => _stadium;
        public double LastTickMs { get; private set; }
        public long TickCount { get; private set; }

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private float _tickTimer;
        private float _evaporationDebt;
        // Managed double buffer for the diffusion stencil (no per-access
        // NativeArray safety checks — see PitchMoistureSim).
        private byte[] _moistureBefore;
        private byte[] _moistureAfter;

        private void Awake()
        {
            Grid = PitchGrid.CreateCentered(_stadium);
            PitchGridInitializer.Initialize(Grid, _stadium.ToInitParams());
            _moistureBefore = new byte[Grid.CellCount];
            _moistureAfter = new byte[Grid.CellCount];
        }

        private void Update()
        {
            _tickTimer += _timeSystem != null ? _timeSystem.ScaledDeltaTime : Time.deltaTime;
            while (_tickTimer >= _tickIntervalSeconds)
            {
                _tickTimer -= _tickIntervalSeconds;
                RunTick();
            }
        }

        private void RunTick()
        {
            // Moisture is byte-quantized; accumulate fractional evaporation and
            // apply only whole units so slow rates do not round up to 1/tick.
            _evaporationDebt += _moistureEvaporationPerSecond * _tickIntervalSeconds;
            int moistureLoss = (int)_evaporationDebt;
            _evaporationDebt -= moistureLoss;

            var tickParams = new PitchTickParams { MoistureLoss = moistureLoss };

            _stopwatch.Restart();
            var cells = Grid.Cells;
            bool diffuse = _moistureDiffusionRate > 0f;
            for (int i = 0; i < cells.Length; i++)
            {
                CellState next = PitchTick.Step(cells[i], tickParams);
                cells[i] = next;
                // Snapshot for the diffusion stencil, folded into this pass so
                // it costs no extra NativeArray access.
                _moistureBefore[i] = next.Moisture;
            }
            int diffusedCells = 0;
            if (diffuse)
            {
                // Irrigation spread (TDD §3.4): softens spray edges, bleeds
                // over-watered spots. Only changed cells are written back.
                PitchMoistureSim.Diffuse(_moistureBefore, _moistureAfter, Grid.Width, Grid.Height, _moistureDiffusionRate);
                diffusedCells = PitchMoistureSim.ApplyMoisture(cells, _moistureBefore, _moistureAfter);
            }
            // TDD §3.4: flag the visual tint refresh only when moisture moved.
            if (moistureLoss > 0 || diffusedCells > 0)
            {
                Grid.MarkMoistureDirty();
            }
            _stopwatch.Stop();

            LastTickMs = _stopwatch.Elapsed.TotalMilliseconds;
            TickCount++;
        }

        private void OnDestroy()
        {
            Grid?.Dispose();
        }
    }
}
