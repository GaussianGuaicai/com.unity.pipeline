using System;

namespace Unity.Pipeline.Runtime.Commands
{
    /// <summary>
    /// Structured result for an input-simulation command: whether the event was injected, and a short
    /// human-readable detail (or an error / unavailable reason).
    /// </summary>
    [Serializable]
    public class InputSimulationResponse
    {
        public bool Success { get; set; }
        public string Command { get; set; }
        public string Detail { get; set; }
        public string Error { get; set; }

        public static InputSimulationResponse Ok(string command, string detail) =>
            new InputSimulationResponse { Success = true, Command = command, Detail = detail };

        public static InputSimulationResponse Fail(string command, string error) =>
            new InputSimulationResponse { Success = false, Command = command, Error = error };

        public static InputSimulationResponse Unavailable(string command) =>
            new InputSimulationResponse
            {
                Success = false,
                Command = command,
                Error = "Input simulation requires the Input System package to be present and active " +
                        "(ENABLE_INPUT_SYSTEM). Legacy input injection is not supported."
            };
    }
}
