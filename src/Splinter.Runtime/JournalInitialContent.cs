using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private readonly bool initialContentGeneration;

    public SourceContentSummaryView? ObserveInitialContent(string[] managedPaths,
        SourceChangeView expected, string[] requestedPaths)
    {
        using (EnterGate())
        {
            Check();
            if (requestedPaths.Length > 4096 || requestedPaths.Distinct(StringComparer.Ordinal).Count() != requestedPaths.Length)
                throw new WorkspaceException("SourceObservationInvalid", "Content evidence requests must contain at most 4096 unique paths.");
            var before = ObserveChanges(managedPaths);
            RequireGeneration(before);
            if (!initialContentGeneration || snapshot is not ISnapshotContentSummaryProvider provider) return null;
            var summaries = new List<SnapshotContentSummary>();
            foreach (string requested in requestedPaths)
            {
                string path = NormalizePath(requested);
                if (path != requested || path.Length == 0)
                    throw new WorkspaceException("SourceObservationInvalid", "Content evidence requires exact normalized file paths.");
                if (pathPolicy.Managed(path, managedPaths) || before.Changes.Any(change =>
                    path.Equals(change.Path, pathPolicy.Comparison) || pathPolicy.IsChild(path, change.Path))) continue;
                var node = Lookup(path);
                if (node is null || node.Entry.Directory || node.Entry.BasePath != path ||
                    node.Entry.Length != node.Entry.BaseLength || node.Extents.Count != 0) continue;
                var summary = provider.GetVerifiedContentSummary(path);
                if (summary is null) continue;
                if (summary.Path != path || summary.Length != node.Entry.Length || summary.Sha256 is not { Length: 64 } ||
                    !summary.Sha256.All(char.IsAsciiHexDigit))
                    throw new WorkspaceException("SourceObservationInvalid", "Immutable content evidence does not match its fixed file identity.");
                summaries.Add(summary);
            }
            Check();
            RequireGeneration(ObserveChanges(managedPaths));
            return new(Identity.Snapshot, before, summaries.ToArray());

            void RequireGeneration(SourceChangeView current)
            {
                if (current.WorkspaceId != expected.WorkspaceId || current.SnapshotId != expected.SnapshotId ||
                    current.Epoch != expected.Epoch || current.Sequence != expected.Sequence ||
                    !current.Changes.SequenceEqual(expected.Changes))
                    throw new WorkspaceException("SourceChanged", "The initial source generation changed; preserve and inspect the current source.");
            }
        }
    }
}
