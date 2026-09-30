using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text.Json;
using Splinter.Protocol;

namespace Splinter.Tests;

public sealed class ProductWriteRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string[] Cases = new[] { "write", "rename", "replace", "truncate", "flush" }
        .SelectMany(op => new[] { "client.before-effect", "client.after-ack" }.Select(b => op + ":" + b))
        .Concat(["write:client.after-write-before-flush", "mmap:client.before-view-flush", "mmap:client.after-ack"]).ToArray();
    private const string Alpha = "Assets/SplinterQualification/Target.fbx", Beta = "Assets/TutorialInfo/Icons/URP.png",
        Gamma = "Assets/SplinterQualification/Recovery-target.fbx";
    private sealed record Request(int Version, string Directory, string MountPath, string Operation, string Boundary,
        string Nonce, string AlphaOracle, string BetaOracle, string WorkspaceId, string SnapshotId,
        int ProviderId, long ProviderStartTimeUtcTicks, string ProviderGeneration, string ProviderToken);

    [Fact]
    public async Task EvidenceRun()
    {
        string? configText = Environment.GetEnvironmentVariable("SPLINTER_PRODUCT_WRITE_RECOVERY_RUN"); if (configText is null) return;
        Assert.True(OperatingSystem.IsWindows()); var c = JsonSerializer.Deserialize<JsonElement>(configText);
        string root = Path.GetFullPath(c.GetProperty("evidencePath").GetString()!), project = Path.GetFullPath(c.GetProperty("project").GetString()!),
            home = Path.GetFullPath(c.GetProperty("home").GetString()!), cli = Path.GetFullPath(c.GetProperty("cliPath").GetString()!),
            basis = c.GetProperty("baseReference").GetString()!;
        string[] cases = c.GetProperty("caseNames").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.NotEmpty(cases); Assert.Equal(cases.Length, cases.Distinct().Count()); Assert.All(cases, name => Assert.Contains(name, Cases));
        Assert.False(Directory.Exists(root) || File.Exists(root)); Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "git"));
        File.WriteAllText(Path.Combine(root, "git", "config"), "[safe]\n\tdirectory = " + project.Replace('\\', '/') + "\n");
        var ct = TestContext.Current.CancellationToken;
        var before = await SourceProof(project, ct); Assert.Equal("", before.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("head").GetString(), await GitText(project, ["rev-parse", basis], ct)); WriteNew(Path.Combine(root, "source-before.json"), before);
        string attributes = await GitText(project, ["check-attr", "text", "eol", "binary", "--", Beta], ct);
        Assert.Contains(Beta + ": text: unset", attributes); Assert.Contains(Beta + ": binary: set", attributes);
        WriteNew(Path.Combine(root, "fixture-attributes.json"), new { beta = Beta, attributes, byteOracleRequiresBinary = true });
        string alphaOracle = Path.Combine(root, "alpha-oracle.bin"), betaOracle = Path.Combine(root, "beta-oracle.bin");
        WriteBytesNew(alphaOracle, File.ReadAllBytes(Path.Combine(project, Alpha))); WriteBytesNew(betaOracle, File.ReadAllBytes(Path.Combine(project, Beta)));
        Assert.True(new FileInfo(alphaOracle).Length >= 12288); Assert.True(new FileInfo(betaOracle).Length >= 4096);
        var binaries = Directory.GetFiles(Path.GetDirectoryName(cli)!).Where(p => Path.GetExtension(p) is ".dll" or ".json")
            .Append(typeof(ProductWriteRecoveryTests).Assembly.Location).Select(p => new { path = p, sha256 = HashFile(p) }).ToArray();
        WriteNew(Path.Combine(root, "candidate.json"), new { version = 1, binaries, os = Environment.OSVersion.ToString(), runtime = Environment.Version.ToString(),
            caseNames = cases, project, home, basis, oraclePayloadBytes = new FileInfo(alphaOracle).Length + new FileInfo(betaOracle).Length,
            scope = "ordinary product CLI/VSS/WinFsp/Git; external client boundaries; no observer/Unity/install/remote" });
        var service = await Cli(cli, root, ["storage", "service", "status"], Path.Combine(root, "service"), ct);
        Assert.False(service.GetProperty("data").GetProperty("callerElevated").GetBoolean()); Assert.Equal("running", service.GetProperty("data").GetProperty("state").GetString());
        var results = new List<JsonElement>(); string[]? priorSnapshots = null;
        foreach (string name in cases)
        {
            string[] parts = name.Split(':'); string trial = Path.Combine(root, name.Replace(':', '-')); Directory.CreateDirectory(trial);
            var result = await Trial(cli, root, project, home, basis, trial, parts[0], parts[1], alphaOracle, betaOracle, ct);
            string[] observedPrior = result.GetProperty("priorSnapshotIds").EnumerateArray().Select(e => e.GetString()!).ToArray();
            priorSnapshots ??= observedPrior; Assert.Equal(priorSnapshots.Order(), observedPrior.Order());
            results.Add(result); WriteNew(Path.Combine(trial, "result.json"), result);
        }
        var after = await SourceProof(project, ct); WriteNew(Path.Combine(root, "source-after.json"), after);
        foreach (string field in new[] { "head", "indexHash", "status", "files", "worktrees" }) Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        foreach (string line in before.GetProperty("refs").GetString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Assert.Contains(line, after.GetProperty("refs").GetString());
        foreach (var binary in binaries) Assert.Equal(binary.sha256, HashFile(binary.path));
        WriteNew(Path.Combine(root, "result.json"), new { version = 1, passed = results.Count, failed = 0, fullMatrix = cases.Length == Cases.Length,
            originalPreserved = true, priorRefsPreserved = true, priorSnapshotsPreserved = true, results,
            qualification = "product-vss-external-client-boundaries-only" });
    }

    [Fact]
    public void ClientActor()
    {
        string? input = Environment.GetEnvironmentVariable("SPLINTER_PRODUCT_WRITE_RECOVERY_CHILD"); if (input is null) return;
        var r = JsonSerializer.Deserialize<Request>(File.ReadAllText(input), Json)!;
        Assert.True(new ContractCatalog().Validate("urn:splinter:v1:product-write-recovery-probe-request", JsonSerializer.SerializeToElement(r, Json)).IsValid);
        string alpha = Path.Combine(r.MountPath, Alpha), beta = Path.Combine(r.MountPath, Beta);
        Assert.Equal(HashFile(r.AlphaOracle), HashFile(alpha)); Assert.Equal(HashFile(r.BetaOracle), HashFile(beta));
        using (var prefix = OpenWrite(alpha)) { prefix.Write(Block(0x3c)); prefix.Flush(true); }
        Assert.Equal(Expected(r, false), KernelModel(r));
        WriteNew(Path.Combine(r.Directory, "prefix.json"), new { version = 1, r.Nonce, modelHash = Expected(r, false), sourceBasisRead = true });
        if (r.Boundary == "client.before-effect") Gate(r, input);
        switch (r.Operation)
        {
            case "write":
                using (var file = OpenWrite(alpha))
                { file.Position = 8192; file.Write(Block(0xa5)); if (r.Boundary == "client.after-write-before-flush") Gate(r, input); file.Flush(true); }
                break;
            case "rename": File.Move(alpha, Path.Combine(r.MountPath, Gamma)); break;
            case "replace": File.Move(alpha, beta, true); break;
            case "truncate": using (var file = OpenWrite(alpha)) { file.SetLength(4096); file.Flush(true); } break;
            case "flush": using (var file = OpenWrite(alpha)) file.Flush(true); break;
            case "mmap":
                using (var file = new FileStream(alpha, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                using (var map = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
                using (var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite))
                { view.WriteArray(8192, Block(0xa5), 0, 4096); if (r.Boundary == "client.before-view-flush") Gate(r, input); view.Flush(); file.Flush(true); }
                break;
            default: throw new InvalidOperationException("Unknown operation.");
        }
        Assert.Equal(Expected(r, true), KernelModel(r));
        WriteNew(Path.Combine(r.Directory, "ack.json"), new { version = 1, r.Nonce, modelHash = Expected(r, true), explicitViewAndFileFlush = r.Operation == "mmap" });
        Gate(r, input);
    }

    private static async Task<JsonElement> Trial(string cli, string root, string project, string home, string basis, string dir,
        string operation, string boundary, string alphaOracle, string betaOracle, CancellationToken ct)
    {
        string key = "nr03-" + Guid.NewGuid().ToString("N");
        string[] ensure = ["workspace", "ensure", "--project", project, "--home", home, "--key", key, "--base", basis, "--storage", "ntfs-snapshot"];
        var initial = await Cli(cli, root, ensure, Path.Combine(dir, "ensure"), ct); var w = initial.GetProperty("data").GetProperty("workspace");
        string id = w.GetProperty("id").GetString()!, mount = w.GetProperty("path").GetString()!, control = Path.Combine(home, "records", id);
        string overlay = Path.Combine(control, "overlay"), journal = Path.Combine(control, "delta", "delta.journal");
        var providerProof = Read(Path.Combine(overlay, "provider-launch-receipt.json"));
        var providerIntent = Read(Path.Combine(overlay, "provider-launch-intent.json")); var overlayRequest = Read(Path.Combine(overlay, "request.json"));
        var capture = Read(Path.Combine(control, "snapshot", "snapshot.json"));
        string snapshotId = capture.GetProperty("snapshot").GetProperty("id").GetString()!;
        string[] priorSnapshots = capture.GetProperty("beforeSnapshotIds").EnumerateArray().Select(e => e.GetString()!).ToArray();
        string registrationHash = HashFile(Path.Combine(control, "git-overlay.json"));
        var r = new Request(1, dir, mount, operation, boundary, Guid.NewGuid().ToString("N"), alphaOracle, betaOracle, id, snapshotId,
            providerProof.GetProperty("processId").GetInt32(), providerProof.GetProperty("startTimeUtcTicks").GetInt64(),
            providerProof.GetProperty("generation").GetString()!, providerProof.GetProperty("token").GetString()!);
        Assert.Equal(r.ProviderGeneration, providerIntent.GetProperty("generation").GetString()); Assert.Equal(r.ProviderToken, overlayRequest.GetProperty("token").GetString());
        Assert.Equal(id, overlayRequest.GetProperty("identity").GetProperty("id").GetString());
        Assert.Equal(snapshotId, overlayRequest.GetProperty("identity").GetProperty("snapshot").GetProperty("id").GetString());
        string input = Path.Combine(dir, "request.json"); WriteNew(input, r);
        foreach (string name in new[] { "provider-launch-intent.json", "provider-launch-receipt.json", "request.json", "state.json" }) CopyNew(Path.Combine(overlay, name), Path.Combine(dir, "before-" + name));
        CopyNew(Path.Combine(control, "snapshot", "snapshot.json"), Path.Combine(dir, "capture.json"));
        using var provider = Process.GetProcessById(r.ProviderId); Assert.Equal(r.ProviderStartTimeUtcTicks, provider.StartTime.ToUniversalTime().Ticks);
        using var actor = StartActor(input); var actorOut = actor.StandardOutput.ReadToEndAsync(ct); var actorErr = actor.StandardError.ReadToEndAsync(ct);
        WriteNew(Path.Combine(dir, "client-launch.json"), new { r.Nonce, processId = actor.Id, processStartTimeUtcTicks = actor.StartTime.ToUniversalTime().Ticks });
        try
        {
            await WaitFile(Path.Combine(dir, "signal.json"), actor, ct);
            var signal = Read(Path.Combine(dir, "signal.json")); Assert.Equal(r.Nonce, signal.GetProperty("nonce").GetString());
            Assert.True(new ContractCatalog().Validate("urn:splinter:v1:product-write-recovery-probe-signal", signal).IsValid);
            Assert.Equal(boundary, signal.GetProperty("boundary").GetString()); Assert.Equal(HashFile(input), signal.GetProperty("requestDigest").GetString());
            Assert.Equal(actor.Id, signal.GetProperty("processId").GetInt32()); Assert.Equal(actor.StartTime.ToUniversalTime().Ticks, signal.GetProperty("processStartTimeUtcTicks").GetInt64());
            Assert.Equal(boundary == "client.after-ack", File.Exists(Path.Combine(dir, "ack.json")));
            Assert.False(provider.HasExited); Assert.Equal(r.ProviderStartTimeUtcTicks, provider.StartTime.ToUniversalTime().Ticks);
            WriteNew(Path.Combine(dir, "termination.json"), new { r.WorkspaceId, r.SnapshotId, r.ProviderId, r.ProviderStartTimeUtcTicks, r.ProviderGeneration,
                clientId = actor.Id, clientStartTimeUtcTicks = actor.StartTime.ToUniversalTime().Ticks, callerElevated = false });
            provider.Kill(); await provider.WaitForExitAsync(ct); actor.Kill(); await actor.WaitForExitAsync(ct);
            await WaitMountAbsent(mount, ct);
            byte[] journalBefore = await ReadAfterExit(journal, ct); WriteBytesNew(Path.Combine(dir, "journal-before-reconnect.bin"), journalBefore);
            var resumed = await Cli(cli, root, ensure, Path.Combine(dir, "resume"), ct); var next = resumed.GetProperty("data").GetProperty("workspace");
            Assert.Equal(id, next.GetProperty("id").GetString()); Assert.Equal(mount, next.GetProperty("path").GetString());
            Assert.Equal(snapshotId, next.GetProperty("snapshot").GetProperty("reference").GetProperty("id").GetString());
            Assert.Equal(registrationHash, HashFile(Path.Combine(control, "git-overlay.json")));
            Assert.Equal(journalBefore, SharedBytes(journal));
            var nextIntent = Read(Path.Combine(overlay, "provider-launch-intent.json")); var nextProof = Read(Path.Combine(overlay, "provider-launch-receipt.json"));
            var nextRequest = Read(Path.Combine(overlay, "request.json"));
            Assert.NotEqual(r.ProviderGeneration, nextIntent.GetProperty("generation").GetString());
            Assert.NotEqual(r.ProviderStartTimeUtcTicks, nextProof.GetProperty("startTimeUtcTicks").GetInt64());
            Assert.Equal(r.ProviderToken, nextProof.GetProperty("token").GetString());
            Assert.Equal(overlayRequest.GetProperty("identity").GetRawText(), nextRequest.GetProperty("identity").GetRawText());
            Assert.NotEqual(overlayRequest.GetProperty("readAccess").GetProperty("verificationId").GetString(), nextRequest.GetProperty("readAccess").GetProperty("verificationId").GetString());
            foreach (string name in new[] { "provider-launch-intent.json", "provider-launch-receipt.json", "request.json", "state.json" }) CopyNew(Path.Combine(overlay, name), Path.Combine(dir, "after-" + name));
            string model = KernelModel(r); bool pending = operation != "flush" && model == Expected(r, true);
            if (boundary is "client.after-ack" or "client.after-write-before-flush") Assert.Equal(Expected(r, true), model);
            else if (boundary == "client.before-view-flush") Assert.True(pending || model == Expected(r, false));
            else Assert.Equal(Expected(r, false), model);
            // A second identical ensure must reuse this new provider and leave the journal unchanged.
            await Cli(cli, root, ensure, Path.Combine(dir, "resume-repeat"), ct);
            Assert.Equal(nextProof.GetRawText(), Read(Path.Combine(overlay, "provider-launch-receipt.json")).GetRawText());
            Assert.Equal(journalBefore, SharedBytes(journal)); Assert.Equal(model, KernelModel(r));
            using (var writer = OpenWrite(Path.Combine(mount, Beta))) { writer.Write(Block(0x7e)); writer.Flush(true); }
            Assert.Equal(Expected(r, pending, true), KernelModel(r));
            WriteBytesNew(Path.Combine(dir, "journal-before-preserve.bin"), SharedBytes(journal));
            string preserve = Path.Combine(dir, "preserve-request.json");
            WriteNew(preserve, new { version = 1, action = "preserve-worker", project, owner = new { id = "nr03-write-recovery", generation = 1 }, requestKey = "preserve-" + r.Nonce, workspaceId = id });
            var preserved = await Cli(cli, root, ["validation", "--input", preserve, "--home", home], Path.Combine(dir, "preserve"), ct);
            var preservation = preserved.GetProperty("data").GetProperty("result");
            Assert.False(preservation.GetProperty("validationClaimed").GetBoolean());
            var savedResult = preservation.GetProperty("result"); string revision = savedResult.GetProperty("revision").GetString()!;
            string reference = savedResult.GetProperty("resultReference").GetString()!;
            Assert.True(savedResult.GetProperty("independentOfSnapshot").GetBoolean()); Assert.Equal("stable-journal-v1", savedResult.GetProperty("collectionProtocol").GetString());
            Assert.Equal(revision, await GitText(project, ["rev-parse", reference], ct));
            foreach (var item in Files(r, pending, true)) Assert.Equal(Hash(item.Value), Hash(await GitBytes(project, ["show", revision + ":" + item.Key], ct)));
            var treePaths = (await GitText(project, ["ls-tree", "-r", "--name-only", revision], ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (string selected in new[] { Alpha, Beta, Gamma }) Assert.Equal(Files(r, pending, true).ContainsKey(selected), treePaths.Contains(selected));
            var status = await Cli(cli, root, ["workspace", "status", "--id", id, "--home", home], Path.Combine(dir, "status-before-release"), ct);
            var storage = status.GetProperty("data").GetProperty("workspace").GetProperty("snapshot").GetProperty("observation").GetProperty("storage");
            Assert.Equal(0, storage.GetProperty("initialPayloadCopiedBytes").GetInt64()); Assert.Equal(0, storage.GetProperty("readCacheBytes").GetInt64());
            var released = await Cli(cli, root, ["workspace", "release", "--id", id, "--home", home], Path.Combine(dir, "release"), ct);
            Assert.Equal("released", released.GetProperty("data").GetProperty("workspace").GetProperty("phase").GetString());
            Assert.True(PathMissing(mount)); Assert.True(PathMissing(Path.GetDirectoryName(journal)!));
            Assert.True(PathMissing(Read(Path.Combine(control, "git-overlay.json")).GetProperty("gitDirectory").GetString()!));
            int resumedPid = nextProof.GetProperty("processId").GetInt32(); long resumedStart = nextProof.GetProperty("startTimeUtcTicks").GetInt64();
            try { using var remaining = Process.GetProcessById(resumedPid); Assert.True(remaining.HasExited || remaining.StartTime.ToUniversalTime().Ticks != resumedStart); }
            catch (ArgumentException) { /* Exact recorded provider is absent. */ }
            var saved = Read(Path.Combine(control, "workspace.json")); var observation = saved.GetProperty("snapshot").GetProperty("observation");
            foreach (string field in new[] { "snapshotAvailable", "mounted", "providerAlive" }) Assert.False(observation.GetProperty(field).GetBoolean());
            foreach (string field in new[] { "openHandles", "workingSetBytes", "privateBytes" }) Assert.Equal(0, observation.GetProperty(field).GetInt64());
            var isolation = Read(Path.Combine(control, "snapshot", "release-isolation.json"));
            Assert.Empty(isolation.GetProperty("missingOtherIds").EnumerateArray()); Assert.True(isolation.GetProperty("deletionAttempted").GetBoolean());
            foreach (string prior in priorSnapshots) Assert.Contains(prior, isolation.GetProperty("beforeIds").EnumerateArray().Select(e => e.GetString()));
            CopyNew(Path.Combine(control, "snapshot", "release-isolation.json"), Path.Combine(dir, "release-isolation.json"));
            CopyNew(Path.Combine(control, "workspace.json"), Path.Combine(dir, "released-workspace.json"));
            Assert.Equal(revision, await GitText(project, ["rev-parse", reference], ct));
            var result = JsonSerializer.SerializeToElement(new { version = 1, operation, boundary, r.Nonce, workspaceId = id, snapshotId,
                actualProviderTermination = true, sameSnapshotRegistrationDelta = true, newProviderGeneration = true, currentVerificationChanged = true,
                acknowledgedDataPreserved = true, repeatedEnsureJournalUnchanged = true, resumedReadWrite = true, prefixPreserved = true,
                beforeJournalHash = Hash(journalBefore), recoveredModelHash = model, pendingWriteSurvived = pending, independentResultReference = reference,
                independentResultRevision = revision, validationClaimed = false, priorSnapshotIds = priorSnapshots, released = true,
                openHandles = 0, ownedLiveChildren = 0, initialPayloadCopiedBytes = 0, readCacheBytes = 0 }, Json);
            Assert.True(new ContractCatalog().Validate("urn:splinter:v1:product-write-recovery-probe-result", result).IsValid); return result;
        }
        finally
        {
            // Only the actor launched here may be ended on failure. Product provider/snapshot/workspace stay available for explicit recovery.
            if (!actor.HasExited) { actor.Kill(); await actor.WaitForExitAsync(CancellationToken.None); }
            WriteNew(Path.Combine(dir, "client-output.json"), new { stdout = await actorOut, stderr = await actorErr });
        }
    }

    private static void Gate(Request r, string input)
    {
        WriteNew(Path.Combine(r.Directory, "signal.json"), new { version = 1, r.Nonce, r.Boundary, requestDigest = HashFile(input),
            processId = Environment.ProcessId, processStartTimeUtcTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks });
        using var gate = new ManualResetEventSlim(); if (!gate.Wait(TimeSpan.FromMinutes(5))) throw new TimeoutException("Preserve this exact workspace and evidence.");
    }
    private static Process StartActor(string input)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { typeof(ProductWriteRecoveryTests).Assembly.Location, "-method", "Splinter.Tests.ProductWriteRecoveryTests.ClientActor" }) start.ArgumentList.Add(arg);
        start.Environment.Remove("SPLINTER_PRODUCT_WRITE_RECOVERY_RUN"); start.Environment["SPLINTER_PRODUCT_WRITE_RECOVERY_CHILD"] = input; return Process.Start(start)!;
    }
    private static async Task<JsonElement> Cli(string cli, string root, string[] args, string stem, CancellationToken ct)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(cli); foreach (string arg in args) start.ArgumentList.Add(arg); start.Environment["XDG_CONFIG_HOME"] = root;
        using var child = Process.Start(start)!; var stdout = child.StandardOutput.ReadToEndAsync(ct); var stderr = child.StandardError.ReadToEndAsync(ct);
        WriteNew(stem + ".command.json", new { args, processId = child.Id, processStartTimeUtcTicks = child.StartTime.ToUniversalTime().Ticks });
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromMinutes(3));
        await child.WaitForExitAsync(deadline.Token); string output = await stdout; WriteBytesNew(stem + ".stdout.json", System.Text.Encoding.UTF8.GetBytes(output));
        WriteBytesNew(stem + ".stderr.txt", System.Text.Encoding.UTF8.GetBytes(await stderr));
        Assert.True(child.ExitCode == 0, output); var response = JsonSerializer.Deserialize<JsonElement>(output);
        Assert.False(response.TryGetProperty("error", out _)); return response;
    }
    private static async Task<JsonElement> SourceProof(string project, CancellationToken ct)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in (await GitText(project, ["ls-files"], ct)).Split('\n', StringSplitOptions.RemoveEmptyEntries)) files[path] = HashFile(Path.Combine(project, path));
        return JsonSerializer.SerializeToElement(new { head = await GitText(project, ["rev-parse", "HEAD"], ct), indexHash = HashFile(Path.Combine(project, ".git", "index")),
            status = await GitText(project, ["status", "--porcelain"], ct), refs = await GitText(project, ["show-ref"], ct), worktrees = await GitText(project, ["worktree", "list", "--porcelain"], ct), files }, Json);
    }
    private static async Task<string> GitText(string project, string[] args, CancellationToken ct) => System.Text.Encoding.UTF8.GetString(await GitBytes(project, args, ct)).TrimEnd('\r', '\n');
    private static async Task<byte[]> GitBytes(string project, string[] args, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { "-c", "safe.directory=" + project.Replace('\\', '/'), "-C", project }.Concat(args)) start.ArgumentList.Add(arg);
        using var child = Process.Start(start)!; using var bytes = new MemoryStream(); var output = child.StandardOutput.BaseStream.CopyToAsync(bytes, ct); var error = child.StandardError.ReadToEndAsync(ct);
        await child.WaitForExitAsync(ct); await output; Assert.True(child.ExitCode == 0, await error); return bytes.ToArray();
    }
    private static SortedDictionary<string, byte[]> Files(Request r, bool pending, bool rewrite = false)
    {
        byte[] alpha = File.ReadAllBytes(r.AlphaOracle); Block(0x3c).CopyTo(alpha, 0);
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal) { [Alpha] = alpha, [Beta] = File.ReadAllBytes(r.BetaOracle) };
        if (pending) switch (r.Operation)
        {
            case "write": case "mmap": Block(0xa5).CopyTo(alpha, 8192); break;
            case "rename": files[Gamma] = alpha; files.Remove(Alpha); break;
            case "replace": files[Beta] = alpha; files.Remove(Alpha); break;
            case "truncate": files[Alpha] = alpha[..4096]; break;
        }
        if (rewrite) Block(0x7e).CopyTo(files[Beta], 0); return files;
    }
    private static string Expected(Request r, bool pending, bool rewrite = false) => Digest(Files(r, pending, rewrite));
    private static string KernelModel(Request r)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (string relative in new[] { Alpha, Beta, Gamma }) { string path = Path.Combine(r.MountPath, relative); if (File.Exists(path)) files[relative] = File.ReadAllBytes(path); }
        return Digest(files);
    }
    private static string Digest(SortedDictionary<string, byte[]> files) => Hash(JsonSerializer.SerializeToUtf8Bytes(files.Select(f => new { path = f.Key, length = f.Value.Length, sha256 = Hash(f.Value) })));
    private static async Task WaitFile(string path, Process actor, CancellationToken ct)
    { var watch = Stopwatch.StartNew(); while (!File.Exists(path) && !actor.HasExited && watch.Elapsed < TimeSpan.FromSeconds(45)) await Task.Delay(25, ct); Assert.True(File.Exists(path), "Signal missing; retain exact workspace: " + path); }
    private static async Task WaitMountAbsent(string path, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); while (!PathMissing(path) && watch.Elapsed < TimeSpan.FromSeconds(15)) await Task.Delay(25, ct);
        Assert.True(PathMissing(path), "Still mounted/unknown; preserve it.");
    }
    private static bool PathMissing(string path)
    { try { File.GetAttributes(path); return false; } catch (FileNotFoundException) { return true; } catch (DirectoryNotFoundException) { return true; } }
    private static async Task<byte[]> ReadAfterExit(string path, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew(); while (true)
        { try { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); using var bytes = new MemoryStream(); file.CopyTo(bytes); return bytes.ToArray(); }
          catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 && watch.Elapsed < TimeSpan.FromSeconds(5)) { await Task.Delay(25, ct); } }
    }
    private static FileStream OpenWrite(string path) => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.WriteThrough);
    private static byte[] Block(byte value) => Enumerable.Repeat(value, 4096).ToArray();
    private static byte[] SharedBytes(string path)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try { Splinter.Platform.Windows.WindowsProcessWorkspaceProvider.CheckPath(path); using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var bytes = new MemoryStream(); file.CopyTo(bytes); return bytes.ToArray(); }
            catch (IOException error) when ((error is FileNotFoundException or DirectoryNotFoundException || (error.HResult & 0xffff) is 32 or 33) && watch.Elapsed < TimeSpan.FromSeconds(1)) { Thread.Sleep(20); }
        }
    }
    private static JsonElement Read(string path) => JsonSerializer.Deserialize<JsonElement>(SharedBytes(path));
    private static string HashFile(string path) { using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); return Convert.ToHexStringLower(SHA256.HashData(file)); }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void WriteNew<T>(string path, T value) => WriteBytesNew(path, JsonSerializer.SerializeToUtf8Bytes(value, Json));
    private static void WriteBytesNew(string path, byte[] bytes)
    { using (var file = new FileStream(path + ".tmp", FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete)) { file.Write(bytes); file.Flush(true); } File.Move(path + ".tmp", path, false); }
    private static void CopyNew(string from, string to) => WriteBytesNew(to, SharedBytes(from));
}
