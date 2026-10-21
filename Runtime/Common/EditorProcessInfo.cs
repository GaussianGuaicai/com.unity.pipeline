#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.MPE;

namespace Unity.Pipeline
{
    /// <summary>
    /// Facts about the current Editor process. In the Runtime assembly so runtime code running in the
    /// Editor can use them too.
    /// </summary>
    static class EditorProcessInfo
    {
        /// <summary>
        /// Whether this is the Editor process that owns the project, as opposed to an asset import
        /// worker or another secondary process.
        /// </summary>
        /// <returns>True in the main Editor process.</returns>
        internal static bool IsMainProcess()
        {
            // A read-only or virtual project clone (e.g. Multiplayer Play Mode) owns its own project.
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg == "-readonly" || arg == "--virtual-project-clone")
                    return true;
            }

            if (AssetDatabase.IsAssetImportWorkerProcess())
                return false;

            return ProcessService.level == ProcessLevel.Main;
        }

        /// <summary>
        /// Whether the Editor was launched with -automated. A Unity console warning for this fact
        /// is unactionable (nobody watches the console in an agent-driven workflow) and, worse, was
        /// logged once per constructed server instance — the isolated test servers built per test
        /// (see TestEditorPipelineServer) spammed it dozens of times per suite run. The fact is
        /// instead surfaced as structured data on the instance descriptor (see
        /// <see cref="PipelineDescriptorPublisher"/>) — the one thing every client reads before it can
        /// connect at all (UUM-149977).
        /// </summary>
        /// <param name="commandLineArgs">The process's command line arguments.</param>
        /// <returns>True if <c>-automated</c> is one of them.</returns>
        internal static bool IsAutomated(string[] commandLineArgs)
        {
            foreach (var arg in commandLineArgs)
            {
                if (arg == "-automated")
                    return true;
            }
            return false;
        }
    }
}
#endif
