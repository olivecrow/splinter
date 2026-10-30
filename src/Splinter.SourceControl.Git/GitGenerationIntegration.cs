using Splinter.Core;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider : IWorkspaceGenerationBranchSource
{
    public async Task<IntegrationResult> IntegrateGenerationAsync(WorkspaceRecord w, WorkspaceResultGeneration generation, string target, CancellationToken ct = default)
    {
        if (generation.GitReference is null || await GitSourceResult.VerifyAsync(w.ProjectPath, generation.GitReference, ct) != generation.GitFingerprint)
            throw Error("GitResultIncomplete", "Keep the missing or changed independent source generation.");
        string revision = await Required(w.ProjectPath, ["rev-parse", generation.GitReference], ct);
        return await MergeRevisionAsync(w, revision, target, ct);
    }
}
