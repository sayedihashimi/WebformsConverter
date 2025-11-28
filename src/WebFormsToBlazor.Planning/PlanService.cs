using System.Text;

namespace WebFormsToBlazor.Planning;

public sealed class PlanService
{
    public async Task<string?> ReadPlanMarkdownAsync(string outputRoot, CancellationToken cancellationToken)
    {
        var planPath = Path.Combine(outputRoot, "plan.md");
        if (!File.Exists(planPath)) return null;
        return await File.ReadAllTextAsync(planPath, cancellationToken);
    }

    public async Task WritePlanMarkdownAsync(string outputRoot, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputRoot);
        var planPath = Path.Combine(outputRoot, "plan.md");
        await File.WriteAllTextAsync(planPath, content, cancellationToken);
    }

    public async Task<MigrationPlan> LoadPlanAsync(string outputRoot, string solutionPath, CancellationToken cancellationToken)
    {
        var markdown = await ReadPlanMarkdownAsync(outputRoot, cancellationToken) ?? string.Empty;
        var plan = new MigrationPlan { SolutionPath = solutionPath };
        plan.Steps = ParseSteps(markdown);
        return plan;
    }

    public async Task SavePlanAsync(string outputRoot, MigrationPlan plan, CancellationToken cancellationToken)
    {
        var md = SerializePlan(plan);
        await WritePlanMarkdownAsync(outputRoot, md, cancellationToken);
    }

    private static List<PlanStep> ParseSteps(string markdown)
    {
        var steps = new List<PlanStep>();
        var fence = "```plan-step";
        int idx = 0;
        while (true)
        {
            var start = markdown.IndexOf(fence, idx, StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            var end = markdown.IndexOf("```", start + fence.Length, StringComparison.OrdinalIgnoreCase);
            if (end < 0) break;
            var block = markdown.Substring(start + fence.Length, end - (start + fence.Length));
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var dict = new Dictionary<string, string>();
            string id = string.Empty;
            PlanStepKind kind = PlanStepKind.CreateBlazorServerProject;
            List<string> depends = new();
            bool inParams = false;
            bool inDepends = false;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.StartsWith("id:")) id = line[3..].Trim();
                else if (line.StartsWith("kind:")) kind = Enum.Parse<PlanStepKind>(line[5..].Trim(), ignoreCase: true);
                else if (line.StartsWith("params:")) { inParams = true; inDepends = false; }
                else if (line.StartsWith("dependsOn:")) { inDepends = true; inParams = false; }
                else if (inParams && line.Contains(':'))
                {
                    var p = line.Split(':', 2);
                    dict[p[0].Trim()] = p[1].Trim();
                }
                else if (inDepends)
                {
                    if (line.StartsWith("- ")) depends.Add(line[2..].Trim());
                }
            }
            steps.Add(new PlanStep { Id = id, Kind = kind, Params = dict, DependsOn = depends });
            idx = end + 3;
        }
        return steps;
    }

    private static string SerializePlan(MigrationPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# WebForms ? Blazor Server Migration Plan");
        sb.AppendLine("\n<!-- plan-meta:");
        sb.AppendLine($"{{\n  \"version\": 1,\n  \"solutionPath\": \"{plan.SolutionPath}\"\n}}" );
        sb.AppendLine("-->");
        sb.AppendLine();
        foreach (var step in plan.Steps)
        {
            sb.AppendLine("```plan-step");
            sb.AppendLine($"id: {step.Id}");
            sb.AppendLine($"kind: {step.Kind}");
            sb.AppendLine("params:");
            foreach (var kv in step.Params)
            {
                sb.AppendLine($"  {kv.Key}: {kv.Value}");
            }
            sb.AppendLine("dependsOn:");
            foreach (var d in step.DependsOn)
            {
                sb.AppendLine($"  - {d}");
            }
            sb.AppendLine("```");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}