namespace WebFormsToBlazor.Planning;

public enum PlanStepKind
{
    CreateBlazorServerProject,
    ConfigureSharedServices,
    PageConversion
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
    public List<PlanStep> Steps { get; set; } = [];
}
