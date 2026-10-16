# Editor Tests

This directory owns EditMode command and server regression tests.
`PipelineTestServer` isolates HTTP tests from the live Editor server;
`PipelineAnalyticsTests` captures events through `PipelineAnalytics.s_Send`.
Use `LogAssert.Expect(LogType, ...)` for cross-version log assertions.
Preserve error status, parameter validation, analytics payload and privacy assertions.