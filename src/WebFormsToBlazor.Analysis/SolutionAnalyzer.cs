using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebFormsToBlazor.Analysis;

public sealed class SolutionAnalyzer
{
    public async Task<AnalysisModel> AnalyzeAsync(string solutionPath, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(solutionPath);
        // TODO: Use MSBuildWorkspace to analyze projects for System.Web dependencies and pages.
        var model = new AnalysisModel
        {
            SolutionPath = full,
            WebFormsProjects = []
        };
        return await Task.FromResult(model);
    }
}

public sealed class AnalysisModel
{
    public required string SolutionPath { get; init; }
    public List<WebFormsProjectInfo> WebFormsProjects { get; init; } = [];

    public string ToJson()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        return JsonSerializer.Serialize(this, opts);
    }

    public AnalysisSummary ToSummary()
    {
        return new AnalysisSummary
        {
            SolutionPath = SolutionPath,
            ProjectCount = WebFormsProjects.Count
        };
    }
}

public sealed class WebFormsProjectInfo
{
    public required string ProjectPath { get; init; }
    public List<string> AspxPages { get; init; } = [];
    public List<string> UserControls { get; init; } = [];
    public List<string> MasterPages { get; init; } = [];
}

public sealed class AnalysisSummary
{
    public required string SolutionPath { get; init; }
    public int ProjectCount { get; init; }
}