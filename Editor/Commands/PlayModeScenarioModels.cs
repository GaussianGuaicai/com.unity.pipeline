#if UNITY_6000_3_OR_NEWER && MPPM_AVAILABLE
namespace Unity.Pipeline.Editor.Commands
{
    // Shared DTOs for the Play Mode Scenario commands. Available whenever MPPM reflection is
    // (6.3+), so both the 6.5+/6.7 implementation (PlayModeScenarioCommands.cs) and the 6.3
    // implementation (PlayModeScenarioCommands63.cs) can consume them, as can MppmReflection.
    internal sealed class ScenarioPlayer
    {
        public string Id;
        public string Name;
        public string Type;
        public string Controller;
        public string RunMode;
    }

    internal sealed class MppmPlayer
    {
        public string Name;
        public string State;
        public string VpId;
    }
}
#endif
