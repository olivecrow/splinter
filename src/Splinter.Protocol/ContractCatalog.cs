using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace Splinter.Protocol;

public sealed class ContractCatalog
{
    private readonly Dictionary<string, JsonElement> documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonSchema> schemas = new(StringComparer.Ordinal);

    public ContractCatalog(bool development = false)
    {
        Assembly assembly = typeof(ContractCatalog).Assembly;
        // Catalogs can coexist in one process; never register application schemas globally.
        var buildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".json", StringComparison.Ordinal)))
        {
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("$id", out var id))
                continue;
            string key = id.GetString()!;
            documents.Add(key, root.Clone());
            schemas.Add(key, JsonSchema.FromText(root.GetRawText(), buildOptions));
        }

        string catalogName = assembly.GetManifestResourceNames().Single(name => name.EndsWith("commands.json", StringComparison.Ordinal));
        using Stream catalogStream = assembly.GetManifestResourceStream(catalogName)!;
        using var catalogDocument = JsonDocument.Parse(catalogStream);
        Commands = catalogDocument.RootElement.GetProperty("commands").EnumerateArray()
            .Where(item => development || !item.TryGetProperty("developmentOnly", out var flag) || !flag.GetBoolean())
            .Select(item => new CommandDescriptor(
                item.GetProperty("id").GetString()!, item.GetProperty("description").GetString()!,
                item.GetProperty("cliUsage").GetString()!, item.GetProperty("inputTransport").GetString()!,
                item.GetProperty("inputSchema").GetString()!, item.GetProperty("outputSchema").GetString()!,
                item.GetProperty("sideEffects").GetString()!, item.GetProperty("retry").GetString()!))
            .ToArray();
        if (development)
            Commands = Commands.Select(command => command.Id == "workspace.ensure"
                ? command with { InputSchema = "urn:splinter:v1:workspace-development-ensure-request",
                    CliUsage = "splinter-dev workspace ensure --project <path> --key <key> --storage <ntfs-snapshot|git-snapshot|plastic-snapshot|project-snapshot|git-checkout> [--source git|none|plastic] [--application unity|none] [--base <reference>] [--editor] [--home <path>] [--max-journal-bytes <bytes>] [--library-basis <id>] [--project-basis <id>]" }
                : command).ToArray();
        foreach (var command in Commands)
        {
            if (!schemas.ContainsKey(command.InputSchema) || !schemas.ContainsKey(command.OutputSchema))
                throw new InvalidOperationException($"Missing schema for '{command.Id}'.");
        }
    }

    public IReadOnlyList<CommandDescriptor> Commands { get; }
    public IReadOnlyCollection<string> SchemaIds => documents.Keys;
    public JsonElement GetSchema(string id) => documents[id];
    public CommandDescriptor? Find(string command) => Commands.FirstOrDefault(item => item.Id == command);
    public EvaluationResults Validate(string schemaId, JsonElement value) =>
        schemas[schemaId].Evaluate(value, new EvaluationOptions { OutputFormat = OutputFormat.List });
}

public sealed record CommandDescriptor(
    string Id, string Description, string CliUsage, string InputTransport,
    string InputSchema, string OutputSchema, string SideEffects, string Retry);
