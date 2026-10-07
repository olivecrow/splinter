using System.Text;

namespace Splinter.SourceControl.Git;

public sealed partial class GitWorkspaceProvider
{
    private const int AttributeInputBytes = 256 * 1024;

    // NUL stdin avoids argument-length limits and preserves literal filenames.
    // Keep output incremental so a repository-wide query does not accumulate
    // every attribute result in another whole-tree string.
    internal static async Task AttributeOutputAsync(string cwd, IReadOnlyList<string> paths,
        bool cached, bool all, Action<string> consume, CancellationToken ct)
    {
        string[] args = ["check-attr", .. cached ? new[] { "--cached" } : [], "-z", "--stdin",
            .. all ? new[] { "--all" } : new[] { "filter", "working-tree-encoding" }];
        int offset = 0;
        while (offset < paths.Count)
        {
            ct.ThrowIfCancellationRequested();
            var input = new StringBuilder();
            int bytes = 0;
            while (offset < paths.Count)
            {
                string path = paths[offset];
                if (path.Contains('\0')) throw Error("GitUnsafePath", "A source path contains the attribute-query delimiter.");
                int size = Encoding.UTF8.GetByteCount(path) + 1;
                // A single path remains indivisible, as it was in argv queries.
                if (bytes != 0 && size > AttributeInputBytes - bytes) break;
                input.Append(path).Append('\0');
                bytes += size;
                offset++;
            }
            var result = await Run(cwd, args, ct, input.ToString());
            if (result.ExitCode != 0) throw Error("GitCommandFailed", result.Error.Trim());
            consume(result.Output.TrimEnd('\r', '\n'));
        }
    }
}
