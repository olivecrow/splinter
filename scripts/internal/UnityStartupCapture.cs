#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Splinter.Development.UnityStartup
{
    // Diagnostic reader only. No product command, Editor callback, package or run-state writes.
    public static class StartupCapture
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        public sealed class Options
        {
            public string Stage { get; set; } = "";
            public string ControlPath { get; set; } = "";
            public string OutputPath { get; set; } = "";
            public string[] InputManifests { get; set; } = Array.Empty<string>();
            public string? BeforeCapture { get; set; }
            public int MaxFiles { get; set; } = 2048;
            public long MaxFileBytes { get; set; } = 32L * 1024 * 1024;
            public long MaxTotalBytes { get; set; } = 128L * 1024 * 1024;
        }
        internal sealed record Lease(int Pid, long StartTicks, string Executable);
        internal sealed record Identity(string Id, string Path, string Project, string Digest, string BaseRevision,
            string Snapshot, string GeneratedBasis, Lease Provider, Lease? Editor);
        private sealed record Anchor(DateTimeOffset Utc, long MonotonicTicks, long Frequency);
        private sealed record Artifact(string Name, string Source, long Bytes, string Sha256, DateTime LastWriteUtc,
            bool AppendOnlyObservation);

        public static string Run(string optionsJson)
        {
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("This capture requires Windows.");
            Options options = JsonSerializer.Deserialize<Options>(optionsJson, Json) ?? throw new ArgumentException("Missing options.");
            return Execute(options, ProbeProcess, Native.IsSplinterVolume);
        }

        internal static string Execute(Options o, Func<Lease, bool> processProbe, Func<string, bool> mountProbe)
        {
            ValidateOptions(o);
            string control = Path.GetFullPath(o.ControlPath), output = Path.GetFullPath(o.OutputPath);
            Guard(control, null); Guard(output, null);
            if (File.Exists(output) || Directory.Exists(output)) throw new IOException("OutputAlreadyExists");
            // Verify before creating output; released/foreign/missing mounts produce no success artifact.
            Identity identity = Verify(control, o.Stage, processProbe, mountProbe);
            if (Related(output, identity.Path) || Related(output, identity.Project) || Related(output, control))
                throw new IOException("EvidenceOverlapsOwnedOrOriginalResources");
            foreach (string manifest in o.InputManifests)
                if (Related(Path.GetFullPath(manifest), output)) throw new IOException("EvidenceOverlapsInput");
            string? parent = Path.GetDirectoryName(output);
            if (parent is null || !Directory.Exists(parent)) throw new IOException("EvidenceParentMissing");
            Anchor begin = Now();
            Directory.CreateDirectory(output);
            var captured = new List<Artifact>();
            var missing = new List<string>();
            long copied = 0;
            try
            {
                Save(output, "provider-before.json", SanitizedProvider(control));
                Add(Path.Combine(identity.Path, "ProjectSettings", "ProjectVersion.txt"), "project/ProjectVersion.txt", false, true);
                Add(Path.Combine(identity.Path, "Packages", "manifest.json"), "project/manifest.json", false, false);
                Add(Path.Combine(identity.Path, "Packages", "packages-lock.json"), "project/packages-lock.json", false, false);
                for (int i = 0; i < o.InputManifests.Length; i++)
                    Add(Path.GetFullPath(o.InputManifests[i]), "inputs/" + i + ".json", false, true);
                foreach (string path in EnumerateBee(identity.Path, o.MaxFiles * 8))
                    Add(path, "workspace/" + Path.GetRelativePath(identity.Path, path).Replace('\\', '/'), false, true);
                Add(Path.Combine(control, "editor.log"), "editor.log", true, o.Stage == "live");
                Add(Path.Combine(control, "git-preparation.json"), "git-preparation.json", false, false);
                Identity after = Verify(control, o.Stage, processProbe, mountProbe);
                if (identity != after) throw new IOException("CaptureOwnershipChanged");
                Save(output, "provider-after.json", SanitizedProvider(control));
                Anchor end = Now();
                string log = Path.Combine(output, "payload", "editor.log");
                object phases = File.Exists(log) ? Analyze(File.ReadAllText(log)) : new { observations = Array.Empty<object>() };
                Save(output, "analysis.json", phases);
                if (o.BeforeCapture != null) Save(output, "interval.json", CompareCapture(o.BeforeCapture, output, identity, begin, captured));
                var result = new
                {
                    kind = "development-unity-startup-capture", version = 1, complete = true, stage = o.Stage,
                    identity, began = begin, ended = end,
                    captureSeconds = (end.MonotonicTicks - begin.MonotonicTicks) / (double)begin.Frequency,
                    limits = new { o.MaxFiles, o.MaxFileBytes, o.MaxTotalBytes }, copiedBytes = copied,
                    artifacts = captured, missingOptional = missing,
                    boundaries = new[] { "Per-file observations, not an atomic Library snapshot or compile proof.",
                        "No Editor launch/stop, refresh, external-package or source mutation.",
                        "Live editor.log is a bounded prefix; source may append after capture.",
                        "UTC/monotonic anchors bound capture; log entries without timestamps have order only.",
                        "Diagnostic capture overhead is not normal startup time; retain before-start and live outputs separately." }
                };
                // Completion marker is last and never overwrites an existing result.
                Save(output, "capture.json", result);
                return JsonSerializer.Serialize(result, Json);
            }
            catch (Exception error)
            {
                Save(output, "failure.json", new { complete = false, error = error.Message, began = begin, ended = Now(), artifacts = captured });
                throw;
            }

            void Add(string source, string name, bool append, bool required)
            {
                Guard(source, Under(source, identity.Path) ? identity.Path : null);
                if (!File.Exists(source))
                {
                    if (required) throw new IOException("CaptureInputMissing: " + source);
                    missing.Add(name); return;
                }
                if (captured.Count >= o.MaxFiles) throw new IOException("CaptureFileLimit");
                string destination = Path.Combine(output, "payload", name.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using SafeFileHandle handle = Native.OpenFile(source);
                using var input = new FileStream(handle, FileAccess.Read);
                long length = input.Length;
                DateTime before = File.GetLastWriteTimeUtc(source);
                if (length > o.MaxFileBytes || length > o.MaxTotalBytes - copied) throw new IOException("CaptureByteLimit: " + name);
                using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[65536]; long remaining = length;
                while (remaining > 0)
                {
                    int read = input.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
                    if (read == 0) throw new IOException("CaptureInputShortened: " + name);
                    target.Write(buffer, 0, read); hash.AppendData(buffer, 0, read); remaining -= read;
                }
                Guard(source, Under(source, identity.Path) ? identity.Path : null);
                if (!append && (input.Length != length || File.GetLastWriteTimeUtc(source) != before))
                    throw new IOException("CaptureInputChanged: " + name);
                target.Flush(true); copied += length;
                captured.Add(new Artifact(name, source, length, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), before, append));
            }
        }

        private static Identity Verify(string control, string stage, Func<Lease, bool> probe, Func<string, bool> volume)
        {
            using JsonDocument record = Read(Path.Combine(control, "workspace.json"));
            JsonElement w = record.RootElement;
            if (w.GetProperty("version").GetInt32() != 3 || Text(w, "applicationKind") != "unity" ||
                !new[] { "git-snapshot", "plastic-snapshot", "project-snapshot" }.Contains(Text(w, "storageMode")) ||
                new[] { "released", "discarded", "reclaim-pending", "discard-reclaim-pending" }.Contains(Text(w, "phase")))
                throw new IOException("CaptureWorkspaceNotActive");
            string id = Text(w, "id"), root = Path.GetFullPath(Text(w, "path")), project = Path.GetFullPath(Text(w, "projectPath"));
            if (Path.GetFileName(control.TrimEnd(Path.DirectorySeparatorChar)) != id || Same(root, project))
                throw new IOException("CaptureWorkspaceIdentityMismatch");
            JsonElement snapshot = w.GetProperty("snapshot"), mount = snapshot.GetProperty("mount");
            if (!snapshot.GetProperty("attached").GetBoolean() || Text(mount, "id") != id || !Same(Text(mount, "path"), root))
                throw new IOException("CaptureMountIdentityMismatch");
            string snapshotId = Text(snapshot.GetProperty("reference"), "id");
            var provider = new Lease(mount.GetProperty("providerProcessId").GetInt32(), mount.GetProperty("providerStartTimeUtcTicks").GetInt64(), "");
            string overlay = Path.Combine(control, "overlay");
            using JsonDocument request = Read(Path.Combine(overlay, "request.json"));
            using JsonDocument state = Read(Path.Combine(overlay, "state.json"));
            using JsonDocument launch = Read(Path.Combine(overlay, "provider-launch-receipt.json"));
            JsonElement q = request.RootElement, s = state.RootElement, p = launch.RootElement;
            if (q.GetProperty("identity").GetProperty("version").GetInt32() != 2 || s.GetProperty("version").GetInt32() != 1 ||
                Text(q.GetProperty("identity"), "id") != id ||
                Text(q.GetProperty("identity").GetProperty("snapshot"), "id") != snapshotId ||
                !Same(Text(q, "mountPath"), root) || !Same(Text(q, "controlPath"), overlay) ||
                Text(s, "id") != id || Text(s, "snapshotId") != snapshotId || Text(s, "phase") != "mounted" ||
                !Same(Text(s, "mountPath"), root) || Text(s, "token") != Text(q, "token") || Text(p, "token") != Text(q, "token") ||
                s.GetProperty("processId").GetInt32() != provider.Pid || p.GetProperty("processId").GetInt32() != provider.Pid ||
                s.GetProperty("processStartTimeUtcTicks").GetInt64() != provider.StartTicks || p.GetProperty("startTimeUtcTicks").GetInt64() != provider.StartTicks ||
                !probe(provider)) throw new IOException("CaptureProviderOwnershipMismatch");
            Guard(root, root);
            if (!Directory.Exists(root) || !volume(root)) throw new IOException("CaptureMountUnavailable");
            Lease? editor = null;
            string receipt = Path.Combine(control, "launch-receipt.json"), intent = Path.Combine(control, "launch-intent.json");
            if (stage == "before-start")
            {
                if (File.Exists(receipt) || File.Exists(intent) || File.Exists(Path.Combine(root, "Temp", "UnityLockfile")))
                    throw new IOException("CaptureNotBeforeFirstLaunch");
            }
            else
            {
                using JsonDocument r = Read(receipt), i = Read(intent), h = Read(Path.Combine(control, "heartbeat.json"));
                JsonElement re = r.RootElement, ie = i.RootElement, he = h.RootElement;
                editor = new Lease(re.GetProperty("processId").GetInt32(), re.GetProperty("startTimeUtcTicks").GetInt64(), Text(re, "executablePath"));
                string token = Text(re, "token");
                double age = (DateTimeOffset.UtcNow - he.GetProperty("observedAt").GetDateTimeOffset()).TotalSeconds;
                if (Text(ie, "id") != id || !Same(Text(ie, "projectPath"), root) || Text(ie, "token") != token ||
                    !Same(Text(ie, "executable"), editor.Executable) || !Same(Text(re.GetProperty("group"), "controlPath"), control) ||
                    Text(he, "token") != token || !Same(Text(he, "projectPath"), root) ||
                    he.GetProperty("pid").GetInt32() != editor.Pid || he.GetProperty("startTimeUtcTicks").GetInt64() != editor.StartTicks ||
                    age < -2 || age > 15 || !probe(editor)) throw new IOException("CaptureEditorOwnershipMismatch");
            }
            string generated = w.TryGetProperty("generatedDirectoryId", out JsonElement g) ? g.GetString() ?? "" : "";
            return new Identity(id, root, project, Text(w, "requestDigest"), Text(w, "baseRevision"), snapshotId, generated, provider, editor);
        }

        private static object SanitizedProvider(string control)
        {
            using JsonDocument document = Read(Path.Combine(control, "overlay", "state.json"));
            JsonElement state = document.RootElement;
            return new { observedAt = state.GetProperty("observedAt").Clone(), phase = Text(state, "phase"),
                storage = Optional(state, "storage"), gitReads = Optional(state, "gitReads"),
                writeAdmission = Optional(state, "writeAdmission"), anchor = Now() };
        }
        private static JsonElement? Optional(JsonElement root, string name) => root.TryGetProperty(name, out JsonElement value) ? value.Clone() : null;

        private static object CompareCapture(string beforePath, string currentPath, Identity current, Anchor begin, List<Artifact> artifacts)
        {
            Guard(beforePath, null);
            using JsonDocument before = Read(Path.Combine(beforePath, "capture.json"));
            JsonElement b = before.RootElement, i = b.GetProperty("identity"), provider = i.GetProperty("provider");
            if (!b.GetProperty("complete").GetBoolean() || Text(b, "kind") != "development-unity-startup-capture" ||
                Text(b, "stage") != "before-start" || Text(i, "id") != current.Id || !Same(Text(i, "path"), current.Path) ||
                Text(i, "digest") != current.Digest || Text(i, "snapshot") != current.Snapshot ||
                provider.GetProperty("pid").GetInt32() != current.Provider.Pid || provider.GetProperty("startTicks").GetInt64() != current.Provider.StartTicks)
                throw new IOException("CaptureIntervalIdentityMismatch");
            JsonElement end = b.GetProperty("ended");
            long frequency = end.GetProperty("frequency").GetInt64(), ticks = end.GetProperty("monotonicTicks").GetInt64();
            if (frequency != begin.Frequency || ticks > begin.MonotonicTicks) throw new IOException("CaptureClockMismatch");
            using JsonDocument priorState = Read(Path.Combine(beforePath, "provider-after.json"));
            using JsonDocument currentState = Read(Path.Combine(currentPath, "provider-before.json"));
            JsonElement previous = priorState.RootElement.GetProperty("storage").GetProperty("metrics");
            JsonElement now = currentState.RootElement.GetProperty("storage").GetProperty("metrics");
            var deltas = new Dictionary<string, long>(StringComparer.Ordinal);
            bool sameEpoch = previous.TryGetProperty("epoch", out JsonElement pe) && now.TryGetProperty("epoch", out JsonElement ne) && pe.GetString() == ne.GetString();
            if (sameEpoch)
                foreach (string name in new[] { "flushCount", "flushTicks", "commitCount", "journalReadBytes", "baseReadBytes" })
                    if (previous.TryGetProperty(name, out JsonElement p) && now.TryGetProperty(name, out JsonElement n))
                    {
                        long delta = n.GetInt64() - p.GetInt64();
                        if (delta < 0) throw new IOException("CaptureCounterRegressed");
                        deltas.Add(name, delta);
                    }
            var earlier = b.GetProperty("artifacts").EnumerateArray().ToDictionary(a => Text(a, "name"), a => Text(a, "sha256"), StringComparer.Ordinal);
            var later = artifacts.ToDictionary(a => a.Name, a => a.Sha256, StringComparer.Ordinal);
            return new { priorCapture = Path.GetFullPath(beforePath), intervalStart = end.Clone(), intervalEnd = begin,
                intervalSeconds = (begin.MonotonicTicks - ticks) / (double)frequency,
                utcSeconds = (begin.Utc - end.GetProperty("utc").GetDateTimeOffset()).TotalSeconds,
                sameProviderEpoch = sameEpoch, providerCounterDeltas = deltas,
                artifactChanges = new { added = later.Keys.Except(earlier.Keys).Order(StringComparer.Ordinal).ToArray(),
                    removed = earlier.Keys.Except(later.Keys).Order(StringComparer.Ordinal).ToArray(),
                    changed = later.Keys.Where(k => earlier.TryGetValue(k, out string? hash) && hash != later[k]).Order(StringComparer.Ordinal).ToArray() },
                providerStopwatchFrequency = Optional(now, "stopwatchFrequency"),
                boundary = "Caller interval from prelaunch capture end to live capture begin; includes caller dispatch/waits, not certified Editor startup time. Provider samples bracket their own read instants; counters and log scopes are not additive." };
        }
        private static IEnumerable<string> EnumerateBee(string root, int maximumEntries)
        {
            string bee = Path.Combine(root, "Library", "Bee"); Guard(bee, root);
            if (!Directory.Exists(bee)) yield break;
            var pending = new Stack<string>(); pending.Push(bee); int visited = 0;
            while (pending.Count != 0)
            {
                string directory = pending.Pop(); Guard(directory, root);
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++visited > maximumEntries) throw new IOException("CaptureEnumerationLimit");
                    Guard(path, root);
                    if (Directory.Exists(path)) { pending.Push(path); continue; }
                    string name = Path.GetFileName(path);
                    if (name.EndsWith(".dag", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dag.json", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".dag.payloads", StringComparison.OrdinalIgnoreCase) || name.EndsWith("-inputdata.json", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".traceevents", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".rsp", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".rsp2", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".UnityAdditionalFile.txt", StringComparison.OrdinalIgnoreCase) ||
                        name == "bee_backend.info") yield return path;
                }
            }
        }
        internal static object Analyze(string text)
        {
            var actions = new Dictionary<string, int>(StringComparer.Ordinal);
            var observations = new List<object>(); int line = 0; bool firstBuild = true, truncated = false;
            using var reader = new StringReader(text);
            for (string? entry = reader.ReadLine(); entry != null; entry = reader.ReadLine())
            {
                line++;
                Match action = Regex.Match(entry, @"^\[\s*\d+/\d+\s+[^]]+\]\s+(\S+)");
                if (firstBuild && action.Success)
                { string name = action.Groups[1].Value; actions[name] = actions.TryGetValue(name, out int count) ? count + 1 : 1; }
                if (entry.Contains("Tundra build success", StringComparison.Ordinal)) firstBuild = false;
                if (entry.Contains("Duration:", StringComparison.Ordinal) || entry.Contains("Tundra build", StringComparison.Ordinal) ||
                    entry.Contains("Asset Pipeline Refresh", StringComparison.Ordinal) || entry.Contains("CompileScripts:", StringComparison.Ordinal) ||
                    entry.Contains("Domain Reload Profiling:", StringComparison.Ordinal) || entry.Contains("LoadAssemblies (", StringComparison.Ordinal) ||
                    entry.Contains("ProcessInitializeOnLoadAttributes (", StringComparison.Ordinal))
                {
                    if (observations.Count == 10000) { truncated = true; continue; }
                    if (entry.Length > 4096) truncated = true;
                    observations.Add(new { line, text = entry.Substring(0, Math.Min(entry.Length, 4096)).Trim(), timeBasis = "ordered-log-line; no absolute timestamp inferred" });
                }
            }
            return new { firstBuildActions = actions, observations, analysisTruncated = truncated, additive = false,
                note = "Nested compiler/reload/flush scopes overlap. These observations do not identify individual initialization callbacks or causal savings." };
        }
        private static bool ProbeProcess(Lease lease)
        {
            try
            {
                using Process process = Process.GetProcessById(lease.Pid);
                return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == lease.StartTicks &&
                    (lease.Executable.Length == 0 || Same(process.MainModule?.FileName ?? "", lease.Executable));
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
            catch (Win32Exception) { return false; }
        }
        private static void ValidateOptions(Options o)
        {
            if (o.Stage != "before-start" && o.Stage != "live") throw new ArgumentException("Unknown capture stage.");
            if (o.BeforeCapture != null && o.Stage != "live") throw new ArgumentException("BeforeCapture requires live capture.");
            if (o.MaxFiles < 1 || o.MaxFiles > 10000 || o.MaxFileBytes < 1 || o.MaxFileBytes > 256L * 1024 * 1024 ||
                o.MaxTotalBytes < 1 || o.MaxTotalBytes > 1024L * 1024 * 1024 || o.InputManifests.Length > 16)
                throw new ArgumentException("Capture limits are outside the bounded range.");
        }
        private static Anchor Now() => new Anchor(DateTimeOffset.UtcNow, Stopwatch.GetTimestamp(), Stopwatch.Frequency);
        private static string Text(JsonElement element, string name) => element.GetProperty(name).GetString() ?? throw new IOException("Missing identity: " + name);
        private static bool Same(string a, string b) => string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)), StringComparison.OrdinalIgnoreCase);
        private static bool Under(string path, string root) => Same(path, root) || Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        private static bool Related(string a, string b) => Under(a, b) || Under(b, a);
        private static void Guard(string path, string? mount)
        {
            for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            {
                if (!File.Exists(current) && !Directory.Exists(current)) continue;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0 && (mount == null || !Same(current, mount)))
                    throw new IOException("CaptureReparsePath: " + current);
            }
        }
        private static JsonDocument Read(string path)
        {
            Guard(path, null);
            using SafeFileHandle handle = Native.OpenFile(path);
            using var input = new FileStream(handle, FileAccess.Read);
            if (input.Length > 4 * 1024 * 1024) throw new IOException("ControlFileTooLarge");
            return JsonDocument.Parse(input);
        }
        private static void Save(string output, string name, object value)
        {
            Guard(output, null);
            using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, value, Json); stream.Flush(true);
        }
        private static class Native
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int kind, out AttributeTag information, uint size);
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern bool GetVolumeInformationByHandleW(SafeFileHandle handle, StringBuilder? label, uint size, out uint serial,
                out uint maximumComponent, out uint flags, StringBuilder fileSystem, uint fileSystemSize);
            [StructLayout(LayoutKind.Sequential)] private struct AttributeTag { public uint Attributes; public uint Tag; }
            internal static SafeFileHandle OpenFile(string path)
            {
                var handle = CreateFileW(path, 0x80000000, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero); // READ, all shares, OPEN_REPARSE_POINT
                if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
                if (!GetFileInformationByHandleEx(handle, 9, out AttributeTag tag, 8) || (tag.Attributes & 0x410) != 0)
                { handle.Dispose(); throw new IOException("CaptureNotPlainFile: " + path); }
                return handle;
            }
            internal static bool IsSplinterVolume(string path)
            {
                using var handle = CreateFileW(path, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
                if (handle.IsInvalid) return false;
                var name = new StringBuilder(64);
                return GetVolumeInformationByHandleW(handle, null, 0, out _, out _, out _, name, 64) && name.ToString() == "SplinterSO";
            }
        }
    }
}
