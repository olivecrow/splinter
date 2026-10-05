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
        var command = new Command("view", "Manage optional immutable source views and a shared Library cache.");
        var input = new Option<string>("--input") { Required = true };
        command.Options.Add(input); root.Subcommands.Add(command);
        command.SetAction(async parse =>
        {
            select("view.execute");
            using var stream = File.OpenRead(parse.GetValue(input)!);
            if (stream.Length > 1024 * 1024) throw new WorkspaceException("InputTooLarge", "Storage view request exceeds 1 MiB.");
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!JsonPropertyValidation.HasUniqueProperties(document.RootElement))
                throw new WorkspaceException("ValidationRequestInvalid", "Duplicate request fields are ambiguous.");
            if (!contracts.Validate("urn:splinter:v1:validation-product-request", document.RootElement).IsValid)
                throw new WorkspaceException("ValidationRequestInvalid", "Request does not match view.execute schema.");
            var request = document.RootElement.Deserialize<ValidationProductRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
                { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true })!;
            var result = await new ValidationProduct(new WorkspaceStore(home(parse))).ExecuteAsync(request, ct);
            respond(Response(request, result, home(parse)));
        });
    }

    internal static ApiResponse Response(ValidationProductRequest request, object result, string home)
    {
        var response = ApiResponse.Success("view.execute", new { request.Action, result });
        if (request.Action != "start") return response;
        var observed = JsonSerializer.SerializeToElement(result, ApiJson.Options);
        bool accepted =
            observed.GetProperty("application").GetProperty("running").GetBoolean() &&
            !observed.GetProperty("application").GetProperty("ready").GetBoolean();
        if (!accepted) return response;
        string viewId = observed.GetProperty("selection").GetProperty("intent").GetProperty("cache").GetProperty("viewId").GetString()!;
        var status = new { version = 1, action = "status", project = Path.GetFullPath(request.Project), request.Owner,
            requestKey = "status-" + WorkspaceStore.Key(request.RequestKey), viewId };
        // JSON-file commands return the next payload for the caller to save and
        // pass to --input; never overwrite the caller's original request file.
        return response with { Status = "Accepted", Terminal = false,
            NextActions = [new("view.execute", new { home = Path.GetFullPath(home), request = status })] };
    }
}
