# Repository Guidelines

## Package Structure

`Runtime/` contains the player-safe HTTP server, command contracts, and hot-reload runtime. `Editor/` contains Unity Editor automation commands and server startup. `CodeGen/` provides the hot-reload IL post-processor. `Tests/Editor/` and `Tests/Runtime/` hold Unity Test Framework coverage; `Documentation~/` is the UPM documentation source.

Key assemblies are `Runtime/Unity.Pipeline.asmdef`, `Editor/Unity.Pipeline.Editor.asmdef`, and `CodeGen/Unity.Pipeline.CodeGen.asmdef`. Keep public command names and JSON response fields backward compatible.

## Compatibility Contracts

The package supports Unity 2022.3 LTS and newer. Keep Unity 6 APIs behind the existing `UNITY_6000_x_OR_NEWER` branches and preserve their 2022.3 fallbacks in `Runtime/Common/PipelineUtils.cs`. Hot reload remains Mono-only; do not represent it as IL2CPP-compatible.

The Input System is optional. Runtime input commands must continue returning a structured unavailable result when `ENABLE_INPUT_SYSTEM` is absent. Input System-specific tests live in `Tests/Editor/InputSystem/` and are activated by the `PIPELINE_INPUT_SYSTEM` version define; do not add unconditional `Unity.InputSystem` references to core assemblies.

## Testing

Run a compile-only compatibility check with Unity 2022.3:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'F:\Workspaces\Test Project 2022' -logFile 'F:\Workspaces\Test Project 2022\Logs\pipeline-validation.log'
```

Use Unity Test Framework for focused command tests before running the full editor suite. Keep editor tests isolated from the live server by using `PipelineTestServer`.