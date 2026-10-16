# Editor Compatibility

This directory owns Editor server startup, command dispatch, authoring and analytics.
Key entry points are `EditorPipelineStartup.cs`, `EditorPipelineServer.cs` and
`PipelineAnalytics.cs`. Preserve command names and response contracts.

Guard Unity 6-only APIs and retain Unity 2022.3 fallbacks. Analytics uses native
`IAnalytic` on Unity 2023.2+ and legacy registered events on older Editors; preserve
event names, payload fields, limits, user-command redaction and the test send hook.