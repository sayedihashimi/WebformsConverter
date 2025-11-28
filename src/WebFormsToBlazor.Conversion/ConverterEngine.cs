using System.Diagnostics;
using WebFormsToBlazor.Planning;

namespace WebFormsToBlazor.Conversion;

public sealed class ConverterEngine
{
    private static string EnsureUnderOutputRoot(string outputRoot, string relativePath)
    {
        var combined = Path.Combine(outputRoot, relativePath);
        var full = Path.GetFullPath(combined);
        var fullRoot = Path.GetFullPath(outputRoot);
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Output path '{full}' escapes output root '{fullRoot}'.");
        }
        return full;
    }

    public async Task<(bool ok, string details)> CreateBlazorServerProjectAsync(string outputRoot, string solutionPath, PlanStep step, bool dryRun, CancellationToken ct)
    {
        var name = step.Params["projectName"];
        var rel = step.Params["relativeOutputDir"];
        var projectDir = EnsureUnderOutputRoot(outputRoot, rel);
        if (dryRun) return (true, $"Would run: dotnet new blazorserver -n {name} -o {projectDir}");

        Directory.CreateDirectory(projectDir);
        var proc = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"new blazorserver -n {name} -o \"{projectDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });
        if (proc == null) return (false, "Failed to start dotnet.");
        await proc.WaitForExitAsync(ct);
        var output = await proc.StandardOutput.ReadToEndAsync();
        var error = await proc.StandardError.ReadToEndAsync();
        var success = proc.ExitCode == 0;
        var details = success ? output : error;
        return (success, details);
    }
}
