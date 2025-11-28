using WebFormsToBlazor.Analysis;
using WebFormsToBlazor.Planning;
using WebFormsToBlazor.Conversion;

namespace WebFormsToBlazor.McpServer;

public static class MigrationTools
{
    // Placeholder methods: remove MCP attributes until MCP package is available.
    public static async Task<AnalysisSummary> AnalyzeSolution(
        string solutionPath,
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

    public static async Task<string?> ReadPlan(
        string solutionPath,
        PlanService planService,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        return await planService.ReadPlanMarkdownAsync(outputRoot, cancellationToken);
    }

    public static async Task WritePlan(
        string solutionPath,
        string content,
        PlanService planService,
        CancellationToken cancellationToken)
    {
        var outputRoot = GetOutputRoot(solutionPath);
        await planService.WritePlanMarkdownAsync(outputRoot, content, cancellationToken);
    }

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

    private static string GetOutputRoot(string solutionPath)
    {
        var fullSolutionPath = Path.GetFullPath(solutionPath);
        var solutionDir = Path.GetDirectoryName(fullSolutionPath)
                         ?? throw new InvalidOperationException("No solution directory.");
        var outputRoot = Path.Combine(solutionDir, ".webforms2blazor");
        Directory.CreateDirectory(outputRoot);
        return outputRoot;
    }
}