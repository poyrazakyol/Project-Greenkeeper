using Greenkeeper.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Greenkeeper.Tests
{
    /// <summary>
    /// Asset contract for the push mower model (ART_DIRECTION §4, ASSET_LIST #1):
    /// the tool definition points at a prefab within the first-person tool
    /// triangle budget, with project materials (none embedded in the FBX)
    /// and no colliders (the model is a child of the player's
    /// CharacterController and would block its own movement).
    /// </summary>
    public class PushMowerAssetTests
    {
        private const string MowerDefinitionPath = "Assets/_Project/Data/Tool_PushMower.asset";
        private const string ModelPath = "Assets/_Project/Art/Models/Tools/SM_PushMower.fbx";
        private const string MaterialsDir = "Assets/_Project/Art/Materials/";
        private const int MinTris = 3000;
        private const int MaxTris = 8000;

        private static GameObject LoadPrefab()
        {
            var definition = AssetDatabase.LoadAssetAtPath<ToolDefinitionSO>(MowerDefinitionPath);
            Assert.IsNotNull(definition, "Mower definition missing.");
            Assert.IsNotNull(definition.modelPrefab, "Mower definition has no model prefab.");
            return definition.modelPrefab;
        }

        [Test]
        public void ModelPrefab_MeshIsWithinToolTriangleBudget()
        {
            MeshFilter filter = LoadPrefab().GetComponentInChildren<MeshFilter>();
            Assert.IsNotNull(filter);
            Assert.IsNotNull(filter.sharedMesh);

            Mesh mesh = filter.sharedMesh;
            long tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                tris += mesh.GetIndexCount(s) / 3;
            }
            Assert.That(tris, Is.InRange(MinTris, MaxTris));
        }

        [Test]
        public void ModelPrefab_UsesProjectMaterialsOnePerSubmesh()
        {
            GameObject prefab = LoadPrefab();
            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
            Mesh mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;

            Assert.AreEqual(mesh.subMeshCount, renderer.sharedMaterials.Length);
            Assert.LessOrEqual(renderer.sharedMaterials.Length, 2, "ART_DIRECTION: at most 2 materials.");
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.IsNotNull(material);
                StringAssert.StartsWith(MaterialsDir, AssetDatabase.GetAssetPath(material));
            }
        }

        [Test]
        public void ModelPrefab_HasNoColliders()
        {
            Assert.IsEmpty(LoadPrefab().GetComponentsInChildren<Collider>(true));
        }

        [Test]
        public void ModelImporter_DoesNotImportEmbeddedMaterials()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            Assert.IsNotNull(importer);
            Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode);
        }
    }
}
