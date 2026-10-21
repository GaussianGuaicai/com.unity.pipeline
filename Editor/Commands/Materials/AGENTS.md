# Material Commands

`MaterialCommands.cs` reads and edits material properties. Preserve Undo, authoring-root checks, and the raw render queue value of -1 for shader inheritance. Unity 2022.3 reads m_CustomRenderQueue through SerializedObject instead of Material.rawRenderQueue.