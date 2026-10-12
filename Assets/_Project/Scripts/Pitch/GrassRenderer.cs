using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Greenkeeper.Pitch
{
    /// <summary>
    /// Instanced grass blade renderer (TDD §3.3): the pitch is split into
    /// tiles, each visible tile is one instanced draw whose blade positions
    /// are derived in the vertex shader from the instance id — no per-instance
    /// CPU buffers. Blades sample RT_GrassState for height/color; pattern bend
    /// (RT_Pattern) arrives in F0.6. This block is the T1 risk measurement:
    /// budget ≤4 ms GPU (TDD §1.3/§13).
    /// </summary>
    public sealed class GrassRenderer : MonoBehaviour
    {
        [SerializeField] private PitchRenderBridge _bridge;
        [SerializeField] private GrassRenderSettingsSO _settings;
        [SerializeField] private Shader _grassShader;

        /// <summary>Debug toggle (G key via GrassDebugControls) — the honest T1 A/B switch.</summary>
        public bool RenderingEnabled { get; set; } = true;

        /// <summary>Debug LOD band tint (F5 via GrassDebugControls).</summary>
        public bool LodTintEnabled { get; set; }

        public int VisibleTileCount { get; private set; }
        public int TotalInstanceCount { get; private set; }
        public double LastCullMs { get; private set; }

        private static readonly int TileOriginSizeId = Shader.PropertyToID("_TileOriginSize");
        private static readonly int TileSeedId = Shader.PropertyToID("_TileSeed");
        private static readonly int WidthScaleId = Shader.PropertyToID("_WidthScale");
        private static readonly int LodTintColorId = Shader.PropertyToID("_LodTintColor");

        private static readonly Color NearBandTint = new Color(0.2f, 1f, 0.2f);
        private static readonly Color MidBandTint = new Color(1f, 0.9f, 0.2f);

        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        private GrassTile[] _tiles;
        private MaterialPropertyBlock[] _tileProps;
        private Mesh _bladeMesh;
        private Material _material;
        private Camera _camera;

        private void Start()
        {
            float maxBladeHeight = _settings.bladeHeightAtMaxM * (1f + _settings.bladeHeightJitter);
            _tiles = GrassTileLayout.Build(_bridge.Bounds, _settings.tileSizeM, maxBladeHeight);

            _tileProps = new MaterialPropertyBlock[_tiles.Length];
            for (int i = 0; i < _tiles.Length; i++)
            {
                var props = new MaterialPropertyBlock();
                GrassTile tile = _tiles[i];
                props.SetVector(TileOriginSizeId, new Vector4(
                    tile.OriginXz.x, tile.OriginXz.y, tile.SizeXz.x, tile.SizeXz.y));
                props.SetFloat(TileSeedId, i * 7919f); // decorrelate the per-tile hash streams
                _tileProps[i] = props;
            }

            _bladeMesh = CreateBladeMesh();
            Shader shader = _grassShader != null ? _grassShader : Shader.Find("Greenkeeper/GrassBlade");
            _material = new Material(shader);
            ApplySettingsToMaterial();
            PushSharedColorGlobals();
        }

        private void ApplySettingsToMaterial()
        {
            _material.SetFloat("_BladeWidth", _settings.bladeWidthM);
            _material.SetFloat("_BladeHeightAtMax", _settings.bladeHeightAtMaxM);
            _material.SetFloat("_HeightJitter", _settings.bladeHeightJitter);
            _material.SetFloat("_WidthJitter", _settings.bladeWidthJitter);
            _material.SetFloat("_RootDarken", _settings.rootDarken);
            _material.SetFloat("_DamageThreshold", _settings.damageDiscardThreshold);
            _material.SetFloat("_SwayAmp", _settings.swayAmplitudeM);
            _material.SetFloat("_SwayFreq", _settings.swayFrequency);
            _material.SetFloat("_BendAmount", _settings.bendAmount01);
            _material.SetFloat("_BendNormalWeight", _settings.bendNormalWeight);
            _material.SetFloat("_SpecStrength", _settings.bladeSpecStrength);
            _material.SetFloat("_SpecPower", _settings.bladeSpecPower);
            _material.SetFloat("_StripeAlbedoGain", _settings.stripeAlbedoGain);
        }

        private void PushSharedColorGlobals()
        {
            // Shared by GrassBlade and PitchGround so the far band (ground
            // texture takes over, TDD §3.3) matches the blade coloring and
            // stripe response. Ground params go through globals because the
            // ground material is an asset.
            Shader.SetGlobalColor("_GrassHealthyColor", _settings.healthyColor);
            Shader.SetGlobalColor("_GrassDryColor", _settings.dryColor);
            Shader.SetGlobalFloat("_GrassMoistureDarken", _settings.moistureDarken);
            Shader.SetGlobalFloat("_GroundStripeContrast", _settings.groundStripeContrast);
            Shader.SetGlobalFloat("_GroundSpecStrength", _settings.groundSpecStrength);
            Shader.SetGlobalFloat("_GroundSpecPower", _settings.groundSpecPower);
            Shader.SetGlobalFloat("_GroundBendNormalWeight", _settings.bendNormalWeight);
            Shader.SetGlobalFloat("_GroundStripeAlbedoGain", _settings.stripeAlbedoGain);

            // Moisture dose visuals (M.3): shared by blades and ground so the
            // wet look / dry tint read consistently across the LOD bands.
            Shader.SetGlobalFloat("_PuddleThreshold", _settings.puddleThreshold);
            Shader.SetGlobalFloat("_DryThreshold", _settings.dryThreshold);
            Shader.SetGlobalColor("_MoistureDryColor", _settings.moistureDryColor);
            Shader.SetGlobalFloat("_WetStart", _settings.wetStart);
            // Guard against an inverted/zero-width ramp from inspector edits.
            Shader.SetGlobalFloat("_WetFull", Mathf.Max(_settings.wetFull, _settings.wetStart + 0.01f));
            Shader.SetGlobalFloat("_WetDarken", _settings.wetDarken);
            Shader.SetGlobalFloat("_WetSaturation", _settings.wetSaturation);
            Shader.SetGlobalFloat("_WetGloss", _settings.wetGloss);
            Shader.SetGlobalFloat("_WetSheenPower", _settings.wetSheenPower);
            Shader.SetGlobalColor("_PuddleWaterColor", _settings.puddleWaterColor);
            Shader.SetGlobalFloat("_PuddleOpacity", _settings.puddleOpacity);
            Shader.SetGlobalColor("_PuddleSkyColor", _settings.puddleSkyColor);
            Shader.SetGlobalFloat("_PuddleSpecular", _settings.puddleSpecular);
            Shader.SetGlobalFloat("_PuddleGlintPower", _settings.puddleGlintPower);
            Shader.SetGlobalFloat("_PuddleReflection", _settings.puddleReflection);
            Shader.SetGlobalFloat("_PuddleRipple", _settings.puddleRipple);
            Shader.SetGlobalFloat("_PuddleNoiseScale", Mathf.Max(0.05f, _settings.puddleNoiseScaleM));
            Shader.SetGlobalFloat("_PuddleCoverageAtThreshold", _settings.puddleCoverageAtThreshold);
            Shader.SetGlobalFloat("_PuddleCoverageAtFull", _settings.puddleCoverageAtFull);
            Shader.SetGlobalFloat("_PuddleCoverageExponent", _settings.puddleCoverageExponent);
        }

        private void Update()
        {
            VisibleTileCount = 0;
            TotalInstanceCount = 0;
            if (!RenderingEnabled || _tiles == null)
            {
                return;
            }

            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            _material.SetFloat("_LodTintOn", LodTintEnabled ? 1f : 0f);

            _stopwatch.Restart();
            GeometryUtility.CalculateFrustumPlanes(_camera, _frustumPlanes);
            var cameraXz = new Vector2(_camera.transform.position.x, _camera.transform.position.z);

            for (int i = 0; i < _tiles.Length; i++)
            {
                GrassTile tile = _tiles[i];
                if (!GeometryUtility.TestPlanesAABB(_frustumPlanes, tile.Aabb))
                {
                    continue;
                }

                float distance = GrassTileLayout.DistanceToTileXz(cameraXz, tile.OriginXz, tile.SizeXz);
                float density = GrassLod.DensityAt(
                    distance, _settings.nearEndM, _settings.midEndM, _settings.farEndM,
                    _settings.nearDensityPerM2, _settings.midDensityPerM2);
                int instanceCount = GrassLod.InstanceCount(density, tile.AreaM2, _settings.maxInstancesPerTile);
                if (instanceCount == 0)
                {
                    continue;
                }

                MaterialPropertyBlock props = _tileProps[i];
                props.SetFloat(WidthScaleId, GrassLod.WidthScaleAt(
                    distance, _settings.nearEndM, _settings.midEndM, _settings.midWidthScale));
                if (LodTintEnabled)
                {
                    GrassLodBand band = GrassLod.BandAt(
                        distance, _settings.nearEndM, _settings.midEndM, _settings.farEndM);
                    props.SetColor(LodTintColorId, band == GrassLodBand.Near ? NearBandTint : MidBandTint);
                }

                var renderParams = new RenderParams(_material)
                {
                    matProps = props,
                    worldBounds = tile.Aabb,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false
                };
                Graphics.RenderMeshPrimitives(renderParams, _bladeMesh, 0, instanceCount);

                VisibleTileCount++;
                TotalInstanceCount += instanceCount;
            }
            _stopwatch.Stop();
            LastCullMs = _stopwatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Unit blade: 1 m wide x 1 m tall tapered quad in the XY plane,
        /// pivot at the root. The shader scales width/height per instance.
        /// </summary>
        private static Mesh CreateBladeMesh()
        {
            var mesh = new Mesh { name = "GrassBlade" };
            mesh.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.22f, 1f, 0f),
                new Vector3(0.22f, 1f, 0f)
            });
            mesh.SetUVs(0, new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            // Bounds are irrelevant: RenderParams.worldBounds drives culling.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
            if (_bladeMesh != null)
            {
                Destroy(_bladeMesh);
            }
        }
    }
}
