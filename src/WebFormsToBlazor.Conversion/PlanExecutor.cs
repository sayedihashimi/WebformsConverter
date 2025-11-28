using WebFormsToBlazor.Planning;

namespace WebFormsToBlazor.Conversion;

public sealed class PlanExecutor
{
    private readonly ConverterEngine _converter;

    public PlanExecutor(ConverterEngine converter)
    {
        _converter = converter;
    }

    public async Task<ExecutePlanResult> ExecuteAsync(
        MigrationPlan plan,
        string outputRoot,
        string? fromStepId,
        string? toStepId,
        bool dryRun,
        CancellationToken ct)
    {
        var steps = OrderSteps(plan.Steps);
        if (fromStepId != null)
            steps = steps.SkipWhile(s => s.Id != fromStepId).ToList();
        if (toStepId != null)
            steps = steps.TakeWhile(s => s.Id != toStepId).Append(steps.First(s => s.Id == toStepId)).ToList();
        return await ExecuteSteps(plan, outputRoot, steps, dryRun, ct);
    }

    public async Task<ExecutePlanResult> ExecuteSingleAsync(
        MigrationPlan plan,
        string outputRoot,
        string stepId,
        bool dryRun,
        CancellationToken ct)
    {
        var steps = OrderSteps(plan.Steps);
        var target = steps.First(s => s.Id == stepId);
        // include dependencies before target
        var index = steps.IndexOf(target);
        steps = steps.Take(index + 1).ToList();
        return await ExecuteSteps(plan, outputRoot, steps, dryRun, ct);
    }

    private async Task<ExecutePlanResult> ExecuteSteps(
        MigrationPlan plan,
        string outputRoot,
        List<PlanStep> steps,
        bool dryRun,
        CancellationToken ct)
    {
        var result = new ExecutePlanResult();
        foreach (var step in steps)
        {
            (bool ok, string details) = step.Kind switch
            {
                PlanStepKind.CreateBlazorServerProject => await _converter.CreateBlazorServerProjectAsync(outputRoot, plan.SolutionPath, step, dryRun, ct),
                _ => (true, "No-op")
            };
            result.ExecutedSteps.Add(new ExecutedStepResult
            {
                Id = step.Id,
                Status = ok ? "success" : "failed",
                Details = details
            });
            if (!ok) break;
        }
        return result;
    }

    private static List<PlanStep> OrderSteps(List<PlanStep> steps)
    {
        var map = steps.ToDictionary(s => s.Id);
        var visited = new HashSet<string>();
        var output = new List<PlanStep>();

        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            var step = map[id];
            foreach (var dep in step.DependsOn)
            {
                Visit(dep);
            }
            visited.Add(id);
            output.Add(step);
        }

        foreach (var s in steps)
        {
            Visit(s.Id);
        }
        return output;
    }
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