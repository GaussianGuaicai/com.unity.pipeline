# Editor Console

`EditorConsoleEntries.cs` wraps the internal console store; `EditorConsoleGroundTruth.cs` publishes main-thread snapshots for background commands. Preserve sampling when no Console window is open and restore any temporary filter changes. Unity 2022.3 uses reflected `LogEntries.GetCountsByType`; newer editors can use `ConsoleWindowUtility`.