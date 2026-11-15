using Greenkeeper.EditorTools;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Guards the first-person lens the scene builder applies: it must set the
    /// FOV explicitly, so a scene rebuild never falls back to Cinemachine's
    /// default (40), which hid the held tool (art/push-mower).
    /// </summary>
    public class StadiumSceneBuilderTests
    {
        [Test]
        public void FirstPersonFieldOfView_Is60()
        {
            Assert.AreEqual(60f, StadiumSceneBuilder.FirstPersonFieldOfView);
        }

        [Test]
        public void ApplyFirstPersonLens_SetsFieldOfViewOnNewCamera()
        {
            var go = new GameObject("CM_Test");
            try
            {
                var camera = go.AddComponent<CinemachineCamera>();
                Assert.AreNotEqual(StadiumSceneBuilder.FirstPersonFieldOfView, camera.Lens.FieldOfView,
                    "Precondition: a fresh camera starts at the Cinemachine default.");

                StadiumSceneBuilder.ApplyFirstPersonLens(camera);

                Assert.AreEqual(StadiumSceneBuilder.FirstPersonFieldOfView, camera.Lens.FieldOfView);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
