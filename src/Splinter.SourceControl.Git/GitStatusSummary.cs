using System.Text;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

internal sealed record GitStatusSummary(string Preview, long Entries, bool Truncated);

public sealed partial class GitWorkspaceProvider
{
    private static async Task<string> ReadBoundedOutput(StreamReader reader)
    {
        char[] buffer = new char[4096]; var prefix = new StringBuilder();
        for (int count; (count = await reader.ReadAsync(buffer)) != 0;)
            if (prefix.Length < 16384) prefix.Append(buffer, 0, Math.Min(count, 16384 - prefix.Length));
        return prefix.ToString();
    }
    // Always drain Git, including when the caller only needs a clean/dirty
    // decision. A project-sized porcelain listing never becomes one string.
    internal static GitStatusSummary ObserveStatus(string project, CancellationToken ct, bool preview = true)
    {
        using var records = GitRecords(project, ["status", "--porcelain=v1", "-z", "--untracked-files=all"], ct).GetEnumerator();
        var text = new StringBuilder(); long entries = 0; int shown = 0; bool truncated = false;
        while (records.MoveNext())
        {
            string row = records.Current;
            if (row.Length < 4) throw new WorkspaceException("GitStatusInvalid", "Unexpected Git status record.");
            string? original = null;
            if (row[0] is 'R' or 'C' || row[1] is 'R' or 'C')
            {
                if (!records.MoveNext()) throw new WorkspaceException("GitStatusInvalid", "The renamed status entry has no original path.");
                original = records.Current;
            }
            entries = checked(entries + 1);
            if (!preview || shown == 100 || text.Length >= 16384) { truncated = true; continue; }
            if (text.Length != 0) text.Append('\n'); shown++;
            Append(row);
            if (original is not null) { Append(" <- "); Append(original); }
        }
        return new(text.ToString(), entries, truncated);
        void Append(string value)
        {
            int take = Math.Min(value.Length, Math.Min(512, 16384 - text.Length));
            if (take > 0) text.Append(value.AsSpan(0, take));
            if (take != value.Length) truncated = true;
        }
    }
}
