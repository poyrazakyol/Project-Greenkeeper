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
        }
    }
}
