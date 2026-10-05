#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PlasticSCM5", "client");
                string client = Path.Combine(directory, "cm.dll");
                var context = new ClientContext(client);
                var api = context.LoadFromAssemblyPath(client);
                var core = context.LoadFromAssemblyPath(Path.Combine(directory, "plasticclientcore.dll"));
                if (api.GetName().Version != new Version(11, 0, 16, 9915) || core.GetName().Version != api.GetName().Version)
                { output.Write(-2); output.Flush(); return; }
                string setup = Console.ReadLine() ?? throw new EndOfStreamException();
                if (setup.Length > 4096) throw new InvalidDataException();
                using var json = JsonDocument.Parse(setup);
                string repository = json.RootElement.GetProperty("repository").GetString() ?? throw new InvalidDataException();
                Type apiType = api.GetType("Codice.Client.Api.BaseCommandsAPI", true)!;
                Call(apiType, null, "Init", new object?[] { null });
                object instance = Call(apiType, null, "Get", Array.Empty<object?>());
                object info = apiType.GetField("mObjectInfoProvider", Members)!.GetValue(instance)!;
                object repo = Call(info.GetType(), info, "GetRepositoryInfo", new object?[] { repository, null });
                Type connectionType = core.GetType("Codice.Client.Common.CmConnection", true)!;
                object connection = Call(connectionType, null, "Get", Array.Empty<object?>());
                object handler = Call(connectionType, connection, "GetItemHandler", new object?[] { Property(repo, "Server") });
                Type downloaderType = core.GetType("Codice.Client.BaseCommands.Download.StandardDataPacketDownloader", true)!;
                object downloader = downloaderType.GetConstructors(Members).Single().Invoke(new[] { Field(repo, "Id"), handler, null });
                Type compression = core.GetType("Codice.CM.Common.Compression", true)!;
                Type compressionType = core.GetType("Codice.CM.Common.EnumCompressionType", true)!;
                byte[] decoded = new byte[SegmentBytes];
                string? request;
                while ((request = Console.ReadLine()) != null)
                {
                    if (request.Length is < 1 or > 20 || request.Any(c => c < '0' || c > '9') || !long.TryParse(request, out long revision)) throw new InvalidDataException();
                    ulong segment = 0;
                    while (true)
                    {
                        object packet = Call(downloaderType, downloader, "GetSegmentData", new object?[] { revision, segment });
                        object reader = Call(packet.GetType(), packet, "GetReader", Array.Empty<object?>());
                        try
                        {
                            object data = Call(reader.GetType(), reader, "ReadFileData", Array.Empty<object?>());
                            byte[] stored = (byte[])Field(data, "Data");
                            if ((long)Field(data, "objId") != revision || (ulong)Field(data, "segment") != segment || stored.Length > SegmentBytes + 65536)
                                throw new InvalidDataException();
                            short format = (short)Field(data, "compression");
                            int length = stored.Length == 0 ? 0 : (int)Call(compression, null, "Uncompress",
                                new object?[] { Enum.ToObject(compressionType, format), stored, stored.Length, decoded });
                            if (length < 0 || length > decoded.Length) throw new InvalidDataException();
                            for (int offset = 0; offset < length; offset += 65536)
                            {
                                int count = Math.Min(65536, length - offset);
                                output.Write(count); output.Write(decoded, offset, count); output.Flush();
                            }
                            if ((bool)Field(data, "bLastSegment")) break;
                            if (length == 0 || ++segment > 2048) throw new InvalidDataException();
                        }
                        finally { Call(reader.GetType(), reader, "CloseRead", Array.Empty<object?>()); }
                    }
                    output.Write(0); output.Flush();
                }
            }
            catch { output.Write(-1); output.Flush(); }
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
