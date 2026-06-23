namespace Greenkeeper.Core
{
    /// <summary>
    /// Payload of the OnDayEnded channel (TDD §5.2): plain-data night summary
    /// shown on the morning card ("Gece: çim +1.2 mm uzadı, 3 bölge kurudu").
    /// Lives in Core so every assembly can consume it; produced by the pitch
    /// day-end simulation, stamped with the day by the pipeline orchestrator.
    /// </summary>
    public struct DayReport
    {
        /// <summary>The day that just ended (1-based).</summary>
        public int Day;

        public float AverageGrowthMm;
        public int DriedCellCount;
        public float AverageMoistureDelta;
    }
}
