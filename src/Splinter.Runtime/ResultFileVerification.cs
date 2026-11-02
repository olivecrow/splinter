using Splinter.Core;

namespace Splinter.Runtime;

public sealed partial class ResultFileContentStore
{
    public VerificationScope BeginVerification() => new(this);
    // A verified composed-file hash is a proof of this exact descriptor and
    // its content-addressed inputs. Re-prove every input's current ownership
    // and bytes before reusing it; equal result/root hashes alone do not suffice.
    public sealed class VerificationScope : IDisposable
    {
        private readonly ResultFileContentStore owner;
        private readonly ImmutableReadProofs proofs;
        private readonly HashSet<(string Hash, ImmutableFileContentDescriptor Descriptor)> files = [];
        private readonly object gate = new();
        private bool disposed;
        internal long FullFileHashes { get; private set; }
        internal long CompositionReuses { get; private set; }
        internal long PayloadHashes => proofs.HashAdmissions;
        internal VerificationScope(ResultFileContentStore owner) { this.owner = owner; proofs = new(owner.pins); }
        public void Verify(string hash, long length, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(disposed, this); ValidateHash(hash);
            if (length < 0) throw Invalid("An independent file has a negative length.");
            if (owner.fixedRoot is not null || File.Exists(Path.Combine(owner.directory, "catalog.json")))
            {
                using var reader = ImmutableReadProofs.OpenMetadata(owner.directory, proofs); var pages = reader.Pages;
                var entry = pages.Open(owner.fixedRoot ?? owner.Load(pages, proofs).Files).FindEntry(hash);
                if (entry is not null)
                {
                    var descriptor = Decode(entry, pages.Empty);
                    if (descriptor.Length != length) throw Invalid("The independent descriptor length changed.");
                    bool reuse; lock (gate) reuse = files.Contains((hash, descriptor));
                    if (reuse)
                    {
                        // Reopen through ordinary ownership/catalog checks. The
                        // byte proof is reused only under the exact native fence;
                        // over-budget inputs are hashed again by OpenReference.
                        if (descriptor.BasisHash is not null)
                            using (owner.OpenReference(new(descriptor.BasisHash, descriptor.BasisObjectLength, descriptor.BasisRawRange), proofs, ct)) { }
                        long end = 0;
                        foreach (var item in pages.Open(descriptor.Ranges).Enumerate())
                        {
                            ct.ThrowIfCancellationRequested(); var range = DecodeRange(item);
                            if (range.Offset < end || range.Offset > length - range.Length) throw Invalid("The independent ranges overlap or exceed the file.");
                            end = checked(range.Offset + range.Length);
                            using (owner.OpenChunk(range, ct, proofs)) { }
                        }
                        lock (gate) CompositionReuses++;
                    }
                    else
                    {
                        using var input = new Composite(owner, descriptor, pages, false, ct, proofs);
                        VerifyFile(input, hash, length, ct);
                        lock (gate)
                        {
                            FullFileHashes++;
                            // External Git basis availability retains its own
                            // boundary proof and the ordinary whole-file path.
                            if (!descriptor.ExternalBasis && files.Count < 1024) files.Add((hash, descriptor));
                        }
                    }
                    return;
                }
            }
            if (owner.externalBasis is not null && !File.Exists(Path.Combine(owner.control, "files", "objects", hash)))
            { using var external = owner.externalBasis(hash, length); VerifyFile(external, hash, length, ct); return; }
            // OwnedContentStore proves the entire whole-object hash itself.
            using (owner.whole.OpenVerified(owner.workspace, owner.control, hash, length, proofs, ct)) { }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; files.Clear(); proofs.Dispose();
        }
    }
}
