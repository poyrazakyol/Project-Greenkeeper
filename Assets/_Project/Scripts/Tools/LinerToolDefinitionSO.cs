using UnityEngine;

namespace Greenkeeper.Tools
{
    /// <summary>
    /// Line-marker stats (TDD §4.3, §5.3). A dedicated SO (not the mower's
    /// ToolDefinitionSO) by decision: liner stats — line width, paint strength,
    /// stroke recording — share nothing with the mower's cut steps/basket;
    /// merging them would build a god-object. TDD §5.3 does not forbid a
    /// definition type per tool.
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Liner Tool Definition", fileName = "LinerTool_New")]
    public sealed class LinerToolDefinitionSO : ScriptableObject
    {
        public string toolName = "Push Liner";

        [Header("Paint")]
        [Tooltip("Painted line width in meters (FIFA lines are 0.10-0.12 m).")]
        public float lineWidthM = 0.12f;
        [Tooltip("Cart center distance in front of the player.")]
        public float forwardOffsetM = 0.6f;
        [Tooltip("Target paint coverage (0-100) written under the cart.")]
        [Range(0, 100)] public int paintStrength = 100;

        [Header("Stroke recording")]
        [Tooltip("Minimum spacing between recorded stroke points (m); thins the polyline.")]
        public float minStrokePointSpacingM = 0.1f;

        [Header("Line guidance (M.3.5, GDD §4.2 equipment progression)")]
        [Tooltip("Strength of the ghost guide overlay while this liner is equipped: 0 = none, 1 = full. Amateur gear is weak; higher-tier liners ship a higher value — no code change.")]
        [Range(0f, 1f)] public float guidanceStrength01 = 0.35f;

        [Header("Progression (data only — economy logic arrives at M.7)")]
        public int unlockTier = 1;
        public float price = 0f;
    }
}
