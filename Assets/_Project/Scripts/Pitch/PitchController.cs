using System.Diagnostics;
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
        [SerializeField] private float _tickIntervalSeconds = 0.25f;

        [Tooltip("Moisture units (0-100 scale) evaporated per second. Placeholder rate; becomes weather-driven data in Phase 2.")]
        [SerializeField] private float _moistureEvaporationPerSecond = 0.2f;

        public PitchGrid Grid { get; private set; }
        public StadiumDefinitionSO Stadium => _stadium;
        public double LastTickMs { get; private set; }
        public long TickCount { get; private set; }

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private float _tickTimer;
        private float _evaporationDebt;

        private void Awake()
        {
            Grid = PitchGrid.CreateCentered(_stadium);
            PitchGridInitializer.Initialize(Grid, _stadium.ToInitParams());
        }

        private void Update()
        {
            _tickTimer += Time.deltaTime;
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
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = PitchTick.Step(cells[i], tickParams);
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
