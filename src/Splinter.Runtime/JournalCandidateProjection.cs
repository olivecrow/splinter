using System.Security.Cryptography;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private static bool ProjectionHash(string? value) => value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static HashSet<string> ValidateProjection(CandidateSourceProjection proof, OverlayIdentity identity)
    {
        if (proof.Version != 1 || proof.OwnerId != identity.Id || proof.Snapshot != identity.Snapshot ||
            !ProjectionHash(proof.EvidenceHash) || proof.Files is not { Length: > 0 and <= 16 })
            throw new WorkspaceException("CandidateProjectionInvalid", "Require current evidence for this exact owner and immutable snapshot.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in proof.Files)
        {
            if (file is null || string.IsNullOrWhiteSpace(file.Path) || file.Path.Length > 128 || file.Path is "." or ".." ||
                file.Path.Contains('/') || file.Path.Contains('\\') || file.Path.Contains(':') || file.Path.Any(char.IsControl) ||
                file.Path.EndsWith('.') || file.Path.EndsWith(' ') || file.Length is < 1 or > 4096 ||
                !ProjectionHash(file.ContentHash) || !paths.Add(file.Path) ||
                identity.ExcludedTopLevel?.Contains(file.Path, StringComparer.OrdinalIgnoreCase) != true)
                throw new WorkspaceException("CandidateProjectionInvalid", "Only bounded, distinct, already-excluded top-level private files can be projected.");
        }
        return paths;
    }

    private HashSet<string> QualifyProjection(CandidateSourceProjection proof)
    {
        var projected = ValidateProjection(proof, Identity);
        foreach (var file in proof.Files)
        {
            if (!paths.TryGetValue(file.Path, out var id) || !nodes.TryGetValue(id, out var node) ||
                node.Entry.Directory || node.Entry.Deleted || node.Entry.BasePath is not null ||
                node.Entry.BaseLength != 0 || node.Entry.Length != file.Length)
                throw new WorkspaceException("CandidateProjectionMismatch", "The source must contain the exact qualified private file.");
            byte[] bytes = new byte[file.Length];
            if (Read(id, 0, bytes) != bytes.Length || Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.ContentHash)
                throw new WorkspaceException("CandidateProjectionMismatch", "Managed-file content differs from the adapter evidence; preserve the source.");
        }
        return projected;
    }

    private static void ValidateProjectionMutation(Mutation mutation, IReadOnlySet<string> projected)
    {
        foreach (string path in projected)
        {
            bool exact = mutation.Entry.Path.Equals(path, StringComparison.OrdinalIgnoreCase);
            bool child = mutation.Entry.Path.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase);
            bool destination = mutation.Destination is { } to &&
                (to.Equals(path, StringComparison.OrdinalIgnoreCase) || to.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase));
            if (child || destination || exact && (mutation.Operation == "rename" || mutation.Entry.Directory || mutation.Entry.BasePath is not null))
                throw new WorkspaceException("CandidateProjectionHistoryUnsupported", "Managed-file history crosses the qualified private-file boundary; preserve it without projection.");
        }
    }
}
