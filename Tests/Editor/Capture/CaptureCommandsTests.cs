using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Pipeline.Editor.Authoring;
using Unity.Pipeline.Editor.Commands.Capture;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Pipeline.Tests.Editor.Capture
{
    /// <summary>
    /// Tests for the visual-feedback commands (CLI-199), exercised directly and via PipelineClient.
    /// Render tests are GPU-gated: under batchmode/headless the graphics device is
    /// <see cref="GraphicsDeviceType.Null"/> and the tests self-ignore rather than fail.
    /// </summary>
    public class CaptureCommandsTests
    {
        private const string CameraName = "CLI199_Cam";
        private const string SaveFolder = "Assets/CLI199_CaptureTests";

        // PNG file signature: 0x89 'P' 'N' 'G' 0x0D 0x0A 0x1A 0x0A.
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private GameObject m_CameraObject;

        [SetUp]
        public void SetUp()
        {
            m_CameraObject = new GameObject(CameraName);
            m_CameraObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_CameraObject != null)
                UnityEngine.Object.DestroyImmediate(m_CameraObject);

            if (AssetDatabase.IsValidFolder(SaveFolder))
                AssetDatabase.DeleteAsset(SaveFolder);
        }

        private static string AbsolutePath(string projectRelative) =>
            Path.Combine(ProjectPaths.ProjectRoot, projectRelative);

        private static bool IsHeadless => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        private static void AssertPngSignature(byte[] bytes)
        {
            Assert.GreaterOrEqual(bytes.Length, PngSignature.Length, "PNG payload too short to contain a signature");
            for (var i = 0; i < PngSignature.Length; i++)
                Assert.AreEqual(PngSignature[i], bytes[i], $"PNG signature mismatch at byte {i}");
        }

        #region Direct

        [Test]
        public void CaptureGameView_NamedCamera_ReturnsPng()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var result = CaptureCommands.CaptureGameView(64, 64, CameraName);

            Assert.IsNotNull(result, "Capture should return a result");
            Assert.IsNotEmpty(result.Base64, "Base64 payload should be non-empty");
            Assert.AreEqual(64, result.Width, "Width should match the requested size");
            Assert.AreEqual(64, result.Height, "Height should match the requested size");
            Assert.AreEqual("png", result.Encoding);
            Assert.AreEqual($"camera:{CameraName}", result.Source);
            Assert.IsNull(result.SavedPath, "No savePath was requested");

            var bytes = Convert.FromBase64String(result.Base64);
            AssertPngSignature(bytes);
            Assert.AreEqual(bytes.Length, result.Bytes, "Reported byte length should match decoded payload");
        }

        [Test]
        public void CaptureSceneView_ReturnsPng()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                Assert.Ignore("No Scene View");

            var result = CaptureCommands.CaptureSceneView(64, 64);

            Assert.IsNotNull(result, "Capture should return a result");
            Assert.IsNotEmpty(result.Base64, "Base64 payload should be non-empty");
            Assert.AreEqual("sceneView", result.Source);

            var bytes = Convert.FromBase64String(result.Base64);
            AssertPngSignature(bytes);
        }

        [Test]
        public void CaptureGameView_SavePath_OmitsBase64_AndWritesPng()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var result = CaptureCommands.CaptureGameView(64, 64, CameraName, $"{SaveFolder}/path_only.png");

            Assert.IsNull(result.Base64, "save_path without include_inline_image should omit the base64 payload");
            Assert.IsNotNull(result.SavedPath, "SavedPath should be set");

            var fileBytes = File.ReadAllBytes(AbsolutePath(result.SavedPath));
            AssertPngSignature(fileBytes);
            Assert.AreEqual(fileBytes.Length, result.Bytes, "Reported byte length should match the file");
        }

        [Test]
        public void CaptureGameView_SavePath_IncludeImage_ReturnsFileAndBase64()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var result = CaptureCommands.CaptureGameView(64, 64, CameraName, $"{SaveFolder}/both.png", includeInlineImage: true);

            Assert.IsNotEmpty(result.Base64, "include_inline_image=true should return the base64 payload");
            Assert.IsNotNull(result.SavedPath, "SavedPath should be set");
            AssertPngSignature(Convert.FromBase64String(result.Base64));
            AssertPngSignature(File.ReadAllBytes(AbsolutePath(result.SavedPath)));
        }

        [Test]
        public void CaptureGameView_MaxResolution_ClampsInlineRender()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            // No save_path: the inline image is the only artifact, so the cap applies to the render.
            var result = CaptureCommands.CaptureGameView(128, 64, CameraName, maxResolution: 32);

            Assert.AreEqual(32, result.Width, "Long edge should be clamped to max_resolution");
            Assert.AreEqual(16, result.Height, "Short edge should keep the aspect ratio");
            AssertPngSignature(Convert.FromBase64String(result.Base64));
        }

        [Test]
        public void CaptureGameView_SavePath_MaxResolution_DownscalesInlineOnly()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var result = CaptureCommands.CaptureGameView(128, 64, CameraName, $"{SaveFolder}/full.png",
                includeInlineImage: true, maxResolution: 32);

            Assert.AreEqual(128, result.Width, "The saved file keeps the requested resolution");
            Assert.AreEqual(32, result.InlineWidth, "The inline copy is downscaled to max_resolution");
            Assert.AreEqual(16, result.InlineHeight, "The inline copy keeps the aspect ratio");
            AssertPngSignature(Convert.FromBase64String(result.Base64));
            AssertPngSignature(File.ReadAllBytes(AbsolutePath(result.SavedPath)));
        }

        [Test]
        public void CaptureSceneView_SavePath_OmitsBase64_AndWritesPng()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
                Assert.Ignore("No Scene View");

            var result = CaptureCommands.CaptureSceneView(64, 64, $"{SaveFolder}/scene_path_only.png");

            Assert.IsNull(result.Base64, "save_path without include_inline_image should omit the base64 payload");
            Assert.IsNotNull(result.SavedPath, "SavedPath should be set");
            AssertPngSignature(File.ReadAllBytes(AbsolutePath(result.SavedPath)));
        }

        #endregion

        #region ViaClient

        [Test]
        public void CaptureGameView_ViaClient_Succeeds()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            using (var server = new PipelineTestServer())
            {
                var response = server.Execute("capture_game_view", new { width = 64, height = 64, camera = CameraName });

                Assert.IsTrue(response.IsSuccess, $"capture_game_view should succeed: {response.Error}");
                Assert.IsTrue(response.HasValidJson, "Response should have valid JSON");
                Assert.IsTrue(response.JsonResponse.ContainsKey("result"), "Should have result field");
            }
        }

        [Test]
        public void CaptureGameView_ViaClient_SavePath_SerializedResultOmitsBase64Key()
        {
            if (IsHeadless)
                Assert.Ignore("No GPU in batchmode");

            using (var server = new PipelineTestServer())
            {
                var response = server.Execute("capture_game_view",
                    new { width = 64, height = 64, camera = CameraName, save_path = $"{SaveFolder}/via_client.png" });

                Assert.IsTrue(response.IsSuccess, $"capture_game_view should succeed: {response.Error}");
                var result = (JObject)response.JsonResponse["result"];
                Assert.IsFalse(result.ContainsKey("base64"),
                    "A path-only result must not carry a base64 key on the wire (AUTHAPI-8)");
                Assert.IsNotNull(result["savedPath"]?.ToString(), "savedPath should be present");
            }
        }

        #endregion
    }
}
