using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Resolves how strong the ghost line guide is shown (M.3.5). Pure, so the
    /// rule "guidance belongs to the liner" is test-guarded: the debug
    /// override may only replace the strength while a liner is equipped; with
    /// any other tool the guide follows the tool's own strength (which is 0).
    /// </summary>
    public static class LineGuideStrength
    {
        public static float Resolve(float toolStrength01, float? debugOverride01, bool linerEquipped)
        {
            float strength = linerEquipped && debugOverride01.HasValue
                ? debugOverride01.Value
                : toolStrength01;
            return Mathf.Clamp01(strength);
        }
    }
}
