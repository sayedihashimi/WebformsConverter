# WebForms → Blazor Server MCP Migration Tool
## Copilot Instructions

These instructions define how to structure and constrain a **.NET 10 MCP server** that analyzes ASP.NET Web Forms solutions and generates **Blazor Server** output, using a user-editable migration plan.

Everything in this document is authoritative for Copilot / ChatGPT assistance inside this repo.

---

## 1. Goals

This repo contains an MCP server that:

- Analyzes an existing **ASP.NET Web Forms** solution (**read-only**).
- Generates an editable **migration plan**: `plan.md`.
- Executes that plan to:
  - Create a **new Blazor Server project**.
  - Generate Blazor components for selected Web Forms pages.
- **Never modifies Web Forms source files or project files**:
  - No changes to existing `.csproj` / `.vbproj`.
  - No edits to `.aspx`, `.ascx`, `.master`, `.cs`, `.vb`, `web.config`, etc.
- Writes all generated artifacts under a dedicated output root:
  - `<solutionDir>/.webforms2blazor/`
- When a new project is created:
  - The new project is created under `.webforms2blazor/…`.
  - The new project is **added to the solution (.sln)** using:
    - `dotnet sln <slnPath> add <projectPath>`
  - Modifying the `.sln` file in this way is explicitly allowed.

---

## 2. Tech Stack & Constraints

- Runtime: **.NET 10** for all projects.
- Language: **C#** only.
- MCP implementation: [`modelcontextprotocol/csharp-sdk`](https://github.com/modelcontextprotocol/csharp-sdk).
- Transport: **stdio** MCP server.
- Avoid any additional stacks (TypeScript, Python, Node, etc.) unless absolutely unavoidable.

Project-wide conventions:

```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
```

---

## 3. Solution Layout

Create this structure:

```text
WebFormsToBlazor/
  WebFormsToBlazor.sln

  src/
    WebFormsToBlazor.McpServer/    # MCP server entry point + tool definitions
    WebFormsToBlazor.Analysis/     # Read-only Web Forms solution analysis
    WebFormsToBlazor.Planning/     # plan.md model + parser/serializer
    WebFormsToBlazor.Conversion/   # Plan executor + Blazor generation

  docs/
    plan-format.md                 # Details of plan.md format
    usage.md                       # How to run the MCP server & tools
```

All four projects are added to `WebFormsToBlazor.sln`.

---

## 4. Output Directory Rules (Critical)

Given a solution:

```text
<solutionDir>/LegacyWebForms.sln
```

The tool may **only write** under:

```text
<solutionDir>/.webforms2blazor/
```

Expected contents:

```text
.webforms2blazor/
  analysis.json
  plan.md
  blazor/
    Legacy.Blazor/
      Legacy.Blazor.csproj
      Pages/
        Index.razor
        Orders.razor
      ...
  logs/
    execute-plan-YYYYMMDD-HHMMSS.log
```

Additionally:

- It is allowed to modify the **solution file** (`.sln`) to **add** new generated projects.
- No other files outside `.webforms2blazor/` may be created or modified.

**Do not**:

- Modify any existing Web Forms project file.
- Modify any Web Forms source file.
- Rewrite or move existing assets.

---

## 5. Project Responsibilities

### 5.1 WebFormsToBlazor.Analysis

**Responsibilities:**

- Perform **read-only** analysis of the existing Web Forms solution.
- Use MSBuild/Roslyn (e.g., `MSBuildWorkspace`) to:
  - Detect projects that depend on `System.Web` / `System.Web.UI`.
  - Enumerate `.aspx`, `.ascx`, `.master` and associated code-behind files.
  - Identify master pages and usage.
- Produce a serializable `AnalysisModel`.
- Write the analysis model as JSON to:
  - `<solutionDir>/.webforms2blazor/analysis.json`

**Key types:**

```csharp
public sealed class SolutionAnalyzer
{
    public Task<AnalysisModel> AnalyzeAsync(
        string solutionPath,
        CancellationToken cancellationToken);
}

public sealed class AnalysisModel
{
    public required string SolutionPath { get; init; }
    public List<WebFormsProjectInfo> WebFormsProjects { get; init; } = [];
    public string ToJson();
    public AnalysisSummary ToSummary();
}
```

Implementation notes:

- `solutionPath` can be relative or absolute; normalize to full path.
- Never write anything in the Web Forms project trees.
- Only write `analysis.json` under `.webforms2blazor/`.

---

### 5.2 WebFormsToBlazor.Planning

Handles the concept of a **plan** and the `plan.md` file.

#### 5.2.1 Plan Model

```csharp
public enum PlanStepKind
{
    CreateBlazorServerProject,
    ConfigureSharedServices,
    PageConversion
    // future: AuthMigration, StaticFiles, etc.
}

public sealed class PlanStep
{
    public required string Id { get; init; }
    public required PlanStepKind Kind { get; init; }
    public required Dictionary<string, string> Params { get; init; }
    public List<string> DependsOn { get; init; } = [];
}

public sealed class MigrationPlan
{
    public string Version { get; init; } = "1";
    public required string SolutionPath { get; init; }
    public List<PlanStep> Steps { get; init; } = [];
}
```

#### 5.2.2 Plan Location

`plan.md` is stored at:

```text
<solutionDir>/.webforms2blazor/plan.md
```

It is **user-owned**:

- The MCP and LLM generate an initial version.
- The user can open and edit this file freely.
- On every execution, the latest `plan.md` must be re-read and treated as the source of truth.

#### 5.2.3 Plan Format (Markdown)

The file is human-readable markdown with machine-parseable fenced blocks.

Example:

```markdown
# WebForms → Blazor Server Migration Plan

<!-- plan-meta:
{
  "version": 1,
  "solutionPath": "LegacyWebForms.sln"
}
-->

## Global steps

```plan-step
id: Infra-CreateBlazorServerProject
kind: CreateBlazorServerProject
params:
  projectName: Legacy.Blazor
  relativeOutputDir: blazor/Legacy.Blazor
dependsOn: []
```

```plan-step
id: Infra-ConfigureSharedServices
kind: ConfigureSharedServices
params:
  legacyProject: src/LegacyWebForms/LegacyWebForms.csproj
  blazorProject: blazor/Legacy.Blazor/Legacy.Blazor.csproj
dependsOn:
  - Infra-CreateBlazorServerProject
```

## Page migrations

```plan-step
id: Page-Home
kind: PageConversion
params:
  sourceAspx: src/LegacyWebForms/Default.aspx
  targetRazor: blazor/Legacy.Blazor/Pages/Index.razor
  route: "/"
dependsOn:
  - Infra-CreateBlazorServerProject
  - Infra-ConfigureSharedServices
```
```

- Fenced blocks with language `plan-step` contain the structured step definitions.
- The rest of the markdown is comments / explanations for the user.

#### 5.2.4 PlanService

```csharp
public sealed class PlanService
{
    public Task<string?> ReadPlanMarkdownAsync(
        string outputRoot,
        CancellationToken cancellationToken);

    public Task WritePlanMarkdownAsync(
        string outputRoot,
        string content,
        CancellationToken cancellationToken);

    public Task<MigrationPlan> LoadPlanAsync(
        string outputRoot,
        string solutionPath,
        CancellationToken cancellationToken);

    public Task SavePlanAsync(
        string outputRoot,
        MigrationPlan plan,
        CancellationToken cancellationToken);
}
```

Responsibilities:

- Compute paths relative to `outputRoot`.
- Read `plan.md` as text (if present).
- Parse all ```plan-step fenced blocks into `PlanStep` instances.
- Serialize a `MigrationPlan` back into markdown with fenced blocks.
- **Never** write outside `outputRoot`.

---

### 5.3 WebFormsToBlazor.Conversion

Executes a `MigrationPlan` and performs the actual generation into `.webforms2blazor/`.

#### 5.3.1 ConverterEngine

A lower-level service that performs specific actions:

- Create Blazor Server project.
- Configure shared services (e.g., dependency injection bridging).
- Convert individual Web Forms pages to Blazor components.

Must **never** write outside `outputRoot`.

#### 5.3.2 PlanExecutor

```csharp
public sealed class PlanExecutor
{
    private readonly ConverterEngine _converter;

    public PlanExecutor(ConverterEngine converter)
    {
        _converter = converter;
    }

    public Task<ExecutePlanResult> ExecuteAsync(
        MigrationPlan plan,
        string outputRoot,
        string? fromStepId,
        string? toStepId,
        bool dryRun,
        CancellationToken ct);

    public Task<ExecutePlanResult> ExecuteSingleAsync(
        MigrationPlan plan,
        string outputRoot,
        string stepId,
        bool dryRun,
        CancellationToken ct);
}

public sealed class ExecutePlanResult
{
    public List<ExecutedStepResult> ExecutedSteps { get; init; } = [];
}

public sealed class ExecutedStepResult
{
    public required string Id { get; init; }
    public required string Status { get; init; } // "success" | "warning" | "failed"
    public string? Details { get; init; }
}
```

Execution behavior:

- Re-read and parse `plan.md` on each run (via `PlanService`).
- Topologically sort steps based on `DependsOn`.
- Support:
  - Executing all steps.
  - Executing a range (`fromStepId` .. `toStepId`).
  - Executing a single step.
- When `dryRun = true`, only simulate and report what would be done (no writes).

#### 5.3.3 Output Path Guard

Implement a helper to guarantee all writes are under `outputRoot`:

```csharp
private static string EnsureUnderOutputRoot(string outputRoot, string relativePath)
{
    var combined = Path.Combine(outputRoot, relativePath);
    var full = Path.GetFullPath(combined);
    var fullRoot = Path.GetFullPath(outputRoot);

    if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"Output path '{full}' escapes output root '{fullRoot}'.");
    }

    return full;
}
```

Use this in all generation code.

#### 5.3.4 CreateBlazorServerProject Step

For a step like:

```yaml
kind: CreateBlazorServerProject
params:
  projectName: Legacy.Blazor
  relativeOutputDir: blazor/Legacy.Blazor
```

Executor must:

1. Compute target directory under output root:

   ```csharp
   var projectDir = EnsureUnderOutputRoot(outputRoot, step.Params["relativeOutputDir"]);
   ```

2. Run `dotnet new blazorserver`:

   ```bash
   dotnet new blazorserver -n Legacy.Blazor -o <projectDir>
   ```

3. Add the project to the solution:

   - Get solution directory from `plan.SolutionPath`.
   - Normalize to full path.
   - Run:

   ```bash
   dotnet sln <slnPath> add <projectDir>/Legacy.Blazor.csproj
   ```

4. Do **not** touch any existing Web Forms projects.

---

## 6. WebFormsToBlazor.McpServer

Implements the actual MCP server using the C# SDK.

### 6.1 Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WebFormsToBlazor.Analysis;
using WebFormsToBlazor.Planning;
using WebFormsToBlazor.Conversion;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    // MCP best practice: log to stderr
    o.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton<SolutionAnalyzer>()
    .AddSingleton<PlanService>()
    .AddSingleton<ConverterEngine>()
    .AddSingleton<PlanExecutor>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // scans for [McpServerToolType] tools

await builder.Build().RunAsync();
```

---

### 6.2 MCP Tools

Create a static class `MigrationTools` annotated with `[McpServerToolType]`.

Helper to compute `outputRoot`:

```csharp
private static string GetOutputRoot(string solutionPath)
{
    var fullSolutionPath = Path.GetFullPath(solutionPath);
    var solutionDir = Path.GetDirectoryName(fullSolutionPath)
                     ?? throw new InvalidOperationException("No solution directory.");
    var outputRoot = Path.Combine(solutionDir, ".webforms2blazor");
    Directory.CreateDirectory(outputRoot);
    return outputRoot;
}
```

#### 6.2.1 analyzeSolution

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;
using WebFormsToBlazor.Analysis;
using WebFormsToBlazor.Planning;
using WebFormsToBlazor.Conversion;

[McpServerToolType]
public static class MigrationTools
{
    [McpServerTool, Description("Analyze a WebForms solution and produce analysis.json under .webforms2blazor.")]
    public static async Task<AnalysisSummary> AnalyzeSolution(
        [Description("Path to the .sln file.")] string solutionPath,
        SolutionAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        var analysis = await analyzer.AnalyzeAsync(solutionPath, cancellationToken);

        var analysisPath = Path.Combine(outputRoot, "analysis.json");
        await File.WriteAllTextAsync(
            analysisPath,
            analysis.ToJson(),
            cancellationToken);

        return analysis.ToSummary();
    }
```

#### 6.2.2 readPlan

```csharp
    [McpServerTool, Description("Read the current plan.md from .webforms2blazor.")]
    public static async Task<string?> ReadPlan(
        string solutionPath,
        PlanService planService,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        return await planService.ReadPlanMarkdownAsync(outputRoot, cancellationToken);
    }
```

#### 6.2.3 writePlan

```csharp
    [McpServerTool, Description("Write plan.md to .webforms2blazor (overwrite).")]
    public static async Task WritePlan(
        string solutionPath,
        string content,
        PlanService planService,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        await planService.WritePlanMarkdownAsync(outputRoot, content, cancellationToken);
    }
```

#### 6.2.4 executePlan

```csharp
    [McpServerTool, Description("Execute steps from plan.md in dependency order.")]
    public static async Task<ExecutePlanResult> ExecutePlan(
        string solutionPath,
        string? fromStepId,
        string? toStepId,
        bool dryRun,
        PlanService planService,
        PlanExecutor executor,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        var plan = await planService.LoadPlanAsync(outputRoot, solutionPath, cancellationToken);

        return await executor.ExecuteAsync(
            plan,
            outputRoot,
            fromStepId,
            toStepId,
            dryRun,
            cancellationToken);
    }
```

#### 6.2.5 executePlanStep

```csharp
    [McpServerTool, Description("Execute a single step from plan.md by ID.")]
    public static async Task<ExecutePlanResult> ExecutePlanStep(
        string solutionPath,
        string stepId,
        bool dryRun,
        PlanService planService,
        PlanExecutor executor,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        var plan = await planService.LoadPlanAsync(outputRoot, solutionPath, cancellationToken);

        return await executor.ExecuteSingleAsync(
            plan,
            outputRoot,
            stepId,
            dryRun,
            cancellationToken);
    }

    // GetOutputRoot helper lives here
}
```

---

## 7. Expected Agent Workflow

1. User: “Analyze this Web Forms solution and create a migration plan.”
2. LLM calls `AnalyzeSolution(solutionPath)`.
3. LLM inspects `analysis.json` (if needed) and generates an initial `plan.md`.
4. LLM calls `WritePlan(solutionPath, content)` to write `plan.md`.
5. User edits `.webforms2blazor/plan.md` manually (reorder, delete, add steps).
6. User: “Run the plan up through Orders page.”
7. LLM calls `ExecutePlan(solutionPath, fromStepId: null, toStepId: "Page-Orders", dryRun: false)`.
8. PlanExecutor:
   - Reads `plan.md`.
   - Resolves steps and dependencies.
   - Creates new Blazor Server project under `.webforms2blazor/…`.
   - Adds the Blazor project to the solution via `dotnet sln add`.
   - Generates `.razor` files under `.webforms2blazor/…`.
9. LLM summarizes results and may suggest edits to `plan.md`.

---

## 8. Invariants to Always Respect

- **Web Forms code is read-only.**
- All generated artifacts must live under `.webforms2blazor/`.
- The only allowed modification outside `.webforms2blazor/` is:
  - Updating the `.sln` file via `dotnet sln <slnPath> add <csprojPath>` to reference new projects under `.webforms2blazor/`.
- `plan.md` is user-editable and must be re-read and respected on every execution.
- Steps must be executed in dependency order (`DependsOn`).
- `dryRun` mode must not write anything (just simulate actions).

---
