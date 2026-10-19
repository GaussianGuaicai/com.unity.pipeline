# Material Commands

`MaterialCommands.cs` reads and edits material properties through the shared authoring helpers. Preserve Undo, root validation, and response fields. The render queue response must retain the raw `-1` inheritance value; on Unity 2022.3 read `m_CustomRenderQueue`, not the resolved `Material.renderQueue`.