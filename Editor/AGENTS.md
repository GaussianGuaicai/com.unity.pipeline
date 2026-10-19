# Editor Integration

This folder hosts editor server startup, command discovery, configuration inspectors, and automation commands. Key entry points are `EditorPipelineStartup.cs`, `EditorPipelineServer.cs`, and `PipelineAnalytics.cs`.

Keep Unity 2022.3 compilable. Guard newer editor APIs with version defines and preserve command contracts. The IAnalytic-based analytics implementation requires Unity 2023.2 or newer; older editors skip this optional reporting without affecting command execution.