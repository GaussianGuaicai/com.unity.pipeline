# Code Analysis Dependencies

This folder contains the namespace-isolated Roslyn and metadata DLLs used by runtime compilation and the IL interpreter. The DLL importer `.meta` files control their platform availability.

Keep PluginImporter `serializedVersion: 2` and the list-based `platformData` format readable by Unity 2022.3. Preserve GUIDs, platform selections, automatic references, and the editor/development/runtime-pipeline define constraint. Do not replace these DLLs with non-isolated assemblies.