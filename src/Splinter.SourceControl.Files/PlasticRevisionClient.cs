using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Splinter.Core;

namespace Splinter.SourceControl.Files;

public sealed record PlasticRevisionEntry(string Path, bool Directory, string Revision, long Length, string ContentHash, long Timestamp);
public interface IPlasticRevisionReader
{
    Stream OpenRevision(string treeSpec, string revision);
}
// A bounded hint over the same exact revisions and serialized stream lease.
// Readers without the qualified bulk API retain the ordinary stream contract.
public sealed record PlasticRevisionReadRequest(string Revision, long Length, string ContentHash);
public interface IPlasticRevisionBatchReader : IPlasticRevisionReader
{
    bool HasPreparedRevision(string revision);
    void PrepareRevisions(string treeSpec, IReadOnlyList<PlasticRevisionReadRequest> revisions);
}
public interface IPlasticRevisionClient : IPlasticRevisionReader
{
    Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default);
    async Task<PlasticRevisionManifest> ListAsync(string treeSpec, string catalogDirectory, CancellationToken ct = default)
    {
        using var manifest = await ListAsync(treeSpec, ct);
        return manifest.WriteCatalog(catalogDirectory, ct);
    }
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
        var output = new StringBuilder(); char[] scratch = new char[4096]; int n; bool exceeded = false;
        while ((n = await reader.ReadAsync(scratch, ct)) != 0)
        { int retained = Math.Min(n, limit - output.Length); output.Append(scratch, 0, retained); exceeded |= retained != n; }
        if (exceeded) throw new IOException("Revision diagnostic output exceeded its bounded buffer.");
        return output.ToString();
    }
    public async Task<PlasticRevisionManifest> ListAsync(string treeSpec, CancellationToken ct = default)
    {
        var scratch = new Splinter.Runtime.TemporaryMetadataDirectory();
        try { return (await ListAsync(treeSpec, scratch.Path, ct)) with { ScratchOwner = scratch }; }
        catch { try { scratch.Dispose(); } catch (IOException) { } throw; }
    }
    public async Task<PlasticRevisionManifest> ListAsync(string treeSpec, string catalogDirectory, CancellationToken ct = default)
    {
        RequireTreeSpec(treeSpec);
        using var process = Start(["ls", "/", "--tree=" + treeSpec, "-R", "--xml", "--encoding=utf-8"]);
        using var cancellation = ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var error = BoundedText(process.StandardError, 16384, ct);
        try
        {
            using var reader = XmlReader.Create(process.StandardOutput.BaseStream,
                new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 0 });
            var manifest = ParseCatalog(treeSpec, reader, catalogDirectory, ct);
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(await error)) throw Failure("PlasticBasisUnavailable", "The fixed tree could not be observed.");
            return manifest;
        }
        catch (Exception ex) when (ex is XmlException or IOException or FormatException or InvalidOperationException or ArgumentException or OverflowException)
        { ct.ThrowIfCancellationRequested(); throw Failure("PlasticBasisInvalid", "Preserve an invalid or incomplete fixed tree listing."); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
    }

    private static PlasticRevisionManifest ParseCatalog(string treeSpec, XmlReader reader, string directory, CancellationToken ct)
    {
        XElement? root = null;
        IEnumerable<PlasticRevisionEntry> Entries()
        {
            bool results = false, items = false;
            while (reader.Read())
            {
                ct.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (reader.Name == "LsResults") { if (results) throw new FormatException(); results = true; continue; }
                if (reader.Name == "LsItems") { if (!results || items) throw new FormatException(); items = true; continue; }
                if (reader.Name != "LsItem") continue;
                if (!items) throw new FormatException();
                XElement node;
                using (var itemReader = reader.ReadSubtree()) node = XElement.Load(itemReader);
                if (Required(node, "CurrentPath") == "/")
                {
                    if (root is not null || Required(node, "Type") != "dir" || !Numeric(Required(node, "RepId")) ||
                        !Numeric(Required(node, "RevId")) || !Guid.TryParseExact(Required(node, "Guid"), "D", out _)) throw new FormatException();
                    root = node; continue;
                }
                if (root is null) throw new FormatException();
                yield return ParseEntry(node, Required(root, "RepId"));
            }
            if (root is null || !results || !items) throw new FormatException();
        }
        var descriptor = PlasticRevisionManifest.BuildCatalog(directory, Entries(), ct);
        string repository = Required(root!, "Repository");
        if (repository.Length > 2048 || !repository.StartsWith("rep:") || repository.Any(char.IsControl)) throw new FormatException();
        return new(treeSpec, Required(root!, "RepId"), Required(root!, "RevId"), Required(root!, "Guid"), null!, repository)
        { Catalog = descriptor, CatalogDirectory = Path.GetFullPath(directory) };
    }

    private static PlasticRevisionEntry ParseEntry(XElement node, string repositoryId)
    {
        string full = Required(node, "CurrentPath"); if (!full.StartsWith('/')) throw new FormatException();
        string path = full[1..], type = Required(node, "Type"), revision = Required(node, "RevId");
        if (!FileWorkspaceProvider.ValidPath(path) || !Numeric(revision) || Required(node, "RepId") != repositoryId ||
            node.Element("SymlinkTarget")?.Value is not (null or "") || type is not ("dir" or "txt" or "bin" or "file" or "File")) throw new FormatException();
        long length = long.Parse(Required(node, "Size"), CultureInfo.InvariantCulture);
        bool directory = type == "dir"; string hash = Required(node, "Hash");
        if (length < 0 || directory && length != 0 || Convert.FromBase64String(hash).Length != 16) throw new FormatException();
        long timestamp = DateTimeOffset.Parse(Required(node, "Date"), CultureInfo.InvariantCulture).UtcDateTime.ToFileTimeUtc();
        return new(path, directory, revision, length, hash, timestamp);
    }
    public async Task VerifyProjectAsync(string project, PlasticRevisionManifest manifest, CancellationToken ct = default)
    {
        using var process = Start(["ls", project, "--xml", "--encoding=utf-8"], project);
        using var cancel = ct.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var error = BoundedText(process.StandardError, 16384, ct);
        try
        {
            using var reader = XmlReader.Create(process.StandardOutput.BaseStream,
                new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 0 });
            VerifyProjectReader(project, manifest, reader); await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(await error)) throw Failure("PlasticBasisUnavailable", "The exact original registration could not be observed.");
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
    }
    public static void VerifyProjectListing(string project, PlasticRevisionManifest manifest, string xml)
    {
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 0 });
            VerifyProjectReader(project, manifest, reader);
        }
        catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException or ArgumentException)
        { throw Failure("PlasticBasisIdentityMismatch", "The local root must identify the exact original repository/server; private files do not identify the basis."); }
    }
    private static void VerifyProjectReader(string project, PlasticRevisionManifest manifest, XmlReader reader)
    {
        try
        {
            bool results = false, found = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (reader.Name == "LsResults") { if (results) throw new FormatException(); results = true; continue; }
                if (reader.Name != "LsItem") continue;
                XElement root; using (var item = reader.ReadSubtree()) root = XElement.Load(item);
                if (root.Element("Name")?.Value != ".") continue;
                if (!results || found) throw new FormatException(); found = true;
                string path = Required(root, "CurrentPath");
                if (!Path.IsPathFullyQualified(path) || !string.Equals(Path.GetFullPath(path), Path.GetFullPath(project), StringComparison.OrdinalIgnoreCase) ||
                    Required(root, "Status") != "Controlled" || Required(root, "Type") != "dir" ||
                    root.Element("SymlinkTarget")?.Value is not (null or "") || Required(root, "RepId") != manifest.RepositoryId || Required(root, "Repository") != manifest.Repository)
                    throw new FormatException();
            }
            if (!results || !found) throw new FormatException();
        }
        catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException or ArgumentException)
        { throw Failure("PlasticBasisIdentityMismatch", "The local root must identify the exact original repository/server."); }
    }
    public static PlasticRevisionManifest Parse(string treeSpec, string xml)
    {
        RequireTreeSpec(treeSpec);
        var scratch = new Splinter.Runtime.TemporaryMetadataDirectory();
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 0 });
            return ParseCatalog(treeSpec, reader, scratch.Path, CancellationToken.None) with { ScratchOwner = scratch };
        }
        catch (Exception ex) when (ex is XmlException or FormatException or InvalidOperationException or ArgumentException or OverflowException)
        { try { scratch.Dispose(); } catch (IOException) { } throw Failure("PlasticBasisInvalid", "Preserve an invalid, ambiguous or unsupported fixed tree listing."); }
        catch { try { scratch.Dispose(); } catch (IOException) { } throw; }
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
