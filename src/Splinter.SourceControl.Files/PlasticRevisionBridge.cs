#nullable enable
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

namespace Splinter.SourceControl.Files
{
    // Also compiled inside the owned PowerShell child. The vendor adapter is
    // fixed to the qualified client; it never starts cm or a REST listener.
    internal static class PlasticRevisionBridge
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private const int SegmentBytes = 4 * 1024 * 1024;
        public static void Run()
        {
            using var output = new BinaryWriter(Console.OpenStandardOutput());
            Console.SetOut(TextWriter.Null);
            string stage = "client-load";
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PlasticSCM5", "client");
                string client = Path.Combine(directory, "cm.dll");
                var context = new ClientContext(client);
                var api = context.LoadFromAssemblyPath(client);
                var core = context.LoadFromAssemblyPath(Path.Combine(directory, "plasticclientcore.dll"));
                if (api.GetName().Version != new Version(11, 0, 16, 10484) || core.GetName().Version != api.GetName().Version)
                { output.Write(-2); output.Flush(); return; }
                string setup = Console.ReadLine() ?? throw new EndOfStreamException();
                if (setup.Length > 4096) throw new InvalidDataException();
                using var json = JsonDocument.Parse(setup);
                string repository = json.RootElement.GetProperty("repository").GetString() ?? throw new InvalidDataException();
                Type apiType = api.GetType("Codice.Client.Api.BaseCommandsAPI", true)!;
                stage = "client-initialize";
                Call(apiType, null, "Init", new object?[] { null });
                object instance = Call(apiType, null, "Get", Array.Empty<object?>());
                object info = apiType.GetField("mObjectInfoProvider", Members)!.GetValue(instance)!;
                stage = "repository-identity";
                object repo = Call(info.GetType(), info, "GetRepositoryInfo", new object?[] { repository, null });
                Type connectionType = core.GetType("Codice.Client.Common.CmConnection", true)!;
                object connection = Call(connectionType, null, "Get", Array.Empty<object?>());
                object handler = Call(connectionType, connection, "GetItemHandler", new object?[] { Property(repo, "Server") });
                Type downloaderType = core.GetType("Codice.Client.BaseCommands.Download.StandardDataPacketDownloader", true)!;
                object downloader = downloaderType.GetConstructors(Members).Single().Invoke(new[] { Field(repo, "Id"), handler, null });
                Type compression = core.GetType("Codice.CM.Common.Compression", true)!;
                Type compressionType = core.GetType("Codice.CM.Common.EnumCompressionType", true)!;
                byte[] decoded = new byte[SegmentBytes];
                var prepared = new Dictionary<long, byte[]>();
                string? request;
                while ((request = Console.ReadLine()) != null)
                {
                    stage = "revision-request";
                    if (request.StartsWith("{", StringComparison.Ordinal))
                    {
                        if (request.Length > 16384) throw new InvalidDataException();
                        using var batchJson = JsonDocument.Parse(request);
                        var root = batchJson.RootElement;
                        long requested = long.Parse(root.GetProperty("requested").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                        BatchItem[] items = root.GetProperty("batch").EnumerateArray().Select(item => new BatchItem(
                            long.Parse(item.GetProperty("revision").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                            item.GetProperty("length").GetInt64(), item.GetProperty("contentHash").GetString()!)).ToArray();
                        ValidateBatchRequests(items);
                        stage = "small-files-packet";
                        object packet = Call(downloaderType, downloader, "GetSmallFilesData", new object?[] { items.Select(item => item.Revision).ToArray() });
                        if ((long)Call(packet.GetType(), packet, "GetSize", Array.Empty<object?>()) > SegmentBytes + 65536) throw new InvalidDataException();
                        object reader = Call(packet.GetType(), packet, "GetReader", Array.Empty<object?>());
                        try
                        {
                            IEnumerable<BatchRecord> Records()
                            {
                                while (!(bool)Call(reader.GetType(), reader, "Eof", Array.Empty<object?>()))
                                {
                                    object data = Call(reader.GetType(), reader, "ReadFileData", Array.Empty<object?>());
                                    yield return new BatchRecord((long)Field(data, "objId"), (ulong)Field(data, "segment"),
                                        (bool)Field(data, "bLastSegment"), (short)Field(data, "compression"), (byte[])Field(data, "Data"));
                                }
                            }
                            stage = "small-files-proof";
                            prepared = ValidateBatch(items, Records(), decoded, (format, stored) => (int)Call(compression, null, "Uncompress",
                                new object?[] { Enum.ToObject(compressionType, format), stored, stored.Length, decoded }));
                        }
                        finally { Call(reader.GetType(), reader, "CloseRead", Array.Empty<object?>()); }
                        if (!prepared.TryGetValue(requested, out var bytes)) throw new InvalidDataException();
                        WriteRevision(output, bytes); continue;
                    }
                    if (request.Length is < 1 or > 20 || request.Any(c => c < '0' || c > '9') || !long.TryParse(request, out long revision)) throw new InvalidDataException();
                    if (prepared.TryGetValue(revision, out var cached)) { WriteRevision(output, cached); continue; }
                    prepared.Clear();
                    ulong segment = 0;
                    while (true)
                    {
                        stage = "revision-segment";
                        object packet = Call(downloaderType, downloader, "GetSegmentData", new object?[] { revision, segment });
                        object reader = Call(packet.GetType(), packet, "GetReader", Array.Empty<object?>());
                        try
                        {
                            object data = Call(reader.GetType(), reader, "ReadFileData", Array.Empty<object?>());
                            byte[] stored = (byte[])Field(data, "Data");
                            if ((long)Field(data, "objId") != revision || (ulong)Field(data, "segment") != segment || stored.Length > SegmentBytes + 65536)
                                throw new InvalidDataException();
                            short format = (short)Field(data, "compression");
                            int length = DecodeSegment(format, stored, decoded, () => (int)Call(compression, null, "Uncompress",
                                new object?[] { Enum.ToObject(compressionType, format), stored, stored.Length, decoded }));
                            for (int offset = 0; offset < length; offset += 65536)
                            {
                                int count = Math.Min(65536, length - offset);
                                output.Write(count); output.Write(decoded, offset, count); output.Flush();
                            }
                            if ((bool)Field(data, "bLastSegment")) break;
                            if (length == 0 || segment == ulong.MaxValue) throw new InvalidDataException();
                            segment++;
                        }
                        finally { Call(reader.GetType(), reader, "CloseRead", Array.Empty<object?>()); }
                    }
                    output.Write(0); output.Flush();
                }
            }
            catch (Exception ex)
            {
                if (Environment.GetEnvironmentVariable("SPLINTER_PLASTIC_READER_DIAGNOSTICS") == "1")
                    Console.Error.WriteLine("plastic-reader-stage=" + stage + ";exception=" + (ex is TargetInvocationException invocation ? invocation.InnerException?.GetType().Name : ex.GetType().Name));
                output.Write(-1); output.Flush();
            }
        }
        private static void WriteRevision(BinaryWriter output, byte[] bytes)
        {
            for (int offset = 0; offset < bytes.Length; offset += 65536)
            { int count = Math.Min(65536, bytes.Length - offset); output.Write(count); output.Write(bytes, offset, count); output.Flush(); }
            output.Write(0); output.Flush();
        }
        internal sealed record BatchItem(long Revision, long Length, string ContentHash);
        internal sealed record BatchRecord(long Revision, ulong Segment, bool Last, short Format, byte[] Stored);
        internal static void ValidateBatchRequests(IReadOnlyList<BatchItem> items)
        {
            if (items.Count is < 1 or > 64) throw new InvalidDataException();
            var ids = new HashSet<long>(); long length = 0;
            foreach (var item in items)
                if (item.Revision < 0 || item.Length < 0 || (length = checked(length + item.Length)) > SegmentBytes || !ids.Add(item.Revision) ||
                    Convert.FromBase64String(item.ContentHash).Length != 16) throw new InvalidDataException();
        }
        internal static Dictionary<long, byte[]> ValidateBatch(IReadOnlyList<BatchItem> items, IEnumerable<BatchRecord> records,
            byte[] decoded, Func<short, byte[], int> uncompress)
        {
            ValidateBatchRequests(items);
            var expected = items.ToDictionary(item => item.Revision); var result = new Dictionary<long, byte[]>();
            foreach (var record in records)
            {
                if (!expected.TryGetValue(record.Revision, out var item) || result.ContainsKey(record.Revision) || record.Segment != 0 || !record.Last)
                    throw new InvalidDataException();
                int length = DecodeSegment(record.Format, record.Stored, decoded, () => uncompress(record.Format, record.Stored));
                if (length != item.Length || !MD5.HashData(decoded.AsSpan(0, length)).AsSpan().SequenceEqual(Convert.FromBase64String(item.ContentHash)))
                    throw new InvalidDataException();
                result.Add(record.Revision, decoded.AsSpan(0, length).ToArray());
            }
            if (result.Count != expected.Count) throw new InvalidDataException();
            return result;
        }
        internal static int DecodeSegment(short format, byte[] stored, byte[] decoded, Func<int> uncompress)
        {
            // Qualified client formats: None=0, ZLib=2. Uncompress returns zero
            // for None, so raw packets (including BOMs) must bypass it.
            if (decoded.Length != SegmentBytes || stored.Length > SegmentBytes + 65536)
                throw new InvalidDataException();
            if (format == 0)
            {
                if (stored.Length > decoded.Length) throw new InvalidDataException();
                Buffer.BlockCopy(stored, 0, decoded, 0, stored.Length);
                return stored.Length;
            }
            if (format != 2) throw new InvalidDataException();
            int length = stored.Length == 0 ? 0 : uncompress();
            if (length < 0 || length > decoded.Length || (stored.Length != 0 && length == 0))
                throw new InvalidDataException();
            return length;
        }
        private static object Call(Type type, object? target, string name, object?[] arguments) =>
            type.GetMethods(Members).Single(m => m.Name == name && m.GetParameters().Length == arguments.Length).Invoke(target, arguments)!;
        private static object Field(object value, string name) => value.GetType().GetField(name, Members)!.GetValue(value)!;
        private static object Property(object value, string name) => value.GetType().GetProperty(name, Members)!.GetValue(value)!;
        private sealed class ClientContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver resolver;
            internal ClientContext(string client) { resolver = new AssemblyDependencyResolver(client); }
            protected override Assembly? Load(AssemblyName name)
            { string? path = resolver.ResolveAssemblyToPath(name); return path == null ? null : LoadFromAssemblyPath(path); }
            protected override IntPtr LoadUnmanagedDll(string name)
            { string? path = resolver.ResolveUnmanagedDllToPath(name); return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path); }
        }
    }
}
