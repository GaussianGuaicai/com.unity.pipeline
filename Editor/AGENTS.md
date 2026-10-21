# Editor Integration

This directory owns editor commands, startup, settings, and analytics. `EditorPipelineStartup.cs` starts the server; `PipelineAnalytics.cs` reports usage without interrupting commands. Keep Unity 6 APIs behind version defines and retain Unity 2022.3 equivalents. Preserve command names, analytics payload fields, and user settings across reloads.