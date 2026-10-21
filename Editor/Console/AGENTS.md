# Editor Console

`EditorConsoleEntries.cs` adapts the internal editor log store; `EditorConsoleGroundTruth.cs` publishes snapshots for requests made during compilation. Keep reflection failures nonfatal and retain update-driven sampling even when no Console window is open. Unity 2022.3 reads counts through LogEntries rather than ConsoleWindowUtility.