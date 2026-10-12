using UnityEngine;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Tuning data for the instanced grass renderer (TDD §3.3). All densities,
    /// distances and colors live here so T1 perf tuning never touches code
    /// (data-in-SO rule, TDD §1.2).
    /// </summary>
    [CreateAssetMenu(menuName = "Greenkeeper/Grass Render Settings", fileName = "GrassRender_New")]
    public sealed class GrassRenderSettingsSO : ScriptableObject
    {
        [Header("Tiling")]
        public float tileSizeM = 8f;
        [Tooltip("Hard cap per tile so a bad density value cannot explode the draw call.")]
        public int maxInstancesPerTile = 24000;

        [Header("LOD bands (distance from camera, meters)")]
        public float nearEndM = 18f;
        public float midEndM = 42f;
        public float farEndM = 70f;

        [Header("Density (blades per m²)")]
        public float nearDensityPerM2 = 250f;
        public float midDensityPerM2 = 60f;
        [Tooltip("Blade width multiplier reached at the mid band end; fewer blades stay visually dense.")]
        public float midWidthScale = 2.2f;

        [Header("Blade shape (meters)")]
        [Tooltip("World height of a blade when the RT height channel is at maximum (60 mm logical). Slight visual exaggeration over the logical value is intended.")]
        public float bladeHeightAtMaxM = 0.09f;
        public float bladeWidthM = 0.014f;
        [Range(0f, 0.5f)] public float bladeHeightJitter = 0.2f;
        [Range(0f, 0.5f)] public float bladeWidthJitter = 0.25f;

        [Header("Color (sampled health/moisture mapping)")]
        public Color healthyColor = new Color(0.20f, 0.50f, 0.14f);
        public Color dryColor = new Color(0.55f, 0.52f, 0.18f);
        [Tooltip("Multiplier applied at full moisture; wet grass reads darker and richer.")]
        [Range(0.5f, 1f)] public float moistureDarken = 0.85f;
        [Tooltip("Root-to-tip ambient occlusion: brightness multiplier at the blade root.")]
        [Range(0f, 1f)] public float rootDarken = 0.55f;

        [Header("Damage")]
        [Tooltip("RT damage channel value above which a blade is culled (bald spot).")]
        [Range(0f, 1f)] public float damageDiscardThreshold = 0.5f;

        [Header("Moisture dose visuals (M.3, TDD §4.4, GDD §3.1.4)")]
        [Tooltip("Moisture (0-1) above which standing water patches form on the ground.")]
        [Range(0f, 1f)] public float puddleThreshold = 0.85f;
        [Tooltip("Moisture below this reads as dried (shifts toward the dry tint).")]
        [Range(0f, 1f)] public float dryThreshold = 0.30f;
        public Color moistureDryColor = new Color(0.62f, 0.55f, 0.28f);

        [Tooltip("Moisture where the wet look (darker, more saturated, glossy) starts ramping in. Just above the flat starting/overnight moisture (0.50 ± 0.03 jitter) so untouched grass reads dry.")]
        [Range(0f, 1f)] public float wetStart = 0.52f;
        [Tooltip("Moisture where the wet look is complete. Keep it at or below the puddle threshold so every cell above it is fully wet whether or not water stands on it; lower = a short spray already reads clearly wet.")]
        [Range(0f, 1f)] public float wetFull = 0.75f;
        [Tooltip("Brightness multiplier of fully wet grass (ramped by the wet look, not by raw moisture).")]
        [Range(0.2f, 1f)] public float wetDarken = 0.4f;
        [Tooltip("Extra saturation of fully wet grass (0 = none). Wet grass reads deeper green, never lighter.")]
        [Range(0f, 1f)] public float wetSaturation = 0.35f;
        [Tooltip("Strength of the broad wet sheen (visible from most view angles; tinted by the grass, never white).")]
        [Range(0f, 2f)] public float wetGloss = 0.5f;
        [Tooltip("Broadness of the wet sheen lobe (lower = wider, visible at more angles).")]
        [Range(1f, 32f)] public float wetSheenPower = 6f;

        [Tooltip("Deep water tone the body fades toward (dark).")]
        public Color puddleWaterColor = new Color(0.04f, 0.06f, 0.06f);
        [Tooltip("How much the water body hides the soaked ground beneath: 0 = clear shallow water, 1 = opaque dark water.")]
        [Range(0f, 1f)] public float puddleOpacity = 0.55f;
        [Tooltip("Sky color reflected by standing water. Fresnel keeps it faint when looking down, strong at grazing angles.")]
        public Color puddleSkyColor = new Color(0.45f, 0.55f, 0.65f);
        [Tooltip("Sky reflection strength (multiplies the fresnel term).")]
        [Range(0f, 2f)] public float puddleReflection = 1f;
        [Tooltip("Sun glint strength — glints appear only on ripple crests.")]
        [Range(0f, 4f)] public float puddleSpecular = 1.2f;
        [Tooltip("Sun glint tightness (higher = smaller sparkle points).")]
        [Range(4f, 256f)] public float puddleGlintPower = 140f;
        [Tooltip("Surface ripple: how much noise tilts the water normal (varies glints/reflection).")]
        [Range(0f, 1f)] public float puddleRipple = 0.2f;
        [Tooltip("Size of the water patch noise in meters (patches, not a disc).")]
        public float puddleNoiseScaleM = 0.9f;

        [Header("Puddle coverage curve (moisture → fraction of area under water)")]
        [Tooltip("Approximate covered fraction right at the puddle threshold (scattered patches).")]
        [Range(0f, 1f)] public float puddleCoverageAtThreshold = 0.12f;
        [Tooltip("Approximate covered fraction at moisture 100 (near-continuous sheet).")]
        [Range(0f, 1f)] public float puddleCoverageAtFull = 0.95f;
        [Tooltip("Curve shape between the two: 1 = linear, >1 = water spreads late, <1 = spreads early.")]
        [Range(0.25f, 4f)] public float puddleCoverageExponent = 1.5f;

        [Header("Pattern / stripes (F0.6 — the signature visual, GDD §3.1.2)")]
        [Tooltip("Blade tip offset along the mow direction, as a fraction of blade height.")]
        [Range(0f, 1f)] public float bendAmount01 = 0.45f;
        [Tooltip("How strongly the lighting normal tilts against the bend direction.")]
        [Range(0f, 1f)] public float bendNormalWeight = 0.6f;
        [Range(0f, 1f)] public float bladeSpecStrength = 0.35f;
        public float bladeSpecPower = 24f;
        [Tooltip("Direct view-dependent brightness term on stripes (F0.7 contrast polish), applied to blades and ground alike.")]
        [Range(0f, 0.5f)] public float stripeAlbedoGain = 0.18f;
        [Tooltip("Stripe lighting contribution on the far-LOD ground (stripes must read at distance, TDD §3.3).")]
        [Range(0f, 1f)] public float groundStripeContrast = 0.5f;
        [Range(0f, 1f)] public float groundSpecStrength = 0.2f;
        public float groundSpecPower = 16f;

        [Header("Idle sway (cheap vertex-only)")]
        public float swayAmplitudeM = 0.008f;
        public float swayFrequency = 1.6f;

        private void OnValidate()
        {
            tileSizeM = Mathf.Max(tileSizeM, 1f);
            nearEndM = Mathf.Max(nearEndM, 0f);
            midEndM = Mathf.Max(midEndM, nearEndM);
            farEndM = Mathf.Max(farEndM, midEndM);
            nearDensityPerM2 = Mathf.Max(nearDensityPerM2, 0f);
            midDensityPerM2 = Mathf.Clamp(midDensityPerM2, 0f, nearDensityPerM2);
            maxInstancesPerTile = Mathf.Max(maxInstancesPerTile, 0);
            bladeSpecPower = Mathf.Max(bladeSpecPower, 1f);
            groundSpecPower = Mathf.Max(groundSpecPower, 1f);
        }
    }
}
