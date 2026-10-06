using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class JournalDeltaStore
{
    private sealed record JournalInitialization(int Version, OverlayIdentity Identity, string Attempt);
    private static readonly JsonSerializerOptions InitializationJson = new()
    { RespectRequiredConstructorParameters = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private static string InitializationPath(string directory) => Path.Combine(directory, "journal-initialization.json");

    // This intent selects ON only for a new journal. Reconnect never converts an
    // existing SJR2 journal, including an empty one. No mutation is allowed until
    // the seed and identity have been published and the intent has been removed.
    private static void InitializeLatest(string directory, OverlayIdentity identity, IJournalMutationObserver? observer)
    {
        string intentPath = InitializationPath(directory), journalPath = Path.Combine(directory, "delta.journal");
        string identityPath = Path.Combine(directory, "identity.json"), guardPath = journalPath + ".writer.lock";
        foreach (string path in new[] { intentPath, intentPath + ".pending", journalPath, identityPath, guardPath })
            WorkspaceStore.EnsurePlainPath(path);
        JournalInitialization intent;
        if (File.Exists(intentPath) || File.Exists(intentPath + ".pending"))
        {
            if (File.Exists(intentPath) && File.Exists(intentPath + ".pending"))
                throw LatestJournalStorage.Corrupt("Two initialization intents remain; preserve both.");
            string savedPath = File.Exists(intentPath) ? intentPath : intentPath + ".pending";
            if (new FileInfo(savedPath).Length > 65536) throw LatestJournalStorage.Corrupt("The initialization intent exceeds its bound.");
            try { intent = JsonSerializer.Deserialize<JournalInitialization>(File.ReadAllBytes(savedPath), InitializationJson)
                ?? throw LatestJournalStorage.Corrupt("The journal initialization intent is empty."); }
            catch (JsonException) { throw LatestJournalStorage.Corrupt("The journal initialization intent is incomplete or corrupt."); }
        }
        else
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                throw LatestJournalStorage.Corrupt("A new journal directory contains unowned entries.");
            intent = new(1, identity, Guid.NewGuid().ToString("N"));
            using (var pending = new FileStream(intentPath + ".pending", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            { JsonSerializer.Serialize(pending, intent, InitializationJson); pending.Flush(true); }
            observer?.Observe(new("latest.initialize.intent-flushed", "initialize", 0, 0, 0, 0));
        }
        if (intent.Version != 1 || !Guid.TryParseExact(intent.Attempt, "N", out _) ||
            JsonSerializer.Serialize(intent.Identity) != JsonSerializer.Serialize(identity))
            throw LatestJournalStorage.Corrupt("The initialization intent has another identity or format.");
        if (!File.Exists(intentPath))
        {
            using (var pending = new FileStream(intentPath + ".pending", FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) pending.Flush(true);
            File.Move(intentPath + ".pending", intentPath);
        }
        using var guard = new FileStream(guardPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read, 1);
        if (guard.Length != 0) throw LatestJournalStorage.Corrupt("The initialization writer guard changed.");
        void Visit(string boundary) => observer?.Observe(new(boundary, "initialize", 0, 0, 0, 0));
        Visit("latest.initialize.intent");
        PublishSeed(identityPath, JsonSerializer.SerializeToUtf8Bytes(identity), () => Visit("latest.initialize.identity-flushed"));
        Visit("latest.initialize.identity");
        string objectsName = "latest-" + intent.Attempt, objectsPath = Path.Combine(directory, objectsName);
        WorkspaceStore.EnsurePlainPath(objectsPath); Directory.CreateDirectory(objectsPath);
        foreach (string entry in Directory.EnumerateFileSystemEntries(objectsPath))
        {
            WorkspaceStore.EnsurePlainPath(entry);
            if (!File.Exists(entry) || Path.GetFileName(entry) != "owner.lock" || new FileInfo(entry).Length != 0)
                throw LatestJournalStorage.Corrupt("An unfinished seed contains an unowned object.");
        }
        using (var owner = new FileStream(Path.Combine(objectsPath, "owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
            owner.Flush(true);
        Visit("latest.initialize.objects");
        var root = new LatestJournalStorage.Root(4, LatestJournalStorage.Hash(JsonSerializer.SerializeToUtf8Bytes(identity)),
            objectsName, 0, 1, 0, 1, 0, 0, []);
        byte[] rootBytes = LatestJournalStorage.Encode(root, "SJL4");
        if (rootBytes.Length > identity.MaxJournalBytes) throw new WorkspaceException("DeltaFull", "The initial journal exceeds its budget.");
        PublishSeed(journalPath, rootBytes, () => Visit("latest.initialize.root-flushed"));
        Visit("latest.initialize.published");
        File.Delete(intentPath);
    }

    private static void PublishSeed(string path, byte[] bytes, Action? flushed = null)
    {
        string pending = path + ".initializing"; WorkspaceStore.EnsurePlainPath(pending);
        if (File.Exists(path))
        {
            if (File.Exists(pending) || new FileInfo(path).Length != bytes.Length || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                throw LatestJournalStorage.Corrupt("A published seed differs from its initialization intent.");
            return;
        }
        if (File.Exists(pending))
        {
            if (new FileInfo(pending).Length != bytes.Length || !File.ReadAllBytes(pending).AsSpan().SequenceEqual(bytes))
                throw LatestJournalStorage.Corrupt("An unfinished seed differs from its initialization intent.");
            using var file = new FileStream(pending, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            file.Flush(true);
        }
        else
        {
            using var file = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            file.Write(bytes); file.Flush(true);
        }
        flushed?.Invoke();
        File.Move(pending, path);
    }
}
