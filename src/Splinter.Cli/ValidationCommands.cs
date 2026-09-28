using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using Splinter.Core;
using Splinter.Protocol;
using Splinter.Runtime;
using Splinter.Workflows.Windows;

namespace Splinter.Cli;

internal static class ValidationCommands
{
    public static void Add(RootCommand root, ContractCatalog contracts, Func<ParseResult, string> home,
        Action<string> select, Action<ApiResponse> respond, CancellationToken ct)
    {
        var command = new Command("validation", "Publish worker candidates and control one persistent Unity validation view.");
        var input = new Option<string>("--input") { Required = true };
        command.Options.Add(input); root.Subcommands.Add(command);
        command.SetAction(async parse =>
        {
            select("validation.execute");
            using var stream = File.OpenRead(parse.GetValue(input)!);
            if (stream.Length > 1024 * 1024) throw new WorkspaceException("InputTooLarge", "Validation request exceeds 1 MiB.");
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            RequireUniqueFields(document.RootElement);
            if (!contracts.Validate("urn:splinter:v1:validation-product-request", document.RootElement).IsValid)
                throw new WorkspaceException("ValidationRequestInvalid", "Request does not match validation.execute schema.");
            var request = document.RootElement.Deserialize<ValidationProductRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true })!;
            var result = await new ValidationProduct(new WorkspaceStore(home(parse))).ExecuteAsync(request, ct);
            respond(Response(request, result, home(parse)));
        });
    }

    internal static ApiResponse Response(ValidationProductRequest request, object result, string home)
    {
        var observed = JsonSerializer.SerializeToElement(result, ApiJson.Options);
        bool accepted = request.Action == "refresh" || request.Action == "start" &&
            observed.GetProperty("application").GetProperty("running").GetBoolean() &&
            !observed.GetProperty("application").GetProperty("ready").GetBoolean();
        var response = ApiResponse.Success("validation.execute", new { request.Action, result });
        if (!accepted) return response;
        string viewId = observed.GetProperty("selection").GetProperty("intent").GetProperty("cache").GetProperty("viewId").GetString()!;
        var status = new { version = 1, action = "status", project = Path.GetFullPath(request.Project), request.Owner,
            requestKey = "status-" + WorkspaceStore.Key(request.RequestKey), viewId };
        // JSON-file commands return the next payload for the caller to save and
        // pass to --input; never overwrite the caller's original request file.
        return response with { Status = "Accepted", Terminal = false,
            NextActions = [new("validation.execute", new { home = Path.GetFullPath(home), request = status })] };
    }
    private static void RequireUniqueFields(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new WorkspaceException("ValidationRequestInvalid", "Duplicate request fields are ambiguous.");
                RequireUniqueFields(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RequireUniqueFields(item);
    }
}
