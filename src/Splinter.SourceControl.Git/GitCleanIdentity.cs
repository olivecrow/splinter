using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Splinter.Core;

namespace Splinter.SourceControl.Git;

internal enum GitCleanRequirement { None, Always, NoCarriageReturn, NoCarriageReturnOrNul }

// An operation-local proof of a narrow subset of Git clean conversion. Decisions
// use the exact config/attribute output included in the source interpretation
// digest; the caller rechecks that digest and the source generation before ACK.
internal sealed class GitCleanIdentity
{
    private sealed class Attributes
    {
        public string? Text, Eol, Crlf;
        public bool OtherConversion;
    }

    private readonly Dictionary<string, Attributes> attributes = new(StringComparer.Ordinal);
    private readonly bool enabled;
    private readonly bool autoCrlf;
    private bool valid = true;

    public GitCleanIdentity(string config)
    {
        string? auto = "false", safe = "false";
        foreach (string entry in config.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = entry.IndexOf('\n');
            string key = separator < 0 ? entry : entry[..separator];
            string? value = separator < 0 ? null : entry[(separator + 1)..];
            if (key.Equals("core.autocrlf", StringComparison.OrdinalIgnoreCase)) auto = value;
            if (key.Equals("core.safecrlf", StringComparison.OrdinalIgnoreCase)) safe = value;
        }
        // Leave validation/warnings for unsupported values and safecrlf to Git.
        enabled = IsFalse(safe) && (IsFalse(auto) || IsTrue(auto) || auto == "input");
        autoCrlf = !IsFalse(auto);
    }

    public void AddAttributes(string output)
    {
        if (!enabled || !valid || output.Length == 0) return;
        string[] fields = output.Split('\0');
        if (fields[^1] != "" || (fields.Length - 1) % 3 != 0) { valid = false; return; }
        for (int i = 0; i < fields.Length - 1; i += 3)
        {
            if (!attributes.TryGetValue(fields[i], out var value))
                attributes.Add(fields[i], value = new());
            switch (fields[i + 1])
            {
                case "text": value.Text = fields[i + 2]; break;
                case "eol": value.Eol = fields[i + 2]; break;
                case "crlf": value.Crlf = fields[i + 2]; break;
                // Even unset is conservative: check-attr's text output does not
                // distinguish -filter from the literal driver name filter=unset.
                case "filter": case "working-tree-encoding": case "ident": value.OtherConversion = true; break;
            }
        }
    }

    public GitCleanRequirement Requirement(string path)
    {
        if (!enabled || !valid) return GitCleanRequirement.None;
        attributes.TryGetValue(path, out var value);
        if (value?.OtherConversion == true) return GitCleanRequirement.None;
        string? text = value?.Text;
        bool noEolOverride = value?.Eol is null && value?.Crlf is null;
        // "unset" may mean either -text or literal text=unset (unspecified to
        // Git). Accept only conditions safe under both interpretations.
        bool unspecifiedOrBinary = text is null or "unset" or "unspecified";
        if (noEolOverride && unspecifiedOrBinary && !autoCrlf) return GitCleanRequirement.Always;
        if (text == "auto" || noEolOverride && unspecifiedOrBinary)
            return GitCleanRequirement.NoCarriageReturnOrNul;
        return GitCleanRequirement.NoCarriageReturn;
    }

    internal static async Task<(string RawHash, string? GitHash)> HashAsync(Stream stream,
        GitCleanRequirement requirement, HashAlgorithmName algorithm, CancellationToken ct)
    {
        const int blockBytes = 1024 * 1024;
        long expectedLength = stream.Length;
        if (stream.Position != 0 || expectedLength < 0 || requirement == GitCleanRequirement.None)
            throw new ArgumentException("A content identity proof requires a complete source stream.", nameof(stream));
        using var raw = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var git = IncrementalHash.CreateHash(algorithm);
        git.AppendData(Encoding.ASCII.GetBytes("blob " + expectedLength.ToString(CultureInfo.InvariantCulture) + "\0"));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(blockBytes);
        try
        {
            bool hasCr = false, hasNul = false;
            long read = 0;
            int count;
            while ((count = await stream.ReadAsync(buffer.AsMemory(0, blockBytes), ct)) != 0)
            {
                read = checked(read + count);
                if (read > expectedLength) throw Changed();
                raw.AppendData(buffer.AsSpan(0, count));
                git.AppendData(buffer.AsSpan(0, count));
                if (requirement != GitCleanRequirement.Always)
                {
                    if (!hasCr) hasCr = buffer.AsSpan(0, count).Contains((byte)'\r');
                    if (!hasNul && requirement == GitCleanRequirement.NoCarriageReturnOrNul)
                        hasNul = buffer.AsSpan(0, count).Contains((byte)0);
                }
            }
            ct.ThrowIfCancellationRequested();
            if (read != expectedLength || stream.Length != expectedLength) throw Changed();
            // Git convert.c gathers statistics over the entire input. A NUL is
            // sufficient (not necessary) for its automatic binary decision;
            // explicit text must use the stricter no-CR proof. We never infer
            // identity from Git's separate diff binary-prefix heuristic. Limit
            // this sufficient condition to avoid overflow of Git's unsigned
            // statistics (and unsigned-long input length on Windows).
            bool identity = requirement == GitCleanRequirement.Always || !hasCr ||
                requirement == GitCleanRequirement.NoCarriageReturnOrNul && hasNul && expectedLength <= uint.MaxValue;
            return (Convert.ToHexStringLower(raw.GetHashAndReset()),
                identity ? Convert.ToHexStringLower(git.GetHashAndReset()) : null);
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private static WorkspaceException Changed() => new("SourceChanged", "Source length changed during content verification; preserve edits and retry.");
    private static bool IsFalse(string? value) => value is not null && (value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("off", StringComparison.OrdinalIgnoreCase) || value is "" or "0");
    private static bool IsTrue(string? value) => value is null || value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase) || value == "1";
}
