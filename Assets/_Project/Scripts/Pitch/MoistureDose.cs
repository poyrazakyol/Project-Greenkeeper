using Unity.Collections;

namespace Greenkeeper.Pitch
{
    /// <summary>Dose state of a cell's moisture (TDD §4.4).</summary>
    public enum DoseState
    {
        Dry,
        Ok,
        Puddle
    }

    public struct DoseCounts
    {
        public int Dry;
        public int Ok;
        public int Puddle;
        public float AverageMoisture;
    }

    /// <summary>
    /// Pure moisture dose classification (TDD §4.4: Moisture &lt; 30 dry,
    /// Moisture &gt; 85 puddle). The logical authority the inspector reads
    /// ("su birikintisi hücre sayısı + kuru bölge oranı", TDD §6.2) and the
    /// M.3 debug HUD. UnityEngine-free; EditMode tested.
    /// </summary>
    public static class MoistureDose
    {
        public static DoseState Classify(int moisture, int dryThreshold, int puddleThreshold)
        {
            if (moisture < dryThreshold)
            {
                return DoseState.Dry;
            }
            return moisture > puddleThreshold ? DoseState.Puddle : DoseState.Ok;
        }

        public static DoseCounts CountStates(NativeArray<CellState> cells, int dryThreshold, int puddleThreshold)
        {
            var counts = new DoseCounts();
            long moistureSum = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                int m = cells[i].Moisture;
                moistureSum += m;
                switch (Classify(m, dryThreshold, puddleThreshold))
                {
                    case DoseState.Dry: counts.Dry++; break;
                    case DoseState.Puddle: counts.Puddle++; break;
                    default: counts.Ok++; break;
                }
            }
            counts.AverageMoisture = cells.Length > 0 ? (float)moistureSum / cells.Length : 0f;
            return counts;
        }
    }
}
