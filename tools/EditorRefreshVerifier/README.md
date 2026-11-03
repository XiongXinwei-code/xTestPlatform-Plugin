# Editor refresh verification

This tool verifies the editor-to-host save contract from one deployed plugin folder. It loads the folder's actual core descriptors, editor descriptors and SDK assemblies, creates all editors on one WPF STA, and changes a safe scalar through each editor's view model.

The injected `ExecuteCommand` callback simulates the host contract: run the save action, call the real core plugin's `GenerateDescription`, and update the synthetic step description. The second edit also refreshes the same editor inside the callback to exercise synchronous re-entry. The tool checks serialized settings, restored values, callback invocation, refreshed VM values and description consistency. It raises unload before releasing each editor.

No executor is created and no expression or device step is executed. Editor construction may perform its ordinary local metadata work. This is **not a test of the running xTestPlatform application** or a claim that every individual control was manually clicked.

Run each plugin folder in a separate process because SDK/WPF/Syncfusion resources and assemblies are process-wide:

```powershell
dotnet build tools/EditorRefreshVerifier/EditorRefreshVerifier.csproj -c Release -o artifacts/validation/editor-refresh-verifier
dotnet artifacts/validation/editor-refresh-verifier/EditorRefreshVerifier.dll artifacts/Release/2026-11-02-editor-refresh/Plugins/CAN artifacts/validation/editor-refresh/CAN.json
```

An optional third argument restricts verification to one exact step type ID. JSON goes to the supplied report path, or stdout when no path is supplied. Progress goes to stderr. Exit codes: `0` all matching editors passed, `1` invalid usage, `2` any editor or assembly failed. A missing safe scalar or flush entry point is a failure requiring a targeted test, not a silent skip.

The report records the core DLL identity, MVID and SHA-256 and whether the selected scalar actually changes the generated description. A description may legitimately omit that scalar; equality with the descriptor's current generated output remains mandatory.

Use `--host-factory <installation-directory>` to also verify new-step initialization against the installed platform's actual `StepFactory.CreateStepFromPlugin`. The flag may appear before or after the existing positional arguments:

```powershell
dotnet artifacts/validation/editor-refresh-verifier/EditorRefreshVerifier.dll artifacts/Release/Plugins/CAN artifacts/validation/editor-refresh/CAN-host.json IO.CanDbcWrite --host-factory "C:/Program Files/xTestPlatform"
```

This mode loads the installed Core/SDK before the candidate plugin, constructs an empty registry and address generator, and creates detached in-memory steps through the real factory. It preserves the factory's initial settings bytes and version metadata. The same editor creation, two edits, synchronous save re-entry and unload checks then run. No executor, user project or hardware operation is started. The report includes host Core/SDK identity and each step's initial setting version and serializer version, including when editor creation fails.

Without this flag, the original isolated mode remains available: the tool creates a synthetic step and explicitly sets its metadata to the serializer version. That mode verifies the editor/save contract but cannot detect a mismatch between the installed factory's default metadata and a plugin's declared version. Host-factory mode is an offline integration check, not execution inside the running platform's sequence-list UI. If the installed factory signature changes, the tool reports an assembly error instead of substituting synthetic metadata.
