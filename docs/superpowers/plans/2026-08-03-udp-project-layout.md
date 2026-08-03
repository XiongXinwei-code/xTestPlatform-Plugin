# UDP Project Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the UDP plugin with the repository's one-top-level-plugin-folder layout while keeping its build, test, and deployment contract intact.

**Architecture:** `UdpCommunication/` remains the only UDP-related folder in the repository root. Its runtime, UI, and test projects become direct children, and the publish script becomes `UdpCommunication/Publish-Plugin.ps1`; solution, project, test, and user-facing documentation references are updated atomically.

**Tech Stack:** .NET 8 / WPF, xUnit, PowerShell, Visual Studio solution files.

## Global Constraints

- Keep only `UdpCommunication/` as the UDP project folder under the repository root; remove the tracked legacy `UdpCommunicationStepPlugin*` root directories from this branch.
- Keep the project folder names, assembly names, namespaces, plugin behavior, and package versions unchanged.
- The target UDP layout is `UdpCommunication/UdpCommunication.StepPlugin/`, `UdpCommunication/UdpCommunication.StepPlugin.UI/`, `UdpCommunication/UdpCommunication.StepPlugin.Tests/`, `UdpCommunication/UdpCommunication.sln`, `UdpCommunication/Publish-Plugin.ps1`, and `UdpCommunication/TESTING.md`.
- Do not stage or commit existing unrelated working-tree changes.
- Release compilation and the complete UDP test suite must succeed before delivery.
- The final generated package is `Plugins/UdpCommunication`; customer validation only requires copying that directory into the target platform's plugin directory.

---

### Task 1: Flatten the UDP project layout and preserve the deployment contract

**Files:**
- Move: `UdpCommunication/src/UdpCommunication.StepPlugin/` → `UdpCommunication/UdpCommunication.StepPlugin/`
- Move: `UdpCommunication/src/UdpCommunication.StepPlugin.UI/` → `UdpCommunication/UdpCommunication.StepPlugin.UI/`
- Move: `UdpCommunication/tests/UdpCommunication.StepPlugin.Tests/` → `UdpCommunication/UdpCommunication.StepPlugin.Tests/`
- Move: `UdpCommunication/build/Publish-Plugin.ps1` → `UdpCommunication/Publish-Plugin.ps1`
- Modify: `UdpCommunication/UdpCommunication.sln`
- Modify: `UdpCommunication/UdpCommunication.StepPlugin.Tests/UdpCommunication.StepPlugin.Tests.csproj`
- Modify: `UdpCommunication/UdpCommunication.StepPlugin.Tests/UdpPluginDescriptionTests.cs`
- Modify: `UdpCommunication/UdpCommunication.StepPlugin.Tests/PluginDeploymentTests.cs`
- Modify: `UdpCommunication/Publish-Plugin.ps1`
- Modify: `UdpCommunication/TESTING.md`

**Interfaces:**
- Consumes: Existing project names `UdpCommunication.StepPlugin`, `UdpCommunication.StepPlugin.UI`, and `UdpCommunication.StepPlugin.Tests`; existing publish output contract `Plugins/UdpCommunication`.
- Produces: A solution whose project paths and project references resolve in the flattened layout, plus a release package containing the same required assemblies and no host assemblies.

- [ ] **Step 1: Write the failing layout assertions**

Update the two repository-path tests before moving any implementation files:

```csharp
var iconPath = Path.Combine(repositoryRoot, "UdpCommunication", "UdpCommunication.StepPlugin.UI", "Resources", "Icons", "udp.png");
var xamlPath = Path.Combine(repositoryRoot, "UdpCommunication", "UdpCommunication.StepPlugin.UI", "Views", "UdpEditorView.xaml");
```

```csharp
var script = Path.Combine(repositoryRoot, "UdpCommunication", "Publish-Plugin.ps1");
```

- [ ] **Step 2: Run the focused tests to verify the expected RED state**

Run:

```powershell
dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter "FullyQualifiedName~UdpPluginDescriptionTests|FullyQualifiedName~PluginDeploymentTests"
```

Expected: FAIL because the direct-child UI path and root-level publish script do not exist yet.

- [ ] **Step 3: Move projects and update all active path references**

Use Git-aware moves, then apply these exact path transformations:

```text
UdpCommunication.sln
  src\\UdpCommunication.StepPlugin\\UdpCommunication.StepPlugin.csproj
  → UdpCommunication.StepPlugin\\UdpCommunication.StepPlugin.csproj
  tests\\UdpCommunication.StepPlugin.Tests\\UdpCommunication.StepPlugin.Tests.csproj
  → UdpCommunication.StepPlugin.Tests\\UdpCommunication.StepPlugin.Tests.csproj
  src\\UdpCommunication.StepPlugin.UI\\UdpCommunication.StepPlugin.UI.csproj
  → UdpCommunication.StepPlugin.UI\\UdpCommunication.StepPlugin.UI.csproj

UdpCommunication.StepPlugin.Tests.csproj
  ..\\..\\src\\UdpCommunication.StepPlugin\\UdpCommunication.StepPlugin.csproj
  → ..\\UdpCommunication.StepPlugin\\UdpCommunication.StepPlugin.csproj
  ..\\..\\src\\UdpCommunication.StepPlugin.UI\\UdpCommunication.StepPlugin.UI.csproj
  → ..\\UdpCommunication.StepPlugin.UI\\UdpCommunication.StepPlugin.UI.csproj

Publish-Plugin.ps1
  $projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
  $uiProjectPath = Join-Path $projectRoot 'UdpCommunication.StepPlugin.UI\\UdpCommunication.StepPlugin.UI.csproj'
  $sourceDirectory = Join-Path $projectRoot "UdpCommunication.StepPlugin.UI\\bin\\$Configuration\\net8.0-windows7.0"
```

Keep the default publish destination as `Plugins/UdpCommunication`, retain the required/forbidden assembly lists, and remove the now-empty `src/`, `tests/`, and `build/` directories.

- [ ] **Step 4: Run the focused tests to verify GREEN**

Run:

```powershell
dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --filter "FullyQualifiedName~UdpPluginDescriptionTests|FullyQualifiedName~PluginDeploymentTests"
```

Expected: PASS. The deployment test must create a package containing both UDP plugin DLLs and `Microsoft.NET.StringTools.dll`, while excluding host assemblies.

- [ ] **Step 5: Update active usage documentation and inspect the migration diff**

In `UdpCommunication/TESTING.md`, replace the command reference with:

```text
UdpCommunication/Publish-Plugin.ps1
```

Then run:

```powershell
git diff --check -- UdpCommunication
git diff --summary -- UdpCommunication
```

Expected: no whitespace errors; the summary shows project/script renames instead of accidental content deletion.

- [ ] **Step 6: Run release verification and generate the customer package**

Run:

```powershell
dotnet build UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --nologo
dotnet test UdpCommunication/UdpCommunication.sln --configuration Release --no-restore --verbosity minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File UdpCommunication/Publish-Plugin.ps1 -Configuration Release
```

Expected: build exit code 0, all UDP tests pass, and `Plugins/UdpCommunication/` contains the two UDP DLLs, `Microsoft.NET.StringTools.dll`, and the required Syncfusion/MessagePack dependencies without host assemblies.

- [ ] **Step 7: Commit the scoped migration**

```powershell
git add -A UdpCommunication docs/superpowers/plans/2026-08-03-udp-project-layout.md
git commit -m "refactor: align UDP plugin layout"
```

Expected: the commit contains the UDP layout migration, deletion of the tracked legacy UDP root directories, and its implementation plan; it excludes unrelated workspace files.

## Plan Self-Review

- Spec coverage: Task 1 moves all three projects and the publish script, updates all active references, confirms the prescribed flat layout, and performs build, test, and package validation.
- Placeholder scan: no placeholders or deferred implementation steps remain.
- Type consistency: no public type or namespace changes are introduced; paths match the target structure exactly.
