using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Splinter.Core;

namespace Splinter.SourceControl.Files;

public sealed record PlasticRevisionEntry(string Path, bool Directory, string Revision, long Length, string ContentHash, long Timestamp);
public sealed record PlasticRevisionManifest(string TreeSpec, string RepositoryId, string RootRevision, string RootGuid, PlasticRevisionEntry[] Entries, string Repository);
public interface IPlasticRevisionReader
{
    Stream OpenRevision(string treeSpec, string revision);
}
public interface IPlasticRevisionClient : IPlasticRevisionReader
{
    Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default);
    Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default);
    IPlasticRevisionReader CreateReadSession() => this;
}

// Only fixed tree enumeration and private raw revision streams. No update, checkout,
// registration, check-in, server setting or payload file is created here.
public sealed class PlasticRevisionClient : IPlasticRevisionClient
{
    public static void RequireTreeSpec(string spec)
    {
        if (string.IsNullOrEmpty(spec)) throw Failure("PlasticBasisUnsupported", "A fixed recorded tree is required.");
        int at = spec.IndexOf('@');
        if (at < 1 || spec.Length > 2048 || !long.TryParse(spec.AsSpan(0, at), NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id < 0 ||
            spec[(at + 1)..].Length == 0 || spec.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is ';' or '"' or '\\' or '#'))
            throw Failure("PlasticBasisUnsupported", "Supply an exact numeric changeset@repository@server, never a moving branch.");
    }
    internal static WorkspaceException Failure(string code, string message) => new(code, message);
    private static Process Start(string[] args, string? workingDirectory = null)
    {
        var info = new ProcessStartInfo("cm") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (workingDirectory is not null) info.WorkingDirectory = workingDirectory;
        foreach (string arg in args) info.ArgumentList.Add(arg);
        try { return Process.Start(info) ?? throw Failure("PlasticBasisUnavailable", "The fixed revision client did not start."); }
        catch (System.ComponentModel.Win32Exception) { throw Failure("PlasticBasisUnavailable", "The fixed revision client is unavailable."); }
    }
    private static async Task<string> BoundedText(StreamReader reader, int limit, CancellationToken ct)
    {
        var output = new StringBuilder(); char[] scratch = new char[4096]; int n;
        while ((n = await reader.ReadAsync(scratch, ct)) != 0)
        { if (output.Length > limit - n) throw new IOException("Revision metadata output limit."); output.Append(scratch, 0, n); }
        return output.ToString();
    }
    public async Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default)
    {
        RequireTreeSpec(treeSpec);
        return Parse(treeSpec, await Listing(["ls", "/", "--tree=" + treeSpec, "-R", "--xml", "--encoding=utf-8"], null, ct));
    }
    public async Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default)
    {
        VerifyProjectListing(project, manifest, await Listing(["ls", project, "--xml", "--encoding=utf-8"], project, ct));
    }
    public static void VerifyProjectListing(string project, PlasticRevisionManifest manifest, string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
            var doc = XDocument.Load(reader);
            if (doc.Root?.Name != "LsResults") throw new FormatException();
            var root = doc.Root.Element("LsItems")?.Elements("LsItem").Single(n => n.Element("Name")?.Value == ".") ?? throw new FormatException();
            string path = Required(root, "CurrentPath");
            if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(project), StringComparison.OrdinalIgnoreCase) ||
                Required(root, "Status") != "Controlled" || Required(root, "Type") != "dir" ||
                root.Element("SymlinkTarget")?.Value is not (null or "") || Required(root, "RepId") != manifest.RepositoryId || Required(root, "Repository") != manifest.Repository)
                throw new FormatException();
        }
        catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException or ArgumentException)
        { throw Failure("PlasticBasisIdentityMismatch", "The local root must identify the exact original repository/server; private files do not identify the basis."); }
    }
    private static async Task<string> Listing(string[] arguments, string? workingDirectory, CancellationToken ct)
    {
        using var process = Start(arguments, workingDirectory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var output = BoundedText(process.StandardOutput, 16 * 1024 * 1024, timeout.Token);
            var error = BoundedText(process.StandardError, 16384, timeout.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(timeout.Token));
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(await error)) throw Failure("PlasticBasisUnavailable", "The fixed tree could not be observed.");
            return await output;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        { ct.ThrowIfCancellationRequested(); throw Failure("PlasticBasisUnavailable", "The fixed tree observation failed or exceeded its bounds."); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
    }
    public static PlasticRevisionManifest Parse(string treeSpec, string xml)
    {
        RequireTreeSpec(treeSpec);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
            var doc = XDocument.Load(reader); var nodes = doc.Root?.Element("LsItems")?.Elements("LsItem").ToArray();
            if (doc.Root?.Name != "LsResults" || nodes is null || nodes.Length is < 1 or > 32768) throw new FormatException();
            var root = nodes.Single(n => n.Element("CurrentPath")?.Value == "/");
            string repo = Required(root, "RepId"), rootRevision = Required(root, "RevId"), guid = Required(root, "Guid");
            if (!Guid.TryParseExact(guid, "D", out _) || !Numeric(repo) || !Numeric(rootRevision) || Required(root, "Type") != "dir") throw new FormatException();
            long pathBytes = 0; var entries = new List<PlasticRevisionEntry>();
            foreach (var node in nodes.Where(n => n != root))
            {
                string full = Required(node, "CurrentPath"); if (!full.StartsWith('/')) throw new FormatException();
                string path = full[1..]; string type = Required(node, "Type"), revision = Required(node, "RevId");
                if (!FileWorkspaceProvider.ValidPath(path) || !Numeric(revision) || Required(node, "RepId") != repo ||
                    node.Element("SymlinkTarget")?.Value is not (null or "") || type is not ("dir" or "txt" or "bin" or "file" or "File")) throw new FormatException();
                pathBytes += Encoding.UTF8.GetByteCount(path); if (pathBytes > 1024 * 1024) throw new FormatException();
                long length = long.Parse(Required(node, "Size"), CultureInfo.InvariantCulture);
                bool directory = type == "dir"; string hash = Required(node, "Hash");
                if (length < 0 || length > 8L * 1024 * 1024 * 1024 || directory && length != 0 || Convert.FromBase64String(hash).Length != 16) throw new FormatException();
                long timestamp = DateTimeOffset.Parse(Required(node, "Date"), CultureInfo.InvariantCulture).UtcDateTime.ToFileTimeUtc();
                entries.Add(new(path, directory, revision, length, hash, timestamp));
            }
            string repository = Required(root, "Repository"); if (repository.Length > 2048 || !repository.StartsWith("rep:") || repository.Any(char.IsControl)) throw new FormatException();
            return new(treeSpec, repo, rootRevision, guid, entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray(), repository);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException or ArgumentException or OverflowException)
        { throw Failure("PlasticBasisInvalid", "Preserve an invalid, ambiguous or unsupported fixed tree listing."); }
    }
    private static string Required(XElement node, string name) => node.Element(name)?.Value is { Length: > 0 } value ? value : throw new FormatException();
    internal static bool Numeric(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long n) && n >= 0;
    public Stream OpenRevision(string treeSpec, string revision)
    {
        var session = new PlasticRevisionStreamSession();
        try { return session.OpenOwnedRevision(treeSpec, revision); }
        catch { session.Dispose(); throw; }
    }
    public IPlasticRevisionReader CreateReadSession() => new PlasticRevisionStreamSession();
}
