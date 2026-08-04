using System;
using Unity.Pipeline.Commands;

namespace Unity.Pipeline.Runtime.Commands
{
    /// <summary>
    /// Stable command facade for optional Input System integration. The implementation lives in the
    /// conditionally compiled Unity.Pipeline.InputSystem assembly; projects without that package keep
    /// the same command surface and receive a structured unavailable result.
    /// </summary>
    public static class RuntimeInputCommand
    {
        private const string BackendTypeName =
            "Unity.Pipeline.Runtime.Commands.InputSystemRuntimeInputBackend, Unity.Pipeline.InputSystem";

        private static IRuntimeInputBackend s_Backend;

        [CliCommand("simulate_key", "Simulate a keyboard key event (Input System). Drives the running app.",
            MainThreadRequired = true, RuntimeOnly = true)]
        public static InputSimulationResponse SimulateKey(
            [CliArg("key", "Input System Key name, e.g. Space, W, Enter, LeftArrow", Required = true)] string key,
            [CliArg("action", "down | up | press (down+up). Default: press")] string action = "press")
        {
            var backend = GetBackend();
            return backend != null
                ? backend.SimulateKey(key, action)
                : InputSimulationResponse.Unavailable("simulate_key");
        }

        [CliCommand("simulate_pointer", "Simulate a mouse/pointer event at screen coordinates (Input System).",
            MainThreadRequired = true, RuntimeOnly = true)]
        public static InputSimulationResponse SimulatePointer(
            [CliArg("x", "Screen X in pixels (origin bottom-left)", Required = true)] float x,
            [CliArg("y", "Screen Y in pixels (origin bottom-left)", Required = true)] float y,
            [CliArg("action", "move | down | up | click (down+up). Default: click")] string action = "click",
            [CliArg("button", "left | right | middle. Default: left")] string button = "left")
        {
            var backend = GetBackend();
            return backend != null
                ? backend.SimulatePointer(x, y, action, button)
                : InputSimulationResponse.Unavailable("simulate_pointer");
        }

        private static IRuntimeInputBackend GetBackend()
        {
            if (s_Backend != null)
                return s_Backend;

            var backendType = Type.GetType(BackendTypeName, throwOnError: false);
            if (backendType != null && typeof(IRuntimeInputBackend).IsAssignableFrom(backendType))
                s_Backend = Activator.CreateInstance(backendType) as IRuntimeInputBackend;

            return s_Backend;
        }
    }

    /// <summary>Contract implemented by the optional Input System assembly.</summary>
    public interface IRuntimeInputBackend
    {
        InputSimulationResponse SimulateKey(string key, string action);
        InputSimulationResponse SimulatePointer(float x, float y, string action, string button);
    }
}
