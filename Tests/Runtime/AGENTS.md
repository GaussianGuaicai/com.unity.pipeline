# Runtime Tests

This directory covers runtime commands, hot reload and Player lifecycle behavior.
`Unity.Pipeline.Tests.Runtime.asmdef` must remain runnable in PlayMode and Players.
Keep the `TestAssemblies` marker so Unity 2022.3 resolves NUnit; do not fix missing
test references by limiting the entire assembly to Editor platforms.