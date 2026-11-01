using System.ComponentModel;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Splinter.Core;
using Splinter.Runtime;

namespace Splinter.Platform.Windows;

public sealed record OriginalPublicationMetrics(long AdmittedTargets, int PeakAdmissionWorkers, int PeakPublicationWorkers,
    int RemainingWorkers, int PeakNativeHandles, long MaximumSinglePayloadBytes, long PeakReservedPayloadBytes);

// The project lock serializes Splinter clients. These handles also exclude
// external writes/renames to existing affected files and parent identities.
// Publication never replaces an occupant of a name, including after recovery.
public sealed class WindowsOriginalFilePublisher : IOriginalFilePublisher
{
    private readonly Action<string, int>? boundary;
    private readonly bool allowPlasticAdministration;
    private readonly bool allowGitAdministration;
    private readonly Func<string, IDisposable?>? managedRoot;
    public OriginalPublicationMetrics? LastPublicationMetrics { get; private set; }
    public WindowsOriginalFilePublisher(bool allowPlasticAdministration = false, Func<string, IDisposable?>? managedRoot = null, bool allowGitAdministration = false)
    { this.allowPlasticAdministration = allowPlasticAdministration; this.managedRoot = managedRoot; this.allowGitAdministration = allowGitAdministration; }
    internal WindowsOriginalFilePublisher(Action<string, int> boundary, bool allowPlasticAdministration = false)
    { this.boundary = boundary; this.allowPlasticAdministration = allowPlasticAdministration; }
    public string RootIdentity(string project)
    {
        using var lease = managedRoot?.Invoke(project);
        using var guard = new Guard(project, _ => false, allowPlasticAdministration, lease is not null, allowGitAdministration);
        return Id(guard.Root);
    }
    public OriginalFileObservation? Observe(string project, string relativePath, CancellationToken ct = default)
    {
        using var lease = managedRoot?.Invoke(project);
        using var guard = new Guard(project, _ => false, allowPlasticAdministration, lease is not null, allowGitAdministration);
        return guard.Observe(relativePath, ct);
    }

    public void Publish(OriginalApplyState state, Func<OriginalFileVersion, Stream> openPayload, Action persist, CancellationToken ct = default)
    {
        var metrics = new PublicationMetrics();
        try { PublishCore(state, openPayload, persist, metrics, ct); }
        finally { LastPublicationMetrics = metrics.Snapshot(); }
    }
    private void PublishCore(OriginalApplyState state, Func<OriginalFileVersion, Stream> openPayload, Action persist,
        PublicationMetrics metrics, CancellationToken ct)
    {
        var plan = state.Plan;
        using var pages = OriginalApplyPages.BeginPublication(state);
        if (allowPlasticAdministration && plan.SourceIdentity is null) throw Conflict("An explicit Plastic source guard is required.");
        if (allowGitAdministration && Path.Exists(Path.Combine(plan.ProjectPath, ".git")) && plan.SourceIdentity is null)
            throw Conflict("An explicit Git source guard is required.");
        using var lease = managedRoot?.Invoke(plan.ProjectPath);
        using var guard = new Guard(plan.ProjectPath, path => plan.FindChange(path) is { Before.Kind: "directory", After: null }, allowPlasticAdministration, lease is not null, allowGitAdministration);
        metrics.Guard = guard;
        if (Id(guard.Root) != plan.ProjectIdentity) throw Conflict("The physical original root changed.");
        string intent = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state.IntentKey)))[..24];
        string Prefix(long i, string? nonce) => $".splinter-apply-{plan.OwnerId}-{intent}-{i}" + (nonce is null ? "" : "-" + nonce);

        object progressGate = new();
        void Commit(long index, OriginalApplyStep step)
        {
            lock (progressGate)
            {
                state.SaveStep(index, new() { Phase = step.Phase, StagedIdentity = step.StagedIdentity, StagedBytes = step.StagedBytes, StageNonce = step.StageNonce });
                persist();
            }
        }

        // Open/check the entire write set before creating a stage or mutating
        // the original. The leases stay held until this call returns.
        IEnumerable<(long Index, OriginalApplyChange Change)> AdmissionTargets()
        {
            long index = 0;
            foreach (var change in plan.EnumerateChanges()) yield return (index++, change);
        }
        try
        {
            Parallel.ForEach(Partitioner.Create(AdmissionTargets(), EnumerablePartitionerOptions.NoBuffering),
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct }, item =>
            {
                using var activeAdmission = metrics.Enter(admission: true);
                long i = item.Index; var change = item.Change;
            var step = state.StepAt(i);
            var current = guard.Observe(change.Path, ct, mutation: true);
            string stageName = Sibling(change.Path, Prefix(i, step.StageNonce) + ".new");
            if (step.Phase == "staging" && step.StagedIdentity is null && step.StageNonce is not null && guard.NameExists(stageName))
            {
                if (!Same(current, change.Expected) || guard.NameExists(Sibling(change.Path, Prefix(i, step.StageNonce) + ".old")))
                    throw Conflict("Preserve the fixed original identity and an unexpected retired name before retrying: " + change.Path);
                // The prior attempt was durably admitted but did not durably
                // acquire a native identity. Keep it untouched and report it;
                // a new nonce remains inside this same plan and intent.
                lock (progressGate)
                {
                    state.PreservedScratchRoot = (state.PageProgress ?? throw Conflict("The preserved scratch ledger is unavailable."))
                        .PreserveScratch(stageName, null, "native-stage-identity-not-recorded");
                    step.StageNonce = Guid.NewGuid().ToString("N"); Commit(i, step);
                }
                stageName = Sibling(change.Path, Prefix(i, step.StageNonce) + ".new");
            }
            var staged = guard.Observe(stageName, ct, mutation: true);
            var retired = guard.Observe(Sibling(change.Path, Prefix(i, step.StageNonce) + ".old"), ct, mutation: true);
            if (staged is not null && (step.StagedIdentity is null || staged.Identity != step.StagedIdentity))
                throw Conflict("Preserve an unowned or replaced publication stage: " + change.Path);
            if (retired is not null && retired.Identity != change.Expected?.Identity)
                throw Conflict("Preserve an unowned or replaced retired file: " + change.Path);
            if (step.Phase == "done" || step.Phase == "published")
            {
                if (!Matches(current, change.After) || !change.AlreadyMatches && change.After is not null && current?.Identity != step.StagedIdentity)
                    throw Conflict("A previously published original changed: " + change.Path);
            }
            else if (current?.Identity == step.StagedIdentity && step.StagedIdentity is not null)
            {
                if (!Matches(current, change.After)) throw Conflict("The published stage changed: " + change.Path);
            }
            else if (retired is not null || step.Phase == "retired")
            {
                if (current is not null && !Same(current, change.Expected)) throw Conflict("A new original occupies the retired name: " + change.Path);
                if (retired is not null && !Matches(retired, change.Before)) throw Conflict("The retired original changed: " + change.Path);
            }
            else if (!Same(current, change.Expected)) throw Conflict("The original changed after planning: " + change.Path);
            if (step.Phase == "pending" && (staged is not null || retired is not null))
                throw Conflict("A publication name already exists: " + change.Path);
                Interlocked.Increment(ref metrics.AdmittedTargets);
                boundary?.Invoke("target-locked", (int)Math.Min(i, int.MaxValue));
            });
        }
        catch (AggregateException error)
        { ExceptionDispatchInfo.Capture(error.Flatten().InnerExceptions[0]).Throw(); throw; }
        boundary?.Invoke("preflight", -1);
        void Reached(string name, long index) => boundary?.Invoke(name, (int)Math.Min(index, int.MaxValue));
        long reservationLimit = plan.EnumerateChanges().Where(c => c.After?.Kind == "file").Select(c => c.After!.Length).DefaultIfEmpty().Max();
        using var reservations = new StagingReservations(reservationLimit);
        metrics.Reservations = reservations; metrics.MaximumPayload = reservationLimit;
        long minimumControlBytes = OperationDiskSpace.ControlBytes(state);
        using var workers = CancellationTokenSource.CreateLinkedTokenSource(ct); var workToken = workers.Token;
        void PublishOne(long i)
        {
            using var activeWorker = metrics.Enter(admission: false);
            workToken.ThrowIfCancellationRequested();
            var c = plan.ChangeAt(i); var prior = state.StepAt(i);
            var s = new OriginalApplyStep { Phase = prior.Phase, StagedIdentity = prior.StagedIdentity, StagedBytes = prior.StagedBytes, StageNonce = prior.StageNonce };
            string stagePath = Sibling(c.Path, Prefix(i, s.StageNonce) + ".new"), backupPath = Sibling(c.Path, Prefix(i, s.StageNonce) + ".old");
            if (s.Phase == "done") return;
            if (c.AlreadyMatches) { s.Phase = "done"; Commit(i, s); return; }
            var original = guard.Observe(c.Path, workToken, mutation: true);
            if (c.After is null)
            {
                if (original is not null)
                {
                    if (!Same(original, c.Expected)) throw Conflict("The deletion target changed: " + c.Path);
                    s.Phase = "retired"; Commit(i, s);
                    guard.Delete(c.Path, c.Expected!.Identity);
                    Reached("deleted", i);
                }
                s.Phase = "done"; Commit(i, s); return;
            }
            // A rename may have completed before its receipt was saved.
            if (original?.Identity == s.StagedIdentity && Matches(original, c.After))
            { s.Phase = "published"; Commit(i, s); }
            if (s.Phase != "published")
            {
                var stage = guard.Observe(stagePath, workToken, mutation: true);
                if (stage is null)
                {
                    if (s.StagedIdentity is not null) throw Conflict("The owned stage is missing: " + c.Path);
                    s.StageNonce ??= Guid.NewGuid().ToString("N");
                    stagePath = Sibling(c.Path, Prefix(i, s.StageNonce) + ".new"); backupPath = Sibling(c.Path, Prefix(i, s.StageNonce) + ".old");
                    s.Phase = "staging"; Commit(i, s);
                    Reached("stage-attempt-owned", i);
                    var handle = guard.Create(stagePath, c.After.Kind == "directory");
                    Reached("stage-created-before-receipt", i);
                    s.StagedIdentity = Id(handle); Commit(i, s);
                    Reached("stage-created", i);
                }
                if (s.Phase == "staging")
                {
                    guard.Fill(stagePath, s.StagedIdentity!, c.After, openPayload, minimumControlBytes, workToken);
                    if (original is not null) guard.CopySecurity(c.Path, stagePath);
                    guard.SetMetadata(stagePath, c.After, original is null ? null : c.Path);
                    s.StagedBytes = c.After.Length; s.Phase = "staged"; Commit(i, s);
                    Reached("staged", i);
                }
                stage = guard.Observe(stagePath, workToken, mutation: true);
                if (stage?.Identity != s.StagedIdentity || !Matches(stage, c.After)) throw Conflict("The owned payload stage changed: " + c.Path);
                if (original is not null)
                {
                    original = guard.Observe(c.Path, workToken, mutation: true);
                    if (!Same(original, c.Expected)) throw Conflict("The original changed before retirement: " + c.Path);
                    Reached("original-checked", i);
                    guard.Rename(c.Path, backupPath, c.Expected!.Identity);
                    Reached("retired-before-receipt", i);
                    s.Phase = "retired"; Commit(i, s);
                    Reached("retired", i);
                }
                guard.Rename(stagePath, c.Path, s.StagedIdentity!);
                Reached("published-before-receipt", i);
                s.Phase = "published"; Commit(i, s);
                Reached("published", i);
            }
            var backup = guard.Observe(backupPath, workToken, mutation: true);
            if (backup is not null)
            {
                if (!Same(backup, c.Expected)) throw Conflict("The retired original changed before cleanup: " + c.Path);
                guard.Delete(backupPath, backup.Identity);
                Reached("retired-removed", i);
            }
            s.Phase = "done"; Commit(i, s);
        }
        // Directory topology remains ordered. Independent leaves run only
        // after all target/ancestor handles passed admission and stay pinned.
        // A bounded batch avoids retaining one task per target. Companion
        // metadata waits for the source task in the same batch; earlier batches
        // have already drained before the next one starts.
        var batch = new List<(string Path, Task Task)>(8);
        void Drain()
        {
            try { Task.WhenAll(batch.Select(item => item.Task)).GetAwaiter().GetResult(); }
            catch (Exception ex) { ExceptionDispatchInfo.Capture(ex).Throw(); }
            finally { batch.Clear(); }
        }
        try
        {
            // Retained stages occupy disk even while no worker owns them.
            // Finish those first, before admitting any fresh temporary payload.
            IEnumerable<KeyValuePair<long, OriginalApplyStep>> RecoverySteps()
            {
                if (state.PageProgress is not null)
                { foreach (var pair in state.PageProgress.Enumerate()) yield return pair; }
                else for (int i = 0; i < state.Steps.Length; i++) yield return new(i, state.Steps[i]);
            }
            foreach (var pair in RecoverySteps())
            {
                long current = pair.Key; var step = pair.Value;
                if (step.Phase == "done" || step.StagedIdentity is null) continue;
                var change = plan.ChangeAt(current);
                using var reservation = reservations.Acquire(change.After?.Length ?? 0, workToken);
                PublishOne(current);
            }
            long index = 0;
            foreach (var change in plan.EnumerateChanges())
            {
                long current = index++;
                if (state.StepAt(current).Phase == "done") continue;
                if (change.After?.Kind == "directory" || change.Before?.Kind == "directory")
                { Drain(); PublishOne(current); continue; }
                Task? dependency = change.Path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                    ? batch.LastOrDefault(item => item.Path.Equals(change.Path[..^5], StringComparison.OrdinalIgnoreCase)).Task : null;
                batch.Add((change.Path, Task.Run(async () =>
                {
                    try
                    {
                        if (dependency is not null) await dependency.ConfigureAwait(false);
                        using var reservation = reservations.Acquire(change.After?.Length ?? 0, workToken);
                        try { PublishOne(current); }
                        catch { workers.Cancel(); throw; }
                    }
                    catch { workers.Cancel(); throw; }
                }, CancellationToken.None)));
                if (batch.Count == 8) Drain();
            }
            Drain();
        }
        catch
        {
            workers.Cancel();
            // Existing tasks can own staged bytes and native handles. Always
            // join them before releasing guards or reporting the failure.
            try { Drain(); } catch { }
            throw;
        }
        state.Completed = true; persist();
        boundary?.Invoke("completed", -1);
    }

    private sealed class StagingReservations(long limit) : IDisposable
    {
        private readonly object gate = new();
        private long reserved;
        public long Peak { get; private set; }
        public IDisposable Acquire(long bytes, CancellationToken ct)
        {
            lock (gate)
            {
                while (bytes > limit - reserved) { ct.ThrowIfCancellationRequested(); Monitor.Wait(gate, 25); }
                ct.ThrowIfCancellationRequested(); reserved += bytes; Peak = Math.Max(Peak, reserved); return new Reservation(this, bytes);
            }
        }
        private void Release(long bytes) { lock (gate) { reserved -= bytes; Monitor.PulseAll(gate); } }
        public void Dispose() { }
        private sealed class Reservation(StagingReservations owner, long bytes) : IDisposable
        {
            private bool released;
            public void Dispose() { if (!released) { released = true; owner.Release(bytes); } }
        }
    }

    private sealed class PublicationMetrics
    {
        public Guard? Guard;
        public StagingReservations? Reservations;
        public long AdmittedTargets, MaximumPayload;
        private int admission, publishing, peakAdmission, peakPublishing;
        public IDisposable Enter(bool admission)
        {
            int active = admission ? Interlocked.Increment(ref this.admission) : Interlocked.Increment(ref publishing);
            ref int peak = ref (admission ? ref peakAdmission : ref peakPublishing);
            int previous;
            do { previous = Volatile.Read(ref peak); if (previous >= active) break; } while (Interlocked.CompareExchange(ref peak, active, previous) != previous);
            return new Active(this, admission);
        }
        public OriginalPublicationMetrics Snapshot() => new(Interlocked.Read(ref AdmittedTargets), peakAdmission, peakPublishing,
            admission + publishing, Guard?.PeakHandleCount ?? 0, MaximumPayload, Reservations?.Peak ?? 0);
        private sealed class Active(PublicationMetrics owner, bool admission) : IDisposable
        {
            public void Dispose() { if (admission) Interlocked.Decrement(ref owner.admission); else Interlocked.Decrement(ref owner.publishing); }
        }
    }

    private static string Sibling(string relative, string leaf) => relative.Contains('/') ? relative[..(relative.LastIndexOf('/') + 1)] + leaf : leaf;
    private static bool Same(OriginalFileObservation? a, OriginalFileObservation? b) => a is null ? b is null :
        b is not null && a.Identity == b.Identity && Matches(a, b.Version);
    private static bool Matches(OriginalFileObservation? value, OriginalFileVersion? expected) => expected is null ? value is null :
        expected.SameContent(value?.Version) && expected.RelevantAttributes == value!.Version.RelevantAttributes;
    private static WorkspaceException Conflict(string message) => new("OriginalApplyConflict", message);
    private static void Check(bool ok, string operation)
    {
        if (!ok) { int error = Marshal.GetLastWin32Error(); throw new WorkspaceException("OriginalApplyNativeFailure", operation + ": " + new Win32Exception(error).Message, new { nativeError = error }); }
    }
    private static Info Information(SafeFileHandle handle)
    { Check(GetFileInformationByHandle(handle, out var info), "Inspect original handle"); return info; }
    private static string Id(SafeFileHandle h) { var i = Information(h); return $"{i.Volume:X8}:{i.IndexHigh:X8}{i.IndexLow:X8}"; }
    private static long Length(Info i) => ((long)i.SizeHigh << 32) | i.SizeLow;
    private static long Time(NativeTime t) => ((long)t.High << 32) | t.Low;
    private const uint DeleteAccess = 0x10000, ReadControl = 0x20000, ReadAttributes = 0x80, Synchronize = 0x100000;
    private const uint UnsupportedAttributes = 0x400 | 0x800 | 0x4000 | 0x200;

    private sealed class Guard : IDisposable
    {
        private readonly ConcurrentDictionary<string, SafeFileHandle> handles = new(StringComparer.OrdinalIgnoreCase);
        private readonly object handleGate = new();
        private int peakHandles, activeHandles;
        public int PeakHandleCount => Volatile.Read(ref peakHandles);
        private void ObserveHandleCount()
        {
            int count = Interlocked.Increment(ref activeHandles), prior;
            do { prior = Volatile.Read(ref peakHandles); if (prior >= count) break; } while (Interlocked.CompareExchange(ref peakHandles, count, prior) != prior);
        }
        private readonly Func<string, bool> deleteDirectory;
        private readonly string project;
        private readonly bool managed;
        public SafeFileHandle Root { get; }
        public Guard(string project, Func<string, bool> deleteDirectory, bool allowPlasticAdministration, bool managed = false, bool allowGitAdministration = false)
        {
            if (!OperatingSystem.IsWindows()) throw new WorkspaceException("OriginalApplyUnsupported", "Conditional publication requires Windows NTFS.");
            this.project = Path.GetFullPath(project);
            this.managed = managed;
            this.deleteDirectory = deleteDirectory;
            try
            {
                // Pin each existing ancestor, including the volume root.
                var paths = new Stack<string>();
                for (string? p = this.project; p is not null; p = Path.GetDirectoryName(p)) paths.Push(p);
                while (paths.TryPop(out var p)) PinDirectory(p);
                Root = handles[this.project];
                var fs = new StringBuilder(32);
                Check(GetVolumeInformationByHandleW(Root, null, 0, out _, out _, out _, fs, fs.Capacity), "Inspect original filesystem");
                if (fs.ToString() != (managed ? "SplinterSO" : "NTFS")) throw new WorkspaceException("OriginalApplyUnsupported", "The fixed original filesystem differs from its verified access contract.");
                for (string? p = this.project; p is not null; p = Path.GetDirectoryName(p))
                    foreach (string vcs in new[] { ".git", ".plastic", ".hg", ".svn" })
                        if (Present(Path.Combine(p, vcs)) && !(p == this.project && (allowPlasticAdministration && vcs == ".plastic" || allowGitAdministration && vcs == ".git")))
                            throw new WorkspaceException("VcsMetadataUnsupported", "VCS administration requires its explicit compatibility boundary.");
            }
            catch { Dispose(); throw; }
        }
        private string Full(string relative)
        {
            if (string.IsNullOrEmpty(relative) || relative.Split('/').Any(p => p.Length == 0 || p is "." or ".." ||
                new[] { ".git", ".plastic", ".hg", ".svn" }.Contains(p, StringComparer.OrdinalIgnoreCase) ||
                p.EndsWith(' ') || p.EndsWith('.') || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.Length > 255 ||
                p.Split('.')[0].ToUpperInvariant() is "CON" or "PRN" or "AUX" or "NUL" or "COM1" or "COM2" or "COM3" or "COM4" or "COM5" or "COM6" or "COM7" or "COM8" or "COM9" or "LPT1" or "LPT2" or "LPT3" or "LPT4" or "LPT5" or "LPT6" or "LPT7" or "LPT8" or "LPT9"))
                throw new WorkspaceException("OriginalApplyUnsupported", "Preserve unsupported publication path names.");
            string path = Path.GetFullPath(Path.Combine(project, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw Conflict("A publication path escaped the fixed root.");
            return path;
        }
        private static bool Present(string path)
        {
            try { _ = File.GetAttributes(path); return true; }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return false; }
        }
        private SafeFileHandle? Open(string path, bool mutation, bool stage = false)
        {
            lock (handleGate)
            {
            if (handles.TryGetValue(path, out var existing)) return existing;
            // Desired delete access makes an existing writer's share mask part
            // of admission. No sharing for writes/deletes is granted in return.
            uint access = (Present(path) && (File.GetAttributes(path) & FileAttributes.Directory) != 0 ? 0x20u : 1u) |
                ReadAttributes | ReadControl | (mutation ? DeleteAccess : 0) | (stage ? 0x2u | 0x100u | 0x40000u | 0x80000u : 0);
            var handle = CreateFileW(path, access, 1, IntPtr.Zero, 3, 0x02000000u | (managed && path == project ? 0u : 0x00200000u), IntPtr.Zero);
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error(); handle.Dispose();
                if (error is 2 or 3) return null;
                throw new WorkspaceException("OriginalApplyNativeFailure", "Cannot pin an original or publication handle: " + new Win32Exception(error).Message, new { nativeError = error });
            }
            try
            {
                var info = Information(handle);
                if ((info.Attributes & UnsupportedAttributes) != 0 || (info.Attributes & 16) == 0 && info.Links != 1)
                    throw new WorkspaceException("OriginalApplyUnsupported", "Reparse, compressed, encrypted, sparse or multiply linked original data must be preserved.");
                if (mutation && (info.Attributes & 1) != 0) throw new WorkspaceException("OriginalApplyUnsupported", "Read-only original entries require separate metadata handling.");
                if (!handles.TryAdd(path, handle)) throw Conflict("A pinned original handle was duplicated."); ObserveHandleCount(); return handle;
            }
            catch { handle.Dispose(); throw; }
            }
        }
        private SafeFileHandle PinDirectory(string path)
        {
            var h = Open(path, path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && deleteDirectory(Path.GetRelativePath(project, path).Replace('\\', '/'))) ?? throw Conflict("An original ancestor is missing.");
            if ((Information(h).Attributes & 16) == 0) throw Conflict("An original ancestor is no longer a directory.");
            return h;
        }
        private bool Parents(string path)
        {
            var paths = new Stack<string>();
            for (string? p = Path.GetDirectoryName(path); p is not null && p.Length > project.Length; p = Path.GetDirectoryName(p)) paths.Push(p);
            while (paths.TryPop(out var p))
            {
                if (!Present(p)) return false;
                PinDirectory(p);
            }
            return true;
        }
        public bool NameExists(string relative) => Present(Full(relative));
        public OriginalFileObservation? Observe(string relative, CancellationToken ct, bool mutation = false)
        {
            string path = Full(relative);
            if (!Parents(path)) return null;
            var handle = Open(path, mutation, stage: mutation && Path.GetFileName(path).StartsWith(".splinter-apply-", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".new", StringComparison.Ordinal));
            if (handle is null) return null;
            var info = Information(handle); bool directory = (info.Attributes & 16) != 0;
            if ((info.Attributes & UnsupportedAttributes) != 0 || !directory && info.Links != 1 || mutation && (info.Attributes & 1) != 0)
                throw new WorkspaceException("OriginalApplyUnsupported", "A pinned entry gained unsupported metadata or links.");
            // Exact managed roots advertise NamedStreams=false; preparation
            // rejected ADS on NTFS and this provider cannot create them.
            if (!managed) RequireSingleStream(path, directory);
            string? hash = directory ? null : Hash(handle, Length(info), ct);
            return new(Id(handle), new(directory ? "directory" : "file", directory ? 0 : Length(info), hash,
                (int)info.Attributes, DateTime.FromFileTimeUtc(Time(info.Write)).Ticks));
        }
        public SafeFileHandle Create(string relative, bool directory)
        {
            string path = Full(relative);
            if (!Parents(path)) throw Conflict("The staged parent is not available.");
            var parent = PinDirectory(Path.GetDirectoryName(path)!);
            string leaf = Path.GetFileName(path);
            IntPtr text = Marshal.StringToHGlobalUni(leaf), unicode = IntPtr.Zero;
            try
            {
                var name = new UnicodeString { Length = checked((ushort)(leaf.Length * 2)), MaximumLength = checked((ushort)(leaf.Length * 2)), Buffer = text };
                unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>()); Marshal.StructureToPtr(name, unicode, false);
                var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(), ObjectName = unicode, Attributes = 0x40 };
                uint access = ReadAttributes | ReadControl | DeleteAccess | Synchronize | 0x100 | 0x40000 | 0x80000 | (directory ? 0u : 3u);
                int status = NtCreateFile(out var h, access, ref attributes, out _, IntPtr.Zero, directory ? 16u : 128u, 1, 2,
                    0x20u | 0x00200000u | (directory ? 1u : 0x40u), IntPtr.Zero, 0);
                if (status < 0)
                { h?.Dispose(); throw new WorkspaceException("OriginalApplyNativeFailure", "Create owned publication stage failed.", new { nativeError = RtlNtStatusToDosError(status) }); }
                if (!handles.TryAdd(path, h)) { h.Dispose(); throw Conflict("An owned publication stage was duplicated."); } ObserveHandleCount(); return h;
            }
            finally { if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
        }
        public void Fill(string relative, string identity, OriginalFileVersion version, Func<OriginalFileVersion, Stream> payload, long minimumControlBytes, CancellationToken ct)
        {
            string path = Full(relative); var h = Open(path, true, stage: true) ?? throw Conflict("The owned stage disappeared.");
            if (Id(h) != identity) throw Conflict("The stage identity changed.");
            if (version.Kind == "directory") return;
            using var input = payload(version); byte[] bytes = new byte[65536], existing = new byte[65536];
            long length = Length(Information(h)), offset = 0;
            if (length > version.Length) throw Conflict("The incomplete stage has foreign trailing content.");
            _ = OperationDiskSpace.Require("publish-original-file", path, version.Length, WindowsJournalFilePins.AllocatedBytes(h), minimumControlBytes);
            // The final length is already known. Reserve that allocation once
            // instead of letting incremental EOF growth over-allocate the stage.
            IntPtr allocation = Marshal.AllocHGlobal(8);
            try { Marshal.WriteInt64(allocation, version.Length); Check(SetFileInformationByHandle(h, 5, allocation, 8), "Reserve the bounded publication payload"); }
            finally { Marshal.FreeHGlobal(allocation); }
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while ((count = input.Read(bytes, 0, bytes.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested(); hash.AppendData(bytes, 0, count);
                int retained = (int)Math.Min(count, Math.Max(0, length - offset));
                if (retained > 0 && (RandomAccess.Read(h, existing.AsSpan(0, retained), offset) != retained || !existing.AsSpan(0, retained).SequenceEqual(bytes.AsSpan(0, retained))))
                    throw Conflict("Preserve the altered incomplete payload stage.");
                RandomAccess.Write(h, bytes.AsSpan(retained, count - retained), offset + retained);
                offset += count;
            }
            if (offset != version.Length || Convert.ToHexStringLower(hash.GetHashAndReset()) != version.Hash) throw Conflict("The independent payload changed.");
            Check(FlushFileBuffers(h), "Flush owned publication payload");
        }
        public void CopySecurity(string original, string stage)
        {
            var source = handles[Full(original)]; var target = handles[Full(stage)];
            _ = GetKernelObjectSecurity(source, 7, null, 0, out uint required);
            if (required is 0 or > 65536) throw new WorkspaceException("OriginalApplyUnsupported", "Preserve an unsupported original security descriptor.");
            byte[] descriptor = new byte[required];
            Check(GetKernelObjectSecurity(source, 7, descriptor, required, out _), "Read original security descriptor");
            if (managed)
            {
                // This verified WinFsp view has one fixed owner descriptor and
                // no persistent ACL API. Verify equality instead of requesting
                // an unsupported security mutation or ignoring a mismatch.
                byte[] actual = new byte[required];
                Check(GetKernelObjectSecurity(target, 7, actual, required, out uint length), "Verify managed publication security");
                if (length != required || !actual.AsSpan().SequenceEqual(descriptor))
                    throw new WorkspaceException("OriginalApplyUnsupported", "Preserve differing managed publication security.");
                return;
            }
            // Also preserve the DACL protection flag, so inheritance does not
            // silently change a file's permissions when it is replaced.
            Check(GetSecurityDescriptorControl(descriptor, out ushort control, out _), "Read original security control");
            Check(SetKernelObjectSecurity(target, 7u | ((control & 0x1000) != 0 ? 0x80000000u : 0x20000000u), descriptor), "Preserve original owner, group and DACL");
        }
        public void SetMetadata(string relative, OriginalFileVersion version, string? original)
        {
            var h = handles[Full(relative)]; var info = Information(h);
            var basic = new BasicInfo { Creation = original is null ? Time(info.Creation) : Time(Information(handles[Full(original)]).Creation),
                Write = new DateTime(version.ModifiedUtcTicks, DateTimeKind.Utc).ToFileTimeUtc(), Attributes = (uint)version.Attributes };
            IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<BasicInfo>());
            try { Marshal.StructureToPtr(basic, buffer, false); Check(SetFileInformationByHandle(h, 0, buffer, (uint)Marshal.SizeOf<BasicInfo>()), "Preserve supported file metadata"); }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public void Rename(string from, string to, string identity)
        {
            string source = Full(from), target = Full(to); var h = handles[source];
            if (Id(h) != identity || !Parents(target)) throw Conflict("A publication identity changed.");
            _ = PinDirectory(Path.GetDirectoryName(target)!);
            if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(target), StringComparison.OrdinalIgnoreCase))
                throw Conflict("Publication renames must stay in the pinned same directory.");
            byte[] name = Encoding.Unicode.GetBytes(Path.GetFileName(target));
            int rootOffset = IntPtr.Size == 8 ? 8 : 4, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
            int size = nameOffset + name.Length + 8;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.WriteInt32(buffer, 0, 0); // ReplaceIfExists = false
                Marshal.WriteIntPtr(buffer, rootOffset, IntPtr.Zero); Marshal.WriteInt32(buffer, lengthOffset, name.Length);
                Marshal.Copy(name, 0, buffer + nameOffset, name.Length);
                Marshal.WriteInt64(buffer, nameOffset + name.Length, 0);
                int status = NtSetInformationFile(h, out _, buffer, (uint)size, 10);
                if (status < 0) throw new WorkspaceException("OriginalApplyNativeFailure", "Publish without replacing an occupant failed.", new { nativeError = RtlNtStatusToDosError(status) });
                if (!handles.TryRemove(source, out _) || !handles.TryAdd(target, h)) throw Conflict("The pinned rename inventory changed.");
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        public void Delete(string relative, string identity)
        {
            string path = Full(relative); var h = handles[path];
            if (Id(h) != identity) throw Conflict("A cleanup identity changed.");
            IntPtr buffer = Marshal.AllocHGlobal(4);
            try { Marshal.WriteInt32(buffer, 1); Check(SetFileInformationByHandle(h, 4, buffer, 4), "Delete only the exact owned or planned entry"); }
            finally { Marshal.FreeHGlobal(buffer); }
            if (!handles.TryRemove(path, out _)) throw Conflict("The pinned deletion inventory changed."); Interlocked.Decrement(ref activeHandles); h.Dispose();
            try
            {
                if (Present(path)) throw new WorkspaceException("OriginalApplyPending", "The exact deleted entry still has a live name or reader; preserve its receipt.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new WorkspaceException("OriginalApplyPending", "Deletion absence cannot yet be observed; preserve the exact publication intent.", new { ex.HResult }); }
        }
        private static string Hash(SafeFileHandle handle, long length, CancellationToken ct)
        {
            // Opening FILE_READ_DATA is required for these reads; CreateFile's
            // directory-safe access is augmented for ordinary files below.
            byte[] buffer = new byte[65536]; long offset = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            while (offset < length)
            { ct.ThrowIfCancellationRequested(); int count = RandomAccess.Read(handle, buffer.AsSpan(0, (int)Math.Min(buffer.Length, length - offset)), offset); if (count == 0) throw Conflict("An original read ended early."); hash.AppendData(buffer, 0, count); offset += count; }
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        private static void RequireSingleStream(string path, bool directory)
        {
            var find = FindFirstStreamW(path, 0, out var data, 0);
            if (find == new IntPtr(-1))
            { if (directory && Marshal.GetLastWin32Error() == 38) return; Check(false, "Inspect original streams"); return; }
            try
            {
                if (directory || data.Name != "::$DATA" || FindNextStreamW(find, out _)) throw new WorkspaceException("OriginalApplyUnsupported", "Alternate data streams require separate preservation.");
                int error = Marshal.GetLastWin32Error(); if (error != 38) Check(false, "Inspect original stream tail");
            }
            finally { _ = FindClose(find); }
        }
        public void Dispose() { foreach (var pair in handles) pair.Value.Dispose(); handles.Clear(); }
    }

    // Resolve the destination leaf from an already verified ordinary parent,
    // without traversing its mutable absolute name again. The caller retains
    // both identities and checks parent type/path before and after publication.
    internal static void MoveOwnedEntry(SafeFileHandle source, SafeFileHandle parent, string leaf)
    {
        if (string.IsNullOrEmpty(leaf) || leaf is "." or ".." || leaf.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new WorkspaceException("GeneratedRestoreConflict", "Invalid topology destination leaf.");
        byte[] name = Encoding.Unicode.GetBytes(leaf);
        int rootOffset = IntPtr.Size == 8 ? 8 : 4, lengthOffset = rootOffset + IntPtr.Size, nameOffset = lengthOffset + 4;
        int size = nameOffset + name.Length + 8;
        IntPtr buffer = Marshal.AllocHGlobal(size); bool parentRef = false;
        try
        {
            parent.DangerousAddRef(ref parentRef);
            Marshal.WriteInt32(buffer, 0, 0); // ReplaceIfExists = false.
            Marshal.WriteIntPtr(buffer, rootOffset, parent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, name.Length);
            Marshal.Copy(name, 0, buffer + nameOffset, name.Length);
            Marshal.WriteInt64(buffer, nameOffset + name.Length, 0);
            // FILE_RENAME_INFORMATION: exact RootDirectory + one leaf name.
            // https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information
            int status = NtSetInformationFile(source, out _, buffer, (uint)size, 10);
            if (status < 0) throw new WorkspaceException("GeneratedRestoreConflict", "Cannot publish the topology entry into its pinned parent.", new { nativeError = RtlNtStatusToDosError(status) });
        }
        finally { if (parentRef) parent.DangerousRelease(); Marshal.FreeHGlobal(buffer); }
    }

    // The restoration caller owns and validates the ordinary parent handle.
    // The returned handle is the newly created identity, not a path reopen.
    internal static SafeFileHandle CreateOwnedDirectory(SafeFileHandle parent, string leaf, bool allowChildMutations = false) =>
        CreateOwnedEntry(parent, leaf, directory: true, allowChildMutations: allowChildMutations);
    internal static SafeFileHandle CreateOwnedFile(SafeFileHandle parent, string leaf) => CreateOwnedEntry(parent, leaf, directory: false);
    private static SafeFileHandle CreateOwnedEntry(SafeFileHandle parent, string leaf, bool directory, bool allowChildMutations = false)
    {
        if (string.IsNullOrEmpty(leaf) || leaf is "." or ".." || leaf.IndexOfAny(['/', '\\', ':']) >= 0)
            throw new WorkspaceException("GeneratedRestoreConflict", "Invalid owned-entry leaf.");
        bool parentRef = false;
        IntPtr text = Marshal.StringToHGlobalUni(leaf), unicode = IntPtr.Zero;
        try
        {
            parent.DangerousAddRef(ref parentRef);
            var name = new UnicodeString { Length = checked((ushort)(leaf.Length * 2)), MaximumLength = checked((ushort)(leaf.Length * 2)), Buffer = text };
            unicode = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>()); Marshal.StructureToPtr(name, unicode, false);
            var attributes = new ObjectAttributes { Length = Marshal.SizeOf<ObjectAttributes>(), RootDirectory = parent.DangerousGetHandle(), ObjectName = unicode, Attributes = 0x40 };
            // Directories: LIST_DIRECTORY | READ/WRITE_ATTRIBUTES | DELETE |
            // SYNCHRONIZE, SHARE_READ. Files: GENERIC_READ | GENERIC_WRITE |
            // SYNCHRONIZE, no sharing. Both exclusively FILE_CREATE with
            // SYNCHRONOUS_NONALERT | OPEN_REPARSE and an exact type constraint.
            // A scratch namespace keeps only a read/list/synchronize fence:
            // permit child metadata operations while denying deletion of this
            // exact created directory. Restore's moving/writable owner stays
            // unchanged and continues to share only reads.
            uint access = directory ? allowChildMutations ? 0x00100081u : 0x00110181u : 0xC0100000u;
            uint share = directory ? allowChildMutations ? 3u : 1u : 0u;
            int status = NtCreateFile(out var handle, access, ref attributes, out var io, IntPtr.Zero,
                directory ? 16u : 128u, share, 2, directory ? 0x00200021u : 0x00200060u, IntPtr.Zero, 0);
            if (status < 0 || io.Information != (IntPtr)2)
            {
                handle?.Dispose();
                throw new WorkspaceException("GeneratedRestoreConflict", "Cannot exclusively create the owned entry.", new { nativeError = RtlNtStatusToDosError(status) });
            }
            return handle;
        }
        finally { if (parentRef) parent.DangerousRelease(); if (unicode != IntPtr.Zero) Marshal.FreeHGlobal(unicode); Marshal.FreeHGlobal(text); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeTime { public uint Low, High; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public uint Attributes; public NativeTime Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [StructLayout(LayoutKind.Sequential)] private struct BasicInfo { public long Creation, Access, Write, Change; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString { public ushort Length, MaximumLength; public IntPtr Buffer; }
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes { public int Length; public IntPtr RootDirectory, ObjectName; public uint Attributes; public IntPtr SecurityDescriptor, SecurityQualityOfService; }
    [StructLayout(LayoutKind.Sequential)] private struct IoStatus { public IntPtr Status, Information; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StreamData { public long Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)] public string Name; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle h, out Info info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetVolumeInformationByHandleW(SafeFileHandle h, StringBuilder? volume, int capacity, out uint serial, out uint max, out uint flags, StringBuilder fs, int fsCapacity);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle h, int kind, IntPtr buffer, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FlushFileBuffers(SafeFileHandle h);
    [DllImport("ntdll.dll")] private static extern int NtCreateFile(out SafeFileHandle h, uint access, ref ObjectAttributes attributes, out IoStatus status, IntPtr allocation, uint fileAttributes, uint share, uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("ntdll.dll")] private static extern uint RtlNtStatusToDosError(int status);
    [DllImport("ntdll.dll")] private static extern int NtSetInformationFile(SafeFileHandle h, out IoStatus status, IntPtr buffer, uint size, int kind);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetKernelObjectSecurity(SafeFileHandle h, uint information, byte[]? descriptor, uint length, out uint required);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetKernelObjectSecurity(SafeFileHandle h, uint information, byte[] descriptor);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSecurityDescriptorControl(byte[] descriptor, out ushort control, out uint revision);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr FindFirstStreamW(string path, int level, out StreamData data, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindNextStreamW(IntPtr h, out StreamData data);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FindClose(IntPtr h);
}
