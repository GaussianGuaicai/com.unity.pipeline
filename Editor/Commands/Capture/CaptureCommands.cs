using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Unity.Pipeline.Editor.Commands.Capture
{
    /// <summary>
    /// Visual-feedback commands (CLI-199): render a camera or the Scene View to a PNG and return it
    /// base64-encoded, so an agent can "see" the editor without a display. Optionally writes the PNG
    /// under the project (sandboxed via <see cref="ProjectPaths"/>) and refreshes the AssetDatabase.
    ///
    /// Captures require a GPU; in batchmode/headless the device type is
    /// <see cref="GraphicsDeviceType.Null"/> and the commands throw so callers get a clear message
    /// instead of an empty image.
    /// </summary>
    public static class CaptureCommands
    {
        private const int MaxDimension = 4096;

        [CliCommand("capture_game_view", "Render a camera to a PNG. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both).")]
        public static CaptureResult CaptureGameView(
            [CliArg("width", "Output width in px (default 1280; capped 4096).")] int width = 1280,
            [CliArg("height", "Output height in px (default 720; capped 4096).")] int height = 720,
            [CliArg("camera", "Optional camera name; defaults to Camera.main, else the first enabled camera.")] string camera = null,
            [CliArg("save_path", "Optional project-relative path to write the PNG (e.g. Screenshots/foo.png). When set, the result omits the inline base64 image unless include_inline_image=true.")] string savePath = null,
            [CliArg("include_inline_image", "Also return the image inline as base64 when save_path is set (default false: path-only result). Only meaningful together with save_path.")] bool includeInlineImage = false,
            [CliArg("max_resolution", "Cap on the inline image's longest edge (e.g. 512). Only applies when an inline image is returned (no save_path, or save_path + include_inline_image=true); the save_path file keeps the requested resolution.")] int maxResolution = 0)
        {
            GuardHasGpu();

            var cam = ResolveCamera(camera);
            if (cam == null)
                throw new ArgumentException("No camera found to capture.");

            return Capture(cam, width, height, $"camera:{cam.name}", savePath, includeInlineImage, maxResolution);
        }

        [CliCommand("capture_scene_view", "Render the active Scene View to a PNG. Returns it inline as base64, unless save_path is set (path-only result; pass include_inline_image=true to get both).")]
        public static CaptureResult CaptureSceneView(
            [CliArg("width", "Output width in px (default 1280; capped 4096).")] int width = 1280,
            [CliArg("height", "Output height in px (default 720; capped 4096).")] int height = 720,
            [CliArg("save_path", "Optional project-relative path to write the PNG (e.g. Screenshots/foo.png). When set, the result omits the inline base64 image unless include_inline_image=true.")] string savePath = null,
            [CliArg("include_inline_image", "Also return the image inline as base64 when save_path is set (default false: path-only result). Only meaningful together with save_path.")] bool includeInlineImage = false,
            [CliArg("max_resolution", "Cap on the inline image's longest edge (e.g. 512). Only applies when an inline image is returned (no save_path, or save_path + include_inline_image=true); the save_path file keeps the requested resolution.")] int maxResolution = 0)
        {
            GuardHasGpu();

            var sv = SceneView.lastActiveSceneView;
            if (sv == null || sv.camera == null)
                throw new ArgumentException("No active Scene View to capture.");

            return Capture(sv.camera, width, height, "sceneView", savePath, includeInlineImage, maxResolution);
        }

        /// <summary>
        /// Shared capture core. Renders at the requested (clamped) size and writes the file when
        /// <paramref name="savePath"/> is set. The image is inlined as base64 only when there is no
        /// file, or on <paramref name="includeInlineImage"/> — a save_path result is otherwise
        /// path-only, so agent tool results stay small (AUTHAPI-8). <paramref name="maxResolution"/>
        /// caps the inline image's longest edge (no-op when no inline image is returned); the saved
        /// file keeps the requested resolution.
        /// </summary>
        private static CaptureResult Capture(Camera cam, int width, int height, string source,
            string savePath, bool includeInlineImage, int maxResolution)
        {
            var w = Mathf.Clamp(width, 1, MaxDimension);
            var h = Mathf.Clamp(height, 1, MaxDimension);

            var wantsFile = !string.IsNullOrEmpty(savePath);
            var wantsInline = !wantsFile || includeInlineImage;

            // Without a file the inline image is the only artifact: the cap applies to the render itself.
            if (!wantsFile && maxResolution > 0)
                (w, h) = ClampToLongEdge(w, h, maxResolution);

            var png = EncodeCameraToPng(cam, w, h);
            var savedPath = WriteIfRequested(png, savePath);

            string base64 = null;
            int? inlineW = null, inlineH = null;
            if (wantsInline)
            {
                var inlinePng = png;
                if (wantsFile && maxResolution > 0 && maxResolution < Mathf.Max(w, h))
                {
                    // Re-render small for the inline copy; the file already has the full resolution.
                    var (iw, ih) = ClampToLongEdge(w, h, maxResolution);
                    inlinePng = EncodeCameraToPng(cam, iw, ih);
                    inlineW = iw;
                    inlineH = ih;
                }
                base64 = Convert.ToBase64String(inlinePng);
            }

            return new CaptureResult
            {
                Width = w,
                Height = h,
                Encoding = "png",
                Base64 = base64,
                Bytes = png.Length,
                Source = source,
                SavedPath = savedPath,
                InlineWidth = inlineW,
                InlineHeight = inlineH
            };
        }

        /// <summary>Scale (w, h) down so the longest edge is at most <paramref name="maxEdge"/>, preserving aspect.</summary>
        private static (int w, int h) ClampToLongEdge(int w, int h, int maxEdge)
        {
            var longest = Mathf.Max(w, h);
            if (maxEdge <= 0 || longest <= maxEdge)
                return (w, h);

            var scale = (float)maxEdge / longest;
            return (Mathf.Max(1, Mathf.RoundToInt(w * scale)), Mathf.Max(1, Mathf.RoundToInt(h * scale)));
        }

        /// <summary>Throw when no GPU is available (batchmode/headless), where a render would be blank.</summary>
        private static void GuardHasGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("No GPU available (batchmode/headless); cannot capture.");
        }

        /// <summary>
        /// Resolve the camera to capture: by name (if provided), else <see cref="Camera.main"/>,
        /// else the first enabled camera. Returns null when nothing matches.
        /// </summary>
        private static Camera ResolveCamera(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                // Camera.allCameras only includes enabled cameras; also scan all loaded cameras so a
                // disabled-but-named camera can still be targeted explicitly.
                var byName = Camera.allCameras.FirstOrDefault(c => c.name == name)
                    ?? PipelineUtils.FindObjectsByType<Camera>().FirstOrDefault(c => c.name == name);
                return byName;
            }

            if (Camera.main != null)
                return Camera.main;

            return Camera.allCameras.FirstOrDefault();
        }

        /// <summary>
        /// Render <paramref name="cam"/> off-screen into a temporary RenderTexture and encode the
        /// result to PNG bytes. The camera's target texture and the active RenderTexture are restored
        /// in a finally block so capturing never leaves global render state dirty.
        /// </summary>
        private static byte[] EncodeCameraToPng(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                try
                {
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply();
                    return ImageConversion.EncodeToPNG(tex);
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>
        /// When <paramref name="savePath"/> is set, validate it through the project-path sandbox,
        /// write the PNG to its absolute filesystem location, refresh the AssetDatabase, and return
        /// the resolved project-relative asset path. Returns null when no path was requested.
        /// </summary>
        private static string WriteIfRequested(byte[] png, string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
                return null;

            var resolved = ProjectPaths.Resolve(savePath, out var err);
            if (resolved == null)
                throw new ArgumentException(err);

            // ProjectPaths.Resolve returns a project-relative path; combine it with the project root
            // (the folder that contains Assets/) to get the absolute file to write.
            var absolute = Path.Combine(ProjectPaths.ProjectRoot, resolved);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(absolute, png);
            AssetDatabase.Refresh();
            return resolved;
        }
    }

    /// <summary>
    /// Result of a capture command: the rendered dimensions, the source, the project-relative path
    /// the PNG was written to (null when not saved), and the base64 payload — omitted from the JSON
    /// when a save_path result is path-only (AUTHAPI-8).
    /// </summary>
    [Serializable]
    public class CaptureResult
    {
        /// <summary>Rendered width in pixels (after clamping).</summary>
        [JsonProperty("width")]
        public int Width { get; set; }

        /// <summary>Rendered height in pixels (after clamping).</summary>
        [JsonProperty("height")]
        public int Height { get; set; }

        /// <summary>Image encoding; always "png".</summary>
        [JsonProperty("encoding")]
        public string Encoding { get; set; }

        /// <summary>Base64-encoded PNG bytes; null (omitted) when save_path is set without include_inline_image.</summary>
        [JsonProperty("base64", NullValueHandling = NullValueHandling.Ignore)]
        public string Base64 { get; set; }

        /// <summary>Length of the raw PNG byte array.</summary>
        [JsonProperty("bytes")]
        public int Bytes { get; set; }

        /// <summary>What was captured, e.g. "camera:Main Camera" or "sceneView".</summary>
        [JsonProperty("source")]
        public string Source { get; set; }

        /// <summary>Project-relative path the PNG was also written to, or null.</summary>
        [JsonProperty("savedPath")]
        public string SavedPath { get; set; }

        /// <summary>Inline image width when max_resolution downscaled it below the saved file's; omitted otherwise.</summary>
        [JsonProperty("inlineWidth", NullValueHandling = NullValueHandling.Ignore)]
        public int? InlineWidth { get; set; }

        /// <summary>Inline image height when max_resolution downscaled it below the saved file's; omitted otherwise.</summary>
        [JsonProperty("inlineHeight", NullValueHandling = NullValueHandling.Ignore)]
        public int? InlineHeight { get; set; }
    }
}
